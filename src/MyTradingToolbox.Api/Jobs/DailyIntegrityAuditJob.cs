using Microsoft.Extensions.Logging;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Services.Integrity;
using Quartz;

namespace MyTradingToolbox.Api.Jobs;

[DisallowConcurrentExecution]
public class DailyIntegrityAuditJob : IJob
{
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IDataIntegrityService _integrityService;
    private readonly ILogger<DailyIntegrityAuditJob> _logger;

    public DailyIntegrityAuditJob(
        IWatchlistRepository watchlistRepo,
        IDataIntegrityService integrityService,
        ILogger<DailyIntegrityAuditJob> logger)
    {
        _watchlistRepo = watchlistRepo;
        _integrityService = integrityService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Scheduled Data Integrity Audit & Auto-Repair triggered at {Time}", DateTime.UtcNow);
        try
        {
            var symbols = await _watchlistRepo.GetAllAsync(context.CancellationToken);
            foreach (var sym in symbols.Where(s => s.IsActiveHarvesting))
            {
                var audit = await _integrityService.AuditSymbolAsync(sym.Symbol, context.CancellationToken);
                _logger.LogInformation("Integrity score for {Symbol}: {Score}% (Missing days: {Missing})",
                    sym.Symbol, audit.HealthScorePercent, audit.TotalExpectedTradingDays - audit.ActualDaysPresent);

                // Auto-repair if gaps exist
                if (audit.HealthScorePercent < 100m)
                {
                    _logger.LogInformation("Auto-healing identified data gaps for {Symbol}...", sym.Symbol);
                    await _integrityService.AutoRepairGapsAsync(sym.Symbol, context.CancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled Data Integrity Audit job encountered an error.");
        }
    }
}
