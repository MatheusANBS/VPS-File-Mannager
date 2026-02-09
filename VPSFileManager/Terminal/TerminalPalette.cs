using System.Windows.Media;

namespace VPSFileManager.Terminal
{
    /// <summary>
    /// Paleta de cores do terminal, estilo Windows Terminal (One Half Dark).
    /// Suporta cores ANSI 0-15, paleta 256 e RGB.
    /// </summary>
    public static class TerminalPalette
    {
        // Cores padrão (One Half Dark theme - similar ao Windows Terminal)
        private static readonly Color[] StandardColors = new Color[]
        {
            // Normal (0-7)
            Color.FromRgb(40, 44, 52),     // 0: Black
            Color.FromRgb(224, 108, 117),   // 1: Red
            Color.FromRgb(152, 195, 121),   // 2: Green
            Color.FromRgb(229, 192, 123),   // 3: Yellow
            Color.FromRgb(97, 175, 239),    // 4: Blue
            Color.FromRgb(198, 120, 221),   // 5: Magenta
            Color.FromRgb(86, 182, 194),    // 6: Cyan
            Color.FromRgb(171, 178, 191),   // 7: White
            // Bright (8-15)
            Color.FromRgb(92, 99, 112),     // 8: Bright Black
            Color.FromRgb(224, 108, 117),   // 9: Bright Red
            Color.FromRgb(152, 195, 121),   // 10: Bright Green
            Color.FromRgb(229, 192, 123),   // 11: Bright Yellow
            Color.FromRgb(97, 175, 239),    // 12: Bright Blue
            Color.FromRgb(198, 120, 221),   // 13: Bright Magenta
            Color.FromRgb(86, 182, 194),    // 14: Bright Cyan
            Color.FromRgb(220, 223, 228),   // 15: Bright White
        };

        // Cor padrão texto e fundo
        public static readonly Color DefaultForeground = Color.FromRgb(204, 204, 204);   // #CCCCCC
        public static readonly Color DefaultBackground = Color.FromRgb(30, 30, 30);       // #1E1E1E
        public static readonly Color SelectionBackground = Color.FromArgb(80, 97, 175, 239);
        public static readonly Color CursorColor = Color.FromRgb(204, 204, 204);

        // Cache de cores 256
        private static Color[]? _palette256;

        /// <summary>
        /// Resolve uma TerminalColor para uma cor WPF.
        /// </summary>
        public static Color ResolveColor(TerminalColor color, bool isForeground)
        {
            if (color.IsDefault)
                return isForeground ? DefaultForeground : DefaultBackground;

            if (color.IsRgb)
                return Color.FromRgb(color.R, color.G, color.B);

            if (color.Index >= 0 && color.Index < 16)
                return StandardColors[color.Index];

            if (color.Index >= 16 && color.Index < 256)
                return Get256Color(color.Index);

            return isForeground ? DefaultForeground : DefaultBackground;
        }

        /// <summary>
        /// Obtém um Brush cacheado para uma cor.
        /// </summary>
        public static SolidColorBrush GetBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Color Get256Color(int index)
        {
            if (_palette256 == null)
                Build256Palette();
            return _palette256![index];
        }

        private static void Build256Palette()
        {
            _palette256 = new Color[256];

            // 0-15: Standard colors
            for (int i = 0; i < 16; i++)
                _palette256[i] = StandardColors[i];

            // 16-231: 6x6x6 color cube
            for (int i = 16; i < 232; i++)
            {
                int idx = i - 16;
                int r = idx / 36;
                int g = (idx / 6) % 6;
                int b = idx % 6;
                _palette256[i] = Color.FromRgb(
                    r == 0 ? (byte)0 : (byte)(55 + r * 40),
                    g == 0 ? (byte)0 : (byte)(55 + g * 40),
                    b == 0 ? (byte)0 : (byte)(55 + b * 40));
            }

            // 232-255: Grayscale
            for (int i = 232; i < 256; i++)
            {
                byte v = (byte)(8 + (i - 232) * 10);
                _palette256[i] = Color.FromRgb(v, v, v);
            }
        }
    }
}
