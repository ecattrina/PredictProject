using ForecastApp1.Data;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/debts")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class DebtsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DebtsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] long? supplierId, [FromQuery] long? contractId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var q = _db.IncomingDebtSnapshots.AsNoTracking()
            .Include(d => d.Supplier).Include(d => d.Contract)
            .Where(d => d.DeletedAt == null);
        if (supplierId.HasValue) q = q.Where(d => d.SupplierId == supplierId);
        if (contractId.HasValue) q = q.Where(d => d.ContractId == contractId);
        if (from.HasValue) q = q.Where(d => d.DebtDate >= from);
        if (to.HasValue) q = q.Where(d => d.DebtDate <= to);
        var list = await q.OrderByDescending(d => d.DebtDate).Take(1000)
            .Select(d => new
            {
                d.Id,
                d.SupplierId,
                SupplierCode = d.Supplier.SupplierCode,
                d.ContractId,
                Contract = d.Contract.InternalContractNumber,
                d.DebtDate,
                d.DebtAmount,
                d.SourceImportBatchId
            }).ToListAsync(ct);
        return Ok(list);
    }
}
