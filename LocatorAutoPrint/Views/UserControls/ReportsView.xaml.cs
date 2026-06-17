using LocatorAutoPrint.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LocatorAutoPrint.Views.UserControls
{
    public partial class ReportsView : UserControl
    {
        public ReportsView()
        {
            InitializeComponent();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Optional: Copy all columns on double-click
            if (sender is DataGrid dataGrid && dataGrid.SelectedItem != null)
            {
                if (dataGrid.DataContext is ReportsViewModel viewModel)
                {
                    viewModel.CopyAllColumnsCommand?.Execute(null);
                }
            }
        }
    }
}