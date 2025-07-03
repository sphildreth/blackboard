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
    public class FileTransferServiceTests
    {
        private readonly Mock<IDatabaseManager> _mockDatabaseManager;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IFileAreaService> _mockFileAreaService;
        private readonly FileTransferService _service;

        public FileTransferServiceTests()
        {
            _mockDatabaseManager = new Mock<IDatabaseManager>();
            _mockLogger = new Mock<ILogger>();
            _mockFileAreaService = new Mock<IFileAreaService>();
            _service = new FileTransferService(_mockDatabaseManager.Object, _mockLogger.Object, _mockFileAreaService.Object);
        }

        [Fact]
        public async Task StartDownloadSessionAsync_ValidFile_ReturnsSession()
        {
            // Arrange
            var fileId = 1;
            var userId = 100;
            var file = new BbsFileDto 
            { 
                Id = fileId, 
                AreaId = 1, 
                OriginalFileName = "test.exe", 
                Size = 1024 
            };

            _mockFileAreaService.Setup(x => x.GetFileAsync(fileId))
                               .ReturnsAsync(file);
            _mockFileAreaService.Setup(x => x.CanUserAccessAreaAsync(userId, file.AreaId, false))
                               .ReturnsAsync(true);

            // Act
            var session = await _service.StartDownloadSessionAsync(fileId, userId, FileTransferProtocol.ZMODEM);

            // Assert
            Assert.NotNull(session);
            Assert.Equal(userId, session.UserId);
            Assert.Equal(fileId, session.FileId);
            Assert.Equal(FileTransferProtocol.ZMODEM, session.Protocol);
            Assert.False(session.IsUpload);
            Assert.Equal("test.exe", session.FileName);
            Assert.Equal(1024, session.FileSize);
        }

        [Fact]
        public async Task StartDownloadSessionAsync_FileNotFound_ThrowsArgumentException()
        {
            // Arrange
            var fileId = 999;
            var userId = 100;

            _mockFileAreaService.Setup(x => x.GetFileAsync(fileId))
                               .ReturnsAsync((BbsFileDto?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.StartDownloadSessionAsync(fileId, userId, FileTransferProtocol.ZMODEM));
        }

        [Fact]
        public async Task StartDownloadSessionAsync_NoAccess_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var fileId = 1;
            var userId = 100;
            var file = new BbsFileDto 
            { 
                Id = fileId, 
                AreaId = 1, 
                OriginalFileName = "test.exe", 
                Size = 1024 
            };

            _mockFileAreaService.Setup(x => x.GetFileAsync(fileId))
                               .ReturnsAsync(file);
            _mockFileAreaService.Setup(x => x.CanUserAccessAreaAsync(userId, file.AreaId, false))
                               .ReturnsAsync(false);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 
                _service.StartDownloadSessionAsync(fileId, userId, FileTransferProtocol.ZMODEM));
        }

        [Fact]
        public async Task StartUploadSessionAsync_ValidArea_ReturnsSession()
        {
            // Arrange
            var areaId = 1;
            var userId = 100;
            var fileName = "upload.exe";
            var fileSize = 2048L;
            var area = new FileAreaDto 
            { 
                Id = areaId, 
                Name = "Games", 
                MaxFileSize = 10485760 // 10MB
            };

            _mockFileAreaService.Setup(x => x.CanUserAccessAreaAsync(userId, areaId, true))
                               .ReturnsAsync(true);
            _mockFileAreaService.Setup(x => x.GetFileAreaAsync(areaId))
                               .ReturnsAsync(area);

            // Act
            var session = await _service.StartUploadSessionAsync(areaId, fileName, fileSize, userId, FileTransferProtocol.YMODEM);

            // Assert
            Assert.NotNull(session);
            Assert.Equal(userId, session.UserId);
            Assert.Equal(0, session.FileId); // Not set until upload completes
            Assert.Equal(FileTransferProtocol.YMODEM, session.Protocol);
            Assert.True(session.IsUpload);
            Assert.Equal(fileName, session.FileName);
            Assert.Equal(fileSize, session.FileSize);
        }

        [Fact]
        public async Task StartUploadSessionAsync_FileTooLarge_ThrowsArgumentException()
        {
            // Arrange
            var areaId = 1;
            var userId = 100;
            var fileName = "large.exe";
            var fileSize = 20971520L; // 20MB
            var area = new FileAreaDto 
            { 
                Id = areaId, 
                Name = "Games", 
                MaxFileSize = 10485760 // 10MB
            };

            _mockFileAreaService.Setup(x => x.CanUserAccessAreaAsync(userId, areaId, true))
                               .ReturnsAsync(true);
            _mockFileAreaService.Setup(x => x.GetFileAreaAsync(areaId))
                               .ReturnsAsync(area);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.StartUploadSessionAsync(areaId, fileName, fileSize, userId, FileTransferProtocol.YMODEM));
        }

        [Fact]
        public async Task UpdateProgressAsync_ValidSession_UpdatesProgress()
        {
            // Arrange
            var fileId = 1;
            var userId = 100;
            var file = new BbsFileDto 
            { 
                Id = fileId, 
                AreaId = 1, 
                OriginalFileName = "test.exe", 
                Size = 1024 
            };

            _mockFileAreaService.Setup(x => x.GetFileAsync(fileId))
                               .ReturnsAsync(file);
            _mockFileAreaService.Setup(x => x.CanUserAccessAreaAsync(userId, file.AreaId, false))
                               .ReturnsAsync(true);

            var session = await _service.StartDownloadSessionAsync(fileId, userId, FileTransferProtocol.ZMODEM);
            var sessionId = session.SessionId;

            // Act
            var result = await _service.UpdateProgressAsync(sessionId, 512);

            // Assert
            Assert.True(result);
            
            var updatedSession = await _service.GetSessionAsync(sessionId);
            Assert.NotNull(updatedSession);
            Assert.Equal(512, updatedSession.BytesTransferred);
            Assert.Equal(50.0, updatedSession.ProgressPercentage);
        }

        [Fact]
        public async Task CompleteSessionAsync_ValidSession_CompletesSession()
        {
            // Arrange
            var fileId = 1;
            var userId = 100;
            var file = new BbsFileDto 
            { 
                Id = fileId, 
                AreaId = 1, 
                OriginalFileName = "test.exe", 
                Size = 1024 
            };

            _mockFileAreaService.Setup(x => x.GetFileAsync(fileId))
                               .ReturnsAsync(file);
            _mockFileAreaService.Setup(x => x.CanUserAccessAreaAsync(userId, file.AreaId, false))
                               .ReturnsAsync(true);

            _mockDatabaseManager.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
                               .ReturnsAsync(1);

            var session = await _service.StartDownloadSessionAsync(fileId, userId, FileTransferProtocol.ZMODEM);
            var sessionId = session.SessionId;

            // Act
            var result = await _service.CompleteSessionAsync(sessionId, true);

            // Assert
            Assert.True(result);
            
            var completedSession = await _service.GetSessionAsync(sessionId);
            Assert.Null(completedSession); // Should be removed from active sessions
        }

        [Fact]
        public async Task GetConcurrentTransferCountAsync_MultipleActiveSessions_ReturnsCorrectCount()
        {
            // Arrange
            var userId = 100;
            var otherUserId = 200;
            
            var file1 = new BbsFileDto { Id = 1, AreaId = 1, OriginalFileName = "test1.exe", Size = 1024 };
            var file2 = new BbsFileDto { Id = 2, AreaId = 1, OriginalFileName = "test2.exe", Size = 2048 };
            var file3 = new BbsFileDto { Id = 3, AreaId = 1, OriginalFileName = "test3.exe", Size = 512 };

            _mockFileAreaService.Setup(x => x.GetFileAsync(It.IsAny<int>()))
                               .ReturnsAsync((int id) => id switch
                               {
                                   1 => file1,
                                   2 => file2,
                                   3 => file3,
                                   _ => null
                               });
            _mockFileAreaService.Setup(x => x.CanUserAccessAreaAsync(It.IsAny<int>(), It.IsAny<int>(), false))
                               .ReturnsAsync(true);

            // Start sessions for the user
            await _service.StartDownloadSessionAsync(1, userId, FileTransferProtocol.ZMODEM);
            await _service.StartDownloadSessionAsync(2, userId, FileTransferProtocol.XMODEM);
            
            // Start session for another user
            await _service.StartDownloadSessionAsync(3, otherUserId, FileTransferProtocol.YMODEM);

            // Act
            var userCount = await _service.GetConcurrentTransferCountAsync(userId);
            var otherUserCount = await _service.GetConcurrentTransferCountAsync(otherUserId);

            // Assert
            Assert.Equal(2, userCount);
            Assert.Equal(1, otherUserCount);
        }

        [Fact]
        public async Task IsProtocolSupportedAsync_AllProtocols_ReturnsTrue()
        {
            // Act & Assert
            Assert.True(await _service.IsProtocolSupportedAsync(FileTransferProtocol.ZMODEM));
            Assert.True(await _service.IsProtocolSupportedAsync(FileTransferProtocol.XMODEM));
            Assert.True(await _service.IsProtocolSupportedAsync(FileTransferProtocol.YMODEM));
            Assert.True(await _service.IsProtocolSupportedAsync(FileTransferProtocol.HTTP));
        }

        [Fact]
        public async Task GetProtocolInstructionsAsync_ReturnsCorrectInstructions()
        {
            // Act
            var zmodemDownload = await _service.GetProtocolInstructionsAsync(FileTransferProtocol.ZMODEM, false);
            var xmodemUpload = await _service.GetProtocolInstructionsAsync(FileTransferProtocol.XMODEM, true);

            // Assert
            Assert.Contains("ZMODEM", zmodemDownload);
            Assert.Contains("download", zmodemDownload);
            Assert.Contains("XMODEM", xmodemUpload);
            Assert.Contains("upload", xmodemUpload);
        }

        [Fact]
        public async Task GenerateZmodemInitCommandAsync_ReturnsCorrectCommand()
        {
            // Arrange
            var session = new FileTransferSession
            {
                IsUpload = false,
                FileName = "test.exe"
            };

            // Act
            var command = await _service.GenerateZmodemInitCommandAsync(session);

            // Assert
            var commandString = System.Text.Encoding.ASCII.GetString(command);
            Assert.Contains("sz", commandString);
            Assert.Contains("test.exe", commandString);
        }

        [Fact]
        public async Task GenerateXYmodemInitCommandAsync_ReturnsCorrectCommand()
        {
            // Arrange
            var session = new FileTransferSession
            {
                Protocol = FileTransferProtocol.XMODEM,
                IsUpload = true,
                FileName = "upload.exe"
            };

            // Act
            var command = await _service.GenerateXYmodemInitCommandAsync(session);

            // Assert
            var commandString = System.Text.Encoding.ASCII.GetString(command);
            Assert.Contains("rx", commandString);
        }
    }
}
