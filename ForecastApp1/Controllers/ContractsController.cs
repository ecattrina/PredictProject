using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Models;
using ForecastApp1.Security;
using ForecastApp1.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[Route("api/[controller]")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ContractsController : Controller
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

    [HttpGet("~/Contracts")]
    [HttpGet("~/Contracts/Index")]
    public async Task<IActionResult> Index([FromQuery] long? supplierId, [FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Contracts.AsNoTracking().Include(c => c.Supplier).Where(c => c.DeletedAt == null);
        if (supplierId.HasValue) query = query.Where(c => c.SupplierId == supplierId);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.InternalContractNumber.Contains(q));
        var list = await query.OrderBy(c => c.InternalContractNumber).Take(500)
            .Select(c => new ContractListRow(c.Id, c.SupplierId, c.Supplier.SupplierCode, c.InternalContractNumber, c.ExternalContractNumber, c.ContractDate, c.ContractAmount))
            .ToListAsync(ct);
        ViewData["q"] = q;
        ViewData["supplierId"] = supplierId;
        return View(list);
    }

    [HttpGet("~/Contracts/Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadSupplierSelectAsync(ct);
        return View(new ContractFormModel());
    }

    [HttpPost("~/Contracts/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(
            nameof(ContractFormModel.SupplierId),
            nameof(ContractFormModel.InternalContractNumber),
            nameof(ContractFormModel.ExternalContractNumber),
            nameof(ContractFormModel.ContractDate),
            nameof(ContractFormModel.ContractAmount))]
        ContractFormModel model, CancellationToken ct)
    {
        model.InternalContractNumber = model.InternalContractNumber?.Trim();
        if (string.IsNullOrEmpty(model.InternalContractNumber))
            ModelState.AddModelError(nameof(model.InternalContractNumber), "Укажите внутренний номер");
        if (!ModelState.IsValid)
        {
            await LoadSupplierSelectAsync(ct);
            return View(model);
        }

        var dto = new ContractDto
        {
            SupplierId = model.SupplierId,
            InternalContractNumber = model.InternalContractNumber!,
            ExternalContractNumber = model.ExternalContractNumber,
            ContractDate = model.ContractDate,
            ContractAmount = model.ContractAmount
        };
        var r = await TryCreateContractAsync(dto, ct);
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Ошибка сохранения");
            await LoadSupplierSelectAsync(ct);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("~/Contracts/Edit/{id:long}")]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var c = await _db.Contracts.AsNoTracking().Include(x => x.Supplier).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c is null) return NotFound();
        ViewBag.SupplierLabel = $"{c.Supplier.SupplierCode} — {c.Supplier.Name}";
        await LoadSupplierSelectAsync(ct);
        return View(new ContractFormModel
        {
            Id = c.Id,
            SupplierId = c.SupplierId,
            InternalContractNumber = c.InternalContractNumber,
            ExternalContractNumber = c.ExternalContractNumber,
            ContractDate = c.ContractDate,
            ContractAmount = c.ContractAmount
        });
    }

    [HttpPost("~/Contracts/Edit/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id,
        [Bind(
            nameof(ContractFormModel.ExternalContractNumber),
            nameof(ContractFormModel.ContractDate),
            nameof(ContractFormModel.ContractAmount))]
        ContractFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            await LoadSupplierSelectAsync(ct);
            var full = await _db.Contracts.AsNoTracking().Include(x => x.Supplier).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (full is null) return NotFound();
            model.SupplierId = full.SupplierId;
            model.InternalContractNumber = full.InternalContractNumber;
            ViewBag.SupplierLabel = $"{full.Supplier.SupplierCode} — {full.Supplier.Name}";
            return View(model);
        }

        var dto = new ContractDto
        {
            SupplierId = 0,
            InternalContractNumber = "",
            ExternalContractNumber = model.ExternalContractNumber,
            ContractDate = model.ContractDate,
            ContractAmount = model.ContractAmount
        };
        var r = await TryUpdateContractAsync(id, dto, ct);
        if (!r.Ok)
        {
            if (r.Code == 404) return NotFound();
            ModelState.AddModelError("", r.Error ?? "Ошибка сохранения");
            await LoadSupplierSelectAsync(ct);
            var full = await _db.Contracts.AsNoTracking().Include(x => x.Supplier).FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (full is null) return NotFound();
            model.SupplierId = full.SupplierId;
            model.InternalContractNumber = full.InternalContractNumber;
            ViewBag.SupplierLabel = $"{full.Supplier.SupplierCode} — {full.Supplier.Name}";
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("~/Contracts/Delete/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await TryDeleteContractAsync(id, ct);
        if (!r.Ok)
            TempData["Error"] = r.Error ?? "Не удалось удалить";
        return RedirectToAction(nameof(Index));
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
        if (dto == null) return BadRequest(new { message = "Пустое тело запроса" });
        var r = await TryCreateContractAsync(dto, ct);
        if (!r.Ok) return r.Code == 409 ? Conflict(new { message = r.Error }) : BadRequest(new { message = r.Error });
        return Ok(new { id = r.NewId });
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] ContractDto dto, CancellationToken ct)
    {
        if (dto == null) return BadRequest(new { message = "Пустое тело запроса" });
        var r = await TryUpdateContractAsync(id, dto, ct);
        if (!r.Ok) return r.Code == 404 ? NotFound() : BadRequest(new { message = r.Error });
        return Ok();
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult> DeleteApi(long id, CancellationToken ct)
    {
        var r = await TryDeleteContractAsync(id, ct);
        if (!r.Ok) return r.Code == 404 ? NotFound() : BadRequest(new { message = r.Error });
        return Ok();
    }

    private sealed record MutationOk(bool Ok, string? Error, int Code = 0, long? NewId = null);

    private async Task LoadSupplierSelectAsync(CancellationToken ct)
    {
        var items = await _db.Suppliers.AsNoTracking()
            .Where(s => s.DeletedAt == null)
            .OrderBy(s => s.SupplierCode)
            .Take(1000)
            .Select(s => new SelectListItem($"{s.SupplierCode} — {s.Name}", s.Id.ToString()))
            .ToListAsync(ct);
        ViewBag.SupplierOptions = items;
    }

    private async Task<MutationOk> TryCreateContractAsync(ContractDto dto, CancellationToken ct)
    {
        var num = dto.InternalContractNumber.Trim();
        if (await _db.Contracts.AnyAsync(c => c.SupplierId == dto.SupplierId && c.InternalContractNumber == num && c.DeletedAt == null, ct))
            return new MutationOk(false, "Договор уже существует", 409);
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
        return new MutationOk(true, null, 0, c.Id);
    }

    private async Task<MutationOk> TryUpdateContractAsync(long id, ContractDto dto, CancellationToken ct)
    {
        var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c is null) return new MutationOk(false, null, 404);
        c.ExternalContractNumber = dto.ExternalContractNumber?.Trim();
        c.ContractDate = dto.ContractDate ?? c.ContractDate;
        c.ContractAmount = dto.ContractAmount ?? c.ContractAmount;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("contract.update", "contract", id.ToString(), ct: ct);
        return new MutationOk(true, null);
    }

    private async Task<MutationOk> TryDeleteContractAsync(long id, CancellationToken ct)
    {
        var c = await _db.Contracts.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (c is null) return new MutationOk(false, null, 404);
        var used = await _db.PaymentScheduleItems.AnyAsync(p => p.ContractId == id && p.DeletedAt == null, ct);
        if (used) return new MutationOk(false, "Нельзя удалить: есть строки расчёта", 400);
        c.DeletedAt = DateTime.UtcNow;
        c.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("contract.soft_delete", "contract", id.ToString(), ct: ct);
        return new MutationOk(true, null);
    }
}
