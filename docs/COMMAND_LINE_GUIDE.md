# Blackboard Command Line Arguments

Blackboard supports various command-line arguments to customize its behavior.

## Usage

```bash
dotnet run -- [options]
# or after building:
./Blackboard [options]
```

## Available Options

### Basic Options

| Option | Short | Description | Example |
|--------|--------|-------------|---------|
| `--console` | `-c` | Run in console mode without Terminal.Gui interface | `--console` |
| `--version` |  | Display version information and exit | `--version` |
| `--help` |  | Show help message | `--help` |

### Configuration Options

| Option | Short | Description | Example |
|--------|--------|-------------|---------|
| `--config` |  | Specify a custom configuration file path | `--config /path/to/config.yml` |
| `--verbose` | `-v` | Enable verbose logging | `--verbose` |

### Server Options

| Option | Short | Description | Example |
|--------|--------|-------------|---------|
| `--port` | `-p` | Override the telnet server port | `--port 2323` |
| `--no-server` |  | Do not start the telnet server automatically | `--no-server` |

## Examples

### Run in console mode on a custom port
```bash
dotnet run -- --console --port 2323
```

### Run with custom configuration and verbose logging
```bash
dotnet run -- --config /etc/blackboard/custom.yml --verbose
```

### Start without auto-starting the telnet server
```bash
dotnet run -- --no-server
```

### Console mode for server deployment
```bash
dotnet run -- --console --verbose --port 23
```

## Console Mode

When running with `--console` flag, Blackboard will:
- Initialize all services (database, telnet server, etc.)
- Start the telnet server (if not disabled with `--no-server`)
- Run in headless mode without the Terminal.Gui interface
- Accept Ctrl+C for graceful shutdown
- Log all activity to the configured log output

This mode is ideal for:
- Production server deployments
- Docker containers
- Running as a system service
- Automated testing and CI/CD

## Future Command-Line Options

The command-line parser is designed to be extensible. Future options may include:
- `--backup` - Perform database backup and exit
- `--maintenance` - Run in maintenance mode
- `--import-users` - Import users from a file
- `--export-data` - Export system data
- `--validate-config` - Validate configuration and exit
