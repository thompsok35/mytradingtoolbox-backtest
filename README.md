# MyTradingToolbox-Backtest: Market Data Vault & Backtesting Engine

**MyTradingToolbox-Backtest** is a centralized, self-hosted Market Data Vault and Quantitative Backtesting Engine engineered to serve as the single source of truth for the entire MyTradingToolbox ecosystem (`itmCCbot`, `Market Insights - Expected Price`, and automated strategy scanners).

---

## Key Features

1. **$0/Month Perpetual EOD Harvester**:
   - Automated Quartz background cron running Monday–Friday at **4:05 PM ET**.
   - Pulls daily closing option chains (all strikes/expirations with full Greeks) and stock OHLCV candles via Tradier Brokerage free API tokens.
2. **Multi-Source Bulk Historical Seeder**:
   - **Tradier Driver**: Free EOD ingestion.
   - **ThetaData Driver**: Historical bridge for 1–2 years of past EOD data.
   - **MarketData.app Filtered Driver**: Conserves API credits by querying focused DTE/Delta ranges.
   - **CBOE CSV Bulk Importer**: Automatic column mapping and schema normalization for flat files.
3. **High-Performance In-The-Money (ITM) Covered Call Backtesting Engine**:
   - Dynamic strike selection targeting specific Call Delta (e.g. 0.60–0.80 Δ) and DTE (20–45 DTE).
   - Day-by-day mark-to-market using actual historical option chains and closing prices.
   - Intelligent exit triggers: Profit Target % (e.g. 65%), Delta breach rolling, Expiration assignment, Stop Loss.
   - Comprehensive quantitative performance analytics: CAGR %, Sharpe Ratio, Sortino Ratio, Max Drawdown %, Win Rate, Profit Factor, Alpha % vs Buy & Hold Benchmark.
4. **Data Integrity & Auto-Repair Center**:
   - Calendar gap detector (filtering weekends and US market holidays).
   - Quote validation (detecting inverted bid/ask, missing Greeks).
   - GitHub-style coverage heatmap and 1-Click Auto-Repair backfill routine.
5. **Modern Financial Terminal UI Dashboard**:
   - Built with React 18+, TypeScript, Tailwind CSS, Lucide Icons, and Recharts.
   - Interactive Time-Travel Option Chain reconstructed for any historical date with Payoff curves & Volatility Smile/Skew diagrams.
6. **Consumer REST API with Token Authentication**:
   - Secured endpoints with Bearer API tokens and usage logging.
   - Interactive Swagger/OpenAPI documentation.

---

## Quick Start (Local Development)

### 1. Backend (.NET 10)
```bash
# From workspace root
dotnet run --project src/MyTradingToolbox.Api/MyTradingToolbox.Api.csproj
```
- API & Swagger documentation will be available at: `http://localhost:5000/swagger`

### 2. Frontend (React + Vite)
```bash
cd frontend
npm install
npm run dev
```
- Web Dashboard will be available at: `http://localhost:3000`

### 3. Run Automated Tests
```bash
dotnet test
```

---

## Docker Deployment

To launch PostgreSQL, .NET Web API, and Nginx Frontend together:

```bash
docker-compose up -d --build
```

- **Frontend Dashboard**: `http://localhost:3000`
- **REST API & Swagger**: `http://localhost:5000/swagger`
- **PostgreSQL Database**: `localhost:5432` (`marketdata_vault`)

---

## Consumer REST API Endpoints

All endpoints support `Authorization: Bearer <API_KEY>` or `X-API-KEY: <key>`.

| Method | Endpoint | Description |
| :--- | :--- | :--- |
| `GET` | `/api/v1/options/chain/{symbol}` | Returns option chain with Greeks for a date with DTE/Delta/Strike filters |
| `GET` | `/api/v1/stocks/candles/{symbol}` | Returns stock price candles between `from` and `to` |
| `GET` | `/api/v1/options/quotes/{optionSymbol}` | Returns price/Greeks trajectory for an OCC contract symbol |
| `GET` | `/api/v1/market/coverage/{symbol}` | Returns available date ranges and integrity health status |
| `GET` | `/api/v1/market/watchlist` | Lists all tracked watchlist symbols and metadata |
| `POST` | `/api/v1/backtest/execute` | Server-side execution of ITM Covered Call strategy simulator |
| `POST` | `/api/v1/harvester/trigger` | Manually triggers ingestion for a specific ticker and date range |
| `POST` | `/api/v1/harvester/run-daily` | Executes immediate EOD harvest on all active watchlist tickers |
| `POST` | `/api/v1/harvester/upload-csv` | Bulk drag-and-drop ingestion of CBOE / flat CSV option files |
| `POST` | `/api/v1/integrity/audit/{symbol}` | Runs calendar integrity scan and computes health score |
| `POST` | `/api/v1/integrity/repair/{symbol}` | 1-Click backfill for identified missing session gaps |
| `GET` | `/api/v1/auth/keys` | Lists consumer API tokens and request metrics |
| `POST` | `/api/v1/auth/keys` | Generates a new Bearer API Key for consumer apps |
