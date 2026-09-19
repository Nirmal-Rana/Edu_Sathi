using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.Services;
using System.Security.Claims;
using UglyToad.PdfPig;

namespace EduSathi.Controllers
{
    [Authorize]
    public class QuestionariesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly GeminiService _geminiService;

        public QuestionariesController(ApplicationDbContext context, IWebHostEnvironment env, GeminiService geminiService)
        {
            _context = context;
            _env = env;
            _geminiService = geminiService;
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

        // GET: /Questionaries/Index
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Questionaries/CreateCustom
        public async Task<IActionResult> CreateCustom()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userDocs = await _context.UploadedDocuments
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return View(userDocs);
        }

        // POST: /Questionaries/CreateCustom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustom(List<IFormFile>? newPdfFiles, List<int>? selectedDocumentIds, int questionCount, string category, string roomName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            string combinedExtractedText = "";
            UploadedDocument? primaryDoc = null;

            // 1. Handle newly uploaded files
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

                        // Extract text using PdfPig
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
                            Summary = await _geminiService.GenerateSummaryAsync(fileText),
                            UploadedAt = DateTime.UtcNow
                        };

                        _context.UploadedDocuments.Add(newDoc);
                        primaryDoc = newDoc; // Track for question mapping if needed
                    }
                }
                await _context.SaveChangesAsync();
            }

            // 2. Include text from previously selected files if any
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

            // Safety check for text limits
            if (combinedExtractedText.Length > 25000)
            {
                combinedExtractedText = combinedExtractedText.Substring(0, 25000);
            }

            // Fallback if no text found
            if (string.IsNullOrWhiteSpace(combinedExtractedText))
            {
                combinedExtractedText = "General academic practice material.";
            }

            // 3. Generate dynamic MCQs using Gemini service based on requested question count
            if (primaryDoc != null)
            {
                string aiResponse = await _geminiService.GenerateMcqsAsync(combinedExtractedText, questionCount);

                // Add a sample parsed question entity linked to the primary document so exams/quizzes can load it
                primaryDoc.Questions.Add(new Question
                {
                    QuestionText = $"AI Generated Review Question based on uploaded files (Target count: {questionCount})",
                    OptionA = "Review Content A",
                    OptionB = "Review Content B",
                    OptionC = "Review Content C",
                    OptionD = "Review Content D",
                    CorrectOption = "A",
                    Level = QuestionLevel.Medium,
                    Explanation = aiResponse
                });

                await _context.SaveChangesAsync();
            }

            // 4. Route based on category choice (using case-insensitive comparison)
            if (string.Equals(category, "Solo", StringComparison.OrdinalIgnoreCase))
            {
                int docId = primaryDoc != null ? primaryDoc.Id : 0;
                return RedirectToAction("QuizSession", "Quiz", new { id = docId });
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

                _context.CustomRooms.Add(room);
                await _context.SaveChangesAsync();

                return RedirectToAction("RoomLobby", new { roomCode = room.RoomCode });
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Questionaries/RoomLobby/{roomCode}
        public async Task<IActionResult> RoomLobby(string roomCode)
        {
            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode);

            if (room == null) return NotFound();

            return View(room);
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