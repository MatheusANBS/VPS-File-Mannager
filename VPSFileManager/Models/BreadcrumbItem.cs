using CommunityToolkit.Mvvm.ComponentModel;

namespace VPSFileManager.Models
{
    public partial class BreadcrumbItem : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string path = string.Empty;

        [ObservableProperty]
        private bool isLast = false;

        public BreadcrumbItem(string name, string path, bool isLast = false)
        {
            Name = name;
            Path = path;
            IsLast = isLast;
        }
    }
}
