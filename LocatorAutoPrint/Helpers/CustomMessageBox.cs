using System.Windows;
using LocatorAutoPrint.Views.Modals;

namespace LocatorAutoPrint.Helpers
{
    public static class CustomMessageBox
    {
        public static MessageBoxResult Show(string message, string title = "Information", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.None)
        {
            var dialog = new CustomMessageBoxWindow(message, title, buttons, image);
            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                dialog.Owner = Application.Current.MainWindow;
            }
            dialog.ShowDialog();
            return dialog.Result;
        }
    }
}