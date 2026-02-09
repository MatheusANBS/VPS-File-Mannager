using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui;
using Wpf.Ui.Controls;
using VPSFileManager.Models;
using VPSFileManager.ViewModels;
using VPSFileManager.Services;

namespace VPSFileManager.Views
{
    public partial class MainWindow : FluentWindow
    {
        private readonly ISnackbarService _snackbarService;
        private bool _isDragging = false;
        
        // FIX BUG #5: Campos para debounce do drag-leave
        private System.Diagnostics.Stopwatch? _dragLeaveTimer;
        private const int DRAG_LEAVE_DEBOUNCE_MS = 100;

        public MainWindow()
        {
            InitializeComponent();
            
            // Configurar SnackbarService
            _snackbarService = new SnackbarService();
            _snackbarService.SetSnackbarPresenter(RootSnackbar);
            
            // Configurar evento de Snackbar
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.SnackbarRequested += ViewModel_SnackbarRequested;
            }
        }

        private void ViewModel_SnackbarRequested(object? sender, (string Title, string Message) args)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _snackbarService.Show(args.Title, args.Message, ControlAppearance.Success, 
                    new SymbolIcon(SymbolRegular.Checkmark24), System.TimeSpan.FromSeconds(4));
            });
        }

        private void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.MenuItem menuItem && 
                menuItem.CommandParameter is FileItem fileItem &&
                DataContext is MainViewModel viewModel)
            {
                viewModel.DownloadItemCommand.Execute(fileItem);
            }
        }

        private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.MenuItem menuItem && 
                menuItem.CommandParameter is FileItem fileItem &&
                DataContext is MainViewModel viewModel)
            {
                viewModel.RenameItemCommand.Execute(fileItem);
            }
        }

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.MenuItem menuItem && 
                menuItem.CommandParameter is FileItem fileItem &&
                DataContext is MainViewModel viewModel)
            {
                viewModel.DeleteItemCommand.Execute(fileItem);
            }
        }

        private void ContextMenu_Edit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.MenuItem menuItem && 
                menuItem.CommandParameter is FileItem fileItem &&
                DataContext is MainViewModel viewModel)
            {
                viewModel.EditFileCommand.Execute(fileItem);
            }
        }

        private void ContextMenu_Open_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.MenuItem menuItem && 
                menuItem.CommandParameter is FileItem fileItem &&
                DataContext is MainViewModel viewModel)
            {
                viewModel.OpenItemCommand.Execute(fileItem);
            }
        }

        private void SavedConnections_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is SavedConnection savedConnection)
            {
                var viewModel = DataContext as MainViewModel;
                if (viewModel != null)
                {
                    // Confirmar antes de conectar
                    var confirmDialog = new ConfirmDialog(
                        "Connect to Server",
                        $"Connect to {savedConnection.Name}?\n\nHost: {savedConnection.Host}\nUsername: {savedConnection.Username}",
                        ConfirmDialogType.Info,
                        "Connect",
                        "Cancel")
                    {
                        Owner = this
                    };

                    if (confirmDialog.ShowDialog() == true)
                    {
                        viewModel.ConnectToSavedConnectionCommand.Execute(savedConnection);
                    }
                }
            }
        }

        private void FileItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is FileItem fileItem)
            {
                var viewModel = DataContext as MainViewModel;
                viewModel?.NavigateToCommand.Execute(fileItem);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && sender is Wpf.Ui.Controls.PasswordBox passwordBox)
            {
                viewModel.ConnectionInfo.Password = passwordBox.Password;
                
                // Limpar senha após conexão bem-sucedida (segurança)
                if (viewModel.IsConnected)
                {
                    passwordBox.Clear();
                }
            }
        }

        private void FileBrowser_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                if (!_isDragging)
                {
                    _isDragging = true;
                    _dragLeaveTimer?.Stop();  // FIX BUG #5: Cancelar debounce se voltou para drag
                    DragDropOverlay.Visibility = Visibility.Visible;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void FileBrowser_DragLeave(object sender, DragEventArgs e)
        {
            // FIX BUG #5: Usar debounce para evitar flicker com child elements
            _dragLeaveTimer?.Stop();
            _dragLeaveTimer = System.Diagnostics.Stopwatch.StartNew();
            
            System.Threading.Tasks.Task.Delay(DRAG_LEAVE_DEBOUNCE_MS).ContinueWith(_ =>
            {
                if (_dragLeaveTimer?.ElapsedMilliseconds >= DRAG_LEAVE_DEBOUNCE_MS)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!_isDragging)  // Dupla verificação
                        {
                            DragDropOverlay.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            });
        }

        private async void FileBrowser_Drop(object sender, DragEventArgs e)
        {
            try
            {
                _isDragging = false;
                DragDropOverlay.Visibility = Visibility.Collapsed;
                
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (DataContext is MainViewModel viewModel && files != null)
                    {
                        await viewModel.UploadDroppedFilesAsync(files);
                    }
                }
            }
            catch (Exception ex)
            {
                // FIX BUG #4: Garantir que overlay está limpo mesmo com erro
                _isDragging = false;
                DragDropOverlay.Visibility = Visibility.Collapsed;
                
                System.Diagnostics.Debug.WriteLine($"Drop error: {ex}");
                // Erro será mostrado via ShowNotification do viewmodel
            }
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel && sender is System.Windows.Controls.ListBox listBox)
            {
                viewModel.SelectedFiles.Clear();
                foreach (var item in listBox.SelectedItems.Cast<FileItem>())
                {
                    viewModel.SelectedFiles.Add(item);
                }
                // Forçar atualização da UI
                viewModel.UpdateSelectedCount();
            }
        }

        private async void PathEditBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                if (e.Key == Key.Enter)
                {
                    await viewModel.CommitEditPathCommand.ExecuteAsync(null);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    viewModel.CancelEditPathCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }
    }
}