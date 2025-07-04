using Blackboard.Core.Network;
using Blackboard.Core.Configuration;
using Serilog;

namespace Blackboard.Core.Services;

public class ScreenSequenceService : IScreenSequenceService
{
    private readonly IAnsiScreenService _ansiScreenService;
    private readonly ILogger _logger;
    private readonly Dictionary<string, string[]> _sequences;

    public ScreenSequenceService(IAnsiScreenService ansiScreenService, ILogger logger)
    {
        _ansiScreenService = ansiScreenService;
        _logger = logger;
        _sequences = new Dictionary<string, string[]>();
        
        // Initialize default sequences
        InitializeDefaultSequences();
    }

    public async Task ShowSequenceAsync(string sequenceName, TelnetConnection connection, UserContext context)
    {
        try
        {
            if (!_sequences.TryGetValue(sequenceName.ToUpperInvariant(), out var stages))
            {
                _logger.Warning("Screen sequence {SequenceName} not found", sequenceName);
                return;
            }

            _logger.Debug("Showing screen sequence {SequenceName} with {StageCount} stages", sequenceName, stages.Length);

            foreach (var stage in stages)
            {
                await ShowScreenIfConditionsMetAsync(stage, connection, context);
                
                // Small delay between screens for better user experience
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error showing screen sequence {SequenceName}", sequenceName);
        }
    }

    public Task<string[]> GetSequenceStagesAsync(string sequenceName)
    {
        _sequences.TryGetValue(sequenceName.ToUpperInvariant(), out var stages);
        return Task.FromResult(stages ?? Array.Empty<string>());
    }

    public async Task<bool> ShowScreenIfConditionsMetAsync(string screenName, TelnetConnection connection, UserContext context)
    {
        try
        {
            // Check if screen exists
            if (!await _ansiScreenService.ScreenExistsAsync(screenName))
            {
                _logger.Debug("Screen {ScreenName} does not exist, skipping", screenName);
                return false;
            }

            // For now, we'll implement basic conditions later
            // TODO: Load screen configuration and evaluate conditions
            var screenConditions = new ScreenConditions(); // Default empty conditions
            
            if (!_ansiScreenService.EvaluateConditions(screenConditions, context))
            {
                _logger.Debug("Screen {ScreenName} conditions not met, skipping", screenName);
                return false;
            }

            // Render and send the screen
            var screenContent = await _ansiScreenService.RenderScreenAsync(screenName, context);
            await connection.SendAnsiAsync(screenContent);
            
            _logger.Debug("Successfully showed screen {ScreenName}", screenName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error showing screen {ScreenName}", screenName);
            return false;
        }
    }

    private void InitializeDefaultSequences()
    {
        // Login sequence
        _sequences["LOGIN"] = new[] { "CONNECT", "LOGON1", "LOGON2", "LOGON3" };
        
        // New user sequence
        _sequences["NEWUSER"] = new[] { "NEWUSER1", "NEWUSER2", "NEWUSER3" };
        
        // Logoff sequence
        _sequences["LOGOFF"] = new[] { "LOGOFF" };
        
        // Pre-login sequence
        _sequences["PRELOGIN"] = new[] { "CONNECT", "LOGON1" };
        
        // Post-login sequence
        _sequences["POSTLOGIN"] = new[] { "LOGON2", "LOGON3" };

        _logger.Information("Initialized {SequenceCount} default screen sequences", _sequences.Count);
    }

    /// <summary>
    /// Adds or updates a custom screen sequence
    /// </summary>
    public void RegisterSequence(string sequenceName, string[] stages)
    {
        _sequences[sequenceName.ToUpperInvariant()] = stages;
        _logger.Information("Registered screen sequence {SequenceName} with {StageCount} stages", sequenceName, stages.Length);
    }

    /// <summary>
    /// Removes a screen sequence
    /// </summary>
    public bool UnregisterSequence(string sequenceName)
    {
        var removed = _sequences.Remove(sequenceName.ToUpperInvariant());
        if (removed)
        {
            _logger.Information("Unregistered screen sequence {SequenceName}", sequenceName);
        }
        return removed;
    }
}
