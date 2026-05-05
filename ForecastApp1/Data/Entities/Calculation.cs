namespace ForecastApp1.Data.Entities;

public class PaymentCalculationRun
{
    public long Id { get; set; }
    public Guid RunCode { get; set; }
    public DateOnly CalculationDate { get; set; }
    public string CalendarCode { get; set; } = "default";
    public long? FilterSupplierId { get; set; }
    public Supplier? FilterSupplier { get; set; }
    public long? FilterContractId { get; set; }
    public Contract? FilterContract { get; set; }
    public string Status { get; set; } = "draft";
    public bool Archived { get; set; }
    public long StartedByUserId { get; set; }
    public AppUser StartedByUser { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ParametersJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<PaymentScheduleItem> ScheduleItems { get; set; } = new List<PaymentScheduleItem>();
    public ICollection<UncoveredDebtItem> UncoveredDebts { get; set; } = new List<UncoveredDebtItem>();
}

public class PaymentScheduleItem
{
    public long Id { get; set; }
    public long CalculationRunId { get; set; }
    public PaymentCalculationRun CalculationRun { get; set; } = null!;
    public long SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public long ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public long IncomingDebtSnapshotId { get; set; }
    public IncomingDebtSnapshot IncomingDebtSnapshot { get; set; } = null!;
    public long ActualShipmentId { get; set; }
    public ActualShipment ActualShipment { get; set; } = null!;
    public string OrderNumber { get; set; } = "";
    public string ShipmentDocNumber { get; set; } = null!;
    public DateOnly ShipmentDocDate { get; set; }
    public decimal PayAmount { get; set; }
    public DateOnly DueDateByCondition { get; set; }
    public DateOnly ForecastPaymentDate { get; set; }
    public int AppliedDelayDays { get; set; }
    public bool IsOverdueVsCalcDate { get; set; }
    public bool ShiftedForWeekendOrHoliday { get; set; }
    public bool RaisedToCalculationDate { get; set; }
    public string? WarningText { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<PaymentScheduleAllocation> Allocations { get; set; } = new List<PaymentScheduleAllocation>();
}

public class PaymentScheduleAllocation
{
    public long Id { get; set; }
    public long CalculationRunId { get; set; }
    public long ScheduleItemId { get; set; }
    public PaymentScheduleItem ScheduleItem { get; set; } = null!;
    public long IncomingDebtSnapshotId { get; set; }
    public long ActualShipmentId { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal DebtAmountBefore { get; set; }
    public decimal ShipmentAmountTotal { get; set; }
    public long? ContractConditionId { get; set; }
    public ContractCondition? ContractCondition { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UncoveredDebtItem
{
    public long Id { get; set; }
    public long CalculationRunId { get; set; }
    public PaymentCalculationRun CalculationRun { get; set; } = null!;
    public long IncomingDebtSnapshotId { get; set; }
    public IncomingDebtSnapshot IncomingDebtSnapshot { get; set; } = null!;
    public decimal UncoveredAmount { get; set; }
    public string WarningText { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
