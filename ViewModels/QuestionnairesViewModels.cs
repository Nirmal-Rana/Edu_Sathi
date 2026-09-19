using EduSathi.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EduSathi.ViewModels
{
    /// <summary>Backs Views/Questionnaires/CreateCustom.cshtml.</summary>
    public class CreateRoomViewModel
    {
        public string Name { get; set; } = string.Empty;

        // Multiple PDFs can be dropped in from the device at once.
        public List<IFormFile>? NewPdfFiles { get; set; }

        // And/or multiple previously uploaded PDFs can be picked from storage.
        public List<int> SelectedExistingDocumentIds { get; set; } = new List<int>();

        public RoomCategory Category { get; set; } = RoomCategory.Solo;

        // "Number of Questions to Generate" - clamped server-side to 1..30.
        public int QuestionCount { get; set; } = 5;

        // Checked friends (Global rooms only) get added as participants immediately,
        // without needing the room code.
        public List<string> InviteFriendUserIds { get; set; } = new List<string>();
        public List<UploadedDocument> PreviousDocuments { get; set; } = new List<UploadedDocument>();
        public List<FriendSummary> Friends { get; set; } = new List<FriendSummary>();
    }

    /// <summary>Backs Views/Questionnaires/RoomLobby.cshtml.</summary>
    public class RoomLobbyViewModel
    {
        public int RoomId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public bool IsStarted { get; set; }
        public List<QuizRoomParticipant> Participants { get; set; } = new List<QuizRoomParticipant>();
    }

    /// <summary>
    /// Backs Views/Questionnaires/RoomQuiz.cshtml. Same shape as ExamController's
    /// QuizAttemptViewModel, but the question set is combined from every document
    /// attached to the room (rather than one document/level), and it carries a
    /// leaderboard for Global rooms once people start finishing.
    /// </summary>
    public class RoomQuizViewModel
    {
        public int RoomId { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public RoomCategory Category { get; set; }

        public List<Question> Questions { get; set; } = new List<Question>();
        public Dictionary<int, string> Answers { get; set; } = new Dictionary<int, string>();

        public bool IsSubmitted { get; set; }
        public int CorrectCount { get; set; }

        public int Total => Questions.Count;
        public int PercentCorrect => Total > 0 ? (int)Math.Round(100.0 * CorrectCount / Total) : 0;
        public int XpEarned => CorrectCount * 10;

        public List<LeaderboardEntry> Leaderboard { get; set; } = new List<LeaderboardEntry>();

        public static IEnumerable<(string Letter, string Text)> OptionsOf(Question q)
        {
            if (!string.IsNullOrWhiteSpace(q.OptionA)) yield return ("A", q.OptionA);
            if (!string.IsNullOrWhiteSpace(q.OptionB)) yield return ("B", q.OptionB);
            if (!string.IsNullOrWhiteSpace(q.OptionC)) yield return ("C", q.OptionC);
            if (!string.IsNullOrWhiteSpace(q.OptionD)) yield return ("D", q.OptionD);
        }
    }

    public class LeaderboardEntry
    {
        public string DisplayName { get; set; } = string.Empty;
        public bool IsHost { get; set; }
        public int? Score { get; set; }
        public int? Total { get; set; }
        public bool Finished => Score.HasValue;
    }

    /// <summary>Backs Views/Questionnaires/MyQuizzes.cshtml - one row per room the user created or joined.</summary>
    public class MyQuizRow
    {
        public int RoomId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public RoomCategory Category { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsStarted { get; set; }
        public bool IsHost { get; set; }
        public int ParticipantCount { get; set; }
        public int? MyScore { get; set; }
        public int? MyTotal { get; set; }
        public bool MyCompleted => MyScore.HasValue;
    }
}