using System.Text;
using Serilog;

namespace Blackboard.Core.Network;

/// <summary>
///     Handles ANSI file reading and sending with proper encoding
/// </summary>
public class AnsiFileHandler
{
    private readonly ILogger _logger;

    public AnsiFileHandler(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Reads an ANSI file and returns it as raw string for sending to client
    /// </summary>
    public async Task<string> ReadAnsiFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.Warning("ANSI file not found: {FilePath}", filePath);
            return string.Empty;
        }

        try
        {
            // Read the file as raw bytes and convert to string for telnet sending
            // The files now have proper ESC sequences, so we just need to read them as-is
            var fileBytes = await File.ReadAllBytesAsync(filePath);

            // Convert bytes to string using Latin-1 encoding to preserve raw bytes
            return Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading ANSI file: {FilePath}", filePath);
            return $"[Error loading ANSI file: {Path.GetFileName(filePath)}]\r\n";
        }
    }

    /// <summary>
    ///     Sends an ANSI string to a connection
    /// </summary>
    public async Task SendAnsiAsync(ITelnetConnection connection, string ansiContent)
    {
        if (string.IsNullOrEmpty(ansiContent))
            return;

        try
        {
            // Send the ANSI content as-is - it now has proper escape sequences
            await connection.SendAsync(ansiContent);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending ANSI content to connection");
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
}