using System;
using System.IO;
using System.Windows;
using Wpf.Ui.Controls;

namespace VPSFileManager.Views
{
    public partial class FolderPickerDialog : FluentWindow
    {
        public string SelectedPath { get; private set; } = string.Empty;

        public FolderPickerDialog(string description = "Select a folder", string initialPath = "")
        {
            InitializeComponent();
            
            DescriptionText.Text = description;
            
            // Definir caminho inicial
            if (!string.IsNullOrEmpty(initialPath) && Directory.Exists(initialPath))
            {
                SelectedPath = initialPath;
            }
            else
            {
                // Padrão: pasta Downloads
                SelectedPath = GetSpecialFolderPath("Downloads");
            }
            
            UpdateCurrentPath();
        }

        private void UpdateCurrentPath()
        {
            CurrentPathText.Text = SelectedPath;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = SelectedPath,
                Description = "Select a folder"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedPath = dialog.SelectedPath;
                UpdateCurrentPath();
            }
        }

        private void SpecialFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button && button.Tag is string folderName)
            {
                SelectedPath = GetSpecialFolderPath(folderName);
                UpdateCurrentPath();
            }
        }

        private string GetSpecialFolderPath(string folderName)
        {
            return folderName switch
            {
                "Desktop" => Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "MyDocuments" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                "MyMusic" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                "MyPictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "MyVideos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                _ => Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SelectedPath) || !Directory.Exists(SelectedPath))
            {
                System.Windows.MessageBox.Show("Please select a valid folder.", "Invalid Folder", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

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
