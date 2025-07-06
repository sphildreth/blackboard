using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public class FileTransferService : IFileTransferService
{
    private readonly ConcurrentDictionary<string, FileTransferSession> _activeSessions;
    private readonly IDatabaseManager _databaseManager;
    private readonly IFileAreaService _fileAreaService;
    private readonly ILogger _logger;
    private readonly int _maxConcurrentTransfers;
    private readonly string _tempTransferPath;

    public FileTransferService(IDatabaseManager databaseManager, ILogger logger,
        IFileAreaService fileAreaService, string tempTransferPath = "temp/transfers",
        int maxConcurrentTransfers = 10)
    {
        _databaseManager = databaseManager;
        _logger = logger;
        _fileAreaService = fileAreaService;
        _activeSessions = new ConcurrentDictionary<string, FileTransferSession>();
        _tempTransferPath = tempTransferPath;
        _maxConcurrentTransfers = maxConcurrentTransfers;

        // Ensure temp directory exists
        if (!Directory.Exists(_tempTransferPath)) Directory.CreateDirectory(_tempTransferPath);
    }

    #region Transfer Session Management

    public async Task<FileTransferSession> StartDownloadSessionAsync(int fileId, int userId, FileTransferProtocol protocol)
    {
        var file = await _fileAreaService.GetFileAsync(fileId);
        if (file == null) throw new ArgumentException("File not found", nameof(fileId));

        if (!await _fileAreaService.CanUserAccessAreaAsync(userId, file.AreaId, false)) throw new UnauthorizedAccessException("User does not have download permission for this area");

        if (!await CanUserStartTransferAsync(userId)) throw new InvalidOperationException("User has reached maximum concurrent transfer limit");

        var session = new FileTransferSession
        {
            UserId = userId,
            FileId = fileId,
            Protocol = protocol,
            IsUpload = false,
            FileName = file.OriginalFileName,
            FileSize = file.Size
        };

        _activeSessions[session.SessionId] = session;

        _logger.Information("Started download session {SessionId} for file {FileId} by user {UserId} using {Protocol}",
            session.SessionId, fileId, userId, protocol);

        return session;
    }

    public async Task<FileTransferSession> StartUploadSessionAsync(int areaId, string fileName, long fileSize, int userId, FileTransferProtocol protocol)
    {
        if (!await _fileAreaService.CanUserAccessAreaAsync(userId, areaId, true)) throw new UnauthorizedAccessException("User does not have upload permission for this area");

        if (!await CanUserStartTransferAsync(userId)) throw new InvalidOperationException("User has reached maximum concurrent transfer limit");

        var area = await _fileAreaService.GetFileAreaAsync(areaId);
        if (area == null) throw new ArgumentException("File area not found", nameof(areaId));

        if (fileSize > area.MaxFileSize) throw new ArgumentException($"File size exceeds maximum allowed size of {FormatFileSize(area.MaxFileSize)}");

        var session = new FileTransferSession
        {
            UserId = userId,
            FileId = 0, // Will be set after upload completes
            Protocol = protocol,
            IsUpload = true,
            FileName = fileName,
            FileSize = fileSize
        };

        _activeSessions[session.SessionId] = session;

        _logger.Information("Started upload session {SessionId} for file {FileName} to area {AreaId} by user {UserId} using {Protocol}",
            session.SessionId, fileName, areaId, userId, protocol);

        return session;
    }

    public async Task<FileTransferSession?> GetSessionAsync(string sessionId)
    {
        _activeSessions.TryGetValue(sessionId, out var session);
        return await Task.FromResult(session);
    }

    public async Task<bool> CompleteSessionAsync(string sessionId, bool successful, string? errorMessage = null)
    {
        if (!_activeSessions.TryRemove(sessionId, out var session)) return false;

        session.IsCompleted = true;
        session.IsSuccessful = successful;
        session.ErrorMessage = errorMessage;
        session.EndTime = DateTime.UtcNow;

        // Log transfer completion
        var duration = session.EndTime.Value - session.StartTime;
        var transferRate = session.BytesTransferred / duration.TotalSeconds;

        _logger.Information("Transfer session {SessionId} completed. Success: {Successful}, " +
                            "Bytes: {BytesTransferred}/{FileSize}, Duration: {Duration:mm\\:ss}, Rate: {Rate:F1} bytes/sec",
            sessionId, successful, session.BytesTransferred, session.FileSize, duration, transferRate);

        // Record transfer in database for statistics
        const string sql = @"
            INSERT INTO FileTransfers (SessionId, UserId, FileId, Protocol, IsUpload, 
                                     FileName, FileSize, BytesTransferred, StartTime, EndTime, 
                                     IsSuccessful, ErrorMessage)
            VALUES (@SessionId, @UserId, @FileId, @Protocol, @IsUpload, 
                   @FileName, @FileSize, @BytesTransferred, @StartTime, @EndTime, 
                   @IsSuccessful, @ErrorMessage)";

        try
        {
            await _databaseManager.ExecuteAsync(sql, new
            {
                SessionId = sessionId,
                session.UserId,
                session.FileId,
                Protocol = session.Protocol.ToString(),
                session.IsUpload,
                session.FileName,
                session.FileSize,
                session.BytesTransferred,
                session.StartTime,
                session.EndTime,
                session.IsSuccessful,
                session.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to record transfer session {SessionId} in database", sessionId);
        }

        return true;
    }

    public Task<bool> UpdateProgressAsync(string sessionId, long bytesTransferred)
    {
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            session.BytesTransferred = bytesTransferred;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    #endregion

    #region Protocol Support

    public async Task<bool> IsProtocolSupportedAsync(FileTransferProtocol protocol)
    {
        // For now, all protocols are "supported" but ZMODEM/XMODEM/YMODEM are placeholders
        return await Task.FromResult(true);
    }

    public async Task<string> GetProtocolInstructionsAsync(FileTransferProtocol protocol, bool isUpload)
    {
        var action = isUpload ? "upload" : "download";

        var instructions = protocol switch
        {
            FileTransferProtocol.ZMODEM => $"Prepare your terminal for ZMODEM {action}. The transfer will begin automatically.",
            FileTransferProtocol.XMODEM => $"Prepare your terminal for XMODEM {action}. Use XMODEM-CRC if available.",
            FileTransferProtocol.YMODEM => $"Prepare your terminal for YMODEM {action}. YMODEM supports batch transfers.",
            FileTransferProtocol.HTTP => $"Use the provided URL for HTTP {action}. Modern web browsers are supported.",
            _ => "Unknown protocol"
        };

        return await Task.FromResult(instructions);
    }

    #endregion

    #region ZMODEM Protocol

    public async Task<byte[]> GenerateZmodemInitCommandAsync(FileTransferSession session)
    {
        // ZMODEM initialization sequence - this is a simplified implementation
        // In a real implementation, you would generate proper ZMODEM headers

        var command = session.IsUpload
            ? "rz\r\n" // Receive ZMODEM
            : $"sz \"{session.FileName}\"\r\n"; // Send ZMODEM

        return await Task.FromResult(Encoding.ASCII.GetBytes(command));
    }

    public async Task<bool> ProcessZmodemDataAsync(string sessionId, byte[] data)
    {
        // Placeholder for ZMODEM data processing
        // In a real implementation, you would parse ZMODEM packets and handle the protocol

        if (!_activeSessions.TryGetValue(sessionId, out var session)) return false;

        // Update progress (simplified)
        session.BytesTransferred += data.Length;

        _logger.Debug("Processed {ByteCount} bytes for ZMODEM session {SessionId}", data.Length, sessionId);

        return await Task.FromResult(true);
    }

    #endregion

    #region XMODEM/YMODEM Protocol

    public async Task<byte[]> GenerateXYmodemInitCommandAsync(FileTransferSession session)
    {
        // XMODEM/YMODEM initialization - simplified implementation
        var protocolName = session.Protocol == FileTransferProtocol.XMODEM ? "rx" : "ry";

        var command = session.IsUpload
            ? $"{protocolName}\r\n"
            : $"s{protocolName.Substring(1)} \"{session.FileName}\"\r\n";

        return await Task.FromResult(Encoding.ASCII.GetBytes(command));
    }

    public async Task<bool> ProcessXYmodemDataAsync(string sessionId, byte[] data)
    {
        // Placeholder for XMODEM/YMODEM data processing
        if (!_activeSessions.TryGetValue(sessionId, out var session)) return false;

        session.BytesTransferred += data.Length;

        _logger.Debug("Processed {ByteCount} bytes for {Protocol} session {SessionId}",
            data.Length, session.Protocol, sessionId);

        return await Task.FromResult(true);
    }

    #endregion

    #region HTTP Transfer

    public async Task<string> GenerateDownloadUrlAsync(int fileId, int userId, TimeSpan? expiry = null)
    {
        var file = await _fileAreaService.GetFileAsync(fileId);
        if (file == null) throw new ArgumentException("File not found", nameof(fileId));

        if (!await _fileAreaService.CanUserAccessAreaAsync(userId, file.AreaId, false)) throw new UnauthorizedAccessException("User does not have download permission for this file");

        // Generate a secure token for the download
        var token = GenerateSecureToken();
        var expiryTime = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(1));

        // Store the token temporarily (in a real implementation, you'd use a proper token store)
        const string sql = @"
            INSERT INTO DownloadTokens (Token, FileId, UserId, ExpiresAt)
            VALUES (@Token, @FileId, @UserId, @ExpiresAt)";

        await _databaseManager.ExecuteAsync(sql, new
        {
            Token = token,
            FileId = fileId,
            UserId = userId,
            ExpiresAt = expiryTime
        });

        return $"/api/files/download/{token}";
    }

    public async Task<string> GenerateUploadUrlAsync(int areaId, int userId, TimeSpan? expiry = null)
    {
        if (!await _fileAreaService.CanUserAccessAreaAsync(userId, areaId, true)) throw new UnauthorizedAccessException("User does not have upload permission for this area");

        var token = GenerateSecureToken();
        var expiryTime = DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(1));

        const string sql = @"
            INSERT INTO UploadTokens (Token, AreaId, UserId, ExpiresAt)
            VALUES (@Token, @AreaId, @UserId, @ExpiresAt)";

        await _databaseManager.ExecuteAsync(sql, new
        {
            Token = token,
            AreaId = areaId,
            UserId = userId,
            ExpiresAt = expiryTime
        });

        return $"/api/files/upload/{token}";
    }

    #endregion

    #region Transfer Statistics

    public async Task<IEnumerable<FileTransferSession>> GetActiveSessionsAsync()
    {
        return await Task.FromResult(_activeSessions.Values.ToList());
    }

    public async Task<IEnumerable<FileTransferSession>> GetUserSessionHistoryAsync(int userId, int count = 50)
    {
        const string sql = @"
            SELECT SessionId, UserId, FileId, Protocol, IsUpload, FileName, FileSize,
                   BytesTransferred, StartTime, EndTime, IsSuccessful, ErrorMessage
            FROM FileTransfers 
            WHERE UserId = @UserId
            ORDER BY StartTime DESC
            LIMIT @Count";

        var transfers = await _databaseManager.QueryAsync<dynamic>(sql, new { UserId = userId, Count = count });

        return transfers.Select(t => new FileTransferSession
        {
            SessionId = t.SessionId,
            UserId = t.UserId,
            FileId = t.FileId,
            Protocol = Enum.Parse<FileTransferProtocol>(t.Protocol),
            IsUpload = t.IsUpload,
            FileName = t.FileName,
            FileSize = t.FileSize,
            BytesTransferred = t.BytesTransferred,
            StartTime = t.StartTime,
            EndTime = t.EndTime,
            IsCompleted = t.EndTime != null,
            IsSuccessful = t.IsSuccessful,
            ErrorMessage = t.ErrorMessage
        }).ToList();
    }

    public async Task<int> GetConcurrentTransferCountAsync(int userId)
    {
        var userSessions = _activeSessions.Values.Where(s => s.UserId == userId);
        return await Task.FromResult(userSessions.Count());
    }

    public async Task<bool> CanUserStartTransferAsync(int userId)
    {
        var currentCount = await GetConcurrentTransferCountAsync(userId);
        return currentCount < _maxConcurrentTransfers;
    }

    #endregion

    #region Helper Methods

    private static string GenerateSecureToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F1} GB";
    }

    #endregion
}