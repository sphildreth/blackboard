<div align="center">

![Blackboard Logo](assets/logo.svg)

# 🏴‍☠️ Blackboard

**A modern terminal-based bulletin board system built with .NET 8**

*Bringing the nostalgic BBS experience to the modern era*

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20Windows%20%7C%20macOS-lightgrey.svg)](#requirements)
[![Language](https://img.shields.io/badge/language-C%23-239120.svg)](https://docs.microsoft.com/en-us/dotnet/csharp/)

[Features](#-features) • [Quick Start](#-quick-start) • [Documentation](#-documentation) • [Contributing](#-contributing) • [Support](#-support)

</div>

---

## 📖 About

Blackboard is a modern implementation of classic bulletin board systems (BBS), inspired by legendary systems like **Telegard**, **Mystic**, and **Renegade**. Built with .NET 8 and featuring a rich terminal interface, it combines the nostalgic charm of 1980s-90s BBSes with modern reliability, security, and maintainability.

Whether you're a sysop looking to run a retro BBS community or a developer interested in terminal applications and networking protocols, Blackboard provides a solid, extensible foundation with comprehensive features.

## ✨ Features

### 🏗️ Core Infrastructure
- **🚀 Modern Architecture** - Clean, modular .NET 8 solution with dependency injection
- **🌐 Custom Telnet Server** - Full ANSI/VT100 support with proper telnet protocol negotiation
- **🖥️ Terminal Administration** - Rich Terminal.Gui interface for system management
- **🗃️ SQLite Database** - Reliable data persistence with WAL mode and connection pooling
- **⚙️ YAML Configuration** - Hot-reloadable configuration with file watching
- **📝 Comprehensive Logging** - Structured logging with Serilog (console + file output)

### 👥 User Management & Security
- **🔐 Secure Authentication** - BCrypt password hashing with complexity requirements
- **👤 User Profiles** - Customizable profiles with preferences and statistics
- **🛡️ Access Control** - Permission levels with granular access control
- **🔒 Session Management** - Secure session handling with timeout controls
- **📊 Audit Logging** - Complete audit trail for administrative actions

### 💬 Messaging System
- **📧 Private Messaging** - Secure inbox/outbox with read/unread tracking
- **📋 Public Message Boards** - Threaded discussions with moderation tools
- **🎨 ANSI Editor** - Built-in ANSI art editor for creative message composition
- **🔍 Message Search** - Full-text search with pagination support
- **🚫 User Blocking** - Block/unblock system for user privacy
- **📈 Message Quotas** - Configurable daily and monthly limits

### 📂 File Management
- **🗂️ File Areas** - Organized file libraries with permissions and quotas
- **📥 File Transfers** - Upload/download support with approval workflow
- **⭐ Rating System** - 5-star rating system with user comments
- **🏷️ File Tagging** - Tag-based organization and search
- **📊 Statistics** - Download tracking and usage analytics
- **🧹 Auto-cleanup** - Automatic removal of expired files

### 🎮 Door Game System
- **🚪 Door Registry** - Comprehensive door game management interface
- **💾 DOS Games Support** - DOSBox integration for classic BBS doors
- **📄 Drop File Standards** - Support for DOOR.SYS, DORINFO1.DEF formats
- **🔌 FOSSIL Emulation** - Serial port to telnet emulation for legacy compatibility
- **🎯 Access Controls** - User-level permissions and scheduling
- **📈 Game Statistics** - Session monitoring and usage statistics

### 🖥️ Administration
- **📊 Real-time Dashboard** - Live system statistics and monitoring
- **👨‍💼 User Management** - Comprehensive user administration tools
- **🔧 Configuration Manager** - Hot-reloadable YAML configuration system
- **📝 Log Viewer** - Built-in log analysis and monitoring
- **🛠️ Maintenance Tools** - Database optimization and cleanup utilities

## 🚀 Quick Start

### Requirements

- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** or later
- **Operating System**: Linux, Windows, or macOS
- **Terminal**: Any ANSI/VT100 compatible terminal or telnet client

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/blackboard.git
   cd blackboard
   ```

2. **Build the solution**
   ```bash
   dotnet build
   ```

3. **Run the BBS**
   ```bash
   dotnet run --project src/Blackboard
   ```

4. **Connect to your BBS**
   ```bash
   telnet localhost 2323
   ```

> **Note**: The system starts with the telnet server offline by default for security. Use the admin interface to enable it.

### Default Configuration

| Setting | Default Value | Description |
|---------|---------------|-------------|
| **Telnet Port** | 2323 | Non-privileged port (23 requires root) |
| **Database** | `data/blackboard.db` | SQLite database location |
| **Configuration** | `blackboard.yml` | Main configuration file |
| **Logs** | `logs/` | Log file directory |
| **Screens** | `screens/` | ANSI screen files |

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Telnet Client │────│  Blackboard      │────│ SQLite Database │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                       ┌────────┴────────┐
                       │                 │
                ┌──────▼──────┐   ┌──────▼──────┐
                │ Terminal UI │   │    YAML     │
                │  (Admin)    │   │   Config    │
                └─────────────┘   └─────────────┘
```

### Project Structure

```
Blackboard/
├── 📁 src/
│   ├── 🎯 Blackboard/              # Main application & Terminal.Gui interface
│   ├── 🧠 Blackboard.Core/         # Core business logic & services
│   └── 💾 Blackboard.Data/         # Data access layer & SQLite management
├── 🧪 tests/                       # Unit and integration tests
├── 📚 docs/                        # Documentation & guides
├── ⚙️  blackboard.yml              # Configuration file
└── 📄 README.md                    # This file
```

## 📚 Documentation

- **[🚪 Door System Guide](docs/DOOR_SYSTEM_GUIDE.md)** - Complete guide to setting up door games
- **[📋 Product Requirements](docs/PRD.md)** - Detailed feature specifications
- **[✅ Development Tasks](docs/TASKS.md)** - Current development roadmap

## 🛠️ Development

### Building from Source

```bash
# Clone and build
git clone https://github.com/yourusername/blackboard.git
cd blackboard
dotnet restore
dotnet build

# Run tests
dotnet test

# Run with hot reload
dotnet watch --project src/Blackboard
```

### Key Technologies

- **Framework**: .NET 8.0
- **UI**: Terminal.Gui
- **Database**: SQLite with Entity Framework
- **Logging**: Serilog
- **Configuration**: YamlDotNet
- **Security**: BCrypt.Net, JWT tokens
- **Testing**: xUnit, Moq

### Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please read our [Contributing Guidelines](CONTRIBUTING.md) for details on our code of conduct and development process.

## 🚧 Current Status

### ✅ Completed Features

**Phase 1: Core System**
- [x] .NET 8.0 project structure with modular design
- [x] Custom telnet server with ANSI/VT100 support
- [x] Terminal.Gui administration interface
- [x] SQLite database with comprehensive schema
- [x] YAML configuration system with hot-reload
- [x] Serilog structured logging

**Phase 2: User Management**
- [x] User registration and authentication
- [x] Profile management and preferences
- [x] Permission levels and access control
- [x] Password security with BCrypt
- [x] Session management and audit logging

**Phase 3: Administration**
- [x] Real-time dashboard with system statistics
- [x] Active sessions monitoring
- [x] Configuration management interface
- [x] User management tools
- [x] System alerts and notifications

**Phase 4: Messaging**
- [x] Private messaging with inbox/outbox
- [x] Public message boards with threading
- [x] ANSI editor integration
- [x] Message search and pagination
- [x] User blocking and preferences
- [x] Admin moderation tools

**Phase 5: File Management**
- [x] File areas with permissions
- [x] Upload/download with approval workflow
- [x] File search and tagging
- [x] Rating system with comments
- [x] Statistics and auto-cleanup

**Phase 6: Door Games**
- [x] Door registry and management
- [x] DOSBox integration for DOS games
- [x] Drop file support (DOOR.SYS, DORINFO1.DEF)
- [x] FOSSIL emulation for legacy compatibility
- [x] Access controls and scheduling

### 🚧 In Development

**Phase 7: Inter-BBS Networks**
- [ ] FidoNet protocol support
- [ ] QWK/REP packet processing
- [ ] Network configuration interface
- [ ] Message routing and import/export

**Phase 8: Enhanced UI**
- [ ] Custom ANSI art screens and menus
- [ ] Real-time updates and notifications
- [ ] Enhanced keyboard navigation
- [ ] Template system for dynamic content

## 🔒 Security

- System starts with telnet server offline by default
- Default telnet port 2323 (non-privileged)
- BCrypt password hashing with salt
- Comprehensive audit logging
- Session timeout controls
- Database WAL mode for better concurrency

## 📝 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by classic BBS systems: **Telegard**, **Mystic BBS**, and **Renegade**
- Built with modern .NET technologies
- Terminal.Gui for rich console interfaces
- The BBS community for keeping the spirit alive

## 🆘 Support

- **Issues**: [GitHub Issues](https://github.com/yourusername/blackboard/issues)
- **Discussions**: [GitHub Discussions](https://github.com/yourusername/blackboard/discussions)
- **Documentation**: [Project Wiki](https://github.com/yourusername/blackboard/wiki)

---

<div align="center">

**[⬆ Back to Top](#-blackboard-bbs)**

Made with ❤️ by the Blackboard community

</div>
