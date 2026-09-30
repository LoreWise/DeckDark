using System.Collections.Generic;
using UnityEngine;

namespace DeckDark.Test3D
{
    /// <summary>
    /// Objetos do porao 3D que mudam com a historia (paginas liberadas do caderno do Rafa):
    ///  - relogio de parede que para as 3:17;
    ///  - foto dos irmaos na estante, depois com um rosto riscado, depois com alguem a mais;
    ///  - desenho de giz de cera do personagem do jogador colado sobre o poster de discoteca;
    ///  - uma cadeira a mais, vazia, virada para a mesa.
    /// Todas as texturas sao desenhadas aqui em codigo.
    /// Ganchos no Basement3DTest: BuildStory() no Awake e UpdateStory() no Update.
    /// </summary>
    public partial class Basement3DTest
    {
        // ---- controlado pelo jogo (UnityPrototype) ----
        public int StoryStage;       // 0..12 paginas liberadas
        public bool StoryWizard;     // o desenho mostra o mago em vez do guerreiro

        int storyBuiltStage = -1;
        bool storyBuiltWizard;
        Texture2D photoTex, drawingTex;
        bool storyReady;

        // relogio
        readonly Vector3 clockPos = new Vector3(-0.42f, 1.72f, 2.28f);
        float clockHourAngle, clockMinuteAngle, clockSecondAngle;

        static Matrix4x4 HiddenMatrix { get { return Matrix4x4.TRS(new Vector3(0f, -30f, 0f), Quaternion.identity, Vector3.zero); } }

        /// <summary>Item que so aparece quando a condicao e verdadeira.</summary>
        void AddStoryItem(Mesh mesh, Material mat, Matrix4x4 m, bool blend, System.Func<bool> visible)
        {
            var it = new Item { Mesh = mesh, Mat = mat, M = m, Blend = blend };
            it.Dynamic = () => visible() ? m : HiddenMatrix;
            items.Add(it);
        }

        void BuildStory()
        {
            if (shader == null) return;

            // ---------------- relogio de parede ----------------
            var clockFace = Mat(ClockFaceTexture(), Color.white, Vector2.one, 0f, 0.3f, true);
            items.Add(new Item { Mesh = quad, Mat = clockFace, Blend = true, M = Matrix4x4.TRS(clockPos, Quaternion.identity, new Vector3(0.24f, 0.24f, 1f)) });
            var handMat = Mat(white, new Color(0.08f, 0.07f, 0.07f), Vector2.one);
            var secMat = Mat(white, new Color(0.7f, 0.12f, 0.1f), Vector2.one);
            items.Add(new Item { Mesh = cube, Mat = handMat, Dynamic = () => HandMatrix(clockHourAngle, 0.055f, 0.012f, 0.012f) });
            items.Add(new Item { Mesh = cube, Mat = handMat, Dynamic = () => HandMatrix(clockMinuteAngle, 0.085f, 0.008f, 0.016f) });
            items.Add(new Item { Mesh = cube, Mat = secMat, Dynamic = () => HandMatrix(clockSecondAngle, 0.09f, 0.004f, 0.02f) });

            // ---------------- foto na prateleira de cima ----------------
            photoTex = NewTex(40, 30);
            var frameRot = Quaternion.Euler(10f, -14f, 0f);
            var framePos = new Vector3(-1.22f, 1.57f + 0.085f, 2.04f);
            var frameBase = Matrix4x4.TRS(framePos, frameRot, Vector3.one);
            var frameMat = Mat(white, new Color(0.22f, 0.14f, 0.08f), Vector2.one);
            AddStoryItem(cube, frameMat, frameBase * Matrix4x4.Scale(new Vector3(0.22f, 0.17f, 0.02f)), false, () => StoryStage >= 2);
            var photoMat = Mat(photoTex, Color.white, Vector2.one, 0f, 0.35f);
            AddStoryItem(quad, photoMat, frameBase * Matrix4x4.TRS(new Vector3(0f, 0f, -0.0115f), Quaternion.identity, new Vector3(0.18f, 0.135f, 1f)), false, () => StoryStage >= 2);

            // ---------------- desenho sobre o poster de discoteca ----------------
            drawingTex = NewTex(42, 58);
            var drawMat = Mat(drawingTex, Color.white, Vector2.one, 0f, 0.3f);
            AddStoryItem(quad, drawMat, Matrix4x4.TRS(new Vector3(1.35f, 1.42f, 2.282f), Quaternion.Euler(0f, 0f, -2f), new Vector3(0.46f, 0.63f, 1f)), false, () => StoryStage >= 5);

            // ---------------- cadeira vazia a mais ----------------
            var chairMat = Mat(Tex("tile_wood"), new Color(0.55f, 0.55f, 0.55f), Vector2.one);
            var chair = Matrix4x4.TRS(new Vector3(0.95f, 0f, 1.35f), Quaternion.Euler(0f, 42f, 0f), Vector3.one);
            System.Func<bool> chairOn = () => StoryStage >= 8;
            AddStoryItem(cube, chairMat, chair * Matrix4x4.TRS(new Vector3(0f, 0.45f, 0f), Quaternion.identity, new Vector3(0.42f, 0.04f, 0.4f)), false, chairOn);
            AddStoryItem(cube, chairMat, chair * Matrix4x4.TRS(new Vector3(0f, 0.85f, 0.19f), Quaternion.identity, new Vector3(0.42f, 0.8f, 0.04f)), false, chairOn);
            foreach (var p in new[] { new Vector2(-0.18f, -0.17f), new Vector2(0.18f, -0.17f), new Vector2(-0.18f, 0.17f), new Vector2(0.18f, 0.17f) })
                AddStoryItem(cube, chairMat, chair * Matrix4x4.TRS(new Vector3(p.x, 0.215f, p.y), Quaternion.identity, new Vector3(0.035f, 0.43f, 0.035f)), false, chairOn);
            AddStoryItem(quad, Mat(Tex("blob"), Color.white, Vector2.one, 0f, 0f, true), Matrix4x4.TRS(new Vector3(0.95f, 0.002f, 1.35f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.6f, 0.55f, 1f)), true, chairOn);

            storyReady = true;
        }

        /// <summary>Ponteiro: gira em volta do centro do relogio (angulo em graus, sentido horario).</summary>
        Matrix4x4 HandMatrix(float angle, float length, float width, float depth)
        {
            var rot = Quaternion.Euler(0f, 0f, -angle);
            var pos = clockPos + new Vector3(0f, 0f, -depth) + rot * new Vector3(0f, length * 0.4f, 0f);
            return Matrix4x4.TRS(pos, rot, new Vector3(width, length, 0.004f));
        }

        void UpdateStory()
        {
            if (!storyReady) return;
            if (StoryStage != storyBuiltStage || StoryWizard != storyBuiltWizard)
            {
                storyBuiltStage = StoryStage;
                storyBuiltWizard = StoryWizard;
                PaintPhoto(photoTex, StoryStage);
                PaintDrawing(drawingTex, StoryWizard);
            }

            float t = Time.time;
            if (StoryStage >= 7)
            {
                // parado as 3:17; o ponteiro dos segundos tenta andar e volta
                clockHourAngle = (3f + 17f / 60f) * 30f;
                clockMinuteAngle = 17f * 6f;
                float f = t % 1.7f;
                clockSecondAngle = 42f * 6f + (f < 0.12f ? 6f : 0f);
            }
            else
            {
                // anda normal, comecando as 5 da tarde
                float minutes = 17f * 60f + 12f + t / 60f;
                clockHourAngle = (minutes / 60f % 12f) * 30f;
                clockMinuteAngle = (minutes % 60f) * 6f;
                clockSecondAngle = Mathf.Floor(t % 60f) * 6f;
            }
        }

        // ------------------------------------------------------------------ texturas desenhadas em codigo

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        /// <summary>Pincel simples: coordenadas com y para baixo, como no resto do jogo.</summary>
        class Painter
        {
            public readonly int W, H;
            public readonly Color32[] Px;
            public Painter(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }
            public void Set(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[(H - 1 - y) * W + x] = c; }
            public void Fill(int x, int y, int w, int h, Color32 c) { for (int j = y; j < y + h; j++) for (int i = x; i < x + w; i++) Set(i, j, c); }
            public void Disc(int cx, int cy, float r, Color32 c)
            {
                int ri = Mathf.CeilToInt(r);
                for (int j = -ri; j <= ri; j++) for (int i = -ri; i <= ri; i++) if (i * i + j * j <= r * r) Set(cx + i, cy + j, c);
            }
            public void Line(int x0, int y0, int x1, int y1, Color32 c, int thick = 1)
            {
                int n = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
                for (int k = 0; k <= n; k++)
                {
                    float f = n == 0 ? 0f : (float)k / n;
                    int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, f)), y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, f));
                    Fill(x - (thick - 1) / 2, y - (thick - 1) / 2, thick, thick, c);
                }
            }
            public void Apply(Texture2D t) { t.SetPixels32(Px); t.Apply(false); }
        }

        static Texture2D ClockFaceTexture()
        {
            var p = new Painter(32, 32);
            var rim = new Color32(40, 34, 30, 255);
            var face = new Color32(226, 218, 196, 255);
            var tick = new Color32(40, 36, 40, 255);
            p.Disc(16, 16, 15.5f, rim);
            p.Disc(16, 16, 13.5f, face);
            for (int h = 0; h < 12; h++)
            {
                float a = h / 12f * Mathf.PI * 2f;
                int x = 16 + Mathf.RoundToInt(Mathf.Sin(a) * 11.5f), y = 16 - Mathf.RoundToInt(Mathf.Cos(a) * 11.5f);
                p.Set(x, y, tick);
                if (h % 3 == 0) p.Set(x + (x < 16 ? 1 : (x > 16 ? -1 : 0)), y + (y < 16 ? 1 : (y > 16 ? -1 : 0)), tick);
            }
            var t = NewTex(32, 32);
            p.Apply(t);
            return t;
        }

        /// <summary>Foto de dois meninos. Estagio 4: o rosto do irmao riscado. Estagio 9: alguem a mais no fundo.</summary>
        static void PaintPhoto(Texture2D t, int stage)
        {
            if (t == null) return;
            var p = new Painter(t.width, t.height);
            var bg = new Color32(176, 152, 116, 255);
            var floor = new Color32(128, 108, 84, 255);
            var skin = new Color32(214, 176, 136, 255);
            var hair = new Color32(70, 46, 30, 255);
            p.Fill(0, 0, p.W, p.H, bg);
            p.Fill(0, 20, p.W, p.H - 20, floor);
            // borda branca da foto
            for (int x = 0; x < p.W; x++) { p.Set(x, 0, new Color32(236, 228, 210, 255)); p.Set(x, p.H - 1, new Color32(236, 228, 210, 255)); }
            for (int y = 0; y < p.H; y++) { p.Set(0, y, new Color32(236, 228, 210, 255)); p.Set(p.W - 1, y, new Color32(236, 228, 210, 255)); }

            if (stage >= 9)
            {
                // a figura escura atras deles, com o rosto branco
                p.Fill(19, 6, 6, 20, new Color32(30, 24, 26, 255));
                p.Disc(21, 5, 3f, new Color32(244, 240, 232, 255));
                p.Set(20, 5, new Color32(10, 8, 8, 255)); p.Set(22, 5, new Color32(10, 8, 8, 255));
            }
            // Rafa (menor, camisa de flanela)
            p.Fill(8, 16, 9, 11, new Color32(138, 44, 44, 255));
            p.Disc(12, 11, 4f, skin);
            p.Fill(8, 6, 9, 3, hair);
            p.Set(11, 11, new Color32(30, 20, 20, 255)); p.Set(13, 11, new Color32(30, 20, 20, 255));
            // Marcos (maior, camiseta escura)
            p.Fill(26, 13, 10, 14, new Color32(58, 74, 90, 255));
            p.Disc(31, 8, 4.5f, skin);
            p.Fill(27, 3, 9, 3, hair);
            p.Set(30, 8, new Color32(30, 20, 20, 255)); p.Set(32, 8, new Color32(30, 20, 20, 255));
            p.Line(29, 11, 33, 11, new Color32(120, 60, 50, 255));
            // braco no ombro do irmao menor
            p.Line(26, 15, 17, 17, new Color32(58, 74, 90, 255), 2);

            if (stage >= 4)
            {
                // o rosto do Marcos riscado com a ponta de uma caneta
                var scratch = new Color32(240, 234, 222, 255);
                p.Line(26, 3, 36, 13, scratch);
                p.Line(36, 3, 26, 13, scratch);
                p.Line(27, 5, 35, 6, scratch);
                p.Line(27, 9, 36, 10, scratch);
                p.Line(28, 12, 34, 4, scratch);
            }
            p.Apply(t);
        }

        /// <summary>Desenho de giz de cera: o personagem do jogador e a figura de mascara atras dele.</summary>
        static void PaintDrawing(Texture2D t, bool wizard)
        {
            if (t == null) return;
            var p = new Painter(t.width, t.height);
            var paper = new Color32(232, 228, 214, 255);
            var line = new Color32(196, 208, 220, 255);
            p.Fill(0, 0, p.W, p.H, paper);
            for (int y = 6; y < p.H; y += 5) for (int x = 0; x < p.W; x++) p.Set(x, y, line);
            for (int y = 0; y < p.H; y++) p.Set(4, y, new Color32(220, 160, 160, 255));
            // fita adesiva
            p.Fill(16, 0, 10, 3, new Color32(200, 190, 160, 255));

            var blue = new Color32(50, 80, 170, 255);
            int cx = 14, cy = 20;
            // o personagem de palitinho
            for (int a = 0; a < 16; a++)
            {
                float r = a / 16f * Mathf.PI * 2f;
                p.Set(cx + Mathf.RoundToInt(Mathf.Cos(r) * 4f), cy + Mathf.RoundToInt(Mathf.Sin(r) * 4f), blue);
            }
            p.Line(cx, cy + 4, cx, cy + 18, blue, 2);
            p.Line(cx, cy + 8, cx - 6, cy + 14, blue, 2);
            p.Line(cx, cy + 8, cx + 6, cy + 11, blue, 2);
            p.Line(cx, cy + 18, cx - 5, cy + 28, blue, 2);
            p.Line(cx, cy + 18, cx + 5, cy + 28, blue, 2);
            if (wizard)
            {
                var purple = new Color32(122, 58, 168, 255);
                p.Line(cx - 5, cy - 4, cx + 1, cy - 14, purple, 2);
                p.Line(cx + 5, cy - 4, cx + 1, cy - 14, purple, 2);
                p.Line(cx - 5, cy - 4, cx + 5, cy - 4, purple, 2);
                p.Line(cx + 7, cy + 11, cx + 9, cy - 4, new Color32(138, 90, 42, 255), 2);
            }
            else
            {
                var steel = new Color32(140, 140, 152, 255);
                p.Line(cx + 6, cy + 11, cx + 13, cy - 1, steel, 2);
                p.Line(cx + 4, cy + 8, cx + 9, cy + 12, steel, 1);
            }

            // a figura alta de mascara, com a mao no ombro dele
            var black = new Color32(26, 20, 24, 255);
            int mx = 31, my = 10;
            p.Fill(mx - 5, my + 7, 10, 38, black);
            p.Line(mx - 5, my + 12, cx + 3, cy + 6, black, 2);
            p.Disc(mx, my + 2, 4.5f, new Color32(244, 240, 232, 255));
            p.Set(mx - 2, my + 1, black); p.Set(mx + 2, my + 1, black);
            p.Set(mx - 2, my + 2, black); p.Set(mx + 2, my + 2, black);
            p.Line(mx - 2, my + 5, mx + 2, my + 5, new Color32(170, 30, 30, 255));

            // "YOU" em vermelho embaixo do personagem (letras 3x5)
            var red = new Color32(168, 40, 40, 255);
            DrawLetters(p, "YOU", 7, 51, red);
            p.Apply(t);
        }

        static void DrawLetters(Painter p, string s, int x, int y, Color32 c)
        {
            foreach (char ch in s)
            {
                string[] g;
                switch (ch)
                {
                    case 'Y': g = new[] { "#.#", "#.#", ".#.", ".#.", ".#." }; break;
                    case 'O': g = new[] { "###", "#.#", "#.#", "#.#", "###" }; break;
                    case 'U': g = new[] { "#.#", "#.#", "#.#", "#.#", "###" }; break;
                    default: g = new[] { "...", "...", "...", "...", "..." }; break;
                }
                for (int j = 0; j < 5; j++) for (int i = 0; i < 3; i++) if (g[j][i] == '#') p.Set(x + i, y + j, c);
                x += 4;
            }
        }
    }
}
