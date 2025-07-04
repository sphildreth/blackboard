using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Blackboard.Core.Services;
using Blackboard.Core.Models;
using Moq;
using Serilog;
using Xunit;

namespace Blackboard.Core.Tests.Services
{
    public class AnsiScreenServiceTests : IDisposable
    {
        private readonly string _tempScreensDir;
        private readonly Mock<ILogger> _loggerMock;
        private readonly Mock<ITemplateVariableProcessor> _templateProcessorMock;
        private readonly AnsiScreenService _service;

        public AnsiScreenServiceTests()
        {
            _tempScreensDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempScreensDir);
            
            _loggerMock = new Mock<ILogger>();
            _templateProcessorMock = new Mock<ITemplateVariableProcessor>();
            
            _service = new AnsiScreenService(_tempScreensDir, _loggerMock.Object, _templateProcessorMock.Object);
        }

        [Fact]
        public async Task ScreenExistsAsync_ReturnsTrueWhenScreenExists()
        {
            // Arrange
            var screenName = "test";
            var screenPath = Path.Combine(_tempScreensDir, "test.ans");
            await File.WriteAllTextAsync(screenPath, "Test screen content");

            // Act
            var exists = await _service.ScreenExistsAsync(screenName);

            // Assert
            Assert.True(exists);
        }

        [Fact]
        public async Task ScreenExistsAsync_ReturnsFalseWhenScreenDoesNotExist()
        {
            // Arrange
            var screenName = "nonexistent";

            // Act
            var exists = await _service.ScreenExistsAsync(screenName);

            // Assert
            Assert.False(exists);
        }

        [Fact]
        public async Task RenderScreenAsync_ProcessesTemplateVariables()
        {
            // Arrange
            var screenName = "template_test";
            var screenContent = "Hello {USER_NAME}!";
            var processedContent = "Hello TestUser!";
            
            var screenPath = Path.Combine(_tempScreensDir, "template_test.ans");
            await File.WriteAllTextAsync(screenPath, screenContent);

            var userContext = new UserContext
            {
                User = new DTOs.UserProfileDto { Handle = "TestUser" }
            };

            _templateProcessorMock
                .Setup(x => x.ProcessVariablesAsync(screenContent, userContext))
                .ReturnsAsync(processedContent);

            // Act
            var result = await _service.RenderScreenAsync(screenName, userContext);

            // Assert
            Assert.Equal(processedContent, result);
            _templateProcessorMock.Verify(x => x.ProcessVariablesAsync(screenContent, userContext), Times.Once);
        }

        [Fact]
        public async Task RenderScreenAsync_ReturnsFallbackWhenScreenNotFound()
        {
            // Arrange
            var screenName = "nonexistent";
            var expectedFallbackContent = "=== NONEXISTENT ===\r\n[ANSI screen not available]\r\n";
            
            var userContext = new UserContext();

            _templateProcessorMock
                .Setup(x => x.ProcessVariablesAsync(It.IsAny<string>(), userContext))
                .ReturnsAsync((string content, UserContext ctx) => content);

            // Act
            var result = await _service.RenderScreenAsync(screenName, userContext);

            // Assert
            Assert.Equal(expectedFallbackContent, result);
        }

        [Fact]
        public void EvaluateConditions_ReturnsTrueWhenConditionsMet()
        {
            // Arrange
            var conditions = new ScreenConditions
            {
                MinSecurityLevel = 10
            };

            var userContext = new UserContext
            {
                User = new DTOs.UserProfileDto { SecurityLevel = SecurityLevel.Moderator }
            };

            // Act
            var result = _service.EvaluateConditions(conditions, userContext);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void EvaluateConditions_ReturnsFalseWhenConditionsNotMet()
        {
            // Arrange
            var conditions = new ScreenConditions
            {
                MinSecurityLevel = 100
            };

            var userContext = new UserContext
            {
                User = new DTOs.UserProfileDto { SecurityLevel = SecurityLevel.User }
            };

            // Act
            var result = _service.EvaluateConditions(conditions, userContext);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ClearCache_DoesNotThrow()
        {
            // Act & Assert
            _service.ClearCache();
        }

        public void Dispose()
        {
            _service?.Dispose();
            if (Directory.Exists(_tempScreensDir))
            {
                Directory.Delete(_tempScreensDir, true);
            }
        }
    }
}
