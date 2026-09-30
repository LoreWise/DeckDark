using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeckDark.Test3D
{
    /// <summary>
    /// Teste da mesa em 3D pixelado.
    /// Monta o porao com formas simples, desenha tudo numa textura pequena (480x270)
    /// e amplia sem suavizar. Nao usa luzes nem cameras do Unity: a lampada e calculada
    /// no shader DarkDeck/PixelLit, entao funciona em qualquer pipeline.
    /// Abre com F3 durante o jogo ou pelo menu Dark Deck > Play 3D Test.
    /// </summary>
    public partial class Basement3DTest : MonoBehaviour
    {
        const int W = 480, H = 270;

        class Item
        {
            public Mesh Mesh;
            public Material Mat;
            public Matrix4x4 M;
            public bool Blend;
            public System.Func<Matrix4x4> Dynamic;   // para objetos que se mexem
        }

        readonly List<Item> items = new List<Item>();
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        RenderTexture rt;
        Shader shader;
        Mesh cube, quad, cyl, sphere, d20;
        Texture2D white, black;

        // camera
        Vector3 camPos = new Vector3(0f, 1.22f, -0.95f);
        Vector3 camTarget = new Vector3(0f, 0.9f, 0.55f);
        float fov = 48f;
        Vector2 sway;

        // lampada (pendulo preso no teto)
        readonly Vector3 lampPivot = new Vector3(0f, 2.6f, 0.35f);
        const float cordLen = 1.08f;
        Vector3 lampPos;
        
        public static Basement3DTest Open()
        {
            var existing = FindAnyObjectByType<Basement3DTest>(FindObjectsInactive.Include);
            if (existing != null) { existing.gameObject.SetActive(true); existing.enabled = true; return existing; }
            var go = new GameObject("DarkDeck 3D Test");
            return go.AddComponent<Basement3DTest>();
        }

        void Awake()
        {
            shader = Resources.Load<Shader>("DarkDeckPixelLit");
            if (shader == null) shader = Shader.Find("DarkDeck/PixelLit");
            if (shader == null) Debug.LogError("DarkDeck: shader DarkDeck/PixelLit nao encontrado.");

            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Point;
            rt.Create();

            white = Solid(Color.white); black = Solid(Color.black);
            cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            quad = BuildQuad();
            d20 = BuildIcosahedron();

            BuildScene();
            BuildStory();
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
        }

        // ------------------------------------------------------------------ texturas

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
        }

        Texture2D Tex(string name)
        {
            Texture2D t;
            if (textures.TryGetValue(name, out t)) return t;
            string path = Path.Combine(Application.streamingAssetsPath, "Test3D", name + ".png");
            t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (File.Exists(path)) t.LoadImage(File.ReadAllBytes(path));
            else { Debug.LogWarning("DarkDeck: textura faltando " + path); t = white; }
            t.filterMode = FilterMode.Point;
            t.wrapMode = name.StartsWith("tile_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            textures[name] = t;
            return t;
        }

        Material Mat(Texture tex, Color color, Vector2 tiling, float emission = 0f, float flat = 0f, bool blend = false)
        {
            var m = new Material(shader);
            m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", color);
            m.SetVector("_Tiling", new Vector4(tiling.x, tiling.y, 0, 0));
            m.SetFloat("_Emission", emission);
            m.SetFloat("_Flat", flat);
            if (blend)
            {
                m.SetFloat("_Cutoff", 0.01f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
            }
            return m;
        }

        // ------------------------------------------------------------------ montagem

        Item Add(Mesh mesh, Material mat, Vector3 pos, Vector3 euler, Vector3 scale, bool blend = false)
        {
            var it = new Item { Mesh = mesh, Mat = mat, M = Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale), Blend = blend };
            items.Add(it);
            return it;
        }

        void Box(string tex, Color c, Vector3 pos, Vector3 size, float tile = 0f, Vector3 euler = default)
        {
            var t = tex == null ? (Texture)white : Tex(tex);
            var tiling = tile > 0 ? new Vector2(Mathf.Max(size.x, size.z) / tile, size.y / tile) : Vector2.one;
            Add(cube, Mat(t, c, tiling), pos, euler, size);
        }

        /// <summary>Quad deitado sobre uma superficie (papel, carta, sombra).</summary>
        void Flat(string tex, Vector3 pos, float yaw, Vector2 size, bool blend = false, float flat = 0f)
        {
            Add(quad, Mat(Tex(tex), Color.white, Vector2.one, 0f, flat, blend), pos, new Vector3(90f, yaw, 0f), new Vector3(size.x, size.y, 1f), blend);
        }

        /// <summary>Quad em pe, virado para a camera (miniaturas, Rafa).</summary>
        Item Billboard(string tex, Vector3 bottomCenter, float height)
        {
            var t = Tex(tex);
            float w = height * t.width / t.height;
            var pos = bottomCenter + Vector3.up * height * 0.5f;
            Vector3 dir = camPos - pos; dir.y = 0;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + 180f;
            return Add(quad, Mat(t, Color.white, Vector2.one, 0f, 0.55f), pos, new Vector3(0, yaw, 0), new Vector3(w, height, 1f));
        }

        void Blob(Vector3 pos, float w, float d)
        {
            Flat("blob", pos + Vector3.up * 0.002f, 0f, new Vector2(w, d), true);
        }

        void BuildScene()
        {
            var wood = new Color(1f, 1f, 1f);
            float top = 0.78f;

            // sala 5x5 com teto baixo
            Add(quad, Mat(Tex("tile_floor"), Color.white, new Vector2(8, 8)), new Vector3(0, 0, 0.5f), new Vector3(90, 0, 0), new Vector3(5, 5, 1));
            Add(quad, Mat(black, Color.white, Vector2.one), new Vector3(0, 2.6f, 0.5f), new Vector3(-90, 0, 0), new Vector3(5, 5, 1));
            Add(quad, Mat(Tex("tile_wall"), Color.white, new Vector2(6, 3.1f)), new Vector3(0, 1.3f, 2.3f), new Vector3(0, 0, 0), new Vector3(5, 2.6f, 1));
            Add(quad, Mat(Tex("tile_wall"), Color.white, new Vector2(6, 3.1f)), new Vector3(-2.2f, 1.3f, 0.5f), new Vector3(0, -90, 0), new Vector3(5, 2.6f, 1));
            Add(quad, Mat(Tex("tile_wall"), Color.white, new Vector2(6, 3.1f)), new Vector3(2.2f, 1.3f, 0.5f), new Vector3(0, 90, 0), new Vector3(5, 2.6f, 1));

            // mesa
            Box("tile_wood", wood, new Vector3(0, top - 0.03f, 0.35f), new Vector3(1.5f, 0.06f, 1.0f), 0.5f);
            foreach (var p in new[] { new Vector2(-0.68f, -0.1f), new Vector2(0.68f, -0.1f), new Vector2(-0.68f, 0.8f), new Vector2(0.68f, 0.8f) })
                Box("tile_wood", new Color(0.8f, 0.8f, 0.8f), new Vector3(p.x, (top - 0.06f) * 0.5f, p.y), new Vector3(0.07f, top - 0.06f, 0.07f), 0.5f);
            Blob(new Vector3(0, 0, 0.35f), 2.0f, 1.4f);

            // cadeira do Rafa
            Box("tile_wood", new Color(0.7f, 0.7f, 0.7f), new Vector3(0, 0.45f, 1.25f), new Vector3(0.45f, 0.04f, 0.42f), 0.5f);
            Box("tile_wood", new Color(0.7f, 0.7f, 0.7f), new Vector3(0, 0.85f, 1.45f), new Vector3(0.45f, 0.8f, 0.04f), 0.5f);

            // Rafa atras da mesa
            rafaItem = Billboard("rafa_face", new Vector3(0, 0.7f, 1.2f), 0.8f);
            rafaFace = rafaItem.Mat;
            rafaMask = Mat(Tex("rafa"), Color.white, Vector2.one, 0f, 0.55f);

            // escudo do mestre: painel central e duas abas
            var screenMat = Mat(Tex("dm_screen"), Color.white, Vector2.one);
            Add(quad, screenMat, new Vector3(0, top + 0.13f, 0.72f), new Vector3(8, 0, 0), new Vector3(0.62f, 0.26f, 1));
            var side = Mat(Tex("dm_screen"), new Color(0.8f, 0.8f, 0.8f), new Vector2(0.5f, 1f));
            Add(quad, side, new Vector3(-0.433f, top + 0.13f, 0.634f), new Vector3(8, -35, 0), new Vector3(0.3f, 0.26f, 1));
            Add(quad, side, new Vector3(0.433f, top + 0.13f, 0.634f), new Vector3(8, 35, 0), new Vector3(0.3f, 0.26f, 1));

            Blob(new Vector3(0.45f, top, 0.3f), 0.11f, 0.06f);
            d20Item = Add(d20, Mat(white, new Color(0.55f, 0.12f, 0.14f), Vector2.one), new Vector3(0.45f, top + 0.038f, 0.3f), new Vector3(20, 30, 10), Vector3.one * 0.045f);

            Blob(new Vector3(-0.6f, top, 0.52f), 0.14f, 0.08f);
            Add(cyl, Mat(Tex("can"), Color.white, new Vector2(1, 1)), new Vector3(-0.6f, top + 0.06f, 0.52f), new Vector3(0, 150, 0), new Vector3(0.066f, 0.06f, 0.066f));

            // estante com caixas no fundo, a esquerda
            for (int i = 0; i < 3; i++)
                Box("tile_wood", new Color(0.75f, 0.75f, 0.75f), new Vector3(-1.45f, 0.45f + i * 0.55f, 2.1f), new Vector3(1.1f, 0.04f, 0.35f), 0.5f);
            Box("tile_wood", new Color(0.6f, 0.6f, 0.6f), new Vector3(-2.0f, 0.85f, 2.1f), new Vector3(0.04f, 1.7f, 0.35f), 0.5f);
            Box("tile_wood", new Color(0.6f, 0.6f, 0.6f), new Vector3(-0.9f, 0.85f, 2.1f), new Vector3(0.04f, 1.7f, 0.35f), 0.5f);
            Box("tile_cardboard", Color.white, new Vector3(-1.7f, 0.63f, 2.1f), new Vector3(0.36f, 0.32f, 0.3f), 0f, new Vector3(0, 8, 0));
            Box("tile_cardboard", new Color(0.9f, 0.85f, 0.8f), new Vector3(-1.2f, 0.6f, 2.1f), new Vector3(0.3f, 0.26f, 0.28f), 0f, new Vector3(0, -5, 0));
            Box("tile_cardboard", Color.white, new Vector3(-1.5f, 1.17f, 2.1f), new Vector3(0.42f, 0.36f, 0.3f), 0f, new Vector3(0, -3, 0));
            Box("tile_cardboard", new Color(0.85f, 0.8f, 0.75f), new Vector3(-0.6f, 0.2f, 1.9f), new Vector3(0.45f, 0.4f, 0.4f), 0f, new Vector3(0, 20, 0));

            // posteres
            Add(quad, Mat(Tex("poster_band"), Color.white, Vector2.one), new Vector3(0.75f, 1.55f, 2.29f), new Vector3(0, 0, 3), new Vector3(0.42f, 0.58f, 1));
            Add(quad, Mat(Tex("poster_disco"), Color.white, Vector2.one), new Vector3(1.35f, 1.42f, 2.29f), new Vector3(0, 0, -4), new Vector3(0.42f, 0.58f, 1));

            // lampada: fio, cupula e bulbo seguem o pendulo
            var cordMat = Mat(black, Color.white, Vector2.one);
            var shadeMat = Mat(white, new Color(0.18f, 0.3f, 0.22f), Vector2.one);
            var bulbMat = Mat(white, new Color(1f, 0.92f, 0.7f), Vector2.one, 1f);
            items.Add(new Item { Mesh = cube, Mat = cordMat, Dynamic = () => Matrix4x4.TRS((lampPivot + lampPos) * 0.5f + Vector3.up * 0.04f, LampRot(), new Vector3(0.012f, cordLen, 0.012f)) });
            items.Add(new Item { Mesh = cyl, Mat = shadeMat, Dynamic = () => Matrix4x4.TRS(lampPos + Vector3.up * 0.08f, LampRot(), new Vector3(0.34f, 0.05f, 0.34f)) });
            items.Add(new Item { Mesh = sphere, Mat = bulbMat, Dynamic = () => Matrix4x4.TRS(lampPos + Vector3.up * 0.02f, LampRot(), Vector3.one * 0.09f) });
        }

        // ---------------------------------------------------------------- miniaturas 3D

        const float TableTop = 0.78f;
        List<DeckDark.View.GameApp.MiniView> minis;
        bool showMat;
        int matX, matY, matW, matH;
        readonly Dictionary<string, Material> miniMats = new Dictionary<string, Material>();
        Material baseMat, ringRed, ringGold, matMat;

        /// <summary>Chamado pelo jogo a cada quadro com as pecas do combate (posicoes na tela 480x270).</summary>
        public void SetCombat(List<DeckDark.View.GameApp.MiniView> list, bool mat, int x, int y, int w, int h)
        {
            minis = list; showMat = mat; matX = x; matY = y; matW = w; matH = h;
        }

        /// <summary>Ponto da mesa que aparece no pixel (px, py), pela camera parada (sem o balanco do mouse).</summary>
        Vector3 OnTable(float px, float py)
        {
            var rot0 = Quaternion.LookRotation(camTarget - camPos, Vector3.up);
            float t = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float nx = px / W * 2f - 1f, ny = 1f - py / H * 2f;
            var d = rot0 * new Vector3(nx * t * W / H, ny * t, 1f);
            float k = (TableTop - camPos.y) / d.y;
            return camPos + d * k;
        }

        float WorldPerPixel(Vector3 p)
        {
            var fwd = (camTarget - camPos).normalized;
            return 2f * Vector3.Dot(p - camPos, fwd) * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) / H;
        }

        Material MiniMat(DeckDark.View.GameApp.MiniView v)
        {
            string key = v.Key + (v.Flash ? "#" : "");
            Material m;
            if (miniMats.TryGetValue(key, out m)) return m;
            int w = v.Rows[0].Length, h = v.Rows.Length;
            var tex = new Texture2D(w + 2, h + 2, TextureFormat.RGBA32, false);
            var px = new Color32[(w + 2) * (h + 2)];
            var solid = new bool[w + 2, h + 2];
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    char ch = v.Rows[j][i];
                    if (ch == '.' || ch == ' ') continue;
                    var c = v.Pal(ch);
                    if (!c.HasValue) continue;
                    var col = v.Flash ? new Color32(244, 239, 228, 255) : new Color32(c.Value.R, c.Value.G, c.Value.B, 255);
                    int x = i + 1, y = h - j;   // textura comeca embaixo
                    px[y * (w + 2) + x] = col; solid[x, y] = true;
                }
            // contorno escuro, como tinta de miniatura
            for (int y = 0; y < h + 2; y++)
                for (int x = 0; x < w + 2; x++)
                {
                    if (solid[x, y]) continue;
                    bool nb = (x > 0 && solid[x - 1, y]) || (x < w + 1 && solid[x + 1, y]) || (y > 0 && solid[x, y - 1]) || (y < h + 1 && solid[x, y + 1]);
                    if (nb) px[y * (w + 2) + x] = new Color32(42, 36, 32, 255);
                }
            tex.SetPixels32(px);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            m = Mat(tex, Color.white, Vector2.one, v.Flash ? 0.6f : 0f, 0.6f);
            miniMats[key] = m;
            return m;
        }

        Texture2D GridTexture()
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var paper = new Color32(216, 210, 188, 255); var line = new Color32(180, 196, 200, 255);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    t.SetPixel(x, y, (x % 16 == 0 || y % 16 == 0) ? line : paper);
            t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Repeat; t.Apply();
            return t;
        }

        void Draw(Mesh mesh, Material mat, Matrix4x4 m)
        {
            mat.SetMatrix("_DD_M", m);
            if (mat.SetPass(0)) Graphics.DrawMeshNow(mesh, m);
        }

        /// <summary>Tabuleiro de papel e miniaturas. Roda depois dos opacos da sala.</summary>
        void DrawCombatPieces()
        {
            if (baseMat == null)
            {
                baseMat = Mat(white, new Color(0.2f, 0.19f, 0.17f), Vector2.one);
                ringRed = Mat(white, new Color(0.85f, 0.25f, 0.2f), Vector2.one, 0.7f);
                ringGold = Mat(white, new Color(0.95f, 0.75f, 0.25f), Vector2.one, 0.7f);
                matMat = Mat(GridTexture(), Color.white, Vector2.one);
            }
            var camYaw = Quaternion.Euler(0, Mathf.Atan2(camPos.x - camTarget.x, camPos.z - camTarget.z) * Mathf.Rad2Deg + 180f, 0);

            if (showMat)
            {
                // folha de papel de tamanho fixo, centrada nos pes das miniaturas (fica antes do escudo do mestre)
                float feet = matY + matH * 0.75f;
                var c = OnTable(matX + matW * 0.5f, feet);
                float width = Mathf.Min(1.05f, (OnTable(matX + matW, feet) - OnTable(matX, feet)).magnitude + 0.1f);
                float depth = 0.46f;
                c.z = Mathf.Min(c.z, 0.6f - depth * 0.5f);
                matMat.SetVector("_Tiling", new Vector4(width / 0.07f, depth / 0.07f, 0, 0));
                Draw(quad, matMat, Matrix4x4.TRS(c + Vector3.up * 0.0015f, Quaternion.Euler(90, 0, 0), new Vector3(width, depth, 1)));
            }
            if (minis == null) return;

            foreach (var v in minis)
            {
                var p = OnTable(v.X, v.FeetY);
                float wpp = WorldPerPixel(p);
                int w = v.Rows[0].Length + 2, h = v.Rows.Length + 2;
                float mw = w * 2 * wpp, mh = h * 2 * wpp;
                p += camYaw * Vector3.right * (v.Wobble * wpp) + Vector3.up * (v.Lift * wpp);
                var mat = MiniMat(v);

                // base redonda (e anel de destaque)
                float r = v.Rows[0].Length * 2 * wpp * 0.85f;
                if (v.Ring > 0) Draw(cyl, v.Ring == 1 ? ringRed : ringGold, Matrix4x4.TRS(p + Vector3.up * 0.003f, Quaternion.identity, new Vector3(r * 1.3f, 0.003f, r * 0.9f)));
                if (!v.Lying) Draw(cyl, baseMat, Matrix4x4.TRS(p + Vector3.up * 0.007f, Quaternion.identity, new Vector3(r, 0.007f, r * 0.7f)));

                float flip = v.FaceLeft ? -1f : 1f;
                if (v.Lying)
                {
                    // tombada de lado sobre a mesa
                    var rot = camYaw * Quaternion.Euler(90, 0, v.FaceLeft ? -90 : 90);
                    Draw(quad, mat, Matrix4x4.TRS(p + Vector3.up * 0.004f, rot, new Vector3(mw * flip, mh, 1)));
                }
                else
                {
                    Draw(quad, mat, Matrix4x4.TRS(p + Vector3.up * (0.014f + mh * 0.5f), camYaw, new Vector3(mw * flip, mh, 1)));
                }
            }
        }

        Item d20Item, rafaItem;
        Material rafaFace, rafaMask;

        // ---- controlado pelo jogo ----
        public float LampLevel = 1f;   // brilho da lampada vindo do jogo (inclui as piscadas)
        public float DreadLevel;       // 0..1, deixa a luz mais fria e avermelhada
        public bool MaskOn;
        public float ShakeAmount;
        public RenderTexture Output { get { return rt; } }
        float lampAngleX, lampAngleZ;

        Quaternion LampRot() { return Quaternion.Euler(lampAngleX, 0, lampAngleZ); }

        // ------------------------------------------------------------------ malhas

        static Mesh BuildQuad()
        {
            var m = new Mesh();
            m.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            return m;
        }

        /// <summary>Icosaedro de faces chapadas: o d20.</summary>
        static Mesh BuildIcosahedron()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] f =
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            var verts = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < f.Length; i += 3)
            {
                var a = v[f[i]].normalized; var b = v[f[i + 1]].normalized; var c = v[f[i + 2]].normalized;
                var n = Vector3.Cross(b - a, c - a).normalized;
                int k = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                nrm.Add(n); nrm.Add(n); nrm.Add(n);
                uv.Add(Vector2.zero); uv.Add(Vector2.zero); uv.Add(Vector2.zero);
                tri.Add(k); tri.Add(k + 1); tri.Add(k + 2);
            }
            var m = new Mesh();
            m.SetVertices(verts); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            return m;
        }

        // ------------------------------------------------------------------ quadro a quadro

        void Update()
        {
            float t = Time.time;

#if ENABLE_INPUT_SYSTEM
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);
#else
            Vector2 mouse = Input.mousePosition;
#endif
            if (rafaItem != null) rafaItem.Mat = MaskOn ? rafaMask : rafaFace;

            // a camera acompanha o mouse de leve, como quem mexe a cabeca
            var targetSway = new Vector2(mouse.x / Mathf.Max(1, Screen.width) - 0.5f, mouse.y / Mathf.Max(1, Screen.height) - 0.5f);
            sway = Vector2.Lerp(sway, targetSway, 1f - Mathf.Exp(-4f * Time.deltaTime));

            // pendulo da lampada
            lampAngleX = Mathf.Sin(t * 0.7f) * 3.5f;
            lampAngleZ = Mathf.Sin(t * 0.53f + 1.3f) * 2.5f;
            lampPos = lampPivot + LampRot() * (Vector3.down * cordLen);

            UpdateStory();
            RenderScene();
        }

        void RenderScene()
        {
            if (shader == null) return;

            var shakeV = ShakeAmount > 0 ? Random.insideUnitSphere * ShakeAmount * 0.004f : Vector3.zero;
            var eye = camPos + new Vector3(sway.x * 0.04f, sway.y * 0.02f, 0) + shakeV;
            var look = camTarget + new Vector3(sway.x * 0.15f, sway.y * 0.08f, 0);
            var rot = Quaternion.LookRotation(look - eye, Vector3.up);
            // matriz de visao do Unity: camera olha para -Z
            var view = Matrix4x4.TRS(eye, rot, new Vector3(1, 1, -1)).inverse;
            var proj = Matrix4x4.Perspective(fov, (float)W / H, 0.03f, 20f);
            var vp = GL.GetGPUProjectionMatrix(proj, true) * view;

            Shader.SetGlobalMatrix("_DD_VP", vp);
            Shader.SetGlobalVector("_DD_CamPos", eye);
            Shader.SetGlobalVector("_LampPos", lampPos);
            Shader.SetGlobalColor("_LampColor", Color.Lerp(new Color(1f, 0.82f, 0.55f), new Color(0.95f, 0.55f, 0.5f), DreadLevel));
            Shader.SetGlobalFloat("_LampIntensity", 1.55f * LampLevel);
            Shader.SetGlobalFloat("_LampRange", 3.6f);
            Shader.SetGlobalColor("_Ambient", Color.Lerp(new Color(0.07f, 0.06f, 0.1f), new Color(0.09f, 0.03f, 0.04f), DreadLevel));
            Shader.SetGlobalFloat("_Levels", 6f);

            // o d20 gira devagar, como se tivesse acabado de parar
            if (d20Item != null)
                d20Item.M = Matrix4x4.TRS(d20Item.M.GetColumn(3), Quaternion.Euler(20, 30 + Mathf.Sin(Time.time * 0.8f) * 4f, 10), Vector3.one * 0.045f);

            var prev = RenderTexture.active;
            Graphics.SetRenderTarget(rt);
            GL.Clear(true, true, new Color(0.02f, 0.015f, 0.02f));
            GL.PushMatrix();
            // opacos primeiro, depois os transparentes (sombras) do mais longe para o mais perto
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) DrawCombatPieces();
                foreach (var it in items)
                {
                    if (it.Blend != (pass == 1)) continue;
                    var m = it.Dynamic != null ? it.Dynamic() : it.M;
                    it.Mat.SetMatrix("_DD_M", m);
                    if (!it.Mat.SetPass(0)) continue;
                    Graphics.DrawMeshNow(it.Mesh, m);
                }
            }
            GL.PopMatrix();
            RenderTexture.active = prev;
        }
    }
}
