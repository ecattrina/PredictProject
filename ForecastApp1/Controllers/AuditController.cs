using ForecastApp1.Data;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.Admin)]
public class AuditController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AuditController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] long? userId, [FromQuery] int take = 200, CancellationToken ct = default)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(a => a.OccurredAt >= from);
        if (to.HasValue) q = q.Where(a => a.OccurredAt <= to);
        if (userId.HasValue) q = q.Where(a => a.UserId == userId);
        var list = await q.OrderByDescending(a => a.OccurredAt).Take(take)
            .Select(a => new { a.Id, a.OccurredAt, a.UserId, a.Action, a.EntityType, a.EntityId, a.ImportBatchId, a.CalculationRunId, a.PayloadJson })
            .ToListAsync(ct);
        return Ok(list);
    }
}
