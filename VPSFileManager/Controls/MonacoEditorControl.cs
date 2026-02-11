using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace VPSFileManager.Controls
{
    /// <summary>
    /// Controle WPF que encapsula o Monaco Editor via WebView2.
    /// Replica exatamente a interface de edição do VS Code.
    /// </summary>
    public class MonacoEditorControl : ContentControl
    {
        private WebView2? _webView;
        private bool _isEditorReady;
        private string? _pendingContent;
        private string? _pendingLanguage;
        private string? _pendingFileUri;
        private TaskCompletionSource<bool>? _readyTcs;

        /// <summary>Indica se o editor Monaco está pronto para uso.</summary>
        public bool IsReady => _isEditorReady;

        #region Events

        /// <summary>Conteúdo do editor mudou.</summary>
        public event EventHandler<MonacoContentChangedArgs>? ContentChanged;

        /// <summary>Posição do cursor mudou.</summary>
        public event EventHandler<MonacoCursorChangedArgs>? CursorChanged;

        /// <summary>Seleção mudou.</summary>
        public event EventHandler<MonacoSelectionChangedArgs>? SelectionChanged;

        /// <summary>Ctrl+S pressionado no editor.</summary>
        public event EventHandler? SaveRequested;

        /// <summary>Ctrl+W pressionado no editor.</summary>
        public event EventHandler? CloseRequested;

        /// <summary>Editor está pronto para uso.</summary>
        public event EventHandler? EditorReady;

        #endregion

        #region Dependency Properties

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(MonacoEditorControl),
                new PropertyMetadata(false, OnIsReadOnlyChanged));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        private static async void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MonacoEditorControl ctrl && ctrl._isEditorReady)
                await ctrl.ExecuteJsAsync($"setReadOnly({((bool)e.NewValue).ToString().ToLower()})");
        }

        #endregion

        public MonacoEditorControl()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_webView != null) return;

            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30) // #1e1e1e
            };

            Content = _webView;

            try
            {
                // Inicializar WebView2 com user data folder temporário
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VPSFileManager", "WebView2Cache");

                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userDataFolder);

                await _webView.EnsureCoreWebView2Async(env);

                // Configurar WebView2
                var settings = _webView.CoreWebView2.Settings;
                settings.IsScriptEnabled = true;
                settings.AreDefaultContextMenusEnabled = true; // Monaco tem seu próprio context menu
                settings.IsZoomControlEnabled = false;
                settings.AreDevToolsEnabled = false;
                settings.IsStatusBarEnabled = false;
                settings.AreDefaultScriptDialogsEnabled = false;
                settings.IsWebMessageEnabled = true;

                // Receber mensagens do Monaco
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // Carregar o HTML do Monaco
                var htmlPath = GetMonacoHtmlPath();
                if (File.Exists(htmlPath))
                {
                    _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
                }
                else
                {
                    // Fallback: carregar HTML embutido como string
                    var html = GetEmbeddedMonacoHtml();
                    _webView.CoreWebView2.NavigateToString(html);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MonacoEditor] Init error: {ex.Message}");
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_webView != null)
            {
                _webView.CoreWebView2?.Stop();
                _webView.Dispose();
                _webView = null;
            }
            _isEditorReady = false;
        }

        #region Monaco HTML Location

        private static string GetMonacoHtmlPath()
        {
            // Buscar no diretório do executável
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var paths = new[]
            {
                Path.Combine(exeDir, "Editor", "monaco.html"),
                Path.Combine(exeDir, "monaco.html"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Editor", "monaco.html"),
            };

            foreach (var path in paths)
            {
                if (File.Exists(path))
                    return path;
            }
            return paths[0]; // Retorna o primeiro caminho esperado
        }

        private static string GetEmbeddedMonacoHtml()
        {
            // Fallback mínimo caso o arquivo HTML não seja encontrado
            return @"<!DOCTYPE html><html><body style='background:#1e1e1e;color:#fff;display:flex;align-items:center;justify-content:center;height:100vh;font-family:sans-serif'>
                <p>Monaco Editor HTML not found. Rebuild the application.</p></body></html>";
        }

        #endregion

        #region WebMessage Handling

        private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // Usar TryGetWebMessageAsString porque o JS envia JSON.stringify (string).
                // WebMessageAsJson re-codificaria a string como JSON causando double-encoding.
                var json = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var type = root.GetProperty("type").GetString();
                var data = root.GetProperty("data");

                switch (type)
                {
                    case "ready":
                        _isEditorReady = true;
                        _readyTcs?.TrySetResult(true);
                        EditorReady?.Invoke(this, EventArgs.Empty);

                        // Se havia conteúdo pendente, aplicar agora
                        if (_pendingContent != null)
                        {
                            if (!string.IsNullOrEmpty(_pendingFileUri))
                            {
                                _ = SetContentWithUriAsync(_pendingContent, _pendingLanguage ?? "plaintext", _pendingFileUri);
                            }
                            else
                            {
                                _ = SetContentAsync(_pendingContent, _pendingLanguage ?? "plaintext");
                            }
                            _pendingContent = null;
                            _pendingLanguage = null;
                            _pendingFileUri = null;
                        }
                        break;

                    case "contentChanged":
                        ContentChanged?.Invoke(this, new MonacoContentChangedArgs
                        {
                            IsDirty = data.GetProperty("isDirty").GetBoolean(),
                            LineCount = data.GetProperty("lineCount").GetInt32(),
                            Content = data.TryGetProperty("content", out var contentProp)
                                ? contentProp.GetString() ?? "" : ""
                        });
                        break;

                    case "cursorChanged":
                        CursorChanged?.Invoke(this, new MonacoCursorChangedArgs
                        {
                            LineNumber = data.GetProperty("lineNumber").GetInt32(),
                            Column = data.GetProperty("column").GetInt32()
                        });
                        break;

                    case "selectionChanged":
                        SelectionChanged?.Invoke(this, new MonacoSelectionChangedArgs
                        {
                            StartLine = data.GetProperty("startLine").GetInt32(),
                            StartColumn = data.GetProperty("startColumn").GetInt32(),
                            EndLine = data.GetProperty("endLine").GetInt32(),
                            EndColumn = data.GetProperty("endColumn").GetInt32(),
                            SelectedLength = data.GetProperty("selectedLength").GetInt32()
                        });
                        break;

                    case "save":
                        SaveRequested?.Invoke(this, EventArgs.Empty);
                        break;

                    case "close":
                        CloseRequested?.Invoke(this, EventArgs.Empty);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MonacoEditor] Message error: {ex.Message}");
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Aguarda o editor estar pronto.
        /// </summary>
        public Task WaitForReadyAsync()
        {
            if (_isEditorReady) return Task.CompletedTask;
            _readyTcs = new TaskCompletionSource<bool>();
            return _readyTcs.Task;
        }

        /// <summary>
        /// Define o conteúdo e linguagem do editor.
        /// </summary>
        public async Task SetContentAsync(string content, string language = "plaintext")
        {
            if (!_isEditorReady)
            {
                _pendingContent = content;
                _pendingLanguage = language;
                return;
            }

            // Escapar o conteúdo para JavaScript
            var escapedContent = EscapeForJs(content);
            await ExecuteJsAsync($"setContent({escapedContent}, '{language}')");
        }

        /// <summary>
        /// Define o conteúdo com URI de arquivo virtual.
        /// A URI garante que arquivos .tsx/.jsx sejam reconhecidos corretamente pelo Monaco.
        /// </summary>
        public async Task SetContentWithUriAsync(string content, string language, string fileUri)
        {
            if (!_isEditorReady)
            {
                _pendingContent = content;
                _pendingLanguage = language;
                _pendingFileUri = fileUri;
                return;
            }

            var escapedContent = EscapeForJs(content);
            var escapedUri = EscapeForJs(fileUri);
            await ExecuteJsAsync($"setContentWithUri({escapedContent}, '{language}', {escapedUri})");
        }

        /// <summary>
        /// Obtém o conteúdo atual do editor.
        /// </summary>
        public async Task<string> GetContentAsync()
        {
            if (!_isEditorReady) return _pendingContent ?? "";
            var result = await ExecuteJsAsync("getContent()");
            // O resultado vem como JSON string, precisamos desserializar
            try
            {
                return JsonSerializer.Deserialize<string>(result) ?? "";
            }
            catch
            {
                return result?.Trim('"') ?? "";
            }
        }

        /// <summary>
        /// Muda a linguagem do editor.
        /// </summary>
        public async Task SetLanguageAsync(string language)
        {
            if (_isEditorReady)
                await ExecuteJsAsync($"setLanguage('{language}')");
        }

        /// <summary>
        /// Muda o tamanho da fonte.
        /// </summary>
        public async Task SetFontSizeAsync(int size)
        {
            if (_isEditorReady)
                await ExecuteJsAsync($"setFontSize({size})");
        }

        /// <summary>
        /// Ativa/desativa word wrap.
        /// </summary>
        public async Task SetWordWrapAsync(bool enabled)
        {
            if (_isEditorReady)
                await ExecuteJsAsync($"setWordWrap('{(enabled ? "on" : "off")}')");
        }

        /// <summary>
        /// Ativa/desativa minimap.
        /// </summary>
        public async Task SetMinimapAsync(bool enabled)
        {
            if (_isEditorReady)
                await ExecuteJsAsync($"setMinimap({enabled.ToString().ToLower()})");
        }

        /// <summary>
        /// Vai para uma linha específica.
        /// </summary>
        public async Task GoToLineAsync(int lineNumber)
        {
            if (_isEditorReady)
                await ExecuteJsAsync($"goToLine({lineNumber})");
        }

        /// <summary>
        /// Abre o diálogo de Find.
        /// </summary>
        public async Task FindAsync()
        {
            if (!_isEditorReady) return;
            await FocusEditorAsync();
            await ExecuteJsAsync("find()");
        }

        /// <summary>
        /// Abre o diálogo de Find & Replace.
        /// </summary>
        public async Task FindAndReplaceAsync()
        {
            if (!_isEditorReady) return;
            await FocusEditorAsync();
            await ExecuteJsAsync("replace()");
        }

        /// <summary>
        /// Formata o documento.
        /// </summary>
        public async Task FormatDocumentAsync()
        {
            if (!_isEditorReady) return;
            await FocusEditorAsync();
            await ExecuteJsAsync("formatDocument()");
        }

        /// <summary>
        /// Marca o conteúdo atual como salvo (limpa o dirty state).
        /// </summary>
        public async Task MarkAsSavedAsync()
        {
            if (_isEditorReady)
                await ExecuteJsAsync("markAsSaved()");
        }

        /// <summary>
        /// Verifica se há mudanças não salvas.
        /// </summary>
        public async Task<bool> IsDirtyAsync()
        {
            if (!_isEditorReady) return false;
            var result = await ExecuteJsAsync("isDirtyState()");
            return result == "true";
        }

        /// <summary>
        /// Atualiza opções do editor via JSON.
        /// </summary>
        public async Task UpdateOptionsAsync(object options)
        {
            if (!_isEditorReady) return;
            var json = JsonSerializer.Serialize(options);
            var escaped = EscapeForJs(json);
            await ExecuteJsAsync($"setEditorOptions({escaped})");
        }

        /// <summary>
        /// Dá foco ao WebView2 e ao editor Monaco.
        /// Deve ser chamado antes de executar comandos via toolbar click.
        /// </summary>
        public async Task FocusEditorAsync()
        {
            if (_webView == null || !_isEditorReady) return;
            _webView.Focus();
            await ExecuteJsAsync("editor.focus()");
        }

        /// <summary>
        /// Executa um comando/ação do Monaco Editor pelo ID.
        /// Exemplo: "undo", "redo", "editor.foldAll", "editor.action.gotoLine"
        /// </summary>
        public async Task ExecuteCommandAsync(string commandId)
        {
            if (!_isEditorReady) return;
            await FocusEditorAsync();
            await ExecuteJsAsync($"executeCommand('{commandId}')");
        }

        #endregion

        #region Helpers

        private async Task<string> ExecuteJsAsync(string script)
        {
            if (_webView?.CoreWebView2 == null) return "";
            try
            {
                return await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MonacoEditor] JS error: {ex.Message}");
                return "";
            }
        }

        private static string EscapeForJs(string value)
        {
            // Serializar como JSON string para escapar corretamente
            return JsonSerializer.Serialize(value);
        }

        #endregion

        #region Language Detection

        /// <summary>
        /// Detecta a linguagem do Monaco baseado na extensão do arquivo.
        /// Retorna o identificador de linguagem que o Monaco reconhece.
        /// </summary>
        public static string DetectLanguage(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            var name = Path.GetFileName(fileName).ToLowerInvariant();

            // Arquivos especiais (por nome)
            return name switch
            {
                "dockerfile" or "dockerfile.dev" or "dockerfile.prod" => "dockerfile",
                "makefile" or "gnumakefile" => "makefile",
                "rakefile" or "gemfile" => "ruby",
                ".gitignore" or ".dockerignore" or ".npmignore" or ".eslintignore" => "ignore",
                ".editorconfig" => "ini",
                ".env" or ".env.local" or ".env.production" => "dotenv",
                _ => ext switch
                {
                    // Web
                    ".html" or ".htm" or ".xhtml" => "html",
                    ".css" => "css",
                    ".scss" => "scss",
                    ".sass" => "sass",
                    ".less" => "less",
                    ".js" or ".mjs" or ".cjs" => "javascript",
                    ".jsx" => "javascript",
                    ".ts" or ".mts" or ".cts" => "typescript",
                    ".tsx" => "typescript",
                    ".json" or ".jsonc" or ".json5" => "json",
                    ".graphql" or ".gql" => "graphql",
                    ".vue" => "html",
                    ".svelte" => "html",

                    // .NET
                    ".cs" => "csharp",
                    ".csx" => "csharp",
                    ".vb" => "vb",
                    ".fs" or ".fsx" or ".fsi" => "fsharp",
                    ".xaml" => "xml",
                    ".csproj" or ".fsproj" or ".vbproj" or ".sln" => "xml",
                    ".razor" or ".cshtml" => "razor",

                    // JVM
                    ".java" => "java",
                    ".kt" or ".kts" => "kotlin",
                    ".scala" or ".sc" => "scala",
                    ".groovy" or ".gradle" => "groovy",
                    ".clj" or ".cljs" or ".cljc" => "clojure",

                    // Sistemas / baixo nível
                    ".c" or ".h" => "c",
                    ".cpp" or ".cxx" or ".cc" or ".hpp" or ".hxx" or ".hh" => "cpp",
                    ".rs" => "rust",
                    ".go" => "go",
                    ".swift" => "swift",
                    ".m" or ".mm" => "objective-c",
                    ".dart" => "dart",
                    ".zig" => "zig",

                    // Scripting
                    ".py" or ".pyw" or ".pyi" => "python",
                    ".rb" or ".erb" => "ruby",
                    ".php" or ".phtml" => "php",
                    ".pl" or ".pm" => "perl",
                    ".lua" => "lua",
                    ".r" or ".rmd" => "r",
                    ".jl" => "julia",
                    ".ex" or ".exs" => "elixir",
                    ".erl" or ".hrl" => "erlang",

                    // Shell
                    ".sh" or ".bash" or ".zsh" or ".fish" => "shell",
                    ".ps1" or ".psm1" or ".psd1" => "powershell",
                    ".bat" or ".cmd" => "bat",

                    // Config / Data
                    ".xml" or ".xsl" or ".xsd" or ".svg" or ".wsdl" => "xml",
                    ".yaml" or ".yml" => "yaml",
                    ".toml" => "toml",
                    ".ini" or ".cfg" or ".conf" => "ini",
                    ".properties" => "properties",
                    ".env" => "dotenv",

                    // Database
                    ".sql" => "sql",
                    ".pgsql" => "pgsql",
                    ".mysql" => "mysql",

                    // Docs
                    ".md" or ".markdown" or ".mdown" => "markdown",
                    ".rst" or ".rest" => "restructuredtext",
                    ".tex" or ".latex" => "latex",
                    ".adoc" or ".asciidoc" => "asciidoc",

                    // Outros
                    ".dockerfile" => "dockerfile",
                    ".tf" or ".tfvars" => "hcl",
                    ".proto" => "protobuf",
                    ".asm" or ".s" => "asm",
                    ".hbs" or ".handlebars" => "handlebars",
                    ".pug" or ".jade" => "pug",
                    ".twig" => "twig",
                    ".ejs" => "html",
                    ".log" => "log",
                    ".csv" or ".tsv" => "plaintext",
                    ".txt" or ".text" => "plaintext",
                    ".diff" or ".patch" => "diff",
                    _ => "plaintext"
                }
            };
        }

        #endregion
    }

    #region Event Args

    public class MonacoContentChangedArgs : EventArgs
    {
        public bool IsDirty { get; set; }
        public int LineCount { get; set; }
        public string Content { get; set; } = "";
    }

    public class MonacoCursorChangedArgs : EventArgs
    {
        public int LineNumber { get; set; }
        public int Column { get; set; }
    }

    public class MonacoSelectionChangedArgs : EventArgs
    {
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
        public int SelectedLength { get; set; }
    }

    #endregion
}
