using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LocatorAutoPrint.ViewModels
{
    public class LocatorPrintViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;
        private readonly PrintService _printService;
        private readonly ConfigService _configService;
        private readonly LocatorMaintenanceViewModel _maintenanceVM;
        private readonly RestoreService _restoreService;
        private readonly DispatcherTimer _timer;

        private string _locatorInput;
        public string LocatorInput
        {
            get => _locatorInput;
            set { _locatorInput = value; OnPropertyChanged(); }
        }

        private ProgressStats _stats = new ProgressStats();
        public ProgressStats Stats
        {
            get => _stats;
            set { _stats = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand PrintCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand ShowGuideCommand { get; }
        public ICommand ShowHowToCommand { get; }
        public ICommand ShowAppInfoCommand { get; }

        public ICommand OpenCountsheetListCommand { get; }
        public ICommand OpenPrelocMaintenanceCommand { get; }
        public ICommand RestoreLocatorCommand { get; }

        public LocatorPrintViewModel(
            DatabaseService dbService,
            PrintService printService,
            ConfigService configService,
            LocatorMaintenanceViewModel maintenanceVM,
            RestoreService restoreService)
        {
            _dbService = dbService;
            _printService = printService;
            _configService = configService;
            _maintenanceVM = maintenanceVM;
            _restoreService = restoreService;

            RefreshCommand = new RelayCommand(async _ => await LoadStatsAsync());
            PrintCommand = new RelayCommand(async _ => await ExecutePrintAsync());
            CloseCommand = new RelayCommand(_ => Application.Current.Shutdown());

            ShowGuideCommand = new RelayCommand(_ => CustomMessageBox.Show("Enter locator numbers separated by periods (e.g., 1.2.3) or ranges (e.g., 5-10).", "User Guide"));

            ShowHowToCommand = new RelayCommand(_ => {
                var win = new Views.Modals.HowToUseWindow();
                win.ShowDialog();
            });

            ShowAppInfoCommand = new RelayCommand(_ => ShowAppInfo());

            OpenCountsheetListCommand = new RelayCommand(_ => {
                var win = new Views.Modals.CountsheetListWindow { DataContext = _maintenanceVM };
                _maintenanceVM.LoadCountsheetListCommand.Execute(null);
                win.ShowDialog();
            });

            OpenPrelocMaintenanceCommand = new RelayCommand(_ => {
                var win = new Views.Modals.PrelocMaintenanceWindow { DataContext = _maintenanceVM };
                _maintenanceVM.LoadPrelocListCommand.Execute(null);
                win.ShowDialog();
            });

            RestoreLocatorCommand = new RelayCommand(async _ => {

                var dialog = new Views.Modals.InputDialogWindow("Enter Locator Number to restore from backup file:", "Restore Locator");

                if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputText))
                    return;

                string input = dialog.InputText.Trim();

                if (CustomMessageBox.Show($"This will REPLACE ALL EXISTING RECORDS for SlotNo {input}. Continue?", "CRITICAL WARNING", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                {
                    var result = await _restoreService.RestoreLocatorAsync(input);
                    CustomMessageBox.Show(result.Message, result.Success ? "Restore Complete" : "Restore Failed", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
                }
            });

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += async (s, e) => await LoadStatsAsync();
            _timer.Start();

            _ = LoadStatsAsync();
        }

        private async System.Threading.Tasks.Task LoadStatsAsync()
        {
            var newStats = await _dbService.GetProgressPercentagesAsync();
            Stats = newStats;
        }

        private async System.Threading.Tasks.Task ExecutePrintAsync()
        {
            var locators = LocatorParser.Parse(LocatorInput);
            if (locators.Count == 0) return;

            foreach (var locatorNo in locators)
            {
                var status = await _dbService.CheckLocatorStatusAsync(locatorNo);
                if (!status.Exists)
                {
                    CustomMessageBox.Show($"Locator Number: {locatorNo} does not exist in the database.", "Invalid Locator", MessageBoxButton.OK, MessageBoxImage.Warning);
                    continue;
                }
                if (!status.IsClosed)
                {
                    CustomMessageBox.Show($"Locator {locatorNo} is currently OPEN (0).\n\nPlease ensure the device/locator is closed before printing.", "Locator Open", MessageBoxButton.OK, MessageBoxImage.Warning);
                    continue;
                }

                string storeName = await _dbService.GetStoreNameAsync(_configService.Config.DefaultStoreNum, _configService.Config.FallbackStoreName);
                var records = await _dbService.GetCountSheetDataAsync(locatorNo);

                if (records.Count == 0)
                {
                    CustomMessageBox.Show($"No count records found for Locator Number: {locatorNo}", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    continue;
                }

                _printService.BackupToTextFile(locatorNo, records);
                await _printService.PrintLocatorSheetAsync(locatorNo, storeName, records);
            }

            LocatorInput = string.Empty;
        }

        private void ShowAppInfo()
        {
            CustomMessageBox.Show(
                "Version History:\n\n" +
                "v1.0\n• Initial release\n\n" +
                "v2.1\n• Added auto-refresh for real-time progress tracking\n• Added location-based progress cards\n• Added remaining locators counter\n\n" +
                "v3.0\n• Complete system modernization\n• Added Edit Count Sheet module\n• Added comprehensive Reports (INF PDF Export, SKU Search, Stock Value, Summary)\n• Added User Management & Session Tracking\n• Added Locator Maintenance & Database Restore features\n• Live database connection monitoring\n\n" +
                "Developed by Jake Panlilio - IT SF1 (722)\nZone 11 © 2026",
                "About");
        }
    }
}