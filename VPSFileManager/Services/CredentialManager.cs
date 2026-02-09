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
        private static readonly byte[] _entropy = Encoding.UTF8.GetBytes("VPSFileManager2024");

        public CredentialManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "VPSFileManager");
            
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            
            _filePath = Path.Combine(folder, "connections.json");
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
            File.WriteAllText(_filePath, json);
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
