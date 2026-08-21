using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Calculators;

public static class BlackScholesCalculator
{
    private const double RiskFreeRate = 0.045; // 4.5% baseline treasury rate

    public static (decimal Delta, decimal Gamma, decimal Theta, decimal Vega, decimal IV) ComputeGreeks(
        decimal spotPrice,
        decimal strikePrice,
        int dte,
        OptionSide side,
        decimal optionMarketPrice)
    {
        if (spotPrice <= 0 || strikePrice <= 0 || optionMarketPrice <= 0)
        {
            return (side == OptionSide.Call ? (spotPrice > strikePrice ? 1.0m : 0.0m) : (spotPrice < strikePrice ? -1.0m : 0.0m), 0m, 0m, 0m, 0m);
        }

        double s = (double)spotPrice;
        double k = (double)strikePrice;
        double t = Math.Max(1.0 / 365.0, (double)dte / 365.0);
        double p = (double)optionMarketPrice;
        bool isCall = side == OptionSide.Call;

        double iv = CalculateImpliedVolatility(s, k, t, RiskFreeRate, p, isCall);
        if (iv <= 0.001)
        {
            // Fallback intrinsic delta if IV cannot be solved
            decimal fallbackDelta = isCall
                ? (s >= k ? 0.70m : 0.30m)
                : (s <= k ? -0.70m : -0.30m);
            return (fallbackDelta, 0m, 0m, 0m, 0.30m);
        }

        double d1 = (Math.Log(s / k) + (RiskFreeRate + 0.5 * iv * iv) * t) / (iv * Math.Sqrt(t));
        double d2 = d1 - iv * Math.Sqrt(t);

        double delta = isCall ? NormalCdf(d1) : NormalCdf(d1) - 1.0;
        double gamma = NormalPdf(d1) / (s * iv * Math.Sqrt(t));
        double vega = s * NormalPdf(d1) * Math.Sqrt(t) / 100.0; // 1% change

        double thetaCall = (-s * NormalPdf(d1) * iv / (2.0 * Math.Sqrt(t)) - RiskFreeRate * k * Math.Exp(-RiskFreeRate * t) * NormalCdf(d2)) / 365.0;
        double thetaPut = (-s * NormalPdf(d1) * iv / (2.0 * Math.Sqrt(t)) + RiskFreeRate * k * Math.Exp(-RiskFreeRate * t) * NormalCdf(-d2)) / 365.0;
        double theta = isCall ? thetaCall : thetaPut;

        return (
            Delta: Math.Round((decimal)delta, 4),
            Gamma: Math.Round((decimal)gamma, 4),
            Theta: Math.Round((decimal)theta, 4),
            Vega: Math.Round((decimal)vega, 4),
            IV: Math.Round((decimal)iv, 4)
        );
    }

    public static double CalculateImpliedVolatility(double s, double k, double t, double r, double marketPrice, bool isCall)
    {
        double intrinsic = isCall ? Math.Max(0.0, s - k) : Math.Max(0.0, k - s);
        if (marketPrice < intrinsic) marketPrice = intrinsic + 0.01;

        double sigma = 0.50; // Initial guess 50% IV
        for (int i = 0; i < 30; i++)
        {
            double price = BlackScholesPrice(s, k, t, r, sigma, isCall);
            double diff = price - marketPrice;
            if (Math.Abs(diff) < 0.001) return sigma;

            double d1 = (Math.Log(s / k) + (r + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
            double vega = s * NormalPdf(d1) * Math.Sqrt(t);

            if (vega < 1e-6) break;

            double step = diff / vega;
            sigma -= step;

            if (sigma <= 0.001) sigma = 0.001;
            if (sigma > 5.0) sigma = 5.0;
        }

        return Math.Clamp(sigma, 0.01, 5.0);
    }

    public static double BlackScholesPrice(double s, double k, double t, double r, double sigma, bool isCall)
    {
        double d1 = (Math.Log(s / k) + (r + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
        double d2 = d1 - sigma * Math.Sqrt(t);

        if (isCall)
        {
            return s * NormalCdf(d1) - k * Math.Exp(-r * t) * NormalCdf(d2);
        }
        else
        {
            return k * Math.Exp(-r * t) * NormalCdf(-d2) - s * NormalCdf(-d1);
        }
    }

    public static double NormalCdf(double x)
    {
        // Abramowitz and Stegun approximation
        double b1 = 0.319381530;
        double b2 = -0.356563782;
        double b3 = 1.781477937;
        double b4 = -1.821255978;
        double b5 = 1.330274429;
        double p = 0.2316419;
        double c = 0.3989422804014337; // 1 / sqrt(2 * pi)

        if (x >= 0.0)
        {
            double t = 1.0 / (1.0 + p * x);
            return 1.0 - c * Math.Exp(-x * x / 2.0) * t * (t * (t * (t * (t * b5 + b4) + b3) + b2) + b1);
        }
        else
        {
            double t = 1.0 / (1.0 - p * x);
            return c * Math.Exp(-x * x / 2.0) * t * (t * (t * (t * (t * b5 + b4) + b3) + b2) + b1);
        }
    }

    public static double NormalPdf(double x)
    {
        return 0.3989422804014337 * Math.Exp(-0.5 * x * x);
    }
}
