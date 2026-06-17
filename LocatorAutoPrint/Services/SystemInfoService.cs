using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace LocatorAutoPrint.Services
{
    public class SystemInfoService
    {
        public string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            return ip?.ToString() ?? "127.0.0.1";
        }

        public string GetSqlServerAddress(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                return builder.DataSource;
            }
            catch
            {
                return "Unknown DB Host";
            }
        }
    }
}