using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.ViewModels;
using EduSathi.Services;

namespace EduSathi.Controllers
{
    // Single responsibility: present a solo quiz for a document and grade it
    // once submitted. Does not touch PDF upload, text extraction, or summary
    // generation — that belongs to SummaryController. It does own making sure
    // the quiz itself has questions, since that's part of "presenting the quiz".
    [Authorize]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly McqService _mcqService;

        public QuizController(ApplicationDbContext context, McqService mcqService)
        {
            _context = context;
            _mcqService = mcqService;
        }

        // GET: /Quiz/QuizSession/{id}
        public async Task<IActionResult> QuizSession(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var document = await _context.UploadedDocuments
                .Include(d => d.Questions) // Include the generated questions
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (document == null) return NotFound();

            // If no questions exist yet for this document, generate them now.
            // This only ever calls McqService — it never touches the summary.
            if (!document.Questions.Any())
            {
                await GenerateQuestionsAsync(document);
            }

            var viewModel = new QuizSessionViewModel
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Summary = document.Summary,
                Questions = document.Questions.ToList()
            };

            return View(viewModel);
        }

        private async Task GenerateQuestionsAsync(UploadedDocument document)
        {
            foreach (var level in new[] { QuestionLevel.Basic, QuestionLevel.Medium, QuestionLevel.Hard })
            {
                try
                {
                    var jsonResponse = await _mcqService.GenerateMcqsAsync(document.ExtractedText, (int)level);

                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        string cleaned = jsonResponse.Trim();

                        // Gemini sometimes wraps its JSON in ```json ... ``` fences even when
                        // told not to; strip them before deserializing.
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
                                document.Questions.Add(new Question
                                {
                                    UploadedDocumentId = document.Id,
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