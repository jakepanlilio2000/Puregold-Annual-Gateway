using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Services;
using LocatorAutoPrint.Views.Modals;

namespace LocatorAutoPrint.ViewModels
{
    public class AboutViewModel : ViewModelBase
    {
        private readonly UpdateService _updateService;
        private readonly SystemInfoService _sysInfo;
        private readonly ConfigService _configService;

        public string AppName { get; set; } = "Puregold Annual Gateway";
        public string AppFullName { get; set; } = "Locator Auto Print & Inventory Gateway";
        public string CurrentVersion { get; set; } = "v3.5";
        public string BuildTag { get; set; } = "v3.5 (Production Release)";
        public string BuildDate { get; set; } = DateTime.Now.ToString("MMMM yyyy");
        public string RuntimePlatform { get; set; } = $".NET Framework 4.8 | {(Environment.Is64BitProcess ? "x64 Process" : "x86 (32-bit) Process")}";

        // ============================================================
        // 1. LOCAL TELEMETRY
        // ============================================================
        public string MachineName { get; set; } = Environment.MachineName;
        public string WindowsUser { get; set; } = $"{Environment.UserDomainName}\\{Environment.UserName}";
        public string OsVersion { get; set; } = Environment.OSVersion.VersionString;
        public string OsArchitecture { get; set; } = $"{(Environment.Is64BitOperatingSystem ? "64-bit (x64)" : "32-bit (x86)")} OS";

        private string _localIpAddress = "Disconnected";
        public string LocalIpAddress
        {
            get => _localIpAddress;
            set { _localIpAddress = value; OnPropertyChanged(); }
        }

        private string _macAddress = "N/A";
        public string MacAddress
        {
            get => _macAddress;
            set { _macAddress = value; OnPropertyChanged(); }
        }

        // ============================================================
        // 2. DATABASE & CONNECTION STATUS
        // ============================================================
        private string _dbServer = "127.0.0.1";
        public string DbServer
        {
            get => _dbServer;
            set { _dbServer = value; OnPropertyChanged(); }
        }

        private int _dbPort = 1433;
        public int DbPort
        {
            get => _dbPort;
            set { _dbPort = value; OnPropertyChanged(); }
        }

        private string _dbInstance = "PUREGOLD";
        public string DbInstance
        {
            get => _dbInstance;
            set { _dbInstance = value; OnPropertyChanged(); }
        }

        private bool _isDbReachable = true;
        public bool IsDbReachable
        {
            get => _isDbReachable;
            set { _isDbReachable = value; OnPropertyChanged(); OnPropertyChanged(nameof(DbHealthDisplay)); }
        }

        private string _dbHealthDisplay = "Testing Socket...";
        public string DbHealthDisplay
        {
            get => _dbHealthDisplay;
            set { _dbHealthDisplay = value; OnPropertyChanged(); }
        }

        // ============================================================
        // 3. DEVELOPER & IDENTITY METADATA
        // ============================================================
        public string DeveloperName { get; set; } = "Jake Ashley C. Panlilio";
        public string PersonalEmail { get; set; } = "jakepanlilio2000@gmail.com";
        public string CompanyEmail { get; set; } = "jcpanlilio@puregold.intra";
        public string StoreLocation { get; set; } = "Store Code: 722 - San Fernando I (Zone 11)";

        public string EscalationDirective { get; set; } =
            "For escalation, email jcpanlilio@puregold.intra via Thunderbird (Store Code: 722 - San Fernando I)";

        // ============================================================
        // 4. UPDATER STATUS & TELEMETRY
        // ============================================================
        private string _updateStatus = "Click 'Check for Updates' to query the intranet repository.";
        public string UpdateStatus
        {
            get => _updateStatus;
            set { _updateStatus = value; OnPropertyChanged(); }
        }

        private bool _isChecking;
        public bool IsChecking
        {
            get => _isChecking;
            set { _isChecking = value; OnPropertyChanged(); }
        }

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set { _isUpdateAvailable = value; OnPropertyChanged(); }
        }

        private string _latestVersion = "3.5";
        public string LatestVersion
        {
            get => _latestVersion;
            set { _latestVersion = value; OnPropertyChanged(); }
        }

        private string _remoteFileName;
        public string RemoteFileName
        {
            get => _remoteFileName;
            set { _remoteFileName = value; OnPropertyChanged(); }
        }

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        private double _downloadProgress;
        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        // ============================================================
        // COMMANDS
        // ============================================================
        public ICommand CheckUpdateCommand { get; }
        public ICommand DownloadUpdateCommand { get; }
        public ICommand OpenHelpGuideCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand LaunchEmailCommand { get; }

        public AboutViewModel(UpdateService updateService, SystemInfoService sysInfo, ConfigService configService)
        {
            _updateService = updateService ?? new UpdateService();
            _sysInfo = sysInfo ?? new SystemInfoService();
            _configService = configService;

            // Initialize telemetries
            if (_sysInfo != null)
            {
                LocalIpAddress = _sysInfo.GetLocalIpAddress();
                MacAddress = _sysInfo.GetMacAddress();

                if (_configService != null)
                {
                    DbServer = _sysInfo.GetSqlServerAddress(_configService.ConnectionString);
                    DbPort = _sysInfo.GetSqlServerPort(_configService.ConnectionString, _configService.Config?.AppPort ?? 1433);
                    DbInstance = _sysInfo.GetDatabaseName(_configService.ConnectionString);
                }
            }

            CheckUpdateCommand = new RelayCommand(async _ => await RunCheckForUpdatesAsync(), _ => !IsChecking && !IsDownloading);
            DownloadUpdateCommand = new RelayCommand(async _ => await RunDownloadUpdateAsync(), _ => IsUpdateAvailable && !IsDownloading);
            OpenHelpGuideCommand = new RelayCommand(_ => ShowHelpGuide());
            TestConnectionCommand = new RelayCommand(async _ => await CheckDbHealthAsync());
            LaunchEmailCommand = new RelayCommand(_ => LaunchThunderbirdEmail());

            _ = CheckDbHealthAsync();
        }

        public async Task CheckDbHealthAsync()
        {
            try
            {
                DbHealthDisplay = "Testing Socket...";
                bool reachable = await _sysInfo.CheckSocketReachableAsync(DbServer, DbPort);
                IsDbReachable = reachable;
                DbHealthDisplay = reachable ? $"Online (Port {DbPort} Reachable)" : $"Unreachable (Host/Port {DbPort})";
            }
            catch (Exception ex)
            {
                IsDbReachable = false;
                DbHealthDisplay = "Unreachable";
                ErrorLoggerService.LogException("AboutViewModel.CheckDbHealthAsync", ex, isTerminating: false);
            }
        }

        private async Task RunCheckForUpdatesAsync()
        {
            IsChecking = true;
            UpdateStatus = "Connecting to repository (192.168.200.177)...";

            try
            {
                var result = await _updateService.CheckForUpdatesAsync();

                UpdateStatus = result.StatusMessage;
                IsUpdateAvailable = result.IsUpdateAvailable;
                LatestVersion = result.LatestVersion;
                RemoteFileName = result.RemoteFileName;

                if (!result.Success && !string.IsNullOrEmpty(result.ErrorDetails))
                {
                    ErrorLoggerService.LogException("AboutViewModel.CheckForUpdates", new Exception(result.ErrorDetails), isTerminating: false);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = "Unable to reach update server (192.168.200.177)";
                ErrorLoggerService.LogException("AboutViewModel.CheckForUpdatesAsync", ex, isTerminating: false);
            }
            finally
            {
                IsChecking = false;
            }
        }

        private async Task RunDownloadUpdateAsync()
        {
            if (string.IsNullOrEmpty(RemoteFileName)) return;

            IsDownloading = true;
            DownloadProgress = 0;
            UpdateStatus = $"Downloading {RemoteFileName}...";

            try
            {
                string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Puregold Updates");
                Directory.CreateDirectory(targetDir);
                string targetFilePath = Path.Combine(targetDir, RemoteFileName);

                var progressReporter = new Progress<double>(pct =>
                {
                    DownloadProgress = pct;
                    UpdateStatus = $"Downloading {RemoteFileName}... {pct:0}%";
                });

                bool success = await _updateService.DownloadUpdateAsync(RemoteFileName, targetFilePath, progressReporter);

                if (success && File.Exists(targetFilePath))
                {
                    UpdateStatus = $"Downloaded to Desktop\\Puregold Updates\\{RemoteFileName}";
                    var promptResult = CustomMessageBox.Show(
                        $"Update {RemoteFileName} has been downloaded to:\n{targetFilePath}\n\nWould you like to open the folder now?",
                        "Update Downloaded",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (promptResult == MessageBoxResult.Yes)
                    {
                        Process.Start("explorer.exe", $"/select,\"{targetFilePath}\"");
                    }
                }
                else
                {
                    UpdateStatus = "Download failed. Please check network connection.";
                    CustomMessageBox.Show("Unable to download update executable from FTP repository.", "Download Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Download failed: {ex.Message}";
                ErrorLoggerService.LogException("AboutViewModel.DownloadUpdateAsync", ex, isTerminating: false);
            }
            finally
            {
                IsDownloading = false;
            }
        }

        private void LaunchThunderbirdEmail()
        {
            try
            {
                string mailtoUri = "mailto:jcpanlilio@puregold.intra?subject=%5BEscalation%20-%20Store%20722%5D%20A%26VG%203.5%20Issue&body=Store%20Code:%20722%20-%20San%20Fernando%20I%0D%0AMachine:%20" 
                    + Uri.EscapeDataString(Environment.MachineName) 
                    + "%0D%0AIP:%20" 
                    + Uri.EscapeDataString(LocalIpAddress)
                    + "%0D%0A%0D%0ADescribe%20issue%20below:%0D%0A";

                Process.Start(new ProcessStartInfo(mailtoUri) { UseShellExecute = true });
            }
            catch
            {
                CustomMessageBox.Show(
                    "Unable to launch email client automatically.\nPlease email jcpanlilio@puregold.intra via Thunderbird directly.\n\nSubject: [Escalation - Store 722] A&VG 3.5 Issue",
                    "Email Support",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ShowHelpGuide()
        {
            try
            {
                var guide = new HowToUseWindow();
                guide.Owner = Application.Current.MainWindow;
                guide.ShowDialog();
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("AboutViewModel.ShowHelpGuide", ex, isTerminating: false);
            }
        }
    }
}

