using Blackboard.Core.Services;
using Blackboard.Core.Tests.Helpers;
using Serilog;

namespace Blackboard.Core.Tests.Services;

public class SessionServiceTests : IAsyncLifetime
{
    private readonly TestDatabaseHelper _databaseHelper;
    private readonly ISessionService _sessionService;
    private readonly ILogger _logger;

    public SessionServiceTests()
    {
        _databaseHelper = new TestDatabaseHelper();
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        _sessionService = new SessionService(_databaseHelper.DatabaseManager, _logger);
    }

    public async Task InitializeAsync()
    {
        await _databaseHelper.InitializeAsync();
        // Create test users for session tests (some tests need multiple users)
        await TestDataHelper.CreateTestUserAsync(_databaseHelper.DatabaseManager, "testuser1", 1);
        await TestDataHelper.CreateTestUserAsync(_databaseHelper.DatabaseManager, "testuser2", 2);
        await TestDataHelper.CreateTestUserAsync(_databaseHelper.DatabaseManager, "testuser3", 3);
    }

    public Task DisposeAsync()
    {
        _databaseHelper.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldCreateValidSession()
    {
        // Arrange
        var userId = 1;
        var ipAddress = "192.168.1.100";
        var userAgent = "Test Browser";

        // Act
        var session = await _sessionService.CreateSessionAsync(userId, ipAddress, userAgent);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().NotBeNullOrEmpty();
        session.UserId.Should().Be(userId);
        session.IpAddress.Should().Be(ipAddress);
        session.UserAgent.Should().Be(userAgent);
        session.IsActive.Should().BeTrue();
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        session.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddHours(23));
    }

    [Fact]
    public async Task CreateSessionAsync_ShouldGenerateUniqueSessionIds()
    {
        // Arrange
        var userId = 1;
        var ipAddress = "192.168.1.100";

        // Act
        var session1 = await _sessionService.CreateSessionAsync(userId, ipAddress);
        var session2 = await _sessionService.CreateSessionAsync(userId, ipAddress);

        // Assert
        session1.Id.Should().NotBe(session2.Id);
    }

    [Fact]
    public async Task GetSessionAsync_WithValidSessionId_ShouldReturnSession()
    {
        // Arrange
        var userId = 1;
        var ipAddress = "192.168.1.100";
        var createdSession = await _sessionService.CreateSessionAsync(userId, ipAddress);

        // Act
        var retrievedSession = await _sessionService.GetSessionAsync(createdSession.Id);

        // Assert
        retrievedSession.Should().NotBeNull();
        retrievedSession!.Id.Should().Be(createdSession.Id);
        retrievedSession.UserId.Should().Be(userId);
        retrievedSession.IpAddress.Should().Be(ipAddress);
    }

    [Fact]
    public async Task GetSessionAsync_WithInvalidSessionId_ShouldReturnNull()
    {
        // Act
        var session = await _sessionService.GetSessionAsync("invalid-session-id");

        // Assert
        session.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_WithValidActiveSession_ShouldReturnTrue()
    {
        // Arrange
        var userId = 1;
        var ipAddress = "192.168.1.100";
        var session = await _sessionService.CreateSessionAsync(userId, ipAddress);

        // Act
        var isValid = await _sessionService.ValidateSessionAsync(session.Id);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSessionAsync_WithInvalidSessionId_ShouldReturnFalse()
    {
        // Act
        var isValid = await _sessionService.ValidateSessionAsync("invalid-session-id");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSessionAsync_WithInactiveSession_ShouldReturnFalse()
    {
        // Arrange
        var userId = 1;
        var ipAddress = "192.168.1.100";
        var session = await _sessionService.CreateSessionAsync(userId, ipAddress);
        await _sessionService.EndSessionAsync(session.Id);

        // Act
        var isValid = await _sessionService.ValidateSessionAsync(session.Id);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExtendSessionAsync_WithValidSession_ShouldUpdateExpirationTime()
    {
        // Arrange
        var userId = 1;
        var ipAddress = "192.168.1.100";
        var session = await _sessionService.CreateSessionAsync(userId, ipAddress);
        var beforeExtension = DateTime.UtcNow;

        // Act
        var result = await _sessionService.ExtendSessionAsync(session.Id, TimeSpan.FromHours(2));

        // Assert
        result.Should().BeTrue();

        // Verify the session was extended to approximately 2 hours from now
        var updatedSession = await _sessionService.GetSessionAsync(session.Id);
        var expectedExpiration = beforeExtension.AddHours(2);
        updatedSession!.ExpiresAt.Should().BeCloseTo(expectedExpiration, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ExtendSessionAsync_WithInvalidSession_ShouldReturnFalse()
    {
        // Act
        var result = await _sessionService.ExtendSessionAsync("invalid-session-id", TimeSpan.FromHours(1));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EndSessionAsync_WithValidSession_ShouldDeactivateSession()
    {
        // Arrange
        var userId = 1;
        var ipAddress = "192.168.1.100";
        var session = await _sessionService.CreateSessionAsync(userId, ipAddress);

        // Act
        var result = await _sessionService.EndSessionAsync(session.Id);

        // Assert
        result.Should().BeTrue();

        // Verify session is no longer valid
        var isValid = await _sessionService.ValidateSessionAsync(session.Id);
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task EndSessionAsync_WithInvalidSession_ShouldReturnFalse()
    {
        // Act
        var result = await _sessionService.EndSessionAsync("invalid-session-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task EndAllUserSessionsAsync_ShouldDeactivateAllUserSessions()
    {
        // Arrange
        var userId = 1;
        var session1 = await _sessionService.CreateSessionAsync(userId, "192.168.1.100");
        var session2 = await _sessionService.CreateSessionAsync(userId, "192.168.1.101");
        var otherUserSession = await _sessionService.CreateSessionAsync(2, "192.168.1.102");

        // Act
        var result = await _sessionService.EndAllUserSessionsAsync(userId);

        // Assert
        result.Should().BeTrue();

        // Verify user's sessions are invalid
        (await _sessionService.ValidateSessionAsync(session1.Id)).Should().BeFalse();
        (await _sessionService.ValidateSessionAsync(session2.Id)).Should().BeFalse();

        // Verify other user's session is still valid
        (await _sessionService.ValidateSessionAsync(otherUserSession.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task GetActiveSessionsAsync_ShouldReturnOnlyActiveSessionsForUser()
    {
        // Arrange
        var userId = 1;
        var activeSession1 = await _sessionService.CreateSessionAsync(userId, "192.168.1.100");
        var activeSession2 = await _sessionService.CreateSessionAsync(userId, "192.168.1.101");
        var inactiveSession = await _sessionService.CreateSessionAsync(userId, "192.168.1.102");
        var otherUserSession = await _sessionService.CreateSessionAsync(2, "192.168.1.103");

        // End one session
        await _sessionService.EndSessionAsync(inactiveSession.Id);

        // Act
        var activeSessions = await _sessionService.GetActiveSessionsAsync(userId);

        // Assert
        activeSessions.Should().HaveCount(2);
        activeSessions.Should().Contain(s => s.Id == activeSession1.Id);
        activeSessions.Should().Contain(s => s.Id == activeSession2.Id);
        activeSessions.Should().NotContain(s => s.Id == inactiveSession.Id);
        activeSessions.Should().NotContain(s => s.Id == otherUserSession.Id);
    }

    [Fact]
    public async Task GetAllActiveSessionsAsync_ShouldReturnAllActiveSessionsAcrossAllUsers()
    {
        // Arrange
        var session1 = await _sessionService.CreateSessionAsync(1, "192.168.1.100");
        var session2 = await _sessionService.CreateSessionAsync(2, "192.168.1.101");
        var inactiveSession = await _sessionService.CreateSessionAsync(3, "192.168.1.102");

        // End one session
        await _sessionService.EndSessionAsync(inactiveSession.Id);

        // Act
        var allActiveSessions = await _sessionService.GetAllActiveSessionsAsync();

        // Assert
        allActiveSessions.Should().HaveCount(2);
        allActiveSessions.Should().Contain(s => s.Id == session1.Id);
        allActiveSessions.Should().Contain(s => s.Id == session2.Id);
        allActiveSessions.Should().NotContain(s => s.Id == inactiveSession.Id);
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_ShouldRemoveExpiredSessions()
    {
        // Arrange
        var userId = 1;
        var activeSession = await _sessionService.CreateSessionAsync(userId, "192.168.1.100");
        
        // Create an expired session by directly manipulating the database
        // Note: In a real scenario, this would be handled by time passage
        var expiredSessionId = "expired-session-id";
        await _databaseHelper.DatabaseManager.ExecuteAsync(
            "INSERT INTO UserSessions (Id, UserId, IpAddress, CreatedAt, ExpiresAt, IsActive) VALUES (@Id, @UserId, @IpAddress, @CreatedAt, @ExpiresAt, @IsActive)",
            new { Id = expiredSessionId, UserId = userId, IpAddress = "192.168.1.101", CreatedAt = DateTime.UtcNow.AddDays(-2), ExpiresAt = DateTime.UtcNow.AddDays(-1), IsActive = true });

        // Act
        await _sessionService.CleanupExpiredSessionsAsync();

        // Assert
        var expiredSession = await _sessionService.GetSessionAsync(expiredSessionId);
        expiredSession.Should().BeNull();

        var activeSessionStillExists = await _sessionService.GetSessionAsync(activeSession.Id);
        activeSessionStillExists.Should().NotBeNull();
    }
}
