using BeeEye.Shared.Time;

namespace BeeEye.Workers;

/// <summary>
/// Placeholder background service proving the workers host composes and runs.
/// Durable asynchronous workloads (ingestion, batch scoring, notifications) will be
/// implemented as Container Apps Jobs / Service Bus consumers and replace this.
/// </summary>
public sealed class HeartbeatWorker(ILogger<HeartbeatWorker> logger, IClock clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("BeeEye workers host alive at {UtcNow:o}", clock.UtcNow);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
