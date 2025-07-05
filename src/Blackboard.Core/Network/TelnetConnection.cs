using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace Blackboard.Core.Network;

public class TelnetConnection : ITelnetConnection
{
    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly ILogger _logger;
    private readonly int _timeoutSeconds;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _isConnected;

    public event EventHandler? Disconnected;

    public EndPoint? RemoteEndPoint => _tcpClient?.Client?.RemoteEndPoint;
    public string RemoteEndPointString => RemoteEndPoint?.ToString() ?? "Unknown";
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    public DateTime ConnectedAt { get; }

    public TelnetConnection(TcpClient tcpClient, ILogger logger, int timeoutSeconds)
    {
        _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutSeconds = timeoutSeconds;
        _stream = tcpClient.GetStream();
        _cancellationTokenSource = new CancellationTokenSource();
        _isConnected = true;
        ConnectedAt = DateTime.UtcNow;

        // Set timeouts
        _tcpClient.ReceiveTimeout = timeoutSeconds * 1000;
        _tcpClient.SendTimeout = timeoutSeconds * 1000;
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Send initial telnet negotiations
            await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.WILL, TelnetOption.ECHO);
            await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.WILL, TelnetOption.SUPPRESS_GO_AHEAD);
            await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.DO, TelnetOption.TERMINAL_TYPE);
            
            _logger.Debug("Telnet connection initialized for {RemoteEndPoint}", RemoteEndPoint);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize telnet connection for {RemoteEndPoint}", RemoteEndPoint);
            throw;
        }
    }

    public async Task SendAsync(string data)
    {
        if (!IsConnected) return;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            await _stream.WriteAsync(bytes, _cancellationTokenSource.Token);
            await _stream.FlushAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error sending data to {RemoteEndPoint}", RemoteEndPoint);
            await DisconnectAsync();
        }
    }

    public async Task SendLineAsync(string line)
    {
        await SendAsync(line + "\r\n");
    }

    public async Task SendAnsiAsync(string ansiSequence)
    {
        await SendAsync(ansiSequence);
    }

    public async Task<string> ReadLineAsync()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            var buffer = new byte[1024];
            var result = new StringBuilder();
            
            while (IsConnected)
            {
                var bytesRead = await _stream.ReadAsync(buffer, _cancellationTokenSource.Token);
                if (bytesRead == 0)
                {
                    await DisconnectAsync();
                    break;
                }

                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                foreach (char c in data)
                {
                    if (c == '\r' || c == '\n')
                    {
                        if (result.Length > 0)
                            return result.ToString();
                    }
                    else if (c >= 32 && c < 127) // Printable ASCII
                    {
                        result.Append(c);
                    }
                    // Handle telnet commands and other control characters
                    else if (c == (char)TelnetCommand.IAC)
                    {
                        // Handle telnet command sequences
                        // This is a simplified implementation
                        continue;
                    }
                }
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error reading from {RemoteEndPoint}", RemoteEndPoint);
            await DisconnectAsync();
            return string.Empty;
        }
    }

    public async Task<char> ReadCharAsync()
    {
        if (!IsConnected) return '\0';

        try
        {
            while (IsConnected)
            {
                var buffer = new byte[1];
                var bytesRead = await _stream.ReadAsync(buffer, _cancellationTokenSource.Token);
                
                if (bytesRead == 0)
                {
                    await DisconnectAsync();
                    return '\0';
                }

                var b = buffer[0];

                // Handle telnet IAC (Interpret As Command) sequences
                if (b == TelnetCommand.IAC)
                {
                    // Read the next byte (the command)
                    var commandBuffer = new byte[1];
                    var commandBytesRead = await _stream.ReadAsync(commandBuffer, _cancellationTokenSource.Token);
                    
                    if (commandBytesRead == 0)
                    {
                        await DisconnectAsync();
                        return '\0';
                    }

                    var command = commandBuffer[0];

                    // Handle telnet commands
                    if (command == TelnetCommand.DO || command == TelnetCommand.DONT ||
                        command == TelnetCommand.WILL || command == TelnetCommand.WONT)
                    {
                        // Read the option byte
                        var optionBuffer = new byte[1];
                        var optionBytesRead = await _stream.ReadAsync(optionBuffer, _cancellationTokenSource.Token);
                        
                        if (optionBytesRead == 0)
                        {
                            await DisconnectAsync();
                            return '\0';
                        }

                        var option = optionBuffer[0];
                        
                        // Process the telnet negotiation
                        await ProcessTelnetCommand(command, option);
                        
                        // Continue reading for the next character
                        continue;
                    }
                    else if (command == TelnetCommand.IAC)
                    {
                        // Escaped IAC (IAC IAC means literal 255)
                        return (char)255;
                    }
                    else
                    {
                        // Other commands - just skip for now
                        continue;
                    }
                }
                else
                {
                    // Regular character
                    return (char)b;
                }
            }

            return '\0';
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error reading character from {RemoteEndPoint}", RemoteEndPoint);
            await DisconnectAsync();
            return '\0';
        }
    }

    private async Task ProcessTelnetCommand(byte command, byte option)
    {
        // Handle telnet option negotiations
        switch (option)
        {
            case TelnetOption.ECHO:
                if (command == TelnetCommand.DO)
                {
                    // Client wants us to echo - we already said WILL ECHO
                    // No response needed
                }
                else if (command == TelnetCommand.DONT)
                {
                    // Client doesn't want us to echo
                    await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.WONT, TelnetOption.ECHO);
                }
                break;
                
            case TelnetOption.SUPPRESS_GO_AHEAD:
                if (command == TelnetCommand.DO)
                {
                    // Client agrees to suppress go-ahead - good
                    // No response needed
                }
                break;
                
            case TelnetOption.TERMINAL_TYPE:
                if (command == TelnetCommand.WILL)
                {
                    // Client will provide terminal type
                    // No response needed for now
                }
                break;
                
            default:
                // For other options, respond with DON'T/WON'T
                if (command == TelnetCommand.WILL)
                {
                    await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.DONT, option);
                }
                else if (command == TelnetCommand.DO)
                {
                    await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.WONT, option);
                }
                break;
        }
    }

    private async Task SendTelnetCommandAsync(params byte[] command)
    {
        if (!IsConnected) return;

        try
        {
            await _stream.WriteAsync(command, _cancellationTokenSource.Token);
            await _stream.FlushAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error sending telnet command to {RemoteEndPoint}", RemoteEndPoint);
            await DisconnectAsync();
        }
    }

    public Task DisconnectAsync()
    {
        if (!_isConnected) return Task.CompletedTask;

        _isConnected = false;
        
        try
        {
            _cancellationTokenSource.Cancel();
            _stream.Close();
            _tcpClient.Close();
            
            _logger.Debug("Disconnected from {RemoteEndPoint}", RemoteEndPoint);
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error during disconnect from {RemoteEndPoint}", RemoteEndPoint);
        }
        
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }
}

// Telnet protocol constants
public static class TelnetCommand
{
    public const byte IAC = 255;  // Interpret As Command
    public const byte WILL = 251;
    public const byte WONT = 252;
    public const byte DO = 253;
    public const byte DONT = 254;
}

public static class TelnetOption
{
    public const byte ECHO = 1;
    public const byte SUPPRESS_GO_AHEAD = 3;
    public const byte TERMINAL_TYPE = 24;
    public const byte WINDOW_SIZE = 31;
}
