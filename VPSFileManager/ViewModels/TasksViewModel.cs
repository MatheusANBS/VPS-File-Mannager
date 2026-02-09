using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VPSFileManager.Models;
using VPSFileManager.Services;
using MessageBox = System.Windows.MessageBox;

namespace VPSFileManager.ViewModels
{
    public partial class TasksViewModel : ObservableObject
    {
        private readonly ISftpService _sftpService;
        private string? _password;

        [ObservableProperty]
        private ObservableCollection<TaskCommand> tasks = new();

        [ObservableProperty]
        private TaskCommand? selectedTask;

        [ObservableProperty]
        private string taskOutput = string.Empty;

        [ObservableProperty]
        private bool isExecuting;

        public TasksViewModel(ISftpService sftpService)
        {
            _sftpService = sftpService;
            InitializeTasks();
        }

        /// <summary>
        /// Define a senha para uso em comandos sudo
        /// </summary>
        public void SetPassword(string password)
        {
            _password = password;
        }

        [RelayCommand]
        public void SelectTask(TaskCommand task)
        {
            SelectedTask = task;
        }

        private void InitializeTasks()
        {
            // Tarefa 1: PM2 Restart All
            var restartAllTask = new TaskCommand(
                "PM2 Restart All",
                "sudo pm2 restart all",
                "Reinicia todos os processos gerenciados pelo PM2"
            );

            // Tarefa 2: PM2 Restart (com opções dinâmicas)
            var restartSpecificTask = new TaskCommand(
                "PM2 Restart Specific",
                "sudo pm2 restart {0}",
                LoadPM2Apps,
                "Reinicia um processo específico do PM2"
            );

            // Tarefa 3: PM2 Status
            var pm2StatusTask = new TaskCommand(
                "PM2 Status",
                "sudo pm2 status",
                "Exibe o status de todos os processos PM2"
            );

            Tasks.Add(restartAllTask);
            Tasks.Add(restartSpecificTask);
            Tasks.Add(pm2StatusTask);
        }

        /// <summary>
        /// Carrega a lista de aplicações PM2 via SSH
        /// </summary>
        private async Task<List<string>> LoadPM2Apps(string command)
        {
            try
            {
                // Se não temos senha, pedir antes de tentar carregar
                if (string.IsNullOrEmpty(_password))
                {
                    var passwordWindow = new Views.PasswordPromptWindow
                    {
                        Owner = System.Windows.Application.Current?.MainWindow
                    };

                    if (passwordWindow.ShowDialog() == true)
                    {
                        _password = passwordWindow.Password;
                    }
                    else
                    {
                        return new List<string>();
                    }
                }

                TaskOutput += "Consultando aplicações PM2...\n";

                // Chamar o serviço para listar aplicações PM2
                var apps = await _sftpService.GetPM2ApplicationsListAsync(_password);
                
                if (apps.Count == 0)
                {
                    TaskOutput += "⚠️ Nenhuma aplicação PM2 foi encontrada.\n";
                    TaskOutput += "Verifique se o PM2 está instalado e se há aplicações rodando.\n";
                }
                else
                {
                    TaskOutput += $"✓ Encontradas {apps.Count} aplicação(ões): {string.Join(", ", apps)}\n";
                }
                
                return apps;
            }
            catch (Exception ex)
            {
                TaskOutput += $"❌ Erro ao carregar aplicações PM2: {ex.Message}\n";
                MessageBox.Show($"Erro ao carregar aplicações PM2: {ex.Message}", "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return new List<string>();
            }
        }

        [RelayCommand]
        public async Task ExecuteTask()
        {
            if (SelectedTask == null)
            {
                MessageBox.Show("Selecione uma tarefa", "Atenção", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (SelectedTask.RequiresSelection && string.IsNullOrEmpty(SelectedTask.SelectedOption))
            {
                await LoadTaskOptions();
                if (string.IsNullOrEmpty(SelectedTask.SelectedOption))
                {
                    MessageBox.Show("Selecione uma opção para executar esta tarefa", "Atenção", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }
            }

            IsExecuting = true;
            TaskOutput = $"Executando: {SelectedTask.Name}...\n\n";

            try
            {
                var command = SelectedTask.GetFinalCommand();
                
                // Se o comando contém sudo e não temos senha, pedir
                if (command.Contains("sudo") && string.IsNullOrEmpty(_password))
                {
                    var passwordWindow = new Views.PasswordPromptWindow
                    {
                        Owner = System.Windows.Application.Current?.MainWindow
                    };

                    if (passwordWindow.ShowDialog() == true)
                    {
                        _password = passwordWindow.Password;
                    }
                    else
                    {
                        TaskOutput = "Execução cancelada pelo usuário";
                        IsExecuting = false;
                        return;
                    }
                }

                string result;

                // Se o comando contém sudo e temos a senha, usar ExecuteCommandWithPasswordAsync
                if (command.Contains("sudo") && !string.IsNullOrEmpty(_password))
                {
                    result = await _sftpService.ExecuteCommandWithPasswordAsync(command, _password!);
                }
                else
                {
                    result = await _sftpService.ExecuteCommandAsync(command);
                }

                TaskOutput = $"Comando: {command}\n\n--- Saída ---\n{result}";
            }
            catch (Exception ex)
            {
                TaskOutput = $"ERRO: {ex.Message}";
                MessageBox.Show($"Erro ao executar tarefa: {ex.Message}", "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        [RelayCommand]
        public async Task LoadTaskOptions()
        {
            if (SelectedTask?.OptionsLoaderFunc == null)
                return;

            try
            {
                IsExecuting = true;
                TaskOutput = $"Carregando opções para {SelectedTask.Name}...\n";

                var options = await SelectedTask.OptionsLoaderFunc(SelectedTask.Command);
                
                SelectedTask.AvailableOptions.Clear();
                foreach (var option in options)
                {
                    SelectedTask.AvailableOptions.Add(option);
                }

                TaskOutput += $"Encontradas {options.Count} opções\n";
            }
            catch (Exception ex)
            {
                TaskOutput += $"\nERRO ao carregar opções: {ex.Message}";
                MessageBox.Show($"Erro ao carregar opções: {ex.Message}", "Erro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        [RelayCommand]
        public void ClearOutput()
        {
            TaskOutput = string.Empty;
        }
    }
}
