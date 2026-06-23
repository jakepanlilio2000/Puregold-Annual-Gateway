using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using LocatorAutoPrint.Models;
using System.IO;
using System.Text;

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
                                SlotNo = reader["SlotNo"].ToString(),
                                RecordCount = Convert.ToInt32(reader["RecordCount"]),
                                InUse = Convert.ToBoolean(reader["InUse"]),
                                Closed = Convert.ToBoolean(reader["Closed"]),
                                Location = reader["Location"].ToString(),
                                Status = reader["Status"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task UpdateLocatorToggleAsync(string slotNo, bool inUse, bool closed)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                IF EXISTS (SELECT 1 FROM PUREGOLD.dbo.LOCATOR WHERE SlotNo = @slotNo)
                BEGIN
                    -- Update if it already exists
                    UPDATE PUREGOLD.dbo.LOCATOR 
                    SET InUse = @inUse, Closed = @closed 
                    WHERE SlotNo = @slotNo
                END
                ELSE
                BEGIN
                    -- Insert a new record if it was previously unused
                    INSERT INTO PUREGOLD.dbo.LOCATOR (SlotNo, RecNo, InUse, Closed) 
                    VALUES (@slotNo, 0, @inUse, @closed)
                END";

                    cmd.Parameters.AddWithValue("@slotNo", slotNo);
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
                    cmd.CommandText = "SELECT SlotNo, Name FROM PUREGOLD.dbo.PRELOC ORDER BY SlotNo";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new PrelocModel
                            {
                                SlotNo = reader["SlotNo"].ToString(),
                                Name = reader["Name"].ToString()
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task<(bool Success, string Message)> AddPrelocAsync(string slotNo, string name)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var checkCmd = conn.CreateCommand())
                {
                    checkCmd.CommandText = "SELECT COUNT(1) FROM PUREGOLD.dbo.PRELOC WHERE SlotNo = @slotNo";
                    checkCmd.Parameters.AddWithValue("@slotNo", slotNo);
                    if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0)
                        return (false, "Duplicate SlotNo exists.");
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "INSERT INTO PUREGOLD.dbo.PRELOC (SlotNo, Name) VALUES (@slotNo, @name)";
                    cmd.Parameters.AddWithValue("@slotNo", slotNo);
                    cmd.Parameters.AddWithValue("@name", name ?? string.Empty);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            return (true, "Locator added successfully.");
        }

        public async Task UpdatePrelocAsync(string slotNo, string newName)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE PUREGOLD.dbo.PRELOC SET Name = @name WHERE SlotNo = @slotNo";
                    cmd.Parameters.AddWithValue("@name", newName ?? string.Empty);
                    cmd.Parameters.AddWithValue("@slotNo", slotNo);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeletePrelocAsync(string slotNo)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM PUREGOLD.dbo.PRELOC WHERE SlotNo = @slotNo";
                    cmd.Parameters.AddWithValue("@slotNo", slotNo);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<List<CountsheetDetailModel>> GetCountsheetDetailsAsync(string slotNo)
        {
            var list = new List<CountsheetDetailModel>();
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

                    cmd.Parameters.AddWithValue("@slotNo", slotNo);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            list.Add(new CountsheetDetailModel
                            {
                                SlotNo = reader["SlotNo"].ToString(),
                                RecNo = Convert.ToInt32(reader["RecNo"]),
                                CountDate = reader["CountDate"] != DBNull.Value ? Convert.ToDateTime(reader["CountDate"]).ToString("MM/dd/yy HH:mm:ss") : "",
                                UPC = reader["UPC"].ToString(),
                                SKU = Convert.ToDecimal(reader["SKU"]).ToString("0"),
                                Descr = reader["Descr"].ToString(),
                                Qty = Convert.ToDouble(reader["Qty"]),
                                Added = Convert.ToBoolean(reader["Added"]),
                                Edited = Convert.ToBoolean(reader["Edited"])
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
                var details = await GetCountsheetDetailsAsync(slotNo);
                if (details.Count == 0) return (false, "No records found to backup.");

                string backupFolder = Path.Combine(appBaseDir, "cntsheet");
                if (!Directory.Exists(backupFolder)) Directory.CreateDirectory(backupFolder);

                var sb = new StringBuilder();
                foreach (var rec in details)
                {
                    string colLoc = rec.SlotNo.PadRight(4);
                    string colRec = rec.RecNo.ToString().PadRight(4);
                    string colDate = rec.CountDate.PadRight(18);
                    string colUpc = rec.UPC.PadRight(15);
                    string colSku = rec.SKU.PadRight(8);
                    string colDesc = rec.Descr.PadRight(32);
                    string colQty = rec.Qty.ToString("0.00").PadRight(8);

                    sb.AppendLine($"{colLoc}{colRec}{colDate}{colUpc}{colSku}{colDesc}{colQty}");
                }

                string filePath = Path.Combine(backupFolder, $"{slotNo}.txt");
                File.WriteAllText(filePath, sb.ToString(), Encoding.ASCII);

                return (true, $"Backup successfully saved to:\n{filePath}");
            }
            catch (Exception ex)
            {
                return (false, $"Backup failed: {ex.Message}");
            }
        }

    }
}