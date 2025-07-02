using Blackboard.Core.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Blackboard.Core.Services;

public class SessionCleanupService : BackgroundService
{
    private readonly ISessionService _sessionService;
    private readonly ILogger _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(15);

    public SessionCleanupService(ISessionService sessionService, ILogger logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Session cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _sessionService.CleanupExpiredSessionsAsync();
                await Task.Delay(_cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error occurred during session cleanup");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait a bit before retrying
            }
        }

        _logger.Information("Session cleanup service stopped");
    }
}
