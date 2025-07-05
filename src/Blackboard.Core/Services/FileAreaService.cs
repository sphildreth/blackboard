using System.Security.Cryptography;
using System.Text.Json;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public class FileAreaService : IFileAreaService
{
    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger _logger;
    private readonly string _fileStorageBasePath;

    public FileAreaService(IDatabaseManager databaseManager, ILogger logger, string fileStorageBasePath = "files")
    {
        _databaseManager = databaseManager;
        _logger = logger;
        _fileStorageBasePath = fileStorageBasePath;
        
        // Ensure base storage directory exists
        if (!Directory.Exists(_fileStorageBasePath))
        {
            Directory.CreateDirectory(_fileStorageBasePath);
        }
    }

    #region File Area Management

    public async Task<IEnumerable<FileAreaDto>> GetAllFileAreasAsync()
    {
        const string sql = @"
            SELECT fa.*, 
                   COUNT(f.Id) as FileCount,
                   COALESCE(SUM(f.Size), 0) as TotalSize
            FROM FileAreas fa
            LEFT JOIN Files f ON fa.Id = f.AreaId AND f.IsActive = 1
            GROUP BY fa.Id
            ORDER BY fa.Name";

        return await _databaseManager.QueryAsync<FileAreaDto>(sql);
    }

    public async Task<IEnumerable<FileAreaDto>> GetActiveFileAreasAsync()
    {
        const string sql = @"
            SELECT fa.*, 
                   COUNT(f.Id) as FileCount,
                   COALESCE(SUM(f.Size), 0) as TotalSize
            FROM FileAreas fa
            LEFT JOIN Files f ON fa.Id = f.AreaId AND f.IsActive = 1
            WHERE fa.IsActive = 1
            GROUP BY fa.Id
            ORDER BY fa.Name";

        return await _databaseManager.QueryAsync<FileAreaDto>(sql);
    }

    public async Task<FileAreaDto?> GetFileAreaAsync(int areaId)
    {
        const string sql = @"
            SELECT fa.*, 
                   COUNT(f.Id) as FileCount,
                   COALESCE(SUM(f.Size), 0) as TotalSize
            FROM FileAreas fa
            LEFT JOIN Files f ON fa.Id = f.AreaId AND f.IsActive = 1
            WHERE fa.Id = @AreaId
            GROUP BY fa.Id";

        return await _databaseManager.QueryFirstOrDefaultAsync<FileAreaDto>(sql, new { AreaId = areaId });
    }

    public async Task<FileAreaDto?> GetFileAreaByNameAsync(string name)
    {
        const string sql = @"
            SELECT fa.*, 
                   COUNT(f.Id) as FileCount,
                   COALESCE(SUM(f.Size), 0) as TotalSize
            FROM FileAreas fa
            LEFT JOIN Files f ON fa.Id = f.AreaId AND f.IsActive = 1
            WHERE fa.Name = @Name
            GROUP BY fa.Id";

        return await _databaseManager.QueryFirstOrDefaultAsync<FileAreaDto>(sql, new { Name = name });
    }

    public async Task<FileAreaDto> CreateFileAreaAsync(FileAreaDto fileArea)
    {
        const string sql = @"
            INSERT INTO FileAreas (Name, Description, Path, RequiredLevel, UploadLevel, 
                                 IsActive, MaxFileSize, AllowUploads, AllowDownloads)
            VALUES (@Name, @Description, @Path, @RequiredLevel, @UploadLevel, 
                   @IsActive, @MaxFileSize, @AllowUploads, @AllowDownloads);
            SELECT last_insert_rowid();";

        var id = await _databaseManager.QueryFirstAsync<int>(sql, fileArea);
        
        // Create physical directory
        var areaPath = Path.Combine(_fileStorageBasePath, fileArea.Path);
        if (!Directory.Exists(areaPath))
        {
            Directory.CreateDirectory(areaPath);
        }

        _logger.Information("Created file area {AreaName} with ID {AreaId}", fileArea.Name, id);
        
        return (await GetFileAreaAsync(id))!;
    }

    public async Task<FileAreaDto> UpdateFileAreaAsync(FileAreaDto fileArea)
    {
        const string sql = @"
            UPDATE FileAreas 
            SET Name = @Name, Description = @Description, Path = @Path, 
                RequiredLevel = @RequiredLevel, UploadLevel = @UploadLevel,
                IsActive = @IsActive, MaxFileSize = @MaxFileSize, 
                AllowUploads = @AllowUploads, AllowDownloads = @AllowDownloads
            WHERE Id = @Id";

        await _databaseManager.ExecuteAsync(sql, fileArea);
        
        _logger.Information("Updated file area {AreaName} (ID: {AreaId})", fileArea.Name, fileArea.Id);
        
        return (await GetFileAreaAsync(fileArea.Id))!;
    }

    public async Task<bool> DeleteFileAreaAsync(int areaId)
    {
        // Get area info before deletion
        var area = await GetFileAreaAsync(areaId);
        if (area == null) return false;

        // Delete all files in the area
        const string deleteFilesSql = "UPDATE Files SET IsActive = 0 WHERE AreaId = @AreaId";
        await _databaseManager.ExecuteAsync(deleteFilesSql, new { AreaId = areaId });

        // Delete the area
        const string deleteAreaSql = "UPDATE FileAreas SET IsActive = 0 WHERE Id = @AreaId";
        var result = await _databaseManager.ExecuteAsync(deleteAreaSql, new { AreaId = areaId });

        if (result > 0)
        {
            _logger.Information("Deleted file area {AreaName} (ID: {AreaId})", area.Name, areaId);
        }

        return result > 0;
    }

    public async Task<bool> CanUserAccessAreaAsync(int userId, int areaId, bool isUpload = false)
    {
        const string sql = @"
            SELECT fa.RequiredLevel, fa.UploadLevel, fa.IsActive, 
                   fa.AllowUploads, fa.AllowDownloads, u.SecurityLevel
            FROM FileAreas fa
            CROSS JOIN Users u
            WHERE fa.Id = @AreaId AND u.Id = @UserId";

        var result = await _databaseManager.QueryFirstOrDefaultAsync<dynamic>(sql, new { AreaId = areaId, UserId = userId });
        if (result == null) return false;

        // Check if area is active
        bool isActive;
        try
        {
            isActive = Convert.ToBoolean(result.IsActive);
        }
        catch
        {
            isActive = false;
        }
        if (!isActive) return false;

        // Extract values safely
        bool allowUploads, allowDownloads;
        int securityLevel, requiredLevel, uploadLevel;
        
        try
        {
            allowUploads = Convert.ToBoolean(result.AllowUploads);
            allowDownloads = Convert.ToBoolean(result.AllowDownloads);
            securityLevel = Convert.ToInt32(result.SecurityLevel);
            requiredLevel = Convert.ToInt32(result.RequiredLevel);
            uploadLevel = Convert.ToInt32(result.UploadLevel);
        }
        catch
        {
            return false;
        }

        // Check security level requirement
        if (isUpload)
        {
            // For uploads, check both required level and upload level, and if uploads are allowed
            return allowUploads && securityLevel >= requiredLevel && securityLevel >= uploadLevel;
        }
        else
        {
            // For downloads, check required level and if downloads are allowed
            return allowDownloads && securityLevel >= requiredLevel;
        }
    }

    #endregion

    #region File Management

    public async Task<FileSearchResultDto> SearchFilesAsync(string? searchTerm = null, int? areaId = null, 
        string[]? tags = null, int page = 1, int pageSize = 20, string? sortBy = null, bool sortDesc = false)
    {
        var whereConditions = new List<string> { "f.IsActive = 1", "f.IsApproved = 1" };
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            whereConditions.Add("(f.FileName LIKE @SearchTerm OR f.Description LIKE @SearchTerm)");
            parameters["SearchTerm"] = $"%{searchTerm}%";
        }

        if (areaId.HasValue)
        {
            whereConditions.Add("f.AreaId = @AreaId");
            parameters["AreaId"] = areaId.Value;
        }

        if (tags != null && tags.Length > 0)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                whereConditions.Add($"f.Tags LIKE @Tag{i}");
                parameters[$"Tag{i}"] = $"%\"{tags[i]}\"%";
            }
        }

        var orderBy = sortBy?.ToLower() switch
        {
            "name" => "f.FileName",
            "size" => "f.Size",
            "date" => "f.UploadDate",
            "downloads" => "f.DownloadCount",
            _ => "f.UploadDate"
        };
        orderBy += sortDesc ? " DESC" : " ASC";

        var whereClause = string.Join(" AND ", whereConditions);
        
        // Count query
        var countSql = $@"
            SELECT COUNT(*)
            FROM Files f
            INNER JOIN FileAreas fa ON f.AreaId = fa.Id
            WHERE {whereClause}";

        var totalCount = await _databaseManager.QueryFirstAsync<int>(countSql, parameters);

        // Add pagination parameters
        parameters["PageSize"] = pageSize;
        parameters["Offset"] = (page - 1) * pageSize;

        // Data query with pagination
        var dataSql = $@"
            SELECT f.*, fa.Name as AreaName, 
                   u1.Handle as UploaderHandle, u2.Handle as ApproverHandle,
                   COALESCE(AVG(CAST(fr.Rating AS REAL)), 0) as AverageRating,
                   COUNT(fr.Id) as RatingCount,
                   f.Tags as TagsJson
            FROM Files f
            INNER JOIN FileAreas fa ON f.AreaId = fa.Id
            LEFT JOIN Users u1 ON f.UploaderId = u1.Id
            LEFT JOIN Users u2 ON f.ApprovedBy = u2.Id
            LEFT JOIN FileRatings fr ON f.Id = fr.FileId
            WHERE {whereClause}
            GROUP BY f.Id
            ORDER BY {orderBy}
            LIMIT @PageSize OFFSET @Offset";

        var files = await _databaseManager.QueryAsync<dynamic>(dataSql, parameters);

        // Convert to DTOs and process tags
        var fileResults = new List<BbsFileDto>();
        foreach (dynamic fileData in files)
        {
            try
            {
                var file = new BbsFileDto
                {
                    Id = GetDynamicProperty<int>(fileData, "Id"),
                    AreaId = GetDynamicProperty<int>(fileData, "AreaId"),
                    AreaName = GetDynamicProperty<string>(fileData, "AreaName") ?? string.Empty,
                    FileName = GetDynamicProperty<string>(fileData, "FileName") ?? string.Empty,
                    OriginalFileName = GetDynamicProperty<string>(fileData, "OriginalFileName") ?? string.Empty,
                    Description = GetDynamicProperty<string>(fileData, "Description"),
                    FilePath = GetDynamicProperty<string>(fileData, "FilePath") ?? string.Empty,
                    Size = GetDynamicProperty<long>(fileData, "Size"),
                    MimeType = GetDynamicProperty<string>(fileData, "MimeType"),
                    UploadDate = GetDynamicProperty<DateTime>(fileData, "UploadDate"),
                    UploaderId = GetDynamicProperty<int>(fileData, "UploaderId"),
                    UploaderHandle = GetDynamicProperty<string>(fileData, "UploaderHandle"),
                    DownloadCount = GetDynamicProperty<int>(fileData, "DownloadCount"),
                    LastDownloadAt = GetDynamicProperty<DateTime?>(fileData, "LastDownloadAt"),
                    IsApproved = GetDynamicProperty<bool>(fileData, "IsApproved"),
                    ApprovedBy = GetDynamicProperty<int?>(fileData, "ApprovedBy"),
                    ApproverHandle = GetDynamicProperty<string>(fileData, "ApproverHandle"),
                    ApprovedAt = GetDynamicProperty<DateTime?>(fileData, "ApprovedAt"),
                    IsActive = GetDynamicProperty<bool>(fileData, "IsActive"),
                    ExpiresAt = GetDynamicProperty<DateTime?>(fileData, "ExpiresAt"),
                    AverageRating = GetDynamicProperty<double>(fileData, "AverageRating"),
                    RatingCount = GetDynamicProperty<int>(fileData, "RatingCount"),
                    Checksum = GetDynamicProperty<string>(fileData, "Checksum") ?? string.Empty
                };

                // Process tags
                string? tagsJson = GetDynamicProperty<string>(fileData, "TagsJson");
                file.Tags = string.IsNullOrEmpty(tagsJson) ? Array.Empty<string>() : 
                           JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
                file.SizeFormatted = FormatFileSize(file.Size);
                
                fileResults.Add(file);
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Error processing file data for file {FileId}", fileData.Id);
                // Skip this file and continue with others
            }
        }

        return new FileSearchResultDto
        {
            Files = fileResults,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<BbsFileDto?> GetFileAsync(int fileId)
    {
        const string sql = @"
            SELECT f.*, fa.Name as AreaName, 
                   u1.Handle as UploaderHandle, u2.Handle as ApproverHandle,
                   COALESCE(AVG(CAST(fr.Rating AS REAL)), 0) as AverageRating,
                   COUNT(fr.Id) as RatingCount
            FROM Files f
            INNER JOIN FileAreas fa ON f.AreaId = fa.Id
            LEFT JOIN Users u1 ON f.UploaderId = u1.Id
            LEFT JOIN Users u2 ON f.ApprovedBy = u2.Id
            LEFT JOIN FileRatings fr ON f.Id = fr.FileId
            WHERE f.Id = @FileId AND f.IsActive = 1
            GROUP BY f.Id";

        var fileData = await _databaseManager.QueryFirstOrDefaultAsync<dynamic>(sql, new { FileId = fileId });
        if (fileData == null) return null;

        var file = new BbsFileDto
        {
            Id = fileData.Id,
            AreaId = fileData.AreaId,
            AreaName = fileData.AreaName,
            FileName = fileData.FileName,
            OriginalFileName = fileData.OriginalFileName,
            Description = fileData.Description,
            FilePath = fileData.FilePath,
            Size = fileData.Size,
            MimeType = fileData.MimeType,
            UploadDate = fileData.UploadDate,
            UploaderId = fileData.UploaderId,
            UploaderHandle = fileData.UploaderHandle,
            DownloadCount = fileData.DownloadCount,
            LastDownloadAt = fileData.LastDownloadAt,
            IsApproved = fileData.IsApproved,
            ApprovedBy = fileData.ApprovedBy,
            ApproverHandle = fileData.ApproverHandle,
            ApprovedAt = fileData.ApprovedAt,
            IsActive = fileData.IsActive,
            ExpiresAt = fileData.ExpiresAt,
            AverageRating = fileData.AverageRating,
            RatingCount = fileData.RatingCount,
            Checksum = fileData.Checksum ?? string.Empty
        };

        // Process tags
        string? tagsJson = fileData.Tags;
        file.Tags = string.IsNullOrEmpty(tagsJson) ? Array.Empty<string>() : 
                   JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
        file.SizeFormatted = FormatFileSize(file.Size);

        return file;
    }

    public async Task<IEnumerable<BbsFileDto>> GetFilesByAreaAsync(int areaId, int page = 1, int pageSize = 20)
    {
        var result = await SearchFilesAsync(areaId: areaId, page: page, pageSize: pageSize);
        return result.Files;
    }

    public async Task<IEnumerable<BbsFileDto>> GetPendingApprovalFilesAsync()
    {
        const string sql = @"
            SELECT f.*, fa.Name as AreaName, 
                   u1.Handle as UploaderHandle,
                   0 as AverageRating, 0 as RatingCount
            FROM Files f
            INNER JOIN FileAreas fa ON f.AreaId = fa.Id
            LEFT JOIN Users u1 ON f.UploaderId = u1.Id
            WHERE f.IsActive = 1 AND f.IsApproved = 0
            ORDER BY f.UploadDate ASC";

        var filesData = await _databaseManager.QueryAsync<dynamic>(sql);
        
        var files = new List<BbsFileDto>();
        foreach (var fileData in filesData)
        {
            var file = new BbsFileDto
            {
                Id = fileData.Id,
                AreaId = fileData.AreaId,
                AreaName = fileData.AreaName,
                FileName = fileData.FileName,
                OriginalFileName = fileData.OriginalFileName,
                Description = fileData.Description,
                FilePath = fileData.FilePath,
                Size = fileData.Size,
                MimeType = fileData.MimeType,
                UploadDate = fileData.UploadDate,
                UploaderId = fileData.UploaderId,
                UploaderHandle = fileData.UploaderHandle,
                DownloadCount = fileData.DownloadCount,
                LastDownloadAt = fileData.LastDownloadAt,
                IsApproved = fileData.IsApproved,
                ApprovedBy = fileData.ApprovedBy,
                ApprovedAt = fileData.ApprovedAt,
                IsActive = fileData.IsActive,
                ExpiresAt = fileData.ExpiresAt,
                AverageRating = fileData.AverageRating,
                RatingCount = fileData.RatingCount,
                Checksum = fileData.Checksum ?? string.Empty
            };

            // Process tags
            string? tagsJson = fileData.Tags;
            file.Tags = string.IsNullOrEmpty(tagsJson) ? Array.Empty<string>() : 
                       JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
            file.SizeFormatted = FormatFileSize(file.Size);
            
            files.Add(file);
        }

        return files;
    }

    public async Task<IEnumerable<BbsFileDto>> GetRecentUploadsAsync(int count = 10)
    {
        var result = await SearchFilesAsync(page: 1, pageSize: count, sortBy: "date", sortDesc: true);
        return result.Files;
    }

    public async Task<IEnumerable<BbsFileDto>> GetMostDownloadedFilesAsync(int count = 10)
    {
        var result = await SearchFilesAsync(page: 1, pageSize: count, sortBy: "downloads", sortDesc: true);
        return result.Files;
    }

    #endregion

    #region File Upload/Download

    public async Task<BbsFileDto> UploadFileAsync(FileUploadDto upload, int uploaderId)
    {
        // Validate area access
        if (!await CanUserAccessAreaAsync(uploaderId, upload.AreaId, isUpload: true))
        {
            throw new UnauthorizedAccessException("User does not have upload permission for this area");
        }

        var area = await GetFileAreaAsync(upload.AreaId);
        if (area == null)
        {
            throw new ArgumentException("File area not found");
        }

        if (upload.FileData.Length > area.MaxFileSize)
        {
            throw new ArgumentException($"File size exceeds maximum allowed size of {FormatFileSize(area.MaxFileSize)}");
        }

        // Generate unique filename to prevent conflicts
        var extension = Path.GetExtension(upload.FileName);
        var baseFileName = Path.GetFileNameWithoutExtension(upload.FileName);
        var uniqueFileName = $"{baseFileName}_{Guid.NewGuid():N}{extension}";
        
        var areaPath = Path.Combine(_fileStorageBasePath, area.Path);
        var filePath = Path.Combine(areaPath, uniqueFileName);

        // Ensure directory exists
        if (!Directory.Exists(areaPath))
        {
            Directory.CreateDirectory(areaPath);
        }

        // Calculate checksum
        var checksum = CalculateChecksum(upload.FileData);

        // Check for duplicate files by checksum
        const string duplicateCheckSql = "SELECT COUNT(*) FROM Files WHERE AreaId = @AreaId AND Checksum = @Checksum AND IsActive = 1";
        var duplicateCount = await _databaseManager.QueryFirstAsync<int>(duplicateCheckSql, 
            new { AreaId = upload.AreaId, Checksum = checksum });

        if (duplicateCount > 0)
        {
            throw new InvalidOperationException("A file with identical content already exists in this area");
        }

        // Save file to disk
        await System.IO.File.WriteAllBytesAsync(filePath, upload.FileData);

        // Insert file record
        const string insertSql = @"
            INSERT INTO Files (AreaId, FileName, OriginalFileName, Description, FilePath, Size, 
                             Checksum, MimeType, Tags, UploaderId, IsApproved)
            VALUES (@AreaId, @FileName, @OriginalFileName, @Description, @FilePath, @Size, 
                   @Checksum, @MimeType, @Tags, @UploaderId, @IsApproved);
            SELECT last_insert_rowid();";

        var fileId = await _databaseManager.QueryFirstAsync<int>(insertSql, new
        {
            AreaId = upload.AreaId,
            FileName = uniqueFileName,
            OriginalFileName = upload.FileName,
            Description = upload.Description,
            FilePath = filePath,
            Size = upload.FileData.Length,
            Checksum = checksum,
            MimeType = upload.MimeType,
            Tags = upload.Tags.Length > 0 ? JsonSerializer.Serialize(upload.Tags) : null,
            UploaderId = uploaderId,
            IsApproved = 0 // Files require approval by default
        });

        _logger.Information("User {UploaderId} uploaded file {FileName} to area {AreaId}", 
            uploaderId, upload.FileName, upload.AreaId);

        return (await GetFileAsync(fileId))!;
    }

    public async Task<Stream?> DownloadFileAsync(int fileId, int userId)
    {
        var file = await GetFileAsync(fileId);
        if (file == null || !file.IsActive || !file.IsApproved)
        {
            return null;
        }

        if (!await CanUserAccessAreaAsync(userId, file.AreaId, isUpload: false))
        {
            throw new UnauthorizedAccessException("User does not have download permission for this area");
        }

        if (!System.IO.File.Exists(file.FilePath))
        {
            _logger.Error("File not found on disk: {FilePath}", file.FilePath);
            return null;
        }

        // Record download
        await RecordDownloadAsync(fileId, userId);

        return new FileStream(file.FilePath, FileMode.Open, FileAccess.Read);
    }

    public async Task<bool> DeleteFileAsync(int fileId, int userId)
    {
        var file = await GetFileAsync(fileId);
        if (file == null) return false;

        // Check if user can delete (owner or admin)
        const string userSql = "SELECT SecurityLevel FROM Users WHERE Id = @UserId";
        var userLevel = await _databaseManager.QueryFirstOrDefaultAsync<int?>(userSql, new { UserId = userId });
        
        if (userLevel == null) return false;

        bool canDelete = file.UploaderId == userId || userLevel >= 255; // Admin level

        if (!canDelete)
        {
            throw new UnauthorizedAccessException("User does not have permission to delete this file");
        }

        // Soft delete the file
        const string deleteSql = "UPDATE Files SET IsActive = 0 WHERE Id = @FileId";
        var result = await _databaseManager.ExecuteAsync(deleteSql, new { FileId = fileId });

        if (result > 0)
        {
            _logger.Information("User {UserId} deleted file {FileName} (ID: {FileId})", 
                userId, file.FileName, fileId);
        }

        return result > 0;
    }

    public async Task RecordDownloadAsync(int fileId, int userId)
    {
        const string sql = @"
            UPDATE Files 
            SET DownloadCount = DownloadCount + 1, LastDownloadAt = CURRENT_TIMESTAMP 
            WHERE Id = @FileId";

        await _databaseManager.ExecuteAsync(sql, new { FileId = fileId });
        
        _logger.Debug("Recorded download of file {FileId} by user {UserId}", fileId, userId);
    }

    #endregion

    #region File Approval

    public async Task<bool> ApproveFileAsync(int fileId, int approverId)
    {
        const string sql = @"
            UPDATE Files 
            SET IsApproved = 1, ApprovedBy = @ApproverId, ApprovedAt = CURRENT_TIMESTAMP 
            WHERE Id = @FileId";

        var result = await _databaseManager.ExecuteAsync(sql, new { FileId = fileId, ApproverId = approverId });
        
        if (result > 0)
        {
            _logger.Information("File {FileId} approved by user {ApproverId}", fileId, approverId);
        }

        return result > 0;
    }

    public async Task<bool> RejectFileAsync(int fileId, int approverId, string? reason = null)
    {
        var file = await GetFileAsync(fileId);
        if (file == null) return false;

        // Delete the physical file
        if (System.IO.File.Exists(file.FilePath))
        {
            System.IO.File.Delete(file.FilePath);
        }

        // Remove from database
        const string sql = "UPDATE Files SET IsActive = 0 WHERE Id = @FileId";
        var result = await _databaseManager.ExecuteAsync(sql, new { FileId = fileId });

        if (result > 0)
        {
            _logger.Information("File {FileId} rejected by user {ApproverId}. Reason: {Reason}", 
                fileId, approverId, reason ?? "No reason provided");
        }

        return result > 0;
    }

    #endregion

    #region File Ratings & Comments

    public async Task<IEnumerable<FileRatingDto>> GetFileRatingsAsync(int fileId)
    {
        const string sql = @"
            SELECT fr.*, u.Handle as UserHandle
            FROM FileRatings fr
            INNER JOIN Users u ON fr.UserId = u.Id
            WHERE fr.FileId = @FileId
            ORDER BY fr.RatingDate DESC";

        return await _databaseManager.QueryAsync<FileRatingDto>(sql, new { FileId = fileId });
    }

    public async Task<FileRatingDto?> GetUserFileRatingAsync(int fileId, int userId)
    {
        const string sql = @"
            SELECT fr.*, u.Handle as UserHandle
            FROM FileRatings fr
            INNER JOIN Users u ON fr.UserId = u.Id
            WHERE fr.FileId = @FileId AND fr.UserId = @UserId";

        return await _databaseManager.QueryFirstOrDefaultAsync<FileRatingDto>(sql, 
            new { FileId = fileId, UserId = userId });
    }

    public async Task<FileRatingDto> AddFileRatingAsync(int fileId, int userId, int rating, string? comment = null)
    {
        if (rating < 1 || rating > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5");
        }

        const string sql = @"
            INSERT INTO FileRatings (FileId, UserId, Rating, Comment)
            VALUES (@FileId, @UserId, @Rating, @Comment);
            SELECT last_insert_rowid();";

        var ratingId = await _databaseManager.QueryFirstAsync<int>(sql, new
        {
            FileId = fileId,
            UserId = userId,
            Rating = rating,
            Comment = comment
        });

        _logger.Information("User {UserId} rated file {FileId} with {Rating} stars", 
            userId, fileId, rating);

        return (await _databaseManager.QueryFirstAsync<FileRatingDto>(@"
            SELECT fr.*, u.Handle as UserHandle
            FROM FileRatings fr
            INNER JOIN Users u ON fr.UserId = u.Id
            WHERE fr.Id = @Id", new { Id = ratingId }));
    }

    public async Task<FileRatingDto> UpdateFileRatingAsync(int fileId, int userId, int rating, string? comment = null)
    {
        if (rating < 1 || rating > 5)
        {
            throw new ArgumentException("Rating must be between 1 and 5");
        }

        const string sql = @"
            UPDATE FileRatings 
            SET Rating = @Rating, Comment = @Comment, RatingDate = CURRENT_TIMESTAMP
            WHERE FileId = @FileId AND UserId = @UserId";

        await _databaseManager.ExecuteAsync(sql, new
        {
            FileId = fileId,
            UserId = userId,
            Rating = rating,
            Comment = comment
        });

        return (await GetUserFileRatingAsync(fileId, userId))!;
    }

    public async Task<bool> DeleteFileRatingAsync(int fileId, int userId)
    {
        const string sql = "DELETE FROM FileRatings WHERE FileId = @FileId AND UserId = @UserId";
        var result = await _databaseManager.ExecuteAsync(sql, new { FileId = fileId, UserId = userId });
        return result > 0;
    }

    #endregion

    #region Statistics

    public async Task<FileAreaStatisticsDto> GetFileAreaStatisticsAsync()
    {
        const string sql = @"
            SELECT 
                COUNT(DISTINCT fa.Id) as TotalAreas,
                COUNT(DISTINCT CASE WHEN fa.IsActive = 1 THEN fa.Id END) as ActiveAreas,
                COUNT(DISTINCT f.Id) as TotalFiles,
                COUNT(DISTINCT CASE WHEN f.IsApproved = 1 THEN f.Id END) as ApprovedFiles,
                COUNT(DISTINCT CASE WHEN f.IsApproved = 0 AND f.IsActive = 1 THEN f.Id END) as PendingApproval,
                COALESCE(SUM(CASE WHEN f.IsActive = 1 THEN f.Size ELSE 0 END), 0) as TotalFileSize,
                COUNT(DISTINCT CASE WHEN f.LastDownloadAt >= date('now', '-1 day') THEN f.Id END) as DownloadsToday,
                COUNT(DISTINCT CASE WHEN f.UploadDate >= date('now', '-1 day') THEN f.Id END) as UploadsToday
            FROM FileAreas fa
            LEFT JOIN Files f ON fa.Id = f.AreaId";

        var stats = await _databaseManager.QueryFirstAsync<FileAreaStatisticsDto>(sql);

        // Get most active areas
        const string areasSql = @"
            SELECT fa.Id, fa.Name, fa.Description, fa.Path, fa.RequiredLevel, fa.UploadLevel, 
                   fa.IsActive, fa.MaxFileSize, fa.AllowUploads, fa.AllowDownloads, 
                   fa.CreatedAt, fa.UpdatedAt,
                   COUNT(f.Id) as FileCount, COALESCE(SUM(f.Size), 0) as TotalSize
            FROM FileAreas fa
            LEFT JOIN Files f ON fa.Id = f.AreaId AND f.IsActive = 1
            WHERE fa.IsActive = 1
            GROUP BY fa.Id, fa.Name, fa.Description, fa.Path, fa.RequiredLevel, fa.UploadLevel,
                     fa.IsActive, fa.MaxFileSize, fa.AllowUploads, fa.AllowDownloads,
                     fa.CreatedAt, fa.UpdatedAt
            ORDER BY COUNT(f.Id) DESC
            LIMIT 5";

        stats.MostActiveAreas = (await _databaseManager.QueryAsync<FileAreaDto>(areasSql)).ToList();

        // Get most downloaded files
        stats.MostDownloadedFiles = (await GetMostDownloadedFilesAsync(5)).ToList();

        // Get recent uploads
        stats.RecentUploads = (await GetRecentUploadsAsync(5)).ToList();

        return stats;
    }

    public async Task<FileAreaStatisticsDto> GetUserFileStatisticsAsync(int userId)
    {
        const string sql = @"
            SELECT 
                0 as TotalAreas,
                0 as ActiveAreas,
                COUNT(DISTINCT f.Id) as TotalFiles,
                COUNT(DISTINCT CASE WHEN f.IsApproved = 1 THEN f.Id END) as ApprovedFiles,
                COUNT(DISTINCT CASE WHEN f.IsApproved = 0 AND f.IsActive = 1 THEN f.Id END) as PendingApproval,
                COALESCE(SUM(CASE WHEN f.IsActive = 1 THEN f.Size ELSE 0 END), 0) as TotalFileSize,
                0 as DownloadsToday,
                COUNT(DISTINCT CASE WHEN f.UploadDate >= date('now', '-1 day') THEN f.Id END) as UploadsToday
            FROM Files f
            WHERE f.UploaderId = @UserId";

        var stats = await _databaseManager.QueryFirstAsync<FileAreaStatisticsDto>(sql, new { UserId = userId });

        // Get user's recent uploads
        const string uploadsSql = @"
            SELECT f.*, fa.Name as AreaName, u.Handle as UploaderHandle,
                   COALESCE(AVG(CAST(fr.Rating AS REAL)), 0) as AverageRating,
                   COUNT(fr.Id) as RatingCount
            FROM Files f
            INNER JOIN FileAreas fa ON f.AreaId = fa.Id
            LEFT JOIN Users u ON f.UploaderId = u.Id
            LEFT JOIN FileRatings fr ON f.Id = fr.FileId
            WHERE f.UploaderId = @UserId AND f.IsActive = 1
            GROUP BY f.Id
            ORDER BY f.UploadDate DESC
            LIMIT 5";

        var recentUploadsData = await _databaseManager.QueryAsync<dynamic>(uploadsSql, new { UserId = userId });
        var recentUploads = new List<BbsFileDto>();
        
        foreach (var fileData in recentUploadsData)
        {
            var file = new BbsFileDto
            {
                Id = fileData.Id,
                AreaId = fileData.AreaId,
                AreaName = fileData.AreaName,
                FileName = fileData.FileName,
                OriginalFileName = fileData.OriginalFileName,
                Description = fileData.Description,
                FilePath = fileData.FilePath,
                Size = fileData.Size,
                MimeType = fileData.MimeType,
                UploadDate = fileData.UploadDate,
                UploaderId = fileData.UploaderId,
                UploaderHandle = fileData.UploaderHandle,
                DownloadCount = fileData.DownloadCount,
                LastDownloadAt = fileData.LastDownloadAt,
                IsApproved = fileData.IsApproved,
                ApprovedBy = fileData.ApprovedBy,
                ApprovedAt = fileData.ApprovedAt,
                IsActive = fileData.IsActive,
                ExpiresAt = fileData.ExpiresAt,
                AverageRating = fileData.AverageRating,
                RatingCount = fileData.RatingCount,
                Checksum = fileData.Checksum ?? string.Empty
            };

            // Process tags
            string? tagsJson = fileData.Tags;
            file.Tags = string.IsNullOrEmpty(tagsJson) ? Array.Empty<string>() : 
                       JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
            file.SizeFormatted = FormatFileSize(file.Size);
            
            recentUploads.Add(file);
        }

        stats.RecentUploads = recentUploads.ToList();
        stats.MostActiveAreas = new List<FileAreaDto>();
        stats.MostDownloadedFiles = new List<BbsFileDto>();

        return stats;
    }

    #endregion

    #region Batch Operations

    public async Task<int> CleanupExpiredFilesAsync()
    {
        const string sql = @"
            UPDATE Files 
            SET IsActive = 0 
            WHERE ExpiresAt IS NOT NULL AND ExpiresAt < CURRENT_TIMESTAMP AND IsActive = 1";

        var result = await _databaseManager.ExecuteAsync(sql);
        
        if (result > 0)
        {
            _logger.Information("Cleaned up {Count} expired files", result);
        }

        return result;
    }

    public async Task<int> CleanupOrphanedFilesAsync()
    {
        const string sql = @"
            SELECT f.Id, f.FilePath
            FROM Files f
            WHERE f.IsActive = 0";

        var orphanedFiles = await _databaseManager.QueryAsync<dynamic>(sql);
        int deletedCount = 0;

        foreach (var file in orphanedFiles)
        {
            try
            {
                if (System.IO.File.Exists(file.FilePath))
                {
                    System.IO.File.Delete(file.FilePath);
                    deletedCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to delete orphaned file: {FilePath}", file.FilePath);
            }
        }

        if (deletedCount > 0)
        {
            _logger.Information("Cleaned up {Count} orphaned files from disk", deletedCount);
        }

        return deletedCount;
    }

    public async Task<bool> ValidateFileIntegrityAsync(int fileId)
    {
        var file = await GetFileAsync(fileId);
        if (file == null || !System.IO.File.Exists(file.FilePath))
        {
            return false;
        }

        var fileData = await System.IO.File.ReadAllBytesAsync(file.FilePath);
        var calculatedChecksum = CalculateChecksum(fileData);

        return calculatedChecksum == file.Checksum;
    }

    public async Task<IEnumerable<BbsFileDto>> GetFilesNeedingValidationAsync()
    {
        // Return files that haven't been validated recently or have suspicious activity
        const string sql = @"
            SELECT f.*, fa.Name as AreaName, u.Handle as UploaderHandle,
                   0 as AverageRating, 0 as RatingCount
            FROM Files f
            INNER JOIN FileAreas fa ON f.AreaId = fa.Id
            LEFT JOIN Users u ON f.UploaderId = u.Id
            WHERE f.IsActive = 1 AND f.IsApproved = 1
            ORDER BY f.LastDownloadAt DESC NULLS LAST
            LIMIT 100";

        var filesData = await _databaseManager.QueryAsync<dynamic>(sql);
        var files = new List<BbsFileDto>();
        
        foreach (var fileData in filesData)
        {
            var file = new BbsFileDto
            {
                Id = fileData.Id,
                AreaId = fileData.AreaId,
                AreaName = fileData.AreaName,
                FileName = fileData.FileName,
                OriginalFileName = fileData.OriginalFileName,
                Description = fileData.Description,
                FilePath = fileData.FilePath,
                Size = fileData.Size,
                MimeType = fileData.MimeType,
                UploadDate = fileData.UploadDate,
                UploaderId = fileData.UploaderId,
                UploaderHandle = fileData.UploaderHandle,
                DownloadCount = fileData.DownloadCount,
                LastDownloadAt = fileData.LastDownloadAt,
                IsApproved = fileData.IsApproved,
                ApprovedBy = fileData.ApprovedBy,
                ApprovedAt = fileData.ApprovedAt,
                IsActive = fileData.IsActive,
                ExpiresAt = fileData.ExpiresAt,
                AverageRating = 0,
                RatingCount = 0,
                Checksum = fileData.Checksum ?? string.Empty
            };

            // Process tags
            string? tagsJson = fileData.Tags;
            file.Tags = string.IsNullOrEmpty(tagsJson) ? Array.Empty<string>() : 
                       JsonSerializer.Deserialize<string[]>(tagsJson) ?? Array.Empty<string>();
            file.SizeFormatted = FormatFileSize(file.Size);
            
            files.Add(file);
        }

        return files;
    }

    #endregion

    #region File Tags

    public async Task<IEnumerable<string>> GetAllTagsAsync()
    {
        const string sql = "SELECT DISTINCT Tags FROM Files WHERE Tags IS NOT NULL AND IsActive = 1";
        var tagRows = await _databaseManager.QueryAsync<string>(sql);
        
        var allTags = new HashSet<string>();
        foreach (var tagRow in tagRows)
        {
            if (!string.IsNullOrEmpty(tagRow))
            {
                var tags = JsonSerializer.Deserialize<string[]>(tagRow) ?? Array.Empty<string>();
                foreach (var tag in tags)
                {
                    allTags.Add(tag);
                }
            }
        }

        return allTags.OrderBy(t => t);
    }

    public async Task<IEnumerable<string>> GetPopularTagsAsync(int count = 20)
    {
        var allTags = await GetAllTagsAsync();
        
        // Count occurrences
        var tagCounts = new Dictionary<string, int>();
        
        const string sql = "SELECT Tags FROM Files WHERE Tags IS NOT NULL AND IsActive = 1";
        var tagRows = await _databaseManager.QueryAsync<string>(sql);
        
        foreach (var tagRow in tagRows)
        {
            if (!string.IsNullOrEmpty(tagRow))
            {
                var tags = JsonSerializer.Deserialize<string[]>(tagRow) ?? Array.Empty<string>();
                foreach (var tag in tags)
                {
                    tagCounts[tag] = tagCounts.GetValueOrDefault(tag, 0) + 1;
                }
            }
        }

        return tagCounts.OrderByDescending(kv => kv.Value)
                       .Take(count)
                       .Select(kv => kv.Key);
    }

    public async Task<bool> AddFileTagsAsync(int fileId, string[] tags)
    {
        var file = await GetFileAsync(fileId);
        if (file == null) return false;

        var existingTags = file.Tags.ToList();
        var newTags = tags.Where(t => !existingTags.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList();
        
        if (newTags.Count == 0) return true;

        existingTags.AddRange(newTags);
        var updatedTagsJson = JsonSerializer.Serialize(existingTags.ToArray());

        const string sql = "UPDATE Files SET Tags = @Tags WHERE Id = @FileId";
        var result = await _databaseManager.ExecuteAsync(sql, new { Tags = updatedTagsJson, FileId = fileId });

        return result > 0;
    }

    public async Task<bool> RemoveFileTagsAsync(int fileId, string[] tags)
    {
        var file = await GetFileAsync(fileId);
        if (file == null) return false;

        var existingTags = file.Tags.ToList();
        var updatedTags = existingTags.Where(t => !tags.Contains(t, StringComparer.OrdinalIgnoreCase)).ToArray();

        var updatedTagsJson = updatedTags.Length > 0 ? JsonSerializer.Serialize(updatedTags) : null;

        const string sql = "UPDATE Files SET Tags = @Tags WHERE Id = @FileId";
        var result = await _databaseManager.ExecuteAsync(sql, new { Tags = updatedTagsJson, FileId = fileId });

        return result > 0;
    }

    #endregion

    #region Helper Methods

    private static string CalculateChecksum(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToHexString(hash);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1048576) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1073741824) return $"{bytes / 1048576.0:F1} MB";
        return $"{bytes / 1073741824.0:F1} GB";
    }

    private static T GetDynamicProperty<T>(dynamic obj, string propertyName)
    {
        try
        {
            // Try dynamic access first
            var value = ((object)obj).GetType().GetProperty(propertyName)?.GetValue(obj);
            if (value == null)
                return default(T)!;
            
            if (typeof(T) == typeof(string))
                return (T)(object)(value?.ToString() ?? string.Empty);
            
            if (typeof(T) == typeof(int))
                return (T)(object)Convert.ToInt32(value);
            
            if (typeof(T) == typeof(long))
                return (T)(object)Convert.ToInt64(value);
            
            if (typeof(T) == typeof(bool))
                return (T)(object)Convert.ToBoolean(value);
            
            if (typeof(T) == typeof(DateTime))
                return (T)(object)Convert.ToDateTime(value);
            
            if (typeof(T) == typeof(double))
                return (T)(object)Convert.ToDouble(value);
            
            // Handle nullable types
            if (typeof(T) == typeof(int?))
                return (T)(object)(value != null ? Convert.ToInt32(value) : (int?)null)!;
            
            if (typeof(T) == typeof(DateTime?))
                return (T)(object)(value != null ? Convert.ToDateTime(value) : (DateTime?)null)!;
            
            return (T)value;
        }
        catch
        {
            return default(T)!;
        }
    }

    #endregion
}
