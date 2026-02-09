using CommunityToolkit.Mvvm.ComponentModel;

namespace VPSFileManager.Models
{
    public partial class ConnectionInfo : ObservableObject
    {
        [ObservableProperty]
        private string host = string.Empty;

        [ObservableProperty]
        private int port = 22;

        [ObservableProperty]
        private string username = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private string privateKeyPath = string.Empty;

        [ObservableProperty]
        private bool usePrivateKey;

        [ObservableProperty]
        private string name = string.Empty;

        public string DisplayName => string.IsNullOrEmpty(Name) ? $"{Username}@{Host}" : Name;
    }
}
