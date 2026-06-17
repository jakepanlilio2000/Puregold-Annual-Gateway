namespace LocatorAutoPrint.Models
{
    public class StockValueModel
    {
        public string Sku { get; set; }
        public string Description { get; set; }
        public double OnHand { get; set; }
        public double UnitAveCost { get; set; }
        public string StockAmt { get; set; }
    }
}