using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using LocatorAutoPrint.ViewModels;

namespace LocatorAutoPrint.Views.UserControls
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContextChanged += SettingsView_DataContextChanged;
        }

        private void SettingsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SettingsViewModel oldVm)
            {
                oldVm.PropertyChanged -= Vm_PropertyChanged;
            }

            if (e.NewValue is SettingsViewModel newVm)
            {
                newVm.PropertyChanged += Vm_PropertyChanged;
                SyncPassword(newVm);
            }
        }

        private void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is SettingsViewModel vm)
            {
                if (e.PropertyName == nameof(SettingsViewModel.DbPass) || e.PropertyName == nameof(SettingsViewModel.ShowPassword))
                {
                    SyncPassword(vm);
                }
            }
        }

        private void SyncPassword(SettingsViewModel vm)
        {
            if (DbPasswordBox != null && DbPasswordBox.Password != vm.DbPass)
            {
                DbPasswordBox.Password = vm.DbPass ?? string.Empty;
            }
        }

        private void DbPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm && !vm.ShowPassword)
            {
                vm.DbPass = DbPasswordBox.Password;
            }
        }
    }
}

