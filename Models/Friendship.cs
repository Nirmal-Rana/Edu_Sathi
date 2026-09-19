namespace EduSathi.Models
{
    public class Friendship
    {
        public int Id { get; set; }

       
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        
        public string FriendId { get; set; } = string.Empty;
        public ApplicationUser Friend { get; set; } = null!;

        public bool IsAccepted { get; set; } = true; 
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    }
}