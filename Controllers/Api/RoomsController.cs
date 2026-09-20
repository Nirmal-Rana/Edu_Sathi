using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.Services;

namespace EduSathi.Controllers.Api
{
   
    public class RoomsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly McqService _mcqService;

        public RoomsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, McqService mcqService)
        {
            _context = context;
            _userManager = userManager;
            _mcqService = mcqService;
        }

        // POST: /api/v1/rooms/join
        [HttpPost("join")]
        public async Task<IActionResult> JoinRoom([FromBody] JoinRoomDto model)
        {
            if (string.IsNullOrEmpty(model.RoomCode))
                return BadRequest(new { status = "error", code = "INVALID_CODE", message = "Room code is required." });

            var cleanCode = model.RoomCode.Trim().ToUpper();
            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.RoomCode == cleanCode && r.IsActive);

            if (room == null)
                return NotFound(new { status = "error", code = "ROOM_NOT_FOUND", message = "Room not found or inactive." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            var participant = room.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null)
            {
                room.Participants.Add(new RoomParticipant
                {
                    UserId = userId ?? "",
                    UserName = user?.FullName ?? user?.UserName ?? "Participant"
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "success",
                message = "Joined room successfully",
                data = new { roomCode = room.RoomCode, roomName = room.RoomName, isStarted = room.IsQuizStarted }
            });
        }

        // POST: /api/v1/rooms/create
        [HttpPost("create")]
        public async Task<IActionResult> CreateCustomRoom([FromBody] CreateRoomDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _context.Users.FindAsync(userId);

            var doc = await _context.UploadedDocuments.FirstOrDefaultAsync(d => d.Id == model.DocumentId && d.UserId == userId);
            if (doc == null)
                return NotFound(new { status = "error", code = "DOC_NOT_FOUND", message = "Source document not found." });

            // Generate Questions using AI service
            string extractedText = doc.ExtractedText.Length > 25000 ? doc.ExtractedText.Substring(0, 25000) : doc.ExtractedText;

            // McqService.GenerateMcqsAsync(text, level, questionCount) - level 2 = Medium.
            // CreateRoomDto has no difficulty field yet, so Medium is the default for now.
            string aiResponse = await _mcqService.GenerateMcqsAsync(extractedText, 2, model.QuestionCount);

            doc.Questions.Add(new Question
            {
                QuestionText = $"AI Generated Review Session ({model.QuestionCount} questions)",
                OptionA = "Option A",
                OptionB = "Option B",
                OptionC = "Option C",
                OptionD = "Option D",
                CorrectOption = "A",
                Level = QuestionLevel.Medium,
                Explanation = aiResponse
            });
            await _context.SaveChangesAsync();

            string roomCode = Guid.NewGuid().ToString().Substring(0, 6).ToUpper();
            var room = new CustomRoom
            {
                RoomCode = roomCode,
                CreatorId = userId ?? "",
                RoomName = string.IsNullOrEmpty(model.RoomName) ? $"{doc.FileName} Quiz" : model.RoomName,
                IsActive = true,
                IsQuizStarted = false
            };

            room.Participants.Add(new RoomParticipant
            {
                UserId = userId ?? "",
                UserName = user?.FullName ?? user?.UserName ?? "Host"
            });

            _context.CustomRooms.Add(room);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "success",
                message = "Room created successfully",
                data = new
                {
                    roomCode = room.RoomCode,
                    roomName = room.RoomName,
                    documentName = doc.FileName,
                    questionCount = model.QuestionCount
                }
            });
        }

        // GET: /api/v1/rooms/{roomCode}
        [HttpGet("{roomCode}")]
        public async Task<IActionResult> GetRoomDetails(string roomCode)
        {
            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.RoomCode == roomCode.ToUpper());

            if (room == null)
                return NotFound(new { status = "error", code = "ROOM_NOT_FOUND", message = "Room not found." });

            return Ok(new
            {
                status = "success",
                data = new
                {
                    roomCode = room.RoomCode,
                    roomName = room.RoomName,
                    isStarted = room.IsQuizStarted,
                    participants = room.Participants.Select(p => new
                    {
                        userId = p.UserId,
                        userName = p.UserName,
                        hasSubmitted = p.HasSubmitted, // Fixed to use HasSubmitted property
                        score = p.Score
                    })
                }
            });
        }

        // POST: /api/v1/rooms/invite
        [HttpPost("invite")]
        public async Task<IActionResult> InviteFriend([FromBody] InviteFriendDto model)
        {
            var room = await _context.CustomRooms
                .Include(r => r.Participants)
                .FirstOrDefaultAsync(r => r.RoomCode == model.RoomCode.ToUpper());

            if (room == null)
                return NotFound(new { status = "error", code = "ROOM_NOT_FOUND", message = "Room not found." });

            var friendUser = await _userManager.FindByIdAsync(model.FriendId);
            if (friendUser == null)
                return NotFound(new { status = "error", code = "USER_NOT_FOUND", message = "Friend not found." });

            if (!room.Participants.Any(p => p.UserId == model.FriendId))
            {
                room.Participants.Add(new RoomParticipant
                {
                    UserId = friendUser.Id,
                    UserName = friendUser.FullName ?? friendUser.UserName ?? "Participant"
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new { status = "success", message = "Invitation sent successfully." });
        }

        // POST: /api/v1/rooms/start
        [HttpPost("start")]
        public async Task<IActionResult> StartQuiz([FromBody] StartRoomDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var room = await _context.CustomRooms.FirstOrDefaultAsync(r => r.RoomCode == model.RoomCode.ToUpper() && r.CreatorId == userId);

            if (room == null)
                return Unauthorized(new { status = "error", code = "UNAUTHORIZED", message = "Only the host can start this quiz." });

            room.IsQuizStarted = true;
            await _context.SaveChangesAsync();

            return Ok(new { status = "success", message = "Quiz started successfully." });
        }
    }

    // DTOs for Mobile API Payload Support
    public class JoinRoomDto { public string RoomCode { get; set; } = string.Empty; }
    public class CreateRoomDto
    {
        public int DocumentId { get; set; }
        public int QuestionCount { get; set; } = 10;
        public string RoomName { get; set; } = string.Empty;
    }
    public class InviteFriendDto
    {
        public string RoomCode { get; set; } = string.Empty;
        public string FriendId { get; set; } = string.Empty;
    }
    public class StartRoomDto { public string RoomCode { get; set; } = string.Empty; }
}