using System.Collections.Generic;

namespace LocatorAutoPrint.Models
{
    public class CountRecord
    {
        public string RecNo { get; set; }
        public string UPC { get; set; }
        public string SKU { get; set; }
        public string Descr { get; set; }

        public string Qty { get; set; }
        public string OldQtyStr { get; set; } 
        public string EditedQtyStr { get; set; } 

        public string RawQtyForBackup { get; set; }
        public string FormattedDate { get; set; }
        public double CleanQty { get; set; }
    }

    public class LocatorPrintSummary
    {
        public List<CountRecord> EditedRecords { get; set; } = new List<CountRecord>();
        public int TotalEdited { get; set; }
        public int TotalAdded { get; set; }
        public int TotalScanned { get; set; }
        public double GrandTotal { get; set; }
        public int InfCount { get; set; }
        public string CountDate { get; set; }
    }
}