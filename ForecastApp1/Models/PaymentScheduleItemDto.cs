namespace ForecastApp1.Models
{
    public class PaymentScheduleItemDto
    {
        public string SupplierCode { get; set; }
        public string InternalContractNumber { get; set; }
        public string OrderNumber { get; set; }
        public string ShipmentDocNumber { get; set; }
        public DateTime ShipmentDocDate { get; set; }
        public DateTime PaymentDate { get; set; } 
        public DateTime PaymentDateByCondition { get; set; } 
        public decimal TotalAmount { get; set; }
    }
}
