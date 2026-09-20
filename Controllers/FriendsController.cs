using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduSathi.Data;
using EduSathi.Models;

[Authorize]
public class FriendsController : Controller
{
    private readonly ApplicationDbContext _context;

    public FriendsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: /Friends
    public async Task<IActionResult> Index()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Fetch pending requests sent TO the current user
        var receivedRequests = await _context.Friendships
            .Include(f => f.User) // The user who sent the request
            .Where(f => f.FriendId == currentUserId && !f.IsAccepted)
            .ToListAsync();

        // Fetch pending requests sent BY the current user
        var sentRequests = await _context.Friendships
            .Include(f => f.Friend) // The user who received the request
            .Where(f => f.UserId == currentUserId && !f.IsAccepted)
            .ToListAsync();

        // Fetch accepted friends (where user is either UserId or FriendId)
        var acceptedFriendships = await _context.Friendships
            .Include(f => f.User)
            .Include(f => f.Friend)
            .Where(f => f.IsAccepted && (f.UserId == currentUserId || f.FriendId == currentUserId))
            .ToListAsync();

        var viewModel = new FriendsViewModel
        {
            ReceivedRequests = receivedRequests,
            SentRequests = sentRequests,
            Friends = acceptedFriendships
        };

        return View(viewModel);
    }

    // POST: /Friends/SendRequest
    [HttpPost]
    public async Task<IActionResult> SendRequest(string friendEmail)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == friendEmail);

        if (targetUser == null)
        {
            TempData["ErrorMessage"] = "User with this email address was not found.";
            return RedirectToAction(nameof(Index));
        }

        if (targetUser.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot send a friend request to yourself.";
            return RedirectToAction(nameof(Index));
        }

        // Check if any relationship already exists between these two users
        var existing = await _context.Friendships
            .FirstOrDefaultAsync(f => (f.UserId == currentUserId && f.FriendId == targetUser.Id) ||
                                      (f.UserId == targetUser.Id && f.FriendId == currentUserId));

        if (existing != null)
        {
            TempData["ErrorMessage"] = "A connection or request already exists with this user.";
            return RedirectToAction(nameof(Index));
        }

        var friendship = new Friendship
        {
            UserId = currentUserId,
            FriendId = targetUser.Id,
            IsAccepted = false,
            ConnectedAt = DateTime.UtcNow
        };

        _context.Friendships.Add(friendship);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Friend request sent successfully!";
        return RedirectToAction(nameof(Index));
    }

    // POST: /Friends/AcceptRequest
    [HttpPost]
    public async Task<IActionResult> AcceptRequest(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var friendship = await _context.Friendships.FindAsync(id);

        if (friendship != null && friendship.FriendId == currentUserId)
        {
            friendship.IsAccepted = true;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Friend request accepted!";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Friends/RejectRequest
    [HttpPost]
    public async Task<IActionResult> RejectRequest(int id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var friendship = await _context.Friendships.FindAsync(id);

        if (friendship != null && (friendship.FriendId == currentUserId || friendship.UserId == currentUserId))
        {
            _context.Friendships.Remove(friendship);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Friend request removed/rejected.";
        }

        return RedirectToAction(nameof(Index));
    }
}