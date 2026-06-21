using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;
using Microsoft.Win32; 
using System.Collections.Generic;
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
            }
        }

        public ReportsViewModel(ReportsService reportsService, PdfExportService pdfService, StockValueService stockService)
        {
            _reportsService = reportsService;
            _pdfService = pdfService;
            _stockService = stockService;

            LoadInfCommand = new RelayCommand(async _ => await LoadInfRecordsAsync());
            ExportInfToPdfCommand = new RelayCommand(_ => ExportInfToPdf(), _ => HasInfRecords);
            LoadSummaryCommand = new RelayCommand(async _ => await LoadSummaryAsync());
            LoadMonitoringCommand = new RelayCommand(async _ => await LoadMonitoringAsync());
            LoadStockCommand = new RelayCommand(async _ => await LoadStockAsync());
            SearchSkuCommand = new RelayCommand(async _ => await ExecuteSkuSearchAsync(), _ => !string.IsNullOrWhiteSpace(SkuSearchQuery));
            AddToMasterfileCommand = new RelayCommand(async _ => await AddToMasterfileAsync(), _ => SelectedSkuResult != null);
            CopyUpcCommand = new RelayCommand(_ => CopyToClipboard("UPC"), _ => SelectedSkuResult != null);
            CopySkuCommand = new RelayCommand(_ => CopyToClipboard("SKU"), _ => SelectedSkuResult != null);
            CopyDescriptionCommand = new RelayCommand(_ => CopyToClipboard("Description"), _ => SelectedSkuResult != null);
            CopyAllColumnsCommand = new RelayCommand(_ => CopyAllToClipboard(), _ => SelectedSkuResult != null);
        }

        private void CopyToClipboard(string fieldName)
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
                System.Windows.Clipboard.SetText(textToCopy);
                ShowCopyFeedbackMessage($"Copied {fieldName} to clipboard!");
            }
        }

        private async Task AddToMasterfileAsync()
        {
            IsLoading = true;
            try
            {
                var result = await _reportsService.AddToMasterfileAsync(SelectedSkuResult);

                if (result.Success)
                {
                    ShowCopyFeedbackMessage(result.Message);
                }
                else
                {
                    MessageBox.Show(result.Message, "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
        private void CopyAllToClipboard()
        {
            var item = SelectedSkuResult;
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


        private async Task LoadStockAsync()
        {
            IsLoading = true;
            try
            {
                var results = await _stockService.GetStockValuesAsync();
                StockValues = new ObservableCollection<StockValueModel>(results); 

                _stockLoaded = true;
                OnPropertyChanged(nameof(HasNoStockData));
            }
            finally { IsLoading = false; }
        }

        private async System.Threading.Tasks.Task LoadInfRecordsAsync()
        {
            IsLoading = true;
            try
            {
                var results = await _reportsService.GetInfRecordsAsync();
                InfRecords = new ObservableCollection<InfReportModel>(results);

                _infLoaded = true;
                OnPropertyChanged(nameof(HasNoInfRecords));
                OnPropertyChanged(nameof(HasInfRecords));
            }
            finally { IsLoading = false; }
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

        private async Task ExecuteSkuSearchAsync()
        {
            IsLoading = true;
            try
            {
                var results = await _reportsService.SearchSkuAsync(SkuSearchQuery);
                SkuResults = new ObservableCollection<ItemLookupResult>(results);

                OnPropertyChanged(nameof(HasNoSkuResults));
            }
            finally { IsLoading = false; }
        }

        private async Task LoadSummaryAsync()
        {
            IsLoading = true;
            try
            {
                var results = await _reportsService.GetSummaryReportAsync();
                SummaryRecords = new ObservableCollection<SummaryReportModel>(results); 
            }
            finally { IsLoading = false; }
        }

        private async Task LoadMonitoringAsync()
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