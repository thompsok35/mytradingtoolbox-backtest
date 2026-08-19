using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Services.Harvester;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/harvester")]
public class HarvesterController : ControllerBase
{
    private readonly IHarvestOrchestrator _harvester;
    private readonly ICSVImporterService _csvImporter;
    private readonly IHarvestJobRepository _jobRepo;

    public HarvesterController(
        IHarvestOrchestrator harvester,
        ICSVImporterService csvImporter,
        IHarvestJobRepository jobRepo)
    {
        _harvester = harvester;
        _csvImporter = csvImporter;
        _jobRepo = jobRepo;
    }

    /// <summary>
    /// Manually triggers ingestion for a specific symbol, source, and date range
    /// </summary>
    [HttpPost("trigger")]
    public async Task<ActionResult<DataHarvestJob>> TriggerHarvest(
        [FromQuery] string symbol,
        [FromQuery] JobType source,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return BadRequest("Symbol is required.");

        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var job = await _harvester.TriggerSeedAsync(symbol, source, fromDate, toDate, ct);
        return Ok(job);
    }

    /// <summary>
    /// Runs the daily automated EOD harvest for all active watchlist symbols
    /// </summary>
    [HttpPost("run-daily")]
    public async Task<ActionResult<DataHarvestJob>> RunDailyHarvest(CancellationToken ct)
    {
        var job = await _harvester.RunDailyHarvestAsync(ct);
        return Ok(job);
    }

    /// <summary>
    /// Uploads and ingests a bulk CBOE / flat CSV dataset
    /// </summary>
    [HttpPost("upload-csv")]
    public async Task<ActionResult> UploadCsv([FromForm] IFormFile file, [FromQuery] string? fallbackSymbol, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Please provide a valid CSV file.");

        using var stream = file.OpenReadStream();
        var (snapshots, candles, report) = await _csvImporter.ImportCsvAsync(stream, fallbackSymbol, ct);

        return Ok(new
        {
            snapshotsInserted = snapshots,
            candlesInserted = candles,
            report
        });
    }

    /// <summary>
    /// Returns recent data harvesting jobs and execution logs
    /// </summary>
    [HttpGet("jobs")]
    public async Task<ActionResult<List<DataHarvestJob>>> GetJobs([FromQuery] int count = 50, CancellationToken ct = default)
    {
        var jobs = await _jobRepo.GetRecentJobsAsync(count, ct);
        return Ok(jobs);
    }
}
