using System.Windows;

namespace LocatorAutoPrint.Views.Modals
{
    public partial class CustomMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageBoxWindow(string message, string title, MessageBoxButton buttons)
        {
            InitializeComponent();
            this.Title = title;
            MessageTextBlock.Text = message;
            switch (buttons)
            {
                case MessageBoxButton.OK:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnOk.IsDefault = true;
                    break;
                case MessageBoxButton.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnYes.IsDefault = true;
                    BtnNo.IsCancel = true;
                    break;
                case MessageBoxButton.OKCancel:
                    BtnOk.Visibility = Visibility.Visible;
                    BtnNo.Content = "Cancel"; 
                    BtnNo.Visibility = Visibility.Visible;
                    BtnOk.IsDefault = true;
                    BtnNo.IsCancel = true;
                    break;
            }
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            DialogResult = true;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            DialogResult = false;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            DialogResult = true;
            Close();
        }
    }
}