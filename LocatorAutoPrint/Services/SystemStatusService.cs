using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using LocatorAutoPrint.Models;

namespace LocatorAutoPrint.Services
{
    public class SystemStatusService
    {
        private readonly string _connectionString;

        public SystemStatusService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<bool> CheckDbConnectionAsync()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        public async Task<SystemStatusModel> GetHeaderStatusAsync()
        {
            var status = new SystemStatusModel();
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT 
                                SUM(CASE 
                                        WHEN ISNULL(pre.statusCancel, 0) <> 1 
                                         AND ISNULL(loc.InUse, 0) <> 1 
                                         AND ISNULL(loc.Closed, 0) = 1 
                                        THEN 1 ELSE 0 
                                    END) AS TotalLocators,
                                SUM(CASE 
                                        WHEN ISNULL(loc.InUse, 0) = 1 
                                        THEN 1 ELSE 0 
                                    END) AS ActiveConnections
                            FROM PUREGOLD.dbo.PRELOC pre
                            LEFT JOIN PUREGOLD.dbo.LOCATOR loc 
                                ON loc.SlotNo = pre.SlotNo";

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                status.TotalLocators = reader["TotalLocators"] != DBNull.Value ? Convert.ToInt32(reader["TotalLocators"]) : 0;
                                status.UnclosedLocators = reader["ActiveConnections"] != DBNull.Value ? Convert.ToInt32(reader["ActiveConnections"]) : 0;
                            }
                        }
                    }
                }
            }
            catch {  }
            return status;
        }
    }
}