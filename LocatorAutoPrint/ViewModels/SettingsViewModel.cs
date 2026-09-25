using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;

namespace LocatorAutoPrint.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly SystemInfoService _sysInfo;

        private static readonly Regex IpRegex = new Regex(
            @"^((25[0-5]|(2[0-4]|1\d|[1-9]|\d)\b)\.){3}(25[0-5]|(2[0-4]|1\d|[1-9]|\d)\b)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // -------------------------------------------------------------
        // Sub-Tab Navigation
        // -------------------------------------------------------------
        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetField(ref _selectedTabIndex, value);
        }

        // -------------------------------------------------------------
        // 1. Connection & Network Settings
        // -------------------------------------------------------------
        private string _dbHost;
        public string DbHost
        {
            get => _dbHost;
            set => SetField(ref _dbHost, value);
        }

        private string _dbPort;
        public string DbPort
        {
            get => _dbPort;
            set => SetField(ref _dbPort, value);
        }

        private string _dbCatalog;
        public string DbCatalog
        {
            get => _dbCatalog;
            set => SetField(ref _dbCatalog, value);
        }

        private string _dbUser;
        public string DbUser
        {
            get => _dbUser;
            set => SetField(ref _dbUser, value);
        }

        private string _dbPass;
        public string DbPass
        {
            get => _dbPass;
            set => SetField(ref _dbPass, value);
        }

        private bool _showPassword;
        public bool ShowPassword
        {
            get => _showPassword;
            set => SetField(ref _showPassword, value);
        }

        private string _ftpHost;
        public string FtpHost
        {
            get => _ftpHost;
            set => SetField(ref _ftpHost, value);
        }

        private string _ftpDirectory;
        public string FtpDirectory
        {
            get => _ftpDirectory;
            set => SetField(ref _ftpDirectory, value);
        }

        private string _ftpPrefix;
        public string FtpPrefix
        {
            get => _ftpPrefix;
            set => SetField(ref _ftpPrefix, value);
        }

        private string _ftpTimeoutSeconds;
        public string FtpTimeoutSeconds
        {
            get => _ftpTimeoutSeconds;
            set => SetField(ref _ftpTimeoutSeconds, value);
        }

        // -------------------------------------------------------------
        // 2. Corrections & Data Adjustment Settings
        // -------------------------------------------------------------
        private string _storeCode;
        public string StoreCode
        {
            get => _storeCode;
            set => SetField(ref _storeCode, value);
        }

        private string _fallbackStoreName;
        public string FallbackStoreName
        {
            get => _fallbackStoreName;
            set => SetField(ref _fallbackStoreName, value);
        }

        private string _toleranceLimit;
        public string ToleranceLimit
        {
            get => _toleranceLimit;
            set => SetField(ref _toleranceLimit, value);
        }

        private string _dateFormat;
        public string DateFormat
        {
            get => _dateFormat;
            set => SetField(ref _dateFormat, value);
        }

        public List<string> AvailableDateFormats { get; } = new List<string>
        {
            "MM/dd/yyyy",
            "yyyyMMdd",
            "yyyy-MM-dd"
        };

        private string _pollIntervalSeconds;
        public string PollIntervalSeconds
        {
            get => _pollIntervalSeconds;
            set => SetField(ref _pollIntervalSeconds, value);
        }

        // -------------------------------------------------------------
        // 3. Operational Variables & Paths
        // -------------------------------------------------------------
        private string _exportDirectory;
        public string ExportDirectory
        {
            get => _exportDirectory;
            set => SetField(ref _exportDirectory, value);
        }

        private string _logDirectory;
        public string LogDirectory
        {
            get => _logDirectory;
            set => SetField(ref _logDirectory, value);
        }

        private string _downloadDirectory;
        public string DownloadDirectory
        {
            get => _downloadDirectory;
            set => SetField(ref _downloadDirectory, value);
        }

        private bool _autoCheckUpdates;
        public bool AutoCheckUpdates
        {
            get => _autoCheckUpdates;
            set => SetField(ref _autoCheckUpdates, value);
        }

        private bool _verboseLogging;
        public bool VerboseLogging
        {
            get => _verboseLogging;
            set => SetField(ref _verboseLogging, value);
        }


        // -------------------------------------------------------------
        // Connection Testing Diagnostics
        // -------------------------------------------------------------
        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set => SetField(ref _isTesting, value);
        }

        private string _testStatus = "Idle";
        public string TestStatus
        {
            get => _testStatus;
            set => SetField(ref _testStatus, value);
        }

        private string _testMessage = "Click 'Test Connection' to verify network and database reachability.";
        public string TestMessage
        {
            get => _testMessage;
            set => SetField(ref _testMessage, value);
        }

        private Brush _testStatusColor = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
        public Brush TestStatusColor
        {
            get => _testStatusColor;
            set => SetField(ref _testStatusColor, value);
        }

        private Brush _testStatusBackground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC));
        public Brush TestStatusBackground
        {
            get => _testStatusBackground;
            set => SetField(ref _testStatusBackground, value);
        }

        // -------------------------------------------------------------
        // Commands
        // -------------------------------------------------------------
        public ICommand TestConnectionCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand RestoreDefaultsCommand { get; }
        public ICommand SelectTabCommand { get; }

        public SettingsViewModel(ConfigService configService, SystemInfoService sysInfo)
        {
            _configService = configService;
            _sysInfo = sysInfo;

            LoadFromModel(_configService?.Config ?? _configService?.CreateDefaultConfig());

            TestConnectionCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsTesting);
            SaveSettingsCommand = new RelayCommand(async _ => await SaveSettingsAsync());
            RestoreDefaultsCommand = new RelayCommand(_ => RestoreDefaults());
            SelectTabCommand = new RelayCommand(param =>
            {
                if (param != null && int.TryParse(param.ToString(), out int tabIndex))
                {
                    SelectedTabIndex = tabIndex;
                }
            });
        }

        private void LoadFromModel(ConfigModel model)
        {
            if (model == null) return;

            DbHost = model.DbHost ?? "192.92.1.100";
            DbPort = model.DbPort > 0 ? model.DbPort.ToString() : "1433";
            DbCatalog = string.IsNullOrWhiteSpace(model.DbCatalog) ? "PUREGOLD" : model.DbCatalog;
            DbUser = model.DbUser ?? "sa";
            DbPass = model.DbPass ?? "sa";

            FtpHost = model.FtpHost ?? "192.168.200.177";
            FtpDirectory = model.FtpDirectory ?? "/toho/(722)San_Fernando/Others/Annual Gateway/";
            FtpPrefix = model.FtpPrefix ?? "A&VG";
            FtpTimeoutSeconds = model.FtpTimeoutSeconds > 0 ? model.FtpTimeoutSeconds.ToString() : "10";

            StoreCode = model.DefaultStoreNum ?? "722";
            FallbackStoreName = model.FallbackStoreName ?? "PUREGOLD SAN FERNANDO";
            ToleranceLimit = model.ToleranceLimit.ToString();
            DateFormat = string.IsNullOrWhiteSpace(model.DateFormat) ? "MM/dd/yyyy" : model.DateFormat;
            PollIntervalSeconds = model.PollIntervalSeconds > 0 ? model.PollIntervalSeconds.ToString() : "10";

            ExportDirectory = model.ExportDirectory ?? "";
            LogDirectory = model.LogDirectory ?? "";
            DownloadDirectory = model.DownloadDirectory ?? "";
            AutoCheckUpdates = model.AutoCheckUpdates;
            VerboseLogging = model.VerboseLogging;
        }

        private async Task TestConnectionAsync()
        {
            if (IsTesting) return;

            IsTesting = true;
            TestStatus = "Testing";
            TestMessage = "Pinging host and testing SQL Server handshake...";
            TestStatusColor = new SolidColorBrush(Color.FromRgb(0x02, 0x84, 0xC7));
            TestStatusBackground = new SolidColorBrush(Color.FromRgb(0xF0, 0xF9, 0xFF));

            string host = (DbHost ?? "").Trim();
            if (string.IsNullOrWhiteSpace(host))
            {
                SetTestResult(false, "Database Host cannot be empty.");
                IsTesting = false;
                return;
            }

            if (!int.TryParse(DbPort, out int port) || port < 1 || port > 65535)
            {
                SetTestResult(false, "Invalid Database Port (must be 1-65535).");
                IsTesting = false;
                return;
            }

            await Task.Run(() =>
            {
                // Step 1: TCP Socket Reachability
                try
                {
                    using (var client = new TcpClient())
                    {
                        var connectTask = client.ConnectAsync(host, port);
                        if (!connectTask.Wait(2500))
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                                SetTestResult(false, $"Host Unreachable: Could not connect to {host}:{port} within 2.5 seconds. Check network route and firewall."));
                            return;
                        }
                    }
                }
                catch (Exception sockEx)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        SetTestResult(false, $"Socket Error: {sockEx.Message} ({host}:{port})"));
                    return;
                }

                // Step 2: SQL Server Handshake
                try
                {
                    var tempConfig = new ConfigModel
                    {
                        DbHost = host,
                        DbPort = port,
                        DbCatalog = (DbCatalog ?? "").Trim(),
                        DbUser = (DbUser ?? "").Trim(),
                        DbPass = DbPass ?? ""
                    };
                    string connStr = ConfigService.BuildConnectionString(tempConfig);

                    using (var conn = new SqlConnection(connStr))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = "SELECT @@VERSION";
                            cmd.CommandTimeout = 4;
                            var version = cmd.ExecuteScalar()?.ToString() ?? "Unknown";
                            string shortVer = version.Contains("\n") ? version.Substring(0, version.IndexOf('\n')).Trim() : version;

                            Application.Current.Dispatcher.Invoke(() =>
                                SetTestResult(true, $"Handshake Successful! Connected to {host}:{port} ({tempConfig.DbCatalog}).\n{shortVer}"));
                        }
                    }
                }
                catch (Exception sqlEx)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        SetTestResult(false, $"Database Handshake Failed: {sqlEx.Message}"));
                }
            });

            IsTesting = false;
        }

        private void SetTestResult(bool success, string message)
        {
            TestStatus = success ? "Success" : "Failed";
            TestMessage = message;
            if (success)
            {
                TestStatusColor = new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46)); // Emerald text
                TestStatusBackground = new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5)); // Mint bg
            }
            else
            {
                TestStatusColor = new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)); // Red text
                TestStatusBackground = new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)); // Pink bg
            }
        }

        private async Task SaveSettingsAsync()
        {
            // Defensive Validations
            string error = ValidateInputs();
            if (!string.IsNullOrEmpty(error))
            {
                CustomMessageBox.Show(error, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if critical parameters changed
            bool criticalChanged = HasCriticalParametersChanged();
            if (criticalChanged)
            {
                var confirm = CustomMessageBox.Show(
                    "You are modifying critical database or network parameters.\n\n" +
                    "Changes will immediately apply to all database queries, sync timers, and FTP update checkers.\n\n" +
                    "Are you sure you want to save and apply these settings?",
                    "Confirm Critical Configuration",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            // Test directory write permissions
            string dirError = ValidateDirectoryPermissions();
            if (!string.IsNullOrEmpty(dirError))
            {
                CustomMessageBox.Show(dirError, "Directory Permission Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build new ConfigModel
            int.TryParse(DbPort, out int dbPort);
            int.TryParse(FtpTimeoutSeconds, out int ftpTimeout);
            int.TryParse(ToleranceLimit, out int tolerance);
            int.TryParse(PollIntervalSeconds, out int pollSec);

            var newConfig = new ConfigModel
            {
                DbHost = (DbHost ?? "").Trim(),
                DbPort = dbPort > 0 ? dbPort : 1433,
                DbCatalog = string.IsNullOrWhiteSpace(DbCatalog) ? "PUREGOLD" : DbCatalog.Trim(),
                DbUser = (DbUser ?? "").Trim(),
                DbPass = DbPass ?? "",
                AppPort = _configService?.Config?.AppPort ?? 982,

                FtpHost = (FtpHost ?? "").Trim(),
                FtpDirectory = (FtpDirectory ?? "").Trim(),
                FtpPrefix = (FtpPrefix ?? "").Trim(),
                FtpTimeoutSeconds = ftpTimeout > 0 ? ftpTimeout : 10,

                DefaultStoreNum = (StoreCode ?? "").Trim(),
                FallbackStoreName = (FallbackStoreName ?? "").Trim(),
                ToleranceLimit = Math.Max(0, tolerance),
                DateFormat = string.IsNullOrWhiteSpace(DateFormat) ? "MM/dd/yyyy" : DateFormat,
                PollIntervalSeconds = pollSec > 0 ? pollSec : 10,

                ExportDirectory = (ExportDirectory ?? "").Trim(),
                LogDirectory = (LogDirectory ?? "").Trim(),
                DownloadDirectory = (DownloadDirectory ?? "").Trim(),
                AutoCheckUpdates = AutoCheckUpdates,
                VerboseLogging = VerboseLogging
            };

            bool saved = _configService.SaveConfig(newConfig);
            if (saved)
            {
                CustomMessageBox.Show("Configuration successfully saved to config.json and applied to runtime.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                CustomMessageBox.Show("Failed to save configuration file. Please check file write permissions.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RestoreDefaults()
        {
            var confirm = CustomMessageBox.Show(
                "Are you sure you want to restore all configuration values to their factory defaults?\n\n" +
                "Unsaved changes will be discarded.",
                "Restore Default Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm == MessageBoxResult.Yes)
            {
                var defaults = _configService.CreateDefaultConfig();
                LoadFromModel(defaults);
                SetTestResult(false, "Defaults loaded into view. Click 'Save & Apply' to persist to disk.");
            }
        }

        private string ValidateInputs()
        {
            // 1. Host Validation
            if (string.IsNullOrWhiteSpace(DbHost))
            {
                return "Database Host / IP cannot be blank.";
            }

            string hostTrimmed = DbHost.Trim();
            if (!hostTrimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                !hostTrimmed.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
                !IpRegex.IsMatch(hostTrimmed) &&
                !Uri.CheckHostName(hostTrimmed).ToString().Contains("Dns"))
            {
                return $"Database Host '{hostTrimmed}' is not a valid IPv4 address or hostname.";
            }

            // 2. Port Validation
            if (!int.TryParse(DbPort, out int dbPort) || dbPort < 1 || dbPort > 65535)
            {
                return "Database Port must be a numeric value between 1 and 65535.";
            }

            // 3. FTP Host Validation
            if (!string.IsNullOrWhiteSpace(FtpHost))
            {
                string ftpTrimmed = FtpHost.Trim();
                if (!ftpTrimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
                    !IpRegex.IsMatch(ftpTrimmed) &&
                    !Uri.CheckHostName(ftpTrimmed).ToString().Contains("Dns"))
                {
                    return $"FTP Host '{ftpTrimmed}' is not a valid IPv4 address or hostname.";
                }
            }

            // 4. FTP Timeout
            if (!int.TryParse(FtpTimeoutSeconds, out int ftpSec) || ftpSec < 1 || ftpSec > 120)
            {
                return "FTP Timeout must be a numeric value between 1 and 120 seconds.";
            }

            // 5. Store Code
            if (string.IsNullOrWhiteSpace(StoreCode))
            {
                return "Store Code cannot be blank (e.g. 722).";
            }

            // 6. Tolerance Limit
            if (!int.TryParse(ToleranceLimit, out int tol) || tol < 0)
            {
                return "Tolerance Limit must be a non-negative integer.";
            }

            // 7. Poll Interval
            if (!int.TryParse(PollIntervalSeconds, out int poll) || poll < 1 || poll > 3600)
            {
                return "Poll Interval must be between 1 and 3600 seconds.";
            }

            return null;
        }

        private string ValidateDirectoryPermissions()
        {
            var pathsToTest = new Dictionary<string, string>
            {
                { "Export Directory", ExportDirectory },
                { "Log Directory", LogDirectory },
                { "Download Directory", DownloadDirectory }
            };

            foreach (var kvp in pathsToTest)
            {
                string label = kvp.Key;
                string dir = kvp.Value;

                if (!string.IsNullOrWhiteSpace(dir))
                {
                    try
                    {
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        string testFile = Path.Combine(dir, $".perm_test_{Guid.NewGuid():N}.tmp");
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch (Exception ex)
                    {
                        return $"Write permission denied for {label}:\n{dir}\n\nError: {ex.Message}";
                    }
                }
            }

            return null;
        }

        private bool HasCriticalParametersChanged()
        {
            var current = _configService?.Config;
            if (current == null) return false;

            int.TryParse(DbPort, out int dbPort);

            return !string.Equals(current.DbHost?.Trim(), (DbHost ?? "").Trim(), StringComparison.OrdinalIgnoreCase) ||
                   current.DbPort != dbPort ||
                   !string.Equals(current.DbCatalog?.Trim(), (DbCatalog ?? "").Trim(), StringComparison.OrdinalIgnoreCase) ||
                   !string.Equals(current.DbUser?.Trim(), (DbUser ?? "").Trim(), StringComparison.Ordinal) ||
                   !string.Equals(current.DbPass ?? "", DbPass ?? "", StringComparison.Ordinal) ||
                   !string.Equals(current.FtpHost?.Trim(), (FtpHost ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}

