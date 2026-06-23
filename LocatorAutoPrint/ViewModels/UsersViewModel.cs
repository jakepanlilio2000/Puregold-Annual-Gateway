using LocatorAutoPrint.Commands;
using LocatorAutoPrint.Models;
using LocatorAutoPrint.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

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

        private async System.Threading.Tasks.Task LogoutMobileAppAsync()
        {
            if (MessageBox.Show($"Force logout mobile app for user: {SelectedUser.Username}?", "Confirm Mobile Logout", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _userService.LogoutMobileAppAsync(SelectedUser.Username);
                await LoadUsersAsync();
                MessageBox.Show("User logged out from mobile app successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private bool CanSubmit() => !string.IsNullOrWhiteSpace(SingleInputName);

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            var list = await _userService.GetUsersAsync();
            UsersList = new ObservableCollection<UserModel>(list);

            var pingTasks = UsersList.Where(u => !string.IsNullOrWhiteSpace(u.IpAddress)).Select(async user =>
            {
                user.IsOnline = await PingAddressAsync(user.IpAddress);
            });

            await Task.WhenAll(pingTasks);
        }

        private async Task<bool> PingAddressAsync(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress)) return false;

            try
            {
                using (var pinger = new Ping())
                {
                    var reply = await pinger.SendPingAsync(ipAddress, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        private async System.Threading.Tasks.Task AddUserAsync()
        {
            string storeCode = _configService.Config.DefaultStoreNum;
            await _userService.AddUserAsync(SingleInputName, SingleInputName, SingleInputName, storeCode);

            ClearForm();
            await LoadUsersAsync();
            MessageBox.Show("User Added Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async System.Threading.Tasks.Task EditUserAsync()
        {
            string storeCode = _configService.Config.DefaultStoreNum;

            // UPDATED: Use the SelectedUser's ORIGINAL username as the database key so it doesn't fail if you rename them
            string originalUsername = SelectedUser.Username;

            await _userService.UpdateUserAsync(originalUsername, SingleInputName, SingleInputName, storeCode);

            ClearForm();
            await LoadUsersAsync();
            MessageBox.Show("User Updated Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async System.Threading.Tasks.Task DeleteUserAsync()
        {
            if (MessageBox.Show($"Delete user {SelectedUser.Username}?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _userService.DeleteUserAsync(SelectedUser.Username);
                ClearForm();
                await LoadUsersAsync();
            }
        }

        private void ClearForm()
        {
            SingleInputName = string.Empty;
            SelectedUser = null;
        }
    }
}