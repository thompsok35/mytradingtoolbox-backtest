using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyTradingToolbox.Services.Backtest;
using MyTradingToolbox.Services.Clients;
using MyTradingToolbox.Services.Configuration;
using MyTradingToolbox.Services.Harvester;
using MyTradingToolbox.Services.Integrity;

namespace MyTradingToolbox.Services;

public static class ServicesExtensions
{
    public static IServiceCollection AddMarketDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MarketDataSettings>(configuration.GetSection("MarketData"));

        services.AddHttpClient<ITradierClient, TradierClient>();
        services.AddHttpClient<IThetaDataClient, ThetaDataClient>();
        services.AddHttpClient<IMarketDataClient, MarketDataClient>();

        services.AddScoped<ICSVImporterService, CSVImporterService>();
        services.AddScoped<IHarvestOrchestrator, HarvestOrchestrator>();
        services.AddScoped<IDataIntegrityService, DataIntegrityService>();
        services.AddScoped<IBacktestEngine, BacktestEngine>();

        return services;
    }
}
