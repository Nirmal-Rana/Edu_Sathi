using System.Text.Json.Serialization;

namespace EduSathi.Models
{
    public class Question
    {
        public int Id { get; set; }
        public int UploadedDocumentId { get; set; }
        public UploadedDocument UploadedDocument { get; set; } = null!;

        [JsonPropertyName("questionText")]
        [JsonInclude]
        public string QuestionText { get; set; } = string.Empty;

        [JsonPropertyName("optionA")]
        [JsonInclude]
        public string OptionA { get; set; } = string.Empty;

        [JsonPropertyName("optionB")]
        [JsonInclude]
        public string OptionB { get; set; } = string.Empty;

        [JsonPropertyName("optionC")]
        [JsonInclude]
        public string OptionC { get; set; } = string.Empty;

        [JsonPropertyName("optionD")]
        [JsonInclude]
        public string OptionD { get; set; } = string.Empty;

        [JsonPropertyName("correctOption")]
        [JsonInclude]
        public string CorrectOption { get; set; } = string.Empty;

        [JsonPropertyName("explanation")]
        [JsonInclude]
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