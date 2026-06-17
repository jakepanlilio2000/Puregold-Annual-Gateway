using System;
using System.Collections.Generic;
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
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT SlotNo, RecNo, SKU, UPC, Descr, Qty 
                FROM PUREGOLD.dbo.COUNTSHEET 
                WHERE Descr = 'INF'
                ORDER BY SlotNo, RecNo";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT c.SlotNo, COUNT(c.RecNo) as RecordCount, SUM(c.EditedQty) as TotalQty, 
                       COUNT(DISTINCT c.SKU) as SkuCount, p.remarks as Remarks
                FROM PUREGOLD.dbo.COUNTSHEET c
                LEFT JOIN PUREGOLD.dbo.PRELOC p ON c.SlotNo = p.SlotNo
                GROUP BY c.SlotNo, p.remarks
                ORDER BY c.SlotNo";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
                await conn.OpenAsync();
                using (var cmd1 = conn.CreateCommand())
                {
                    cmd1.CommandText = "SELECT COUNT(SlotNo) FROM PUREGOLD.dbo.LOCATOR WHERE InUse = 1 AND Closed = 0";
                    kpis.LoadedLocators = Convert.ToInt32(await cmd1.ExecuteScalarAsync());
                }
                using (var cmd2 = conn.CreateCommand())
                {
                    cmd2.CommandText = "SELECT COUNT(DISTINCT c.SlotNo) FROM PUREGOLD.dbo.COUNTSHEET c JOIN PUREGOLD.dbo.LOCATOR l ON c.SlotNo = l.SlotNo WHERE l.Closed = 0";
                    kpis.PreCounts = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
                }
            }
            return kpis;
        }

        public async Task<List<UnloadedLocatorModel>> GetUnloadedLocatorsAsync()
        {
            var results = new List<UnloadedLocatorModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT p.SlotNo, p.Name, p.stocklocation as Location
                FROM PUREGOLD.dbo.PRELOC p
                LEFT JOIN PUREGOLD.dbo.COUNTSHEET c ON p.SlotNo = c.SlotNo
                LEFT JOIN PUREGOLD.dbo.LOCATOR l ON p.SlotNo = l.SlotNo
                WHERE c.SlotNo IS NULL OR l.InUse = 0 OR l.InUse IS NULL";

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT SlotNo, Name, aisle, bay, bayname, stocklocation FROM PUREGOLD.dbo.PRELOC";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        SELECT cupc, sku, citem 
                        FROM exclusivesdb.dbo.TBLpricechk 
                        WHERE cupc LIKE @kw OR sku LIKE @kw OR citem LIKE @kw";

                    cmd.Parameters.AddWithValue("@kw", $"%{keyword}%");

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
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
    }
}