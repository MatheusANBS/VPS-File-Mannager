using System;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using VPSFileManager.Models;

namespace VPSFileManager.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        public event EventHandler? CloseRequested;

        public SettingsViewModel()
        {
        }

        [RelayCommand]
        private void Close()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
