
namespace EduSathi.Models
{
    public class Question
    {
        public int Id { get; set; }
        public int UploadedDocumentId { get; set; }
        public UploadedDocument UploadedDocument { get; set; } = null!;

        public string QuestionText { get; set; } = string.Empty;
        public string OptionA { get; set; } = string.Empty;
        public string OptionB { get; set; } = string.Empty;
        public string OptionC { get; set; } = string.Empty;
        public string OptionD { get; set; } = string.Empty;
        public string CorrectOption { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public QuestionLevel Level { get; set; }
    }

    public enum QuestionLevel
    {
        Basic,
        Medium,
        Hard
    }
}