using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using VPSFileManager.Controls;
using VPSFileManager.ViewModels;

namespace VPSFileManager.Views
{
    public partial class EditorWindow : Wpf.Ui.Controls.FluentWindow
    {
        private EditorViewModel? _viewModel;
        private int _currentFontSize = 14;

        public EditorWindow()
        {
            InitializeComponent();
        }

        public EditorWindow(EditorViewModel viewModel) : this()
        {
            _viewModel = viewModel;
            DataContext = viewModel;

            // Interceptar fechamento da janela (botão X)
            Closing += EditorWindow_Closing;

            // Detectar linguagem baseado no nome do arquivo
            var language = MonacoEditorControl.DetectLanguage(viewModel.FileName);
            viewModel.LanguageDisplay = GetLanguageDisplayName(language);

            // Quando o Monaco estiver pronto, carregar o conteúdo
            MonacoEditor.EditorReady += async (s, e) =>
            {
                // Criar URI virtual do arquivo remoto para resolução de módulos
                var fileUri = $"file://{viewModel.FilePath.Replace('\\', '/')}";
                await MonacoEditor.SetContentWithUriAsync(viewModel.Content, language, fileUri);
            };

            // Content changes do Monaco -> ViewModel
            MonacoEditor.ContentChanged += (s, e) =>
            {
                if (e is MonacoContentChangedArgs args)
                {
                    viewModel.Content = args.Content;
                    viewModel.LineCount = args.LineCount;
                    viewModel.DirtyIndicator = args.IsDirty ? "●" : "";
                }
            };

            // Cursor position updates
            MonacoEditor.CursorChanged += (s, e) =>
            {
                if (e is MonacoCursorChangedArgs args)
                {
                    viewModel.CursorPosition = $"Ln {args.LineNumber}, Col {args.Column}";
                }
            };

            // Selection updates
            MonacoEditor.SelectionChanged += (s, e) =>
            {
                if (e is MonacoSelectionChangedArgs args)
                {
                    viewModel.SelectionInfo = args.SelectedLength > 0
                        ? $"{args.SelectedLength} selected"
                        : "";
                }
            };

            // Ctrl+S do Monaco -> Save
            MonacoEditor.SaveRequested += async (s, e) =>
            {
                await viewModel.SaveAsync();
                await MonacoEditor.MarkAsSavedAsync();
                viewModel.DirtyIndicator = "";
            };

            // Ctrl+W do Monaco -> Close
            MonacoEditor.CloseRequested += (s, e) =>
            {
                Close(); // Vai disparar o Closing event que cuida do prompt
            };

            // Se conteúdo já existe e o editor não carregou ainda, ele será
            // passado via _pendingContent no MonacoEditorControl
            if (!string.IsNullOrEmpty(viewModel.Content))
            {
                var fileUri = $"file://{viewModel.FilePath.Replace('\\', '/')}";
                _ = MonacoEditor.SetContentWithUriAsync(viewModel.Content, language, fileUri);
            }
        }

        #region Toolbar Actions

        private bool _closingHandled;

        private void EditorWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Evitar reentrada
            if (_closingHandled) return;

            if (_viewModel == null || !_viewModel.HasUnsavedChanges())
                return;

            // Cancelar o fechamento — mostrar diálogo depois que o evento terminar
            e.Cancel = true;

            Dispatcher.BeginInvoke(new Action(async () =>
            {
                var dialog = new SaveConfirmDialog(_viewModel.FileName)
                {
                    Owner = this
                };
                dialog.ShowDialog();

                switch (dialog.Result)
                {
                    case SaveDialogResult.Save:
                        await _viewModel.SaveAsync();
                        if (MonacoEditor?.IsReady == true)
                            await MonacoEditor.MarkAsSavedAsync();
                        _closingHandled = true;
                        Close();
                        break;

                    case SaveDialogResult.DontSave:
                        _closingHandled = true;
                        Close();
                        break;

                    case SaveDialogResult.Cancel:
                        break;
                }
            }));
        }

        private async void OnUndo(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.ExecuteCommandAsync("undo");
        }

        private async void OnRedo(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.ExecuteCommandAsync("redo");
        }

        private async void OnFind(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.FindAsync();
        }

        private async void OnReplace(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.FindAndReplaceAsync();
        }

        private async void OnFormat(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.FormatDocumentAsync();
        }

        private async void OnFoldAll(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.ExecuteCommandAsync("editor.foldAll");
        }

        private async void OnUnfoldAll(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.ExecuteCommandAsync("editor.unfoldAll");
        }

        private async void OnGoToLine(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.ExecuteCommandAsync("editor.action.gotoLine");
        }

        private async void OnCommandPalette(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            await MonacoEditor.ExecuteCommandAsync("editor.action.quickCommand");
        }

        private async void OnWordWrapChanged(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            if (sender is ToggleButton tb)
                await MonacoEditor.SetWordWrapAsync(tb.IsChecked == true);
        }

        private async void OnMinimapChanged(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            if (sender is ToggleButton tb)
                await MonacoEditor.SetMinimapAsync(tb.IsChecked == true);
        }

        private async void OnFontDecrease(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            if (_currentFontSize > 8)
            {
                _currentFontSize--;
                TxtFontSize.Text = _currentFontSize.ToString();
                await MonacoEditor.SetFontSizeAsync(_currentFontSize);
            }
        }

        private async void OnFontIncrease(object sender, RoutedEventArgs e)
        {
            if (MonacoEditor?.IsReady != true) return;
            if (_currentFontSize < 32)
            {
                _currentFontSize++;
                TxtFontSize.Text = _currentFontSize.ToString();
                await MonacoEditor.SetFontSizeAsync(_currentFontSize);
            }
        }

        private void OnShowShortcuts(object sender, RoutedEventArgs e)
        {
            var shortcutsWindow = new ShortcutsWindow
            {
                Owner = this
            };
            shortcutsWindow.ShowDialog();
        }

        #endregion

        /// <summary>
        /// Retorna nome de display amigável para a linguagem.
        /// </summary>
        private static string GetLanguageDisplayName(string languageId)
        {
            return languageId switch
            {
                "javascript" => "JavaScript",
                "typescript" => "TypeScript",
                "csharp" => "C#",
                "cpp" => "C++",
                "c" => "C",
                "python" => "Python",
                "java" => "Java",
                "ruby" => "Ruby",
                "php" => "PHP",
                "html" => "HTML",
                "css" => "CSS",
                "scss" => "SCSS",
                "less" => "Less",
                "json" => "JSON",
                "xml" => "XML",
                "yaml" => "YAML",
                "markdown" => "Markdown",
                "sql" => "SQL",
                "shell" => "Shell Script",
                "powershell" => "PowerShell",
                "bat" => "Batch",
                "dockerfile" => "Dockerfile",
                "rust" => "Rust",
                "go" => "Go",
                "swift" => "Swift",
                "kotlin" => "Kotlin",
                "scala" => "Scala",
                "dart" => "Dart",
                "lua" => "Lua",
                "perl" => "Perl",
                "r" => "R",
                "fsharp" => "F#",
                "vb" => "Visual Basic",
                "razor" => "Razor",
                "ini" => "INI",
                "toml" => "TOML",
                "graphql" => "GraphQL",
                "diff" => "Diff",
                "plaintext" => "Plain Text",
                _ => languageId.Length > 0
                    ? char.ToUpper(languageId[0]) + languageId.Substring(1)
                    : "Plain Text"
            };
        }
    }
}
