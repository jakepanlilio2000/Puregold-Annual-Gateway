using System.Windows;

namespace LocatorAutoPrint.Views.Modals
{
    public partial class InputDialogWindow : Window
    {
        public string InputText => InputTextBox.Text;

        public InputDialogWindow(string prompt, string title)
        {
            InitializeComponent();
            Title = title;
            PromptTextBlock.Text = prompt;
            Loaded += (s, e) => InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}