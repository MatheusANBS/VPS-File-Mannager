using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using SshConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace VPSFileManager.Services
{
    public class TerminalService : IDisposable
    {
        private SshClient? _sshClient;
        private ShellStream? _shellStream;
        private CancellationTokenSource? _readCts;
        private bool _isConnected;
        private static readonly System.Text.RegularExpressions.Regex AnsiRegex = new(
            @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        // Modo de dados: Raw envia tudo (ANSI incluso), Clean remove ANSI
        private bool _rawMode = false;

        public bool IsConnected => _isConnected && _shellStream != null;
        
        /// <summary>
        /// Dados recebidos do shell. No modo Raw, inclui sequências ANSI.
        /// </summary>
        public event EventHandler<string>? DataReceived;
        public event EventHandler? Disconnected;

        /// <summary>
        /// Conectar com dimensões padrão (120x30), limpa ANSI.
        /// </summary>
        public async Task ConnectAsync(Models.ConnectionInfo connectionInfo)
        {
            await ConnectAsync(connectionInfo, 120, 30, rawMode: false);
        }

        /// <summary>
        /// Conectar com dimensões e modo customizados.
        /// rawMode=true: envia dados raw com ANSI (para terminal emulador).
        /// </summary>
        public async Task ConnectAsync(Models.ConnectionInfo connectionInfo, int columns, int rows, bool rawMode = true)
        {
            _rawMode = rawMode;

            await Task.Run(() =>
            {
                SshConnectionInfo sshConnectionInfo;

                if (connectionInfo.UsePrivateKey && !string.IsNullOrEmpty(connectionInfo.PrivateKeyPath))
                {
                    var keyFile = new PrivateKeyFile(connectionInfo.PrivateKeyPath);
                    sshConnectionInfo = new SshConnectionInfo(
                        connectionInfo.Host,
                        connectionInfo.Port,
                        connectionInfo.Username,
                        new PrivateKeyAuthenticationMethod(connectionInfo.Username, keyFile));
                }
                else
                {
                    sshConnectionInfo = new SshConnectionInfo(
                        connectionInfo.Host,
                        connectionInfo.Port,
                        connectionInfo.Username,
                        new PasswordAuthenticationMethod(connectionInfo.Username, connectionInfo.Password));
                }

                _sshClient = new SshClient(sshConnectionInfo);
                _sshClient.Connect();

                // Criar shell stream com PTY e dimensões especificadas
                _shellStream = _sshClient.CreateShellStream(
                    terminalName: "xterm-256color",
                    columns: (uint)columns,
                    rows: (uint)rows,
                    width: (uint)(columns * 8),
                    height: (uint)(rows * 16),
                    bufferSize: 65536);

                _isConnected = true;

                // Iniciar leitura assíncrona
                _readCts = new CancellationTokenSource();
                StartReading(_readCts.Token);
            });
        }

        private void StartReading(CancellationToken token)
        {
            Task.Run(async () =>
            {
                var buffer = new byte[8192];
                
                while (!token.IsCancellationRequested && _shellStream != null)
                {
                    try
                    {
                        if (_shellStream.DataAvailable)
                        {
                            var bytesRead = await _shellStream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead > 0)
                            {
                                var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                if (_rawMode)
                                {
                                    // Enviar dados crus com ANSI para o terminal emulador
                                    DataReceived?.Invoke(this, text);
                                }
                                else
                                {
                                    // Limpar códigos ANSI para exibição simples
                                    var cleanText = CleanAnsiCodes(text);
                                    DataReceived?.Invoke(this, cleanText);
                                }
                            }
                        }
                        else
                        {
                            await Task.Delay(30, token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Ignorar erros de leitura
                    }
                }
            }, token);
        }

        public void SendCommand(string command)
        {
            if (_shellStream != null && _isConnected)
            {
                _shellStream.WriteLine(command);
            }
        }

        public void SendKey(string key)
        {
            if (_shellStream != null && _isConnected)
            {
                _shellStream.Write(key);
            }
        }

        public void SendCtrlC()
        {
            SendKey("\x03"); // Ctrl+C
        }

        public void SendCtrlD()
        {
            SendKey("\x04"); // Ctrl+D
        }

        public void Resize(int columns, int rows)
        {
            if (_shellStream == null || !_isConnected) return;

            try
            {
                // Tentar enviar window-change-request via reflexão no canal SSH
                // SSH.NET não expõe isso publicamente, mas o protocolo suporta
                var channelField = _shellStream.GetType().GetField("_channel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (channelField != null)
                {
                    var channel = channelField.GetValue(_shellStream);
                    if (channel != null)
                    {
                        var sendMethod = channel.GetType().GetMethod("SendPseudoTerminalRequest",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                        
                        // Se não encontrar, tentar via SendWindowChangeRequest
                        var windowChangeMethod = channel.GetType().GetMethod("SendWindowChangeRequest",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                        if (windowChangeMethod != null)
                        {
                            windowChangeMethod.Invoke(channel, new object[] { (uint)columns, (uint)rows, (uint)(columns * 8), (uint)(rows * 16) });
                            return;
                        }
                    }
                }
            }
            catch
            {
                // Reflexão falhou, usar fallback
            }

            // Fallback: usar stty silenciosamente (sem echo)
            try
            {
                _shellStream.Write($"stty cols {columns} rows {rows}\n");
            }
            catch { }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _readCts?.Cancel();
            
            _shellStream?.Close();
            _shellStream?.Dispose();
            _shellStream = null;

            _sshClient?.Disconnect();
            _sshClient?.Dispose();
            _sshClient = null;

            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private string CleanAnsiCodes(string text)
        {
            // Remove códigos de escape ANSI (cores, cursor, etc.)
            // Mantém o texto legível
            return AnsiRegex.Replace(text, string.Empty);
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
