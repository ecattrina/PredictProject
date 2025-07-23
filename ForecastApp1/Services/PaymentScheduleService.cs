using ForecastApp1.Models;
using Npgsql;
using Dapper;

namespace ForecastApp1.Services
{
    public class PaymentScheduleService
    {
        private readonly Func<NpgsqlConnection> _connectionFactory;

        public PaymentScheduleService(Func<NpgsqlConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<List<PaymentScheduleItemDto>> GetPaymentScheduleAsync(DateTime calculationStartDate)
        {
            Console.WriteLine($"[START] Расчет графика платежей на дату: {calculationStartDate:yyyy-MM-dd}");

            using var connection = _connectionFactory();
            await connection.OpenAsync();

            // Получаем актуальные долги на указанную дату
            var debts = (await connection.QueryAsync<DebtItem>(@"
            SELECT supplier_code, internal_contract_number, debt_amount
            FROM incoming_debt
            WHERE debt_date = (
            SELECT MAX(debt_date) FROM incoming_debt WHERE debt_date <= @Date
            )", new { Date = calculationStartDate })).ToList();

            Console.WriteLine($"[INFO] Найдено долгов: {debts.Count}");

            var result = new List<PaymentScheduleItemDto>();
            // Обрабатываем каждый долг по поставщику и договору
            foreach (var debt in debts)
            {
                Console.WriteLine($"[DEBT] Поставщик: {debt.supplier_code}, Контракт: {debt.internal_contract_number}, Сумма: {debt.debt_amount}");

                decimal remainingDebt = debt.debt_amount;

                // Получить отгрузки по договору и поставщику
                var shipments = (await connection.QueryAsync<ShipmentItem>(@"
                SELECT s.supplier_code, s.internal_contract_number, s.order_number, s.shipment_doc_number, s.shipment_doc_date, s.shipment_amount,
                 cc.payment_delay_days
                 FROM actual_shipments s
                 INNER JOIN contract_conditions cc
                  ON cc.internal_contract_number = s.internal_contract_number
                WHERE s.supplier_code = @SupplierCode AND s.internal_contract_number = @InternalContractNumber
                ORDER BY s.shipment_doc_date DESC
                ", new
                {
                    SupplierCode = debt.supplier_code,
                    InternalContractNumber = debt.internal_contract_number
                })).ToList();

                Console.WriteLine($"[INFO] Найдено отгрузок: {shipments.Count}");

                var selectedShipments = new List<(ShipmentItem, decimal)>(); 
                // Распределяем долг на отгрузки
                foreach (var shipment in shipments)
                {
                    if (remainingDebt <= 0) break;

                    decimal amountToTake = Math.Min(remainingDebt, shipment.shipment_amount);
                    remainingDebt -= amountToTake;
                    selectedShipments.Add((shipment, amountToTake));

                    Console.WriteLine($"[SELECTED] Отгрузка от {shipment.shipment_doc_date:yyyy-MM-dd}, сумма: {shipment.shipment_amount}, взято: {amountToTake}, осталось долга: {remainingDebt}");
                }
               
                // Расчет даты платежа по каждой отгрузке
                foreach (var (shipment, amount) in selectedShipments)
                { // Дата оплаты по договору (отгрузка + отсрочка)
                    DateTime paymentDateByCondition = shipment.shipment_doc_date.AddDays(shipment.payment_delay_days);
                    // Если дата в прошлом — берем расчетную дату
                    DateTime rawForecastDate = paymentDateByCondition >= calculationStartDate
                      ? paymentDateByCondition
                       : calculationStartDate;
                    // Корректируем на ближайший рабочий день
                    DateTime forecastedPaymentDate = AdjustToNearestWorkday(rawForecastDate);
                  
                    result.Add(new PaymentScheduleItemDto
                    {
                        SupplierCode = shipment.supplier_code,
                        InternalContractNumber = shipment.internal_contract_number,
                        OrderNumber = shipment.order_number,
                        ShipmentDocNumber = shipment.shipment_doc_number,
                        ShipmentDocDate = shipment.shipment_doc_date,
                        TotalAmount = amount,
                        PaymentDateByCondition = shipment.shipment_doc_date.AddDays(shipment.payment_delay_days),
                        PaymentDate = forecastedPaymentDate
                    });

                    Console.WriteLine($"[RESULT] Поставщик: {shipment.supplier_code}, Дата оплаты: {forecastedPaymentDate:yyyy-MM-dd}, Сумма: {amount}");
                }
            }
            // Группируем по поставщику и дате платежа
            var groupedResult = result
                .GroupBy(r => new { r.SupplierCode, r.PaymentDate })
                .Select(g => new PaymentScheduleItemDto
                {
                    SupplierCode = g.Key.SupplierCode,
                    PaymentDate = g.Key.PaymentDate,
                    TotalAmount = g.Sum(x => x.TotalAmount)
                })
                .OrderBy(r => r.PaymentDate)
                .ToList();

            Console.WriteLine($"[DONE] Сформировано записей в графике: {groupedResult.Count}");

            return result.OrderBy(r => r.PaymentDate).ToList();
        }
        private DateTime AdjustToNearestWorkday(DateTime date)
        {
            // Сдвигаем дату вперёд, если она попадает на выходной
            while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
            }
            return date;
        }
    }
}
