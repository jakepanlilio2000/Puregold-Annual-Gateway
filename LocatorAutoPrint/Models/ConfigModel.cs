using System;

namespace LocatorAutoPrint.Models
{
    public class ConfigModel
    {
        // -------------------------------------------------------------
        // 1. Connection & Network Settings
        // -------------------------------------------------------------
        public string DbHost { get; set; } = "192.92.1.100";
        public int DbPort { get; set; } = 1433;
        public string DbCatalog { get; set; } = "PUREGOLD";
        public string DbUser { get; set; } = "sa";
        public string DbPass { get; set; } = "sa";
        public int AppPort { get; set; } = 982;

        public string FtpHost { get; set; } = "192.168.200.177";
        public string FtpDirectory { get; set; } = "/toho/(722)San_Fernando/Others/Annual Gateway/";
        public string FtpPrefix { get; set; } = "A&VG";
        public int FtpTimeoutSeconds { get; set; } = 10;

        // -------------------------------------------------------------
        // 2. Corrections & Data Adjustment Settings
        // -------------------------------------------------------------
        public string DefaultStoreNum { get; set; } = "722";
        public string FallbackStoreName { get; set; } = "PUREGOLD SAN FERNANDO";
        public int ToleranceLimit { get; set; } = 0;
        public string DateFormat { get; set; } = "MM/dd/yyyy";
        public int PollIntervalSeconds { get; set; } = 10;

        // -------------------------------------------------------------
        // 3. Operational Variables & Paths
        // -------------------------------------------------------------
        public string ExportDirectory { get; set; } = "";
        public string LogDirectory { get; set; } = "";
        public string DownloadDirectory { get; set; } = "";
        public bool AutoCheckUpdates { get; set; } = true;
        public bool VerboseLogging { get; set; } = false;

        public ConfigModel Clone()
        {
            return (ConfigModel)this.MemberwiseClone();
        }
    }
}
