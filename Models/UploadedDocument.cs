using System;
using System.Collections.Generic;

namespace EduSathi.Models
{
    public enum DocumentStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public class UploadedDocument
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string ExtractedText { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        // --- New fields, added for the mobile app ---

        // Persisted processing status. This is the real fix for "stuck on
        // Processing..." — the mobile app can now poll a real enum instead
        // of inferring state from pages == 0.
        public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

        // Real page count, filled in once PdfPig has parsed the file.
        public int Pages { get; set; } = 0;

        // The AI-generated {overview, keyTakeaways} payload, stored as raw
        // JSON so GET /summary can return it without re-calling the model.
        public string? SummaryJson { get; set; }

        // Set when Status == Failed, so the client/dev can see why.
        public string? ProcessingError { get; set; }

        public ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}