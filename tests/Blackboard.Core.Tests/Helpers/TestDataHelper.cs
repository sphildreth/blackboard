using Blackboard.Core.Configuration;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;
using Blackboard.Data;

namespace Blackboard.Core.Tests.Helpers;

public static class TestDataHelper
{
    public static UserRegistrationDto CreateValidUserRegistration(string handle = "testuser")
    {
        return new UserRegistrationDto
        {
            Handle = handle,
            Email = $"{handle}@test.com",
            Password = "SecurePass123!",
            FirstName = "Test",
            LastName = "User",
            Location = "Test City, TC",
            PhoneNumber = "555-0123"
        };
    }

    public static UserLoginDto CreateValidUserLogin(string handle = "testuser", string password = "SecurePass123!")
    {
        return new UserLoginDto
        {
            Handle = handle,
            Password = password,
            IpAddress = "127.0.0.1",
            UserAgent = "Test Client"
        };
    }

    public static UserUpdateDto CreateUserUpdate()
    {
        return new UserUpdateDto
        {
            Email = "updated@test.com",
            FirstName = "Updated",
            LastName = "User",
            Location = "Updated City, UC",
            PhoneNumber = "555-9999"
        };
    }

    public static PasswordChangeDto CreatePasswordChange(string currentPassword = "SecurePass123!", string newPassword = "NewPass456!")
    {
        return new PasswordChangeDto
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        };
    }

    public static SecuritySettings CreateSecuritySettings()
    {
        return new SecuritySettings
        {
            MaxLoginAttempts = 3,
            LockoutDurationMinutes = 30,
            PasswordMinLength = 8,
            RequirePasswordComplexity = true,
            PasswordExpirationDays = 90,
            EnableAuditLogging = true,
            EnableEncryption = true
        };
    }

    public static SystemConfiguration CreateTestSystemConfiguration()
    {
        return new SystemConfiguration
        {
            Database = new DatabaseSettings
            {
                ConnectionString = "Data Source=:memory:",
                EnableWalMode = false
            },
            Security = CreateSecuritySettings(),
            Network = new NetworkSettings
            {
                TelnetPort = 2323,
                MaxConcurrentConnections = 100
            },
            Logging = new LoggingSettings
            {
                LogLevel = "Information",
                EnableFileLogging = false,
                EnableConsoleLogging = true
            }
        };
    }

    public static async Task<User> CreateTestUserAsync(DatabaseManager databaseManager, string handle = "testuser", int id = 1)
    {
        var user = new User
        {
            Id = id,
            Handle = handle,
            Email = $"{handle}@test.com",
            FirstName = "Test",
            LastName = "User",
            Location = "Test City, TC",
            PhoneNumber = "555-0123",
            PasswordHash = "dummy_hash",
            Salt = "dummy_salt",
            SecurityLevel = SecurityLevel.User,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastLoginAt = null,
            PasswordExpiresAt = DateTime.UtcNow.AddDays(90),
            FailedLoginAttempts = 0,
            LockedUntil = null
        };

        const string sql = @"
            INSERT INTO Users (Id, Handle, Email, FirstName, LastName, Location, PhoneNumber, 
                             PasswordHash, Salt, SecurityLevel, IsActive, CreatedAt, UpdatedAt,
                             LastLoginAt, PasswordExpiresAt, FailedLoginAttempts, LockedUntil)
            VALUES (@Id, @Handle, @Email, @FirstName, @LastName, @Location, @PhoneNumber,
                    @PasswordHash, @Salt, @SecurityLevel, @IsActive, @CreatedAt, @UpdatedAt,
                    @LastLoginAt, @PasswordExpiresAt, @FailedLoginAttempts, @LockedUntil)";

        await databaseManager.ExecuteAsync(sql, user);
        return user;
    }
}
