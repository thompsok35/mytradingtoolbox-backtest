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
        int ownedShares = 0;
        decimal currentCostBasisPerShare = 0m;
        decimal originalStockEntryPrice = 0m;
        ActivePosition? currentPosition = null;
        var trades = new List<BacktestTrade>();
        var equityCurve = new List<EquityPoint>();
        decimal peakEquity = request.InitialCapital;
        int tradeCounter = 0;

        foreach (var candle in candles)
        {
            var date = candle.Date;
            var spotPrice = candle.Close;

            // 1. Manage existing active short call position
            if (currentPosition != null)
            {
                decimal currentOptionPrice;
                decimal currentDelta;
                if (currentPosition.CachedQuotes.TryGetValue(date, out var cachedQ))
                {
                    currentOptionPrice = cachedQ.Mid;
                    currentDelta = cachedQ.Delta;
                }
                else
                {
                    currentOptionPrice = Math.Max(0.01m, spotPrice - currentPosition.Strike);
                    currentDelta = 0.50m;
                }

                var currentDte = (currentPosition.ExpirationDate.ToDateTime(TimeOnly.MinValue) - date.ToDateTime(TimeOnly.MinValue)).Days;

                bool shouldExit = false;
                ExitReason exitReason = ExitReason.ManualClose;
                decimal exitStockPrice = spotPrice;
                decimal exitOptionPrice = currentOptionPrice;

                // 1. Expiration / Near-expiration check (Primary harvest of Annualized ROC upon Assignment)
                if (currentDte <= request.CloseDteThreshold || date >= currentPosition.ExpirationDate)
                {
                    shouldExit = true;
                    if (spotPrice >= currentPosition.Strike)
                    {
                        exitReason = ExitReason.Assignment;
                        exitStockPrice = currentPosition.Strike; // Shares called away at strike price
                        exitOptionPrice = 0m;
                    }
                    else
                    {
                        exitReason = ExitReason.Expiration;
                        exitOptionPrice = 0m; // Expired worthless OTM, kept full option premium
                    }
                }
                // 2. Defensive Delta Breach Roll check (if stock price breaches downward past safety threshold)
                else if (request.RollOnDeltaBreach && Math.Abs(currentDelta) < request.RollDeltaThreshold)
                {
                    shouldExit = true;
                    exitReason = ExitReason.DeltaBreachRoll;
                }
                // 3. Stop Loss check
                else if (request.StopLossPercent.HasValue)
                {
                    var unrealizedLoss = (currentPosition.StockEntryPrice - spotPrice) - (currentPosition.OptionEntryPremium - currentOptionPrice);
                    if (unrealizedLoss >= (currentCostBasisPerShare * request.StopLossPercent.Value))
                    {
                        shouldExit = true;
                        exitReason = ExitReason.StopLossHit;
                    }
                }
                // 4. Optional early profit target check (only if explicitly set)
                else if (request.ProfitTargetPercent.HasValue && request.ProfitTargetPercent.Value > 0)
                {
                    var maxOptionProfit = currentPosition.OptionEntryPremium;
                    var currentOptionGain = currentPosition.OptionEntryPremium - currentOptionPrice;
                    if (maxOptionProfit > 0 && (currentOptionGain / maxOptionProfit) >= request.ProfitTargetPercent.Value)
                    {
                        shouldExit = true;
                        exitReason = ExitReason.ProfitTargetHit;
                    }
                }

                if (shouldExit)
                {
                    tradeCounter++;
                    var holdDays = (date.ToDateTime(TimeOnly.MinValue) - currentPosition.EntryDate.ToDateTime(TimeOnly.MinValue)).Days;
                    if (holdDays <= 0) holdDays = 1;

                    decimal tradePnl;
                    decimal returnPct;
                    decimal proceeds;

                    if (exitReason == ExitReason.Assignment)
                    {
                        // Shares called away at strike price
                        // Charan's Rule #2: Max Profit = (Strike - Net Cost Basis)
                        proceeds = (currentPosition.Strike * ownedShares) - (request.CommissionPerContract * currentPosition.Contracts);
                        var totalCost = currentCostBasisPerShare * ownedShares;
                        tradePnl = proceeds - totalCost;
                        returnPct = totalCost > 0 ? (tradePnl / totalCost) * 100m : 0m;

                        currentCash += proceeds;

                        trades.Add(new BacktestTrade
                        {
                            Id = Guid.NewGuid(),
                            TradeNumber = tradeCounter,
                            TradeType = currentPosition.TradeType,
                            EntryDate = currentPosition.EntryDate,
                            ExitDate = date,
                            Contracts = currentPosition.Contracts,
                            StockEntryPrice = currentPosition.StockEntryPrice,
                            StockExitPrice = exitStockPrice,
                            OptionSymbol = currentPosition.OptionSymbol,
                            Strike = currentPosition.Strike,
                            ExpirationDate = currentPosition.ExpirationDate,
                            EntryDelta = currentPosition.EntryDelta,
                            EntryProbITM = currentPosition.EntryProbITM,
                            OptionEntryPremium = currentPosition.OptionEntryPremium,
                            OptionExitPremium = 0m,
                            NetDebitPaid = currentCostBasisPerShare,
                            AdjustedCostBasisPerShare = Math.Round(currentCostBasisPerShare, 2),
                            TotalDebitOutlay = Math.Round(totalCost, 2),
                            NetCreditReceived = proceeds / (currentPosition.Contracts * 100m),
                            RealizedPnlDollars = Math.Round(tradePnl, 2),
                            ReturnOnCapitalPercent = Math.Round(returnPct, 2),
                            HoldDays = holdDays,
                            ExitReason = exitReason,
                            Notes = $"Assigned at strike ${currentPosition.Strike:F2}. Cost basis was ${currentCostBasisPerShare:F2}. Captured {(tradePnl >= 0 ? "+" : "")}${tradePnl:F2} profit."
                        });

                        // Position resolved, shares delivered
                        ownedShares = 0;
                        currentCostBasisPerShare = 0m;
                        originalStockEntryPrice = 0m;
                        currentPosition = null;
                    }
                    else if (exitReason == ExitReason.Expiration)
                    {
                        // Call expired worthless OTM. Trader retains 100% of the option premium collected!
                        // Shares are NOT sold. Cost basis was already reduced upon entry.
                        var optionGain = (currentPosition.OptionEntryPremium * ownedShares) - (request.CommissionPerContract * currentPosition.Contracts);
                        tradePnl = optionGain;
                        var totalCost = currentCostBasisPerShare * ownedShares;
                        returnPct = totalCost > 0 ? (tradePnl / totalCost) * 100m : 0m;

                        trades.Add(new BacktestTrade
                        {
                            Id = Guid.NewGuid(),
                            TradeNumber = tradeCounter,
                            TradeType = currentPosition.TradeType,
                            EntryDate = currentPosition.EntryDate,
                            ExitDate = date,
                            Contracts = currentPosition.Contracts,
                            StockEntryPrice = currentPosition.StockEntryPrice,
                            StockExitPrice = spotPrice,
                            OptionSymbol = currentPosition.OptionSymbol,
                            Strike = currentPosition.Strike,
                            ExpirationDate = currentPosition.ExpirationDate,
                            EntryDelta = currentPosition.EntryDelta,
                            EntryProbITM = currentPosition.EntryProbITM,
                            OptionEntryPremium = currentPosition.OptionEntryPremium,
                            OptionExitPremium = 0m,
                            NetDebitPaid = currentCostBasisPerShare,
                            AdjustedCostBasisPerShare = Math.Round(currentCostBasisPerShare, 2),
                            TotalDebitOutlay = Math.Round(totalCost, 2),
                            NetCreditReceived = currentPosition.OptionEntryPremium,
                            RealizedPnlDollars = Math.Round(tradePnl, 2),
                            ReturnOnCapitalPercent = Math.Round(returnPct, 2),
                            HoldDays = holdDays,
                            ExitReason = exitReason,
                            Notes = $"Unassigned. Kept 100% option premium (${currentPosition.OptionEntryPremium:F2}/sh). Shares retained at adjusted cost basis ${currentCostBasisPerShare:F2}."
                        });

                        // Keep shares, ready to sell next cycle call
                        currentPosition = null;
                    }
                    else if (exitReason == ExitReason.DeltaBreachRoll)
                    {
                        // Buy to close the short call to defend position, keeping shares
                        // Charan's Rule #5: Buy back old call, cost basis increases by buyback price
                        var buybackCost = (exitOptionPrice * ownedShares) + (request.CommissionPerContract * currentPosition.Contracts) + (request.SlippagePerContract * ownedShares);
                        currentCash -= buybackCost;
                        currentCostBasisPerShare += (buybackCost / ownedShares);

                        var optionPnl = ((currentPosition.OptionEntryPremium - exitOptionPrice) * ownedShares) - (request.CommissionPerContract * 2 * currentPosition.Contracts);
                        tradePnl = optionPnl;
                        var totalCost = currentCostBasisPerShare * ownedShares;
                        returnPct = totalCost > 0 ? (tradePnl / totalCost) * 100m : 0m;

                        trades.Add(new BacktestTrade
                        {
                            Id = Guid.NewGuid(),
                            TradeNumber = tradeCounter,
                            TradeType = BacktestTradeType.CoveredCallRoll,
                            EntryDate = currentPosition.EntryDate,
                            ExitDate = date,
                            Contracts = currentPosition.Contracts,
                            StockEntryPrice = currentPosition.StockEntryPrice,
                            StockExitPrice = spotPrice,
                            OptionSymbol = currentPosition.OptionSymbol,
                            Strike = currentPosition.Strike,
                            ExpirationDate = currentPosition.ExpirationDate,
                            EntryDelta = currentPosition.EntryDelta,
                            EntryProbITM = currentPosition.EntryProbITM,
                            OptionEntryPremium = currentPosition.OptionEntryPremium,
                            OptionExitPremium = exitOptionPrice,
                            NetDebitPaid = currentCostBasisPerShare,
                            AdjustedCostBasisPerShare = Math.Round(currentCostBasisPerShare, 2),
                            TotalDebitOutlay = Math.Round(totalCost, 2),
                            NetCreditReceived = 0m,
                            RealizedPnlDollars = Math.Round(tradePnl, 2),
                            ReturnOnCapitalPercent = Math.Round(returnPct, 2),
                            HoldDays = holdDays,
                            ExitReason = exitReason,
                            Notes = $"Defensive roll triggered (Δ{currentDelta:F2}). Bought to close call @ ${exitOptionPrice:F2}. Shares retained at adjusted basis ${currentCostBasisPerShare:F2}."
                        });

                        // Keep shares, ready to sell replacement defensive call
                        currentPosition = null;
                    }
                    else
                    {
                        // StopLossHit or emergency exit: Liquidate entire position to cash
                        proceeds = (exitStockPrice * ownedShares) 
                                 - (exitOptionPrice * ownedShares) 
                                 - (request.CommissionPerContract * 2 * currentPosition.Contracts)
                                 - (request.SlippagePerContract * ownedShares);
                        var totalCost = currentCostBasisPerShare * ownedShares;
                        tradePnl = proceeds - totalCost;
                        returnPct = totalCost > 0 ? (tradePnl / totalCost) * 100m : 0m;

                        currentCash += proceeds;

                        trades.Add(new BacktestTrade
                        {
                            Id = Guid.NewGuid(),
                            TradeNumber = tradeCounter,
                            TradeType = currentPosition.TradeType,
                            EntryDate = currentPosition.EntryDate,
                            ExitDate = date,
                            Contracts = currentPosition.Contracts,
                            StockEntryPrice = currentPosition.StockEntryPrice,
                            StockExitPrice = exitStockPrice,
                            OptionSymbol = currentPosition.OptionSymbol,
                            Strike = currentPosition.Strike,
                            ExpirationDate = currentPosition.ExpirationDate,
                            EntryDelta = currentPosition.EntryDelta,
                            EntryProbITM = currentPosition.EntryProbITM,
                            OptionEntryPremium = currentPosition.OptionEntryPremium,
                            OptionExitPremium = exitOptionPrice,
                            NetDebitPaid = currentCostBasisPerShare,
                            AdjustedCostBasisPerShare = Math.Round(currentCostBasisPerShare, 2),
                            TotalDebitOutlay = Math.Round(totalCost, 2),
                            NetCreditReceived = proceeds / (currentPosition.Contracts * 100m),
                            RealizedPnlDollars = Math.Round(tradePnl, 2),
                            ReturnOnCapitalPercent = Math.Round(returnPct, 2),
                            HoldDays = holdDays,
                            ExitReason = exitReason,
                            Notes = $"Liquidated position via {exitReason}. Exit spot: ${exitStockPrice:F2}."
                        });

                        ownedShares = 0;
                        currentCostBasisPerShare = 0m;
                        originalStockEntryPrice = 0m;
                        currentPosition = null;
                    }
                }
            }

            // 2. Open new call position if no short call is active
            if (currentPosition == null)
            {
                if (ownedShares == 0 && currentCash >= (spotPrice * 100m))
                {
                    // SCENARIO 1: Fresh Buy-Write from cash
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
                        var computedChain = chain
                            .Where(c => c.Strike <= spotPrice)
                            .Select(c =>
                            {
                                var optPrice = c.Mid > 0 ? c.Mid : (c.Last > 0 ? c.Last : Math.Max(0.01m, spotPrice - c.Strike));
                                var greeks = Core.Calculators.BlackScholesCalculator.ComputeGreeks(
                                    spotPrice, c.Strike, c.DTE, c.Side, optPrice);

                                var premium = c.Bid > 0 ? c.Bid : c.Mid;
                                var debit = spotPrice - premium + request.SlippagePerContract;
                                var downsideBuffer = spotPrice > 0 ? (premium / spotPrice) * 100m : 0m;
                                var assignmentProfit = c.Strike - debit;
                                var dte = Math.Max(1, c.DTE);
                                var annualizedRoc = debit > 0 ? (assignmentProfit / debit) * (365m / dte) * 100m : -999m;

                                c.Delta = greeks.Delta;
                                c.ImpliedVolatility = greeks.IV;

                                return new
                                {
                                    Contract = c,
                                    Premium = premium,
                                    NetDebit = debit,
                                    DownsideBuffer = downsideBuffer,
                                    AssignmentProfit = assignmentProfit,
                                    AnnualizedRoc = annualizedRoc,
                                    Delta = greeks.Delta,
                                    ProbITM = greeks.ProbabilityOfITM
                                };
                            })
                            .ToList();

                        var candidates = computedChain
                            .Where(x => x.AssignmentProfit > 0
                                     && x.DownsideBuffer >= request.MinDownsideBufferPercent
                                     && x.AnnualizedRoc >= request.MinAnnualizedRocPercent
                                     && Math.Abs(x.ProbITM - request.TargetDelta) <= request.DeltaTolerance)
                            .OrderBy(x => Math.Abs(x.ProbITM - request.TargetDelta))
                            .ThenBy(x => Math.Abs(x.Contract.DTE - request.TargetDte))
                            .ThenByDescending(x => x.AnnualizedRoc)
                            .ThenByDescending(x => x.DownsideBuffer)
                            .ToList();

                        var selected = candidates.FirstOrDefault();

                        if (selected != null)
                        {
                            var candidate = selected.Contract;
                            var callPremium = selected.Premium;
                            var netDebitPerShare = selected.NetDebit;
                            var costPerContract = (netDebitPerShare * 100m) + (request.CommissionPerContract * 2);

                            if (costPerContract > 0 && currentCash >= costPerContract)
                            {
                                int contracts = 1;
                                switch (request.SizingMode)
                                {
                                    case PositionSizingMode.FixedContracts:
                                        contracts = Math.Max(1, request.FixedContracts);
                                        break;
                                    case PositionSizingMode.FixedDollarBudget:
                                        var maxBudget = request.FixedDollarBudget > 0 ? request.FixedDollarBudget : 2500m;
                                        contracts = Math.Max(1, (int)(maxBudget / costPerContract));
                                        break;
                                    case PositionSizingMode.PortfolioCompoundingPercent:
                                        var allocPct = request.AllocationPercent > 0 ? request.AllocationPercent : 0.10m;
                                        contracts = Math.Max(1, (int)((currentCash * allocPct) / costPerContract));
                                        break;
                                    default:
                                        contracts = 1;
                                        break;
                                }

                                int maxAffordable = (int)(currentCash / costPerContract);
                                if (contracts > maxAffordable) contracts = maxAffordable;
                                if (contracts < 1) contracts = 1;

                                var totalCost = costPerContract * contracts;
                                currentCash -= totalCost;

                                ownedShares = contracts * 100;
                                currentCostBasisPerShare = netDebitPerShare;
                                originalStockEntryPrice = spotPrice;

                                var lifetimeQuotes = await _optionRepo.GetQuotesByOptionSymbolAsync(candidate.OptionSymbol, date, candidate.ExpirationDate, ct);
                                var quoteDict = lifetimeQuotes.ToDictionary(
                                    q => q.SnapshotDate, 
                                    q => (Mid: q.Mid > 0 ? q.Mid : (q.Last > 0 ? q.Last : Math.Max(0.01m, spotPrice - candidate.Strike)), 
                                          Delta: q.Delta ?? candidate.Delta ?? request.TargetDelta));

                                currentPosition = new ActivePosition
                                {
                                    TradeType = BacktestTradeType.BuyWrite,
                                    EntryDate = date,
                                    Contracts = contracts,
                                    StockEntryPrice = spotPrice,
                                    OptionSymbol = candidate.OptionSymbol,
                                    Strike = candidate.Strike,
                                    ExpirationDate = candidate.ExpirationDate,
                                    EntryDelta = candidate.Delta ?? selected.Delta,
                                    EntryProbITM = selected.ProbITM,
                                    OptionEntryPremium = callPremium,
                                    NetDebitPaid = netDebitPerShare,
                                    TotalCost = totalCost,
                                    CachedQuotes = quoteDict
                                };
                            }
                        }
                    }
                }
                else if (ownedShares > 0)
                {
                    // SCENARIO 2: Covered Call against Existing Held Shares
                    // Charan's Rule #4: Strike MUST be >= currentCostBasisPerShare (Breakeven Defense Rule!)
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
                        var computedChain = chain
                            .Where(c => c.Strike >= currentCostBasisPerShare) // Charan's Breakeven Rule
                            .Select(c =>
                            {
                                var optPrice = c.Mid > 0 ? c.Mid : (c.Last > 0 ? c.Last : Math.Max(0.01m, spotPrice - c.Strike));
                                var greeks = Core.Calculators.BlackScholesCalculator.ComputeGreeks(
                                    spotPrice, c.Strike, c.DTE, c.Side, optPrice);

                                var premium = c.Bid > 0 ? c.Bid : c.Mid;
                                var assignmentProfit = c.Strike - (currentCostBasisPerShare - premium);
                                var dte = Math.Max(1, c.DTE);
                                var annualizedRoc = currentCostBasisPerShare > 0 ? (assignmentProfit / currentCostBasisPerShare) * (365m / dte) * 100m : 0m;

                                return new
                                {
                                    Contract = c,
                                    Premium = premium,
                                    AnnualizedRoc = annualizedRoc,
                                    Delta = greeks.Delta,
                                    ProbITM = greeks.ProbabilityOfITM
                                };
                            })
                            .Where(x => x.Premium > 0)
                            .OrderBy(x => Math.Abs(x.ProbITM - request.TargetDelta))
                            .ThenBy(x => Math.Abs(x.Contract.DTE - request.TargetDte))
                            .ThenByDescending(x => x.AnnualizedRoc)
                            .ToList();

                        var selected = computedChain.FirstOrDefault();
                        if (selected != null)
                        {
                            var candidate = selected.Contract;
                            var callPremium = selected.Premium;
                            int contracts = ownedShares / 100;

                            var netCredit = (callPremium - request.SlippagePerContract) * ownedShares - (request.CommissionPerContract * contracts);
                            currentCash += netCredit;
                            currentCostBasisPerShare -= (callPremium - request.SlippagePerContract); // Further reduces cost basis!

                            var lifetimeQuotes = await _optionRepo.GetQuotesByOptionSymbolAsync(candidate.OptionSymbol, date, candidate.ExpirationDate, ct);
                            var quoteDict = lifetimeQuotes.ToDictionary(
                                q => q.SnapshotDate, 
                                q => (Mid: q.Mid > 0 ? q.Mid : (q.Last > 0 ? q.Last : Math.Max(0.01m, spotPrice - candidate.Strike)), 
                                      Delta: q.Delta ?? candidate.Delta ?? request.TargetDelta));

                            currentPosition = new ActivePosition
                            {
                                TradeType = BacktestTradeType.CoveredCallNextCycle,
                                EntryDate = date,
                                Contracts = contracts,
                                StockEntryPrice = originalStockEntryPrice > 0 ? originalStockEntryPrice : spotPrice,
                                OptionSymbol = candidate.OptionSymbol,
                                Strike = candidate.Strike,
                                ExpirationDate = candidate.ExpirationDate,
                                EntryDelta = candidate.Delta ?? selected.Delta,
                                EntryProbITM = selected.ProbITM,
                                OptionEntryPremium = callPremium,
                                NetDebitPaid = currentCostBasisPerShare,
                                TotalCost = currentCostBasisPerShare * ownedShares,
                                CachedQuotes = quoteDict
                            };
                        }
                    }
                }
            }

            // 3. Mark to Market daily equity
            decimal stockValue = ownedShares * spotPrice;
            decimal optionLiability = 0m;

            if (currentPosition != null)
            {
                decimal optPrice;
                if (currentPosition.CachedQuotes.TryGetValue(date, out var cachedMark))
                {
                    optPrice = cachedMark.Mid;
                }
                else
                {
                    optPrice = Math.Max(0.01m, spotPrice - currentPosition.Strike);
                }
                optionLiability = optPrice * ownedShares;
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
        if (ownedShares > 0 && candles.Count > 0)
        {
            tradeCounter++;
            var lastCandle = candles.Last();
            var exitStockPrice = lastCandle.Close;
            decimal optPrice = 0m;
            if (currentPosition != null)
            {
                optPrice = currentPosition.CachedQuotes.TryGetValue(lastCandle.Date, out var lastQuote) ? lastQuote.Mid : Math.Max(0.01m, exitStockPrice - currentPosition.Strike);
            }
            var proceeds = (exitStockPrice * ownedShares) - (optPrice * ownedShares);
            var totalCost = currentCostBasisPerShare * ownedShares;
            var tradePnl = proceeds - totalCost;
            var holdDays = currentPosition != null ? (lastCandle.Date.ToDateTime(TimeOnly.MinValue) - currentPosition.EntryDate.ToDateTime(TimeOnly.MinValue)).Days : 1;
            if (holdDays <= 0) holdDays = 1;

            currentCash += proceeds;

            trades.Add(new BacktestTrade
            {
                Id = Guid.NewGuid(),
                TradeNumber = tradeCounter,
                TradeType = currentPosition?.TradeType ?? BacktestTradeType.BuyWrite,
                EntryDate = currentPosition?.EntryDate ?? lastCandle.Date,
                ExitDate = lastCandle.Date,
                Contracts = ownedShares / 100,
                StockEntryPrice = originalStockEntryPrice > 0 ? originalStockEntryPrice : exitStockPrice,
                StockExitPrice = exitStockPrice,
                OptionSymbol = currentPosition?.OptionSymbol ?? string.Empty,
                Strike = currentPosition?.Strike ?? 0m,
                ExpirationDate = currentPosition?.ExpirationDate ?? lastCandle.Date,
                EntryDelta = currentPosition?.EntryDelta ?? 0m,
                EntryProbITM = currentPosition?.EntryProbITM ?? 0m,
                OptionEntryPremium = currentPosition?.OptionEntryPremium ?? 0m,
                OptionExitPremium = optPrice,
                NetDebitPaid = currentCostBasisPerShare,
                AdjustedCostBasisPerShare = Math.Round(currentCostBasisPerShare, 2),
                TotalDebitOutlay = Math.Round(totalCost, 2),
                NetCreditReceived = proceeds / ownedShares,
                RealizedPnlDollars = Math.Round(tradePnl, 2),
                ReturnOnCapitalPercent = totalCost > 0 ? Math.Round((tradePnl / totalCost) * 100m, 2) : 0m,
                HoldDays = holdDays,
                ExitReason = ExitReason.ManualClose,
                Notes = $"Liquidated lingering shares at end of backtest period. Final spot: ${exitStockPrice:F2}, Basis: ${currentCostBasisPerShare:F2}."
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
        public BacktestTradeType TradeType { get; set; } = BacktestTradeType.BuyWrite;
        public DateOnly EntryDate { get; set; }
        public int Contracts { get; set; }
        public decimal StockEntryPrice { get; set; }
        public string OptionSymbol { get; set; } = string.Empty;
        public decimal Strike { get; set; }
        public DateOnly ExpirationDate { get; set; }
        public decimal EntryDelta { get; set; }
        public decimal EntryProbITM { get; set; }
        public decimal OptionEntryPremium { get; set; }
        public decimal NetDebitPaid { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<DateOnly, (decimal Mid, decimal Delta)> CachedQuotes { get; set; } = new();
    }
}
