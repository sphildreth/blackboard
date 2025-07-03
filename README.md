# Blackboard

A modern terminal-based BBS built with .NET 8, inspired by classic BBS systems like Telegard, Mystic, and Renegade.

## Phase 1 Implementation

This implementation covers the foundational components of the Blackboard system:

### ✅ Completed Features

- **✅ .NET 8.0+ project structure** - Modular solution with separate projects for Core, Data, and UI
- **✅ Custom Telnet server** - Implemented using System.Net.Sockets with ANSI/VT100 support
- **✅ Terminal.Gui integration** - Full terminal-based UI for system administration
- **✅ SQLite database schema** - Complete database schema with tables for users, sessions, logs, and messages
- **✅ YAML-based configuration** - Hot-reloadable configuration system with file watching
- **✅ Serilog logging** - Comprehensive logging to consol~~~~e and files with configurable levels

### Project Structure

```
Blackboard/
├── src/
│   ├── Blackboard/                 # Main application
│   │   ├── Program.cs             # Application entry point
│   │   ├── UI/MainApplication.cs  # Terminal.Gui main interface
│   │   └── Blackboard.csproj
│   ├── Blackboard.Core/           # Core business logic
│   │   ├── Configuration/         # YAML configuration system
│   │   ├── Logging/              # Serilog configuration
│   │   ├── Network/              # Telnet server implementation
│   │   └── Blackboard.Core.csproj
│   └── Blackboard.Data/           # Data access layer
│       ├── DatabaseManager.cs    # SQLite database management
│       └── Blackboard.Data.csproj  
├── docs/
│   ├── PRD.md                    # Product Requirements Document
│   └── TASKS.md                  # Task checklist
├── blackboard.yml            # Default configuration
└── Blackboard.sln               # Solution file
```

### Key Components

#### 1. Configuration System
- YAML-based configuration with hot-reload capability
- Organized into logical sections (System, Network, Security, Database, Logging)
- File watcher automatically reloads configuration when changed
- Default configuration created automatically if none exists

#### 2. Telnet Server
- Custom implementation using System.Net.Sockets
- Support for ANSI/VT100 terminal sequences
- Configurable connection limits and timeouts
- Proper telnet protocol negotiation
- Connection management with event notifications

#### 3. Database Layer
- SQLite with WAL mode for better performance
- Complete schema for all planned features
- Automated table creation and indexing
- Backup functionality
- Connection pooling and timeout handling

#### 4. Logging System
- Serilog with multiple sinks (console, file)
- Configurable log levels and retention
- Separate error log files
- Machine name, process ID, and thread ID enrichment
- Structured logging support

#### 5. Terminal UI
- Terminal.Gui-based administration interface
- Real-time system status display
- Active connections monitoring
- Server start/stop controls
- Menu system for future features

### Configuration

The system uses a YAML configuration file located at `config/blackboard.yml`. Key settings include:

- **System**: Board name, sysop information, user limits
- **Network**: Telnet port, connection limits, timeouts
- **Security**: Password policies, lockout settings
- **Database**: Connection string, backup settings
- **Logging**: Log levels, file settings, retention

### Building and Running

1. Ensure you have .NET 8.0 SDK installed
2. Clone the repository
3. Build the solution:
   ```bash
   dotnet build
   ```
4. Run the application:
   ```bash
   dotnet run --project src/Blackboard
   ```

### Default Settings

- **Telnet Port**: 2323 (configurable)
- **Database**: SQLite at `data/blackboard.db`
- **Logs**: Stored in `logs/` directory
- **Config**: `config/blackboard.yml`

### Phase 4: Messaging System

#### ✅ Completed Features
- **Private messaging** - Full inbox/outbox system with read/unread tracking
- **Public message boards** - Threaded discussions with moderation and sticky messages
- **System messages** - Admin announcements and notifications
- **ANSI editor integration** - Built-in ANSI/ASCII art editor for message composition
- **Message search & pagination** - Full-text search with paginated results
- **Unread message tracking** - Real-time unread count and notification system
- **Message quotas** - Daily and monthly message limits with quota enforcement
- **User preferences** - Customizable notification settings and message controls
- **User blocking system** - Block/unblock users with JSON-based storage
- **Admin moderation tools** - Edit/delete messages, reporting system, approval queue

#### Key Features
- **ANSI Editor**: Interactive ANSI art editor accessible from message composition
- **Search System**: Search messages by content with pagination support
- **Quota Management**: Configurable daily (100) and monthly (3000) message limits
- **Blocking System**: Users can block others, stored as JSON arrays in preferences
- **Real-time Updates**: Unread counts and notifications update in real-time
- **BBS Integration**: Full integration with telnet session handler and menu system

### Security Notes

- System starts with telnet server offline by default
- Default telnet port is 2323 (not privileged port 23)
- All configuration is externalized and reloadable
- Comprehensive logging for security monitoring
- Database uses WAL mode for better concurrency

### License

MIT License - See LICENSE file for details
