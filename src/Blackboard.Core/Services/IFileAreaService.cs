using Blackboard.Core.DTOs;

namespace Blackboard.Core.Services;

public interface IFileAreaService
{
    // File Area Management
    Task<IEnumerable<FileAreaDto>> GetAllFileAreasAsync();
    Task<IEnumerable<FileAreaDto>> GetActiveFileAreasAsync();
    Task<FileAreaDto?> GetFileAreaAsync(int areaId);
    Task<FileAreaDto?> GetFileAreaByNameAsync(string name);
    Task<FileAreaDto> CreateFileAreaAsync(FileAreaDto fileArea);
    Task<FileAreaDto> UpdateFileAreaAsync(FileAreaDto fileArea);
    Task<bool> DeleteFileAreaAsync(int areaId);
    Task<bool> CanUserAccessAreaAsync(int userId, int areaId, bool isUpload = false);

    // File Management
    Task<FileSearchResultDto> SearchFilesAsync(string? searchTerm = null, int? areaId = null,
        string[]? tags = null, int page = 1, int pageSize = 20, string? sortBy = null, bool sortDesc = false);

    Task<BbsFileDto?> GetFileAsync(int fileId);
    Task<IEnumerable<BbsFileDto>> GetFilesByAreaAsync(int areaId, int page = 1, int pageSize = 20);
    Task<IEnumerable<BbsFileDto>> GetPendingApprovalFilesAsync();
    Task<IEnumerable<BbsFileDto>> GetRecentUploadsAsync(int count = 10);
    Task<IEnumerable<BbsFileDto>> GetMostDownloadedFilesAsync(int count = 10);

    // File Upload/Download
    Task<BbsFileDto> UploadFileAsync(FileUploadDto upload, int uploaderId);
    Task<Stream?> DownloadFileAsync(int fileId, int userId);
    Task<bool> DeleteFileAsync(int fileId, int userId);
    Task RecordDownloadAsync(int fileId, int userId);

    // File Approval (Admin)
    Task<bool> ApproveFileAsync(int fileId, int approverId);
    Task<bool> RejectFileAsync(int fileId, int approverId, string? reason = null);

    // File Ratings & Comments
    Task<IEnumerable<FileRatingDto>> GetFileRatingsAsync(int fileId);
    Task<FileRatingDto?> GetUserFileRatingAsync(int fileId, int userId);
    Task<FileRatingDto> AddFileRatingAsync(int fileId, int userId, int rating, string? comment = null);
    Task<FileRatingDto> UpdateFileRatingAsync(int fileId, int userId, int rating, string? comment = null);
    Task<bool> DeleteFileRatingAsync(int fileId, int userId);

    // Statistics
    Task<FileAreaStatisticsDto> GetFileAreaStatisticsAsync();
    Task<FileAreaStatisticsDto> GetUserFileStatisticsAsync(int userId);

    // Batch Operations
    Task<int> CleanupExpiredFilesAsync();
    Task<int> CleanupOrphanedFilesAsync();
    Task<bool> ValidateFileIntegrityAsync(int fileId);
    Task<IEnumerable<BbsFileDto>> GetFilesNeedingValidationAsync();

    // File Tags
    Task<IEnumerable<string>> GetAllTagsAsync();
    Task<IEnumerable<string>> GetPopularTagsAsync(int count = 20);
    Task<bool> AddFileTagsAsync(int fileId, string[] tags);
    Task<bool> RemoveFileTagsAsync(int fileId, string[] tags);
}