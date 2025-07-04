using Blackboard.Core.Network;
using Blackboard.Core.Configuration;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

            // Load screen configuration and evaluate conditions
            var screenConditions = await LoadScreenConditionsAsync(screenName);
            
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

    /// <summary>
    /// Loads screen conditions from configuration
    /// </summary>
    private async Task<ScreenConditions> LoadScreenConditionsAsync(string screenName)
    {
        try
        {
            // For now, return basic default conditions
            // In the future, this could load from YAML config files per screen
            // or from a central screen configuration database
            
            // Check for screen-specific config file
            // Look for a .conditions.yml file next to the screen file
            var screenDir = Path.GetDirectoryName(screenName) ?? "";
            var screenFileName = Path.GetFileNameWithoutExtension(screenName);
            var conditionsFileName = $"{screenFileName}.conditions.yml";
            
            var configPath = Path.Combine(Path.GetDirectoryName(_ansiScreenService.GetType().Assembly.Location) ?? "", 
                "screens", screenDir, conditionsFileName);
            
            if (File.Exists(configPath))
            {
                // Load YAML configuration for this screen
                var yaml = await File.ReadAllTextAsync(configPath);
                
                try
                {
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .Build();
                    
                    var screenConditions = deserializer.Deserialize<ScreenConditions>(yaml);
                    _logger.Debug("Loaded screen conditions for {ScreenName} from {ConfigPath}", screenName, configPath);
                    return screenConditions ?? new ScreenConditions();
                }
                catch (Exception parseEx)
                {
                    _logger.Warning(parseEx, "Failed to parse YAML configuration for {ScreenName} at {ConfigPath}", screenName, configPath);
                    return new ScreenConditions(); // Default to no conditions on parse error
                }
            }
            
            // Return default empty conditions for now
            return new ScreenConditions();
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error loading screen conditions for {ScreenName}", screenName);
            return new ScreenConditions(); // Default to no conditions
        }
    }
}
