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
public class SuppliersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public SuppliersController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Suppliers.AsNoTracking().Where(s => s.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.SupplierCode.Contains(q) || s.Name.Contains(q));
        var list = await query.OrderBy(s => s.SupplierCode).Take(500).Select(s => new { s.Id, s.SupplierCode, s.Name }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] Supplier body, CancellationToken ct)
    {
        body.SupplierCode = body.SupplierCode.Trim();
        body.Name = body.Name.Trim();
        if (await _db.Suppliers.AnyAsync(s => s.SupplierCode == body.SupplierCode && s.DeletedAt == null, ct))
            return Conflict(new { message = "Код уже существует" });
        body.CreatedAt = DateTime.UtcNow;
        body.UpdatedAt = DateTime.UtcNow;
        _db.Suppliers.Add(body);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("supplier.create", "supplier", body.Id.ToString(), ct: ct);
        return Ok(new { id = body.Id });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] Supplier patch, CancellationToken ct)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (s is null) return NotFound();
        s.Name = patch.Name.Trim();
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("supplier.update", "supplier", id.ToString(), ct: ct);
        return Ok();
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (s is null) return NotFound();
        var used = await _db.PaymentScheduleItems.AnyAsync(p => p.SupplierId == id && p.DeletedAt == null, ct);
        if (used) return BadRequest(new { message = "Нельзя удалить: есть строки расчёта" });
        s.DeletedAt = DateTime.UtcNow;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("supplier.soft_delete", "supplier", id.ToString(), ct: ct);
        return Ok();
    }
}
