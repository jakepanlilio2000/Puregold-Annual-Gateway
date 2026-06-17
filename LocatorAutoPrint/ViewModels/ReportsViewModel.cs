using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Microsoft.Win32; 
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;

namespace LocatorAutoPrint.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private readonly ReportsService _reportsService;
        private readonly PdfExportService _pdfService;
        private readonly StockValueService _stockService;
        public ObservableCollection<InfReportModel> InfRecords { get; } = new ObservableCollection<InfReportModel>();
        private bool _infLoaded;
        public bool HasNoInfRecords => _infLoaded && InfRecords.Count == 0;
        public bool HasInfRecords => InfRecords.Count > 0;
        public ObservableCollection<StockValueModel> StockValues { get; } = new ObservableCollection<StockValueModel>();
        private bool _stockLoaded;
        public bool HasNoStockData => _stockLoaded && StockValues.Count == 0;

        public ICommand LoadStockCommand { get; }
        public ICommand LoadInfCommand { get; }
        public ICommand ExportInfToPdfCommand { get; }

        public ObservableCollection<ItemLookupResult> SkuResults { get; } = new ObservableCollection<ItemLookupResult>();
        private string _skuSearchQuery;
        public string SkuSearchQuery { get => _skuSearchQuery; set { _skuSearchQuery = value; OnPropertyChanged(); } }
        public bool HasNoSkuResults => SkuResults.Count == 0;
        public ICommand SearchSkuCommand { get; }
        public ICommand CopyUpcCommand { get; }
        public ICommand CopySkuCommand { get; }
        public ICommand CopyDescriptionCommand { get; }
        public ICommand CopyAllColumnsCommand { get; }

        public ObservableCollection<SummaryReportModel> SummaryRecords { get; } = new ObservableCollection<SummaryReportModel>();
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
            }
        }

        public ReportsViewModel(ReportsService reportsService, PdfExportService pdfService, StockValueService stockService)
        {
            _reportsService = reportsService;
            _pdfService = pdfService;
            _stockService = stockService;

            LoadInfCommand = new RelayCommand(async _ => await LoadInfRecordsAsync());
            ExportInfToPdfCommand = new RelayCommand(_ => ExportInfToPdf(), _ => HasInfRecords);

            SearchSkuCommand = new RelayCommand(async _ => await ExecuteSkuSearchAsync(), _ => !string.IsNullOrWhiteSpace(SkuSearchQuery));
            LoadSummaryCommand = new RelayCommand(async _ => await LoadSummaryAsync());
            LoadMonitoringCommand = new RelayCommand(async _ => await LoadMonitoringAsync());
            LoadStockCommand = new RelayCommand(async _ => await LoadStockAsync());
            CopyUpcCommand = new RelayCommand(_ => CopyToClipboard("UPC"), _ => SkuResults.Count > 0 && SkuResults.FirstOrDefault() != null);
            CopySkuCommand = new RelayCommand(_ => CopyToClipboard("SKU"), _ => SkuResults.Count > 0);
            CopyDescriptionCommand = new RelayCommand(_ => CopyToClipboard("Description"), _ => SkuResults.Count > 0);
            CopyAllColumnsCommand = new RelayCommand(_ => CopyAllToClipboard(), _ => SkuResults.Count > 0);
        }

        private void CopyToClipboard(string fieldName)
        {
            if (SkuResults.Count == 0) return;

            var item = SkuResults.FirstOrDefault();
            if (item == null) return;

            string textToCopy = string.Empty;

            switch (fieldName.ToLower())
            {
                case "upc":
                    textToCopy = item.UPC;
                    break;
                case "sku":
                    textToCopy = item.SKU;
                    break;
                case "description":
                    textToCopy = item.Description;
                    break;
            }

            if (!string.IsNullOrEmpty(textToCopy))
            {
                System.Windows.Clipboard.SetText(textToCopy);
                ShowCopyFeedbackMessage($"Copied {fieldName} to clipboard!");
            }
        }

        private void CopyAllToClipboard()
        {
            if (SkuResults.Count == 0) return;

            var item = SkuResults.FirstOrDefault();
            if (item == null) return;

            string allData = $"UPC: {item.UPC}\nSKU: {item.SKU}\nDescription: {item.Description}";
            System.Windows.Clipboard.SetText(allData);
            ShowCopyFeedbackMessage("Copied all data to clipboard!");
        }

        private async void ShowCopyFeedbackMessage(string message)
        {
            CopyFeedbackMessage = message;
            ShowCopyFeedback = true;
            await System.Threading.Tasks.Task.Delay(2000);
            ShowCopyFeedback = false;
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
        

        private async System.Threading.Tasks.Task LoadStockAsync()
        {
            IsLoading = true;
            try
            {
                StockValues.Clear();
                var results = await _stockService.GetStockValuesAsync();
                foreach (var r in results) StockValues.Add(r);
                _stockLoaded = true;
                OnPropertyChanged(nameof(HasNoStockData));
            }
            finally
            {
                IsLoading = false; 
            }
        }

        private async System.Threading.Tasks.Task LoadInfRecordsAsync()
        {
            IsLoading = true;
            try
            {
                InfRecords.Clear();
                var results = await _reportsService.GetInfRecordsAsync();
                foreach (var r in results) InfRecords.Add(r);
                _infLoaded = true;
                OnPropertyChanged(nameof(HasNoInfRecords));
                OnPropertyChanged(nameof(HasInfRecords));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ExportInfToPdf()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf",
                FileName = "INF_Report.pdf"
            };

            if (sfd.ShowDialog() == true)
            {
                _pdfService.ExportInfReportToPdf(InfRecords.ToList(), sfd.FileName);
                System.Windows.MessageBox.Show("PDF Exported Successfully.", "Export Complete");
            }
        }

        private async System.Threading.Tasks.Task ExecuteSkuSearchAsync()
        {
            IsLoading = true;
            try
            {
                SkuResults.Clear();
                var results = await _reportsService.SearchSkuAsync(SkuSearchQuery);
                foreach (var r in results) SkuResults.Add(r);
                OnPropertyChanged(nameof(HasNoSkuResults));
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async System.Threading.Tasks.Task LoadSummaryAsync()
        {
            IsLoading = true;
            try
            {
                SummaryRecords.Clear();
                var results = await _reportsService.GetSummaryReportAsync();
                foreach (var r in results) SummaryRecords.Add(r);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async System.Threading.Tasks.Task LoadMonitoringAsync()
        {
            IsLoading = true;
            try
            {
                MonitoringKpis = await _reportsService.GetMonitoringKpisAsync();

                UnloadedLocators.Clear();
                var unloads = await _reportsService.GetUnloadedLocatorsAsync();
                foreach (var u in unloads) UnloadedLocators.Add(u);

                LocatorLocations.Clear();
                var locs = await _reportsService.GetLocatorLocationsAsync();
                foreach (var l in locs) LocatorLocations.Add(l);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}