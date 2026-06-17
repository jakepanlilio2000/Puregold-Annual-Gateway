using System;
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

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }
        public string HeaderHostDisplay => $"Host: {Environment.MachineName} | IP: {_sysInfo.GetLocalIpAddress()} | Port: {_configService.Config.AppPort}";
        public string FooterDbDisplay => DbStatus == "SQL Server Connected" ? $"SQL Server Connected ({_sysInfo.GetSqlServerAddress(_configService.ConnectionString)})" : "SQL Server Disconnected";
        public string HostMachineName => Environment.MachineName;

        private string _currentTime;
        public string CurrentTime { get => _currentTime; set { _currentTime = value; OnPropertyChanged(); } }

        private string _dbStatus = "Checking...";
        public string DbStatus { get => _dbStatus; set { _dbStatus = value; OnPropertyChanged(); } }

        private SystemStatusModel _headerStatus = new SystemStatusModel();
        public SystemStatusModel HeaderStatus { get => _headerStatus; set { _headerStatus = value; OnPropertyChanged(); } }

        public ICommand ShowPrintCommand { get; }
        public ICommand ShowEditCommand { get; }
        public ICommand ShowReportsCommand { get; }
        public ICommand ShowUsersCommand { get; }

        public MainViewModel(
            LocatorPrintViewModel locatorPrintViewModel,
            EditCountSheetViewModel editCountSheetViewModel,
            ReportsViewModel reportsViewModel,
            UsersViewModel users,
            SystemStatusService statusService,
            ConfigService configService,
            SystemInfoService sysInfo)
        {
            UsersViewModel = users;
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
            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("hh:mm:ss tt  MM/dd/yyyy");
            _clockTimer.Start();
            _dbTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _dbTimer.Tick += async (s, e) => await CheckSystemStatusAsync();
            _dbTimer.Start();

            _ = CheckSystemStatusAsync(); 
        }

        private async System.Threading.Tasks.Task CheckSystemStatusAsync()
        {
            bool isConnected = await _statusService.CheckDbConnectionAsync();
            DbStatus = isConnected ? "SQL Server Connected" : "SQL Server Disconnected";
            OnPropertyChanged(nameof(FooterDbDisplay));

            if (isConnected) HeaderStatus = await _statusService.GetHeaderStatusAsync();
        }
    }
}