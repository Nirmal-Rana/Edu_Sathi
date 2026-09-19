using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UglyToad.PdfPig;
using EduSathi.Data;
using EduSathi.Models;

namespace EduSathi.Controllers.Api
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DocumentsController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
                    fileName = d.FileName,
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

            string fileText = "";
            try
            {
                using (var pdf = PdfDocument.Open(filePath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        fileText += page.Text + " ";
                    }
                }
            }
            catch
            {
                fileText = "Uploaded document content.";
            }

            var newDoc = new UploadedDocument
            {
                UserId = userId ?? "",
                FileName = file.FileName,
                FilePath = filePath,
                ExtractedText = fileText,
                Summary = string.Empty,
                UploadedAt = DateTime.UtcNow
            };

            _context.UploadedDocuments.Add(newDoc);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "success",
                message = "File uploaded successfully",
                data = new
                {
                    id = newDoc.Id,
                    fileName = newDoc.FileName,
                    uploadDate = newDoc.UploadedAt
                }
            });
        }
    }

    // DTO Wrapper for file upload support in Swagger
    public class DocumentUploadDto
    {
        public IFormFile File { get; set; } = default!;
    }
}