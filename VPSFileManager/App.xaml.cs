using System;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace VPSFileManager
{
    public partial class App : Application
    {
        public App()
        {
            Dispatcher.UnhandledException += Dispatcher_UnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Erro ao iniciar: {ex.Message}\n\n{ex.StackTrace}", "Erro de Inicialização", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show($"Erro: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            e.Handled = true;
            Shutdown(1);
        }
    }
}
