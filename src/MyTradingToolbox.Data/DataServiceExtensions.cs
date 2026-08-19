using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Data.Repositories;

namespace MyTradingToolbox.Data;

public static class DataServiceExtensions
{
    public static IServiceCollection AddMarketDataLayer(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=marketdata_vault;Username=postgres;Password=postgres;";

        services.AddDbContext<MarketDataContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(3);
                npgsql.CommandTimeout(60);
            }));

        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<IOptionSnapshotRepository, OptionSnapshotRepository>();
        services.AddScoped<IStockCandleRepository, StockCandleRepository>();
        services.AddScoped<IHarvestJobRepository, HarvestJobRepository>();
        services.AddScoped<IIntegrityRepository, IntegrityRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();

        return services;
    }
}
