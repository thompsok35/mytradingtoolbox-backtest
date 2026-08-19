namespace MyTradingToolbox.Core.Entities;

public class DataIntegrityAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Symbol { get; set; } = string.Empty;
    public DateOnly AuditDate { get; set; }
    public int TotalExpectedTradingDays { get; set; }
    public int ActualDaysPresent { get; set; }
    public string MissingDatesJson { get; set; } = "[]";
    public int CorruptQuotesCount { get; set; }
    public decimal HealthScorePercent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
