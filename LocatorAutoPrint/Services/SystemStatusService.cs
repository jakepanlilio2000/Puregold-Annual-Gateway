using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq; 
using System.Net.NetworkInformation;
using System.Threading; 
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

                    // 1. Get the standard database stats (Total Locators, etc.)
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            SELECT 
                                SUM(CASE 
                                        WHEN ISNULL(pre.statusCancel, 0) <> 1 
                                         AND ISNULL(loc.InUse, 0) <> 1 
                                         AND ISNULL(loc.Closed, 0) = 1 
                                        THEN 1 ELSE 0 
                                    END) AS TotalLocators
                            FROM PUREGOLD.dbo.PRELOC pre
                            LEFT JOIN PUREGOLD.dbo.LOCATOR loc 
                                ON loc.SlotNo = pre.SlotNo";

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                status.TotalLocators = reader["TotalLocators"] != DBNull.Value ? Convert.ToInt32(reader["TotalLocators"]) : 0;
                            }
                        }
                    }

                    // 2. Fetch all IPs that are currently logged in
                    var activeIps = new List<string>();
                    using (var cmd = conn.CreateCommand())
                    {
                        // Gets IPs that are not null and not empty
                        cmd.CommandText = "SELECT ipaddress FROM PUREGOLD.dbo.tblUsers WHERE ipaddress IS NOT NULL AND LTRIM(RTRIM(ipaddress)) <> ''";
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                activeIps.Add(reader["ipaddress"].ToString().Trim());
                            }
                        }
                    }

                    // 3. Ping all IPs simultaneously
                    int onlineCount = 0;
                    var pingTasks = activeIps.Select(async ip =>
                    {
                        if (await PingAddressAsync(ip))
                        {
                            // Thread-safe increment (prevents crashes if two pings finish at the exact same millisecond)
                            Interlocked.Increment(ref onlineCount);
                        }
                    });

                    await Task.WhenAll(pingTasks);
                    status.OnlineConnections = onlineCount;
                }
            }
            catch { /* Return defaults if DB or network drops */ }
            return status;
        }

        // Helper method to execute the ping
        private async Task<bool> PingAddressAsync(string ipAddress)
        {
            try
            {
                using (var pinger = new Ping())
                {
                    // 1000ms timeout
                    var reply = await pinger.SendPingAsync(ipAddress, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}