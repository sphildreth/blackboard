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
        services.AddSingleton<IAuthenticationContextService, AuthenticationContextService>();
        services.AddTransient<IAuditService, AuditService>();
        services.AddTransient<IUserService, UserService>();
        services.AddTransient<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IMessageService, MessageService>();
        
        // Register FileAreaService with configuration-based path
        services.AddScoped<IFileAreaService>(provider =>
        {
            var databaseManager = provider.GetRequiredService<DatabaseManager>();
            var logger = provider.GetRequiredService<ILogger>();
            var configuration = provider.GetRequiredService<SystemConfiguration>();
            
            var filesPath = PathResolver.ResolvePath(configuration.System.FilesPath, configuration.System.RootPath);
            return new FileAreaService(databaseManager, logger, filesPath);
        });
        
        services.AddScoped<IFileTransferService, FileTransferService>();
        services.AddScoped<IFileCompressionService, FileCompressionService>();

        // Door Game System Services (Phase 6)
        services.AddScoped<IDoorService, DoorService>();
        services.AddScoped<IFossilEmulationService, FossilEmulationService>();

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
    public IFileTransferService FileTransferService => GetService<IFileTransferService>();
    public IFileCompressionService FileCompressionService => GetService<IFileCompressionService>();
    public IDoorService DoorService => GetService<IDoorService>();
    public IFossilEmulationService FossilEmulationService => GetService<IFossilEmulationService>();
    public DatabaseManager DatabaseManager => GetService<DatabaseManager>();
    public SystemConfiguration SystemConfiguration => GetService<SystemConfiguration>();
    public IAuthenticationContextService AuthenticationContextService => GetService<IAuthenticationContextService>();
}
