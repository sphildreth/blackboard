using Blackboard.Core.DTOs;
using Blackboard.Core.Network;
using Blackboard.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Blackboard.Core.Tests.Services;

public class FossilEmulationServiceTests : IDisposable
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly FossilEmulationService _fossilService;
    private readonly Mock<TelnetConnection> _mockTelnetConnection;

    public FossilEmulationServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
        _fossilService = new FossilEmulationService(_mockLogger.Object);
        _mockTelnetConnection = new Mock<TelnetConnection>();
    }

    public void Dispose()
    {
        _fossilService?.Dispose();
    }

    #region Session Management Tests

    [Fact]
    public async Task CreateFossilSessionAsync_ValidInput_CreatesSession()
    {
        // Arrange
        var sessionId = "test-session-123";

        // Act
        var result = await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Assert
        result.Should().NotBeNull();
        result.SessionId.Should().Be(sessionId);
        result.IsActive.Should().BeTrue();
        result.ComPort.Should().Be(1);
        result.BaudRate.Should().Be(38400);
        result.DataBits.Should().Be(8);
        result.StopBits.Should().Be(1);
        result.Parity.Should().Be("none");
    }

    [Fact]
    public async Task CreateFossilSessionAsync_DuplicateSession_ThrowsException()
    {
        // Arrange
        var sessionId = "duplicate-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object));
    }

    [Fact]
    public async Task GetFossilSessionAsync_ExistingSession_ReturnsSession()
    {
        // Arrange
        var sessionId = "existing-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.GetFossilSessionAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(sessionId);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetFossilSessionAsync_NonExistentSession_ReturnsNull()
    {
        // Act
        var result = await _fossilService.GetFossilSessionAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CloseFossilSessionAsync_ExistingSession_ClosesSession()
    {
        // Arrange
        var sessionId = "session-to-close";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.CloseFossilSessionAsync(sessionId);

        // Assert
        result.Should().BeTrue();
        
        var session = await _fossilService.GetFossilSessionAsync(sessionId);
        session.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveFossilSessionsAsync_MultipleActiveSessions_ReturnsActiveSessions()
    {
        // Arrange
        var sessionId1 = "active-session-1";
        var sessionId2 = "active-session-2";
        var sessionId3 = "session-to-close";

        await _fossilService.CreateFossilSessionAsync(sessionId1, _mockTelnetConnection.Object);
        await _fossilService.CreateFossilSessionAsync(sessionId2, _mockTelnetConnection.Object);
        await _fossilService.CreateFossilSessionAsync(sessionId3, _mockTelnetConnection.Object);
        await _fossilService.CloseFossilSessionAsync(sessionId3);

        // Act
        var result = await _fossilService.GetActiveFossilSessionsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.SessionId == sessionId1);
        result.Should().Contain(s => s.SessionId == sessionId2);
        result.Should().NotContain(s => s.SessionId == sessionId3);
    }

    #endregion

    #region Named Pipe Tests

    [Fact]
    public async Task CreateNamedPipeAsync_ValidInput_CreatesPipe()
    {
        // Arrange
        var sessionId = "pipe-session";
        var comPort = "COM1";

        // Act
        var result = await _fossilService.CreateNamedPipeAsync(sessionId, comPort);

        // Assert
        result.Should().Be($"fossil_{sessionId}");
    }

    [Fact]
    public async Task CreateNamedPipeAsync_DuplicatePipe_ThrowsException()
    {
        // Arrange
        var sessionId = "duplicate-pipe-session";
        await _fossilService.CreateNamedPipeAsync(sessionId);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fossilService.CreateNamedPipeAsync(sessionId));
    }

    [Fact]
    public async Task StartPipeServerAsync_ValidPipe_StartsServer()
    {
        // Arrange
        var sessionId = "pipe-server-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        var pipeName = await _fossilService.CreateNamedPipeAsync(sessionId);

        // Act
        var result = await _fossilService.StartPipeServerAsync(pipeName, sessionId);

        // Assert
        result.Should().BeTrue();
        
        var isActive = await _fossilService.IsPipeActiveAsync(pipeName);
        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task StopPipeServerAsync_ActivePipe_StopsServer()
    {
        // Arrange
        var sessionId = "pipe-stop-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        var pipeName = await _fossilService.CreateNamedPipeAsync(sessionId);
        await _fossilService.StartPipeServerAsync(pipeName, sessionId);

        // Act
        var result = await _fossilService.StopPipeServerAsync(pipeName);

        // Assert
        result.Should().BeTrue();
        
        var isActive = await _fossilService.IsPipeActiveAsync(pipeName);
        isActive.Should().BeFalse();
    }

    #endregion

    #region FOSSIL Driver Emulation Tests

    [Fact]
    public async Task InitializeFossilDriverAsync_ValidSession_InitializesDriver()
    {
        // Arrange
        var sessionId = "init-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.InitializeFossilDriverAsync(sessionId, 2, 57600);

        // Assert
        result.Should().BeTrue();
        
        var session = await _fossilService.GetFossilSessionAsync(sessionId);
        session!.ComPort.Should().Be(2);
        session.BaudRate.Should().Be(57600);
    }

    [Fact]
    public async Task SetBaudRateAsync_ValidSession_SetsBaudRate()
    {
        // Arrange
        var sessionId = "baudrate-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.SetBaudRateAsync(sessionId, 115200);

        // Assert
        result.Should().BeTrue();
        
        var session = await _fossilService.GetFossilSessionAsync(sessionId);
        session!.BaudRate.Should().Be(115200);
    }

    [Fact]
    public async Task SetDataFormatAsync_ValidSession_SetsFormat()
    {
        // Arrange
        var sessionId = "format-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.SetDataFormatAsync(sessionId, 7, 2, "even");

        // Assert
        result.Should().BeTrue();
        
        var session = await _fossilService.GetFossilSessionAsync(sessionId);
        session!.DataBits.Should().Be(7);
        session.StopBits.Should().Be(2);
        session.Parity.Should().Be("even");
    }

    [Fact]
    public async Task DeinitializeFossilDriverAsync_ValidSession_DeinitializesDriver()
    {
        // Arrange
        var sessionId = "deinit-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        await _fossilService.InitializeFossilDriverAsync(sessionId);

        // Act
        var result = await _fossilService.DeinitializeFossilDriverAsync(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Data Transfer Tests

    [Fact]
    public async Task SendDataAsync_ValidSession_SendsData()
    {
        // Arrange
        var sessionId = "send-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        var data = "Hello, World!"u8.ToArray();

        // Act
        var result = await _fossilService.SendDataAsync(sessionId, data);

        // Assert
        result.Should().Be(data.Length);
        
        var (sent, _) = await _fossilService.GetSessionStatisticsAsync(sessionId);
        sent.Should().Be(data.Length);
    }

    [Fact]
    public async Task ReceiveDataAsync_EmptyBuffer_ReturnsEmptyArray()
    {
        // Arrange
        var sessionId = "receive-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.ReceiveDataAsync(sessionId);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInputBufferCountAsync_EmptyBuffer_ReturnsZero()
    {
        // Arrange
        var sessionId = "buffer-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.GetInputBufferCountAsync(sessionId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task FlushInputBufferAsync_ValidSession_ClearsBuffer()
    {
        // Arrange
        var sessionId = "flush-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.FlushInputBufferAsync(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task FlushOutputBufferAsync_ValidSession_ClearsBuffer()
    {
        // Arrange
        var sessionId = "flush-output-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.FlushOutputBufferAsync(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Flow Control Tests

    [Fact]
    public async Task SetDtrAsync_ValidSession_SetsDtrState()
    {
        // Arrange
        var sessionId = "dtr-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.SetDtrAsync(sessionId, false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetRtsAsync_ValidSession_SetsRtsState()
    {
        // Arrange
        var sessionId = "rts-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.SetRtsAsync(sessionId, false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetCtsAsync_ValidSession_ReturnsCtsState()
    {
        // Arrange
        var sessionId = "cts-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.GetCtsAsync(sessionId);

        // Assert
        result.Should().BeTrue(); // Default state is true
    }

    [Fact]
    public async Task GetDsrAsync_ValidSession_ReturnsDsrState()
    {
        // Arrange
        var sessionId = "dsr-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.GetDsrAsync(sessionId);

        // Assert
        result.Should().BeTrue(); // Default state is true
    }

    [Fact]
    public async Task GetDcdAsync_ValidSession_ReturnsDcdState()
    {
        // Arrange
        var sessionId = "dcd-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.GetDcdAsync(sessionId);

        // Assert
        result.Should().BeTrue(); // Default state is true
    }

    [Fact]
    public async Task GetRiAsync_ValidSession_ReturnsRiState()
    {
        // Arrange
        var sessionId = "ri-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.GetRiAsync(sessionId);

        // Assert
        result.Should().BeFalse(); // Default state is false
    }

    #endregion

    #region Configuration and Status Tests

    [Fact]
    public async Task IsSessionActiveAsync_ActiveSession_ReturnsTrue()
    {
        // Arrange
        var sessionId = "active-check-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.IsSessionActiveAsync(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsSessionActiveAsync_ClosedSession_ReturnsFalse()
    {
        // Arrange
        var sessionId = "inactive-check-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        await _fossilService.CloseFossilSessionAsync(sessionId);

        // Act
        var result = await _fossilService.IsSessionActiveAsync(sessionId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetSessionUpTimeAsync_ActiveSession_ReturnsUpTime()
    {
        // Arrange
        var sessionId = "uptime-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        
        // Wait a small amount to ensure some uptime
        await Task.Delay(10);

        // Act
        var result = await _fossilService.GetSessionUpTimeAsync(sessionId);

        // Assert
        result.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task GetSessionStatisticsAsync_NewSession_ReturnsZeroStats()
    {
        // Arrange
        var sessionId = "stats-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var (sent, received) = await _fossilService.GetSessionStatisticsAsync(sessionId);

        // Assert
        sent.Should().Be(0);
        received.Should().Be(0);
    }

    [Fact]
    public async Task ValidateFossilEnvironmentAsync_ReturnsValidationResult()
    {
        // Act
        var result = await _fossilService.ValidateFossilEnvironmentAsync();

        // Assert
        result.Should().BeTrue(); // Should be true in test environment
    }

    #endregion

    #region Legacy Support Tests

    [Fact]
    public async Task GenerateFossilBatchFileAsync_ValidInput_GeneratesBatchFile()
    {
        // Arrange
        var sessionId = "batch-session";
        var comPort = "COM1";
        var doorExecutable = "door.exe";
        var parameters = "door.sys";

        // Act
        var result = await _fossilService.GenerateFossilBatchFileAsync(sessionId, comPort, doorExecutable, parameters);

        // Assert
        result.Should().NotBeNullOrEmpty();
        File.Exists(result).Should().BeTrue();
        
        var content = await File.ReadAllTextAsync(result);
        content.Should().Contain($"SET FOSSIL_PORT={comPort}");
        content.Should().Contain($"fossil_{sessionId}");
        content.Should().Contain(doorExecutable);
        content.Should().Contain(parameters);
        
        // Cleanup
        File.Delete(result);
    }

    [Fact]
    public async Task SetupFossilEnvironmentAsync_ValidDirectory_ReturnsTrue()
    {
        // Arrange
        var workingDirectory = Path.Combine(Path.GetTempPath(), "fossil_test_env");

        // Act
        var result = await _fossilService.SetupFossilEnvironmentAsync(workingDirectory);

        // Assert
        result.Should().BeTrue();
        Directory.Exists(workingDirectory).Should().BeTrue();
        
        // Cleanup
        Directory.Delete(workingDirectory, true);
    }

    [Fact]
    public async Task CleanupFossilEnvironmentAsync_ValidSession_CleansUp()
    {
        // Arrange
        var sessionId = "cleanup-session";
        var batchFile = await _fossilService.GenerateFossilBatchFileAsync(sessionId, "COM1", "test.exe");

        // Act
        var result = await _fossilService.CleanupFossilEnvironmentAsync(sessionId);

        // Assert
        result.Should().BeTrue();
        File.Exists(batchFile).Should().BeFalse();
    }

    #endregion

    #region Interrupt Handling Tests

    [Fact]
    public async Task EnableInterruptsAsync_ValidSession_EnablesInterrupts()
    {
        // Arrange
        var sessionId = "interrupt-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.EnableInterruptsAsync(sessionId, 0x14);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DisableInterruptsAsync_ValidSession_DisablesInterrupts()
    {
        // Arrange
        var sessionId = "interrupt-disable-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        await _fossilService.EnableInterruptsAsync(sessionId);

        // Act
        var result = await _fossilService.DisableInterruptsAsync(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SimulateInterruptAsync_ValidSession_SimulatesInterrupt()
    {
        // Arrange
        var sessionId = "simulate-interrupt-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.SimulateInterruptAsync(sessionId, 0x14);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Debugging and Monitoring Tests

    [Fact]
    public async Task EnableFossilLoggingAsync_ValidSession_EnablesLogging()
    {
        // Arrange
        var sessionId = "logging-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.EnableFossilLoggingAsync(sessionId, "debug");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DisableFossilLoggingAsync_ValidSession_DisablesLogging()
    {
        // Arrange
        var sessionId = "logging-disable-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);
        await _fossilService.EnableFossilLoggingAsync(sessionId);

        // Act
        var result = await _fossilService.DisableFossilLoggingAsync(sessionId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetFossilLogAsync_ValidSession_ReturnsLogs()
    {
        // Arrange
        var sessionId = "log-retrieve-session";
        await _fossilService.CreateFossilSessionAsync(sessionId, _mockTelnetConnection.Object);

        // Act
        var result = await _fossilService.GetFossilLogAsync(sessionId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty(); // No logs generated yet
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task AllMethods_NonExistentSession_HandleGracefully()
    {
        var nonExistentSession = "non-existent-session";

        // Test various methods with non-existent session
        (await _fossilService.GetFossilSessionAsync(nonExistentSession)).Should().BeNull();
        (await _fossilService.CloseFossilSessionAsync(nonExistentSession)).Should().BeFalse();
        (await _fossilService.InitializeFossilDriverAsync(nonExistentSession)).Should().BeFalse();
        (await _fossilService.SetBaudRateAsync(nonExistentSession, 9600)).Should().BeFalse();
        (await _fossilService.SendDataAsync(nonExistentSession, "test"u8.ToArray())).Should().Be(0);
        (await _fossilService.ReceiveDataAsync(nonExistentSession)).Should().BeEmpty();
        (await _fossilService.GetInputBufferCountAsync(nonExistentSession)).Should().Be(0);
        (await _fossilService.IsSessionActiveAsync(nonExistentSession)).Should().BeFalse();
        
        var (sent, received) = await _fossilService.GetSessionStatisticsAsync(nonExistentSession);
        sent.Should().Be(0);
        received.Should().Be(0);
    }

    #endregion
}
