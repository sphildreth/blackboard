using System.Text;
using System.Text.RegularExpressions;
using Blackboard.Core.Network;
using Serilog;

namespace Blackboard.Core.Services;

/// <summary>
///     Service for adapting content based on terminal capabilities
///     Handles conversion between CP437, UTF-8, and plain text formats
/// </summary>
public interface IContentAdaptationService
{
    /// <summary>
    ///     Adapt ANSI content based on terminal capabilities
    /// </summary>
    Task<string> AdaptAnsiContentAsync(string content, ITelnetConnection connection);

    /// <summary>
    ///     Convert CP437 content to UTF-8 with Unicode box drawing characters
    /// </summary>
    string ConvertCP437ToUtf8(string cp437Content);

    /// <summary>
    ///     Strip ANSI codes and convert to plain text
    /// </summary>
    string ConvertToPlainText(string ansiContent);

    /// <summary>
    ///     Get the best encoding for a given connection
    /// </summary>
    Encoding GetBestEncoding(ITelnetConnection connection);
}

public class ContentAdaptationService : IContentAdaptationService
{
    // CP437 to Unicode mapping for box drawing characters
    private static readonly Dictionary<byte, string> CP437ToUnicode = new()
    {
        // Box drawing characters
        { 0xC9, "┌" }, // Top-left corner
        { 0xBB, "┐" }, // Top-right corner
        { 0xC8, "└" }, // Bottom-left corner
        { 0xBC, "┘" }, // Bottom-right corner
        { 0xCD, "─" }, // Horizontal line
        { 0xBA, "│" }, // Vertical line
        { 0xCC, "├" }, // Left T
        { 0xB9, "┤" }, // Right T
        { 0xCB, "┬" }, // Top T
        { 0xCA, "┴" }, // Bottom T
        { 0xCE, "┼" }, // Cross
        { 0xC4, "─" }, // Single horizontal
        { 0xB3, "│" }, // Single vertical
        { 0xDA, "┌" }, // Single top-left
        { 0xBF, "┐" }, // Single top-right
        { 0xC0, "└" }, // Single bottom-left
        { 0xD9, "┘" }, // Single bottom-right
        { 0xC3, "├" }, // Single left T
        { 0xB4, "┤" }, // Single right T
        { 0xC2, "┬" }, // Single top T
        { 0xC1, "┴" }, // Single bottom T
        { 0xC5, "┼" }, // Single cross
        // Double-line characters
        { 0xD5, "╒" }, // Double top-left mixed
        { 0xB8, "╕" }, // Double top-right mixed
        { 0xD4, "╘" }, // Double bottom-left mixed
        { 0xBE, "╛" }, // Double bottom-right mixed
        { 0xD6, "╓" }, // Double left mixed
        { 0xB7, "╖" }, // Double right mixed
        { 0xD3, "╙" }, // Double left mixed
        { 0xBD, "╜" } // Double right mixed
    };

    private readonly ILogger _logger;

    public ContentAdaptationService(ILogger logger)
    {
        _logger = logger;
    }

    public Task<string> AdaptAnsiContentAsync(string content, ITelnetConnection connection)
    {
        try
        {
            // Decision tree for content adaptation
            if (!connection.SupportsAnsi)
                // Terminal doesn't support ANSI - convert to plain text
                return Task.FromResult(ConvertToPlainText(content));

            if (connection.SupportsCP437)
                // Terminal supports CP437 - send as-is (already in CP437)
                return Task.FromResult(content);

            if (connection.IsModernTerminal)
                // Modern terminal - convert CP437 box drawing to Unicode
                return Task.FromResult(ConvertCP437ToUtf8(content));

            // Fallback: assume basic ANSI support
            return Task.FromResult(content);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error adapting content for terminal {TerminalType}", connection.TerminalType);
            return Task.FromResult(content); // Return original content on error
        }
    }

    public string ConvertCP437ToUtf8(string cp437Content)
    {
        if (string.IsNullOrEmpty(cp437Content))
            return cp437Content;

        try
        {
            // Convert CP437 bytes to Unicode characters
            var cp437Bytes = Encoding.GetEncoding(437).GetBytes(cp437Content);
            var result = new StringBuilder();

            foreach (var b in cp437Bytes)
                if (CP437ToUnicode.TryGetValue(b, out var unicodeChar))
                    result.Append(unicodeChar);
                else if (b >= 32 && b <= 126)
                    // Standard ASCII - keep as-is
                    result.Append((char)b);
                else if (b == 10 || b == 13)
                    // Line endings
                    result.Append((char)b);
                else
                    // Other characters - try to convert or substitute
                    try
                    {
                        var unicodeEquivalent = Encoding.GetEncoding(437).GetString(new[] { b });
                        result.Append(unicodeEquivalent);
                    }
                    catch
                    {
                        result.Append('?'); // Substitute with question mark
                    }

            return result.ToString();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error converting CP437 to UTF-8");
            return cp437Content; // Return original on error
        }
    }

    public string ConvertToPlainText(string ansiContent)
    {
        if (string.IsNullOrEmpty(ansiContent))
            return ansiContent;

        try
        {
            // Remove ANSI escape sequences
            var plainText = StripAnsiCodes(ansiContent);

            // Convert box drawing characters to ASCII equivalents
            plainText = ConvertBoxDrawingToAscii(plainText);

            return plainText;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error converting to plain text");
            return ansiContent;
        }
    }

    public Encoding GetBestEncoding(ITelnetConnection connection)
    {
        if (connection.SupportsCP437)
            try
            {
                return Encoding.GetEncoding(437);
            }
            catch
            {
                // Fallback if CP437 not available
            }

        if (connection.IsModernTerminal) return Encoding.UTF8;

        // Default fallback
        return Encoding.ASCII;
    }

    private string StripAnsiCodes(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove ANSI escape sequences
        var ansiPattern = @"\x1b\[[0-9;]*[a-zA-Z]";
        var result = Regex.Replace(text, ansiPattern, "");

        // Remove other escape sequences
        result = Regex.Replace(result, @"\x1b\[[0-9;]*", "");

        return result;
    }

    private string ConvertBoxDrawingToAscii(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var result = text;

        // Convert Unicode box drawing to ASCII
        result = result.Replace("┌", "+");
        result = result.Replace("┐", "+");
        result = result.Replace("└", "+");
        result = result.Replace("┘", "+");
        result = result.Replace("├", "+");
        result = result.Replace("┤", "+");
        result = result.Replace("┬", "+");
        result = result.Replace("┴", "+");
        result = result.Replace("┼", "+");
        result = result.Replace("─", "-");
        result = result.Replace("│", "|");

        // Double-line characters
        result = result.Replace("╒", "+");
        result = result.Replace("╕", "+");
        result = result.Replace("╘", "+");
        result = result.Replace("╛", "+");
        result = result.Replace("╓", "+");
        result = result.Replace("╖", "+");
        result = result.Replace("╙", "+");
        result = result.Replace("╜", "+");
        result = result.Replace("═", "=");
        result = result.Replace("║", "|");

        return result;
    }
}