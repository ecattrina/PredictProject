namespace ForecastApp1.Services.Import;

/// <summary>Каталог кодов ошибок импорта (≥30).</summary>
public static class ImportErrorCodes
{
    public const string E_EXCEL_READ = "E_EXCEL_READ";
    public const string E_FILE_TOO_LARGE = "E_FILE_TOO_LARGE";
    public const string E_FILE_TYPE = "E_FILE_TYPE";
    public const string E_UNKNOWN_BLOCK = "E_UNKNOWN_BLOCK";
    public const string E_MISSING_COLUMNS = "E_MISSING_COLUMNS";
    public const string E_SUPPLIER_CODE_EMPTY = "E_SUPPLIER_CODE_EMPTY";
    public const string E_CONTRACT_EMPTY = "E_CONTRACT_EMPTY";
    public const string E_UNKNOWN_SUPPLIER = "E_UNKNOWN_SUPPLIER";
    public const string E_UNKNOWN_CONTRACT = "E_UNKNOWN_CONTRACT";
    public const string E_CONTRACT_WRONG_SUPPLIER = "E_CONTRACT_WRONG_SUPPLIER";
    public const string E_DATE_INVALID = "E_DATE_INVALID";
    public const string E_AMOUNT_INVALID = "E_AMOUNT_INVALID";
    public const string E_AMOUNT_NEGATIVE = "E_AMOUNT_NEGATIVE";
    public const string E_AMOUNT_ZERO = "E_AMOUNT_ZERO";
    public const string E_DUP_IN_FILE = "E_DUP_IN_FILE";
    public const string E_DUP_IN_DB = "E_DUP_IN_DB";
    public const string E_FILE_REUPLOAD_HASH = "E_FILE_REUPLOAD_HASH";
    public const string E_CONDITION_MISSING = "E_CONDITION_MISSING";
    public const string E_CONDITION_OVERLAP = "E_CONDITION_OVERLAP";
    public const string E_CALENDAR_MISSING = "E_CALENDAR_MISSING";
    public const string E_SHIP_DOC_EMPTY = "E_SHIP_DOC_EMPTY";
    public const string E_SHIP_SUM_MISMATCH = "E_SHIP_SUM_MISMATCH";
    public const string E_DEBT_NEGATIVE = "E_DEBT_NEGATIVE";
    public const string E_ORDER_NUMBER_EMPTY = "E_ORDER_NUMBER_EMPTY";
    public const string E_HEADER_ROW = "E_HEADER_ROW";
    public const string E_SHEET_EMPTY = "E_SHEET_EMPTY";
    public const string E_ROW_PARSE = "E_ROW_PARSE";
    public const string E_CURRENCY_INVALID = "E_CURRENCY_INVALID";
    public const string E_CONDITION_DELAY_INVALID = "E_CONDITION_DELAY_INVALID";
    public const string E_COMMIT_NOT_READY = "E_COMMIT_NOT_READY";
    public const string E_ROLLBACK_BLOCKED = "E_ROLLBACK_BLOCKED";
    public const string E_BATCH_STATUS = "E_BATCH_STATUS";
    public const string W_PARTIAL_BLOCK = "W_PARTIAL_BLOCK";
    public const string W_FUTURE_DATE = "W_FUTURE_DATE";
    public const string W_NO_CONDITION_FOR_SHIP_DATE = "W_NO_CONDITION_FOR_SHIP_DATE";
    public const string W_CONTRACT_ARCHIVED = "W_CONTRACT_ARCHIVED";
    public const string W_MANY_DEBT_SAME_DATE = "W_MANY_DEBT_SAME_DATE";
}
