using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Models;

namespace EduSathi.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class ProfileController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /api/v1/profile
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId ?? "");

            if (user == null)
                return NotFound(new { status = "error", code = "USER_NOT_FOUND", message = "User profile not found." });

            // Calculate quick user stats from history/documents
            int totalDocuments = await _context.UploadedDocuments.CountAsync(d => d.UserId == user.Id);
            int totalQuizzes = await _context.QuizHistories.CountAsync(h => h.UserId == user.Id);

            return Ok(new
            {
                status = "success",
                message = "Profile retrieved successfully",
                data = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName ?? user.UserName,
                    stats = new
                    {
                        documentsUploaded = totalDocuments,
                        quizzesCompleted = totalQuizzes
                    }
                }
            });
        }

        // PUT: /api/v1/profile/update
        [HttpPut("update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId ?? "");

            if (user == null)
                return NotFound(new { status = "error", code = "USER_NOT_FOUND", message = "User profile not found." });

            user.FullName = model.FullName;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new { status = "error", code = "UPDATE_FAILED", message = "Failed to update profile." });
            }

            return Ok(new
            {
                status = "success",
                message = "Profile updated successfully",
                data = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName
                }
            });
        }
    }

    public class UpdateProfileDto
    {
        public string FullName { get; set; } = string.Empty;
    }
}