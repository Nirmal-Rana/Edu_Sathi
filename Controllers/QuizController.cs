using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.ViewModels;

namespace EduSathi.Controllers
{
    // Single responsibility: present a solo quiz for a document and grade it
    // once submitted. Does not touch PDF upload, text extraction, or summary
    // generation — that belongs to SummaryController.
    [Authorize]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuizController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Quiz/QuizSession/{id}
        public async Task<IActionResult> QuizSession(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var document = await _context.UploadedDocuments
                .Include(d => d.Questions) // Include the generated questions
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (document == null) return NotFound();

            var viewModel = new QuizSessionViewModel
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Summary = document.Summary,
                Questions = document.Questions.ToList()
            };

            return View(viewModel);
        }

        // POST: /Quiz/SubmitQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitQuiz(int documentId, Dictionary<int, string> userAnswers)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var document = await _context.UploadedDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

            if (document == null) return NotFound();

            int score = 0;
            int totalQuestions = userAnswers != null ? userAnswers.Count : 0;

            if (userAnswers != null)
            {
                foreach (var kvp in userAnswers)
                {
                    int questionId = kvp.Key;
                    string selectedOption = kvp.Value;

                    var question = await _context.Questions.FindAsync(questionId);
                    if (question != null && question.CorrectOption.Equals(selectedOption, System.StringComparison.OrdinalIgnoreCase))
                    {
                        score++;
                    }
                }
            }

            _context.QuizHistories.Add(new QuizHistory
            {
                UserId = userId ?? "",
                QuizTitle = document.FileName,
                Category = "Solo",
                Score = score,
                TotalQuestions = totalQuestions > 0 ? totalQuestions : 1
            });

            await _context.SaveChangesAsync();

            var resultViewModel = new QuizResultViewModel
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Score = score,
                TotalQuestions = totalQuestions
            };

            return View("QuizResult", resultViewModel);
        }
    }
}