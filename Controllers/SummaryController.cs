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
    // Single responsibility: accept a PDF (new upload or previously stored),
    // extract its text, generate an AI summary, and persist the resulting
    // UploadedDocument. Does not know anything about quiz questions, grading,
    // or quiz history — that belongs to QuizController.
    [Authorize]
    public class SummaryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly SummaryService _summaryService;

        public SummaryController(ApplicationDbContext context, IWebHostEnvironment env, SummaryService summaryService)
        {
            _context = context;
            _env = env;
            _summaryService = summaryService;
        }

        // GET: /Summary
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

        // POST: /Summary/ProcessSubmission
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

                // Generate comprehensive learning summary only
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

            // Summary's job ends here — hand off to QuizController for the quiz experience.
            return RedirectToAction("QuizSession", "Quiz", new { id = targetDocumentId });
        }
    }
}