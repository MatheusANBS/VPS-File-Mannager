using Wpf.Ui.Controls;

namespace VPSFileManager.Views
{
    public partial class ShortcutsWindow : FluentWindow
    {
        public ShortcutsWindow()
        {
            InitializeComponent();
        }

        private void OnClose(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}
