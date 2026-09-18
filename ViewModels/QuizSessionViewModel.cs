***REMOVED***using EduSathi.Models;
using System.Collections.Generic;

namespace EduSathi.ViewModels
{
    public class QuizSessionViewModel
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;

        // Categorized questions for tiered difficulty levels
        public List<Question> BasicQuestions { get; set; } = new List<Question>();
        public List<Question> MediumQuestions { get; set; } = new List<Question>();
        public List<Question> HardQuestions { get; set; } = new List<Question>();
    }
}