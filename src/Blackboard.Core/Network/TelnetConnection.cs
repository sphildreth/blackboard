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
    private readonly Encoding _encoding;
    private bool _isConnected;
    private bool _supportsAnsi = true; // Default to supporting ANSI
    private string _terminalType = "ANSI";

    public event EventHandler? Disconnected;

    public EndPoint? RemoteEndPoint => _tcpClient?.Client?.RemoteEndPoint;
    public string RemoteEndPointString => RemoteEndPoint?.ToString() ?? "Unknown";
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    public bool SupportsAnsi => _supportsAnsi;
    public string TerminalType => _terminalType;
    public DateTime ConnectedAt { get; }

    public TelnetConnection(TcpClient tcpClient, ILogger logger, int timeoutSeconds, string encodingName = "ASCII")
    {
        _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutSeconds = timeoutSeconds;
        _encoding = GetEncodingByName(encodingName);
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
            await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.DO, TelnetOption.WINDOW_SIZE);
            
            // Send a small delay to allow negotiations to complete
            await Task.Delay(100);
            
            // Test ANSI support by sending a query
            await TestAnsiSupportAsync();
            
            _logger.Debug("Telnet connection initialized for {RemoteEndPoint} - Terminal: {TerminalType}, ANSI: {SupportsAnsi}", 
                RemoteEndPoint, _terminalType, _supportsAnsi);
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
            // Use configured encoding for telnet data transmission
            // ASCII/CP437 ensures proper ANSI art alignment and positioning
            var bytes = _encoding.GetBytes(data);
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
        if (_supportsAnsi)
        {
            await SendAsync(ansiSequence);
        }
        else
        {
            // Strip ANSI codes for non-ANSI terminals
            var plainText = StripAnsiCodes(ansiSequence);
            await SendAsync(plainText);
        }
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

                var data = _encoding.GetString(buffer, 0, bytesRead);
                
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

    private async Task TestAnsiSupportAsync()
    {
        try
        {
            // Send a simple ANSI query to test support
            // This sends ESC[6n (Device Status Report) which ANSI terminals respond to
            await SendAsync("\x1b[6n");
            
            // Give a short timeout for response
            var timeout = Task.Delay(500);
            var readTask = ReadResponseAsync();
            var completedTask = await Task.WhenAny(readTask, timeout);
            
            if (completedTask == readTask)
            {
                var response = readTask.Result;
                // If we get a response like ESC[row;colR, terminal supports ANSI
                if (response.Contains("[") && response.Contains("R"))
                {
                    _supportsAnsi = true;
                    _terminalType = "ANSI";
                    _logger.Debug("Terminal supports ANSI: {Response}", response);
                }
                else
                {
                    _supportsAnsi = false;
                    _terminalType = "TTY";
                    _logger.Debug("Terminal does not support ANSI");
                }
            }
            else
            {
                // No response - assume basic terminal
                _supportsAnsi = false;
                _terminalType = "TTY";
                _logger.Debug("No ANSI response from terminal");
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error testing ANSI support, defaulting to ANSI enabled");
            _supportsAnsi = true; // Default to ANSI if test fails
        }
    }

    private async Task<string> ReadResponseAsync()
    {
        var response = new StringBuilder();
        try
        {
            while (response.Length < 20) // Limit response length
            {
                var ch = await ReadCharAsync();
                if (ch == '\0') break;
                response.Append(ch);
                if (ch == 'R') break; // End of ANSI response
            }
            return response.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
            
        // Remove ANSI escape sequences using regex
        // This pattern matches ESC[ followed by any characters and ending with a letter
        var ansiPattern = @"\x1b\[[0-9;]*[a-zA-Z]";
        var result = System.Text.RegularExpressions.Regex.Replace(text, ansiPattern, "");
        
        // Also remove other escape sequences
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\x1b\[[0-9;]*", "");
        
        return result;
    }

    public void Dispose()
    {
        _cancellationTokenSource?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }

    private static Encoding GetEncodingByName(string encodingName)
    {
        // Register code pages encoding provider for CP437 support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        
        return encodingName.ToUpperInvariant() switch
        {
            "ASCII" => Encoding.ASCII,
            "UTF-8" => Encoding.UTF8,
            "CP437" => GetCP437Encoding(),
            _ => Encoding.ASCII // Default fallback
        };
    }

    private static Encoding GetCP437Encoding()
    {
        try
        {
            // Try to get CP437 (original IBM PC encoding) for authentic BBS experience
            // This requires System.Text.Encoding.CodePages package
            return Encoding.GetEncoding(437);
        }
        catch
        {
            // Fallback to ASCII if CP437 is not available
            return Encoding.ASCII;
        }
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
