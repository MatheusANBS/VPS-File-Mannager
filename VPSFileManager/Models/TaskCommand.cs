using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VPSFileManager.Models
{
    /// <summary>
    /// Representa um comando/tarefa que pode ser executado no servidor VPS
    /// </summary>
    public class TaskCommand : ObservableObject
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _command = string.Empty;
        private ObservableCollection<string> _availableOptions = new();
        private string _selectedOption = string.Empty;
        private bool _requiresSelection;

        /// <summary>
        /// Nome da tarefa
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Descrição da tarefa
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Comando a ser executado. Pode conter {0} como placeholder para opção selecionada
        /// </summary>
        public string Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }

        /// <summary>
        /// Lista de opções disponíveis para o comando (ex: PM2 apps)
        /// </summary>
        public ObservableCollection<string> AvailableOptions
        {
            get => _availableOptions ??= new ObservableCollection<string>();
            set => SetProperty(ref _availableOptions, value);
        }

        /// <summary>
        /// Opção selecionada
        /// </summary>
        public string SelectedOption
        {
            get => _selectedOption;
            set => SetProperty(ref _selectedOption, value);
        }

        /// <summary>
        /// Indica se a tarefa requer seleção de opção
        /// </summary>
        public bool RequiresSelection
        {
            get => _requiresSelection;
            set => SetProperty(ref _requiresSelection, value);
        }

        /// <summary>
        /// Função para carregar opções dinâmicas (ex: executar pm2 list e parse resultado)
        /// </summary>
        public Func<string, Task<List<string>>>? OptionsLoaderFunc { get; set; }

        public TaskCommand()
        {
        }

        public TaskCommand(string name, string command, string description = "")
        {
            Name = name;
            Command = command;
            Description = description;
            RequiresSelection = false;
        }

        public TaskCommand(string name, string command, Func<string, Task<List<string>>> optionsLoader, string description = "")
        {
            Name = name;
            Command = command;
            Description = description;
            OptionsLoaderFunc = optionsLoader;
            RequiresSelection = true;
        }

        /// <summary>
        /// Gera o comando final com as opções selecionadas
        /// </summary>
        public string GetFinalCommand()
        {
            if (RequiresSelection && !string.IsNullOrEmpty(SelectedOption))
            {
                return string.Format(Command, SelectedOption);
            }
            return Command;
        }
    }
}
