using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;
using VPSFileManager.Models;
using VPSFileManager.Services;

namespace VPSFileManager.Views
{
    public partial class TerminalWindow : FluentWindow
    {
        private TerminalService? _terminalService;
        private ConnectionInfo? _connectionInfo;
        private bool _isConnected;
        private System.Windows.Threading.DispatcherTimer? _resizeTimer;

        public TerminalWindow()
        {
            InitializeComponent();

            // Configurar eventos do terminal control
            TerminalView.DataInput += OnTerminalInput;

            Loaded += TerminalWindow_Loaded;
            Closing += TerminalWindow_Closing;
        }

        /// <summary>
        /// Conecta o terminal a um servidor SSH.
        /// </summary>
        public async Task ConnectAsync(ConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
            ConnectionLabel.Text = $"{connectionInfo.Username}@{connectionInfo.Host}:{connectionInfo.Port}";

            try
            {
                StatusText.Text = "Connecting...";
                StatusDot.Fill = System.Windows.Media.Brushes.Orange;

                _terminalService = new TerminalService();
                _terminalService.DataReceived += OnDataReceived;
                _terminalService.Disconnected += OnDisconnected;

                // Calcular tamanho do terminal baseado na janela
                TerminalView.FitToSize();
                var (cols, rows) = TerminalView.GetTerminalSize();

                // Conectar com as dimensões corretas
                await _terminalService.ConnectAsync(connectionInfo, cols, rows);

                _isConnected = true;
                StatusText.Text = "Connected";
                StatusDot.Fill = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(78, 201, 176)); // #4EC9B0
                UpdateSizeLabel();

                // Foco no terminal
                TerminalView.Focus();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Connection failed: {ex.Message}";
                StatusDot.Fill = System.Windows.Media.Brushes.Red;
                
                System.Windows.MessageBox.Show(
                    $"Failed to connect terminal:\n{ex.Message}",
                    "Terminal Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void OnTerminalInput(object? sender, string data)
        {
            if (_isConnected && _terminalService != null)
            {
                _terminalService.SendKey(data);
            }
        }

        private void OnDataReceived(object? sender, string data)
        {
            // Escrever dados raw no terminal emulador
            Dispatcher.Invoke(() =>
            {
                TerminalView.Write(data);
            });
        }

        private void OnDisconnected(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _isConnected = false;
                StatusText.Text = "Disconnected";
                StatusDot.Fill = System.Windows.Media.Brushes.Gray;
                
                TerminalView.Write("\r\n\x1b[31m[Connection closed]\x1b[0m\r\n");
            });
        }

        private void TerminalWindow_Loaded(object sender, RoutedEventArgs e)
        {
            TerminalView.Focus();
            UpdateSizeLabel();
        }

        private void TerminalWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _isConnected = false;

            if (_terminalService != null)
            {
                _terminalService.DataReceived -= OnDataReceived;
                _terminalService.Disconnected -= OnDisconnected;
                _terminalService.Disconnect();
                _terminalService.Dispose();
                _terminalService = null;
            }

            TerminalView.Dispose();
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            // Enviar clear para o terminal remoto
            if (_isConnected && _terminalService != null)
            {
                _terminalService.SendCommand("clear");
            }
            TerminalView.Focus();
        }

        private void CopyBtn_Click(object sender, RoutedEventArgs e)
        {
            var text = TerminalView.Terminal.GetVisibleText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    Clipboard.SetText(text.TrimEnd());
                }
                catch { }
            }
            TerminalView.Focus();
        }

        private void UpdateSizeLabel()
        {
            var (cols, rows) = TerminalView.GetTerminalSize();
            SizeLabel.Text = $"{cols}×{rows}";
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            // Debounce o resize para não enviar muitos comandos enquanto arrasta
            if (_resizeTimer == null)
            {
                _resizeTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(150)
                };
                _resizeTimer.Tick += (s, e) =>
                {
                    _resizeTimer.Stop();
                    ApplyResize();
                };
            }

            _resizeTimer.Stop();
            _resizeTimer.Start();
        }

        private void ApplyResize()
        {
            TerminalView.FitToSize();
            var (cols, rows) = TerminalView.GetTerminalSize();
            UpdateSizeLabel();

            // Notificar o servidor SSH sobre o novo tamanho
            // Usar stty silenciosamente sem echo
            if (_isConnected && _terminalService != null && cols > 0 && rows > 0)
            {
                _terminalService.Resize(cols, rows);
            }
        }
    }
}
