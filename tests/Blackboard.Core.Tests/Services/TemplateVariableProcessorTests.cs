using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blackboard.Core.Services;
using Blackboard.Data;
using Moq;
using Serilog;
using Xunit;

namespace Blackboard.Core.Tests.Services
{
    public class TemplateVariableProcessorTests
    {
        private readonly Mock<ILogger> _loggerMock;
        private readonly TemplateVariableProcessor _processor;

        public TemplateVariableProcessorTests()
        {
            _loggerMock = new Mock<ILogger>();
            var databaseManagerMock = new Mock<IDatabaseManager>();
            _processor = new TemplateVariableProcessor(_loggerMock.Object, databaseManagerMock.Object);
        }

        [Fact]
        public async Task ProcessVariablesAsync_ReplacesUserVariables()
        {
            // Arrange
            var template = "Hello {USER_NAME}! Welcome to {BBS_NAME}.";
            var userContext = new UserContext
            {
                User = new DTOs.UserProfileDto { Handle = "TestUser" },
                SystemInfo = new Dictionary<string, object>
                {
                    ["BBS_NAME"] = "Test BBS"
                }
            };

            // Act
            var result = await _processor.ProcessVariablesAsync(template, userContext);

            // Assert
            Assert.Contains("Hello TestUser!", result);
            Assert.Contains("Welcome to Test BBS", result);
        }

        [Fact]
        public async Task ProcessVariablesAsync_ReplacesSystemVariables()
        {
            // Arrange
            var template = "Current time: {CURRENT_TIME}, Date: {CURRENT_DATE}";
            var userContext = new UserContext();

            // Act
            var result = await _processor.ProcessVariablesAsync(template, userContext);

            // Assert
            Assert.DoesNotContain("{CURRENT_TIME}", result);
            Assert.DoesNotContain("{CURRENT_DATE}", result);
            Assert.Matches(@"\d{2}:\d{2}:\d{2}", result); // Time format
            Assert.Matches(@"\d{4}-\d{2}-\d{2}", result); // Date format
        }

        [Fact]
        public async Task ProcessVariablesAsync_ReplacesConnectionVariables()
        {
            // Arrange
            var template = "Connected from {CALLER_IP} at {CONNECT_TIME}";
            var connectTime = DateTime.Now;
            var userContext = new UserContext
            {
                CallerIp = "192.168.1.100",
                ConnectTime = connectTime
            };

            // Act
            var result = await _processor.ProcessVariablesAsync(template, userContext);

            // Assert
            Assert.Contains("192.168.1.100", result);
            Assert.Contains(connectTime.ToString("HH:mm:ss"), result);
        }

        [Fact]
        public async Task ProcessVariablesAsync_KeepsUnknownVariables()
        {
            // Arrange
            var template = "Hello {UNKNOWN_VARIABLE}!";
            var userContext = new UserContext();

            // Act
            var result = await _processor.ProcessVariablesAsync(template, userContext);

            // Assert
            Assert.Equal(template, result); // Should remain unchanged
        }

        [Fact]
        public async Task ProcessVariablesAsync_HandlesEmptyContent()
        {
            // Arrange
            var template = "";
            var userContext = new UserContext();

            // Act
            var result = await _processor.ProcessVariablesAsync(template, userContext);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public async Task ProcessVariablesAsync_HandlesNullContent()
        {
            // Arrange
            string? template = null;
            var userContext = new UserContext();

            // Act
            var result = await _processor.ProcessVariablesAsync(template!, userContext);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task RegisterVariableProvider_AllowsCustomVariables()
        {
            // Arrange
            var template = "Custom: {CUSTOM_VAR}";
            var userContext = new UserContext();

            _processor.RegisterVariableProvider("CUSTOM", ctx => new Dictionary<string, object>
            {
                ["CUSTOM_VAR"] = "CustomValue"
            });

            // Act
            var result = await _processor.ProcessVariablesAsync(template, userContext);

            // Assert
            Assert.Contains("Custom: CustomValue", result);
        }
    }
}
