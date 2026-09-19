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
    // NEW FILE. No ProfileController existed, so nothing is being overwritten.
    // Converted from Profile.aspx.cs.
    //
    // Reads from Identity (ApplicationUser.FullName / ContactNumber / Email) and
    // from UploadedDocuments. It does not write anything.
    //
    // Sign-out is NOT here — _DashboardLayout posts to the Identity area page
    // /Account/Logout, exactly as your original layout did.
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var documentCount = await _context.UploadedDocuments
                .CountAsync(d => d.UserId == userId);

            var questionCount = await _context.Questions
                .CountAsync(q => q.UploadedDocument.UserId == userId);

            var vm = new ProfilePageViewModel
            {
                DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? "Student") : user.FullName,
                Email = user.Email ?? "",
                ContactNumber = user.ContactNumber,
                DocumentCount = documentCount,
                QuestionCount = questionCount
            };

            return View(vm);
        }
    }
}
