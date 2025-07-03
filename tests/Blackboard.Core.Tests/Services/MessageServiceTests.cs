using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Moq;
using Xunit;

namespace Blackboard.Core.Tests.Services
{
    public class MessageServiceTests
    {
        private readonly Mock<Data.IDatabaseManager> _dbMock;
        private readonly MessageService _service;

        public MessageServiceTests()
        {
            _dbMock = new Mock<Data.IDatabaseManager>();
            _service = new MessageService(_dbMock.Object);
        }

        [Fact]
        public async Task SendPrivateMessageAsync_InsertsMessageAndReturnsIt()
        {
            _dbMock.Setup(db => db.QueryFirstAsync<long>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            var msg = await _service.SendPrivateMessageAsync(1, 2, "Test", "Hello");
            Assert.Equal(1, msg.Id);
            Assert.Equal(1, msg.FromUserId);
            Assert.Equal(2, msg.ToUserId);
            Assert.Equal("Test", msg.Subject);
            Assert.Equal("Hello", msg.Body);
            Assert.Equal(MessageType.Private, msg.MessageType);
        }

        [Fact]
        public async Task GetInboxAsync_ReturnsMessages()
        {
            var messages = new List<Message> { new Message { Id = 1, ToUserId = 2 } };
            _dbMock.Setup(db => db.QueryAsync<Message>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(messages);
            var result = await _service.GetInboxAsync(2);
            Assert.Single(result);
            Assert.Equal(1, result.First().Id);
        }

        [Fact]
        public async Task GetOutboxAsync_ReturnsMessages()
        {
            var messages = new List<Message> { new Message { Id = 1, FromUserId = 1 } };
            _dbMock.Setup(db => db.QueryAsync<Message>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(messages);
            var result = await _service.GetOutboxAsync(1);
            Assert.Single(result);
            Assert.Equal(1, result.First().Id);
        }

        [Fact]
        public async Task GetMessageByIdAsync_ReturnsMessage()
        {
            var message = new Message { Id = 1, ToUserId = 2 };
            _dbMock.Setup(db => db.QueryFirstOrDefaultAsync<Message>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(message);
            var result = await _service.GetMessageByIdAsync(1, 2);
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task MarkAsReadAsync_ExecutesUpdate()
        {
            _dbMock.Setup(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            await _service.MarkAsReadAsync(1, 2);
            _dbMock.Verify(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task DeleteMessageAsync_ExecutesUpdate()
        {
            _dbMock.Setup(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            await _service.DeleteMessageAsync(1, 2);
            _dbMock.Verify(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }
    }
}
