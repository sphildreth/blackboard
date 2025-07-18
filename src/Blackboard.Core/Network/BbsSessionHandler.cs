using System.Diagnostics;
using Blackboard.Core.Configuration;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Serilog;

namespace Blackboard.Core.Network;

public class BbsSessionHandler
{
    private readonly IAnsiScreenService _ansiScreenService;
    private readonly ConfigurationManager? _configManager;
    private readonly IFileAreaService _fileAreaService;
    private readonly IKeyboardHandlerService _keyboardHandler;
    private readonly ILogger _logger;
    private readonly IMessageService _messageService;
    private readonly string _screensDir;
    private readonly IScreenSequenceService _screenSequenceService;
    private readonly ISessionService _sessionService;
    private readonly IUserService _userService;

    public BbsSessionHandler(
        IUserService userService,
        ISessionService sessionService,
        IMessageService messageService,
        IFileAreaService fileAreaService,
        IAnsiScreenService ansiScreenService,
        IScreenSequenceService screenSequenceService,
        IKeyboardHandlerService keyboardHandler,
        ILogger logger,
        string screensDir,
        ConfigurationManager? configManager = null)
    {
        _userService = userService;
        _sessionService = sessionService;
        _messageService = messageService;
        _fileAreaService = fileAreaService;
        _ansiScreenService = ansiScreenService;
        _screenSequenceService = screenSequenceService;
        _keyboardHandler = keyboardHandler;
        _logger = logger;
        _screensDir = screensDir;
        _configManager = configManager;
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

                var choice = await _keyboardHandler.ReadLineAsync(connection);
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
                            // Create session for newly registered user
                            session = await _sessionService.CreateSessionAsync(user.Id, connection.RemoteEndPoint?.ToString() ?? "unknown");
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

            if (user != null && session != null) await HandleAuthenticatedSession(connection, user, session, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in BBS session handling");
            await connection.SendLineAsync("An error occurred. Please try again later.");
        }
    }

    private async Task ShowWelcomeScreen(TelnetConnection connection)
    {
        // Create basic context for welcome screen
        var userContext = new UserContext
        {
            CallerIp = connection.RemoteEndPoint?.ToString(),
            ConnectTime = DateTime.UtcNow,
            SystemInfo = new Dictionary<string, object>
            {
                ["BBS_NAME"] = await GetBbsNameFromConfigAsync(),
                ["BBS_VERSION"] = "1.0",
                ["NODE_NUMBER"] = 1
            }
        };

        // Show pre-login screen sequence (CONNECT, LOGON1)
        await _screenSequenceService.ShowSequenceAsync("PRELOGIN", connection, userContext);
    }

    private async Task<(UserProfileDto?, UserSession?)> HandleLogin(TelnetConnection connection)
    {
        try
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== LOGIN ===");
            await connection.SendAsync("Handle: ");
            
            var handle = await _keyboardHandler.ReadLineAsync(connection);

            if (string.IsNullOrWhiteSpace(handle))
            {
                await connection.SendLineAsync("Handle cannot be empty.");
                return (null, null);
            }

            await connection.SendAsync("Password: ");
            var password = await _keyboardHandler.ReadLineAsync(connection, false);

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

            await connection.SendLineAsync("Invalid credentials. Please try again.");
            return (null, null);
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
            var handle = await _keyboardHandler.ReadLineAsync(connection);

            if (string.IsNullOrWhiteSpace(handle))
            {
                await connection.SendLineAsync("Handle cannot be empty.");
                return null;
            }

            await connection.SendAsync("Email Address: ");
            var email = await _keyboardHandler.ReadLineAsync(connection);

            if (string.IsNullOrWhiteSpace(email) && (_configManager?.Configuration.Security.RequireEmailAddress ?? false))
            {
                await connection.SendLineAsync("Email cannot be empty.");
                return null;
            }

            await connection.SendAsync("Password: ");
            var password = await _keyboardHandler.ReadLineAsync(connection, false);

            if (string.IsNullOrWhiteSpace(password))
            {
                await connection.SendLineAsync("Password cannot be empty.");
                return null;
            }

            await connection.SendAsync("Confirm Password: ");
            var confirmPassword = await _keyboardHandler.ReadLineAsync(connection, false);

            if (password != confirmPassword)
            {
                await connection.SendLineAsync("Passwords do not match.");
                return null;
            }

            await connection.SendAsync("First Name (optional): ");
            var firstName = await _keyboardHandler.ReadLineAsync(connection);

            await connection.SendAsync("Last Name (optional): ");
            var lastName = await _keyboardHandler.ReadLineAsync(connection);

            await connection.SendAsync("Location (optional): ");
            var location = await _keyboardHandler.ReadLineAsync(connection);

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

            await connection.SendLineAsync("Registration failed. Handle or email may already be taken.");
            return null;
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
        await _keyboardHandler.ReadLineAsync(connection);
    }

    private async Task HandleAuthenticatedSession(TelnetConnection connection, UserProfileDto user, UserSession session, CancellationToken cancellationToken)
    {
        // Create user context for template processing
        var userContext = new UserContext
        {
            User = user,
            Session = session,
            CallerIp = connection.RemoteEndPoint?.ToString(),
            ConnectTime = session.CreatedAt,
            SystemInfo = new Dictionary<string, object>
            {
                ["BBS_NAME"] = await GetBbsNameFromConfigAsync(),
                ["BBS_VERSION"] = "1.0",
                ["NODE_NUMBER"] = 1,
                ["USERS_ONLINE"] = (await _sessionService.GetAllActiveSessionsAsync()).Count(),
                ["TOTAL_USERS"] = (await _userService.GetUsersAsync(0, int.MaxValue)).Count()
            }
        };

        // Show post-login screen sequence (LOGON2, LOGON3)
        await _screenSequenceService.ShowSequenceAsync("POSTLOGIN", connection, userContext);

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
        var currentMenu = menuConfigFile;

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

            // Load and send the screen file (ASCII preferred, ANSI if user requested it)
            var preferAnsi = connection.UserRequestedAnsi;
            var screenContent = await MenuConfigLoader.LoadScreenAsync(screensDir, menuConfig.Screen, preferAnsi);
            
            // Send the screen content using the appropriate method
            if (screenContent.Contains("\x1b[") || screenContent.Contains("["))
            {
                // Content contains ANSI codes, use SendAnsiAsync which handles ASCII/ANSI detection
                await connection.SendAnsiAsync(screenContent);
            }
            else
            {
                // Pure ASCII content, send directly
                await connection.SendAsync(screenContent);
            }

            // Show prompt and wait for input
            await connection.SendAsync(menuConfig.Prompt ?? "Choice: ");
            var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim().ToLower();

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
                    switch (option.Action.ToLower())
                    {
                        case "quit":
                        case "logoff":
                        case "logout":
                            await _sessionService.EndSessionAsync(session.Id);
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
                            await ShowMessageMenu(connection, user);
                            break;
                        case "files":
                            await ShowFileMenu(connection, user);
                            break;
                        case "terminal":
                            await ShowTerminalSettings(connection);
                            break;
                        // Add more actions as needed
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
        await _keyboardHandler.ReadLineAsync(connection);
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
                if (sessionUser != null) await connection.SendLineAsync($"{sessionUser.Handle,-15} {sessionUser.Location ?? "Unknown",-20} {session.CreatedAt:HH:mm:ss}");
            }
        }

        await connection.SendLineAsync("");
        await connection.SendLineAsync("Press any key to continue...");
        await _keyboardHandler.ReadLineAsync(connection);
    }

    private async Task ShowSystemInfo(TelnetConnection connection)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== SYSTEM INFORMATION ===");
        await connection.SendLineAsync("BBS Software: Blackboard v1.0");
        await connection.SendLineAsync($"Platform: .NET 8.0 on {Environment.OSVersion}");
        await connection.SendLineAsync($"Server Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await connection.SendLineAsync($"Uptime: {DateTime.UtcNow - Process.GetCurrentProcess().StartTime:d\\d\\ h\\h\\ m\\m}");

        var totalUsers = (await _userService.GetUsersAsync(0, int.MaxValue)).Count();
        var activeSessions = await _sessionService.GetAllActiveSessionsAsync();

        await connection.SendLineAsync($"Total Users: {totalUsers}");
        await connection.SendLineAsync($"Users Online: {activeSessions.Count()}");
        await connection.SendLineAsync("");
        await connection.SendLineAsync("Press any key to continue...");
        await _keyboardHandler.ReadLineAsync(connection);
    }

    // Phase 4: Enhanced messaging functionality
    private async Task ShowMessageMenu(TelnetConnection connection, UserProfileDto user)
    {
        while (true)
        {
            var unreadCount = await _messageService.GetUnreadCountAsync(user.Id);
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== MESSAGES ===");
            await connection.SendLineAsync($"1. Inbox {(unreadCount > 0 ? $"({unreadCount} unread)" : "")}");
            await connection.SendLineAsync("2. Outbox");
            await connection.SendLineAsync("3. Send Message");
            await connection.SendLineAsync("4. Search Messages");
            await connection.SendLineAsync("5. Message Preferences");
            await connection.SendLineAsync("6. Back");
            await connection.SendAsync("Select option: ");
            var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim();
            switch (input)
            {
                case "1":
                    await ShowInbox(connection, user);
                    break;
                case "2":
                    await ShowOutbox(connection, user);
                    break;
                case "3":
                    await SendPrivateMessage(connection, user);
                    break;
                case "4":
                    await ShowMessageSearch(connection, user);
                    break;
                case "5":
                    await ShowMessagePreferences(connection, user);
                    break;
                case "6":
                    return;
                default:
                    await connection.SendLineAsync("Invalid option.");
                    break;
            }
        }
    }

    private async Task ShowInbox(TelnetConnection connection, UserProfileDto user)
    {
        var messages = await _messageService.GetInboxAsync(user.Id);
        await connection.SendLineAsync("\n--- INBOX ---");
        var i = 1;
        foreach (var msg in messages) await connection.SendLineAsync($"{i++}. From: {msg.FromUserId} | Subject: {msg.Subject} | {(msg.IsRead ? "Read" : "Unread")} | {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        await connection.SendLineAsync("Enter message # to read, or press Enter to return.");
        var input = await _keyboardHandler.ReadLineAsync(connection);
        if (int.TryParse(input, out var idx) && idx > 0 && idx <= messages.Count())
        {
            var msg = messages.ElementAt(idx - 1);
            await ShowMessageDetail(connection, user, msg);
        }
    }

    private async Task ShowOutbox(TelnetConnection connection, UserProfileDto user)
    {
        var messages = await _messageService.GetOutboxAsync(user.Id);
        await connection.SendLineAsync("\n--- OUTBOX ---");
        var i = 1;
        foreach (var msg in messages) await connection.SendLineAsync($"{i++}. To: {msg.ToUserId} | Subject: {msg.Subject} | {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        await connection.SendLineAsync("Enter message # to view, or press Enter to return.");
        var input = await _keyboardHandler.ReadLineAsync(connection);
        if (int.TryParse(input, out var idx) && idx > 0 && idx <= messages.Count())
        {
            var msg = messages.ElementAt(idx - 1);
            await ShowMessageDetail(connection, user, msg);
        }
    }

    private async Task ShowMessageDetail(TelnetConnection connection, UserProfileDto user, Message msg)
    {
        await connection.SendLineAsync("\n--- MESSAGE ---");
        await connection.SendLineAsync($"From: {msg.FromUserId}");
        await connection.SendLineAsync($"To: {msg.ToUserId}");
        await connection.SendLineAsync($"Subject: {msg.Subject}");
        await connection.SendLineAsync($"Date: {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        await connection.SendLineAsync($"Body:\n{msg.Body}");
        if (!msg.IsRead && msg.ToUserId == user.Id)
            await _messageService.MarkAsReadAsync(msg.Id, user.Id);
        await connection.SendLineAsync("Press D to delete, Enter to return.");
        var input = await _keyboardHandler.ReadLineAsync(connection);
        if (input?.Trim().ToUpper() == "D")
        {
            await _messageService.DeleteMessageAsync(msg.Id, user.Id);
            await connection.SendLineAsync("Message deleted.");
        }
    }

    private async Task SendPrivateMessage(TelnetConnection connection, UserProfileDto user)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("--- SEND PRIVATE MESSAGE ---");
        await connection.SendAsync("To User ID: ");
        var toUserIdStr = await _keyboardHandler.ReadLineAsync(connection);
        if (!int.TryParse(toUserIdStr, out var toUserId))
        {
            await connection.SendLineAsync("Invalid user ID.");
            return;
        }

        // Check if the target user has blocked this user
        if (await _messageService.IsUserBlockedAsync(user.Id, toUserId))
        {
            await connection.SendLineAsync("Cannot send message: You have been blocked by this user.");
            return;
        }

        // Check if user can send messages (quota)
        if (!await _messageService.CanSendMessageAsync(user.Id))
        {
            var quota = await _messageService.GetMessageQuotaAsync(user.Id);
            await connection.SendLineAsync($"Cannot send message: Daily quota of {quota} messages exceeded.");
            return;
        }

        await connection.SendAsync("Subject: ");
        var subject = await _keyboardHandler.ReadLineAsync(connection);

        // Check user preferences for ANSI editor
        var preferences = await _messageService.GetUserPreferencesAsync(user.Id);
        string body;

        if (preferences.EnableAnsiEditor)
        {
            await connection.SendLineAsync("Use ANSI editor? (Y/n): ");
            var useAnsi = await _keyboardHandler.ReadLineAsync(connection);
            if (string.IsNullOrEmpty(useAnsi) || useAnsi.ToUpper().StartsWith("Y"))
            {
                body = await ComposeWithAnsiEditor(connection);
            }
            else
            {
                await connection.SendAsync("Message Body: ");
                body = await _keyboardHandler.ReadLineAsync(connection);
            }
        }
        else
        {
            await connection.SendAsync("Message Body: ");
            body = await _keyboardHandler.ReadLineAsync(connection);
        }

        // Add signature if enabled
        if (preferences.ShowSignature && !string.IsNullOrEmpty(preferences.Signature)) body += $"\n\n---\n{preferences.Signature}";

        await _messageService.SendPrivateMessageAsync(user.Id, toUserId, subject ?? string.Empty, body ?? string.Empty);
        await connection.SendLineAsync("Message sent.");
    }

    private async Task<string> ComposeWithAnsiEditor(TelnetConnection connection)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== ANSI EDITOR ===");
        await connection.SendLineAsync("Enter your message. Type '/save' on a new line to finish, '/quit' to cancel:");
        await connection.SendLineAsync("");

        var lines = new List<string>();
        while (true)
        {
            var line = await _keyboardHandler.ReadLineAsync(connection);
            if (line == null) break;

            if (line.Trim().ToLower() == "/save") break;

            if (line.Trim().ToLower() == "/quit")
            {
                await connection.SendLineAsync("Message composition cancelled.");
                return string.Empty;
            }

            if (line.Trim().ToLower() == "/help")
            {
                await connection.SendLineAsync("ANSI Editor Commands:");
                await connection.SendLineAsync("  /save  - Save and send message");
                await connection.SendLineAsync("  /quit  - Cancel message");
                await connection.SendLineAsync("  /help  - Show this help");
                await connection.SendLineAsync("  ANSI codes supported (e.g., \\x1b[31m for red)");
                continue;
            }

            lines.Add(line);
        }

        return string.Join("\n", lines);
    }

    private async Task ShowMessageSearch(TelnetConnection connection, UserProfileDto user)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== MESSAGE SEARCH ===");
        await connection.SendAsync("Enter search term: ");
        var query = await _keyboardHandler.ReadLineAsync(connection);

        if (string.IsNullOrWhiteSpace(query))
        {
            await connection.SendLineAsync("Search cancelled.");
            return;
        }

        var results = await _messageService.SearchMessagesAsync(user.Id, query, 1, 50);
        await connection.SendLineAsync($"\n--- SEARCH RESULTS for '{query}' ---");

        var i = 1;
        foreach (var msg in results)
        {
            var messageType = msg.MessageType == MessageType.Private ? "PM" : "PUB";
            var direction = msg.FromUserId == user.Id ? "TO" : "FROM";
            var otherUserId = msg.FromUserId == user.Id ? msg.ToUserId : msg.FromUserId;
            await connection.SendLineAsync($"{i++}. [{messageType}] {direction}: {otherUserId} | Subject: {msg.Subject} | {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        if (!results.Any()) await connection.SendLineAsync("No messages found matching your search.");

        await connection.SendLineAsync("\nPress any key to continue...");
        await _keyboardHandler.ReadLineAsync(connection);
    }

    private async Task ShowMessagePreferences(TelnetConnection connection, UserProfileDto user)
    {
        var preferences = await _messageService.GetUserPreferencesAsync(user.Id);

        while (true)
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== MESSAGE PREFERENCES ===");
            await connection.SendLineAsync($"1. Allow Private Messages: {(preferences.AllowPrivateMessages ? "Yes" : "No")}");
            await connection.SendLineAsync($"2. Notify on New Message: {(preferences.NotifyOnNewMessage ? "Yes" : "No")}");
            await connection.SendLineAsync($"3. Show Signature: {(preferences.ShowSignature ? "Yes" : "No")}");
            await connection.SendLineAsync($"4. Enable ANSI Editor: {(preferences.EnableAnsiEditor ? "Yes" : "No")}");
            await connection.SendLineAsync($"5. Auto-mark Read: {(preferences.AutoMarkRead ? "Yes" : "No")}");
            await connection.SendLineAsync("6. Edit Signature");
            await connection.SendLineAsync("7. Manage Blocked Users");
            await connection.SendLineAsync("8. Back");
            await connection.SendAsync("Select option: ");

            var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim();
            switch (input)
            {
                case "1":
                    preferences.AllowPrivateMessages = !preferences.AllowPrivateMessages;
                    await _messageService.UpdateUserPreferencesAsync(user.Id, preferences);
                    await connection.SendLineAsync($"Private messages {(preferences.AllowPrivateMessages ? "enabled" : "disabled")}.");
                    break;
                case "2":
                    preferences.NotifyOnNewMessage = !preferences.NotifyOnNewMessage;
                    await _messageService.UpdateUserPreferencesAsync(user.Id, preferences);
                    await connection.SendLineAsync($"New message notifications {(preferences.NotifyOnNewMessage ? "enabled" : "disabled")}.");
                    break;
                case "3":
                    preferences.ShowSignature = !preferences.ShowSignature;
                    await _messageService.UpdateUserPreferencesAsync(user.Id, preferences);
                    await connection.SendLineAsync($"Signature display {(preferences.ShowSignature ? "enabled" : "disabled")}.");
                    break;
                case "4":
                    preferences.EnableAnsiEditor = !preferences.EnableAnsiEditor;
                    await _messageService.UpdateUserPreferencesAsync(user.Id, preferences);
                    await connection.SendLineAsync($"ANSI editor {(preferences.EnableAnsiEditor ? "enabled" : "disabled")}.");
                    break;
                case "5":
                    preferences.AutoMarkRead = !preferences.AutoMarkRead;
                    await _messageService.UpdateUserPreferencesAsync(user.Id, preferences);
                    await connection.SendLineAsync($"Auto-mark read {(preferences.AutoMarkRead ? "enabled" : "disabled")}.");
                    break;
                case "6":
                    await EditSignature(connection, user, preferences);
                    break;
                case "7":
                    await ManageBlockedUsers(connection, user);
                    break;
                case "8":
                    return;
                default:
                    await connection.SendLineAsync("Invalid option.");
                    break;
            }
        }
    }

    private async Task EditSignature(TelnetConnection connection, UserProfileDto user, MessagePreferences preferences)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== EDIT SIGNATURE ===");
        await connection.SendLineAsync($"Current signature: {preferences.Signature ?? "(none)"}");
        await connection.SendAsync("Enter new signature (max 200 chars, empty to clear): ");

        var newSignature = await _keyboardHandler.ReadLineAsync(connection);
        if (newSignature != null && newSignature.Length > 200)
        {
            newSignature = newSignature.Substring(0, 200);
            await connection.SendLineAsync("Signature truncated to 200 characters.");
        }

        preferences.Signature = string.IsNullOrWhiteSpace(newSignature) ? null : newSignature;
        await _messageService.UpdateUserPreferencesAsync(user.Id, preferences);
        await connection.SendLineAsync("Signature updated.");
    }

    private async Task ManageBlockedUsers(TelnetConnection connection, UserProfileDto user)
    {
        while (true)
        {
            var blockedUsers = await _messageService.GetBlockedUsersAsync(user.Id);
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== BLOCKED USERS ===");

            if (blockedUsers.Any())
            {
                await connection.SendLineAsync("Currently blocked users:");
                foreach (var blockedUserId in blockedUsers) await connection.SendLineAsync($"  User ID: {blockedUserId}");
            }
            else
            {
                await connection.SendLineAsync("No users are currently blocked.");
            }

            await connection.SendLineAsync("");
            await connection.SendLineAsync("1. Block a user");
            await connection.SendLineAsync("2. Unblock a user");
            await connection.SendLineAsync("3. Back");
            await connection.SendAsync("Select option: ");

            var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim();
            switch (input)
            {
                case "1":
                    await connection.SendAsync("Enter User ID to block: ");
                    var blockIdStr = await _keyboardHandler.ReadLineAsync(connection);
                    if (int.TryParse(blockIdStr, out var blockId))
                    {
                        if (blockId == user.Id)
                            await connection.SendLineAsync("You cannot block yourself.");
                        else if (await _messageService.BlockUserAsync(user.Id, blockId))
                            await connection.SendLineAsync($"User {blockId} has been blocked.");
                        else
                            await connection.SendLineAsync("Failed to block user.");
                    }
                    else
                    {
                        await connection.SendLineAsync("Invalid User ID.");
                    }

                    break;
                case "2":
                    await connection.SendAsync("Enter User ID to unblock: ");
                    var unblockIdStr = await _keyboardHandler.ReadLineAsync(connection);
                    if (int.TryParse(unblockIdStr, out var unblockId))
                    {
                        if (await _messageService.UnblockUserAsync(user.Id, unblockId))
                            await connection.SendLineAsync($"User {unblockId} has been unblocked.");
                        else
                            await connection.SendLineAsync("Failed to unblock user.");
                    }
                    else
                    {
                        await connection.SendLineAsync("Invalid User ID.");
                    }

                    break;
                case "3":
                    return;
                default:
                    await connection.SendLineAsync("Invalid option.");
                    break;
            }
        }
    }

    // Phase 5: File Area Management functionality
    private async Task ShowFileMenu(TelnetConnection connection, UserProfileDto user)
    {
        while (true)
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== FILE AREAS ===");
            await connection.SendLineAsync("1. Browse File Areas");
            await connection.SendLineAsync("2. Search Files");
            await connection.SendLineAsync("3. Recent Uploads");
            await connection.SendLineAsync("4. Most Downloaded");
            await connection.SendLineAsync("5. My Uploads");
            await connection.SendLineAsync("6. Upload File");
            await connection.SendLineAsync("7. Back");
            await connection.SendAsync("Select option: ");

            var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim();
            switch (input)
            {
                case "1":
                    await BrowseFileAreas(connection, user);
                    break;
                case "2":
                    await SearchFiles(connection, user);
                    break;
                case "3":
                    await ShowRecentUploads(connection, user);
                    break;
                case "4":
                    await ShowMostDownloaded(connection, user);
                    break;
                case "5":
                    await ShowMyUploads(connection, user);
                    break;
                case "6":
                    await UploadFile(connection, user);
                    break;
                case "7":
                    return;
                default:
                    await connection.SendLineAsync("Invalid option.");
                    break;
            }
        }
    }

    private async Task BrowseFileAreas(TelnetConnection connection, UserProfileDto user)
    {
        var areas = await _fileAreaService.GetActiveFileAreasAsync();

        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== FILE AREAS ===");

        var areaList = areas.ToList();
        if (!areaList.Any())
        {
            await connection.SendLineAsync("No file areas available.");
            return;
        }

        for (var i = 0; i < areaList.Count; i++)
        {
            var area = areaList[i];
            var canAccess = await _fileAreaService.CanUserAccessAreaAsync(user.Id, area.Id);
            var accessIndicator = canAccess ? "" : " [RESTRICTED]";
            await connection.SendLineAsync($"{i + 1}. {area.Name} ({area.FileCount} files){accessIndicator}");
            if (!string.IsNullOrEmpty(area.Description))
                await connection.SendLineAsync($"   {area.Description}");
        }

        await connection.SendLineAsync("");
        await connection.SendAsync("Enter area number to browse (or press Enter to return): ");
        var input = await _keyboardHandler.ReadLineAsync(connection);

        if (int.TryParse(input, out var areaIndex) && areaIndex > 0 && areaIndex <= areaList.Count)
        {
            var selectedArea = areaList[areaIndex - 1];
            await BrowseFilesInArea(connection, user, selectedArea);
        }
    }

    private async Task BrowseFilesInArea(TelnetConnection connection, UserProfileDto user, FileAreaDto area)
    {
        if (!await _fileAreaService.CanUserAccessAreaAsync(user.Id, area.Id))
        {
            await connection.SendLineAsync("You do not have access to this file area.");
            return;
        }

        var page = 1;
        const int pageSize = 10;

        while (true)
        {
            var result = await _fileAreaService.SearchFilesAsync(areaId: area.Id, page: page, pageSize: pageSize);

            await connection.SendLineAsync("");
            await connection.SendLineAsync($"=== {area.Name} === (Page {page})");

            if (!result.Files.Any())
                await connection.SendLineAsync("No files found in this area.");
            else
                for (var i = 0; i < result.Files.Count; i++)
                {
                    var file = result.Files[i];
                    await connection.SendLineAsync($"{i + 1}. {file.OriginalFileName} ({file.SizeFormatted})");
                    await connection.SendLineAsync($"   Downloaded {file.DownloadCount} times | Rating: {file.AverageRating:F1}/5.0");
                    if (!string.IsNullOrEmpty(file.Description))
                        await connection.SendLineAsync($"   {file.Description}");
                }

            await connection.SendLineAsync("");

            var options = new List<string>();
            if (result.Files.Any()) options.Add("Enter file number to view details");
            if (result.HasPreviousPage) options.Add("P for previous page");
            if (result.HasNextPage) options.Add("N for next page");
            options.Add("Enter to return");

            await connection.SendLineAsync(string.Join(", ", options));
            await connection.SendAsync("Choice: ");

            var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim().ToLower();

            if (string.IsNullOrEmpty(input)) break;

            if (input == "n" && result.HasNextPage)
            {
                page++;
            }
            else if (input == "p" && result.HasPreviousPage)
            {
                page--;
            }
            else if (int.TryParse(input, out var fileIndex) && fileIndex > 0 && fileIndex <= result.Files.Count)
            {
                var selectedFile = result.Files[fileIndex - 1];
                await ShowFileDetails(connection, user, selectedFile);
            }
            else
            {
                await connection.SendLineAsync("Invalid choice.");
            }
        }
    }

    private async Task ShowFileDetails(TelnetConnection connection, UserProfileDto user, BbsFileDto file)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync($"=== {file.OriginalFileName} ===");
        await connection.SendLineAsync($"Area: {file.AreaName}");
        await connection.SendLineAsync($"Size: {file.SizeFormatted}");
        await connection.SendLineAsync($"Uploaded: {file.UploadDate:yyyy-MM-dd} by {file.UploaderHandle ?? "Unknown"}");
        await connection.SendLineAsync($"Downloads: {file.DownloadCount}");
        await connection.SendLineAsync($"Rating: {file.AverageRating:F1}/5.0 ({file.RatingCount} ratings)");

        if (file.Tags.Any())
            await connection.SendLineAsync($"Tags: {string.Join(", ", file.Tags)}");

        if (!string.IsNullOrEmpty(file.Description))
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("Description:");
            await connection.SendLineAsync(file.Description);
        }

        // Show ratings/comments
        var ratings = await _fileAreaService.GetFileRatingsAsync(file.Id);
        if (ratings.Any())
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("Recent Comments:");
            foreach (var rating in ratings.Take(3)) await connection.SendLineAsync($"  {rating.UserHandle}: {rating.Rating}/5 - {rating.Comment ?? "No comment"}");
        }

        await connection.SendLineAsync("");
        await connection.SendLineAsync("1. Download File");
        await connection.SendLineAsync("2. Rate/Comment");
        await connection.SendLineAsync("3. Back");
        await connection.SendAsync("Choice: ");

        var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim();
        switch (input)
        {
            case "1":
                await DownloadFile(connection, user, file);
                break;
            case "2":
                await RateFile(connection, user, file);
                break;
            case "3":
                return;
        }
    }

    private async Task DownloadFile(TelnetConnection connection, UserProfileDto user, BbsFileDto file)
    {
        try
        {
            if (!await _fileAreaService.CanUserAccessAreaAsync(user.Id, file.AreaId))
            {
                await connection.SendLineAsync("You do not have download access to this file area.");
                return;
            }

            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== DOWNLOAD FILE ===");
            await connection.SendLineAsync($"File: {file.OriginalFileName} ({file.SizeFormatted})");
            await connection.SendLineAsync("");
            await connection.SendLineAsync("Transfer protocols available:");
            await connection.SendLineAsync("1. HTTP Download (recommended for modern clients)");
            await connection.SendLineAsync("2. ZMODEM (classic BBS protocol)");
            await connection.SendLineAsync("3. XMODEM");
            await connection.SendLineAsync("4. Cancel");
            await connection.SendAsync("Select protocol: ");

            var protocolChoice = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim();

            switch (protocolChoice)
            {
                case "1":
                    await connection.SendLineAsync("HTTP download not yet implemented in Phase 5.");
                    await connection.SendLineAsync("This feature will be added in a future update.");
                    break;
                case "2":
                    await connection.SendLineAsync("ZMODEM download not yet implemented in Phase 5.");
                    await connection.SendLineAsync("This feature will be added in a future update.");
                    break;
                case "3":
                    await connection.SendLineAsync("XMODEM download not yet implemented in Phase 5.");
                    await connection.SendLineAsync("This feature will be added in a future update.");
                    break;
                case "4":
                    return;
                default:
                    await connection.SendLineAsync("Invalid choice.");
                    return;
            }

            // For now, just record the download attempt
            await _fileAreaService.RecordDownloadAsync(file.Id, user.Id);
            await connection.SendLineAsync("Download recorded (protocol implementation pending).");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error downloading file {FileId} for user {UserId}", file.Id, user.Id);
            await connection.SendLineAsync("Error occurred during download.");
        }
    }

    private async Task RateFile(TelnetConnection connection, UserProfileDto user, BbsFileDto file)
    {
        try
        {
            var existingRating = await _fileAreaService.GetUserFileRatingAsync(file.Id, user.Id);

            await connection.SendLineAsync("");
            await connection.SendLineAsync($"=== RATE FILE: {file.OriginalFileName} ===");

            if (existingRating != null)
            {
                await connection.SendLineAsync($"Your current rating: {existingRating.Rating}/5");
                if (!string.IsNullOrEmpty(existingRating.Comment))
                    await connection.SendLineAsync($"Your comment: {existingRating.Comment}");
            }

            await connection.SendAsync("Enter rating (1-5): ");
            var ratingInput = await _keyboardHandler.ReadLineAsync(connection);

            if (!int.TryParse(ratingInput, out var rating) || rating < 1 || rating > 5)
            {
                await connection.SendLineAsync("Invalid rating. Please enter a number from 1 to 5.");
                return;
            }

            await connection.SendAsync("Enter comment (optional, press Enter to skip): ");
            var comment = await _keyboardHandler.ReadLineAsync(connection);

            if (string.IsNullOrWhiteSpace(comment))
                comment = null;

            if (existingRating != null)
            {
                await _fileAreaService.UpdateFileRatingAsync(file.Id, user.Id, rating, comment);
                await connection.SendLineAsync("Your rating has been updated!");
            }
            else
            {
                await _fileAreaService.AddFileRatingAsync(file.Id, user.Id, rating, comment);
                await connection.SendLineAsync("Thank you for your rating!");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error rating file {FileId} for user {UserId}", file.Id, user.Id);
            await connection.SendLineAsync("Error occurred while saving rating.");
        }
    }

    private async Task SearchFiles(TelnetConnection connection, UserProfileDto user)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== SEARCH FILES ===");
        await connection.SendAsync("Enter search term (filename or description): ");

        var searchTerm = await _keyboardHandler.ReadLineAsync(connection);
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            await connection.SendLineAsync("Search cancelled.");
            return;
        }

        var result = await _fileAreaService.SearchFilesAsync(searchTerm.Trim(), pageSize: 20);

        await connection.SendLineAsync("");
        await connection.SendLineAsync($"=== SEARCH RESULTS: '{searchTerm}' ===");

        if (!result.Files.Any())
        {
            await connection.SendLineAsync("No files found matching your search.");
            return;
        }

        await connection.SendLineAsync($"Found {result.TotalCount} files:");
        await connection.SendLineAsync("");

        for (var i = 0; i < result.Files.Count; i++)
        {
            var file = result.Files[i];
            await connection.SendLineAsync($"{i + 1}. {file.OriginalFileName} ({file.SizeFormatted}) - {file.AreaName}");
            if (!string.IsNullOrEmpty(file.Description))
                await connection.SendLineAsync($"   {file.Description}");
        }

        await connection.SendLineAsync("");
        await connection.SendAsync("Enter file number to view details (or press Enter to return): ");
        var input = await _keyboardHandler.ReadLineAsync(connection);

        if (int.TryParse(input, out var fileIndex) && fileIndex > 0 && fileIndex <= result.Files.Count)
        {
            var selectedFile = result.Files[fileIndex - 1];
            await ShowFileDetails(connection, user, selectedFile);
        }
    }

    private async Task ShowRecentUploads(TelnetConnection connection, UserProfileDto user)
    {
        var recentFiles = await _fileAreaService.GetRecentUploadsAsync(15);

        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== RECENT UPLOADS ===");

        if (!recentFiles.Any())
        {
            await connection.SendLineAsync("No recent uploads found.");
            return;
        }

        var fileList = recentFiles.ToList();
        for (var i = 0; i < fileList.Count; i++)
        {
            var file = fileList[i];
            await connection.SendLineAsync($"{i + 1}. {file.OriginalFileName} ({file.SizeFormatted}) - {file.AreaName}");
            await connection.SendLineAsync($"   Uploaded {file.UploadDate:yyyy-MM-dd} by {file.UploaderHandle ?? "Unknown"}");
        }

        await connection.SendLineAsync("");
        await connection.SendAsync("Enter file number to view details (or press Enter to return): ");
        var input = await _keyboardHandler.ReadLineAsync(connection);

        if (int.TryParse(input, out var fileIndex) && fileIndex > 0 && fileIndex <= fileList.Count)
        {
            var selectedFile = fileList[fileIndex - 1];
            await ShowFileDetails(connection, user, selectedFile);
        }
    }

    private async Task ShowMostDownloaded(TelnetConnection connection, UserProfileDto user)
    {
        var popularFiles = await _fileAreaService.GetMostDownloadedFilesAsync(15);

        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== MOST DOWNLOADED FILES ===");

        if (!popularFiles.Any())
        {
            await connection.SendLineAsync("No download statistics available.");
            return;
        }

        var fileList = popularFiles.ToList();
        for (var i = 0; i < fileList.Count; i++)
        {
            var file = fileList[i];
            await connection.SendLineAsync($"{i + 1}. {file.OriginalFileName} ({file.DownloadCount} downloads) - {file.AreaName}");
            await connection.SendLineAsync($"   {file.SizeFormatted} | Rating: {file.AverageRating:F1}/5.0");
        }

        await connection.SendLineAsync("");
        await connection.SendAsync("Enter file number to view details (or press Enter to return): ");
        var input = await _keyboardHandler.ReadLineAsync(connection);

        if (int.TryParse(input, out var fileIndex) && fileIndex > 0 && fileIndex <= fileList.Count)
        {
            var selectedFile = fileList[fileIndex - 1];
            await ShowFileDetails(connection, user, selectedFile);
        }
    }

    private async Task ShowMyUploads(TelnetConnection connection, UserProfileDto user)
    {
        var userStats = await _fileAreaService.GetUserFileStatisticsAsync(user.Id);

        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== MY UPLOADS ===");
        await connection.SendLineAsync($"Total uploads: {userStats.TotalFiles}");
        await connection.SendLineAsync($"Approved files: {userStats.ApprovedFiles}");
        await connection.SendLineAsync($"Pending approval: {userStats.PendingApproval}");
        await connection.SendLineAsync($"Total size: {FormatFileSize(userStats.TotalFileSize)}");
        await connection.SendLineAsync("");

        if (!userStats.RecentUploads.Any())
        {
            await connection.SendLineAsync("You haven't uploaded any files yet.");
            return;
        }

        await connection.SendLineAsync("Your recent uploads:");
        for (var i = 0; i < userStats.RecentUploads.Count; i++)
        {
            var file = userStats.RecentUploads[i];
            var status = file.IsApproved ? "APPROVED" : "PENDING";
            await connection.SendLineAsync($"{i + 1}. {file.OriginalFileName} ({file.SizeFormatted}) - {status}");
            await connection.SendLineAsync($"   Area: {file.AreaName} | Downloads: {file.DownloadCount}");
        }

        await connection.SendLineAsync("");
        await connection.SendAsync("Enter file number to view details (or press Enter to return): ");
        var input = await _keyboardHandler.ReadLineAsync(connection);

        if (int.TryParse(input, out var fileIndex) && fileIndex > 0 && fileIndex <= userStats.RecentUploads.Count)
        {
            var selectedFile = userStats.RecentUploads[fileIndex - 1];
            await ShowFileDetails(connection, user, selectedFile);
        }
    }

    private async Task UploadFile(TelnetConnection connection, UserProfileDto user)
    {
        await connection.SendLineAsync("");
        await connection.SendLineAsync("=== UPLOAD FILE ===");
        await connection.SendLineAsync("File uploads via telnet are not yet implemented in Phase 5.");
        await connection.SendLineAsync("This feature requires protocol implementation (ZMODEM/XMODEM/YMODEM).");
        await connection.SendLineAsync("");
        await connection.SendLineAsync("Available upload areas for your security level:");

        var areas = await _fileAreaService.GetActiveFileAreasAsync();
        var uploadableAreas = new List<FileAreaDto>();

        foreach (var area in areas)
            if (await _fileAreaService.CanUserAccessAreaAsync(user.Id, area.Id, true))
            {
                uploadableAreas.Add(area);
                await connection.SendLineAsync($"  - {area.Name}: {area.Description ?? "No description"}");
                await connection.SendLineAsync($"    Max file size: {FormatFileSize(area.MaxFileSize)}");
            }

        if (!uploadableAreas.Any()) await connection.SendLineAsync("You do not have upload permissions for any file areas.");

        await connection.SendLineAsync("");
        await connection.SendLineAsync("File upload protocol implementation will be added in a future update.");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F1} GB";
    }

    // Fix all remaining ReadLineAsync calls to use keyboard handler
    private async Task<string> ReadInputAsync(TelnetConnection connection, bool echo = true)
    {
        return await _keyboardHandler.ReadLineAsync(connection, echo);
    }

    private Task<string> GetBbsNameFromConfigAsync()
    {
        try
        {
            if (_configManager?.Configuration?.System?.BoardName != null) return Task.FromResult(_configManager.Configuration.System.BoardName);

            // Fallback to default
            return Task.FromResult("Blackboard");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting BBS name from configuration");
            return Task.FromResult("Blackboard"); // Default fallback
        }
    }

    private async Task ShowTerminalSettings(TelnetConnection connection)
    {
        while (true)
        {
            await connection.SendLineAsync("");
            await connection.SendLineAsync("=== TERMINAL SETTINGS ===");
            await connection.SendLineAsync($"Current mode: {(connection.UserRequestedAnsi ? "ANSI" : "ASCII")}");
            await connection.SendLineAsync($"Terminal type: {connection.TerminalType}");
            await connection.SendLineAsync($"Client: {connection.ClientSoftware}");
            await connection.SendLineAsync("");
            await connection.SendLineAsync("Options:");
            
            if (connection.UserRequestedAnsi)
            {
                await connection.SendLineAsync("1. Switch to ASCII mode (recommended for basic terminals)");
            }
            else
            {
                await connection.SendLineAsync("1. Enable ANSI mode (colors and graphics)");
            }
            
            await connection.SendLineAsync("2. Test terminal display");
            await connection.SendLineAsync("3. Back to main menu");
            await connection.SendAsync("Choice: ");

            var input = (await _keyboardHandler.ReadLineAsync(connection) ?? "").Trim();
            switch (input)
            {
                case "1":
                    if (connection.UserRequestedAnsi)
                    {
                        connection.DisableAnsiMode();
                        await connection.SendLineAsync("ASCII mode enabled. Screens will now display in plain text.");
                        await connection.SendLineAsync("Note: You may need to reconnect to see the change take full effect.");
                    }
                    else
                    {
                        connection.EnableAnsiMode();
                        await connection.SendLineAsync("ANSI mode enabled. Screens will now display with colors and graphics.");
                        await connection.SendLineAsync("Note: Make sure your terminal supports ANSI escape sequences.");
                    }
                    break;
                case "2":
                    await ShowTerminalTest(connection);
                    break;
                case "3":
                    return;
                default:
                    await connection.SendLineAsync("Invalid choice. Please try again.");
                    break;
            }
        }
    }

    private async Task ShowTerminalTest(TelnetConnection connection)
    {
        // Load the test screen with the current terminal settings
        var preferAnsi = connection.UserRequestedAnsi;
        var testScreen = await MenuConfigLoader.LoadScreenAsync(_screensDir, "login/test", preferAnsi);
        
        if (testScreen.Contains("\x1b[") || testScreen.Contains("["))
        {
            await connection.SendAnsiAsync(testScreen);
        }
        else
        {
            await connection.SendAsync(testScreen);
        }
        
        await connection.SendLineAsync("");
        await connection.SendLineAsync("Press any key to return to terminal settings...");
        await _keyboardHandler.ReadLineAsync(connection);
    }
}