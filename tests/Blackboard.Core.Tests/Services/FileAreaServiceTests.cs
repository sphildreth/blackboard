using System;
using System.Collections.Generic;
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
    public class FileAreaServiceTests
    {
        private readonly Mock<IDatabaseManager> _mockDatabaseManager;
        private readonly Mock<ILogger> _mockLogger;
        private readonly FileAreaService _service;

        public FileAreaServiceTests()
        {
            _mockDatabaseManager = new Mock<IDatabaseManager>();
            _mockLogger = new Mock<ILogger>();
            _service = new FileAreaService(_mockDatabaseManager.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAllFileAreasAsync_ReturnsAreas()
        {
            // Arrange
            var areas = new List<FileAreaDto>
            {
                new FileAreaDto { Id = 1, Name = "Games", Description = "Game files", IsActive = true, FileCount = 5, TotalSize = 1024L }
            };
            _mockDatabaseManager.Setup(x => x.QueryAsync<FileAreaDto>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(areas);

            // Act
            var result = await _service.GetAllFileAreasAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("Games", result.First().Name);
        }

        [Fact]
        public async Task GetFileAreaAsync_ReturnsArea_WhenExists()
        {
            // Arrange
            var area = new FileAreaDto 
            { 
                Id = 1, 
                Name = "Games", 
                Description = "Game files", 
                IsActive = true, 
                FileCount = 5, 
                TotalSize = 1024L 
            };
            _mockDatabaseManager.Setup(x => x.QueryFirstOrDefaultAsync<FileAreaDto>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(area);

            // Act
            var result = await _service.GetFileAreaAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Games", result.Name);
            Assert.Equal(5, result.FileCount);
        }

        [Fact]
        public async Task GetFileAreaAsync_ReturnsNull_WhenNotExists()
        {
            // Arrange
            _mockDatabaseManager.Setup(x => x.QueryFirstOrDefaultAsync<FileAreaDto>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync((FileAreaDto?)null);

            // Act
            var result = await _service.GetFileAreaAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetActiveFileAreasAsync_ReturnsOnlyActiveAreas()
        {
            // Arrange
            var areas = new List<FileAreaDto>
            {
                new FileAreaDto { Id = 1, Name = "Games", IsActive = true },
                new FileAreaDto { Id = 2, Name = "Utils", IsActive = true }
            };
            _mockDatabaseManager.Setup(x => x.QueryAsync<FileAreaDto>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(areas);

            // Act
            var result = await _service.GetActiveFileAreasAsync();

            // Assert
            Assert.Equal(2, result.Count());
            Assert.All(result, area => Assert.True(area.IsActive));
        }

        [Fact]
        public async Task SearchFilesAsync_ReturnsResults_WithValidSearch()
        {
            // Arrange
            var files = new List<BbsFileDto>
            {
                new BbsFileDto { Id = 1, FileName = "test.exe", AreaName = "Games", IsActive = true, IsApproved = true }
            };
            var totalCount = 1;
            
            _mockDatabaseManager.Setup(x => x.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(totalCount);
            _mockDatabaseManager.Setup(x => x.QueryAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(new List<dynamic> 
                               { 
                                   new { 
                                       Id = 1, 
                                       AreaId = 1,
                                       AreaName = "Games", 
                                       FileName = "test.exe",
                                       OriginalFileName = "test.exe",
                                       Description = "Test file",
                                       FilePath = "/path/test.exe",
                                       Size = 1024L,
                                       MimeType = "application/octet-stream",
                                       UploadDate = DateTime.UtcNow,
                                       UploaderId = 1,
                                       UploaderHandle = "testuser",
                                       DownloadCount = 0,
                                       LastDownloadAt = (DateTime?)null,
                                       IsApproved = true,
                                       ApprovedBy = (int?)null,
                                       ApproverHandle = (string?)null,
                                       ApprovedAt = (DateTime?)null,
                                       IsActive = true,
                                       ExpiresAt = (DateTime?)null,
                                       AverageRating = 0.0,
                                       RatingCount = 0,
                                       Checksum = "abc123",
                                       Tags = (string?)null
                                   }
                               });

            // Act
            var result = await _service.SearchFilesAsync("test");

            // Assert
            Assert.Single(result.Files);
            Assert.Equal(1, result.TotalCount);
            Assert.Equal("test.exe", result.Files.First().FileName);
        }

        [Fact]
        public async Task GetFileAreaStatisticsAsync_ReturnsValidStatistics()
        {
            // Arrange
            var stats = new FileAreaStatisticsDto
            {
                TotalAreas = 5,
                ActiveAreas = 4,
                TotalFiles = 100,
                ApprovedFiles = 95,
                PendingApproval = 5,
                TotalFileSize = 1024000L
            };
            
            var mockFileAreas = new List<FileAreaDto>
            {
                new FileAreaDto { Id = 1, Name = "Area1", FileCount = 10, TotalSize = 1000 },
                new FileAreaDto { Id = 2, Name = "Area2", FileCount = 5, TotalSize = 500 }
            };
            
            // Mock the basic statistics query
            _mockDatabaseManager.Setup(x => x.QueryFirstAsync<FileAreaStatisticsDto>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(stats);
            
            // Mock the file areas query (for MostActiveAreas)
            _mockDatabaseManager.Setup(x => x.QueryAsync<FileAreaDto>(It.Is<string>(sql => sql.Contains("SELECT fa.Id, fa.Name")), It.IsAny<object>()))
                               .ReturnsAsync(mockFileAreas);
            
            // Mock the SearchFilesAsync calls (for GetMostDownloadedFilesAsync and GetRecentUploadsAsync)
            // Count query for SearchFilesAsync
            _mockDatabaseManager.Setup(x => x.QueryFirstAsync<int>(It.Is<string>(sql => sql.Contains("SELECT COUNT(*)")), It.IsAny<object>()))
                               .ReturnsAsync(0);
            
            // Data query for SearchFilesAsync  
            _mockDatabaseManager.Setup(x => x.QueryAsync<dynamic>(It.Is<string>(sql => sql.Contains("SELECT f.*, fa.Name as AreaName")), It.IsAny<object>()))
                               .ReturnsAsync(new List<dynamic>());

            // Act
            var result = await _service.GetFileAreaStatisticsAsync();

            // Assert
            Assert.Equal(5, result.TotalAreas);
            Assert.Equal(4, result.ActiveAreas);
            Assert.Equal(100, result.TotalFiles);
            Assert.Equal(95, result.ApprovedFiles);
            Assert.Equal(5, result.PendingApproval);
        }

        [Fact]
        public async Task CanUserAccessAreaAsync_ReturnsFalse_WhenInsufficientLevel()
        {
            // Arrange
            var accessResult = new 
            { 
                RequiredLevel = 50, 
                UploadLevel = 75, 
                IsActive = true, 
                AllowUploads = true, 
                AllowDownloads = true, 
                SecurityLevel = 25 
            };
            
            _mockDatabaseManager.Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(accessResult);

            // Act
            var result = await _service.CanUserAccessAreaAsync(1, 1, isUpload: false);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CanUserAccessAreaAsync_ReturnsTrue_WhenSufficientLevel()
        {
            // Arrange
            dynamic accessResult = new System.Dynamic.ExpandoObject();
            accessResult.RequiredLevel = 50;
            accessResult.UploadLevel = 75;
            accessResult.IsActive = true;
            accessResult.AllowUploads = true;
            accessResult.AllowDownloads = true;
            accessResult.SecurityLevel = 100;
            
            _mockDatabaseManager.Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
                               .Returns(Task.FromResult<dynamic>(accessResult));

            // Act
            var result = await _service.CanUserAccessAreaAsync(1, 1, isUpload: false);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void FileAreaService_Construction_Success()
        {
            // Test that the service can be constructed successfully
            var service = new FileAreaService(_mockDatabaseManager.Object, _mockLogger.Object);
            Assert.NotNull(service);
        }
    }
}
