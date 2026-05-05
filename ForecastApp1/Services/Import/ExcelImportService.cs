using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using ForecastApp1.Data;
using ForecastApp1.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForecastApp1.Services.Import;

public interface IExcelImportService
{
    Task ParseUploadedBatchAsync(long batchId, CancellationToken ct);
}

public sealed class ExcelImportService : IExcelImportService
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ExcelImportService(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task ParseUploadedBatchAsync(long batchId, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstAsync(b => b.Id == batchId, ct);
        await _db.ImportRows.Where(r => r.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.ImportErrors.Where(e => e.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await ClearStagingAsync(batchId, ct);

        var fullPath = Path.Combine(_env.ContentRootPath, batch.StoragePath);
        if (!File.Exists(fullPath))
            throw new InvalidOperationException("Файл загрузки не найден на сервере");

        List<string> detected = new();
        try
        {
            using var workbook = new XLWorkbook(fullPath);
            foreach (var ws in workbook.Worksheets)
            {
                if (ShouldSkipImportSheet(ws.Name))
                    continue;
                var sheetBlocks = await ParseSheetAsync(batchId, ws, ct);
                detected.AddRange(sheetBlocks);
            }
        }
        catch (Exception ex)
        {
            batch.Status = ImportBatchStatuses.Error;
            _db.ImportErrors.Add(new ImportError
            {
                ImportBatchId = batchId,
                ErrorCode = ImportErrorCodes.E_EXCEL_READ,
                Severity = ImportSeverities.Blocking,
                Message = "Ошибка чтения Excel: " + ex.Message,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
            return;
        }

        batch.DetectedBlocksJson = JsonSerializer.Serialize(detected.Distinct());
        batch.Status = ImportBatchStatuses.Parsed;
        await _db.SaveChangesAsync(ct);
    }

    private async Task ClearStagingAsync(long batchId, CancellationToken ct)
    {
        await _db.StagingSuppliers.Where(x => x.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.StagingContracts.Where(x => x.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.StagingContractConditions.Where(x => x.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.StagingIncomingDebts.Where(x => x.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.StagingActualShipments.Where(x => x.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
        await _db.StagingSupplierOrders.Where(x => x.ImportBatchId == batchId).ExecuteDeleteAsync(ct);
    }

    private async Task<List<string>> ParseSheetAsync(long batchId, IXLWorksheet ws, CancellationToken ct)
    {
        var used = ws.RangeUsed();
        if (used is null) return new List<string>();

        var maxRow = used.LastRow().RowNumber();
        var maxCol = used.LastColumn().ColumnNumber();

        Dictionary<int, string>? colMap = null;
        var blocks = new ConcurrentBag<string>();

        for (var r = 1; r <= maxRow; r++)
        {
            if (IsRowEmpty(ws, r, maxCol)) continue;

            var trialMap = TryBuildColumnMap(ws, r, maxCol);
            if (trialMap is not null)
            {
                colMap = trialMap;
                continue;
            }

            if (colMap is not null)
            {
                var dict = BuildRowDict(ws, r, colMap);
                var type = ExcelHeaderMapper.ClassifyRow(dict);
                if (type is not null)
                {
                    var raw = JsonSerializer.Serialize(dict);
                    var rowEntity = new ImportRow
                    {
                        ImportBatchId = batchId,
                        SheetName = ws.Name,
                        RowNumber = r,
                        RawJson = raw,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.ImportRows.Add(rowEntity);
                    await _db.SaveChangesAsync(ct);

                    rowEntity.DetectedEntityType = type;
                    await _db.SaveChangesAsync(ct);

                    blocks.Add(type);
                    await WriteStagingAsync(batchId, rowEntity.Id, ws.Name, r, type, dict, ct);
                    continue;
                }
            }

            if (!IsRowEmpty(ws, r, maxCol))
                colMap = null;
        }

        return blocks.ToList();
    }

    private static bool ShouldSkipImportSheet(string sheetName)
    {
        var n = ExcelHeaderMapper.Normalize(sheetName);
        return n.Contains("readme", StringComparison.OrdinalIgnoreCase)
               || n.Contains("ожидаем", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<int, string>? TryBuildColumnMap(IXLWorksheet ws, int headerRow, int maxCol)
    {
        var map = new Dictionary<int, string>();
        for (var c = 1; c <= maxCol; c++)
        {
            var h = ws.Cell(headerRow, c).GetString();
            var canon = ExcelHeaderMapper.MapCanonical(ExcelHeaderMapper.Normalize(h));
            if (canon is not null) map[c] = canon;
        }

        return map.Count >= 2 ? map : null;
    }

    private static Dictionary<string, string?> BuildRowDict(IXLWorksheet ws, int r, Dictionary<int, string> colMap)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, key) in colMap)
            dict[key] = GetCellImportText(ws.Cell(r, col));
        return dict;
    }

    /// <summary>Текст ячейки для импорта: даты Excel (в т.ч. серийный номер) в dd.MM.yyyy.</summary>
    private static string? GetCellImportText(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;

        try
        {
            if (cell.DataType == XLDataType.DateTime)
                return DateOnly.FromDateTime(cell.GetDateTime()).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            if (cell.DataType == XLDataType.Number)
            {
                var d = cell.GetDouble();
                if (d is >= 20000 and <= 80000 && Math.Abs(d - Math.Round(d)) < 1e-9)
                {
                    try
                    {
                        return DateOnly.FromDateTime(DateTime.FromOADate(d)).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        /* use formatted */
                    }
                }
            }
        }
        catch
        {
            /* GetFormattedString */
        }

        return cell.GetFormattedString();
    }

    private static bool IsRowEmpty(IXLWorksheet ws, int r, int maxCol)
    {
        for (int c = 1; c <= maxCol; c++)
            if (!ws.Cell(r, c).IsEmpty()) return false;
        return true;
    }

    private async Task WriteStagingAsync(long batchId, long rowId, string sheet, int rowNum, string type, Dictionary<string, string?> d, CancellationToken ct)
    {
        switch (type)
        {
            case "supplier":
                _db.StagingSuppliers.Add(new StagingSupplier
                {
                    ImportBatchId = batchId,
                    ImportRowId = rowId,
                    SupplierCode = d.GetValueOrDefault("supplier_code") ?? "",
                    Name = d.GetValueOrDefault("supplier_name") ?? "",
                    RowNumber = rowNum,
                    SheetName = sheet,
                    CreatedAt = DateTime.UtcNow
                });
                break;
            case "contract":
                _db.StagingContracts.Add(new StagingContract
                {
                    ImportBatchId = batchId,
                    ImportRowId = rowId,
                    SupplierCode = d.GetValueOrDefault("supplier_code") ?? "",
                    InternalContractNumber = d.GetValueOrDefault("internal_contract_number") ?? "",
                    ExternalContractNumber = d.GetValueOrDefault("external_contract_number"),
                    ContractDate = ParseDateOrNull(d.GetValueOrDefault("contract_date")),
                    ContractAmount = ParseDecimalOrNull(d.GetValueOrDefault("contract_amount")),
                    RowNumber = rowNum,
                    SheetName = sheet,
                    CreatedAt = DateTime.UtcNow
                });
                break;
            case "condition":
                _db.StagingContractConditions.Add(new StagingContractCondition
                {
                    ImportBatchId = batchId,
                    ImportRowId = rowId,
                    ContractKey = d.GetValueOrDefault("condition_contract") ?? d.GetValueOrDefault("internal_contract_number") ?? "",
                    InternalContractNumber = d.GetValueOrDefault("internal_contract_number"),
                    SupplierCode = d.GetValueOrDefault("supplier_code"),
                    PaymentDelayDays = (int)(ParseDecimalOrNull(d.GetValueOrDefault("payment_delay_days")) ?? 0),
                    ValidFrom = ParseDateOrNull(d.GetValueOrDefault("valid_from")),
                    ValidTo = ParseDateOrNull(d.GetValueOrDefault("valid_to")),
                    RowNumber = rowNum,
                    SheetName = sheet,
                    CreatedAt = DateTime.UtcNow
                });
                break;
            case "debt":
                _db.StagingIncomingDebts.Add(new StagingIncomingDebt
                {
                    ImportBatchId = batchId,
                    ImportRowId = rowId,
                    SupplierCode = d.GetValueOrDefault("supplier_code") ?? "",
                    InternalContractNumber = d.GetValueOrDefault("internal_contract_number") ?? "",
                    DebtDate = ParseDateOrNull(d.GetValueOrDefault("debt_date")) ?? default,
                    DebtAmount = ParseDecimalOrNull(d.GetValueOrDefault("debt_amount")) ?? 0,
                    RowNumber = rowNum,
                    SheetName = sheet,
                    CreatedAt = DateTime.UtcNow
                });
                break;
            case "shipment":
                _db.StagingActualShipments.Add(new StagingActualShipment
                {
                    ImportBatchId = batchId,
                    ImportRowId = rowId,
                    SupplierCode = d.GetValueOrDefault("supplier_code") ?? "",
                    InternalContractNumber = d.GetValueOrDefault("internal_contract_number") ?? "",
                    OrderNumber = d.GetValueOrDefault("order_number") ?? "",
                    ShipmentDocNumber = d.GetValueOrDefault("shipment_doc_number") ?? "",
                    ShipmentDocDate = ParseDateOrNull(d.GetValueOrDefault("shipment_doc_date")) ?? default,
                    ShipmentAmount = ParseDecimalOrNull(d.GetValueOrDefault("shipment_amount")) ?? 0,
                    RowNumber = rowNum,
                    SheetName = sheet,
                    CreatedAt = DateTime.UtcNow
                });
                break;
            case "order":
                _db.StagingSupplierOrders.Add(new StagingSupplierOrder
                {
                    ImportBatchId = batchId,
                    ImportRowId = rowId,
                    SupplierCode = d.GetValueOrDefault("supplier_code") ?? "",
                    InternalContractNumber = d.GetValueOrDefault("internal_contract_number") ?? "",
                    OrderNumber = d.GetValueOrDefault("order_number") ?? "",
                    OrderDate = ParseDateOrNull(d.GetValueOrDefault("contract_date")),
                    OrderAmount = ParseDecimalOrNull(d.GetValueOrDefault("contract_amount")),
                    RowNumber = rowNum,
                    SheetName = sheet,
                    CreatedAt = DateTime.UtcNow
                });
                break;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static DateOnly? ParseDateOrNull(string? s) =>
        ExcelHeaderMapper.TryParseDate(s, out var d) ? d : null;

    private static decimal? ParseDecimalOrNull(string? s) =>
        ExcelHeaderMapper.TryParseDecimal(s, out var x) ? x : null;

    public static string Sha256(Stream stream)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
