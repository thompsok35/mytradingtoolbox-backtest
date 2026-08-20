using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Utils;

namespace MyTradingToolbox.Tests;

public static class TestFixtureDataGenerator
{
    public static List<HistoricalStockCandle> GenerateTestCandles(string symbol, DateOnly from, DateOnly to, decimal basePrice = 220m)
    {
        var list = new List<HistoricalStockCandle>();
        var current = from;
        var dayIndex = 0;

        while (current <= to)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                var price = basePrice + (decimal)Math.Sin(dayIndex * 0.1) * 10m;
                list.Add(new HistoricalStockCandle
                {
                    Id = Guid.NewGuid(),
                    Symbol = symbol,
                    Date = current,
                    Open = price * 0.995m,
                    High = price * 1.01m,
                    Low = price * 0.99m,
                    Close = price,
                    Volume = 50000000,
                    Vwap = price,
                    DataSource = DataSource.Tradier
                });
                dayIndex++;
            }
            current = current.AddDays(1);
        }
        return list;
    }

    public static List<HistoricalOptionSnapshot> GenerateTestOptionSnapshots(string symbol, DateOnly date, decimal spotPrice)
    {
        var list = new List<HistoricalOptionSnapshot>();
        var dteList = new[] { 7, 14, 30, 45, 60 };
        var centerStrike = Math.Round(spotPrice / 5m) * 5m;

        foreach (var dte in dteList)
        {
            var expDate = date.AddDays(dte);
            for (int i = -4; i <= 4; i++)
            {
                var strike = centerStrike + (i * 5m);
                var callMid = Math.Max(0.50m, spotPrice - strike + 2m);
                var delta = Math.Clamp(0.50m + ((spotPrice - strike) / 20m), 0.10m, 0.95m);

                list.Add(new HistoricalOptionSnapshot
                {
                    Id = Guid.NewGuid(),
                    UnderlyingSymbol = symbol,
                    SnapshotDate = date,
                    OptionSymbol = OCCParser.Format(symbol, expDate, OptionSide.Call, strike),
                    ExpirationDate = expDate,
                    DTE = dte,
                    Strike = strike,
                    Side = OptionSide.Call,
                    Bid = callMid - 0.05m,
                    Ask = callMid + 0.05m,
                    Mid = callMid,
                    Last = callMid,
                    Delta = delta,
                    ImpliedVolatility = 0.25m,
                    UnderlyingPrice = spotPrice,
                    Volume = 1000,
                    OpenInterest = 5000,
                    DataSource = DataSource.Tradier
                });
            }
        }
        return list;
    }
}
