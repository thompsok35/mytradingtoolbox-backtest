using Microsoft.Extensions.Logging;
using MyTradingToolbox.Services.Harvester;
using Quartz;

namespace MyTradingToolbox.Api.Jobs;

[DisallowConcurrentExecution]
public class DailyHarvestJob : IJob
{
    private readonly IHarvestOrchestrator _harvester;
    private readonly ILogger<DailyHarvestJob> _logger;

    public DailyHarvestJob(IHarvestOrchestrator harvester, ILogger<DailyHarvestJob> logger)
    {
        _harvester = harvester;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Scheduled EOD Market Harvest triggered at {Time}", DateTime.UtcNow);
        try
        {
            var job = await _harvester.RunDailyHarvestAsync(context.CancellationToken);
            _logger.LogInformation("Scheduled EOD Harvest completed with status {Status}. Rows inserted: {Rows}", job.Status, job.RowsInserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled Daily Harvest encountered an error.");
        }
    }
}
