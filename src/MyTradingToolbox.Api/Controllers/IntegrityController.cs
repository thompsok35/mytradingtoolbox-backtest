using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Services.Integrity;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/integrity")]
public class IntegrityController : ControllerBase
{
    private readonly IDataIntegrityService _integrityService;
    private readonly IIntegrityRepository _integrityRepo;

    public IntegrityController(IDataIntegrityService integrityService, IIntegrityRepository integrityRepo)
    {
        _integrityService = integrityService;
        _integrityRepo = integrityRepo;
    }

    /// <summary>
    /// Runs an integrity audit for a specific symbol
    /// </summary>
    [HttpPost("audit/{symbol}")]
    public async Task<ActionResult<DataIntegrityAudit>> AuditSymbol(string symbol, CancellationToken ct)
    {
        var audit = await _integrityService.AuditSymbolAsync(symbol, ct);
        return Ok(audit);
    }

    /// <summary>
    /// Triggers automated 1-click gap backfilling and data repair
    /// </summary>
    [HttpPost("repair/{symbol}")]
    public async Task<ActionResult<DataHarvestJob>> RepairSymbolGaps(string symbol, CancellationToken ct)
    {
        var job = await _integrityService.AutoRepairGapsAsync(symbol, ct);
        return Ok(job);
    }

    /// <summary>
    /// Returns latest audit scores across all tickers
    /// </summary>
    [HttpGet("audits")]
    public async Task<ActionResult<List<DataIntegrityAudit>>> GetAllAudits(CancellationToken ct)
    {
        var audits = await _integrityRepo.GetAllLatestAuditsAsync(ct);
        return Ok(audits);
    }
}
