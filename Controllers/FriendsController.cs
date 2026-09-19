using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduSathi.Data;
using EduSathi.Models;
using EduSathi.ViewModels;

namespace EduSathi.Controllers
{
    // Friends / Study Buddies: send a request by email, accept/decline, and see
    // your accepted friends. FriendRequest rows double as both the pending
    // request and, once accepted, the friendship record - see Models/FriendRequest.cs.
    [Authorize]
    public class FriendsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FriendsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Friends
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var all = await _context.FriendRequests
                .Include(f => f.Requester)
                .Include(f => f.Addressee)
                .Where(f => f.RequesterUserId == userId || f.AddresseeUserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var vm = new FriendsIndexViewModel
            {
                ReceivedPending = all.Where(f => f.Status == FriendRequestStatus.Pending && f.AddresseeUserId == userId).ToList(),
                SentPending = all.Where(f => f.Status == FriendRequestStatus.Pending && f.RequesterUserId == userId).ToList(),
                Friends = all.Where(f => f.Status == FriendRequestStatus.Accepted)
                    .Select(f =>
                    {
                        var other = f.RequesterUserId == userId ? f.Addressee : f.Requester;
                        return new FriendSummary
                        {
                            UserId = other.Id,
                            DisplayName = string.IsNullOrWhiteSpace(other.FullName) ? (other.Email ?? "Student") : other.FullName,
                            Email = other.Email ?? ""
                        };
                    })
                    .DistinctBy(f => f.UserId)
                    .ToList()
            };

            return View(vm);
        }

        // POST: /Friends/SendRequest
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequest(string email)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            email = (email ?? "").Trim();

            if (string.IsNullOrEmpty(email))
            {
                TempData["FriendError"] = "Enter an email address.";
                return RedirectToAction(nameof(Index));
            }

            var target = await _userManager.FindByEmailAsync(email);
            if (target == null)
            {
                TempData["FriendError"] = "No EduSathi account found with that email.";
                return RedirectToAction(nameof(Index));
            }

            if (target.Id == userId)
            {
                TempData["FriendError"] = "You can't add yourself.";
                return RedirectToAction(nameof(Index));
            }

            var existing = await _context.FriendRequests.FirstOrDefaultAsync(f =>
                (f.RequesterUserId == userId && f.AddresseeUserId == target.Id) ||
                (f.RequesterUserId == target.Id && f.AddresseeUserId == userId));

            if (existing != null)
            {
                TempData["FriendError"] = existing.Status == FriendRequestStatus.Accepted
                    ? "You're already friends."
                    : "A request between you two is already pending.";
                return RedirectToAction(nameof(Index));
            }

            _context.FriendRequests.Add(new FriendRequest
            {
                RequesterUserId = userId,
                AddresseeUserId = target.Id,
                Status = FriendRequestStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Friends/Accept
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id && f.AddresseeUserId == userId);
            if (request == null) return NotFound();

            request.Status = FriendRequestStatus.Accepted;
            request.RespondedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Friends/Decline
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decline(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id && f.AddresseeUserId == userId);
            if (request == null) return NotFound();

            _context.FriendRequests.Remove(request);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Friends/Cancel  (requester withdraws their own pending request)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var request = await _context.FriendRequests.FirstOrDefaultAsync(f => f.Id == id && f.RequesterUserId == userId);
            if (request == null) return NotFound();

            _context.FriendRequests.Remove(request);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}