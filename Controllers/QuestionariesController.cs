using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.Services;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace EduSathi.Controllers
{
    [Authorize]
    public class QuestionariesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly SummaryService _summaryService;
        private readonly McqService _mcqService;

        public QuestionariesController(ApplicationDbContext context, IWebHostEnvironment env, SummaryService summaryService, McqService mcqService)
        {
            _context = context;
            _env = env;
            _summaryService = summaryService;
            _mcqService = mcqService;
        }

        // GET: /Questionaries/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound();

            var histories = await _context.QuizHistories
                .Where(h => h.UserId == userId)
                .ToListAsync();

            ViewBag.TotalQuizzes = histories.Count;
            ViewBag.AverageScore = histories.Any() ? histories.Average(h => (double)h.Score / h.TotalQuestions * 100).ToString("0.0") : "0";

            return View(user);
        }

        // GET: /Questionaries/Index (Questionaries Hub / Document Management)
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userDocs = await _context.UploadedDocuments
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return View(userDocs);
        }

        // GET: /Questionaries/RoomInvites (Dedicated page for invited/active live rooms)
        public async Task<IActionResult> RoomInvites()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var myRooms = await _context.CustomRooms
                .Include(r => r.Participants)
                .Where(r => r.IsActive && r.Participants.Any(p => p.UserId == userId && !p.HasSubmitted))
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return View(myRooms);
        }

        // GET: /Questionaries/CreateCustom
        public async Task<IActionResult> CreateCustom()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userDocs = await _context.UploadedDocuments
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var friends = await _context.Friendships
                .Where(f => f.UserId == userId && f.IsAccepted)
                .Include(f => f.Friend)
                .Select(f => f.Friend)
                .ToListAsync();

            ViewBag.Friends = friends;

            return View(userDocs);
        }

        // POST: /Questionaries/CreateCustom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustom(List<IFormFile>? newPdfFiles, List<int>? selectedDocumentIds, int questionCount, string category, string roomName, List<string>? invitedFriendIds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            string combinedExtractedText = "";
            UploadedDocument? primaryDoc = null;

            if (newPdfFiles != null && newPdfFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.GetTempPath(), "uploads");
                Directory.CreateDirectory(uploadsFolder);

                foreach (var file in newPdfFiles)
                {
                    if (file.Length > 0)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fileStream);
                        }

                        string fileText = "";
                        using (var pdf = PdfDocument.Open(filePath))
                        {
                            foreach (var page in pdf.GetPages())
                            {
                                fileText += page.Text + " ";
                            }
                        }

                        combinedExtractedText += fileText + "\n";

                        var newDoc = new UploadedDocument
                        {
                            UserId = userId ?? "",
                            FileName = file.FileName,
                            FilePath = filePath,
                            ExtractedText = fileText,
                            Summary = await _summaryService.GenerateSummaryAsync(fileText),
                            UploadedAt = DateTime.UtcNow
                        };

                        _context.UploadedDocuments.Add(newDoc);
                        primaryDoc = newDoc;
                    }
                }
                await _context.SaveChangesAsync();
            }

            if (selectedDocumentIds != null && selectedDocumentIds.Count > 0)
            {
                var selectedDocs = await _context.UploadedDocuments
                    .Where(d => selectedDocumentIds.Contains(d.Id) && d.UserId == userId)
                    .ToListAsync();

                foreach (var doc in selectedDocs)
                {
                    combinedExtractedText += doc.ExtractedText + "\n";
                    if (primaryDoc == null) primaryDoc = doc;
                }
            }

            if (combinedExtractedText.Length > 25000)
            {
                combinedExtractedText = combinedExtractedText.Substring(0, 25000);
            }

            if (string.IsNullOrWhiteSpace(combinedExtractedText))
            {
                combinedExtractedText = "General academic practice material.";
            }

            if (primaryDoc != null)
            {
                string aiResponse = await _mcqService.GenerateMcqsAsync(combinedExtractedText, questionCount);

                var parsedQuestions = ParseMcqJson(aiResponse);

                if (parsedQuestions.Count > 0)
                {
                    foreach (var q in parsedQuestions)
                    {
                        primaryDoc.Questions.Add(q);
                    }
                }
                else
                {
                    // Fallback: keep the raw AI reply visible instead of silently failing,
                    // so a bad/unparseable response is obvious rather than showing fake options.
                    primaryDoc.Questions.Add(new Question
                    {
                        QuestionText = "Could not parse AI-generated questions. Raw AI response is shown in the explanation below.",
                        OptionA = "N/A",
                        OptionB = "N/A",
                        OptionC = "N/A",
                        OptionD = "N/A",
                        CorrectOption = "A",
                        Level = QuestionLevel.Medium,
                        Explanation = aiResponse
                    });
                }

                await _context.SaveChangesAsync();
            }

            if (string.Equals(category, "Solo", StringComparison.OrdinalIgnoreCase))
            {
                int docId = primaryDoc != null ? primaryDoc.Id : 0;
                return RedirectToAction("Session", "Quiz", new { id = docId });
            }
            else if (string.Equals(category, "Global", StringComparison.OrdinalIgnoreCase))
            {
                string roomCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();

                var room = new CustomRoom
                {
                    RoomCode = roomCode,
                    CreatorId = userId ?? "",
                    RoomName = string.IsNullOrEmpty(roomName) ? "EduSathi Live Room" : roomName
                };

                room.Participants.Add(new RoomParticipant
                {
                    UserId = userId ?? "",
                    UserName = user?.UserName ?? "Host"
                });

                if (invitedFriendIds != null && invitedFriendIds.Any())
                {
                    foreach (var friendId in invitedFriendIds)
                    {
                        var friendUser = await _context.Users.FindAsync(friendId);
                        if (friendUser != null)
                        {
                            room.Participants.Add(new RoomParticipant
                            {
                                UserId = friendUser.Id,
                                UserName = friendUser.UserName ?? "Participant"
                            });
                        }
                    }
                }

                _context.CustomRooms.Add(room);
                await _context.SaveChangesAsync();

                return RedirectToAction("RoomLobby", new { roomCode = room.RoomCode });
            }

            return RedirectToAction(nameof(Index));
        }

        // Parses the JSON array returned by McqService.GenerateMcqsAsync into Question entities.
        // Gemini sometimes wraps its JSON in ```json ... ``` fences even when told not to, so those
        // are stripped first. Any entry that fails to parse cleanly is skipped rather than crashing
        // the whole batch.
        private List<Question> ParseMcqJson(string aiResponse)
        {
            var result = new List<Question>();

            if (string.IsNullOrWhiteSpace(aiResponse))
                return result;

            string cleaned = aiResponse.Trim();

            // Strip markdown code fences (```json ... ``` or ``` ... ```) if present
            var fenceMatch = Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
            if (fenceMatch.Success)
            {
                cleaned = fenceMatch.Groups[1].Value.Trim();
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(cleaned);

                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return result;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    string GetStr(string propName) =>
                        item.TryGetProperty(propName, out var val) ? (val.GetString() ?? "") : "";

                    string correct = GetStr("correctOption").Trim().ToUpperInvariant();
                    if (correct != "A" && correct != "B" && correct != "C" && correct != "D")
                        correct = "A";

                    var question = new Question
                    {
                        QuestionText = GetStr("question"),
                        OptionA = GetStr("optionA"),
                        OptionB = GetStr("optionB"),
                        OptionC = GetStr("optionC"),
                        OptionD = GetStr("optionD"),
                        CorrectOption = correct,
                        Explanation = GetStr("explanation"),
                        Level = QuestionLevel.Medium
                    };

                    // Skip malformed entries (e.g. missing question text) instead of saving junk rows
                    if (!string.IsNullOrWhiteSpace(question.QuestionText))
                    {
                        result.Add(question);
                    }
                }
            }
            catch (JsonException)
            {
                // Not valid JSON (e.g. Gemini returned an error string or prose) — caller falls back
                return new List<Question>();
            }

            return result;
        }

        // GET: /Questionaries/RoomLobby/{roomCode}
        public async Task<IActionResult> RoomLobby(string roomCode)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room == null) return NotFound();

            var friends = await _context.Friendships
                .Include(f => f.User)
                .Include(f => f.Friend)
                .Where(f => f.IsAccepted && (f.UserId == userId || f.FriendId == userId))
                .Select(f => f.UserId == userId ? f.Friend : f.User)
                .ToListAsync();

            ViewBag.Friends = friends;

            return View(room);
        }

        // GET: /Questionaries/CheckQuizStatus?roomCode={roomCode}
        [HttpGet]
        public async Task<IActionResult> CheckQuizStatus(string roomCode)
        {
            var room = await _context.CustomRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
            if (room == null) return NotFound();
            return Json(new { isStarted = room.IsQuizStarted });
        }

        // POST: /Questionaries/TriggerStartQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TriggerStartQuiz(string roomCode)
        {
            var room = await _context.CustomRooms.FirstOrDefaultAsync(r => r.RoomCode == roomCode);
            if (room != null)
            {
                room.IsQuizStarted = true;
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("LiveQuiz", new { roomCode });
        }

        // POST: /Questionaries/InviteFriendToRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteFriendToRoom(string roomCode, string friendId)
        {
            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room != null)
            {
                bool alreadyJoined = room.Participants.Any(p => p.UserId == friendId);
                if (!alreadyJoined)
                {
                    var friendUser = await _context.Users.FindAsync(friendId);
                    room.Participants.Add(new RoomParticipant
                    {
                        CustomRoomId = room.Id,
                        UserId = friendId,
                        UserName = friendUser?.UserName ?? "Participant"
                    });
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "Friend added to the room successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "This friend is already in the room.";
                }
            }

            return RedirectToAction("RoomLobby", new { roomCode });
        }

        // GET: /Questionaries/LiveQuiz/{roomCode}
        public async Task<IActionResult> LiveQuiz(string roomCode)
        {
            var room = await _context.CustomRooms
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room == null) return NotFound();

            var questions = await _context.Questions
                .Where(q => q.UploadedDocument.UserId == room.CreatorId)
                .ToListAsync();

            ViewBag.RoomId = room.Id;
            ViewBag.RoomCode = room.RoomCode;

            return View(questions);
        }

        // GET: /Questionaries/History
        public async Task<IActionResult> History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var histories = await _context.QuizHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CompletedAt)
                .ToListAsync();

            return View(histories);
        }

        // POST: /Questionaries/JoinRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinRoom(string roomCode)
        {
            if (string.IsNullOrEmpty(roomCode))
            {
                ModelState.AddModelError("", "Please enter a valid room code.");
                return RedirectToAction(nameof(Index));
            }

            var cleanCode = roomCode.Trim().ToUpper();
            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.RoomCode == cleanCode && r.IsActive);

            if (room == null)
            {
                TempData["Error"] = "Room not found or inactive.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            bool alreadyJoined = room.Participants.Any(p => p.UserId == userId);
            if (!alreadyJoined)
            {
                room.Participants.Add(new RoomParticipant
                {
                    UserId = userId ?? "",
                    UserName = user?.UserName ?? "Participant"
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("RoomLobby", new { roomCode = room.RoomCode });
        }

        // POST: /Questionaries/SubmitGlobalQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitGlobalQuiz(int roomId, Dictionary<int, string> userAnswers)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var participant = await _context.RoomParticipants
                .FirstOrDefaultAsync(p => p.CustomRoomId == roomId && p.UserId == userId);

            if (participant == null) return NotFound();

            int score = 0;
            int totalQuestions = userAnswers != null ? userAnswers.Count : 0;

            if (userAnswers != null)
            {
                foreach (var kvp in userAnswers)
                {
                    int questionId = kvp.Key;
                    string selectedOption = kvp.Value;

                    var question = await _context.Questions.FindAsync(questionId);
                    if (question != null && question.CorrectOption.Equals(selectedOption, StringComparison.OrdinalIgnoreCase))
                    {
                        score++;
                    }
                }
            }

            participant.Score = score;
            participant.HasSubmitted = true;

            var room = await _context.CustomRooms.FindAsync(roomId);
            _context.QuizHistories.Add(new QuizHistory
            {
                UserId = userId ?? "",
                QuizTitle = room?.RoomName ?? "Global Custom Room",
                Category = "Global",
                Score = score,
                TotalQuestions = totalQuestions > 0 ? totalQuestions : 1
            });

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(RoomLeaderboard), new { roomId = roomId });
        }

        // GET: /Questionaries/RoomLeaderboard/{roomId}
        public async Task<IActionResult> RoomLeaderboard(int roomId)
        {
            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.Id == roomId);

            if (room == null) return NotFound();

            room.Participants = room.Participants.OrderByDescending(p => p.Score).ToList();

            return View(room);
        }
    }
}