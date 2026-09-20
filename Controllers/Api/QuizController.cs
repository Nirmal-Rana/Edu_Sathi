using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Models;

namespace EduSathi.Controllers.Api
{
    [Route("api/v1/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class QuizController : ApiControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuizController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /api/v1/quiz/{roomCode}/questions
        [HttpGet("{roomCode}/questions")]
        public async Task<IActionResult> GetQuizQuestions(string roomCode)
        {
            var room = await _context.QuizRooms
                .Include(r => r.Documents)
                .ThenInclude(d => d.UploadedDocument)
                .ThenInclude(ud => ud.Questions)
                .FirstOrDefaultAsync(r => r.Code == roomCode.ToUpper());

            if (room == null)
                return NotFound(new { status = "error", code = "ROOM_NOT_FOUND", message = "Room not found." });

            var questions = room.Documents
                .SelectMany(d => d.UploadedDocument.Questions)
                .Select(q => new
                {
                    id = q.Id,
                    questionText = q.QuestionText,
                    optionA = q.OptionA,
                    optionB = q.OptionB,
                    optionC = q.OptionC,
                    optionD = q.OptionD,
                    level = q.Level.ToString()
                })
                .ToList();

            return Ok(new
            {
                status = "success",
                message = "Questions retrieved successfully",
                data = new
                {
                    roomCode = room.Code,
                    roomName = room.Name,
                    questions = questions
                }
            });
        }

        // POST: /api/v1/quiz/submit
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { status = "error", code = "UNAUTHORIZED", message = "Invalid token." });

            var room = await _context.QuizRooms
                .Include(r => r.Participants)
                .Include(r => r.Documents)
                .ThenInclude(d => d.UploadedDocument)
                .ThenInclude(ud => ud.Questions)
                .FirstOrDefaultAsync(r => r.Code == model.RoomCode.ToUpper());

            if (room == null)
                return NotFound(new { status = "error", code = "ROOM_NOT_FOUND", message = "Room not found." });

            var allQuestions = room.Documents.SelectMany(d => d.UploadedDocument.Questions).ToList();
            int score = 0;
            int totalQuestions = allQuestions.Count;

            foreach (var answer in model.Answers)
            {
                var question = allQuestions.FirstOrDefault(q => q.Id == answer.QuestionId);
                if (question != null && question.CorrectOption.Equals(answer.SelectedOption, StringComparison.OrdinalIgnoreCase))
                {
                    score++;
                }
            }

            // Update participant record
            var participant = room.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                participant.Score = score;
                participant.TotalQuestions = totalQuestions;
                participant.CompletedAt = DateTime.UtcNow;
            }

            // Save to Quiz History
            var history = new QuizHistory
            {
                UserId = userId,
                QuizTitle = room.Name,
                Score = score,
                TotalQuestions = totalQuestions > 0 ? totalQuestions : 10,
                CompletedAt = DateTime.UtcNow
            };
            _context.QuizHistories.Add(history);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "success",
                message = "Quiz submitted successfully",
                data = new
                {
                    score = score,
                    totalQuestions = totalQuestions,
                    percentage = totalQuestions > 0 ? (double)score / totalQuestions * 100 : 0
                }
            });
        }

        // POST: /api/v1/quiz/generate
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateQuiz([FromBody] QuizGenerateRequest model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { status = "error", code = "UNAUTHORIZED", message = "Invalid token." });

            var document = await _context.UploadedDocuments
                .Include(d => d.Questions)
                .FirstOrDefaultAsync(d => d.Id == model.DocumentId && d.UserId == userId);

            if (document == null)
                return NotFound(new { status = "error", code = "DOC_NOT_FOUND", message = "Document not found." });

            var questions = document.Questions.Select(q => new
            {
                id = q.Id.ToString(),
                prompt = q.QuestionText,
                options = new[]
                {
                    new { id = "a", label = "A", text = q.OptionA },
                    new { id = "b", label = "B", text = q.OptionB },
                    new { id = "c", label = "C", text = q.OptionC },
                    new { id = "d", label = "D", text = q.OptionD }
                },
                correctOptionId = q.CorrectOption.ToLower()
            }).ToList();

            string mockQuizId = "q_" + Guid.NewGuid().ToString("N").Substring(0, 6);

            return Ok(new
            {
                status = "success",
                message = "Quiz generated successfully",
                data = new
                {
                    quizId = mockQuizId,
                    questions = questions
                }
            });
        }

        // POST: /api/v1/quiz/rooms
        [HttpPost("rooms")]
        public async Task<IActionResult> CreateQuizRoom([FromBody] RoomCreateRequest model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { status = "error", code = "UNAUTHORIZED", message = "Invalid token." });

            string roomCode = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();

            var room = new QuizRoom
            {
                Code = roomCode,
                Name = "Study Room " + roomCode,
                CreatorUserId = userId  // <-- Change this from HostUserId to CreatorUserId
            };

            _context.QuizRooms.Add(room);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "success",
                message = "Room created successfully",
                data = new
                {
                    roomCode = room.Code,
                    ownerId = userId,
                    documentId = model.DocumentId,
                    quizId = model.QuizId,
                    participants = new List<object>()
                }
            });
        }
    }

    // DTOs
    public class QuizSubmissionDto
    {
        public string RoomCode { get; set; } = string.Empty;
        public List<UserAnswerDto> Answers { get; set; } = new();
    }

    public class UserAnswerDto
    {
        public int QuestionId { get; set; }
        public string SelectedOption { get; set; } = string.Empty;
    }

    public class QuizGenerateRequest
    {
        public int DocumentId { get; set; }
        public string Difficulty { get; set; } = "medium";
        public int QuestionCount { get; set; } = 10;
    }

    public class RoomCreateRequest
    {
        public string QuizId { get; set; } = string.Empty;
        public int DocumentId { get; set; }
    }
}