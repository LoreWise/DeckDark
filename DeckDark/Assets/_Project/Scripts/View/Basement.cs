using System;

namespace DeckDark.View
{
    public static class Palette
    {
        public static readonly Rgb Black = Rgb.Hex(0x0b0a0c);
        public static readonly Rgb Ink = Rgb.Hex(0x2b2a3a);
        public static readonly Rgb Pencil = Rgb.Hex(0x5c5b68);
        public static readonly Rgb Paper = Rgb.Hex(0xeadfc4);
        public static readonly Rgb PaperDark = Rgb.Hex(0xcbb994);
        public static readonly Rgb PaperLine = Rgb.Hex(0xa9bccc);
        public static readonly Rgb Red = Rgb.Hex(0xd24a3c);
        public static readonly Rgb DarkRed = Rgb.Hex(0x7a2226);
        public static readonly Rgb Blue = Rgb.Hex(0x5ea4e6);
        public static readonly Rgb Green = Rgb.Hex(0x78c864);
        public static readonly Rgb Gold = Rgb.Hex(0xf0c040);
        public static readonly Rgb Purple = Rgb.Hex(0xb07ae0);
        public static readonly Rgb White = Rgb.Hex(0xf4efe4);
        public static readonly Rgb Wall = Rgb.Hex(0x3a3230);
        public static readonly Rgb WallDark = Rgb.Hex(0x2a2322);
        public static readonly Rgb Wood = Rgb.Hex(0x6a4428);
        public static readonly Rgb WoodDark = Rgb.Hex(0x4a2e1a);
        public static readonly Rgb WoodLight = Rgb.Hex(0x7e5434);
        public static readonly Rgb Skin = Rgb.Hex(0xd09a70);
        public static readonly Rgb SkinDark = Rgb.Hex(0xa87450);
        public static readonly Rgb Mask = Rgb.Hex(0xe8e0cf);
        public static readonly Rgb MaskShade = Rgb.Hex(0xbdb39e);
    }

    /// <summary>O cenario do porao: parede, mesa, o amigo, o escudo do mestre e a luz da lampada.</summary>
    public partial class GameApp
    {
        readonly PixelCanvas bg = new PixelCanvas(W, H);
        float[] directLight;
        static readonly float[] Bayer =
        {
            0f / 16, 8f / 16, 2f / 16, 10f / 16,
            12f / 16, 4f / 16, 14f / 16, 6f / 16,
            3f / 16, 11f / 16, 1f / 16, 9f / 16,
            15f / 16, 7f / 16, 13f / 16, 5f / 16,
        };

        const int TableTop = 100;
        const int LampX = 240, LampY = 26;

        void BuildStaticScene()
        {
            var c = bg;
            c.ResetTint();
            var rnd = new Random(42);

            // ---- Parede de blocos de concreto ----
            c.Fill(0, 0, W, TableTop, Palette.Wall);
            for (int y = 0; y < TableTop; y += 12)
            {
                int off = (y / 12) % 2 == 0 ? 0 : 14;
                c.HLine(0, W - 1, y, Palette.WallDark);
                for (int x = -28 + off; x < W; x += 28) c.VLine(x, y, y + 11, Palette.WallDark);
            }
            for (int i = 0; i < 900; i++)
            {
                int x = rnd.Next(W), y = rnd.Next(TableTop);
                c.Set(x, y, rnd.Next(2) == 0 ? Rgb.Hex(0x453b38) : Rgb.Hex(0x2e2725));
            }
            // mancha de umidade
            for (int i = 0; i < 400; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, r = rnd.NextDouble() * 22;
                c.Set(340 + (int)(Math.Cos(a) * r * 1.4), 70 + (int)(Math.Sin(a) * r), Rgb.Hex(0x2c2826));
            }

            // cano no teto
            c.Fill(0, 0, W, 4, Rgb.Hex(0x4a4a50));
            c.HLine(0, W - 1, 1, Rgb.Hex(0x6a6a72));
            c.HLine(0, W - 1, 4, Rgb.Hex(0x1e1e22));
            for (int x = 30; x < W; x += 90) { c.Fill(x, 0, 4, 6, Rgb.Hex(0x3a3a40)); }

            DrawPosters(c);
            DrawWindow(c);
            DrawShelf(c);

            // relogio (so o fundo; os ponteiros mudam)
            c.FillCircle(186, 22, 10, Rgb.Hex(0x1e1a1a));
            c.FillCircle(186, 22, 9, Rgb.Hex(0xdcd4c0));
            for (int h = 0; h < 12; h++)
            {
                double a = h / 12.0 * Math.PI * 2;
                c.Set(186 + (int)Math.Round(Math.Sin(a) * 7), 22 - (int)Math.Round(Math.Cos(a) * 7), Palette.Ink);
            }

            // cadeira atras do amigo
            c.Fill(210, 64, 60, 36, Rgb.Hex(0x2a1a12));
            c.Rect(210, 64, 60, 36, Rgb.Hex(0x1a0f0a));
            c.Fill(214, 58, 52, 8, Rgb.Hex(0x3a2418));

            // ---- Mesa ----
            c.Fill(0, TableTop, W, H - TableTop, Palette.Wood);
            for (int x = 0; x < W; x += 34)
            {
                int wob = rnd.Next(3) - 1;
                c.VLine(x + wob, TableTop, H - 1, Palette.WoodDark);
                c.VLine(x + wob + 1, TableTop, H - 1, Palette.WoodLight);
            }
            for (int i = 0; i < 260; i++)
            {
                int x = rnd.Next(W), y = TableTop + rnd.Next(H - TableTop);
                int len = 4 + rnd.Next(14);
                c.HLine(x, x + len, y, rnd.Next(3) == 0 ? Palette.WoodLight : Rgb.Hex(0x5c3a22));
            }
            c.Fill(0, TableTop, W, 3, Palette.WoodDark);
            c.HLine(0, W - 1, TableTop + 3, Rgb.Hex(0x3a2212));
            // anel de copo e mancha
            c.Circle(150, 250, 7, Rgb.Hex(0x563620));
            c.Circle(150, 250, 6, Rgb.Hex(0x5e3c24));
            for (int i = 0; i < 60; i++) c.Set(355 + rnd.Next(18), 250 + rnd.Next(8), Rgb.Hex(0x3e1414));

            DrawSnacks(c);

            // ---- Luz pre-calculada ----
            directLight = new float[W * H];
            vigBand = new byte[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - LampX) / 130f, dy = (y - LampY) / 80f;
                    float wall = 1f / (1f + dx * dx * 2.5f + dy * dy * 2f);
                    if (y < LampY) wall *= 0.35f; // o abajur bloqueia a luz para cima
                    float tx = (x - 252) / 235f, ty = (y - 178) / 120f;
                    float table = Math.Max(0f, 1f - (tx * tx + ty * ty));
                    table = (float)Math.Pow(table, 0.7);
                    if (y < TableTop) table = 0;
                    directLight[y * W + x] = Math.Max(wall * 0.95f, table);
                    float vx = (x - W * 0.5f) / (W * 0.5f), vy = (y - H * 0.5f) / (H * 0.5f);
                    float v = (vx * vx * 0.6f + vy * vy) * 0.8f;
                    vigBand[y * W + x] = (byte)Math.Min(15, (int)(v * 15.99f));
                }
        }

        void DrawPosters(PixelCanvas c)
        {
            // Poster de discoteca dos anos 70, desbotado
            int px = 14, py = 18, pw = 40, ph = 54;
            c.Fill(px, py, pw, ph, Rgb.Hex(0x5a3a6a));
            for (int i = 0; i < 9; i++)
            {
                double a = i / 9.0 * Math.PI + Math.PI;
                c.Line(px + 20, py + 34, px + 20 + (int)(Math.Cos(a) * 18), py + 34 + (int)(Math.Sin(a) * 18), Rgb.Hex(0xc07a50));
            }
            c.FillCircle(px + 20, py + 34, 9, Rgb.Hex(0xd89a50));
            c.Fill(px, py + 36, pw, ph - 36, Rgb.Hex(0x4a2e58));
            PixelFont.Small.DrawCentered(c, "DISCO", px + 20, py + 3, Rgb.Hex(0xe8c080));
            PixelFont.Small.DrawCentered(c, "NIGHT 78", px + 20, py + 44, Rgb.Hex(0xc0a0c8));
            c.Rect(px, py, pw, ph, Rgb.Hex(0x2a1e2a));
            c.Set(px + 2, py + 2, Palette.Gold); c.Set(px + pw - 3, py + 2, Palette.Gold);
            if (deaths >= 1)
            {
                // canto rasgado
                c.FillTriangle(px + pw - 12, py + ph - 1, px + pw - 1, py + ph - 1, px + pw - 1, py + ph - 14, Palette.Wall);
            }

            // Poster de uma banda grunge fictícia
            px = 62; py = 24; pw = 42; ph = 52;
            c.Fill(px, py, pw, ph, Rgb.Hex(0x2a2e2c));
            PixelFont.Big.DrawCentered(c, "VERME", px + 21, py + 4, Rgb.Hex(0xd8d4c8));
            for (int i = 0; i < 30; i++)
            {
                int x = px + 6 + i, y = py + 30 + (int)(Math.Sin(i * 0.6) * 5);
                c.Fill(x, y, 2, 2, Rgb.Hex(0xa8b070));
            }
            c.Set(px + 36, py + 27, Palette.Black); c.Set(px + 37, py + 27, Palette.Black);
            PixelFont.Small.DrawCentered(c, "TOUR 91", px + 21, py + 42, Rgb.Hex(0x98a0a0));
            c.Rect(px, py, pw, ph, Rgb.Hex(0x121414));
            c.Fill(px + 17, py - 2, 8, 4, Rgb.Hex(0xb0a890)); // fita adesiva
            if (deaths >= 2)
            {
                // alguem desenhou olhos no poster
                c.FillCircle(px + 12, py + 22, 2, Palette.Black); c.FillCircle(px + 30, py + 22, 2, Palette.Black);
                c.Set(px + 12, py + 22, Palette.Red); c.Set(px + 30, py + 22, Palette.Red);
            }
        }

        void DrawWindow(PixelCanvas c)
        {
            int x = 118, y = 8, w = 48, h = 22;
            c.Fill(x - 2, y - 2, w + 4, h + 4, Rgb.Hex(0x1e1a18));
            var glass = sessions >= 2 ? Rgb.Hex(0x06080e) : Rgb.Hex(0x16223a);
            c.Fill(x, y, w, h, glass);
            if (sessions < 2) c.FillCircle(x + 36, y + 8, 3, Rgb.Hex(0x6a6040)); // poste de luz la fora
            c.VLine(x + w / 2, y, y + h - 1, Rgb.Hex(0x1e1a18));
            for (int i = 0; i < 5; i++) c.VLine(x + 4 + i * 10, y, y + h - 1, Rgb.Hex(0x3a3a3e)); // grade
        }

        void DrawShelf(PixelCanvas c)
        {
            c.Fill(300, 20, 178, 3, Rgb.Hex(0x4e3420));
            c.HLine(300, 477, 23, Rgb.Hex(0x1e140c));
            DrawBox(c, 306, 6, 36, 14, "XMAS");
            DrawBox(c, 346, 9, 30, 11, "DAD");
            DrawBox(c, 380, 4, 46, 16, "DON'T OPEN");
            DrawBox(c, 430, 10, 40, 10, "BOOKS");
        }

        static void DrawBox(PixelCanvas c, int x, int y, int w, int h, string label)
        {
            c.Fill(x, y, w, h, Rgb.Hex(0x8a6a44));
            c.Rect(x, y, w, h, Rgb.Hex(0x4a3420));
            c.HLine(x, x + w - 1, y + 2, Rgb.Hex(0x9c7c50));
            PixelFont.Small.DrawCentered(c, label, x + w / 2, y + h / 2 - 3, Rgb.Hex(0x2a1e12));
        }

        void DrawSnacks(PixelCanvas c)
        {
            // lata de refrigerante
            c.Fill(440, 214, 16, 24, Rgb.Hex(0xa82828));
            c.Fill(440, 214, 16, 3, Rgb.Hex(0xb8b8c0));
            c.Fill(440, 236, 16, 2, Rgb.Hex(0x9898a0));
            c.VLine(443, 218, 234, Rgb.Hex(0xd04040));
            PixelFont.Small.Draw(c, "COLA", 441, 224, Palette.White);
            // saco de salgadinho aberto
            c.FillTriangle(398, 222, 432, 214, 428, 250, Rgb.Hex(0xe0a020));
            c.FillTriangle(398, 222, 428, 250, 400, 254, Rgb.Hex(0xd08a18));
            c.Line(398, 222, 432, 214, Rgb.Hex(0x9a5a10));
            PixelFont.Small.Draw(c, "CHIPS", 406, 232, Rgb.Hex(0x8a2010));
            for (int i = 0; i < 7; i++) c.Fill(386 + i * 5, 250 + (i % 3) * 3, 3, 2, Rgb.Hex(0xe8b040));
            // lapis
            c.Line(134, 262, 170, 250, Rgb.Hex(0xe0b830));
            c.Line(134, 263, 170, 251, Rgb.Hex(0xc09020));
            c.Set(133, 263, Palette.Ink); c.Set(171, 250, Rgb.Hex(0xe08080)); c.Set(172, 250, Rgb.Hex(0xe08080));
        }

        // ------------------------------------------------------------------
        // Elementos dinamicos do cenario (desenhados todo frame, antes da luz)
        // ------------------------------------------------------------------

        void DrawClockHands(PixelCanvas c)
        {
            // Comeca as 5 da tarde e avanca com cada sessao e cada sala. A noite nao acaba.
            float hours = 17f + sessions * 0.6f + (run != null ? run.Current.Layer * 0.35f : 0f);
            double ha = (hours % 12) / 12.0 * Math.PI * 2;
            double ma = (hours % 1) * Math.PI * 2;
            c.Line(186, 22, 186 + (int)Math.Round(Math.Sin(ha) * 4), 22 - (int)Math.Round(Math.Cos(ha) * 4), Palette.Ink);
            c.Line(186, 22, 186 + (int)Math.Round(Math.Sin(ma) * 7), 22 - (int)Math.Round(Math.Cos(ma) * 7), Palette.DarkRed);
        }

        void DrawFriend(PixelCanvas c)
        {
            int bob = (int)Math.Round(Math.Sin(time * 1.4) * 0.8);
            int tilt = Dread > 0.55f && Math.Sin(time * 0.7) > 0.6 ? 1 : 0;
            int cx = 240 + tilt, hy = 58 + bob;
            headX = cx; headY = hy;

            // tronco com camisa de flanela
            var flannel = Rgb.Hex(0x8a2c2c);
            var stripe = Rgb.Hex(0x4e1616);
            for (int y = 76; y < 100; y++)
                for (int x = 212; x < 270; x++)
                {
                    float ex = (x - 240) / 26f, ey = (y - 92) / 16f;
                    bool inside = ex * ex + ey * ey <= 1f || (x >= 214 && x <= 266 && y >= 90);
                    if (!inside) continue;
                    Rgb col = flannel;
                    if (x % 6 == 0 || y % 6 == 0) col = stripe;
                    else if (x % 6 == 3 && y % 2 == 0) col = Rgb.Hex(0x6a2222);
                    c.Set(x, y, col);
                }
            // camiseta por baixo
            c.FillTriangle(232, 78, 248, 78, 240, 92, Rgb.Hex(0x2a2a30));

            // bracos ate as maos no escudo
            c.Fill(208, 80, 10, 10, flannel); c.Fill(262, 80, 10, 10, flannel);
            c.Fill(204, 84, 12, 6, Palette.Skin); c.Fill(264, 84, 12, 6, Palette.Skin);
            for (int i = 0; i < 4; i++) { c.Set(205 + i * 3, 89, Palette.SkinDark); c.Set(265 + i * 3, 89, Palette.SkinDark); }

            // pescoco e cabeca
            c.Fill(cx - 5, hy + 12, 10, 8, Palette.SkinDark);
            c.FillEllipse(cx, hy, 12, 14, Palette.Skin);
            c.FillEllipse(cx - 11, hy + 1, 2, 3, Palette.SkinDark);
            c.FillEllipse(cx + 11, hy + 1, 2, 3, Palette.SkinDark);

            if (!maskOn)
            {
                c.Fill(cx - 6, hy - 1, 3, 2, Palette.Black);
                c.Fill(cx + 4, hy - 1, 3, 2, Palette.Black);
                c.HLine(cx - 7, cx - 3, hy - 4, Rgb.Hex(0x3a2618));
                c.HLine(cx + 3, cx + 7, hy - 4, Rgb.Hex(0x3a2618));
                c.Set(cx, hy + 3, Palette.SkinDark);
                c.HLine(cx - 3, cx + 3, hy + 8, Rgb.Hex(0x7a3a2a));
                c.Set(cx - 4, hy + 7, Rgb.Hex(0x7a3a2a)); c.Set(cx + 4, hy + 7, Rgb.Hex(0x7a3a2a));
            }

            // cabelo baguncado
            var hair = Rgb.Hex(0x3a2618);
            c.FillEllipse(cx, hy - 9, 13, 7, hair);
            c.Fill(cx - 13, hy - 9, 4, 12, hair);
            c.Fill(cx + 10, hy - 9, 4, 12, hair);
            for (int i = -10; i <= 10; i += 4) c.Fill(cx + i, hy - 5, 3, 3 + Math.Abs(i) % 3, hair);
        }

        int headX = 240, headY = 58;

        /// <summary>
        /// A mascara e desenhada depois da luz, para parecer palida e "acesa" no escuro.
        /// </summary>
        void DrawMask(PixelCanvas c)
        {
            if (!maskOn) return;
            int cx = headX, hy = headY;
            TintAt(cx, hy, 0.9f);
            c.SetTint(c.TintR * 0.95f, c.TintG * 0.97f, Math.Min(1.1f, c.TintB * 1.08f));
            var face = Rgb.Hex(0xf4f1ea);
            var shade = Rgb.Hex(0xc9c2b4);
            for (int y = -4; y <= 14; y++)
                for (int x = -11; x <= 11; x++)
                {
                    float ex = x / 11f, ey = (y - 1) / 13f;
                    if (ex * ex + ey * ey > 1f) continue;
                    bool dark = x >= 5 && ((x + y) & 1) == 0 || x >= 8;
                    c.Set(cx + x, hy + y, dark ? shade : face);
                }
            // olhos vazados, compridos
            var hole = Rgb.Hex(0x060406);
            c.FillEllipse(cx - 5, hy + 1, 2, 4, hole);
            c.FillEllipse(cx + 5, hy + 1, 2, 4, hole);
            // escorridos de tinta abaixo dos olhos
            c.VLine(cx - 5, hy + 5, hy + 9, Rgb.Hex(0x6e6660));
            c.VLine(cx + 5, hy + 5, hy + 8, Rgb.Hex(0x6e6660));
            // sorriso pintado de vermelho
            var paint = Rgb.Hex(0x8a1e22);
            c.Set(cx - 5, hy + 10, paint); c.HLine(cx - 4, cx + 4, hy + 11, paint); c.Set(cx + 5, hy + 10, paint);
            // rachadura
            c.Line(cx + 2, hy - 5, cx + 4, hy - 1, Rgb.Hex(0x7a7268));
            c.Line(cx + 4, hy - 1, cx + 3, hy + 2, Rgb.Hex(0x7a7268));
            // elastico
            c.Set(cx - 12, hy, Rgb.Hex(0x1a1210)); c.Set(cx + 12, hy, Rgb.Hex(0x1a1210));
            c.ResetTint();
            if (Dread > 0.4f && (int)(time * 2.3) % 7 != 0)
            {
                c.Set(cx - 5, hy + 1, Palette.Red);
                c.Set(cx + 5, hy + 1, Palette.Red);
            }
        }

        void DrawDmScreen(PixelCanvas c)
        {
            var side = Rgb.Hex(0x5a1c22);
            var front = Rgb.Hex(0x7c2a30);
            var trim = Rgb.Hex(0xb8903a);
            c.Fill(176, 90, 30, 36, side);
            c.Fill(206, 86, 68, 40, front);
            c.Fill(274, 90, 30, 36, side);
            c.Rect(176, 90, 30, 36, trim);
            c.Rect(206, 86, 68, 40, trim);
            c.Rect(274, 90, 30, 36, trim);
            // desenho de dragao a lapis de cor
            var d = Rgb.Hex(0xc8a050);
            c.Line(222, 112, 232, 100, d); c.Line(232, 100, 246, 104, d); c.Line(246, 104, 258, 98, d);
            c.Line(258, 98, 262, 104, d); c.Line(246, 104, 250, 114, d); c.Line(232, 100, 228, 94, d);
            c.Line(228, 94, 236, 96, d);
            c.Set(260, 100, Palette.Red);
            PixelFont.Small.DrawCentered(c, "MASTER", 240, 117, trim);
            PixelFont.Small.DrawCentered(c, "RPG", 191, 104, trim);
            PixelFont.Small.DrawCentered(c, "1991", 289, 104, trim);
            if (run != null && run.RulesVersion >= 2)
            {
                c.Fill(282, 92, 18, 12, Rgb.Hex(0xe8d860));
                PixelFont.Small.Draw(c, "V" + run.RulesVersion, 285, 95, Palette.Ink);
            }
        }

        void DrawLampShade(PixelCanvas c)
        {
            c.VLine(LampX, 4, 14, Rgb.Hex(0x1a1a1a));
            c.FillTriangle(LampX - 5, 14, LampX + 5, 14, LampX + 12, 24, Rgb.Hex(0x2e4a3a));
            c.FillTriangle(LampX - 5, 14, LampX - 12, 24, LampX + 12, 24, Rgb.Hex(0x2e4a3a));
            c.HLine(LampX - 12, LampX + 12, 24, Rgb.Hex(0x1e3026));
        }

        void DrawCrumpledSheets(PixelCanvas c)
        {
            var rnd = new Random(99);
            int n = Math.Min(deaths, 12);
            for (int i = 0; i < n; i++)
            {
                int x = 132 + rnd.Next(36), y = 104 + rnd.Next(18);
                c.FillCircle(x, y, 5, Palette.PaperDark);
                c.Circle(x, y, 5, Rgb.Hex(0x9a8a6a));
                c.Line(x - 3, y - 1, x + 2, y + 2, Rgb.Hex(0x9a8a6a));
                c.Set(x + 1, y - 2, Palette.Pencil);
            }
        }

        // ------------------------------------------------------------------
        // Luz
        // ------------------------------------------------------------------

        float LampIntensity
        {
            get
            {
                float wobble = 0.03f * (float)Math.Sin(time * 11.0) + 0.02f * (float)Math.Sin(time * 23.0);
                return Math.Max(0.1f, 1f - lampDip * 0.8f + wobble + lampFlash * 0.4f);
            }
        }

        void LightColors(out float wr, out float wg, out float wb, out float ar, out float ag, out float ab)
        {
            float d = Dread;
            wr = Lerp(1.08f, 0.88f, d * 0.8f); wg = Lerp(0.9f, 0.96f, d * 0.8f); wb = Lerp(0.66f, 0.78f, d * 0.8f);
            ar = Lerp(0.30f, 0.42f, d); ag = Lerp(0.30f, 0.16f, d); ab = Lerp(0.48f, 0.22f, d);
        }

        static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        byte[] vigBand;
        readonly int[] lutR = new int[12 * 16], lutG = new int[12 * 16], lutB = new int[12 * 16];
        const float AmbientLight = 0.3f;

        /// <summary>
        /// Ilumina a cena inteira com a lampada, em faixas com pontilhado (estilo pixel art).
        /// Usa uma tabela pronta por frame para ficar rapido.
        /// </summary>
        void ApplyLighting()
        {
            float wr, wg, wb, ar, ag, ab;
            LightColors(out wr, out wg, out wb, out ar, out ag, out ab);
            float inten = LampIntensity * 6f;
            float d = Dread;
            float vigStrength = 0.25f + d * 0.45f;
            for (int q = 0; q < 12; q++)
                for (int b = 0; b < 16; b++)
                {
                    float vig = 1f - vigStrength * (b / 15f);
                    float l = q / 6f;
                    int k = q * 16 + b;
                    lutR[k] = (int)((AmbientLight * ar + l * wr) * vig * 256f);
                    lutG[k] = (int)((AmbientLight * ag + l * wg) * vig * 256f);
                    lutB[k] = (int)((AmbientLight * ab + l * wb) * vig * 256f);
                }
            var px = Canvas.Px;
            var dl = directLight;
            var vb = vigBand;
            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                int by = (y & 3) * 4;
                for (int x = 0; x < W; x++)
                {
                    int i = row + x;
                    int q = (int)(dl[i] * inten + Bayer[by + (x & 3)]);
                    if (q > 11) q = 11;
                    int k = q * 16 + vb[i];
                    var p = px[i];
                    int r = (p.R * lutR[k]) >> 8, g = (p.G * lutG[k]) >> 8, b = (p.B * lutB[k]) >> 8;
                    p.R = (byte)(r > 255 ? 255 : r);
                    p.G = (byte)(g > 255 ? 255 : g);
                    p.B = (byte)(b > 255 ? 255 : b);
                    px[i] = p;
                }
            }
        }

        /// <summary>Quanto de luz chega num ponto, para iluminar objetos desenhados depois.</summary>
        void TintAt(int x, int y, float min)
        {
            float wr, wg, wb, ar, ag, ab;
            LightColors(out wr, out wg, out wb, out ar, out ag, out ab);
            x = Math.Max(0, Math.Min(W - 1, x)); y = Math.Max(0, Math.Min(H - 1, y));
            float l = directLight[y * W + x] * LampIntensity;
            float r = AmbientLight * ar + l * wr, g = AmbientLight * ag + l * wg, b = AmbientLight * ab + l * wb;
            float m = Math.Max(r, Math.Max(g, b));
            if (m < min) { float k = min / Math.Max(0.01f, m); r *= k; g *= k; b *= k; }
            Canvas.SetTint(Math.Min(1.1f, r), Math.Min(1.1f, g), Math.Min(1.1f, b));
        }

        void DrawBulbGlow(PixelCanvas c)
        {
            float inten = LampIntensity;
            c.ResetTint();
            c.FillEllipseAlpha(LampX, 26, 10, 4, Rgb.Hex(0xfff0c0), 0.35f * inten);
            c.FillEllipse(LampX, 25, 4, 2, inten > 0.5f ? Rgb.Hex(0xfff6d8) : Rgb.Hex(0x8a8060));
        }
    }
}
