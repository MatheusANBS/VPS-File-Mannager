using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Renci.SshNet;
using VPSFileManager.Models;
using SshConnectionInfo = Renci.SshNet.ConnectionInfo;

namespace VPSFileManager.Services
{
    public class SftpService : ISftpService
    {
        private SftpClient? _sftpClient;
        private SshClient? _sshClient;
        private string _currentDirectory = "/";
        private string _username = "";

        public bool IsConnected => _sftpClient?.IsConnected ?? false;
        public string CurrentDirectory => _currentDirectory;

        public async Task ConnectAsync(Models.ConnectionInfo connectionInfo)
        {
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
            
            try
            {
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

                    // Configurar timeout
                    sshConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
                    sshConnectionInfo.RetryAttempts = 2;

                    _sftpClient = new SftpClient(sshConnectionInfo);
                    _sftpClient.Connect();

                    _sshClient = new SshClient(sshConnectionInfo);
                    _sshClient.Connect();

                    _currentDirectory = _sftpClient.WorkingDirectory;
                    _username = connectionInfo.Username;
                }, cts.Token);
                
                // Log conexão bem-sucedida
                SecurityLogger.Instance.LogConnection(connectionInfo.Host, connectionInfo.Username, true);
            }
            catch
            {
                // Log conexão falhada
                SecurityLogger.Instance.LogConnection(connectionInfo.Host, connectionInfo.Username, false);
                throw;
            }
        }

        public void Disconnect()
        {
            _sftpClient?.Disconnect();
            _sftpClient?.Dispose();
            _sftpClient = null;

            _sshClient?.Disconnect();
            _sshClient?.Dispose();
            _sshClient = null;
        }

        public async Task<IEnumerable<FileItem>> ListDirectoryAsync(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            // Validar path para evitar path traversal
            ValidatePath(path);

            return await Task.Run(() =>
            {
                _currentDirectory = path;
                var files = _sftpClient.ListDirectory(path);

                return files
                    .Where(f => f.Name != ".")
                    .OrderByDescending(f => f.IsDirectory)
                    .ThenBy(f => f.Name)
                    .Select(f => new FileItem
                    {
                        Name = f.Name,
                        FullPath = f.FullName,
                        IsDirectory = f.IsDirectory,
                        Size = f.Length,
                        LastModified = f.LastWriteTime,
                        Permissions = f.IsDirectory ? "drwx" : "-rwx"
                    })
                    .ToList();
            });
        }

        public async Task<string> GetCurrentDirectoryAsync()
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            return await Task.Run(() => _sftpClient.WorkingDirectory);
        }

        public async Task UploadFileAsync(string localPath, string remotePath, IProgress<double>? progress = null)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            await Task.Run(() =>
            {
                using var fileStream = File.OpenRead(localPath);
                var fileSize = fileStream.Length;

                _sftpClient.UploadFile(fileStream, remotePath, uploaded =>
                {
                    var percentage = (double)uploaded / fileSize * 100;
                    progress?.Report(percentage);
                });
            });
        }

        public async Task DownloadFileAsync(string remotePath, string localPath, IProgress<double>? progress = null)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            await Task.Run(() =>
            {
                var fileInfo = _sftpClient.Get(remotePath);
                var fileSize = fileInfo.Length;

                using var fileStream = File.Create(localPath);
                _sftpClient.DownloadFile(remotePath, fileStream, downloaded =>
                {
                    var percentage = (double)downloaded / fileSize * 100;
                    progress?.Report(percentage);
                });
            });
        }

        public async Task CreateDirectoryAsync(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            await Task.Run(() =>
            {
                // Criar diretório recursivamente (pais primeiro)
                var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                var currentPath = path.StartsWith("/") ? "/" : "";
                
                foreach (var part in parts)
                {
                    currentPath = currentPath == "/" ? $"/{part}" : $"{currentPath}/{part}";
                    
                    try
                    {
                        if (!_sftpClient.Exists(currentPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"Creating directory: {currentPath}");
                            _sftpClient.CreateDirectory(currentPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Ignorar se já existe
                        System.Diagnostics.Debug.WriteLine($"Directory creation skipped for {currentPath}: {ex.Message}");
                    }
                }
            });
        }

        public async Task DeleteFileAsync(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            // Log operação
            SecurityLogger.Instance.LogFileOperation(_username, "DELETE_FILE", path, false);

            await Task.Run(() => _sftpClient.DeleteFile(path));
        }

        public async Task DeleteDirectoryAsync(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("SSH not connected");

            // Usar rm -rf via SSH é muito mais confiável para lidar com symlinks e permissões
            await Task.Run(() =>
            {
                var command = _sshClient.CreateCommand($"rm -rf {EscapeShellArgument(path)}");
                var result = command.Execute();
                
                if (command.ExitStatus != 0)
                {
                    var error = command.Error;
                    throw new Exception($"Failed to delete directory: {error}");
                }
            });
        }

        private void DeleteDirectoryRecursive(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            System.Diagnostics.Debug.WriteLine($"Deleting directory: {path}");
            
            try
            {
                var files = _sftpClient.ListDirectory(path);
                
                foreach (var file in files.Where(f => f.Name != "." && f.Name != ".."))
                {
                    try
                    {
                        // Symlinks aparecem como IsDirectory mas falham ao listar
                        if (file.IsDirectory)
                        {
                            // Tentar verificar se é um symlink testando se consegue listar
                            try
                            {
                                _sftpClient.ListDirectory(file.FullName);
                                System.Diagnostics.Debug.WriteLine($"  Recursing into: {file.FullName}");
                                DeleteDirectoryRecursive(file.FullName);
                            }
                            catch (Renci.SshNet.Common.SftpPathNotFoundException)
                            {
                                // É um symlink ou diretório inacessível - deletar como arquivo
                                System.Diagnostics.Debug.WriteLine($"  Deleting symlink/special: {file.FullName}");
                                _sftpClient.DeleteFile(file.FullName);
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"  Deleting file: {file.FullName}");
                            _sftpClient.DeleteFile(file.FullName);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Error deleting {file.FullName}: {ex.Message}");
                        throw new Exception($"Failed to delete '{file.Name}': {ex.Message}", ex);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"  Removing directory: {path}");
                _sftpClient.DeleteDirectory(path);
            }
            catch (Exception ex) when (ex.Message.Contains("No such file"))
            {
                throw new Exception($"Directory not found: {path}", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("Permission denied"))
            {
                throw new Exception($"Permission denied deleting: {path}", ex);
            }
        }

        public async Task RenameAsync(string oldPath, string newPath)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            await Task.Run(() => _sftpClient.RenameFile(oldPath, newPath));
        }

        public async Task<string> ReadFileAsStringAsync(string remotePath)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            return await Task.Run(() =>
            {
                using var stream = new MemoryStream();
                _sftpClient.DownloadFile(remotePath, stream);
                stream.Position = 0;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            });
        }

        public async Task WriteFileFromStringAsync(string remotePath, string content)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            await Task.Run(() =>
            {
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
                _sftpClient.UploadFile(stream, remotePath, true);
            });
        }

        public async Task CopyFileAsync(string sourcePath, string destPath, IProgress<double>? progress = null)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            await Task.Run(() =>
            {
                using var sourceStream = _sftpClient.OpenRead(sourcePath);
                var fileInfo = _sftpClient.Get(sourcePath);
                var fileSize = fileInfo.Length;
                
                using var destStream = _sftpClient.Create(destPath);
                
                var buffer = new byte[81920]; // 80KB buffer
                long totalBytesRead = 0;
                int bytesRead;
                
                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    
                    if (progress != null && fileSize > 0)
                    {
                        var percentage = (double)totalBytesRead / fileSize * 100;
                        progress.Report(percentage);
                    }
                }
            });
        }

        public async Task MoveFileAsync(string sourcePath, string destPath)
        {
            await RenameAsync(sourcePath, destPath);
        }

        public async Task<bool> FileExistsAsync(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            return await Task.Run(() => _sftpClient.Exists(path));
        }

        public async Task<bool> DirectoryExistsAsync(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            return await Task.Run(() =>
            {
                try
                {
                    return _sftpClient.Exists(path) && _sftpClient.Get(path).IsDirectory;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<string> ExecuteCommandAsync(string command)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            // Validar comando para evitar injection
            ValidateCommand(command);

            // Log execução de comando
            SecurityLogger.Instance.LogCommandExecution(_username, command, false);

            return await Task.Run(() =>
            {
                // Executar o comando no diretório atual com escape adequado
                var escapedDirectory = EscapeShellArgument(_currentDirectory);
                var fullCommand = $"cd {escapedDirectory} && {command}";
                using var cmd = _sshClient.CreateCommand(fullCommand);
                cmd.CommandTimeout = TimeSpan.FromSeconds(30);
                var result = cmd.Execute();
                
                var output = cmd.Result;
                var error = cmd.Error;
                
                if (!string.IsNullOrEmpty(error))
                {
                    return $"Error: {error}";
                }
                
                if (string.IsNullOrEmpty(output))
                {
                    return "(no output)";
                }
                
                return output.TrimEnd();
            });
        }

        public async Task<string> ExecuteCommandWithPasswordAsync(string command, string password)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            // Validar comando para evitar injection
            ValidateCommand(command);

            // Log execução de comando com sudo
            SecurityLogger.Instance.LogCommandExecution(_username, command, true);

            return await Task.Run(() =>
            {
                var escapedDirectory = EscapeShellArgument(_currentDirectory);
                
                // Remover "sudo " do comando se existir
                var commandWithoutSudo = command.StartsWith("sudo ") ? command.Substring(5) : command;
                
                // Usar método mais seguro: escrever senha em arquivo temporário protegido
                var tempScriptPath = $"/tmp/.sudo_script_{Guid.NewGuid():N}.sh";
                var scriptContent = $"#!/bin/bash\ncd {escapedDirectory}\n{commandWithoutSudo}";
                
                try
                {
                    // Upload script
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(scriptContent)))
                    {
                        _sftpClient.UploadFile(stream, tempScriptPath);
                    }
                    
                    // Executar com sudo usando -S (stdin) de forma mais segura
                    var sudoCommand = $"echo {EscapeShellArgument(password)} | sudo -S -p '' bash {EscapeShellArgument(tempScriptPath)}";
                    using var cmd = _sshClient.CreateCommand(sudoCommand);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(30);
                    var result = cmd.Execute();
                    
                    var output = cmd.Result;
                    var error = cmd.Error;
                    
                    // Limpar mensagens de sudo
                    output = Regex.Replace(output, @"\[sudo\].*", "");
                    output = output.Trim();
                    
                    if (cmd.ExitStatus != 0 && !string.IsNullOrEmpty(error))
                    {
                        return $"Error: {error}";
                    }
                    
                    return string.IsNullOrEmpty(output) ? "(no output)" : output;
                }
                finally
                {
                    // Limpar script temporário
                    try
                    {
                        if (_sftpClient.Exists(tempScriptPath))
                            _sftpClient.DeleteFile(tempScriptPath);
                    }
                    catch { /* Ignorar erros de limpeza */ }
                }
            });
        }

        private static string EscapeShellArgument(string arg)
        {
            // Escape single quotes para shell Unix
            return "'" + arg.Replace("'", "'\\''") + "'";
        }
        /// <summary>
        /// Valida comando para evitar command injection
        /// </summary>
        private static void ValidateCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("Command cannot be empty", nameof(command));

            // Lista de caracteres potencialmente perigosos
            var dangerousPatterns = new[]
            {
                ";", "|", "&", "$(", "`", ">", "<", "\n", "\r"
            };

            // Verificar padrões perigosos (exceto se for comando confiável)
            foreach (var pattern in dangerousPatterns)
            {
                if (command.Contains(pattern))
                {
                    // Permitir pipes e redirecionamentos em alguns casos confiáveis
                    if (!IsAllowedComplexCommand(command))
                    {
                        throw new SecurityException($"Command contains potentially dangerous pattern: {pattern}");
                    }
                }
            }
        }

        /// <summary>
        /// Verifica se é um comando complexo confiável
        /// </summary>
        private static bool IsAllowedComplexCommand(string command)
        {
            // Lista de comandos permitidos com pipes/redirecionamentos
            var allowedCommands = new[]
            {
                "grep", "awk", "sed", "sort", "uniq", "head", "tail",
                "find", "ls", "cat", "wc", "pm2 list", "pm2 jlist"
            };

            foreach (var allowed in allowedCommands)
            {
                if (command.TrimStart().StartsWith(allowed))
                    return true;
            }

            return false;
        }
        /// <summary>
        /// Executa pm2 list e retorna uma lista com os nomes das aplicações
        /// </summary>
        public async Task<List<string>> GetPM2ApplicationsListAsync(string password)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            return await Task.Run(() =>
            {
                try
                {
                    // Executar comando pm2 jlist (JSON format) que é mais confiável para parsing
                    var command = "sudo pm2 jlist";
                    var output = ExecuteCommandWithPasswordAsync(command, password).GetAwaiter().GetResult();

                    // Tentar parsear JSON primeiro
                    var apps = ParsePM2JsonOutput(output);
                    
                    // Se JSON falhou, tentar formato de tabela
                    if (apps.Count == 0)
                    {
                        command = "sudo pm2 list";
                        output = ExecuteCommandWithPasswordAsync(command, password).GetAwaiter().GetResult();
                        apps = ParsePM2TableOutput(output);
                    }
                    
                    return apps;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao listar aplicações PM2: {ex.Message}");
                    return new List<string>();
                }
            });
        }

        /// <summary>
        /// Parseia a saída JSON do comando pm2 jlist
        /// </summary>
        private List<string> ParsePM2JsonOutput(string output)
        {
            var apps = new List<string>();

            try
            {
                if (string.IsNullOrEmpty(output) || output.StartsWith("Error"))
                    return apps;

                // Remover códigos ANSI
                output = RemoveAnsiCodes(output);
                
                // Tentar encontrar um array JSON na saída
                var jsonStart = output.IndexOf('[');
                var jsonEnd = output.LastIndexOf(']');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    
                    // Parse manual simples de JSON para extrair nomes
                    var matches = Regex.Matches(jsonContent, @"""name""\s*:\s*""([^""]+)""");
                    foreach (Match match in matches)
                    {
                        var appName = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(appName) && !apps.Contains(appName))
                        {
                            apps.Add(appName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao parsear JSON do PM2: {ex.Message}");
            }

            return apps;
        }

        /// <summary>
        /// Parseia a saída do comando pm2 list (formato de tabela)
        /// </summary>
        private List<string> ParsePM2TableOutput(string output)
        {
            var apps = new List<string>();

            if (string.IsNullOrEmpty(output) || output.StartsWith("Error"))
                return apps;

            // Remover códigos ANSI
            output = RemoveAnsiCodes(output);
            
            var lines = output.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                // Ignorar linhas vazias, cabeçalhos e separadores
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                    
                var cleanLine = line.Trim();
                
                if (cleanLine.Contains("┤") || cleanLine.Contains("│"))
                {
                    // Tentar vários padrões de regex
                    // Padrão 1: │ id │ name │ mode │ status │ ...
                    var match = Regex.Match(cleanLine, @"[│┤]\s*\d+\s*[│┤]\s*([a-zA-Z0-9_-]+)\s*[│┤]");
                    
                    if (match.Success)
                    {
                        var appName = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(appName) && 
                            !appName.Equals("name", StringComparison.OrdinalIgnoreCase) &&
                            !apps.Contains(appName))
                        {
                            apps.Add(appName);
                        }
                    }
                }
                else if (Regex.IsMatch(cleanLine, @"^\d+\s+\|\s+\S"))
                {
                    // Padrão alternativo: sem box drawing chars
                    var match = Regex.Match(cleanLine, @"^\d+\s+\|\s+([a-zA-Z0-9_-]+)\s+\|");
                    if (match.Success)
                    {
                        var appName = match.Groups[1].Value.Trim();
                        if (!string.IsNullOrEmpty(appName) && !apps.Contains(appName))
                        {
                            apps.Add(appName);
                        }
                    }
                }
            }

            return apps;
        }

        /// <summary>
        /// Remove códigos ANSI de escape (cores e formatação) do texto
        /// </summary>
        private static string RemoveAnsiCodes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove sequências de escape ANSI: ESC [ ... m
            return Regex.Replace(text, @"\x1B\[[0-9;]*[a-zA-Z]", string.Empty);
        }

        /// <summary>
        /// Valida path para evitar path traversal
        /// </summary>
        /// <summary>
        /// Valida path para evitar path traversal
        /// </summary>
        private static void ValidatePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be empty", nameof(path));

            // Normalizar path
            var normalizedPath = path.Replace("\\", "/");

            // Verificar sequências perigosas
            if (normalizedPath.Contains("/../") || normalizedPath.EndsWith("/.."))
            {
                throw new SecurityException("Path traversal detected: ../ sequences are not allowed");
            }

            // Verificar null bytes
            if (path.Contains("\0"))
            {
                throw new SecurityException("Invalid path: null bytes detected");
            }
        }

        #region Sudo Operations

        /// <summary>
        /// Deleta um arquivo usando sudo (para arquivos protegidos)
        /// </summary>
        public async Task DeleteFileWithSudoAsync(string path, string password)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            // Validar path
            ValidatePath(path);

            await Task.Run(() =>
            {
                var escapedPath = EscapeShellArgument(path);
                var tempScriptPath = $"/tmp/.sudo_delete_{Guid.NewGuid():N}.sh";
                var scriptContent = $"#!/bin/bash\nrm -f {escapedPath}";
                
                try
                {
                    // Criar script temporário
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(scriptContent)))
                    {
                        _sftpClient.UploadFile(stream, tempScriptPath);
                    }
                    
                    // Executar com sudo
                    var sudoCommand = $"echo {EscapeShellArgument(password)} | sudo -S -p '' bash {EscapeShellArgument(tempScriptPath)}";
                    using var cmd = _sshClient.CreateCommand(sudoCommand);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(10);
                    var result = cmd.Execute();
                    
                    if (cmd.ExitStatus != 0 && !string.IsNullOrEmpty(cmd.Error))
                    {
                        var error = cmd.Error.Replace("[sudo] password for", "").Trim();
                        throw new Exception($"Failed to delete file: {error}");
                    }
                }
                finally
                {
                    // Limpar script
                    try
                    {
                        if (_sftpClient.Exists(tempScriptPath))
                            _sftpClient.DeleteFile(tempScriptPath);
                    }
                    catch { /* Ignorar */ }
                }
            });
        }

        /// <summary>
        /// Deleta um diretório usando sudo (para diretórios protegidos)
        /// </summary>
        public async Task DeleteDirectoryWithSudoAsync(string path, string password)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            // Validar path
            ValidatePath(path);

            await Task.Run(() =>
            {
                var escapedPath = EscapeShellArgument(path);
                var tempScriptPath = $"/tmp/.sudo_rmrf_{Guid.NewGuid():N}.sh";
                var scriptContent = $"#!/bin/bash\nrm -rf {escapedPath}";
                
                try
                {
                    // Criar script temporário
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(scriptContent)))
                    {
                        _sftpClient.UploadFile(stream, tempScriptPath);
                    }
                    
                    // Executar com sudo
                    var sudoCommand = $"echo {EscapeShellArgument(password)} | sudo -S -p '' bash {EscapeShellArgument(tempScriptPath)}";
                    using var cmd = _sshClient.CreateCommand(sudoCommand);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(15);
                    var result = cmd.Execute();
                    
                    if (cmd.ExitStatus != 0 && !string.IsNullOrEmpty(cmd.Error))
                    {
                        var error = cmd.Error.Replace("[sudo] password for", "").Trim();
                        throw new Exception($"Failed to delete directory: {error}");
                    }
                }
                finally
                {
                    // Limpar script
                    try
                    {
                        if (_sftpClient.Exists(tempScriptPath))
                            _sftpClient.DeleteFile(tempScriptPath);
                    }
                    catch { /* Ignorar */ }
                }
            });
        }

        /// <summary>
        /// Escreve conteúdo em um arquivo usando sudo (para arquivos protegidos)
        /// </summary>
        public async Task WriteFileWithSudoAsync(string remotePath, string content, string password)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("Not connected to server");

            // Validar path
            ValidatePath(remotePath);

            await Task.Run(() =>
            {
                // Upload do conteúdo para um arquivo temporário
                var tempFilePath = $"/tmp/.content_{Guid.NewGuid():N}.tmp";
                var tempScriptPath = $"/tmp/.sudo_write_{Guid.NewGuid():N}.sh";
                
                try
                {
                    // Upload do conteúdo para arquivo temporário
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                    {
                        _sftpClient.UploadFile(stream, tempFilePath);
                    }
                    
                    // Criar script que move o arquivo com permissões corretas
                    var escapedRemotePath = EscapeShellArgument(remotePath);
                    var escapedTempPath = EscapeShellArgument(tempFilePath);
                    var scriptContent = $"#!/bin/bash\\ncat {escapedTempPath} > {escapedRemotePath}\\nrm -f {escapedTempPath}";
                    
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(scriptContent)))
                    {
                        _sftpClient.UploadFile(stream, tempScriptPath);
                    }
                    
                    // Executar com sudo
                    var sudoCommand = $"echo {EscapeShellArgument(password)} | sudo -S -p '' bash {EscapeShellArgument(tempScriptPath)}";
                    using var cmd = _sshClient.CreateCommand(sudoCommand);
                    cmd.CommandTimeout = TimeSpan.FromSeconds(10);
                    var result = cmd.Execute();
                    
                    if (cmd.ExitStatus != 0)
                    {
                        var error = cmd.Error?.Replace("[sudo] password for", "").Trim();
                        if (!string.IsNullOrEmpty(error))
                            throw new Exception($"Failed to write file: {error}");
                    }
                }
                finally
                {
                    // Limpar arquivos temporários
                    try
                    {
                        if (_sftpClient.Exists(tempFilePath))
                            _sftpClient.DeleteFile(tempFilePath);
                        if (_sftpClient.Exists(tempScriptPath))
                            _sftpClient.DeleteFile(tempScriptPath);
                    }
                    catch { /* Ignorar */ }
                }
            });
        }

        #endregion

        public void Dispose()
        {
            Disconnect();
        }
    }
}
