using System.Text;
using Blackboard.Core.Network;
using Serilog;

namespace Blackboard.Core.Services;

public class KeyboardHandlerService : IKeyboardHandlerService
{
    private readonly Dictionary<string, SpecialKey> _escapeSequences;
    private readonly ILogger _logger;

    public KeyboardHandlerService(ILogger logger)
    {
        _logger = logger;
        _escapeSequences = InitializeEscapeSequences();
    }

    public async Task<KeyInput> ReadKeyAsync(ITelnetConnection connection)
    {
        try
        {
            var ch = await connection.ReadCharAsync();
            
            // Handle escape sequences (special keys)
            if (ch == '\x1b') // ESC
                return await ReadEscapeSequenceAsync(connection);

            // Handle common control characters
            return ch switch
            {
                '\r' or '\n' => new KeyInput(SpecialKey.Enter),
                '\x08' or '\x7f' => new KeyInput(SpecialKey.Backspace), // BS or DEL
                '\t' => new KeyInput(SpecialKey.Tab),
                '\x1b' => new KeyInput(SpecialKey.Escape),
                _ => new KeyInput(ch)
            };
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error reading key input");
            return new KeyInput('\0');
        }
    }

    public async Task<string> ReadLineAsync(ITelnetConnection connection, bool echoInput = true)
    {
        var input = new StringBuilder();

        try
        {
            while (connection.IsConnected)
            {
                var key = await ReadKeyAsync(connection);
                
                // Skip null characters completely
                if (key.Character == '\0' && !key.IsSpecial)
                {
                    continue;
                }

                if (key.IsSpecial)
                {
                    switch (key.SpecialKey)
                    {
                        case SpecialKey.Enter:
                            // If input is empty, consume any lingering newlines and continue waiting
                            if (input.Length == 0)
                            {
                                if (echoInput)
                                    await connection.SendAsync("\r\n");
                                continue;
                            }
                            
                            // Return the accumulated input
                            if (echoInput)
                                await connection.SendAsync("\r\n");
                            return input.ToString();

                        case SpecialKey.Backspace:
                            if (input.Length > 0)
                            {
                                input.Length--;
                                if (echoInput) await connection.SendAsync("\b \b"); // Backspace, space, backspace
                            }
                            break;

                        case SpecialKey.Escape:
                            // Clear current input
                            if (echoInput && input.Length > 0)
                            {
                                await connection.SendAsync(new string('\b', input.Length));
                                await connection.SendAsync(new string(' ', input.Length));
                                await connection.SendAsync(new string('\b', input.Length));
                            }
                            input.Clear();
                            break;
                    }
                }
                else if (char.IsControl(key.Character))
                {
                    // Skip control characters
                    continue;
                }
                else
                {
                    // Regular character
                    input.Append(key.Character);
                    if (echoInput) await connection.SendAsync(key.Character.ToString());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error reading line input");
        }

        return input.ToString();
    }

    public bool IsSpecialKey(KeyInput key)
    {
        return key.IsSpecial;
    }

    public async Task SetEchoModeAsync(ITelnetConnection connection, bool enabled)
    {
        try
        {
            if (enabled)
                // Send WILL ECHO to enable local echo
                await connection.SendAsync("\xFF\xFB\x01"); // IAC WILL ECHO
            else
                // Send WONT ECHO to disable local echo
                await connection.SendAsync("\xFF\xFC\x01"); // IAC WONT ECHO
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error setting echo mode to {Enabled}", enabled);
        }
    }

    private async Task<KeyInput> ReadEscapeSequenceAsync(ITelnetConnection connection)
    {
        try
        {
            var sequence = new StringBuilder("\x1b");

            // Read the next character(s) to determine the escape sequence
            var timeout = Task.Delay(100); // 100ms timeout for escape sequences
            var readTask = connection.ReadCharAsync();

            var completedTask = await Task.WhenAny(readTask, timeout);
            if (completedTask == timeout)
                // Timeout - just return ESC
                return new KeyInput(SpecialKey.Escape);

            var nextChar = readTask.Result;
            sequence.Append(nextChar);

            // Handle common escape sequences
            if (nextChar == '[')
                // ANSI escape sequence
                return await ReadAnsiEscapeSequenceAsync(connection, sequence);

            if (nextChar == 'O')
                // Function key sequence
                return await ReadFunctionKeySequenceAsync(connection, sequence);

            // Unknown escape sequence, return as ESC
            return new KeyInput(SpecialKey.Escape);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error reading escape sequence");
            return new KeyInput(SpecialKey.Escape);
        }
    }

    private async Task<KeyInput> ReadAnsiEscapeSequenceAsync(ITelnetConnection connection, StringBuilder sequence)
    {
        try
        {
            // Read until we get a letter (end of ANSI sequence)
            var timeout = Task.Delay(200);

            while (sequence.Length < 10) // Prevent infinite sequences
            {
                var readTask = connection.ReadCharAsync();
                var completedTask = await Task.WhenAny(readTask, timeout);

                if (completedTask == timeout)
                    break;

                var ch = readTask.Result;
                sequence.Append(ch);

                if (char.IsLetter(ch))
                    break;
            }

            var escapeString = sequence.ToString();

            // Check if we recognize this escape sequence
            if (_escapeSequences.TryGetValue(escapeString, out var specialKey)) return new KeyInput(specialKey);

            return new KeyInput(SpecialKey.None);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error reading ANSI escape sequence");
            return new KeyInput(SpecialKey.None);
        }
    }

    private async Task<KeyInput> ReadFunctionKeySequenceAsync(ITelnetConnection connection, StringBuilder sequence)
    {
        try
        {
            var timeout = Task.Delay(100);
            var readTask = connection.ReadCharAsync();
            var completedTask = await Task.WhenAny(readTask, timeout);

            if (completedTask == timeout)
                return new KeyInput(SpecialKey.None);

            var ch = readTask.Result;
            sequence.Append(ch);

            var escapeString = sequence.ToString();

            if (_escapeSequences.TryGetValue(escapeString, out var specialKey)) return new KeyInput(specialKey);

            return new KeyInput(SpecialKey.None);
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Error reading function key sequence");
            return new KeyInput(SpecialKey.None);
        }
    }

    private Dictionary<string, SpecialKey> InitializeEscapeSequences()
    {
        return new Dictionary<string, SpecialKey>
        {
            // Arrow keys
            ["\x1b[A"] = SpecialKey.ArrowUp,
            ["\x1b[B"] = SpecialKey.ArrowDown,
            ["\x1b[C"] = SpecialKey.ArrowRight,
            ["\x1b[D"] = SpecialKey.ArrowLeft,

            // Home/End
            ["\x1b[H"] = SpecialKey.Home,
            ["\x1b[F"] = SpecialKey.End,
            ["\x1b[1~"] = SpecialKey.Home,
            ["\x1b[4~"] = SpecialKey.End,

            // Page Up/Down
            ["\x1b[5~"] = SpecialKey.PageUp,
            ["\x1b[6~"] = SpecialKey.PageDown,

            // Delete
            ["\x1b[3~"] = SpecialKey.Delete,

            // Function keys (F1-F12)
            ["\x1bOP"] = SpecialKey.F1,
            ["\x1bOQ"] = SpecialKey.F2,
            ["\x1bOR"] = SpecialKey.F3,
            ["\x1bOS"] = SpecialKey.F4,
            ["\x1b[15~"] = SpecialKey.F5,
            ["\x1b[17~"] = SpecialKey.F6,
            ["\x1b[18~"] = SpecialKey.F7,
            ["\x1b[19~"] = SpecialKey.F8,
            ["\x1b[20~"] = SpecialKey.F9,
            ["\x1b[21~"] = SpecialKey.F10,
            ["\x1b[23~"] = SpecialKey.F11,
            ["\x1b[24~"] = SpecialKey.F12
        };
    }
}