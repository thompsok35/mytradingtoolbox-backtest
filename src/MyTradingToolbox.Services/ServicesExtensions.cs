using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyTradingToolbox.Services.Auth;
using MyTradingToolbox.Services.Backtest;
using MyTradingToolbox.Services.Clients;
using MyTradingToolbox.Services.Configuration;
using MyTradingToolbox.Services.Diagnostics;
using MyTradingToolbox.Services.Harvester;
using MyTradingToolbox.Services.Integrity;

namespace MyTradingToolbox.Services;

public static class ServicesExtensions
{
    public static IServiceCollection AddMarketDataServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MarketDataSettings>(configuration.GetSection("MarketData"));

        services.AddHttpClient<ITradierClient, TradierClient>(client =>
        {
            var token = configuration["MarketData:TradierApiToken"] ?? configuration["TRADIER_API_TOKEN"];
            var baseUrl = configuration["MarketData:TradierBaseUrl"] ?? "https://api.tradier.com/v1";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            }
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<IThetaDataClient, ThetaDataClient>(client =>
        {
            var baseUrl = configuration["MarketData:ThetaDataBaseUrl"] ?? "http://127.0.0.1:25510/v2";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<IMarketDataClient, MarketDataClient>(client =>
        {
            var token = configuration["MarketData:MarketDataApiToken"] ?? configuration["MARKETDATA_API_TOKEN"];
            var baseUrl = configuration["MarketData:MarketDataBaseUrl"] ?? "https://api.marketdata.app/v1";
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            }
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        services.AddHttpClient<ISystemDiagnosticsService, SystemDiagnosticsService>();

        services.AddScoped<IHarvestOrchestrator, HarvestOrchestrator>();
        services.AddScoped<ICSVImporterService, CSVImporterService>();
        services.AddScoped<IDataIntegrityService, DataIntegrityService>();
        services.AddScoped<IBacktestEngine, BacktestEngine>();

        // Auth & Security
        services.AddSingleton<ITwoFactorAuthService, TwoFactorAuthService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ISystemDiagnosticsService, SystemDiagnosticsService>();

        return services;
    }
}
