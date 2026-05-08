using ForecastApp1.Data;
using ForecastApp1.Models;
using ForecastApp1.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Controllers;

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Accountant}")]
public class DictionariesController : Controller
{
    private readonly ApplicationDbContext _db;

    public DictionariesController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? q, CancellationToken ct)
    {
        var supQ = _db.Suppliers.AsNoTracking().Where(s => s.DeletedAt == null);
        var ctrQ = _db.Contracts.AsNoTracking().Include(c => c.Supplier).Where(c => c.DeletedAt == null);
        var condQ = _db.ContractConditions.AsNoTracking().Include(c => c.Contract).Where(c => c.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(q))
        {
            supQ = supQ.Where(s => s.SupplierCode.Contains(q) || s.Name.Contains(q));
            ctrQ = ctrQ.Where(c => c.InternalContractNumber.Contains(q) || c.Supplier.SupplierCode.Contains(q) || c.Supplier.Name.Contains(q));
            condQ = condQ.Where(c => c.Contract.InternalContractNumber.Contains(q));
        }

        var suppliers = await supQ.OrderBy(s => s.SupplierCode).Take(500)
            .Select(s => new SupplierListRow(s.Id, s.SupplierCode, s.Name)).ToListAsync(ct);
        var contracts = await ctrQ.OrderBy(c => c.InternalContractNumber).Take(500)
            .Select(c => new ContractListRow(c.Id, c.SupplierId, c.Supplier.SupplierCode, c.InternalContractNumber, c.ExternalContractNumber, c.ContractDate, c.ContractAmount))
            .ToListAsync(ct);
        var conditions = await condQ.OrderBy(c => c.Contract.InternalContractNumber).ThenBy(c => c.ValidFrom).Take(500)
            .Select(c => new ConditionListRow(c.Id, c.ContractId, c.Contract.InternalContractNumber, c.ValidFrom, c.ValidTo, c.PaymentDelayDays, c.Description))
            .ToListAsync(ct);

        ViewData["q"] = q;
        ViewBag.Suppliers = suppliers;
        ViewBag.Contracts = contracts;
        ViewBag.Conditions = conditions;
        return View();
    }
}
