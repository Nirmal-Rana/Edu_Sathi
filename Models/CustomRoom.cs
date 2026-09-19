using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduSathi.Models
{
    public class CustomRoom
    {
        public int Id { get; set; }
        [Required]
        public string RoomCode { get; set; } = string.Empty;
        public string CreatorId { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<RoomParticipant> Participants { get; set; } = new();
    }

    public class RoomParticipant
    {
        public int Id { get; set; }
        public int CustomRoomId { get; set; }
        public CustomRoom CustomRoom { get; set; } = null!;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Score { get; set; } = 0;
        public bool HasSubmitted { get; set; } = false;
    }
}