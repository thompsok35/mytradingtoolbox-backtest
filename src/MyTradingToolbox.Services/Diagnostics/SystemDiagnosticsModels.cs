namespace MyTradingToolbox.Services.Diagnostics;

public class SystemHealthDto
{
    public string Status { get; set; } = "Healthy";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double UptimeHours { get; set; }
    public double MemoryUsageMb { get; set; }
    public int ProcessorCount { get; set; }
    public DatabaseHealthDto Database { get; set; } = new();
    public TradierHealthDto TradierApi { get; set; } = new();
    public SchedulerHealthDto Scheduler { get; set; } = new();
}

public class DatabaseHealthDto
{
    public bool IsConnected { get; set; }
    public long PingLatencyMs { get; set; }
    public string Provider { get; set; } = "PostgreSQL";
    public int TotalWatchlistSymbols { get; set; }
    public int TotalOptionSnapshots { get; set; }
}

public class TradierHealthDto
{
    public bool IsConfigured { get; set; }
    public bool IsOnline { get; set; }
    public long LatencyMs { get; set; }
    public string StatusDescription { get; set; } = "Operational";
}

public class SchedulerHealthDto
{
    public bool IsRunning { get; set; } = true;
    public string DailyHarvestCron { get; set; } = "0 5 16 ? * MON-FRI (4:05 PM ET)";
    public string IntegrityAuditCron { get; set; } = "0 30 16 ? * MON-FRI (4:30 PM ET)";
}

public class SystemLogDto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Information";
    public string Source { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
