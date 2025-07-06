namespace Blackboard.Core.Models;

public class MessagePreferences
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public bool AllowPrivateMessages { get; set; } = true;
    public bool NotifyOnNewMessage { get; set; } = true;
    public bool NotifyOnPublicReply { get; set; } = true;
    public bool ShowSignature { get; set; } = true;
    public string? Signature { get; set; }
    public int MessageQuotaDaily { get; set; } = 100;
    public int MessageQuotaMonthly { get; set; } = 3000;
    public bool EnableAnsiEditor { get; set; } = true;
    public bool AutoMarkRead { get; set; } = false;
    public string? BlockedUsers { get; set; } // JSON array of user IDs
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}