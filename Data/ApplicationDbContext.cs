***REMOVED***using EduSathi.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EduSathi.Data
{
    // Pass ApplicationUser into IdentityDbContext here:
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<UploadedDocument> UploadedDocuments { get; set; }
        public DbSet<Question> Questions { get; set; }
    }
}