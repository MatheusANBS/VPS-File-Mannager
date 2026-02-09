using System.Windows;
using Wpf.Ui.Controls;

namespace VPSFileManager.Views
{
    public partial class PasswordPromptWindow : FluentWindow
    {
        public string? Password { get; set; }

        public PasswordPromptWindow()
        {
            InitializeComponent();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordInput.Password;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Password = null;
            DialogResult = false;
            Close();
        }
    }
}
