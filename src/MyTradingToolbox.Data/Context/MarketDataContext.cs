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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. WatchlistSymbols
        modelBuilder.Entity<WatchlistSymbol>(entity =>
        {
            entity.ToTable("WatchlistSymbols");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.HasIndex(e => e.Symbol).IsUnique();
            entity.Property(e => e.AssetType).HasConversion<string>().HasMaxLength(10);
        });

        // 2. HistoricalStockCandles
        modelBuilder.Entity<HistoricalStockCandle>(entity =>
        {
            entity.ToTable("HistoricalStockCandles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Open).HasPrecision(12, 4);
            entity.Property(e => e.High).HasPrecision(12, 4);
            entity.Property(e => e.Low).HasPrecision(12, 4);
            entity.Property(e => e.Close).HasPrecision(12, 4);
            entity.Property(e => e.Vwap).HasPrecision(12, 4);
            entity.Property(e => e.DataSource).HasConversion<string>().HasMaxLength(20);

            entity.HasIndex(e => e.Symbol);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.Symbol, e.Date }).IsUnique();
        });

        // 3. HistoricalOptionSnapshots
        modelBuilder.Entity<HistoricalOptionSnapshot>(entity =>
        {
            entity.ToTable("HistoricalOptionSnapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnderlyingSymbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.OptionSymbol).HasMaxLength(35).IsRequired();
            entity.Property(e => e.Side).HasConversion<string>().HasMaxLength(4);
            entity.Property(e => e.Bid).HasPrecision(10, 2);
            entity.Property(e => e.Ask).HasPrecision(10, 2);
            entity.Property(e => e.Mid).HasPrecision(10, 2);
            entity.Property(e => e.Last).HasPrecision(10, 2);
            entity.Property(e => e.UnderlyingPrice).HasPrecision(10, 2);
            entity.Property(e => e.Strike).HasPrecision(10, 2);
            entity.Property(e => e.Delta).HasPrecision(8, 5);
            entity.Property(e => e.Gamma).HasPrecision(8, 5);
            entity.Property(e => e.Theta).HasPrecision(8, 5);
            entity.Property(e => e.Vega).HasPrecision(8, 5);
            entity.Property(e => e.Rho).HasPrecision(8, 5);
            entity.Property(e => e.ImpliedVolatility).HasPrecision(8, 5);
            entity.Property(e => e.DataSource).HasConversion<string>().HasMaxLength(20);

            entity.HasIndex(e => e.UnderlyingSymbol);
            entity.HasIndex(e => e.SnapshotDate);
            entity.HasIndex(e => e.OptionSymbol);
            entity.HasIndex(e => e.ExpirationDate);
            entity.HasIndex(e => e.DTE);
            entity.HasIndex(e => e.Strike);
            
            // Unique constraint on [OptionSymbol, SnapshotDate]
            entity.HasIndex(e => new { e.OptionSymbol, e.SnapshotDate }).IsUnique();
            
            // High-performance Composite index for query engine: [UnderlyingSymbol, SnapshotDate, DTE, Strike]
            entity.HasIndex(e => new { e.UnderlyingSymbol, e.SnapshotDate, e.DTE, e.Strike });
        });

        // 4. DataHarvestJobs
        modelBuilder.Entity<DataHarvestJob>(entity =>
        {
            entity.ToTable("DataHarvestJobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobType).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Symbol).HasMaxLength(10);
            entity.Property(e => e.TargetDateRange).HasMaxLength(50);
        });

        // 5. DataIntegrityAudits
        modelBuilder.Entity<DataIntegrityAudit>(entity =>
        {
            entity.ToTable("DataIntegrityAudits");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Symbol).HasMaxLength(10).IsRequired();
            entity.Property(e => e.HealthScorePercent).HasPrecision(5, 2);
            entity.HasIndex(e => new { e.Symbol, e.AuditDate });
        });

        // 6. ApiKey
        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable("ApiKeys");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.ConsumerName).HasMaxLength(100).IsRequired();
        });

        // 7. ApiUsageLog
        modelBuilder.Entity<ApiUsageLog>(entity =>
        {
            entity.ToTable("ApiUsageLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConsumerName).HasMaxLength(100);
            entity.Property(e => e.Endpoint).HasMaxLength(255);
            entity.Property(e => e.HttpMethod).HasMaxLength(10);
            entity.HasIndex(e => e.Timestamp);
        });
    }
}
