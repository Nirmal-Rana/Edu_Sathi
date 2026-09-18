using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Models;
using System.Security.Claims;

namespace EduSathi.Controllers
{
    [Authorize]
    public class QuestionariesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public QuestionariesController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }
        // GET: /Questionaries/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound();

            // Fetch user's quiz history to calculate profile statistics
            var histories = await _context.QuizHistories
                .Where(h => h.UserId == userId)
                .ToListAsync();

            ViewBag.TotalQuizzes = histories.Count;
            ViewBag.AverageScore = histories.Any() ? histories.Average(h => (double)h.Score / h.TotalQuestions * 100).ToString("0.0") : "0";

            return View(user);
        }

        // GET: /Questionaries/Index (Hub with Join Room & Create Custom cards)
        public IActionResult Index()
        {
            return View();
        }

        // GET: /Questionaries/CreateCustom (Form for multi-PDF upload and category selection)
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
        public async Task<IActionResult> CreateCustom(List<IFormFile>? newPdfFiles, List<int>? selectedDocumentIds, string category, string roomName)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            // 1. Handle multi-file PDF uploads if provided
            if (newPdfFiles != null && newPdfFiles.Count > 0)
            {
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
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

                        var newDoc = new UploadedDocument
                        {
                            UserId = userId ?? "",
                            FileName = file.FileName,
                            FilePath = filePath,
                            Summary = "Automated AI summary generated from multi-file upload.",
                            UploadedAt = DateTime.UtcNow
                        };

                        // Add sample mock MCQ questions for demonstration
                        newDoc.Questions.Add(new Question { QuestionText = $"Basic question derived from {file.FileName}?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "A", Level = QuestionLevel.Basic, Explanation = "Basic level explanation." });
                        newDoc.Questions.Add(new Question { QuestionText = $"Medium question derived from {file.FileName}?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "B", Level = QuestionLevel.Medium, Explanation = "Medium level explanation." });

                        _context.UploadedDocuments.Add(newDoc);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // 2. Route based on category choice
            if (category == "Solo")
            {
                // Solo session redirects back to personal exam dashboard/practice hub
                return RedirectToAction("Index", "Exam");
            }
            else if (category == "Global")
            {
                // Generate a unique 6-character room code for peer competition
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

                // Redirect to the live room lobby
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

            // Fetch questions generated by the room creator's uploads
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

        // POST: /Questionaries/JoinRoom (Handles joining via room code input)
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

            // Check if user is already a participant in this room
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

        // POST: /Questionaries/SubmitGlobalQuiz (Handles live room question scoring & history logging)
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

            // Update participant scores and submission status
            participant.Score = score;
            participant.HasSubmitted = true;

            // Log attempt to personal QuizHistory
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

            // Sort participants by score descending for accurate leaderboard presentation
            room.Participants = room.Participants.OrderByDescending(p => p.Score).ToList();

            return View(room);
        }
    }
}