using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Models;
using ForecastApp1.Security;
using ForecastApp1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class SuppliersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public SuppliersController(ApplicationDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet("~/Suppliers")]
    [HttpGet("~/Suppliers/Index")]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Suppliers.AsNoTracking().Where(s => s.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.SupplierCode.Contains(q) || s.Name.Contains(q));
        var list = await query.OrderBy(s => s.SupplierCode).Take(500)
            .Select(s => new SupplierListRow(s.Id, s.SupplierCode, s.Name)).ToListAsync(ct);
        ViewData["q"] = q;
        return View(list);
    }

    [HttpGet("~/Suppliers/Create")]
    public IActionResult Create() => View(new SupplierFormModel());

    [HttpPost("~/Suppliers/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(nameof(SupplierFormModel.SupplierCode), nameof(SupplierFormModel.Name))]
        SupplierFormModel model, CancellationToken ct)
    {
        model.SupplierCode = model.SupplierCode?.Trim();
        if (string.IsNullOrEmpty(model.SupplierCode))
            ModelState.AddModelError(nameof(model.SupplierCode), "Укажите код");
        if (!ModelState.IsValid)
            return View(model);

        var body = new Supplier { SupplierCode = model.SupplierCode!, Name = model.Name.Trim() };
        var r = await TryCreateSupplierAsync(body, ct);
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Ошибка сохранения");
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("~/Suppliers/Edit/{id:long}")]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (s is null) return NotFound();
        return View(new SupplierFormModel { Id = s.Id, SupplierCode = s.SupplierCode, Name = s.Name });
    }

    [HttpPost("~/Suppliers/Edit/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, [Bind(nameof(SupplierFormModel.Name))] SupplierFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            var cur = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            model.SupplierCode = cur?.SupplierCode;
            return View(model);
        }

        var r = await TryUpdateSupplierAsync(id, model.Name.Trim(), ct);
        if (!r.Ok)
        {
            if (r.Code == 404) return NotFound();
            ModelState.AddModelError("", r.Error ?? "Ошибка сохранения");
            var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            model.SupplierCode = s?.SupplierCode;
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("~/Suppliers/Delete/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await TryDeleteSupplierAsync(id, ct);
        if (!r.Ok)
        {
            TempData["Error"] = r.Error ?? "Не удалось удалить";
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Index));
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
        if (body == null) return BadRequest(new { message = "Пустое тело запроса" });
        var r = await TryCreateSupplierAsync(body, ct);
        if (!r.Ok) return r.Code == 409 ? Conflict(new { message = r.Error }) : BadRequest(new { message = r.Error });
        return Ok(new { id = body.Id });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] Supplier patch, CancellationToken ct)
    {
        if (patch == null) return BadRequest(new { message = "Пустое тело запроса" });
        var r = await TryUpdateSupplierAsync(id, patch.Name?.Trim() ?? "", ct);
        if (!r.Ok) return r.Code == 404 ? NotFound() : BadRequest(new { message = r.Error });
        return Ok();
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> DeleteApi(long id, CancellationToken ct)
    {
        var r = await TryDeleteSupplierAsync(id, ct);
        if (!r.Ok) return r.Code == 404 ? NotFound() : BadRequest(new { message = r.Error });
        return Ok();
    }

    private sealed record MutationOk(bool Ok, string? Error, int Code = 0);

    private async Task<MutationOk> TryCreateSupplierAsync(Supplier body, CancellationToken ct)
    {
        body.SupplierCode = body.SupplierCode.Trim();
        body.Name = body.Name.Trim();
        if (await _db.Suppliers.AnyAsync(s => s.SupplierCode == body.SupplierCode && s.DeletedAt == null, ct))
            return new MutationOk(false, "Код уже существует", 409);
        body.CreatedAt = DateTime.UtcNow;
        body.UpdatedAt = DateTime.UtcNow;
        _db.Suppliers.Add(body);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("supplier.create", "supplier", body.Id.ToString(), ct: ct);
        return new MutationOk(true, null);
    }

    private async Task<MutationOk> TryUpdateSupplierAsync(long id, string name, CancellationToken ct)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (s is null) return new MutationOk(false, null, 404);
        s.Name = name;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("supplier.update", "supplier", id.ToString(), ct: ct);
        return new MutationOk(true, null);
    }

    private async Task<MutationOk> TryDeleteSupplierAsync(long id, CancellationToken ct)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (s is null) return new MutationOk(false, null, 404);
        var used = await _db.PaymentScheduleItems.AnyAsync(p => p.SupplierId == id && p.DeletedAt == null, ct);
        if (used) return new MutationOk(false, "Нельзя удалить: есть строки расчёта", 400);
        s.DeletedAt = DateTime.UtcNow;
        s.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("supplier.soft_delete", "supplier", id.ToString(), ct: ct);
        return new MutationOk(true, null);
    }
}
