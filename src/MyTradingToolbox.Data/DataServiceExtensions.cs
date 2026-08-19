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
        var connectionString = ResolvePostgresConnectionString(configuration);

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

    public static string ResolvePostgresConnectionString(IConfiguration configuration)
    {
        // 1. Check raw ConnectionStrings:DefaultConnection or standard environment vars
        var raw = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"]
            ?? configuration["DATABASE_PRIVATE_URL"]
            ?? configuration["POSTGRES_URL"]
            ?? configuration["POSTGRESQL_URL"];

        // 2. Check direct PG environment variables (Railway / Docker standard)
        var pgHost = configuration["PGHOST"];
        var pgUser = configuration["PGUSER"];
        var pgPass = configuration["PGPASSWORD"];
        var pgDb = configuration["PGDATABASE"];
        var pgPort = configuration["PGPORT"] ?? "5432";

        if (!string.IsNullOrWhiteSpace(pgHost) && !string.IsNullOrWhiteSpace(pgUser) && !string.IsNullOrWhiteSpace(pgDb))
        {
            return $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPass};SSL Mode=Prefer;Trust Server Certificate=true;";
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Host=127.0.0.1;Port=5432;Database=marketdata_vault;Username=postgres;Password=postgres;";
        }

        raw = raw.Trim().Trim('"', '\'');

        // 3. If URI format (e.g. postgresql://user:pass@host:port/db), convert to Npgsql key-value format
        if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(raw);
                var userInfo = uri.UserInfo.Split(':');
                var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "postgres";
                var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var dbName = uri.AbsolutePath.TrimStart('/');

                return $"Host={host};Port={port};Database={dbName};Username={user};Password={pass};SSL Mode=Prefer;Trust Server Certificate=true;";
            }
            catch
            {
                // Fallback to raw if parsing fails
            }
        }

        // 4. If key-value format but missing SSL handling, ensure it works with cloud providers
        if (!raw.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase) && !raw.Contains("127.0.0.1") && !raw.Contains("localhost"))
        {
            raw = raw.TrimEnd(';') + ";SSL Mode=Prefer;Trust Server Certificate=true;";
        }

        return raw;
    }
}
