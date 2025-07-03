using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Blackboard.Core;
using Blackboard.Core.Configuration;
using Blackboard.Core.Services;
using Blackboard.Core.DTOs;

namespace Blackboard.Core.Tests.Integration
{
    public class Phase5IntegrationTests : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ServiceManager _serviceManager;
        private readonly string _tempTestPath;

        public Phase5IntegrationTests()
        {
            var services = new ServiceCollection();
            
            // Create test configuration
            var config = new SystemConfiguration
            {
                Database = new DatabaseSettings
                {
                    ConnectionString = "Data Source=:memory:",
                    EnableWalMode = false,
                    ConnectionTimeoutSeconds = 30
                },
                Security = new SecuritySettings(),
                Network = new NetworkSettings(),
                Logging = new LoggingSettings()
            };

            services.AddBlackboardCore(config);
            _serviceProvider = services.BuildServiceProvider();
            _serviceManager = new ServiceManager(_serviceProvider);

            _tempTestPath = Path.Combine(Path.GetTempPath(), "BlackboardPhase5Tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempTestPath);

            // Initialize database
            _serviceManager.DatabaseManager.InitializeAsync().Wait();
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
            if (Directory.Exists(_tempTestPath))
            {
                Directory.Delete(_tempTestPath, true);
            }
        }

        [Fact]
        public void AllPhase5Services_ShouldBeAvailable()
        {
            // Verify all Phase 5 services are properly registered and available
            _serviceManager.FileAreaService.Should().NotBeNull();
            _serviceManager.FileTransferService.Should().NotBeNull();
            _serviceManager.FileCompressionService.Should().NotBeNull();
        }

        [Fact]
        public async Task FileAreaService_ShouldSupportCompleteWorkflow()
        {
            var fileAreaService = _serviceManager.FileAreaService;

            // 1. Create a file area
            var newArea = new FileAreaDto
            {
                Name = "Test Area",
                Description = "Integration test area",
                Path = "test",
                RequiredLevel = 10,
                UploadLevel = 20,
                IsActive = true,
                MaxFileSize = 1024 * 1024, // 1MB
                AllowUploads = true,
                AllowDownloads = true
            };

            var createdArea = await fileAreaService.CreateFileAreaAsync(newArea);
            createdArea.Should().NotBeNull();
            createdArea.Id.Should().BeGreaterThan(0);
            createdArea.Name.Should().Be("Test Area");

            // 2. Verify area can be retrieved
            var retrievedArea = await fileAreaService.GetFileAreaAsync(createdArea.Id);
            retrievedArea.Should().NotBeNull();
            retrievedArea!.Name.Should().Be("Test Area");

            // 3. Get statistics
            var stats = await fileAreaService.GetFileAreaStatisticsAsync();
            stats.Should().NotBeNull();
            stats.TotalAreas.Should().BeGreaterThan(0);

            // 4. Update the area
            retrievedArea.Description = "Updated description";
            var updatedArea = await fileAreaService.UpdateFileAreaAsync(retrievedArea);
            updatedArea.Description.Should().Be("Updated description");

            // 5. Search for files (should be empty initially)
            var searchResult = await fileAreaService.SearchFilesAsync();
            searchResult.Should().NotBeNull();
            searchResult.Files.Should().BeEmpty();
        }

        [Fact]
        public async Task FileTransferService_ShouldSupportProtocolNegotiation()
        {
            var transferService = _serviceManager.FileTransferService;

            // Test protocol support
            var protocols = new[]
            {
                FileTransferProtocol.ZMODEM,
                FileTransferProtocol.XMODEM,
                FileTransferProtocol.YMODEM,
                FileTransferProtocol.HTTP
            };

            foreach (var protocol in protocols)
            {
                var isSupported = await transferService.IsProtocolSupportedAsync(protocol);
                isSupported.Should().BeTrue($"Protocol {protocol} should be supported");

                var instructions = await transferService.GetProtocolInstructionsAsync(protocol, false);
                instructions.Should().NotBeNullOrEmpty($"Instructions for {protocol} should be available");
                instructions.Should().Contain(protocol.ToString());
            }
        }

        [Fact]
        public async Task FileCompressionService_ShouldSupportVariousFormats()
        {
            var compressionService = _serviceManager.FileCompressionService;

            // Test format detection
            var formats = new[]
            {
                ("test.zip", CompressionFormat.ZIP),
                ("test.gz", CompressionFormat.GZIP),
                ("test.tar", CompressionFormat.TAR),
                ("test.7z", CompressionFormat.SEVENZ)
            };

            foreach (var (filename, expectedFormat) in formats)
            {
                var detectedFormat = await compressionService.DetectFormatAsync(filename);
                detectedFormat.Should().Be(expectedFormat, $"Format detection for {filename} should work");
            }

            // Test supported formats
            var supportedFormats = (await compressionService.GetSupportedFormatsAsync()).ToList();
            supportedFormats.Should().Contain(CompressionFormat.ZIP);
            supportedFormats.Should().Contain(CompressionFormat.GZIP);

            // Test format support checking
            var zipSupported = await compressionService.IsFormatSupportedAsync(CompressionFormat.ZIP);
            zipSupported.Should().BeTrue("ZIP format should be supported");

            var gzipSupported = await compressionService.IsFormatSupportedAsync(CompressionFormat.GZIP);
            gzipSupported.Should().BeTrue("GZIP format should be supported");
        }

        [Fact]
        public async Task IntegratedWorkflow_FileAreaToCompressionToTransfer_ShouldWork()
        {
            // This test demonstrates the complete Phase 5 workflow:
            // 1. Create file area
            // 2. Upload files (simulated)
            // 3. Compress files into archive
            // 4. Set up transfer

            var fileAreaService = _serviceManager.FileAreaService;
            var compressionService = _serviceManager.FileCompressionService;
            var transferService = _serviceManager.FileTransferService;

            // 1. Create file area
            var area = new FileAreaDto
            {
                Name = "Integration Test Area",
                Description = "For integration testing",
                Path = "integration_test",
                RequiredLevel = 0,
                UploadLevel = 0,
                IsActive = true,
                MaxFileSize = 5 * 1024 * 1024, // 5MB
                AllowUploads = true,
                AllowDownloads = true
            };

            var createdArea = await fileAreaService.CreateFileAreaAsync(area);
            createdArea.Should().NotBeNull();

            // 2. Create test files for compression
            var testFile1 = Path.Combine(_tempTestPath, "test1.txt");
            var testFile2 = Path.Combine(_tempTestPath, "test2.txt");
            
            await File.WriteAllTextAsync(testFile1, "This is test file 1 content.");
            await File.WriteAllTextAsync(testFile2, "This is test file 2 content.");

            // 3. Test compression
            var archivePath = Path.Combine(_tempTestPath, "test_archive.zip");
            var compressionResult = await compressionService.CompressFilesAsync(
                new[] { testFile1, testFile2 }, 
                archivePath, 
                CompressionFormat.ZIP);

            compressionResult.Should().NotBeNull();
            compressionResult.Success.Should().BeTrue("Compression should succeed");
            File.Exists(archivePath).Should().BeTrue("Archive file should be created");

            // 4. Validate archive
            var isValid = await compressionService.ValidateArchiveAsync(archivePath);
            isValid.Should().BeTrue("Archive should be valid");

            // 5. List archive contents
            var entries = await compressionService.ListArchiveContentsAsync(archivePath);
            entries.Should().HaveCount(2, "Archive should contain 2 files");

            // 6. Test transfer service integration (protocol commands)
            var session = new FileTransferSession
            {
                IsUpload = false,
                FileName = "test_archive.zip",
                Protocol = FileTransferProtocol.ZMODEM
            };

            var zmodemCommand = await transferService.GenerateZmodemInitCommandAsync(session);
            zmodemCommand.Should().NotBeNullOrEmpty("ZMODEM command should be generated");

            session.Protocol = FileTransferProtocol.XMODEM;
            var xmodemCommand = await transferService.GenerateXYmodemInitCommandAsync(session);
            xmodemCommand.Should().NotBeNullOrEmpty("XMODEM command should be generated");
        }

        [Fact]
        public async Task FileAreaStatistics_ShouldProvideAccurateData()
        {
            var fileAreaService = _serviceManager.FileAreaService;

            // Create multiple areas to test statistics
            var areas = new[]
            {
                new FileAreaDto { Name = "Games", Description = "Game files", Path = "games", IsActive = true, MaxFileSize = 1024*1024 },
                new FileAreaDto { Name = "Utils", Description = "Utilities", Path = "utils", IsActive = true, MaxFileSize = 1024*1024 },
                new FileAreaDto { Name = "Docs", Description = "Documentation", Path = "docs", IsActive = false, MaxFileSize = 1024*1024 }
            };

            foreach (var area in areas)
            {
                await fileAreaService.CreateFileAreaAsync(area);
            }

            // Get statistics
            var stats = await fileAreaService.GetFileAreaStatisticsAsync();
            
            stats.Should().NotBeNull();
            stats.TotalAreas.Should().BeGreaterThanOrEqualTo(3, "Should have at least the 3 areas we created");
            stats.ActiveAreas.Should().BeGreaterThanOrEqualTo(2, "Should have at least 2 active areas");

            // Get active areas only
            var activeAreas = (await fileAreaService.GetActiveFileAreasAsync()).ToList();
            activeAreas.Should().HaveCountGreaterThanOrEqualTo(2, "Should have at least 2 active areas");
            activeAreas.Should().OnlyContain(a => a.IsActive, "Active areas query should only return active areas");
        }

        [Fact]
        public void ServiceDependencies_ShouldBeProperlyInjected()
        {
            // Verify that services have their dependencies properly injected
            // This tests the dependency injection configuration

            var fileAreaService = _serviceManager.FileAreaService;
            fileAreaService.Should().NotBeNull();

            var transferService = _serviceManager.FileTransferService;
            transferService.Should().NotBeNull();

            var compressionService = _serviceManager.FileCompressionService;
            compressionService.Should().NotBeNull();

            // These services should all be different instances (scoped)
            var fileAreaService2 = _serviceProvider.GetRequiredService<IFileAreaService>();
            fileAreaService2.Should().NotBeSameAs(fileAreaService, "Scoped services should be different instances");

            // But singleton services should be the same
            var dbManager1 = _serviceManager.DatabaseManager;
            var dbManager2 = _serviceManager.DatabaseManager;
            dbManager1.Should().BeSameAs(dbManager2, "Singleton services should be the same instance");
        }
    }
}
