# System Specification & Build Instructions: MyTradingToolbox-Backtest

## 1. Executive Summary & Vision
**MyTradingToolbox-Backtest** is a centralized, self-hosted Market Data Vault & Backtesting Engine designed to serve as the unified historical data repository for the entire MyTradingToolbox suite (including `itmCCbot`, `Market Insights - Expected Price`, and future strategy scanners).

The application eliminates third-party market data API costs and rate limit bottlenecks by:
1. **Seeding** historical options chains, stock candles, and Greeks using free tier services (ThetaData Free EOD and smart-filtered MarketData.app).
2. **Perpetually Harvesting** daily closing option chains and Greeks at 4:05 PM ET using Tradier's free live brokerage API ($0 ongoing cost).
3. **Serving** historical data and backtest execution to consumer applications via a standardized, high-performance REST API.
4. **Providing Data Administrators** with a modern visual UI to manage tickers, trigger seed/harvest jobs, inspect data integrity, and monitor data coverage.

---

## 2. Technical Stack
* **Backend**: C# .NET 10 (ASP.NET Core Web API) or Python (FastAPI).
* **Database**: PostgreSQL (with TimescaleDB extension or composite B-Tree indexes on `[symbol, date, dte, strike]`).
* **Background Scheduling**: .NET Hosted Services / Quartz.NET / Hangfire.
* **Frontend UI**: React 18+ (Vite) + TypeScript + Tailwind CSS + Lucide Icons + Recharts / Lightweight Charts.
* **Architecture**: REST API with JWT / API-Key authentication for inter-app communication.

---

## 3. Database Schema & Data Models

### 3.1 `WatchlistSymbols`
* `Id` (UUID, Primary Key)
* `Symbol` (VARCHAR(10), Unique, e.g., 'UMAC', 'AAPL', 'SPY')
* `AssetType` (VARCHAR(10), 'Equity', 'ETF', 'Index')
* `IsActiveHarvesting` (BOOLEAN, default true)
* `EarliestAvailableDate` (DATE)
* `LatestAvailableDate` (DATE)
* `TotalSnapshotDays` (INT)
* `TotalOptionRows` (INT)
* `CreatedAt` / `UpdatedAt` (TIMESTAMP)

### 3.2 `HistoricalStockCandles`
* `Id` (UUID, Primary Key)
* `Symbol` (VARCHAR(10), Indexed)
* `Date` (DATE, Indexed)
* `Open` / `High` / `Low` / `Close` (NUMERIC(12, 4))
* `Volume` (BIGINT)
* `Vwap` (NUMERIC(12, 4))
* `DataSource` (VARCHAR(20), 'Tradier', 'MarketData', 'ThetaData')
* *Unique Constraint*: `[Symbol, Date]`

### 3.3 `HistoricalOptionSnapshots`
* `Id` (UUID, Primary Key)
* `UnderlyingSymbol` (VARCHAR(10), Indexed)
* `SnapshotDate` (DATE, Indexed)
* `OptionSymbol` (VARCHAR(35), Indexed, OCC format)
* `ExpirationDate` (DATE, Indexed)
* `DTE` (INT, Indexed)
* `Strike` (NUMERIC(10, 2), Indexed)
* `Side` (VARCHAR(4), 'call' / 'put')
* `Bid` / `Ask` / `Mid` / `Last` (NUMERIC(10, 2))
* `Delta` / `Gamma` / `Theta` / `Vega` / `Rho` (NUMERIC(8, 5))
* `ImpliedVolatility` (NUMERIC(8, 5))
* `UnderlyingPrice` (NUMERIC(10, 2))
* `Volume` / `OpenInterest` (BIGINT)
* `DataSource` (VARCHAR(20), 'TradierEOD', 'MarketData', 'ThetaData', 'CSVImport')
* *Unique Constraint*: `[OptionSymbol, SnapshotDate]`
* *Composite Index*: `[UnderlyingSymbol, SnapshotDate, DTE, Strike]`

### 3.4 `DataHarvestJobs`
* `Id` (UUID, Primary Key)
* `JobType` (VARCHAR(30), 'DailyTradierHarvest', 'ThetaDataSeed', 'MarketDataSeed', 'IntegrityCheck')
* `Symbol` (VARCHAR(10))
* `TargetDateRange` (VARCHAR(50))
* `Status` (VARCHAR(20), 'Pending', 'Running', 'Completed', 'Failed')
* `RowsInserted` (INT)
* `CreditsConsumed` (INT)
* `ExecutionLog` (TEXT)
* `StartedAt` / `CompletedAt` (TIMESTAMP)

### 3.5 `DataIntegrityAudits`
* `Id` (UUID, Primary Key)
* `Symbol` (VARCHAR(10))
* `AuditDate` (DATE)
* `TotalExpectedTradingDays` (INT)
* `ActualDaysPresent` (INT)
* `MissingDatesJson` (JSONB)
* `CorruptQuotesCount` (INT, inverted bid/ask, missing Greeks)
* `HealthScorePercent` (NUMERIC(5, 2))

---

## 4. Ingestion & Harvester Engine

### 4.1 Daily Automated EOD Harvester (Tradier API)
* **Trigger**: Scheduled background cron running Monday–Friday at **4:05 PM ET** (market close).
* **Workflow**:
  1. Reads all active `WatchlistSymbols`.
  2. Queries Tradier `/v1/markets/quotes` for underlying closing spot prices.
  3. Queries Tradier `/v1/markets/options/chains` with Greeks enabled for each symbol.
  4. Upserts all contracts into `HistoricalOptionSnapshots` and candles into `HistoricalStockCandles`.
  5. Cost: **$0.00** (Included free with Tradier Brokerage token).

### 4.2 Bulk Historical Seeder (ThetaData / MarketData / CSV)
* **ThetaData Driver**: Ingests historical EOD options for past 1–2 years via local Python bridge or direct API.
* **MarketData Filtered Driver**: Conserves API credits by querying only necessary DTE ranges (`minDte=7&maxDte=45&side=call`) to avoid the per-strike credit penalty.
* **CSV Bulk Importer**: Allows drag-and-drop ingestion of CBOE / flat file datasets with automatic column mapping.

---

## 5. Administrator UI Dashboard Features

### 5.1 Watchlist & Harvester Control Center
* Table of tracked tickers with toggles for automatic daily harvesting.
* Real-time metadata: date range coverage, total rows stored, storage size in MB, and last harvest status.
* Quick action buttons: `+ Add Symbol`, `⚡ Run Immediate Harvest`, `🌱 Trigger Historical Seed`.

### 5.2 Interactive Data Explorer ("Time-Travel Option Chain")
* Select any symbol and historical date from a date picker.
* Renders the reconstructed full option chain as it existed on that date (Bid, Ask, Mid, Strike, Expiration, DTE, Delta, IV).
* Visual payoff and volatility skew charts.

### 5.3 Data Quality & Integrity Center
* **Coverage Heatmap**: Visual GitHub-style contribution calendar displaying green/amber/red days of data availability per symbol.
* **Gap Detector**: One-click scanner that reports missing trading sessions or corrupted Greeks.
* **Auto-Repair**: One-click button to automatically backfill identified gaps from secondary data sources.

### 5.4 API Key & Consumer App Access
* Generate and revoke `Bearer` API tokens for consumer apps (`itmCCbot`, `MarketInsights`).
* Live request logs and usage analytics per consumer application.
* Integrated interactive Swagger/OpenAPI documentation.

---

## 6. Consumer REST API Endpoints (For Other Apps)

All endpoints require `Authorization: Bearer <API_KEY>` or internal service token.

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/v1/options/chain/{symbol}` | Returns option chain for a given date with DTE, Strike, and Greek filters |
| `GET` | `/api/v1/stocks/candles/{symbol}` | Returns daily/intraday stock price candles between `from` and `to` |
| `GET` | `/api/v1/options/quotes/{optionSymbol}` | Returns historical price trajectory for a specific OCC option symbol |
| `GET` | `/api/v1/market/coverage/{symbol}` | Returns available date ranges and data health status for a ticker |
| `POST` | `/api/v1/backtest/execute` | Server-side execution of standardized ITM Covered Call or custom strategy |
| `POST` | `/api/v1/harvester/trigger` | Manually triggers ingestion for a specific symbol and date range |

---

## 7. Deliverables & Execution Requirements
1. Clean, modular code with repository pattern and dependency injection.
2. Production-ready Dockerfile & `docker-compose.yml` for PostgreSQL + Web API + Frontend.
3. Automated database migrations via EF Core / Alembic.
4. Comprehensive unit & integration tests for data ingestion, deduplication, and backtest calculations.