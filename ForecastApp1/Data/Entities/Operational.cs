namespace ForecastApp1.Data.Entities;

public class IncomingDebtSnapshot
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public long ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public DateOnly DebtDate { get; set; }
    public decimal DebtAmount { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public long SourceImportBatchId { get; set; }
    public ImportBatch SourceImportBatch { get; set; } = null!;
    public string SourceRowHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class ActualShipment
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public long ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string OrderNumber { get; set; } = "";
    public string ShipmentDocNumber { get; set; } = null!;
    public DateOnly ShipmentDocDate { get; set; }
    public decimal ShipmentAmount { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public long SourceImportBatchId { get; set; }
    public ImportBatch SourceImportBatch { get; set; } = null!;
    public string SourceRowHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class SupplierOrder
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public long ContractId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public DateOnly? OrderDate { get; set; }
    public decimal? OrderAmount { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public long SourceImportBatchId { get; set; }
    public string SourceRowHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class ActualPayment
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public long ContractId { get; set; }
    public DateOnly PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public string? DocumentNumber { get; set; }
    public long SourceImportBatchId { get; set; }
    public string SourceRowHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
