using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class RestoreService
    {
        private readonly string _connectionString;
        private readonly string _appBaseDir;

        public RestoreService(string connectionString, string appBaseDir)
        {
            _connectionString = connectionString;
            _appBaseDir = appBaseDir;
        }

        public async Task<(bool Success, string Message)> RestoreLocatorAsync(string locatorId)
        {
            string filePath = Path.Combine(_appBaseDir, "cntsheet", $"{locatorId}.txt");

            if (!File.Exists(filePath))
                return (false, $"Backup file not found:\n{filePath}");

            var records = new List<CountRecord>();

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.Length < 89) continue;

                    records.Add(new CountRecord
                    {
                        RecNo = line.Substring(4, 4).Trim(),
                        FormattedDate = line.Substring(8, 18).Trim(),
                        UPC = line.Substring(26, 15).Trim(),
                        SKU = line.Substring(41, 8).Trim(),
                        Descr = line.Substring(49, 32).Trim(),
                        Qty = line.Substring(81, 8).Trim()
                    });
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            using (var delCmd = conn.CreateCommand())
                            {
                                delCmd.Transaction = transaction;
                                delCmd.CommandText = "DELETE FROM PUREGOLD.dbo.COUNTSHEET WHERE SlotNo = @slotNo";
                                delCmd.Parameters.AddWithValue("@slotNo", locatorId);
                                await delCmd.ExecuteNonQueryAsync();
                            }

                            foreach (var rec in records)
                            {
                                using (var insCmd = conn.CreateCommand())
                                {
                                    insCmd.Transaction = transaction;
                                    insCmd.CommandText = @"
                                        INSERT INTO PUREGOLD.dbo.COUNTSHEET 
                                        (SlotNo, RecNo, CountDate, UPC, SKU, Descr, Qty, EditedQty, Posted, Added, Edited) 
                                        VALUES (@slotNo, @recNo, @cDate, @upc, @sku, @descr, @qty, 0, 0, 0, 0)";

                                    insCmd.Parameters.AddWithValue("@slotNo", locatorId);
                                    insCmd.Parameters.AddWithValue("@recNo", int.Parse(rec.RecNo));

                                    if (DateTime.TryParse(rec.FormattedDate, out DateTime parsedDate))
                                        insCmd.Parameters.AddWithValue("@cDate", parsedDate);
                                    else
                                        insCmd.Parameters.AddWithValue("@cDate", DateTime.Now);

                                    insCmd.Parameters.AddWithValue("@upc", rec.UPC);
                                    insCmd.Parameters.AddWithValue("@sku", decimal.Parse(rec.SKU));
                                    insCmd.Parameters.AddWithValue("@descr", rec.Descr);
                                    insCmd.Parameters.AddWithValue("@qty", decimal.Parse(rec.Qty));

                                    await insCmd.ExecuteNonQueryAsync();
                                }
                            }
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                return (true, $"Successfully restored {records.Count} records for Locator {locatorId}.");
            }
            catch (Exception ex)
            {
                return (false, $"Error during restore process:\n{ex.Message}");
            }
        }
    }
}