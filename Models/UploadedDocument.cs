using System;
using System.Collections.Generic;

namespace EduSathi.Models
{
    public class UploadedDocument
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ExtractedText { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}