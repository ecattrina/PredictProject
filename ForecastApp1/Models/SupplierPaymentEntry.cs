namespace ForecastApp1.Models
{
    public class SupplierPaymentEntry
    {
        public string SupplierCode { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
