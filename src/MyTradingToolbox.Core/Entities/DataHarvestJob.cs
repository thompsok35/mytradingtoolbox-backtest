using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Entities;

public class DataHarvestJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public JobType JobType { get; set; }
    public string? Symbol { get; set; }
    public string? TargetDateRange { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int RowsInserted { get; set; }
    public int CreditsConsumed { get; set; }
    public string? ExecutionLog { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
