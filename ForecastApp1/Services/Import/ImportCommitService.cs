using System.Security.Cryptography;
using System.Text;
using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Services.Import;

public interface IImportCommitService
{
    Task<(bool Ok, string Message)> CommitAsync(long batchId, bool confirmWarnings, CancellationToken ct);
}

public sealed class ImportCommitService : IImportCommitService
{
    private readonly ApplicationDbContext _db;

    public ImportCommitService(ApplicationDbContext db) => _db = db;

    public async Task<(bool Ok, string Message)> CommitAsync(long batchId, bool confirmWarnings, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
        if (batch.Status == ImportBatchStatuses.Committed)
            return (false, "Пакет уже подтверждён");
        if (batch.Status != ImportBatchStatuses.AwaitingCommit)
            return (false, "Подтверждение возможно только после успешной валидации (статус awaiting_commit)");

        var blocking = await _db.ImportErrors.AnyAsync(e => e.ImportBatchId == batchId && e.Severity == ImportSeverities.Blocking, ct);
        if (blocking)
            return (false, "Есть блокирующие ошибки. Подтверждение невозможно.");

        if (!confirmWarnings && await _db.ImportErrors.AnyAsync(e => e.ImportBatchId == batchId && e.Severity == ImportSeverities.Warning, ct))
            return (false, "Есть предупреждения. Подтвердите с флагом confirmWarnings.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await UpsertSuppliersAsync(batchId, ct);
            await UpsertContractsAsync(batchId, ct);
            await UpsertConditionsAsync(batchId, ct);
            await UpsertDebtsAsync(batchId, ct);
            await UpsertShipmentsAsync(batchId, ct);
            await UpsertOrdersAsync(batchId, ct);

            batch.Status = ImportBatchStatuses.Committed;
            batch.CommittedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (true, "Импорт применён");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return (false, ex.Message);
        }
    }

    private static string HashRow(params string[] parts)
    {
        var raw = string.Join("|", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private async Task UpsertSuppliersAsync(long batchId, CancellationToken ct)
    {
        foreach (var s in await _db.StagingSuppliers.Where(x => x.ImportBatchId == batchId).ToListAsync(ct))
        {
            var code = s.SupplierCode.Trim();
            var ex = await _db.Suppliers.FirstOrDefaultAsync(x => x.SupplierCode == code && x.DeletedAt == null, ct);
            if (ex is null)
            {
                _db.Suppliers.Add(new Supplier
                {
                    SupplierCode = code,
                    Name = s.Name.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                ex.Name = s.Name.Trim();
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertContractsAsync(long batchId, CancellationToken ct)
    {
        foreach (var c in await _db.StagingContracts.Where(x => x.ImportBatchId == batchId).ToListAsync(ct))
        {
            var supplier = await _db.Suppliers.FirstAsync(s => s.SupplierCode == c.SupplierCode.Trim() && s.DeletedAt == null, ct);
            var num = c.InternalContractNumber.Trim();
            var ex = await _db.Contracts.FirstOrDefaultAsync(x => x.SupplierId == supplier.Id && x.InternalContractNumber == num && x.DeletedAt == null, ct);
            if (ex is null)
            {
                _db.Contracts.Add(new Contract
                {
                    SupplierId = supplier.Id,
                    InternalContractNumber = num,
                    ExternalContractNumber = c.ExternalContractNumber?.Trim(),
                    ContractDate = c.ContractDate,
                    ContractAmount = c.ContractAmount,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                ex.ExternalContractNumber = c.ExternalContractNumber?.Trim();
                ex.ContractDate = c.ContractDate ?? ex.ContractDate;
                ex.ContractAmount = c.ContractAmount ?? ex.ContractAmount;
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertConditionsAsync(long batchId, CancellationToken ct)
    {
        foreach (var c in await _db.StagingContractConditions.Where(x => x.ImportBatchId == batchId).ToListAsync(ct))
        {
            var contractNum = (c.InternalContractNumber ?? c.ContractKey).Trim();
            var supplierCode = c.SupplierCode?.Trim();
            Contract contract;
            if (!string.IsNullOrEmpty(supplierCode))
                contract = await _db.Contracts.Include(x => x.Supplier)
                    .FirstAsync(x => x.InternalContractNumber == contractNum && x.Supplier.SupplierCode == supplierCode && x.DeletedAt == null, ct);
            else
                contract = await _db.Contracts.FirstAsync(x => x.InternalContractNumber == contractNum && x.DeletedAt == null, ct);

            var from = c.ValidFrom ?? new DateOnly(2000, 1, 1);
            var ex = await _db.ContractConditions.FirstOrDefaultAsync(x => x.ContractId == contract.Id && x.ValidFrom == from && x.DeletedAt == null, ct);
            if (ex is null)
            {
                _db.ContractConditions.Add(new ContractCondition
                {
                    ContractId = contract.Id,
                    ValidFrom = from,
                    ValidTo = c.ValidTo,
                    PaymentDelayDays = c.PaymentDelayDays,
                    SourceImportBatchId = batchId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                ex.ValidTo = c.ValidTo;
                ex.PaymentDelayDays = c.PaymentDelayDays;
                ex.SourceImportBatchId = batchId;
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertDebtsAsync(long batchId, CancellationToken ct)
    {
        foreach (var d in await _db.StagingIncomingDebts.Where(x => x.ImportBatchId == batchId).ToListAsync(ct))
        {
            var supplier = await _db.Suppliers.FirstAsync(s => s.SupplierCode == d.SupplierCode.Trim() && s.DeletedAt == null, ct);
            var contract = await _db.Contracts.FirstAsync(c => c.SupplierId == supplier.Id && c.InternalContractNumber == d.InternalContractNumber.Trim() && c.DeletedAt == null, ct);
            var hash = HashRow(supplier.Id.ToString(), contract.Id.ToString(), d.DebtDate.ToString("O"), d.DebtAmount.ToString("F2"));
            var ex = await _db.IncomingDebtSnapshots.FirstOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ContractId == contract.Id && x.DebtDate == d.DebtDate && x.DeletedAt == null, ct);
            if (ex is null)
            {
                _db.IncomingDebtSnapshots.Add(new IncomingDebtSnapshot
                {
                    SupplierId = supplier.Id,
                    ContractId = contract.Id,
                    DebtDate = d.DebtDate,
                    DebtAmount = d.DebtAmount,
                    SourceImportBatchId = batchId,
                    SourceRowHash = hash,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                ex.DebtAmount = d.DebtAmount;
                ex.SourceImportBatchId = batchId;
                ex.SourceRowHash = hash;
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertShipmentsAsync(long batchId, CancellationToken ct)
    {
        foreach (var s in await _db.StagingActualShipments.Where(x => x.ImportBatchId == batchId).ToListAsync(ct))
        {
            var supplier = await _db.Suppliers.FirstAsync(x => x.SupplierCode == s.SupplierCode.Trim() && x.DeletedAt == null, ct);
            var contract = await _db.Contracts.FirstAsync(c => c.SupplierId == supplier.Id && c.InternalContractNumber == s.InternalContractNumber.Trim() && c.DeletedAt == null, ct);
            var order = s.OrderNumber?.Trim() ?? "";
            var hash = HashRow(supplier.Id.ToString(), contract.Id.ToString(), s.ShipmentDocNumber, s.ShipmentDocDate.ToString("O"), order, s.ShipmentAmount.ToString("F2"));
            var ex = await _db.ActualShipments.FirstOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ContractId == contract.Id
                && x.ShipmentDocNumber == s.ShipmentDocNumber && x.ShipmentDocDate == s.ShipmentDocDate && x.OrderNumber == order && x.DeletedAt == null, ct);
            if (ex is null)
            {
                _db.ActualShipments.Add(new ActualShipment
                {
                    SupplierId = supplier.Id,
                    ContractId = contract.Id,
                    OrderNumber = order,
                    ShipmentDocNumber = s.ShipmentDocNumber.Trim(),
                    ShipmentDocDate = s.ShipmentDocDate,
                    ShipmentAmount = s.ShipmentAmount,
                    SourceImportBatchId = batchId,
                    SourceRowHash = hash,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                ex.ShipmentAmount = s.ShipmentAmount;
                ex.SourceImportBatchId = batchId;
                ex.SourceRowHash = hash;
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task UpsertOrdersAsync(long batchId, CancellationToken ct)
    {
        foreach (var o in await _db.StagingSupplierOrders.Where(x => x.ImportBatchId == batchId).ToListAsync(ct))
        {
            var supplier = await _db.Suppliers.FirstAsync(x => x.SupplierCode == o.SupplierCode.Trim() && x.DeletedAt == null, ct);
            var contract = await _db.Contracts.FirstAsync(c => c.SupplierId == supplier.Id && c.InternalContractNumber == o.InternalContractNumber.Trim() && c.DeletedAt == null, ct);
            var hash = HashRow(supplier.Id.ToString(), contract.Id.ToString(), o.OrderNumber, o.OrderAmount?.ToString("F2") ?? "");
            var ex = await _db.SupplierOrders.FirstOrDefaultAsync(x => x.SupplierId == supplier.Id && x.ContractId == contract.Id && x.OrderNumber == o.OrderNumber.Trim() && x.DeletedAt == null, ct);
            if (ex is null)
            {
                _db.SupplierOrders.Add(new SupplierOrder
                {
                    SupplierId = supplier.Id,
                    ContractId = contract.Id,
                    OrderNumber = o.OrderNumber.Trim(),
                    OrderDate = o.OrderDate,
                    OrderAmount = o.OrderAmount,
                    SourceImportBatchId = batchId,
                    SourceRowHash = hash,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                ex.OrderDate = o.OrderDate ?? ex.OrderDate;
                ex.OrderAmount = o.OrderAmount ?? ex.OrderAmount;
                ex.SourceImportBatchId = batchId;
                ex.SourceRowHash = hash;
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
