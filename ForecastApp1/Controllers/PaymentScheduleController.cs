using ForecastApp1.Data;
using ForecastApp1.Models;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class PaymentScheduleController : Controller
{
    private readonly ApplicationDbContext _db;

    public PaymentScheduleController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var runs = await _db.PaymentCalculationRuns.AsNoTracking()
            .Where(r => r.DeletedAt == null)
            .OrderByDescending(r => r.Id)
            .Take(50)
            .Select(r => new CalculationRunListRow(
                r.Id,
                r.RunCode,
                r.CalculationDate,
                r.Status,
                r.Archived,
                r.StartedAt,
                r.FinishedAt))
            .ToListAsync(ct);
        return View(runs);
    }
}
