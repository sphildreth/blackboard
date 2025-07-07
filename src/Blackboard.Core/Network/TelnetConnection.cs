using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;

namespace Blackboard.Core.Network;

public class TelnetConnection : ITelnetConnection
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Encoding _encoding;
    private readonly ILogger _logger;
    private readonly NetworkStream _stream;
    private readonly TcpClient _tcpClient;
    private readonly int _timeoutSeconds;
    private bool _isConnected;

    // CP437 and encoding detection properties

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

    public event EventHandler? Disconnected;

    public EndPoint? RemoteEndPoint => _tcpClient?.Client?.RemoteEndPoint;
    public string RemoteEndPointString => RemoteEndPoint?.ToString() ?? "Unknown";
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    public bool SupportsAnsi { get; private set; } = false; // Default to ASCII mode

    public bool SupportsCP437 { get; private set; } = false;

    public bool IsModernTerminal { get; private set; }

    public string ClientSoftware { get; private set; } = "Unknown";

    public string TerminalType { get; private set; } = "ASCII"; // Default to ASCII mode

    public bool UserRequestedAnsi { get; private set; } = false; // Track user preference

    public DateTime ConnectedAt { get; }

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

            // Default to ASCII mode - let user explicitly request ANSI if they want it
            await DetectClientCapabilitiesAsync();

            _logger.Debug("Telnet connection initialized for {RemoteEndPoint} - Terminal: {TerminalType}, ANSI: {SupportsAnsi}, UserRequestedAnsi: {UserRequestedAnsi}",
                RemoteEndPoint, TerminalType, SupportsAnsi, UserRequestedAnsi);
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
            // For ANSI content, send raw bytes using Latin-1 encoding to preserve byte values
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(data);
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
        _logger.Information("SendAnsiAsync - UserRequestedAnsi: {UserRequestedAnsi}, SupportsAnsi: {SupportsAnsi}, IsModernTerminal: {IsModernTerminal}, SupportsCP437: {SupportsCP437}, ClientSoftware: {ClientSoftware}",
            UserRequestedAnsi, SupportsAnsi, IsModernTerminal, SupportsCP437, ClientSoftware);

        // Only send ANSI if user explicitly requested it AND terminal supports it
        if (!UserRequestedAnsi || !SupportsAnsi)
        {
            // Strip ANSI codes for ASCII mode
            _logger.Information("Stripping ANSI codes - ASCII mode active");
            var plainText = StripAnsiCodes(ansiSequence);
            await SendAsync(plainText);
            return;
        }

        // For ANSI-capable terminals, adapt content based on encoding capabilities
        if (IsModernTerminal && !SupportsCP437)
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

                foreach (var c in data)
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
                    }
                    else if (command == TelnetCommand.IAC)
                    {
                        // Escaped IAC (IAC IAC means literal 255)
                        return (char)255;
                    }
                    // Other commands - just skip for now
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

    public async Task SendBytesAsync(byte[] data)
    {
        if (!IsConnected || data == null || data.Length == 0) return;

        try
        {
            // Send raw bytes directly - no encoding conversion
            await _stream.WriteAsync(data, _cancellationTokenSource.Token);
            await _stream.FlushAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error sending raw bytes to {RemoteEndPoint}", RemoteEndPoint);
            await DisconnectAsync();
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
                    await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.DONT, option);
                else if (command == TelnetCommand.DO) await SendTelnetCommandAsync(TelnetCommand.IAC, TelnetCommand.WONT, option);
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
                    SupportsAnsi = true;
                    TerminalType = "ANSI";
                    _logger.Information("Terminal supports ANSI: {Response}", response);

                    // Additional capability detection
                    await DetectClientCapabilitiesAsync();
                }
                else
                {
                    // Modern terminals often support ANSI even if they don't respond to device queries
                    SupportsAnsi = true; // Default to true for better compatibility
                    TerminalType = "ANSI";
                    _logger.Information("Terminal response received but not recognized, defaulting to ANSI support");

                    // Additional capability detection
                    await DetectClientCapabilitiesAsync();
                }
            }
            else
            {
                // No response - but modern terminals via telnet often support ANSI
                SupportsAnsi = true; // Default to true for better compatibility
                TerminalType = "ANSI";
                _logger.Information("No ANSI response from terminal, but defaulting to ANSI support for modern compatibility");

                // Additional capability detection
                await DetectClientCapabilitiesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error testing ANSI support, defaulting to ANSI enabled");
            SupportsAnsi = true; // Default to ANSI if test fails
            TerminalType = "ANSI";

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

            // Heuristics for client detection
            var remoteEndPointStr = RemoteEndPointString.ToLower();
            _logger.Information("Remote endpoint: {RemoteEndPoint}", remoteEndPointStr);

            // Detect dedicated BBS clients
            if (remoteEndPointStr.Contains("syncterm") ||
                remoteEndPointStr.Contains("netrunner") ||
                remoteEndPointStr.Contains("qodem"))
            {
                // BBS clients can support ANSI but don't enable by default
                IsModernTerminal = false;
                ClientSoftware = "BBS Terminal";
                _logger.Information("Detected BBS terminal client - ANSI capabilities available but not enabled by default");
            }
            else
            {
                // Modern terminal (telnet, ssh client, etc.)
                IsModernTerminal = true;
                ClientSoftware = "Modern Terminal";
                _logger.Information("Detected modern terminal - ASCII mode by default");
            }

            // Default to ASCII mode - user must explicitly request ANSI
            SupportsAnsi = false;
            UserRequestedAnsi = false;
            TerminalType = "ASCII";
            SupportsCP437 = false;

            _logger.Information("Detection complete - SupportsAnsi: {SupportsAnsi}, IsModernTerminal: {IsModernTerminal}, UserRequestedAnsi: {UserRequestedAnsi}",
                SupportsAnsi, IsModernTerminal, UserRequestedAnsi);
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
            _logger.Information("Testing CP437 support for terminal type: {TerminalType}", TerminalType);

            // Send a CP437 box drawing character and see if we can detect issues
            // This is a heuristic approach since direct detection is difficult

            // For now, use heuristics based on client behavior
            // Modern Linux/Mac terminals: prefer UTF-8
            // Windows Command Prompt: may support CP437
            // Dedicated BBS clients: prefer CP437

            var userAgent = RemoteEndPointString;

            if (TerminalType == "ANSI")
            {
                // Check if this is a telnet connection from a modern terminal emulator
                // Modern terminal emulators connecting via telnet often report ANSI but can handle Unicode
                var clientSoftware = ClientSoftware?.ToLower() ?? "";
                var remoteEndpoint = RemoteEndPointString.ToLower();

                // If it's a telnet connection and not a dedicated BBS client, assume Unicode support
                if (!clientSoftware.Contains("syncterm") &&
                    !clientSoftware.Contains("netrunner") &&
                    !clientSoftware.Contains("qodem") &&
                    !remoteEndpoint.Contains("bbs"))
                {
                    SupportsCP437 = false;
                    _logger.Information("Modern terminal via telnet detected - setting CP437 support to false for Unicode conversion");
                }
                else
                {
                    SupportsCP437 = true;
                    _logger.Information("BBS client detected - setting CP437 support to true");
                }
            }
            else
            {
                SupportsCP437 = false;
                _logger.Information("Non-ANSI terminal detected - setting CP437 support to false");
            }

            _logger.Information("CP437 support detection result: {SupportsCP437}", SupportsCP437);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error testing CP437 support");
            SupportsCP437 = false; // Default to Unicode conversion for safety
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
            TerminalType = "VT100";
        }
        else if (deviceAttributes.Contains("?62;") || deviceAttributes.Contains("?63;"))
        {
            // VT220+ series - modern terminal
            TerminalType = "VT220+";
            IsModernTerminal = true;
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

    /// <summary>
    ///     Converts a CP437 byte to its Unicode equivalent
    /// </summary>
    private static char ConvertCP437Byte(byte b)
    {
        // CP437 character mapping for extended ASCII (128-255)
        return b switch
        {
            // Box drawing characters
            179 => '│', // Single vertical line
            180 => '┤', // Single vertical and left
            191 => '┐', // Single down and left
            192 => '└', // Single up and right
            193 => '┴', // Single up and horizontal
            194 => '┬', // Single down and horizontal
            195 => '├', // Single vertical and right
            196 => '─', // Single horizontal line
            197 => '┼', // Single vertical and horizontal
            217 => '┘', // Single up and left
            218 => '┌', // Single down and right

            // Double line box drawing
            186 => '║', // Double vertical line
            187 => '╗', // Double down and left
            188 => '╝', // Double up and left
            200 => '╚', // Double up and right
            201 => '╔', // Double down and right
            202 => '╩', // Double up and horizontal
            203 => '╦', // Double down and horizontal
            204 => '╠', // Double vertical and right
            205 => '═', // Double horizontal line
            206 => '╬', // Double vertical and horizontal
            185 => '╣', // Double vertical and left

            // Block characters
            176 => '░', // Light shade
            177 => '▒', // Medium shade
            178 => '▓', // Dark shade
            219 => '█', // Full block
            220 => '▄', // Lower half block
            221 => '▌', // Left half block
            222 => '▐', // Right half block
            223 => '▀', // Upper half block

            // Arrow characters
            16 => '►', // Right-pointing triangle
            17 => '◄', // Left-pointing triangle
            30 => '▲', // Up-pointing triangle
            31 => '▼', // Down-pointing triangle

            // Other special characters commonly used in ANSI art
            1 => '☺', // White smiling face
            2 => '☻', // Black smiling face
            3 => '♥', // Black heart suit
            4 => '♦', // Black diamond suit
            5 => '♣', // Black club suit
            6 => '♠', // Black spade suit
            7 => '•', // Bullet
            8 => '◘', // Inverse bullet
            9 => '○', // White circle
            10 => '◙', // Inverse white circle
            11 => '♂', // Male sign
            12 => '♀', // Female sign
            13 => '♪', // Eighth note
            14 => '♫', // Beamed eighth notes
            15 => '☼', // White sun with rays

            // For all other characters, use the Unicode equivalent or fallback
            _ => (char)b
        };
    }    
    
    private string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove ANSI escape sequences using regex
        // This pattern matches ESC[ followed by any characters and ending with a letter
        var ansiPattern = @"\x1b\[[0-9;]*[a-zA-Z]";
        var result = Regex.Replace(text, ansiPattern, "");

        // Also remove other escape sequences
        result = Regex.Replace(result, @"\x1b\[[0-9;]*", "");

        return result;
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

            // The content has already been read with CP437 encoding, so we just need to 
            // map any characters that don't display properly in modern terminals
            var result = new StringBuilder(cp437Content.Length);

            foreach (var c in cp437Content)
            {
                var unicodeChar = c switch
                {
                    // The content is already properly decoded from CP437, so these should already be
                    // the correct Unicode box drawing characters. Just pass them through.
                    '╔' => '┌', // Convert double-line to single-line for better compatibility
                    '╗' => '┐',
                    '╚' => '└',
                    '╝' => '┘',
                    '║' => '│',
                    '═' => '─',
                    '╠' => '├',
                    '╣' => '┤',
                    '╦' => '┬',
                    '╩' => '┴',
                    '╬' => '┼',
                    // Keep single-line box drawing as-is
                    '┌' => '┌',
                    '┐' => '┐',
                    '└' => '└',
                    '┘' => '┘',
                    '│' => '│',
                    '─' => '─',
                    '├' => '├',
                    '┤' => '┤',
                    '┬' => '┬',
                    '┴' => '┴',
                    '┼' => '┼',
                    // Keep other block characters as-is (they should display fine)
                    '█' => '█', // Full block
                    '▀' => '▀', // Upper half block
                    '▄' => '▄', // Lower half block
                    '▌' => '▌', // Left half block
                    '▐' => '▐', // Right half block
                    '░' => '░', // Light shade
                    '▒' => '▒', // Medium shade
                    '▓' => '▓', // Dark shade
                    _ => c // Keep all other characters as-is
                };

                result.Append(unicodeChar);
            }

            var resultString = result.ToString();
            _logger.Information("Conversion result first 50 chars: {Result}",
                resultString.Substring(0, Math.Min(50, resultString.Length)));

            return resultString;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error converting CP437 to Unicode");
            return cp437Content; // Return original on error
        }
    }

    public void EnableAnsiMode()
    {
        UserRequestedAnsi = true;
        SupportsAnsi = true;
        TerminalType = "ANSI";
        _logger.Information("ANSI mode enabled for connection {RemoteEndPoint}", RemoteEndPoint);
    }

    public void DisableAnsiMode()
    {
        UserRequestedAnsi = false;
        SupportsAnsi = false;
        TerminalType = "ASCII";
        _logger.Information("ANSI mode disabled for connection {RemoteEndPoint}", RemoteEndPoint);
    }
}

// Telnet protocol constants
public static class TelnetCommand
{
    public const byte IAC = 255; // Interpret As Command
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