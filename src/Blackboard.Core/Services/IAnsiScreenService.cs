using Blackboard.Core.DTOs;
using Blackboard.Core.Models;

namespace Blackboard.Core.Services;

public interface IAnsiScreenService
{
    /// <summary>
    ///     Renders an ANSI screen with template variable replacement
    /// </summary>
    Task<string> RenderScreenAsync(string screenName, UserContext context);

    /// <summary>
    ///     Checks if a screen file exists
    /// </summary>
    Task<bool> ScreenExistsAsync(string screenName);

    /// <summary>
    ///     Gets the fallback screen content if main screen doesn't exist
    /// </summary>
    Task<string> GetFallbackScreenAsync(string screenName);

    /// <summary>
    ///     Clears the screen cache (useful for hot-reload)
    /// </summary>
    void ClearCache();

    /// <summary>
    ///     Evaluates conditions for screen display
    /// </summary>
    bool EvaluateConditions(ScreenConditions conditions, UserContext context);
}

/// <summary>
///     Context information for rendering screens
/// </summary>
public class UserContext
{
    public UserProfileDto? User { get; set; }
    public UserSession? Session { get; set; }
    public string? CallerIp { get; set; }
    public DateTime ConnectTime { get; set; }
    public Dictionary<string, object> SystemInfo { get; set; } = new();
    public Dictionary<string, object> CustomVariables { get; set; } = new();
}

/// <summary>
///     Screen display conditions
/// </summary>
public class ScreenConditions
{
    public int? MinSecurityLevel { get; set; }
    public int? MaxSecurityLevel { get; set; }
    public List<string>? RequiredGroups { get; set; }
    public bool? FirstTimeUser { get; set; }
    public int? MaxCallsToday { get; set; }
    public TimeSpan? MinTimeLeft { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}