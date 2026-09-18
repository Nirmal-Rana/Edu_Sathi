***REMOVED***using Microsoft.AspNetCore.Identity;

namespace EduSathi.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
    }
}