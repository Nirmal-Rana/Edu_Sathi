using EduSathi.Models;

public class FriendsViewModel
{
    public List<Friendship> ReceivedRequests { get; set; } = new();
    public List<Friendship> SentRequests { get; set; } = new();
    public List<Friendship> Friends { get; set; } = new();
}