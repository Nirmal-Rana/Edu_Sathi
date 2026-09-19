using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Services;

namespace EduSathi.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SummaryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly GeminiService _geminiService;

        public SummaryController(ApplicationDbContext context, GeminiService geminiService)
        {
            _context = context;
            _geminiService = geminiService;
        }

        // GET: /api/v1/summary/{documentId}
        [HttpGet("{documentId}")]
        public async Task<IActionResult> GetDocumentSummary(int documentId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var doc = await _context.UploadedDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId);

            if (doc == null)
                return NotFound(new { status = "error", code = "DOC_NOT_FOUND", message = "Document not found." });

            // If summary hasn't been generated yet, populate it
            if (string.IsNullOrEmpty(doc.Summary) && !string.IsNullOrEmpty(doc.ExtractedText))
            {
                try
                {
                    // Basic fallback generation or AI call block
                    string snippet = doc.ExtractedText.Length > 600 ? doc.ExtractedText.Substring(0, 600) + "..." : doc.ExtractedText;
                    doc.Summary = $"Key Summary for {doc.FileName}:\n\nThis document covers core concepts including: {snippet}";
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    doc.Summary = "Summary could not be processed automatically.";
                }
            }

            return Ok(new
            {
                status = "success",
                message = "Summary retrieved successfully",
                data = new
                {
                    documentId = doc.Id,
                    fileName = doc.FileName,
                    summary = doc.Summary
                }
            });
        }
    }
}