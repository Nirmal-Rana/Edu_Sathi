using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Hubs;
using EduSathi.Models;
using EduSathi.ViewModels;

namespace EduSathi.Controllers
{
    // ============================================================================
    // Questionnaires: the "Solo vs Global live room" flow. Separate from
    // ExamController (which is the single-document upload -> summary -> practice
    // flow already on Home/History) so neither controller has to know about the
    // other's concerns.
    //
    //   Index        GET  /Questionnaires            Hub: join a room or create one
    //   JoinRoom      POST /Questionnaires/JoinRoom    Join a Global room by code
    //   CreateCustom GET  /Questionnaires/CreateCustom Upload/select PDFs, pick Solo/Global
    //   CreateCustom POST /Questionnaires/CreateCustom Creates the room
    //   RoomLobby    GET  /Questionnaires/RoomLobby     Code + live participant list (Global)
    //   StartRoom    POST /Questionnaires/StartRoom     Host only - starts the room for everyone
    //   RoomQuiz     GET  /Questionnaires/RoomQuiz      The combined-question quiz for a room
    //   RoomQuiz     POST /Questionnaires/RoomQuiz      Grades + records this participant's score
    //   MyQuizzes    GET  /Questionnaires/MyQuizzes     Rooms you created or joined
    // ============================================================================
    [Authorize]
    public class QuestionnairesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<RoomHub> _hub;

        private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I - easy to misread on screen

        public QuestionnairesController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            UserManager<ApplicationUser> userManager,
            IHubContext<RoomHub> hub)
        {
            _context = context;
            _env = env;
            _userManager = userManager;
            _hub = hub;
        }

        // GET: /Questionnaires
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Questionnaires/JoinRoom
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

            var room = await _context.QuizRooms
                .FirstOrDefaultAsync(r => r.Code == code);

            if (room == null)
            {
                TempData["JoinError"] = "That room code doesn't match a live room.";
                return RedirectToAction(nameof(Index));
            }

            await AddParticipantIfMissingAsync(room, isHost: false);

            return RedirectToAction(nameof(RoomLobby), new { roomCode = code });
        }

        // GET: /Questionnaires/CreateCustom
        public async Task<IActionResult> CreateCustom()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vm = new CreateRoomViewModel
            {
                PreviousDocuments = await _context.UploadedDocuments
                    .Where(d => d.UserId == userId)
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync(),
                Friends = await GetFriendsAsync(userId!)
            };
            return View(vm);
        }

        // POST: /Questionnaires/CreateCustom
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
                model.Friends = await GetFriendsAsync(userId);
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
                    var doc = await CreateSeededDocumentAsync(userId, file);
                    documentIds.Add(doc.Id);
                }
            }

            var room = new QuizRoom
            {
                Name = string.IsNullOrWhiteSpace(model.Name) ? "Untitled Quiz Room" : model.Name.Trim(),
                CreatorUserId = userId,
                Category = model.Category,
                QuestionCount = Math.Clamp(model.QuestionCount <= 0 ? 5 : model.QuestionCount, 1, 30),
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
                // Solo rooms start themselves - there's no one to wait in a lobby for.
                room.StartedAt = DateTime.UtcNow;
            }

            _context.QuizRooms.Add(room);
            await _context.SaveChangesAsync();

            await AddParticipantIfMissingAsync(room, isHost: true);

            if (model.Category == RoomCategory.Global && model.InviteFriendUserIds != null && model.InviteFriendUserIds.Any())
            {
                var friendIds = await GetFriendsAsync(userId);
                var validIds = friendIds.Select(f => f.UserId).Intersect(model.InviteFriendUserIds).ToList();
                foreach (var friendId in validIds)
                {
                    await AddParticipantForUserAsync(room, friendId, isHost: false);
                }
            }

            if (model.Category == RoomCategory.Solo)
            {
                // Solo rooms have no code - looked up by id instead (see LoadRoomForQuizAsync).
                return RedirectToAction(nameof(RoomQuiz), new { id = room.Id });
            }

            return RedirectToAction(nameof(RoomLobby), new { roomCode = room.Code });
        }

        // GET: /Questionnaires/RoomLobby?roomCode=XXXXXX
        public async Task<IActionResult> RoomLobby(string roomCode)
        {
            var room = await LoadRoomByCodeAsync(roomCode);
            if (room == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var me = room.Participants.FirstOrDefault(p => p.UserId == userId);
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
                IsHost = me.IsHost,
                IsStarted = room.IsStarted,
                Participants = room.Participants.OrderBy(p => p.JoinedAt).ToList()
            };

            return View(vm);
        }

        // POST: /Questionnaires/StartRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartRoom(string roomCode)
        {
            var room = await LoadRoomByCodeAsync(roomCode);
            if (room == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var me = room.Participants.FirstOrDefault(p => p.UserId == userId);
            if (me == null || !me.IsHost) return Forbid();

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

        // GET: /Questionnaires/RoomQuiz?roomCode=XXXXXX   (Global rooms)
        // GET: /Questionnaires/RoomQuiz?id=5               (Solo rooms - no code)
        public async Task<IActionResult> RoomQuiz(string? roomCode, int? id)
        {
            var room = await LoadRoomForQuizAsync(roomCode, id);
            if (room == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var me = room.Participants.FirstOrDefault(p => p.UserId == userId);
            if (me == null) return Forbid();

            if (room.Category == RoomCategory.Global && !room.IsStarted)
            {
                return RedirectToAction(nameof(RoomLobby), new { roomCode = room.Code });
            }

            var vm = BuildQuizViewModel(room, me);
            return View(vm);
        }

        // POST: /Questionnaires/RoomQuiz
        // `answers` binds from inputs named answers[<questionId>] with values "A".."D",
        // same convention ExamController.Quiz uses.
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

        // GET: /Questionnaires/MyQuizzes
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

        // ====================== helpers ======================

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

        // Same as AddParticipantIfMissingAsync, but for adding a *different* user
        // (a friend the host is inviting directly) rather than the signed-in caller.
        private async Task AddParticipantForUserAsync(QuizRoom room, string targetUserId, bool isHost)
        {
            var already = await _context.QuizRoomParticipants
                .AnyAsync(p => p.QuizRoomId == room.Id && p.UserId == targetUserId);
            if (already) return;

            var user = await _userManager.FindByIdAsync(targetUserId);
            if (user == null) return;

            var displayName = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName : (user.Email ?? "Student");

            _context.QuizRoomParticipants.Add(new QuizRoomParticipant
            {
                QuizRoomId = room.Id,
                UserId = targetUserId,
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

        // Accepted friends of the given user, as FriendSummary rows - shared by
        // CreateCustom (GET/POST) for the "Invite Friends Directly" list.
        private async Task<List<FriendSummary>> GetFriendsAsync(string userId)
        {
            var all = await _context.FriendRequests
                .Include(f => f.Requester)
                .Include(f => f.Addressee)
                .Where(f => f.Status == FriendRequestStatus.Accepted &&
                            (f.RequesterUserId == userId || f.AddresseeUserId == userId))
                .ToListAsync();

            return all.Select(f =>
            {
                var other = f.RequesterUserId == userId ? f.Addressee : f.Requester;
                return new FriendSummary
                {
                    UserId = other.Id,
                    DisplayName = string.IsNullOrWhiteSpace(other.FullName) ? (other.Email ?? "Student") : other.FullName,
                    Email = other.Email ?? ""
                };
            })
                .DistinctBy(f => f.UserId)
                .ToList();
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

        // Global rooms are looked up by their shareable code; Solo rooms have no
        // code, so they're looked up by id and scoped to the signed-in creator.
        private async Task<QuizRoom?> LoadRoomForQuizAsync(string? roomCode, int? id)
        {
            if (!string.IsNullOrWhiteSpace(roomCode))
            {
                return await LoadRoomByCodeAsync(roomCode);
            }

            if (id.HasValue)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                return await _context.QuizRooms
                    .Include(r => r.Participants)
                    .Include(r => r.Documents)
                    .FirstOrDefaultAsync(r => r.Id == id.Value && r.Category == RoomCategory.Solo && r.CreatorUserId == userId);
            }

            return null;
        }

        private RoomQuizViewModel BuildQuizViewModel(QuizRoom room, QuizRoomParticipant me)
        {
            var documentIds = room.Documents.Select(d => d.UploadedDocumentId).ToList();
            var questions = _context.Questions
                .Where(q => documentIds.Contains(q.UploadedDocumentId))
                .OrderBy(q => q.UploadedDocumentId).ThenBy(q => q.Level).ThenBy(q => q.Id)
                .Take(room.QuestionCount > 0 ? room.QuestionCount : 5)
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
            // Astronomically unlikely, but fall back to a guaranteed-unique suffix.
            return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        }

        // Mirrors ExamController.ProcessSubmission's new-file branch: saves the PDF,
        // stores a placeholder summary, and seeds one sample question per difficulty
        // level. Kept as its own copy here (rather than shared) because the two
        // controllers' upload flows differ - this one can run for several files in
        // one request instead of exactly one.
        private async Task<UploadedDocument> CreateSeededDocumentAsync(string userId, Microsoft.AspNetCore.Http.IFormFile file)
        {
            string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsFolder);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            var doc = new UploadedDocument
            {
                UserId = userId,
                FileName = file.FileName,
                FilePath = filePath,
                Summary = "This is an automated summary generated from your multi-page PDF text.",
                UploadedAt = DateTime.UtcNow
            };

            doc.Questions.Add(new Question { QuestionText = "What is a basic concept covered in this document?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "A", Level = QuestionLevel.Basic, Explanation = "Basic explanation." });
            doc.Questions.Add(new Question { QuestionText = "How do you apply the medium-level concept here?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "B", Level = QuestionLevel.Medium, Explanation = "Medium explanation." });
            doc.Questions.Add(new Question { QuestionText = "What is the hard analytical conclusion?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "C", Level = QuestionLevel.Hard, Explanation = "Hard explanation." });

            _context.UploadedDocuments.Add(doc);
            await _context.SaveChangesAsync();

            return doc;
        }
    }
}