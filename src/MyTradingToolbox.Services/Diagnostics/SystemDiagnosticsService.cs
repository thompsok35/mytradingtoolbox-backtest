using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Services.Configuration;

namespace MyTradingToolbox.Services.Diagnostics;

public interface ISystemDiagnosticsService
{
    Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct = default);
    Task<TradierHealthDto> TestTradierConnectivityAsync(CancellationToken ct = default);
    List<SystemLogDto> GetRecentLogs(string? level = null, int limit = 100);
    void AddLog(string level, string source, string message, string? exception = null);
}

public class SystemDiagnosticsService : ISystemDiagnosticsService
{
    private static readonly DateTime StartTime = DateTime.UtcNow;
    private static readonly ConcurrentQueue<SystemLogDto> LogBuffer = new();
    private const int MaxLogCapacity = 500;

    private readonly MarketDataContext _db;
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IOptionSnapshotRepository _optionRepo;
    private readonly HttpClient _httpClient;
    private readonly MarketDataSettings _settings;
    private readonly IConfiguration _config;
    private readonly ILogger<SystemDiagnosticsService> _logger;

    public SystemDiagnosticsService(
        MarketDataContext db,
        IWatchlistRepository watchlistRepo,
        IOptionSnapshotRepository optionRepo,
        HttpClient httpClient,
        IOptions<MarketDataSettings> settings,
        IConfiguration config,
        ILogger<SystemDiagnosticsService> logger)
    {
        _db = db;
        _watchlistRepo = watchlistRepo;
        _optionRepo = optionRepo;
        _httpClient = httpClient;
        _settings = settings.Value;
        _config = config;
        _logger = logger;
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct = default)
    {
        var proc = Process.GetCurrentProcess();
        var memMb = Math.Round(proc.WorkingSet64 / (1024.0 * 1024.0), 2);
        var uptime = Math.Round((DateTime.UtcNow - StartTime).TotalHours, 2);

        var dbHealth = new DatabaseHealthDto();
        var sw = Stopwatch.StartNew();
        try
        {
            dbHealth.IsConnected = await _db.Database.CanConnectAsync(ct);
            sw.Stop();
            dbHealth.PingLatencyMs = sw.ElapsedMilliseconds;

            var symbols = await _watchlistRepo.GetAllAsync(ct);
            dbHealth.TotalWatchlistSymbols = symbols.Count;
            dbHealth.TotalOptionSnapshots = symbols.Sum(s => s.TotalOptionRows);
        }
        catch (Exception ex)
        {
            dbHealth.IsConnected = false;
            dbHealth.PingLatencyMs = sw.ElapsedMilliseconds;
            AddLog("Error", "Database", "Failed database health check ping.", ex.ToString());
        }

        var tradierHealth = await TestTradierConnectivityAsync(ct);

        return new SystemHealthDto
        {
            Status = dbHealth.IsConnected ? "Healthy" : "Degraded",
            Timestamp = DateTime.UtcNow,
            UptimeHours = uptime,
            MemoryUsageMb = memMb,
            ProcessorCount = Environment.ProcessorCount,
            Database = dbHealth,
            TradierApi = tradierHealth,
            Scheduler = new SchedulerHealthDto()
        };
    }

    public async Task<TradierHealthDto> TestTradierConnectivityAsync(CancellationToken ct = default)
    {
        var token = _config["MarketData:TradierApiToken"] 
            ?? _config["TRADIER_API_TOKEN"] 
            ?? _config["TRADIER_TOKEN"] 
            ?? Environment.GetEnvironmentVariable("TRADIER_API_TOKEN") 
            ?? Environment.GetEnvironmentVariable("TRADIER_TOKEN") 
            ?? _settings.TradierApiToken;

        var baseUrl = _config["MarketData:TradierBaseUrl"] 
            ?? Environment.GetEnvironmentVariable("TRADIER_BASE_URL") 
            ?? _settings.TradierBaseUrl;

        var hasToken = !string.IsNullOrWhiteSpace(token);
        var result = new TradierHealthDto
        {
            IsConfigured = hasToken
        };

        if (!hasToken)
        {
            result.IsOnline = false;
            result.StatusDescription = "Tradier API Token is not configured (set TRADIER_API_TOKEN in environment)";
            return result;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl.TrimEnd('/')}/markets/quotes?symbols=SPY");
            req.Headers.Add("Authorization", $"Bearer {token.Trim()}");
            req.Headers.Add("Accept", "application/json");

            var resp = await _httpClient.SendAsync(req, ct);
            sw.Stop();
            result.LatencyMs = sw.ElapsedMilliseconds;
            result.IsOnline = resp.IsSuccessStatusCode;
            result.StatusDescription = resp.IsSuccessStatusCode
                ? $"Operational ({sw.ElapsedMilliseconds}ms)"
                : $"Tradier API HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.LatencyMs = sw.ElapsedMilliseconds;
            result.IsOnline = false;
            result.StatusDescription = $"Connection Failed: {ex.Message}";
        }

        return result;
    }

    public List<SystemLogDto> GetRecentLogs(string? level = null, int limit = 100)
    {
        var query = LogBuffer.ToArray().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(l => l.Level.Equals(level, StringComparison.OrdinalIgnoreCase));
        }

        return query.OrderByDescending(l => l.Timestamp).Take(limit).ToList();
    }

    public void AddLog(string level, string source, string message, string? exception = null)
    {
        var entry = new SystemLogDto
        {
            Level = level,
            Source = source,
            Message = message,
            Exception = exception
        };

        LogBuffer.Enqueue(entry);
        while (LogBuffer.Count > MaxLogCapacity)
        {
            LogBuffer.TryDequeue(out _);
        }
    }
}
