using System.Collections.Generic;
using System.Threading.Tasks;
using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Moq;
using Xunit;

namespace Blackboard.Core.Tests.Services
{
    public class PublicBoardServiceTests
    {
        private readonly Mock<Data.IDatabaseManager> _dbMock;
        private readonly MessageService _service;

        public PublicBoardServiceTests()
        {
            _dbMock = new Mock<Data.IDatabaseManager>();
            _service = new MessageService(_dbMock.Object);
        }

        [Fact]
        public async Task CreateThreadAsync_CreatesThread()
        {
            _dbMock.Setup(db => db.QueryFirstAsync<long>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            var thread = await _service.CreateThreadAsync(1, 2, "Test Thread");
            Assert.Equal(1, thread.Id);
            Assert.Equal(1, thread.BoardId);
            Assert.Equal("Test Thread", thread.Title);
            Assert.Equal(2, thread.CreatedByUserId);
        }

        [Fact]
        public async Task PostPublicMessageAsync_InsertsMessage()
        {
            _dbMock.Setup(db => db.QueryFirstAsync<long>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            var msg = await _service.PostPublicMessageAsync(1, 1, 2, "Subject", "Body");
            Assert.Equal(1, msg.Id);
            Assert.Equal(2, msg.FromUserId);
            Assert.Equal(1, msg.BoardId);
            Assert.Equal(1, msg.ThreadId);
            Assert.Equal("Subject", msg.Subject);
            Assert.Equal("Body", msg.Body);
        }

        [Fact]
        public async Task ModerateMessageAsync_UpdatesApproval()
        {
            _dbMock.Setup(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            await _service.ModerateMessageAsync(1, true, true);
            _dbMock.Verify(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task ReportMessageAsync_SetsReported()
        {
            _dbMock.Setup(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            await _service.ReportMessageAsync(1, 2, "Spam");
            _dbMock.Verify(db => db.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task PostSystemMessageAsync_InsertsSystemMessage()
        {
            _dbMock.Setup(db => db.QueryFirstAsync<long>(It.IsAny<string>(), It.IsAny<object>())).ReturnsAsync(1);
            var msg = await _service.PostSystemMessageAsync("System", "Notice");
            Assert.Equal(1, msg.Id);
            Assert.Equal("System", msg.Subject);
            Assert.Equal("Notice", msg.Body);
            Assert.Equal(MessageType.System, msg.MessageType);
        }
    }
}
