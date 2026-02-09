using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using VPSFileManager.Models;
using VPSFileManager.Services;
using System.Linq;
using System.Threading.Tasks;

namespace VPSFileManager.Views
{
    public partial class SearchDialog : FluentWindow
    {
        private readonly ISftpService _sftpService;
        public ObservableCollection<FileItem> SearchResults { get; } = new();
        public FileItem? SelectedResult { get; private set; }

        public SearchDialog(ISftpService sftpService, string currentPath)
        {
            InitializeComponent();
            _sftpService = sftpService;
            SearchPathBox.Text = currentPath;
            ResultsListBox.ItemsSource = SearchResults;
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            var pattern = SearchPatternBox.Text?.Trim();
            if (string.IsNullOrEmpty(pattern))
            {
                StatusText.Text = "Please enter a search pattern";
                return;
            }

            SearchResults.Clear();
            StatusText.Text = "Searching...";
            
            try
            {
                var results = await SearchFilesAsync(
                    SearchPathBox.Text, 
                    pattern!, 
                    RecursiveCheckBox.IsChecked == true,
                    CaseSensitiveCheckBox.IsChecked == true);

                foreach (var item in results)
                {
                    SearchResults.Add(item);
                }

                StatusText.Text = $"Found {results.Count} result(s)";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show($"Search failed: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task<ObservableCollection<FileItem>> SearchFilesAsync(string startPath, string pattern, bool recursive, bool caseSensitive)
        {
            var results = new ObservableCollection<FileItem>();
            
            // Determinar se é busca por extensão ou nome
            bool isExtensionSearch = pattern.StartsWith("*.");
            bool isWildcardSearch = pattern.Contains("*") || pattern.Contains("?");
            
            string searchPattern = caseSensitive ? pattern : pattern.ToLower();
            
            // Construir comando de busca usando find no servidor
            string findCommand;
            
            if (isExtensionSearch)
            {
                // Busca por extensão: *.pdf
                var ext = pattern.Substring(1); // Remove o *
                findCommand = recursive 
                    ? $"find {EscapeShellArg(startPath)} -type f -name '*{ext}' 2>/dev/null"
                    : $"find {EscapeShellArg(startPath)} -maxdepth 1 -type f -name '*{ext}' 2>/dev/null";
            }
            else if (isWildcardSearch)
            {
                // Busca com wildcard
                findCommand = recursive
                    ? $"find {EscapeShellArg(startPath)} -name '{pattern}' 2>/dev/null"
                    : $"find {EscapeShellArg(startPath)} -maxdepth 1 -name '{pattern}' 2>/dev/null";
            }
            else
            {
                // Busca por nome parcial (case insensitive por padrão)
                var iname = caseSensitive ? "name" : "iname";
                findCommand = recursive
                    ? $"find {EscapeShellArg(startPath)} -{iname} '*{pattern}*' 2>/dev/null"
                    : $"find {EscapeShellArg(startPath)} -maxdepth 1 -{iname} '*{pattern}*' 2>/dev/null";
            }

            // Limitar resultados para evitar sobrecarga
            findCommand += " | head -n 500";

            var output = await _sftpService.ExecuteCommandAsync(findCommand);
            
            if (string.IsNullOrWhiteSpace(output) || output.StartsWith("Error"))
                return results;

            var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                try
                {
                    var path = line.Trim();
                    if (string.IsNullOrEmpty(path)) continue;

                    // Obter informações do arquivo
                    var statCommand = $"stat -c '%F|%s|%Y' {EscapeShellArg(path)} 2>/dev/null";
                    var statOutput = await _sftpService.ExecuteCommandAsync(statCommand);
                    
                    if (string.IsNullOrEmpty(statOutput)) continue;

                    var parts = statOutput.Split('|');
                    if (parts.Length < 3) continue;

                    var isDirectory = parts[0].Contains("directory");
                    var size = long.TryParse(parts[1], out var s) ? s : 0;
                    var timestamp = long.TryParse(parts[2], out var t) ? t : 0;
                    var lastModified = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

                    var item = new FileItem
                    {
                        Name = System.IO.Path.GetFileName(path),
                        FullPath = path,
                        IsDirectory = isDirectory,
                        Size = size,
                        LastModified = lastModified
                    };

                    results.Add(item);
                }
                catch
                {
                    // Ignorar erros em arquivos individuais
                    continue;
                }
            }

            return results;
        }

        private string EscapeShellArg(string arg)
        {
            return "'" + arg.Replace("'", "'\\''") + "'";
        }

        private void ResultItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultsListBox.SelectedItem is FileItem item)
            {
                SelectedResult = item;
                DialogResult = true;
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
