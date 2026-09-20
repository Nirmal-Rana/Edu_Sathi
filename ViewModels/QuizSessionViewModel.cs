using System.Collections.Generic;
using EduSathi.Models;

namespace EduSathi.ViewModels
{
    public class QuizSessionViewModel
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;

        // This holds the generated questions to display on the quiz page
        public List<Question> Questions { get; set; } = new List<Question>();
    }
}