# Blackboard – Phased Task List

This checklist is based on the Product Requirements Document (PRD) and is organized by project phases. Each task can be checked off as progress is made.

---

## Phase 1: Core System Foundation
- [x] Set up .NET 8.0+ project structure
- [x] Implement custom Telnet server (System.Net.Sockets, ANSI/VT100 output)
- [x] Integrate Terminal.Gui for terminal-based UI
- [x] Establish SQLite database schema and connection
- [x] Implement YAML-based configuration system with hot-reload
- [x] Integrate Serilog for logging

---

## Phase 2: User Management & Security
- [x] Implement user registration and login (with pre-enter code option)
- [x] Develop user profile management (handle, location, stats, preferences)
- [x] Enforce permission levels and access control
- [x] Implement password security (BCrypt+salt, complexity, expiration, lockouts)
- [x] Add session management (tokens, timeouts, IP tracking)
- [x] Implement audit logging for admin actions

---

## Phase 3: Sysop/Admin Interface
- [x] Build main dashboard with real-time statistics (users, system status, activity)
- [x] Implement active sessions panel (user details, session management)
- [x] Add system alerts and notifications (errors, security, resources)
- [x] Develop configuration management screens (general, network, security)
- [x] Implement user management tools (list/search, editor, stats, bulk ops, audit log)

---

## Phase 4: Messaging System
- [x] Implement private messaging (E2EE, inbox/outbox, read/unread)
- [x] Develop public message boards (threaded, moderation, sticky, search)
- [x] Add system messages (announcements, notifications)
- [x] Integrate ANSI editor for message composition
- [x] Implement message search, pagination, and unread tracking
- [x] Add admin moderation tools (edit/delete, reporting, approval queue)
- [x] Enforce message quotas and preferences

---

## Phase 5: File Area Management
- [x] Implement file libraries and area management (create/edit, permissions)
- [x] Add file upload/download, tagging, and search
- [x] Implement file ratings and comments
- [x] Add batch operations and cleanup tools
- [x] Add database schema (FileAreas, Files, FileRatings tables)
- [x] Implement core service layer (IFileAreaService, FileAreaService)
- [x] Add user interface for file browsing and search
- [x] Implement admin interface for file area management
- [x] Add file approval workflow for admins
- [x] Integrate ZMODEM/XMODEM/YMODEM protocols
- [x] Implement file compression and archiving features

---

## Phase 6: Door Game System
- [x] Develop door registry and management interface
- [x] Support DOS/Win/Linux doors with DOSBox integration
- [x] Implement drop file standards (DOOR.SYS, DORINFO1.DEF)
- [x] Add access controls and scheduling for doors
- [x] Integrate monitoring and stats for door sessions
- [x] Implement maintenance tools (install wizard, compatibility test)
- [x] Implement serial port (FOSSIL) to telnet emulation for legacy door capability like NetFoss

---

## Phase 7: Visual & Usability Enhancements (BBS User Experience)
**Note: These enhancements are for users connecting to the BBS via telnet/terminal clients, not the admin interface**
- [ ] Design ANSI art screens and menus (classic BBS style)
- [ ] Implement color coding, real-time updates, and responsive layout
- [ ] Add keyboard navigation (hotkeys, tab, arrow keys, function keys)
- [ ] Add ability for ANSI screens to have template variables that are replaced at render time with system, user and connection information
- [ ] Integrate context menus and quick actions
- [ ] Ensure user input is echoed back so user sees real time key presses received by server
- [ ] Ensure that ANSI screen files are sent to connected user in a way that their terminal can display properly

---

## Phase 8: Inter-BBS Network Support
- [ ] Implement FidoNet protocol support (FTN, EchoMail, NetMail)
- [ ] Add QWK/REP and RIME/RelayNet support
- [ ] Develop network configuration and node management interface
- [ ] Integrate message routing, import/export, and error handling
- [ ] Add monitoring tools for network traffic and performance

---

## Phase 9: Administrative Tools & Maintenance
- [ ] Build log viewer (system, user, security, error logs)
- [ ] Implement backup and maintenance tools (DB backup, optimization, cleanup)
- [ ] Add import/export for user data, messages, and configs

---

## Phase 10: Security & Compliance
- [ ] Enforce E2EE for messages and files
- [ ] Implement key management and rotation
- [ ] Add rate limiting and lockout mechanisms
- [ ] Ensure all network transport is encrypted (SSH/TLS)
- [ ] Review and update dependencies for security

---

## Phase 11: Documentation & Licensing
- [ ] Write user and sysop documentation
- [ ] Document API, configuration, and deployment steps
- [ ] Ensure MIT license and references are included

---

This checklist can be used to track progress and mark tasks as complete throughout the project.
