using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;

namespace LocatorAutoPrint.ViewModels
{
    public class EditCountSheetViewModel : ViewModelBase
    {
        private readonly EditCountSheetService _service;
        private readonly DatabaseService _dbService;
        private readonly PrintService _printService;
        private readonly ConfigService _configService;
        private string _searchLocator;
        public string SearchLocator { get => _searchLocator; set { _searchLocator = value; OnPropertyChanged(); } }

        private int? _searchRecNo;
        public int? SearchRecNo
        {
            get => _searchRecNo;
            set
            {
                if (_searchRecNo != value)
                {
                    _searchRecNo = value;
                    OnPropertyChanged();
                    if (_searchRecNo.HasValue && !string.IsNullOrWhiteSpace(SearchLocator))
                    {
                        _ = LoadRecordAsync();
                    }
                    else
                    {
                        CurrentRecord = null;
                    }
                }
            }
        }
        private CountSheetEditModel _currentRecord;
        public CountSheetEditModel CurrentRecord { get => _currentRecord; set { _currentRecord = value; OnPropertyChanged(); } }
        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                _isEditMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsViewMode));
            }
        }
        public bool IsViewMode => !_isEditMode && CurrentRecord != null;

        public ICommand LoadCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SearchItemCommand { get; }
        public ICommand PrintEditedCommand { get; }

        public EditCountSheetViewModel(EditCountSheetService service, DatabaseService dbService, PrintService printService, ConfigService configService)
        {
            _service = service;
            _dbService = dbService;
            _printService = printService;
            _configService = configService;

            LoadCommand = new RelayCommand(async _ => await LoadRecordAsync(), _ => !string.IsNullOrEmpty(SearchLocator) && SearchRecNo.HasValue);
            EditCommand = new RelayCommand(_ => IsEditMode = true, _ => CurrentRecord != null && !IsEditMode);
            CancelCommand = new RelayCommand(async _ => { IsEditMode = false; await LoadRecordAsync(); });
            SaveCommand = new RelayCommand(async _ => await SaveRecordAsync(), _ => IsEditMode && CurrentRecord != null);
            SearchItemCommand = new RelayCommand(async _ => await ExecuteItemSearchAsync(), _ => IsEditMode);

            PrintEditedCommand = new RelayCommand(async _ => await ExecutePrintEditedAsync(), _ => !string.IsNullOrWhiteSpace(SearchLocator));
        }

        private async System.Threading.Tasks.Task LoadRecordAsync()
        {
            if (string.IsNullOrEmpty(SearchLocator) || !SearchRecNo.HasValue) return;

            var record = await _service.GetRecordAsync(SearchLocator, SearchRecNo.Value);
            if (record == null)
            {
                CurrentRecord = null;
                return;
            }

            CurrentRecord = record;
            IsEditMode = false;
        }

        private async System.Threading.Tasks.Task SaveRecordAsync()
        {
            bool success = await _service.UpdateRecordAsync(CurrentRecord);
            if (success)
            {
                MessageBox.Show("Count sheet updated successfully.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                IsEditMode = false;
                await LoadRecordAsync();
            }
            else
            {
                MessageBox.Show("Failed to save changes. Record may have been deleted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task ExecuteItemSearchAsync()
        {
            string keyword = CurrentRecord.UPC;
            if (string.IsNullOrWhiteSpace(keyword)) keyword = CurrentRecord.SKU;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                MessageBox.Show("Please enter a UPC or SKU to search.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var results = await _service.SearchItemAsync(keyword);

            if (results.Count >= 1)
            {
                CurrentRecord.UPC = results.First().UPC;
                CurrentRecord.SKU = results.First().SKU;
                CurrentRecord.Descr = results.First().Description;

                if (results.Count > 1) MessageBox.Show("Multiple items found. Auto-filled with the first match.", "Multiple Matches");
            }
            else
            {
                MessageBox.Show("Item not found in Masterfile.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        private async System.Threading.Tasks.Task ExecutePrintEditedAsync()
        {
            if (!int.TryParse(SearchLocator, out int locNo))
            {
                MessageBox.Show("Locator must be a valid number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var summary = await _service.GetEditedRecordsSummaryAsync(SearchLocator);

            if (summary.EditedRecords.Count == 0)
            {
                MessageBox.Show($"No edited records found for Locator {SearchLocator}.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string storeName = await _dbService.GetStoreNameAsync(_configService.Config.DefaultStoreNum, _configService.Config.FallbackStoreName);

            await _printService.PrintEditedLocatorSheetAsync(locNo, storeName, summary);
        }
    }
}