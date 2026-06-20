using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;
using System;
using System.Collections.Generic;
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
        private bool _hasShownProgressError = false;

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

            if (newStats.HasError && !_hasShownProgressError)
            {
                _hasShownProgressError = true;
                CustomMessageBox.Show($"Live Progress failed to update due to a database error:\n\n{newStats.ErrorMessage}", "Progress Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else if (!newStats.HasError)
            {
                _hasShownProgressError = false;
            }

            if (!newStats.HasError)
            {
                Stats = newStats;
            }
        }

        private async System.Threading.Tasks.Task ExecutePrintAsync()
        {
            var locators = LocatorParser.Parse(LocatorInput);
            if (locators.Count == 0) return;
            var errorLog = new List<string>();
            int successCount = 0;
            string storeName = await _dbService.GetStoreNameAsync(_configService.Config.DefaultStoreNum, _configService.Config.FallbackStoreName);

            foreach (var locatorNo in locators)
            {
                try
                {
                    var status = await _dbService.CheckLocatorStatusAsync(locatorNo);
                    if (!status.Exists)
                    {
                        errorLog.Add($"Locator {locatorNo}: Does not exist in database.");
                        continue;
                    }
                    if (!status.IsClosed)
                    {
                        errorLog.Add($"Locator {locatorNo}: Currently OPEN.");
                        continue;
                    }

                    var records = await _dbService.GetCountSheetDataAsync(locatorNo);

                    if (records.Count == 0)
                    {
                        errorLog.Add($"Locator {locatorNo}: No count records found.");
                        continue;
                    }

                    _printService.BackupToTextFile(locatorNo, records);
                    await _printService.PrintLocatorSheetAsync(locatorNo, storeName, records);

                    successCount++;
                }
                catch (Exception ex)
                {
                    errorLog.Add($"Locator {locatorNo}: ERROR - {ex.Message}");
                }
            }

            LocatorInput = string.Empty;

            if (errorLog.Count > 0)
            {
                string summary = $"Print job completed with {errorLog.Count} issue(s). Successful prints: {successCount}\n\n";

               
                if (errorLog.Count > 15)
                {
                    summary += string.Join("\n", errorLog.GetRange(0, 15)) + $"\n...and {errorLog.Count - 15} more.";
                }
                else
                {
                    summary += string.Join("\n", errorLog);
                }

                CustomMessageBox.Show(summary, "Print Job Summary", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (successCount > 0)
            {
                CustomMessageBox.Show($"Successfully queued {successCount} locator(s) for printing.", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowAppInfo()
        {
            CustomMessageBox.Show(
                "Version History:\n\n" +
                "v1.0\n• Initial release\n\n" +
                "v2.1\n• Added auto-refresh for real-time progress tracking\n• Added location-based progress cards\n• Added remaining locators counter\n\n" +
                "v3.0\n• Complete system modernization\n• Added Edit Count Sheet module\n• Added comprehensive Reports (INF PDF Export, SKU Search, Stock Value, Summary)\n• Added User Management & Session Tracking\n• Added Locator Maintenance & Database Restore features\n• Live database connection monitoring\n\n" +
                "v3.1\n• Corrected percentage accuracy\n• Fixed Enter key behavior for printing locators\n• Fixed masterfile lookups when editing locators\n• Added batch print validation and error logging\n• Added status column on users tab that pings the device IP\n\n" +
                "Developed by Jake Panlilio - IT SF1 (722)\nZone 11 © 2026",
                "About");
        }
    }
}