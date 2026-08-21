using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using MyTradingToolbox.Api.Jobs;
using MyTradingToolbox.Api.Middleware;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Services;
using MyTradingToolbox.Services.Diagnostics;
using MyTradingToolbox.Services.Harvester;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// 0. Register Custom In-Memory Diagnostics Logger Provider for Live Troubleshooting Stream
builder.Logging.AddProvider(new DiagnosticsLoggerProvider());

// 1. Add CORS for Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 2. Add Controllers with JSON serialization configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 3. Configure Swagger with Bearer API Key authentication
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MyTradingToolbox-Backtest Market Data API",
        Version = "v1",
        Description = "Centralized Market Data Vault & Backtesting Engine for the MyTradingToolbox ecosystem."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter your Bearer API token or API key (e.g. Bearer mtt_...)",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
});

// 4. Register PostgreSQL Data Layer & Services
builder.Services.AddMarketDataLayer(builder.Configuration);
builder.Services.AddMarketDataServices(builder.Configuration);

// 5. Register Quartz Scheduler for 4:05 PM ET Daily EOD Harvester & 4:30 PM ET Integrity Audit
var cronSchedule = builder.Configuration["MarketData:DailyHarvestCron"] 
    ?? builder.Configuration["DAILY_HARVEST_CRON"] 
    ?? "0 5 16 ? * MON-FRI";
var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");

builder.Services.AddQuartz(q =>
{
    var harvestJobKey = new JobKey("DailyHarvestJob");
    q.AddJob<DailyHarvestJob>(opts => opts.WithIdentity(harvestJobKey));
    q.AddTrigger(opts => opts
        .ForJob(harvestJobKey)
        .WithIdentity("DailyHarvestTrigger")
        .WithCronSchedule(cronSchedule, x => x.InTimeZone(tz)));

    var auditJobKey = new JobKey("DailyIntegrityAuditJob");
    q.AddJob<DailyIntegrityAuditJob>(opts => opts.WithIdentity(auditJobKey));
    q.AddTrigger(opts => opts
        .ForJob(auditJobKey)
        .WithIdentity("DailyIntegrityTrigger")
        .WithCronSchedule("0 30 16 ? * MON-FRI", x => x.InTimeZone(tz)));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

// 6. Global Exception & CORS Pipeline First
app.UseCors("AllowAll");

// 7. Ensure PostgreSQL Database Schema Created & Purge Fake/Synthetic Records
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketDataContext>();
    db.Database.EnsureCreated();

    // Ensure Users table exists even on pre-existing database deployments
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS ""Users"" (
                ""Id"" uuid NOT NULL PRIMARY KEY,
                ""Email"" character varying(256) NOT NULL,
                ""Name"" character varying(256) NOT NULL,
                ""PictureUrl"" text,
                ""Role"" character varying(50) NOT NULL,
                ""IsTwoFactorEnabled"" boolean NOT NULL,
                ""TwoFactorSecret"" character varying(128),
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""LastLoginAt"" timestamp with time zone
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Email"" ON ""Users"" (""Email"");

            -- Purge all synthetic and legacy UMAC records so only clean real vendor data is retained
            DELETE FROM ""HistoricalOptionSnapshots"" WHERE ""UnderlyingSymbol"" = 'UMAC' OR ""DataSource"" = 'Synthetic';
            DELETE FROM ""HistoricalStockCandles"" WHERE ""Symbol"" = 'UMAC' OR ""DataSource"" = 'Synthetic';
            UPDATE ""WatchlistSymbols"" SET ""TotalOptionRows"" = 0, ""TotalSnapshotDays"" = 0, ""EarliestAvailableDate"" = NULL, ""LatestAvailableDate"" = NULL WHERE ""Symbol"" = 'UMAC';
        ");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not execute database setup / purge SQL: {Message}", ex.Message);
    }

    var watchlistRepo = scope.ServiceProvider.GetRequiredService<IWatchlistRepository>();
    var apiKeyRepo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();

    var existingSymbols = await watchlistRepo.GetAllAsync();
    if (existingSymbols.Count == 0)
    {
        var defaultSymbols = new[]
        {
            new WatchlistSymbol { Symbol = "AAPL", AssetType = AssetType.Equity, IsActiveHarvesting = true },
            new WatchlistSymbol { Symbol = "SPY", AssetType = AssetType.ETF, IsActiveHarvesting = true },
            new WatchlistSymbol { Symbol = "QQQ", AssetType = AssetType.ETF, IsActiveHarvesting = true },
            new WatchlistSymbol { Symbol = "MSFT", AssetType = AssetType.Equity, IsActiveHarvesting = true },
            new WatchlistSymbol { Symbol = "UMAC", AssetType = AssetType.Equity, IsActiveHarvesting = true }
        };

        foreach (var sym in defaultSymbols)
        {
            await watchlistRepo.AddOrUpdateAsync(sym);
        }

        var keys = await apiKeyRepo.GetAllKeysAsync();
        if (keys.Count == 0)
        {
            await apiKeyRepo.CreateKeyAsync("itmCCbot", 300);
            await apiKeyRepo.CreateKeyAsync("Market Insights - Expected Price", 300);
        }
    }

    // Refresh coverage metrics for all watchlist tickers based exclusively on real data in db
    var currentWatchlist = await watchlistRepo.GetAllAsync();
    foreach (var sym in currentWatchlist)
    {
        await watchlistRepo.UpdateCoverageStatsAsync(sym.Symbol);
    }
}

// 8. Swagger & Middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyTradingToolbox-Backtest API v1");
    c.RoutePrefix = "swagger";
});

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, engine = "PostgreSQL" }));

app.Run();
