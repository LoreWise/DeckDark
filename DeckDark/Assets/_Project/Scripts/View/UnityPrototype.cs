using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DeckDark.View
{
    /// <summary>
    /// Ponte entre o prototipo e o Unity.
    /// Ele se cria sozinho ao apertar Play em qualquer cena, desenha a tela de pixels
    /// ampliada, toca os sons e guarda o progresso (mortes, vitorias) no PlayerPrefs.
    ///
    /// Para zerar o progresso: menu Options > Reset Progress, ou clique com o
    /// botao direito no componente e escolha "Zerar progresso".
    /// </summary>
    public class UnityPrototype : MonoBehaviour, IGameHost
    {
        /// <summary>Desligue isto quando o jogo tiver cenas proprias e nao quiser mais o prototipo automatico.</summary>
        public static bool AutoStart = true;

        GameApp game;
        Texture2D screenTex;
        Texture2D blackTex;
        Color32[] buffer;
        Rect drawRect;
        float scale = 1f;
        float startTime;

        AudioSource sfxSource;
        AudioSource droneSource;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!AutoStart) return;
            if (FindAnyObjectByType<UnityPrototype>() != null) return;
            var go = new GameObject("DeckDark Prototype");
            go.AddComponent<UnityPrototype>();
        }

        void Awake()
        {
            Application.targetFrameRate = 60;

            screenTex = new Texture2D(GameApp.W, GameApp.H, TextureFormat.RGBA32, false);
            screenTex.filterMode = FilterMode.Point;
            screenTex.wrapMode = TextureWrapMode.Clamp;
            blackTex = new Texture2D(1, 1);
            blackTex.SetPixel(0, 0, Color.black);
            blackTex.Apply();
            buffer = new Color32[GameApp.W * GameApp.H];

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            SetupAudio();
            game = new GameApp(this);
            startTime = Time.unscaledTime;
            Set3D(PlayerPrefs.GetInt("deckdark_use3d", 1) == 1);
        }

        void SetupAudio()
        {
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            foreach (var kv in SfxSynth.BuildAll())
            {
                var clip = AudioClip.Create(kv.Key, kv.Value.Length, 1, SfxSynth.Rate, false);
                clip.SetData(kv.Value, 0);
                clips[kv.Key] = clip;
            }

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            droneSource = gameObject.AddComponent<AudioSource>();
            droneSource.clip = clips["drone"];
            droneSource.loop = true;
            droneSource.volume = 0.35f;
            droneSource.Play();
        }

        DeckDark.Test3D.Basement3DTest table3D;
        bool use3D;

        void Set3D(bool on)
        {
            use3D = on;
            if (on && table3D == null) table3D = DeckDark.Test3D.Basement3DTest.Open();
            if (table3D != null) table3D.enabled = on;
            game.Use3D = on;
            PlayerPrefs.SetInt("deckdark_use3d", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        void Update()
        {
            // F3 alterna entre a mesa 3D e o cenario 2D antigo
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) Set3D(!use3D);
#else
            if (Input.GetKeyDown(KeyCode.F3)) Set3D(!use3D);
#endif
            ComputeRect();

            var input = new GameInput();
            Vector2 mouse = Vector2.zero;
            bool click = false;
            bool escape = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mouse = Mouse.current.position.ReadValue();
                click = Mouse.current.leftButton.wasPressedThisFrame;
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame) click = true;
                if (Keyboard.current.escapeKey.wasPressedThisFrame) escape = true;
            }
#else
            mouse = Input.mousePosition;
            click = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
            escape = Input.GetKeyDown(KeyCode.Escape);
#endif
            // Mouse do Unity comeca embaixo; o prototipo comeca em cima
            float guiY = Screen.height - mouse.y;
            input.X = Mathf.FloorToInt((mouse.x - drawRect.x) / scale);
            input.Y = Mathf.FloorToInt((guiY - drawRect.y) / scale);
            // ignora o clique no botao Play do editor, que chega no primeiro frame
            input.Click = click && Time.unscaledTime - startTime > 0.4f;
            input.Escape = escape;

            game.Update(Time.unscaledDeltaTime, input);
            if (use3D && table3D != null)
            {
                table3D.LampLevel = game.Lamp * game.StoryLampFactor;
                table3D.DreadLevel = game.DreadLevel;
                table3D.MaskOn = game.MaskOn;
                table3D.ShakeAmount = game.Shake;
                table3D.StoryStage = game.StoryStage;
                table3D.StoryWizard = game.StoryWizard;
                table3D.SetCombat(game.Minis, game.ShowMat3D, game.MatLeft, game.MatTop, game.MatWidth, game.MatHeight);
            }
            UploadCanvas();
        }

        void ComputeRect()
        {
            float sx = (float)Screen.width / GameApp.W;
            float sy = (float)Screen.height / GameApp.H;
            float s = Mathf.Min(sx, sy);
            // escala inteira quando possivel, para os pixels ficarem todos do mesmo tamanho
            if (s >= 2f) s = Mathf.Floor(s);
            scale = s;
            float w = GameApp.W * s, h = GameApp.H * s;
            drawRect = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        }

        void UploadCanvas()
        {
            var px = game.Canvas.Px;
            var al = game.Canvas.A;
            bool alpha = use3D;
            int w = GameApp.W, h = GameApp.H;
            for (int y = 0; y < h; y++)
            {
                int src = y * w;
                int dst = (h - 1 - y) * w; // texturas do Unity comecam embaixo
                for (int x = 0; x < w; x++)
                {
                    var p = px[src + x];
                    buffer[dst + x] = new Color32(p.R, p.G, p.B, alpha ? al[src + x] : (byte)255);
                }
            }
            screenTex.SetPixels32(buffer);
            screenTex.Apply(false);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);
            if (use3D && table3D != null) GUI.DrawTexture(drawRect, table3D.Output, ScaleMode.StretchToFill, false);
            GUI.DrawTexture(drawRect, screenTex, ScaleMode.StretchToFill, use3D);
        }

        // ---------------- IGameHost ----------------

        public void PlaySfx(string id, float volume)
        {
            AudioClip clip;
            if (!clips.TryGetValue(id, out clip)) return;
            sfxSource.pitch = Random.Range(0.94f, 1.06f);
            sfxSource.PlayOneShot(clip, volume * 0.8f);
        }

        public int LoadInt(string key, int defaultValue) { return PlayerPrefs.GetInt("deckdark_" + key, defaultValue); }

        public void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey("deckdark_" + key);
            PlayerPrefs.Save();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SaveInt(string key, int value)
        {
            PlayerPrefs.SetInt("deckdark_" + key, value);
            PlayerPrefs.Save();
        }

        [ContextMenu("Zerar progresso")]
        public void ResetProgress()
        {
            foreach (var k in new[] { "deaths", "wins", "sessions", "unlocked", "selected" })
                PlayerPrefs.DeleteKey("deckdark_" + k);
            foreach (var k in GameApp.StoryKeys) PlayerPrefs.DeleteKey("deckdark_" + k);
            PlayerPrefs.Save();
            game = new GameApp(this);
            Debug.Log("DeckDark: progresso zerado.");
        }
    }
}
