namespace ForecastApp1.Data.Entities;

public class Supplier
{
    public long Id { get; set; }
    public string SupplierCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}

public class Contract
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public string InternalContractNumber { get; set; } = null!;
    public string? ExternalContractNumber { get; set; }
    public DateOnly? ContractDate { get; set; }
    public decimal? ContractAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<ContractCondition> Conditions { get; set; } = new List<ContractCondition>();
}

public class ContractCondition
{
    public long Id { get; set; }
    public long ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public int PaymentDelayDays { get; set; }
    public string? Description { get; set; }
    public long? SourceImportBatchId { get; set; }
    public ImportBatch? SourceImportBatch { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class BusinessCalendarDay
{
    public string CalendarCode { get; set; } = "default";
    public DateOnly CalendarDate { get; set; }
    public bool IsWorkingDay { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
