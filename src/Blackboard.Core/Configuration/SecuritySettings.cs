namespace Blackboard.Core.Configuration;

public class SecuritySettings
{
    public int MaxLoginAttempts { get; set; } = 3;
    public int LockoutDurationMinutes { get; set; } = 30;
    public int PasswordMinLength { get; set; } = 8;
    public bool RequirePasswordComplexity { get; set; } = true;

    public bool RequireEmailAddress { get; set; } = true;
    public int PasswordExpirationDays { get; set; } = 90;
    public bool EnableAuditLogging { get; set; } = true;
    public bool EnableEncryption { get; set; } = true;
}