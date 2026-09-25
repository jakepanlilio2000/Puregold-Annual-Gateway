using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LocatorAutoPrint.Services
{
    public class UpdateCheckResult
    {
        public bool Success { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string CurrentVersion { get; set; } = "3.5";
        public string LatestVersion { get; set; } = "3.5";
        public string RemoteFileName { get; set; }
        public string RemoteFileUrl { get; set; }
        public string StatusMessage { get; set; }
        public string ErrorDetails { get; set; }
    }

    public class UpdateService
    {
        public const string CurrentAppVersion = "3.5";

        private const string DefaultFtpUpdateUrl = "ftp://192.168.200.177/toho/(722)San_Fernando/Others/Annual%20Gateway/";
        private const string FtpPassword = "pw@1234";
        private static readonly string[] FtpUsers = {
            @"puregold\1",  @"puregold\2",  @"puregold\3",  @"puregold\4",
            @"puregold\5",  @"puregold\6",  @"puregold\7",  @"puregold\8",
            @"puregold\9",  @"puregold\10", @"puregold\11", @"puregold\12"
        };

        private readonly ConfigService _configService;

        public UpdateService(ConfigService configService = null)
        {
            _configService = configService;
        }

        public string GetFtpUpdateUrl()
        {
            if (_configService?.Config != null && !string.IsNullOrWhiteSpace(_configService.Config.FtpHost))
            {
                string host = _configService.Config.FtpHost.Trim();
                string dir = (_configService.Config.FtpDirectory ?? "").Trim();
                if (!dir.StartsWith("/")) dir = "/" + dir;
                if (!dir.EndsWith("/")) dir = dir + "/";
                return $"ftp://{host}{Uri.EscapeUriString(dir)}";
            }
            return DefaultFtpUpdateUrl;
        }

        public Regex GetFilePatternRegex()
        {
            string prefix = _configService?.Config?.FtpPrefix;
            if (string.IsNullOrWhiteSpace(prefix)) prefix = "A&VG";
            return new Regex(Regex.Escape(prefix) + @"(?<ver>\d+(?:\.\d+)*)\.exe", RegexOptions.IgnoreCase);
        }

        public int GetTimeoutMs()
        {
            int sec = _configService?.Config?.FtpTimeoutSeconds ?? 10;
            return Math.Max(3, sec) * 1000;
        }

        public async Task<UpdateCheckResult> CheckForUpdatesAsync()
        {
            return await Task.Run(() =>
            {
                string ftpUrl = GetFtpUpdateUrl();
                var result = new UpdateCheckResult
                {
                    CurrentVersion = CurrentAppVersion,
                    LatestVersion = CurrentAppVersion,
                    Success = false
                };

                List<string> fileList = null;
                string lastError = null;

                // Try fetching directory listing using FTP user credentials
                foreach (var user in FtpUsers)
                {
                    try
                    {
                        fileList = FetchDirectoryListing(ftpUrl, user, FtpPassword);
                        if (fileList != null)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                    }
                }

                // If authenticated attempts failed, attempt anonymous fallback
                if (fileList == null)
                {
                    try
                    {
                        fileList = FetchDirectoryListing(ftpUrl, "anonymous", "guest@puregold.com.ph");
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                    }
                }

                if (fileList == null)
                {
                    result.Success = false;
                    result.StatusMessage = $"Unable to reach update server ({_configService?.Config?.FtpHost ?? "192.168.200.177"})";
                    result.ErrorDetails = lastError ?? "Network connection timed out or host is offline.";
                    return result;
                }

                // Parse and compare version numbers
                Version highestVer = ParseSemanticVersion(CurrentAppVersion);
                string highestFileName = null;
                string highestVerStr = CurrentAppVersion;
                Regex pattern = GetFilePatternRegex();

                foreach (var entry in fileList)
                {
                    var match = pattern.Match(entry);
                    if (match.Success)
                    {
                        string verString = match.Groups["ver"].Value;
                        Version parsed = ParseSemanticVersion(verString);

                        if (parsed > highestVer)
                        {
                            highestVer = parsed;
                            highestVerStr = verString;
                            highestFileName = match.Value;
                        }
                    }
                }

                result.Success = true;
                result.LatestVersion = highestVerStr;

                Version localVersion = ParseSemanticVersion(CurrentAppVersion);
                if (highestVer > localVersion && !string.IsNullOrEmpty(highestFileName))
                {
                    result.IsUpdateAvailable = true;
                    result.RemoteFileName = highestFileName;
                    result.RemoteFileUrl = ftpUrl.TrimEnd('/') + "/" + highestFileName;
                    result.StatusMessage = $"New version available: v{highestVerStr}";
                }
                else
                {
                    result.IsUpdateAvailable = false;
                    result.StatusMessage = $"You are using the latest version (v{CurrentAppVersion}).";
                }

                return result;
            });
        }

        public async Task<bool> DownloadUpdateAsync(string remoteFileName, string targetFilePath, IProgress<double> progress = null)
        {
            return await Task.Run(() =>
            {
                string targetUrl = GetFtpUpdateUrl().TrimEnd('/') + "/" + remoteFileName;
                int timeoutMs = GetTimeoutMs();

                foreach (var user in FtpUsers)
                {
                    try
                    {
                        var request = (FtpWebRequest)WebRequest.Create(targetUrl);
                        request.Method = WebRequestMethods.Ftp.DownloadFile;
                        request.Credentials = new NetworkCredential(user, FtpPassword);
                        request.UseBinary = true;
                        request.KeepAlive = false;
                        request.Timeout = timeoutMs;
                        request.ReadWriteTimeout = timeoutMs;

                        using (var response = (FtpWebResponse)request.GetResponse())
                        using (var responseStream = response.GetResponseStream())
                        using (var fileStream = new FileStream(targetFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            long totalBytes = response.ContentLength;
                            byte[] buffer = new byte[8192];
                            int read;
                            long totalRead = 0;

                            while ((read = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fileStream.Write(buffer, 0, read);
                                totalRead += read;

                                if (totalBytes > 0 && progress != null)
                                {
                                    double pct = (double)totalRead / totalBytes * 100.0;
                                    progress.Report(pct);
                                }
                            }
                            return true;
                        }
                    }
                    catch
                    {
                        // Try next credential
                    }
                }

                return false;
            });
        }

        private List<string> FetchDirectoryListing(string url, string username, string password)
        {
            var files = new List<string>();
            var request = (FtpWebRequest)WebRequest.Create(url);
            request.Method = WebRequestMethods.Ftp.ListDirectory;
            request.Credentials = new NetworkCredential(username, password);
            request.UseBinary = true;
            request.KeepAlive = false;
            request.Timeout = 4000;
            request.ReadWriteTimeout = 4000;

            using (var response = (FtpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string clean = line.Trim();
                    if (!string.IsNullOrEmpty(clean))
                    {
                        files.Add(Path.GetFileName(clean));
                    }
                }
            }

            return files;
        }

        private static Version ParseSemanticVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return new Version(0, 0, 0, 0);

            var parts = raw.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int mj) ? mj : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int mn) ? mn : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out int b) ? b : 0;
            int rev = parts.Length > 3 && int.TryParse(parts[3], out int r) ? r : 0;

            return new Version(major, minor, build, rev);
        }
    }
}

