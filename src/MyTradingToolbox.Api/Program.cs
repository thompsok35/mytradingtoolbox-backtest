using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using MyTradingToolbox.Api.Jobs;
using MyTradingToolbox.Api.Middleware;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Services;
using MyTradingToolbox.Services.Harvester;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers with JSON serialization configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 2. Configure Swagger with Bearer API Key authentication
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

// 3. Add CORS for Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 4. Register PostgreSQL Data Layer & Services
builder.Services.AddMarketDataLayer(builder.Configuration);
builder.Services.AddMarketDataServices(builder.Configuration);

// 5. Register Quartz Scheduler for 4:05 PM ET Daily EOD Harvester & 4:30 PM ET Integrity Audit
var cronSchedule = builder.Configuration["MarketData:DailyHarvestCron"] ?? "0 5 16 ? * MON-FRI";
var tz = TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");

builder.Services.AddQuartz(q =>
{
    // Job 1: 4:05 PM ET Perpetual EOD Harvester
    var harvestJobKey = new JobKey("DailyHarvestJob");
    q.AddJob<DailyHarvestJob>(opts => opts.WithIdentity(harvestJobKey));
    q.AddTrigger(opts => opts
        .ForJob(harvestJobKey)
        .WithIdentity("DailyHarvestTrigger")
        .WithCronSchedule(cronSchedule, x => x.InTimeZone(tz)));

    // Job 2: 4:30 PM ET Gap-Healing & Data Integrity Auditor
    var auditJobKey = new JobKey("DailyIntegrityAuditJob");
    q.AddJob<DailyIntegrityAuditJob>(opts => opts.WithIdentity(auditJobKey));
    q.AddTrigger(opts => opts
        .ForJob(auditJobKey)
        .WithIdentity("DailyIntegrityTrigger")
        .WithCronSchedule("0 30 16 ? * MON-FRI", x => x.InTimeZone(tz)));
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

var app = builder.Build();

// 6. Ensure PostgreSQL Database Created & Seed Initial Watchlist and Data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketDataContext>();
    db.Database.EnsureCreated();

    var watchlistRepo = scope.ServiceProvider.GetRequiredService<IWatchlistRepository>();
    var apiKeyRepo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
    var harvester = scope.ServiceProvider.GetRequiredService<IHarvestOrchestrator>();

    // Seed default watchlist if empty
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

        // Seed default API keys for consumer apps
        var keys = await apiKeyRepo.GetAllKeysAsync();
        if (keys.Count == 0)
        {
            await apiKeyRepo.CreateKeyAsync("itmCCbot", 300);
            await apiKeyRepo.CreateKeyAsync("Market Insights - Expected Price", 300);
        }

        // Initial 3-month historical seed for AAPL & SPY
        var toDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = toDate.AddMonths(-3);
        await harvester.TriggerSeedAsync("AAPL", JobType.DailyTradierHarvest, fromDate, toDate);
        await harvester.TriggerSeedAsync("SPY", JobType.DailyTradierHarvest, fromDate, toDate);
    }
    else
    {
        // On subsequent restarts, automatically run catch-up backfill for any intermediate trading days
        _ = Task.Run(async () =>
        {
            try
            {
                using var bgScope = app.Services.CreateScope();
                var bgHarvester = bgScope.ServiceProvider.GetRequiredService<IHarvestOrchestrator>();
                await bgHarvester.RunDailyHarvestAsync();
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Startup catch-up backfill encountered an error.");
            }
        });
    }
}

// 7. Middleware Pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyTradingToolbox-Backtest API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow, engine = "PostgreSQL" }));

app.Run();
