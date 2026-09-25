using System;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace LocatorAutoPrint.Services
{
    public class SystemInfoService
    {
        public string GetLocalIpAddress()
        {
            var (ip, _) = GetPrimaryNetworkInterface();
            return ip;
        }

        public string GetMacAddress()
        {
            var (_, mac) = GetPrimaryNetworkInterface();
            return mac;
        }

        public (string ip, string mac) GetPrimaryNetworkInterface()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                                 && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                 && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                                 && !ni.Description.ToLower().Contains("virtual")
                                 && !ni.Description.ToLower().Contains("pseudo")
                                 && !ni.Description.ToLower().Contains("vpn")
                                 && !ni.Description.ToLower().Contains("hyper-v")
                                 && !ni.Description.ToLower().Contains("vmware")
                                 && !ni.Name.ToLower().Contains("vswitch"))
                    .OrderByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                    .ThenByDescending(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    .ToList();

                foreach (var ni in interfaces)
                {
                    var ipProps = ni.GetIPProperties();
                    var ipv4 = ipProps.UnicastAddresses
                        .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork
                                             && !IPAddress.IsLoopback(u.Address));

                    if (ipv4 != null)
                    {
                        var physAddr = ni.GetPhysicalAddress();
                        byte[] bytes = physAddr.GetAddressBytes();
                        string mac = bytes != null && bytes.Length > 0
                            ? string.Join(":", bytes.Select(b => b.ToString("X2")))
                            : "N/A";

                        return (ipv4.Address.ToString(), mac);
                    }
                }

                // Fallback attempt via DNS if no network interface passed the strict filter
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var fallbackIp = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                if (fallbackIp != null)
                {
                    return (fallbackIp.ToString(), "N/A");
                }
            }
            catch
            {
                // Fall through to Disconnected
            }

            return ("Disconnected", "N/A");
        }

        public string GetSqlServerAddress(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                string source = builder.DataSource;
                if (source.Contains(","))
                {
                    return source.Split(',')[0].Trim();
                }
                return source;
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        public string GetDatabaseName(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                return !string.IsNullOrEmpty(builder.InitialCatalog) ? builder.InitialCatalog : "PUREGOLD";
            }
            catch
            {
                return "PUREGOLD";
            }
        }

        public int GetSqlServerPort(string connectionString, int defaultPort = 1433)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                string source = builder.DataSource;
                if (source.Contains(","))
                {
                    string portStr = source.Split(',')[1].Trim();
                    if (int.TryParse(portStr, out int p)) return p;
                }
            }
            catch
            {
            }
            return defaultPort;
        }

        public async Task<bool> CheckSocketReachableAsync(string host, int port = 1433, int timeoutMs = 2000)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Clean host if instance or port is included
                    string targetHost = host;
                    if (targetHost.Contains("\\"))
                    {
                        targetHost = targetHost.Split('\\')[0];
                    }
                    if (targetHost.Contains(","))
                    {
                        targetHost = targetHost.Split(',')[0];
                    }

                    using (var client = new TcpClient())
                    {
                        var result = client.BeginConnect(targetHost, port, null, null);
                        bool success = result.AsyncWaitHandle.WaitOne(timeoutMs);
                        if (success && client.Connected)
                        {
                            client.EndConnect(result);
                            return true;
                        }
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}