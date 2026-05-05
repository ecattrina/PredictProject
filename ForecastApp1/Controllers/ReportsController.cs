using System.Globalization;
using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ReportsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ReportsController(ApplicationDbContext db) => _db = db;

    /// <summary>
    /// Все строки графика по «живым» расчётам: не удалённым и не в архиве (без фильтра по статусу выполнения).
    /// </summary>
    private IQueryable<PaymentScheduleItem> ScheduleItemsForAggregateReport()
    {
        var runIds = _db.PaymentCalculationRuns.AsNoTracking()
            .Where(r => r.DeletedAt == null && !r.Archived)
            .Select(r => r.Id);
        return _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.DeletedAt == null && runIds.Contains(i.CalculationRunId));
    }

    [HttpGet("payments-by-day")]
    public async Task<ActionResult> PaymentsByDay([FromQuery] long runId, CancellationToken ct)
    {
        var q = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runId && i.DeletedAt == null)
            .GroupBy(i => i.ForecastPaymentDate)
            .Select(g => new { date = g.Key, total = g.Sum(x => x.PayAmount) })
            .OrderBy(x => x.date)
            .ToListAsync(ct);
        return Ok(q);
    }

    [HttpGet("payments-by-week")]
    public async Task<ActionResult> PaymentsByWeek([FromQuery] long runId, CancellationToken ct)
    {
        var items = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runId && i.DeletedAt == null)
            .Select(i => new { i.ForecastPaymentDate, i.PayAmount })
            .ToListAsync(ct);
        var q = items.GroupBy(x => ISOWeekKey(x.ForecastPaymentDate))
            .Select(g => new { week = g.Key, total = g.Sum(x => x.PayAmount) })
            .OrderBy(x => x.week)
            .ToList();
        return Ok(q);
    }

    [HttpGet("payments-by-month")]
    public async Task<ActionResult> PaymentsByMonth([FromQuery] long runId, CancellationToken ct)
    {
        var items = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runId && i.DeletedAt == null)
            .Select(i => new { i.ForecastPaymentDate, i.PayAmount })
            .ToListAsync(ct);
        var q = items.GroupBy(x => new { x.ForecastPaymentDate.Year, x.ForecastPaymentDate.Month })
            .Select(g => new { y = g.Key.Year, m = g.Key.Month, total = g.Sum(x => x.PayAmount) })
            .OrderBy(x => x.y).ThenBy(x => x.m)
            .ToList();
        return Ok(q);
    }

    /// <summary>
    /// Данные для главной страницы отчётов: доли поставщиков (круговая) и помесячные выплаты (столбцы).
    /// Учитываются все неархивные расчёты; статус «завершён / нет» не различается.
    /// </summary>
    /// <param name="supplierCode">
    /// Код поставщика (<see cref="Supplier.SupplierCode"/>): столбцы — только по этому поставщику.
    /// Пусто или не найден — столбцы по сводной сумме по всем поставщикам за месяц.
    /// </param>
    [HttpGet("payments-by-month-and-supplier")]
    public async Task<ActionResult> PaymentsByMonthAndSupplier(
        [FromQuery] string? supplierCode,
        CancellationToken ct)
    {
        var codeFilter = supplierCode?.Trim();
        var items = await ScheduleItemsForAggregateReport()
            .Select(i => new
            {
                i.ForecastPaymentDate,
                i.PayAmount,
                i.SupplierId,
                SupplierCode = i.Supplier.SupplierCode,
                SupplierName = i.Supplier.Name
            })
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            return Ok(new
            {
                monthKeys = Array.Empty<string>(),
                monthLabels = Array.Empty<string>(),
                monthlyAmounts = Array.Empty<decimal>(),
                suppliers = Array.Empty<object>(),
                scheduleLineCount = 0,
                supplierCodeRequested = codeFilter,
                supplierResolved = false,
                supplierName = (string?)null
            });
        }

        var monthSet = items
            .Select(x => (x.ForecastPaymentDate.Year, x.ForecastPaymentDate.Month))
            .Distinct()
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToList();

        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var monthKeys = monthSet.Select(x => $"{x.Year:0000}-{x.Month:00}").ToList();
        var monthLabels = monthSet.Select(x =>
        {
            var d = new DateOnly(x.Year, x.Month, 1);
            var s = d.ToString("MMMM yyyy", culture);
            return s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;
        }).ToList();

        var supplierOrder = items
            .GroupBy(x => new { x.SupplierId, x.SupplierCode, x.SupplierName })
            .Select(g => new { g.Key.SupplierId, g.Key.SupplierCode, g.Key.SupplierName, Sum = g.Sum(x => x.PayAmount) })
            .OrderByDescending(x => x.Sum)
            .ThenBy(x => x.SupplierCode)
            .ToList();

        var suppliers = supplierOrder
            .Select(s => (object)new
            {
                supplierId = s.SupplierId,
                supplierCode = s.SupplierCode,
                name = s.SupplierName,
                total = s.Sum
            })
            .ToList();

        long? resolvedSupplierId = null;
        string? resolvedName = null;
        if (!string.IsNullOrEmpty(codeFilter))
        {
            var row = await _db.Suppliers.AsNoTracking()
                .Where(x => x.SupplierCode == codeFilter && x.DeletedAt == null)
                .Select(x => new { x.Id, x.Name })
                .FirstOrDefaultAsync(ct);
            if (row != null)
            {
                resolvedSupplierId = row.Id;
                resolvedName = row.Name;
            }
        }

        var monthlyAmounts = new decimal[monthSet.Count];
        for (var i = 0; i < monthSet.Count; i++)
        {
            var (y, m) = monthSet[i];
            var row = items.Where(x => x.ForecastPaymentDate.Year == y && x.ForecastPaymentDate.Month == m);
            monthlyAmounts[i] = resolvedSupplierId.HasValue
                ? row.Where(x => x.SupplierId == resolvedSupplierId.Value).Sum(x => x.PayAmount)
                : row.Sum(x => x.PayAmount);
        }

        return Ok(new
        {
            monthKeys,
            monthLabels,
            monthlyAmounts,
            suppliers,
            scheduleLineCount = items.Count,
            supplierCodeRequested = codeFilter,
            supplierResolved = resolvedSupplierId.HasValue,
            supplierName = resolvedName
        });
    }

    [HttpGet("payments-by-supplier")]
    public async Task<ActionResult> BySupplier([FromQuery] long runId, CancellationToken ct)
    {
        var q = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runId && i.DeletedAt == null)
            .Include(i => i.Supplier)
            .GroupBy(i => new { i.SupplierId, i.Supplier.Name })
            .Select(g => new { g.Key.SupplierId, g.Key.Name, total = g.Sum(x => x.PayAmount) })
            .OrderByDescending(x => x.total)
            .ToListAsync(ct);
        return Ok(q);
    }

    [HttpGet("payments-by-contract")]
    public async Task<ActionResult> ByContract([FromQuery] long runId, CancellationToken ct)
    {
        var q = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runId && i.DeletedAt == null)
            .Include(i => i.Contract)
            .GroupBy(i => new { i.ContractId, i.Contract.InternalContractNumber })
            .Select(g => new { g.Key.ContractId, g.Key.InternalContractNumber, total = g.Sum(x => x.PayAmount) })
            .OrderByDescending(x => x.total)
            .ToListAsync(ct);
        return Ok(q);
    }

    [HttpGet("overdue-payments")]
    public async Task<ActionResult> Overdue([FromQuery] long runId, CancellationToken ct)
    {
        var q = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runId && i.DeletedAt == null && i.IsOverdueVsCalcDate)
            .Select(i => new { i.Id, i.ForecastPaymentDate, i.PayAmount, i.SupplierId, i.ContractId })
            .ToListAsync(ct);
        return Ok(q);
    }

    [HttpGet("uncovered-debts")]
    public async Task<ActionResult> Uncovered([FromQuery] long runId, CancellationToken ct)
    {
        var q = await _db.UncoveredDebtItems.AsNoTracking()
            .Where(u => u.CalculationRunId == runId)
            .Select(u => new { u.IncomingDebtSnapshotId, u.UncoveredAmount, u.WarningText })
            .ToListAsync(ct);
        return Ok(q);
    }

    [HttpGet("import-quality")]
    public async Task<ActionResult> ImportQuality([FromQuery] long? batchId, CancellationToken ct)
    {
        var q = _db.ImportErrors.AsNoTracking();
        if (batchId.HasValue) q = q.Where(e => e.ImportBatchId == batchId);
        var list = await q.GroupBy(e => new { e.Severity, e.ErrorCode })
            .Select(g => new { g.Key.Severity, g.Key.ErrorCode, count = g.Count() })
            .OrderByDescending(x => x.count)
            .Take(200)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("calculation-comparison")]
    public async Task<ActionResult> Compare([FromQuery] long runA, [FromQuery] long runB, CancellationToken ct)
    {
        var a = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runA && i.DeletedAt == null)
            .GroupBy(i => i.ForecastPaymentDate)
            .Select(g => new { date = g.Key, total = g.Sum(x => x.PayAmount) })
            .ToListAsync(ct);
        var b = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runB && i.DeletedAt == null)
            .GroupBy(i => i.ForecastPaymentDate)
            .Select(g => new { date = g.Key, total = g.Sum(x => x.PayAmount) })
            .ToListAsync(ct);
        var allDates = a.Select(x => x.date).Union(b.Select(x => x.date)).Distinct().OrderBy(d => d).ToList();
        var mapA = a.ToDictionary(x => x.date, x => x.total);
        var mapB = b.ToDictionary(x => x.date, x => x.total);
        var result = allDates.Select(d => new
        {
            date = d,
            totalA = mapA.GetValueOrDefault(d),
            totalB = mapB.GetValueOrDefault(d),
            delta = mapA.GetValueOrDefault(d) - mapB.GetValueOrDefault(d)
        }).ToList();
        return Ok(result);
    }

    [HttpGet("debt-dynamics")]
    public async Task<ActionResult> DebtDynamics([FromQuery] long supplierId, [FromQuery] long contractId, CancellationToken ct)
    {
        var q = await _db.IncomingDebtSnapshots.AsNoTracking()
            .Where(d => d.DeletedAt == null && d.SupplierId == supplierId && d.ContractId == contractId)
            .OrderBy(d => d.DebtDate)
            .Select(d => new { d.DebtDate, d.DebtAmount })
            .ToListAsync(ct);
        return Ok(q);
    }

    [HttpGet("payments-horizon")]
    public async Task<ActionResult> Horizon([FromQuery] long runId, [FromQuery] int days, CancellationToken ct)
    {
        if (days is not (7 or 14 or 30)) days = 7;
        var run = await _db.PaymentCalculationRuns.AsNoTracking().FirstAsync(r => r.Id == runId, ct);
        var to = run.CalculationDate.AddDays(days);
        var q = await _db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.CalculationRunId == runId && i.DeletedAt == null
                && i.ForecastPaymentDate >= run.CalculationDate && i.ForecastPaymentDate <= to)
            .GroupBy(i => i.ForecastPaymentDate)
            .Select(g => new { date = g.Key, total = g.Sum(x => x.PayAmount) })
            .OrderBy(x => x.date)
            .ToListAsync(ct);
        return Ok(new { run.CalculationDate, days, items = q });
    }

    private static string ISOWeekKey(DateOnly d)
    {
        var dt = d.ToDateTime(TimeOnly.MinValue);
        var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
        var week = cal.GetWeekOfYear(dt, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return $"{dt.Year}-W{week:00}";
    }
}
