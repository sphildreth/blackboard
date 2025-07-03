using Blackboard.Core.Configuration;
using Blackboard.Core.Services;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Blackboard.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlackboardCore(this IServiceCollection services, 
        SystemConfiguration? config = null)
    {
        // Ensure config is not null
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config), "SystemConfiguration cannot be null");
        }

        // Configuration
        services.AddSingleton(config);
        services.AddSingleton<IDatabaseConfiguration>(config.Database);
        services.AddSingleton(config.Security);
        services.AddSingleton(config.Network);
        services.AddSingleton(config.Logging);
        services.AddSingleton(config.Security);
        services.AddSingleton(config.Network);
        services.AddSingleton(config.Logging);

        // Database
        services.AddSingleton<DatabaseManager>();

        // Core Services
        services.AddTransient<IPasswordService, PasswordService>();
        services.AddTransient<ISessionService, SessionService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IFileAreaService, FileAreaService>();

        // Background Services
        services.AddHostedService<SessionCleanupService>();

        // Logging
        services.AddSingleton<ILogger>(Log.Logger);

        return services;
    }
}

public class ServiceManager
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    public T? GetOptionalService<T>()
    {
        return _serviceProvider.GetService<T>();
    }

    public IUserService UserService => GetService<IUserService>();
    public ISessionService SessionService => GetService<ISessionService>();
    public IAuditService AuditService => GetService<IAuditService>();
    public IPasswordService PasswordService => GetService<IPasswordService>();
    public IAuthorizationService AuthorizationService => GetService<IAuthorizationService>();
    public IMessageService MessageService => GetService<IMessageService>();
    public IFileAreaService FileAreaService => GetService<IFileAreaService>();
    public DatabaseManager DatabaseManager => GetService<DatabaseManager>();
    public SystemConfiguration SystemConfiguration => GetService<SystemConfiguration>();
}
