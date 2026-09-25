using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;

namespace LocatorAutoPrint.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;

        private readonly ConfigService _configService;
        private readonly SystemInfoService _sysInfo;

        private readonly LocatorPrintViewModel _locatorPrintViewModel;
        private readonly EditCountSheetViewModel _editCountSheetViewModel;
        private readonly SystemStatusService _statusService;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _dbTimer;

        public UsersViewModel UsersViewModel { get; }
        public ReportsViewModel ReportsViewModel { get; }
        public AboutViewModel AboutViewModel { get; }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                if (_currentViewModel != value)
                {
                    _currentViewModel = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPrintActive));
                    OnPropertyChanged(nameof(IsEditActive));
                    OnPropertyChanged(nameof(IsReportsActive));
                    OnPropertyChanged(nameof(IsUsersActive));
                    OnPropertyChanged(nameof(IsAboutActive));
                    OnPropertyChanged(nameof(IsSettingsActive));
                }
            }
        }

        public bool IsPrintActive => CurrentViewModel is LocatorPrintViewModel;
        public bool IsEditActive => CurrentViewModel is EditCountSheetViewModel;
        public bool IsReportsActive => CurrentViewModel is ReportsViewModel;
        public bool IsUsersActive => CurrentViewModel is UsersViewModel;
        public bool IsAboutActive => CurrentViewModel is AboutViewModel;
        public bool IsSettingsActive => CurrentViewModel is SettingsViewModel;

        public string HostMachineName => Environment.MachineName;

        private string _localIpAddress;
        public string LocalIpAddress
        {
            get => _localIpAddress ?? (_localIpAddress = _sysInfo.GetLocalIpAddress());
            set
            {
                if (_localIpAddress != value)
                {
                    _localIpAddress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HeaderIpDisplay));
                    OnPropertyChanged(nameof(IsIpConnected));
                }
            }
        }

        public string HeaderIpDisplay => LocalIpAddress == "Disconnected" ? "IP: Disconnected" : $"IP: {LocalIpAddress}";
        public bool IsIpConnected => LocalIpAddress != "Disconnected";

        public string HeaderHostDisplay => $"Host: {Environment.MachineName} | {HeaderIpDisplay} | Port: {_configService.Config?.AppPort ?? 982}";
        public string FooterDbDisplay => DbStatus == "SQL Server Connected" ? $"SQL Server Connected ({_sysInfo.GetSqlServerAddress(_configService.ConnectionString)})" : "SQL Server Disconnected";

        private string _currentTime;
        public string CurrentTime { get => _currentTime; set { _currentTime = value; OnPropertyChanged(); } }

        private string _dbStatus = "Checking...";
        public string DbStatus { get => _dbStatus; set { _dbStatus = value; OnPropertyChanged(); } }

        private SystemStatusModel _headerStatus = new SystemStatusModel();
        public SystemStatusModel HeaderStatus { get => _headerStatus; set { _headerStatus = value; OnPropertyChanged(); } }

        public SettingsViewModel SettingsViewModel { get; }

        public ICommand ShowPrintCommand { get; }
        public ICommand ShowEditCommand { get; }
        public ICommand ShowReportsCommand { get; }
        public ICommand ShowUsersCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand ShowSettingsCommand { get; }

        public MainViewModel(
            LocatorPrintViewModel locatorPrintViewModel,
            EditCountSheetViewModel editCountSheetViewModel,
            ReportsViewModel reportsViewModel,
            UsersViewModel users,
            AboutViewModel aboutViewModel,
            SettingsViewModel settingsViewModel,
            SystemStatusService statusService,
            ConfigService configService,
            SystemInfoService sysInfo)
        {
            UsersViewModel = users;
            AboutViewModel = aboutViewModel;
            SettingsViewModel = settingsViewModel;
            _configService = configService;
            _sysInfo = sysInfo;
            _locatorPrintViewModel = locatorPrintViewModel;
            _editCountSheetViewModel = editCountSheetViewModel;
            ReportsViewModel = reportsViewModel;
            _statusService = statusService;

            CurrentViewModel = _locatorPrintViewModel;

            ShowPrintCommand = new RelayCommand(_ => CurrentViewModel = _locatorPrintViewModel);
            ShowEditCommand = new RelayCommand(_ => CurrentViewModel = _editCountSheetViewModel);
            ShowReportsCommand = new RelayCommand(_ => CurrentViewModel = ReportsViewModel);
            ShowUsersCommand = new RelayCommand(_ => CurrentViewModel = UsersViewModel);
            ShowAboutCommand = new RelayCommand(_ => CurrentViewModel = AboutViewModel);
            ShowSettingsCommand = new RelayCommand(_ => CurrentViewModel = SettingsViewModel);

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("hh:mm:ss tt  MM/dd/yyyy");
            _clockTimer.Start();

            _dbTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _dbTimer.Tick += async (s, e) => await SafeCheckSystemStatusAsync();
            _dbTimer.Start();

            _ = SafeCheckSystemStatusAsync();
        }

        private async Task SafeCheckSystemStatusAsync()
        {
            try
            {
                LocalIpAddress = _sysInfo.GetLocalIpAddress();

                bool isConnected = await _statusService.CheckDbConnectionAsync();
                DbStatus = isConnected ? "SQL Server Connected" : "SQL Server Disconnected";
                OnPropertyChanged(nameof(FooterDbDisplay));

                if (isConnected)
                {
                    HeaderStatus = await _statusService.GetHeaderStatusAsync();
                }
            }
            catch (Exception ex)
            {
                DbStatus = "SQL Server Disconnected";
                OnPropertyChanged(nameof(FooterDbDisplay));
                ErrorLoggerService.LogException("MainViewModel.CheckSystemStatusAsync", ex, isTerminating: false);
            }
        }
    }
}