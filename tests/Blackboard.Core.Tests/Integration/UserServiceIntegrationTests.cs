using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Blackboard.Core.Tests.Helpers;
using Serilog;

namespace Blackboard.Core.Tests.Integration;

public class UserServiceIntegrationTests : IAsyncLifetime
{
    private readonly TestDatabaseHelper _databaseHelper;
    private readonly IPasswordService _passwordService;
    private readonly ISessionService _sessionService;
    private readonly IAuditService _auditService;
    private readonly IUserService _userService;
    private readonly ILogger _logger;

    public UserServiceIntegrationTests()
    {
        _databaseHelper = new TestDatabaseHelper();
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        var securitySettings = TestDataHelper.CreateSecuritySettings();

        _passwordService = new PasswordService();
        _sessionService = new SessionService(_databaseHelper.DatabaseManager, _logger);
        _auditService = new AuditService(_databaseHelper.DatabaseManager, _logger);
        _userService = new UserService(
            _databaseHelper.DatabaseManager,
            _passwordService,
            _sessionService,
            _auditService,
            securitySettings,
            _logger);
    }

    public async Task InitializeAsync()
    {
        await _databaseHelper.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        _databaseHelper.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task RegisterUserAsync_WithValidData_ShouldCreateUser()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();

        // Act
        var user = await _userService.RegisterUserAsync(registration, "192.168.1.100", "Test Browser");

        // Assert
        user.Should().NotBeNull();
        user!.Handle.Should().Be(registration.Handle);
        user.Email.Should().Be(registration.Email);
        user.FirstName.Should().Be(registration.FirstName);
        user.LastName.Should().Be(registration.LastName);
        user.Location.Should().Be(registration.Location);
        user.IsActive.Should().BeTrue();
        user.SecurityLevel.Should().Be(SecurityLevel.User);
        user.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RegisterUserAsync_WithDuplicateHandle_ShouldReturnNull()
    {
        // Arrange
        var registration1 = TestDataHelper.CreateValidUserRegistration("testuser");
        var registration2 = TestDataHelper.CreateValidUserRegistration("testuser");

        // Act
        var user1 = await _userService.RegisterUserAsync(registration1);
        var user2 = await _userService.RegisterUserAsync(registration2);

        // Assert
        user1.Should().NotBeNull();
        user2.Should().BeNull();
    }

    [Fact]
    public async Task RegisterUserAsync_WithWeakPassword_ShouldReturnNull()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        registration.Password = "weak"; // Too weak

        // Act
        var user = await _userService.RegisterUserAsync(registration);

        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnUserAndSession()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        await _userService.RegisterUserAsync(registration);

        var login = TestDataHelper.CreateValidUserLogin(registration.Handle, registration.Password);

        // Act
        var (user, session) = await _userService.LoginAsync(login);

        // Assert
        user.Should().NotBeNull();
        session.Should().NotBeNull();
        user!.Handle.Should().Be(registration.Handle);
        session!.UserId.Should().Be(user.Id);
        session.IpAddress.Should().Be(login.IpAddress);
        session.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ShouldReturnNull()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        await _userService.RegisterUserAsync(registration);

        var login = TestDataHelper.CreateValidUserLogin(registration.Handle, "wrongpassword");

        // Act
        var (user, session) = await _userService.LoginAsync(login);

        // Assert
        user.Should().BeNull();
        session.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ShouldReturnNull()
    {
        // Arrange
        var login = TestDataHelper.CreateValidUserLogin("nonexistent", "password");

        // Act
        var (user, session) = await _userService.LoginAsync(login);

        // Assert
        user.Should().BeNull();
        session.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_MultipleFailedAttempts_ShouldLockAccount()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        await _userService.RegisterUserAsync(registration);

        var login = TestDataHelper.CreateValidUserLogin(registration.Handle, "wrongpassword");

        // Act - Attempt login 3 times with wrong password (should trigger lockout)
        await _userService.LoginAsync(login);
        await _userService.LoginAsync(login);
        await _userService.LoginAsync(login);

        // Try correct password after lockout
        login.Password = registration.Password;
        var (user, session) = await _userService.LoginAsync(login);

        // Assert
        user.Should().BeNull(); // Should be locked
        session.Should().BeNull();
    }

    [Fact]
    public async Task UpdateUserProfileAsync_WithValidData_ShouldUpdateUser()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        var originalUser = await _userService.RegisterUserAsync(registration);
        originalUser.Should().NotBeNull();

        var update = TestDataHelper.CreateUserUpdate();

        // Act
        var result = await _userService.UpdateUserProfileAsync(originalUser!.Id, update, "192.168.1.100");

        // Assert
        result.Should().BeTrue();

        var updatedUser = await _userService.GetUserByIdAsync(originalUser.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.Email.Should().Be(update.Email);
        updatedUser.FirstName.Should().Be(update.FirstName);
        updatedUser.LastName.Should().Be(update.LastName);
        updatedUser.Location.Should().Be(update.Location);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_ShouldUpdatePassword()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        var user = await _userService.RegisterUserAsync(registration);
        user.Should().NotBeNull();

        var passwordChange = TestDataHelper.CreatePasswordChange(registration.Password, "NewSecurePass456!");

        // Act
        var result = await _userService.ChangePasswordAsync(user!.Id, passwordChange, "192.168.1.100");

        // Assert
        result.Should().BeTrue();

        // Verify old password no longer works
        var oldPasswordLogin = TestDataHelper.CreateValidUserLogin(registration.Handle, registration.Password);
        var (loginUser1, _) = await _userService.LoginAsync(oldPasswordLogin);
        loginUser1.Should().BeNull();

        // Verify new password works
        var newPasswordLogin = TestDataHelper.CreateValidUserLogin(registration.Handle, passwordChange.NewPassword);
        var (loginUser2, session2) = await _userService.LoginAsync(newPasswordLogin);
        loginUser2.Should().NotBeNull();
        session2.Should().NotBeNull();
    }

    [Fact]
    public async Task LockUserAsync_ShouldPreventLogin()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        var user = await _userService.RegisterUserAsync(registration);
        user.Should().NotBeNull();

        // Act
        var lockResult = await _userService.LockUserAsync(user!.Id, TimeSpan.FromHours(1), "Test lock", adminUserId: user.Id);

        // Assert
        lockResult.Should().BeTrue();

        var login = TestDataHelper.CreateValidUserLogin(registration.Handle, registration.Password);
        var (loginUser, session) = await _userService.LoginAsync(login);
        loginUser.Should().BeNull();
        session.Should().BeNull();
    }

    [Fact]
    public async Task UnlockUserAsync_ShouldAllowLogin()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        var user = await _userService.RegisterUserAsync(registration);
        user.Should().NotBeNull();

        await _userService.LockUserAsync(user!.Id, TimeSpan.FromHours(1), "Test lock", adminUserId: user.Id);

        // Act
        var unlockResult = await _userService.UnlockUserAsync(user.Id, adminUserId: user.Id);

        // Assert
        unlockResult.Should().BeTrue();

        var login = TestDataHelper.CreateValidUserLogin(registration.Handle, registration.Password);
        var (loginUser, session) = await _userService.LoginAsync(login);
        loginUser.Should().NotBeNull();
        session.Should().NotBeNull();
    }

    [Fact]
    public async Task SetUserSecurityLevelAsync_ShouldUpdateSecurityLevel()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        var user = await _userService.RegisterUserAsync(registration);
        user.Should().NotBeNull();

        // Act
        var result = await _userService.SetUserSecurityLevelAsync(user!.Id, SecurityLevel.Moderator, adminUserId: user.Id);

        // Assert
        result.Should().BeTrue();

        var updatedUser = await _userService.GetUserByIdAsync(user.Id);
        updatedUser.Should().NotBeNull();
        updatedUser!.SecurityLevel.Should().Be(SecurityLevel.Moderator);
    }

    [Fact]
    public async Task GetUsersAsync_ShouldReturnRegisteredUsers()
    {
        // Arrange
        var registration1 = TestDataHelper.CreateValidUserRegistration("user1");
        var registration2 = TestDataHelper.CreateValidUserRegistration("user2");
        
        await _userService.RegisterUserAsync(registration1);
        await _userService.RegisterUserAsync(registration2);

        // Act
        var users = await _userService.GetUsersAsync();

        // Assert
        users.Should().HaveCount(2);
        users.Should().Contain(u => u.Handle == "user1");
        users.Should().Contain(u => u.Handle == "user2");
    }

    [Fact]
    public async Task SearchUsersAsync_ShouldReturnMatchingUsers()
    {
        // Arrange
        var registration1 = TestDataHelper.CreateValidUserRegistration("testuser1");
        var registration2 = TestDataHelper.CreateValidUserRegistration("testuser2");
        var registration3 = TestDataHelper.CreateValidUserRegistration("differentuser");
        
        await _userService.RegisterUserAsync(registration1);
        await _userService.RegisterUserAsync(registration2);
        await _userService.RegisterUserAsync(registration3);

        // Act
        var searchResults = await _userService.SearchUsersAsync("testuser");

        // Assert
        searchResults.Should().HaveCount(2);
        searchResults.Should().Contain(u => u.Handle == "testuser1");
        searchResults.Should().Contain(u => u.Handle == "testuser2");
        searchResults.Should().NotContain(u => u.Handle == "differentuser");
    }

    [Fact]
    public async Task LogoutAsync_ShouldEndSession()
    {
        // Arrange
        var registration = TestDataHelper.CreateValidUserRegistration();
        await _userService.RegisterUserAsync(registration);

        var login = TestDataHelper.CreateValidUserLogin(registration.Handle, registration.Password);
        var (user, session) = await _userService.LoginAsync(login);
        user.Should().NotBeNull();
        session.Should().NotBeNull();

        // Act
        var logoutResult = await _userService.LogoutAsync(session!.Id, "192.168.1.100");

        // Assert
        logoutResult.Should().BeTrue();

        var isSessionValid = await _sessionService.ValidateSessionAsync(session.Id);
        isSessionValid.Should().BeFalse();
    }
}
