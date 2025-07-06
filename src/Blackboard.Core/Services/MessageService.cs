using System.Text.Json;
using Blackboard.Core.Models;
using Blackboard.Data;

namespace Blackboard.Core.Services;

public class MessageService : IMessageService
{
    private readonly IDatabaseManager _db;

    public MessageService(IDatabaseManager db)
    {
        _db = db;
    }

    public async Task<Message> SendPrivateMessageAsync(int fromUserId, int toUserId, string subject, string body)
    {
        var sql = @"INSERT INTO Messages (FromUserId, ToUserId, Subject, Body, MessageType, CreatedAt) VALUES (@FromUserId, @ToUserId, @Subject, @Body, 'private', CURRENT_TIMESTAMP); SELECT last_insert_rowid();";
        var id = await _db.QueryFirstAsync<long>(sql, new { FromUserId = fromUserId, ToUserId = toUserId, Subject = subject, Body = body });
        return new Message
        {
            Id = (int)id,
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Subject = subject,
            Body = body,
            MessageType = MessageType.Private,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<IEnumerable<Message>> GetInboxAsync(int userId, int page = 1, int pageSize = 20)
    {
        var sql = @"SELECT * FROM Messages WHERE ToUserId = @UserId AND IsDeleted = 0 ORDER BY CreatedAt DESC LIMIT @PageSize OFFSET @Offset";
        return await _db.QueryAsync<Message>(sql, new { UserId = userId, PageSize = pageSize, Offset = (page - 1) * pageSize });
    }

    public async Task<IEnumerable<Message>> GetOutboxAsync(int userId, int page = 1, int pageSize = 20)
    {
        var sql = @"SELECT * FROM Messages WHERE FromUserId = @UserId AND IsDeleted = 0 ORDER BY CreatedAt DESC LIMIT @PageSize OFFSET @Offset";
        return await _db.QueryAsync<Message>(sql, new { UserId = userId, PageSize = pageSize, Offset = (page - 1) * pageSize });
    }

    public async Task<Message?> GetMessageByIdAsync(int messageId, int userId)
    {
        var sql = @"SELECT * FROM Messages WHERE Id = @MessageId AND (ToUserId = @UserId OR FromUserId = @UserId) AND IsDeleted = 0";
        return await _db.QueryFirstOrDefaultAsync<Message>(sql, new { MessageId = messageId, UserId = userId });
    }

    public async Task MarkAsReadAsync(int messageId, int userId)
    {
        var sql = @"UPDATE Messages SET IsRead = 1, ReadAt = CURRENT_TIMESTAMP WHERE Id = @MessageId AND ToUserId = @UserId";
        await _db.ExecuteAsync(sql, new { MessageId = messageId, UserId = userId });
    }

    public async Task DeleteMessageAsync(int messageId, int userId)
    {
        var sql = @"UPDATE Messages SET IsDeleted = 1 WHERE Id = @MessageId AND (ToUserId = @UserId OR FromUserId = @UserId)";
        await _db.ExecuteAsync(sql, new { MessageId = messageId, UserId = userId });
    }

    // --- Phase 4: Public Boards, Threads, Moderation, System Messages ---
    public async Task<Message> PostPublicMessageAsync(int boardId, int threadId, int fromUserId, string subject, string body)
    {
        var sql = @"INSERT INTO Messages (FromUserId, BoardId, ThreadId, Subject, Body, MessageType, CreatedAt, IsApproved) VALUES (@FromUserId, @BoardId, @ThreadId, @Subject, @Body, 'public', CURRENT_TIMESTAMP, 1); SELECT last_insert_rowid();";
        var id = await _db.QueryFirstAsync<long>(sql, new { FromUserId = fromUserId, BoardId = boardId, ThreadId = threadId, Subject = subject, Body = body });
        return new Message
        {
            Id = (int)id,
            FromUserId = fromUserId,
            BoardId = boardId,
            ThreadId = threadId,
            Subject = subject,
            Body = body,
            MessageType = MessageType.Public,
            CreatedAt = DateTime.UtcNow,
            IsApproved = true
        };
    }

    public async Task<IEnumerable<Message>> GetBoardMessagesAsync(int boardId, int page = 1, int pageSize = 20)
    {
        var sql = @"SELECT * FROM Messages WHERE BoardId = @BoardId AND IsDeleted = 0 AND IsApproved = 1 ORDER BY CreatedAt DESC LIMIT @PageSize OFFSET @Offset";
        return await _db.QueryAsync<Message>(sql, new { BoardId = boardId, PageSize = pageSize, Offset = (page - 1) * pageSize });
    }

    public async Task<IEnumerable<MessageThread>> GetThreadsAsync(int boardId, int page = 1, int pageSize = 20)
    {
        var sql = @"SELECT * FROM MessageThreads WHERE BoardId = @BoardId ORDER BY CreatedAt DESC LIMIT @PageSize OFFSET @Offset";
        return await _db.QueryAsync<MessageThread>(sql, new { BoardId = boardId, PageSize = pageSize, Offset = (page - 1) * pageSize });
    }

    public async Task<MessageThread> CreateThreadAsync(int boardId, int userId, string title)
    {
        var sql = @"INSERT INTO MessageThreads (BoardId, Title, CreatedByUserId, CreatedAt) VALUES (@BoardId, @Title, @UserId, CURRENT_TIMESTAMP); SELECT last_insert_rowid();";
        var id = await _db.QueryFirstAsync<long>(sql, new { BoardId = boardId, Title = title, UserId = userId });
        return new MessageThread
        {
            Id = (int)id,
            BoardId = boardId,
            Title = title,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task ModerateMessageAsync(int messageId, bool approve, bool sticky = false)
    {
        var sql = @"UPDATE Messages SET IsApproved = @Approve, IsSticky = @Sticky WHERE Id = @MessageId";
        await _db.ExecuteAsync(sql, new { MessageId = messageId, Approve = approve ? 1 : 0, Sticky = sticky ? 1 : 0 });
    }

    public async Task ReportMessageAsync(int messageId, int userId, string reason)
    {
        var sql = @"UPDATE Messages SET IsReported = 1 WHERE Id = @MessageId";
        await _db.ExecuteAsync(sql, new { MessageId = messageId });
        // Optionally log/report reason elsewhere
    }

    public async Task<Message> PostSystemMessageAsync(string subject, string body)
    {
        var sql = @"INSERT INTO Messages (Subject, Body, MessageType, CreatedAt) VALUES (@Subject, @Body, 'system', CURRENT_TIMESTAMP); SELECT last_insert_rowid();";
        var id = await _db.QueryFirstAsync<long>(sql, new { Subject = subject, Body = body });
        return new Message
        {
            Id = (int)id,
            Subject = subject,
            Body = body,
            MessageType = MessageType.System,
            CreatedAt = DateTime.UtcNow
        };
    }

    // --- Phase 4: Remaining features --- 
    public async Task<IEnumerable<Message>> SearchMessagesAsync(int userId, string query, int page = 1, int pageSize = 20)
    {
        var sql = @"SELECT * FROM Messages WHERE (ToUserId = @UserId OR FromUserId = @UserId OR BoardId IS NOT NULL) AND IsDeleted = 0 AND (Subject LIKE @Query OR Body LIKE @Query) ORDER BY CreatedAt DESC LIMIT @PageSize OFFSET @Offset";
        return await _db.QueryAsync<Message>(sql, new { UserId = userId, Query = "%" + query + "%", PageSize = pageSize, Offset = (page - 1) * pageSize });
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        var sql = @"SELECT COUNT(*) FROM Messages WHERE ToUserId = @UserId AND IsRead = 0 AND IsDeleted = 0";
        return await _db.QueryFirstAsync<int>(sql, new { UserId = userId });
    }

    public async Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId, int page = 1, int pageSize = 20)
    {
        var sql = @"SELECT * FROM Messages WHERE ToUserId = @UserId AND IsRead = 0 AND IsDeleted = 0 ORDER BY CreatedAt DESC LIMIT @PageSize OFFSET @Offset";
        return await _db.QueryAsync<Message>(sql, new { UserId = userId, PageSize = pageSize, Offset = (page - 1) * pageSize });
    }

    public async Task<bool> CanSendMessageAsync(int userId)
    {
        // Example: limit to 100 messages per day
        var sql = @"SELECT COUNT(*) FROM Messages WHERE FromUserId = @UserId AND CreatedAt >= date('now', '-1 day')";
        var sent = await _db.QueryFirstAsync<int>(sql, new { UserId = userId });
        return sent < 100;
    }

    public Task<int> GetMessageQuotaAsync(int userId)
    {
        // Example: 100 messages per day
        return Task.FromResult(100);
    }

    // Message Preferences Implementation
    public async Task<MessagePreferences> GetUserPreferencesAsync(int userId)
    {
        var sql = @"SELECT * FROM MessagePreferences WHERE UserId = @UserId";
        var preferences = await _db.QueryFirstOrDefaultAsync<MessagePreferences>(sql, new { UserId = userId });

        if (preferences == null)
        {
            // Create default preferences for user
            preferences = new MessagePreferences
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var insertSql = @"INSERT INTO MessagePreferences (UserId, AllowPrivateMessages, NotifyOnNewMessage, NotifyOnPublicReply, ShowSignature, MessageQuotaDaily, MessageQuotaMonthly, EnableAnsiEditor, AutoMarkRead, CreatedAt, UpdatedAt) 
                                 VALUES (@UserId, @AllowPrivateMessages, @NotifyOnNewMessage, @NotifyOnPublicReply, @ShowSignature, @MessageQuotaDaily, @MessageQuotaMonthly, @EnableAnsiEditor, @AutoMarkRead, @CreatedAt, @UpdatedAt); 
                                 SELECT last_insert_rowid();";
            var id = await _db.QueryFirstAsync<long>(insertSql, preferences);
            preferences.Id = (int)id;
        }

        return preferences;
    }

    public async Task<bool> UpdateUserPreferencesAsync(int userId, MessagePreferences preferences)
    {
        preferences.UserId = userId;
        preferences.UpdatedAt = DateTime.UtcNow;

        var sql = @"UPDATE MessagePreferences SET 
                       AllowPrivateMessages = @AllowPrivateMessages,
                       NotifyOnNewMessage = @NotifyOnNewMessage,
                       NotifyOnPublicReply = @NotifyOnPublicReply,
                       ShowSignature = @ShowSignature,
                       Signature = @Signature,
                       MessageQuotaDaily = @MessageQuotaDaily,
                       MessageQuotaMonthly = @MessageQuotaMonthly,
                       EnableAnsiEditor = @EnableAnsiEditor,
                       AutoMarkRead = @AutoMarkRead,
                       BlockedUsers = @BlockedUsers,
                       UpdatedAt = @UpdatedAt
                       WHERE UserId = @UserId";

        var result = await _db.ExecuteAsync(sql, preferences);
        return result > 0;
    }

    public async Task<bool> IsUserBlockedAsync(int fromUserId, int toUserId)
    {
        var preferences = await GetUserPreferencesAsync(toUserId);
        if (string.IsNullOrEmpty(preferences.BlockedUsers)) return false;

        try
        {
            var blockedUserIds = JsonSerializer.Deserialize<int[]>(preferences.BlockedUsers) ?? Array.Empty<int>();
            return blockedUserIds.Contains(fromUserId);
        }
        catch
        {
            return false; // Invalid JSON, assume not blocked
        }
    }

    public async Task<bool> BlockUserAsync(int userId, int userToBlockId)
    {
        var preferences = await GetUserPreferencesAsync(userId);
        var blockedUserIds = new List<int>();

        if (!string.IsNullOrEmpty(preferences.BlockedUsers))
            try
            {
                blockedUserIds = JsonSerializer.Deserialize<int[]>(preferences.BlockedUsers)?.ToList() ?? new List<int>();
            }
            catch
            {
                blockedUserIds = new List<int>();
            }

        if (!blockedUserIds.Contains(userToBlockId))
        {
            blockedUserIds.Add(userToBlockId);
            preferences.BlockedUsers = JsonSerializer.Serialize(blockedUserIds.ToArray());
            return await UpdateUserPreferencesAsync(userId, preferences);
        }

        return true; // Already blocked
    }

    public async Task<bool> UnblockUserAsync(int userId, int userToUnblockId)
    {
        var preferences = await GetUserPreferencesAsync(userId);
        if (string.IsNullOrEmpty(preferences.BlockedUsers)) return true;

        try
        {
            var blockedUserIds = JsonSerializer.Deserialize<int[]>(preferences.BlockedUsers)?.ToList() ?? new List<int>();
            if (blockedUserIds.Remove(userToUnblockId))
            {
                preferences.BlockedUsers = blockedUserIds.Count > 0 ? JsonSerializer.Serialize(blockedUserIds.ToArray()) : null;
                return await UpdateUserPreferencesAsync(userId, preferences);
            }
        }
        catch
        {
            return false;
        }

        return true; // Wasn't blocked
    }

    public async Task<IEnumerable<int>> GetBlockedUsersAsync(int userId)
    {
        var preferences = await GetUserPreferencesAsync(userId);
        if (string.IsNullOrEmpty(preferences.BlockedUsers)) return Array.Empty<int>();

        try
        {
            return JsonSerializer.Deserialize<int[]>(preferences.BlockedUsers) ?? Array.Empty<int>();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }
}