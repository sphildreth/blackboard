namespace Blackboard.Core.Models;

public class User
{
    public int Id { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Location { get; set; }
    public string? PhoneNumber { get; set; }
    public SecurityLevel SecurityLevel { get; set; } = SecurityLevel.User;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PasswordExpiresAt { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }

    public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;
    public bool IsPasswordExpired => PasswordExpiresAt.HasValue && PasswordExpiresAt < DateTime.UtcNow;
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

public enum SecurityLevel
{
    Banned = -1,
    User = 0,
    Trusted = 10,
    Moderator = 50,
    CoSysop = 90,
    Sysop = 100
}
