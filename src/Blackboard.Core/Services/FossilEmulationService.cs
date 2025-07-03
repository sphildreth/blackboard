using System.Diagnostics;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Blackboard.Core.DTOs;
using Blackboard.Core.Network;
using Serilog;

namespace Blackboard.Core.Services;

/// <summary>
/// FOSSIL (Fido/Opus/SEAdog Standard Interface Layer) emulation service.
/// Provides a bridge between legacy DOS door games expecting FOSSIL drivers
/// and modern telnet connections, similar to NetFoss functionality.
/// </summary>
public class FossilEmulationService : IFossilEmulationService, IDisposable
{
    private readonly ILogger _logger;
    private readonly Dictionary<string, FossilSession> _activeSessions;
    private readonly Dictionary<string, NamedPipeServerStream> _namedPipes;
    private readonly Dictionary<string, CancellationTokenSource> _pipeTokens;
    private readonly object _lockObject = new();
    private bool _disposed = false;

    public FossilEmulationService(ILogger logger)
    {
        _logger = logger;
        _activeSessions = new Dictionary<string, FossilSession>();
        _namedPipes = new Dictionary<string, NamedPipeServerStream>();
        _pipeTokens = new Dictionary<string, CancellationTokenSource>();
    }

    #region FOSSIL Session Management

    public async Task<FossilEmulationDto> CreateFossilSessionAsync(string sessionId, TelnetConnection telnetConnection)
    {
        lock (_lockObject)
        {
            if (_activeSessions.ContainsKey(sessionId))
            {
                throw new InvalidOperationException($"FOSSIL session {sessionId} already exists");
            }

            var session = new FossilSession
            {
                SessionId = sessionId,
                TelnetConnection = telnetConnection,
                StartTime = DateTime.UtcNow,
                ComPort = "COM1",
                BaudRate = 38400,
                DataBits = 8,
                StopBits = 1,
                Parity = "none",
                InputBuffer = new Queue<byte>(),
                OutputBuffer = new Queue<byte>(),
                IsActive = true,
                BytesSent = 0,
                BytesReceived = 0
            };

            _activeSessions[sessionId] = session;

            _logger.Information("Created FOSSIL session {SessionId} for telnet connection", sessionId);
        }

        return await GetFossilSessionAsync(sessionId) ?? throw new InvalidOperationException("Failed to create session");
    }

    public async Task<bool> CloseFossilSessionAsync(string sessionId)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.IsActive = false;
            session.EndTime = DateTime.UtcNow;
            _activeSessions.Remove(sessionId);

            _logger.Information("Closed FOSSIL session {SessionId}", sessionId);
        }

        // Stop associated pipe server if exists
        await StopPipeServerAsync($"fossil_{sessionId}");

        return true;
    }

    public async Task<FossilEmulationDto?> GetFossilSessionAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return null;

            return new FossilEmulationDto
            {
                SessionId = session.SessionId,
                ComPort = session.ComPort,
                BaudRate = session.BaudRate,
                DataBits = session.DataBits,
                StopBits = session.StopBits,
                Parity = session.Parity,
                IsActive = session.IsActive,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                BytesSent = session.BytesSent,
                BytesReceived = session.BytesReceived,
                PipeName = $"fossil_{sessionId}"
            };
        }
    }

    public async Task<IEnumerable<FossilEmulationDto>> GetActiveFossilSessionsAsync()
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            return _activeSessions.Values
                .Where(s => s.IsActive)
                .Select(s => new FossilEmulationDto
                {
                    SessionId = s.SessionId,
                    ComPort = s.ComPort,
                    BaudRate = s.BaudRate,
                    DataBits = s.DataBits,
                    StopBits = s.StopBits,
                    Parity = s.Parity,
                    IsActive = s.IsActive,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    BytesSent = s.BytesSent,
                    BytesReceived = s.BytesReceived,
                    PipeName = $"fossil_{s.SessionId}"
                })
                .ToList();
        }
    }

    #endregion

    #region Named Pipe Management

    public async Task<string> CreateNamedPipeAsync(string sessionId, string comPort = "COM1")
    {
        var pipeName = $"fossil_{sessionId}";
        
        lock (_lockObject)
        {
            if (_namedPipes.ContainsKey(pipeName))
            {
                throw new InvalidOperationException($"Named pipe {pipeName} already exists");
            }

            var pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            _namedPipes[pipeName] = pipeServer;
        }

        await Task.CompletedTask;
        _logger.Information("Created named pipe {PipeName} for session {SessionId}", pipeName, sessionId);
        
        return pipeName;
    }

    public async Task<bool> StartPipeServerAsync(string pipeName, string sessionId)
    {
        lock (_lockObject)
        {
            if (!_namedPipes.TryGetValue(pipeName, out var pipeServer))
                return false;

            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            var cancellationToken = new CancellationTokenSource();
            _pipeTokens[pipeName] = cancellationToken;

            // Start pipe server in background
            _ = Task.Run(async () => await RunPipeServerAsync(pipeServer, session, cancellationToken.Token), 
                cancellationToken.Token);
        }

        await Task.CompletedTask;
        _logger.Information("Started pipe server {PipeName} for session {SessionId}", pipeName, sessionId);
        return true;
    }

    public async Task<bool> StopPipeServerAsync(string pipeName)
    {
        lock (_lockObject)
        {
            if (_pipeTokens.TryGetValue(pipeName, out var cancellationToken))
            {
                cancellationToken.Cancel();
                _pipeTokens.Remove(pipeName);
            }

            if (_namedPipes.TryGetValue(pipeName, out var pipeServer))
            {
                try
                {
                    pipeServer.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error disposing pipe server {PipeName}", pipeName);
                }
                _namedPipes.Remove(pipeName);
            }
        }

        await Task.CompletedTask;
        _logger.Information("Stopped pipe server {PipeName}", pipeName);
        return true;
    }

    public async Task<bool> IsPipeActiveAsync(string pipeName)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            return _namedPipes.ContainsKey(pipeName) && 
                   _pipeTokens.ContainsKey(pipeName) && 
                   !_pipeTokens[pipeName].Token.IsCancellationRequested;
        }
    }

    #endregion

    #region FOSSIL Driver Emulation

    public async Task<bool> InitializeFossilDriverAsync(string sessionId, int comPort = 1, int baudRate = 38400)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.ComPort = $"COM{comPort}";
            session.BaudRate = baudRate;
            session.IsInitialized = true;
        }

        await Task.CompletedTask;
        _logger.Debug("Initialized FOSSIL driver for session {SessionId} on COM{ComPort} at {BaudRate} baud", 
            sessionId, comPort, baudRate);
        return true;
    }

    public async Task<bool> DeinitializeFossilDriverAsync(string sessionId)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.IsInitialized = false;
        }

        await Task.CompletedTask;
        _logger.Debug("Deinitialized FOSSIL driver for session {SessionId}", sessionId);
        return true;
    }

    public async Task<bool> SetBaudRateAsync(string sessionId, int baudRate)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.BaudRate = baudRate;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> SetDataFormatAsync(string sessionId, int dataBits = 8, int stopBits = 1, string parity = "none")
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.DataBits = dataBits;
            session.StopBits = stopBits;
            session.Parity = parity;
        }

        await Task.CompletedTask;
        return true;
    }

    #endregion

    #region Data Transfer

    public async Task<int> SendDataAsync(string sessionId, byte[] data)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session) || !session.IsActive)
                return 0;

            // Add data to output buffer
            foreach (var b in data)
            {
                session.OutputBuffer.Enqueue(b);
            }

            session.BytesSent += data.Length;
        }

        // Send data to telnet connection asynchronously
        _ = Task.Run(() => ProcessOutputBuffer(sessionId));

        await Task.CompletedTask;
        return data.Length;
    }

    public async Task<byte[]> ReceiveDataAsync(string sessionId, int maxBytes = 1024)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session) || !session.IsActive)
                return Array.Empty<byte>();

            var bytesToRead = Math.Min(maxBytes, session.InputBuffer.Count);
            var data = new byte[bytesToRead];

            for (int i = 0; i < bytesToRead; i++)
            {
                data[i] = session.InputBuffer.Dequeue();
            }

            session.BytesReceived += bytesToRead;
            return data;
        }
    }

    public async Task<int> GetInputBufferCountAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return 0;

            return session.InputBuffer.Count;
        }
    }

    public async Task<int> GetOutputBufferCountAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return 0;

            return session.OutputBuffer.Count;
        }
    }

    public async Task<bool> FlushInputBufferAsync(string sessionId)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.InputBuffer.Clear();
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> FlushOutputBufferAsync(string sessionId)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.OutputBuffer.Clear();
        }

        await Task.CompletedTask;
        return true;
    }

    #endregion

    #region Flow Control and Status

    public async Task<bool> SetDtrAsync(string sessionId, bool state)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.DtrState = state;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> SetRtsAsync(string sessionId, bool state)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.RtsState = state;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> GetCtsAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            return session.CtsState;
        }
    }

    public async Task<bool> GetDsrAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            return session.DsrState;
        }
    }

    public async Task<bool> GetDcdAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            return session.DcdState;
        }
    }

    public async Task<bool> GetRiAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            return session.RiState;
        }
    }

    #endregion

    #region Configuration and Status

    public async Task<bool> IsSessionActiveAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            return _activeSessions.TryGetValue(sessionId, out var session) && session.IsActive;
        }
    }

    public async Task<TimeSpan> GetSessionUpTimeAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return TimeSpan.Zero;

            var endTime = session.EndTime ?? DateTime.UtcNow;
            return endTime - session.StartTime;
        }
    }

    public async Task<(long sent, long received)> GetSessionStatisticsAsync(string sessionId)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return (0, 0);

            return (session.BytesSent, session.BytesReceived);
        }
    }

    public async Task<bool> ValidateFossilEnvironmentAsync()
    {
        try
        {
            // Check if we can create named pipes
            var testPipe = new NamedPipeServerStream("fossil_test", PipeDirection.InOut, 1);
            testPipe.Dispose();

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "FOSSIL environment validation failed");
            return false;
        }
    }

    #endregion

    #region Legacy Support

    public async Task<string> GenerateFossilBatchFileAsync(string sessionId, string comPort, string doorExecutable, string? parameters = null)
    {
        var batchContent = new StringBuilder();
        batchContent.AppendLine("@echo off");
        batchContent.AppendLine($"REM FOSSIL emulation batch file for session {sessionId}");
        batchContent.AppendLine($"SET FOSSIL_PORT={comPort}");
        batchContent.AppendLine($"SET FOSSIL_PIPE=\\\\.\\pipe\\fossil_{sessionId}");
        batchContent.AppendLine("");
        
        if (!string.IsNullOrEmpty(parameters))
        {
            batchContent.AppendLine($"\"{doorExecutable}\" {parameters}");
        }
        else
        {
            batchContent.AppendLine($"\"{doorExecutable}\"");
        }

        var batchPath = Path.Combine(Path.GetTempPath(), $"fossil_{sessionId}.bat");
        await File.WriteAllTextAsync(batchPath, batchContent.ToString());

        _logger.Debug("Generated FOSSIL batch file {BatchPath} for session {SessionId}", batchPath, sessionId);
        return batchPath;
    }

    public async Task<bool> SetupFossilEnvironmentAsync(string workingDirectory)
    {
        try
        {
            if (!Directory.Exists(workingDirectory))
            {
                Directory.CreateDirectory(workingDirectory);
            }

            // Create environment setup files if needed
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to setup FOSSIL environment in {WorkingDirectory}", workingDirectory);
            return false;
        }
    }

    public async Task<bool> CleanupFossilEnvironmentAsync(string sessionId)
    {
        try
        {
            // Clean up batch files
            var batchPath = Path.Combine(Path.GetTempPath(), $"fossil_{sessionId}.bat");
            if (File.Exists(batchPath))
            {
                File.Delete(batchPath);
            }

            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to cleanup FOSSIL environment for session {SessionId}", sessionId);
            return false;
        }
    }

    #endregion

    #region Interrupt Handling

    public async Task<bool> EnableInterruptsAsync(string sessionId, int interruptVector = 0x14)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.InterruptsEnabled = true;
            session.InterruptVector = interruptVector;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> DisableInterruptsAsync(string sessionId)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.InterruptsEnabled = false;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> SimulateInterruptAsync(string sessionId, int interruptNumber)
    {
        // This would simulate DOS interrupts for FOSSIL communication
        // Implementation would depend on specific door requirements
        await Task.CompletedTask;
        
        _logger.Debug("Simulated interrupt {InterruptNumber} for session {SessionId}", interruptNumber, sessionId);
        return true;
    }

    #endregion

    #region Debugging and Monitoring

    public async Task<bool> EnableFossilLoggingAsync(string sessionId, string logLevel = "info")
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.LoggingEnabled = true;
            session.LogLevel = logLevel;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<bool> DisableFossilLoggingAsync(string sessionId)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return false;

            session.LoggingEnabled = false;
        }

        await Task.CompletedTask;
        return true;
    }

    public async Task<IEnumerable<string>> GetFossilLogAsync(string sessionId, int count = 100)
    {
        await Task.CompletedTask;
        
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
                return Array.Empty<string>();

            return session.LogEntries.TakeLast(count).ToList();
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task RunPipeServerAsync(NamedPipeServerStream pipeServer, FossilSession session, CancellationToken cancellationToken)
    {
        try
        {
            await pipeServer.WaitForConnectionAsync(cancellationToken);
            
            _logger.Information("FOSSIL pipe client connected for session {SessionId}", session.SessionId);

            var buffer = new byte[1024];
            
            while (!cancellationToken.IsCancellationRequested && pipeServer.IsConnected)
            {
                try
                {
                    // Read from pipe (door -> FOSSIL)
                    var bytesRead = await pipeServer.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    if (bytesRead > 0)
                    {
                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        
                        // Send to telnet connection
                        if (session.TelnetConnection != null)
                        {
                            var text = Encoding.GetEncoding("CP437").GetString(data);
                            await session.TelnetConnection.SendAsync(text);
                        }
                    }

                    // Write to pipe (FOSSIL -> door) - from input buffer
                    lock (_lockObject)
                    {
                        if (session.InputBuffer.Count > 0)
                        {
                            var writeData = new byte[Math.Min(1024, session.InputBuffer.Count)];
                            for (int i = 0; i < writeData.Length; i++)
                            {
                                writeData[i] = session.InputBuffer.Dequeue();
                            }
                            
                            pipeServer.WriteAsync(writeData, 0, writeData.Length, cancellationToken);
                        }
                    }

                    await Task.Delay(10, cancellationToken); // Small delay to prevent tight loop
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error in FOSSIL pipe communication for session {SessionId}", session.SessionId);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "FOSSIL pipe server error for session {SessionId}", session.SessionId);
        }
        finally
        {
            try
            {
                if (pipeServer.IsConnected)
                {
                    pipeServer.Disconnect();
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error disconnecting FOSSIL pipe for session {SessionId}", session.SessionId);
            }
        }
    }

    private void ProcessOutputBuffer(string sessionId)
    {
        lock (_lockObject)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session) || 
                session.TelnetConnection == null || 
                session.OutputBuffer.Count == 0)
                return;

            var data = new byte[session.OutputBuffer.Count];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = session.OutputBuffer.Dequeue();
            }

            // Send to telnet connection asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    var text = Encoding.GetEncoding("CP437").GetString(data);
                    await session.TelnetConnection.SendAsync(text);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Error sending data to telnet connection for session {SessionId}", sessionId);
                }
            });
        }
    }

    #endregion

    #region Private Classes

    private class FossilSession
    {
        public string SessionId { get; set; } = string.Empty;
        public TelnetConnection? TelnetConnection { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string ComPort { get; set; } = "COM1";
        public int BaudRate { get; set; }
        public int DataBits { get; set; }
        public int StopBits { get; set; }
        public string Parity { get; set; } = "none";
        public bool IsActive { get; set; }
        public bool IsInitialized { get; set; }
        public Queue<byte> InputBuffer { get; set; } = new();
        public Queue<byte> OutputBuffer { get; set; } = new();
        public long BytesSent { get; set; }
        public long BytesReceived { get; set; }
        
        // Flow control signals
        public bool DtrState { get; set; } = true;
        public bool RtsState { get; set; } = true;
        public bool CtsState { get; set; } = true;
        public bool DsrState { get; set; } = true;
        public bool DcdState { get; set; } = true;
        public bool RiState { get; set; } = false;
        
        // Interrupt handling
        public bool InterruptsEnabled { get; set; }
        public int InterruptVector { get; set; } = 0x14;
        
        // Logging
        public bool LoggingEnabled { get; set; }
        public string LogLevel { get; set; } = "info";
        public List<string> LogEntries { get; set; } = new();
    }

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            // Clean up all active sessions
            lock (_lockObject)
            {
                foreach (var token in _pipeTokens.Values)
                {
                    try { token.Cancel(); } catch { }
                    try { token.Dispose(); } catch { }
                }
                
                foreach (var pipe in _namedPipes.Values)
                {
                    try { pipe.Dispose(); } catch { }
                }
                
                _activeSessions.Clear();
                _namedPipes.Clear();
                _pipeTokens.Clear();
            }
            
            _disposed = true;
        }
    }

    #endregion
}
