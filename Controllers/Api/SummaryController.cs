using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Services;


namespace EduSathi.Controllers.Api
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class SummaryController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly SummaryService _summaryService;

        public SummaryController(ApplicationDbContext context, SummaryService summaryService)
        {
            _context = context;
            _summaryService = summaryService;
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

            if (string.IsNullOrEmpty(doc.Summary) && !string.IsNullOrEmpty(doc.ExtractedText))
            {
                try
                {
                    doc.Summary = await _summaryService.GenerateSummaryAsync(doc.ExtractedText);
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