using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Services.Import;

public interface IImportValidationService
{
    Task<ImportValidationResultDto> ValidateAsync(long batchId, CancellationToken ct);
}

public sealed record ImportValidationResultDto(int BlockingCount, int WarningCount, string Status);

public sealed class ImportValidationService : IImportValidationService
{
    private readonly ApplicationDbContext _db;

    public ImportValidationService(ApplicationDbContext db) => _db = db;

    public async Task<ImportValidationResultDto> ValidateAsync(long batchId, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
        batch.Status = ImportBatchStatuses.Validating;
        await _db.ImportErrors.Where(e => e.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.SaveChangesAsync(ct);

        await ValidateSuppliersAsync(batchId, ct);
        await ValidateContractsAsync(batchId, ct);
        await ValidateConditionsAsync(batchId, ct);
        await ValidateDebtsAsync(batchId, ct);
        await ValidateShipmentsAsync(batchId, ct);

        var blocking = await _db.ImportErrors.CountAsync(e => e.ImportBatchId == batchId && e.Severity == ImportSeverities.Blocking, ct);
        var warnings = await _db.ImportErrors.CountAsync(e => e.ImportBatchId == batchId && e.Severity == ImportSeverities.Warning, ct);

        batch.Status = blocking > 0 ? ImportBatchStatuses.ValidationFailed : ImportBatchStatuses.AwaitingCommit;
        await _db.SaveChangesAsync(ct);

        return new ImportValidationResultDto(blocking, warnings, batch.Status);
    }

    private async Task ValidateSuppliersAsync(long batchId, CancellationToken ct)
    {
        var rows = await _db.StagingSuppliers.Where(s => s.ImportBatchId == batchId).ToListAsync(ct);
        var dup = rows.GroupBy(r => r.SupplierCode.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.SupplierCode))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_SUPPLIER_CODE_EMPTY, ImportSeverities.Blocking, "Пустой код поставщика");
            if (string.IsNullOrWhiteSpace(r.Name))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_MISSING_COLUMNS, ImportSeverities.Blocking, "Пустое наименование поставщика");
            if (dup.Contains(r.SupplierCode.Trim()))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_DUP_IN_FILE, ImportSeverities.Blocking, "Дубль поставщика в файле");
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidateContractsAsync(long batchId, CancellationToken ct)
    {
        var supplierCodes = await GetKnownSupplierCodesAsync(batchId, ct);
        var rows = await _db.StagingContracts.Where(s => s.ImportBatchId == batchId).ToListAsync(ct);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.SupplierCode))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_SUPPLIER_CODE_EMPTY, ImportSeverities.Blocking, "Пустой код поставщика");
            if (string.IsNullOrWhiteSpace(r.InternalContractNumber))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_CONTRACT_EMPTY, ImportSeverities.Blocking, "Пустой номер договора");
            if (!supplierCodes.Contains(r.SupplierCode.Trim()))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_UNKNOWN_SUPPLIER, ImportSeverities.Blocking, "Неизвестный поставщик (нет в файле и в базе)");
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidateConditionsAsync(long batchId, CancellationToken ct)
    {
        var rows = await _db.StagingContractConditions.Where(s => s.ImportBatchId == batchId).ToListAsync(ct);
        foreach (var r in rows)
        {
            if (r.PaymentDelayDays < 0 || r.PaymentDelayDays > 3650)
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_CONDITION_DELAY_INVALID, ImportSeverities.Blocking, "Некорректная отсрочка");
            if (string.IsNullOrWhiteSpace(r.InternalContractNumber) && string.IsNullOrWhiteSpace(r.ContractKey))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_MISSING_COLUMNS, ImportSeverities.Blocking, "Не указан договор для условия");
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task<HashSet<string>> GetKnownSupplierCodesAsync(long batchId, CancellationToken ct)
    {
        var staging = await _db.StagingSuppliers.Where(s => s.ImportBatchId == batchId).Select(s => s.SupplierCode).ToListAsync(ct);
        var db = await _db.Suppliers.Where(s => s.DeletedAt == null).Select(s => s.SupplierCode).ToListAsync(ct);
        return db.Concat(staging).Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> ContractExistsForSupplierAsync(long batchId, string supplierCode, string internalNumber, CancellationToken ct)
    {
        var sup = supplierCode.Trim();
        var num = internalNumber.Trim();
        var inStaging = await _db.StagingContracts.AnyAsync(c => c.ImportBatchId == batchId
            && c.SupplierCode == sup && c.InternalContractNumber == num, ct);
        if (inStaging) return true;
        return await _db.Contracts.AnyAsync(c => c.DeletedAt == null && c.InternalContractNumber == num
            && c.Supplier.SupplierCode == sup, ct);
    }

    private async Task ValidateDebtsAsync(long batchId, CancellationToken ct)
    {
        var supplierCodes = await GetKnownSupplierCodesAsync(batchId, ct);
        var rows = await _db.StagingIncomingDebts.Where(s => s.ImportBatchId == batchId).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.SupplierCode))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_SUPPLIER_CODE_EMPTY, ImportSeverities.Blocking, "Пустой код поставщика");
            if (string.IsNullOrWhiteSpace(r.InternalContractNumber))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_CONTRACT_EMPTY, ImportSeverities.Blocking, "Пустой договор");
            if (r.DebtAmount < 0)
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_DEBT_NEGATIVE, ImportSeverities.Blocking, "Отрицательный долг");
            if (r.DebtAmount == 0)
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_AMOUNT_ZERO, ImportSeverities.Warning, "Нулевая сумма долга");

            if (!supplierCodes.Contains(r.SupplierCode.Trim()))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_UNKNOWN_SUPPLIER, ImportSeverities.Blocking, "Неизвестный поставщик");
            else if (!await ContractExistsForSupplierAsync(batchId, r.SupplierCode, r.InternalContractNumber, ct))
            {
                var wrong = await _db.Contracts.AnyAsync(c => c.DeletedAt == null && c.InternalContractNumber == r.InternalContractNumber.Trim()
                    && c.Supplier.SupplierCode != r.SupplierCode.Trim(), ct);
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber,
                    wrong ? ImportErrorCodes.E_CONTRACT_WRONG_SUPPLIER : ImportErrorCodes.E_UNKNOWN_CONTRACT,
                    ImportSeverities.Blocking,
                    wrong ? "Договор в базе привязан к другому поставщику" : "Договор не найден для этого поставщика");
            }

            var k = $"{r.SupplierCode}|{r.InternalContractNumber}|{r.DebtDate:O}";
            if (!seen.Add(k))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_DUP_IN_FILE, ImportSeverities.Blocking, "Дубль долга в файле");
        }
        await _db.SaveChangesAsync(ct);
    }

    private async Task ValidateShipmentsAsync(long batchId, CancellationToken ct)
    {
        var supplierCodes = await GetKnownSupplierCodesAsync(batchId, ct);
        var rows = await _db.StagingActualShipments.Where(s => s.ImportBatchId == batchId).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.ShipmentDocNumber))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_SHIP_DOC_EMPTY, ImportSeverities.Blocking, "Пустой номер документа отгрузки");
            if (r.ShipmentAmount < 0)
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_AMOUNT_NEGATIVE, ImportSeverities.Blocking, "Отрицательная сумма отгрузки");
            if (r.ShipmentAmount == 0)
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_AMOUNT_ZERO, ImportSeverities.Warning, "Нулевая сумма отгрузки");

            if (!supplierCodes.Contains(r.SupplierCode.Trim()))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_UNKNOWN_SUPPLIER, ImportSeverities.Blocking, "Неизвестный поставщик");
            else if (!await ContractExistsForSupplierAsync(batchId, r.SupplierCode, r.InternalContractNumber, ct))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_UNKNOWN_CONTRACT, ImportSeverities.Blocking, "Договор не найден для этого поставщика");

            var k = $"{r.SupplierCode}|{r.InternalContractNumber}|{r.ShipmentDocNumber}|{r.ShipmentDocDate:O}|{r.OrderNumber}";
            if (!seen.Add(k))
                Add(batchId, r.ImportRowId, r.SheetName, r.RowNumber, ImportErrorCodes.E_DUP_IN_FILE, ImportSeverities.Blocking, "Дубль отгрузки в файле");
        }
        await _db.SaveChangesAsync(ct);
    }

    private void Add(long batchId, long? rowId, string sheet, int row, string code, string severity, string msg)
    {
        _db.ImportErrors.Add(new ImportError
        {
            ImportBatchId = batchId,
            ImportRowId = rowId,
            SheetName = sheet,
            RowNumber = row,
            ErrorCode = code,
            Severity = severity,
            Message = msg,
            CreatedAt = DateTime.UtcNow
        });
    }
}
