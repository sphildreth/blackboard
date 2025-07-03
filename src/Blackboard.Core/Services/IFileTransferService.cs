namespace Blackboard.Core.Services;

public enum FileTransferProtocol
{
    ZMODEM,
    XMODEM,
    YMODEM,
    HTTP
}

public class FileTransferSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public int UserId { get; set; }
    public int FileId { get; set; }
    public FileTransferProtocol Protocol { get; set; }
    public bool IsUpload { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long BytesTransferred { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public double ProgressPercentage => FileSize > 0 ? (double)BytesTransferred / FileSize * 100 : 0;
}

public interface IFileTransferService
{
    // Transfer Session Management
    Task<FileTransferSession> StartDownloadSessionAsync(int fileId, int userId, FileTransferProtocol protocol);
    Task<FileTransferSession> StartUploadSessionAsync(int areaId, string fileName, long fileSize, int userId, FileTransferProtocol protocol);
    Task<FileTransferSession?> GetSessionAsync(string sessionId);
    Task<bool> CompleteSessionAsync(string sessionId, bool successful, string? errorMessage = null);
    Task<bool> UpdateProgressAsync(string sessionId, long bytesTransferred);

    // Protocol Support
    Task<bool> IsProtocolSupportedAsync(FileTransferProtocol protocol);
    Task<string> GetProtocolInstructionsAsync(FileTransferProtocol protocol, bool isUpload);

    // ZMODEM Protocol
    Task<byte[]> GenerateZmodemInitCommandAsync(FileTransferSession session);
    Task<bool> ProcessZmodemDataAsync(string sessionId, byte[] data);

    // XMODEM/YMODEM Protocol  
    Task<byte[]> GenerateXYmodemInitCommandAsync(FileTransferSession session);
    Task<bool> ProcessXYmodemDataAsync(string sessionId, byte[] data);

    // HTTP Transfer (for modern clients)
    Task<string> GenerateDownloadUrlAsync(int fileId, int userId, TimeSpan? expiry = null);
    Task<string> GenerateUploadUrlAsync(int areaId, int userId, TimeSpan? expiry = null);

    // Transfer Statistics
    Task<IEnumerable<FileTransferSession>> GetActiveSessionsAsync();
    Task<IEnumerable<FileTransferSession>> GetUserSessionHistoryAsync(int userId, int count = 50);
    Task<int> GetConcurrentTransferCountAsync(int userId);
    Task<bool> CanUserStartTransferAsync(int userId);
}
