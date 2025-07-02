using Microsoft.Data.Sqlite;
using Dapper;
using Serilog;
using Blackboard.Data.Configuration;

namespace Blackboard.Data;

public class DatabaseManager : IDatabaseManager
{
    /// <summary>
    /// Executes a query and maps the result to a list of type T using Dapper.
    /// </summary>
    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");
        return await _connection.QueryAsync<T>(sql, param);
    }

    /// <summary>
    /// Executes a query and returns the first result using Dapper.
    /// </summary>
    public async Task<T> QueryFirstAsync<T>(string sql, object? param = null)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");
        return await _connection.QueryFirstAsync<T>(sql, param);
    }

    /// <summary>
    /// Executes a query and returns the first result or default using Dapper.
    /// </summary>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");
        return await _connection.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    /// <summary>
    /// Executes a command (INSERT, UPDATE, DELETE) using Dapper.
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");
        return await _connection.ExecuteAsync(sql, param);
    }

    private readonly ILogger _logger;
    private readonly IDatabaseConfiguration _config;
    private SqliteConnection? _connection;

    public DatabaseManager(ILogger logger, IDatabaseConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _connection = new SqliteConnection(_config.ConnectionString);
            
            if (_config.EnableWalMode)
            {
                var connectionStringBuilder = new SqliteConnectionStringBuilder(_config.ConnectionString)
                {
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Cache = SqliteCacheMode.Shared
                };
                _connection.ConnectionString = connectionStringBuilder.ToString();
            }

            await _connection.OpenAsync();
            
            // Enable WAL mode if configured
            if (_config.EnableWalMode)
            {
                await ExecuteNonQueryAsync("PRAGMA journal_mode=WAL;");
            }

            // Set other pragmas for performance
            await ExecuteNonQueryAsync("PRAGMA synchronous=NORMAL;");
            await ExecuteNonQueryAsync("PRAGMA foreign_keys=ON;");
            await ExecuteNonQueryAsync("PRAGMA temp_store=MEMORY;");

            await CreateTablesAsync();
            
            _logger.Information("Database initialized successfully at {ConnectionString}", _config.ConnectionString);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize database");
            throw;
        }
    }

    private async Task CreateTablesAsync()
    {
        var createTablesSql = @"
            -- Users table
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Handle TEXT UNIQUE NOT NULL,
                Email TEXT UNIQUE,
                PasswordHash TEXT NOT NULL,
                Salt TEXT NOT NULL,
                FirstName TEXT,
                LastName TEXT,
                Location TEXT,
                PhoneNumber TEXT,
                SecurityLevel INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1,
                LastLoginAt DATETIME,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PasswordExpiresAt DATETIME,
                FailedLoginAttempts INTEGER NOT NULL DEFAULT 0,
                LockedUntil DATETIME
            );

            -- User Sessions table
            CREATE TABLE IF NOT EXISTS UserSessions (
                Id TEXT PRIMARY KEY,
                UserId INTEGER NOT NULL,
                IpAddress TEXT NOT NULL,
                UserAgent TEXT,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ExpiresAt DATETIME NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            -- System Logs table
            CREATE TABLE IF NOT EXISTS SystemLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Level TEXT NOT NULL,
                Message TEXT NOT NULL,
                Exception TEXT,
                Properties TEXT,
                Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            -- Audit Logs table
            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER,
                Action TEXT NOT NULL,
                EntityType TEXT,
                EntityId TEXT,
                OldValues TEXT,
                NewValues TEXT,
                IpAddress TEXT,
                UserAgent TEXT,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
            );

            -- Configuration table for runtime settings
            CREATE TABLE IF NOT EXISTS RuntimeConfiguration (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                DataType TEXT NOT NULL DEFAULT 'string',
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedBy INTEGER,
                FOREIGN KEY (UpdatedBy) REFERENCES Users(Id) ON DELETE SET NULL
            );

            -- Messages table (for Phase 4)
            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FromUserId INTEGER,
                ToUserId INTEGER,
                Subject TEXT NOT NULL,
                Body TEXT NOT NULL,
                IsRead INTEGER NOT NULL DEFAULT 0,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                MessageType TEXT NOT NULL DEFAULT 'private',
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ReadAt DATETIME,
                FOREIGN KEY (FromUserId) REFERENCES Users(Id) ON DELETE SET NULL,
                FOREIGN KEY (ToUserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            -- Create indexes for performance
            CREATE INDEX IF NOT EXISTS idx_users_handle ON Users(Handle);
            CREATE INDEX IF NOT EXISTS idx_users_email ON Users(Email);
            CREATE INDEX IF NOT EXISTS idx_users_last_login ON Users(LastLoginAt);
            CREATE INDEX IF NOT EXISTS idx_user_sessions_user_id ON UserSessions(UserId);
            CREATE INDEX IF NOT EXISTS idx_user_sessions_expires ON UserSessions(ExpiresAt);
            CREATE INDEX IF NOT EXISTS idx_system_logs_timestamp ON SystemLogs(Timestamp);
            CREATE INDEX IF NOT EXISTS idx_audit_logs_user_id ON AuditLogs(UserId);
            CREATE INDEX IF NOT EXISTS idx_audit_logs_timestamp ON AuditLogs(CreatedAt);
            CREATE INDEX IF NOT EXISTS idx_messages_to_user ON Messages(ToUserId);
            CREATE INDEX IF NOT EXISTS idx_messages_from_user ON Messages(FromUserId);
            CREATE INDEX IF NOT EXISTS idx_messages_created ON Messages(CreatedAt);

            -- Create triggers for UpdatedAt
            CREATE TRIGGER IF NOT EXISTS users_updated_at 
                AFTER UPDATE ON Users
                BEGIN
                    UPDATE Users SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;

            CREATE TRIGGER IF NOT EXISTS runtime_config_updated_at 
                AFTER UPDATE ON RuntimeConfiguration
                BEGIN
                    UPDATE RuntimeConfiguration SET UpdatedAt = CURRENT_TIMESTAMP WHERE Key = NEW.Key;
                END;
        ";

        await ExecuteNonQueryAsync(createTablesSql);
        _logger.Information("Database tables created/verified successfully");
    }

    public Task<SqliteCommand> CreateCommandAsync(string sql)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = _config.ConnectionTimeoutSeconds;
        
        return Task.FromResult(command);
    }

    public async Task<int> ExecuteNonQueryAsync(string sql, params SqliteParameter[] parameters)
    {
        using var command = await CreateCommandAsync(sql);
        
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ExecuteScalarAsync(string sql, params SqliteParameter[] parameters)
    {
        using var command = await CreateCommandAsync(sql);
        
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }

        return await command.ExecuteScalarAsync();
    }

    public async Task<SqliteDataReader> ExecuteReaderAsync(string sql, params SqliteParameter[] parameters)
    {
        var command = await CreateCommandAsync(sql);
        
        if (parameters != null)
        {
            command.Parameters.AddRange(parameters);
        }

        return await command.ExecuteReaderAsync();
    }

    public async Task BackupDatabaseAsync(string backupPath)
    {
        if (_connection == null)
            throw new InvalidOperationException("Database not initialized");

        try
        {
            // Ensure backup directory exists
            var directory = Path.GetDirectoryName(backupPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create backup connection
            using var backupConnection = new SqliteConnection($"Data Source={backupPath}");
            await backupConnection.OpenAsync();
            
            // Perform backup
            _connection.BackupDatabase(backupConnection);
            
            _logger.Information("Database backed up to {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to backup database to {BackupPath}", backupPath);
            throw;
        }
    }

    public async Task CloseAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
            _logger.Information("Database connection closed");
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
