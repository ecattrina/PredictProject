using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Security;
using ForecastApp1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/contract-conditions")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ContractConditionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ContractConditionsController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public class ConditionDto
    {
        public long ContractId { get; set; }
        public DateOnly ValidFrom { get; set; }
        public DateOnly? ValidTo { get; set; }
        public int PaymentDelayDays { get; set; }
        public string? Description { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] long? contractId, CancellationToken ct)
    {
        var q = _db.ContractConditions.AsNoTracking().Include(c => c.Contract).Where(c => c.DeletedAt == null);
        if (contractId.HasValue) q = q.Where(c => c.ContractId == contractId);
        var list = await q.OrderByDescending(c => c.ValidFrom).Take(500)
            .Select(c => new
            {
                c.Id,
                c.ContractId,
                c.ValidFrom,
                c.ValidTo,
                c.PaymentDelayDays,
                c.Description,
                Contract = c.Contract.InternalContractNumber
            }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] ConditionDto dto, CancellationToken ct)
    {
        var overlap = await _db.ContractConditions.AnyAsync(c => c.ContractId == dto.ContractId && c.DeletedAt == null
            && c.ValidFrom <= (dto.ValidTo ?? DateOnly.MaxValue)
            && (c.ValidTo == null || c.ValidTo >= dto.ValidFrom), ct);
        if (overlap) return BadRequest(new { message = "Пересечение периодов условий" });

        var e = new ContractCondition
        {
            ContractId = dto.ContractId,
            ValidFrom = dto.ValidFrom,
            ValidTo = dto.ValidTo,
            PaymentDelayDays = dto.PaymentDelayDays,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.ContractConditions.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("condition.create", "contract_condition", e.Id.ToString(), ct: ct);
        return Ok(new { id = e.Id });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] ConditionDto dto, CancellationToken ct)
    {
        var e = await _db.ContractConditions.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (e is null) return NotFound();
        e.ValidFrom = dto.ValidFrom;
        e.ValidTo = dto.ValidTo;
        e.PaymentDelayDays = dto.PaymentDelayDays;
        e.Description = dto.Description;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("condition.update", "contract_condition", id.ToString(), ct: ct);
        return Ok();
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var e = await _db.ContractConditions.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (e is null) return NotFound();
        var used = await _db.PaymentScheduleAllocations.AnyAsync(a => a.ContractConditionId == id, ct);
        if (used) return BadRequest(new { message = "Условие используется в расчёте" });
        e.DeletedAt = DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("condition.soft_delete", "contract_condition", id.ToString(), ct: ct);
        return Ok();
    }
}
