using Microsoft.Extensions.Logging;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Core.Models;

namespace MyTradingToolbox.Services.Backtest;

public interface IBacktestEngine
{
    Task<BacktestResult> ExecuteBacktestAsync(BacktestRequest request, CancellationToken ct = default);
}

public class BacktestEngine : IBacktestEngine
{
    private readonly IOptionSnapshotRepository _optionRepo;
    private readonly IStockCandleRepository _candleRepo;
    private readonly ILogger<BacktestEngine> _logger;

    public BacktestEngine(
        IOptionSnapshotRepository optionRepo,
        IStockCandleRepository candleRepo,
        ILogger<BacktestEngine> logger)
    {
        _optionRepo = optionRepo;
        _candleRepo = candleRepo;
        _logger = logger;
    }

    public async Task<BacktestResult> ExecuteBacktestAsync(BacktestRequest request, CancellationToken ct = default)
    {
        var symbol = request.Symbol.Trim().ToUpperInvariant();
        var candles = await _candleRepo.GetCandlesAsync(symbol, request.StartDate, request.EndDate, ct);
        
        // If not enough candles in database, fetch/generate available dates
        if (candles.Count == 0)
        {
            var dates = await _optionRepo.GetAvailableDatesAsync(symbol, ct);
            if (dates.Count > 0)
            {
                var min = dates.First();
                var max = dates.Last();
                candles = await _candleRepo.GetCandlesAsync(symbol, min, max, ct);
            }
        }

        var result = new BacktestResult
        {
            StrategyName = "ITM Covered Call Strategy",
            Symbol = symbol,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            InitialCapital = request.InitialCapital,
            Parameters = request
        };

        if (candles.Count < 2)
        {
            _logger.LogWarning("Not enough candle data for backtest {Symbol} between {Start} and {End}", symbol, request.StartDate, request.EndDate);
            return result;
        }

        // Benchmark Setup (Buy and Hold)
        var benchmarkStartPrice = candles.First().Close;
        var benchmarkShares = (int)(request.InitialCapital / benchmarkStartPrice);
        var benchmarkCash = request.InitialCapital - (benchmarkShares * benchmarkStartPrice);

        decimal currentCash = request.InitialCapital;
        ActivePosition? currentPosition = null;
        var trades = new List<BacktestTrade>();
        var equityCurve = new List<EquityPoint>();
        decimal peakEquity = request.InitialCapital;
        int tradeCounter = 0;

        foreach (var candle in candles)
        {
            var date = candle.Date;
            var spotPrice = candle.Close;

            // 1. Manage existing active position
            if (currentPosition != null)
            {
                // Find today's option quote for current position contract
                var optionQuotes = await _optionRepo.GetQuotesByOptionSymbolAsync(currentPosition.OptionSymbol, date, date, ct);
                var todayQuote = optionQuotes.FirstOrDefault();
                var currentOptionPrice = todayQuote?.Mid ?? todayQuote?.Ask ?? Math.Max(0.01m, spotPrice - currentPosition.Strike);
                var currentDelta = todayQuote?.Delta ?? 0.50m;
                var currentDte = (currentPosition.ExpirationDate.ToDateTime(TimeOnly.MinValue) - date.ToDateTime(TimeOnly.MinValue)).Days;

                bool shouldExit = false;
                ExitReason exitReason = ExitReason.ManualClose;
                decimal exitStockPrice = spotPrice;
                decimal exitOptionPrice = currentOptionPrice;

                // Profit Target Check: Check if option has decayed sufficiently
                var maxOptionProfit = currentPosition.OptionEntryPremium;
                var currentOptionGain = currentPosition.OptionEntryPremium - currentOptionPrice;
                if (maxOptionProfit > 0 && (currentOptionGain / maxOptionProfit) >= request.ProfitTargetPercent)
                {
                    shouldExit = true;
                    exitReason = ExitReason.ProfitTargetHit;
                }
                // Expiration / Near-expiration check
                else if (currentDte <= request.CloseDteThreshold || date >= currentPosition.ExpirationDate)
                {
                    shouldExit = true;
                    if (spotPrice >= currentPosition.Strike)
                    {
                        exitReason = ExitReason.Assignment;
                        exitStockPrice = currentPosition.Strike; // Shares called away at strike price
                        exitOptionPrice = 0m; // Expired ITM, assigned
                    }
                    else
                    {
                        exitReason = ExitReason.Expiration;
                        exitOptionPrice = 0m; // Expired worthless OTM
                    }
                }
                // Delta Breach Roll check
                else if (request.RollOnDeltaBreach && Math.Abs(currentDelta) < request.RollDeltaThreshold)
                {
                    shouldExit = true;
                    exitReason = ExitReason.DeltaBreachRoll;
                }
                // Stop Loss check
                else if (request.StopLossPercent.HasValue)
                {
                    var unrealizedLoss = (currentPosition.StockEntryPrice - spotPrice) - (currentPosition.OptionEntryPremium - currentOptionPrice);
                    if (unrealizedLoss >= (currentPosition.NetDebitPaid * request.StopLossPercent.Value))
                    {
                        shouldExit = true;
                        exitReason = ExitReason.StopLossHit;
                    }
                }

                if (shouldExit)
                {
                    // Close trade
                    tradeCounter++;
                    var holdDays = (date.ToDateTime(TimeOnly.MinValue) - currentPosition.EntryDate.ToDateTime(TimeOnly.MinValue)).Days;
                    if (holdDays <= 0) holdDays = 1;

                    decimal proceeds = 0m;
                    if (exitReason == ExitReason.Assignment)
                    {
                        // Called away at strike price
                        proceeds = (currentPosition.Strike * 100m * currentPosition.Contracts) - (request.CommissionPerContract * currentPosition.Contracts);
                    }
                    else
                    {
                        // Liquidate stock at current spot price, buy back option at exitOptionPrice
                        proceeds = (exitStockPrice * 100m * currentPosition.Contracts) 
                                   - (exitOptionPrice * 100m * currentPosition.Contracts) 
                                   - (request.CommissionPerContract * 2 * currentPosition.Contracts)
                                   - (request.SlippagePerContract * 100m * currentPosition.Contracts);
                    }

                    var totalCost = currentPosition.TotalCost;
                    var tradePnl = proceeds - totalCost;
                    var returnPct = totalCost > 0 ? (tradePnl / totalCost) * 100m : 0m;

                    currentCash += proceeds;

                    trades.Add(new BacktestTrade
                    {
                        Id = Guid.NewGuid(),
                        TradeNumber = tradeCounter,
                        EntryDate = currentPosition.EntryDate,
                        ExitDate = date,
                        Contracts = currentPosition.Contracts,
                        StockEntryPrice = currentPosition.StockEntryPrice,
                        StockExitPrice = exitStockPrice,
                        OptionSymbol = currentPosition.OptionSymbol,
                        Strike = currentPosition.Strike,
                        ExpirationDate = currentPosition.ExpirationDate,
                        EntryDelta = currentPosition.EntryDelta,
                        OptionEntryPremium = currentPosition.OptionEntryPremium,
                        OptionExitPremium = exitOptionPrice,
                        NetDebitPaid = currentPosition.NetDebitPaid,
                        NetCreditReceived = proceeds / (currentPosition.Contracts * 100m),
                        RealizedPnlDollars = Math.Round(tradePnl, 2),
                        ReturnOnCapitalPercent = Math.Round(returnPct, 2),
                        HoldDays = holdDays,
                        ExitReason = exitReason,
                        Notes = $"Closed via {exitReason} after {holdDays} days. Strike: ${currentPosition.Strike:F2}, Exit spot: ${exitStockPrice:F2}"
                    });

                    currentPosition = null;
                }
            }

            // 2. If no position active and we have available cash, enter a new ITM Covered Call
            if (currentPosition == null && currentCash >= (spotPrice * 100m))
            {
                // Fetch option chain for today
                var chain = await _optionRepo.GetChainAsync(new OptionChainFilter
                {
                    Symbol = symbol,
                    Date = date,
                    Side = OptionSide.Call,
                    MinDte = request.MinDte,
                    MaxDte = request.MaxDte
                }, ct);

                if (chain.Count > 0)
                {
                    // Select contract closest to TargetDte and TargetDelta
                    var candidate = chain
                        .Where(c => c.Strike <= spotPrice && (c.Delta ?? 0.70m) >= 0.50m) // In The Money call
                        .OrderBy(c => Math.Abs(c.DTE - request.TargetDte))
                        .ThenBy(c => Math.Abs((c.Delta ?? 0.70m) - request.TargetDelta))
                        .FirstOrDefault();

                    if (candidate != null)
                    {
                        var callPremium = candidate.Bid > 0 ? candidate.Bid : candidate.Mid;
                        var netDebitPerShare = spotPrice - callPremium + request.SlippagePerContract;
                        var costPerContract = (netDebitPerShare * 100m) + (request.CommissionPerContract * 2);

                        if (costPerContract > 0 && currentCash >= costPerContract)
                        {
                            int contracts = (int)(currentCash / costPerContract);
                            if (contracts < 1) contracts = 1;

                            var totalCost = costPerContract * contracts;
                            currentCash -= totalCost;

                            currentPosition = new ActivePosition
                            {
                                EntryDate = date,
                                Contracts = contracts,
                                StockEntryPrice = spotPrice,
                                OptionSymbol = candidate.OptionSymbol,
                                Strike = candidate.Strike,
                                ExpirationDate = candidate.ExpirationDate,
                                EntryDelta = candidate.Delta ?? request.TargetDelta,
                                OptionEntryPremium = callPremium,
                                NetDebitPaid = netDebitPerShare,
                                TotalCost = totalCost
                            };
                        }
                    }
                }
            }

            // 3. Mark to Market daily equity
            decimal stockValue = 0m;
            decimal optionLiability = 0m;

            if (currentPosition != null)
            {
                stockValue = spotPrice * 100m * currentPosition.Contracts;
                var optQuotes = await _optionRepo.GetQuotesByOptionSymbolAsync(currentPosition.OptionSymbol, date, date, ct);
                var optQuote = optQuotes.FirstOrDefault();
                var optPrice = optQuote?.Mid ?? Math.Max(0.01m, spotPrice - currentPosition.Strike);
                optionLiability = optPrice * 100m * currentPosition.Contracts;
            }

            var totalEquity = currentCash + stockValue - optionLiability;
            if (totalEquity > peakEquity) peakEquity = totalEquity;
            var drawdownPct = peakEquity > 0 ? ((peakEquity - totalEquity) / peakEquity) * 100m : 0m;

            // Benchmark equity
            var benchmarkTotal = benchmarkCash + (benchmarkShares * spotPrice);
            var benchmarkReturn = request.InitialCapital > 0 ? ((benchmarkTotal - request.InitialCapital) / request.InitialCapital) * 100m : 0m;

            equityCurve.Add(new EquityPoint
            {
                Date = date,
                Cash = Math.Round(currentCash, 2),
                StockValue = Math.Round(stockValue, 2),
                OptionValue = Math.Round(optionLiability, 2),
                TotalEquity = Math.Round(totalEquity, 2),
                DrawdownPercent = Math.Round(drawdownPct, 2),
                BenchmarkEquity = Math.Round(benchmarkTotal, 2),
                BenchmarkReturnPercent = Math.Round(benchmarkReturn, 2)
            });
        }

        // Close any lingering open position at end of backtest period
        if (currentPosition != null && candles.Count > 0)
        {
            tradeCounter++;
            var lastCandle = candles.Last();
            var exitStockPrice = lastCandle.Close;
            var optQuotes = await _optionRepo.GetQuotesByOptionSymbolAsync(currentPosition.OptionSymbol, lastCandle.Date, lastCandle.Date, ct);
            var optPrice = optQuotes.FirstOrDefault()?.Mid ?? Math.Max(0.01m, exitStockPrice - currentPosition.Strike);
            var proceeds = (exitStockPrice * 100m * currentPosition.Contracts) - (optPrice * 100m * currentPosition.Contracts);
            var tradePnl = proceeds - currentPosition.TotalCost;
            var holdDays = (lastCandle.Date.ToDateTime(TimeOnly.MinValue) - currentPosition.EntryDate.ToDateTime(TimeOnly.MinValue)).Days;
            if (holdDays <= 0) holdDays = 1;

            currentCash += proceeds;

            trades.Add(new BacktestTrade
            {
                Id = Guid.NewGuid(),
                TradeNumber = tradeCounter,
                EntryDate = currentPosition.EntryDate,
                ExitDate = lastCandle.Date,
                Contracts = currentPosition.Contracts,
                StockEntryPrice = currentPosition.StockEntryPrice,
                StockExitPrice = exitStockPrice,
                OptionSymbol = currentPosition.OptionSymbol,
                Strike = currentPosition.Strike,
                ExpirationDate = currentPosition.ExpirationDate,
                EntryDelta = currentPosition.EntryDelta,
                OptionEntryPremium = currentPosition.OptionEntryPremium,
                OptionExitPremium = optPrice,
                NetDebitPaid = currentPosition.NetDebitPaid,
                NetCreditReceived = proceeds / (currentPosition.Contracts * 100m),
                RealizedPnlDollars = Math.Round(tradePnl, 2),
                ReturnOnCapitalPercent = Math.Round((tradePnl / currentPosition.TotalCost) * 100m, 2),
                HoldDays = holdDays,
                ExitReason = ExitReason.ManualClose,
                Notes = "Closed at conclusion of backtest period."
            });
        }

        result.Trades = trades;
        result.DailyEquityCurve = equityCurve;
        result.Metrics = CalculatePerformanceMetrics(request.InitialCapital, equityCurve, trades);

        return result;
    }

    private static PerformanceMetrics CalculatePerformanceMetrics(decimal initialCapital, List<EquityPoint> equityCurve, List<BacktestTrade> trades)
    {
        var finalEquity = equityCurve.Count > 0 ? equityCurve.Last().TotalEquity : initialCapital;
        var totalProfit = finalEquity - initialCapital;
        var totalReturnPct = initialCapital > 0 ? (totalProfit / initialCapital) * 100m : 0m;

        var totalDays = equityCurve.Count > 1 
            ? (equityCurve.Last().Date.ToDateTime(TimeOnly.MinValue) - equityCurve.First().Date.ToDateTime(TimeOnly.MinValue)).TotalDays 
            : 365.0;
        var years = Math.Max(0.1, totalDays / 365.25);

        var cagrPct = initialCapital > 0 && finalEquity > 0
            ? (decimal)((Math.Pow((double)(finalEquity / initialCapital), 1.0 / years) - 1.0) * 100.0)
            : 0m;

        var benchmarkFinal = equityCurve.Count > 0 ? equityCurve.Last().BenchmarkEquity : initialCapital;
        var benchmarkReturnPct = initialCapital > 0 ? ((benchmarkFinal - initialCapital) / initialCapital) * 100m : 0m;
        var benchmarkCagrPct = initialCapital > 0 && benchmarkFinal > 0
            ? (decimal)((Math.Pow((double)(benchmarkFinal / initialCapital), 1.0 / years) - 1.0) * 100.0)
            : 0m;

        // Daily returns for Sharpe & Sortino & Volatility
        var dailyReturns = new List<double>();
        for (int i = 1; i < equityCurve.Count; i++)
        {
            var prev = (double)equityCurve[i - 1].TotalEquity;
            var curr = (double)equityCurve[i].TotalEquity;
            if (prev > 0)
            {
                dailyReturns.Add((curr - prev) / prev);
            }
        }

        double avgDailyReturn = dailyReturns.Count > 0 ? dailyReturns.Average() : 0.0;
        double stdDev = dailyReturns.Count > 1 
            ? Math.Sqrt(dailyReturns.Sum(r => Math.Pow(r - avgDailyReturn, 2)) / (dailyReturns.Count - 1)) 
            : 0.0;

        double annualizedVol = stdDev * Math.Sqrt(252.0);
        double riskFreeDaily = 0.04 / 252.0;

        double sharpe = stdDev > 0 ? ((avgDailyReturn - riskFreeDaily) / stdDev) * Math.Sqrt(252.0) : 0.0;

        var downsideReturns = dailyReturns.Where(r => r < 0).ToList();
        double downsideStdDev = downsideReturns.Count > 1 
            ? Math.Sqrt(downsideReturns.Sum(r => Math.Pow(r, 2)) / downsideReturns.Count) 
            : 0.0001;
        double sortino = downsideStdDev > 0 ? ((avgDailyReturn - riskFreeDaily) / downsideStdDev) * Math.Sqrt(252.0) : 0.0;

        var maxDrawdown = equityCurve.Count > 0 ? equityCurve.Max(e => e.DrawdownPercent) : 0m;

        int totalTrades = trades.Count;
        var winning = trades.Where(t => t.RealizedPnlDollars > 0).ToList();
        var losing = trades.Where(t => t.RealizedPnlDollars <= 0).ToList();

        decimal totalGains = winning.Sum(t => t.RealizedPnlDollars);
        decimal totalLosses = Math.Abs(losing.Sum(t => t.RealizedPnlDollars));
        decimal profitFactor = totalLosses > 0 ? Math.Round(totalGains / totalLosses, 2) : totalGains > 0 ? 99.99m : 1m;
        decimal winRate = totalTrades > 0 ? Math.Round(((decimal)winning.Count / totalTrades) * 100m, 2) : 0m;

        return new PerformanceMetrics
        {
            InitialCapital = initialCapital,
            FinalEquity = Math.Round(finalEquity, 2),
            TotalNetProfit = Math.Round(totalProfit, 2),
            TotalReturnPercent = Math.Round(totalReturnPct, 2),
            CAGRPercent = Math.Round(cagrPct, 2),
            BenchmarkReturnPercent = Math.Round(benchmarkReturnPct, 2),
            BenchmarkCAGRPercent = Math.Round(benchmarkCagrPct, 2),
            AlphaPercent = Math.Round(cagrPct - benchmarkCagrPct, 2),
            SharpeRatio = Math.Round((decimal)sharpe, 2),
            SortinoRatio = Math.Round((decimal)sortino, 2),
            MaxDrawdownPercent = Math.Round(maxDrawdown, 2),
            WinRatePercent = winRate,
            TotalTrades = totalTrades,
            WinningTrades = winning.Count,
            LosingTrades = losing.Count,
            ProfitFactor = profitFactor,
            AverageTradePnl = totalTrades > 0 ? Math.Round(trades.Average(t => t.RealizedPnlDollars), 2) : 0m,
            AverageWinningTradePnl = winning.Count > 0 ? Math.Round(winning.Average(t => t.RealizedPnlDollars), 2) : 0m,
            AverageLosingTradePnl = losing.Count > 0 ? Math.Round(losing.Average(t => t.RealizedPnlDollars), 2) : 0m,
            AverageHoldDays = totalTrades > 0 ? Math.Round((decimal)trades.Average(t => t.HoldDays), 1) : 0m,
            AnnualizedVolatility = Math.Round((decimal)(annualizedVol * 100.0), 2)
        };
    }

    private class ActivePosition
    {
        public DateOnly EntryDate { get; set; }
        public int Contracts { get; set; }
        public decimal StockEntryPrice { get; set; }
        public string OptionSymbol { get; set; } = string.Empty;
        public decimal Strike { get; set; }
        public DateOnly ExpirationDate { get; set; }
        public decimal EntryDelta { get; set; }
        public decimal OptionEntryPremium { get; set; }
        public decimal NetDebitPaid { get; set; }
        public decimal TotalCost { get; set; }
    }
}
