namespace LocatorAutoPrint.Models
{
    public class ConfigModel
    {
        public string DbHost { get; set; }
        public string DbUser { get; set; }
        public string DbPass { get; set; }
        public string DefaultStoreNum { get; set; }
        public string FallbackStoreName { get; set; }
        public int AppPort { get; set; } = 982;
    }
}