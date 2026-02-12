using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VPSFileManager.Services
{
    public class UpdateInfo
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonPropertyName("assets")]
        public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();

        /// <summary>
        /// Versão extraída do tag_name (remove o prefixo 'v' se existir)
        /// </summary>
        public Version? ParsedVersion
        {
            get
            {
                var tag = TagName.TrimStart('v', 'V');
                return Version.TryParse(tag, out var version) ? version : null;
            }
        }
    }

    public class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }

    public class UpdateService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/MatheusANBS/VPS-File-Mannager/releases/latest";
        private static readonly HttpClient _httpClient;

        static UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "VPSFileManager-UpdateChecker");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        /// <summary>
        /// Versão atual do aplicativo lida do Assembly
        /// </summary>
        public static Version CurrentVersion
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                return assembly.GetName().Version ?? new Version(0, 0, 0);
            }
        }

        /// <summary>
        /// Verifica se há uma atualização disponível no GitHub Releases
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var release = await _httpClient.GetFromJsonAsync<UpdateInfo>(GitHubApiUrl);
                if (release?.ParsedVersion == null) return null;

                var current = CurrentVersion;
                // Comparar apenas Major.Minor.Build (ignora Revision)
                var remoteVersion = release.ParsedVersion;
                var currentComparable = new Version(current.Major, current.Minor, current.Build >= 0 ? current.Build : 0);
                var remoteComparable = new Version(remoteVersion.Major, remoteVersion.Minor, remoteVersion.Build >= 0 ? remoteVersion.Build : 0);

                if (remoteComparable > currentComparable)
                {
                    return release;
                }

                return null; // Já está na versão mais recente
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Erro ao verificar atualização: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Baixa o instalador do GitHub Release
        /// </summary>
        public static async Task<string?> DownloadInstallerAsync(UpdateInfo updateInfo, IProgress<double>? progress = null)
        {
            try
            {
                // Procurar o asset do instalador (.exe)
                GitHubAsset? installerAsset = null;
                foreach (var asset in updateInfo.Assets)
                {
                    if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        installerAsset = asset;
                        break;
                    }
                }

                // Se não encontrou com "Setup", pega o primeiro .exe
                if (installerAsset == null)
                {
                    foreach (var asset in updateInfo.Assets)
                    {
                        if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            installerAsset = asset;
                            break;
                        }
                    }
                }

                if (installerAsset == null)
                {
                    Debug.WriteLine("[UpdateService] Nenhum instalador encontrado nos assets");
                    return null;
                }

                // Baixar para pasta temp
                var tempPath = Path.Combine(Path.GetTempPath(), "VPSFileManager", "Updates");
                Directory.CreateDirectory(tempPath);
                var filePath = Path.Combine(tempPath, installerAsset.Name);

                // Se já existe um download anterior, remove
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                using var response = await _httpClient.GetAsync(installerAsset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? installerAsset.Size;
                var downloadedBytes = 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        progress?.Report((double)downloadedBytes / totalBytes * 100.0);
                    }
                }

                return filePath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Erro ao baixar atualização: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Executa o instalador silenciosamente e fecha o app atual
        /// </summary>
        public static void InstallAndRestart(string installerPath)
        {
            try
            {
                // Inno Setup silent install flags
                // /VERYSILENT = sem interface
                // /SUPPRESSMSGBOXES = sem caixas de diálogo
                // /NORESTART = não reiniciar o Windows
                // /CLOSEAPPLICATIONS = fechar a aplicação se estiver rodando
                var startInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    Arguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS",
                    UseShellExecute = true
                };

                Process.Start(startInfo);

                // Fechar o app atual — o Inno Setup vai reabrir após instalar (postinstall flag)
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    System.Windows.Application.Current.Shutdown();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Erro ao executar instalador: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Formata o tamanho do download para exibição
        /// </summary>
        public static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }
    }
}
