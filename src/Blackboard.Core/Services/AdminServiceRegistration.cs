using Microsoft.Extensions.DependencyInjection;

namespace Blackboard.Core.Services;

public static class AdminServiceRegistration
{
    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.AddScoped<ISystemStatisticsService, SystemStatisticsService>();
        return services;
    }
}