using System.IO.Pipes;
using Blackboard.Core.DTOs;
using Blackboard.Core.Network;

namespace Blackboard.Core.Services;

/// <summary>
/// FOSSIL (Fido/Opus/SEAdog Standard Interface Layer) emulation service.
/// Provides a bridge between legacy DOS door games expecting FOSSIL drivers
/// and modern telnet connections, similar to NetFoss functionality.
/// </summary>
public interface IFossilEmulationService
{
    // FOSSIL Session Management
    Task<FossilEmulationDto> CreateFossilSessionAsync(string sessionId, ITelnetConnection telnetConnection);
    Task<bool> CloseFossilSessionAsync(string sessionId);
    Task<FossilEmulationDto?> GetFossilSessionAsync(string sessionId);
    Task<IEnumerable<FossilEmulationDto>> GetActiveFossilSessionsAsync();

    // Named Pipe Management (for DOS door communication)
    Task<string> CreateNamedPipeAsync(string sessionId, string comPort = "COM1");
    Task<bool> StartPipeServerAsync(string pipeName, string sessionId);
    Task<bool> StopPipeServerAsync(string pipeName);
    Task<bool> IsPipeActiveAsync(string pipeName);

    // FOSSIL Driver Emulation
    Task<bool> InitializeFossilDriverAsync(string sessionId, int comPort = 1, int baudRate = 38400);
    Task<bool> DeinitializeFossilDriverAsync(string sessionId);
    Task<bool> SetBaudRateAsync(string sessionId, int baudRate);
    Task<bool> SetDataFormatAsync(string sessionId, int dataBits = 8, int stopBits = 1, string parity = "none");

    // Data Transfer
    Task<int> SendDataAsync(string sessionId, byte[] data);
    Task<byte[]> ReceiveDataAsync(string sessionId, int maxBytes = 1024);
    Task<int> GetInputBufferCountAsync(string sessionId);
    Task<int> GetOutputBufferCountAsync(string sessionId);
    Task<bool> FlushInputBufferAsync(string sessionId);
    Task<bool> FlushOutputBufferAsync(string sessionId);

    // Flow Control and Status
    Task<bool> SetDtrAsync(string sessionId, bool state);
    Task<bool> SetRtsAsync(string sessionId, bool state);
    Task<bool> GetCtsAsync(string sessionId);
    Task<bool> GetDsrAsync(string sessionId);
    Task<bool> GetDcdAsync(string sessionId);
    Task<bool> GetRiAsync(string sessionId);

    // Configuration and Status
    Task<bool> IsSessionActiveAsync(string sessionId);
    Task<TimeSpan> GetSessionUpTimeAsync(string sessionId);
    Task<(long sent, long received)> GetSessionStatisticsAsync(string sessionId);
    Task<bool> ValidateFossilEnvironmentAsync();

    // Legacy Support
    Task<string> GenerateFossilBatchFileAsync(string sessionId, string comPort, string doorExecutable, string? parameters = null);
    Task<bool> SetupFossilEnvironmentAsync(string workingDirectory);
    Task<bool> CleanupFossilEnvironmentAsync(string sessionId);

    // Interrupt Handling (simulated)
    Task<bool> EnableInterruptsAsync(string sessionId, int interruptVector = 0x14);
    Task<bool> DisableInterruptsAsync(string sessionId);
    Task<bool> SimulateInterruptAsync(string sessionId, int interruptNumber);

    // Debugging and Monitoring
    Task<bool> EnableFossilLoggingAsync(string sessionId, string logLevel = "info");
    Task<bool> DisableFossilLoggingAsync(string sessionId);
    Task<IEnumerable<string>> GetFossilLogAsync(string sessionId, int count = 100);
}
