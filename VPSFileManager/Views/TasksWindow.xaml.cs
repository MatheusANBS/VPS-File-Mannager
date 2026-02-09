using System.Windows;
using Wpf.Ui.Controls;
using VPSFileManager.ViewModels;

namespace VPSFileManager.Views
{
    public partial class TasksWindow : FluentWindow
    {
        public TasksWindow()
        {
            InitializeComponent();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
