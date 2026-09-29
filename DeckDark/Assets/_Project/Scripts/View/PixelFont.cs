using System.Collections.Generic;

namespace DeckDark.View
{
    /// <summary>
    /// Fonte pixelada desenhada a mao, com suporte a acentos do portugues.
    /// Dois tamanhos: Big (5x7) e Small (3x5). Todo texto e convertido para maiusculas.
    /// </summary>
    public class PixelFont
    {
        class Glyph
        {
            public bool[,] On;   // [x, y]
            public int Width;
        }

        readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
        readonly int glyphH;
        readonly int accentRows;
        readonly int spaceWidth;
        public readonly int LineHeight;

        public static readonly PixelFont Big = BuildBig();
        public static readonly PixelFont Small = BuildSmall();

        PixelFont(int glyphH, int accentRows, int spaceWidth)
        {
            this.glyphH = glyphH;
            this.accentRows = accentRows;
            this.spaceWidth = spaceWidth;
            LineHeight = accentRows + glyphH + 2;
        }

        void Add(char c, string rows)
        {
            var lines = rows.Split(' ');
            int w = 0;
            foreach (var l in lines)
                for (int i = 0; i < l.Length; i++) if (l[i] == '#' && i + 1 > w) w = i + 1;
            var g = new Glyph { Width = w, On = new bool[System.Math.Max(w, 1), glyphH] };
            for (int y = 0; y < lines.Length && y < glyphH; y++)
                for (int x = 0; x < lines[y].Length && x < w; x++)
                    g.On[x, y] = lines[y][x] == '#';
            glyphs[c] = g;
        }

        // Separa um caractere acentuado em letra base + acento
        static char Base(char c, out char accent)
        {
            accent = '\0';
            switch (c)
            {
                case 'Á': accent = '\''; return 'A';
                case 'À': accent = '`'; return 'A';
                case 'Â': accent = '^'; return 'A';
                case 'Ã': accent = '~'; return 'A';
                case 'É': accent = '\''; return 'E';
                case 'Ê': accent = '^'; return 'E';
                case 'Í': accent = '\''; return 'I';
                case 'Ó': accent = '\''; return 'O';
                case 'Ô': accent = '^'; return 'O';
                case 'Õ': accent = '~'; return 'O';
                case 'Ú': accent = '\''; return 'U';
                case 'Ç': accent = ','; return 'C';
            }
            return c;
        }

        public int CharWidth(char c)
        {
            c = char.ToUpperInvariant(c);
            if (c == ' ') return spaceWidth;
            char acc;
            char b = Base(c, out acc);
            Glyph g;
            if (!glyphs.TryGetValue(b, out g)) return spaceWidth;
            return g.Width + 1;
        }

        public int Measure(string s)
        {
            int w = 0;
            foreach (char c in s) w += CharWidth(c);
            return w > 0 ? w - 1 : 0;
        }

        public int Draw(PixelCanvas cv, string s, int x, int y, Rgb color, int scale = 1)
        {
            int cx = x;
            foreach (char raw in s)
            {
                char c = char.ToUpperInvariant(raw);
                if (c == ' ') { cx += spaceWidth * scale; continue; }
                char acc;
                char b = Base(c, out acc);
                Glyph g;
                if (!glyphs.TryGetValue(b, out g)) { cx += spaceWidth * scale; continue; }
                int top = y + accentRows * scale;
                for (int gx = 0; gx < g.Width; gx++)
                    for (int gy = 0; gy < glyphH; gy++)
                        if (g.On[gx, gy]) Block(cv, cx + gx * scale, top + gy * scale, scale, color);
                if (acc != '\0') DrawAccent(cv, acc, cx, y, top, g.Width, scale, color);
                cx += (g.Width + 1) * scale;
            }
            return cx - x;
        }

        void DrawAccent(PixelCanvas cv, char acc, int x, int y, int top, int w, int s, Rgb c)
        {
            int mid = (w - 1) / 2;
            if (accentRows >= 2)
            {
                switch (acc)
                {
                    case '\'': Block(cv, x + (mid + 1) * s, y, s, c); Block(cv, x + mid * s, y + s, s, c); break;
                    case '`': Block(cv, x + (mid - 1) * s, y, s, c); Block(cv, x + mid * s, y + s, s, c); break;
                    case '^': Block(cv, x + mid * s, y, s, c); Block(cv, x + (mid - 1) * s, y + s, s, c); Block(cv, x + (mid + 1) * s, y + s, s, c); break;
                    case '~':
                        Block(cv, x + (mid - 1) * s, y + s, s, c); Block(cv, x + mid * s, y, s, c);
                        Block(cv, x + (mid + 1) * s, y + s, s, c); Block(cv, x + (mid + 2) * s, y, s, c); break;
                    case ',': Block(cv, x + mid * s, top + glyphH * s, s, c); Block(cv, x + (mid - 1) * s, top + (glyphH + 1) * s, s, c); break;
                }
            }
            else
            {
                switch (acc)
                {
                    case '\'': Block(cv, x + (w - 1) * s, y, s, c); break;
                    case '`': Block(cv, x, y, s, c); break;
                    case '^': Block(cv, x + mid * s, y, s, c); break;
                    case '~': Block(cv, x + mid * s, y, s, c); Block(cv, x + (w - 1) * s, y, s, c); break;
                    case ',': Block(cv, x + mid * s, top + glyphH * s, s, c); break;
                }
            }
        }

        static void Block(PixelCanvas cv, int x, int y, int s, Rgb c)
        {
            if (s == 1) { cv.Set(x, y, c); return; }
            cv.Fill(x, y, s, s, c);
        }

        public void DrawCentered(PixelCanvas cv, string s, int cx, int y, Rgb color, int scale = 1)
        {
            Draw(cv, s, cx - Measure(s) * scale / 2, y, color, scale);
        }

        public void DrawShadowed(PixelCanvas cv, string s, int x, int y, Rgb color, Rgb shadow, int scale = 1)
        {
            Draw(cv, s, x + scale, y + scale, shadow, scale);
            Draw(cv, s, x, y, color, scale);
        }

        /// <summary>Quebra o texto em linhas que cabem em maxWidth.</summary>
        public List<string> Wrap(string text, int maxWidth)
        {
            var lines = new List<string>();
            foreach (var para in text.Split('\n'))
            {
                var words = para.Split(' ');
                string line = "";
                foreach (var w in words)
                {
                    string test = line.Length == 0 ? w : line + " " + w;
                    if (Measure(test) > maxWidth && line.Length > 0)
                    {
                        lines.Add(line);
                        line = w;
                    }
                    else line = test;
                }
                lines.Add(line);
            }
            return lines;
        }

        public int DrawWrapped(PixelCanvas cv, string text, int x, int y, int maxWidth, Rgb color, bool centered = false, int maxChars = int.MaxValue)
        {
            int yy = y;
            int remaining = maxChars;
            foreach (var line in Wrap(text, maxWidth))
            {
                if (remaining <= 0) break;
                string shown = line.Length > remaining ? line.Substring(0, remaining) : line;
                remaining -= line.Length + 1;
                if (centered) Draw(cv, shown, x + (maxWidth - Measure(line)) / 2, yy, color);
                else Draw(cv, shown, x, yy, color);
                yy += LineHeight;
            }
            return yy - y;
        }

        static PixelFont BuildBig()
        {
            var f = new PixelFont(7, 2, 3);
            f.Add('A', ".###. #...# #...# ##### #...# #...# #...#");
            f.Add('B', "####. #...# #...# ####. #...# #...# ####.");
            f.Add('C', ".###. #...# #.... #.... #.... #...# .###.");
            f.Add('D', "####. #...# #...# #...# #...# #...# ####.");
            f.Add('E', "##### #.... #.... ####. #.... #.... #####");
            f.Add('F', "##### #.... #.... ####. #.... #.... #....");
            f.Add('G', ".###. #...# #.... #.### #...# #...# .####");
            f.Add('H', "#...# #...# #...# ##### #...# #...# #...#");
            f.Add('I', "### .#. .#. .#. .#. .#. ###");
            f.Add('J', "..### ...#. ...#. ...#. #..#. #..#. .##..");
            f.Add('K', "#...# #..#. #.#.. ##... #.#.. #..#. #...#");
            f.Add('L', "#.... #.... #.... #.... #.... #.... #####");
            f.Add('M', "#...# ##.## #.#.# #.#.# #...# #...# #...#");
            f.Add('N', "#...# #...# ##..# #.#.# #..## #...# #...#");
            f.Add('O', ".###. #...# #...# #...# #...# #...# .###.");
            f.Add('P', "####. #...# #...# ####. #.... #.... #....");
            f.Add('Q', ".###. #...# #...# #...# #.#.# #..#. .##.#");
            f.Add('R', "####. #...# #...# ####. #.#.. #..#. #...#");
            f.Add('S', ".#### #.... #.... .###. ....# ....# ####.");
            f.Add('T', "##### ..#.. ..#.. ..#.. ..#.. ..#.. ..#..");
            f.Add('U', "#...# #...# #...# #...# #...# #...# .###.");
            f.Add('V', "#...# #...# #...# #...# #...# .#.#. ..#..");
            f.Add('W', "#...# #...# #...# #.#.# #.#.# #.#.# .#.#.");
            f.Add('X', "#...# #...# .#.#. ..#.. .#.#. #...# #...#");
            f.Add('Y', "#...# #...# .#.#. ..#.. ..#.. ..#.. ..#..");
            f.Add('Z', "##### ....# ...#. ..#.. .#... #.... #####");
            f.Add('0', ".###. #...# #..## #.#.# ##..# #...# .###.");
            f.Add('1', ".#. ##. .#. .#. .#. .#. ###");
            f.Add('2', ".###. #...# ....# ...#. ..#.. .#... #####");
            f.Add('3', "##### ...#. ..#.. ...#. ....# #...# .###.");
            f.Add('4', "...#. ..##. .#.#. #..#. ##### ...#. ...#.");
            f.Add('5', "##### #.... ####. ....# ....# #...# .###.");
            f.Add('6', "..##. .#... #.... ####. #...# #...# .###.");
            f.Add('7', "##### ....# ...#. ..#.. .#... .#... .#...");
            f.Add('8', ".###. #...# #...# .###. #...# #...# .###.");
            f.Add('9', ".###. #...# #...# .#### ....# ...#. .##..");
            f.Add('.', ". . . . . . #");
            f.Add(',', ". . . . . # #");
            f.Add('!', "# # # # # . #");
            f.Add('?', ".###. #...# ....# ...#. ..#.. ..... ..#..");
            f.Add(':', ". # . . . # .");
            f.Add(';', ". # . . . # #");
            f.Add('-', "... ... ... ### ... ... ...");
            f.Add('+', "..... ..#.. ..#.. ##### ..#.. ..#.. .....");
            f.Add('/', "....# ....# ...#. ..#.. .#... #.... #....");
            f.Add('(', ".# #. #. #. #. #. .#");
            f.Add(')', "#. .# .# .# .# .# #.");
            f.Add('\'', "# # . . . . .");
            f.Add('"', "#.# #.# ... ... ... ... ...");
            f.Add('=', ".... .... #### .... #### .... ....");
            f.Add('*', "... #.# .#. #.# ... ... ...");
            f.Add('%', "##..# ##.#. ...#. ..#.. .#... .#.## #..##");
            f.Add('<', "..# .#. #.. .#. ..# ... ...");
            f.Add('>', "#.. .#. ..# .#. #.. ... ...");
            f.Add('_', "..... ..... ..... ..... ..... ..... #####");
            f.Add('#', ".#.#. ##### .#.#. .#.#. ##### .#.#. .....");
            f.Add('&', ".##.. #..#. #.#.. .#... #.#.# #..#. .##.#");
            f.Add('[', "## #. #. #. #. #. ##");
            f.Add(']', "## .# .# .# .# .# ##");
            return f;
        }

        static PixelFont BuildSmall()
        {
            var f = new PixelFont(5, 1, 2);
            f.Add('A', ".#. #.# ### #.# #.#");
            f.Add('B', "##. #.# ##. #.# ##.");
            f.Add('C', ".## #.. #.. #.. .##");
            f.Add('D', "##. #.# #.# #.# ##.");
            f.Add('E', "### #.. ##. #.. ###");
            f.Add('F', "### #.. ##. #.. #..");
            f.Add('G', ".## #.. #.# #.# .##");
            f.Add('H', "#.# #.# ### #.# #.#");
            f.Add('I', "### .#. .#. .#. ###");
            f.Add('J', "..# ..# ..# #.# .#.");
            f.Add('K', "#.# #.# ##. #.# #.#");
            f.Add('L', "#.. #.. #.. #.. ###");
            f.Add('M', "#...# ##.## #.#.# #...# #...#");
            f.Add('N', "#..# ##.# #.## #..# #..#");
            f.Add('O', ".#. #.# #.# #.# .#.");
            f.Add('P', "##. #.# ##. #.. #..");
            f.Add('Q', ".#. #.# #.# ##. .##");
            f.Add('R', "##. #.# ##. #.# #.#");
            f.Add('S', ".## #.. .#. ..# ##.");
            f.Add('T', "### .#. .#. .#. .#.");
            f.Add('U', "#.# #.# #.# #.# ###");
            f.Add('V', "#.# #.# #.# #.# .#.");
            f.Add('W', "#...# #...# #.#.# ##.## #...#");
            f.Add('X', "#.# #.# .#. #.# #.#");
            f.Add('Y', "#.# #.# .#. .#. .#.");
            f.Add('Z', "### ..# .#. #.. ###");
            f.Add('0', "### #.# #.# #.# ###");
            f.Add('1', ".#. ##. .#. .#. ###");
            f.Add('2', "##. ..# .#. #.. ###");
            f.Add('3', "##. ..# .#. ..# ##.");
            f.Add('4', "#.# #.# ### ..# ..#");
            f.Add('5', "### #.. ##. ..# ##.");
            f.Add('6', ".## #.. ### #.# ###");
            f.Add('7', "### ..# .#. .#. .#.");
            f.Add('8', "### #.# ### #.# ###");
            f.Add('9', "### #.# ### ..# ##.");
            f.Add('.', ". . . . #");
            f.Add(',', ". . . # #");
            f.Add('!', "# # # . #");
            f.Add('?', "##. ..# .#. ... .#.");
            f.Add(':', ". # . # .");
            f.Add(';', ". # . # #");
            f.Add('-', "... ... ### ... ...");
            f.Add('+', "... .#. ### .#. ...");
            f.Add('/', "..# ..# .#. #.. #..");
            f.Add('(', ".# #. #. #. .#");
            f.Add(')', "#. .# .# .# #.");
            f.Add('\'', "# # . . .");
            f.Add('"', "#.# #.# ... ... ...");
            f.Add('=', "... ### ... ### ...");
            f.Add('*', "... #.# .#. #.# ...");
            f.Add('%', "#.# ..# .#. #.. #.#");
            f.Add('<', "..# .#. #.. .#. ..#");
            f.Add('>', "#.. .#. ..# .#. #..");
            f.Add('_', "... ... ... ... ###");
            f.Add('#', "#.# ### #.# ### #.#");
            f.Add('&', ".#. #.# .#. #.# .##");
            f.Add('[', "## #. #. #. ##");
            f.Add(']', "## .# .# .# ##");
            return f;
        }
    }
}
