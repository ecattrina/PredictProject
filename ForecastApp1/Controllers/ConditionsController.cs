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

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ConditionsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public ConditionsController(ApplicationDbContext db, IAuditService audit)
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

    [HttpGet("~/Conditions")]
    [HttpGet("~/Conditions/Index")]
    public async Task<IActionResult> Index([FromQuery] long? contractId, CancellationToken ct)
    {
        var q = _db.ContractConditions.AsNoTracking().Include(c => c.Contract).Where(c => c.DeletedAt == null);
        if (contractId.HasValue) q = q.Where(c => c.ContractId == contractId);
        var list = await q.OrderByDescending(c => c.ValidFrom).Take(500)
            .Select(c => new ConditionListRow(
                c.Id,
                c.ContractId,
                c.Contract.InternalContractNumber,
                c.ValidFrom,
                c.ValidTo,
                c.PaymentDelayDays,
                c.Description))
            .ToListAsync(ct);
        ViewData["contractId"] = contractId;
        return View(list);
    }

    [HttpGet("~/Conditions/Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadContractSelectAsync(null, ct);
        return View(new ConditionFormModel());
    }

    [HttpPost("~/Conditions/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(
            nameof(ConditionFormModel.ContractId),
            nameof(ConditionFormModel.ValidFrom),
            nameof(ConditionFormModel.ValidTo),
            nameof(ConditionFormModel.PaymentDelayDays),
            nameof(ConditionFormModel.Description))]
        ConditionFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadContractSelectAsync(model.ContractId, ct);
            return View(model);
        }

        var dto = new ConditionDto
        {
            ContractId = model.ContractId,
            ValidFrom = model.ValidFrom,
            ValidTo = model.ValidTo,
            PaymentDelayDays = model.PaymentDelayDays,
            Description = model.Description
        };
        var r = await TryCreateConditionAsync(dto, ct);
        if (!r.Ok)
        {
            ModelState.AddModelError("", r.Error ?? "Ошибка сохранения");
            await LoadContractSelectAsync(model.ContractId, ct);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("~/Conditions/Edit/{id:long}")]
    public async Task<IActionResult> Edit(long id, CancellationToken ct)
    {
        var e = await _db.ContractConditions.AsNoTracking()
            .Include(x => x.Contract).ThenInclude(c => c.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (e is null) return NotFound();
        ViewBag.ContractLabel = $"{e.Contract.InternalContractNumber} ({e.Contract.Supplier.SupplierCode})";
        await LoadContractSelectAsync(e.ContractId, ct);
        return View(new ConditionFormModel
        {
            Id = e.Id,
            ContractId = e.ContractId,
            ValidFrom = e.ValidFrom,
            ValidTo = e.ValidTo,
            PaymentDelayDays = e.PaymentDelayDays,
            Description = e.Description
        });
    }

    [HttpPost("~/Conditions/Edit/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id,
        [Bind(
            nameof(ConditionFormModel.ValidFrom),
            nameof(ConditionFormModel.ValidTo),
            nameof(ConditionFormModel.PaymentDelayDays),
            nameof(ConditionFormModel.Description))]
        ConditionFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            var cur = await _db.ContractConditions.AsNoTracking()
                .Include(x => x.Contract).ThenInclude(c => c.Supplier)
                .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (cur is null) return NotFound();
            model.ContractId = cur.ContractId;
            ViewBag.ContractLabel = $"{cur.Contract.InternalContractNumber} ({cur.Contract.Supplier.SupplierCode})";
            await LoadContractSelectAsync(model.ContractId, ct);
            return View(model);
        }

        var dto = new ConditionDto
        {
            ContractId = 0,
            ValidFrom = model.ValidFrom,
            ValidTo = model.ValidTo,
            PaymentDelayDays = model.PaymentDelayDays,
            Description = model.Description
        };
        var r = await TryUpdateConditionAsync(id, dto, ct);
        if (!r.Ok)
        {
            if (r.Code == 404) return NotFound();
            ModelState.AddModelError("", r.Error ?? "Ошибка сохранения");
            var cur = await _db.ContractConditions.AsNoTracking()
                .Include(x => x.Contract).ThenInclude(c => c.Supplier)
                .FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
            if (cur is null) return NotFound();
            model.ContractId = cur.ContractId;
            ViewBag.ContractLabel = $"{cur.Contract.InternalContractNumber} ({cur.Contract.Supplier.SupplierCode})";
            await LoadContractSelectAsync(model.ContractId, ct);
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("~/Conditions/Delete/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await TryDeleteConditionAsync(id, ct);
        if (!r.Ok)
            TempData["Error"] = r.Error ?? "Не удалось удалить";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("api/contract-conditions")]
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

    [HttpPost("api/contract-conditions")]
    public async Task<ActionResult> Create([FromBody] ConditionDto dto, CancellationToken ct)
    {
        if (dto == null) return BadRequest(new { message = "Пустое тело запроса" });
        var r = await TryCreateConditionAsync(dto, ct);
        if (!r.Ok) return BadRequest(new { message = r.Error });
        return Ok(new { id = r.NewId });
    }

    [HttpPut("api/contract-conditions/{id:long}")]
    public async Task<ActionResult> Update(long id, [FromBody] ConditionDto dto, CancellationToken ct)
    {
        if (dto == null) return BadRequest(new { message = "Пустое тело запроса" });
        var r = await TryUpdateConditionAsync(id, dto, ct);
        if (!r.Ok) return r.Code == 404 ? NotFound() : BadRequest(new { message = r.Error });
        return Ok();
    }

    [HttpDelete("api/contract-conditions/{id:long}")]
    public async Task<ActionResult> DeleteApi(long id, CancellationToken ct)
    {
        var r = await TryDeleteConditionAsync(id, ct);
        if (!r.Ok) return r.Code == 404 ? NotFound() : BadRequest(new { message = r.Error });
        return Ok();
    }

    private sealed record MutationOk(bool Ok, string? Error, int Code = 0, long? NewId = null);

    private async Task LoadContractSelectAsync(long? selectedId, CancellationToken ct)
    {
        var items = await _db.Contracts.AsNoTracking()
            .Where(c => c.DeletedAt == null)
            .OrderBy(c => c.InternalContractNumber)
            .Take(1000)
            .Select(c => new SelectListItem(
                c.InternalContractNumber + " (" + c.Supplier.SupplierCode + ")",
                c.Id.ToString()))
            .ToListAsync(ct);
        ViewBag.ContractOptions = items;
        ViewData["selectedContractId"] = selectedId;
    }

    private async Task<MutationOk> TryCreateConditionAsync(ConditionDto dto, CancellationToken ct)
    {
        var overlap = await _db.ContractConditions.AnyAsync(c => c.ContractId == dto.ContractId && c.DeletedAt == null
            && c.ValidFrom <= (dto.ValidTo ?? DateOnly.MaxValue)
            && (c.ValidTo == null || c.ValidTo >= dto.ValidFrom), ct);
        if (overlap) return new MutationOk(false, "Пересечение периодов условий");

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
        return new MutationOk(true, null, 0, e.Id);
    }

    private async Task<MutationOk> TryUpdateConditionAsync(long id, ConditionDto dto, CancellationToken ct)
    {
        var e = await _db.ContractConditions.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (e is null) return new MutationOk(false, null, 404);
        e.ValidFrom = dto.ValidFrom;
        e.ValidTo = dto.ValidTo;
        e.PaymentDelayDays = dto.PaymentDelayDays;
        e.Description = dto.Description;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("condition.update", "contract_condition", id.ToString(), ct: ct);
        return new MutationOk(true, null);
    }

    private async Task<MutationOk> TryDeleteConditionAsync(long id, CancellationToken ct)
    {
        var e = await _db.ContractConditions.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (e is null) return new MutationOk(false, null, 404);
        var used = await _db.PaymentScheduleAllocations.AnyAsync(a => a.ContractConditionId == id, ct);
        if (used) return new MutationOk(false, "Условие используется в расчёте", 400);
        e.DeletedAt = DateTime.UtcNow;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("condition.soft_delete", "contract_condition", id.ToString(), ct: ct);
        return new MutationOk(true, null);
    }
}
