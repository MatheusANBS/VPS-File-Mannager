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

        [ObservableProperty]
        private bool useSudo = true;

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
                "pm2 restart all",
                "Reinicia todos os processos gerenciados pelo PM2"
            );

            // Tarefa 2: PM2 Restart (com opções dinâmicas)
            var restartSpecificTask = new TaskCommand(
                "PM2 Restart Specific",
                "pm2 restart {0}",
                LoadPM2Apps,
                "Reinicia um processo específico do PM2"
            );

            // Tarefa 3: PM2 Status
            var pm2StatusTask = new TaskCommand(
                "PM2 Status",
                "pm2 status",
                "Exibe o status de todos os processos PM2"
            );

            // Tarefa 4: PM2 Stop All
            var stopAllTask = new TaskCommand(
                "PM2 Stop All",
                "pm2 stop all",
                "Para todos os processos gerenciados pelo PM2"
            );

            // Tarefa 5: PM2 Stop Specific
            var stopSpecificTask = new TaskCommand(
                "PM2 Stop Specific",
                "pm2 stop {0}",
                LoadPM2Apps,
                "Para um processo específico do PM2"
            );

            // Tarefa 6: PM2 Delete Specific
            var deleteSpecificTask = new TaskCommand(
                "PM2 Delete Specific",
                "pm2 delete {0}",
                LoadPM2Apps,
                "Remove um processo específico do PM2"
            );

            // Tarefa 7: PM2 Logs
            var pm2LogsTask = new TaskCommand(
                "PM2 Logs (last 50)",
                "pm2 logs --lines 50 --nostream",
                "Exibe as últimas 50 linhas de log de todos os processos"
            );

            // Tarefa 8: PM2 Logs Specific
            var pm2LogsSpecificTask = new TaskCommand(
                "PM2 Logs Specific",
                "pm2 logs {0} --lines 50 --nostream",
                LoadPM2Apps,
                "Exibe as últimas 50 linhas de log de um processo específico"
            );

            // Tarefa 9: PM2 Save
            var pm2SaveTask = new TaskCommand(
                "PM2 Save",
                "pm2 save",
                "Salva a lista atual de processos para reinício automático"
            );

            // Tarefa 10: Nginx Restart
            var nginxRestartTask = new TaskCommand(
                "Nginx Restart",
                "systemctl restart nginx",
                "Reinicia o serviço Nginx"
            );

            // Tarefa 11: Nginx Stop
            var nginxStopTask = new TaskCommand(
                "Nginx Stop",
                "systemctl stop nginx",
                "Para o serviço Nginx"
            );

            // Tarefa 12: Nginx Status
            var nginxStatusTask = new TaskCommand(
                "Nginx Status",
                "systemctl status nginx --no-pager",
                "Exibe o status do serviço Nginx"
            );

            // Tarefa 13: Nginx Test Config
            var nginxTestTask = new TaskCommand(
                "Nginx Test Config",
                "nginx -t",
                "Testa a configuração do Nginx por erros de sintaxe"
            );

            Tasks.Add(restartAllTask);
            Tasks.Add(restartSpecificTask);
            Tasks.Add(pm2StatusTask);
            Tasks.Add(stopAllTask);
            Tasks.Add(stopSpecificTask);
            Tasks.Add(deleteSpecificTask);
            Tasks.Add(pm2LogsTask);
            Tasks.Add(pm2LogsSpecificTask);
            Tasks.Add(pm2SaveTask);
            Tasks.Add(nginxRestartTask);
            Tasks.Add(nginxStopTask);
            Tasks.Add(nginxStatusTask);
            Tasks.Add(nginxTestTask);
        }

        /// <summary>
        /// Prepend sudo ao comando se UseSudo estiver ativo
        /// </summary>
        private string ApplySudo(string command)
        {
            if (UseSudo && !command.StartsWith("sudo "))
                return $"sudo {command}";
            return command;
        }

        /// <summary>
        /// Carrega a lista de aplicações PM2 via SSH
        /// </summary>
        private async Task<List<string>> LoadPM2Apps(string command)
        {
            try
            {
                // Se usando sudo e não temos senha, pedir antes de carregar opções
                if (UseSudo && string.IsNullOrEmpty(_password))
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
                var apps = await _sftpService.GetPM2ApplicationsListAsync(UseSudo ? _password : null);

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
            TaskOutput = $"Executando: {SelectedTask.Name}...\n";
            TaskOutput += UseSudo ? "🔒 Modo: sudo ativado\n\n" : "🔓 Modo: sem sudo\n\n";

            try
            {
                var command = ApplySudo(SelectedTask.GetFinalCommand());

                // Se usando sudo e não temos senha, pedir
                if (UseSudo && string.IsNullOrEmpty(_password))
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

                // Se usando sudo e temos a senha, usar ExecuteCommandWithPasswordAsync
                if (UseSudo && !string.IsNullOrEmpty(_password))
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
