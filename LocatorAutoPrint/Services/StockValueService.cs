using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
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
                    cmd.CommandText = "SELECT Sku, Description, [On Hand], [Unit Ave Cost], [STOCK AMT] FROM PUREGOLD.dbo.StockValue";
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            results.Add(new StockValueModel
                            {
                                Sku = reader["Sku"].ToString(),
                                Description = reader["Description"].ToString(),
                                OnHand = reader["On Hand"] != DBNull.Value ? Convert.ToDouble(reader["On Hand"]) : 0,
                                UnitAveCost = reader["Unit Ave Cost"] != DBNull.Value ? Convert.ToDouble(reader["Unit Ave Cost"]) : 0,
                                StockAmt = reader["STOCK AMT"].ToString()
                            });
                        }
                    }
                }
            }
            return results;
        }
    }
}