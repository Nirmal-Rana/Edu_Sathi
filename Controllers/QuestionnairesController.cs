using EduSathi.Data;
using EduSathi.Hubs;
using EduSathi.Models;
using EduSathi.Services;
using EduSathi.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace EduSathi.Controllers
{
    [Authorize]
    public class QuestionnairesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<RoomHub> _hub;
        private readonly McqService _mcqService;

        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public QuestionnairesController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            UserManager<ApplicationUser> userManager,
            IHubContext<RoomHub> hub,
            McqService mcqService)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
            _hub = hub;
            _mcqService = mcqService;
        }

        [AllowAnonymous]
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinRoom(string roomCode)
        {
            var code = (roomCode ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                TempData["JoinError"] = "Enter a room code.";
                return RedirectToAction(nameof(Index));
            }

            var room = await _context.QuizRooms.FirstOrDefaultAsync(r => r.Code == code);
            if (room == null)
            {
                TempData["JoinError"] = "That room code doesn't match a live room.";
                return RedirectToAction(nameof(Index));
            }

            await AddParticipantIfMissingAsync(room, isHost: false);
            return RedirectToAction(nameof(RoomLobby), new { roomCode = code });
        }

        public async Task<IActionResult> CreateCustom()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vm = new CreateRoomViewModel
            {
                PreviousDocuments = await _context.UploadedDocuments
                    .Where(d => d.UserId == userId)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCustom(CreateRoomViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var hasNewFiles = model.NewPdfFiles != null && model.NewPdfFiles.Any(f => f.Length > 0);
            var hasExisting = model.SelectedExistingDocumentIds != null && model.SelectedExistingDocumentIds.Any();

            if (!hasNewFiles && !hasExisting)
            {
                ModelState.AddModelError("", "Upload at least one PDF, or pick a saved one.");
                model.PreviousDocuments = await _context.UploadedDocuments
                    .Where(d => d.UserId == userId).OrderByDescending(d => d.UploadedAt).ToListAsync();
                return View(model);
            }

            var documentIds = new List<int>();

            if (hasExisting)
            {
                documentIds.AddRange(model.SelectedExistingDocumentIds!);
            }

            if (hasNewFiles)
            {
                foreach (var file in model.NewPdfFiles!.Where(f => f.Length > 0))
                {
                    var doc = await CreateSeededDocumentAsync(userId, file, model.QuestionCount);
                    documentIds.Add(doc.Id);
                }
            }

            var room = new QuizRoom
            {
                Name = string.IsNullOrWhiteSpace(model.Name) ? "Untitled Quiz Room" : model.Name.Trim(),
                CreatorUserId = userId,
                Category = model.Category,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var docId in documentIds.Distinct())
            {
                room.Documents.Add(new QuizRoomDocument { UploadedDocumentId = docId });
            }

            if (model.Category == RoomCategory.Global)
            {
                room.Code = await GenerateUniqueCodeAsync();
            }
            else
            {
                room.StartedAt = DateTime.UtcNow;
            }

            _context.QuizRooms.Add(room);
            await _context.SaveChangesAsync();

            await AddParticipantIfMissingAsync(room, isHost: true);

            if (model.Category == RoomCategory.Solo)
            {
                return RedirectToAction(nameof(RoomQuiz), new { id = room.Id });
            }

            return RedirectToAction(nameof(RoomLobby), new { roomCode = room.Code });
        }

        public async Task<IActionResult> RoomLobby(string roomCode)
        {
            var room = await LoadRoomByCodeAsync(roomCode);
            if (room == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account", new { area = "Identity" });

            var me = room.Participants.FirstOrDefault(p => p.UserId == userId);

            if (me == null && room.CreatorUserId == userId)
            {
                await AddParticipantIfMissingAsync(room, isHost: true);
                room = await LoadRoomByCodeAsync(roomCode);
                me = room?.Participants.FirstOrDefault(p => p.UserId == userId);
            }

            if (me == null) return Forbid();

            if (room.IsStarted)
            {
                return RedirectToAction(nameof(RoomQuiz), new { roomCode = room.Code });
            }

            var vm = new RoomLobbyViewModel
            {
                RoomId = room.Id,
                Code = room.Code,
                Name = room.Name,
                IsHost = me.IsHost || room.CreatorUserId == userId,
                IsStarted = room.IsStarted,
                Participants = room.Participants.OrderBy(p => p.JoinedAt).ToList()
            };

            if (vm.IsHost)
            {
                var participantUserIds = room.Participants.Select(p => p.UserId).ToHashSet();

                var friendships = await _context.Friendships
                    .Include(f => f.User)
                    .Include(f => f.Friend)
                    .Where(f => f.IsAccepted && (f.UserId == userId || f.FriendId == userId))
                    .ToListAsync();

                vm.AvailableFriends = friendships
                    .Select(f => f.UserId == userId ? f.Friend : f.User)
                    .Where(u => !participantUserIds.Contains(u.Id))
                    .Select(u => new FriendInviteOption
                    {
                        UserId = u.Id,
                        DisplayName = string.IsNullOrWhiteSpace(u.FullName) ? (u.Email ?? "Student") : u.FullName,
                        Email = u.Email ?? ""
                    })
                    .ToList();
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartRoom(string roomCode)
        {
            var room = await LoadRoomByCodeAsync(roomCode);
            if (room == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var me = room.Participants.FirstOrDefault(p => p.UserId == userId);
            if (me == null || (!me.IsHost && room.CreatorUserId != userId)) return Forbid();

            if (!room.IsStarted)
            {
                room.StartedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _hub.Clients.Group(room.Code).SendAsync("RoomStarted", new
                {
                    redirectUrl = Url.Action(nameof(RoomQuiz), "Questionnaires", new { roomCode = room.Code })
                });
            }

            return RedirectToAction(nameof(RoomQuiz), new { roomCode = room.Code });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteFriendToRoom(string roomCode, string friendUserId)
        {
            var room = await LoadRoomByCodeAsync(roomCode);
            if (room == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var me = room.Participants.FirstOrDefault(p => p.UserId == userId);
            if (me == null || (!me.IsHost && room.CreatorUserId != userId)) return Forbid();

            var alreadyIn = room.Participants.Any(p => p.UserId == friendUserId);
            if (!alreadyIn)
            {
                var friendUser = await _userManager.FindByIdAsync(friendUserId);
                if (friendUser != null)
                {
                    var displayName = string.IsNullOrWhiteSpace(friendUser.FullName) ? (friendUser.Email ?? "Student") : friendUser.FullName;

                    _context.QuizRoomParticipants.Add(new QuizRoomParticipant
                    {
                        QuizRoomId = room.Id,
                        UserId = friendUserId,
                        DisplayName = displayName,
                        IsHost = false,
                        JoinedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(room.Code))
                    {
                        await _hub.Clients.Group(room.Code).SendAsync("ParticipantJoined", new { displayName });
                    }
                }
            }

            return RedirectToAction(nameof(RoomLobby), new { roomCode = room.Code });
        }

        public async Task<IActionResult> RoomQuiz(string? roomCode, int? id)
        {
            var room = await LoadRoomForQuizAsync(roomCode, id);

            if (room == null && id.HasValue)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var doc = await _context.UploadedDocuments
                    .Include(d => d.Questions)
                    .FirstOrDefaultAsync(d => d.Id == id.Value && d.UserId == userId);

                if (doc != null)
                {
                    room = new QuizRoom
                    {
                        Name = doc.FileName,
                        CreatorUserId = userId,
                        Category = RoomCategory.Solo,
                        CreatedAt = DateTime.UtcNow,
                        StartedAt = DateTime.UtcNow
                    };
                    room.Documents.Add(new QuizRoomDocument { UploadedDocumentId = doc.Id });

                    _context.QuizRooms.Add(room);
                    await _context.SaveChangesAsync();

                    await AddParticipantIfMissingAsync(room, isHost: true);
                }
            }

            if (room == null) return NotFound();

            var documentIds = room.Documents.Select(d => d.UploadedDocumentId).ToList();
            bool hasQuestions = await _context.Questions.AnyAsync(q => documentIds.Contains(q.UploadedDocumentId));

            if (!hasQuestions && documentIds.Any())
            {
                foreach (var docId in documentIds)
                {
                    var doc = await _context.UploadedDocuments.FindAsync(docId);
                    if (doc != null && !string.IsNullOrEmpty(doc.ExtractedText))
                    {
                        int targetCount = 10;
                        int baseCount = targetCount / 3;
                        int remainder = targetCount % 3;

                        var levels = new[] { QuestionLevel.Basic, QuestionLevel.Medium, QuestionLevel.Hard };
                        for (int i = 0; i < levels.Length; i++)
                        {
                            var level = levels[i];
                            int currentLevelCount = baseCount + (i < remainder ? 1 : 0);
                            if (currentLevelCount <= 0) continue;

                            try
                            {
                                var jsonResponse = await _mcqService.GenerateMcqsAsync(doc.ExtractedText, (int)level, currentLevelCount);
                                if (!string.IsNullOrEmpty(jsonResponse))
                                {
                                    string cleaned = jsonResponse.Trim();
                                    var fenceMatch = Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
                                    if (fenceMatch.Success)
                                    {
                                        cleaned = fenceMatch.Groups[1].Value.Trim();
                                    }

                                    var generatedQuestions = JsonSerializer.Deserialize<List<Question>>(cleaned, new JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    });

                                    if (generatedQuestions != null)
                                    {
                                        foreach (var q in generatedQuestions)
                                        {
                                            _context.Questions.Add(new Question
                                            {
                                                UploadedDocumentId = doc.Id,
                                                QuestionText = q.QuestionText,
                                                OptionA = q.OptionA,
                                                OptionB = q.OptionB,
                                                OptionC = q.OptionC,
                                                OptionD = q.OptionD,
                                                CorrectOption = q.CorrectOption,
                                                Level = level,
                                                Explanation = q.Explanation
                                            });
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error auto-generating questions: {ex.Message}");
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                }
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var me = room.Participants.FirstOrDefault(p => p.UserId == currentUserId);
            if (me == null)
            {
                await AddParticipantIfMissingAsync(room, isHost: room.CreatorUserId == currentUserId);
                me = room.Participants.FirstOrDefault(p => p.UserId == currentUserId);
                if (me == null) return Forbid();
            }

            if (room.Category == RoomCategory.Global && !room.IsStarted)
            {
                return RedirectToAction(nameof(RoomLobby), new { roomCode = room.Code });
            }

            var vm = BuildQuizViewModel(room, me);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("RoomQuiz")]
        public async Task<IActionResult> RoomQuizSubmit(string? roomCode, int? id, Dictionary<int, string>? answers)
        {
            var room = await LoadRoomForQuizAsync(roomCode, id);
            if (room == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var me = room.Participants.FirstOrDefault(p => p.UserId == userId);
            if (me == null) return Forbid();

            var vm = BuildQuizViewModel(room, me);
            vm.Answers = answers ?? new Dictionary<int, string>();
            vm.IsSubmitted = true;
            vm.CorrectCount = vm.Questions.Count(q =>
                vm.Answers.TryGetValue(q.Id, out var picked) &&
                string.Equals(picked, q.CorrectOption, StringComparison.OrdinalIgnoreCase));

            if (!me.CompletedAt.HasValue)
            {
                me.Score = vm.CorrectCount;
                me.TotalQuestions = vm.Total;
                me.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            vm.Leaderboard = BuildLeaderboard(room);

            if (room.Category == RoomCategory.Global)
            {
                await _hub.Clients.Group(room.Code).SendAsync("LeaderboardUpdated", new
                {
                    entries = vm.Leaderboard.Select(e => new { e.DisplayName, e.IsHost, e.Score, e.Total })
                });
            }

            return View("RoomQuiz", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetAndPracticeAgain(int roomId, string? roomCode)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var participant = await _context.QuizRoomParticipants
                .FirstOrDefaultAsync(p => p.QuizRoomId == roomId && p.UserId == userId);

            if (participant != null)
            {
                participant.Score = null;
                participant.TotalQuestions = null;
                participant.CompletedAt = null;
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrEmpty(roomCode))
            {
                return RedirectToAction(nameof(RoomQuiz), new { roomCode = roomCode });
            }

            return RedirectToAction(nameof(RoomQuiz), new { id = roomId });
        }

        public async Task<IActionResult> MyQuizzes()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var rooms = await _context.QuizRooms
                .Include(r => r.Participants)
                .Where(r => r.CreatorUserId == userId || r.Participants.Any(p => p.UserId == userId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var rows = rooms.Select(r =>
            {
                var mine = r.Participants.FirstOrDefault(p => p.UserId == userId);
                return new MyQuizRow
                {
                    RoomId = r.Id,
                    Code = r.Code,
                    Name = r.Name,
                    Category = r.Category,
                    CreatedAt = r.CreatedAt,
                    IsStarted = r.IsStarted,
                    IsHost = r.CreatorUserId == userId,
                    ParticipantCount = r.Participants.Count,
                    MyScore = mine?.Score,
                    MyTotal = mine?.TotalQuestions
                };
            }).ToList();

            return View(rows);
        }

        private async Task AddParticipantIfMissingAsync(QuizRoom room, bool isHost)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var already = await _context.QuizRoomParticipants
                .AnyAsync(p => p.QuizRoomId == room.Id && p.UserId == userId);
            if (already) return;

            var user = await _userManager.GetUserAsync(User);
            var displayName = !string.IsNullOrWhiteSpace(user?.FullName) ? user!.FullName : (user?.Email ?? "Student");

            _context.QuizRoomParticipants.Add(new QuizRoomParticipant
            {
                QuizRoomId = room.Id,
                UserId = userId,
                DisplayName = displayName,
                IsHost = isHost,
                JoinedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            if (!isHost && !string.IsNullOrEmpty(room.Code))
            {
                await _hub.Clients.Group(room.Code).SendAsync("ParticipantJoined", new { displayName });
            }
        }

        private async Task<QuizRoom?> LoadRoomByCodeAsync(string? roomCode)
        {
            var code = (roomCode ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return null;

            return await _context.QuizRooms
                .Include(r => r.Participants)
                .Include(r => r.Documents)
                .FirstOrDefaultAsync(r => r.Code == code);
        }

        private async Task<QuizRoom?> LoadRoomForQuizAsync(string? roomCode, int? id)
        {
            if (!string.IsNullOrWhiteSpace(roomCode))
            {
                return await LoadRoomByCodeAsync(roomCode);
            }

            if (id.HasValue)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var room = await _context.QuizRooms
                    .Include(r => r.Participants)
                    .Include(r => r.Documents)
                    .FirstOrDefaultAsync(r => r.Id == id.Value && r.CreatorUserId == userId);

                if (room == null)
                {
                    room = await _context.QuizRooms
                        .Include(r => r.Participants)
                        .Include(r => r.Documents)
                        .FirstOrDefaultAsync(r => r.CreatorUserId == userId && r.Documents.Any(d => d.UploadedDocumentId == id.Value));
                }

                return room;
            }

            return null;
        }

        private RoomQuizViewModel BuildQuizViewModel(QuizRoom room, QuizRoomParticipant me)
        {
            var documentIds = room.Documents.Select(d => d.UploadedDocumentId).ToList();

            var questions = _context.Questions
                .Where(q => documentIds.Contains(q.UploadedDocumentId))
                .AsEnumerable()
                .OrderBy(q => Guid.NewGuid())
                .ToList();

            var vm = new RoomQuizViewModel
            {
                RoomId = room.Id,
                RoomCode = room.Code,
                Name = room.Name,
                Category = room.Category,
                Questions = questions
            };

            if (me.CompletedAt.HasValue)
            {
                vm.IsSubmitted = true;
                vm.CorrectCount = me.Score ?? 0;
                vm.Leaderboard = BuildLeaderboard(room);
            }

            return vm;
        }

        private static List<LeaderboardEntry> BuildLeaderboard(QuizRoom room)
        {
            return room.Participants
                .OrderByDescending(p => p.Score ?? -1)
                .ThenBy(p => p.CompletedAt ?? DateTime.MaxValue)
                .Select(p => new LeaderboardEntry
                {
                    DisplayName = p.DisplayName,
                    IsHost = p.IsHost,
                    Score = p.Score,
                    Total = p.TotalQuestions
                })
                .ToList();
        }

        private async Task<string> GenerateUniqueCodeAsync()
        {
            var rng = Random.Shared;
            for (var attempt = 0; attempt < 25; attempt++)
            {
                var chars = new char[6];
                for (var i = 0; i < chars.Length; i++)
                {
                    chars[i] = CodeAlphabet[rng.Next(CodeAlphabet.Length)];
                }
                var code = new string(chars);
                var taken = await _context.QuizRooms.AnyAsync(r => r.Code == code);
                if (!taken) return code;
            }
            return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        }

        private async Task<UploadedDocument> CreateSeededDocumentAsync(string userId, Microsoft.AspNetCore.Http.IFormFile file, int questionCount)
        {
            string uploadsFolder = Path.Combine(Path.GetTempPath(), "EduSathiUploads");
            Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            string extractedText = "";
            try
            {
                using (var pdf = PdfDocument.Open(filePath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        extractedText += page.Text + " ";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse PDF: {ex.Message}");
                extractedText = "Error reading PDF file structure.";
            }

            if (extractedText.Length > 30000)
            {
                extractedText = extractedText.Substring(0, 30000);
            }

            var doc = new UploadedDocument
            {
                UserId = userId,
                FileName = file.FileName,
                FilePath = filePath,
                ExtractedText = extractedText,
                Summary = string.Empty,
                UploadedAt = DateTime.UtcNow
            };

            _context.UploadedDocuments.Add(doc);
            await _context.SaveChangesAsync();

            int baseCount = questionCount / 3;
            int remainder = questionCount % 3;

            var levels = new[] { QuestionLevel.Basic, QuestionLevel.Medium, QuestionLevel.Hard };
            for (int i = 0; i < levels.Length; i++)
            {
                var level = levels[i];
                int currentLevelCount = baseCount + (i < remainder ? 1 : 0);
                if (currentLevelCount <= 0) continue;

                try
                {
                    var jsonResponse = await _mcqService.GenerateMcqsAsync(extractedText, (int)level, currentLevelCount);

                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        string cleaned = jsonResponse.Trim();

                        var fenceMatch = Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)```", RegexOptions.IgnoreCase);
                        if (fenceMatch.Success)
                        {
                            cleaned = fenceMatch.Groups[1].Value.Trim();
                        }

                        var generatedQuestions = JsonSerializer.Deserialize<List<Question>>(cleaned, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (generatedQuestions != null)
                        {
                            foreach (var q in generatedQuestions)
                            {
                                doc.Questions.Add(new Question
                                {
                                    UploadedDocumentId = doc.Id,
                                    QuestionText = q.QuestionText,
                                    OptionA = q.OptionA,
                                    OptionB = q.OptionB,
                                    OptionC = q.OptionC,
                                    OptionD = q.OptionD,
                                    CorrectOption = q.CorrectOption,
                                    Level = level,
                                    Explanation = q.Explanation
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing questions for {level}: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            return doc;
        }

        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToAction("Login", "Account", new { area = "Identity" });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var histories = await _context.QuizHistories.Where(h => h.UserId == userId).ToListAsync();

            ViewBag.TotalQuizzes = histories.Count;
            ViewBag.AverageScore = histories.Any() ? histories.Average(h => (double)h.Score / h.TotalQuestions * 100).ToString("0.0") : "0";

            return View(user);
        }

        public async Task<IActionResult> RoomInvites()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var myRooms = await _context.QuizRooms
                .Include(r => r.Participants)
                .Where(r => r.Participants.Any(p => p.UserId == userId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(myRooms);
        }

        [HttpGet]
        public async Task<IActionResult> Flashcards(int id)
        {
            var document = await _context.UploadedDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == _userManager.GetUserId(User));

            if (document == null) return NotFound();

            return View(document);
        }
    }
}