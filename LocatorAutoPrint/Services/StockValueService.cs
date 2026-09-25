using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class StockValueService
    {
        private readonly string _connectionString;

        public StockValueService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<StockValueModel>> GetStockValuesAsync()
        {
            var results = new List<StockValueModel>();
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandTimeout = 25;
                    cmd.CommandText = "SELECT Sku, Description, [On Hand], [Unit Ave Cost], [STOCK AMT] FROM PUREGOLD.dbo.StockValue";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new StockValueModel
                            {
                                Sku = reader.GetStringSafe("Sku"),
                                Description = reader.GetStringSafe("Description"),
                                OnHand = reader.GetDoubleSafe("On Hand"),
                                UnitAveCost = reader.GetDoubleSafe("Unit Ave Cost"),
                                StockAmt = reader.GetStringSafe("STOCK AMT")
                            });
                        }
                    }
                }
            }
            return results;
        }
    }
}