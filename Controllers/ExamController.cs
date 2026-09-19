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
    // ============================================================================
    // COMPLETE REPLACEMENT for Controllers/ExamController.cs.
    //
    // Index, ProcessSubmission and QuizSession are UNCHANGED — same signatures,
    // same bodies, same behaviour, including the sample-summary and sample-question
    // seeding. Diff this file against yours and you should see only additions.
    //
    // THREE ACTIONS ARE ADDED. All three are new routes; none of them alters an
    // existing one:
    //
    //   History()          GET  /Exam/History
    //       The migrated design has a document history page in the sidebar. The data
    //       is the same query Index() already runs.
    //
    //   Quiz(id, level)    GET  /Exam/Quiz?id=..&level=Basic
    //   Quiz(...)          POST /Exam/Quiz
    //       QuizSession.cshtml listed questions but gave the user no way to answer
    //       them — no radios, no submit, no scoring. The converted design is an
    //       answerable quiz, so it needs somewhere to post to. Grading is done here
    //       on the server, deliberately: the old Web Forms page graded in a postback
    //       and correct answers never reached the browser before submission, and
    //       keeping that property matters more than saving a round trip.
    //
    // Nothing here writes to the database. If you'd rather these live in their own
    // controller, lift the three actions out — they only need _context.
    // ============================================================================
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

        // ====================== ADDED BELOW THIS LINE ======================

        // GET: /Exam/History
        // Same query as Index(), rendered as the document history list from the
        // migrated design. Read-only.
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
        // `answers` binds from inputs named answers[<questionId>] with values "A".."D".
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

            // TODO(backend): nothing is saved. To persist scores, XP and streaks you'd
            // need an attempt table (UserId, UploadedDocumentId, Level, Score, Total,
            // CompletedAt) plus a migration. Profile/Index currently shows "—" for
            // average score and streak for exactly this reason.

            return View("Quiz", vm);
        }

        // Loads one document + one difficulty level, scoped to the signed-in user.
        // Returns null when the document doesn't exist or belongs to someone else,
        // which is the same ownership check QuizSession() does.
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
