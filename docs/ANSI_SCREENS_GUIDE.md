# ANSI Screens & Configurable Menu System

## Overview

Blackboard supports a flexible ANSI screen and menu system inspired by classic BBS software like WWIV, but with modern configurability. This system allows sysops to define custom ANSI screens that are displayed at various stages of the user experience, from login sequences to interactive menus.

## Key Features

- **Configurable Screen Sequences**: Define ANSI screens for login, new user registration, pre-menu, and other system events
- **Template Variable Support**: Include dynamic content like username, time, system stats in ANSI screens
- **Conditional Display**: Show different screens based on user status, security level, or other criteria
- **Interactive Menus**: Create ANSI-based menus with configurable keypress-to-action mappings
- **Fallback Support**: Automatic fallback to default screens if custom ones aren't defined

## Screen Stages

The system supports the following configurable stages:

### Login Sequence
- `CONNECT` - Initial connection banner
- `LOGON1` - Pre-login screen (before username prompt)
- `LOGON2` - Post-login screen (after successful login)
- `LOGON3` - Final login screen (before main menu)

### User Registration
- `NEWUSER1` - New user welcome screen
- `NEWUSER2` - Registration instructions
- `NEWUSER3` - Post-registration confirmation

### System Events
- `LOGOFF` - Logoff/goodbye screen
- `TIMEOUT` - Session timeout warning
- `KICKED` - User kicked message
- `FEEDBACK` - Feedback to sysop prompt

### Menus
- `MAINMENU` - Primary system menu
- `MESSAGES` - Message area menu
- `FILES` - File area menu
- `DOORS` - Door games menu
- `CUSTOM_*` - Custom menu screens

## Configuration Format

Screens are configured in the main YAML configuration file under the `ansi_screens` section:

```yaml
ansi_screens:
  # Screen definitions
  screens:
    LOGON2:
      file: "screens/logon2.ans"
      template_vars: true
      conditions:
        - user_level: ">= 10"
      fallback: "screens/default_logon.ans"
    
    MAINMENU:
      file: "screens/mainmenu.ans"
      template_vars: true
      pause_after: false
      menu_mappings:
        'M': "messages"
        'F': ConfigurationManager.FilesPath
        'D': "doors"
        'U': "user_profile"
        'L': "logoff"
        '?': "help"

  # Global settings
  settings:
    screen_directory: "screens/"
    default_pause: true
    template_delimiter: "{}"
    fallback_screen: "screens/default.ans"
```

## Template Variables

ANSI screens can include template variables that are replaced at render time:

### System Variables
- `{BBS_NAME}` - BBS name from configuration
- `{BBS_VERSION}` - Blackboard version
- `{NODE_NUMBER}` - Current node number
- `{TOTAL_USERS}` - Total registered users
- `{USERS_ONLINE}` - Currently online users
- `{CURRENT_TIME}` - Current system time
- `{CURRENT_DATE}` - Current system date
- `{UPTIME}` - System uptime

### User Variables
- `{USER_NAME}` - User's handle/username
- `{USER_REAL_NAME}` - User's real name
- `{USER_LOCATION}` - User's location
- `{USER_LEVEL}` - User's security level
- `{USER_CALLS}` - User's total calls
- `{USER_LASTCALL}` - User's last call date/time
- `{USER_TIMELEFT}` - Remaining time for session

### Connection Variables
- `{CALLER_IP}` - Caller's IP address
- `{CONNECT_TIME}` - Session start time
- `{SESSION_LENGTH}` - Current session duration
- `{BAUD_RATE}` - Connection speed (emulated)

## Menu Navigation

Interactive menus support single-key navigation with configurable actions:

### Standard Actions
- `messages` - Enter message areas
- `files` - Enter file areas
- `doors` - Enter door games
- `user_profile` - User profile/settings
- `user_list` - User listing
- `time_bank` - Time banking
- `chat` - Sysop chat request
- `feedback` - Send feedback to sysop
- `help` - Display help
- `logoff` - Log off the system
- `shell` - Command shell (if permitted)

### Custom Actions
```yaml
menu_mappings:
  'N': "custom:news"  # Custom action
  'B': "door:bre"     # Specific door
  'T': "goto:SUBMENU" # Jump to another menu
```

## Conditional Display

Screens can be conditionally displayed based on user attributes:

```yaml
conditions:
  - user_level: ">= 100"          # Security level 100 or higher
  - user_group: "sysops"          # Member of sysops group
  - first_call: true              # First time caller
  - calls_today: "< 5"           # Less than 5 calls today
  - time_left: "> 60"            # More than 60 minutes remaining
  - date_range: "12/25-12/31"    # Holiday period
```

## File Organization

Recommended directory structure for ANSI screens:

```
screens/
├── login/
│   ├── connect.ans
│   ├── logon1.ans
│   ├── logon2.ans
│   └── logon3.ans
├── menus/
│   ├── mainmenu.ans
│   ├── messages.ans
│   ├── files.ans
│   └── doors.ans
├── system/
│   ├── logoff.ans
│   ├── timeout.ans
│   └── kicked.ans
└── defaults/
    ├── default.ans
    └── fallback.ans
```

## Implementation Notes

### ANSI Compatibility
- Screens should use standard ANSI/VT100 escape sequences
- Support for 16-color ANSI and extended ASCII characters
- Automatic detection of terminal capabilities when possible

### Performance Considerations
- ANSI files are cached in memory after first load
- Template variable replacement is performed at render time
- File modification monitoring for automatic cache refresh

### Error Handling
- Graceful fallback to text-only messages if ANSI files are missing
- Logging of template variable errors
- Validation of menu key mappings during configuration load

## Example ANSI Screen with Templates

```
[2J[H[1;37;44m
    ╔══════════════════════════════════════════════════════════════════════════╗
    ║                            Welcome to {BBS_NAME}                         ║
    ║                              Version {BBS_VERSION}                       ║
    ╠══════════════════════════════════════════════════════════════════════════╣
    ║  Hello {USER_NAME}!                    Current Time: {CURRENT_TIME}      ║
    ║  Last Call: {USER_LASTCALL}           Users Online: {USERS_ONLINE}       ║
    ║  Call #: {USER_CALLS}                  Time Left: {USER_TIMELEFT} mins   ║
    ╚══════════════════════════════════════════════════════════════════════════╝

[0m
```

This documentation provides a comprehensive guide for implementing and configuring the ANSI screen system in Blackboard.
