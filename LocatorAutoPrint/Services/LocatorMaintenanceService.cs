using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class LocatorMaintenanceService
    {
        private readonly string _connectionString;

        public LocatorMaintenanceService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<LocatorListModel>> GetLocatorListAsync()
        {
            var list = new List<LocatorListModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT 
                    p.SlotNo, 
                    COUNT(c.RecNo) AS RecordCount, 
                    CAST(ISNULL(l.InUse, 0) AS BIT) AS InUse, 
                    CAST(ISNULL(l.Closed, 0) AS BIT) AS Closed,
                    ISNULL(p.Name, 'UNASSIGNED') AS Location,
                    CASE 
                        WHEN ISNULL(p.statusCancel, 0) = 1 THEN 'Inactive'
                        WHEN COUNT(c.RecNo) > 0 THEN 'Active'
                        ELSE 'Unused'
                    END AS Status
                FROM PUREGOLD.dbo.PRELOC p
                LEFT JOIN PUREGOLD.dbo.LOCATOR l ON p.SlotNo = l.SlotNo
                LEFT JOIN PUREGOLD.dbo.COUNTSHEET c ON p.SlotNo = c.SlotNo
                GROUP BY p.SlotNo, l.SlotNo, l.InUse, l.Closed, p.Name, p.statusCancel
                ORDER BY 
                    CASE WHEN ISNUMERIC(p.SlotNo) = 1 THEN CAST(p.SlotNo AS INT) ELSE 999999 END, 
                    p.SlotNo ASC";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new LocatorListModel
                            {
                                SlotNo = reader.GetStringSafe("SlotNo"),
                                RecordCount = reader.GetInt32Safe("RecordCount"),
                                InUse = reader.GetBooleanSafe("InUse"),
                                Closed = reader.GetBooleanSafe("Closed"),
                                Location = reader.GetStringSafe("Location", "UNASSIGNED"),
                                Status = reader.GetStringSafe("Status")
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task UpdateLocatorToggleAsync(string slotNo, bool inUse, bool closed)
        {
            if (string.IsNullOrWhiteSpace(slotNo)) return;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                IF EXISTS (SELECT 1 FROM PUREGOLD.dbo.LOCATOR WHERE SlotNo = @slotNo)
                BEGIN
                    UPDATE PUREGOLD.dbo.LOCATOR 
                    SET InUse = @inUse, Closed = @closed 
                    WHERE SlotNo = @slotNo
                END
                ELSE
                BEGIN
                    INSERT INTO PUREGOLD.dbo.LOCATOR (SlotNo, RecNo, InUse, Closed) 
                    VALUES (@slotNo, 0, @inUse, @closed)
                END";

                    cmd.Parameters.AddWithValue("@slotNo", slotNo.Trim());
                    cmd.Parameters.AddWithValue("@inUse", inUse);
                    cmd.Parameters.AddWithValue("@closed", closed);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<PrelocModel>> GetPrelocListAsync()
        {
            var list = new List<PrelocModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SlotNo, Name FROM PUREGOLD.dbo.PRELOC ORDER BY CASE WHEN ISNUMERIC(SlotNo) = 1 THEN CAST(SlotNo AS INT) ELSE 999999 END, SlotNo";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new PrelocModel
                            {
                                SlotNo = reader.GetStringSafe("SlotNo"),
                                Name = reader.GetStringSafe("Name")
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task<(bool Success, string Message)> AddPrelocAsync(string slotNo, string name)
        {
            if (string.IsNullOrWhiteSpace(slotNo)) return (false, "SlotNo cannot be empty.");

            string cleanSlot = slotNo.Trim();
            string cleanName = (name ?? string.Empty).Trim();

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var checkCmd = conn.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT COUNT(1) FROM PUREGOLD.dbo.PRELOC WHERE SlotNo = @slotNo";
                    checkCmd.Parameters.AddWithValue("@slotNo", cleanSlot);
                    if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                        return (false, "Duplicate SlotNo exists.");
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO PUREGOLD.dbo.PRELOC (SlotNo, Name) VALUES (@slotNo, @name)";
                    cmd.Parameters.AddWithValue("@slotNo", cleanSlot);
                    cmd.Parameters.AddWithValue("@name", cleanName);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return (true, "Locator added successfully.");
        }

        public async Task UpdatePrelocAsync(string slotNo, string newName)
        {
            if (string.IsNullOrWhiteSpace(slotNo)) return;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE PUREGOLD.dbo.PRELOC SET Name = @name WHERE SlotNo = @slotNo";
                    cmd.Parameters.AddWithValue("@name", (newName ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@slotNo", slotNo.Trim());
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeletePrelocAsync(string slotNo)
        {
            if (string.IsNullOrWhiteSpace(slotNo)) return;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM PUREGOLD.dbo.PRELOC WHERE SlotNo = @slotNo";
                    cmd.Parameters.AddWithValue("@slotNo", slotNo.Trim());
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<CountsheetDetailModel>> GetCountsheetDetailsAsync(string slotNo)
        {
            var list = new List<CountsheetDetailModel>();
            if (string.IsNullOrWhiteSpace(slotNo)) return list;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT SlotNo, RecNo, CountDate, UPC, SKU, Descr, Qty, Added, Edited 
                FROM PUREGOLD.dbo.COUNTSHEET 
                WHERE SlotNo = @slotNo 
                ORDER BY RecNo";

                    cmd.Parameters.AddWithValue("@slotNo", slotNo.Trim());

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            DateTime? countDate = reader.GetDateTimeSafe("CountDate");
                            list.Add(new CountsheetDetailModel
                            {
                                SlotNo = reader.GetStringSafe("SlotNo"),
                                RecNo = reader.GetInt32Safe("RecNo"),
                                CountDate = countDate.HasValue ? countDate.Value.ToString("MM/dd/yy HH:mm:ss") : "",
                                UPC = reader.GetStringSafe("UPC"),
                                SKU = reader.GetDecimalStringSafe("SKU", "0", ""),
                                Descr = reader.GetStringSafe("Descr"),
                                Qty = reader.GetDoubleSafe("Qty"),
                                Added = reader.GetBooleanSafe("Added"),
                                Edited = reader.GetBooleanSafe("Edited")
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task<(bool Success, string Message)> BackupLocatorToTxtAsync(string slotNo, string appBaseDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slotNo)) return (false, "Invalid Locator number.");

                var details = await GetCountsheetDetailsAsync(slotNo);
                if (details.Count == 0) return (false, "No records found to backup.");

                string backupFolder = Path.Combine(appBaseDir, "cntsheet");
                if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);

                var sb = new StringBuilder();
                foreach (var rec in details)
                {
                    string colLoc = (rec.SlotNo ?? string.Empty).PadRight(4);
                    string colRec = rec.RecNo.ToString().PadRight(4);
                    string colDate = (rec.CountDate ?? string.Empty).PadRight(18);
                    string colUpc = (rec.UPC ?? string.Empty).PadRight(15);
                    string colSku = (rec.SKU ?? string.Empty).PadRight(8);
                    string colDesc = (rec.Descr ?? string.Empty).PadRight(32);
                    string colQty = rec.Qty.ToString("0.00").PadRight(8);

                    sb.AppendLine($"{colLoc}{colRec}{colDate}{colUpc}{colSku}{colDesc}{colQty}");
                }

                string filePath = Path.Combine(backupFolder, $"{slotNo.Trim()}.txt");
                File.WriteAllText(filePath, sb.ToString(), Encoding.ASCII);

                return (true, $"Backup successfully saved to:\n{filePath}");
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException($"LocatorMaintenanceService.BackupLocatorToTxtAsync({slotNo})", ex);
                return (false, $"Backup failed: {ex.Message}");
            }
        }
    }
}