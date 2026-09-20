using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;
using UglyToad.PdfPig;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.Services;

namespace EduSathi.Controllers.Api
{
   
    public class DocumentsController : ApiControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IServiceScopeFactory _scopeFactory;

        public DocumentsController(ApplicationDbContext context, IWebHostEnvironment env, IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _env = env;
            _scopeFactory = scopeFactory;
        }

        // GET: /api/v1/documents
        [HttpGet]
        public async Task<IActionResult> GetUserDocuments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var documents = await _context.UploadedDocuments
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new
                {
                    id = d.Id,
                    name = d.FileName,
                    pages = d.Pages,
                    status = d.Status.ToString().ToLower(), // "pending" | "processing" | "completed" | "failed"
                    uploadDate = d.UploadedAt,
                    fileSizeKb = System.IO.File.Exists(d.FilePath) ? new FileInfo(d.FilePath).Length / 1024 : 0
                })
                .ToListAsync();

            return Ok(new
            {
                status = "success",
                message = "Documents retrieved successfully",
                data = documents
            });
        }

        // POST: /api/v1/documents/upload
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadDto model)
        {
            var file = model.File;
            if (file == null || file.Length == 0)
                return BadRequest(new { status = "error", code = "INVALID_FILE", message = "No file uploaded." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            string uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.GetTempPath(), "uploads");
            Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Save immediately with status = pending. Text extraction and AI
            // summarization happen afterwards so the upload call returns fast.
            var newDoc = new UploadedDocument
            {
                UserId = userId ?? "",
                FileName = file.FileName,
                FilePath = filePath,
                ExtractedText = string.Empty,
                Summary = string.Empty,
                Status = DocumentStatus.Pending,
                Pages = 0,
                UploadedAt = DateTime.UtcNow
            };

            _context.UploadedDocuments.Add(newDoc);
            await _context.SaveChangesAsync();

            // Fire-and-forget background job. It opens its own DI scope because
            // the controller's _context/services are disposed as soon as this
            // request finishes.
            _ = ProcessDocumentAsync(newDoc.Id);

            return Ok(new
            {
                status = "success",
                message = "File uploaded successfully",
                data = new
                {
                    id = newDoc.Id,
                    name = newDoc.FileName,
                    pages = newDoc.Pages,
                    status = newDoc.Status.ToString().ToLower(),
                    uploadDate = newDoc.UploadedAt
                }
            });
        }

        // GET: /api/v1/documents/{id}/summary
        [HttpGet("{id}/summary")]
        public async Task<IActionResult> GetDocumentSummary(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var doc = await _context.UploadedDocuments
                .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId);

            if (doc == null)
                return NotFound(new { status = "error", code = "DOC_NOT_FOUND", message = "Document not found." });

            if (doc.Status == DocumentStatus.Failed)
            {
                return UnprocessableEntity(new
                {
                    status = "error",
                    code = "PROCESSING_FAILED",
                    message = doc.ProcessingError ?? "Document processing failed."
                });
            }

            if (doc.Status != DocumentStatus.Completed || string.IsNullOrEmpty(doc.SummaryJson))
            {
                // Lets the mobile app poll gracefully instead of guessing from pages == 0.
                return NotFound(new
                {
                    status = "error",
                    code = "NOT_READY",
                    message = "Summary is not ready yet.",
                    data = new { status = doc.Status.ToString().ToLower() }
                });
            }

            var parsedSummary = JsonSerializer.Deserialize<JsonElement>(doc.SummaryJson);

            return Ok(new
            {
                status = "success",
                message = "Summary retrieved successfully",
                data = parsedSummary
            });
        }

        private async Task ProcessDocumentAsync(int documentId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var summaryService = scope.ServiceProvider.GetRequiredService<SummaryService>();

            var doc = await context.UploadedDocuments.FindAsync(documentId);
            if (doc == null) return;

            doc.Status = DocumentStatus.Processing;
            await context.SaveChangesAsync();

            try
            {
                string fileText = "";
                int pageCount = 0;

                using (var pdf = PdfDocument.Open(doc.FilePath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        fileText += page.Text + " ";
                        pageCount++;
                    }
                }

                doc.ExtractedText = fileText;
                doc.Pages = pageCount;
                await context.SaveChangesAsync();

                string textForAi = fileText.Length > 25000 ? fileText.Substring(0, 25000) : fileText;
                var summaryResult = await summaryService.GenerateStructuredSummaryAsync(textForAi);

                if (summaryResult == null)
                {
                    doc.Status = DocumentStatus.Failed;
                    doc.ProcessingError = "AI summary generation failed or returned no content.";
                }
                else
                {
                    doc.SummaryJson = JsonSerializer.Serialize(summaryResult);
                    doc.Status = DocumentStatus.Completed;
                }
            }
            catch (Exception ex)
            {
                doc.Status = DocumentStatus.Failed;
                doc.ProcessingError = ex.Message;
            }

            await context.SaveChangesAsync();
        }
    }

    // DTO Wrapper for file upload support in Swagger
    public class DocumentUploadDto
    {
        public IFormFile File { get; set; } = default!;
    }
}