using Blackboard.Core;
using Blackboard.Core.Services;
using Blackboard.Core.Tests.Helpers;
using Blackboard.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Blackboard.Core.Tests.Integration;

public class ServiceCollectionTests
{
    [Fact]
    public void AddBlackboardCore_ShouldRegisterAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var systemConfig = TestDataHelper.CreateTestSystemConfiguration();

        // Act
        services.AddBlackboardCore(systemConfig);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify all services can be resolved
        serviceProvider.GetRequiredService<DatabaseManager>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IPasswordService>().Should().NotBeNull();
        serviceProvider.GetRequiredService<ISessionService>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IAuditService>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IUserService>().Should().NotBeNull();
        serviceProvider.GetRequiredService<IAuthorizationService>().Should().NotBeNull();
    }

    [Fact]
    public void ServiceManager_ShouldProvideAccessToAllServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var systemConfig = TestDataHelper.CreateTestSystemConfiguration();
        services.AddBlackboardCore(systemConfig);
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var serviceManager = new ServiceManager(serviceProvider);

        // Assert
        serviceManager.UserService.Should().NotBeNull();
        serviceManager.SessionService.Should().NotBeNull();
        serviceManager.AuditService.Should().NotBeNull();
        serviceManager.PasswordService.Should().NotBeNull();
        serviceManager.AuthorizationService.Should().NotBeNull();
        serviceManager.DatabaseManager.Should().NotBeNull();
        serviceManager.SystemConfiguration.Should().NotBeNull();
    }

    [Fact]
    public void ServiceManager_ShouldReturnSameInstanceForSingletons()
    {
        // Arrange
        var services = new ServiceCollection();
        var systemConfig = TestDataHelper.CreateTestSystemConfiguration();
        services.AddBlackboardCore(systemConfig);
        var serviceProvider = services.BuildServiceProvider();
        var serviceManager = new ServiceManager(serviceProvider);

        // Act
        var databaseManager1 = serviceManager.DatabaseManager;
        var databaseManager2 = serviceManager.DatabaseManager;
        var systemConfig1 = serviceManager.SystemConfiguration;
        var systemConfig2 = serviceManager.SystemConfiguration;

        // Assert
        databaseManager1.Should().BeSameAs(databaseManager2);
        systemConfig1.Should().BeSameAs(systemConfig2);
    }

    [Fact]
    public void ServiceManager_ShouldReturnDifferentInstancesForTransients()
    {
        // Arrange
        var services = new ServiceCollection();
        var systemConfig = TestDataHelper.CreateTestSystemConfiguration();
        services.AddBlackboardCore(systemConfig);
        var serviceProvider = services.BuildServiceProvider();
        var serviceManager = new ServiceManager(serviceProvider);

        // Act
        var userService1 = serviceManager.UserService;
        var userService2 = serviceManager.GetService<IUserService>();

        // Assert
        // UserService is registered as transient, so each call should return a new instance
        userService1.Should().NotBeSameAs(userService2);
    }
}
