namespace ForecastApp1.Models
{
    public class ShipmentItem
    {
        public string supplier_code { get; set; }
        public string internal_contract_number { get; set; }
        public string order_number { get; set; }
        public string shipment_doc_number { get; set; }
        public DateTime shipment_doc_date { get; set; }
        public decimal shipment_amount { get; set; }
        public int payment_delay_days { get; set; } 
}
