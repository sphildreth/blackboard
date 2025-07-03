using System.Collections.Generic;
using System.Threading.Tasks;
using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Moq;
using Xunit;

namespace Blackboard.Core.Tests.Services
{
    public class MessageServiceAdvancedTests
    {
        private readonly Mock<Data.IDatabaseManager> _dbMock;
        private readonly MessageService _service;

        public MessageServiceAdvancedTests()
        {
            _dbMock = new Mock<Data.IDatabaseManager>();
            _service = new MessageService(_dbMock.Object);
        }

        [Fact]
        public async Task SearchMessagesAsync_ReturnsResults()
        {
            var messages = new List<Message> { new Message { Id = 1, Subject = "foo" } };
            _dbMock.Setup(db => db.QueryAsync<Message>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(messages);
            var result = await _service.SearchMessagesAsync(1, "foo");
            Assert.Single(result);
            Assert.Equal(1, result.First().Id);
        }

        [Fact]
        public async Task GetUnreadCountAsync_ReturnsCount()
        {
            _dbMock.Setup(db => db.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(5);
            var count = await _service.GetUnreadCountAsync(1);
            Assert.Equal(5, count);
        }

        [Fact]
        public async Task GetUnreadMessagesAsync_ReturnsUnread()
        {
            var messages = new List<Message> { new Message { Id = 2, IsRead = false } };
            _dbMock.Setup(db => db.QueryAsync<Message>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(messages);
            var result = await _service.GetUnreadMessagesAsync(1);
            Assert.Single(result);
            Assert.False(result.First().IsRead);
        }

        [Fact]
        public async Task CanSendMessageAsync_EnforcesQuota()
        {
            _dbMock.Setup(db => db.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(99);
            var canSend = await _service.CanSendMessageAsync(1);
            Assert.True(canSend);
            _dbMock.Setup(db => db.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(100);
            canSend = await _service.CanSendMessageAsync(1);
            Assert.False(canSend);
        }

        [Fact]
        public async Task GetMessageQuotaAsync_ReturnsQuota()
        {
            var quota = await _service.GetMessageQuotaAsync(1);
            Assert.Equal(100, quota);
        }
    }
}
