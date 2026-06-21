using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class EditCountSheetService
    {
        private readonly string _connectionString;

        public EditCountSheetService(string connectionString)
        {
            _connectionString = connectionString;
        }
        public async Task<CountSheetEditModel> GetRecordAsync(string slotNo, int recNo)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT SlotNo, RecNo, UPC, SKU, Descr, Qty, EditedQty 
                        FROM PUREGOLD.dbo.COUNTSHEET 
                        WHERE SlotNo = @slotNo AND RecNo = @recNo";

                    cmd.Parameters.AddWithValue("@slotNo", slotNo);
                    cmd.Parameters.AddWithValue("@recNo", recNo);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new CountSheetEditModel
                            {
                                SlotNo = reader["SlotNo"].ToString(),
                                RecNo = Convert.ToInt32(reader["RecNo"]),
                                UPC = reader["UPC"].ToString(),
                                SKU = Convert.ToDecimal(reader["SKU"]).ToString("0"),
                                Descr = reader["Descr"].ToString(),
                                OriginalQty = Convert.ToDouble(reader["Qty"]),
                                EditedQty = Convert.ToDouble(reader["EditedQty"])
                            };
                        }
                    }
                }
            }
            return null;
        }

        // Add these inside EditCountSheetService class:

        public async Task<int> GetNextRecordNumberAsync(string slotNo)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    // Gets the highest RecNo and adds 1. Defaults to 1 if no records exist.
                    cmd.CommandText = "SELECT ISNULL(MAX(RecNo), 0) + 1 FROM PUREGOLD.dbo.COUNTSHEET WHERE SlotNo = @slotNo";
                    cmd.Parameters.AddWithValue("@slotNo", slotNo);
                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        public async Task<bool> InsertRecordAsync(CountSheetEditModel record)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                INSERT INTO PUREGOLD.dbo.COUNTSHEET 
                (SlotNo, RecNo, UPC, SKU, Descr, Qty, EditedQty, Edited, Added, CountDate)
                VALUES 
                (@slotNo, @recNo, @upc, @sku, @descr, @qty, @editedQty, 1, 1, GETDATE())";

                    cmd.Parameters.AddWithValue("@slotNo", record.SlotNo);
                    cmd.Parameters.AddWithValue("@recNo", record.RecNo);
                    cmd.Parameters.AddWithValue("@upc", record.UPC ?? "");
                    cmd.Parameters.AddWithValue("@sku", record.SKU ?? "");
                    cmd.Parameters.AddWithValue("@descr", record.Descr ?? "");
                    cmd.Parameters.AddWithValue("@qty", 0);
                    cmd.Parameters.AddWithValue("@editedQty", record.EditedQty);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }
        public async Task<List<ItemLookupResult>> SearchItemAsync(string keyword)
        {
            var results = new List<ItemLookupResult>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT UPC, SKU, Descr 
                FROM PUREGOLD.dbo.items 
                WHERE UPC LIKE @kw OR SKU LIKE @kw OR Descr LIKE @kw";

                    cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new ItemLookupResult
                            {
                                UPC = reader["UPC"].ToString(),
                                SKU = Convert.ToDecimal(reader["SKU"]).ToString("0"),
                                Description = reader["Descr"].ToString()
                            });
                        }
                    }
                }
            }
            return results;
        }

        public async Task<bool> UpdateRecordAsync(CountSheetEditModel record)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        UPDATE PUREGOLD.dbo.COUNTSHEET 
                        SET UPC = @upc, 
                            SKU = @sku, 
                            Descr = @descr, 
                            EditedQty = @editedQty, 
                            Edited = 1 
                        WHERE SlotNo = @slotNo AND RecNo = @recNo";

                    cmd.Parameters.AddWithValue("@upc", record.UPC);
                    cmd.Parameters.AddWithValue("@sku", record.SKU);
                    cmd.Parameters.AddWithValue("@descr", record.Descr);
                    cmd.Parameters.AddWithValue("@editedQty", record.EditedQty);
                    cmd.Parameters.AddWithValue("@slotNo", record.SlotNo);
                    cmd.Parameters.AddWithValue("@recNo", record.RecNo);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<LocatorPrintSummary> GetEditedRecordsSummaryAsync(string slotNo)
        {
            var summary = new LocatorPrintSummary();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT RecNo, UPC, SKU, Descr, Qty, EditedQty, CountDate, Edited, Added
                FROM PUREGOLD.dbo.COUNTSHEET 
                WHERE SlotNo = @slotno 
                ORDER BY RecNo";

                    cmd.Parameters.AddWithValue("@slotno", slotNo);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            double oldQty = Convert.ToDouble(reader["Qty"]);
                            double editedQty = Convert.ToDouble(reader["EditedQty"]);
                            bool isEdited = Convert.ToBoolean(reader["Edited"]);
                            bool isAdded = Convert.ToBoolean(reader["Added"]);

                            if (reader["CountDate"] != DBNull.Value && string.IsNullOrEmpty(summary.CountDate))
                            {
                                summary.CountDate = Convert.ToDateTime(reader["CountDate"]).ToString("MM/dd/yyyy");
                            }

                            summary.TotalScanned++;
                            summary.GrandTotal += editedQty; 
                            if (reader["Descr"].ToString().Trim().Equals("INF", StringComparison.OrdinalIgnoreCase)) summary.InfCount++;
                            if (isAdded) summary.TotalAdded++;
                            if (isEdited) summary.TotalEdited++;

                            if (isEdited)
                            {
                                summary.EditedRecords.Add(new CountRecord
                                {
                                    RecNo = reader["RecNo"].ToString(),
                                    UPC = reader["UPC"].ToString(),
                                    SKU = Convert.ToDecimal(reader["SKU"]).ToString("0"),
                                    Descr = reader["Descr"].ToString(),
                                    OldQtyStr = oldQty % 1 == 0 ? oldQty.ToString("0") : oldQty.ToString("0.###"),
                                    EditedQtyStr = editedQty % 1 == 0 ? editedQty.ToString("0") : editedQty.ToString("0.###")
                                });
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(summary.CountDate)) summary.CountDate = DateTime.Now.ToString("MM/dd/yyyy");
            return summary;
        }
    }
}