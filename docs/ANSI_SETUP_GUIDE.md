# ANSI Screen Setup Guide

## Quick Start

The ANSI screen system has been implemented and is ready to use! Here's how to get started:

### 1. Screen Directory Structure

Your ANSI screens should be organized in the `screens/` directory:

```
screens/
├── login/
│   ├── connect.ans     # Initial connection banner
│   ├── logon1.ans      # Pre-login screen
│   ├── logon2.ans      # Post-login welcome
│   └── logon3.ans      # System news/info
├── menus/
│   └── mainmenu.ans    # Main menu display
├── system/
│   └── logoff.ans      # Goodbye screen
└── defaults/
    └── default.ans     # Fallback screen
```

### 2. Template Variables

All screens support template variables using `{VARIABLE_NAME}` syntax:

**System Variables:**
- `{BBS_NAME}` - Your BBS name
- `{BBS_VERSION}` - Blackboard version
- `{CURRENT_TIME}` - Current time (HH:MM:SS)
- `{CURRENT_DATE}` - Current date (YYYY-MM-DD)
- `{USERS_ONLINE}` - Number of users currently online
- `{TOTAL_USERS}` - Total registered users

**User Variables:**
- `{USER_NAME}` - User's handle
- `{USER_REAL_NAME}` - User's real name
- `{USER_LOCATION}` - User's location
- `{USER_LEVEL}` - Security level (numeric)
- `{USER_TIMELEFT}` - Time remaining in session
- `{USER_LASTCALL}` - Last login date/time

**Connection Variables:**
- `{CALLER_IP}` - User's IP address
- `{CONNECT_TIME}` - Session start time
- `{SESSION_LENGTH}` - How long they've been connected

### 3. Menu Configuration

Create YAML files to configure menu behavior. Example `mainmenu.yml`:

```yaml
screen: "menus/mainmenu.ans"
prompt: "Enter your choice: "
options:
  m:
    label: "Message Areas"
    action: "messages"
  f:
    label: "File Areas" 
    action: ConfigurationManager.FilesPath
  l:
    label: "Logoff"
    action: "logoff"
```

### 4. Screen Sequences

The system automatically shows screen sequences:

- **Pre-login:** CONNECT → LOGON1
- **Post-login:** LOGON2 → LOGON3
- **New user:** NEWUSER1 → NEWUSER2 → NEWUSER3

### 5. Creating ANSI Art

Use any ANSI art editor or create manually. The system supports:

- Standard ANSI color codes (16 colors)
- VT100 escape sequences
- Template variable replacement
- Automatic fallback if files are missing

### 6. Testing Your Screens

1. Place your `.ans` files in the appropriate directories
2. Restart the BBS or wait for hot-reload
3. Connect via telnet to see your screens
4. Check logs for any template variable errors

### Example ANSI Screen with Variables

```ansi
[2J[H[1;37;44m
╔════════════════════════════════════════════════════════════════╗
║                    Welcome to {BBS_NAME}                      ║
║                                                                ║
║  Hello {USER_NAME}! Current time: {CURRENT_TIME}              ║
║  Users online: {USERS_ONLINE}                                 ║
╚════════════════════════════════════════════════════════════════╝
[0m
```

The variables will be automatically replaced when the screen is displayed to users.
