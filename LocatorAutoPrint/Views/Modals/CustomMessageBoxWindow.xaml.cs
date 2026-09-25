using System.Windows;
using System.Windows.Media;
using FontAwesome.Sharp;

namespace LocatorAutoPrint.Views.Modals
{
    public partial class CustomMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageBoxWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image = MessageBoxImage.None)
        {
            InitializeComponent();
            this.Title = title;
            MessageTextBlock.Text = message ?? string.Empty;

            ConfigureIcon(image);

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

        private void ConfigureIcon(MessageBoxImage image)
        {
            switch (image)
            {
                case MessageBoxImage.Error: // Also MessageBoxImage.Hand, MessageBoxImage.Stop
                    MessageIcon.Icon = IconChar.CircleExclamation;
                    MessageIcon.Foreground = Brushes.White;
                    IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C"));
                    break;

                case MessageBoxImage.Warning: // Also MessageBoxImage.Exclamation
                    MessageIcon.Icon = IconChar.TriangleExclamation;
                    MessageIcon.Foreground = Brushes.White;
                    IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12"));
                    break;

                case MessageBoxImage.Question:
                    MessageIcon.Icon = IconChar.CircleQuestion;
                    MessageIcon.Foreground = (Brush)Application.Current.TryFindResource("Brush.PrimaryYellow") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD400"));
                    IconBorder.Background = (Brush)Application.Current.TryFindResource("Brush.PrimaryBlue") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005BAC"));
                    break;

                case MessageBoxImage.Information: // Also MessageBoxImage.Asterisk
                default:
                    MessageIcon.Icon = IconChar.CircleInfo;
                    MessageIcon.Foreground = (Brush)Application.Current.TryFindResource("Brush.PrimaryYellow") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD400"));
                    IconBorder.Background = (Brush)Application.Current.TryFindResource("Brush.PrimaryBlue") ?? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#005BAC"));
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
