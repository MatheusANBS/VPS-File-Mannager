using System;
using System.Collections.Generic;
using System.Linq;

namespace VPSFileManager.Terminal
{
    /// <summary>
    /// Atributos visuais de uma célula do terminal.
    /// </summary>
    [Flags]
    public enum CellAttributes : byte
    {
        None = 0,
        Bold = 1,
        Dim = 2,
        Italic = 4,
        Underline = 8,
        Inverse = 16,
        Hidden = 32,
        Strikethrough = 64,
    }

    /// <summary>
    /// Representa uma cor do terminal (indexed 0-255 ou RGB).
    /// </summary>
    public readonly struct TerminalColor : IEquatable<TerminalColor>
    {
        public static readonly TerminalColor Default = new(-1, 0, 0, 0);

        public readonly int Index; // -1 = default, -2 = RGB, 0-255 = palette
        public readonly byte R, G, B;

        private TerminalColor(int index, byte r, byte g, byte b)
        {
            Index = index;
            R = r;
            G = g;
            B = b;
        }

        public static TerminalColor FromIndex(int index) => new(index, 0, 0, 0);
        public static TerminalColor FromRgb(byte r, byte g, byte b) => new(-2, r, g, b);

        public bool IsDefault => Index == -1;
        public bool IsRgb => Index == -2;

        public bool Equals(TerminalColor other) =>
            Index == other.Index && R == other.R && G == other.G && B == other.B;
        public override bool Equals(object? obj) => obj is TerminalColor c && Equals(c);
        public override int GetHashCode() => ((Index * 397) ^ R) * 397 ^ (G << 8 | B);
        public static bool operator ==(TerminalColor a, TerminalColor b) => a.Equals(b);
        public static bool operator !=(TerminalColor a, TerminalColor b) => !a.Equals(b);
    }

    /// <summary>
    /// Uma célula do grid do terminal.
    /// </summary>
    public struct CharacterCell
    {
        public char Character;
        public TerminalColor Foreground;
        public TerminalColor Background;
        public CellAttributes Attributes;

        public static CharacterCell Empty => new()
        {
            Character = ' ',
            Foreground = TerminalColor.Default,
            Background = TerminalColor.Default,
            Attributes = CellAttributes.None
        };
    }

    /// <summary>
    /// Estado do parser de sequências ANSI.
    /// </summary>
    internal enum ParserState
    {
        Ground,
        Escape,
        CharsetSelect,  // Aguardando designador de charset após ESC ( / ) / * / +
        CsiEntry,
        CsiParam,
        CsiIntermediate,
        OscString,
        DcsEntry,
    }

    /// <summary>
    /// Emulador de terminal VT100/xterm completo.
    /// Mantém o buffer de tela, processa sequências ANSI, gerencia cursor e scrollback.
    /// </summary>
    public class VirtualTerminal
    {
        private static int Clamp(int value, int min, int max) => Math.Min(Math.Max(value, min), max);

        // Dimensões
        private int _cols;
        private int _rows;

        // Buffer principal
        private CharacterCell[,] _buffer;

        // Buffer alternativo (para apps full-screen como vim, htop)
        private CharacterCell[,]? _altBuffer;
        private int _altCursorRow, _altCursorCol;
        private bool _useAltBuffer;

        // Scrollback
        private readonly List<CharacterCell[]> _scrollback = new();
        private const int MaxScrollback = 10000;

        // Cursor
        private int _cursorRow;
        private int _cursorCol;
        private bool _cursorVisible = true;

        // Atributos atuais
        private TerminalColor _currentFg = TerminalColor.Default;
        private TerminalColor _currentBg = TerminalColor.Default;
        private CellAttributes _currentAttrs = CellAttributes.None;

        // Scroll region
        private int _scrollTop;
        private int _scrollBottom;

        // Modos
        private bool _autoWrap = true;
        private bool _originMode = false;
        private bool _insertMode = false;
        private bool _lineFeedMode = false; // LF = LF+CR when true
        private bool _wrapPending = false;

        // DECCKM - Application cursor keys mode
        private bool _applicationCursorKeys = false;
        // DECKPAM/DECKPNM - Application keypad mode
        private bool _applicationKeypad = false;
        // Bracketed paste mode
        private bool _bracketedPasteMode = false;
        // Mouse tracking
        private int _mouseTrackingMode = 0; // 0=off, 9=X10, 1000=normal, 1002=button-event, 1003=any-event
        private bool _sgrMouseMode = false; // 1006 SGR extended mouse mode
        // Focus events
        private bool _focusEventsEnabled = false;
        // Last printed character for REP
        private char _lastPrintedChar = ' ';

        // Charsets (G0/G1) para line-drawing
        private bool _useLineDrawing = false;  // true quando SO ativo
        private bool _g0IsLineDrawing = false;  // G0 charset
        private bool _g1IsLineDrawing = false;  // G1 charset
        private int _activeCharset = 0;         // 0=G0 ativo, 1=G1 ativo (SO/SI)

        // Mapa de line-drawing characters (DEC Special Graphics)
        private static readonly Dictionary<char, char> LineDrawingMap = new()
        {
            {'j', '\u2518'}, // ┘
            {'k', '\u2510'}, // ┐
            {'l', '\u250C'}, // ┌
            {'m', '\u2514'}, // └
            {'n', '\u253C'}, // ┼
            {'q', '\u2500'}, // ─
            {'t', '\u251C'}, // ├
            {'u', '\u2524'}, // ┤
            {'v', '\u2534'}, // ┴
            {'w', '\u252C'}, // ┬
            {'x', '\u2502'}, // │
            {'a', '\u2592'}, // ▒ (checkerboard)
            {'f', '\u00B0'}, // degree
            {'g', '\u00B1'}, // plus-minus
            {'h', '\u2592'}, // board
            {'i', '\u2603'}, // lantern (snowman as fallback)
            {'o', '\u23BA'}, // scan line 1
            {'p', '\u23BB'}, // scan line 3
            {'r', '\u23BC'}, // scan line 7
            {'s', '\u23BD'}, // scan line 9
            {'`', '\u25C6'}, // diamond
            {'~', '\u00B7'}, // middle dot
            {',', '\u2190'}, // arrow left
            {'+', '\u2192'}, // arrow right
            {'.', '\u2193'}, // arrow down
            {'-', '\u2191'}, // arrow up
            {'0', '\u2588'}, // full block
            {'y', '\u2264'}, // less-than-or-equal
            {'z', '\u2265'}, // greater-than-or-equal
            {'{', '\u03C0'}, // pi
            {'|', '\u2260'}, // not-equal
            {'}', '\u00A3'}, // pound
            {'_', '\u00A0'}, // nbsp
        };

        // Cursor salvo (DECSC/DECRC)
        private int _savedCursorRow, _savedCursorCol;
        private TerminalColor _savedFg, _savedBg;
        private CellAttributes _savedAttrs;

        // Tab stops
        private HashSet<int> _tabStops = new();

        // Parser
        private ParserState _parserState = ParserState.Ground;
        private readonly List<int> _params = new();
        private string _intermediate = "";
        private string _oscString = "";
        private int _currentParam = -1;

        // Lock para thread-safety
        private readonly object _lock = new();

        // Eventos
        public event Action? ScreenChanged;
        public event Action<string>? TitleChanged;
        public event Action<string>? BellRang;
        /// <summary>
        /// Resposta que o terminal precisa enviar de volta ao host (para DSR, DA, etc.).
        /// </summary>
        public event Action<string>? ResponseRequested;

        public int Columns => _cols;
        public int Rows => _rows;
        public int CursorRow => _cursorRow;
        public int CursorCol => _cursorCol;
        public bool CursorVisible => _cursorVisible;
        public int ScrollbackCount => _scrollback.Count;
        public bool ApplicationCursorKeys => _applicationCursorKeys;
        public bool ApplicationKeypad => _applicationKeypad;
        public bool BracketedPasteMode => _bracketedPasteMode;
        public int MouseTrackingMode => _mouseTrackingMode;
        public bool SgrMouseMode => _sgrMouseMode;
        public bool FocusEventsEnabled => _focusEventsEnabled;

        public VirtualTerminal(int cols = 120, int rows = 30)
        {
            _cols = cols;
            _rows = rows;
            _buffer = new CharacterCell[rows, cols];
            _scrollTop = 0;
            _scrollBottom = rows - 1;

            InitializeTabStops();
            ClearScreen();
        }

        private void InitializeTabStops()
        {
            _tabStops.Clear();
            for (int i = 0; i < _cols; i += 8)
                _tabStops.Add(i);
        }

        /// <summary>
        /// Obtém uma célula do buffer de tela (coordenadas 0-based).
        /// </summary>
        public CharacterCell GetCell(int row, int col)
        {
            lock (_lock)
            {
                if (row < 0 || row >= _rows || col < 0 || col >= _cols)
                    return CharacterCell.Empty;
                return _buffer[row, col];
            }
        }

        /// <summary>
        /// Obtém uma linha do scrollback (0 = mais recente).
        /// </summary>
        public CharacterCell[]? GetScrollbackLine(int index)
        {
            lock (_lock)
            {
                if (index < 0 || index >= _scrollback.Count)
                    return null;
                return _scrollback[_scrollback.Count - 1 - index];
            }
        }

        /// <summary>
        /// Escreve dados no terminal (processando sequências VT100/ANSI).
        /// </summary>
        public void Write(string data)
        {
            lock (_lock)
            {
                foreach (char c in data)
                {
                    ProcessChar(c);
                }
            }
            ScreenChanged?.Invoke();
        }

        /// <summary>
        /// Redimensiona o terminal.
        /// </summary>
        public void Resize(int newCols, int newRows)
        {
            if (newCols < 1 || newRows < 1) return;

            lock (_lock)
            {
                var oldBuffer = _buffer;
                var oldRows = _rows;
                var oldCols = _cols;

                _cols = newCols;
                _rows = newRows;
                _buffer = new CharacterCell[newRows, newCols];

                // Limpar novo buffer
                for (int r = 0; r < newRows; r++)
                    for (int c = 0; c < newCols; c++)
                        _buffer[r, c] = CharacterCell.Empty;

                // Copiar conteúdo antigo
                var copyRows = Math.Min(oldRows, newRows);
                var copyCols = Math.Min(oldCols, newCols);
                for (int r = 0; r < copyRows; r++)
                    for (int c = 0; c < copyCols; c++)
                        _buffer[r, c] = oldBuffer[r, c];

                // Ajustar cursor
                _cursorRow = Math.Min(_cursorRow, newRows - 1);
                _cursorCol = Math.Min(_cursorCol, newCols - 1);

                // Ajustar scroll region
                _scrollTop = 0;
                _scrollBottom = newRows - 1;

                // Recriar alt buffer se necessário
                if (_useAltBuffer)
                {
                    _altBuffer = new CharacterCell[newRows, newCols];
                    for (int r = 0; r < newRows; r++)
                        for (int c = 0; c < newCols; c++)
                            _altBuffer[r, c] = CharacterCell.Empty;
                }

                InitializeTabStops();
            }
            ScreenChanged?.Invoke();
        }

        /// <summary>
        /// Obtém todo o conteúdo visível como texto (para seleção/cópia).
        /// </summary>
        public string GetVisibleText()
        {
            lock (_lock)
            {
                var sb = new System.Text.StringBuilder();
                for (int r = 0; r < _rows; r++)
                {
                    for (int c = 0; c < _cols; c++)
                    {
                        sb.Append(_buffer[r, c].Character == '\0' ? ' ' : _buffer[r, c].Character);
                    }
                    if (r < _rows - 1)
                        sb.AppendLine();
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Obtém texto de uma região específica.
        /// </summary>
        public string GetTextRange(int startRow, int startCol, int endRow, int endCol)
        {
            lock (_lock)
            {
                var sb = new System.Text.StringBuilder();
                for (int r = startRow; r <= endRow && r < _rows; r++)
                {
                    int cStart = (r == startRow) ? startCol : 0;
                    int cEnd = (r == endRow) ? endCol : _cols - 1;
                    for (int c = cStart; c <= cEnd && c < _cols; c++)
                    {
                        sb.Append(_buffer[r, c].Character == '\0' ? ' ' : _buffer[r, c].Character);
                    }
                    if (r < endRow)
                        sb.AppendLine();
                }
                return sb.ToString().TrimEnd();
            }
        }

        #region Parser State Machine

        private void ProcessChar(char c)
        {
            switch (_parserState)
            {
                case ParserState.Ground:
                    ProcessGroundChar(c);
                    break;
                case ParserState.Escape:
                    ProcessEscapeChar(c);
                    break;
                case ParserState.CsiEntry:
                case ParserState.CsiParam:
                case ParserState.CsiIntermediate:
                    ProcessCsiChar(c);
                    break;
                case ParserState.CharsetSelect:
                    // Consumir o caractere designador e configurar o charset
                    // O _intermediate guarda qual charset group (armazenado antes)
                    if (_intermediate == "(")
                    {
                        _g0IsLineDrawing = (c == '0');
                        if (_activeCharset == 0) _useLineDrawing = _g0IsLineDrawing;
                    }
                    else if (_intermediate == ")")
                    {
                        _g1IsLineDrawing = (c == '0');
                        if (_activeCharset == 1) _useLineDrawing = _g1IsLineDrawing;
                    }
                    _parserState = ParserState.Ground;
                    break;
                case ParserState.OscString:
                    ProcessOscChar(c);
                    break;
                case ParserState.DcsEntry:
                    // Ignorar DCS por enquanto
                    if (c == '\x1b')
                        _parserState = ParserState.Escape; // ESC+\ = ST
                    else if (c == '\x9c')
                        _parserState = ParserState.Ground;
                    break;
            }
        }

        private void ProcessGroundChar(char c)
        {
            switch (c)
            {
                case '\x1b': // ESC
                    _parserState = ParserState.Escape;
                    _intermediate = "";
                    _params.Clear();
                    _currentParam = -1;
                    break;
                case '\r': // CR
                    _cursorCol = 0;
                    _wrapPending = false;
                    break;
                case '\n': // LF
                case '\x0b': // VT
                case '\x0c': // FF
                    LineFeed();
                    if (_lineFeedMode) _cursorCol = 0;
                    break;
                case '\b': // BS
                    if (_cursorCol > 0) _cursorCol--;
                    _wrapPending = false;
                    break;
                case '\t': // TAB
                    AdvanceTab();
                    break;
                case '\a': // BEL
                    BellRang?.Invoke("bell");
                    break;
                case '\x0e': // SO (shift out) - ativar G1 charset
                    _activeCharset = 1;
                    _useLineDrawing = _g1IsLineDrawing;
                    break;
                case '\x0f': // SI (shift in) - voltar para G0 charset
                    _activeCharset = 0;
                    _useLineDrawing = _g0IsLineDrawing;
                    break;
                default:
                    if (c >= ' ') // Caractere imprimível
                    {
                        PutChar(c);
                    }
                    break;
            }
        }

        private void ProcessEscapeChar(char c)
        {
            switch (c)
            {
                case '[': // CSI
                    _parserState = ParserState.CsiEntry;
                    _params.Clear();
                    _currentParam = -1;
                    _intermediate = "";
                    break;
                case ']': // OSC
                    _parserState = ParserState.OscString;
                    _oscString = "";
                    break;
                case 'P': // DCS
                    _parserState = ParserState.DcsEntry;
                    break;
                case '7': // DECSC - Save cursor
                    SaveCursor();
                    _parserState = ParserState.Ground;
                    break;
                case '8': // DECRC - Restore cursor
                    RestoreCursor();
                    _parserState = ParserState.Ground;
                    break;
                case 'D': // IND - Index (move down)
                    LineFeed();
                    _parserState = ParserState.Ground;
                    break;
                case 'E': // NEL - Next line
                    _cursorCol = 0;
                    LineFeed();
                    _parserState = ParserState.Ground;
                    break;
                case 'M': // RI - Reverse index (move up)
                    ReverseIndex();
                    _parserState = ParserState.Ground;
                    break;
                case 'H': // HTS - Set tab stop
                    _tabStops.Add(_cursorCol);
                    _parserState = ParserState.Ground;
                    break;
                case 'c': // RIS - Full reset
                    FullReset();
                    _parserState = ParserState.Ground;
                    break;
                case '(': // G0 charset
                case ')': // G1 charset
                case '*': // G2 charset
                case '+': // G3 charset
                    // Próximo char define o charset designator (ex: B=ASCII, 0=line drawing)
                    _intermediate = c.ToString();
                    _parserState = ParserState.CharsetSelect;
                    break;
                case '=': // DECKPAM - Application Keypad Mode
                    _applicationKeypad = true;
                    _parserState = ParserState.Ground;
                    break;
                case '>': // DECKPNM - Normal Keypad Mode
                    _applicationKeypad = false;
                    _parserState = ParserState.Ground;
                    break;
                case '\\': // ST (string terminator)
                    _parserState = ParserState.Ground;
                    break;
                default:
                    _parserState = ParserState.Ground;
                    break;
            }
        }

        private void ProcessCsiChar(char c)
        {
            // C0 controls devem ser executados mesmo dentro de sequências CSI
            // (exceto ESC que aborta a sequência e inicia uma nova)
            if (c == '\x1b')
            {
                // ESC dentro de CSI: abortar CSI e iniciar nova sequência de escape
                _parserState = ParserState.Escape;
                _intermediate = "";
                _params.Clear();
                _currentParam = -1;
                return;
            }
            if (c < ' ' && c != '\x1b')
            {
                // Executar C0 controls (BS, CR, LF, BEL, etc.) sem sair do CSI
                ProcessGroundChar(c);
                return;
            }

            // Caracteres de parâmetro privados (?, >, =) no início da sequência CSI
            if ((c == '?' || c == '>' || c == '=') && _params.Count == 0 && _currentParam == -1)
            {
                _intermediate = c.ToString();
                _parserState = ParserState.CsiParam;
                return;
            }

            if (c >= '0' && c <= '9')
            {
                if (_currentParam == -1) _currentParam = 0;
                _currentParam = _currentParam * 10 + (c - '0');
                _parserState = ParserState.CsiParam;
                return;
            }

            if (c == ';')
            {
                _params.Add(_currentParam == -1 ? 0 : _currentParam);
                _currentParam = -1;
                _parserState = ParserState.CsiParam;
                return;
            }

            if (c == ':') // Sub-parameters separator (used in SGR colon notation)
            {
                // Tratar como ; para simplificar
                _params.Add(_currentParam == -1 ? 0 : _currentParam);
                _currentParam = -1;
                _parserState = ParserState.CsiParam;
                return;
            }

            if (c >= 0x20 && c <= 0x2f) // Intermediate bytes (space, !, ", #, $, etc.)
            {
                _intermediate += c;
                _parserState = ParserState.CsiIntermediate;
                return;
            }

            // Final byte (0x40-0x7E) — executar comando
            if (_currentParam != -1)
                _params.Add(_currentParam);

            ExecuteCsi(c);
            _parserState = ParserState.Ground;
        }

        private void ProcessOscChar(char c)
        {
            if (c == '\a' || c == '\x9c') // BEL or ST
            {
                ExecuteOsc();
                _parserState = ParserState.Ground;
                return;
            }
            if (c == '\x1b') // Pode ser ESC+\ (ST)
            {
                ExecuteOsc();
                _parserState = ParserState.Escape;
                return;
            }
            _oscString += c;
        }

        #endregion

        #region CSI Command Execution

        private void ExecuteCsi(char finalChar)
        {
            int p0 = _params.Count > 0 ? _params[0] : 0;
            int p1 = _params.Count > 1 ? _params[1] : 0;

            if (_intermediate == "?")
            {
                ExecuteDecPrivateMode(finalChar);
                return;
            }
            if (_intermediate == "!")
            {
                if (finalChar == 'p') // DECSTR - Soft reset
                    SoftReset();
                return;
            }

            switch (finalChar)
            {
                case 'A': // CUU - Cursor up
                    _cursorRow = Math.Max(_scrollTop, _cursorRow - Math.Max(1, p0));
                    _wrapPending = false;
                    break;

                case 'B': // CUD - Cursor down
                    _cursorRow = Math.Min(_scrollBottom, _cursorRow + Math.Max(1, p0));
                    _wrapPending = false;
                    break;

                case 'C': // CUF - Cursor forward
                    _cursorCol = Math.Min(_cols - 1, _cursorCol + Math.Max(1, p0));
                    _wrapPending = false;
                    break;

                case 'D': // CUB - Cursor back
                    _cursorCol = Math.Max(0, _cursorCol - Math.Max(1, p0));
                    _wrapPending = false;
                    break;

                case 'E': // CNL - Cursor next line
                    _cursorCol = 0;
                    _cursorRow = Math.Min(_scrollBottom, _cursorRow + Math.Max(1, p0));
                    _wrapPending = false;
                    break;

                case 'F': // CPL - Cursor previous line
                    _cursorCol = 0;
                    _cursorRow = Math.Max(_scrollTop, _cursorRow - Math.Max(1, p0));
                    _wrapPending = false;
                    break;

                case 'G': // CHA - Cursor horizontal absolute
                    _cursorCol = Clamp((p0 > 0 ? p0 : 1) - 1, 0, _cols - 1);
                    _wrapPending = false;
                    break;

                case 'H': // CUP - Cursor position
                case 'f': // HVP - same
                    {
                        int row = Clamp((p0 > 0 ? p0 : 1) - 1, 0, _rows - 1);
                        int col = Clamp((p1 > 0 ? p1 : 1) - 1, 0, _cols - 1);
                        if (_originMode)
                            row = Clamp(row + _scrollTop, _scrollTop, _scrollBottom);
                        _cursorRow = row;
                        _cursorCol = col;
                        _wrapPending = false;
                    }
                    break;

                case 'J': // ED - Erase in display
                    EraseInDisplay(p0);
                    break;

                case 'K': // EL - Erase in line
                    EraseInLine(p0);
                    break;

                case 'L': // IL - Insert lines
                    InsertLines(Math.Max(1, p0));
                    break;

                case 'M': // DL - Delete lines
                    DeleteLines(Math.Max(1, p0));
                    break;

                case 'P': // DCH - Delete characters
                    DeleteChars(Math.Max(1, p0));
                    break;

                case 'S': // SU - Scroll up
                    ScrollUp(Math.Max(1, p0));
                    break;

                case 'T': // SD - Scroll down
                    ScrollDown(Math.Max(1, p0));
                    break;

                case 'X': // ECH - Erase characters
                    EraseChars(Math.Max(1, p0));
                    break;

                case '@': // ICH - Insert characters
                    InsertChars(Math.Max(1, p0));
                    break;

                case 'd': // VPA - Vertical position absolute
                    _cursorRow = Clamp((p0 > 0 ? p0 : 1) - 1, 0, _rows - 1);
                    _wrapPending = false;
                    break;

                case 'g': // TBC - Tab clear
                    if (p0 == 0) _tabStops.Remove(_cursorCol);
                    else if (p0 == 3) _tabStops.Clear();
                    break;

                case 'h': // SM - Set mode
                    if (p0 == 4) _insertMode = true;
                    if (p0 == 20) _lineFeedMode = true;
                    break;

                case 'l': // RM - Reset mode
                    if (p0 == 4) _insertMode = false;
                    if (p0 == 20) _lineFeedMode = false;
                    break;

                case 'm': // SGR - Select Graphic Rendition
                    ExecuteSgr();
                    break;

                case 'n': // DSR - Device status report
                    if (p0 == 5)
                    {
                        // Status report: OK
                        ResponseRequested?.Invoke("\x1b[0n");
                    }
                    else if (p0 == 6)
                    {
                        // Cursor position report (1-based)
                        ResponseRequested?.Invoke($"\x1b[{_cursorRow + 1};{_cursorCol + 1}R");
                    }
                    break;

                case 'r': // DECSTBM - Set scroll region
                    {
                        int top = (p0 > 0 ? p0 : 1) - 1;
                        int bottom = (p1 > 0 ? p1 : _rows) - 1;
                        top = Clamp(top, 0, _rows - 1);
                        bottom = Clamp(bottom, 0, _rows - 1);
                        if (top < bottom)
                        {
                            _scrollTop = top;
                            _scrollBottom = bottom;
                        }
                        _cursorRow = _originMode ? _scrollTop : 0;
                        _cursorCol = 0;
                        _wrapPending = false;
                    }
                    break;

                case 's': // SCP - Save cursor position
                    SaveCursor();
                    break;

                case 'u': // RCP - Restore cursor position
                    RestoreCursor();
                    break;

                case 't': // Window manipulation (mostly ignored)
                    break;

                case 'c': // DA - Device attributes (send response)
                    if (_intermediate == ">")
                    {
                        // Secondary DA: report as VT220
                        ResponseRequested?.Invoke("\x1b[>1;10;0c");
                    }
                    else if (string.IsNullOrEmpty(_intermediate))
                    {
                        // Primary DA: report VT220 with ANSI color
                        ResponseRequested?.Invoke("\x1b[?62;22c");
                    }
                    break;

                case 'q': // DECSCUSR - Set cursor style
                    break;

                case 'b': // REP - Repeat preceding graphic character
                    {
                        int count = Math.Max(1, p0);
                        for (int i = 0; i < count; i++)
                            PutChar(_lastPrintedChar);
                    }
                    break;

                case 'I': // CHT - Cursor Forward Tabulation
                    {
                        int stops = Math.Max(1, p0);
                        for (int i = 0; i < stops; i++)
                            AdvanceTab();
                    }
                    break;

                case 'Z': // CBT - Cursor Backward Tabulation
                    {
                        int stops = Math.Max(1, p0);
                        for (int i = 0; i < stops; i++)
                            BackTab();
                    }
                    break;
            }
        }

        private void ExecuteDecPrivateMode(char finalChar)
        {
            foreach (var p in _params)
            {
                switch (finalChar)
                {
                    case 'h': // DECSET
                        switch (p)
                        {
                            case 1: _applicationCursorKeys = true; break; // DECCKM
                            case 6: _originMode = true; break;
                            case 7: _autoWrap = true; break;
                            case 12: break; // Blinking cursor
                            case 25: _cursorVisible = true; break;
                            case 47: // Alt screen buffer (old)
                            case 1047:
                                SwitchToAltBuffer();
                                break;
                            case 1048:
                                SaveCursor();
                                break;
                            case 1049: // Alt screen + save cursor
                                SaveCursor();
                                SwitchToAltBuffer();
                                ClearScreen();
                                break;
                            case 9: _mouseTrackingMode = 9; break; // X10 mouse
                            case 1000: _mouseTrackingMode = 1000; break; // Normal tracking
                            case 1002: _mouseTrackingMode = 1002; break; // Button-event tracking
                            case 1003: _mouseTrackingMode = 1003; break; // Any-event tracking
                            case 1004: _focusEventsEnabled = true; break; // Focus events
                            case 1005: break; // UTF-8 mouse (legacy, ignored)
                            case 1006: _sgrMouseMode = true; break; // SGR mouse mode
                            case 1015: break; // urxvt mouse (ignored)
                            case 2004: _bracketedPasteMode = true; break; // Bracketed paste
                        }
                        break;
                    case 'l': // DECRST
                        switch (p)
                        {
                            case 1: _applicationCursorKeys = false; break; // DECCKM
                            case 6: _originMode = false; break;
                            case 7: _autoWrap = false; break;
                            case 12: break;
                            case 25: _cursorVisible = false; break;
                            case 47:
                            case 1047:
                                SwitchFromAltBuffer();
                                break;
                            case 1048:
                                RestoreCursor();
                                break;
                            case 1049:
                                SwitchFromAltBuffer();
                                RestoreCursor();
                                break;
                            case 9:
                            case 1000:
                            case 1002:
                            case 1003:
                                _mouseTrackingMode = 0;
                                break;
                            case 1004: _focusEventsEnabled = false; break; // Focus events
                            case 1005: break;
                            case 1006: _sgrMouseMode = false; break; // SGR mouse mode
                            case 1015: break;
                            case 2004: _bracketedPasteMode = false; break; // Bracketed paste
                        }
                        break;
                }
            }
        }

        #endregion

        #region SGR (Colors & Attributes)

        private void ExecuteSgr()
        {
            if (_params.Count == 0)
            {
                ResetAttributes();
                return;
            }

            for (int i = 0; i < _params.Count; i++)
            {
                int p = _params[i];

                switch (p)
                {
                    case 0: ResetAttributes(); break;
                    case 1: _currentAttrs |= CellAttributes.Bold; break;
                    case 2: _currentAttrs |= CellAttributes.Dim; break;
                    case 3: _currentAttrs |= CellAttributes.Italic; break;
                    case 4: _currentAttrs |= CellAttributes.Underline; break;
                    case 7: _currentAttrs |= CellAttributes.Inverse; break;
                    case 8: _currentAttrs |= CellAttributes.Hidden; break;
                    case 9: _currentAttrs |= CellAttributes.Strikethrough; break;
                    case 21: _currentAttrs &= ~CellAttributes.Bold; break;
                    case 22:
                        _currentAttrs &= ~CellAttributes.Bold;
                        _currentAttrs &= ~CellAttributes.Dim;
                        break;
                    case 23: _currentAttrs &= ~CellAttributes.Italic; break;
                    case 24: _currentAttrs &= ~CellAttributes.Underline; break;
                    case 27: _currentAttrs &= ~CellAttributes.Inverse; break;
                    case 28: _currentAttrs &= ~CellAttributes.Hidden; break;
                    case 29: _currentAttrs &= ~CellAttributes.Strikethrough; break;

                    // Foreground colors (standard)
                    case >= 30 and <= 37:
                        _currentFg = TerminalColor.FromIndex(p - 30);
                        break;

                    case 38: // Extended foreground
                        if (i + 1 < _params.Count)
                        {
                            if (_params[i + 1] == 5 && i + 2 < _params.Count)
                            {
                                _currentFg = TerminalColor.FromIndex(_params[i + 2]);
                                i += 2;
                            }
                            else if (_params[i + 1] == 2 && i + 4 < _params.Count)
                            {
                                _currentFg = TerminalColor.FromRgb(
                                    (byte)_params[i + 2],
                                    (byte)_params[i + 3],
                                    (byte)_params[i + 4]);
                                i += 4;
                            }
                        }
                        break;

                    case 39: // Default foreground
                        _currentFg = TerminalColor.Default;
                        break;

                    // Background colors (standard)
                    case >= 40 and <= 47:
                        _currentBg = TerminalColor.FromIndex(p - 40);
                        break;

                    case 48: // Extended background
                        if (i + 1 < _params.Count)
                        {
                            if (_params[i + 1] == 5 && i + 2 < _params.Count)
                            {
                                _currentBg = TerminalColor.FromIndex(_params[i + 2]);
                                i += 2;
                            }
                            else if (_params[i + 1] == 2 && i + 4 < _params.Count)
                            {
                                _currentBg = TerminalColor.FromRgb(
                                    (byte)_params[i + 2],
                                    (byte)_params[i + 3],
                                    (byte)_params[i + 4]);
                                i += 4;
                            }
                        }
                        break;

                    case 49: // Default background
                        _currentBg = TerminalColor.Default;
                        break;

                    // Bright foreground
                    case >= 90 and <= 97:
                        _currentFg = TerminalColor.FromIndex(p - 90 + 8);
                        break;

                    // Bright background
                    case >= 100 and <= 107:
                        _currentBg = TerminalColor.FromIndex(p - 100 + 8);
                        break;
                }
            }
        }

        private void ResetAttributes()
        {
            _currentFg = TerminalColor.Default;
            _currentBg = TerminalColor.Default;
            _currentAttrs = CellAttributes.None;
        }

        #endregion

        #region OSC

        private void ExecuteOsc()
        {
            // OSC 0;title ST - Set window title
            // OSC 2;title ST - Set window title
            var parts = _oscString.Split(new[] { ';' }, 2);
            if (parts.Length >= 2 && int.TryParse(parts[0], out int cmd))
            {
                switch (cmd)
                {
                    case 0:
                    case 2:
                        TitleChanged?.Invoke(parts[1]);
                        break;
                }
            }
        }

        #endregion

        #region Screen Operations

        private void PutChar(char c)
        {
            if (_wrapPending && _autoWrap)
            {
                _cursorCol = 0;
                LineFeed();
                _wrapPending = false;
            }

            if (_insertMode)
            {
                // Shift chars right
                for (int x = _cols - 1; x > _cursorCol; x--)
                    _buffer[_cursorRow, x] = _buffer[_cursorRow, x - 1];
            }

            // Apply line-drawing character set translation
            if (_useLineDrawing && LineDrawingMap.TryGetValue(c, out var mapped))
            {
                c = mapped;
            }

            _lastPrintedChar = c;

            _buffer[_cursorRow, _cursorCol] = new CharacterCell
            {
                Character = c,
                Foreground = _currentFg,
                Background = _currentBg,
                Attributes = _currentAttrs
            };

            _cursorCol++;
            if (_cursorCol >= _cols)
            {
                if (_autoWrap)
                {
                    _cursorCol = _cols - 1;
                    _wrapPending = true;
                }
                else
                {
                    _cursorCol = _cols - 1;
                }
            }
        }

        private void LineFeed()
        {
            _wrapPending = false;
            if (_cursorRow == _scrollBottom)
            {
                ScrollUp(1);
            }
            else if (_cursorRow < _rows - 1)
            {
                _cursorRow++;
            }
        }

        private void ReverseIndex()
        {
            if (_cursorRow == _scrollTop)
            {
                ScrollDown(1);
            }
            else if (_cursorRow > 0)
            {
                _cursorRow--;
            }
        }

        private void ScrollUp(int lines)
        {
            for (int n = 0; n < lines; n++)
            {
                // Salvar linha do topo no scrollback (apenas se não for alt buffer)
                if (!_useAltBuffer)
                {
                    var scrolledLine = new CharacterCell[_cols];
                    for (int c = 0; c < _cols; c++)
                        scrolledLine[c] = _buffer[_scrollTop, c];
                    _scrollback.Add(scrolledLine);
                    if (_scrollback.Count > MaxScrollback)
                        _scrollback.RemoveAt(0);
                }

                // Mover linhas pra cima
                for (int r = _scrollTop; r < _scrollBottom; r++)
                    for (int c = 0; c < _cols; c++)
                        _buffer[r, c] = _buffer[r + 1, c];

                // Limpar última linha
                for (int c = 0; c < _cols; c++)
                    _buffer[_scrollBottom, c] = CharacterCell.Empty;
            }
        }

        private void ScrollDown(int lines)
        {
            for (int n = 0; n < lines; n++)
            {
                // Mover linhas pra baixo
                for (int r = _scrollBottom; r > _scrollTop; r--)
                    for (int c = 0; c < _cols; c++)
                        _buffer[r, c] = _buffer[r - 1, c];

                // Limpar primeira linha
                for (int c = 0; c < _cols; c++)
                    _buffer[_scrollTop, c] = CharacterCell.Empty;
            }
        }

        private void ClearScreen()
        {
            for (int r = 0; r < _rows; r++)
                for (int c = 0; c < _cols; c++)
                    _buffer[r, c] = CharacterCell.Empty;
        }

        private void EraseInDisplay(int mode)
        {
            switch (mode)
            {
                case 0: // Cursor to end
                    // Limpar da posição do cursor até o final da linha
                    for (int c = _cursorCol; c < _cols; c++)
                        _buffer[_cursorRow, c] = CharacterCell.Empty;
                    // Limpar linhas subsequentes
                    for (int r = _cursorRow + 1; r < _rows; r++)
                        for (int c = 0; c < _cols; c++)
                            _buffer[r, c] = CharacterCell.Empty;
                    break;

                case 1: // Start to cursor
                    for (int r = 0; r < _cursorRow; r++)
                        for (int c = 0; c < _cols; c++)
                            _buffer[r, c] = CharacterCell.Empty;
                    for (int c = 0; c <= _cursorCol; c++)
                        _buffer[_cursorRow, c] = CharacterCell.Empty;
                    break;

                case 2: // Entire screen
                    ClearScreen();
                    break;

                case 3: // Entire screen + scrollback
                    ClearScreen();
                    _scrollback.Clear();
                    break;
            }
        }

        private void EraseInLine(int mode)
        {
            switch (mode)
            {
                case 0: // Cursor to end
                    for (int c = _cursorCol; c < _cols; c++)
                        _buffer[_cursorRow, c] = CharacterCell.Empty;
                    break;

                case 1: // Start to cursor
                    for (int c = 0; c <= _cursorCol; c++)
                        _buffer[_cursorRow, c] = CharacterCell.Empty;
                    break;

                case 2: // Entire line
                    for (int c = 0; c < _cols; c++)
                        _buffer[_cursorRow, c] = CharacterCell.Empty;
                    break;
            }
        }

        private void InsertLines(int count)
        {
            if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom) return;
            for (int n = 0; n < count; n++)
            {
                for (int r = _scrollBottom; r > _cursorRow; r--)
                    for (int c = 0; c < _cols; c++)
                        _buffer[r, c] = _buffer[r - 1, c];
                for (int c = 0; c < _cols; c++)
                    _buffer[_cursorRow, c] = CharacterCell.Empty;
            }
        }

        private void DeleteLines(int count)
        {
            if (_cursorRow < _scrollTop || _cursorRow > _scrollBottom) return;
            for (int n = 0; n < count; n++)
            {
                for (int r = _cursorRow; r < _scrollBottom; r++)
                    for (int c = 0; c < _cols; c++)
                        _buffer[r, c] = _buffer[r + 1, c];
                for (int c = 0; c < _cols; c++)
                    _buffer[_scrollBottom, c] = CharacterCell.Empty;
            }
        }

        private void DeleteChars(int count)
        {
            for (int n = 0; n < count; n++)
            {
                for (int c = _cursorCol; c < _cols - 1; c++)
                    _buffer[_cursorRow, c] = _buffer[_cursorRow, c + 1];
                _buffer[_cursorRow, _cols - 1] = CharacterCell.Empty;
            }
        }

        private void InsertChars(int count)
        {
            for (int n = 0; n < count; n++)
            {
                for (int c = _cols - 1; c > _cursorCol; c--)
                    _buffer[_cursorRow, c] = _buffer[_cursorRow, c - 1];
                _buffer[_cursorRow, _cursorCol] = CharacterCell.Empty;
            }
        }

        private void EraseChars(int count)
        {
            for (int i = 0; i < count && _cursorCol + i < _cols; i++)
                _buffer[_cursorRow, _cursorCol + i] = CharacterCell.Empty;
        }

        private void AdvanceTab()
        {
            var nextTabStop = _tabStops.Where(t => t > _cursorCol).OrderBy(t => t).FirstOrDefault();
            if (nextTabStop > _cursorCol)
                _cursorCol = Math.Min(nextTabStop, _cols - 1);
            else
                _cursorCol = Math.Min(_cursorCol + (8 - (_cursorCol % 8)), _cols - 1);
            _wrapPending = false;
        }

        private void BackTab()
        {
            var prevTabStop = _tabStops.Where(t => t < _cursorCol).OrderByDescending(t => t).FirstOrDefault();
            if (prevTabStop < _cursorCol && _tabStops.Any(t => t < _cursorCol))
                _cursorCol = prevTabStop;
            else
                _cursorCol = 0;
            _wrapPending = false;
        }

        #endregion

        #region Buffer Management

        private void SwitchToAltBuffer()
        {
            if (_useAltBuffer) return;
            _useAltBuffer = true;

            // Salvar buffer principal
            _altBuffer = _buffer;
            _altCursorRow = _cursorRow;
            _altCursorCol = _cursorCol;

            // Criar novo buffer
            _buffer = new CharacterCell[_rows, _cols];
            ClearScreen();
        }

        private void SwitchFromAltBuffer()
        {
            if (!_useAltBuffer) return;
            _useAltBuffer = false;

            if (_altBuffer != null)
            {
                _buffer = _altBuffer;
                _cursorRow = _altCursorRow;
                _cursorCol = _altCursorCol;
                _altBuffer = null;
            }
        }

        private void SaveCursor()
        {
            _savedCursorRow = _cursorRow;
            _savedCursorCol = _cursorCol;
            _savedFg = _currentFg;
            _savedBg = _currentBg;
            _savedAttrs = _currentAttrs;
        }

        private void RestoreCursor()
        {
            _cursorRow = Clamp(_savedCursorRow, 0, _rows - 1);
            _cursorCol = Clamp(_savedCursorCol, 0, _cols - 1);
            _currentFg = _savedFg;
            _currentBg = _savedBg;
            _currentAttrs = _savedAttrs;
            _wrapPending = false;
        }

        private void FullReset()
        {
            ResetAttributes();
            _cursorRow = 0;
            _cursorCol = 0;
            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            _autoWrap = true;
            _originMode = false;
            _insertMode = false;
            _lineFeedMode = false;
            _wrapPending = false;
            _cursorVisible = true;
            _useAltBuffer = false;
            _altBuffer = null;
            _applicationCursorKeys = false;
            _applicationKeypad = false;
            _bracketedPasteMode = false;
            _mouseTrackingMode = 0;
            _sgrMouseMode = false;
            _focusEventsEnabled = false;
            _lastPrintedChar = ' ';
            _g0IsLineDrawing = false;
            _g1IsLineDrawing = false;
            _useLineDrawing = false;
            _activeCharset = 0;
            InitializeTabStops();
            ClearScreen();
            _scrollback.Clear();
        }

        private void SoftReset()
        {
            ResetAttributes();
            _scrollTop = 0;
            _scrollBottom = _rows - 1;
            _autoWrap = true;
            _originMode = false;
            _insertMode = false;
            _cursorVisible = true;
            _wrapPending = false;
            _applicationCursorKeys = false;
            _applicationKeypad = false;
            _bracketedPasteMode = false;
            _mouseTrackingMode = 0;
            _sgrMouseMode = false;
            _focusEventsEnabled = false;
            _g0IsLineDrawing = false;
            _g1IsLineDrawing = false;
            _useLineDrawing = false;
            _activeCharset = 0;
        }

        #endregion
    }
}
