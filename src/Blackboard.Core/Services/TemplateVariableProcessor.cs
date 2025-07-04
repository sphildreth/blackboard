using System.Text.RegularExpressions;
using Blackboard.Core.Models;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public class TemplateVariableProcessor : ITemplateVariableProcessor
{
    private readonly ILogger _logger;
    private readonly IDatabaseManager _databaseManager;
    private readonly Dictionary<string, Func<UserContext, Dictionary<string, object>>> _variableProviders;
    private readonly Regex _templateVariableRegex;

    public TemplateVariableProcessor(ILogger logger, IDatabaseManager databaseManager)
    {
        _logger = logger;
        _databaseManager = databaseManager;
        _variableProviders = new Dictionary<string, Func<UserContext, Dictionary<string, object>>>();
        _templateVariableRegex = new Regex(@"\{(\w+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        // Register default variable providers
        RegisterDefaultProviders();
    }

    public async Task<string> ProcessVariablesAsync(string content, UserContext context)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        try
        {
            return await Task.Run(() =>
            {
                return _templateVariableRegex.Replace(content, match =>
                {
                    var variableName = match.Groups[1].Value.ToUpperInvariant();
                    var value = GetVariableValue(variableName, context);
                    return value?.ToString() ?? match.Value; // Keep original if not found
                });
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing template variables");
            return content; // Return original content on error
        }
    }

    public void RegisterVariableProvider(string prefix, Func<UserContext, Dictionary<string, object>> provider)
    {
        _variableProviders[prefix.ToUpperInvariant()] = provider;
    }

    private void RegisterDefaultProviders()
    {
        // System variables
        RegisterVariableProvider("SYSTEM", context => new Dictionary<string, object>
        {
            ["BBS_NAME"] = context.SystemInfo.GetValueOrDefault("BBS_NAME", "Blackboard BBS"),
            ["BBS_VERSION"] = context.SystemInfo.GetValueOrDefault("BBS_VERSION", "1.0"),
            ["NODE_NUMBER"] = context.SystemInfo.GetValueOrDefault("NODE_NUMBER", 1),
            ["TOTAL_USERS"] = context.SystemInfo.GetValueOrDefault("TOTAL_USERS", 0),
            ["USERS_ONLINE"] = context.SystemInfo.GetValueOrDefault("USERS_ONLINE", 0),
            ["CURRENT_TIME"] = DateTime.Now.ToString("HH:mm:ss"),
            ["CURRENT_DATE"] = DateTime.Now.ToString("yyyy-MM-dd"),
            ["UPTIME"] = context.SystemInfo.GetValueOrDefault("UPTIME", "Unknown")
        });

        // User variables
        RegisterVariableProvider("USER", context => new Dictionary<string, object>
        {
            ["USER_NAME"] = context.User?.Handle ?? "Guest",
            ["USER_REAL_NAME"] = $"{context.User?.FirstName} {context.User?.LastName}".Trim(),
            ["USER_LOCATION"] = context.User?.Location ?? "Unknown",
            ["USER_LEVEL"] = ((int)(context.User?.SecurityLevel ?? SecurityLevel.User)).ToString(),
            ["USER_CALLS"] = GetUserCallCount(context.User?.Id ?? 0).ToString(),
            ["USER_LASTCALL"] = context.User?.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never",
            ["USER_TIMELEFT"] = CalculateTimeLeft(context).ToString(@"mm") + " mins"
        });

        // Connection variables
        RegisterVariableProvider("CONNECTION", context => new Dictionary<string, object>
        {
            ["CALLER_IP"] = context.CallerIp ?? "Unknown",
            ["CONNECT_TIME"] = context.ConnectTime.ToString("HH:mm:ss"),
            ["SESSION_LENGTH"] = (DateTime.UtcNow - context.ConnectTime).ToString(@"hh\:mm\:ss"),
            ["BAUD_RATE"] = "33600" // Emulated for nostalgia
        });
    }

    private object? GetVariableValue(string variableName, UserContext context)
    {
        // Try each provider to find the variable
        foreach (var provider in _variableProviders.Values)
        {
            try
            {
                var variables = provider(context);
                if (variables.TryGetValue(variableName, out var value))
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error getting variable {VariableName} from provider", variableName);
            }
        }

        // Check custom variables
        if (context.CustomVariables.TryGetValue(variableName, out var customValue))
        {
            return customValue;
        }

        return null;
    }

    private TimeSpan CalculateTimeLeft(UserContext context)
    {
        // Default session time limit (could be configurable)
        var sessionTimeLimit = TimeSpan.FromMinutes(60);
        var elapsed = DateTime.UtcNow - context.ConnectTime;
        var remaining = sessionTimeLimit - elapsed;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private int GetUserCallCount(long userId)
    {
        if (userId == 0) return 0;
        
        try
        {
            // Count total sessions for the user (represents total calls)
            const string sql = "SELECT COUNT(*) FROM UserSessions WHERE UserId = @UserId";
            return _databaseManager.QueryFirstAsync<int>(sql, new { UserId = userId }).Result;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to get call count for user {UserId}", userId);
            return 0;
        }
    }
}
