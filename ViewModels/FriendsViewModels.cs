using System.Collections.Generic;
using EduSathi.Models;

namespace EduSathi.ViewModels
{
    /// <summary>Backs Views/Friends/Index.cshtml.</summary>
    public class FriendsIndexViewModel
    {
        public List<FriendRequest> ReceivedPending { get; set; } = new List<FriendRequest>();
        public List<FriendRequest> SentPending { get; set; } = new List<FriendRequest>();
        public List<FriendSummary> Friends { get; set; } = new List<FriendSummary>();
    }

    /// <summary>
    /// A trimmed-down view of a friend - reused on Friends/Index and on the
    /// Create Custom "Invite Friends Directly" checkbox list.
    /// </summary>
    public class FriendSummary
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}