# Door Game System - Operator Guide

## Quick Start

### Adding Your First Door Game

1. **Access Admin Interface**
   - Navigate to Admin → Door Management
   - Click "Add Door"

2. **Basic Configuration**
   ```
   Name: TradeWars 2002
   Description: Classic space trading game
   Category: Strategy
   Executable Path: /doors/tw2002/tw.exe
   Command Line: {dropfile}
   Drop File Type: DOOR.SYS
   ```

3. **Access Control**
   ```
   Minimum Level: 10 (verified users)
   Time Limit: 30 minutes
   Daily Limit: 3 sessions
   ```

4. **DOSBox Settings** (for DOS games)
   ```
   ☑ Requires DOSBox
   Memory Size: 16 MB
   Serial Port: COM1
   ```

### Supported Door Types

#### Classic DOS Games
- **TradeWars 2002**: Space trading strategy
- **Legend of the Red Dragon**: Fantasy RPG
- **Barren Realms Elite**: Military strategy
- **Usurper**: Horror/fantasy RPG
- **Operation Overkill**: Post-apocalyptic game

#### Modern Applications
- **BBS Games**: Native Linux/Windows doors
- **Custom Scripts**: Python, Node.js applications
- **Web Doors**: HTTP-based games with telnet bridge

### Drop File Variables

The system automatically substitutes these variables in drop files:

| Variable | Description | Example |
|----------|-------------|---------|
| `{USER_HANDLE}` | User's BBS handle | "CyberWarrior" |
| `{USER_REAL_NAME}` | Real name | "John Smith" |
| `{SECURITY_LEVEL}` | Access level | "50" |
| `{TIME_LEFT}` | Session time remaining | "45" |
| `{COM_PORT}` | Serial port | "COM1" |
| `{BAUD_RATE}` | Connection speed | "38400" |
| `{NODE_NUMBER}` | BBS node number | "1" |
| `{SESSION_ID}` | Unique session ID | "abc123..." |

### FOSSIL Emulation

For DOS doors requiring FOSSIL drivers:

1. **Automatic Setup**: The system creates named pipes for communication
2. **Serial Emulation**: Full COM port simulation with flow control
3. **Compatibility**: Works with most FOSSIL-dependent doors
4. **Debugging**: Built-in logging for troubleshooting

### Maintenance Tasks

#### Daily Operations
- **Session Cleanup**: Automatic removal of expired sessions
- **File Cleanup**: Orphaned drop file removal
- **Statistics Update**: Usage metrics calculation

#### Weekly Operations
- **Door Validation**: Check executable paths and configurations
- **Performance Review**: Analyze usage patterns and performance
- **Backup**: Door configurations and user statistics

### Troubleshooting

#### Common Issues

**Door Won't Start**
```
Check: Executable path exists
Check: Working directory accessible
Check: User has sufficient access level
Check: Daily limit not exceeded
```

**FOSSIL Communication Problems**
```
Check: Named pipe creation successful
Check: DOSBox serial configuration
Check: Telnet connection stability
Review: FOSSIL session logs
```

**Performance Issues**
```
Monitor: Active session count
Check: DOSBox memory allocation
Review: System resource usage
Optimize: Session time limits
```

### Configuration Examples

#### TradeWars 2002 Setup
```yaml
name: "TradeWars 2002"
category: "Strategy"
executable: "/doors/tw2002/tw.exe"
commandLine: "{dropfile}"
workingDirectory: "/doors/tw2002/"
dropFileType: "DOOR.SYS"
dosbox:
  enabled: true
  memory: 16
  serialPort: "COM1"
access:
  minimumLevel: 10
  timeLimit: 60
  dailyLimit: 3
```

#### Modern Python Door
```yaml
name: "Python Chat Bot"
category: "Utility"
executable: "/usr/bin/python3"
commandLine: "/doors/chatbot/main.py {dropfile}"
workingDirectory: "/doors/chatbot/"
dropFileType: "DOOR.SYS"
dosbox:
  enabled: false
access:
  minimumLevel: 1
  timeLimit: 15
  dailyLimit: 10
```

### Monitoring Dashboard

The admin interface provides real-time monitoring:

- **Active Sessions**: Current players and their doors
- **Usage Statistics**: Popular doors, session counts, total time
- **Performance Metrics**: Success rates, error counts, response times
- **System Health**: Resource usage, cleanup status, validation results

### Best Practices

1. **Test First**: Always validate door configuration before making it live
2. **Set Limits**: Use time and daily limits to prevent abuse
3. **Monitor Usage**: Regular review of statistics and performance
4. **Backup Configs**: Keep backups of working door configurations
5. **Update Regularly**: Keep DOSBox and system components updated
6. **Document Setup**: Maintain notes on door-specific configuration quirks

### Advanced Features

#### Scheduling
Set specific hours when doors are available:
```
Available Hours: 18:00-02:00
Time Zone: America/New_York
```

#### Multi-Node Gaming
Allow multiple concurrent players:
```
☑ Multi-Node Enabled
Max Players: 10
```

#### Cost System
Charge credits for door access:
```
Cost: 100 credits per session
```

#### Permissions
Fine-grained access control:
- User-specific allow/deny lists
- Group-based permissions
- Expiring access grants

### Support and Resources

For additional help:
- Check door logs for detailed error information
- Use the built-in validation tools
- Review the FOSSIL emulation logs for communication issues
- Consult the Phase 6 implementation documentation

The Door Game System is designed to be both powerful and user-friendly, supporting everything from classic DOS games to modern applications while maintaining the authentic BBS experience.
