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
- [ ] Implement private messaging (E2EE, inbox/outbox, read/unread)
- [ ] Develop public message boards (threaded, moderation, sticky, search)
- [ ] Add system messages (announcements, notifications)
- [ ] Integrate ANSI editor for message composition
- [ ] Implement message search, pagination, and unread tracking
- [ ] Add admin moderation tools (edit/delete, reporting, approval queue)
- [ ] Enforce message quotas and preferences

---

## Phase 5: File Area Management
- [ ] Implement file libraries and area management (create/edit, permissions)
- [ ] Add file upload/download, tagging, search, and virus scan
- [ ] Integrate ZMODEM/XMODEM/YMODEM and HTTP links
- [ ] Enforce file encryption (AES-256-GCM, per-user keys)
- [ ] Implement file ratings and comments
- [ ] Add batch operations and cleanup tools

---

## Phase 6: Door Game System
- [ ] Develop door registry and management interface
- [ ] Support DOS/Win/Linux doors with DOSBox integration
- [ ] Implement drop file standards (DOOR.SYS, DORINFO1.DEF)
- [ ] Add access controls and scheduling for doors
- [ ] Integrate monitoring and stats for door sessions
- [ ] Implement maintenance tools (install wizard, compatibility test)

---

## Phase 7: Inter-BBS Network Support
- [ ] Implement FidoNet protocol support (FTN, EchoMail, NetMail)
- [ ] Add QWK/REP and RIME/RelayNet support
- [ ] Develop network configuration and node management interface
- [ ] Integrate message routing, import/export, and error handling
- [ ] Add monitoring tools for network traffic and performance

---

## Phase 8: Visual & Usability Enhancements
- [ ] Design ANSI art screens and menus (classic BBS style)
- [ ] Implement color coding, real-time updates, and responsive layout
- [ ] Add keyboard navigation (hotkeys, tab, arrow keys, function keys)
- [ ] Integrate context menus and quick actions

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
