using System.Globalization;
using ForecastApp1.Data;
using ForecastApp1.Dtos;
using ForecastApp1.Security;
using ForecastApp1.Services;
using ForecastApp1.Services.Calculation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/calculations")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class CalculationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentCalculationService _calc;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;

    public CalculationsController(ApplicationDbContext db, IPaymentCalculationService calc, ICurrentUser user, IAuditService audit)
    {
        _db = db;
        _calc = calc;
        _user = user;
        _audit = audit;
    }

    [HttpPost("run")]
    public async Task<ActionResult> Run([FromBody] RunCalculationRequest req, CancellationToken ct)
    {
        try
        {
            var uid = _user.UserId ?? throw new InvalidOperationException("Нет пользователя");
            var cal = req.CalendarCode ?? "default";

            var (supplierId, contractId) = await ResolveRunFiltersAsync(req.SupplierId, req.ContractId, ct);
            if (req.SupplierId.HasValue && !supplierId.HasValue)
                return BadRequest(new { message = "Поставщик не найден: укажите id записи в БД или код поставщика (например 10001)." });
            if (req.ContractId.HasValue && !contractId.HasValue)
                return BadRequest(new { message = "Договор не найден: укажите id записи в БД или внутренний номер договора (например 245)." });

            var id = await _calc.RunAsync(req.CalculationDate, uid, supplierId, contractId, cal, ct);
            await _audit.WriteAsync("calc.run", calculationRunId: id, payload: req, ct: ct);
            return Ok(new { runId = id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// В форму часто вводят код поставщика и номер договора из Excel, а не PK. Сначала ищем по id, иначе по коду/номеру.
    /// </summary>
    private async Task<(long? SupplierId, long? ContractId)> ResolveRunFiltersAsync(
        long? reqSupplierKey,
        long? reqContractKey,
        CancellationToken ct)
    {
        long? supplierId = null;
        if (reqSupplierKey.HasValue)
        {
            var byId = await _db.Suppliers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == reqSupplierKey && s.DeletedAt == null, ct);
            if (byId is not null)
                supplierId = byId.Id;
            else
            {
                var code = reqSupplierKey.Value.ToString(CultureInfo.InvariantCulture);
                var byCode = await _db.Suppliers.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierCode == code && s.DeletedAt == null, ct);
                supplierId = byCode?.Id;
            }
        }

        long? contractId = null;
        if (reqContractKey.HasValue)
        {
            var byId = await _db.Contracts.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == reqContractKey && c.DeletedAt == null, ct);
            if (byId is not null)
                contractId = byId.Id;
            else
            {
                var num = reqContractKey.Value.ToString(CultureInfo.InvariantCulture);
                var q = _db.Contracts.AsNoTracking().Where(c => c.InternalContractNumber == num && c.DeletedAt == null);
                if (supplierId.HasValue)
                    q = q.Where(c => c.SupplierId == supplierId.Value);
                var byNum = await q.FirstOrDefaultAsync(ct);
                contractId = byNum?.Id;
            }
        }

        if (supplierId.HasValue && contractId.HasValue)
        {
            var pairOk = await _db.Contracts.AsNoTracking()
                .AnyAsync(c => c.Id == contractId && c.SupplierId == supplierId && c.DeletedAt == null, ct);
            if (!pairOk)
                throw new InvalidOperationException("Договор не принадлежит выбранному поставщику или не найден.");
        }

        return (supplierId, contractId);
    }

    [HttpGet]
    public async Task<ActionResult> List(CancellationToken ct)
    {
        var list = await _db.PaymentCalculationRuns.AsNoTracking()
            .Where(r => r.DeletedAt == null)
            .OrderByDescending(r => r.Id)
            .Take(100)
            .Select(r => new { r.Id, r.RunCode, r.CalculationDate, r.Status, r.Archived, r.StartedAt, r.FinishedAt })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult> Get(long id, CancellationToken ct)
    {
        var r = await _db.PaymentCalculationRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (r is null) return NotFound();
        return Ok(new
        {
            r.Id,
            r.RunCode,
            r.CalculationDate,
            r.Status,
            r.Archived,
            r.CalendarCode,
            r.FilterSupplierId,
            r.FilterContractId,
            r.StartedAt,
            r.FinishedAt,
            r.ErrorMessage,
            items = await _db.PaymentScheduleItems.CountAsync(i => i.CalculationRunId == id && i.DeletedAt == null, ct),
            uncovered = await _db.UncoveredDebtItems.CountAsync(u => u.CalculationRunId == id, ct)
        });
    }

    [HttpGet("{id:long}/schedule")]
    public async Task<ActionResult<IReadOnlyList<ScheduleItemDto>>> Schedule(long id, CancellationToken ct)
    {
        var list = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == id && i.DeletedAt == null)
            .Include(i => i.Supplier).Include(i => i.Contract)
            .OrderBy(i => i.ForecastPaymentDate).ThenBy(i => i.Id)
            .Select(i => new ScheduleItemDto(
                i.Id,
                i.ForecastPaymentDate,
                i.PayAmount,
                i.SupplierId,
                i.Supplier.Name,
                i.ContractId,
                i.Contract.InternalContractNumber,
                i.ShipmentDocNumber,
                i.ShipmentDocDate,
                i.WarningText))
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:long}/schedule/{itemId:long}/details")]
    public async Task<ActionResult<ScheduleItemDetailsDto>> Details(long id, long itemId, CancellationToken ct)
    {
        var i = await _db.PaymentScheduleItems.AsNoTracking()
            .Include(x => x.Supplier).Include(x => x.Contract)
            .FirstOrDefaultAsync(x => x.Id == itemId && x.CalculationRunId == id && x.DeletedAt == null, ct);
        if (i is null) return NotFound();
        var alloc = await _db.PaymentScheduleAllocations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ScheduleItemId == itemId, ct);
        var debt = await _db.IncomingDebtSnapshots.AsNoTracking().FirstAsync(d => d.Id == i.IncomingDebtSnapshotId, ct);
        var ship = await _db.ActualShipments.AsNoTracking().FirstAsync(s => s.Id == i.ActualShipmentId, ct);

        var dto = new ScheduleItemDto(
            i.Id,
            i.ForecastPaymentDate,
            i.PayAmount,
            i.SupplierId,
            i.Supplier.Name,
            i.ContractId,
            i.Contract.InternalContractNumber,
            i.ShipmentDocNumber,
            i.ShipmentDocDate,
            i.WarningText);

        var details = new ScheduleItemDetailsDto(
            dto,
            debt.Id,
            debt.DebtDate,
            debt.DebtAmount,
            ship.Id,
            ship.ShipmentAmount,
            alloc?.AllocatedAmount ?? i.PayAmount,
            alloc?.ContractConditionId,
            i.AppliedDelayDays,
            i.DueDateByCondition,
            i.ShiftedForWeekendOrHoliday,
            i.RaisedToCalculationDate,
            i.IsOverdueVsCalcDate,
            i.WarningText);

        return Ok(details);
    }

    [HttpPost("{id:long}/archive")]
    public async Task<ActionResult> Archive(long id, CancellationToken ct)
    {
        var r = await _db.PaymentCalculationRuns.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (r is null) return NotFound();
        r.Archived = true;
        r.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("calc.archive", calculationRunId: id, ct: ct);
        return Ok();
    }
}
