using ForecastApp1.Data;
using ForecastApp1.Models;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _db;

    public PaymentsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var items = await _db.PaymentScheduleItems.AsNoTracking()
            .Include(i => i.CalculationRun)
            .Where(i => i.DeletedAt == null
                && i.CalculationRun.DeletedAt == null
                && (i.CalculationRun.Status == "completed" || i.CalculationRun.Status == "completed_with_warnings"))
            .Include(i => i.Supplier)
            .Include(i => i.Contract)
            .OrderBy(i => i.ForecastPaymentDate).ThenBy(i => i.Id)
            .Take(5000)
            .Select(i => new PaymentSchedulePreviewRow(
                i.Id,
                i.CalculationRunId,
                i.ForecastPaymentDate,
                i.PayAmount,
                i.Supplier.Name,
                i.Contract.InternalContractNumber))
            .ToListAsync(ct);

        return View(new PaymentsIndexVm(null, items));
    }
}
