using System;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LocatorAutoPrint.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private readonly ReportsService _reportsService;
        private readonly PdfExportService _pdfService;
        private readonly PrintService _printService;
        private readonly StockValueService _stockService;

        private ObservableCollection<InfReportModel> _infRecords = new ObservableCollection<InfReportModel>();
        public ObservableCollection<InfReportModel> InfRecords { get => _infRecords; set { _infRecords = value; OnPropertyChanged(); } }

        private bool _infLoaded;
        public bool HasNoInfRecords => _infLoaded && InfRecords.Count == 0;
        public bool HasInfRecords => InfRecords.Count > 0;

        private ObservableCollection<StockValueModel> _stockValues = new ObservableCollection<StockValueModel>();
        public ObservableCollection<StockValueModel> StockValues { get => _stockValues; set { _stockValues = value; OnPropertyChanged(); } }

        private bool _stockLoaded;
        public bool HasNoStockData => _stockLoaded && StockValues.Count == 0;

        public ICommand LoadStockCommand { get; }
        public ICommand LoadInfCommand { get; }
        public ICommand ExportInfToPdfCommand { get; }
        public ICommand PrintInfCommand { get; }

        private ObservableCollection<ItemLookupResult> _skuResults = new ObservableCollection<ItemLookupResult>();
        public ObservableCollection<ItemLookupResult> SkuResults { get => _skuResults; set { _skuResults = value; OnPropertyChanged(); } }

        private string _skuSearchQuery;
        public string SkuSearchQuery { get => _skuSearchQuery; set { _skuSearchQuery = value; OnPropertyChanged(); } }
        public bool HasNoSkuResults => SkuResults.Count == 0;

        private ItemLookupResult _selectedSkuResult;
        public ItemLookupResult SelectedSkuResult { get => _selectedSkuResult; set { _selectedSkuResult = value; OnPropertyChanged(); } }

        public ICommand AddToMasterfileCommand { get; }
        public ICommand SearchSkuCommand { get; }
        public ICommand CopyUpcCommand { get; }
        public ICommand CopySkuCommand { get; }
        public ICommand CopyDescriptionCommand { get; }
        public ICommand CopyAllColumnsCommand { get; }

        private ObservableCollection<SummaryReportModel> _summaryRecords = new ObservableCollection<SummaryReportModel>();
        public ObservableCollection<SummaryReportModel> SummaryRecords { get => _summaryRecords; set { _summaryRecords = value; OnPropertyChanged(); } }
        public ICommand LoadSummaryCommand { get; }

        private MonitoringKpiModel _monitoringKpis;
        public MonitoringKpiModel MonitoringKpis { get => _monitoringKpis; set { _monitoringKpis = value; OnPropertyChanged(); } }

        public ObservableCollection<UnloadedLocatorModel> UnloadedLocators { get; } = new ObservableCollection<UnloadedLocatorModel>();
        public ObservableCollection<LocatorLocationModel> LocatorLocations { get; } = new ObservableCollection<LocatorLocationModel>();
        public ICommand LoadMonitoringCommand { get; }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _showCopyFeedback;
        public bool ShowCopyFeedback
        {
            get => _showCopyFeedback;
            set { _showCopyFeedback = value; OnPropertyChanged(); }
        }

        private string _copyFeedbackMessage;
        public string CopyFeedbackMessage
        {
            get => _copyFeedbackMessage;
            set { _copyFeedbackMessage = value; OnPropertyChanged(); }
        }

        public ReportsViewModel(ReportsService reportsService, PdfExportService pdfService, StockValueService stockService, PrintService printService)
        {
            _reportsService = reportsService;
            _pdfService = pdfService;
            _stockService = stockService;
            _printService = printService;

            LoadInfCommand = new RelayCommand(async _ => await LoadInfRecordsAsync(), _ => !IsLoading);
            PrintInfCommand = new RelayCommand(async _ => await PrintInfAsync(), _ => HasInfRecords && !IsLoading);
            ExportInfToPdfCommand = new RelayCommand(_ => ExportInfToPdf(), _ => HasInfRecords && !IsLoading);
            LoadSummaryCommand = new RelayCommand(async _ => await LoadSummaryAsync(), _ => !IsLoading);
            LoadMonitoringCommand = new RelayCommand(async _ => await LoadMonitoringAsync(), _ => !IsLoading);
            LoadStockCommand = new RelayCommand(async _ => await LoadStockAsync(), _ => !IsLoading);
            SearchSkuCommand = new RelayCommand(async _ => await ExecuteSkuSearchAsync(), _ => !string.IsNullOrWhiteSpace(SkuSearchQuery) && !IsLoading);
            AddToMasterfileCommand = new RelayCommand(async _ => await AddToMasterfileAsync(), _ => SelectedSkuResult != null && !IsLoading);
            CopyUpcCommand = new RelayCommand(async _ => await CopyToClipboard("UPC"), _ => SelectedSkuResult != null);
            CopySkuCommand = new RelayCommand(async _ => await CopyToClipboard("SKU"), _ => SelectedSkuResult != null);
            CopyDescriptionCommand = new RelayCommand(async _ => await CopyToClipboard("Description"), _ => SelectedSkuResult != null);
            CopyAllColumnsCommand = new RelayCommand(async _ => await CopyAllToClipboard(), _ => SelectedSkuResult != null);
        }

        private async Task PrintInfAsync()
        {
            if (IsLoading || !HasInfRecords) return;

            IsLoading = true;
            try
            {
                await _printService.PrintInfReportAsync(InfRecords.ToList());
                CustomMessageBox.Show("INF Report sent to printer successfully.", "Print Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.PrintInfAsync", ex, isTerminating: false);
                CustomMessageBox.Show($"Failed to print INF report: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ShowCopyFeedbackMessage(string message)
        {
            CopyFeedbackMessage = message;
            ShowCopyFeedback = true;
            await Task.Delay(2000);
            ShowCopyFeedback = false;
        }

        private async Task CopyToClipboard(string fieldName)
        {
            var item = SelectedSkuResult;
            if (item == null) return;

            string textToCopy = string.Empty;

            switch (fieldName.ToLower())
            {
                case "upc": textToCopy = item.UPC; break;
                case "sku": textToCopy = item.SKU; break;
                case "description": textToCopy = item.Description; break;
            }

            if (!string.IsNullOrEmpty(textToCopy))
            {
                try
                {
                    Clipboard.SetText(textToCopy);
                    await ShowCopyFeedbackMessage($"Copied {fieldName} to clipboard!");
                }
                catch (Exception ex)
                {
                    ErrorLoggerService.LogException("ReportsViewModel.CopyToClipboard", ex);
                }
            }
        }

        private async Task CopyAllToClipboard()
        {
            var item = SelectedSkuResult;
            if (item == null) return;

            try
            {
                string allData = $"UPC: {item.UPC}\nSKU: {item.SKU}\nDescription: {item.Description}";
                Clipboard.SetText(allData);
                await ShowCopyFeedbackMessage("Copied all item data to clipboard!");
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.CopyAllToClipboard", ex);
            }
        }

        private async Task AddToMasterfileAsync()
        {
            if (SelectedSkuResult == null || IsLoading) return;

            IsLoading = true;
            try
            {
                var result = await _reportsService.AddToMasterfileAsync(SelectedSkuResult);

                if (result.Success)
                {
                    await ShowCopyFeedbackMessage(result.Message);
                }
                else
                {
                    CustomMessageBox.Show(result.Message, "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.AddToMasterfileAsync", ex);
                CustomMessageBox.Show($"Operation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadStockAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var results = await _stockService.GetStockValuesAsync();
                StockValues = new ObservableCollection<StockValueModel>(results); 
                _stockLoaded = true;
                OnPropertyChanged(nameof(HasNoStockData));
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.LoadStockAsync", ex);
                CustomMessageBox.Show($"Failed to load Stock Values: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private async Task LoadInfRecordsAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var results = await _reportsService.GetInfRecordsAsync();
                InfRecords = new ObservableCollection<InfReportModel>(results);

                _infLoaded = true;
                OnPropertyChanged(nameof(HasNoInfRecords));
                OnPropertyChanged(nameof(HasInfRecords));
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.LoadInfRecordsAsync", ex);
                CustomMessageBox.Show($"Failed to load INF records: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private void ExportInfToPdf()
        {
            if (InfRecords.Count == 0)
            {
                CustomMessageBox.Show("No INF records available to export.", "Export Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sfd = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = $"INF_Report_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    _pdfService.ExportInfReportToPdf(InfRecords.ToList(), sfd.FileName);
                    CustomMessageBox.Show("PDF Exported Successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    ErrorLoggerService.LogException("ReportsViewModel.ExportInfToPdf", ex);
                    CustomMessageBox.Show($"Failed to export PDF: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task ExecuteSkuSearchAsync()
        {
            if (IsLoading) return;

            if (string.IsNullOrWhiteSpace(SkuSearchQuery))
            {
                CustomMessageBox.Show("Please enter a SKU, UPC, or Description keyword to search.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsLoading = true;
            try
            {
                var results = await _reportsService.SearchSkuAsync(SkuSearchQuery.Trim());
                SkuResults = new ObservableCollection<ItemLookupResult>(results);
                OnPropertyChanged(nameof(HasNoSkuResults));
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.ExecuteSkuSearchAsync", ex);
                CustomMessageBox.Show($"Search failed: {ex.Message}", "Search Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private async Task LoadSummaryAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                var results = await _reportsService.GetSummaryReportAsync();
                SummaryRecords = new ObservableCollection<SummaryReportModel>(results); 
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.LoadSummaryAsync", ex);
                CustomMessageBox.Show($"Failed to load Summary Report: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsLoading = false; }
        }

        private async Task LoadMonitoringAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                MonitoringKpis = await _reportsService.GetMonitoringKpisAsync();

                var unloads = await _reportsService.GetUnloadedLocatorsAsync();
                UnloadedLocators.Clear();
                foreach (var u in unloads) UnloadedLocators.Add(u);

                var locs = await _reportsService.GetLocatorLocationsAsync();
                LocatorLocations.Clear();
                foreach (var l in locs) LocatorLocations.Add(l);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("ReportsViewModel.LoadMonitoringAsync", ex);
                CustomMessageBox.Show($"Failed to load monitoring data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}