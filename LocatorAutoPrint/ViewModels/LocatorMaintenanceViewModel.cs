using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;

namespace LocatorAutoPrint.ViewModels
{
    public class LocatorMaintenanceViewModel : ViewModelBase
    {
        private readonly LocatorMaintenanceService _service;
        private readonly ConfigService _configService;

        public ICollectionView AllLocatorsView { get; private set; }
        public ICollectionView ActiveLocatorsView { get; private set; }
        public ICollectionView UnusedLocatorsView { get; private set; }
        public ICollectionView InactiveLocatorsView { get; private set; }

        public ObservableCollection<string> LocationsList { get; } = new ObservableCollection<string>();
        public ObservableCollection<LocatorListModel> CountsheetList { get; } = new ObservableCollection<LocatorListModel>();
        public ObservableCollection<PrelocModel> PrelocList { get; } = new ObservableCollection<PrelocModel>();
        public ObservableCollection<CountsheetDetailModel> SelectedLocatorDetails { get; } = new ObservableCollection<CountsheetDetailModel>();

        private LocatorListModel _selectedCountsheetLocator;
        public LocatorListModel SelectedCountsheetLocator { get => _selectedCountsheetLocator; set { _selectedCountsheetLocator = value; OnPropertyChanged(); } }

        private string _selectedLocationFilter = "All Locations";
        public string SelectedLocationFilter
        {
            get => _selectedLocationFilter;
            set
            {
                _selectedLocationFilter = value;
                OnPropertyChanged();
                RefreshAllViews();
            }
        }

        private string _searchLocatorQuery;
        public string SearchLocatorQuery
        {
            get => _searchLocatorQuery;
            set
            {
                _searchLocatorQuery = value;
                OnPropertyChanged();
                AllLocatorsView?.Refresh();
                ActiveLocatorsView?.Refresh();
                UnusedLocatorsView?.Refresh();
                InactiveLocatorsView?.Refresh();
            }
        }
        private string _newSlotNo;
        public string NewSlotNo { get => _newSlotNo; set { _newSlotNo = value; OnPropertyChanged(); } }
        private string _newLocatorName;
        public string NewLocatorName { get => _newLocatorName; set { _newLocatorName = value; OnPropertyChanged(); } }

        private PrelocModel _selectedPreloc;
        public PrelocModel SelectedPreloc { get => _selectedPreloc; set { _selectedPreloc = value; OnPropertyChanged(); } }

        public ICommand LoadCountsheetListCommand { get; }
        public ICommand ViewCountsheetCommand { get; }
        public ICommand BackupCountsheetCommand { get; }
        public ICommand LoadPrelocListCommand { get; }
        public ICommand AddPrelocCommand { get; }
        public ICommand EditPrelocCommand { get; }
        public ICommand DeletePrelocCommand { get; }

        public LocatorMaintenanceViewModel(LocatorMaintenanceService service, ConfigService configService)
        {
            _service = service;
            _configService = configService;

            LoadCountsheetListCommand = new RelayCommand(async _ => await LoadCountsheetsAsync());
            ViewCountsheetCommand = new RelayCommand(async _ => await ViewCountsheetAsync(), _ => SelectedCountsheetLocator != null);
            BackupCountsheetCommand = new RelayCommand(async _ => await BackupCountsheetAsync(), _ => SelectedCountsheetLocator != null);

            LoadPrelocListCommand = new RelayCommand(async _ => await LoadPrelocsAsync());
            AddPrelocCommand = new RelayCommand(async _ => await AddPrelocAsync(), _ => !string.IsNullOrWhiteSpace(NewSlotNo) && !string.IsNullOrWhiteSpace(NewLocatorName));
            EditPrelocCommand = new RelayCommand(async _ => await EditPrelocAsync(), _ => SelectedPreloc != null && !string.IsNullOrWhiteSpace(SelectedPreloc.Name));
            DeletePrelocCommand = new RelayCommand(async _ => await DeletePrelocAsync(), _ => SelectedPreloc != null);
        }

        private void RefreshAllViews()
        {
            AllLocatorsView?.Refresh();
            ActiveLocatorsView?.Refresh();
            UnusedLocatorsView?.Refresh();
            InactiveLocatorsView?.Refresh();
        }

        private async Task LoadCountsheetsAsync()
        {
            try
            {
                foreach (var item in CountsheetList) item.PropertyChanged -= LocatorListModel_PropertyChanged;
                CountsheetList.Clear();

                var list = await _service.GetLocatorListAsync();

                var distinctLocations = list.Select(x => x.Location).Distinct().OrderBy(x => x).ToList();
                LocationsList.Clear();
                LocationsList.Add("All Locations");
                foreach (var loc in distinctLocations) LocationsList.Add(loc);
                _selectedLocationFilter = "All Locations";
                OnPropertyChanged(nameof(SelectedLocationFilter));

                foreach (var item in list)
                {
                    item.PropertyChanged += LocatorListModel_PropertyChanged;
                    CountsheetList.Add(item);
                }

                AllLocatorsView = new CollectionViewSource { Source = CountsheetList }.View;
                ActiveLocatorsView = new CollectionViewSource { Source = CountsheetList }.View;
                UnusedLocatorsView = new CollectionViewSource { Source = CountsheetList }.View;
                InactiveLocatorsView = new CollectionViewSource { Source = CountsheetList }.View;

                AllLocatorsView.Filter = CreateFilter(null);
                ActiveLocatorsView.Filter = CreateFilter("Active");
                UnusedLocatorsView.Filter = CreateFilter("Unused");
                InactiveLocatorsView.Filter = CreateFilter("Inactive");

                OnPropertyChanged(nameof(AllLocatorsView));
                OnPropertyChanged(nameof(ActiveLocatorsView));
                OnPropertyChanged(nameof(UnusedLocatorsView));
                OnPropertyChanged(nameof(InactiveLocatorsView));
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("LocatorMaintenanceViewModel.LoadCountsheetsAsync", ex);
                CustomMessageBox.Show($"Failed to load countsheets: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Predicate<object> CreateFilter(string targetStatus)
        {
            return obj =>
            {
                var loc = obj as LocatorListModel;
                if (loc == null) return false;

                if (targetStatus != null && loc.Status != targetStatus) return false;
                if (!string.IsNullOrEmpty(SelectedLocationFilter) && SelectedLocationFilter != "All Locations")
                {
                    if (loc.Location != SelectedLocationFilter) return false;
                }

                if (!string.IsNullOrWhiteSpace(SearchLocatorQuery))
                {
                    if (!loc.SlotNo.Equals(SearchLocatorQuery.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
                }

                return true;
            };
        }

        // CRITICAL FIX: Wrapped inside try/catch to avoid unhandled async void thread crashes
        private async void LocatorListModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is LocatorListModel locator && (e.PropertyName == nameof(locator.InUse) || e.PropertyName == nameof(locator.Closed)))
            {
                try
                {
                    await _service.UpdateLocatorToggleAsync(locator.SlotNo, locator.InUse, locator.Closed);
                }
                catch (Exception ex)
                {
                    ErrorLoggerService.LogException("LocatorMaintenanceViewModel.LocatorListModel_PropertyChanged", ex);
                    CustomMessageBox.Show($"Failed to update locator status: {ex.Message}", "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async Task LoadPrelocsAsync()
        {
            try
            {
                PrelocList.Clear();
                var list = await _service.GetPrelocListAsync();
                foreach (var p in list) PrelocList.Add(p);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("LocatorMaintenanceViewModel.LoadPrelocsAsync", ex);
            }
        }

        private async Task ViewCountsheetAsync()
        {
            try
            {
                SelectedLocatorDetails.Clear();
                var details = await _service.GetCountsheetDetailsAsync(SelectedCountsheetLocator.SlotNo);
                foreach (var d in details) SelectedLocatorDetails.Add(d);

                var win = new Views.Modals.CountsheetDetailsWindow { DataContext = this };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("LocatorMaintenanceViewModel.ViewCountsheetAsync", ex);
                CustomMessageBox.Show($"Failed to view countsheet: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task BackupCountsheetAsync()
        {
            try
            {
                var result = await _service.BackupLocatorToTxtAsync(SelectedCountsheetLocator.SlotNo, _configService.AppBaseDir);
                CustomMessageBox.Show(result.Message, result.Success ? "Backup Success" : "Backup Failed", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("LocatorMaintenanceViewModel.BackupCountsheetAsync", ex);
                CustomMessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task AddPrelocAsync()
        {
            try
            {
                var result = await _service.AddPrelocAsync(NewSlotNo, NewLocatorName);
                if (result.Success)
                {
                    NewSlotNo = string.Empty;
                    NewLocatorName = string.Empty;
                    await LoadPrelocsAsync();
                }
                else
                {
                    MessageBox.Show(result.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("LocatorMaintenanceViewModel.AddPrelocAsync", ex);
                MessageBox.Show($"Error adding preloc: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EditPrelocAsync()
        {
            try
            {
                await _service.UpdatePrelocAsync(SelectedPreloc.SlotNo, SelectedPreloc.Name);
                MessageBox.Show("Locator updated.", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("LocatorMaintenanceViewModel.EditPrelocAsync", ex);
                MessageBox.Show($"Error updating preloc: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeletePrelocAsync()
        {
            try
            {
                if (MessageBox.Show($"Are you sure you want to delete {SelectedPreloc.SlotNo}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await _service.DeletePrelocAsync(SelectedPreloc.SlotNo);
                    await LoadPrelocsAsync();
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("LocatorMaintenanceViewModel.DeletePrelocAsync", ex);
                MessageBox.Show($"Error deleting preloc: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}