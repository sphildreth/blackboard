using System.Diagnostics;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;

namespace Blackboard.Core.Services;

public interface IDoorService
{
    // Door Management
    Task<IEnumerable<DoorDto>> GetAllDoorsAsync();
    Task<IEnumerable<DoorDto>> GetActiveDoorsAsync();
    Task<DoorDto?> GetDoorAsync(int doorId);
    Task<DoorDto?> GetDoorByNameAsync(string name);
    Task<DoorDto> CreateDoorAsync(CreateDoorDto createDto, int createdBy);
    Task<DoorDto> UpdateDoorAsync(DoorDto door);
    Task<bool> DeleteDoorAsync(int doorId);
    Task<IEnumerable<DoorDto>> GetDoorsByCategoryAsync(string category);
    Task<IEnumerable<string>> GetDoorCategoriesAsync();

    // Door Access Control
    Task<bool> CanUserAccessDoorAsync(int userId, int doorId);
    Task<bool> HasUserReachedDailyLimitAsync(int userId, int doorId);
    Task<int> GetUserDailySessionCountAsync(int userId, int doorId);
    Task<bool> IsDoorAvailableAsync(int doorId);
    Task<string?> GetDoorUnavailableReasonAsync(int doorId);

    // Door Sessions
    Task<DoorSessionDto> StartDoorSessionAsync(int doorId, int userId, int? nodeNumber = null);
    Task<bool> EndDoorSessionAsync(string sessionId, int? exitCode = null, string? errorMessage = null);
    Task<DoorSessionDto?> GetActiveSessionAsync(string sessionId);
    Task<IEnumerable<DoorSessionDto>> GetActiveSessionsAsync();
    Task<IEnumerable<DoorSessionDto>> GetActiveSessionsForDoorAsync(int doorId);
    Task<IEnumerable<DoorSessionDto>> GetUserSessionHistoryAsync(int userId, int count = 50);
    Task<bool> TerminateSessionAsync(string sessionId, string reason);
    Task<bool> UpdateSessionActivityAsync(string sessionId);

    // Drop File Management
    Task<DropFileInfo> GenerateDropFileAsync(int doorId, int userId, string sessionId);
    Task<bool> CleanupDropFileAsync(string sessionId);
    string GetDropFileTemplate(string dropFileType);
    Task<bool> ValidateDropFileAsync(string filePath);

    // Door Configuration
    Task<IEnumerable<DoorConfigDto>> GetDoorConfigsAsync(int doorId);
    Task<DoorConfigDto> SetDoorConfigAsync(int doorId, string key, string value, string type = "string");
    Task<bool> DeleteDoorConfigAsync(int doorId, string key);
    Task<string?> GetDoorConfigValueAsync(int doorId, string key);

    // Statistics and Monitoring
    Task<DoorSystemStatisticsDto> GetDoorSystemStatisticsAsync();
    Task<IEnumerable<DoorStatisticsDto>> GetDoorStatisticsAsync(int? doorId = null, int? userId = null);
    Task<DoorStatisticsDto?> GetUserDoorStatisticsAsync(int doorId, int userId);
    Task<bool> UpdateDoorStatisticsAsync(int doorId, int userId, int sessionTime, int? score = null);
    Task<IEnumerable<DoorDto>> GetMostPlayedDoorsAsync(int count = 10);
    Task<IEnumerable<DoorSessionDto>> GetRecentSessionsAsync(int count = 20);

    // DOSBox Integration
    bool IsDosBoxAvailable();
    Task<string> GenerateDosBoxConfigAsync(int doorId, string sessionId);
    Task<Process?> StartDosBoxSessionAsync(int doorId, string sessionId, string dropFilePath);
    Task<bool> ValidateDosBoxInstallationAsync();

    // Permissions
    Task<IEnumerable<DoorPermissionDto>> GetDoorPermissionsAsync(int doorId);
    Task<DoorPermissionDto> AddDoorPermissionAsync(int doorId, int? userId, string? userGroup, string accessType, int grantedBy, DateTime? expiresAt = null);
    Task<bool> RemoveDoorPermissionAsync(int permissionId);
    Task<bool> CheckUserPermissionAsync(int doorId, int userId);

    // Maintenance
    Task<bool> TestDoorExecutableAsync(int doorId);
    Task<IEnumerable<string>> ValidateDoorConfigurationAsync(int doorId);
    Task<int> CleanupExpiredSessionsAsync();
    Task<int> CleanupOrphanedFilesAsync();
    Task<bool> BackupDoorDataAsync(int doorId, string backupPath);
    Task<bool> RestoreDoorDataAsync(int doorId, string backupPath);

    // Logging
    Task LogDoorEventAsync(int doorId, string level, string message, string? details = null, int? sessionId = null);
    Task<IEnumerable<DoorLog>> GetDoorLogsAsync(int? doorId = null, int? sessionId = null, string? level = null, int count = 100);
}
