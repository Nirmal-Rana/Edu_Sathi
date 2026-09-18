using EduSathi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace EduSathi.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // If the user is already signed in, send them directly to the exam dashboard
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Exam");
            }

            // Otherwise, show the public home/landing page
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}