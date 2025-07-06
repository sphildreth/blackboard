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

        // Setup file watcher for hot-reload
        if (Directory.Exists(_screensDirectory))
        {
            _fileWatcher = new FileSystemWatcher(_screensDirectory, "*.ans")
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

    public async Task<string> RenderScreenAsync(string screenName, UserContext context)
    {
        try
        {
            var screenContent = await LoadScreenContentAsync(screenName);
            if (string.IsNullOrEmpty(screenContent))
            {
                _logger.Warning("Screen {ScreenName} not found, using fallback", screenName);
                screenContent = await GetFallbackScreenAsync(screenName);
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
        var filePath = GetScreenFilePath(screenName);
        return Task.FromResult(File.Exists(filePath));
    }

    public async Task<string> GetFallbackScreenAsync(string screenName)
    {
        // Try to load a default screen
        var defaultPath = Path.Combine(_screensDirectory, "defaults", "default.ans");
        if (File.Exists(defaultPath))
        {
            // Read as raw bytes and convert using Latin-1 to preserve byte values
            var fileBytes = await File.ReadAllBytesAsync(defaultPath);
            return Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        }

        // Return a simple text fallback
        return $"=== {screenName.ToUpper()} ===\r\n[ANSI screen not available]\r\n";
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

    private async Task<string> LoadScreenContentAsync(string screenName)
    {
        // Check cache first
        if (_screenCache.TryGetValue(screenName, out var cachedContent)) return cachedContent;

        var filePath = GetScreenFilePath(screenName);
        if (!File.Exists(filePath)) return string.Empty;

        // Read ANSI files using CP437 encoding to preserve box drawing characters
        // Read the ANSI file as raw bytes and convert using Latin-1 to preserve byte values
        var fileBytes = await File.ReadAllBytesAsync(filePath);
        var content = Encoding.GetEncoding("ISO-8859-1").GetString(fileBytes);
        _screenCache.TryAdd(screenName, content);
        return content;
    }

    private string GetScreenFilePath(string screenName)
    {
        // Handle different naming conventions
        var fileName = screenName.EndsWith(".ans", StringComparison.OrdinalIgnoreCase)
            ? screenName
            : $"{screenName}.ans";

        // Try subdirectories first
        var subDirs = new[] { "login", "menus", "system", "doors", "" };
        foreach (var subDir in subDirs)
        {
            var directoryPath = string.IsNullOrEmpty(subDir)
                ? _screensDirectory
                : Path.Combine(_screensDirectory, subDir);

            if (!Directory.Exists(directoryPath))
                continue;

            // First try exact match (for performance)
            var exactPath = Path.Combine(directoryPath, fileName);
            if (File.Exists(exactPath))
                return exactPath;

            // Then try case-insensitive search
            try
            {
                var files = Directory.GetFiles(directoryPath, "*.ans", SearchOption.TopDirectoryOnly);
                var matchingFile = files.FirstOrDefault(f =>
                    string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));

                if (matchingFile != null)
                    return matchingFile;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error searching for screen file in directory {Directory}", directoryPath);
            }
        }

        // Default to root screens directory (for backwards compatibility)
        return Path.Combine(_screensDirectory, fileName);
    }

    private void OnScreenFileChanged(object sender, FileSystemEventArgs e)
    {
        // Remove from cache to force reload
        var screenName = Path.GetFileNameWithoutExtension(e.Name ?? "");
        _screenCache.TryRemove(screenName, out _);
        _logger.Debug("Screen file {FileName} changed, removed from cache", e.Name);
    }

    public void Dispose()
    {
        _fileWatcher?.Dispose();
    }
}