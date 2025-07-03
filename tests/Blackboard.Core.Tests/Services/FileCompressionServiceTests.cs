using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Serilog;
using Blackboard.Core.Services;
using Blackboard.Core.DTOs;
using Blackboard.Data;

namespace Blackboard.Core.Tests.Services
{
    public class FileCompressionServiceTests : IDisposable
    {
        private readonly Mock<IDatabaseManager> _mockDatabaseManager;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IFileAreaService> _mockFileAreaService;
        private readonly FileCompressionService _service;
        private readonly string _tempTestPath;

        public FileCompressionServiceTests()
        {
            _mockDatabaseManager = new Mock<IDatabaseManager>();
            _mockLogger = new Mock<ILogger>();
            _mockFileAreaService = new Mock<IFileAreaService>();
            
            _tempTestPath = Path.Combine(Path.GetTempPath(), "BlackboardTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempTestPath);
            
            _service = new FileCompressionService(_mockDatabaseManager.Object, _mockLogger.Object, 
                _mockFileAreaService.Object, _tempTestPath);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempTestPath))
            {
                Directory.Delete(_tempTestPath, true);
            }
        }

        [Fact]
        public async Task DetectFormatAsync_ZipFile_ReturnsZipFormat()
        {
            // Act
            var format = await _service.DetectFormatAsync("test.zip");

            // Assert
            Assert.Equal(CompressionFormat.ZIP, format);
        }

        [Fact]
        public async Task DetectFormatAsync_GzipFile_ReturnsGzipFormat()
        {
            // Act
            var format = await _service.DetectFormatAsync("test.gz");

            // Assert
            Assert.Equal(CompressionFormat.GZIP, format);
        }

        [Fact]
        public async Task DetectFormatAsync_UnknownExtension_ReturnsNull()
        {
            // Act
            var format = await _service.DetectFormatAsync("test.unknown");

            // Assert
            Assert.Null(format);
        }

        [Fact]
        public async Task IsFormatSupportedAsync_SupportedFormats_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(await _service.IsFormatSupportedAsync(CompressionFormat.ZIP));
            Assert.True(await _service.IsFormatSupportedAsync(CompressionFormat.GZIP));
        }

        [Fact]
        public async Task IsFormatSupportedAsync_UnsupportedFormat_ReturnsFalse()
        {
            // Act & Assert
            Assert.False(await _service.IsFormatSupportedAsync(CompressionFormat.SEVENZ));
            Assert.False(await _service.IsFormatSupportedAsync(CompressionFormat.TAR));
        }

        [Fact]
        public async Task GetSupportedFormatsAsync_ReturnsCorrectFormats()
        {
            // Act
            var formats = (await _service.GetSupportedFormatsAsync()).ToList();

            // Assert
            Assert.Contains(CompressionFormat.ZIP, formats);
            Assert.Contains(CompressionFormat.GZIP, formats);
            Assert.Equal(2, formats.Count);
        }

        [Fact]
        public async Task CompressFileAsync_ValidZipFile_SuccessfulCompression()
        {
            // Arrange
            var inputFile = Path.Combine(_tempTestPath, "input.txt");
            var outputFile = Path.Combine(_tempTestPath, "output.zip");
            var testContent = "This is test content for compression.";
            
            await File.WriteAllTextAsync(inputFile, testContent);

            // Act
            var result = await _service.CompressFileAsync(inputFile, outputFile, CompressionFormat.ZIP);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.True(result.OriginalSize > 0);
            Assert.True(result.CompressedSize > 0);
            Assert.Single(result.Entries);
            Assert.Equal("input.txt", result.Entries.First().FileName);
        }

        [Fact]
        public async Task CompressFileAsync_NonExistentFile_ReturnsFailure()
        {
            // Arrange
            var inputFile = Path.Combine(_tempTestPath, "nonexistent.txt");
            var outputFile = Path.Combine(_tempTestPath, "output.zip");

            // Act
            var result = await _service.CompressFileAsync(inputFile, outputFile, CompressionFormat.ZIP);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Input file not found", result.ErrorMessage);
        }

        [Fact]
        public async Task CompressFilesAsync_MultipleFiles_SuccessfulCompression()
        {
            // Arrange
            var file1 = Path.Combine(_tempTestPath, "file1.txt");
            var file2 = Path.Combine(_tempTestPath, "file2.txt");
            var outputFile = Path.Combine(_tempTestPath, "archive.zip");
            
            await File.WriteAllTextAsync(file1, "Content of file 1");
            await File.WriteAllTextAsync(file2, "Content of file 2");

            var filePaths = new[] { file1, file2 };

            // Act
            var result = await _service.CompressFilesAsync(filePaths, outputFile, CompressionFormat.ZIP);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.OutputPath);
            Assert.True(File.Exists(result.OutputPath));
            Assert.Equal(2, result.Entries.Count);
            Assert.Contains(result.Entries, e => e.FileName == "file1.txt");
            Assert.Contains(result.Entries, e => e.FileName == "file2.txt");
        }

        [Fact]
        public async Task ListArchiveContentsAsync_ValidZipFile_ReturnsEntries()
        {
            // Arrange
            var file1 = Path.Combine(_tempTestPath, "file1.txt");
            var file2 = Path.Combine(_tempTestPath, "file2.txt");
            var archiveFile = Path.Combine(_tempTestPath, "test.zip");
            
            await File.WriteAllTextAsync(file1, "Content 1");
            await File.WriteAllTextAsync(file2, "Content 2");

            await _service.CompressFilesAsync(new[] { file1, file2 }, archiveFile, CompressionFormat.ZIP);

            // Act
            var entries = await _service.ListArchiveContentsAsync(archiveFile);

            // Assert
            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.FileName == "file1.txt");
            Assert.Contains(entries, e => e.FileName == "file2.txt");
            Assert.All(entries, e => Assert.False(e.IsDirectory));
        }

        [Fact]
        public async Task ValidateArchiveAsync_ValidZipFile_ReturnsTrue()
        {
            // Arrange
            var inputFile = Path.Combine(_tempTestPath, "input.txt");
            var archiveFile = Path.Combine(_tempTestPath, "valid.zip");
            
            await File.WriteAllTextAsync(inputFile, "Test content");
            await _service.CompressFileAsync(inputFile, archiveFile, CompressionFormat.ZIP);

            // Act
            var isValid = await _service.ValidateArchiveAsync(archiveFile);

            // Assert
            Assert.True(isValid);
        }

        [Fact]
        public async Task ValidateArchiveAsync_InvalidFile_ReturnsFalse()
        {
            // Arrange
            var fakeArchive = Path.Combine(_tempTestPath, "fake.zip");
            await File.WriteAllTextAsync(fakeArchive, "This is not a valid ZIP file");

            // Act
            var isValid = await _service.ValidateArchiveAsync(fakeArchive);

            // Assert
            Assert.False(isValid);
        }

        [Fact]
        public async Task CompressAndUploadFilesAsync_ValidFiles_CreatesAndUploadsArchive()
        {
            // Arrange
            var areaId = 1;
            var userId = 100;
            var fileIds = new[] { 1, 2 };
            var archiveName = "test_archive.zip";

            var file1 = new BbsFileDto { Id = 1, FilePath = Path.Combine(_tempTestPath, "file1.txt") };
            var file2 = new BbsFileDto { Id = 2, FilePath = Path.Combine(_tempTestPath, "file2.txt") };
            
            await File.WriteAllTextAsync(file1.FilePath, "Content 1");
            await File.WriteAllTextAsync(file2.FilePath, "Content 2");

            var uploadedFile = new BbsFileDto 
            { 
                Id = 99, 
                FileName = archiveName, 
                AreaId = areaId,
                Size = 1024 
            };

            _mockFileAreaService.Setup(x => x.GetFileAsync(1)).ReturnsAsync(file1);
            _mockFileAreaService.Setup(x => x.GetFileAsync(2)).ReturnsAsync(file2);
            _mockFileAreaService.Setup(x => x.UploadFileAsync(It.IsAny<FileUploadDto>(), userId))
                               .ReturnsAsync(uploadedFile);

            // Act
            var result = await _service.CompressAndUploadFilesAsync(areaId, fileIds, userId, archiveName);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(archiveName, result.FileName);
            Assert.Equal(areaId, result.AreaId);
            
            // Verify upload was called with correct parameters
            _mockFileAreaService.Verify(x => x.UploadFileAsync(
                It.Is<FileUploadDto>(dto => 
                    dto.AreaId == areaId && 
                    dto.FileName == archiveName && 
                    dto.MimeType == "application/zip" &&
                    dto.Tags.Contains("archive")), 
                userId), Times.Once);
        }

        [Fact]
        public async Task CreateCompressedFileAreaArchiveAsync_ValidArea_CreatesArchive()
        {
            // Arrange
            var areaId = 1;
            var userId = 100;
            var area = new FileAreaDto { Id = areaId, Name = "TestArea" };
            
            var file1 = new BbsFileDto { Id = 1, FilePath = Path.Combine(_tempTestPath, "file1.txt") };
            var file2 = new BbsFileDto { Id = 2, FilePath = Path.Combine(_tempTestPath, "file2.txt") };
            
            await File.WriteAllTextAsync(file1.FilePath, "Content 1");
            await File.WriteAllTextAsync(file2.FilePath, "Content 2");

            var files = new[] { file1, file2 };
            var uploadedArchive = new BbsFileDto 
            { 
                Id = 99, 
                FileName = "TestArea_Archive_20231201_120000.zip", 
                AreaId = areaId 
            };

            _mockFileAreaService.Setup(x => x.GetFileAreaAsync(areaId)).ReturnsAsync(area);
            _mockFileAreaService.Setup(x => x.GetFilesByAreaAsync(areaId, 1, 1000)).ReturnsAsync(files);
            _mockFileAreaService.Setup(x => x.UploadFileAsync(It.IsAny<FileUploadDto>(), userId))
                               .ReturnsAsync(uploadedArchive);

            // Act
            var result = await _service.CreateCompressedFileAreaArchiveAsync(areaId, userId);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("TestArea_Archive", result.FileName);
            Assert.Equal(areaId, result.AreaId);
            
            // Verify upload was called
            _mockFileAreaService.Verify(x => x.UploadFileAsync(It.IsAny<FileUploadDto>(), userId), Times.Once);
        }
    }
}
