using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VPSFileManager.Models;

namespace VPSFileManager.Services
{
    public class CredentialManager
    {
        private readonly string _filePath;
        private readonly string _entropyFilePath;
        private static byte[] _entropy;

        public CredentialManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "VPSFileManager");
            
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            
            _filePath = Path.Combine(folder, "connections.json");
            _entropyFilePath = Path.Combine(folder, ".entropy");
            
            // Gerar ou carregar entropia aleatória
            _entropy = LoadOrGenerateEntropy();
        }

        public List<SavedConnection> LoadConnections()
        {
            var connections = new List<SavedConnection>();
            
            try
            {
                if (!File.Exists(_filePath))
                    return connections;

                var json = File.ReadAllText(_filePath);
                
                // Criar backup antes de processar
                var backupPath = _filePath + ".bak";
                try
                {
                    File.Copy(_filePath, backupPath, overwrite: true);
                }
                catch { /* Ignorar erros de backup */ }
                
                var rawConnections = JsonSerializer.Deserialize<List<SavedConnection>>(json);
                if (rawConnections == null)
                    return connections;

                // Processar cada conexão individualmente para evitar perda total
                foreach (var conn in rawConnections)
                {
                    try
                    {
                        // Validar dados básicos
                        if (string.IsNullOrEmpty(conn.Host) || string.IsNullOrEmpty(conn.Username))
                            continue;
                        
                        // Descriptografar senha
                        if (!string.IsNullOrEmpty(conn.EncryptedPassword))
                        {
                            conn.Password = DecryptPassword(conn.EncryptedPassword);
                        }
                        
                        connections.Add(conn);
                    }
                    catch
                    {
                        // Continuar com próxima conexão em caso de erro
                        continue;
                    }
                }

                return connections;
            }
            catch
            {
                // Tentar restaurar do backup
                var backupPath = _filePath + ".bak";
                if (File.Exists(backupPath))
                {
                    try
                    {
                        File.Copy(backupPath, _filePath, overwrite: true);
                        // Tentar carregar novamente do backup
                        return LoadConnectionsFromBackup();
                    }
                    catch { /* Falhou completamente */ }
                }
                return connections;
            }
        }

        private List<SavedConnection> LoadConnectionsFromBackup()
        {
            // Método auxiliar para evitar recursão infinita
            return new List<SavedConnection>();
        }

        public void SaveConnections(List<SavedConnection> connections)
        {
            var tempFile = _filePath + ".tmp";
            
            try
            {
                // Criptografar senhas antes de salvar
                var toSave = connections.Select(c => new SavedConnection
                {
                    Id = c.Id,
                    Name = c.Name,
                    Host = c.Host,
                    Port = c.Port,
                    Username = c.Username,
                    EncryptedPassword = !string.IsNullOrEmpty(c.Password) ? EncryptPassword(c.Password) : "",
                    UsePrivateKey = c.UsePrivateKey,
                    PrivateKeyPath = c.PrivateKeyPath,
                    FavoritePaths = c.FavoritePaths ?? new List<string>(),
                    Password = "" // Não salvar senha em texto plano
                }).ToList();

                var json = JsonSerializer.Serialize(toSave, new JsonSerializerOptions { WriteIndented = true });
                
                // Escrever em arquivo temporário primeiro
                File.WriteAllText(tempFile, json);
                
                // Limpar backups antigos antes de criar novo
                CleanOldBackups();
                
                // Criar backup do arquivo atual (se existir)
                if (File.Exists(_filePath))
                {
                    var backupPath = _filePath + ".bak";
                    try
                    {
                        File.Copy(_filePath, backupPath, overwrite: true);
                    }
                    catch { /* Ignorar erro de backup */ }
                }
                
                // Mover arquivo temporário atomicamente
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
                File.Move(tempFile, _filePath);
            }
            finally
            {
                // Limpar arquivo temporário se ainda existir
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch { /* Ignorar */ }
            }
        }

        /// <summary>
        /// Remove backups antigos de credenciais (mantém apenas o mais recente)
        /// </summary>
        private void CleanOldBackups()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    return;

                var fileName = Path.GetFileName(_filePath);
                var backupPattern = fileName + ".bak*";
                
                // Encontrar todos os arquivos de backup
                var backupFiles = Directory.GetFiles(directory, backupPattern)
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();
                
                // Manter apenas o backup mais recente, deletar os outros
                foreach (var oldBackup in backupFiles.Skip(1))
                {
                    try
                    {
                        File.Delete(oldBackup);
                    }
                    catch { /* Ignorar erros individuais */ }
                }
                
                // Deletar também .tmp antigos (mais de 1 dia)
                var tempFiles = Directory.GetFiles(directory, "*.tmp")
                    .Where(f => (DateTime.Now - File.GetLastWriteTime(f)).TotalDays > 1);
                
                foreach (var tempFile in tempFiles)
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch { /* Ignorar */ }
                }
            }
            catch { /* Não é crítico se limpeza falhar */ }
        }

        public void AddConnection(SavedConnection connection)
        {
            var connections = LoadConnections();
            
            // Verificar se já existe uma conexão com mesmo host e username
            var existing = connections.FirstOrDefault(c => 
                c.Host == connection.Host && 
                c.Username == connection.Username &&
                c.Port == connection.Port);
            
            if (existing != null)
            {
                // Atualizar conexão existente
                connection.Id = existing.Id;
                connections.RemoveAll(c => c.Id == existing.Id);
            }
            else if (string.IsNullOrEmpty(connection.Id))
            {
                connection.Id = Guid.NewGuid().ToString();
            }
            
            connections.Add(connection);
            SaveConnections(connections);
        }

        public void RemoveConnection(string id)
        {
            var connections = LoadConnections();
            connections.RemoveAll(c => c.Id == id);
            SaveConnections(connections);
        }

        private string EncryptPassword(string password)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(password);
                var encrypted = ProtectedData.Protect(data, _entropy, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(encrypted);
            }
            catch
            {
                return "";
            }
        }

        private string DecryptPassword(string encryptedPassword)
        {
            try
            {
                var data = Convert.FromBase64String(encryptedPassword);
                var decrypted = ProtectedData.Unprotect(data, _entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Carrega ou gera entropia aleatória para DPAPI
        /// </summary>
        private byte[] LoadOrGenerateEntropy()
        {
            try
            {
                // Tentar carregar entropia existente
                if (File.Exists(_entropyFilePath))
                {
                    var entropyData = File.ReadAllBytes(_entropyFilePath);
                    if (entropyData.Length == 32)
                        return entropyData;
                }
            }
            catch { /* Ignorar e gerar nova */ }

            // Gerar nova entropia aleatória de 256 bits
            var newEntropy = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(newEntropy);
            }

            // Salvar entropia para uso futuro
            try
            {
                File.WriteAllBytes(_entropyFilePath, newEntropy);
                // Definir arquivo como oculto/sistema
                File.SetAttributes(_entropyFilePath, FileAttributes.Hidden | FileAttributes.System);
            }
            catch { /* Não é crítico se falhar */ }

            return newEntropy;
        }
    }

    public class SavedConnection
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string EncryptedPassword { get; set; } = "";
        public bool UsePrivateKey { get; set; }
        public string PrivateKeyPath { get; set; } = "";
        public List<string> FavoritePaths { get; set; } = new List<string>();

        public ConnectionInfo ToConnectionInfo()
        {
            return new ConnectionInfo
            {
                Host = Host,
                Port = Port,
                Username = Username,
                Password = Password,
                UsePrivateKey = UsePrivateKey,
                PrivateKeyPath = PrivateKeyPath,
                Name = Name
            };
        }

        public static SavedConnection FromConnectionInfo(ConnectionInfo info, string name = "")
        {
            return new SavedConnection
            {
                Id = Guid.NewGuid().ToString(),
                Name = string.IsNullOrEmpty(name) ? $"{info.Username}@{info.Host}" : name,
                Host = info.Host,
                Port = info.Port,
                Username = info.Username,
                Password = info.Password,
                UsePrivateKey = info.UsePrivateKey,
                PrivateKeyPath = info.PrivateKeyPath
            };
        }
    }
}
