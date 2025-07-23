using Dapper;
using ExcelDataReader;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Data;
using System.Diagnostics.Contracts;
public class ExcelImportService
{
    private readonly Func<NpgsqlConnection> _connectionFactory;

    public ExcelImportService(Func<NpgsqlConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }
    // Основной метод импорта данных из Excel-файла
    public async Task ImportAsync(Stream fileStream)
{
      
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    using var reader = ExcelReaderFactory.CreateReader(fileStream);
    var result = reader.AsDataSet();

    var table = result.Tables[0];
    var blocks = ExtractBlocks(table);

        using var connection = _connectionFactory();
        await connection.OpenAsync();
        // Импорт по блокам, если они найдены
        if (blocks.TryGetValue("Поставщики", out var suppliersTable))
            await ImportSuppliers(connection, suppliersTable);
        if (blocks.TryGetValue("Договоры", out var contractsTable))
            await ImportContracts(connection, contractsTable);

        if (blocks.TryGetValue("Долг", out var debtTable))
        await ImportDebt(connection, debtTable);

    if (blocks.TryGetValue("Отгрузки", out var shipmentsTable))
        await ImportShipments(connection, shipmentsTable);


    if (blocks.TryGetValue("Условия", out var conditionsTable))
        await ImportConditions(connection, conditionsTable);

 

    if (blocks.TryGetValue("Заказы", out var ordersTable))
        await ImportOrders(connection, ordersTable);

    if (blocks.TryGetValue("Доли", out var sharesTable))
        await ImportShipmentShares(connection, sharesTable);
}

    // Извлекает логические блоки из общего DataTable на основе ключевых заголовков
    private Dictionary<string, DataTable> ExtractBlocks(DataTable table)
    {
        var result = new Dictionary<string, DataTable>();

        var blockMarkers = new Dictionary<string, string>
    {
        { "Долг", "Долг перед поставщиками входящий" },
        { "Отгрузки", "Отгрузки от поставщиков фактические" },
        { "Договоры", "Договоры" },
        { "Условия", "Условия договоров" },
        { "Поставщики", "Справочник поставщиков" },
        { "Заказы", "Заказы поставщикам" },
        { "Доли", "Статистика отгрузки заказов от поставщиков" }
    };

        var rowStartMap = new Dictionary<string, int>();

        // 🔍 Для отладки — покажи весь 2-й столбец
        Console.WriteLine(">> Содержимое второго столбца (для поиска заголовков):");
        for (int i = 0; i < table.Rows.Count; i++)
        {
            Console.WriteLine($"{i}: {table.Rows[i][1]?.ToString()}");
        }

        // 🔎 Поиск начала блоков (игнорируя регистр и пробелы)
        for (int i = 0; i < table.Rows.Count; i++)
        {
            var cell = table.Rows[i][1]?.ToString()?.Trim().ToLower();
            if (cell == null) continue;

            foreach (var key in blockMarkers)
            {
                var marker = key.Value.ToLower();
                if (cell.Contains(marker))
                {
                    rowStartMap[key.Key] = i;
                    Console.WriteLine($"✅ Найден блок \"{key.Key}\" в строке {i}");
                }
            }
        }

        // ⚠️ Предупреждение о ненайденных блоках
        foreach (var key in blockMarkers.Keys)
        {
            if (!rowStartMap.ContainsKey(key))
            {
                Console.WriteLine($"⚠️ Блок \"{key}\" не найден (искали: \"{blockMarkers[key]}\")");
            }
        }

        // 🧩 Вырезка подтаблиц
        var ordered = rowStartMap.OrderBy(x => x.Value).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var key = ordered[i].Key;
            var startRow = ordered[i].Value + 1;
            var endRow = (i + 1 < ordered.Count) ? ordered[i + 1].Value : table.Rows.Count;

            var subTable = table.Clone();
            for (int r = startRow; r < endRow; r++)
            {
                subTable.ImportRow(table.Rows[r]);
            }

            result[key] = subTable;
        }

        return result;
    }

    private async Task ImportDebt(NpgsqlConnection conn, DataTable table)
    {
        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (string.IsNullOrWhiteSpace(row[1]?.ToString())) break;

            var supplierCode = row[1].ToString();
            var contractCode = row[2].ToString();
            if (string.IsNullOrWhiteSpace(supplierCode) || string.IsNullOrWhiteSpace(contractCode))
                continue; // пропускаем неполные строки
            var date = DateTime.Parse(row[3].ToString());
            var amount = decimal.Parse(row[4].ToString());

            await conn.ExecuteAsync(@"
            INSERT INTO incoming_debt (supplier_code, internal_contract_number, debt_date, debt_amount)
            VALUES (@s, @c, @d, @a)
            ON CONFLICT DO NOTHING;",
                new { s = supplierCode, c = contractCode, d = date, a = amount });
        }
    }

    private async Task ImportShipments(NpgsqlConnection conn, DataTable table)
    {
        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (string.IsNullOrWhiteSpace(row[1]?.ToString())) break;

            string supplier = row[1]?.ToString();
            string contract = row[2]?.ToString();
            string order = row[3]?.ToString();
            string shipmentDoc = row[4]?.ToString();
            DateTime docDate = Convert.ToDateTime(row[5]);
            decimal amount = Convert.ToDecimal(row[6]);

            await conn.ExecuteAsync(@"
            INSERT INTO actual_shipments
            (supplier_code, internal_contract_number, order_number, shipment_doc_number, shipment_doc_date, shipment_amount)
            VALUES (@Supplier, @Contract, @Order, @DocNum, @DocDate, @Amount)",
                new { Supplier = supplier, Contract = contract, Order = order, DocNum = shipmentDoc, DocDate = docDate, Amount = amount });
        }
    }

    private async Task ImportContracts(NpgsqlConnection conn, DataTable table)
    {
        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (string.IsNullOrWhiteSpace(row[1]?.ToString())) break;

            string supplier = row[1]?.ToString();
            string internalNumber = row[2]?.ToString();
            string externalNumber = row[3]?.ToString();
            DateTime date = Convert.ToDateTime(row[4]);
            Console.WriteLine($"Пытаемся вставить договор: {internalNumber} (поставщик {supplier})");
            await conn.ExecuteAsync(@"
            INSERT INTO contracts
            (internal_contract_number, supplier_code, external_contract_number, contract_date)
            VALUES (@IntNum, @Supplier, @ExtNum, @Date)
            ON CONFLICT (internal_contract_number) DO NOTHING",
                new { IntNum = internalNumber, Supplier = supplier, ExtNum = externalNumber, Date = date });
        }
    }

    private async Task ImportConditions(NpgsqlConnection conn, DataTable table)
    {
        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (string.IsNullOrWhiteSpace(row[1]?.ToString())) break;

            string contract = row[1]?.ToString();
            string month = row[2]?.ToString();
            int delay = int.Parse(row[3].ToString());

            await conn.ExecuteAsync(@"
            INSERT INTO contract_conditions
            (internal_contract_number, order_month, payment_delay_days)
            VALUES (@Contract, @Month, @Delay)
            ON CONFLICT DO NOTHING",
                new { Contract = contract, Month = month, Delay = delay });
        }
    }
    private async Task ImportSuppliers(NpgsqlConnection conn, DataTable table)
    {
        for (int i = 1; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            var code = row[1]?.ToString()?.Trim();
            var name = row[2]?.ToString()?.Trim();

            Console.WriteLine($"Пытаемся вставить поставщика: {code} — {name}");

            if (string.IsNullOrWhiteSpace(code)) break;

            await conn.ExecuteAsync(@"
            INSERT INTO suppliers (supplier_code, supplier_name)
            VALUES (@Code, @Name)
            ON CONFLICT (supplier_code) DO NOTHING;",
                new { Code = code, Name = name });
        }
    }
    private async Task ImportOrders(NpgsqlConnection conn, DataTable table)
    {
        for (int i = 2; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (string.IsNullOrWhiteSpace(row[1]?.ToString())) break;

            string supplier = row[1]?.ToString();
            string contract = row[2]?.ToString();
            string yearMonth = row[3]?.ToString();
            string orderNumber = row[4]?.ToString();
            decimal amount = Convert.ToDecimal(row[5]);

            await conn.ExecuteAsync(@"
            INSERT INTO supplier_orders 
            (supplier_code, internal_contract_number, order_month, order_number, order_amount)
            VALUES (@Supplier, @Contract, @Month, @Order, @Amount)
            ON CONFLICT DO NOTHING;",
                new { Supplier = supplier, Contract = contract, Month = yearMonth, Order = orderNumber, Amount = amount });
        }
    }
    private async Task ImportShipmentShares(NpgsqlConnection conn, DataTable table)
    {
        for (int i = 2; i < table.Rows.Count; i++)
        {
            var row = table.Rows[i];
            if (string.IsNullOrWhiteSpace(row[1]?.ToString())) break;

            string supplier = row[1]?.ToString();
            decimal share0 = decimal.TryParse(row[2]?.ToString(), out var s0) ? s0 : 0;
            decimal share1 = decimal.TryParse(row[3]?.ToString(), out var s1) ? s1 : 0;
            decimal share2 = decimal.TryParse(row[4]?.ToString(), out var s2) ? s2 : 0;

            await conn.ExecuteAsync(@"
            INSERT INTO shipment_shares
            (supplier_code, share_month_0, share_month_1, share_month_2)
            VALUES (@Supplier, @S0, @S1, @S2)
            ON CONFLICT (supplier_code) DO NOTHING;",
                new { Supplier = supplier, S0 = share0, S1 = share1, S2 = share2 });
        }
    }
}
