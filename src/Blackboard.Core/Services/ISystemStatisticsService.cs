using Blackboard.Core.DTOs;

namespace Blackboard.Core.Services;

public interface ISystemStatisticsService
{
    Task<SystemStatisticsDto> GetSystemStatisticsAsync();
    Task<DashboardStatisticsDto> GetDashboardStatisticsAsync();
    Task<IEnumerable<ActiveSessionDto>> GetActiveSessionsAsync();
    Task<IEnumerable<SystemAlertDto>> GetSystemAlertsAsync();
    Task<SystemResourcesDto> GetSystemResourcesAsync();
    Task<DatabaseStatusDto> GetDatabaseStatusAsync();
}
