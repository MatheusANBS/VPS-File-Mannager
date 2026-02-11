using System;
using System.Windows;
using Wpf.Ui.Controls;
using VPSFileManager.Services;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace VPSFileManager.Views
{
    public partial class UpdateWindow : FluentWindow
    {
        private readonly UpdateInfo _updateInfo;
        private bool _isDownloading = false;

        public UpdateWindow(UpdateInfo updateInfo)
        {
            InitializeComponent();
            _updateInfo = updateInfo;

            // Preencher informações
            var currentVersion = UpdateService.CurrentVersion;
            var newVersion = updateInfo.TagName.TrimStart('v', 'V');
            VersionText.Text = $"v{currentVersion.Major}.{currentVersion.Minor}.{currentVersion.Build} → v{newVersion}";

            // Changelog - usar o body do release ou mensagem padrão
            var changelog = string.IsNullOrWhiteSpace(updateInfo.Body) 
                ? "No release notes available." 
                : updateInfo.Body;
            
            // Limpar markdown básico para exibição
            changelog = changelog
                .Replace("## ", "")
                .Replace("### ", "")
                .Replace("**", "")
                .Replace("- ", "• ")
                .Replace("* ", "• ");

            ChangelogText.Text = changelog;

            // Mostrar tamanho do download se disponível
            if (updateInfo.Assets.Length > 0)
            {
                foreach (var asset in updateInfo.Assets)
                {
                    if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        VersionText.Text += $"  •  {UpdateService.FormatSize(asset.Size)}";
                        break;
                    }
                }
            }
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading) return;
            _isDownloading = true;

            // Mostrar progresso, esconder botões de ação
            ProgressPanel.Visibility = Visibility.Visible;
            UpdateButton.IsEnabled = false;
            UpdateButton.Content = "Downloading...";
            CancelButton.IsEnabled = false;

            try
            {
                var progress = new Progress<double>(percent =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        DownloadProgress.Value = percent;
                        ProgressPercentText.Text = $"{percent:F0}%";
                    });
                });

                var installerPath = await UpdateService.DownloadInstallerAsync(_updateInfo, progress);

                if (installerPath != null)
                {
                    ProgressStatusText.Text = "Installing...";
                    ProgressPercentText.Text = "100%";
                    DownloadProgress.Value = 100;

                    // Pequeno delay para o usuário ver o 100%
                    await System.Threading.Tasks.Task.Delay(500);

                    // Executar instalador silencioso e fechar o app
                    UpdateService.InstallAndRestart(installerPath);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "Failed to download the update. Please try again later.",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    ResetButtons();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"An error occurred during the update:\n{ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                ResetButtons();
            }
        }

        private void ResetButtons()
        {
            _isDownloading = false;
            ProgressPanel.Visibility = Visibility.Collapsed;
            UpdateButton.IsEnabled = true;
            UpdateButton.Content = "Update & Restart";
            CancelButton.IsEnabled = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDownloading)
            {
                Close();
            }
        }
    }
}
