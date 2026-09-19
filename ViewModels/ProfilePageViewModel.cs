namespace EduSathi.ViewModels
{
    /// <summary>
    /// NEW FILE. Backs Views/Profile/Index.cshtml.
    ///
    /// Only fields that exist today are here. Profile.aspx also showed "Avg. score"
    /// and "Day streak"; there is no attempt/score table in the database, so those
    /// are rendered as "—" in the view rather than invented. Add an attempt table
    /// and they become real numbers with no view change beyond swapping the dash.
    /// </summary>
    public class ProfilePageViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;

        public int DocumentCount { get; set; }
        public int QuestionCount { get; set; }

        public string Initials
        {
            get
            {
                var result = "";
                foreach (var part in DisplayName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    if (result.Length >= 2) break;
                    result += char.ToUpperInvariant(part[0]);
                }
                return result.Length == 0 ? "?" : result;
            }
        }
    }
}
