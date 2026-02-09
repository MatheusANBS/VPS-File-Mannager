using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;

namespace VPSFileManager.Views
{
    public class ShortcutInfo
    {
        public string Keys { get; set; }
        public string Action { get; set; }
        public string Description { get; set; }
        public string Context { get; set; }
    }

    public partial class HelpWindow : FluentWindow
    {
        public HelpWindow()
        {
            InitializeComponent();
            LoadShortcuts();
        }

        private void LoadShortcuts()
        {
            var shortcuts = new ObservableCollection<ShortcutInfo>
            {
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + R", 
                    Action = "Atualizar", 
                    Description = "Recarrega a lista de arquivos ao conectado",
                    Context = "Qualquer lugar"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + U", 
                    Action = "Upload", 
                    Description = "Faz upload de arquivos para o servidor",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + D", 
                    Action = "Download", 
                    Description = "Baixa arquivo selecionado para seu PC",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + N", 
                    Action = "Nova Pasta", 
                    Description = "Cria uma nova pasta no diretório atual",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Delete", 
                    Action = "Deletar", 
                    Description = "Deleta arquivo ou pasta selecionados",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "F2", 
                    Action = "Renomear", 
                    Description = "Abre diálogo de renomeação",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + H", 
                    Action = "Ir para Root", 
                    Description = "Navega para o diretório raiz (/)",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Backspace", 
                    Action = "Subir Diretório", 
                    Description = "Sobe um nível na hierarquia de pastas",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Alt + ←", 
                    Action = "Voltar", 
                    Description = "Volta para o diretório anterior (histórico)",
                    Context = "Navegação"
                },
                new ShortcutInfo 
                { 
                    Keys = "Alt + →", 
                    Action = "Avançar", 
                    Description = "Avança para o próximo diretório (histórico)",
                    Context = "Navegação"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + F", 
                    Action = "Busca Avançada", 
                    Description = "Abre o diálogo de busca recursiva com glob patterns",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + A", 
                    Action = "Selecionar Tudo", 
                    Description = "Seleciona todos os arquivos do diretório",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + L", 
                    Action = "Editar Path", 
                    Description = "Permite editar manualmente o caminho da pasta",
                    Context = "Gerenciador"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + S", 
                    Action = "Salvar", 
                    Description = "Salva arquivo no editor de código",
                    Context = "Editor"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + C", 
                    Action = "Interromper (SIGINT)", 
                    Description = "Interrompe o processo executado no terminal",
                    Context = "Terminal"
                },
                new ShortcutInfo 
                { 
                    Keys = "Ctrl + D", 
                    Action = "EOF/Logout", 
                    Description = "Encerra sessão ou envia EOF ao terminal",
                    Context = "Terminal"
                }
            };

            ShortcutsControl.ItemsSource = shortcuts;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
