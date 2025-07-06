namespace Blackboard.Core.Services;

public interface ITemplateVariableProcessor
{
    /// <summary>
    ///     Processes template variables in the given content string
    /// </summary>
    Task<string> ProcessVariablesAsync(string content, UserContext context);

    /// <summary>
    ///     Registers a custom variable provider
    /// </summary>
    void RegisterVariableProvider(string prefix, Func<UserContext, Dictionary<string, object>> provider);
}