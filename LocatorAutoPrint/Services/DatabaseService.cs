using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _appBaseDir;

        public DatabaseService(string connectionString, string appBaseDir)
        {
            _connectionString = connectionString;
            _appBaseDir = appBaseDir;
        }

        public async Task<ProgressStats> GetProgressPercentagesAsync()
        {
            var stats = new ProgressStats();

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    bool hasStockLocation = false;
                    using (var checkCmd = conn.CreateCommand())
                    {
                        checkCmd.CommandText = "SELECT 1 FROM sys.columns WHERE Name = N'stocklocation' AND Object_ID = Object_ID(N'PUREGOLD.dbo.PRELOC')";
                        var result = await checkCmd.ExecuteScalarAsync();
                        hasStockLocation = (result != null);
                    }
                    string locationColumnLogic = hasStockLocation
                        ? "ISNULL(NULLIF(LTRIM(RTRIM(pre.stocklocation)), ''), pre.bayname)"
                        : "pre.bayname";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $@"
                            SELECT 
                                pre.Name AS Location,
                                {locationColumnLogic} AS stocklocation,
                                SUM(CASE 
                                        WHEN ISNULL(pre.statusCancel, 0) <> 1 
                                         AND ISNULL(loc.InUse, 0) <> 1 
                                         AND ISNULL(loc.Closed, 0) = 1 
                                        THEN 1 ELSE 0 
                                    END) AS CompletedBays,
                                SUM(CASE WHEN ISNULL(pre.statusCancel, 0) = 1 THEN 1 ELSE 0 END) AS CancelledBays,
                                COUNT(pre.SlotNo) AS TotalBays
                            FROM PUREGOLD.dbo.PRELOC pre
                            LEFT JOIN PUREGOLD.dbo.LOCATOR loc 
                                ON loc.SlotNo = pre.SlotNo
                            GROUP BY 
                                pre.Name, 
                                {locationColumnLogic}";

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string loc = reader.GetStringSafe("Location").ToLower();
                                string stockloc = reader.GetStringSafe("stocklocation").ToLower();

                                double comp = reader.GetDoubleSafe("CompletedBays");
                                double cancel = reader.GetDoubleSafe("CancelledBays");
                                double total = reader.GetDoubleSafe("TotalBays");

                                if (loc.Contains("selling"))
                                {
                                    if (stockloc.Contains("buffer") || stockloc.Contains("topload") || stockloc.Contains("top load"))
                                    {
                                        stats.Buffer.Comp += comp;
                                        stats.Buffer.Cancel += cancel;
                                        stats.Buffer.Total += total;
                                    }
                                    else
                                    {
                                        stats.Selling.Comp += comp;
                                        stats.Selling.Cancel += cancel;
                                        stats.Selling.Total += total;
                                    }
                                }
                                else if (loc.Contains("warehouse") || loc.Contains("receiving") || stockloc.Contains("receiving"))
                                {
                                    stats.Warehouse.Comp += comp;
                                    stats.Warehouse.Cancel += cancel;
                                    stats.Warehouse.Total += total;
                                }

                                stats.Overall.Comp += comp;
                                stats.Overall.Cancel += cancel;
                                stats.Overall.Total += total;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                stats.ErrorMessage = ex.Message;
                ErrorLoggerService.LogException("DatabaseService.GetProgressPercentagesAsync", ex);
            }

            double Calc(double c, double cx, double t) => (t - cx) <= 0 ? 0 : (c / (t - cx)) * 100;

            stats.Overall.Pct = Calc(stats.Overall.Comp, stats.Overall.Cancel, stats.Overall.Total);
            stats.Selling.Pct = Calc(stats.Selling.Comp, stats.Selling.Cancel, stats.Selling.Total);
            stats.Warehouse.Pct = Calc(stats.Warehouse.Comp, stats.Warehouse.Cancel, stats.Warehouse.Total);
            stats.Buffer.Pct = Calc(stats.Buffer.Comp, stats.Buffer.Cancel, stats.Buffer.Total);

            return stats;
        }

        public async Task<(bool Exists, bool IsClosed)> CheckLocatorStatusAsync(int locatorNo)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Closed FROM PUREGOLD.dbo.LOCATOR WHERE SlotNo = @slotno";
                    cmd.Parameters.AddWithValue("@slotno", locatorNo);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            bool isClosed = reader.GetBooleanSafe("Closed");
                            return (true, isClosed);
                        }
                        return (false, false);
                    }
                }
            }
        }

        public async Task<string> GetStoreNameAsync(string defaultStoreNum, string fallbackStoreName)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT STRNAM FROM exclusivesdb.dbo.tblstore WHERE strnum = @strnum";
                        cmd.Parameters.AddWithValue("@strnum", defaultStoreNum ?? string.Empty);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string name = reader.GetStringSafe("STRNAM");
                                if (!string.IsNullOrWhiteSpace(name))
                                {
                                    return name;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("DatabaseService.GetStoreNameAsync", ex);
            }
            return fallbackStoreName;
        }

        public async Task<List<CountRecord>> GetCountSheetDataAsync(int locatorNo)
        {
            var records = new List<CountRecord>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT RecNo, UPC, SKU, Descr, EditedQty, CountDate FROM PUREGOLD.dbo.COUNTSHEET WHERE SlotNo = @slotno ORDER BY RecNo";
                    cmd.Parameters.AddWithValue("@slotno", locatorNo);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            double cleanQty = reader.GetDoubleSafe("EditedQty");
                            string qtyStr = cleanQty % 1 == 0 ? cleanQty.ToString("0") : cleanQty.ToString("0.###");

                            DateTime? countDate = reader.GetDateTimeSafe("CountDate");
                            string formattedDate = countDate.HasValue ? countDate.Value.ToString("M/d/yy HH:mm:ss") : "";

                            records.Add(new CountRecord
                            {
                                RecNo = reader.GetStringSafe("RecNo"),
                                UPC = reader.GetStringSafe("UPC"),
                                SKU = reader.GetDecimalStringSafe("SKU", "0", ""),
                                Descr = reader.GetStringSafe("Descr"),
                                Qty = qtyStr,
                                RawQtyForBackup = cleanQty.ToString("0.00"),
                                FormattedDate = formattedDate,
                                CleanQty = cleanQty
                            });
                        }
                    }
                }
            }
            return records;
        }

        public async Task BackupDatabaseAsync(string dbName)
        {
            string backupRoot = Path.Combine(_appBaseDir, "backups");
            if (!Directory.Exists(backupRoot)) Directory.CreateDirectory(backupRoot);

            string tempBackup = Path.Combine(Path.GetTempPath(), $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = $"BACKUP DATABASE [{dbName}] TO DISK = N'{tempBackup}' WITH INIT, STATS = 10;";
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            if (File.Exists(tempBackup))
            {
                string finalPath = Path.Combine(backupRoot, Path.GetFileName(tempBackup));
                File.Move(tempBackup, finalPath);
            }
        }
    }
}