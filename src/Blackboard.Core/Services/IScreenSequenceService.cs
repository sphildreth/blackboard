using Blackboard.Core.Network;

namespace Blackboard.Core.Services;

public interface IScreenSequenceService
{
    /// <summary>
    /// Shows a sequence of screens (e.g., LOGON1, LOGON2, LOGON3)
    /// </summary>
    Task ShowSequenceAsync(string sequenceName, ITelnetConnection connection, UserContext context);
    
    /// <summary>
    /// Gets the stages for a specific sequence
    /// </summary>
    Task<string[]> GetSequenceStagesAsync(string sequenceName);
    
    /// <summary>
    /// Shows a single screen from a sequence if conditions are met
    /// </summary>
    Task<bool> ShowScreenIfConditionsMetAsync(string screenName, ITelnetConnection connection, UserContext context);
}
