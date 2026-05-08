using ForecastApp1.Data;
using ForecastApp1.Models;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ImportController : Controller
{
    private readonly ApplicationDbContext _db;

    public ImportController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var batches = await _db.ImportBatches.AsNoTracking()
            .OrderByDescending(b => b.Id)
            .Take(50)
            .ToListAsync(ct);
        var rows = new List<ImportBatchListRow>();
        foreach (var b in batches)
        {
            var rc = await _db.ImportRows.CountAsync(r => r.ImportBatchId == b.Id, ct);
            var ec = await _db.ImportErrors.CountAsync(e => e.ImportBatchId == b.Id, ct);
            rows.Add(new ImportBatchListRow(b.Id, b.OriginalFileName, b.Status, b.CreatedAt, rc, ec));
        }

        return View(rows);
    }
}
