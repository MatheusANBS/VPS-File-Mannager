using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace VPSFileManager.Models
{
    public partial class FileItem : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string fullPath = string.Empty;

        [ObservableProperty]
        private bool isDirectory;

        [ObservableProperty]
        private long size;

        [ObservableProperty]
        private DateTime lastModified;

        [ObservableProperty]
        private string permissions = string.Empty;

        [ObservableProperty]
        private bool isSelected;

        public SymbolRegular Icon => IsDirectory ? SymbolRegular.Folder24 : GetFileIcon();

        public string FormattedSize => IsDirectory ? "--" : FormatFileSize(Size);

        public string FormattedDate => LastModified.ToString("dd/MM/yyyy HH:mm");

        private SymbolRegular GetFileIcon()
        {
            var ext = System.IO.Path.GetExtension(Name).ToLower();
            return ext switch
            {
                ".txt" or ".log" or ".md" => SymbolRegular.Document24,
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => SymbolRegular.Image24,
                ".mp3" or ".wav" or ".flac" or ".ogg" => SymbolRegular.MusicNote124,
                ".mp4" or ".avi" or ".mkv" or ".mov" => SymbolRegular.Video24,
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => SymbolRegular.FolderZip24,
                ".pdf" => SymbolRegular.DocumentPdf24,
                ".doc" or ".docx" => SymbolRegular.Document24,
                ".xls" or ".xlsx" => SymbolRegular.Document24,
                ".cs" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".h" => SymbolRegular.Code24,
                ".json" or ".xml" or ".yaml" or ".yml" => SymbolRegular.Braces24,
                ".sh" or ".bash" or ".ps1" => SymbolRegular.WindowConsole20,
                ".exe" or ".dll" => SymbolRegular.Apps24,
                ".html" or ".css" or ".scss" => SymbolRegular.Globe24,
                ".sql" => SymbolRegular.Database24,
                ".config" or ".env" => SymbolRegular.Settings24,
                _ => SymbolRegular.Document24
            };
        }

        // Cor do ícone baseada no tipo de arquivo
        public string IconColor
        {
            get
            {
                if (IsDirectory) return "#FFD54F"; // Amarelo para pastas
                
                var ext = System.IO.Path.GetExtension(Name).ToLower();
                return ext switch
                {
                    ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "#4FC3F7", // Azul claro para imagens
                    ".mp3" or ".wav" or ".flac" or ".ogg" => "#F06292", // Rosa para áudio
                    ".mp4" or ".avi" or ".mkv" or ".mov" => "#BA68C8", // Roxo para vídeo
                    ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "#FFB74D", // Laranja para arquivos compactados
                    ".pdf" => "#EF5350", // Vermelho para PDF
                    ".cs" => "#68217A", // Roxo para C#
                    ".js" or ".ts" => "#F7DF1E", // Amarelo para JS/TS
                    ".py" => "#3776AB", // Azul para Python
                    ".java" => "#ED8B00", // Laranja para Java
                    ".html" or ".css" or ".scss" => "#E44D26", // Laranja para web
                    ".json" or ".xml" or ".yaml" or ".yml" => "#81C784", // Verde para configs
                    ".sh" or ".bash" => "#4EAA25", // Verde para shell
                    ".sql" => "#00758F", // Azul escuro para SQL
                    ".md" or ".txt" => "#90A4AE", // Cinza para texto
                    _ => "#BDBDBD" // Cinza padrão
                };
            }
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }
}
