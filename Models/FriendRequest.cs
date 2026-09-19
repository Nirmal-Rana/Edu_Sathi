using System;

namespace EduSathi.Models
{
    public enum FriendRequestStatus
    {
        Pending,
        Accepted,
        Declined
    }

    // A single row is both the pending request and, once Status is Accepted, the
    // friendship itself - there's no separate Friendship table. "My Friends List"
    // is just rows where the current user is either side and Status == Accepted.
    public class FriendRequest
    {
        public int Id { get; set; }

        public string RequesterUserId { get; set; } = string.Empty;
        public ApplicationUser Requester { get; set; } = null!;

        public string AddresseeUserId { get; set; } = string.Empty;
        public ApplicationUser Addressee { get; set; } = null!;

        public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RespondedAt { get; set; }
    }
}