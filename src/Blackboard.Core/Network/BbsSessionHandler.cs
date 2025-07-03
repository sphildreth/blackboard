using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Blackboard.Core.DTOs;
using Serilog;
using System.Diagnostics;

namespace Blackboard.Core.Network;

public class BbsSessionHandler
{
    private readonly IUserService _userService;
    private readonly ISessionService _sessionService;
    private readonly ILogger _logger;
    private readonly string _screensDir;

    public BbsSessionHandler(IUserService userService, ISessionService sessionService, ILogger logger, string screensDir)
    {
        _userService = userService;
        _sessionService = sessionService;
        _logger = logger;
        _screensDir = screensDir;
    }

    public async Task HandleSessionAsync(TelnetConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await ShowWelcomeScreen(connection);

            UserProfileDto? user = null;
            UserSession? session = null;

            // Main authentication loop
            while (user == null && !cancellationToken.IsCancellationRequested)
            {
                await connection.SendLineAsync("");
                await connection.SendLineAsync("Please choose an option:");
                await connection.SendLineAsync("(L)ogin, (R)egister, (G)uest, or (Q)uit");
                await connection.SendAsync("Choice: ");

                var choice = await connection.ReadLineAsync();
                if (string.IsNullOrEmpty(choice))
                    continue;

                switch (choice.ToUpper().Trim())
                {
                    case "L":
                    case "LOGIN":
                        (user, session) = await HandleLogin(connection);
                        break;
                    case "R":
                    case "REGISTER":
                        user = await HandleRegistration(connection);
                        if (user != null)
                        {
                            // Create session for newly registered user
                            session = await _sessionService.CreateSessionAsync(user.Id, connection.RemoteEndPoint?.ToString() ?? "unknown");
                        }
                        break;
                    case "G":
                    case "GUEST":
                        await HandleGuestAccess(connection);
                        return;
                    case "Q":
                    case "QUIT":
                        await connection.SendLineAsync("Goodbye!");
                        return;
                    default:
                        await connection.SendLineAsync("Invalid choice. Please try again.");
                        break;
                }
            }

            if (user != null && session != null)
            {
                await HandleAuthenticatedSession(connection, user, session, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in BBS session handling");
            await connection.SendLineAsync("An error occurred. Please try again later.");
        }
    }

    private async Task ShowWelcomeScreen(TelnetConnection connection)
    {
        // Try to load and display logon.ans from the screens directory
        var logonScreen = await MenuConfigLoader.LoadScreenAsync(_screensDir, "logon.ans");
        await connection.SendLineAsync(logonScreen);
    }

    private async Task<(UserProfileDto?, UserSession?)> HandleLogin(TelnetConnection connection)
    {
        try
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== LOGIN ===");
            await connection.SendAsync("Handle: ");
            var handle = await connection.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(handle))
            {
                await connection.SendLineAsync("Handle cannot be empty.");
                return (null, null);
            }

            await connection.SendAsync("Password: ");
            var password = await connection.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(password))
            {
                await connection.SendLineAsync("Password cannot be empty.");
                return (null, null);
            }

            var loginDto = new UserLoginDto
            {
                Handle = handle,
                Password = password,
                IpAddress = connection.RemoteEndPoint?.ToString() ?? "unknown",
                UserAgent = "Telnet Client"
            };

            var (user, session) = await _userService.LoginAsync(loginDto);

            if (user != null)
            {
                await connection.SendLineAsync($"Welcome back, {user.Handle}!");
                _logger.Information("User {Handle} logged in from {IP}", user.Handle, connection.RemoteEndPoint);
                return (user, session);
            }
            else
            {
                await connection.SendLineAsync("Invalid credentials. Please try again.");
                return (null, null);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during login");
            await connection.SendLineAsync("Login failed. Please try again.");
            return (null, null);
        }
    }

    private async Task<UserProfileDto?> HandleRegistration(TelnetConnection connection)
    {
        try
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== REGISTRATION ===");
            
            await connection.SendAsync("Desired Handle: ");
            var handle = await connection.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(handle))
            {
                await connection.SendLineAsync("Handle cannot be empty.");
                return null;
            }

            await connection.SendAsync("Email Address: ");
            var email = await connection.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(email))
            {
                await connection.SendLineAsync("Email cannot be empty.");
                return null;
            }

            await connection.SendAsync("Password: ");
            var password = await connection.ReadLineAsync();

            if (string.IsNullOrWhiteSpace(password))
            {
                await connection.SendLineAsync("Password cannot be empty.");
                return null;
            }

            await connection.SendAsync("Confirm Password: ");
            var confirmPassword = await connection.ReadLineAsync();

            if (password != confirmPassword)
            {
                await connection.SendLineAsync("Passwords do not match.");
                return null;
            }

            await connection.SendAsync("First Name (optional): ");
            var firstName = await connection.ReadLineAsync();

            await connection.SendAsync("Last Name (optional): ");
            var lastName = await connection.ReadLineAsync();

            await connection.SendAsync("Location (optional): ");
            var location = await connection.ReadLineAsync();

            var registrationDto = new UserRegistrationDto
            {
                Handle = handle,
                Email = email,
                Password = password,
                FirstName = firstName,
                LastName = lastName,
                Location = location
            };

            var user = await _userService.RegisterUserAsync(registrationDto, connection.RemoteEndPoint?.ToString(), "Telnet Client");

            if (user != null)
            {
                await connection.SendLineAsync($"Registration successful! Welcome to the board, {user.Handle}!");
                _logger.Information("New user {Handle} registered from {IP}", user.Handle, connection.RemoteEndPoint);
                return user;
            }
            else
            {
                await connection.SendLineAsync("Registration failed. Handle or email may already be taken.");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error during registration");
            await connection.SendLineAsync("Registration failed. Please try again.");
            return null;
        }
    }

    private async Task HandleGuestAccess(TelnetConnection connection)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== GUEST ACCESS ===");
        await connection.SendLineAsync("Guest access provides limited functionality:");
        await connection.SendLineAsync("- View public information");
        await connection.SendLineAsync("- Read announcements");
        await connection.SendLineAsync("- Register for full access");
        await connection.SendLineAsync("");
        await connection.SendLineAsync("Guest features not yet implemented.");
        await connection.SendLineAsync("Press any key to return to main menu...");
        await connection.ReadLineAsync();
    }

    private async Task HandleAuthenticatedSession(TelnetConnection connection, UserProfileDto user, UserSession session, CancellationToken cancellationToken)
    {
        // Send all the LOGON*.ANS screens in in order
        

        var menuConfigFile = "mainmenu.yml";
        await ShowMenuLoop(connection, _screensDir, menuConfigFile, user, session, cancellationToken);
    }

    private async Task ShowMenuLoop(
        TelnetConnection connection,
        string screensDir,
        string menuConfigFile,
        UserProfileDto user,
        UserSession session,
        CancellationToken cancellationToken)
    {
        string currentMenu = menuConfigFile;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Load menu config
            var menuConfigPath = Path.Combine(screensDir, currentMenu);
            if (!File.Exists(menuConfigPath))
            {
                await connection.SendLineAsync($"[Menu config not found: {currentMenu}]");
                return;
            }
            var menuConfig = await MenuConfigLoader.LoadAsync(menuConfigPath);

            // Load and send the screen file
            var screenContent = await MenuConfigLoader.LoadScreenAsync(screensDir, menuConfig.Screen);
            await connection.SendLineAsync(screenContent);

            // Show prompt and wait for input
            await connection.SendAsync(menuConfig.Prompt ?? "Choice: ");
            var input = (await connection.ReadLineAsync() ?? "").Trim().ToLower();

            if (menuConfig.Options != null && menuConfig.Options.TryGetValue(input, out var option))
            {
                if (!string.IsNullOrEmpty(option.Screen))
                {
                    // If the option points to another menu config, switch to it
                    if (option.Screen.EndsWith(".yml") || option.Screen.EndsWith(".yaml"))
                    {
                        currentMenu = option.Screen;
                        continue;
                    }
                    // Otherwise, just show the screen and loop
                    var nextScreen = await MenuConfigLoader.LoadScreenAsync(screensDir, option.Screen);
                    await connection.SendLineAsync(nextScreen);
                }

                // Handle special actions
                if (!string.IsNullOrEmpty(option.Action))
                {
                    switch (option.Action.ToLower())
                    {
                        case "quit":
                        case "logoff":
                        case "logout":
                            await _sessionService.EndSessionAsync(session.Id);
                            await connection.SendLineAsync("Thank you for calling Blackboard BBS!");
                            await connection.SendLineAsync("Session ended. Goodbye!");
                            return;
                        case "userprofile":
                            await ShowUserProfile(connection, user);
                            break;
                        case "whoisonline":
                            await ShowWhoIsOnline(connection);
                            break;
                        case "systeminfo":
                            await ShowSystemInfo(connection);
                            break;
                        case "messages":
                            await connection.SendLineAsync("Message system not yet implemented.");
                            break;
                        case "files":
                            await connection.SendLineAsync("File system not yet implemented.");
                            break;
                        // Add more actions as needed
                    }
                }
            }
            else
            {
                await connection.SendLineAsync("Invalid choice. Please try again.");
            }
        }
    }

    private async Task ShowUserProfile(TelnetConnection connection, UserProfileDto user)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== USER PROFILE ===");
        await connection.SendLineAsync($"Handle: {user.Handle}");
        await connection.SendLineAsync($"Email: {user.Email}");
        await connection.SendLineAsync($"Name: {user.FirstName} {user.LastName}");
        await connection.SendLineAsync($"Location: {user.Location}");
        await connection.SendLineAsync($"Security Level: {user.SecurityLevel}");
        await connection.SendLineAsync($"Member Since: {user.CreatedAt:yyyy-MM-dd}");
        await connection.SendLineAsync($"Last Login: {user.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never"}");
        await connection.SendLineAsync("");
        await connection.SendLineAsync("Press any key to continue...");
        await connection.ReadLineAsync();
    }

    private async Task ShowWhoIsOnline(TelnetConnection connection)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== WHO'S ONLINE ===");
        
        var activeSessions = await _sessionService.GetAllActiveSessionsAsync();
        if (!activeSessions.Any())
        {
            await connection.SendLineAsync("No one else is currently online.");
        }
        else
        {
            await connection.SendLineAsync($"{"Handle",-15} {"Location",-20} {"Login Time",-20}");
            await connection.SendLineAsync(new string('─', 55));
            
            foreach (var session in activeSessions)
            {
                var sessionUser = await _userService.GetUserByIdAsync(session.UserId);
                if (sessionUser != null)
                {
                    await connection.SendLineAsync($"{sessionUser.Handle,-15} {sessionUser.Location ?? "Unknown",-20} {session.CreatedAt:HH:mm:ss}");
                }
            }
        }
        
        await connection.SendLineAsync("");
        await connection.SendLineAsync("Press any key to continue...");
        await connection.ReadLineAsync();
    }

    private async Task ShowSystemInfo(TelnetConnection connection)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== SYSTEM INFORMATION ===");
        await connection.SendLineAsync($"BBS Software: Blackboard v1.0");
        await connection.SendLineAsync($"Platform: .NET 8.0 on {Environment.OSVersion}");
        await connection.SendLineAsync($"Server Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await connection.SendLineAsync($"Uptime: {DateTime.UtcNow - Process.GetCurrentProcess().StartTime:d\\d\\ h\\h\\ m\\m}");
        
        var totalUsers = (await _userService.GetUsersAsync(0, int.MaxValue)).Count();
        var activeSessions = await _sessionService.GetAllActiveSessionsAsync();
        
        await connection.SendLineAsync($"Total Users: {totalUsers}");
        await connection.SendLineAsync($"Users Online: {activeSessions.Count()}");
        await connection.SendLineAsync("");
        await connection.SendLineAsync("Press any key to continue...");
        await connection.ReadLineAsync();
    }
}
