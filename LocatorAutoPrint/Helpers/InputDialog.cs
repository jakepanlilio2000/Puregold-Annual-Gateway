using System.Windows;
using System.Windows.Controls;

namespace LocatorAutoPrint.Helpers
{
    public static class InputDialog
    {
        public static string Show(string prompt, string title)
        {
            Window inputWindow = new Window
            {
                Title = title,
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            StackPanel panel = new StackPanel { Margin = new Thickness(15) };
            panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) });

            TextBox inputBox = new TextBox { Height = 24 };
            panel.Children.Add(inputBox);

            Button okButton = new Button { Content = "OK", Width = 80, Height = 25, Margin = new Thickness(0, 15, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
            okButton.Click += (s, e) => { inputWindow.DialogResult = true; };
            panel.Children.Add(okButton);

            inputWindow.Content = panel;
            inputBox.Focus();

            if (inputWindow.ShowDialog() == true)
            {
                return inputBox.Text;
            }

            return string.Empty;
        }
    }
}