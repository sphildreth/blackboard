using System;

namespace Blackboard.Core.Models
{
    public enum MessageType
    {
        Private,
        Public,
        System
    }

    public class Message
    {
        public int Id { get; set; }
        public int? FromUserId { get; set; }
        public int? ToUserId { get; set; } // null for public/system messages
        public int? BoardId { get; set; } // null for private/system messages
        public int? ThreadId { get; set; } // null for private/system messages
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public bool IsDeleted { get; set; }
        public MessageType MessageType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsApproved { get; set; } = true; // for moderation
        public bool IsSticky { get; set; } = false;
        public bool IsReported { get; set; } = false;
    }
}
