using System.Globalization;
using System.Text;

namespace ForecastApp1.Services.Import;

public static class ExcelHeaderMapper
{
    public static string Normalize(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)) return "";
        var s = header.Trim().ToLowerInvariant().Replace('ё', 'е');
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch) || ch is '.' or '_' or '№' or '#') sb.Append(ch == '№' ? '#' : ch);
            else if (char.IsWhiteSpace(ch)) sb.Append(' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["supplier_code"] = ["код поставщика", "код", "supplier code", "supplier_code", "поставщик код"],
        ["supplier_name"] = ["наименование поставщика", "название поставщика", "название", "поставщик", "name", "supplier name"],
        ["internal_contract_number"] = ["внутренний номер договора", "номер договора", "договор", "contract", "internal contract", "код договора"],
        ["external_contract_number"] = ["внешний номер договора", "внешний договор", "external contract"],
        ["contract_date"] = ["дата договора", "дата заказа", "contract date"],
        ["contract_amount"] = ["сумма договора", "сумма заказа", "contract amount"],
        ["debt_date"] = ["дата долга", "дата снимка", "debt date", "на дату", "дата"],
        ["debt_amount"] = ["сумма долга", "долг", "debt", "debt amount", "сальдо"],
        ["order_number"] = ["номер заказа", "заказ", "order", "order number"],
        ["shipment_doc_number"] = ["номер документа отгрузки", "номер отгрузки", "накладная", "shipment doc", "doc no"],
        ["shipment_doc_date"] = ["дата документа отгрузки", "дата отгрузки", "shipment date"],
        ["shipment_amount"] = ["сумма отгрузки", "сумма по отгрузке", "shipment amount"],
        ["payment_delay_days"] = ["отсрочка", "дней отсрочки", "количество дней отсрочки платежа", "delay", "payment delay", "дни"],
        ["valid_from"] = ["действует с", "дата начала действия", "valid from", "с даты"],
        ["valid_to"] = ["действует по", "дата окончания действия", "valid to", "по дату"],
        ["condition_contract"] = ["договор условия", "условие договор", "contract for condition"]
    };

    /// <summary>Сопоставление заголовка: точное совпадение и «длинные» подстроки (избегаем ложных срабатываний вроде «дата» в «дата документа отгрузки»).</summary>
    public static string? MapCanonical(string normalizedHeader)
    {
        if (string.IsNullOrEmpty(normalizedHeader)) return null;

        string? bestKey = null;
        var bestScore = 0;

        foreach (var (key, list) in Synonyms)
        {
            foreach (var syn in list)
            {
                var ns = Normalize(syn);
                if (ns.Length == 0) continue;
                var score = HeaderMatchScore(normalizedHeader, ns);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestKey = key;
                }
            }
        }

        return bestKey;
    }

    private static int HeaderMatchScore(string header, string normalizedSynonym)
    {
        if (header == normalizedSynonym) return 100_000 + normalizedSynonym.Length;
        // Подстрока только для синонимов от 5 символов — короткие («дата», «долг») только по полному совпадению.
        if (normalizedSynonym.Length >= 5 && header.Contains(normalizedSynonym, StringComparison.Ordinal))
            return 50_000 + normalizedSynonym.Length;
        return 0;
    }

    public static bool TryParseDate(string? s, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        string[] fmts = ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd", "dd/MM/yyyy", "M/d/yyyy"];
        if (DateOnly.TryParseExact(t, fmts, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        if (DateOnly.TryParse(t, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out date)
            || DateOnly.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        if (double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa)
            && oa is >= 20000 and <= 80000
            && Math.Abs(oa - Math.Round(oa)) < 1e-9)
        {
            try
            {
                date = DateOnly.FromDateTime(DateTime.FromOADate(oa));
                return true;
            }
            catch { /* ignore */ }
        }
        return false;
    }

    public static bool TryParseDecimal(string? s, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().Replace('\u00A0', ' ').Replace(" ", "");
        if (t.Contains(',') && t.Contains('.')) t = t.Replace(".", "");
        t = t.Replace(',', '.');
        return decimal.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    public static string? ClassifyRow(IReadOnlyDictionary<string, string?> row)
    {
        bool Has(string k) => row.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v);

        if (Has("debt_date") && Has("debt_amount") && Has("supplier_code") && Has("internal_contract_number"))
            return "debt";
        if (Has("shipment_doc_number") && Has("shipment_doc_date") && Has("shipment_amount") && Has("supplier_code") && Has("internal_contract_number"))
            return "shipment";
        if (Has("payment_delay_days") && (Has("internal_contract_number") || Has("condition_contract")))
            return "condition";
        if (Has("supplier_code") && Has("supplier_name") && !Has("internal_contract_number") && !Has("debt_date") && !Has("shipment_doc_number"))
            return "supplier";
        if (Has("order_number") && Has("supplier_code") && Has("internal_contract_number") && !Has("shipment_doc_number"))
            return "order";
        if (Has("supplier_code") && Has("internal_contract_number") && !Has("debt_amount") && !Has("shipment_doc_number"))
            return "contract";
        return null;
    }
}
