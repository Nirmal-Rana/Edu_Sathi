using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Models;

namespace EduSathi.Controllers.Api
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [ApiController]
    [Route("api/v1/[controller]")]
    public class FriendsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FriendsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /api/v1/friends
        [HttpGet]
        public async Task<IActionResult> GetFriends()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { status = "error", code = "UNAUTHORIZED", message = "Invalid token claims." });

            // Fetch friendships where user is involved and it is accepted
            var friendships = await _context.Friendships
                .Include(f => f.User)
                .Include(f => f.Friend)
                .Where(f => f.IsAccepted && (f.UserId == userId || f.FriendId == userId))
                .ToListAsync();

            var friendsList = friendships.Select(f =>
            {
                var friendUser = f.UserId == userId ? f.Friend : f.User;
                return new
                {
                    id = friendUser?.Id,
                    name = friendUser?.FullName ?? friendUser?.UserName,
                    email = friendUser?.Email,
                    profileImage = "https://cdn.edusathi.com/profiles/default.jpg" // Fallback or user profile URL
                };
            }).ToList();

            return Ok(new
            {
                status = "success",
                message = "Friends retrieved successfully",
                data = friendsList
            });
        }

        // POST: /api/v1/friends/add
        [HttpPost("add")]
        public async Task<IActionResult> AddFriend([FromBody] AddFriendDto model)
        {
            if (!ModelState.IsValid)
                return UnprocessableEntity(new { status = "error", code = "VALIDATION_FAILED", message = "Invalid input." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == model.Email) // Basic check
            {
                return BadRequest(new { status = "error", code = "SELF_ADD_FAILED", message = "You cannot add yourself as a friend." });
            }

            var friendUser = await _userManager.FindByEmailAsync(model.Email);
            if (friendUser == null)
            {
                return NotFound(new { status = "error", code = "USER_NOT_FOUND", message = "No user found with this email address." });
            }

            // Check if friendship already exists
            var existing = await _context.Friendships
                .FirstOrDefaultAsync(f => (f.UserId == userId && f.FriendId == friendUser.Id) || (f.UserId == friendUser.Id && f.FriendId == userId));

            if (existing != null)
            {
                return BadRequest(new { status = "error", code = "ALREADY_FRIENDS", message = "You are already connected or a request is pending." });
            }

            var friendship = new Friendship
            {
                UserId = userId ?? "",
                FriendId = friendUser.Id,
                IsAccepted = true // Direct acceptance or adjust based on your flow
            };

            _context.Friendships.Add(friendship);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "success",
                message = "Friend added successfully",
                data = new
                {
                    id = friendUser.Id,
                    name = friendUser.FullName ?? friendUser.UserName,
                    email = friendUser.Email
                }
            });
        }

        // DELETE: /api/v1/friends/{friendId}
        [HttpDelete("{friendId}")]
        public async Task<IActionResult> RemoveFriend(string friendId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var friendship = await _context.Friendships
                .FirstOrDefaultAsync(f => (f.UserId == userId && f.FriendId == friendId) || (f.UserId == friendId && f.FriendId == userId));

            if (friendship == null)
            {
                return NotFound(new { status = "error", code = "NOT_FOUND", message = "Friendship record not found." });
            }

            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                status = "success",
                message = "Friend removed successfully"
            });
        }
    }

    public class AddFriendDto
    {
        public string Email { get; set; } = string.Empty;
    }
}