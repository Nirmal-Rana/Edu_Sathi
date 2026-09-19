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

                var existingDoc = await _context.UploadedDocuments
                    .FirstOrDefaultAsync(d => d.Id == targetDocumentId && d.UserId == userId);

                if (existingDoc == null)
                {
                    ModelState.AddModelError("", "Selected document not found.");
                    model.PreviousDocuments = await _context.UploadedDocuments.Where(d => d.UserId == userId).ToListAsync();
                    return View("Index", model);
                }

                // If summary wasn't generated yet for this existing doc, generate it now
                if (string.IsNullOrWhiteSpace(existingDoc.Summary))
                {
                    existingDoc.Summary = await _summaryService.GenerateSummaryAsync(existingDoc.ExtractedText);
                    await _context.SaveChangesAsync();
                }
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

                if (extractedText.Length > 20000)
                {
                    extractedText = extractedText.Substring(0, 20000);
                }

                // Generate AI summary
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

            // REDIRECT TO SUMMARY PAGE (Not Quiz)
            return RedirectToAction("Details", new { id = targetDocumentId });
        }

        // GET: /Summary/Details/{id}
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var doc = await _context.UploadedDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (doc == null)
            {
                return NotFound();
            }

            return View(doc);
        }
    }
}