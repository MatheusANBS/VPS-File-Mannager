using System.Windows;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.IO;
using ICSharpCode.AvalonEdit.Highlighting;
using VPSFileManager.ViewModels;

namespace VPSFileManager.Views
{
    public partial class EditorWindow : Wpf.Ui.Controls.FluentWindow
    {
        public EditorWindow()
        {
            InitializeComponent();
        }

        public EditorWindow(EditorViewModel viewModel) : this()
        {
            DataContext = viewModel;
            
            // Bind TextEditor Document ao ViewModel
            TextEditor.Text = viewModel.Content;
            TextEditor.TextChanged += (s, e) => viewModel.Content = TextEditor.Text;
            
            // Aplicar syntax highlighting baseado na extensão
            ApplySyntaxHighlighting(viewModel.FileName);
        }

        private void ApplySyntaxHighlighting(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            var fileNameLower = Path.GetFileName(fileName).ToLower();
            
            try
            {
                IHighlightingDefinition? definition = null;
                
                // Detectar tipo de arquivo especial (por nome completo)
                // Arquivos como .gitignore, .env, dockerfile, etc.
                if (fileNameLower.StartsWith(".git") || fileNameLower == ".dockerignore" || fileNameLower == ".npmignore" ||
                    fileNameLower.StartsWith(".env") || fileNameLower == "dockerfile" || fileNameLower == "makefile" ||
                    fileNameLower == "readme" || fileNameLower == "license" || fileNameLower == "changelog")
                {
                    definition = CreateGenericHighlighting("Config");
                }
                else if (!string.IsNullOrEmpty(extension))
                {
                    // Detectar por extensão
                    definition = extension switch
                    {
                        ".cs" => HighlightingManager.Instance.GetDefinition("C#"),
                        ".xml" or ".xaml" or ".config" or ".csproj" or ".props" or ".targets" => HighlightingManager.Instance.GetDefinition("XML"),
                        ".html" or ".htm" => HighlightingManager.Instance.GetDefinition("HTML"),
                        ".js" or ".mjs" or ".cjs" => HighlightingManager.Instance.GetDefinition("JavaScript"),
                        ".ts" or ".tsx" => HighlightingManager.Instance.GetDefinition("JavaScript"), // TypeScript usa JS como base
                        ".json" or ".jsonc" => HighlightingManager.Instance.GetDefinition("JavaScript"),
                        ".css" or ".scss" or ".sass" or ".less" => HighlightingManager.Instance.GetDefinition("CSS"),
                        ".py" or ".pyw" or ".pyi" => HighlightingManager.Instance.GetDefinition("Python"),
                        ".sql" => HighlightingManager.Instance.GetDefinition("SQL"),
                        ".php" => HighlightingManager.Instance.GetDefinition("PHP"),
                        ".sh" or ".bash" or ".zsh" => HighlightingManager.Instance.GetDefinition("Bash"),
                        ".java" => HighlightingManager.Instance.GetDefinition("Java"),
                        ".cpp" or ".cc" or ".cxx" or ".c" or ".h" or ".hpp" => HighlightingManager.Instance.GetDefinition("C++"),
                        ".vb" => HighlightingManager.Instance.GetDefinition("VB"),
                        ".lua" => HighlightingManager.Instance.GetDefinition("Lua"),
                        ".md" or ".markdown" => HighlightingManager.Instance.GetDefinition("MarkDown"),
                        ".txt" or ".log" or ".csv" or ".ini" or ".conf" or ".cfg" => CreateGenericHighlighting("Text"),
                        ".yaml" or ".yml" => CreateGenericHighlighting("YAML"),
                        ".toml" => CreateGenericHighlighting("TOML"),
                        ".ps1" or ".psm1" or ".psd1" => HighlightingManager.Instance.GetDefinition("PowerShell"),
                        _ => CreateGenericHighlighting("Generic")
                    };
                }
                else
                {
                    // Arquivo sem extensão - usar genérico
                    definition = CreateGenericHighlighting("Generic");
                }
                
                // Sempre aplicar highlighting (mesmo para arquivos genéricos)
                if (definition != null)
                {
                    TextEditor.SyntaxHighlighting = ApplyVSCodeColors(definition);
                }
            }
            catch
            {
                // Fallback: aplicar highlighting genérico
                TextEditor.SyntaxHighlighting = ApplyVSCodeColors(CreateGenericHighlighting("Generic"));
            }
        }
        
        private IHighlightingDefinition CreateGenericHighlighting(string name)
        {
            // Criar highlighting básico para arquivos sem definição
            var ruleSet = new HighlightingRuleSet();
            
            // VS Code Dark+ Colors
            var commentColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x6A, 0x99, 0x55)) }; // Verde
            var stringColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xCE, 0x91, 0x78)) }; // Laranja/Salmão
            var numberColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xB5, 0xCE, 0xA8)) }; // Verde claro
            var keywordColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x56, 0x9C, 0xD6)) }; // Azul
            
            // Comentários com # (Python, Bash, YAML, etc.)
            ruleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"#.*$", RegexOptions.Compiled | RegexOptions.Multiline),
                Color = commentColor
            });
            
            // Comentários com // (C-style)
            ruleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"//.*$", RegexOptions.Compiled | RegexOptions.Multiline),
                Color = commentColor
            });
            
            // Comentários de bloco /* */
            ruleSet.Spans.Add(new HighlightingSpan
            {
                StartExpression = new Regex(@"/\*", RegexOptions.Compiled),
                EndExpression = new Regex(@"\*/", RegexOptions.Compiled),
                SpanColor = commentColor
            });
            
            // Strings com aspas duplas
            ruleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"""([^""\\]|\\.)*""", RegexOptions.Compiled),
                Color = stringColor
            });
            
            // Strings com aspas simples
            ruleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"'([^'\\]|\\.)*'", RegexOptions.Compiled),
                Color = stringColor
            });
            
            // Números
            ruleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"\b\d+\.?\d*\b", RegexOptions.Compiled),
                Color = numberColor
            });
            
            // Keywords comuns (true, false, null, etc.)
            ruleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"\b(true|false|null|undefined|nil|none|True|False|None)\b", RegexOptions.Compiled),
                Color = keywordColor
            });
            
            return new CustomHighlightingDefinition(name, ruleSet);
        }

        private IHighlightingDefinition ApplyVSCodeColors(IHighlightingDefinition original)
        {
            // VS Code Dark+ Theme - Cores oficiais
            var commentColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x6A, 0x99, 0x55)) }; // Verde
            var stringColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xCE, 0x91, 0x78)) }; // Laranja/Salmão
            var keywordColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x56, 0x9C, 0xD6)) }; // Azul
            var numberColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xB5, 0xCE, 0xA8)) }; // Verde claro
            var functionColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0xDC, 0xDC, 0xAA)) }; // Amarelo
            var typeColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)) }; // Cyan/Turquesa
            var variableColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)) }; // Azul claro
            var propertyColor = new HighlightingColor { Foreground = new SimpleHighlightingBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)) }; // Azul claro

            var customDefinition = new HighlightingRuleSet();
            
            // Copiar Rules e aplicar cores do VS Code
            foreach (var rule in original.MainRuleSet.Rules)
            {
                var newRule = new HighlightingRule
                {
                    Regex = rule.Regex
                };
                
                // Mapear cores baseado no nome da regra
                var colorName = rule.Color?.Name?.ToLower() ?? "";
                newRule.Color = colorName switch
                {
                    var s when s.Contains("comment") => commentColor,
                    var s when s.Contains("string") || s.Contains("char") => stringColor,
                    var s when s.Contains("keyword") || s.Contains("modifier") => keywordColor,
                    var s when s.Contains("number") || s.Contains("digit") => numberColor,
                    var s when s.Contains("method") || s.Contains("function") => functionColor,
                    var s when s.Contains("type") || s.Contains("class") || s.Contains("interface") => typeColor,
                    var s when s.Contains("property") || s.Contains("field") => propertyColor,
                    var s when s.Contains("variable") || s.Contains("parameter") => variableColor,
                    _ => rule.Color // Manter cor original se não identificar
                };
                
                customDefinition.Rules.Add(newRule);
            }
            
            // Copiar Spans (comentários de bloco, strings multi-linha, etc.)
            foreach (var span in original.MainRuleSet.Spans)
            {
                var newSpan = new HighlightingSpan
                {
                    StartExpression = span.StartExpression,
                    EndExpression = span.EndExpression,
                    RuleSet = span.RuleSet
                };
                
                // Aplicar cor correta para spans
                var spanColorName = span.SpanColor?.Name?.ToLower() ?? "";
                var startPattern = span.StartExpression?.ToString().ToLower() ?? "";
                
                if (spanColorName.Contains("comment") || startPattern.Contains("//") || 
                    startPattern.Contains("#") || startPattern.Contains(@"/\*"))
                {
                    newSpan.SpanColor = commentColor;
                }
                else if (spanColorName.Contains("string") || startPattern.Contains("\"") || startPattern.Contains("'"))
                {
                    newSpan.SpanColor = stringColor;
                }
                else
                {
                    newSpan.SpanColor = span.SpanColor;
                }
                
                customDefinition.Spans.Add(newSpan);
            }
            
            // Adicionar regras extras para garantir detecção de comentários
            customDefinition.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"//.*$", RegexOptions.Compiled | RegexOptions.Multiline),
                Color = commentColor
            });
            
            customDefinition.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"#.*$", RegexOptions.Compiled | RegexOptions.Multiline),
                Color = commentColor
            });

            return new CustomHighlightingDefinition(original.Name, customDefinition);
        }

        private class CustomHighlightingDefinition : IHighlightingDefinition
        {
            private readonly string _name;
            private readonly HighlightingRuleSet _mainRuleSet;

            public CustomHighlightingDefinition(string name, HighlightingRuleSet mainRuleSet)
            {
                _name = name;
                _mainRuleSet = mainRuleSet;
            }

            public string Name => _name;
            public HighlightingRuleSet MainRuleSet => _mainRuleSet;
            public System.Collections.Generic.IEnumerable<HighlightingColor> NamedHighlightingColors => 
                new HighlightingColor[0];
            public System.Collections.Generic.IDictionary<string, string> Properties => 
                new System.Collections.Generic.Dictionary<string, string>();

            public HighlightingColor GetNamedColor(string name) => null;
            public HighlightingRuleSet GetNamedRuleSet(string name) => null;
        }
    }
}
