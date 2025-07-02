namespace Blackboard.Core.Models;

public class UserSession
{
    public string Id { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;

    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
    public bool IsValid => IsActive && !IsExpired;
}
