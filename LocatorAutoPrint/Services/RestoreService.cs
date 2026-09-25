using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
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
            if (string.IsNullOrWhiteSpace(locatorId))
            {
                return (false, "Please provide a valid Locator number.");
            }

            string cleanLocatorId = locatorId.Trim();
            string filePath = Path.Combine(_appBaseDir, "cntsheet", $"{cleanLocatorId}.txt");

            if (!File.Exists(filePath))
                return (false, $"Backup file not found:\n{filePath}");

            var records = new List<CountRecord>();

            try
            {
                string[] lines = File.ReadAllLines(filePath);
                int lineIndex = 0;
                foreach (var line in lines)
                {
                    lineIndex++;
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

                if (records.Count == 0)
                {
                    return (false, $"Backup file for Locator {cleanLocatorId} exists but contains no valid records.");
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
                                delCmd.Parameters.AddWithValue("@slotNo", cleanLocatorId);
                                await delCmd.ExecuteNonQueryAsync();
                            }

                            int fallbackRecNo = 1;
                            foreach (var rec in records)
                            {
                                using (var insCmd = conn.CreateCommand())
                                {
                                    insCmd.Transaction = transaction;
                                    insCmd.CommandText = @"
                                        INSERT INTO PUREGOLD.dbo.COUNTSHEET 
                                        (SlotNo, RecNo, CountDate, UPC, SKU, Descr, Qty, EditedQty, Posted, Added, Edited) 
                                        VALUES (@slotNo, @recNo, @cDate, @upc, @sku, @descr, @qty, @qty, 0, 0, 0)";

                                    insCmd.Parameters.AddWithValue("@slotNo", cleanLocatorId);

                                    if (int.TryParse(rec.RecNo, out int parsedRecNo))
                                        insCmd.Parameters.AddWithValue("@recNo", parsedRecNo);
                                    else
                                        insCmd.Parameters.AddWithValue("@recNo", fallbackRecNo);

                                    if (DateTime.TryParse(rec.FormattedDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate) ||
                                        DateTime.TryParse(rec.FormattedDate, out parsedDate))
                                        insCmd.Parameters.AddWithValue("@cDate", parsedDate);
                                    else
                                        insCmd.Parameters.AddWithValue("@cDate", DateTime.Now);

                                    insCmd.Parameters.AddWithValue("@upc", rec.UPC ?? string.Empty);

                                    if (decimal.TryParse(rec.SKU, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedSku))
                                        insCmd.Parameters.AddWithValue("@sku", parsedSku);
                                    else
                                        insCmd.Parameters.AddWithValue("@sku", 0m);

                                    insCmd.Parameters.AddWithValue("@descr", rec.Descr ?? string.Empty);

                                    if (decimal.TryParse(rec.Qty, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedQty))
                                        insCmd.Parameters.AddWithValue("@qty", parsedQty);
                                    else
                                        insCmd.Parameters.AddWithValue("@qty", 0m);

                                    await insCmd.ExecuteNonQueryAsync();
                                }
                                fallbackRecNo++;
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            try { transaction.Rollback(); } catch { }
                            ErrorLoggerService.LogException($"RestoreService.RestoreLocatorAsync({cleanLocatorId}) - Transaction Failed", ex);
                            throw;
                        }
                    }
                }
                return (true, $"Successfully restored {records.Count} records for Locator {cleanLocatorId}.");
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException($"RestoreService.RestoreLocatorAsync({cleanLocatorId})", ex);
                return (false, $"Error during restore process:\n{ex.Message}");
            }
        }
    }
}