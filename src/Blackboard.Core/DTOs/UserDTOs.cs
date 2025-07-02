using Blackboard.Core.Models;

namespace Blackboard.Core.DTOs;

public class UserRegistrationDto
{
    public string Handle { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Location { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PreEnterCode { get; set; }
}

public class UserLoginDto
{
    public string Handle { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
}

public class UserUpdateDto
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Location { get; set; }
    public string? PhoneNumber { get; set; }
}

public class PasswordChangeDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class UserProfileDto
{
    public int Id { get; set; }
    public string Handle { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Location { get; set; }
    public string? PhoneNumber { get; set; }
    public SecurityLevel SecurityLevel { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsLocked { get; set; }
    public bool IsPasswordExpired { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
