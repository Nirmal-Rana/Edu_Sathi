using System;
using System.Collections.Generic;

namespace EduSathi.Models
{
    // Solo = private practice, questions generated for one person only.
    // Global = a shareable live room other people join with a code and compete in.
    public enum RoomCategory
    {
        Solo,
        Global
    }

    public class QuizRoom
    {
        public int Id { get; set; }

        // 6-character shareable code (e.g. "9C283C"). Only set for Global rooms -
        // Solo rooms have no one to share a code with, so this stays empty for them.
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string CreatorUserId { get; set; } = string.Empty;

        public RoomCategory Category { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Null until the host presses "Start Live Quiz" (Global) or the moment the
        // room is created (Solo, which starts itself - see QuestionnairesController).
        public DateTime? StartedAt { get; set; }

        public bool IsStarted => StartedAt.HasValue;

        public ICollection<QuizRoomDocument> Documents { get; set; } = new List<QuizRoomDocument>();
        public ICollection<QuizRoomParticipant> Participants { get; set; } = new List<QuizRoomParticipant>();
    }

    // A room can pull questions from more than one uploaded PDF ("Upload multiple
    // PDFs" on Create Custom), so this is a join table rather than a single FK.
    public class QuizRoomDocument
    {
        public int Id { get; set; }

        public int QuizRoomId { get; set; }
        public QuizRoom QuizRoom { get; set; } = null!;

        public int UploadedDocumentId { get; set; }
        public UploadedDocument UploadedDocument { get; set; } = null!;
    }

    public class QuizRoomParticipant
    {
        public int Id { get; set; }

        public int QuizRoomId { get; set; }
        public QuizRoom QuizRoom { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Null until this participant finishes the quiz.
        public int? Score { get; set; }
        public int? TotalQuestions { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}