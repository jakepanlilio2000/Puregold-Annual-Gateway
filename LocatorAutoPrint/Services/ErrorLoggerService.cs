using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LocatorAutoPrint.Services
{
    public static class ErrorLoggerService
    {
        private const string FtpBaseDirectory = "ftp://192.168.200.177/toho/(722)San_Fernando/Others/Annual%20Gateway/logs/";
        private const string FtpPassword = "pw@1234";
        private static readonly string[] FtpUsers = {
            @"puregold\ftp1",  @"puregold\ftp2",  @"puregold\ftp3",  @"puregold\ftp4",
            @"puregold\ftp5",  @"puregold\ftp6",  @"puregold\ftp7",  @"puregold\ftp8",
            @"puregold\ftp9",  @"puregold\ftp10", @"puregold\ftp11", @"puregold\ftp12"
        };

        public static void LogException(string context, Exception ex, bool isTerminating = false)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine($"ERROR / CRASH REPORT - {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Machine Name  : {Environment.MachineName}");
                sb.AppendLine($"Logged In User: {Environment.UserDomainName}\\{Environment.UserName}");
                sb.AppendLine($"Context       : {context}");
                sb.AppendLine($"Is Terminating: {isTerminating}");
                sb.AppendLine("--------------------------------------------------------------------------------");

                int depth = 0;
                Exception currentEx = ex;
                while (currentEx != null)
                {
                    sb.AppendLine($"[Level {depth}] {currentEx.GetType().FullName}: {currentEx.Message}");
                    sb.AppendLine($"Stack Trace:\n{currentEx.StackTrace}");
                    sb.AppendLine("--------------------------------------------------------------------------------");
                    currentEx = currentEx.InnerException;
                    depth++;
                }

                string localDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashLogs");
                Directory.CreateDirectory(localDirectory);

                string fileName = $"{(isTerminating ? "Crash" : "Error")}_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmssfff}.log";
                string localFilePath = Path.Combine(localDirectory, fileName);
                File.WriteAllText(localFilePath, sb.ToString());

                // Fire-and-forget non-blocking upload with low timeout
                Task.Run(() => UploadLogToFtp(fileName, localFilePath));
            }
            catch
            {
                // Never allow telemetry logging itself to crash the application
            }
        }

        private static void UploadLogToFtp(string remoteFileName, string localFilePath)
        {
            try
            {
                if (!File.Exists(localFilePath)) return;
                byte[] fileBytes = File.ReadAllBytes(localFilePath);
                string targetUrl = FtpBaseDirectory.TrimEnd('/') + "/" + remoteFileName;

                foreach (var user in FtpUsers)
                {
                    try
                    {
                        var request = (FtpWebRequest)WebRequest.Create(targetUrl);
                        request.Method = WebRequestMethods.Ftp.UploadFile;
                        request.Credentials = new NetworkCredential(user, FtpPassword);
                        request.UseBinary = true;
                        request.KeepAlive = false;
                        request.Timeout = 3000;
                        request.ReadWriteTimeout = 3000;

                        using (var requestStream = request.GetRequestStream())
                        {
                            requestStream.Write(fileBytes, 0, fileBytes.Length);
                        }

                        using (var response = (FtpWebResponse)request.GetResponse())
                        {
                            return; 
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch
            {
                // Silent fail for network background upload
            }
        }
    }
}