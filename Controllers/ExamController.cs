using System;
using System.Collections.Generic;
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

        // Replaced GeminiService with your two new isolated services
        private readonly SummaryService _summaryService;
        private readonly McqService _mcqService;

        public ExamController(ApplicationDbContext context, IWebHostEnvironment env, SummaryService summaryService, McqService mcqService)
        {
            _context = context;
            _env = env;
            _summaryService = summaryService;
            _mcqService = mcqService;
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

                // Generates comprehensive learning summary using the new SummaryService
                string aiSummary = await _summaryService.GenerateSummaryAsync(extractedText);

                var newDoc = new UploadedDocument
                {
                    UserId = userId ?? string.Empty,
                    FileName = model.NewPdfFile.FileName,
                    FilePath = filePath,
                    ExtractedText = extractedText,
                    Summary = aiSummary,
                    UploadedAt = DateTime.UtcNow
                };

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
                .Include(d => d.Questions) // Include the generated questions
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (document == null) return NotFound();

            var viewModel = new QuizSessionViewModel
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Summary = document.Summary,
                Questions = document.Questions.ToList() // Pass the questions here!
            };

            return View(viewModel);
        }

        // ====================== ADDED BELOW THIS LINE ======================

        // GET: /Exam/History
        public async Task<IActionResult> History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userDocs = await _context.UploadedDocuments
                .Include(d => d.Questions)
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return View(userDocs);
        }

        // GET: /Exam/Quiz?id=5&level=Basic
        public async Task<IActionResult> Quiz(int id, QuestionLevel level = QuestionLevel.Basic)
        {
            var vm = await BuildAttemptAsync(id, level);
            if (vm == null) return NotFound();
            return View(vm);
        }

        // POST: /Exam/Quiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Quiz")]
        public async Task<IActionResult> QuizSubmit(int id, QuestionLevel level, Dictionary<int, string>? answers)
        {
            var vm = await BuildAttemptAsync(id, level);
            if (vm == null) return NotFound();

            vm.Answers = answers ?? new Dictionary<int, string>();
            vm.IsSubmitted = true;
            vm.CorrectCount = vm.Questions.Count(q =>
                vm.Answers.TryGetValue(q.Id, out var picked) &&
                string.Equals(picked, q.CorrectOption, StringComparison.OrdinalIgnoreCase));

            return View("Quiz", vm);
        }

        private async Task<QuizAttemptViewModel?> BuildAttemptAsync(int documentId, QuestionLevel level)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var document = await _context.UploadedDocuments
                .Include(d => d.Questions)
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

            if (document == null) return null;

            return new QuizAttemptViewModel
            {
                DocumentId = document.Id,
                FileName = document.FileName,
                Level = level,
                Questions = document.Questions.Where(q => q.Level == level).ToList()
            };
        }
    }
}