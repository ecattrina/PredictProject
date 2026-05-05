using ForecastApp1.Data;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[ApiController]
[Route("api/shipments")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class ShipmentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ShipmentsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult> List([FromQuery] long? supplierId, [FromQuery] long? contractId, CancellationToken ct)
    {
        var q = _db.ActualShipments.AsNoTracking()
            .Include(s => s.Supplier).Include(s => s.Contract)
            .Where(s => s.DeletedAt == null);
        if (supplierId.HasValue) q = q.Where(s => s.SupplierId == supplierId);
        if (contractId.HasValue) q = q.Where(s => s.ContractId == contractId);
        var list = await q.OrderByDescending(s => s.ShipmentDocDate).Take(1000)
            .Select(s => new
            {
                s.Id,
                s.SupplierId,
                SupplierCode = s.Supplier.SupplierCode,
                s.ContractId,
                Contract = s.Contract.InternalContractNumber,
                s.OrderNumber,
                s.ShipmentDocNumber,
                s.ShipmentDocDate,
                s.ShipmentAmount,
                s.SourceImportBatchId
            }).ToListAsync(ct);
        return Ok(list);
    }
}
