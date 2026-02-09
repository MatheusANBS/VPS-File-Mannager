using System;
using System.IO;
using System.Linq;

namespace VPSFileManager.Services
{
    /// <summary>
    /// Logger de segurança para auditar ações críticas
    /// </summary>
    public class SecurityLogger
    {
        private readonly string _logPath;
        private readonly object _lockObject = new object();

        public SecurityLogger()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "VPSFileManager", "Logs");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            _logPath = Path.Combine(folder, $"security_{DateTime.Now:yyyy-MM}.log");
        }

        /// <summary>
        /// Registra evento de segurança
        /// </summary>
        public void LogEvent(string level, string action, string details, string? username = null)
        {
            try
            {
                lock (_lockObject)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var userInfo = string.IsNullOrEmpty(username) ? "N/A" : username;
                    var entry = $"[{timestamp}] {level,-8} | {userInfo,-20} | {action,-30} | {details}";

                    File.AppendAllText(_logPath, entry + Environment.NewLine);

                    // Rotação de logs se ficar muito grande (>10MB)
                    RotateLogsIfNeeded();
                }
            }
            catch
            {
                // Falha silenciosa - não queremos quebrar app por causa de log
            }
        }

        /// <summary>
        /// Registra conexão SSH
        /// </summary>
        public void LogConnection(string host, string username, bool success)
        {
            var status = success ? "SUCCESS" : "FAILED";
            LogEvent("INFO", "SSH_CONNECTION", $"{status} - {username}@{host}", username);
        }

        /// <summary>
        /// Registra execução de comando
        /// </summary>
        public void LogCommandExecution(string username, string command, bool withSudo = false)
        {
            var action = withSudo ? "SUDO_COMMAND" : "COMMAND_EXECUTION";
            var sanitizedCommand = SanitizeCommand(command);
            LogEvent("INFO", action, sanitizedCommand, username);
        }

        /// <summary>
        /// Registra operação de arquivo
        /// </summary>
        public void LogFileOperation(string username, string operation, string path, bool withSudo = false)
        {
            var action = withSudo ? $"SUDO_{operation}" : operation;
            LogEvent("INFO", action, path, username);
        }

        /// <summary>
        /// Registra tentativa de acesso negado
        /// </summary>
        public void LogAccessDenied(string username, string action, string resource)
        {
            LogEvent("WARNING", "ACCESS_DENIED", $"{action} on {resource}", username);
        }

        /// <summary>
        /// Registra erro de segurança
        /// </summary>
        public void LogSecurityError(string username, string error, string details)
        {
            LogEvent("ERROR", error, details, username);
        }

        /// <summary>
        /// Remove informações sensíveis do comando
        /// </summary>
        private string SanitizeCommand(string command)
        {
            // Esconder senhas em comandos
            if (command.Contains("sudo"))
            {
                return System.Text.RegularExpressions.Regex.Replace(
                    command,
                    @"echo\s+['\""](.*?)['\""]",
                    "echo '***'");
            }
            return command;
        }

        /// <summary>
        /// Rotaciona logs se ficarem muito grandes
        /// </summary>
        private void RotateLogsIfNeeded()
        {
            try
            {
                var fileInfo = new FileInfo(_logPath);
                if (fileInfo.Exists && fileInfo.Length > 10 * 1024 * 1024) // 10MB
                {
                    var rotatedPath = _logPath.Replace(".log", $"_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.Move(_logPath, rotatedPath);

                    // Limpar logs muito antigos (mais de 3 meses)
                    var logDir = Path.GetDirectoryName(_logPath);
                    if (!string.IsNullOrEmpty(logDir))
                    {
                        var oldLogs = Directory.GetFiles(logDir, "security_*.log")
                            .Where(f => (DateTime.Now - File.GetCreationTime(f)).TotalDays > 90);

                        foreach (var oldLog in oldLogs)
                        {
                            try { File.Delete(oldLog); } catch { }
                        }
                    }
                }
            }
            catch { /* Ignorar erros de rotação */ }
        }

        /// <summary>
        /// Obtém instância singleton do logger
        /// </summary>
        private static readonly Lazy<SecurityLogger> _instance = new Lazy<SecurityLogger>(() => new SecurityLogger());
        public static SecurityLogger Instance => _instance.Value;
    }
}
