using EduSathi.Models;
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

        // Questionnaires / live rooms (Solo + Global competitive quizzes).
        public DbSet<QuizRoom> QuizRooms { get; set; }
        public DbSet<QuizRoomDocument> QuizRoomDocuments { get; set; }
        public DbSet<QuizRoomParticipant> QuizRoomParticipants { get; set; }

        protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // A room's shareable code only needs to be unique while it matters (i.e.
            // among Global rooms with a non-empty code); a filtered unique index lets
            // every Solo room keep Code = "" without colliding.
            builder.Entity<QuizRoom>()
                .HasIndex(r => r.Code)
                .IsUnique()
                .HasFilter("[Code] <> ''");
        }
    }
}