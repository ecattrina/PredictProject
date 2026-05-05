namespace ForecastApp1.Dtos;

public record LoginRequest(string Email, string Password);
public record UserListDto(long Id, string Email, string FullName, bool IsActive, string[] Roles);
public record CreateUserRequest(string Email, string Password, string FullName, string[] Roles);
public record UpdateUserRequest(string? FullName, bool? IsActive, string[]? Roles, string? NewPassword);

public record ImportBatchDto(long Id, string FileName, string Status, string FileHash, DateTime CreatedAt, int? RowCount, int? ErrorCount);
public record ImportCommitRequest(bool ConfirmWarnings);

public record RunCalculationRequest(DateOnly CalculationDate, long? SupplierId, long? ContractId, string? CalendarCode);

public record ScheduleItemDto(
    long Id,
    DateOnly ForecastPaymentDate,
    DateOnly DueByCondition,
    decimal PayAmount,
    long SupplierId,
    string SupplierName,
    long ContractId,
    string ContractNumber,
    string ShipmentDocNumber,
    DateOnly ShipmentDocDate,
    string? Warning);

public record ScheduleItemDetailsDto(
    ScheduleItemDto Item,
    long DebtSnapshotId,
    DateOnly DebtDate,
    decimal DebtAmount,
    long ShipmentId,
    decimal ShipmentTotal,
    decimal AllocatedAmount,
    long? ConditionId,
    int DelayDays,
    DateOnly DueByCondition,
    bool WeekendShift,
    bool RaisedToCalcDate,
    bool Overdue,
    string? Warning);
