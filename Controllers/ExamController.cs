using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.ViewModels;
using EduSathi.Services;
using UglyToad.PdfPig;

namespace EduSathi.Controllers
{
    [Authorize]
    public class ExamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly GeminiService _geminiService;

        public ExamController(ApplicationDbContext context, IWebHostEnvironment env, GeminiService geminiService)
        {
            _context = context;
            _env = env;
            _geminiService = geminiService;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userDocs = await _context.UploadedDocuments
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var viewModel = new ExamDashboardViewModel
            {
                PreviousDocuments = userDocs
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessSubmission(ExamDashboardViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int targetDocumentId;

            if (model.SelectedExistingDocumentId.HasValue)
            {
                targetDocumentId = model.SelectedExistingDocumentId.Value;
            }
            else if (model.NewPdfFile != null && model.NewPdfFile.Length > 0)
            {
                // Saving to TempPath prevents Visual Studio Hot Reload from crashing the app during upload
                string uploadsFolder = Path.Combine(Path.GetTempPath(), "EduSathiUploads");
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.NewPdfFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewPdfFile.CopyToAsync(fileStream);
                }

                string extractedText = "";
                using (var pdf = PdfDocument.Open(filePath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        extractedText += page.Text + " ";
                    }
                }

                if (extractedText.Length > 30000)
                {
                    extractedText = extractedText.Substring(0, 30000);
                }

                string aiSummary = await _geminiService.GenerateSummaryAsync(extractedText);


                var newDoc = new UploadedDocument
                {
                    UserId = userId ?? string.Empty,
                    FileName = model.NewPdfFile.FileName,
                    FilePath = filePath,
                    ExtractedText = extractedText,
                    Summary = aiSummary,
                    UploadedAt = DateTime.UtcNow
                };

                var basicQuestions = await _geminiService.GenerateQuestionsAsync(extractedText, QuestionLevel.Basic, 2);
                var mediumQuestions = await _geminiService.GenerateQuestionsAsync(extractedText, QuestionLevel.Medium, 2);
                var hardQuestions = await _geminiService.GenerateQuestionsAsync(extractedText, QuestionLevel.Hard, 2);

                foreach (var q in basicQuestions) newDoc.Questions.Add(q);
                foreach (var q in mediumQuestions) newDoc.Questions.Add(q);
                foreach (var q in hardQuestions) newDoc.Questions.Add(q);

                _context.UploadedDocuments.Add(newDoc);
                await _context.SaveChangesAsync();

                targetDocumentId = newDoc.Id;
            }
            else
            {
                ModelState.AddModelError("", "Please upload a new PDF or select an existing one.");
                model.PreviousDocuments = await _context.UploadedDocuments.Where(d => d.UserId == userId).ToListAsync();
                return View("Index", model);
            }

            return RedirectToAction(nameof(QuizSession), new { id = targetDocumentId });
        }

        public async Task<IActionResult> QuizSession(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var document = await _context.UploadedDocuments
                .Include(d => d.Questions)
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (document == null) return NotFound();

            var viewModel = new QuizSessionViewModel
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Summary = document.Summary,
                BasicQuestions = document.Questions.Where(q => q.Level == QuestionLevel.Basic).ToList(),
                MediumQuestions = document.Questions.Where(q => q.Level == QuestionLevel.Medium).ToList(),
                HardQuestions = document.Questions.Where(q => q.Level == QuestionLevel.Hard).ToList()
            };

            return View(viewModel);
        }
    }
}