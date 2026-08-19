using System.Globalization;
using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Utils;

public static class OCCParser
{
    /// <summary>
    /// Parses an OCC standardized option symbol (e.g. AAPL260918C00220000)
    /// </summary>
    public static bool TryParse(string occSymbol, out string underlying, out DateOnly expiration, out OptionSide side, out decimal strike)
    {
        underlying = string.Empty;
        expiration = default;
        side = OptionSide.Call;
        strike = 0m;

        if (string.IsNullOrWhiteSpace(occSymbol) || occSymbol.Length < 15)
            return false;

        try
        {
            var trimmed = occSymbol.Trim().ToUpperInvariant();
            var len = trimmed.Length;
            
            // Format: [Underlying 1-6 chars][YYMMDD 6 chars][C|P 1 char][Strike 8 chars]
            var strikeStr = trimmed.Substring(len - 8, 8);
            var typeChar = trimmed[len - 9];
            var dateStr = trimmed.Substring(len - 15, 6);
            var underStr = trimmed.Substring(0, len - 15);

            underlying = underStr;
            
            if (typeChar == 'C') side = OptionSide.Call;
            else if (typeChar == 'P') side = OptionSide.Put;
            else return false;

            if (!DateTime.TryParseExact(dateStr, "yyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return false;
            
            expiration = DateOnly.FromDateTime(dt);

            if (!decimal.TryParse(strikeStr, NumberStyles.None, CultureInfo.InvariantCulture, out var strikeRaw))
                return false;

            strike = strikeRaw / 1000m;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generates standard OCC option symbol (e.g. AAPL260918C00220000)
    /// </summary>
    public static string Format(string underlying, DateOnly expiration, OptionSide side, decimal strike)
    {
        var sideChar = side == OptionSide.Call ? 'C' : 'P';
        var dateStr = expiration.ToString("yyMMdd");
        var strikeInt = (long)Math.Round(strike * 1000m);
        var strikeStr = strikeInt.ToString("D8");
        return $"{underlying.ToUpperInvariant().PadRight(6)}{dateStr}{sideChar}{strikeStr}".Replace(" ", "");
    }
}
