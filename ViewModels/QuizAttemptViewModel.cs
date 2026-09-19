using EduSathi.Models;
using System.Collections.Generic;

namespace EduSathi.ViewModels
{
    /// <summary>
    /// NEW FILE. Backs Views/Exam/Quiz.cshtml.
    ///
    /// QuizSessionViewModel lists questions but has nowhere to record what the user
    /// picked or whether they were right, because the old QuizSession.cshtml never
    /// let anyone answer. This carries one difficulty level's worth of questions
    /// plus the graded result.
    ///
    /// Nothing here is persisted yet — grading happens in ExamController.Quiz and
    /// is thrown away on the next request. See the TODO in that action for where an
    /// attempt/score table would plug in.
    /// </summary>
    public class QuizAttemptViewModel
    {
        public int DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public QuestionLevel Level { get; set; }

        public List<Question> Questions { get; set; } = new List<Question>();

        /// <summary>Question.Id -> the option letter the user picked ("A".."D").</summary>
        public Dictionary<int, string> Answers { get; set; } = new Dictionary<int, string>();

        public bool IsSubmitted { get; set; }
        public int CorrectCount { get; set; }

        public int Total => Questions.Count;
        public int PercentCorrect => Total > 0 ? (int)System.Math.Round(100.0 * CorrectCount / Total) : 0;
        public int XpEarned => CorrectCount * 10;

        /// <summary>Label shown in the heading. Question levels are Basic/Medium/Hard.</summary>
        public string LevelLabel => Level.ToString();

        /// <summary>The four options of a question as (letter, text) pairs, skipping blanks.</summary>
        public static IEnumerable<(string Letter, string Text)> OptionsOf(Question q)
        {
            if (!string.IsNullOrWhiteSpace(q.OptionA)) yield return ("A", q.OptionA);
            if (!string.IsNullOrWhiteSpace(q.OptionB)) yield return ("B", q.OptionB);
            if (!string.IsNullOrWhiteSpace(q.OptionC)) yield return ("C", q.OptionC);
            if (!string.IsNullOrWhiteSpace(q.OptionD)) yield return ("D", q.OptionD);
        }
    }
}
