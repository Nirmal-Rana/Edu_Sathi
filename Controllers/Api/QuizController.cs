using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Models;

namespace EduSathi.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class QuizController : ControllerBase
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

            // Update participant record (Removed non-existent HasSubmitted property)
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
    }

    public class QuizSubmissionDto
    {
        public string RoomCode { get; set; } = string.Empty;
        public List<UserAnswerDto> Answers { get; set; } = new();
    }

    public class UserAnswerDto
    {
        public int QuestionId { get; set; }
        public string SelectedOption { get; set; } = string.Empty; // e.g., "A", "B", "C", "D"
    }
}