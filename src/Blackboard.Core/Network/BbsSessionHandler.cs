using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Blackboard.Core.DTOs;
using Serilog;
using System.Diagnostics;
using System.Linq;

namespace Blackboard.Core.Network;

public class BbsSessionHandler
{
    private readonly IUserService _userService;
    private readonly ISessionService _sessionService;
    private readonly IMessageService _messageService;
    private readonly ILogger _logger;
    private readonly string _screensDir;

    public BbsSessionHandler(IUserService userService, ISessionService sessionService, IMessageService messageService, ILogger logger, string screensDir)
    {
        _userService = userService;
        _sessionService = sessionService;
        _messageService = messageService;
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
                            await ShowMessageMenu(connection, user);
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
            var input = (await connection.ReadLineAsync() ?? "").Trim();
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
        int i = 1;
        foreach (var msg in messages)
        {
            await connection.SendLineAsync($"{i++}. From: {msg.FromUserId} | Subject: {msg.Subject} | {(msg.IsRead ? "Read" : "Unread")} | {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        }
        await connection.SendLineAsync("Enter message # to read, or press Enter to return.");
        var input = await connection.ReadLineAsync();
        if (int.TryParse(input, out int idx) && idx > 0 && idx <= messages.Count())
        {
            var msg = messages.ElementAt(idx - 1);
            await ShowMessageDetail(connection, user, msg);
        }
    }

    private async Task ShowOutbox(TelnetConnection connection, UserProfileDto user)
    {
        var messages = await _messageService.GetOutboxAsync(user.Id);
        await connection.SendLineAsync("\n--- OUTBOX ---");
        int i = 1;
        foreach (var msg in messages)
        {
            await connection.SendLineAsync($"{i++}. To: {msg.ToUserId} | Subject: {msg.Subject} | {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        }
        await connection.SendLineAsync("Enter message # to view, or press Enter to return.");
        var input = await connection.ReadLineAsync();
        if (int.TryParse(input, out int idx) && idx > 0 && idx <= messages.Count())
        {
            var msg = messages.ElementAt(idx - 1);
            await ShowMessageDetail(connection, user, msg);
        }
    }

    private async Task ShowMessageDetail(TelnetConnection connection, UserProfileDto user, Message msg)
    {
        await connection.SendLineAsync($"\n--- MESSAGE ---");
        await connection.SendLineAsync($"From: {msg.FromUserId}");
        await connection.SendLineAsync($"To: {msg.ToUserId}");
        await connection.SendLineAsync($"Subject: {msg.Subject}");
        await connection.SendLineAsync($"Date: {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        await connection.SendLineAsync($"Body:\n{msg.Body}");
        if (!msg.IsRead && msg.ToUserId == user.Id)
            await _messageService.MarkAsReadAsync(msg.Id, user.Id);
        await connection.SendLineAsync("Press D to delete, Enter to return.");
        var input = await connection.ReadLineAsync();
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
        var toUserIdStr = await connection.ReadLineAsync();
        if (!int.TryParse(toUserIdStr, out int toUserId))
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
        var subject = await connection.ReadLineAsync();

        // Check user preferences for ANSI editor
        var preferences = await _messageService.GetUserPreferencesAsync(user.Id);
        string body;

        if (preferences.EnableAnsiEditor)
        {
            await connection.SendLineAsync("Use ANSI editor? (Y/n): ");
            var useAnsi = await connection.ReadLineAsync();
            if (string.IsNullOrEmpty(useAnsi) || useAnsi.ToUpper().StartsWith("Y"))
            {
                body = await ComposeWithAnsiEditor(connection);
            }
            else
            {
                await connection.SendAsync("Message Body: ");
                body = await connection.ReadLineAsync();
            }
        }
        else
        {
            await connection.SendAsync("Message Body: ");
            body = await connection.ReadLineAsync();
        }

        // Add signature if enabled
        if (preferences.ShowSignature && !string.IsNullOrEmpty(preferences.Signature))
        {
            body += $"\n\n---\n{preferences.Signature}";
        }

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
            var line = await connection.ReadLineAsync();
            if (line == null) break;

            if (line.Trim().ToLower() == "/save")
            {
                break;
            }
            else if (line.Trim().ToLower() == "/quit")
            {
                await connection.SendLineAsync("Message composition cancelled.");
                return string.Empty;
            }
            else if (line.Trim().ToLower() == "/help")
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
        var query = await connection.ReadLineAsync();
        
        if (string.IsNullOrWhiteSpace(query))
        {
            await connection.SendLineAsync("Search cancelled.");
            return;
        }

        var results = await _messageService.SearchMessagesAsync(user.Id, query, 1, 50);
        await connection.SendLineAsync($"\n--- SEARCH RESULTS for '{query}' ---");
        
        int i = 1;
        foreach (var msg in results)
        {
            var messageType = msg.MessageType == MessageType.Private ? "PM" : "PUB";
            var direction = msg.FromUserId == user.Id ? "TO" : "FROM";
            var otherUserId = msg.FromUserId == user.Id ? msg.ToUserId : msg.FromUserId;
            await connection.SendLineAsync($"{i++}. [{messageType}] {direction}: {otherUserId} | Subject: {msg.Subject} | {msg.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        if (!results.Any())
        {
            await connection.SendLineAsync("No messages found matching your search.");
        }

        await connection.SendLineAsync("\nPress any key to continue...");
        await connection.ReadLineAsync();
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
            await connection.SendLineAsync($"6. Edit Signature");
            await connection.SendLineAsync($"7. Manage Blocked Users");
            await connection.SendLineAsync($"8. Back");
            await connection.SendAsync("Select option: ");
            
            var input = (await connection.ReadLineAsync() ?? "").Trim();
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
        
        var newSignature = await connection.ReadLineAsync();
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
                foreach (var blockedUserId in blockedUsers)
                {
                    await connection.SendLineAsync($"  User ID: {blockedUserId}");
                }
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
            
            var input = (await connection.ReadLineAsync() ?? "").Trim();
            switch (input)
            {
                case "1":
                    await connection.SendAsync("Enter User ID to block: ");
                    var blockIdStr = await connection.ReadLineAsync();
                    if (int.TryParse(blockIdStr, out int blockId))
                    {
                        if (blockId == user.Id)
                        {
                            await connection.SendLineAsync("You cannot block yourself.");
                        }
                        else if (await _messageService.BlockUserAsync(user.Id, blockId))
                        {
                            await connection.SendLineAsync($"User {blockId} has been blocked.");
                        }
                        else
                        {
                            await connection.SendLineAsync("Failed to block user.");
                        }
                    }
                    else
                    {
                        await connection.SendLineAsync("Invalid User ID.");
                    }
                    break;
                case "2":
                    await connection.SendAsync("Enter User ID to unblock: ");
                    var unblockIdStr = await connection.ReadLineAsync();
                    if (int.TryParse(unblockIdStr, out int unblockId))
                    {
                        if (await _messageService.UnblockUserAsync(user.Id, unblockId))
                        {
                            await connection.SendLineAsync($"User {unblockId} has been unblocked.");
                        }
                        else
                        {
                            await connection.SendLineAsync("Failed to unblock user.");
                        }
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
}
