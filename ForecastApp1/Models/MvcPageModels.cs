namespace ForecastApp1.Models;

public sealed record SupplierListRow(long Id, string SupplierCode, string Name);

public sealed record ContractListRow(
    long Id,
    long SupplierId,
    string SupplierCode,
    string InternalContractNumber,
    string? ExternalContractNumber,
    DateOnly? ContractDate,
    decimal? ContractAmount);

public sealed record ConditionListRow(
    long Id,
    long ContractId,
    string ContractNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    int PaymentDelayDays,
    string? Description);

public sealed record ImportBatchListRow(
    long Id,
    string OriginalFileName,
    string Status,
    DateTime CreatedAt,
    int Rows,
    int Errors);

public sealed record CalculationRunListRow(
    long Id,
    Guid RunCode,
    DateOnly CalculationDate,
    string Status,
    bool Archived,
    DateTime StartedAt,
    DateTime? FinishedAt);

public sealed record PaymentSchedulePreviewRow(
    long Id,
    long CalculationRunId,
    DateOnly ForecastPaymentDate,
    decimal PayAmount,
    string SupplierName,
    string ContractNumber);

public sealed record PaymentsIndexVm(long? LatestRunId, IReadOnlyList<PaymentSchedulePreviewRow> Items);
