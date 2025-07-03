using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Blackboard.Data;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Blackboard.Core.Tests.Services
{
    public class MessagePreferencesTests
    {
        private readonly Mock<IDatabaseManager> _dbMock;
        private readonly MessageService _messageService;

        public MessagePreferencesTests()
        {
            _dbMock = new Mock<IDatabaseManager>();
            _messageService = new MessageService(_dbMock.Object);
        }

        [Fact]
        public async Task GetUserPreferences_ShouldReturnDefaultPreferences_ForNewUser()
        {
            // Arrange
            var userId = 1;

            // Mock: First call returns null (no existing preferences)
            _dbMock.Setup(db => db.QueryFirstOrDefaultAsync<MessagePreferences>(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync((MessagePreferences?)null);
            
            // Mock: Insert operation returns the new ID
            _dbMock.Setup(db => db.QueryFirstAsync<long>(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(1L);

            // Act
            var preferences = await _messageService.GetUserPreferencesAsync(userId);

            // Assert
            Assert.NotNull(preferences);
            Assert.Equal(userId, preferences.UserId);
            Assert.True(preferences.AllowPrivateMessages);
            Assert.True(preferences.NotifyOnNewMessage);
            Assert.True(preferences.NotifyOnPublicReply);
            Assert.Equal(100, preferences.MessageQuotaDaily);
            Assert.Equal(3000, preferences.MessageQuotaMonthly);
            Assert.True(preferences.EnableAnsiEditor);
        }

        [Fact]
        public async Task UpdateUserPreferences_ShouldUpdatePreferences_Successfully()
        {
            // Arrange
            var userId = 1;
            var preferences = new MessagePreferences
            {
                UserId = userId,
                AllowPrivateMessages = false,
                NotifyOnNewMessage = false,
                MessageQuotaDaily = 50,
                NotifyOnPublicReply = true,
                ShowSignature = false,
                EnableAnsiEditor = true,
                AutoMarkRead = true
            };

            _dbMock.Setup(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(1);

            // Act
            var result = await _messageService.UpdateUserPreferencesAsync(userId, preferences);

            // Assert
            Assert.True(result);
            _dbMock.Verify(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task BlockUser_ShouldAddUserToBlockedList()
        {
            // Arrange
            var userId = 1;
            var userToBlockId = 2;

            _dbMock.Setup(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(1);

            // Act
            var result = await _messageService.BlockUserAsync(userId, userToBlockId);

            // Assert
            Assert.True(result);
            _dbMock.Verify(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task UnblockUser_ShouldRemoveUserFromBlockedList()
        {
            // Arrange
            var userId = 1;
            var userToUnblockId = 2;

            // Mock GetUserPreferencesAsync to return preferences with blocked users
            var mockPreferences = new MessagePreferences
            {
                UserId = userId,
                BlockedUsers = "[2,3]" // User 2 and 3 are blocked initially
            };

            _dbMock.Setup(db => db.QueryFirstOrDefaultAsync<MessagePreferences>(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(mockPreferences);

            // Mock UpdateUserPreferencesAsync (which calls ExecuteAsync)
            _dbMock.Setup(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(1);

            // Act
            var result = await _messageService.UnblockUserAsync(userId, userToUnblockId);

            // Assert
            Assert.True(result);
            _dbMock.Verify(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task IsUserBlocked_ShouldReturnCorrectStatus()
        {
            // Arrange
            var fromUserId = 1;
            var toUserId = 2;

            // Mock the GetUserPreferencesAsync call to return preferences with blocked users
            var mockPreferences = new MessagePreferences
            {
                UserId = toUserId,
                BlockedUsers = "[1]" // fromUserId is blocked
            };

            _dbMock.Setup(db => db.QueryFirstOrDefaultAsync<MessagePreferences>(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(mockPreferences);

            // Act
            var isBlocked = await _messageService.IsUserBlockedAsync(fromUserId, toUserId);

            // Assert
            Assert.True(isBlocked);
        }

        [Fact]
        public async Task GetBlockedUsers_ShouldReturnBlockedUsersList()
        {
            // Arrange
            var userId = 1;
            var mockPreferences = new MessagePreferences
            {
                UserId = userId,
                BlockedUsers = "[2,3,4]" // JSON array of blocked user IDs
            };

            _dbMock.Setup(db => db.QueryFirstOrDefaultAsync<MessagePreferences>(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(mockPreferences);

            // Act
            var result = await _messageService.GetBlockedUsersAsync(userId);

            // Assert
            Assert.Equal(3, result.Count());
            Assert.Contains(2, result);
            Assert.Contains(3, result);
            Assert.Contains(4, result);
        }

        [Fact]
        public async Task CanSendMessage_ShouldReturnFalse_WhenUserExceededQuota()
        {
            // Arrange
            var userId = 1;
            
            // Mock a query that returns a count higher than quota (100)
            _dbMock.Setup(db => db.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(150); // User has sent 150 messages today, quota is 100

            // Act
            var canSend = await _messageService.CanSendMessageAsync(userId);

            // Assert
            Assert.False(canSend);
        }

        [Fact]
        public async Task CanSendMessage_ShouldReturnTrue_WhenWithinQuota()
        {
            // Arrange
            var userId = 1;
            
            // Mock a query that returns a count lower than quota (100)
            _dbMock.Setup(db => db.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>()))
                   .ReturnsAsync(50); // User has sent 50 messages today, quota is 100

            // Act
            var canSend = await _messageService.CanSendMessageAsync(userId);

            // Assert
            Assert.True(canSend);
        }
    }
}
