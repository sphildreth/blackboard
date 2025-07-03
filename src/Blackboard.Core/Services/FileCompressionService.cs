using System.IO.Compression;
using System.Text.Json;
using Blackboard.Core.DTOs;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public enum CompressionFormat
{
    ZIP,
    GZIP,
    TAR,
    SEVENZ // 7-Zip format (would require external library)
}

public class ArchiveEntry
{
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsDirectory { get; set; }
}

public class CompressionResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public long OriginalSize { get; set; }
    public long CompressedSize { get; set; }
    public double CompressionRatio => OriginalSize > 0 ? (double)CompressedSize / OriginalSize : 0;
    public string? ErrorMessage { get; set; }
    public List<ArchiveEntry> Entries { get; set; } = new();
}

public interface IFileCompressionService
{
    // Single File Compression
    Task<CompressionResult> CompressFileAsync(string inputFilePath, string outputFilePath, CompressionFormat format);
    Task<CompressionResult> DecompressFileAsync(string inputFilePath, string outputDirectory);
    
    // Multiple Files/Directory Compression
    Task<CompressionResult> CompressDirectoryAsync(string directoryPath, string outputFilePath, CompressionFormat format);
    Task<CompressionResult> CompressFilesAsync(IEnumerable<string> filePaths, string outputFilePath, CompressionFormat format);
    
    // Archive Management
    Task<List<ArchiveEntry>> ListArchiveContentsAsync(string archivePath);
    Task<CompressionResult> ExtractSpecificFilesAsync(string archivePath, IEnumerable<string> fileNames, string outputDirectory);
    Task<bool> ValidateArchiveAsync(string archivePath);
    
    // BBS File Integration
    Task<BbsFileDto> CreateCompressedFileAreaArchiveAsync(int areaId, int userId, string? description = null);
    Task<BbsFileDto> CompressAndUploadFilesAsync(int areaId, IEnumerable<int> fileIds, int userId, string archiveName, string? description = null);
    
    // Format Detection and Support
    Task<CompressionFormat?> DetectFormatAsync(string filePath);
    Task<bool> IsFormatSupportedAsync(CompressionFormat format);
    Task<IEnumerable<CompressionFormat>> GetSupportedFormatsAsync();
}

public class FileCompressionService : IFileCompressionService
{
    private readonly IDatabaseManager _databaseManager;
    private readonly ILogger _logger;
    private readonly IFileAreaService _fileAreaService;
    private readonly string _tempCompressionPath;
    private readonly Dictionary<string, CompressionFormat> _formatMap;

    public FileCompressionService(IDatabaseManager databaseManager, ILogger logger, 
        IFileAreaService fileAreaService, string tempCompressionPath = "temp/compression")
    {
        _databaseManager = databaseManager;
        _logger = logger;
        _fileAreaService = fileAreaService;
        _tempCompressionPath = tempCompressionPath;
        
        // Map file extensions to compression formats
        _formatMap = new Dictionary<string, CompressionFormat>(StringComparer.OrdinalIgnoreCase)
        {
            { ".zip", CompressionFormat.ZIP },
            { ".gz", CompressionFormat.GZIP },
            { ".tar", CompressionFormat.TAR },
            { ".7z", CompressionFormat.SEVENZ }
        };
        
        // Ensure temp directory exists
        if (!Directory.Exists(_tempCompressionPath))
        {
            Directory.CreateDirectory(_tempCompressionPath);
        }
    }

    #region Single File Compression

    public async Task<CompressionResult> CompressFileAsync(string inputFilePath, string outputFilePath, CompressionFormat format)
    {
        var result = new CompressionResult();
        
        try
        {
            if (!File.Exists(inputFilePath))
            {
                result.ErrorMessage = "Input file not found";
                return result;
            }

            var inputFileInfo = new FileInfo(inputFilePath);
            result.OriginalSize = inputFileInfo.Length;

            switch (format)
            {
                case CompressionFormat.ZIP:
                    await CompressToZipAsync(new[] { inputFilePath }, outputFilePath);
                    break;
                case CompressionFormat.GZIP:
                    await CompressToGZipAsync(inputFilePath, outputFilePath);
                    break;
                default:
                    result.ErrorMessage = $"Compression format {format} not supported for single files";
                    return result;
            }

            if (File.Exists(outputFilePath))
            {
                var outputFileInfo = new FileInfo(outputFilePath);
                result.CompressedSize = outputFileInfo.Length;
                result.OutputPath = outputFilePath;
                result.Success = true;
                
                result.Entries.Add(new ArchiveEntry
                {
                    FileName = Path.GetFileName(inputFilePath),
                    RelativePath = Path.GetFileName(inputFilePath),
                    OriginalSize = result.OriginalSize,
                    CompressedSize = result.CompressedSize,
                    LastModified = inputFileInfo.LastWriteTime,
                    IsDirectory = false
                });

                _logger.Information("Compressed file {InputFile} to {OutputFile} using {Format}. " +
                                  "Size: {OriginalSize} -> {CompressedSize} bytes ({CompressionRatio:P1})",
                    inputFilePath, outputFilePath, format, result.OriginalSize, result.CompressedSize, 1 - result.CompressionRatio);
            }
            else
            {
                result.ErrorMessage = "Compression failed - output file not created";
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.Error(ex, "Error compressing file {InputFile}", inputFilePath);
        }

        return result;
    }

    public async Task<CompressionResult> DecompressFileAsync(string inputFilePath, string outputDirectory)
    {
        var result = new CompressionResult();
        
        try
        {
            if (!File.Exists(inputFilePath))
            {
                result.ErrorMessage = "Input archive not found";
                return result;
            }

            var format = await DetectFormatAsync(inputFilePath);
            if (format == null)
            {
                result.ErrorMessage = "Unknown or unsupported archive format";
                return result;
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var inputFileInfo = new FileInfo(inputFilePath);
            result.CompressedSize = inputFileInfo.Length;

            switch (format.Value)
            {
                case CompressionFormat.ZIP:
                    await ExtractZipAsync(inputFilePath, outputDirectory, result);
                    break;
                case CompressionFormat.GZIP:
                    await ExtractGZipAsync(inputFilePath, outputDirectory, result);
                    break;
                default:
                    result.ErrorMessage = $"Decompression format {format} not yet implemented";
                    return result;
            }

            result.Success = true;
            _logger.Information("Decompressed archive {InputFile} to {OutputDirectory}. " +
                              "Extracted {FileCount} files totaling {TotalSize} bytes",
                inputFilePath, outputDirectory, result.Entries.Count, result.OriginalSize);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.Error(ex, "Error decompressing file {InputFile}", inputFilePath);
        }

        return result;
    }

    #endregion

    #region Multiple Files/Directory Compression

    public async Task<CompressionResult> CompressDirectoryAsync(string directoryPath, string outputFilePath, CompressionFormat format)
    {
        var result = new CompressionResult();
        
        try
        {
            if (!Directory.Exists(directoryPath))
            {
                result.ErrorMessage = "Input directory not found";
                return result;
            }

            var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
            
            switch (format)
            {
                case CompressionFormat.ZIP:
                    await CompressDirectoryToZipAsync(directoryPath, outputFilePath, result);
                    break;
                default:
                    result.ErrorMessage = $"Directory compression format {format} not yet implemented";
                    return result;
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.Error(ex, "Error compressing directory {DirectoryPath}", directoryPath);
        }

        return result;
    }

    public async Task<CompressionResult> CompressFilesAsync(IEnumerable<string> filePaths, string outputFilePath, CompressionFormat format)
    {
        var result = new CompressionResult();
        
        try
        {
            var existingFiles = filePaths.Where(File.Exists).ToList();
            if (!existingFiles.Any())
            {
                result.ErrorMessage = "No valid input files found";
                return result;
            }

            switch (format)
            {
                case CompressionFormat.ZIP:
                    await CompressToZipAsync(existingFiles, outputFilePath);
                    break;
                default:
                    result.ErrorMessage = $"Multi-file compression format {format} not yet implemented";
                    return result;
            }

            // Calculate totals
            foreach (var filePath in existingFiles)
            {
                var fileInfo = new FileInfo(filePath);
                result.OriginalSize += fileInfo.Length;
                
                result.Entries.Add(new ArchiveEntry
                {
                    FileName = Path.GetFileName(filePath),
                    RelativePath = Path.GetFileName(filePath),
                    OriginalSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    IsDirectory = false
                });
            }

            if (File.Exists(outputFilePath))
            {
                var outputFileInfo = new FileInfo(outputFilePath);
                result.CompressedSize = outputFileInfo.Length;
                result.OutputPath = outputFilePath;
                result.Success = true;
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.Error(ex, "Error compressing files");
        }

        return result;
    }

    #endregion

    #region Archive Management

    public async Task<List<ArchiveEntry>> ListArchiveContentsAsync(string archivePath)
    {
        var entries = new List<ArchiveEntry>();
        
        try
        {
            var format = await DetectFormatAsync(archivePath);
            if (format == null)
            {
                return entries;
            }

            switch (format.Value)
            {
                case CompressionFormat.ZIP:
                    using (var archive = ZipFile.OpenRead(archivePath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            entries.Add(new ArchiveEntry
                            {
                                FileName = Path.GetFileName(entry.FullName),
                                RelativePath = entry.FullName,
                                OriginalSize = entry.Length,
                                CompressedSize = entry.CompressedLength,
                                LastModified = entry.LastWriteTime.DateTime,
                                IsDirectory = entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\")
                            });
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error listing archive contents for {ArchivePath}", archivePath);
        }

        return entries;
    }

    public async Task<CompressionResult> ExtractSpecificFilesAsync(string archivePath, IEnumerable<string> fileNames, string outputDirectory)
    {
        var result = new CompressionResult();
        
        try
        {
            var format = await DetectFormatAsync(archivePath);
            if (format == null)
            {
                result.ErrorMessage = "Unknown archive format";
                return result;
            }

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var fileNameSet = new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);

            switch (format.Value)
            {
                case CompressionFormat.ZIP:
                    using (var archive = ZipFile.OpenRead(archivePath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (fileNameSet.Contains(entry.Name) || fileNameSet.Contains(entry.FullName))
                            {
                                var destinationPath = Path.Combine(outputDirectory, entry.Name);
                                await using var entryStream = entry.Open();
                                await using var fileStream = File.Create(destinationPath);
                                await entryStream.CopyToAsync(fileStream);
                                
                                result.Entries.Add(new ArchiveEntry
                                {
                                    FileName = entry.Name,
                                    RelativePath = entry.FullName,
                                    OriginalSize = entry.Length,
                                    CompressedSize = entry.CompressedLength,
                                    LastModified = entry.LastWriteTime.DateTime,
                                    IsDirectory = false
                                });
                                
                                result.OriginalSize += entry.Length;
                            }
                        }
                    }
                    break;
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger.Error(ex, "Error extracting specific files from {ArchivePath}", archivePath);
        }

        return result;
    }

    public async Task<bool> ValidateArchiveAsync(string archivePath)
    {
        try
        {
            var format = await DetectFormatAsync(archivePath);
            if (format == null)
            {
                return false;
            }

            switch (format.Value)
            {
                case CompressionFormat.ZIP:
                    using (var archive = ZipFile.OpenRead(archivePath))
                    {
                        // Try to read each entry to validate integrity
                        foreach (var entry in archive.Entries)
                        {
                            using var stream = entry.Open();
                            // Just opening is usually enough to validate basic integrity
                        }
                    }
                    return true;
                    
                default:
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Archive validation failed for {ArchivePath}", archivePath);
            return false;
        }
    }

    #endregion

    #region BBS File Integration

    public async Task<BbsFileDto> CreateCompressedFileAreaArchiveAsync(int areaId, int userId, string? description = null)
    {
        var area = await _fileAreaService.GetFileAreaAsync(areaId);
        if (area == null)
        {
            throw new ArgumentException("File area not found", nameof(areaId));
        }

        var files = await _fileAreaService.GetFilesByAreaAsync(areaId, page: 1, pageSize: 1000);
        if (!files.Any())
        {
            throw new InvalidOperationException("No files found in the specified area");
        }

        var archiveName = $"{area.Name}_Archive_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        var tempArchivePath = Path.Combine(_tempCompressionPath, archiveName);
        
        // Get actual file paths
        var filePaths = files.Where(f => File.Exists(f.FilePath)).Select(f => f.FilePath).ToList();
        
        var compressionResult = await CompressFilesAsync(filePaths, tempArchivePath, CompressionFormat.ZIP);
        if (!compressionResult.Success)
        {
            throw new InvalidOperationException($"Failed to create archive: {compressionResult.ErrorMessage}");
        }

        // Upload the archive as a new file
        var fileData = await File.ReadAllBytesAsync(tempArchivePath);
        var uploadDto = new FileUploadDto
        {
            AreaId = areaId,
            FileName = archiveName,
            Description = description ?? $"Archive of {area.Name} area containing {files.Count()} files",
            Tags = new[] { "archive", "collection", area.Name.ToLowerInvariant() },
            FileData = fileData,
            MimeType = "application/zip"
        };

        var uploadedFile = await _fileAreaService.UploadFileAsync(uploadDto, userId);
        
        // Clean up temp file
        if (File.Exists(tempArchivePath))
        {
            File.Delete(tempArchivePath);
        }

        _logger.Information("Created compressed archive {ArchiveName} for area {AreaName} containing {FileCount} files",
            archiveName, area.Name, files.Count());

        return uploadedFile;
    }

    public async Task<BbsFileDto> CompressAndUploadFilesAsync(int areaId, IEnumerable<int> fileIds, int userId, string archiveName, string? description = null)
    {
        var fileIdList = fileIds.ToList();
        var files = new List<BbsFileDto>();
        
        foreach (var fileId in fileIdList)
        {
            var file = await _fileAreaService.GetFileAsync(fileId);
            if (file != null && File.Exists(file.FilePath))
            {
                files.Add(file);
            }
        }

        if (!files.Any())
        {
            throw new InvalidOperationException("No valid files found for compression");
        }

        var tempArchivePath = Path.Combine(_tempCompressionPath, archiveName);
        var filePaths = files.Select(f => f.FilePath).ToList();
        
        var compressionResult = await CompressFilesAsync(filePaths, tempArchivePath, CompressionFormat.ZIP);
        if (!compressionResult.Success)
        {
            throw new InvalidOperationException($"Failed to create archive: {compressionResult.ErrorMessage}");
        }

        var fileData = await File.ReadAllBytesAsync(tempArchivePath);
        var uploadDto = new FileUploadDto
        {
            AreaId = areaId,
            FileName = archiveName,
            Description = description ?? $"Archive containing {files.Count} selected files",
            Tags = new[] { "archive", "collection" },
            FileData = fileData,
            MimeType = "application/zip"
        };

        var uploadedFile = await _fileAreaService.UploadFileAsync(uploadDto, userId);
        
        // Clean up temp file
        if (File.Exists(tempArchivePath))
        {
            File.Delete(tempArchivePath);
        }

        return uploadedFile;
    }

    #endregion

    #region Format Detection and Support

    public async Task<CompressionFormat?> DetectFormatAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        if (_formatMap.TryGetValue(extension, out var format))
        {
            return await Task.FromResult(format);
        }

        // Could add magic number detection here for files without proper extensions
        return await Task.FromResult<CompressionFormat?>(null);
    }

    public async Task<bool> IsFormatSupportedAsync(CompressionFormat format)
    {
        var supportedFormats = new[] { CompressionFormat.ZIP, CompressionFormat.GZIP };
        return await Task.FromResult(supportedFormats.Contains(format));
    }

    public async Task<IEnumerable<CompressionFormat>> GetSupportedFormatsAsync()
    {
        return await Task.FromResult(new[] { CompressionFormat.ZIP, CompressionFormat.GZIP });
    }

    #endregion

    #region Private Helper Methods

    private Task CompressToZipAsync(IEnumerable<string> filePaths, string outputPath)
    {
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        
        foreach (var filePath in filePaths)
        {
            if (File.Exists(filePath))
            {
                var entryName = Path.GetFileName(filePath);
                archive.CreateEntryFromFile(filePath, entryName);
            }
        }
        
        return Task.CompletedTask;
    }

    private Task CompressDirectoryToZipAsync(string directoryPath, string outputPath, CompressionResult result)
    {
        ZipFile.CreateFromDirectory(directoryPath, outputPath);
        
        // Calculate statistics
        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            result.OriginalSize += fileInfo.Length;
            
            var relativePath = Path.GetRelativePath(directoryPath, file);
            result.Entries.Add(new ArchiveEntry
            {
                FileName = Path.GetFileName(file),
                RelativePath = relativePath,
                OriginalSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                IsDirectory = false
            });
        }

        if (File.Exists(outputPath))
        {
            var outputFileInfo = new FileInfo(outputPath);
            result.CompressedSize = outputFileInfo.Length;
            result.OutputPath = outputPath;
            result.Success = true;
        }
        
        return Task.CompletedTask;
    }

    private async Task CompressToGZipAsync(string inputPath, string outputPath)
    {
        await using var originalFileStream = File.OpenRead(inputPath);
        await using var compressedFileStream = File.Create(outputPath);
        await using var compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress);
        await originalFileStream.CopyToAsync(compressionStream);
    }

    private async Task ExtractZipAsync(string inputPath, string outputDirectory, CompressionResult result)
    {
        using var archive = ZipFile.OpenRead(inputPath);
        
        foreach (var entry in archive.Entries)
        {
            if (!entry.FullName.EndsWith("/")) // Skip directories
            {
                var destinationPath = Path.Combine(outputDirectory, entry.FullName);
                var destinationDir = Path.GetDirectoryName(destinationPath);
                
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                await using var entryStream = entry.Open();
                await using var fileStream = File.Create(destinationPath);
                await entryStream.CopyToAsync(fileStream);
                
                result.Entries.Add(new ArchiveEntry
                {
                    FileName = entry.Name,
                    RelativePath = entry.FullName,
                    OriginalSize = entry.Length,
                    CompressedSize = entry.CompressedLength,
                    LastModified = entry.LastWriteTime.DateTime,
                    IsDirectory = false
                });
                
                result.OriginalSize += entry.Length;
            }
        }
    }

    private async Task ExtractGZipAsync(string inputPath, string outputDirectory, CompressionResult result)
    {
        var outputFileName = Path.GetFileNameWithoutExtension(inputPath);
        var outputPath = Path.Combine(outputDirectory, outputFileName);
        
        await using var compressedFileStream = File.OpenRead(inputPath);
        await using var decompressionStream = new GZipStream(compressedFileStream, CompressionMode.Decompress);
        await using var outputFileStream = File.Create(outputPath);
        await decompressionStream.CopyToAsync(outputFileStream);
        
        var outputFileInfo = new FileInfo(outputPath);
        result.OriginalSize = outputFileInfo.Length;
        
        result.Entries.Add(new ArchiveEntry
        {
            FileName = outputFileName,
            RelativePath = outputFileName,
            OriginalSize = outputFileInfo.Length,
            CompressedSize = new FileInfo(inputPath).Length,
            LastModified = outputFileInfo.LastWriteTime,
            IsDirectory = false
        });
    }

    #endregion
}
