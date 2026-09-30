using System;

namespace DeckDark.View
{
    public struct Rgb
    {
        public byte R, G, B;
        public Rgb(int r, int g, int b)
        {
            R = (byte)Clamp(r); G = (byte)Clamp(g); B = (byte)Clamp(b);
        }

        public static Rgb Hex(int hex) { return new Rgb((hex >> 16) & 255, (hex >> 8) & 255, hex & 255); }

        public Rgb Mul(float f) { return new Rgb((int)(R * f), (int)(G * f), (int)(B * f)); }

        public static Rgb Lerp(Rgb a, Rgb b, float t)
        {
            return new Rgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        static int Clamp(int v) { return v < 0 ? 0 : (v > 255 ? 255 : v); }
    }

    /// <summary>
    /// Uma tela de baixa resolucao desenhada pixel a pixel.
    /// Tudo do prototipo e desenhado aqui e depois ampliado sem suavizacao,
    /// o que da o visual de pixel art.
    /// </summary>
    public class PixelCanvas
    {
        public readonly int W, H;
        public readonly Rgb[] Px;
        /// <summary>Cobertura de cada pixel (0 = transparente). So importa com TrackAlpha ligado (modo mesa 3D).</summary>
        public readonly byte[] A;
        public bool TrackAlpha;

        // Multiplicador de luz aplicado a tudo que for desenhado (usado para iluminar objetos)
        public float TintR = 1f, TintG = 1f, TintB = 1f;
        // Deslocamento global (tremor de tela)
        public int OffX, OffY;

        public PixelCanvas(int w, int h)
        {
            W = w; H = h;
            Px = new Rgb[w * h];
            A = new byte[w * h];
        }

        public void SetTint(float f) { TintR = TintG = TintB = f; }
        public void SetTint(float r, float g, float b) { TintR = r; TintG = g; TintB = b; }
        public void ResetTint() { TintR = TintG = TintB = 1f; }

        Rgb Tinted(Rgb c)
        {
            if (TintR == 1f && TintG == 1f && TintB == 1f) return c;
            return new Rgb((int)(c.R * TintR), (int)(c.G * TintG), (int)(c.B * TintB));
        }

        public void CopyFrom(PixelCanvas other) { Array.Copy(other.Px, Px, Px.Length); Array.Copy(other.A, A, A.Length); }

        public void Clear(Rgb c)
        {
            for (int i = 0; i < Px.Length; i++) { Px[i] = c; A[i] = 255; }
        }

        /// <summary>Limpa tudo para transparente (o fundo 3D aparece por tras).</summary>
        public void ClearTransparent()
        {
            Array.Clear(Px, 0, Px.Length);
            Array.Clear(A, 0, A.Length);
        }

        /// <summary>Mistura "por cima" levando em conta a cobertura do pixel de baixo.</summary>
        void Over(int i, Rgb c, float a)
        {
            int da = A[i];
            if (da >= 255) { Px[i] = Rgb.Lerp(Px[i], c, a); return; }
            float db = da / 255f;
            float oa = a + db * (1f - a);
            if (oa <= 0.001f) return;
            var d = Px[i];
            float k = db * (1f - a);
            Px[i] = new Rgb((int)((c.R * a + d.R * k) / oa), (int)((c.G * a + d.G * k) / oa), (int)((c.B * a + d.B * k) / oa));
            A[i] = (byte)(oa * 255f);
        }

        public void Set(int x, int y, Rgb c)
        {
            x += OffX; y += OffY;
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            int i = y * W + x;
            Px[i] = Tinted(c);
            A[i] = 255;
        }

        public Rgb Get(int x, int y)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return default(Rgb);
            return Px[y * W + x];
        }

        public void Blend(int x, int y, Rgb c, float a)
        {
            x += OffX; y += OffY;
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            int i = y * W + x;
            if (TrackAlpha) { Over(i, Tinted(c), a); return; }
            Px[i] = Rgb.Lerp(Px[i], Tinted(c), a);
        }

        public void Fill(int x, int y, int w, int h, Rgb c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) Set(i, j, c);
        }

        public void FillAlpha(int x, int y, int w, int h, Rgb c, float a)
        {
            // versao rapida: recorta o retangulo uma vez e mistura com inteiros
            x += OffX; y += OffY;
            int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
            int x1 = Math.Min(W, x + w), y1 = Math.Min(H, y + h);
            if (x0 >= x1 || y0 >= y1) return;
            var t = Tinted(c);
            if (TrackAlpha)
            {
                float fa = Math.Max(0f, Math.Min(1f, a));
                for (int j = y0; j < y1; j++)
                    for (int i = x0; i < x1; i++) Over(j * W + i, t, fa);
                return;
            }
            int ia = (int)(Math.Max(0f, Math.Min(1f, a)) * 256);
            int cr = t.R, cg = t.G, cb = t.B;
            for (int j = y0; j < y1; j++)
            {
                int row = j * W;
                for (int i = x0; i < x1; i++)
                {
                    var p = Px[row + i];
                    p.R = (byte)(p.R + (((cr - p.R) * ia) >> 8));
                    p.G = (byte)(p.G + (((cg - p.G) * ia) >> 8));
                    p.B = (byte)(p.B + (((cb - p.B) * ia) >> 8));
                    Px[row + i] = p;
                }
            }
        }

        /// <summary>Preenche alternando duas cores em xadrez: textura classica de pixel art.</summary>
        public void Dither(int x, int y, int w, int h, Rgb a, Rgb b)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++) Set(i, j, ((i + j) & 1) == 0 ? a : b);
        }

        public void Rect(int x, int y, int w, int h, Rgb c)
        {
            HLine(x, x + w - 1, y, c);
            HLine(x, x + w - 1, y + h - 1, c);
            VLine(x, y, y + h - 1, c);
            VLine(x + w - 1, y, y + h - 1, c);
        }

        public void HLine(int x0, int x1, int y, Rgb c)
        {
            if (x1 < x0) { int t = x0; x0 = x1; x1 = t; }
            for (int x = x0; x <= x1; x++) Set(x, y, c);
        }

        public void VLine(int x, int y0, int y1, Rgb c)
        {
            if (y1 < y0) { int t = y0; y0 = y1; y1 = t; }
            for (int y = y0; y <= y1; y++) Set(x, y, c);
        }

        public void Line(int x0, int y0, int x1, int y1, Rgb c)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        /// <summary>Linha tracejada, como lapis no papel.</summary>
        public void DashedLine(int x0, int y0, int x1, int y1, Rgb c, int on, int off)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, step = 0;
            while (true)
            {
                if (step % (on + off) < on) Set(x0, y0, c);
                step++;
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void FillEllipse(int cx, int cy, int rx, int ry, Rgb c)
        {
            if (rx <= 0 || ry <= 0) return;
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                    if ((x * x) * (ry * ry) + (y * y) * (rx * rx) <= (rx * rx) * (ry * ry)) Set(cx + x, cy + y, c);
        }

        public void FillEllipseAlpha(int cx, int cy, int rx, int ry, Rgb c, float a)
        {
            if (rx <= 0 || ry <= 0) return;
            for (int y = -ry; y <= ry; y++)
                for (int x = -rx; x <= rx; x++)
                    if ((x * x) * (ry * ry) + (y * y) * (rx * rx) <= (rx * rx) * (ry * ry)) Blend(cx + x, cy + y, c, a);
        }

        public void FillCircle(int cx, int cy, int r, Rgb c) { FillEllipse(cx, cy, r, r, c); }

        public void Circle(int cx, int cy, int r, Rgb c)
        {
            int x = r, y = 0, err = 1 - r;
            while (x >= y)
            {
                Set(cx + x, cy + y, c); Set(cx + y, cy + x, c); Set(cx - y, cy + x, c); Set(cx - x, cy + y, c);
                Set(cx - x, cy - y, c); Set(cx - y, cy - x, c); Set(cx + y, cy - x, c); Set(cx + x, cy - y, c);
                y++;
                if (err < 0) err += 2 * y + 1; else { x--; err += 2 * (y - x) + 1; }
            }
        }

        public void FillTriangle(int x0, int y0, int x1, int y1, int x2, int y2, Rgb c)
        {
            int minX = Math.Min(x0, Math.Min(x1, x2)), maxX = Math.Max(x0, Math.Max(x1, x2));
            int minY = Math.Min(y0, Math.Min(y1, y2)), maxY = Math.Max(y0, Math.Max(y1, y2));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int w0 = (x1 - x0) * (y - y0) - (y1 - y0) * (x - x0);
                    int w1 = (x2 - x1) * (y - y1) - (y2 - y1) * (x - x1);
                    int w2 = (x0 - x2) * (y - y2) - (y0 - y2) * (x - x2);
                    if ((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0)) Set(x, y, c);
                }
        }

        /// <summary>Desenha um sprite definido como texto. Cada caractere mapeia para uma cor; '.' e transparente.</summary>
        public void Sprite(string[] rows, int x, int y, Func<char, Rgb?> palette, bool flipX = false, int scale = 1)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                string row = rows[j];
                for (int i = 0; i < row.Length; i++)
                {
                    char ch = row[flipX ? row.Length - 1 - i : i];
                    if (ch == '.' || ch == ' ') continue;
                    var c = palette(ch);
                    if (!c.HasValue) continue;
                    if (scale == 1) Set(x + i, y + j, c.Value);
                    else Fill(x + i * scale, y + j * scale, scale, scale, c.Value);
                }
            }
        }

        /// <summary>Sprite com contorno escuro de 1 pixel, para destacar do fundo claro.</summary>
        public void SpriteOutlined(string[] rows, int x, int y, Func<char, Rgb?> palette, bool flipX, int scale, Rgb outline)
        {
            for (int j = 0; j < rows.Length; j++)
            {
                string row = rows[j];
                for (int i = 0; i < row.Length; i++)
                {
                    char ch = row[flipX ? row.Length - 1 - i : i];
                    if (ch == '.' || ch == ' ') continue;
                    Fill(x + i * scale - 1, y + j * scale - 1, scale + 2, scale + 2, outline);
                }
            }
            Sprite(rows, x, y, palette, flipX, scale);
        }

        /// <summary>Contorno de 1 pixel ao redor de um sprite (para destacar ao passar o mouse).</summary>
        public void SpriteOutline(string[] rows, int x, int y, Rgb c, int scale = 1)
        {
            for (int j = -1; j <= rows.Length; j++)
                for (int i = -1; i <= 24; i++)
                {
                    if (Solid(rows, i, j)) continue;
                    if (Solid(rows, i - 1, j) || Solid(rows, i + 1, j) || Solid(rows, i, j - 1) || Solid(rows, i, j + 1))
                        Fill(x + i * scale, y + j * scale, scale, scale, c);
                }
        }

        static bool Solid(string[] rows, int i, int j)
        {
            if (j < 0 || j >= rows.Length) return false;
            if (i < 0 || i >= rows[j].Length) return false;
            char ch = rows[j][i];
            return ch != '.' && ch != ' ';
        }
    }
}
