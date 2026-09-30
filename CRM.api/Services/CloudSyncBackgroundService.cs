using System;
using System.Threading;
using System.Threading.Tasks;
using CRM.Infrastructure.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CRM.api.Services;

public class CloudSyncBackgroundService : BackgroundService
{
    private readonly ICloudSyncService _cloudSyncService;
    private readonly ILogger<CloudSyncBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    public CloudSyncBackgroundService(
        ICloudSyncService cloudSyncService,
        ILogger<CloudSyncBackgroundService> logger)
    {
        _cloudSyncService = cloudSyncService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CloudSyncBackgroundService started. Monitoring cloud connectivity and sync queue...");

        try
        {
            // Initial delay on startup to allow database initialization to complete
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    bool isOnline = await _cloudSyncService.IsCloudAvailableAsync(stoppingToken);
                    if (isOnline)
                    {
                        var result = await _cloudSyncService.ProcessPendingSyncQueueAsync(stoppingToken);
                        if (result.ProcessedCount > 0)
                        {
                            _logger.LogInformation("CloudSync background tick: {Message}", result.Message);
                        }
                    }
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Exception in CloudSyncBackgroundService cycle.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal graceful shutdown requested; exit cleanly
        }
        finally
        {
            _logger.LogInformation("CloudSyncBackgroundService stopped.");
        }
    }
}
