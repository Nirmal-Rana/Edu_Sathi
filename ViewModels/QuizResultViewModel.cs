namespace EduSathi.ViewModels
{
    public class QuizResultViewModel
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
    }
}