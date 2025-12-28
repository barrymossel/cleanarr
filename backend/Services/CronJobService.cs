using NCrontab;

namespace Cleanarr.Services;

public class CronJobService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConfigService _config;
    private readonly ILogger<CronJobService> _logger;
    private CrontabSchedule? _schedule;
    private DateTime _nextRun;

    public CronJobService(
        IServiceScopeFactory scopeFactory,
        ConfigService config,
        ILogger<CronJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
        
        UpdateSchedule();
    }

    private void UpdateSchedule()
    {
        var cronExpression = _config.Get("CronSchedule", "0 */6 * * *"); // Default: every 6 hours
        try
        {
            _schedule = CrontabSchedule.Parse(cronExpression);
            _nextRun = _schedule.GetNextOccurrence(DateTime.Now);
            _logger.LogInformation($"Cron schedule set to: {cronExpression}, next run: {_nextRun}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Invalid cron expression: {ex.Message}");
            _schedule = CrontabSchedule.Parse("0 */6 * * *");
            _nextRun = _schedule.GetNextOccurrence(DateTime.Now);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            
            if (now >= _nextRun && _schedule != null)
            {
                _logger.LogInformation("Starting scheduled media sync...");
                
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var syncService = scope.ServiceProvider.GetRequiredService<MediaSyncService>();
                    await syncService.SyncAllAsync();
                    
                    _logger.LogInformation("Media sync completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Media sync failed: {ex.Message}");
                }

                UpdateSchedule();
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
