namespace LocatorAutoPrint.Models
{
    public class CountsheetDetailModel
    {
        public string SlotNo { get; set; }
        public int RecNo { get; set; }
        public string CountDate { get; set; }
        public string UPC { get; set; }
        public string SKU { get; set; }
        public string Descr { get; set; }
        public double Qty { get; set; }
        public bool Added { get; set; }
        public bool Edited { get; set; }
    }
}