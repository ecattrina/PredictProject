using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using ForecastApp1.Services.Calendar;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Services.Calculation;

public interface IPaymentCalculationService
{
    Task<long> RunAsync(DateOnly calculationDate, long userId, long? filterSupplierId, long? filterContractId, string calendarCode, CancellationToken ct);
}

public sealed class PaymentCalculationService : IPaymentCalculationService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendarService _calendar;

    public PaymentCalculationService(ApplicationDbContext db, IBusinessCalendarService calendar)
    {
        _db = db;
        _calendar = calendar;
    }

    public async Task<long> RunAsync(DateOnly calculationDate, long userId, long? filterSupplierId, long? filterContractId, string calendarCode, CancellationToken ct)
    {
        var run = new PaymentCalculationRun
        {
            RunCode = Guid.NewGuid(),
            CalculationDate = calculationDate,
            CalendarCode = calendarCode,
            FilterSupplierId = filterSupplierId,
            FilterContractId = filterContractId,
            Status = "running",
            Archived = false,
            StartedByUserId = userId,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.PaymentCalculationRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        try
        {
            var debtCandidates = await _db.IncomingDebtSnapshots.AsNoTracking()
                .Where(d => d.DeletedAt == null && d.DebtDate <= calculationDate)
                .Where(d => !filterSupplierId.HasValue || d.SupplierId == filterSupplierId)
                .Where(d => !filterContractId.HasValue || d.ContractId == filterContractId)
                .ToListAsync(ct);

            var latestDebts = debtCandidates
                .GroupBy(d => new { d.SupplierId, d.ContractId })
                .Select(g => g.OrderByDescending(x => x.DebtDate).ThenByDescending(x => x.Id).First())
                .ToList();

            foreach (var debt in latestDebts)
            {
                var shipments = await _db.ActualShipments
                    .Where(s => s.DeletedAt == null && s.SupplierId == debt.SupplierId && s.ContractId == debt.ContractId)
                    .OrderByDescending(s => s.ShipmentDocDate)
                    .ThenByDescending(s => s.Id)
                    .ToListAsync(ct);

                var remaining = debt.DebtAmount;
                foreach (var sh in shipments)
                {
                    if (remaining <= 0) break;
                    var take = Math.Min(remaining, sh.ShipmentAmount);
                    if (take <= 0) continue;

                    var cond = await ResolveConditionAsync(debt.ContractId, sh.ShipmentDocDate, ct);
                    var delay = cond?.PaymentDelayDays ?? 0;
                    var warn = cond is null ? "Нет условия договора на дату отгрузки, отсрочка принята 0" : null;

                    var rawDue = sh.ShipmentDocDate.AddDays(delay);
                    var afterCond = await _calendar.NextWorkingDayOnOrAfterAsync(rawDue, calendarCode, ct);
                    var shiftedWeekend = afterCond != rawDue;

                    var raisedToCalc = false;
                    DateOnly forecast;
                    if (afterCond < calculationDate)
                    {
                        raisedToCalc = true;
                        forecast = await _calendar.NextWorkingDayOnOrAfterAsync(calculationDate, calendarCode, ct);
                    }
                    else
                    {
                        forecast = await _calendar.NextWorkingDayOnOrAfterAsync(afterCond, calendarCode, ct);
                    }

                    var overdue = rawDue < calculationDate;

                    var item = new PaymentScheduleItem
                    {
                        CalculationRunId = run.Id,
                        SupplierId = debt.SupplierId,
                        ContractId = debt.ContractId,
                        IncomingDebtSnapshotId = debt.Id,
                        ActualShipmentId = sh.Id,
                        OrderNumber = sh.OrderNumber,
                        ShipmentDocNumber = sh.ShipmentDocNumber,
                        ShipmentDocDate = sh.ShipmentDocDate,
                        PayAmount = take,
                        DueDateByCondition = afterCond,
                        ForecastPaymentDate = forecast,
                        AppliedDelayDays = delay,
                        IsOverdueVsCalcDate = overdue,
                        ShiftedForWeekendOrHoliday = shiftedWeekend,
                        RaisedToCalculationDate = raisedToCalc,
                        WarningText = warn,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.PaymentScheduleItems.Add(item);
                    await _db.SaveChangesAsync(ct);

                    _db.PaymentScheduleAllocations.Add(new PaymentScheduleAllocation
                    {
                        CalculationRunId = run.Id,
                        ScheduleItemId = item.Id,
                        IncomingDebtSnapshotId = debt.Id,
                        ActualShipmentId = sh.Id,
                        AllocatedAmount = take,
                        DebtAmountBefore = debt.DebtAmount,
                        ShipmentAmountTotal = sh.ShipmentAmount,
                        ContractConditionId = cond?.Id,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync(ct);

                    remaining -= take;
                }

                if (remaining > 0)
                {
                    _db.UncoveredDebtItems.Add(new UncoveredDebtItem
                    {
                        CalculationRunId = run.Id,
                        IncomingDebtSnapshotId = debt.Id,
                        UncoveredAmount = remaining,
                        WarningText = "Долг покрыт не полностью: не хватило отгрузок",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            run.Status = "completed";
            run.FinishedAt = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return run.Id;
        }
        catch (Exception ex)
        {
            run.Status = "failed";
            run.ErrorMessage = ex.Message;
            run.FinishedAt = DateTime.UtcNow;
            run.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task<ContractCondition?> ResolveConditionAsync(long contractId, DateOnly shipDate, CancellationToken ct)
    {
        return await _db.ContractConditions.AsNoTracking()
            .Where(c => c.ContractId == contractId && c.DeletedAt == null)
            .Where(c => c.ValidFrom <= shipDate && (c.ValidTo == null || c.ValidTo >= shipDate))
            .OrderByDescending(c => c.ValidFrom)
            .FirstOrDefaultAsync(ct);
    }
}
