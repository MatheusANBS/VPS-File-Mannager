using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VPSFileManager.Models;

namespace VPSFileManager.Services
{
    public interface ISftpService : IDisposable
    {
        bool IsConnected { get; }
        string CurrentDirectory { get; }

        Task ConnectAsync(ConnectionInfo connectionInfo);
        void Disconnect();
        
        Task<IEnumerable<FileItem>> ListDirectoryAsync(string path);
        Task<string> GetCurrentDirectoryAsync();
        
        Task UploadFileAsync(string localPath, string remotePath, IProgress<double>? progress = null);
        Task DownloadFileAsync(string remotePath, string localPath, IProgress<double>? progress = null);
        
        Task<string> ReadFileAsStringAsync(string remotePath);
        Task WriteFileFromStringAsync(string remotePath, string content);
        
        Task CreateDirectoryAsync(string path);
        Task DeleteFileAsync(string path);
        Task DeleteDirectoryAsync(string path);
        Task RenameAsync(string oldPath, string newPath);
        
        Task CopyFileAsync(string sourcePath, string destPath, IProgress<double>? progress = null);
        Task MoveFileAsync(string sourcePath, string destPath);
        Task<bool> FileExistsAsync(string path);
        
        Task<string> ExecuteCommandAsync(string command);
        Task<string> ExecuteCommandWithPasswordAsync(string command, string password);
        Task<bool> DirectoryExistsAsync(string path);
        Task<List<string>> GetPM2ApplicationsListAsync(string password);
        
        // Sudo operations
        Task DeleteFileWithSudoAsync(string path, string password);
        Task DeleteDirectoryWithSudoAsync(string path, string password);
        Task WriteFileWithSudoAsync(string remotePath, string content, string password);
    }
}
