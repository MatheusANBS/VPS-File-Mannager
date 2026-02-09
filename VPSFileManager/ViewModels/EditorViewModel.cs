using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPSFileManager.Services;

namespace VPSFileManager.ViewModels
{
    public partial class EditorViewModel : ObservableObject
    {
        private readonly ISftpService _sftpService;
        private readonly string _remotePath;
        private Window? _window;
        private string _originalContent = string.Empty;

        [ObservableProperty]
        private string content = string.Empty;

        [ObservableProperty]
        private string fileName = string.Empty;

        [ObservableProperty]
        private string filePath = string.Empty;

        [ObservableProperty]
        private string title = "File Editor";

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private int lineCount;

        [ObservableProperty]
        private string fileSize = "0 bytes";

        [ObservableProperty]
        private string encoding = "UTF-8";

        partial void OnContentChanged(string value)
        {
            LineCount = value.Split('\n').Length;
            FileSize = Models.FileItem.FormatFileSize(System.Text.Encoding.UTF8.GetByteCount(value));
        }

        public EditorViewModel(ISftpService sftpService, string remotePath, string content)
        {
            _sftpService = sftpService;
            _remotePath = remotePath;
            
            Content = content;
            _originalContent = content;
            FileName = Path.GetFileName(remotePath);
            FilePath = remotePath;
            Title = $"Editing: {FileName}";
            
            LineCount = content.Split('\n').Length;
            FileSize = Models.FileItem.FormatFileSize(System.Text.Encoding.UTF8.GetByteCount(content));
        }

        public void SetWindow(Window window)
        {
            _window = window;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                StatusMessage = "Saving...";
                
                try
                {
                    await _sftpService.WriteFileFromStringAsync(_remotePath, Content);
                }
                catch (Exception ex) when (ex.Message.Contains("Permission denied") || ex.Message.Contains("permission denied") || ex.Message.Contains("SFTP session not open"))
                {
                    // Pedir senha sudo
                    var passwordWindow = new Views.PasswordPromptWindow
                    {
                        Owner = _window
                    };

                    if (passwordWindow.ShowDialog() == true)
                    {
                        StatusMessage = "Saving with sudo...";
                        await _sftpService.WriteFileWithSudoAsync(_remotePath, Content, passwordWindow.Password);
                    }
                    else
                    {
                        StatusMessage = "Save cancelled";
                        return;
                    }
                }
                
                _originalContent = Content; // Atualizar conteúdo original após salvar
                StatusMessage = $"Saved at {DateTime.Now:HH:mm:ss}";
                
                // Auto-hide status após 3 segundos
                await Task.Delay(3000);
                StatusMessage = "Ready";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to save file: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task CloseAsync()
        {
            if (HasUnsavedChanges())
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Do you want to save before closing?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    await SaveAsync();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            _window?.Close();
        }

        private bool HasUnsavedChanges()
        {
            return Content != _originalContent;
        }
    }
}
