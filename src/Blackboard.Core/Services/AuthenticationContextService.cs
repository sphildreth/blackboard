namespace Blackboard.Core.Services;

/// <summary>
///     Service for managing current authentication context
///     Provides access to the currently authenticated user's information
/// </summary>
public interface IAuthenticationContextService
{
    /// <summary>
    ///     Gets the current authenticated user ID
    /// </summary>
    /// <returns>The user ID if authenticated, null otherwise</returns>
    int? GetCurrentUserId();

    /// <summary>
    ///     Gets the current authenticated user's session ID
    /// </summary>
    /// <returns>The session ID if authenticated, null otherwise</returns>
    string? GetCurrentSessionId();

    /// <summary>
    ///     Checks if the current user has admin privileges
    /// </summary>
    /// <returns>True if the current user is an admin, false otherwise</returns>
    bool IsCurrentUserAdmin();

    /// <summary>
    ///     Sets the current authentication context (for login)
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="sessionId">The session ID</param>
    /// <param name="isAdmin">Whether the user has admin privileges</param>
    void SetCurrentContext(int userId, string sessionId, bool isAdmin);

    /// <summary>
    ///     Clears the current authentication context (for logout)
    /// </summary>
    void ClearCurrentContext();
}

/// <summary>
///     Basic implementation of authentication context service
///     This is a simple in-memory implementation that should be replaced
///     with a proper session-based implementation in production
/// </summary>
public class AuthenticationContextService : IAuthenticationContextService
{
    private string? _currentSessionId;
    private int? _currentUserId;
    private bool _isAdmin;

    public int? GetCurrentUserId()
    {
        return _currentUserId;
    }

    public string? GetCurrentSessionId()
    {
        return _currentSessionId;
    }

    public bool IsCurrentUserAdmin()
    {
        return _isAdmin;
    }

    public void SetCurrentContext(int userId, string sessionId, bool isAdmin)
    {
        _currentUserId = userId;
        _currentSessionId = sessionId;
        _isAdmin = isAdmin;
    }

    public void ClearCurrentContext()
    {
        _currentUserId = null;
        _currentSessionId = null;
        _isAdmin = false;
    }
}