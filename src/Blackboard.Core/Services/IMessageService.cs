using Blackboard.Core.Models;

namespace Blackboard.Core.Services;

public interface IMessageService
{
    Task<Message> SendPrivateMessageAsync(int fromUserId, int toUserId, string subject, string body);
    Task<IEnumerable<Message>> GetInboxAsync(int userId, int page = 1, int pageSize = 20);
    Task<IEnumerable<Message>> GetOutboxAsync(int userId, int page = 1, int pageSize = 20);
    Task<Message?> GetMessageByIdAsync(int messageId, int userId);
    Task MarkAsReadAsync(int messageId, int userId);

    Task DeleteMessageAsync(int messageId, int userId);

    // Public board and system message methods
    Task<Message> PostPublicMessageAsync(int boardId, int threadId, int fromUserId, string subject, string body);
    Task<IEnumerable<Message>> GetBoardMessagesAsync(int boardId, int page = 1, int pageSize = 20);
    Task<IEnumerable<MessageThread>> GetThreadsAsync(int boardId, int page = 1, int pageSize = 20);
    Task<MessageThread> CreateThreadAsync(int boardId, int userId, string title);
    Task ModerateMessageAsync(int messageId, bool approve, bool sticky = false);
    Task ReportMessageAsync(int messageId, int userId, string reason);
    Task<Message> PostSystemMessageAsync(string subject, string body);

    // Phase 4: Remaining features
    Task<IEnumerable<Message>> SearchMessagesAsync(int userId, string query, int page = 1, int pageSize = 20);
    Task<int> GetUnreadCountAsync(int userId);
    Task<IEnumerable<Message>> GetUnreadMessagesAsync(int userId, int page = 1, int pageSize = 20);
    Task<bool> CanSendMessageAsync(int userId);
    Task<int> GetMessageQuotaAsync(int userId);

    // Message Preferences
    Task<MessagePreferences> GetUserPreferencesAsync(int userId);
    Task<bool> UpdateUserPreferencesAsync(int userId, MessagePreferences preferences);
    Task<bool> IsUserBlockedAsync(int fromUserId, int toUserId);
    Task<bool> BlockUserAsync(int userId, int userToBlockId);
    Task<bool> UnblockUserAsync(int userId, int userToUnblockId);
    Task<IEnumerable<int>> GetBlockedUsersAsync(int userId);
}