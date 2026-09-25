using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Helpers;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;

namespace LocatorAutoPrint.ViewModels
{
    public class UsersViewModel : ViewModelBase
    {
        private readonly UserService _userService;
        private readonly ConfigService _configService;

        private ObservableCollection<UserModel> _usersList = new ObservableCollection<UserModel>();
        public ObservableCollection<UserModel> UsersList
        {
            get => _usersList;
            set { _usersList = value; OnPropertyChanged(); }
        }

        private string _singleInputName;
        public string SingleInputName
        {
            get => _singleInputName;
            set { _singleInputName = value; OnPropertyChanged(); }
        }

        private UserModel _selectedUser;
        public UserModel SelectedUser
        {
            get => _selectedUser;
            set
            {
                _selectedUser = value;
                OnPropertyChanged();
                if (_selectedUser != null)
                {
                    SingleInputName = _selectedUser.Username;
                }
            }
        }

        public ICommand LoadUsersCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand LogoutMobileAppCommand { get; }

        public UsersViewModel(UserService userService, ConfigService configService)
        {
            _userService = userService;
            _configService = configService;

            LoadUsersCommand = new RelayCommand(async _ => await LoadUsersAsync());
            AddUserCommand = new RelayCommand(async _ => await AddUserAsync(), _ => CanSubmit());
            EditUserCommand = new RelayCommand(async _ => await EditUserAsync(), _ => CanSubmit() && SelectedUser != null);
            DeleteUserCommand = new RelayCommand(async _ => await DeleteUserAsync(), _ => SelectedUser != null);
            ClearFormCommand = new RelayCommand(_ => ClearForm());
            LogoutMobileAppCommand = new RelayCommand(async _ => await LogoutMobileAppAsync(), _ => SelectedUser != null);
        }

        private async Task LogoutMobileAppAsync()
        {
            if (SelectedUser == null) return;

            try
            {
                if (CustomMessageBox.Show($"Force logout mobile app for user: {SelectedUser.Username}?", "Confirm Mobile Logout", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    await _userService.LogoutMobileAppAsync(SelectedUser.Username);
                    await LoadUsersAsync();
                    CustomMessageBox.Show("User logged out from mobile app successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("UsersViewModel.LogoutMobileAppAsync", ex);
                CustomMessageBox.Show($"Failed to logout mobile app: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanSubmit() => !string.IsNullOrWhiteSpace(SingleInputName);

        private async Task LoadUsersAsync()
        {
            try
            {
                var list = await _userService.GetUsersAsync();
                UsersList = new ObservableCollection<UserModel>(list);

                var pingTasks = UsersList.Where(u => !string.IsNullOrWhiteSpace(u.IpAddress)).Select(async user =>
                {
                    user.IsOnline = await PingAddressAsync(user.IpAddress);
                });

                await Task.WhenAll(pingTasks);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("UsersViewModel.LoadUsersAsync", ex);
                CustomMessageBox.Show($"Failed to load users: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<bool> PingAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return false;

            try
            {
                using (var pinger = new Ping())
                {
                    var reply = await pinger.SendPingAsync(ipAddress.Trim(), 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task AddUserAsync()
        {
            if (!CanSubmit()) return;

            string cleanName = SingleInputName.Trim();
            if (cleanName.Length > 50)
            {
                CustomMessageBox.Show("User Identifier cannot exceed 50 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string storeCode = _configService.Config?.DefaultStoreNum ?? "722";
                await _userService.AddUserAsync(cleanName, cleanName, cleanName, storeCode);

                ClearForm();
                await LoadUsersAsync();
                CustomMessageBox.Show("User Added Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("UsersViewModel.AddUserAsync", ex);
                CustomMessageBox.Show($"Error adding user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EditUserAsync()
        {
            if (!CanSubmit() || SelectedUser == null) return;

            string cleanName = SingleInputName.Trim();
            if (cleanName.Length > 50)
            {
                CustomMessageBox.Show("User Identifier cannot exceed 50 characters.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string storeCode = _configService.Config?.DefaultStoreNum ?? "722";
                string originalUsername = SelectedUser.Username;

                await _userService.UpdateUserAsync(originalUsername, cleanName, cleanName, storeCode);

                ClearForm();
                await LoadUsersAsync();
                CustomMessageBox.Show("User Updated Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("UsersViewModel.EditUserAsync", ex);
                CustomMessageBox.Show($"Error updating user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DeleteUserAsync()
        {
            if (SelectedUser == null) return;

            try
            {
                if (CustomMessageBox.Show($"Are you sure you want to delete user {SelectedUser.Username}?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    await _userService.DeleteUserAsync(SelectedUser.Username);
                    ClearForm();
                    await LoadUsersAsync();
                    CustomMessageBox.Show("User deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ErrorLoggerService.LogException("UsersViewModel.DeleteUserAsync", ex);
                CustomMessageBox.Show($"Error deleting user: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm()
        {
            SingleInputName = string.Empty;
            SelectedUser = null;
        }
    }
}