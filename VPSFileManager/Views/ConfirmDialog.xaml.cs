using System.Windows;
using Wpf.Ui.Controls;

namespace VPSFileManager.Views
{
    public enum ConfirmDialogType
    {
        Warning,
        Danger,
        Info
    }

    public partial class ConfirmDialog : FluentWindow
    {
        public ConfirmDialog(string title, string message, ConfirmDialogType dialogType = ConfirmDialogType.Warning, string confirmText = "Confirm", string cancelText = "Cancel")
        {
            InitializeComponent();
            
            TitleText.Text = title;
            MessageText.Text = message;
            ConfirmButton.Content = confirmText;
            CancelButton.Content = cancelText;
            
            Title = title;

            // Configure icon and appearance based on dialog type
            switch (dialogType)
            {
                case ConfirmDialogType.Warning:
                    IconSymbol.Symbol = SymbolRegular.Warning24;
                    IconSymbol.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F7B547"));
                    ConfirmButton.Appearance = ControlAppearance.Caution;
                    break;
                    
                case ConfirmDialogType.Danger:
                    IconSymbol.Symbol = SymbolRegular.ErrorCircle24;
                    IconSymbol.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E74856"));
                    ConfirmButton.Appearance = ControlAppearance.Danger;
                    break;
                    
                case ConfirmDialogType.Info:
                    IconSymbol.Symbol = SymbolRegular.Info24;
                    IconSymbol.Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0078D4"));
                    ConfirmButton.Appearance = ControlAppearance.Primary;
                    break;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
