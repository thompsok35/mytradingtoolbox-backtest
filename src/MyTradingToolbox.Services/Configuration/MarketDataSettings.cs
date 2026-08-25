namespace MyTradingToolbox.Services.Configuration;

public class MarketDataSettings
{
    public string TradierApiToken { get; set; } = string.Empty;
    public string TradierBaseUrl { get; set; } = "https://api.tradier.com/v1";
    public string ThetaDataBaseUrl { get; set; } = "http://127.0.0.1:25503/v3";
    public string MarketDataBaseUrl { get; set; } = "https://api.marketdata.app/v1";
    public string MarketDataApiToken { get; set; } = string.Empty;
    public bool UseSimulatedDataIfNoToken { get; set; } = true;
    public string DailyHarvestCron { get; set; } = "0 5 16 ? * MON-FRI"; // 4:05 PM ET Mon-Fri
}
