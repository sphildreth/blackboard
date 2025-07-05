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

    // CP437 and encoding detection properties
    private bool _supportsCP437 = true; // Default to CP437 support
    private bool _isModernTerminal = false;
    private string _clientSoftware = "Unknown";

    public event EventHandler? Disconnected;

    public EndPoint? RemoteEndPoint => _tcpClient?.Client?.RemoteEndPoint;
    public string RemoteEndPointString => RemoteEndPoint?.ToString() ?? "Unknown";
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    public bool SupportsAnsi => _supportsAnsi;
    public bool SupportsCP437 => _supportsCP437;
    public bool IsModernTerminal => _isModernTerminal;
    public string ClientSoftware => _clientSoftware;
    public string TerminalType => _terminalType;
    public DateTime ConnectedAt { get; }

    public TelnetConnection(TcpClient tcpClient, ILogger logger, int timeoutSeconds)
    {
        _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeoutSeconds = timeoutSeconds;
        _encoding = GetEncodingByName("CP437"); // Default to CP437 for authentic BBS experience
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
        _logger.Information("SendAnsiAsync - SupportsAnsi: {SupportsAnsi}, IsModernTerminal: {IsModernTerminal}, SupportsCP437: {SupportsCP437}, ClientSoftware: {ClientSoftware}", 
            _supportsAnsi, _isModernTerminal, _supportsCP437, _clientSoftware);

        if (!_supportsAnsi)
        {
            // Strip ANSI codes for non-ANSI terminals
            _logger.Information("Stripping ANSI codes for non-ANSI terminal");
            var plainText = StripAnsiCodes(ansiSequence);
            await SendAsync(plainText);
            return;
        }

        // For ANSI-capable terminals, adapt content based on encoding capabilities
        if (_isModernTerminal && !_supportsCP437)
        {
            // Modern terminal - convert CP437 box drawing to Unicode
            _logger.Information("Converting CP437 to Unicode for modern terminal");
            var adaptedContent = ConvertCP437ToUnicode(ansiSequence);
            await SendAsync(adaptedContent);
        }
        else
        {
            // Send raw CP437 content for retro BBS clients or CP437-capable terminals
            _logger.Information("Sending raw CP437 content for retro/CP437-capable terminal");
            await SendAsync(ansiSequence);
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
            _logger.Information("Testing ANSI support for terminal");
            
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
                _logger.Information("ANSI test response received: {Response}", response);
                
                // If we get a response like ESC[row;colR, terminal supports ANSI
                if (response.Contains("[") && response.Contains("R"))
                {
                    _supportsAnsi = true;
                    _terminalType = "ANSI";
                    _logger.Information("Terminal supports ANSI: {Response}", response);
                    
                    // Additional capability detection
                    await DetectClientCapabilitiesAsync();
                }
                else
                {
                    // Modern terminals often support ANSI even if they don't respond to device queries
                    _supportsAnsi = true; // Default to true for better compatibility
                    _terminalType = "ANSI";
                    _logger.Information("Terminal response received but not recognized, defaulting to ANSI support");
                    
                    // Additional capability detection
                    await DetectClientCapabilitiesAsync();
                }
            }
            else
            {
                // No response - but modern terminals via telnet often support ANSI
                _supportsAnsi = true; // Default to true for better compatibility
                _terminalType = "ANSI";
                _logger.Information("No ANSI response from terminal, but defaulting to ANSI support for modern compatibility");
                
                // Additional capability detection
                await DetectClientCapabilitiesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error testing ANSI support, defaulting to ANSI enabled");
            _supportsAnsi = true; // Default to ANSI if test fails
            _terminalType = "ANSI";
            
            try
            {
                await DetectClientCapabilitiesAsync();
            }
            catch (Exception ex2)
            {
                _logger.Error(ex2, "Error in capability detection");
            }
        }
    }

    private async Task DetectClientCapabilitiesAsync()
    {
        try
        {
            _logger.Information("Starting client capability detection");
            
            // Test for modern terminal detection using Primary Device Attributes
            await SendAsync("\x1b[c");
            
            var timeout = Task.Delay(300);
            var readTask = ReadResponseAsync();
            var completedTask = await Task.WhenAny(readTask, timeout);
            
            if (completedTask == readTask)
            {
                var response = readTask.Result;
                _logger.Information("Terminal device attributes: {Response}", response);
                
                // Analyze response to determine terminal capabilities
                AnalyzeTerminalCapabilities(response);
            }
            else
            {
                _logger.Information("No terminal device attributes response received");
            }
            
            // Test for UTF-8 support by checking locale
            // Modern terminals typically support UTF-8, older BBS clients prefer CP437
            var remoteEndPointStr = RemoteEndPointString.ToLower();
            _logger.Information("Remote endpoint: {RemoteEndPoint}", remoteEndPointStr);
            
            // Heuristics for client detection
            if (remoteEndPointStr.Contains("syncterm") || 
                remoteEndPointStr.Contains("netrunner") ||
                remoteEndPointStr.Contains("qodem"))
            {
                _supportsCP437 = true;
                _isModernTerminal = false;
                _clientSoftware = "BBS Terminal";
                _logger.Information("Detected BBS terminal client");
            }
            else
            {
                // Assume modern terminal (telnet, ssh client, etc.)
                _isModernTerminal = true;
                _clientSoftware = "Modern Terminal";
                _logger.Information("Detected modern terminal");
                
                // Modern terminals may not properly display CP437 characters
                // Test with a CP437 specific character
                await TestCP437SupportAsync();
            }
            
            _logger.Information("Detection complete - SupportsAnsi: {SupportsAnsi}, IsModernTerminal: {IsModernTerminal}, SupportsCP437: {SupportsCP437}", 
                _supportsAnsi, _isModernTerminal, _supportsCP437);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error detecting client capabilities");
        }
    }

    private Task TestCP437SupportAsync()
    {
        try
        {
            _logger.Information("Testing CP437 support for terminal type: {TerminalType}", _terminalType);
            
            // Send a CP437 box drawing character and see if we can detect issues
            // This is a heuristic approach since direct detection is difficult
            
            // For now, use heuristics based on client behavior
            // Modern Linux/Mac terminals: prefer UTF-8
            // Windows Command Prompt: may support CP437
            // Dedicated BBS clients: prefer CP437
            
            var userAgent = RemoteEndPointString;
            
            if (_terminalType == "ANSI")
            {
                // For modern terminals connecting via telnet, assume they need Unicode conversion
                _supportsCP437 = false;
                _logger.Information("ANSI terminal detected - setting CP437 support to false for Unicode conversion");
            }
            else
            {
                _supportsCP437 = false;
                _logger.Information("Non-ANSI terminal detected - setting CP437 support to false");
            }
            
            _logger.Information("CP437 support detection result: {SupportsCP437}", _supportsCP437);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error testing CP437 support");
            _supportsCP437 = false; // Default to Unicode conversion for safety
        }
        
        return Task.CompletedTask;
    }

    private void AnalyzeTerminalCapabilities(string deviceAttributes)
    {
        // VT100 series: \x1b[?1;0c
        // VT102: \x1b[?6c  
        // Modern terminals often report VT100+ compatibility
        // BBS terminals may report specific codes
        
        if (deviceAttributes.Contains("?1;") || deviceAttributes.Contains("?6"))
        {
            // VT100/VT102 compatibility - good for ANSI
            _terminalType = "VT100";
        }
        else if (deviceAttributes.Contains("?62;") || deviceAttributes.Contains("?63;"))
        {
            // VT220+ series - modern terminal
            _terminalType = "VT220+";
            _isModernTerminal = true;
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
            "UTF-8" => Encoding.UTF8,
            _ => GetCP437Encoding() // Default fallback
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
            // Fallback to UTF8 if CP437 is not available
            return Encoding.UTF8;
        }
    }

    private string ConvertCP437ToUnicode(string cp437Content)
    {
        if (string.IsNullOrEmpty(cp437Content))
            return cp437Content;

        try
        {
            _logger.Information("Converting content with length {Length}, first 50 chars: {Preview}", 
                cp437Content.Length, cp437Content.Substring(0, Math.Min(50, cp437Content.Length)));

            // Convert CP437 box drawing characters to standard Unicode box drawing
            var result = cp437Content
                // Double line box drawing characters (CP437 -> Unicode)
                .Replace('╔', '┌')  // Double top-left -> single top-left
                .Replace('╗', '┐')  // Double top-right -> single top-right
                .Replace('╚', '└')  // Double bottom-left -> single bottom-left
                .Replace('╝', '┘')  // Double bottom-right -> single bottom-right
                .Replace('║', '│')  // Double vertical -> single vertical
                .Replace('═', '─')  // Double horizontal -> single horizontal
                .Replace('╠', '├')  // Double left T -> single left T
                .Replace('╣', '┤')  // Double right T -> single right T
                .Replace('╦', '┬')  // Double top T -> single top T
                .Replace('╩', '┴')  // Double bottom T -> single bottom T
                .Replace('╬', '┼'); // Double cross -> single cross

            _logger.Information("Conversion result first 50 chars: {Result}", 
                result.Substring(0, Math.Min(50, result.Length)));

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error converting CP437 to Unicode");
            return cp437Content; // Return original on error
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
