using System;
using System.ComponentModel.DataAnnotations;

namespace EduSathi.Models
{
    public class QuizHistory
    {
        public int Id { get; set; }
        [Required]
        public string UserId { get; set; } = string.Empty;
        public string QuizTitle { get; set; } = string.Empty;
        public string Category { get; set; } = "Solo"; 
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }
}