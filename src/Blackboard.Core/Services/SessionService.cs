using System.Security.Cryptography;
using Blackboard.Core.Models;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public interface ISessionService
{
    Task<UserSession> CreateSessionAsync(int userId, string ipAddress, string? userAgent = null);
    Task<UserSession?> GetSessionAsync(string sessionId);
    Task<bool> ValidateSessionAsync(string sessionId);
    Task<bool> ExtendSessionAsync(string sessionId, TimeSpan? extension = null);
    Task<bool> EndSessionAsync(string sessionId);
    Task<bool> EndAllUserSessionsAsync(int userId);
    Task<IEnumerable<UserSession>> GetActiveSessionsAsync(int userId);
    Task<IEnumerable<UserSession>> GetAllActiveSessionsAsync();
    Task CleanupExpiredSessionsAsync();
}

public class SessionService : ISessionService
{
    private readonly DatabaseManager _database;
    private readonly ILogger _logger;
    private readonly TimeSpan _defaultSessionDuration = TimeSpan.FromHours(24);

    public SessionService(DatabaseManager database, ILogger logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<UserSession> CreateSessionAsync(int userId, string ipAddress, string? userAgent = null)
    {
        var sessionId = GenerateSessionId();
        var session = new UserSession
        {
            Id = sessionId,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(_defaultSessionDuration),
            IsActive = true
        };

        const string sql = @"
            INSERT INTO UserSessions (Id, UserId, IpAddress, UserAgent, CreatedAt, ExpiresAt, IsActive)
            VALUES (@Id, @UserId, @IpAddress, @UserAgent, @CreatedAt, @ExpiresAt, @IsActive)";

        await _database.ExecuteAsync(sql, session);
        
        _logger.Information("Created session {SessionId} for user {UserId} from {IpAddress}", 
            sessionId, userId, ipAddress);

        return session;
    }

    public async Task<UserSession?> GetSessionAsync(string sessionId)
    {
        const string sql = @"
            SELECT Id, UserId, IpAddress, UserAgent, CreatedAt, ExpiresAt, IsActive
            FROM UserSessions 
            WHERE Id = @SessionId";

        var sessions = await _database.QueryAsync<UserSession>(sql, new { SessionId = sessionId });
        return sessions.FirstOrDefault();
    }

    public async Task<bool> ValidateSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null || !session.IsValid)
        {
            if (session != null && !session.IsValid)
            {
                await EndSessionAsync(sessionId);
            }
            return false;
        }

        return true;
    }

    public async Task<bool> ExtendSessionAsync(string sessionId, TimeSpan? extension = null)
    {
        var extendBy = extension ?? _defaultSessionDuration;
        var newExpirationTime = DateTime.UtcNow.Add(extendBy);

        const string sql = @"
            UPDATE UserSessions 
            SET ExpiresAt = @ExpiresAt 
            WHERE Id = @SessionId AND IsActive = 1";

        var rowsAffected = await _database.ExecuteAsync(sql, new 
        { 
            SessionId = sessionId, 
            ExpiresAt = newExpirationTime 
        });

        if (rowsAffected > 0)
        {
            _logger.Debug("Extended session {SessionId} until {ExpiresAt}", sessionId, newExpirationTime);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> EndSessionAsync(string sessionId)
    {
        const string sql = @"
            UPDATE UserSessions 
            SET IsActive = 0 
            WHERE Id = @SessionId";

        var rowsAffected = await _database.ExecuteAsync(sql, new { SessionId = sessionId });
        
        if (rowsAffected > 0)
        {
            _logger.Information("Ended session {SessionId}", sessionId);
        }

        return rowsAffected > 0;
    }

    public async Task<bool> EndAllUserSessionsAsync(int userId)
    {
        const string sql = @"
            UPDATE UserSessions 
            SET IsActive = 0 
            WHERE UserId = @UserId AND IsActive = 1";

        var rowsAffected = await _database.ExecuteAsync(sql, new { UserId = userId });
        
        if (rowsAffected > 0)
        {
            _logger.Information("Ended {Count} sessions for user {UserId}", rowsAffected, userId);
        }

        return rowsAffected > 0;
    }

    public async Task<IEnumerable<UserSession>> GetActiveSessionsAsync(int userId)
    {
        const string sql = @"
            SELECT Id, UserId, IpAddress, UserAgent, CreatedAt, ExpiresAt, IsActive
            FROM UserSessions 
            WHERE UserId = @UserId AND IsActive = 1 AND ExpiresAt > @Now
            ORDER BY CreatedAt DESC";

        return await _database.QueryAsync<UserSession>(sql, new 
        { 
            UserId = userId, 
            Now = DateTime.UtcNow 
        });
    }

    public async Task<IEnumerable<UserSession>> GetAllActiveSessionsAsync()
    {
        const string sql = @"
            SELECT s.Id, s.UserId, s.IpAddress, s.UserAgent, s.CreatedAt, s.ExpiresAt, s.IsActive,
                   u.Handle as UserHandle
            FROM UserSessions s
            INNER JOIN Users u ON s.UserId = u.Id
            WHERE s.IsActive = 1 AND s.ExpiresAt > @Now
            ORDER BY s.CreatedAt DESC";

        return await _database.QueryAsync<UserSession>(sql, new { Now = DateTime.UtcNow });
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        const string sql = @"
            DELETE FROM UserSessions 
            WHERE ExpiresAt < @Now OR IsActive = 0";

        var deletedCount = await _database.ExecuteAsync(sql, new { Now = DateTime.UtcNow });
        
        if (deletedCount > 0)
        {
            _logger.Information("Cleaned up {Count} expired sessions", deletedCount);
        }
    }

    private static string GenerateSessionId()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
