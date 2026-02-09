using System.Windows;
using Wpf.Ui.Controls;

namespace VPSFileManager.Views
{
    /// <summary>
    /// Resultado do diálogo de salvar antes de fechar.
    /// </summary>
    public enum SaveDialogResult
    {
        /// <summary>Usuário quer salvar e fechar.</summary>
        Save,
        /// <summary>Usuário quer fechar sem salvar.</summary>
        DontSave,
        /// <summary>Usuário cancelou — não fechar.</summary>
        Cancel
    }

    public partial class SaveConfirmDialog : FluentWindow
    {
        public SaveDialogResult Result { get; private set; } = SaveDialogResult.Cancel;

        public SaveConfirmDialog()
        {
            InitializeComponent();
        }

        public SaveConfirmDialog(string fileName) : this()
        {
            MessageText.Text = $"Do you want to save changes to \"{fileName}\" before closing?";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Result = SaveDialogResult.Save;
            DialogResult = true;
            Close();
        }

        private void DontSave_Click(object sender, RoutedEventArgs e)
        {
            Result = SaveDialogResult.DontSave;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = SaveDialogResult.Cancel;
            DialogResult = false;
            Close();
        }
    }
}
