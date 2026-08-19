-- Schema & Composite Index Initialization for MyTradingToolbox Market Data Vault

CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- 1. WatchlistSymbols
CREATE TABLE IF NOT EXISTS "WatchlistSymbols" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Symbol" VARCHAR(10) NOT NULL UNIQUE,
    "AssetType" VARCHAR(10) NOT NULL DEFAULT 'Equity',
    "IsActiveHarvesting" BOOLEAN NOT NULL DEFAULT TRUE,
    "EarliestAvailableDate" DATE,
    "LatestAvailableDate" DATE,
    "TotalSnapshotDays" INT NOT NULL DEFAULT 0,
    "TotalOptionRows" INT NOT NULL DEFAULT 0,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. HistoricalStockCandles
CREATE TABLE IF NOT EXISTS "HistoricalStockCandles" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Symbol" VARCHAR(10) NOT NULL,
    "Date" DATE NOT NULL,
    "Open" NUMERIC(12, 4) NOT NULL,
    "High" NUMERIC(12, 4) NOT NULL,
    "Low" NUMERIC(12, 4) NOT NULL,
    "Close" NUMERIC(12, 4) NOT NULL,
    "Volume" BIGINT NOT NULL,
    "Vwap" NUMERIC(12, 4),
    "DataSource" VARCHAR(20) NOT NULL DEFAULT 'Tradier',
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "UQ_StockCandle_Symbol_Date" UNIQUE ("Symbol", "Date")
);
CREATE INDEX IF NOT EXISTS "IX_StockCandles_Symbol" ON "HistoricalStockCandles" ("Symbol");
CREATE INDEX IF NOT EXISTS "IX_StockCandles_Date" ON "HistoricalStockCandles" ("Date");

-- 3. HistoricalOptionSnapshots
CREATE TABLE IF NOT EXISTS "HistoricalOptionSnapshots" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "UnderlyingSymbol" VARCHAR(10) NOT NULL,
    "SnapshotDate" DATE NOT NULL,
    "OptionSymbol" VARCHAR(35) NOT NULL,
    "ExpirationDate" DATE NOT NULL,
    "DTE" INT NOT NULL,
    "Strike" NUMERIC(10, 2) NOT NULL,
    "Side" VARCHAR(4) NOT NULL,
    "Bid" NUMERIC(10, 2) NOT NULL,
    "Ask" NUMERIC(10, 2) NOT NULL,
    "Mid" NUMERIC(10, 2) NOT NULL,
    "Last" NUMERIC(10, 2) NOT NULL,
    "Delta" NUMERIC(8, 5),
    "Gamma" NUMERIC(8, 5),
    "Theta" NUMERIC(8, 5),
    "Vega" NUMERIC(8, 5),
    "Rho" NUMERIC(8, 5),
    "ImpliedVolatility" NUMERIC(8, 5),
    "UnderlyingPrice" NUMERIC(10, 2) NOT NULL,
    "Volume" BIGINT NOT NULL DEFAULT 0,
    "OpenInterest" BIGINT NOT NULL DEFAULT 0,
    "DataSource" VARCHAR(20) NOT NULL DEFAULT 'TradierEOD',
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "UQ_OptionSnapshot_Symbol_Date" UNIQUE ("OptionSymbol", "SnapshotDate")
);

-- High Performance Composite B-Tree Indexes
CREATE INDEX IF NOT EXISTS "IX_OptionSnapshots_Composite_Query" ON "HistoricalOptionSnapshots" ("UnderlyingSymbol", "SnapshotDate", "DTE", "Strike");
CREATE INDEX IF NOT EXISTS "IX_OptionSnapshots_Underlying" ON "HistoricalOptionSnapshots" ("UnderlyingSymbol");
CREATE INDEX IF NOT EXISTS "IX_OptionSnapshots_Date" ON "HistoricalOptionSnapshots" ("SnapshotDate");
CREATE INDEX IF NOT EXISTS "IX_OptionSnapshots_Expiration" ON "HistoricalOptionSnapshots" ("ExpirationDate");
CREATE INDEX IF NOT EXISTS "IX_OptionSnapshots_DTE" ON "HistoricalOptionSnapshots" ("DTE");
CREATE INDEX IF NOT EXISTS "IX_OptionSnapshots_Strike" ON "HistoricalOptionSnapshots" ("Strike");

-- 4. DataHarvestJobs
CREATE TABLE IF NOT EXISTS "DataHarvestJobs" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "JobType" VARCHAR(30) NOT NULL,
    "Symbol" VARCHAR(10),
    "TargetDateRange" VARCHAR(50),
    "Status" VARCHAR(20) NOT NULL DEFAULT 'Pending',
    "RowsInserted" INT NOT NULL DEFAULT 0,
    "CreditsConsumed" INT NOT NULL DEFAULT 0,
    "ExecutionLog" TEXT,
    "StartedAt" TIMESTAMP WITH TIME ZONE,
    "CompletedAt" TIMESTAMP WITH TIME ZONE
);

-- 5. DataIntegrityAudits
CREATE TABLE IF NOT EXISTS "DataIntegrityAudits" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Symbol" VARCHAR(10) NOT NULL,
    "AuditDate" DATE NOT NULL,
    "TotalExpectedTradingDays" INT NOT NULL,
    "ActualDaysPresent" INT NOT NULL,
    "MissingDatesJson" TEXT NOT NULL DEFAULT '[]',
    "CorruptQuotesCount" INT NOT NULL DEFAULT 0,
    "HealthScorePercent" NUMERIC(5, 2) NOT NULL DEFAULT 100.00,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 6. ApiKeys
CREATE TABLE IF NOT EXISTS "ApiKeys" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Key" VARCHAR(64) NOT NULL UNIQUE,
    "ConsumerName" VARCHAR(100) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "RateLimitPerMinute" INT NOT NULL DEFAULT 120,
    "TotalRequests" BIGINT NOT NULL DEFAULT 0,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "ExpiresAt" TIMESTAMP WITH TIME ZONE,
    "LastUsedAt" TIMESTAMP WITH TIME ZONE
);

-- 7. ApiUsageLogs
CREATE TABLE IF NOT EXISTS "ApiUsageLogs" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "ApiKeyId" UUID NOT NULL,
    "ConsumerName" VARCHAR(100) NOT NULL,
    "Endpoint" VARCHAR(255) NOT NULL,
    "HttpMethod" VARCHAR(10) NOT NULL,
    "StatusCode" INT NOT NULL,
    "ResponseTimeMs" BIGINT NOT NULL,
    "Timestamp" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    "IpAddress" VARCHAR(45)
);
CREATE INDEX IF NOT EXISTS "IX_ApiUsageLogs_Timestamp" ON "ApiUsageLogs" ("Timestamp");
