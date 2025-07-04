using System;
using System.Threading.Tasks;
using Blackboard.Core.Services;
using Blackboard.Core.Network;
using Moq;
using Serilog;
using Xunit;

namespace Blackboard.Core.Tests.Services
{
    public class ScreenSequenceServiceTests
    {
        private readonly Mock<IAnsiScreenService> _ansiScreenServiceMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly ScreenSequenceService _service;

        public ScreenSequenceServiceTests()
        {
            _ansiScreenServiceMock = new Mock<IAnsiScreenService>();
            _loggerMock = new Mock<ILogger>();
            _service = new ScreenSequenceService(_ansiScreenServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task ShowSequenceAsync_ShowsAllStagesInOrder()
        {
            // Arrange
            var connectionMock = new Mock<TelnetConnection>();
            var userContext = new UserContext();
            
            _ansiScreenServiceMock.Setup(x => x.ScreenExistsAsync(It.IsAny<string>()))
                .ReturnsAsync(true);
            _ansiScreenServiceMock.Setup(x => x.EvaluateConditions(It.IsAny<ScreenConditions>(), It.IsAny<UserContext>()))
                .Returns(true);
            _ansiScreenServiceMock.Setup(x => x.RenderScreenAsync(It.IsAny<string>(), It.IsAny<UserContext>()))
                .ReturnsAsync("Screen content");

            // Act
            await _service.ShowSequenceAsync("LOGIN", connectionMock.Object, userContext);

            // Assert
            _ansiScreenServiceMock.Verify(x => x.ScreenExistsAsync("CONNECT"), Times.Once);
            _ansiScreenServiceMock.Verify(x => x.ScreenExistsAsync("LOGON1"), Times.Once);
            _ansiScreenServiceMock.Verify(x => x.ScreenExistsAsync("LOGON2"), Times.Once);
            _ansiScreenServiceMock.Verify(x => x.ScreenExistsAsync("LOGON3"), Times.Once);
        }

        [Fact]
        public async Task ShowSequenceAsync_SkipsNonExistentScreens()
        {
            // Arrange
            var connectionMock = new Mock<TelnetConnection>();
            var userContext = new UserContext();
            
            _ansiScreenServiceMock.Setup(x => x.ScreenExistsAsync("CONNECT"))
                .ReturnsAsync(true);
            _ansiScreenServiceMock.Setup(x => x.ScreenExistsAsync("LOGON1"))
                .ReturnsAsync(false); // This screen doesn't exist
            _ansiScreenServiceMock.Setup(x => x.ScreenExistsAsync("LOGON2"))
                .ReturnsAsync(true);
            _ansiScreenServiceMock.Setup(x => x.ScreenExistsAsync("LOGON3"))
                .ReturnsAsync(true);
                
            _ansiScreenServiceMock.Setup(x => x.EvaluateConditions(It.IsAny<ScreenConditions>(), It.IsAny<UserContext>()))
                .Returns(true);
            _ansiScreenServiceMock.Setup(x => x.RenderScreenAsync(It.IsAny<string>(), It.IsAny<UserContext>()))
                .ReturnsAsync("Screen content");

            // Act
            await _service.ShowSequenceAsync("LOGIN", connectionMock.Object, userContext);

            // Assert
            _ansiScreenServiceMock.Verify(x => x.RenderScreenAsync("CONNECT", userContext), Times.Once);
            _ansiScreenServiceMock.Verify(x => x.RenderScreenAsync("LOGON1", userContext), Times.Never);
            _ansiScreenServiceMock.Verify(x => x.RenderScreenAsync("LOGON2", userContext), Times.Once);
            _ansiScreenServiceMock.Verify(x => x.RenderScreenAsync("LOGON3", userContext), Times.Once);
        }

        [Fact]
        public async Task GetSequenceStagesAsync_ReturnsCorrectStages()
        {
            // Act
            var stages = await _service.GetSequenceStagesAsync("LOGIN");

            // Assert
            Assert.Equal(new[] { "CONNECT", "LOGON1", "LOGON2", "LOGON3" }, stages);
        }

        [Fact]
        public async Task GetSequenceStagesAsync_ReturnsEmptyForUnknownSequence()
        {
            // Act
            var stages = await _service.GetSequenceStagesAsync("UNKNOWN");

            // Assert
            Assert.Empty(stages);
        }

        [Fact]
        public async Task ShowScreenIfConditionsMetAsync_ReturnsTrueWhenSuccessful()
        {
            // Arrange
            var connectionMock = new Mock<TelnetConnection>();
            var userContext = new UserContext();
            
            _ansiScreenServiceMock.Setup(x => x.ScreenExistsAsync("TEST"))
                .ReturnsAsync(true);
            _ansiScreenServiceMock.Setup(x => x.EvaluateConditions(It.IsAny<ScreenConditions>(), userContext))
                .Returns(true);
            _ansiScreenServiceMock.Setup(x => x.RenderScreenAsync("TEST", userContext))
                .ReturnsAsync("Screen content");

            // Act
            var result = await _service.ShowScreenIfConditionsMetAsync("TEST", connectionMock.Object, userContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ShowScreenIfConditionsMetAsync_ReturnsFalseWhenScreenNotFound()
        {
            // Arrange
            var connectionMock = new Mock<TelnetConnection>();
            var userContext = new UserContext();
            
            _ansiScreenServiceMock.Setup(x => x.ScreenExistsAsync("NONEXISTENT"))
                .ReturnsAsync(false);

            // Act
            var result = await _service.ShowScreenIfConditionsMetAsync("NONEXISTENT", connectionMock.Object, userContext);

            // Assert
            Assert.False(result);
        }
    }
}
