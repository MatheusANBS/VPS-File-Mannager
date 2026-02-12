using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using VPSFileManager.Models;
using VPSFileManager.Services;
using VPSFileManager.Views;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Application = System.Windows.Application;

namespace VPSFileManager.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISftpService _sftpService;
        private readonly CredentialManager _credentialManager;
        private readonly TerminalService _terminalService;
        private readonly System.Text.StringBuilder _terminalBuffer = new(capacity: 32768);
        private const int MAX_TERMINAL_BUFFER = 50000;

        // Upload control
        private readonly System.Threading.SemaphoreSlim _uploadSemaphore = new(1, 1);
        private int _activeUploads = 0;
        private System.Threading.CancellationTokenSource? _uploadCancellation;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmptyDirectory))]
        private ObservableCollection<FileItem> files = new();

        [ObservableProperty]
        private FileItem? selectedFile;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedFilesCount))]
        private ObservableCollection<FileItem> selectedFiles = new();

        [ObservableProperty]
        private ConnectionInfo connectionInfo = new();

        [ObservableProperty]
        private string currentPath = "/";

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEmptyDirectory))]
        private bool isLoading;

        private string _statusMessage = "Disconnected";
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    if (Application.Current?.Dispatcher?.CheckAccess() == true)
                    {
                        SetProperty(ref _statusMessage, value);
                    }
                    else
                    {
                        Application.Current?.Dispatcher?.Invoke(() => SetProperty(ref _statusMessage, value));
                    }
                }
            }
        }

        [ObservableProperty]
        private double uploadProgress;

        [ObservableProperty]
        private bool isUploading;

        [ObservableProperty]
        private string commandInput = string.Empty;

        [ObservableProperty]
        private string commandOutput = string.Empty;

        [ObservableProperty]
        private string terminalOutput = string.Empty;

        [ObservableProperty]
        private string terminalInput = string.Empty;

        [ObservableProperty]
        private bool showConnectionDialog = true;

        [ObservableProperty]
        private ObservableCollection<string> pathSegments = new();

        // Conexões salvas
        [ObservableProperty]
        private ObservableCollection<SavedConnection> savedConnections = new();

        [ObservableProperty]
        private SavedConnection? selectedSavedConnection;

        [ObservableProperty]
        private bool saveCredentials = true;

        [ObservableProperty]
        private string connectionName = string.Empty;

        // Terminal interativo
        [ObservableProperty]
        private bool useInteractiveTerminal = false;

        [ObservableProperty]
        private bool isTerminalConnected = false;

        [ObservableProperty]
        private bool showTerminal = false;

        // InfoBar properties
        [ObservableProperty]
        private bool showInfoBar = false;

        [ObservableProperty]
        private string infoBarTitle = string.Empty;

        [ObservableProperty]
        private string infoBarMessage = string.Empty;

        [ObservableProperty]
        private Wpf.Ui.Controls.InfoBarSeverity infoBarSeverity = Wpf.Ui.Controls.InfoBarSeverity.Informational;

        // Breadcrumb navigation
        [ObservableProperty]
        private ObservableCollection<BreadcrumbItem> breadcrumbItems = new();

        // Favorite paths
        [ObservableProperty]
        private ObservableCollection<string> favoritePaths = new();

        // Search and filter
        [ObservableProperty]
        private string searchQuery = string.Empty;

        [ObservableProperty]
        private bool isSearching = false;

        // Navigation history
        private readonly System.Collections.Generic.Stack<string> _navigationBackStack = new();
        private readonly System.Collections.Generic.Stack<string> _navigationForwardStack = new();

        // Selection state cache
        private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> _selectionCache = new();

        // Selected files count
        [ObservableProperty]
        private int selectedFilesCount = 0;

        // Path editing
        [ObservableProperty]
        private bool isEditingPath = false;

        [ObservableProperty]
        private string editablePath = "/";

        // App settings
        [ObservableProperty]
        private bool autoConnect = false;

        // Tasks ViewModel
        [ObservableProperty]
        private TasksViewModel tasksViewModel = null!;

        // Upload statistics
        [ObservableProperty]
        private string uploadSpeed = string.Empty;

        [ObservableProperty]
        private string uploadEta = string.Empty;

        private AppSettings _appSettings = AppSettings.Load();

        // Auto-update
        [ObservableProperty]
        private bool isUpdateAvailable;

        [ObservableProperty]
        private string updateVersionText = string.Empty;

        private UpdateInfo? _pendingUpdate;

        // Multiple selection flag
        public bool HasMultipleSelection => SelectedFilesCount > 1;

        // Empty directory state
        public bool IsEmptyDirectory => IsConnected && !IsLoading && Files.Count == 0;

        public void UpdateSelectedCount()
        {
            SelectedFilesCount = SelectedFiles.Count;
            OnPropertyChanged(nameof(HasMultipleSelection));
        }

        public MainViewModel()
        {
            _sftpService = new SftpService();
            _credentialManager = new CredentialManager();
            _terminalService = new TerminalService();

            // Inicializar TasksViewModel
            TasksViewModel = new TasksViewModel(_sftpService);

            // Carregar conexões salvas
            LoadSavedConnections();

            // Configurar eventos do terminal
            _terminalService.DataReceived += OnTerminalDataReceived;
            _terminalService.Disconnected += OnTerminalDisconnected;

            // Inicializar path editável
            EditablePath = CurrentPath;

            // Carregar settings e auto-connect
            AutoConnect = _appSettings.AutoConnect;
            _ = TryAutoConnectAsync();

            // Verificar atualizações em background
            _ = CheckForUpdatesAsync();
        }

        /// <summary>
        /// Verifica se há atualizações disponíveis no GitHub
        /// </summary>
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                await Task.Delay(2000); // Esperar 2s para não atrasar o startup
                var update = await UpdateService.CheckForUpdateAsync();
                if (update != null)
                {
                    _pendingUpdate = update;
                    UpdateVersionText = $"v{update.TagName.TrimStart('v', 'V')}";

                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        IsUpdateAvailable = true;
                    });
                }
            }
            catch
            {
                // Silenciar erros de verificação de atualização
            }
        }

        [RelayCommand]
        private void ShowUpdateWindow()
        {
            if (_pendingUpdate == null) return;

            var updateWindow = new Views.UpdateWindow(_pendingUpdate)
            {
                Owner = Application.Current?.MainWindow
            };
            updateWindow.ShowDialog();
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterFiles();
        }

        partial void OnCurrentPathChanged(string value)
        {
            EditablePath = value;
            // Sempre salvar o path atual
            if (IsConnected && !string.IsNullOrEmpty(value))
            {
                _appSettings.LastPath = value;
                _appSettings.Save();
            }
        }

        private ObservableCollection<FileItem> _allFiles = new();

        private void FilterFiles()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                // Restaurar todos os arquivos
                Files.Clear();
                foreach (var file in _allFiles)
                {
                    Files.Add(file);
                }
                IsSearching = false;
                return;
            }

            IsSearching = true;
            var query = SearchQuery.ToLower();
            var filtered = _allFiles.Where(f =>
                f.Name.ToLower().Contains(query) ||
                f.FormattedSize.ToLower().Contains(query) ||
                f.FormattedDate.Contains(query) ||
                (f.IsDirectory && "folder".Contains(query)));

            Files.Clear();
            foreach (var file in filtered)
            {
                Files.Add(file);
            }
        }

        private void LoadSavedConnections()
        {
            var connections = _credentialManager.LoadConnections();
            SavedConnections.Clear();
            foreach (var conn in connections)
            {
                SavedConnections.Add(conn);
            }
        }

        private void OnTerminalDataReceived(object? sender, string data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _terminalBuffer.Append(data);
                // Limitar tamanho do buffer para não consumir muita memória
                if (_terminalBuffer.Length > MAX_TERMINAL_BUFFER)
                {
                    _terminalBuffer.Remove(0, _terminalBuffer.Length - (MAX_TERMINAL_BUFFER / 2));
                }
                TerminalOutput = _terminalBuffer.ToString();
            });
        }

        private void OnTerminalDisconnected(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsTerminalConnected = false;
                TerminalOutput += "\r\n[Terminal disconnected]\r\n";
            });
        }

        [RelayCommand]
        private void LoadSavedConnection()
        {
            if (SelectedSavedConnection == null) return;

            ConnectionInfo = SelectedSavedConnection.ToConnectionInfo();
            ConnectionName = SelectedSavedConnection.Name;

            // Carregar favoritos da conexão
            FavoritePaths.Clear();
            if (SelectedSavedConnection.FavoritePaths != null)
            {
                foreach (var path in SelectedSavedConnection.FavoritePaths)
                {
                    FavoritePaths.Add(path);
                }
            }
        }

        [RelayCommand]
        private async Task ConnectToSavedConnection(SavedConnection savedConnection)
        {
            if (savedConnection == null) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Connecting...";

                // Carregar informações da conexão
                ConnectionInfo = savedConnection.ToConnectionInfo();
                ConnectionName = savedConnection.Name;

                // Carregar favoritos
                FavoritePaths.Clear();
                if (savedConnection.FavoritePaths != null)
                {
                    foreach (var path in savedConnection.FavoritePaths)
                    {
                        FavoritePaths.Add(path);
                    }
                }

                // Conectar
                await _sftpService.ConnectAsync(ConnectionInfo);

                IsConnected = true;
                ShowConnectionDialog = false;
                StatusMessage = $"Connected to {ConnectionInfo.Host}";
                SelectedSavedConnection = savedConnection;

                // Salvar como última conexão para auto-connect
                _appSettings.LastConnectionId = savedConnection.Id;
                _appSettings.Save();

                // Navegar para o último path salvo, ou home se não houver
                if (!string.IsNullOrEmpty(_appSettings.LastPath))
                {
                    CurrentPath = _appSettings.LastPath;
                }
                else
                {
                    CurrentPath = $"/home/{ConnectionInfo.Username}";
                }
                await RefreshDirectoryAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
                MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void DeleteSavedConnection()
        {
            if (SelectedSavedConnection == null) return;

            var result = MessageBox.Show(
                $"Delete saved connection '{SelectedSavedConnection.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _credentialManager.RemoveConnection(SelectedSavedConnection.Id);
                LoadSavedConnections();
            }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (string.IsNullOrEmpty(ConnectionInfo.Host) || string.IsNullOrEmpty(ConnectionInfo.Username))
            {
                StatusMessage = "Please fill in host and username";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = "Connecting...";

                await _sftpService.ConnectAsync(ConnectionInfo);

                IsConnected = true;
                ShowConnectionDialog = false;
                StatusMessage = $"Connected to {ConnectionInfo.Host}";

                // Passar a senha ao TasksViewModel para uso em comandos sudo
                TasksViewModel.SetPassword(ConnectionInfo.Password);

                // Salvar credenciais se solicitado
                if (SaveCredentials)
                {
                    var name = string.IsNullOrEmpty(ConnectionName)
                        ? $"{ConnectionInfo.Username}@{ConnectionInfo.Host}"
                        : ConnectionName;

                    var savedConn = SavedConnection.FromConnectionInfo(ConnectionInfo, name);
                    _credentialManager.AddConnection(savedConn);
                    SelectedSavedConnection = savedConn;
                    LoadSavedConnections();

                    _appSettings.LastConnectionId = savedConn.Id;
                    _appSettings.Save();
                }

                // Navegar para o último path salvo, ou home se não houver
                if (!string.IsNullOrEmpty(_appSettings.LastPath))
                {
                    CurrentPath = _appSettings.LastPath;
                }
                else
                {
                    CurrentPath = $"/home/{ConnectionInfo.Username}";
                }
                await RefreshDirectoryAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connection failed: {ex.Message}";
                MessageBox.Show($"Failed to connect: {ex.Message}", "Connection Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OpenTasksWindow()
        {
            var tasksWindow = new Views.TasksWindow
            {
                DataContext = TasksViewModel,
                Owner = Application.Current?.MainWindow
            };
            tasksWindow.ShowDialog();
        }

        [RelayCommand]
        private void OpenDashboardWindow()
        {
            if (!IsConnected)
            {
                ShowNotification("Not Connected", "Please connect to a server first.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            var viewModel = new DashboardViewModel(_sftpService);
            var dashboardWindow = new Views.DashboardWindow
            {
                Owner = Application.Current?.MainWindow
            };
            dashboardWindow.Show();
            dashboardWindow.StartMonitoring(viewModel);
        }

        [RelayCommand]
        private void ShowHelp()
        {
            var helpWindow = new Views.HelpWindow
            {
                Owner = Application.Current?.MainWindow
            };
            helpWindow.ShowDialog();
        }

        [RelayCommand]
        private void Disconnect()
        {
            // Salvar bookmarks antes de desconectar
            if (SelectedSavedConnection != null)
            {
                SelectedSavedConnection.FavoritePaths = FavoritePaths.ToList();
                _credentialManager.AddConnection(SelectedSavedConnection);
            }

            _sftpService.Disconnect();

            IsConnected = false;
            ShowConnectionDialog = true;
            Files.Clear();
            CurrentPath = "/";
            StatusMessage = "Disconnected";
            CommandOutput = string.Empty;
        }

        // Upload simples - suporte a diretórios
        public async Task UploadDroppedFilesAsync(string[] filePaths)
        {
            if (!IsConnected || filePaths == null || filePaths.Length == 0) return;

            IsUploading = true;
            try
            {
                // Expandir diretórios
                var allFiles = new List<(string FullPath, string RelativePath)>();
                foreach (var path in filePaths)
                {
                    if (Directory.Exists(path))
                    {
                        var dirName = Path.GetFileName(path);
                        var files = Directory.GetFiles(path, "*", System.IO.SearchOption.AllDirectories);
                        foreach (var file in files)
                        {
                            var relativePath = file.Substring(path.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            allFiles.Add((file, $"{dirName}/{relativePath}".Replace("\\", "/")));
                        }
                    }
                    else if (File.Exists(path))
                    {
                        var fileName = Path.GetFileName(path);
                        allFiles.Add((path, fileName));
                    }
                }

                if (allFiles.Count == 0)
                {
                    StatusMessage = "No files to upload";
                    return;
                }

                // Criar diretórios
                var dirsToCreate = new HashSet<string>();
                foreach (var (fullPath, relativePath) in allFiles)
                {
                    var dirPath = Path.GetDirectoryName(relativePath);
                    if (!string.IsNullOrEmpty(dirPath))
                    {
                        var remoteDirPath = $"{CurrentPath}/{dirPath}".Replace("\\", "/").Replace("//", "/");
                        dirsToCreate.Add(remoteDirPath);
                    }
                }

                foreach (var dir in dirsToCreate.OrderBy(d => d.Length))
                {
                    try { await _sftpService.CreateDirectoryAsync(dir); } catch { }
                }

                // Upload dos arquivos
                for (int i = 0; i < allFiles.Count; i++)
                {
                    var (fullPath, relativePath) = allFiles[i];
                    StatusMessage = $"Uploading {i + 1}/{allFiles.Count}: {relativePath}";
                    var remotePath = $"{CurrentPath}/{relativePath}".Replace("\\", "/").Replace("//", "/");

                    System.Diagnostics.Debug.WriteLine($"Uploading: {fullPath} -> {remotePath}");

                    try
                    {
                        await _sftpService.UploadFileAsync(fullPath, remotePath, null);
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = $"Failed to upload '{relativePath}': {ex.Message}";
                        System.Diagnostics.Debug.WriteLine($"Upload error: {errorMsg}");
                        throw new Exception(errorMsg, ex);
                    }
                }

                StatusMessage = $"Uploaded {allFiles.Count} file(s)";
                ShowSnackbar("Upload Complete", $"Successfully uploaded {allFiles.Count} file(s)");

                await Task.Delay(500);
                await RefreshDirectoryAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Upload failed: {ex.Message}";
                ShowNotification("Upload Failed", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
            finally
            {
                IsUploading = false;
            }
        }

        [RelayCommand]
        private async Task RefreshDirectoryAsync()
        {
            if (!IsConnected) return;

            try
            {
                IsLoading = true;
                var items = await _sftpService.ListDirectoryAsync(CurrentPath);

                _allFiles.Clear();
                foreach (var item in items)
                {
                    _allFiles.Add(item);
                }

                // Aplicar filtro se houver busca ativa
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    FilterFiles();
                }
                else
                {
                    Files.Clear();
                    foreach (var item in _allFiles)
                    {
                        Files.Add(item);
                    }
                }

                UpdatePathSegments();
                UpdateBreadcrumb();
                StatusMessage = $"{Files.Count} items | {CurrentPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                ShowNotification("Error", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateBreadcrumb()
        {
            BreadcrumbItems.Clear();

            // Add root
            BreadcrumbItems.Add(new BreadcrumbItem("🏠 root", "/", CurrentPath == "/"));

            if (CurrentPath != "/")
            {
                var parts = CurrentPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var currentBuiltPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    currentBuiltPath += "/" + parts[i];
                    var isLast = i == parts.Length - 1;
                    BreadcrumbItems.Add(new BreadcrumbItem(parts[i], currentBuiltPath, isLast));
                }
            }
        }

        [RelayCommand]
        private async Task NavigateToBreadcrumb(string path)
        {
            if (string.IsNullOrEmpty(path) || path == CurrentPath) return;

            System.Diagnostics.Debug.WriteLine($"DEBUG: Navigating via breadcrumb from '{CurrentPath}' to '{path}'");
            System.Diagnostics.Debug.WriteLine($"DEBUG: Back stack before: {string.Join(" -> ", _navigationBackStack)}");
            System.Diagnostics.Debug.WriteLine($"DEBUG: Forward stack before: {string.Join(" -> ", _navigationForwardStack)}");

            // Salvar seleções atuais
            SaveCurrentSelections();

            // Salvar histórico antes de navegar
            _navigationBackStack.Push(CurrentPath);
            _navigationForwardStack.Clear(); // Limpar forward ao navegar para novo path

            System.Diagnostics.Debug.WriteLine($"DEBUG: Back stack after push: {string.Join(" -> ", _navigationBackStack)}");

            CurrentPath = path;
            await RefreshDirectoryAsync();

            // Restaurar seleções se existirem
            RestoreSelections();

            System.Diagnostics.Debug.WriteLine($"DEBUG: Navigation complete, now at '{CurrentPath}'");
        }

        [RelayCommand]
        private async Task GoBackAsync()
        {
            if (_navigationBackStack.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Going back from '{CurrentPath}'");
                System.Diagnostics.Debug.WriteLine($"DEBUG: Back stack: {string.Join(" -> ", _navigationBackStack)}");
                System.Diagnostics.Debug.WriteLine($"DEBUG: Forward stack: {string.Join(" -> ", _navigationForwardStack)}");

                SaveCurrentSelections();
                _navigationForwardStack.Push(CurrentPath);
                CurrentPath = _navigationBackStack.Pop();
                await RefreshDirectoryAsync();
                RestoreSelections();

                System.Diagnostics.Debug.WriteLine($"DEBUG: Went back to '{CurrentPath}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("DEBUG: Cannot go back - back stack is empty");
            }
        }

        [RelayCommand]
        private async Task GoForwardAsync()
        {
            if (_navigationForwardStack.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Going forward from '{CurrentPath}'");
                System.Diagnostics.Debug.WriteLine($"DEBUG: Back stack: {string.Join(" -> ", _navigationBackStack)}");
                System.Diagnostics.Debug.WriteLine($"DEBUG: Forward stack: {string.Join(" -> ", _navigationForwardStack)}");

                SaveCurrentSelections();
                _navigationBackStack.Push(CurrentPath);
                CurrentPath = _navigationForwardStack.Pop();
                await RefreshDirectoryAsync();
                RestoreSelections();

                System.Diagnostics.Debug.WriteLine($"DEBUG: Went forward to '{CurrentPath}'");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("DEBUG: Cannot go forward - forward stack is empty");
            }
        }

        private void ShowNotification(string title, string message, Wpf.Ui.Controls.InfoBarSeverity severity)
        {
            InfoBarTitle = title;
            InfoBarMessage = message;
            InfoBarSeverity = severity;
            ShowInfoBar = true;

            // Auto-hide com timeouts variáveis baseados na severidade
            var timeout = severity switch
            {
                Wpf.Ui.Controls.InfoBarSeverity.Error => 10000,      // 10s para erros
                Wpf.Ui.Controls.InfoBarSeverity.Warning => 7000,      // 7s para warnings
                Wpf.Ui.Controls.InfoBarSeverity.Success => 3000,      // 3s para sucesso
                _ => 5000                                              // 5s para info
            };

            Task.Delay(timeout).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(() => ShowInfoBar = false);
            });
        }

        // Método público para o Snackbar (será chamado do code-behind)
        public void ShowSnackbar(string title, string message)
        {
            // Este método será sobrescrito pelo code-behind via evento
            SnackbarRequested?.Invoke(this, (title, message));
        }

        public event EventHandler<(string Title, string Message)>? SnackbarRequested;

        [RelayCommand]
        private async Task NavigateToAsync(FileItem? item)
        {
            if (item == null) return;

            if (item.Name == "..")
            {
                await GoUpAsync();
                return;
            }

            if (item.IsDirectory)
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Navigating to directory '{item.FullPath}' from '{CurrentPath}'");

                // Salvar seleções atuais
                SaveCurrentSelections();

                // Salvar histórico antes de navegar
                _navigationBackStack.Push(CurrentPath);
                _navigationForwardStack.Clear();

                System.Diagnostics.Debug.WriteLine($"DEBUG: Back stack after directory navigation: {string.Join(" -> ", _navigationBackStack)}");

                CurrentPath = item.FullPath;
                await RefreshDirectoryAsync();

                // Restaurar seleções se existirem
                RestoreSelections();
            }
            else
            {
                // É um arquivo - abrir para edição
                await EditFileAsync(item);
            }
        }

        [RelayCommand]
        private async Task OpenItemAsync(FileItem? item)
        {
            if (item == null) return;

            // Se for arquivo de texto, abrir no editor
            if (!item.IsDirectory && IsTextFile(item.Name))
            {
                await EditFileAsync(item);
            }
            else
            {
                await NavigateToAsync(item);
            }
        }

        [RelayCommand]
        private async Task EditFileAsync(FileItem? item)
        {
            if (item == null || item.IsDirectory) return;

            try
            {
                IsLoading = true;
                StatusMessage = $"Loading {item.Name}...";

                // Ler conteúdo do arquivo
                var content = await _sftpService.ReadFileAsStringAsync(item.FullPath);

                // Criar e abrir janela do editor
                var editorViewModel = new EditorViewModel(_sftpService, item.FullPath, content);
                var editorWindow = new Views.EditorWindow(editorViewModel);
                editorViewModel.SetWindow(editorWindow);

                StatusMessage = "Ready";
                editorWindow.Show();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open file: {ex.Message}";
                ShowNotification("Editor Error", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool IsTextFile(string fileName)
        {
            var textExtensions = new[]
            {
                ".txt", ".log", ".conf", ".config", ".ini", ".json", ".xml", ".yml", ".yaml",
                ".sh", ".bash", ".py", ".js", ".ts", ".jsx", ".tsx", ".css", ".scss", ".sass",
                ".html", ".htm", ".md", ".markdown", ".sql", ".cs", ".java", ".php", ".rb",
                ".go", ".rs", ".c", ".cpp", ".h", ".hpp", ".env", ".gitignore", ".dockerignore"
            };

            var extension = Path.GetExtension(fileName).ToLower();
            return textExtensions.Contains(extension);
        }

        [RelayCommand]
        private async Task GoUpAsync()
        {
            if (CurrentPath == "/") return;

            var parent = Path.GetDirectoryName(CurrentPath)?.Replace("\\", "/") ?? "/";
            if (string.IsNullOrEmpty(parent)) parent = "/";

            CurrentPath = parent;
            await RefreshDirectoryAsync();
        }

        [RelayCommand]
        private async Task GoHomeAsync()
        {
            CurrentPath = "/";
            await RefreshDirectoryAsync();
        }

        [RelayCommand]
        private async Task NavigateToPathAsync(string path)
        {
            CurrentPath = path;
            await RefreshDirectoryAsync();
        }

        [RelayCommand]
        private async Task OpenAdvancedSearch()
        {
            if (!IsConnected) return;

            var searchDialog = new Views.SearchDialog(_sftpService, CurrentPath)
            {
                Owner = Application.Current?.MainWindow
            };

            if (searchDialog.ShowDialog() == true && searchDialog.SelectedResult != null)
            {
                var result = searchDialog.SelectedResult;

                // Se for diretório, navegar até ele
                if (result.IsDirectory)
                {
                    CurrentPath = result.FullPath;
                    await RefreshDirectoryAsync();
                }
                else
                {
                    // Se for arquivo, navegar até o diretório pai e selecionar o arquivo
                    var directory = System.IO.Path.GetDirectoryName(result.FullPath)?.Replace("\\", "/") ?? "/";
                    CurrentPath = directory;
                    await RefreshDirectoryAsync();

                    // Selecionar o arquivo
                    var file = Files.FirstOrDefault(f => f.FullPath == result.FullPath);
                    if (file != null)
                    {
                        SelectedFile = file;
                    }
                }
            }
        }

        [RelayCommand]
        private void AddCurrentToFavorites()
        {
            if (!IsConnected || string.IsNullOrEmpty(CurrentPath)) return;

            // Evitar duplicados
            if (FavoritePaths.Contains(CurrentPath))
            {
                ShowSnackbar("Already in Favorites", $"{CurrentPath} is already bookmarked");
                return;
            }

            FavoritePaths.Add(CurrentPath);

            // Salvar favoritos na conexão atual
            SaveFavoritesToConnection();

            ShowSnackbar("Bookmark Added", $"Added {CurrentPath} to favorites");
        }

        [RelayCommand]
        private void RemoveFavorite(string? path)
        {
            if (string.IsNullOrEmpty(path) || !FavoritePaths.Contains(path)) return;

            FavoritePaths.Remove(path);

            // Salvar favoritos na conexão atual
            SaveFavoritesToConnection();

            ShowSnackbar("Bookmark Removed", $"Removed {path} from favorites");
        }

        [RelayCommand]
        private async Task NavigateToFavoriteAsync(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // Validar existência antes de navegar
            try
            {
                if (!await _sftpService.DirectoryExistsAsync(path))
                {
                    var confirmDialog = new Views.ConfirmDialog(
                        "Invalid Path",
                        $"Directory '{path}' no longer exists. Remove from favorites?",
                        Views.ConfirmDialogType.Warning,
                        "Remove",
                        "Keep")
                    {
                        Owner = Application.Current?.MainWindow
                    };

                    if (confirmDialog.ShowDialog() == true)
                    {
                        RemoveFavorite(path);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowNotification("Navigation Error", $"Cannot access path: {ex.Message}", Wpf.Ui.Controls.InfoBarSeverity.Error);
                return;
            }

            await NavigateToPathAsync(path);
        }

        private void SaveFavoritesToConnection()
        {
            if (SelectedSavedConnection != null)
            {
                SelectedSavedConnection.FavoritePaths = FavoritePaths.ToList();
                _credentialManager.AddConnection(SelectedSavedConnection);
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            SelectedFiles.Clear();
            SelectedFile = null;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void SelectAll()
        {
            SelectedFiles.Clear();
            foreach (var file in Files.Where(f => f.Name != ".."))
            {
                SelectedFiles.Add(file);
            }
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void ToggleSearch()
        {
            IsSearching = !IsSearching;
            if (!IsSearching)
            {
                SearchQuery = string.Empty;
            }
        }

        [RelayCommand]
        private async Task UploadFilesAsync()
        {
            if (!IsConnected) return;

            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select files to upload"
            };

            if (dialog.ShowDialog() == true)
            {
                await _uploadSemaphore.WaitAsync();
                _uploadCancellation = new System.Threading.CancellationTokenSource();

                try
                {
                    System.Threading.Interlocked.Increment(ref _activeUploads);
                    IsUploading = _activeUploads > 0;

                    var totalFiles = dialog.FileNames.Length;
                    var currentFile = 0;

                    foreach (var file in dialog.FileNames)
                    {
                        if (_uploadCancellation.Token.IsCancellationRequested)
                        {
                            StatusMessage = "Upload cancelled";
                            break;
                        }

                        currentFile++;
                        var fileName = Path.GetFileName(file);
                        var remotePath = $"{CurrentPath}/{fileName}".Replace("//", "/");

                        StatusMessage = $"Uploading {fileName} ({currentFile}/{totalFiles})...";

                        var progress = new Progress<double>(p => UploadProgress = p);
                        await _sftpService.UploadFileAsync(file, remotePath, progress);
                    }

                    if (!_uploadCancellation.Token.IsCancellationRequested)
                    {
                        StatusMessage = $"Uploaded {totalFiles} file(s) successfully";
                        await RefreshDirectoryAsync();
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Upload failed: {ex.Message}";
                    MessageBox.Show($"Upload failed: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    System.Threading.Interlocked.Decrement(ref _activeUploads);
                    IsUploading = _activeUploads > 0;
                    if (_activeUploads == 0)
                        UploadProgress = 0;
                    _uploadCancellation?.Dispose();
                    _uploadCancellation = null;
                    _uploadSemaphore.Release();
                }
            }
        }

        [RelayCommand]
        private void CancelUpload()
        {
            _uploadCancellation?.Cancel();
        }

        [RelayCommand]
        private async Task DownloadFileAsync()
        {
            if (!IsConnected)
            {
                ShowNotification("Not Connected", "Please connect to a server first.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            // Baixar arquivos e pastas selecionados
            var itemsToDownload = SelectedFiles.Where(f => f.Name != "..").ToList();

            if (itemsToDownload.Count == 0)
            {
                ShowNotification("No Selection", "Please select file(s) or folder(s) to download.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            if (itemsToDownload.Count == 1)
            {
                // Download único - usar o método dedicado que já trata arquivo e pasta
                await DownloadItemAsync(itemsToDownload[0]);
                return;
            }

            // Download múltiplo
            var folderDialog = new FolderPickerDialog("Select where to save the downloaded files");

            if (folderDialog.ShowDialog() == true)
            {
                try
                {
                    IsUploading = true;
                    var totalItems = itemsToDownload.Count;
                    var currentItem = 0;

                    foreach (var item in itemsToDownload)
                    {
                        currentItem++;
                        StatusMessage = $"Downloading {item.Name} ({currentItem}/{totalItems})...";

                        if (item.IsDirectory)
                        {
                            var localPath = Path.Combine(folderDialog.SelectedPath, item.Name);
                            await DownloadDirectoryRecursiveAsync(item.FullPath, localPath);
                        }
                        else
                        {
                            var localPath = Path.Combine(folderDialog.SelectedPath, item.Name);
                            var progress = new Progress<double>(p => UploadProgress = p);
                            await _sftpService.DownloadFileAsync(item.FullPath, localPath, progress);
                        }
                    }

                    StatusMessage = $"Downloaded {totalItems} item(s) successfully";
                    ShowSnackbar("Download Complete", $"Items saved to {folderDialog.SelectedPath}");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Download failed: {ex.Message}";
                    ShowNotification("Download Failed", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
                }
                finally
                {
                    IsUploading = false;
                    UploadProgress = 0;
                }
            }
        }

        [RelayCommand]
        private async Task DownloadItemAsync(FileItem? item)
        {
            if (!IsConnected || item == null || item.Name == "..") return;

            if (item.IsDirectory)
            {
                // Download folder
                var dialog = new FolderPickerDialog($"Select where to save folder '{item.Name}'");

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        IsLoading = true;
                        var localPath = Path.Combine(dialog.SelectedPath, item.Name);
                        StatusMessage = $"Downloading folder {item.Name}...";

                        await DownloadDirectoryRecursiveAsync(item.FullPath, localPath);

                        StatusMessage = $"Downloaded folder {item.Name} successfully";
                        ShowSnackbar("Download Complete", $"Folder saved to {localPath}");
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Download failed: {ex.Message}";
                        ShowNotification("Download Failed", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
                    }
                    finally
                    {
                        IsLoading = false;
                        UploadProgress = 0;
                    }
                }
            }
            else
            {
                // Download file
                var dialog = new SaveFileDialog
                {
                    FileName = item.Name,
                    Title = "Save file as"
                };

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        IsLoading = true;
                        StatusMessage = $"Downloading {item.Name}...";

                        var progress = new Progress<double>(p => UploadProgress = p);
                        await _sftpService.DownloadFileAsync(item.FullPath, dialog.FileName, progress);

                        StatusMessage = $"Downloaded {item.Name} successfully";
                        ShowSnackbar("Download Complete", $"File saved to {dialog.FileName}");
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"Download failed: {ex.Message}";
                        ShowNotification("Download Failed", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
                    }
                    finally
                    {
                        IsLoading = false;
                        UploadProgress = 0;
                    }
                }
            }
        }

        private async Task DownloadDirectoryRecursiveAsync(string remotePath, string localPath)
        {
            // Criar diretório local
            Directory.CreateDirectory(localPath);

            // Listar arquivos e pastas
            var items = await _sftpService.ListDirectoryAsync(remotePath);

            foreach (var item in items.Where(f => f.Name != "." && f.Name != ".."))
            {
                var remoteItemPath = $"{remotePath}/{item.Name}".Replace("//", "/");
                var localItemPath = Path.Combine(localPath, item.Name);

                if (item.IsDirectory)
                {
                    await DownloadDirectoryRecursiveAsync(remoteItemPath, localItemPath);
                }
                else
                {
                    StatusMessage = $"Downloading {item.Name}...";
                    await _sftpService.DownloadFileAsync(remoteItemPath, localItemPath, null);
                }
            }
        }

        [RelayCommand]
        private async Task CreateFolderAsync()
        {
            if (!IsConnected) return;

            var inputWindow = new Views.InputPromptWindow(
                "Create Folder",
                "Enter a name for the new folder",
                "Folder Name",
                "New Folder")
            {
                Owner = Application.Current?.MainWindow
            };

            if (inputWindow.ShowDialog() == true && !string.IsNullOrEmpty(inputWindow.InputValue))
            {
                var folderName = inputWindow.InputValue;
                try
                {
                    var path = $"{CurrentPath}/{folderName}".Replace("//", "/");
                    await _sftpService.CreateDirectoryAsync(path);
                    StatusMessage = $"Created folder: {folderName}";
                    await RefreshDirectoryAsync();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Failed to create folder: {ex.Message}";
                    MessageBox.Show($"Failed to create folder: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedAsync()
        {
            if (!IsConnected)
            {
                ShowNotification("Not Connected", "Please connect to a server first.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            // Usar múltiplos arquivos selecionados ou apenas o selecionado
            var filesToDelete = SelectedFiles.Where(f => f.Name != "..").ToList();

            if (filesToDelete.Count == 0)
            {
                if (SelectedFile != null && SelectedFile.Name != "..")
                {
                    filesToDelete.Add(SelectedFile);
                }
            }

            if (filesToDelete.Count == 0)
            {
                ShowNotification("No Selection", "Please select files or folders to delete.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            var message = filesToDelete.Count == 1
                ? $"Are you sure you want to delete '{filesToDelete[0].Name}'?"
                : $"Are you sure you want to delete {filesToDelete.Count} items?";

            var confirmDialog = new Views.ConfirmDialog(
                "Confirm Delete",
                message,
                Views.ConfirmDialogType.Danger,
                "Delete",
                "Cancel")
            {
                Owner = Application.Current?.MainWindow
            };

            if (confirmDialog.ShowDialog() == true)
            {
                try
                {
                    IsLoading = true;
                    var deletedCount = 0;
                    string? sudoPassword = null;

                    foreach (var file in filesToDelete)
                    {
                        StatusMessage = $"Deleting {file.Name}...";

                        try
                        {
                            if (file.IsDirectory)
                            {
                                await _sftpService.DeleteDirectoryAsync(file.FullPath);
                            }
                            else
                            {
                                await _sftpService.DeleteFileAsync(file.FullPath);
                            }
                        }
                        catch (Exception ex) when (ex.Message.Contains("Permission denied") || ex.Message.Contains("permission denied"))
                        {
                            // Pedir senha sudo se ainda não temos
                            if (string.IsNullOrEmpty(sudoPassword))
                            {
                                var passwordWindow = new Views.PasswordPromptWindow
                                {
                                    Owner = Application.Current?.MainWindow
                                };

                                if (passwordWindow.ShowDialog() == true)
                                {
                                    sudoPassword = passwordWindow.Password;
                                }
                                else
                                {
                                    throw new OperationCanceledException("Operação cancelada pelo usuário.");
                                }
                            }

                            // Tentar novamente com sudo
                            StatusMessage = $"Deleting {file.Name} with sudo...";
                            if (file.IsDirectory)
                            {
                                await _sftpService.DeleteDirectoryWithSudoAsync(file.FullPath, sudoPassword);
                            }
                            else
                            {
                                await _sftpService.DeleteFileWithSudoAsync(file.FullPath, sudoPassword);
                            }
                        }
                        deletedCount++;
                    }

                    StatusMessage = $"Deleted {deletedCount} item(s)";
                    ShowSnackbar("Delete Complete", $"{deletedCount} item(s) deleted");
                    await RefreshDirectoryAsync();
                }
                catch (OperationCanceledException)
                {
                    StatusMessage = "Delete cancelled";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Delete failed: {ex.Message}";
                    ShowNotification("Delete Failed", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task DeleteItemAsync(FileItem? item)
        {
            if (!IsConnected || item == null || item.Name == "..") return;

            var confirmDialog = new Views.ConfirmDialog(
                "Confirm Delete",
                $"Are you sure you want to delete '{item.Name}'?",
                Views.ConfirmDialogType.Danger,
                "Delete",
                "Cancel")
            {
                Owner = Application.Current?.MainWindow
            };

            if (confirmDialog.ShowDialog() == true)
            {
                try
                {
                    IsLoading = true;
                    StatusMessage = $"Deleting {item.Name}...";

                    System.Diagnostics.Debug.WriteLine($"Deleting: {item.FullPath} (IsDirectory: {item.IsDirectory})");

                    try
                    {
                        if (item.IsDirectory)
                        {
                            await _sftpService.DeleteDirectoryAsync(item.FullPath);
                        }
                        else
                        {
                            await _sftpService.DeleteFileAsync(item.FullPath);
                        }
                    }
                    catch (Exception ex) when (ex.Message.Contains("Permission denied") || ex.Message.Contains("permission denied"))
                    {
                        // Pedir senha sudo
                        var passwordWindow = new Views.PasswordPromptWindow
                        {
                            Owner = Application.Current?.MainWindow
                        };

                        if (passwordWindow.ShowDialog() == true)
                        {
                            StatusMessage = $"Deleting {item.Name} with sudo...";
                            if (item.IsDirectory)
                            {
                                await _sftpService.DeleteDirectoryWithSudoAsync(item.FullPath, passwordWindow.Password);
                            }
                            else
                            {
                                await _sftpService.DeleteFileWithSudoAsync(item.FullPath, passwordWindow.Password);
                            }
                        }
                        else
                        {
                            throw new OperationCanceledException("Operação cancelada pelo usuário.");
                        }
                    }

                    StatusMessage = $"Deleted {item.Name}";
                    ShowSnackbar("Delete Complete", $"{item.Name} was deleted");
                    await RefreshDirectoryAsync();
                }
                catch (OperationCanceledException)
                {
                    StatusMessage = "Delete cancelled";
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to delete '{item.Name}': {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"Delete error: {errorMsg}\nPath: {item.FullPath}\nStack: {ex.StackTrace}");
                    StatusMessage = $"Delete failed: {ex.Message}";
                    ShowNotification("Delete Failed", errorMsg, Wpf.Ui.Controls.InfoBarSeverity.Error);
                }
                finally
                {
                    IsLoading = false;
                }
            }
        }

        [RelayCommand]
        private async Task RenameSelectedAsync()
        {
            if (!IsConnected)
            {
                ShowNotification("Not Connected", "Please connect to a server first.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            var fileToRename = SelectedFiles.FirstOrDefault();
            if (fileToRename == null)
            {
                fileToRename = SelectedFile;
            }

            if (fileToRename == null || fileToRename.Name == "..")
            {
                ShowNotification("No Selection", "Please select a file or folder to rename.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            var inputWindow = new Views.InputPromptWindow(
                "Rename",
                $"Enter a new name for '{fileToRename.Name}'",
                "New Name",
                fileToRename.Name)
            {
                Owner = Application.Current?.MainWindow
            };

            if (inputWindow.ShowDialog() == true && !string.IsNullOrEmpty(inputWindow.InputValue) && inputWindow.InputValue != fileToRename.Name)
            {
                var newName = inputWindow.InputValue;
                try
                {
                    var directory = Path.GetDirectoryName(fileToRename.FullPath)?.Replace("\\", "/") ?? CurrentPath;
                    var newPath = $"{directory}/{newName}".Replace("//", "/");

                    await _sftpService.RenameAsync(fileToRename.FullPath, newPath);
                    StatusMessage = $"Renamed to: {newName}";
                    ShowSnackbar("Rename Complete", $"{fileToRename.Name} → {newName}");
                    await RefreshDirectoryAsync();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Rename failed: {ex.Message}";
                    ShowNotification("Rename Failed", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
                }
            }
        }

        [RelayCommand]
        private async Task RenameItemAsync(FileItem? item)
        {
            if (!IsConnected || item == null || item.Name == "..") return;

            var inputWindow = new Views.InputPromptWindow(
                "Rename",
                $"Enter a new name for '{item.Name}'",
                "New Name",
                item.Name)
            {
                Owner = Application.Current?.MainWindow
            };

            if (inputWindow.ShowDialog() == true && !string.IsNullOrEmpty(inputWindow.InputValue) && inputWindow.InputValue != item.Name)
            {
                var newName = inputWindow.InputValue;
                try
                {
                    var directory = Path.GetDirectoryName(item.FullPath)?.Replace("\\", "/") ?? CurrentPath;
                    var newPath = $"{directory}/{newName}".Replace("//", "/");

                    await _sftpService.RenameAsync(item.FullPath, newPath);
                    StatusMessage = $"Renamed to: {newName}";
                    ShowSnackbar("Rename Complete", $"{item.Name} → {newName}");
                    await RefreshDirectoryAsync();
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Rename failed: {ex.Message}";
                    ShowNotification("Rename Failed", ex.Message, Wpf.Ui.Controls.InfoBarSeverity.Error);
                }
            }
        }

        // Terminal simples (não-interativo)
        [RelayCommand]
        private async Task ExecuteCommandAsync()
        {
            if (!IsConnected || string.IsNullOrEmpty(CommandInput)) return;

            var cmd = CommandInput.Trim();
            CommandOutput += $"$ {cmd}\n";
            CommandInput = string.Empty;

            try
            {
                var result = await _sftpService.ExecuteCommandAsync(cmd);
                CommandOutput += $"{result}\n\n";
            }
            catch (Exception ex)
            {
                CommandOutput += $"❌ Error: {ex.Message}\n\n";
            }
        }

        [RelayCommand]
        private void SendCtrlD()
        {
            if (IsTerminalConnected)
            {
                _terminalService.SendCtrlD();
            }
        }

        [RelayCommand]
        private void OpenWindowsTerminal()
        {
            if (!IsConnected)
            {
                ShowNotification("Not Connected", "Please connect to a server first.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            if (string.IsNullOrEmpty(ConnectionInfo?.Username) || string.IsNullOrEmpty(ConnectionInfo?.Host))
            {
                ShowNotification("Invalid Connection", "Connection information is missing.", Wpf.Ui.Controls.InfoBarSeverity.Error);
                return;
            }

            try
            {
                // Construir comando SSH para Windows Terminal
                var username = ConnectionInfo.Username;
                var host = ConnectionInfo.Host;
                var port = ConnectionInfo.Port;

                var sshCommand = $"ssh {username}@{host}";

                if (port != 22)
                {
                    sshCommand += $" -p {port}";
                }

                if (ConnectionInfo.UsePrivateKey && !string.IsNullOrEmpty(ConnectionInfo.PrivateKeyPath))
                {
                    sshCommand += $" -i \"\"\"{ConnectionInfo.PrivateKeyPath}\"\"\"";
                }

                // Tentar abrir com Windows Terminal
                var wtPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "WindowsApps", "wt.exe");

                if (File.Exists(wtPath))
                {
                    // Windows Terminal instalado - usar cmd /k para manter aberto
                    var args = $"-w 0 cmd /k \"{sshCommand}\"";
                    System.Diagnostics.Process.Start(wtPath, args);
                }
                else
                {
                    // Fallback para cmd.exe
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/k {sshCommand}",
                        UseShellExecute = true
                    });
                }

                ShowSnackbar("Terminal Opened", $"SSH session: {username}@{host}");
            }
            catch (Exception ex)
            {
                ShowNotification("Terminal Error", $"Failed to open terminal: {ex.Message}", Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
        }

        [RelayCommand]
        private async Task OpenTerminalWindowAsync()
        {
            if (!IsConnected)
            {
                ShowNotification("Not Connected", "Please connect to a server first.", Wpf.Ui.Controls.InfoBarSeverity.Warning);
                return;
            }

            if (ConnectionInfo == null || string.IsNullOrEmpty(ConnectionInfo.Host))
            {
                ShowNotification("Invalid Connection", "Connection information is missing.", Wpf.Ui.Controls.InfoBarSeverity.Error);
                return;
            }

            try
            {
                var terminalWindow = new Views.TerminalWindow
                {
                    Owner = Application.Current.MainWindow
                };
                terminalWindow.Show();

                // Conectar o terminal (usa sua própria instância de TerminalService com raw mode)
                await terminalWindow.ConnectAsync(ConnectionInfo);
            }
            catch (Exception ex)
            {
                ShowNotification("Terminal Error", $"Failed to open terminal: {ex.Message}", Wpf.Ui.Controls.InfoBarSeverity.Error);
            }
        }

        [RelayCommand]
        private void ClearTerminal()
        {
            TerminalOutput = string.Empty;
        }

        // Selection persistence methods
        private void SaveCurrentSelections()
        {
            if (SelectedFiles.Count > 0)
            {
                var selectedNames = new System.Collections.Generic.HashSet<string>(
                    SelectedFiles.Select(f => f.FullPath));
                _selectionCache[CurrentPath] = selectedNames;
                System.Diagnostics.Debug.WriteLine($"DEBUG: Saved {SelectedFiles.Count} selections for path '{CurrentPath}': {string.Join(", ", SelectedFiles.Select(f => f.Name))}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: No selections to save for path '{CurrentPath}'");
            }
        }

        private void RestoreSelections()
        {
            SelectedFiles.Clear();

            if (_selectionCache.TryGetValue(CurrentPath, out var selectedPaths))
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: Found {selectedPaths.Count} cached selections for path '{CurrentPath}': {string.Join(", ", selectedPaths)}");
                var restoredCount = 0;
                foreach (var file in Files)
                {
                    if (selectedPaths.Contains(file.FullPath))
                    {
                        SelectedFiles.Add(file);
                        restoredCount++;
                        System.Diagnostics.Debug.WriteLine($"DEBUG: Restored selection for '{file.Name}'");
                    }
                }
                UpdateSelectedCount();
                System.Diagnostics.Debug.WriteLine($"DEBUG: Restored {restoredCount} out of {selectedPaths.Count} selections");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"DEBUG: No cached selections found for path '{CurrentPath}'");
            }
        }

        // Path editing commands
        [RelayCommand]
        private void StartEditPath()
        {
            IsEditingPath = true;
        }

        [RelayCommand]
        private async Task CommitEditPathAsync()
        {
            IsEditingPath = false;

            if (string.IsNullOrWhiteSpace(EditablePath))
            {
                EditablePath = CurrentPath;
                return;
            }

            // Normalizar path
            var normalizedPath = EditablePath.Trim();
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            // Verificar se path existe
            if (!await _sftpService.DirectoryExistsAsync(normalizedPath))
            {
                ShowNotification("Invalid Path", $"Directory '{normalizedPath}' does not exist.",
                    Wpf.Ui.Controls.InfoBarSeverity.Warning);
                EditablePath = CurrentPath;
                return;
            }

            // Navegar para o path
            SaveCurrentSelections();
            _navigationBackStack.Push(CurrentPath);
            _navigationForwardStack.Clear();

            CurrentPath = normalizedPath;
            await RefreshDirectoryAsync();
            RestoreSelections();
        }

        [RelayCommand]
        private void CancelEditPath()
        {
            IsEditingPath = false;
            EditablePath = CurrentPath;
        }

        [RelayCommand]
        private void ToggleTerminal()
        {
            ShowTerminal = !ShowTerminal;
        }

        [RelayCommand]
        private void BrowsePrivateKey()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Private Key File",
                Filter = "All Files (*.*)|*.*|PEM Files (*.pem)|*.pem|PPK Files (*.ppk)|*.ppk"
            };

            if (dialog.ShowDialog() == true)
            {
                ConnectionInfo.PrivateKeyPath = dialog.FileName;
            }
        }

        private void UpdatePathSegments()
        {
            PathSegments.Clear();

            var segments = CurrentPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var currentFullPath = "";

            PathSegments.Add("/");

            foreach (var segment in segments)
            {
                currentFullPath += "/" + segment;
                PathSegments.Add(segment);
            }
        }

        private async Task TryAutoConnectAsync()
        {
            if (!AutoConnect || string.IsNullOrEmpty(_appSettings.LastConnectionId))
                return;

            // Aguardar um pouco para UI carregar
            await Task.Delay(500);

            var lastConnection = SavedConnections.FirstOrDefault(c => c.Id == _appSettings.LastConnectionId);
            if (lastConnection != null)
            {
                try
                {
                    await ConnectToSavedConnectionCommand.ExecuteAsync(lastConnection);
                }
                catch
                {
                    // Falha silenciosa - usuário pode conectar manualmente
                    StatusMessage = "Auto-connect failed. Please connect manually.";
                }
            }
        }

        partial void OnAutoConnectChanged(bool value)
        {
            _appSettings.AutoConnect = value;
            _appSettings.Save();
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWindow = new Views.SettingsWindow
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settingsWindow.ShowDialog();
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F1} B/s";
            if (bytesPerSecond < 1024 * 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            if (bytesPerSecond < 1024 * 1024 * 1024)
                return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
            return $"{bytesPerSecond / (1024 * 1024 * 1024):F1} GB/s";
        }

        private static string FormatEta(double seconds)
        {
            if (seconds < 60)
                return $"{seconds:F0}s";
            if (seconds < 3600)
                return $"{seconds / 60:F0}m {seconds % 60:F0}s";
            return $"{seconds / 3600:F0}h {(seconds % 3600) / 60:F0}m";
        }
    }
}
