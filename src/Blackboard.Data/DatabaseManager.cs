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

            -- File Areas table (for Phase 5)
            CREATE TABLE IF NOT EXISTS FileAreas (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT UNIQUE NOT NULL,
                Description TEXT,
                Path TEXT NOT NULL,
                RequiredLevel INTEGER NOT NULL DEFAULT 0,
                UploadLevel INTEGER NOT NULL DEFAULT 10,
                IsActive INTEGER NOT NULL DEFAULT 1,
                MaxFileSize INTEGER NOT NULL DEFAULT 10485760, -- 10MB default
                AllowUploads INTEGER NOT NULL DEFAULT 1,
                AllowDownloads INTEGER NOT NULL DEFAULT 1,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            -- Files table (for Phase 5)
            CREATE TABLE IF NOT EXISTS Files (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AreaId INTEGER NOT NULL,
                FileName TEXT NOT NULL,
                OriginalFileName TEXT NOT NULL,
                Description TEXT,
                FilePath TEXT NOT NULL,
                Size INTEGER NOT NULL,
                Checksum TEXT NOT NULL,
                MimeType TEXT,
                Tags TEXT, -- JSON array of tags
                UploadDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                UploaderId INTEGER,
                DownloadCount INTEGER NOT NULL DEFAULT 0,
                LastDownloadAt DATETIME,
                IsApproved INTEGER NOT NULL DEFAULT 0,
                ApprovedBy INTEGER,
                ApprovedAt DATETIME,
                IsActive INTEGER NOT NULL DEFAULT 1,
                ExpiresAt DATETIME,
                FOREIGN KEY (AreaId) REFERENCES FileAreas(Id) ON DELETE CASCADE,
                FOREIGN KEY (UploaderId) REFERENCES Users(Id) ON DELETE SET NULL,
                FOREIGN KEY (ApprovedBy) REFERENCES Users(Id) ON DELETE SET NULL
            );

            -- File Ratings table (for Phase 5)
            CREATE TABLE IF NOT EXISTS FileRatings (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FileId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                Rating INTEGER NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
                Comment TEXT,
                RatingDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (FileId) REFERENCES Files(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                UNIQUE(FileId, UserId)
            );

            -- File Transfer tracking table (for Phase 5 - ZMODEM/XMODEM/YMODEM support)
            CREATE TABLE IF NOT EXISTS FileTransfers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL UNIQUE,
                UserId INTEGER NOT NULL,
                FileId INTEGER,
                Protocol TEXT NOT NULL,
                IsUpload INTEGER NOT NULL,
                FileName TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                BytesTransferred INTEGER NOT NULL DEFAULT 0,
                StartTime DATETIME NOT NULL,
                EndTime DATETIME,
                IsSuccessful INTEGER,
                ErrorMessage TEXT,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                FOREIGN KEY (FileId) REFERENCES Files(Id) ON DELETE SET NULL
            );

            -- Download token table for HTTP downloads
            CREATE TABLE IF NOT EXISTS DownloadTokens (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Token TEXT NOT NULL UNIQUE,
                FileId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ExpiresAt DATETIME NOT NULL,
                IsUsed INTEGER NOT NULL DEFAULT 0,
                UsedAt DATETIME,
                FOREIGN KEY (FileId) REFERENCES Files(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            -- Upload token table for HTTP uploads
            CREATE TABLE IF NOT EXISTS UploadTokens (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Token TEXT NOT NULL UNIQUE,
                AreaId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ExpiresAt DATETIME NOT NULL,
                IsUsed INTEGER NOT NULL DEFAULT 0,
                UsedAt DATETIME,
                ResultingFileId INTEGER,
                FOREIGN KEY (AreaId) REFERENCES FileAreas(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                FOREIGN KEY (ResultingFileId) REFERENCES Files(Id) ON DELETE SET NULL
            );

            -- Door Game System tables (for Phase 6)
            CREATE TABLE IF NOT EXISTS Doors (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Description TEXT,
                Category TEXT NOT NULL,
                ExecutablePath TEXT NOT NULL,
                CommandLine TEXT,
                WorkingDirectory TEXT,
                DropFileType TEXT NOT NULL DEFAULT 'DOOR.SYS',
                DropFileLocation TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                RequiresDosBox INTEGER NOT NULL DEFAULT 0,
                DosBoxConfigPath TEXT,
                SerialPort TEXT DEFAULT 'COM1',
                MemorySize INTEGER DEFAULT 16,
                MinimumLevel INTEGER DEFAULT 0,
                MaximumLevel INTEGER DEFAULT 255,
                TimeLimit INTEGER DEFAULT 60,
                DailyLimit INTEGER DEFAULT 5,
                Cost INTEGER DEFAULT 0,
                SchedulingEnabled INTEGER DEFAULT 0,
                AvailableHours TEXT,
                TimeZone TEXT DEFAULT 'UTC',
                MultiNodeEnabled INTEGER DEFAULT 0,
                MaxPlayers INTEGER DEFAULT 1,
                InterBbsEnabled INTEGER DEFAULT 0,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                CreatedBy INTEGER,
                FOREIGN KEY (CreatedBy) REFERENCES Users(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS DoorSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL UNIQUE,
                DoorId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                NodeNumber INTEGER,
                StartTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                EndTime DATETIME,
                ExitCode INTEGER,
                DropFilePath TEXT,
                WorkingDirectory TEXT,
                ProcessId INTEGER,
                Status TEXT NOT NULL DEFAULT 'starting',
                ErrorMessage TEXT,
                LastActivity DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS DoorConfigs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                ConfigKey TEXT NOT NULL,
                ConfigValue TEXT NOT NULL,
                ConfigType TEXT DEFAULT 'string',
                FOREIGN KEY (DoorId) REFERENCES Doors(Id) ON DELETE CASCADE,
                UNIQUE(DoorId, ConfigKey)
            );

            CREATE TABLE IF NOT EXISTS DoorPermissions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                UserId INTEGER,
                UserGroup TEXT,
                AccessType TEXT NOT NULL,
                GrantedBy INTEGER NOT NULL,
                GrantedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                ExpiresAt DATETIME,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                FOREIGN KEY (GrantedBy) REFERENCES Users(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS DoorStatistics (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                TotalSessions INTEGER DEFAULT 0,
                TotalTime INTEGER DEFAULT 0,
                LastPlayed DATETIME,
                HighScore INTEGER,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id) ON DELETE CASCADE,
                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                UNIQUE(DoorId, UserId)
            );

            CREATE TABLE IF NOT EXISTS DoorLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                SessionId INTEGER,
                LogLevel TEXT NOT NULL,
                Message TEXT NOT NULL,
                Details TEXT,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id) ON DELETE CASCADE,
                FOREIGN KEY (SessionId) REFERENCES DoorSessions(Id) ON DELETE SET NULL
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

            -- File area indexes
            CREATE INDEX IF NOT EXISTS idx_file_areas_name ON FileAreas(Name);
            CREATE INDEX IF NOT EXISTS idx_file_areas_active ON FileAreas(IsActive);
            CREATE INDEX IF NOT EXISTS idx_files_area_id ON Files(AreaId);
            CREATE INDEX IF NOT EXISTS idx_files_filename ON Files(FileName);
            CREATE INDEX IF NOT EXISTS idx_files_uploader ON Files(UploaderId);
            CREATE INDEX IF NOT EXISTS idx_files_upload_date ON Files(UploadDate);
            CREATE INDEX IF NOT EXISTS idx_files_approved ON Files(IsApproved);
            CREATE INDEX IF NOT EXISTS idx_files_active ON Files(IsActive);
            CREATE INDEX IF NOT EXISTS idx_file_ratings_file_id ON FileRatings(FileId);
            CREATE INDEX IF NOT EXISTS idx_file_ratings_user_id ON FileRatings(UserId);

            -- File transfer indexes
            CREATE INDEX IF NOT EXISTS idx_file_transfers_session_id ON FileTransfers(SessionId);
            CREATE INDEX IF NOT EXISTS idx_file_transfers_user_id ON FileTransfers(UserId);
            CREATE INDEX IF NOT EXISTS idx_file_transfers_start_time ON FileTransfers(StartTime);
            CREATE INDEX IF NOT EXISTS idx_download_tokens_token ON DownloadTokens(Token);
            CREATE INDEX IF NOT EXISTS idx_download_tokens_expires ON DownloadTokens(ExpiresAt);
            CREATE INDEX IF NOT EXISTS idx_upload_tokens_token ON UploadTokens(Token);
            CREATE INDEX IF NOT EXISTS idx_upload_tokens_expires ON UploadTokens(ExpiresAt);

            -- Door system indexes
            CREATE INDEX IF NOT EXISTS idx_doors_name ON Doors(Name);
            CREATE INDEX IF NOT EXISTS idx_doors_category ON Doors(Category);
            CREATE INDEX IF NOT EXISTS idx_doors_active ON Doors(IsActive);
            CREATE INDEX IF NOT EXISTS idx_door_sessions_door_id ON DoorSessions(DoorId);
            CREATE INDEX IF NOT EXISTS idx_door_sessions_user_id ON DoorSessions(UserId);
            CREATE INDEX IF NOT EXISTS idx_door_sessions_session_id ON DoorSessions(SessionId);
            CREATE INDEX IF NOT EXISTS idx_door_sessions_status ON DoorSessions(Status);
            CREATE INDEX IF NOT EXISTS idx_door_sessions_start_time ON DoorSessions(StartTime);
            CREATE INDEX IF NOT EXISTS idx_door_configs_door_id ON DoorConfigs(DoorId);
            CREATE INDEX IF NOT EXISTS idx_door_permissions_door_id ON DoorPermissions(DoorId);
            CREATE INDEX IF NOT EXISTS idx_door_permissions_user_id ON DoorPermissions(UserId);
            CREATE INDEX IF NOT EXISTS idx_door_statistics_door_id ON DoorStatistics(DoorId);
            CREATE INDEX IF NOT EXISTS idx_door_statistics_user_id ON DoorStatistics(UserId);
            CREATE INDEX IF NOT EXISTS idx_door_logs_door_id ON DoorLogs(DoorId);
            CREATE INDEX IF NOT EXISTS idx_door_logs_session_id ON DoorLogs(SessionId);
            CREATE INDEX IF NOT EXISTS idx_door_logs_timestamp ON DoorLogs(Timestamp);

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

            CREATE TRIGGER IF NOT EXISTS file_areas_updated_at 
                AFTER UPDATE ON FileAreas
                BEGIN
                    UPDATE FileAreas SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;

            CREATE TRIGGER IF NOT EXISTS doors_updated_at 
                AFTER UPDATE ON Doors
                BEGIN
                    UPDATE Doors SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
                END;

            CREATE TRIGGER IF NOT EXISTS door_statistics_updated_at 
                AFTER UPDATE ON DoorStatistics
                BEGIN
                    UPDATE DoorStatistics SET UpdatedAt = CURRENT_TIMESTAMP WHERE Id = NEW.Id;
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
