***REMOVED***using System;
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

namespace EduSathi.Controllers
{
    [Authorize]
    public class ExamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ExamController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // GET: /Exam/Index (Dashboard for uploading or picking past PDFs)
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

        // POST: /Exam/ProcessSubmission
        [HttpPost]
        public async Task<IActionResult> ProcessSubmission(ExamDashboardViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int targetDocumentId;

            if (model.SelectedExistingDocumentId.HasValue)
            {
                // User chose a previously stored PDF
                targetDocumentId = model.SelectedExistingDocumentId.Value;
            }
            else if (model.NewPdfFile != null && model.NewPdfFile.Length > 0)
            {
                // User uploaded a new PDF file from device
                string uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsFolder);
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.NewPdfFile.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.NewPdfFile.CopyToAsync(fileStream);
                }

                // Temporary sample summary & questions (AI parsing service integration can go here next)
                string sampleSummary = "This is an automated summary generated from your multi-page PDF text.";

                var newDoc = new UploadedDocument
                {
                    UserId = userId ?? string.Empty,
                    FileName = model.NewPdfFile.FileName,
                    FilePath = filePath,
                    Summary = sampleSummary,
                    UploadedAt = DateTime.UtcNow
                };

                // Add sample questions categorized by difficulty levels
                newDoc.Questions.Add(new Question { QuestionText = "What is a basic concept covered in this document?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "A", Level = QuestionLevel.Basic, Explanation = "Basic explanation." });
                newDoc.Questions.Add(new Question { QuestionText = "How do you apply the medium-level concept here?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "B", Level = QuestionLevel.Medium, Explanation = "Medium explanation." });
                newDoc.Questions.Add(new Question { QuestionText = "What is the hard analytical conclusion?", OptionA = "Option A", OptionB = "Option B", OptionC = "Option C", OptionD = "Option D", CorrectOption = "C", Level = QuestionLevel.Hard, Explanation = "Hard explanation." });

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

        // GET: /Exam/QuizSession/{id}
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