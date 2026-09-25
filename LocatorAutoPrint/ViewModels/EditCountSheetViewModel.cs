using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
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
        public string SearchLocator
        {
            get => _searchLocator;
            set
            {
                if (_searchLocator != value)
                {
                    _searchLocator = value;
                    OnPropertyChanged();
                    CurrentRecord = null;
                    IsEditMode = false;
                    IsAddMode = false;
                    if (SearchRecNo.HasValue && !string.IsNullOrWhiteSpace(_searchLocator))
                    {
                        _ = LoadRecordAsync();
                    }
                }
            }
        }

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
                        IsEditMode = false;
                        IsAddMode = false;
                    }
                }
            }
        }

        private CountSheetEditModel _currentRecord;
        public CountSheetEditModel CurrentRecord
        {
            get => _currentRecord;
            set
            {
                _currentRecord = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsViewMode));
            }
        }

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
        public ICommand AddRecordCommand { get; }

        public EditCountSheetViewModel(EditCountSheetService service, DatabaseService dbService, PrintService printService, ConfigService configService)
        {
            _service = service;
            _dbService = dbService;
            _printService = printService;
            _configService = configService;

            LoadCommand = new RelayCommand(async _ => await LoadRecordAsync(), _ => !string.IsNullOrEmpty(SearchLocator) && SearchRecNo.HasValue && !IsProcessing);
            EditCommand = new RelayCommand(_ => IsEditMode = true, _ => CurrentRecord != null && !IsEditMode && !IsProcessing);
            CancelCommand = new RelayCommand(async _ => await HandleCancelAsync(), _ => !IsProcessing);
            SaveCommand = new RelayCommand(async _ => await SaveRecordAsync(), _ => IsEditMode && CurrentRecord != null && !IsProcessing);
            SearchItemCommand = new RelayCommand(async param => await ExecuteItemSearchAsync(param as string), _ => IsEditMode && !IsProcessing);
            PrintEditedCommand = new RelayCommand(async _ => await ExecutePrintEditedAsync(), _ => !string.IsNullOrWhiteSpace(SearchLocator) && !IsProcessing);
            AddRecordCommand = new RelayCommand(async _ => await AddNewRecordAsync(), _ => !string.IsNullOrWhiteSpace(SearchLocator) && !IsEditMode && !IsProcessing);
        }

        private async Task HandleCancelAsync()
        {
            if (IsAddMode)
            {
                IsAddMode = false;
                IsEditMode = false;
                CurrentRecord = null;
                SearchRecNo = null;
            }
            else
            {
                IsEditMode = false;
                await LoadRecordAsync();
            }
        }

        private async Task AddNewRecordAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchLocator))
            {
                CustomMessageBox.Show("Please enter a Locator number first.", "Locator Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int nextRecNo = await _service.GetNextRecordNumberAsync(SearchLocator.Trim());

                CurrentRecord = new CountSheetEditModel
                {
                    SlotNo = SearchLocator.Trim(),
                    RecNo = nextRecNo,
                    UPC = string.Empty,
                    SKU = string.Empty,
                    Descr = string.Empty,
                    OriginalQty = 0,
                    EditedQty = 0
                };

                IsAddMode = true;
                IsEditMode = true;
                _searchRecNo = nextRecNo;
                OnPropertyChanged(nameof(SearchRecNo));
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("EditCountSheetViewModel.AddNewRecordAsync", ex);
                CustomMessageBox.Show($"Failed to generate next record: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadRecordAsync()
        {
            if (string.IsNullOrEmpty(SearchLocator) || !SearchRecNo.HasValue) return;

            try
            {
                var record = await _service.GetRecordAsync(SearchLocator.Trim(), SearchRecNo.Value);
                if (record == null)
                {
                    CurrentRecord = null;
                    return;
                }

                CurrentRecord = record;
                IsEditMode = false;
                IsAddMode = false;
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("EditCountSheetViewModel.LoadRecordAsync", ex);
                CustomMessageBox.Show($"Failed to load record: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SaveRecordAsync()
        {
            if (IsProcessing || CurrentRecord == null) return;

            // Defensive validations
            if (double.IsNaN(CurrentRecord.EditedQty) || double.IsInfinity(CurrentRecord.EditedQty) || CurrentRecord.EditedQty < 0)
            {
                CustomMessageBox.Show("Quantity must be a valid non-negative number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentRecord.UPC) && string.IsNullOrWhiteSpace(CurrentRecord.SKU) && string.IsNullOrWhiteSpace(CurrentRecord.Descr))
            {
                CustomMessageBox.Show("Record must have at least a UPC, SKU, or Description.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
                    CustomMessageBox.Show("Record saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    CustomMessageBox.Show("Failed to save changes. Record may have been deleted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("EditCountSheetViewModel.SaveRecordAsync", ex);
                CustomMessageBox.Show($"Failed to save: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsProcessing = false; }
        }

        private async Task ExecuteItemSearchAsync(string triggerType = null)
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
                var results = await _service.SearchItemAsync(keyword.Trim());

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
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("EditCountSheetViewModel.ExecuteItemSearchAsync", ex);
                if (!isAuto) CustomMessageBox.Show($"Search failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsProcessing = false; }
        }

        private async Task ExecutePrintEditedAsync()
        {
            if (IsProcessing) return;

            if (string.IsNullOrWhiteSpace(SearchLocator))
            {
                CustomMessageBox.Show("Please enter a Locator number to print edited count sheet.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(SearchLocator.Trim(), out int locNo))
            {
                CustomMessageBox.Show("Locator must be a valid number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsProcessing = true;
            try
            {
                var summary = await _service.GetEditedRecordsSummaryAsync(SearchLocator.Trim());
                if (summary.EditedRecords.Count == 0)
                {
                    CustomMessageBox.Show($"No edited records found for Locator {SearchLocator.Trim()}.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string storeName = await _dbService.GetStoreNameAsync(_configService.Config?.DefaultStoreNum, _configService.Config?.FallbackStoreName ?? "PUREGOLD");
                await _printService.PrintEditedLocatorSheetAsync(locNo, storeName, summary);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("EditCountSheetViewModel.ExecutePrintEditedAsync", ex);
                CustomMessageBox.Show($"Print failed: {ex.Message}", "Print Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsProcessing = false; }
        }
    }
}
