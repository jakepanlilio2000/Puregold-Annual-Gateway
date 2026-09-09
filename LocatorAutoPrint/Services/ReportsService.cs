using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class ReportsService
    {
        private readonly string _connectionString;

        public ReportsService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<InfReportModel>> GetInfRecordsAsync()
        {
            var results = new List<InfReportModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 15;
                    cmd.CommandText = @"
                SELECT SlotNo, RecNo, SKU, UPC, Descr, Qty 
                FROM PUREGOLD.dbo.COUNTSHEET WITH (NOLOCK)
                WHERE Descr = 'INF'
                ORDER BY SlotNo, RecNo";

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            results.Add(new InfReportModel
                            {
                                SlotNo = reader["SlotNo"].ToString(),
                                RecNo = Convert.ToInt32(reader["RecNo"]),
                                SKU = Convert.ToDecimal(reader["SKU"]).ToString("0"),
                                UPC = reader["UPC"].ToString(),
                                Descr = reader["Descr"].ToString(),
                                Qty = Convert.ToDouble(reader["Qty"])
                            });
                        }
                    }
                }
            }
            return results;
        }

        public async Task<List<SummaryReportModel>> GetSummaryReportAsync()
        {
            var results = new List<SummaryReportModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 15;
                    cmd.CommandText = @"
                SELECT c.SlotNo, COUNT(c.RecNo) as RecordCount, SUM(c.EditedQty) as TotalQty, 
                       COUNT(DISTINCT c.SKU) as SkuCount, p.remarks as Remarks
                FROM PUREGOLD.dbo.COUNTSHEET c WITH (NOLOCK)
                LEFT JOIN PUREGOLD.dbo.PRELOC p WITH (NOLOCK) ON c.SlotNo = p.SlotNo
                GROUP BY c.SlotNo, p.remarks
                ORDER BY c.SlotNo";

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            results.Add(new SummaryReportModel
                            {
                                SlotNo = reader["SlotNo"].ToString(),
                                RecordCount = Convert.ToInt32(reader["RecordCount"]),
                                TotalQty = Convert.ToDouble(reader["TotalQty"]),
                                SkuCount = Convert.ToInt32(reader["SkuCount"]),
                                Remarks = reader["Remarks"].ToString()
                            });
                        }
                    }
                }
            }
            return results;
        }

        public async Task<MonitoringKpiModel> GetMonitoringKpisAsync()
        {
            var kpis = new MonitoringKpiModel();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd1 = conn.CreateCommand())
                {
                    cmd1.CommandTimeout = 10;
                    cmd1.CommandText = "SELECT COUNT(SlotNo) FROM PUREGOLD.dbo.LOCATOR WITH (NOLOCK) WHERE InUse = 1 AND Closed = 0";
                    kpis.LoadedLocators = Convert.ToInt32(await cmd1.ExecuteScalarAsync().ConfigureAwait(false));
                }
                using (var cmd2 = conn.CreateCommand())
                {
                    cmd2.CommandTimeout = 10;
                    cmd2.CommandText = "SELECT COUNT(DISTINCT c.SlotNo) FROM PUREGOLD.dbo.COUNTSHEET c WITH (NOLOCK) JOIN PUREGOLD.dbo.LOCATOR l WITH (NOLOCK) ON c.SlotNo = l.SlotNo WHERE l.Closed = 0";
                    kpis.PreCounts = Convert.ToInt32(await cmd2.ExecuteScalarAsync().ConfigureAwait(false));
                }
            }
            return kpis;
        }

        public async Task<List<UnloadedLocatorModel>> GetUnloadedLocatorsAsync()
        {
            var results = new List<UnloadedLocatorModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 15;
                    cmd.CommandText = @"
                SELECT p.SlotNo, p.Name, p.stocklocation as Location
                FROM PUREGOLD.dbo.PRELOC p WITH (NOLOCK)
                LEFT JOIN PUREGOLD.dbo.COUNTSHEET c WITH (NOLOCK) ON p.SlotNo = c.SlotNo
                LEFT JOIN PUREGOLD.dbo.LOCATOR l WITH (NOLOCK) ON p.SlotNo = l.SlotNo
                WHERE c.SlotNo IS NULL OR l.InUse = 0 OR l.InUse IS NULL";

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            results.Add(new UnloadedLocatorModel
                            {
                                SlotNo = reader["SlotNo"].ToString(),
                                Name = reader["Name"].ToString(),
                                Location = reader["Location"].ToString()
                            });
                        }
                    }
                }
            }
            return results;
        }

        public async Task<List<LocatorLocationModel>> GetLocatorLocationsAsync()
        {
            var results = new List<LocatorLocationModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 15;
                    cmd.CommandText = "SELECT SlotNo, Name, aisle, bay, bayname, stocklocation FROM PUREGOLD.dbo.PRELOC WITH (NOLOCK)";
                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            results.Add(new LocatorLocationModel
                            {
                                SlotNo = reader["SlotNo"].ToString(),
                                Name = reader["Name"].ToString(),
                                Aisle = reader["aisle"].ToString(),
                                Bay = reader["bay"].ToString(),
                                BayName = reader["bayname"].ToString(),
                                StockLocation = reader["stocklocation"].ToString()
                            });
                        }
                    }
                }
            }
            return results;
        }

        public async Task<List<ItemLookupResult>> SearchSkuAsync(string keyword)
        {
            var results = new List<ItemLookupResult>();
            if (string.IsNullOrWhiteSpace(keyword)) return results;

            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 15;
                    cmd.CommandText = @"
                        SELECT cupc, sku, citem 
                        FROM exclusivesdb.dbo.TBLpricechk WITH (NOLOCK)
                        WHERE cupc LIKE @kw OR sku LIKE @kw OR citem LIKE @kw";

                    cmd.Parameters.Add("@kw", SqlDbType.VarChar, 100).Value = $"%{keyword.Trim()}%";

                    using (var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync().ConfigureAwait(false))
                        {
                            results.Add(new ItemLookupResult
                            {
                                UPC = reader["cupc"].ToString(),
                                SKU = reader["sku"] != DBNull.Value ? Convert.ToDecimal(reader["sku"]).ToString("0") : "",
                                Description = reader["citem"].ToString()
                            });
                        }
                    }
                }
            }
            return results;
        }

        public async Task<(bool Success, string Message)> AddToMasterfileAsync(ItemLookupResult item)
        {
            if (item == null) return (false, "No item selected.");

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync().ConfigureAwait(false);

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandTimeout = 15;
                        cmd.CommandText = @"
                        IF EXISTS (SELECT 1 FROM PUREGOLD.dbo.ITEMS WITH (NOLOCK) WHERE UPC = @upc)
                        BEGIN
                            SELECT -1; -- Indicates already exists
                        END
                        ELSE
                        BEGIN
                            INSERT INTO PUREGOLD.dbo.ITEMS (UPC, SKU, Descr, Price, Type) 
                            VALUES (@upc, @sku, @descr, @price, @type);
                            SELECT 1;  -- Indicates inserted
                        END";

                        cmd.Parameters.Add("@upc", SqlDbType.VarChar, 50).Value = (object)item.UPC ?? string.Empty;
                        cmd.Parameters.Add("@sku", SqlDbType.VarChar, 50).Value = (object)item.SKU ?? string.Empty;
                        cmd.Parameters.Add("@descr", SqlDbType.VarChar, 255).Value = (object)item.Description ?? string.Empty;
                        cmd.Parameters.Add("@price", SqlDbType.Decimal).Value = 1.0m;
                        cmd.Parameters.Add("@type", SqlDbType.VarChar, 50).Value = "Standard Item";

                        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                        int status = Convert.ToInt32(result);

                        if (status == -1)
                        {
                            return (false, "Item already exists in the Masterfile.");
                        }

                        return (true, "Successfully added to Masterfile!");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsService.AddToMasterfileAsync", ex);
                return (false, $"Database error: {ex.Message}");
            }
        }
    }
}