namespace ForecastApp1.Data.Entities;

public class AuditLogEntry
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public long? UserId { get; set; }
    public AppUser? User { get; set; }
    public string Action { get; set; } = null!;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public long? ImportBatchId { get; set; }
    public long? CalculationRunId { get; set; }
    public string? IpAddress { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime CreatedAt { get; set; }
}
