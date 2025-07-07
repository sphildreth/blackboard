using System.Collections.Concurrent;
using System.Text;
using Blackboard.Core.Models;
using Serilog;

namespace Blackboard.Core.Services;

public class AnsiScreenService : IAnsiScreenService
{
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, string> _screenCache;
    private readonly string _screensDirectory;
    private readonly ITemplateVariableProcessor _templateProcessor;

    public AnsiScreenService(string screensDirectory, ILogger logger, ITemplateVariableProcessor templateProcessor)
    {
        _screensDirectory = screensDirectory;
        _logger = logger;
        _templateProcessor = templateProcessor;
        _screenCache = new ConcurrentDictionary<string, string>();

        // Setup file watcher for hot-reload (watch both ASCII and ANSI files)
        if (Directory.Exists(_screensDirectory))
        {
            _fileWatcher = new FileSystemWatcher(_screensDirectory, "*.*")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            _fileWatcher.Changed += OnScreenFileChanged;
            _fileWatcher.Created += OnScreenFileChanged;
            _fileWatcher.Deleted += OnScreenFileChanged;
            _fileWatcher.EnableRaisingEvents = true;
        }
    }

    public async Task<string> RenderScreenAsync(string screenName, UserContext context, bool preferAnsi = false)
    {
        try
        {
            var screenContent = await LoadScreenContentAsync(screenName, preferAnsi);
            if (string.IsNullOrEmpty(screenContent))
            {
                _logger.Warning("Screen {ScreenName} not found, using fallback", screenName);
                screenContent = await GetFallbackScreenAsync(screenName, preferAnsi);
            }

            // Process template variables
            return await _templateProcessor.ProcessVariablesAsync(screenContent, context);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error rendering screen {ScreenName}", screenName);
            return $"[Error loading screen: {screenName}]";
        }
    }

    public Task<bool> ScreenExistsAsync(string screenName)
    {
        // Check for both ASCII and ANSI variants
        var asciiPath = GetScreenFilePath(screenName, false);
        var ansiPath = GetScreenFilePath(screenName, true);
        return Task.FromResult(File.Exists(asciiPath) || File.Exists(ansiPath));
    }

    public async Task<string> GetFallbackScreenAsync(string screenName, bool preferAnsi = false)
    {
        // Try to load a default screen - prefer ASCII unless ANSI is requested
        var defaultFileName = preferAnsi ? "default.ans" : "default.asc";
        var defaultPath = Path.Combine(_screensDirectory, "defaults", defaultFileName);
        
        if (File.Exists(defaultPath))
        {
            // Read as raw bytes and convert using Latin-1 to preserve byte values
            var fileBytes = await File.ReadAllBytesAsync(defaultPath);
            return Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        }

        // Fallback to the other format if primary doesn't exist
        var fallbackFileName = preferAnsi ? "default.asc" : "default.ans";
        var fallbackPath = Path.Combine(_screensDirectory, "defaults", fallbackFileName);
        
        if (File.Exists(fallbackPath))
        {
            var fileBytes = await File.ReadAllBytesAsync(fallbackPath);
            return Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        }

        // Return a simple text fallback
        return $"=== {screenName.ToUpper()} ===\r\n[Screen not available]\r\n";
    }

    public void ClearCache()
    {
        _screenCache.Clear();
        _logger.Information("ANSI screen cache cleared");
    }

    public bool EvaluateConditions(ScreenConditions conditions, UserContext context)
    {
        try
        {
            // Security level check
            if (conditions.MinSecurityLevel.HasValue &&
                (int)(context.User?.SecurityLevel ?? SecurityLevel.User) < conditions.MinSecurityLevel.Value)
                return false;

            if (conditions.MaxSecurityLevel.HasValue &&
                (int)(context.User?.SecurityLevel ?? SecurityLevel.User) > conditions.MaxSecurityLevel.Value)
                return false;

            // First time user check
            if (conditions.FirstTimeUser.HasValue && context.User != null)
            {
                var isFirstTime = context.User.CreatedAt.Date == DateTime.UtcNow.Date;
                if (conditions.FirstTimeUser.Value != isFirstTime)
                    return false;
            }

            // Time left check
            if (conditions.MinTimeLeft.HasValue && context.Session != null)
            {
                var sessionLength = DateTime.UtcNow - context.Session.CreatedAt;
                var timeLeft = TimeSpan.FromMinutes(60) - sessionLength; // Assuming 60 min sessions
                if (timeLeft < conditions.MinTimeLeft.Value)
                    return false;
            }

            // Date range check
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (conditions.StartDate.HasValue && today < conditions.StartDate.Value)
                return false;
            if (conditions.EndDate.HasValue && today > conditions.EndDate.Value)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error evaluating screen conditions");
            return true; // Default to showing screen on error
        }
    }

    private async Task<string> LoadScreenContentAsync(string screenName, bool preferAnsi = false)
    {
        // Create cache key that includes preference
        var cacheKey = $"{screenName}_{(preferAnsi ? "ansi" : "ascii")}";
        
        // Check cache first
        if (_screenCache.TryGetValue(cacheKey, out var cachedContent)) return cachedContent;

        var filePath = GetScreenFilePath(screenName, preferAnsi);
        if (!File.Exists(filePath)) return string.Empty;

        // Read files using Latin-1 encoding to preserve byte values (works for both ASCII and ANSI)
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var content = Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        _screenCache.TryAdd(cacheKey, content);
        return content;
    }

    private string GetScreenFilePath(string screenName, bool preferAnsi = false)
    {
        // Determine file extensions to try based on preference
        var primaryExtension = preferAnsi ? ".ans" : ".asc";
        var fallbackExtension = preferAnsi ? ".asc" : ".ans";
        
        // Handle different naming conventions
        var baseName = screenName.EndsWith(".ans", StringComparison.OrdinalIgnoreCase) ||
                      screenName.EndsWith(".asc", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(screenName)
            : screenName;

        // Try subdirectories first
        var subDirs = new[] { "login", "menus", "system", "doors", "" };
        foreach (var subDir in subDirs)
        {
            var directoryPath = string.IsNullOrEmpty(subDir)
                ? _screensDirectory
                : Path.Combine(_screensDirectory, subDir);

            if (!Directory.Exists(directoryPath))
                continue;

            // First try preferred format
            var primaryFileName = $"{baseName}{primaryExtension}";
            var primaryPath = Path.Combine(directoryPath, primaryFileName);
            if (File.Exists(primaryPath))
                return primaryPath;

            // Then try fallback format
            var fallbackFileName = $"{baseName}{fallbackExtension}";
            var fallbackPath = Path.Combine(directoryPath, fallbackFileName);
            if (File.Exists(fallbackPath))
                return fallbackPath;

            // Try case-insensitive search for both formats
            try
            {
                var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly);
                
                // First search for preferred format
                var matchingFile = files.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), primaryFileName, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                    return matchingFile;

                // Then search for fallback format
                matchingFile = files.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), fallbackFileName, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                    return matchingFile;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error searching for screen file in directory {Directory}", directoryPath);
            }
        }

        // Default to preferred format in root screens directory (for backwards compatibility)
        return Path.Combine(_screensDirectory, $"{baseName}{primaryExtension}");
    }

    private void OnScreenFileChanged(object sender, FileSystemEventArgs e)
    {
        // Remove from cache to force reload - clear both ASCII and ANSI cache entries
        var fileName = e.Name ?? "";
        var screenName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        // Only process .asc and .ans files
        if (extension != ".asc" && extension != ".ans")
            return;
            
        // Clear both cache entries for this screen
        _screenCache.TryRemove($"{screenName}_ascii", out _);
        _screenCache.TryRemove($"{screenName}_ansi", out _);
        
        _logger.Debug("Screen file {FileName} changed, removed from cache", fileName);
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}