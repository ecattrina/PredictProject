namespace ForecastApp1.Data.Entities;

public class ImportBatch
{
    public long Id { get; set; }
    public long UploadedByUserId { get; set; }
    public AppUser UploadedByUser { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string FileHash { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string StoragePath { get; set; } = null!;
    public string? MimeType { get; set; }
    public string Status { get; set; } = ImportBatchStatuses.Uploaded;
    public DateTime? CommittedAt { get; set; }
    public DateTime? RolledBackAt { get; set; }
    public string? Notes { get; set; }
    public string? DetectedBlocksJson { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<ImportRow> Rows { get; set; } = new List<ImportRow>();
    public ICollection<ImportError> Errors { get; set; } = new List<ImportError>();
}

public static class ImportBatchStatuses
{
    public const string Uploaded = "uploaded";
    public const string Parsed = "parsed";
    public const string Validating = "validating";
    public const string ValidationFailed = "validation_failed";
    public const string AwaitingCommit = "awaiting_commit";
    public const string Committed = "committed";
    public const string RolledBack = "rolled_back";
    public const string Error = "error";
}

public class ImportRow
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public ImportBatch Batch { get; set; } = null!;
    public string SheetName { get; set; } = null!;
    public int RowNumber { get; set; }
    public string RawJson { get; set; } = null!;
    public string? DetectedEntityType { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ImportError
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public ImportBatch Batch { get; set; } = null!;
    public long? ImportRowId { get; set; }
    public ImportRow? Row { get; set; }
    public string? SheetName { get; set; }
    public int? RowNumber { get; set; }
    public string? ColumnName { get; set; }
    public string ErrorCode { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? DetailsJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public static class ImportSeverities
{
    public const string Blocking = "blocking";
    public const string Warning = "warning";
    public const string Info = "info";
}

public class StagingSupplier
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public long? ImportRowId { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int RowNumber { get; set; }
    public string SheetName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class StagingContract
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public long? ImportRowId { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string InternalContractNumber { get; set; } = null!;
    public string? ExternalContractNumber { get; set; }
    public DateOnly? ContractDate { get; set; }
    public decimal? ContractAmount { get; set; }
    public int RowNumber { get; set; }
    public string SheetName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class StagingContractCondition
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public long? ImportRowId { get; set; }
    public string ContractKey { get; set; } = null!;
    public string? InternalContractNumber { get; set; }
    public string? SupplierCode { get; set; }
    public int PaymentDelayDays { get; set; }
    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public int RowNumber { get; set; }
    public string SheetName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class StagingIncomingDebt
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public long? ImportRowId { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string InternalContractNumber { get; set; } = null!;
    public DateOnly DebtDate { get; set; }
    public decimal DebtAmount { get; set; }
    public int RowNumber { get; set; }
    public string SheetName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class StagingActualShipment
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public long? ImportRowId { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string InternalContractNumber { get; set; } = null!;
    public string OrderNumber { get; set; } = "";
    public string ShipmentDocNumber { get; set; } = null!;
    public DateOnly ShipmentDocDate { get; set; }
    public decimal ShipmentAmount { get; set; }
    public int RowNumber { get; set; }
    public string SheetName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}

public class StagingSupplierOrder
{
    public long Id { get; set; }
    public long ImportBatchId { get; set; }
    public long? ImportRowId { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string InternalContractNumber { get; set; } = null!;
    public string OrderNumber { get; set; } = null!;
    public DateOnly? OrderDate { get; set; }
    public decimal? OrderAmount { get; set; }
    public int RowNumber { get; set; }
    public string SheetName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
