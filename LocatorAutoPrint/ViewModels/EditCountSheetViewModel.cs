using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers; // Changed to use your CustomMessageBox
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

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

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

        // NEW: Track if we are inserting a new record instead of updating an old one
        private bool _isAddMode;
        public bool IsAddMode
        {
            get => _isAddMode;
            set { _isAddMode = value; OnPropertyChanged(); }
        }

        public bool IsViewMode => !_isEditMode && CurrentRecord != null;

        public ICommand LoadCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SearchItemCommand { get; }
        public ICommand PrintEditedCommand { get; }

        // NEW: Add Record Command
        public ICommand AddRecordCommand { get; }

        public EditCountSheetViewModel(EditCountSheetService service, DatabaseService dbService, PrintService printService, ConfigService configService)
        {
            _service = service;
            _dbService = dbService;
            _printService = printService;
            _configService = configService;

            LoadCommand = new RelayCommand(async _ => await LoadRecordAsync(), _ => !string.IsNullOrEmpty(SearchLocator) && SearchRecNo.HasValue && !IsProcessing);
            EditCommand = new RelayCommand(_ => IsEditMode = true, _ => CurrentRecord != null && !IsEditMode && !IsProcessing);
            CancelCommand = new RelayCommand(async _ => { IsEditMode = false; IsAddMode = false; await LoadRecordAsync(); }, _ => !IsProcessing);

            // Locked Commands
            SaveCommand = new RelayCommand(async _ => await SaveRecordAsync(), _ => IsEditMode && CurrentRecord != null && !IsProcessing);
            SearchItemCommand = new RelayCommand(async param => await ExecuteItemSearchAsync(param as string), _ => IsEditMode && !IsProcessing);
            PrintEditedCommand = new RelayCommand(async _ => await ExecutePrintEditedAsync(), _ => !string.IsNullOrWhiteSpace(SearchLocator) && !IsProcessing);
            AddRecordCommand = new RelayCommand(async _ => await AddNewRecordAsync(), _ => !string.IsNullOrWhiteSpace(SearchLocator) && !IsEditMode && !IsProcessing);
        }

        // NEW: Generates the blank record with an auto-incremented RecNo
        private async System.Threading.Tasks.Task AddNewRecordAsync()
        {
            int nextRecNo = await _service.GetNextRecordNumberAsync(SearchLocator);

            CurrentRecord = new CountSheetEditModel
            {
                SlotNo = SearchLocator,
                RecNo = nextRecNo,
                UPC = string.Empty,
                SKU = string.Empty,
                Descr = string.Empty,
                OriginalQty = 0,
                EditedQty = 0
            };

            IsAddMode = true;
            IsEditMode = true;

            // Clear out the search box so it visually matches the new RecNo
            _searchRecNo = nextRecNo;
            OnPropertyChanged(nameof(SearchRecNo));
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
            IsAddMode = false;
        }

        private async System.Threading.Tasks.Task SaveRecordAsync()
        {
            if (IsProcessing) return;
            IsProcessing = true;
            try
            {
                bool success;
                if (IsAddMode) success = await _service.InsertRecordAsync(CurrentRecord);
                else success = await _service.UpdateRecordAsync(CurrentRecord);

                if (success)
                {
                    IsEditMode = false;
                    IsAddMode = false;
                    CurrentRecord = null;
                    SearchRecNo = null;
                }
                else
                {
                    CustomMessageBox.Show("Failed to save changes. Record may have been deleted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally { IsProcessing = false; }
        }

        private async System.Threading.Tasks.Task ExecuteItemSearchAsync(string triggerType = null)
        {
            if (IsProcessing) return;

            bool isAuto = triggerType == "Auto";

            string keyword = CurrentRecord?.UPC;
            if (string.IsNullOrWhiteSpace(keyword)) keyword = CurrentRecord?.SKU;

            if (string.IsNullOrWhiteSpace(keyword))
            {
               
                if (!isAuto) CustomMessageBox.Show("Please enter a UPC or SKU to search.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsProcessing = true;
            try
            {
                var results = await _service.SearchItemAsync(keyword);

                if (results.Count >= 1)
                {
                    CurrentRecord.UPC = results.First().UPC;
                    CurrentRecord.SKU = results.First().SKU;
                    CurrentRecord.Descr = results.First().Description;

                    
                    OnPropertyChanged(nameof(CurrentRecord));
                }
                else
                {
                    
                    if (!isAuto) CustomMessageBox.Show("Item not found in Masterfile.", "Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally { IsProcessing = false; }
        }

        private async System.Threading.Tasks.Task ExecutePrintEditedAsync()
        {
            if (IsProcessing) return;
            IsProcessing = true;
            try
            {
                if (!int.TryParse(SearchLocator, out int locNo))
                {
                    CustomMessageBox.Show("Locator must be a valid number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var summary = await _service.GetEditedRecordsSummaryAsync(SearchLocator);
                if (summary.EditedRecords.Count == 0)
                {
                    CustomMessageBox.Show($"No edited records found for Locator {SearchLocator}.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string storeName = await _dbService.GetStoreNameAsync(_configService.Config.DefaultStoreNum, _configService.Config.FallbackStoreName);
                await _printService.PrintEditedLocatorSheetAsync(locNo, storeName, summary);
            }
            finally { IsProcessing = false; }
        }
    }
}