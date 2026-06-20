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

        public ObservableCollection<UserModel> UsersList { get; } = new ObservableCollection<UserModel>();

        private string _firstName;
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); } }

        private string _lastName;
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); } }

        private string _username;
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _rawPassword;
        public string RawPassword { get => _rawPassword; set { _rawPassword = value; OnPropertyChanged(); } }

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
                    Username = _selectedUser.Username;
                    var names = _selectedUser.Fullname?.Split(' ');
                    if (names != null && names.Length >= 2)
                    {
                        FirstName = names[0];
                        LastName = string.Join(" ", names, 1, names.Length - 1);
                    }
                }
            }
        }

        public ICommand LoadUsersCommand { get; }
        public ICommand AddUserCommand { get; }
        public ICommand EditUserCommand { get; }
        public ICommand DeleteUserCommand { get; }
        public ICommand ClearFormCommand { get; }
        public ICommand LogoutMobileAppCommand { get; }

        public UsersViewModel(UserService userService)
        {
            _userService = userService;
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
        private bool CanSubmit() => !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(RawPassword);

        private async System.Threading.Tasks.Task LoadUsersAsync()
        {
            UsersList.Clear();
            var list = await _userService.GetUsersAsync();
            foreach (var u in list) UsersList.Add(u);
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
            await _userService.AddUserAsync(Username, RawPassword, FirstName, LastName);
            ClearForm();
            await LoadUsersAsync();
            MessageBox.Show("User Added Successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async System.Threading.Tasks.Task EditUserAsync()
        {
            await _userService.UpdateUserAsync(Username, RawPassword, FirstName, LastName);
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
            FirstName = LastName = Username = RawPassword = string.Empty;
            SelectedUser = null;
        }
    }
}