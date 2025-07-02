using Blackboard.Core.Configuration;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public interface IUserService
{
    Task<UserProfileDto?> RegisterUserAsync(UserRegistrationDto registration, string? ipAddress = null, string? userAgent = null);
    Task<(UserProfileDto? User, UserSession? Session)> LoginAsync(UserLoginDto login);
    Task<bool> LogoutAsync(string sessionId, string? ipAddress = null);
    Task<UserProfileDto?> GetUserByIdAsync(int userId);
    Task<UserProfileDto?> GetUserByHandleAsync(string handle);
    Task<bool> UpdateUserProfileAsync(int userId, UserUpdateDto update, string? ipAddress = null);
    Task<bool> ChangePasswordAsync(int userId, PasswordChangeDto passwordChange, string? ipAddress = null);
    Task<bool> ResetPasswordAsync(string handle, string? ipAddress = null);
    Task<bool> LockUserAsync(int userId, TimeSpan? duration = null, string? reason = null, int? adminUserId = null, string? ipAddress = null);
    Task<bool> UnlockUserAsync(int userId, int? adminUserId = null, string? ipAddress = null);
    Task<bool> SetUserSecurityLevelAsync(int userId, SecurityLevel securityLevel, int adminUserId, string? ipAddress = null);
    Task<IEnumerable<UserProfileDto>> GetUsersAsync(int skip = 0, int take = 50);
    Task<IEnumerable<UserProfileDto>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 50);
    Task<bool> ValidatePreEnterCodeAsync(string? preEnterCode);
}

public class UserService : IUserService
{
    private readonly DatabaseManager _database;
    private readonly IPasswordService _passwordService;
    private readonly ISessionService _sessionService;
    private readonly IAuditService _auditService;
    private readonly SecuritySettings _securitySettings;
    private readonly ILogger _logger;

    public UserService(
        DatabaseManager database,
        IPasswordService passwordService,
        ISessionService sessionService,
        IAuditService auditService,
        SecuritySettings securitySettings,
        ILogger logger)
    {
        _database = database;
        _passwordService = passwordService;
        _sessionService = sessionService;
        _auditService = auditService;
        _securitySettings = securitySettings;
        _logger = logger;
    }

    public async Task<UserProfileDto?> RegisterUserAsync(UserRegistrationDto registration, string? ipAddress = null, string? userAgent = null)
    {
        // Validate pre-enter code if provided
        if (!string.IsNullOrEmpty(registration.PreEnterCode) && !await ValidatePreEnterCodeAsync(registration.PreEnterCode))
        {
            _logger.Warning("Invalid pre-enter code provided during registration: {Handle}", registration.Handle);
            return null;
        }

        // Validate password complexity
        if (!_passwordService.ValidatePasswordComplexity(registration.Password, _securitySettings))
        {
            _logger.Warning("Password complexity validation failed for registration: {Handle}", registration.Handle);
            return null;
        }

        // Check if handle already exists
        var existingUser = await GetUserByHandleAsync(registration.Handle);
        if (existingUser != null)
        {
            _logger.Warning("Attempted registration with existing handle: {Handle}", registration.Handle);
            return null;
        }

        // Check if email already exists (if provided)
        if (!string.IsNullOrEmpty(registration.Email))
        {
            var userWithEmail = await GetUserByEmailAsync(registration.Email);
            if (userWithEmail != null)
            {
                _logger.Warning("Attempted registration with existing email: {Email}", registration.Email);
                return null;
            }
        }

        try
        {
            var salt = _passwordService.GenerateSalt();
            var passwordHash = _passwordService.HashPassword(registration.Password, salt);
            var passwordExpiry = _securitySettings.PasswordExpirationDays > 0 
                ? DateTime.UtcNow.AddDays(_securitySettings.PasswordExpirationDays) 
                : (DateTime?)null;

            const string sql = @"
                INSERT INTO Users (Handle, Email, PasswordHash, Salt, FirstName, LastName, Location, PhoneNumber, 
                                 SecurityLevel, IsActive, CreatedAt, UpdatedAt, PasswordExpiresAt)
                VALUES (@Handle, @Email, @PasswordHash, @Salt, @FirstName, @LastName, @Location, @PhoneNumber, 
                        @SecurityLevel, @IsActive, @CreatedAt, @UpdatedAt, @PasswordExpiresAt);
                SELECT last_insert_rowid();";

            var userId = await _database.QueryFirstAsync<int>(sql, new
            {
                registration.Handle,
                registration.Email,
                PasswordHash = passwordHash,
                Salt = salt,
                registration.FirstName,
                registration.LastName,
                registration.Location,
                registration.PhoneNumber,
                SecurityLevel = (int)SecurityLevel.User,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                PasswordExpiresAt = passwordExpiry
            });

            await _auditService.LogAsync("USER_REGISTERED", userId, "User", userId.ToString(), 
                ipAddress: ipAddress, userAgent: userAgent);

            _logger.Information("User registered successfully: {Handle} (ID: {UserId})", registration.Handle, userId);

            return await GetUserByIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to register user: {Handle}", registration.Handle);
            return null;
        }
    }

    public async Task<(UserProfileDto? User, UserSession? Session)> LoginAsync(UserLoginDto login)
    {
        try
        {
            var user = await GetUserByHandleInternalAsync(login.Handle);
            if (user == null)
            {
                _logger.Warning("Login attempt with non-existent handle: {Handle} from {IpAddress}", 
                    login.Handle, login.IpAddress);
                return (null, null);
            }

            // Check if user is locked
            if (user.IsLocked)
            {
                _logger.Warning("Login attempt for locked user: {Handle} from {IpAddress}", 
                    login.Handle, login.IpAddress);
                await _auditService.LogAsync("LOGIN_ATTEMPT_LOCKED_USER", user.Id, "User", user.Id.ToString(), 
                    ipAddress: login.IpAddress, userAgent: login.UserAgent);
                return (null, null);
            }

            // Check if user is active
            if (!user.IsActive)
            {
                _logger.Warning("Login attempt for inactive user: {Handle} from {IpAddress}", 
                    login.Handle, login.IpAddress);
                return (null, null);
            }

            // Verify password
            if (!_passwordService.VerifyPassword(login.Password, user.PasswordHash, user.Salt))
            {
                await HandleFailedLoginAsync(user, login.IpAddress, login.UserAgent);
                return (null, null);
            }

            // Reset failed login attempts on successful login
            if (user.FailedLoginAttempts > 0)
            {
                await ResetFailedLoginAttemptsAsync(user.Id);
            }

            // Update last login time
            await UpdateLastLoginAsync(user.Id);

            // Create session
            var session = await _sessionService.CreateSessionAsync(user.Id, login.IpAddress, login.UserAgent);

            await _auditService.LogAsync("USER_LOGIN_SUCCESS", user.Id, "User", user.Id.ToString(), 
                ipAddress: login.IpAddress, userAgent: login.UserAgent);

            _logger.Information("User logged in successfully: {Handle} from {IpAddress}", 
                user.Handle, login.IpAddress);

            var userProfile = MapToUserProfile(user);
            return (userProfile, session);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process login for {Handle}", login.Handle);
            return (null, null);
        }
    }

    public async Task<bool> LogoutAsync(string sessionId, string? ipAddress = null)
    {
        var session = await _sessionService.GetSessionAsync(sessionId);
        if (session == null)
        {
            return false;
        }

        var success = await _sessionService.EndSessionAsync(sessionId);
        if (success)
        {
            await _auditService.LogAsync("USER_LOGOUT", session.UserId, "User", session.UserId.ToString(), 
                ipAddress: ipAddress);
            _logger.Information("User logged out: Session {SessionId}", sessionId);
        }

        return success;
    }

    public async Task<UserProfileDto?> GetUserByIdAsync(int userId)
    {
        const string sql = @"
            SELECT Id, Handle, Email, FirstName, LastName, Location, PhoneNumber, SecurityLevel, 
                   IsActive, LastLoginAt, CreatedAt, UpdatedAt, PasswordExpiresAt, FailedLoginAttempts, LockedUntil
            FROM Users 
            WHERE Id = @UserId";

        var users = await _database.QueryAsync<User>(sql, new { UserId = userId });
        var user = users.FirstOrDefault();
        
        return user != null ? MapToUserProfile(user) : null;
    }

    public async Task<UserProfileDto?> GetUserByHandleAsync(string handle)
    {
        var user = await GetUserByHandleInternalAsync(handle);
        return user != null ? MapToUserProfile(user) : null;
    }

    private async Task<User?> GetUserByHandleInternalAsync(string handle)
    {
        const string sql = @"
            SELECT Id, Handle, Email, PasswordHash, Salt, FirstName, LastName, Location, PhoneNumber, 
                   SecurityLevel, IsActive, LastLoginAt, CreatedAt, UpdatedAt, PasswordExpiresAt, 
                   FailedLoginAttempts, LockedUntil
            FROM Users 
            WHERE Handle = @Handle COLLATE NOCASE";

        var users = await _database.QueryAsync<User>(sql, new { Handle = handle });
        return users.FirstOrDefault();
    }

    private async Task<User?> GetUserByIdInternalAsync(int userId)
    {
        const string sql = @"
            SELECT Id, Handle, Email, PasswordHash, Salt, FirstName, LastName, Location, PhoneNumber, 
                   SecurityLevel, IsActive, LastLoginAt, CreatedAt, UpdatedAt, PasswordExpiresAt, 
                   FailedLoginAttempts, LockedUntil
            FROM Users 
            WHERE Id = @UserId";

        var users = await _database.QueryAsync<User>(sql, new { UserId = userId });
        return users.FirstOrDefault();
    }

    private async Task<User?> GetUserByEmailAsync(string email)
    {
        const string sql = @"
            SELECT Id, Handle, Email, PasswordHash, Salt, FirstName, LastName, Location, PhoneNumber, 
                   SecurityLevel, IsActive, LastLoginAt, CreatedAt, UpdatedAt, PasswordExpiresAt, 
                   FailedLoginAttempts, LockedUntil
            FROM Users 
            WHERE Email = @Email COLLATE NOCASE";

        var users = await _database.QueryAsync<User>(sql, new { Email = email });
        return users.FirstOrDefault();
    }

    public async Task<bool> UpdateUserProfileAsync(int userId, UserUpdateDto update, string? ipAddress = null)
    {
        var existingUser = await GetUserByIdAsync(userId);
        if (existingUser == null)
            return false;

        try
        {
            const string sql = @"
                UPDATE Users 
                SET Email = @Email, FirstName = @FirstName, LastName = @LastName, 
                    Location = @Location, PhoneNumber = @PhoneNumber, UpdatedAt = @UpdatedAt
                WHERE Id = @UserId";

            var rowsAffected = await _database.ExecuteAsync(sql, new
            {
                UserId = userId,
                update.Email,
                update.FirstName,
                update.LastName,
                update.Location,
                update.PhoneNumber,
                UpdatedAt = DateTime.UtcNow
            });

            if (rowsAffected > 0)
            {
                await _auditService.LogEntityChangeAsync<object>(userId, "USER_PROFILE_UPDATED", userId.ToString(), 
                    existingUser, update, ipAddress);
                _logger.Information("User profile updated: {UserId}", userId);
            }

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update user profile: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ChangePasswordAsync(int userId, PasswordChangeDto passwordChange, string? ipAddress = null)
    {
        var user = await GetUserByIdInternalAsync(userId); // We need to get the full user with password
        if (user == null)
            return false;

        // Verify current password
        if (!_passwordService.VerifyPassword(passwordChange.CurrentPassword, user.PasswordHash, user.Salt))
        {
            _logger.Warning("Invalid current password provided for password change: {UserId}", userId);
            return false;
        }

        // Validate new password complexity
        if (!_passwordService.ValidatePasswordComplexity(passwordChange.NewPassword, _securitySettings))
        {
            _logger.Warning("New password complexity validation failed: {UserId}", userId);
            return false;
        }

        try
        {
            var newSalt = _passwordService.GenerateSalt();
            var newPasswordHash = _passwordService.HashPassword(passwordChange.NewPassword, newSalt);
            var passwordExpiry = _securitySettings.PasswordExpirationDays > 0 
                ? DateTime.UtcNow.AddDays(_securitySettings.PasswordExpirationDays) 
                : (DateTime?)null;

            const string sql = @"
                UPDATE Users 
                SET PasswordHash = @PasswordHash, Salt = @Salt, PasswordExpiresAt = @PasswordExpiresAt, 
                    UpdatedAt = @UpdatedAt
                WHERE Id = @UserId";

            var rowsAffected = await _database.ExecuteAsync(sql, new
            {
                UserId = userId,
                PasswordHash = newPasswordHash,
                Salt = newSalt,
                PasswordExpiresAt = passwordExpiry,
                UpdatedAt = DateTime.UtcNow
            });

            if (rowsAffected > 0)
            {
                await _auditService.LogAsync("PASSWORD_CHANGED", userId, "User", userId.ToString(), 
                    ipAddress: ipAddress);
                _logger.Information("Password changed for user: {UserId}", userId);
            }

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to change password for user: {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(string handle, string? ipAddress = null)
    {
        var user = await GetUserByHandleInternalAsync(handle);
        if (user == null)
            return false;

        try
        {
            var newPassword = _passwordService.GenerateSecurePassword();
            var newSalt = _passwordService.GenerateSalt();
            var newPasswordHash = _passwordService.HashPassword(newPassword, newSalt);

            const string sql = @"
                UPDATE Users 
                SET PasswordHash = @PasswordHash, Salt = @Salt, PasswordExpiresAt = @PasswordExpiresAt, 
                    FailedLoginAttempts = 0, LockedUntil = NULL, UpdatedAt = @UpdatedAt
                WHERE Id = @UserId";

            var rowsAffected = await _database.ExecuteAsync(sql, new
            {
                UserId = user.Id,
                PasswordHash = newPasswordHash,
                Salt = newSalt,
                PasswordExpiresAt = DateTime.UtcNow.AddDays(1), // Force password change in 1 day
                UpdatedAt = DateTime.UtcNow
            });

            if (rowsAffected > 0)
            {
                await _auditService.LogAsync("PASSWORD_RESET", user.Id, "User", user.Id.ToString(), 
                    ipAddress: ipAddress);
                _logger.Information("Password reset for user: {Handle}. New password: {Password}", handle, newPassword);
                // In a real system, you'd email this or display it securely
            }

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to reset password for user: {Handle}", handle);
            return false;
        }
    }

    public async Task<bool> LockUserAsync(int userId, TimeSpan? duration = null, string? reason = null, int? adminUserId = null, string? ipAddress = null)
    {
        var lockUntil = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : DateTime.UtcNow.AddDays(365);

        const string sql = @"
            UPDATE Users 
            SET LockedUntil = @LockedUntil, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        var rowsAffected = await _database.ExecuteAsync(sql, new
        {
            UserId = userId,
            LockedUntil = lockUntil,
            UpdatedAt = DateTime.UtcNow
        });

        if (rowsAffected > 0)
        {
            // End all active sessions for the locked user
            await _sessionService.EndAllUserSessionsAsync(userId);

            await _auditService.LogAsync("USER_LOCKED", adminUserId, "User", userId.ToString(), 
                newValues: new { Reason = reason, LockedUntil = lockUntil }, ipAddress: ipAddress);
            _logger.Information("User locked: {UserId} until {LockedUntil} by {AdminUserId}", 
                userId, lockUntil, adminUserId);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> UnlockUserAsync(int userId, int? adminUserId = null, string? ipAddress = null)
    {
        const string sql = @"
            UPDATE Users 
            SET LockedUntil = NULL, FailedLoginAttempts = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        var rowsAffected = await _database.ExecuteAsync(sql, new
        {
            UserId = userId,
            UpdatedAt = DateTime.UtcNow
        });

        if (rowsAffected > 0)
        {
            await _auditService.LogAsync("USER_UNLOCKED", adminUserId, "User", userId.ToString(), 
                ipAddress: ipAddress);
            _logger.Information("User unlocked: {UserId} by {AdminUserId}", userId, adminUserId);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> SetUserSecurityLevelAsync(int userId, SecurityLevel securityLevel, int adminUserId, string? ipAddress = null)
    {
        var existingUser = await GetUserByIdAsync(userId);
        if (existingUser == null)
            return false;

        const string sql = @"
            UPDATE Users 
            SET SecurityLevel = @SecurityLevel, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        var rowsAffected = await _database.ExecuteAsync(sql, new
        {
            UserId = userId,
            SecurityLevel = (int)securityLevel,
            UpdatedAt = DateTime.UtcNow
        });

        if (rowsAffected > 0)
        {
            await _auditService.LogEntityChangeAsync<object>(adminUserId, "USER_SECURITY_LEVEL_CHANGED", userId.ToString(), 
                new { SecurityLevel = existingUser.SecurityLevel }, 
                new { SecurityLevel = securityLevel }, ipAddress);
            _logger.Information("User security level changed: {UserId} from {OldLevel} to {NewLevel} by {AdminUserId}", 
                userId, existingUser.SecurityLevel, securityLevel, adminUserId);
        }

        return rowsAffected > 0;
    }

    public async Task<IEnumerable<UserProfileDto>> GetUsersAsync(int skip = 0, int take = 50)
    {
        const string sql = @"
            SELECT Id, Handle, Email, FirstName, LastName, Location, PhoneNumber, SecurityLevel, 
                   IsActive, LastLoginAt, CreatedAt, UpdatedAt, PasswordExpiresAt, FailedLoginAttempts, LockedUntil
            FROM Users 
            ORDER BY Handle
            LIMIT @Take OFFSET @Skip";

        var users = await _database.QueryAsync<User>(sql, new { Skip = skip, Take = take });
        return users.Select(MapToUserProfile);
    }

    public async Task<IEnumerable<UserProfileDto>> SearchUsersAsync(string searchTerm, int skip = 0, int take = 50)
    {
        const string sql = @"
            SELECT Id, Handle, Email, FirstName, LastName, Location, PhoneNumber, SecurityLevel, 
                   IsActive, LastLoginAt, CreatedAt, UpdatedAt, PasswordExpiresAt, FailedLoginAttempts, LockedUntil
            FROM Users 
            WHERE Handle LIKE @SearchTerm 
               OR FirstName LIKE @SearchTerm 
               OR LastName LIKE @SearchTerm 
               OR Email LIKE @SearchTerm
            ORDER BY Handle
            LIMIT @Take OFFSET @Skip";

        var users = await _database.QueryAsync<User>(sql, new 
        { 
            SearchTerm = $"%{searchTerm}%", 
            Skip = skip, 
            Take = take 
        });
        
        return users.Select(MapToUserProfile);
    }

    public async Task<bool> ValidatePreEnterCodeAsync(string? preEnterCode)
    {
        if (string.IsNullOrEmpty(preEnterCode))
            return true; // No pre-enter code required

        // Check if pre-enter code exists in runtime configuration
        const string sql = @"
            SELECT Value FROM RuntimeConfiguration 
            WHERE Key = 'PreEnterCode' AND Value = @PreEnterCode";

        var result = await _database.QueryFirstOrDefaultAsync<string>(sql, new { PreEnterCode = preEnterCode });
        return result != null;
    }

    private async Task HandleFailedLoginAsync(User user, string? ipAddress, string? userAgent)
    {
        var newFailedAttempts = user.FailedLoginAttempts + 1;
        DateTime? lockUntil = null;

        if (newFailedAttempts >= _securitySettings.MaxLoginAttempts)
        {
            lockUntil = DateTime.UtcNow.AddMinutes(_securitySettings.LockoutDurationMinutes);
        }

        const string sql = @"
            UPDATE Users 
            SET FailedLoginAttempts = @FailedLoginAttempts, LockedUntil = @LockedUntil, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        await _database.ExecuteAsync(sql, new
        {
            UserId = user.Id,
            FailedLoginAttempts = newFailedAttempts,
            LockedUntil = lockUntil,
            UpdatedAt = DateTime.UtcNow
        });

        await _auditService.LogAsync("LOGIN_FAILED", user.Id, "User", user.Id.ToString(), 
            ipAddress: ipAddress, userAgent: userAgent);

        _logger.Warning("Failed login attempt {Attempt}/{MaxAttempts} for user {Handle} from {IpAddress}. {LockStatus}", 
            newFailedAttempts, _securitySettings.MaxLoginAttempts, user.Handle, ipAddress,
            lockUntil.HasValue ? $"User locked until {lockUntil}" : "User not locked");
    }

    private async Task ResetFailedLoginAttemptsAsync(int userId)
    {
        const string sql = @"
            UPDATE Users 
            SET FailedLoginAttempts = 0, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        await _database.ExecuteAsync(sql, new
        {
            UserId = userId,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private async Task UpdateLastLoginAsync(int userId)
    {
        const string sql = @"
            UPDATE Users 
            SET LastLoginAt = @LastLoginAt, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        await _database.ExecuteAsync(sql, new
        {
            UserId = userId,
            LastLoginAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }

    private static UserProfileDto MapToUserProfile(User user)
    {
        return new UserProfileDto
        {
            Id = user.Id,
            Handle = user.Handle,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Location = user.Location,
            PhoneNumber = user.PhoneNumber,
            SecurityLevel = user.SecurityLevel,
            IsActive = user.IsActive,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            IsLocked = user.IsLocked,
            IsPasswordExpired = user.IsPasswordExpired,
            DisplayName = user.DisplayName
        };
    }
}
