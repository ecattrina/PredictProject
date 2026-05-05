using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Services.Import;

public interface IImportRollbackService
{
    Task<(bool Ok, string Message)> RollbackAsync(long batchId, CancellationToken ct);
}

public sealed class ImportRollbackService : IImportRollbackService
{
    private readonly ApplicationDbContext _db;

    public ImportRollbackService(ApplicationDbContext db) => _db = db;

    public async Task<(bool Ok, string Message)> RollbackAsync(long batchId, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
        if (batch.Status != ImportBatchStatuses.Committed)
            return (false, "Откат возможен только для подтверждённого пакета");

        var usedInCalc = await _db.PaymentScheduleAllocations.AnyAsync(a =>
            _db.IncomingDebtSnapshots.Any(d => d.Id == a.IncomingDebtSnapshotId && d.SourceImportBatchId == batchId && d.DeletedAt == null)
            || _db.ActualShipments.Any(s => s.Id == a.ActualShipmentId && s.SourceImportBatchId == batchId && s.DeletedAt == null), ct);

        if (usedInCalc)
            return (false, "Данные этого импорта участвуют в расчёте. Физический откат запрещён. Используйте корректирующий импорт.");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            await _db.IncomingDebtSnapshots.Where(d => d.SourceImportBatchId == batchId && d.DeletedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedAt, now), ct);
            await _db.ActualShipments.Where(d => d.SourceImportBatchId == batchId && d.DeletedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedAt, now), ct);
            await _db.SupplierOrders.Where(d => d.SourceImportBatchId == batchId && d.DeletedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.DeletedAt, now), ct);

            batch.Status = ImportBatchStatuses.RolledBack;
            batch.RolledBackAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (true, "Откат выполнен (операционные строки помечены удалёнными)");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return (false, ex.Message);
        }
    }
}
