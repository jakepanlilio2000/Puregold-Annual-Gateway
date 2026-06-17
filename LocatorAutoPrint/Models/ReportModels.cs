namespace LocatorAutoPrint.Models
{
    public class SummaryReportModel
    {
        public string SlotNo { get; set; }
        public int RecordCount { get; set; }
        public double TotalQty { get; set; }
        public int SkuCount { get; set; }
        public string Remarks { get; set; }
    }

    public class MonitoringKpiModel
    {
        public int LoadedLocators { get; set; }
        public int PreCounts { get; set; }
    }

    public class UnloadedLocatorModel
    {
        public string SlotNo { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
    }

    public class LocatorLocationModel
    {
        public string SlotNo { get; set; }
        public string Name { get; set; }
        public string Aisle { get; set; }
        public string Bay { get; set; }
        public string BayName { get; set; }
        public string StockLocation { get; set; }
    }

    public class SystemStatusModel
    {
        public int TotalLocators { get; set; }
        public int UnclosedLocators { get; set; }
    }
}