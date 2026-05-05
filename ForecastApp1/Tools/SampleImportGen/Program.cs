using ClosedXML.Excel;

// ForecastApp1/Tools/SampleImportGen/bin/Debug/net9.0 -> 5x .. = ForecastApp1
var forecastAppRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var outDir = Path.Combine(forecastAppRoot, "wwwroot", "samples");
Directory.CreateDirectory(outDir);
var path = Path.Combine(outDir, "test-import-sample.xlsx");

using var wb = new XLWorkbook();
var ws = wb.AddWorksheet("Импорт");

string[] headers =
[
    "Код поставщика",
    "Наименование поставщика",
    "Номер договора",
    "Внешний номер договора",
    "Дата договора",
    "Сумма договора",
    "Отсрочка, дней",
    "Действует с",
    "Действует по",
    "Дата долга",
    "Сумма долга",
    "Номер заказа",
    "Номер документа отгрузки",
    "Дата документа отгрузки",
    "Сумма отгрузки"
];

for (var c = 0; c < headers.Length; c++)
    ws.Cell(1, c + 1).Value = headers[c];
ws.Row(1).Style.Font.Bold = true;

// Один широкий лист: строки классифицируются по заполненным колонкам (см. ExcelHeaderMapper.ClassifyRow).
string?[][] rows =
[
    // Поставщик
    ["SUP-DEMO", "ООО «Демо Поставщик»", "", "", "", "", "", "", "", "", "", "", "", "", ""],
    // Договор
    ["SUP-DEMO", "", "CNT-DEMO-001", "EXT-7788", "01.02.2024", "1500000", "", "", "", "", "", "", "", "", ""],
    // Условие оплаты (отсрочка)
    ["SUP-DEMO", "", "CNT-DEMO-001", "", "", "", "45", "01.02.2024", "", "", "", "", "", "", ""],
    // Долг (снимок)
    ["SUP-DEMO", "", "CNT-DEMO-001", "", "", "", "", "", "", "01.03.2026", "125000,50", "", "", "", ""],
    // Заказ
    ["SUP-DEMO", "", "CNT-DEMO-001", "", "", "", "", "", "", "", "", "PO-100", "", "", ""],
    // Отгрузка
    ["SUP-DEMO", "", "CNT-DEMO-001", "", "", "", "", "", "", "", "", "PO-100", "ТОРГ-001", "15.01.2026", "250000"]
];

for (var r = 0; r < rows.Length; r++)
{
    for (var c = 0; c < rows[r].Length; c++)
    {
        var v = rows[r][c];
        if (!string.IsNullOrEmpty(v))
            ws.Cell(r + 2, c + 1).Value = v;
    }
}

ws.Columns().AdjustToContents(1, headers.Length, 2.5);
wb.SaveAs(path);
Console.WriteLine($"Создан файл: {path}");
