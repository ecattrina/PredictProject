using System.Text.Json;
using ForecastApp1.Data;
using ForecastApp1.Data.Entities;

namespace ForecastApp1.Services;

public interface IAuditService
{
    Task WriteAsync(string action, string? entityType = null, string? entityId = null, long? importBatchId = null, long? calculationRunId = null, object? payload = null, CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IHttpContextAccessor _http;

    public AuditService(ApplicationDbContext db, ICurrentUser user, IHttpContextAccessor http)
    {
        _db = db;
        _user = user;
        _http = http;
    }

    public async Task WriteAsync(string action, string? entityType = null, string? entityId = null, long? importBatchId = null, long? calculationRunId = null, object? payload = null, CancellationToken ct = default)
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();
        _db.AuditLogs.Add(new AuditLogEntry
        {
            OccurredAt = DateTime.UtcNow,
            UserId = _user.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ImportBatchId = importBatchId,
            CalculationRunId = calculationRunId,
            IpAddress = ip,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
