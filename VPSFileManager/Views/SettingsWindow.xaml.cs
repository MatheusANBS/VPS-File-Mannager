using System.Windows;
using Wpf.Ui.Controls;
using VPSFileManager.ViewModels;

namespace VPSFileManager.Views
{
    public partial class SettingsWindow : FluentWindow
    {
        public SettingsWindow()
        {
            InitializeComponent();
            
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.CloseRequested += (s, e) => DialogResult = true;
            }
        }
    }
}
