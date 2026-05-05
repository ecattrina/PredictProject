using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Security;
using ForecastApp1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ContractsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ContractsController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public class ContractDto
    {
        public long SupplierId { get; set; }
        public string InternalContractNumber { get; set; } = null!;
        public string? ExternalContractNumber { get; set; }
        public DateOnly? ContractDate { get; set; }
        public decimal? ContractAmount { get; set; }
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] long? supplierId, [FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Contracts.AsNoTracking().Include(c => c.Supplier).Where(c => c.DeletedAt == null);
        if (supplierId.HasValue) query = query.Where(c => c.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.InternalContractNumber.Contains(q));
        var list = await query.OrderBy(c => c.InternalContractNumber).Take(500)
            .Select(c => new { c.Id, c.SupplierId, SupplierCode = c.Supplier.SupplierCode, c.InternalContractNumber, c.ExternalContractNumber, c.ContractDate, c.ContractAmount })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] ContractDto dto, CancellationToken ct)
    {
        var num = dto.InternalContractNumber.Trim();
        if (await _db.Contracts.AnyAsync(c => c.SupplierId == dto.SupplierId && c.InternalContractNumber == num && c.DeletedAt == null, ct))
            return Conflict(new { message = "Договор уже существует" });
        var c = new Contract
        {
            SupplierId = dto.SupplierId,
            InternalContractNumber = num,
            ExternalContractNumber = dto.ExternalContractNumber?.Trim(),
            ContractDate = dto.ContractDate,
            ContractAmount = dto.ContractAmount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.Contracts.Add(c);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("contract.create", "contract", c.Id.ToString(), ct: ct);
        return Ok(new { id = c.Id });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] ContractDto dto, CancellationToken ct)
    {
        var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c is null) return NotFound();
        c.ExternalContractNumber = dto.ExternalContractNumber?.Trim();
        c.ContractDate = dto.ContractDate ?? c.ContractDate;
        c.ContractAmount = dto.ContractAmount ?? c.ContractAmount;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("contract.update", "contract", id.ToString(), ct: ct);
        return Ok();
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c is null) return NotFound();
        var used = await _db.PaymentScheduleItems.AnyAsync(p => p.ContractId == id && p.DeletedAt == null, ct);
        if (used) return BadRequest(new { message = "Нельзя удалить: есть строки расчёта" });
        c.DeletedAt = DateTime.UtcNow;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("contract.soft_delete", "contract", id.ToString(), ct: ct);
        return Ok();
    }
}
