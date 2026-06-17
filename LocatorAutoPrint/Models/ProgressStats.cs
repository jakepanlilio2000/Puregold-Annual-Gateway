namespace LocatorAutoPrint.Models
{
    public class StatGroup
    {
        public double Comp { get; set; }
        public double Cancel { get; set; }
        public double Total { get; set; }
        public double Pct { get; set; }
        public double Rem => Total - Comp - Cancel;
    }

    public class ProgressStats
    {
        public StatGroup Overall { get; set; } = new StatGroup();
        public StatGroup Selling { get; set; } = new StatGroup();
        public StatGroup Warehouse { get; set; } = new StatGroup();
        public StatGroup Buffer { get; set; } = new StatGroup();
    }
}