using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VPSFileManager.Terminal;

namespace VPSFileManager.Controls
{
    /// <summary>
    /// Controle WPF de emulador de terminal com renderização de caractere-a-caractere,
    /// suporte a cores ANSI, cursor piscante, seleção de texto e scrollback.
    /// Visual inspirado no Windows Terminal.
    /// </summary>
    public class TerminalControl : FrameworkElement
    {
        private VirtualTerminal _terminal;

        // Font
        private Typeface _typeface;
        private double _fontSize = 14;
        private double _cellWidth;
        private double _cellHeight;
        private double _baseline;

        // Cursor
        private DispatcherTimer? _cursorBlinkTimer;
        private bool _cursorBlinkState = true;

        // Scroll
        private int _scrollOffset = 0; // 0 = sem scroll (mostrando últimas linhas)

        // Seleção
        private bool _isSelecting;
        private int _selStartRow, _selStartCol;
        private int _selEndRow, _selEndCol;
        private bool _hasSelection;

        // Brush cache
        private readonly Dictionary<Color, SolidColorBrush> _brushCache = new();

        // Eventos
        public event EventHandler<string>? DataInput;

        // Propriedades
        public VirtualTerminal Terminal => _terminal;

        public TerminalControl()
        {
            _terminal = new VirtualTerminal(120, 30);
            _terminal.ScreenChanged += OnScreenChanged;
            _terminal.TitleChanged += OnTitleChanged;

            // Configurar fonte
            _typeface = CreateTypeface();
            CalculateCellSize();

            // Configurar cursor piscante
            _cursorBlinkTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(530)
            };
            _cursorBlinkTimer.Tick += (s, e) =>
            {
                _cursorBlinkState = !_cursorBlinkState;
                InvalidateVisual();
            };
            _cursorBlinkTimer.Start();

            // Propriedades do controle
            Focusable = true;
            FocusVisualStyle = null;
            ClipToBounds = true;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            Cursor = Cursors.IBeam;
        }

        /// <summary>
        /// Escreve dados no terminal (recebidos do SSH).
        /// </summary>
        public void Write(string data)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => Write(data));
                return;
            }
            _terminal.Write(data);
            // Reset cursor blink quando há atividade
            _cursorBlinkState = true;
        }

        /// <summary>
        /// Redimensiona o terminal para caber no controle.
        /// </summary>
        public void FitToSize()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            int cols = Math.Max(10, (int)(ActualWidth / _cellWidth));
            int rows = Math.Max(5, (int)(ActualHeight / _cellHeight));

            if (cols != _terminal.Columns || rows != _terminal.Rows)
            {
                _terminal.Resize(cols, rows);
                _scrollOffset = 0;
                InvalidateVisual();
            }
        }

        public (int cols, int rows) GetTerminalSize()
        {
            return (_terminal.Columns, _terminal.Rows);
        }

        #region Font & Cell Size

        private Typeface CreateTypeface()
        {
            // Tentar fontes monospace em ordem de preferência
            string[] fonts = { "Cascadia Mono", "Cascadia Code", "Consolas", "Courier New" };
            foreach (var fontName in fonts)
            {
                var family = new FontFamily(fontName);
                var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Regular, FontStretches.Normal);
                if (typeface.TryGetGlyphTypeface(out _))
                    return typeface;
            }
            return new Typeface("Courier New");
        }

        private void CalculateCellSize()
        {
            var ft = new FormattedText(
                "M",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                _typeface,
                _fontSize,
                Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            _cellWidth = Math.Ceiling(ft.WidthIncludingTrailingWhitespace);
            _cellHeight = Math.Ceiling(ft.Height);
            _baseline = ft.Baseline;

            // Usar pelo menos 1 pixel
            if (_cellWidth < 1) _cellWidth = 8;
            if (_cellHeight < 1) _cellHeight = 16;
        }

        #endregion

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            var totalWidth = ActualWidth;
            var totalHeight = ActualHeight;

            if (totalWidth <= 0 || totalHeight <= 0) return;

            // Background
            dc.DrawRectangle(GetBrush(TerminalPalette.DefaultBackground), null,
                new Rect(0, 0, totalWidth, totalHeight));

            int rows = _terminal.Rows;
            int cols = _terminal.Columns;

            double pixelsPerDip;
            try
            {
                pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            }
            catch
            {
                pixelsPerDip = 1.0;
            }

            // Renderizar cada linha
            for (int row = 0; row < rows; row++)
            {
                double y = row * _cellHeight;
                if (y > totalHeight) break;

                // Determinar de onde pegar os dados (scrollback ou tela)
                int bufferRow;
                bool isScrollback;

                if (_scrollOffset > 0)
                {
                    int scrollbackRow = _scrollOffset - row;
                    if (scrollbackRow > 0)
                    {
                        // Está no scrollback
                        isScrollback = true;
                        bufferRow = scrollbackRow - 1;
                    }
                    else
                    {
                        // Está na tela
                        isScrollback = false;
                        bufferRow = -scrollbackRow;
                    }
                }
                else
                {
                    isScrollback = false;
                    bufferRow = row;
                }

                RenderLine(dc, row, bufferRow, isScrollback, y, cols, pixelsPerDip);
            }

            // Renderizar cursor (apenas se na tela visível e sem scroll)
            if (_scrollOffset == 0 && _terminal.CursorVisible && _cursorBlinkState)
            {
                double cursorX = _terminal.CursorCol * _cellWidth;
                double cursorY = _terminal.CursorRow * _cellHeight;

                if (IsFocused)
                {
                    // Cursor bloco preenchido quando focado
                    dc.DrawRectangle(GetBrush(TerminalPalette.CursorColor), null,
                        new Rect(cursorX, cursorY, _cellWidth, _cellHeight));

                    // Desenhar o caractere sob o cursor com cor invertida
                    var cell = _terminal.GetCell(_terminal.CursorRow, _terminal.CursorCol);
                    char ch = cell.Character;
                    if (ch != '\0' && ch != ' ')
                    {
                        var ft = new FormattedText(
                            ch.ToString(),
                            CultureInfo.InvariantCulture,
                            FlowDirection.LeftToRight,
                            _typeface,
                            _fontSize,
                            GetBrush(TerminalPalette.DefaultBackground),
                            pixelsPerDip);
                        dc.DrawText(ft, new Point(cursorX, cursorY));
                    }
                }
                else
                {
                    // Cursor contorno quando desfocado
                    dc.DrawRectangle(null,
                        new Pen(GetBrush(TerminalPalette.CursorColor), 1),
                        new Rect(cursorX + 0.5, cursorY + 0.5, _cellWidth - 1, _cellHeight - 1));
                }
            }

            // Renderizar seleção
            if (_hasSelection)
            {
                RenderSelection(dc);
            }
        }

        private void RenderLine(DrawingContext dc, int displayRow, int bufferRow,
            bool isScrollback, double y, int cols, double pixelsPerDip)
        {
            // Background pass: desenhar fundos não-default
            for (int col = 0; col < cols; col++)
            {
                CharacterCell cell;
                if (isScrollback)
                {
                    var line = _terminal.GetScrollbackLine(bufferRow);
                    cell = (line != null && col < line.Length) ? line[col] : CharacterCell.Empty;
                }
                else
                {
                    cell = _terminal.GetCell(bufferRow, col);
                }

                Color bgColor;
                if ((cell.Attributes & CellAttributes.Inverse) != 0)
                    bgColor = TerminalPalette.ResolveColor(cell.Foreground, true);
                else
                    bgColor = TerminalPalette.ResolveColor(cell.Background, false);

                if (bgColor != TerminalPalette.DefaultBackground)
                {
                    dc.DrawRectangle(GetBrush(bgColor), null,
                        new Rect(col * _cellWidth, y, _cellWidth, _cellHeight));
                }
            }

            // Text pass: construir texto por spans de mesma cor
            int spanStart = 0;
            Color currentColor = GetForegroundColor(GetCellForRender(isScrollback, bufferRow, 0, cols));
            var currentAttrs = GetCellForRender(isScrollback, bufferRow, 0, cols).Attributes;

            for (int col = 0; col <= cols; col++)
            {
                Color nextColor;
                CellAttributes nextAttrs;

                if (col < cols)
                {
                    var cell = GetCellForRender(isScrollback, bufferRow, col, cols);
                    nextColor = GetForegroundColor(cell);
                    nextAttrs = cell.Attributes;
                }
                else
                {
                    nextColor = default;
                    nextAttrs = default;
                }

                // Quando a cor muda ou chegamos ao fim, desenhar o span acumulado
                if (col == cols || nextColor != currentColor || nextAttrs != currentAttrs)
                {
                    if (col > spanStart)
                    {
                        var spanText = BuildSpanText(isScrollback, bufferRow, spanStart, col, cols);
                        if (!string.IsNullOrEmpty(spanText))
                        {
                            var weight = (currentAttrs & CellAttributes.Bold) != 0
                                ? FontWeights.Bold : FontWeights.Regular;
                            var style = (currentAttrs & CellAttributes.Italic) != 0
                                ? FontStyles.Italic : FontStyles.Normal;

                            var spanTypeface = new Typeface(_typeface.FontFamily, style, weight, FontStretches.Normal);

                            var ft = new FormattedText(
                                spanText,
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                spanTypeface,
                                _fontSize,
                                GetBrush(currentColor),
                                pixelsPerDip);

                            dc.DrawText(ft, new Point(spanStart * _cellWidth, y));

                            // Underline
                            if ((currentAttrs & CellAttributes.Underline) != 0)
                            {
                                var underlineY = y + _cellHeight - 2;
                                dc.DrawLine(new Pen(GetBrush(currentColor), 1),
                                    new Point(spanStart * _cellWidth, underlineY),
                                    new Point(col * _cellWidth, underlineY));
                            }

                            // Strikethrough
                            if ((currentAttrs & CellAttributes.Strikethrough) != 0)
                            {
                                var strikeY = y + _cellHeight / 2;
                                dc.DrawLine(new Pen(GetBrush(currentColor), 1),
                                    new Point(spanStart * _cellWidth, strikeY),
                                    new Point(col * _cellWidth, strikeY));
                            }
                        }
                    }

                    spanStart = col;
                    currentColor = nextColor;
                    currentAttrs = nextAttrs;
                }
            }
        }

        private CharacterCell GetCellForRender(bool isScrollback, int bufferRow, int col, int cols)
        {
            if (isScrollback)
            {
                var line = _terminal.GetScrollbackLine(bufferRow);
                return (line != null && col < line.Length) ? line[col] : CharacterCell.Empty;
            }
            return _terminal.GetCell(bufferRow, col);
        }

        private Color GetForegroundColor(CharacterCell cell)
        {
            if ((cell.Attributes & CellAttributes.Inverse) != 0)
                return TerminalPalette.ResolveColor(cell.Background, false);

            var color = TerminalPalette.ResolveColor(cell.Foreground, true);

            // Dim: reduzir brilho
            if ((cell.Attributes & CellAttributes.Dim) != 0)
            {
                color = Color.FromRgb(
                    (byte)(color.R * 0.6),
                    (byte)(color.G * 0.6),
                    (byte)(color.B * 0.6));
            }

            return color;
        }

        private string BuildSpanText(bool isScrollback, int bufferRow, int startCol, int endCol, int cols)
        {
            var chars = new char[endCol - startCol];
            for (int c = startCol; c < endCol; c++)
            {
                CharacterCell cell;
                if (isScrollback)
                {
                    var line = _terminal.GetScrollbackLine(bufferRow);
                    cell = (line != null && c < line.Length) ? line[c] : CharacterCell.Empty;
                }
                else
                {
                    cell = _terminal.GetCell(bufferRow, c);
                }

                var ch = cell.Character;
                chars[c - startCol] = (ch == '\0') ? ' ' : ch;
            }
            return new string(chars);
        }

        private void RenderSelection(DrawingContext dc)
        {
            if (!_hasSelection) return;

            int startRow = _selStartRow, startCol = _selStartCol;
            int endRow = _selEndRow, endCol = _selEndCol;

            // Normalizar (start < end)
            if (startRow > endRow || (startRow == endRow && startCol > endCol))
            {
                (startRow, endRow) = (endRow, startRow);
                (startCol, endCol) = (endCol, startCol);
            }

            var brush = GetBrush(TerminalPalette.SelectionBackground);

            for (int row = startRow; row <= endRow && row < _terminal.Rows; row++)
            {
                int cStart = (row == startRow) ? startCol : 0;
                int cEnd = (row == endRow) ? endCol : _terminal.Columns - 1;

                dc.DrawRectangle(brush, null,
                    new Rect(cStart * _cellWidth, row * _cellHeight,
                        (cEnd - cStart + 1) * _cellWidth, _cellHeight));
            }
        }

        #endregion

        #region Input Handling

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (!string.IsNullOrEmpty(e.Text))
            {
                DataInput?.Invoke(this, e.Text);
                _scrollOffset = 0;
                e.Handled = true;
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            string? sequence = null;
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

            // Ctrl+Shift+C = Copiar
            if (ctrl && shift && e.Key == Key.C)
            {
                CopySelection();
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+V = Colar
            if (ctrl && shift && e.Key == Key.V)
            {
                PasteClipboard();
                e.Handled = true;
                return;
            }

            // Ctrl+letra = caractere de controle
            if (ctrl && !shift && !alt)
            {
                if (e.Key >= Key.A && e.Key <= Key.Z)
                {
                    char ctrlChar = (char)(e.Key - Key.A + 1);
                    sequence = ctrlChar.ToString();
                    e.Handled = true;
                }
            }

            if (sequence == null)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        sequence = "\r";
                        break;
                    case Key.Back:
                        sequence = "\x7f";
                        break;
                    case Key.Tab:
                        sequence = "\t";
                        break;
                    case Key.Escape:
                        sequence = "\x1b";
                        break;
                    case Key.Delete:
                        sequence = "\x1b[3~";
                        break;
                    case Key.Up:
                        sequence = ctrl ? "\x1b[1;5A" : "\x1b[A";
                        break;
                    case Key.Down:
                        sequence = ctrl ? "\x1b[1;5B" : "\x1b[B";
                        break;
                    case Key.Right:
                        sequence = ctrl ? "\x1b[1;5C" : "\x1b[C";
                        break;
                    case Key.Left:
                        sequence = ctrl ? "\x1b[1;5D" : "\x1b[D";
                        break;
                    case Key.Home:
                        sequence = "\x1b[H";
                        break;
                    case Key.End:
                        sequence = "\x1b[F";
                        break;
                    case Key.PageUp:
                        if (shift)
                        {
                            ScrollUp(10);
                            e.Handled = true;
                            return;
                        }
                        sequence = "\x1b[5~";
                        break;
                    case Key.PageDown:
                        if (shift)
                        {
                            ScrollDown(10);
                            e.Handled = true;
                            return;
                        }
                        sequence = "\x1b[6~";
                        break;
                    case Key.Insert:
                        sequence = "\x1b[2~";
                        break;
                    case Key.F1: sequence = "\x1bOP"; break;
                    case Key.F2: sequence = "\x1bOQ"; break;
                    case Key.F3: sequence = "\x1bOR"; break;
                    case Key.F4: sequence = "\x1bOS"; break;
                    case Key.F5: sequence = "\x1b[15~"; break;
                    case Key.F6: sequence = "\x1b[17~"; break;
                    case Key.F7: sequence = "\x1b[18~"; break;
                    case Key.F8: sequence = "\x1b[19~"; break;
                    case Key.F9: sequence = "\x1b[20~"; break;
                    case Key.F10: sequence = "\x1b[21~"; break;
                    case Key.F11: sequence = "\x1b[23~"; break;
                    case Key.F12: sequence = "\x1b[24~"; break;
                }
            }

            if (sequence != null)
            {
                DataInput?.Invoke(this, sequence);
                _scrollOffset = 0;
                ClearSelection();
                e.Handled = true;
            }
        }

        #endregion

        #region Mouse / Selection

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();

            var pos = e.GetPosition(this);
            int col = (int)(pos.X / _cellWidth);
            int row = (int)(pos.Y / _cellHeight);

            _selStartRow = row;
            _selStartCol = col;
            _selEndRow = row;
            _selEndCol = col;
            _isSelecting = true;
            _hasSelection = false;

            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isSelecting && e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(this);
                _selEndRow = Math.Min(Math.Max((int)(pos.Y / _cellHeight), 0), _terminal.Rows - 1);
                _selEndCol = Math.Min(Math.Max((int)(pos.X / _cellWidth), 0), _terminal.Columns - 1);

                _hasSelection = _selStartRow != _selEndRow || _selStartCol != _selEndCol;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            _isSelecting = false;
            ReleaseMouseCapture();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            int lines = e.Delta > 0 ? 3 : -3;

            if (lines > 0)
                ScrollUp(lines);
            else
                ScrollDown(-lines);

            e.Handled = true;
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);

            if (_hasSelection)
            {
                CopySelection();
                ClearSelection();
            }
            else
            {
                PasteClipboard();
            }
            e.Handled = true;
        }

        private void CopySelection()
        {
            if (!_hasSelection) return;

            int startRow = _selStartRow, startCol = _selStartCol;
            int endRow = _selEndRow, endCol = _selEndCol;

            if (startRow > endRow || (startRow == endRow && startCol > endCol))
            {
                (startRow, endRow) = (endRow, startRow);
                (startCol, endCol) = (endCol, startCol);
            }

            var text = _terminal.GetTextRange(startRow, startCol, endRow, endCol);
            if (!string.IsNullOrEmpty(text))
            {
                try
                {
                    Clipboard.SetText(text);
                }
                catch { }
            }
        }

        private void PasteClipboard()
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Substituir \n por \r para o terminal
                        text = text.Replace("\r\n", "\r").Replace("\n", "\r");
                        DataInput?.Invoke(this, text);
                        _scrollOffset = 0;
                    }
                }
            }
            catch { }
        }

        private void ClearSelection()
        {
            _hasSelection = false;
            InvalidateVisual();
        }

        #endregion

        #region Scroll

        private void ScrollUp(int lines)
        {
            int maxScroll = _terminal.ScrollbackCount;
            _scrollOffset = Math.Min(_scrollOffset + lines, maxScroll);
            InvalidateVisual();
        }

        private void ScrollDown(int lines)
        {
            _scrollOffset = Math.Max(_scrollOffset - lines, 0);
            InvalidateVisual();
        }

        #endregion

        #region Layout

        protected override Size MeasureOverride(Size availableSize)
        {
            // Preferimos preencher todo o espaço disponível
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // Recalcular tamanho do terminal quando o controle é redimensionado
            Dispatcher.BeginInvoke(new Action(FitToSize), DispatcherPriority.Background);
            return finalSize;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            _cursorBlinkState = true;
            _cursorBlinkTimer?.Start();
            InvalidateVisual();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            _cursorBlinkTimer?.Stop();
            _cursorBlinkState = true;
            InvalidateVisual();
        }

        #endregion

        #region Helpers

        private void OnScreenChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => InvalidateVisual()), DispatcherPriority.Render);
                return;
            }
            InvalidateVisual();
        }

        private void OnTitleChanged(string title)
        {
            // Propagar para a window pai
            var window = Window.GetWindow(this);
            if (window != null)
            {
                Dispatcher.Invoke(() =>
                {
                    window.Title = $"Terminal — {title}";
                });
            }
        }

        private SolidColorBrush GetBrush(Color color)
        {
            if (_brushCache.TryGetValue(color, out var cached))
                return cached;

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            _brushCache[color] = brush;
            return brush;
        }

        public void Dispose()
        {
            _cursorBlinkTimer?.Stop();
            _cursorBlinkTimer = null;
        }

        #endregion
    }
}
