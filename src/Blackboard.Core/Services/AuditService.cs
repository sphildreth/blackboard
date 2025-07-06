using System.Text.Json;
using Blackboard.Core.Models;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public interface IAuditService
{
    Task LogAsync(string action, int? userId = null, string? entityType = null, string? entityId = null,
        object? oldValues = null, object? newValues = null, string? ipAddress = null, string? userAgent = null);

    Task LogUserActionAsync(int userId, string action, string? ipAddress = null, string? userAgent = null);

    Task LogEntityChangeAsync<T>(int? userId, string action, string entityId, T? oldValues, T? newValues,
        string? ipAddress = null, string? userAgent = null);

    Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int? userId = null, DateTime? fromDate = null,
        DateTime? toDate = null, int limit = 100);

    Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(int userId, int limit = 50);
}

public class AuditService : IAuditService
{
    private readonly DatabaseManager _database;
    private readonly ILogger _logger;

    public AuditService(DatabaseManager database, ILogger logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task LogAsync(string action, int? userId = null, string? entityType = null, string? entityId = null,
        object? oldValues = null, object? newValues = null, string? ipAddress = null, string? userAgent = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
                NewValues = newValues != null ? JsonSerializer.Serialize(newValues) : null,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };

            const string sql = @"
                INSERT INTO AuditLogs (UserId, Action, EntityType, EntityId, OldValues, NewValues, IpAddress, UserAgent, CreatedAt)
                VALUES (@UserId, @Action, @EntityType, @EntityId, @OldValues, @NewValues, @IpAddress, @UserAgent, @CreatedAt)";

            await _database.ExecuteAsync(sql, auditLog);

            _logger.Debug("Audit log created: {Action} by user {UserId}", action, userId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create audit log for action {Action}", action);
        }
    }

    public async Task LogUserActionAsync(int userId, string action, string? ipAddress = null, string? userAgent = null)
    {
        await LogAsync(action, userId, "User", userId.ToString(), ipAddress: ipAddress, userAgent: userAgent);
    }

    public async Task LogEntityChangeAsync<T>(int? userId, string action, string entityId, T? oldValues, T? newValues,
        string? ipAddress = null, string? userAgent = null)
    {
        var entityType = typeof(T).Name;
        await LogAsync(action, userId, entityType, entityId, oldValues, newValues, ipAddress, userAgent);
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync(int? userId = null, DateTime? fromDate = null,
        DateTime? toDate = null, int limit = 100)
    {
        var sql = @"
            SELECT Id, UserId, Action, EntityType, EntityId, OldValues, NewValues, IpAddress, UserAgent, CreatedAt
            FROM AuditLogs 
            WHERE 1=1";

        var parameters = new Dictionary<string, object>();

        if (userId.HasValue)
        {
            sql += " AND UserId = @UserId";
            parameters.Add("UserId", userId.Value);
        }

        if (fromDate.HasValue)
        {
            sql += " AND CreatedAt >= @FromDate";
            parameters.Add("FromDate", fromDate.Value);
        }

        if (toDate.HasValue)
        {
            sql += " AND CreatedAt <= @ToDate";
            parameters.Add("ToDate", toDate.Value);
        }

        sql += " ORDER BY CreatedAt DESC LIMIT @Limit";
        parameters.Add("Limit", limit);

        return await _database.QueryAsync<AuditLog>(sql, parameters);
    }

    public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(int userId, int limit = 50)
    {
        const string sql = @"
            SELECT Id, UserId, Action, EntityType, EntityId, OldValues, NewValues, IpAddress, UserAgent, CreatedAt
            FROM AuditLogs 
            WHERE UserId = @UserId 
            ORDER BY CreatedAt DESC 
            LIMIT @Limit";

        return await _database.QueryAsync<AuditLog>(sql, new { UserId = userId, Limit = limit });
    }
}