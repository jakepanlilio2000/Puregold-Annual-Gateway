using System;
using LocatorAutoPrint.ViewModels;

namespace LocatorAutoPrint.Models
{
    public class UserModel : ViewModelBase
    {
        private string _username;
        private string _password;
        private string _fullname;
        private int? _activeLocator;
        private DateTime? _lastLogin;
        private DateTime? _lastLogout;
        private string _ipAddress;

        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
        public string Fullname { get => _fullname; set { _fullname = value; OnPropertyChanged(); } }
        public int? ActiveLocator { get => _activeLocator; set { _activeLocator = value; OnPropertyChanged(); } }
        public DateTime? LastLogin { get => _lastLogin; set { _lastLogin = value; OnPropertyChanged(); } }
        public DateTime? LastLogout { get => _lastLogout; set { _lastLogout = value; OnPropertyChanged(); } }
        public string IpAddress { get => _ipAddress; set { _ipAddress = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLogin)); } }

        public bool IsLogin => !string.IsNullOrEmpty(IpAddress);
    }
}