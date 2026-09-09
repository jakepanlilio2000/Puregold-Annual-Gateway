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
            @"puregold\1",  @"puregold\2",  @"puregold\3",  @"puregold\4",
            @"puregold\5",  @"puregold\6",  @"puregold\7",  @"puregold\8",
            @"puregold\9",  @"puregold\10", @"puregold\11", @"puregold\12"
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

               
                Task.Run(() => UploadLogToFtp(fileName, localFilePath));
            }
            catch
            {
               
            }
        }

        private static void UploadLogToFtp(string remoteFileName, string localFilePath)
        {
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
                    request.Timeout = 10000;

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
    }
}