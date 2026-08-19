using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;

namespace MyTradingToolbox.Data.Context;

public class MarketDataContext : DbContext
{
    public MarketDataContext(DbContextOptions<MarketDataContext> options) : base(options)
    {
    }

    public DbSet<WatchlistSymbol> WatchlistSymbols => Set<WatchlistSymbol>();
    public DbSet<HistoricalStockCandle> HistoricalStockCandles => Set<HistoricalStockCandle>();
    public DbSet<HistoricalOptionSnapshot> HistoricalOptionSnapshots => Set<HistoricalOptionSnapshot>();
    public DbSet<DataHarvestJob> DataHarvestJobs => Set<DataHarvestJob>();
    public DbSet<DataIntegrityAudit> DataIntegrityAudits => Set<DataIntegrityAudit>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ApiUsageLog> ApiUsageLogs => Set<ApiUsageLog>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. WatchlistSymbol
        modelBuilder.Entity<WatchlistSymbol>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Symbol).IsUnique();
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.AssetType).HasConversion<string>().HasMaxLength(10);
        });

        // 2. HistoricalStockCandle
        modelBuilder.Entity<HistoricalStockCandle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();
            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.Date);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.DataSource).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Open).HasPrecision(12, 4);
            entity.Property(e => e.High).HasPrecision(12, 4);
            entity.Property(e => e.Low).HasPrecision(12, 4);
            entity.Property(e => e.Close).HasPrecision(12, 4);
            entity.Property(e => e.Vwap).HasPrecision(12, 4);
        });

        // 3. HistoricalOptionSnapshot (Composite Indexes for sub-10ms Backtest queries)
        modelBuilder.Entity<HistoricalOptionSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OptionSymbol, e.SnapshotDate }).IsUnique();
            entity.HasIndex(e => new { e.UnderlyingSymbol, e.SnapshotDate, e.DTE, e.Strike });
            entity.HasIndex(e => e.UnderlyingSymbol);
            entity.HasIndex(e => e.SnapshotDate);
            entity.HasIndex(e => e.ExpirationDate);
            entity.HasIndex(e => e.DTE);
            entity.HasIndex(e => e.Strike);

            entity.Property(e => e.UnderlyingSymbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.OptionSymbol).HasMaxLength(35).IsRequired();
            entity.Property(e => e.Side).HasConversion<string>().HasMaxLength(4);
            entity.Property(e => e.DataSource).HasConversion<string>().HasMaxLength(20);

            entity.Property(e => e.Strike).HasPrecision(10, 2);
            entity.Property(e => e.Bid).HasPrecision(10, 2);
            entity.Property(e => e.Ask).HasPrecision(10, 2);
            entity.Property(e => e.Mid).HasPrecision(10, 2);
            entity.Property(e => e.Last).HasPrecision(10, 2);
            entity.Property(e => e.UnderlyingPrice).HasPrecision(10, 2);

            entity.Property(e => e.Delta).HasPrecision(8, 5);
            entity.Property(e => e.Gamma).HasPrecision(8, 5);
            entity.Property(e => e.Theta).HasPrecision(8, 5);
            entity.Property(e => e.Vega).HasPrecision(8, 5);
            entity.Property(e => e.Rho).HasPrecision(8, 5);
            entity.Property(e => e.ImpliedVolatility).HasPrecision(8, 5);
        });

        // 4. DataHarvestJob
        modelBuilder.Entity<DataHarvestJob>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10);
            entity.Property(e => e.JobType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TargetDateRange).HasMaxLength(50);
        });

        // 5. DataIntegrityAudit
        modelBuilder.Entity<DataIntegrityAudit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.HealthScorePercent).HasPrecision(5, 2);
        });

        // 6. ApiKey
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).HasMaxLength(128).IsRequired();
            entity.Property(e => e.ConsumerName).HasMaxLength(100).IsRequired();
        });

        // 7. ApiUsageLog
        modelBuilder.Entity<ApiUsageLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.ConsumerName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Endpoint).HasMaxLength(255).IsRequired();
            entity.Property(e => e.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(45);
        });

        // 8. AppUser
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TwoFactorSecret).HasMaxLength(255);
        });
    }
}
