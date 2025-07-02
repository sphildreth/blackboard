
# Blackboard - Product Requirements Document (PRD)

---

## 1. Overview

**Blackboard** is a terminal-based BBS application written in C# that listens for inbound telnet connections (not serial port). It is inspired by classic BBS systems such as Telegard, Mystic, Renegade, Impulse, and Wildcat. Blackboard aims to provide a modern, robust, and feature-rich BBS experience with a classic ANSI aesthetic.

---

## 2. Feature Summary

- **User Registration:**
  - Users can register for new accounts. If the board is locked, a pre-enter code is required for sign-up.
- **Sysop/Admin:**
  - Sysop user can edit users and system settings.
- **Configuration System:**
  - Key-pair configuration system stored in YAML format.
  - Sysop can manage configurations from the main screen.
  - Configuration changes take effect immediately (hot-reload).
- **ANSI Menus:**
  - ANSI menus for navigation (messages, doors, files, etc.).
- **Messaging Platform:**
  - Robust in-app messaging (no external email/notifications).
- **Inter-BBS Networks:**
  - Supports FidoNet, QWK, and custom protocols for inter-BBS messaging.
- **DOS Door Game Support:**
  - Run DOS BBS doors via DOSBox, including serial port emulation.
  - Initial support for DOOR.SYS and DORINFO.DEF drop files.
- **Sysop Dashboard:**
  - Main screen displays call statistics and system status.

---


## 3. Application User Interface

When running, the terminal UI displays system statistics. This is the primary interface for the Sysop to configure and manage the system, including toggling system options (e.g., online/offline).

---


## 4. Sysop Interface Requirements

### 4.1 Main Dashboard (Terminal.Gui)

#### Real-Time Statistics Panel
- **Current Users Online:** Live count, list of connected users (handle, location, activity)
- **System Status:** Online/offline toggle, uptime, system load
- **Today's Activity:** Calls, registrations, messages, files, peak users
- **Historical Stats:** Total calls, users, messages, files, first online date

#### Active Sessions Panel
- **User Session Details:** Handle, real name, location, login time, activity, connection info, last activity
- **Session Management:** Drop users, send system messages, monitor activity, view user screen

#### System Alerts & Notifications
- **Error Log Summary:** Recent errors/warnings
- **Security Alerts:** Failed logins, suspicious activity
- **System Resources:** Memory, disk, CPU
- **Database Status:** Connection, backup, maintenance

---


### 4.2 Configuration Management Interface

#### System Configuration Screens
- **General:** BBS name, sysop, location, max users, new user level, online/offline, maintenance mode
- **Network:** Telnet port, timeouts, max connections, IP controls
- **Security:** Password policy, lockout, session timeout, encryption

#### User Management
- **User List/Search:** Paginated, filterable
- **User Editor:** Edit profiles, access, restrictions
- **User Stats:** Call/message/file history
- **Bulk Ops:** Mass level changes, purge inactive
- **Audit Log:** Track admin changes

#### Message System Admin
- **Board Management:** Create/edit/delete areas, permissions
- **Moderation:** Flagged messages, content review
- **System Messages:** Announcements
- **Stats:** Area activity, popular topics, participation

#### File Area Admin
- **Area Management:** Create/edit, permissions
- **Maintenance:** Orphan cleanup, integrity, virus scan
- **Upload Queue:** Approve uploads
- **Stats:** Popular downloads, activity, storage

#### Inter-BBS Network Admin
- **FidoNet Config:** Node setup, addressing, credentials
- **Echo Area:** Subscribe/unsubscribe, linking
- **Monitoring:** Status, message flow, packets
- **Routing:** Gateways between local/network
- **Stats:** Traffic, errors, performance

---


### 4.3 Administrative Tools

#### Log Viewer
- **System Logs:** Real-time/historical, filterable
- **User Activity Logs:** Per-user action history
- **Security Logs:** Auth events, failed attempts, admin actions
- **Error Logs:** Errors, warnings, diagnostics

#### Backup & Maintenance
- **Database Backup:** Manual/scheduled
- **System Maintenance:** DB optimization, log rotation, cleanup
- **Import/Export:** User data, message archives, config backups

#### Door Game Management
- **Door Registry:** Create/edit/delete door definitions
- **Settings:** Name, description, category, executable, working dir, env vars, time/player limits, schedule
- **Drop File Config:** Type, template, location, cleanup
- **DOSBox Integration:** Path, config, drive mapping, serial, memory, video, sound
- **Access Controls:** User level/group, allow/deny lists
- **Game Settings:** Tournaments, high scores, backup
- **Monitoring:** Active sessions, usage stats, performance, logs
- **Maintenance Tools:** Install wizard, drop file test, game data mgmt, compatibility test

---


## 5. Interface Design

### 5.1 Navigation Structure
- **Main Menu:** Dashboard, Users, Messages, Files, Networks, Config, Tools, Logs
- **Quick Actions:** Hotkeys (F1-F12)
- **Status Bar:** Time, system status, user count, pending tasks
- **Context Menus:** Right-click actions

### 5.2 Visual Design
- **Color Coding:** Status types (online/offline, errors, warnings)
- **Real-Time Updates:** Auto-refresh (5-30s, configurable)
- **Responsive Layout:** Adapts to terminal size
- **ANSI Compatibility:** Classic BBS look with modern controls

### 5.3 Keyboard Navigation
- **Hotkeys:** Single-key shortcuts
- **Tab Navigation:** Tab/Shift-Tab between controls
- **Arrow Keys:** List/menu navigation
- **Function Keys:** F1=Help, F2=Save, F3=Search, F10=Menu, etc.

---


## 6. Integration Requirements
- **Config Hot-Reload:** Immediate effect, no restart
- **Database:** Real-time stats/user data
- **Logging:** Serilog integration
- **Network:** Monitor telnet server status/metrics

---


## 7. Technical Requirements
- **Target Framework:** .NET 8.0+
- **Database:** SQLite
- **UI:** Terminal.Gui
- **Logging:** Serilog
- **YAML:** YamlDotNet
- **Concurrent Users:** 50+ supported
- **Security:** Encrypted passwords, session timeouts
- **Network:** Custom telnet server (System.Net.Sockets, no 3rd party), direct ANSI/VT100 output
- **Deployment:** Cross-platform (Windows/Linux)

---


## 8. ANSI Art & Screen Design

- **Style:** All screens/menus/artwork in the style of classic ANSI groups (ACiD, iCE)
- **Consistency:** Consistent color, shading, and layout (1990s BBS art)
- **Authenticity:** Use real ANSI art tools (PabloDraw, Moebius, ACiDDraw)
- **Branding:** Custom ANSI logos/headers for "Blackboard"
- **Review:** All screens reviewed for guideline adherence

> **References:**
> - [ACiD Artpacks Archive](https://16colo.rs/group/acid/)
> - [iCE Artpacks Archive](https://16colo.rs/group/ice/)

---



## 9. Messaging System Architecture

### 9.1 Message Types
1. **Private Messages (PM):** Direct user-to-user, inbox/outbox, read/unread, delete/archive, **E2EE**
2. **Public Boards:** Topic-based, threaded, moderation, sticky/pinned, stored plaintext
3. **System Messages:** Announcements, welcomes, notifications

### 9.2 Features
- **Composition:** ANSI editor, signatures, length limits, quote/reply
- **Display:** ANSI color, threading, timestamps, user profiles
- **Search/Nav:** Search by author/subject/content/date, pagination, new/unread tracking

### 9.3 User Message Management
- **Inbox:** Sort, folders, bulk ops, quota
- **Preferences:** Notifications, signature, block/ignore

### 9.4 Admin Features
- **Moderation:** Edit/delete, reporting, suspension, approval queue
- **Board Mgmt:** Create/edit/delete, permissions, retention, export/backup

### 9.5 Database Schema
```sql
-- Core message tables
Messages (MessageId, BoardId, UserId, Subject, Content, CreateDate, ModifyDate, IsDeleted)
MessageBoards (BoardId, Name, Description, IsPublic, RequiredAccess, RetentionDays)
PrivateMessages (PMId, FromUserId, ToUserId, Subject, Content, SentDate, ReadDate, IsDeleted)
MessageReplies (ReplyId, ParentMessageId, UserId, Content, CreateDate)

-- User message tracking
UserMessageStatus (UserId, MessageId, IsRead, IsFlagged, LastViewDate)
MessageReports (ReportId, MessageId, ReporterId, Reason, ReportDate, Status)
```

### 9.6 Storage & Security
- **Encoding:** UTF-8, preserve ANSI
- **Max Size:** 32KB (configurable)
- **Compression:** Archive messages auto-compressed
- **Access Control:** Board-level permissions, E2EE for PMs, audit trail
- **Filtering:** Word filter, spam detection, rate limiting

### 9.7 Performance
- **Caching:** Recent messages in memory
- **Lazy Loading:** For content/replies
- **Indexing:** On UserId, BoardId, CreateDate
- **Scalability:** Archive, pagination, background cleanup

---


## 10. Licensing

Blackboard is MIT licensed.

---


## 11. System Architecture

### 11.1 Core Components
1. **Telnet Server Engine:** TCP listener, session mgmt, ANSI/VT100, timeouts
2. **User Management:** Auth, session tracking, permission levels, lockout, logging
3. **Menu/Nav System:** ANSI menus, input, nav stack, YAML config
4. **Config Mgmt:** YAML files, hot-reload, validation, templates
5. **Inter-BBS Engine:** FidoNet, QWK/REP, routing, session mgmt

### 11.2 Networking
- **Port:** Default 23 (configurable)
- **Connections:** Async TCP
- **Session Isolation:** Per-user
- **Protocol:** Telnet + ANSI
- **Security:** SSH (future)

### 11.3 Data Flow
```
[Telnet Client] → [TCP Server] → [Session Manager] → [Menu System] → [Core Services]
                                      ↓
[Database Layer] ← [Business Logic] ← [User Interface]
```

---


## 12. User Management & Security

### 12.1 User Account System
- **Registration:** New user app, approval, pre-enter code, TOS acceptance
- **Profiles:** Handle, location, stats, preferences, signature, notes
- **Permission Levels:**
  - 10: New User (limited)
  - 20: Validated (full message)
  - 30: Trusted (file uploads)
  - 50: Co-Sysop (user mgmt)
  - 100: Sysop (full access)

### 12.2 Security Implementation
- **Password:** BCrypt+salt, complexity, expiration, lockouts
- **Session:** Tokens, timeouts, auto-logout, IP tracking
- **Access Control:** Function-level, time/geography, concurrent login limits

### 12.3 Security Requirements
- **Key Mgmt:** Secure RNG, OS/hardware storage, no plaintext, rotation, encrypted backups
- **E2EE:** Only endpoints decrypt PMs/files, server can't read, public messages plaintext for search/moderation
- **Forward Secrecy:** Ephemeral keys (e.g., Diffie-Hellman)
- **Admin Access:** No access to user keys/messages unless authorized, all admin actions logged
- **Transport:** All network encrypted, telnet via SSH/TLS, no plaintext credentials
- **Integrity:** Authenticated encryption (AES-GCM), digital signatures for critical data
- **Logging:** No sensitive data in logs, all security events logged
- **Rate Limiting:** For auth, messages, files; lockout on repeated failures
- **Dependency Security:** Keep libs up-to-date
- **Key Recovery:** Secure process or permanent loss; backups encrypted

---


## 13. File System Architecture

### 13.1 File Areas
- **Libraries:** Categorized (Games, Utilities, Docs, etc.), descriptions, metadata, ratios, integrity (CRC/MD5)
- **Management:** Batch upload/download, tagging, search, virus scan, expiration/cleanup
- **Protocols:** ZMODEM (primary), X/YMODEM fallback, HTTP links, resume support

### 13.2 Database Schema
```sql
FileAreas (AreaId, Name, Description, Path, RequiredLevel, UploadLevel)
Files (FileId, AreaId, FileName, Description, Size, UploadDate, UploaderId, DownloadCount)
FileRatings (FileId, UserId, Rating, Comment, RatingDate)
```

### 13.3 Encryption
- **E2EE:** AES-256-GCM for all user messages/files
- **Per-User Keys:** Unique, never shared; compromise doesn't affect others
- **Key Mgmt:** Secure gen/storage/rotation, no plaintext, hardware/OS protection
- **Forward Secrecy:** Session/message keys
- **Access Control:** Only intended users/processes can decrypt; admin can't bypass

---


## 14. Door Game System Architecture

### 14.1 Framework
- **Registry:** Central DB for door definitions; supports DOS/Win/Linux doors
- **Supported Types:** Classic DOS (TradeWars, L.O.R.D., etc.), DOSBox emulation
- **Drop File Standards:** DOOR.SYS (primary), DORINFO1.DEF

### 14.2 Door Config Schema
```yaml
doors:
  - id: "tradewars2002"
    name: "TradeWars 2002"
    description: "Classic space trading game"
    category: "Strategy"
    executable: "/doors/tw2002/tw.exe"
    workingDirectory: "/doors/tw2002/"
    commandLine: "{dropfile}"
    dropFileType: "DOOR.SYS"
    dropFileLocation: "{workingDirectory}/door.sys"
    dosbox:
      enabled: true
      configFile: "/doors/tw2002/dosbox.conf"
      serialPort: "COM1"
      memorySize: "16MB"
    access:
      minimumLevel: 20
      maximumLevel: 100
      allowedUsers: []
      blockedUsers: []
      timeLimit: 60
      dailyLimit: 2
      cost: 0
    schedule:
      enabled: true
      availableHours: "06:00-23:00"
      timeZone: "America/New_York"
    multiNode:
      enabled: true
      maxPlayers: 10
      ibbsEnabled: false
```

### 14.3 Security & Isolation
- **Sandboxing:** Isolated process, resource limits, network/firewall, virtual dirs
- **User Data:** Minimal info in drop files, temp cleanup, save isolation, audit trail

### 14.4 DOSBox Integration
- **Emulation:** Auto-setup, serial redir, CGA/EGA/VGA, sound
- **Performance:** CPU/mem/disk tuning, parallel sessions

### 14.5 Inter-BBS Gaming
- **Network:** Leagues, sync, stats, migration
- **Data Exchange:** Secure transfer, validation, conflict resolution, backup

### 14.6 Database Schema
```sql
-- Door definitions/config
Doors (DoorId, Name, Description, Category, ExecutablePath, CommandLine, WorkingDirectory)
DoorConfigs (ConfigId, DoorId, ConfigKey, ConfigValue, ConfigType)
DoorDropFiles (DropFileId, DoorId, FileType, Template, OutputPath)

-- User access/permissions
DoorAccess (AccessId, DoorId, UserId, AccessType, GrantDate, ExpiryDate)
DoorPermissions (PermissionId, DoorId, MinLevel, MaxLevel, TimeLimit, DailyLimit, Cost)

-- Usage/stats
DoorSessions (SessionId, DoorId, UserId, StartTime, EndTime, Duration, ExitCode)
DoorStatistics (StatId, DoorId, UserId, TotalSessions, TotalTime, LastPlayed, HighScore)
DoorLogs (LogId, SessionId, LogLevel, Message, Timestamp)

-- Game data/high scores
DoorGameData (GameDataId, DoorId, UserId, DataKey, DataValue, LastUpdated)
DoorHighScores (ScoreId, DoorId, UserId, Score, AchievedDate, Description)
```

---

## 15. Inter-BBS Network Support

### 15.1 FidoNet Integration
- **FTN Protocol:** Full compliance, addressing, packet exchange, compatible with BinkleyTerm, FrontDoor, InterMail
- **Processing:** Import/export .PKT, bundle handling, routing, duplicate detection
- **EchoMail:** Subscribe, gate, moderate, manage areas
- **NetMail:** Direct, priorities, file attach, receipts

### 15.2 Other Networks
- **QWK:** Packet gen/processing, REP import, compression, conference sync
- **RIME:** RelayNet support, hub/node config, routing
- **Custom:** Extensible, plugin support, format translation

### 15.3 Network Config Interface
- **Node Setup:** Address, system/sysop info, capabilities, session password
- **Mailer Config:** Poll/routing, connection methods, archive/compression, event scheduling
- **Echo Area:** Browse, subscribe, link, retention, moderation, export
- **Transport:** External mailer, built-in BinkP, logging, retry, error handling, scheduled/batch processing

### 15.4 Database Schema
```sql
-- FidoNet/network config
FidoNetNodes (NodeId, Zone, Net, Node, Point, Domain, SystemName, SysopName, Location)
EchoAreas (EchoId, EchoTag, Description, NetworkType, LastMessage, MessageCount, IsActive)
NetworkAddresses (AddressId, NetworkType, Address, SystemName, Capabilities, LastSeen)

-- Routing/tracking
MessageRouting (RouteId, MessageId, FromAddress, ToAddress, RoutingPath, ProcessDate)
NetworkMessages (NetMessageId, EchoId, MessageId, OriginalAddress, ImportDate, ExportDate)
MessageKludges (KludgeId, MessageId, KludgeType, KludgeData, ProcessDate)

-- Stats/monitoring
NetworkStats (StatId, NetworkType, Date, MessagesImported, MessagesExported, BytesTransferred)
NetworkSessions (SessionId, RemoteAddress, SessionType, StartTime, EndTime, PacketsProcessed)
NetworkErrors (ErrorId, NetworkType, ErrorMessage, ErrorDate, Resolution)
```

### 15.5 Message Format & Security
- **Formats:** FTS-0001, kludge lines, origin/tear/via lines
- **Packets:** Type 2+, 2.2, legacy
- **Compression:** ZIP, ARJ, LZH, RAR, auto-detect, password, multi-volume
- **Validation:** Node list, fake detection, approval, point auth
- **Security:** Digital signatures, NetMail encryption, spam/dup filtering, content validation
- **Session:** Password auth, secure transfer, logging, intrusion detection

### 15.6 Monitoring & Tools
- **Traffic:** Volume, bandwidth, peak, error rates
- **Performance:** Speed, queue depth, storage, connection success
- **Sysop Tools:** Status dashboard, import/export, queue, errors, echo admin, stats, flow, maintenance

---

