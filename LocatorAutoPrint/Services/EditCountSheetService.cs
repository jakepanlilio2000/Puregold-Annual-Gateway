using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Threading.Tasks;
using LocatorAutoPrint.Helpers;
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
            if (string.IsNullOrWhiteSpace(slotNo) || recNo <= 0) return null;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT
                            SlotNo,
                            RecNo,
                            UPC,
                            SKU,
                            Descr,
                            Qty,
                            EditedQty,
                            CountDate,
                            Posted,
                            Added,
                            Edited,
                            LastEditedQty
                        FROM PUREGOLD.dbo.COUNTSHEET
                        WHERE SlotNo = @slotNo
                        AND RecNo = @recNo";

                    cmd.Parameters.AddWithValue("@slotNo", slotNo.Trim());
                    cmd.Parameters.AddWithValue("@recNo", recNo);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new CountSheetEditModel
                            {
                                SlotNo = reader.GetStringSafe("SlotNo"),
                                RecNo = reader.GetInt32Safe("RecNo"),
                                UPC = reader.GetStringSafe("UPC"),
                                SKU = reader.GetDecimalStringSafe("SKU", "0", ""),
                                Descr = reader.GetStringSafe("Descr"),
                                OriginalQty = reader.GetDoubleSafe("Qty"),
                                EditedQty = reader.GetDoubleSafe("EditedQty")
                            };
                        }
                    }
                }
            }
            return null;
        }

        public async Task<int> GetNextRecordNumberAsync(string slotNo)
        {
            if (string.IsNullOrWhiteSpace(slotNo)) return 1;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT ISNULL(MAX(RecNo), 0) + 1 FROM PUREGOLD.dbo.COUNTSHEET WHERE SlotNo = @slotNo";
                    cmd.Parameters.AddWithValue("@slotNo", slotNo.Trim());
                    var result = await cmd.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 1;
                }
            }
        }

        public async Task<bool> InsertRecordAsync(CountSheetEditModel record)
        {
            if (record == null) return false;

            decimal.TryParse(record.SKU, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal skuValue);

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

                    cmd.Parameters.AddWithValue("@slotNo", (record.SlotNo ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@recNo", record.RecNo);
                    cmd.Parameters.AddWithValue("@upc", (record.UPC ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@sku", skuValue); 
                    cmd.Parameters.AddWithValue("@descr", (record.Descr ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@qty", 0.0);
                    cmd.Parameters.AddWithValue("@editedQty", record.EditedQty);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<List<ItemLookupResult>> SearchItemAsync(string keyword)
        {
            var results = new List<ItemLookupResult>();
            if (string.IsNullOrWhiteSpace(keyword)) return results;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                                    SELECT UPC, SKU, Descr
                                    FROM PUREGOLD.dbo.Items
                                    WHERE
                                           UPC   LIKE @kw
                                        OR SKU   LIKE @kw
                                        OR Descr LIKE @kw
                                    ORDER BY UPC";

                    cmd.Parameters.AddWithValue("@kw", $"%{keyword.Trim()}%");

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new ItemLookupResult
                            {
                                UPC = reader.GetStringSafe("UPC"),
                                SKU = reader.GetDecimalStringSafe("SKU", "0", ""),
                                Description = reader.GetStringSafe("Descr")
                            });
                        }
                    }
                }
            }
            return results;
        }

        public async Task<bool> UpdateRecordAsync(CountSheetEditModel record)
        {
            if (record == null) return false;

            decimal.TryParse(record.SKU, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal skuValue);

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
                WHERE SlotNo = @slotNo 
                  AND RecNo = @recNo";

                    cmd.Parameters.AddWithValue("@upc", (record.UPC ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@sku", skuValue);
                    cmd.Parameters.AddWithValue("@descr", (record.Descr ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@editedQty", record.EditedQty);
                    cmd.Parameters.AddWithValue("@slotNo", (record.SlotNo ?? string.Empty).Trim());
                    cmd.Parameters.AddWithValue("@recNo", record.RecNo);

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        throw new Exception("The record could not be found to update. It may have been deleted by another user.");
                    }

                    return true;
                }
            }
        }

        public async Task<LocatorPrintSummary> GetEditedRecordsSummaryAsync(string slotNo)
        {
            var summary = new LocatorPrintSummary();
            if (string.IsNullOrWhiteSpace(slotNo)) return summary;

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

                    cmd.Parameters.AddWithValue("@slotno", slotNo.Trim());

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            double oldQty = reader.GetDoubleSafe("Qty");
                            double editedQty = reader.GetDoubleSafe("EditedQty");
                            bool isEdited = reader.GetBooleanSafe("Edited");
                            bool isAdded = reader.GetBooleanSafe("Added");

                            DateTime? dt = reader.GetDateTimeSafe("CountDate");
                            if (dt.HasValue && string.IsNullOrEmpty(summary.CountDate))
                            {
                                summary.CountDate = dt.Value.ToString("MM/dd/yyyy");
                            }

                            summary.TotalScanned++;
                            summary.GrandTotal += editedQty;
                            string descr = reader.GetStringSafe("Descr");
                            if (descr.Equals("INF", StringComparison.OrdinalIgnoreCase)) summary.InfCount++;
                            if (isAdded) summary.TotalAdded++;
                            if (isEdited) summary.TotalEdited++;

                            if (isEdited)
                            {
                                summary.EditedRecords.Add(new CountRecord
                                {
                                    RecNo = reader.GetStringSafe("RecNo"),
                                    UPC = reader.GetStringSafe("UPC"),
                                    SKU = reader.GetDecimalStringSafe("SKU", "0", ""),
                                    Descr = descr,
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