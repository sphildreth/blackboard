using System.Text;
using Serilog;

namespace Blackboard.Core.Network;

/// <summary>
///     Handles both ASCII and ANSI file reading and sending with proper encoding
/// </summary>
public class ScreenFileHandler
{
    private readonly ILogger _logger;

    public ScreenFileHandler(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Reads a screen file (ASCII preferred, ANSI fallback) and returns it as string for sending to client
    /// </summary>
    public async Task<string> ReadScreenFileAsync(string baseFileName, bool preferAnsi = false)
    {
        var asciiFile = $"{baseFileName}.asc";
        var ansiFile = $"{baseFileName}.ans";

        // Check which files exist
        var asciiExists = File.Exists(asciiFile);
        var ansiExists = File.Exists(ansiFile);

        string targetFile;
        bool isAnsiContent = false;

        if (preferAnsi && ansiExists)
        {
            targetFile = ansiFile;
            isAnsiContent = true;
            _logger.Information("Using ANSI file: {FilePath}", targetFile);
        }
        else if (asciiExists)
        {
            targetFile = asciiFile;
            _logger.Information("Using ASCII file: {FilePath}", targetFile);
        }
        else if (ansiExists)
        {
            targetFile = ansiFile;
            isAnsiContent = true;
            _logger.Information("Falling back to ANSI file: {FilePath}", targetFile);
        }
        else
        {
            _logger.Warning("Neither ASCII nor ANSI file found for: {BaseFileName}", baseFileName);
            return $"[Screen file not found: {baseFileName}]\r\n";
        }

        try
        {
            if (isAnsiContent)
            {
                // Read ANSI file with proper encoding for raw bytes
                var fileBytes = await File.ReadAllBytesAsync(targetFile);
                return Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
            }
            else
            {
                // Read ASCII file as UTF-8 text
                return await File.ReadAllTextAsync(targetFile, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading screen file: {FilePath}", targetFile);
            return $"[Error loading screen file: {Path.GetFileName(targetFile)}]\r\n";
        }
    }

    /// <summary>
    ///     Reads a screen file with a full path (detects ASCII vs ANSI by extension)
    /// </summary>
    public async Task<string> ReadScreenFileByPathAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            _logger.Warning("Screen file not found: {FilePath}", filePath);
            return $"[Screen file not found: {Path.GetFileName(filePath)}]\r\n";
        }

        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            bool isAnsiContent = extension == ".ans";

            if (isAnsiContent)
            {
                // Read ANSI file with proper encoding for raw bytes
                var fileBytes = await File.ReadAllBytesAsync(filePath);
                return Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
            }
            else
            {
                // Read ASCII file as UTF-8 text
                return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reading screen file: {FilePath}", filePath);
            return $"[Error loading screen file: {Path.GetFileName(filePath)}]\r\n";
        }
    }

    /// <summary>
    ///     Sends screen content to a connection based on the connection's capabilities
    /// </summary>
    public async Task SendScreenAsync(ITelnetConnection connection, string screenContent)
    {
        if (string.IsNullOrEmpty(screenContent))
            return;

        try
        {
            // If the content contains ANSI escape sequences, use SendAnsiAsync which handles ASCII/ANSI detection
            if (screenContent.Contains("\x1b[") || screenContent.Contains("["))
            {
                await connection.SendAnsiAsync(screenContent);
            }
            else
            {
                // Pure ASCII content, send directly
                await connection.SendAsync(screenContent);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error sending screen content to connection");
        }
    }
}
