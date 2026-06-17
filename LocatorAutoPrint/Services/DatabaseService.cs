using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
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
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT 
                                pre.Name AS Location,
                                pre.stocklocation,
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
                            GROUP BY pre.Name, pre.stocklocation";

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                string loc = reader["Location"].ToString().ToLower();
                                string stockloc = reader["stocklocation"].ToString().ToLower();

                                double comp = Convert.ToDouble(reader["CompletedBays"]);
                                double cancel = Convert.ToDouble(reader["CancelledBays"]);
                                double total = Convert.ToDouble(reader["TotalBays"]);

                                if (stockloc.Contains("top load") || stockloc.Contains("buffer"))
                                {
                                    stats.Buffer.Comp += comp; stats.Buffer.Cancel += cancel; stats.Buffer.Total += total;
                                }
                                else if (loc.Contains("warehouse") || loc.Contains("receiving"))
                                {
                                    stats.Warehouse.Comp += comp; stats.Warehouse.Cancel += cancel; stats.Warehouse.Total += total;
                                }
                                else if (loc.Contains("selling"))
                                {
                                    stats.Selling.Comp += comp; stats.Selling.Cancel += cancel; stats.Selling.Total += total;
                                }
                            }
                        }
                    }
                }
            }
            catch {  }

            double Calc(double c, double cx, double t) => (t - cx) <= 0 ? 0 : (c / (t - cx)) * 100;

            stats.Overall.Comp = stats.Selling.Comp + stats.Warehouse.Comp + stats.Buffer.Comp;
            stats.Overall.Cancel = stats.Selling.Cancel + stats.Warehouse.Cancel + stats.Buffer.Cancel;
            stats.Overall.Total = stats.Selling.Total + stats.Warehouse.Total + stats.Buffer.Total;

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
                            string closedVal = reader["Closed"].ToString();
                            bool isClosed = closedVal == "1" || closedVal.Equals("true", StringComparison.OrdinalIgnoreCase);
                            return (true, isClosed);
                        }
                        return (false, false);
                    }
                }
            }
        }

        public async Task<string> GetStoreNameAsync(string defaultStoreNum, string fallbackStoreName)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT STRNAM FROM exclusivesdb.dbo.tblstore WHERE strnum = @strnum";
                    cmd.Parameters.AddWithValue("@strnum", defaultStoreNum);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return reader["STRNAM"].ToString();
                        }
                    }
                }
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
                            double cleanQty = Convert.ToDouble(reader["EditedQty"]);
                            string qtyStr = cleanQty % 1 == 0 ? cleanQty.ToString("0") : cleanQty.ToString("0.###");

                            string formattedDate = "";
                            if (reader["CountDate"] != DBNull.Value)
                            {
                                formattedDate = Convert.ToDateTime(reader["CountDate"]).ToString("M/d/yy HH:mm:ss");
                            }

                            records.Add(new CountRecord
                            {
                                RecNo = reader["RecNo"].ToString(),
                                UPC = reader["UPC"].ToString(),
                                SKU = reader["SKU"].ToString(),
                                Descr = reader["Descr"].ToString(),
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

            string finalPath = Path.Combine(backupRoot, Path.GetFileName(tempBackup));
            File.Move(tempBackup, finalPath);
        }
    }
}