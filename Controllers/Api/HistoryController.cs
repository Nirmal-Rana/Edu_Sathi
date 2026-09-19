using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;

namespace EduSathi.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class HistoryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public HistoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /api/v1/history
        [HttpGet]
        public async Task<IActionResult> GetUserQuizHistory()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { status = "error", code = "UNAUTHORIZED", message = "Invalid token." });

            var histories = await _context.QuizHistories
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.CompletedAt)
                .Select(h => new
                {
                    id = h.Id,
                    quizTitle = h.QuizTitle,
                    score = h.Score,
                    totalQuestions = h.TotalQuestions,
                    percentage = h.TotalQuestions > 0 ? (double)h.Score / h.TotalQuestions * 100 : 0,
                    completedAt = h.CompletedAt
                })
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                message = "Quiz history retrieved successfully",
                data = histories
            });
        }
    }
}