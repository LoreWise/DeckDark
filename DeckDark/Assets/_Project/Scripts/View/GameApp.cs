using System;
using System.Collections.Generic;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>O que o jogo precisa do "mundo de fora" (Unity ou teste).</summary>
    public interface IGameHost
    {
        void PlaySfx(string id, float volume);
        int LoadInt(string key, int defaultValue);
        void SaveInt(string key, int value);
        void DeleteKey(string key);
        string LoadString(string key, string defaultValue);   // save da run e codex (SaveHooks, CodexView)
        void SaveString(string key, string value);
        void Quit();
    }

    public struct GameInput
    {
        public int X, Y;      // posicao do mouse em pixels virtuais
        public bool Click;
        public bool Escape;
    }

    public enum GameScreen { Menu, NewGame, Options, Intro, Map, Combat, Reward, Event, Treasure, Tavern, Death, Victory, Notes, Codex }

    public enum GameSpeed { Slow, Normal, Fast }

    /// <summary>
    /// O prototipo inteiro: telas, fluxo da run e animacoes.
    /// A logica de regras fica em DeckDark.Core; aqui so se decide o que mostrar e quando.
    /// Arquivos: GameApp (fluxo), Menus (menu, opcoes, pausa), GameRender (desenho) e Basement (cenario).
    /// </summary>
    public partial class GameApp
    {
        public const int W = 480, H = 270;
        public const string Title = "DARK DECK";

        readonly IGameHost host;
        public readonly PixelCanvas Canvas = new PixelCanvas(W, H);

        GameScreen screen = GameScreen.Menu;
        float time;
        GameInput input;
        readonly Random fxRng = new Random(1234);

        // Progresso salvo entre sessoes
        int deaths, wins, sessions;
        int unlockedRules;     // maior versao de Homebrew liberada
        int selectedRules;     // versao escolhida para a proxima partida
        GameSpeed speed;

        RunState run;
        Combat combat;

        // ---------- Fala do amigo ----------
        class SpeechLine
        {
            public string Text;
            public bool WaitClick;
            public Action OnStart;
        }

        readonly Queue<SpeechLine> speechQueue = new Queue<SpeechLine>();
        SpeechLine speech;
        float speechTyped;
        float speechHold;
        bool scriptActive;
        Action scriptDone;
        bool maskOn;

        // ---- mesa 3D: o cenario vem do Unity, o jogo desenha so a interface por cima ----
        public bool Use3D;
        PlayerClass selectedClass;
        public bool MaskOn { get { return maskOn; } }
        public float Lamp { get { return LampIntensity; } }
        public float DreadLevel { get { return Dread; } }
        public bool CombatScreen { get { return screen == GameScreen.Combat; } }
        public float Shake { get { return shake; } }

        // ---------- Sequenciador de animacoes ----------
        class Beat { public float At; public Action Act; }
        readonly List<Beat> beats = new List<Beat>();
        float beatCursor;
        bool Busy { get { return beats.Count > 0; } }

        // ---------- Efeitos ----------
        class DiceFx
        {
            public int X, Y;
            public TestRoll Roll;
            public float Rolling;       // tempo restante rolando
            public float Life;          // tempo restante visivel
            public bool Hidden;
            public int Announced;
            public string Label;
            public int Flicker;
        }
        DiceFx dice;

        class Floater { public float X, Y, T; public string Text; public Rgb Color; }
        readonly List<Floater> floaters = new List<Floater>();

        float shake;
        float lampDip;
        float lampFlash;

        // Telas de escolha
        RelicDef treasureRelic;
        EventDef currentEvent;
        string eventResult;
        bool eventResolved;
        string tavernResult;
        bool newRulesUnlocked;

        public GameApp(IGameHost host)
        {
            this.host = host;
            LoadProgress();
            BuildStaticScene();
        }

        void LoadProgress()
        {
            deaths = host.LoadInt("deaths", 0);
            wins = host.LoadInt("wins", 0);
            sessions = host.LoadInt("sessions", 0);
            // Quem jogou a versao anterior ja liberou versoes com as vitorias antigas
            unlockedRules = Math.Max(1, Math.Min(Homebrew.MaxVersion, host.LoadInt("unlocked", 1 + wins)));
            selectedRules = Math.Max(1, Math.Min(unlockedRules, host.LoadInt("selected", unlockedRules)));
            selectedClass = (PlayerClass)Math.Max(0, Math.Min(1, host.LoadInt("class", 0)));
            speed = (GameSpeed)Math.Max(0, Math.Min(2, host.LoadInt("speed", (int)GameSpeed.Normal)));
            maskOn = sessions > 0;
            LoadStory();
        }

        /// <summary>Multiplicador de todas as animacoes. Escolhido em Options.</summary>
        float SpeedFactor
        {
            get
            {
                switch (speed)
                {
                    case GameSpeed.Slow: return 0.6f;
                    case GameSpeed.Fast: return 1.8f;
                    default: return 1f;
                }
            }
        }

        bool InRun
        {
            get
            {
                return run != null && screen != GameScreen.Menu && screen != GameScreen.NewGame && screen != GameScreen.Options;
            }
        }

        /// <summary>0 = tudo normal, 1 = pesadelo. Cresce com o andar e com as mortes.</summary>
        float Dread
        {
            get
            {
                float d = 0.08f * Math.Min(deaths, 5);
                if (InRun) d += 0.5f * run.Current.Layer / run.LastLayer;
                if (combat != null && combat.HiddenRolls && screen == GameScreen.Combat) d += 0.2f;
                if (screen == GameScreen.Death) d += 0.15f;
                return Math.Min(1f, d);
            }
        }

        void Sfx(string id, float vol = 1f) { host.PlaySfx(id, vol); }

        // =====================================================================
        // Loop principal
        // =====================================================================

        public void Update(float dt, GameInput inp)
        {
            if (dt > 0.1f) dt = 0.1f;
            input = inp;
            time += dt;

            if (input.Escape) HandleEscape();

            if (!paused)
            {
                float adt = dt * SpeedFactor;
                UpdateBeats(adt);
                UpdateSpeech(adt);
                UpdateFx(dt, adt);
                StoryUpdate(dt);
            }

            if (input.Click) HandleClick();
            SaveHooksTick();   // auto-save no mapa e registro do codex (SaveHooks.cs)
            Render();
        }

        void UpdateBeats(float dt)
        {
            if (beats.Count == 0) { beatCursor = 0; return; }
            beatCursor += dt;
            int guard = 0;
            while (beats.Count > 0 && beats[0].At <= beatCursor && guard++ < 50)
            {
                var b = beats[0];
                beats.RemoveAt(0);
                b.Act();
            }
            if (beats.Count == 0) beatCursor = 0;
        }

        /// <summary>Agenda uma acao para depois da ultima agendada.</summary>
        void Then(float delay, Action a)
        {
            float last = beats.Count > 0 ? beats[beats.Count - 1].At : beatCursor;
            beats.Add(new Beat { At = last + delay, Act = a });
        }

        void UpdateSpeech(float dt)
        {
            if (speech == null)
            {
                if (speechQueue.Count > 0) StartLine(speechQueue.Dequeue());
                return;
            }
            speechTyped += dt * 38f;
            if (speechTyped >= speech.Text.Length)
            {
                speechHold += dt;
                if (!speech.WaitClick && speechHold > 2.8f + speech.Text.Length * 0.035f)
                {
                    speech = null;
                    if (speechQueue.Count > 0) StartLine(speechQueue.Dequeue());
                }
            }
        }

        void StartLine(SpeechLine l)
        {
            speech = l;
            speechTyped = 0;
            speechHold = 0;
            if (l.OnStart != null) l.OnStart();
        }

        string FriendName { get { return !maskOn ? "RAFA" : (Dread > 0.6f ? "???" : "THE MASTER"); } }

        /// <summary>Um comentario do amigo que some sozinho e nao trava o jogo.</summary>
        void Say(string text)
        {
            if (scriptActive) return;
            speechQueue.Clear();
            StartLine(new SpeechLine { Text = text });
        }

        /// <summary>Uma sequencia de falas que espera cliques. Trava o resto ate terminar.</summary>
        void Script(Action done, params SpeechLine[] lines)
        {
            speechQueue.Clear();
            speech = null;
            foreach (var l in lines) { l.WaitClick = true; speechQueue.Enqueue(l); }
            scriptActive = true;
            scriptDone = done;
        }

        SpeechLine L(string text, Action onStart = null)
        {
            return new SpeechLine { Text = text, OnStart = onStart };
        }

        void AdvanceScript()
        {
            if (speech == null) return;
            if (speechTyped < speech.Text.Length) { speechTyped = speech.Text.Length; return; }
            Sfx("click", 0.5f);
            speech = null;
            if (speechQueue.Count > 0) StartLine(speechQueue.Dequeue());
            else
            {
                scriptActive = false;
                var d = scriptDone;
                scriptDone = null;
                if (d != null) d();
            }
        }

        void ClearSpeech()
        {
            speechQueue.Clear();
            speech = null;
            scriptActive = false;
            scriptDone = null;
        }

        /// <summary>dt = tempo real (luz, tremor); adt = tempo de animacao (afetado pela velocidade).</summary>
        void UpdateFx(float dt, float adt)
        {
            if (dice != null)
            {
                if (dice.Rolling > 0)
                {
                    dice.Rolling -= adt;
                    if (((int)(time * 30)) % 2 == 0) dice.Flicker = fxRng.Next(1, 21);
                }
                else dice.Life -= adt;
                if (dice.Life <= 0) dice = null;
            }
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                floaters[i].T += adt;
                floaters[i].Y -= adt * 10f;
                if (floaters[i].T > 1.8f) floaters.RemoveAt(i);
            }
            shake = Math.Max(0, shake - dt * 12f);
            playerHurt = Math.Max(0, playerHurt - adt * 2.5f);
            UpdateFx(adt);
            foreach (var v in enemyViews)
            {
                v.Hurt = Math.Max(0, v.Hurt - adt * 2.5f);
                if (v.Dead || v.Fled) v.Fade = Math.Min(1f, v.Fade + adt * 0.45f);
            }
            lampFlash = Math.Max(0, lampFlash - dt * 2f);

            // Piscadas da lampada: mais frequentes conforme o clima piora
            lampDip = Math.Max(0, lampDip - dt * 4f);
            if (fxRng.NextDouble() < dt * (0.05 + Dread * 0.6))
            {
                lampDip = 0.4f + (float)fxRng.NextDouble() * 0.5f * (0.4f + Dread);
                if (lampDip > 0.55f) Sfx("flicker", 0.4f);
            }
        }

        void Float(float x, float y, string text, Rgb c)
        {
            floaters.Add(new Floater { X = x, Y = y, Text = text, Color = c });
        }

        // =====================================================================
        // Cliques
        // =====================================================================

        void HandleClick()
        {
            if (paused) { ClickPause(); return; }

            switch (screen)
            {
                case GameScreen.Menu: ClickMenu(); return;
                case GameScreen.NewGame: ClickNewGame(); return;
                case GameScreen.Options: ClickOptions(); return;
                case GameScreen.Notes: ClickNotes(); return;
                case GameScreen.Codex: ClickCodex(); return;
            }

            if (InRun && Hover(MenuTagX, MenuTagY, MenuTagW, MenuTagH)) { OpenPause(); return; }
            if (scriptActive) { AdvanceScript(); return; }
            if (Busy) return;

            switch (screen)
            {
                case GameScreen.Map: ClickMap(); break;
                case GameScreen.Combat: ClickCombat(); break;
                case GameScreen.Reward: ClickReward(); break;
                case GameScreen.Event: ClickEvent(); break;
                case GameScreen.Treasure: ClickTreasure(); break;
                case GameScreen.Tavern: ClickTavern(); break;
            }
        }

        static bool Inside(GameInput i, int x, int y, int w, int h)
        {
            return i.X >= x && i.X < x + w && i.Y >= y && i.Y < y + h;
        }

        bool Hover(int x, int y, int w, int h) { return Inside(input, x, y, w, h); }

        // =====================================================================
        // Fluxo da sessao
        // =====================================================================

        void StartSession()
        {
            Sfx("click");
            sessions++;
            host.SaveInt("sessions", sessions);
            host.SaveInt("selected", selectedRules);
            BuildStaticScene();
            run = new RunState(Environment.TickCount, selectedRules, selectedClass);
            combat = null;
            beats.Clear();
            dice = null;
            floaters.Clear();
            screen = GameScreen.Intro;
            shownPlayerHp = run.Sheet.Hp;

            var lines = new List<SpeechLine>();
            if (sessions == 1)
            {
                lines.Add(L("OK, SIT DOWN. I SPENT ALL WEEK MAKING THIS CAMPAIGN."));
                lines.Add(L("WAIT. I FOUND SOMETHING IN ONE OF MY DAD'S BOXES."));
                lines.Add(L("THERE. NOW I'M THE MASTER.", () => { maskOn = true; lampDip = 1f; shake = 2f; Sfx("flicker", 0.8f); Sfx("curse", 0.6f); }));
            }
            else if (wins == 0)
            {
                string[] again =
                {
                    "YOU'RE BACK.",
                    "AGAIN.",
                    "YOU ALWAYS PICK THE WARRIOR.",
                    "THERE ARE MORE SHEETS IN THE DRAWER. LOTS OF THEM.",
                    "YOUR MOM ISN'T COMING TO PICK YOU UP. I ASKED.",
                };
                lines.Add(L(again[Math.Min(deaths, again.Length - 1)]));
            }
            else
            {
                lines.Add(L("ANOTHER CAMPAIGN. SAME TABLE. SAME DICE."));
            }
            if (selectedRules > 1) lines.Add(L("HOMEBREW V" + selectedRules + ".0 TODAY. YOU ASKED FOR IT."));
            lines.Add(L("YOUR WARRIOR REACHES THE ENTRANCE OF THE FACELESS KING'S DUNGEON."));
            lines.Add(L("THREE ROOMS UNTIL YOU REACH HIM. CHOOSE YOUR PATH."));
            Script(() => { screen = GameScreen.Map; }, lines.ToArray());
        }

        void EndRunToMenu()
        {
            ClearSpeech();
            beats.Clear();
            dice = null;
            floaters.Clear();
            run = null;
            combat = null;
            paused = false;
            screen = GameScreen.Menu;
            BuildStaticScene();
            StoryAfterRun();
        }

        // ---------------- Mapa ----------------

        void ClickMap()
        {
            var node = HoveredNode();
            if (node == null || !run.CanTravelTo(node)) return;
            Sfx("pencil");
            run.TravelTo(node);
            Then(0.5f, () => EnterNode(node));
        }

        void EnterNode(MapNode node)
        {
            switch (node.Type)
            {
                case NodeType.Combat:
                case NodeType.Elite:
                case NodeType.Boss:
                    StartCombat(run.EncounterForCurrent(), node.Type);
                    break;
                case NodeType.Event:
                    currentEvent = run.NextEvent();
                    eventResult = null;
                    eventResolved = false;
                    screen = GameScreen.Event;
                    if (currentEvent.MasterLine != null) Say(currentEvent.MasterLine);
                    break;
                case NodeType.Treasure:
                    treasureRelic = run.RollRelic();
                    screen = GameScreen.Treasure;
                    Say("A CHEST. GO AHEAD. IT'S NOT A TRAP THIS TIME.");
                    break;
                case NodeType.Tavern:
                    tavernResult = null;
                    screen = GameScreen.Tavern;
                    Say("SNACK BREAK.");
                    break;
            }
        }

        void ReturnToMap()
        {
            screen = GameScreen.Map;
            dice = null;
            combat = null;
            int layer = run.Current.Layer;
            if (layer == 1) Say("IT'S GETTING LATE, HUH?");
            else if (layer == 2) Say("YOUR MOM CALLED. I TOLD HER YOU LEFT.");
            else if (layer == 3) Say("NOBODY'S COMING DOWN HERE. DON'T WORRY.");
            else if (layer == 4) Say("THE LIGHT IS FINE. IT'S ALWAYS BEEN LIKE THIS.");
            else if (layer == 5) Say("HE'S WAITING FOR YOU.");
        }

        void ShowRoll(TestRoll roll, bool hidden, int announced, string label)
        {
            dice = new DiceFx
            {
                X = 257, Y = 146, Roll = roll, Rolling = hidden ? 1.6f : 1.1f, Life = 1.8f,
                Hidden = hidden, Announced = announced, Label = label
            };
            Sfx("dice");
        }

        // ---------------- Recompensa ----------------

        void ClickReward()
        {
            for (int i = 0; i < rewardCards.Length; i++)
            {
                if (Hover(RewardCardX(i), RewardY, CardW, CardH))
                {
                    Sfx("pencil");
                    run.Sheet.Deck.Add(rewardCards[i]);
                    Then(0.5f, ReturnToMap);
                    return;
                }
            }
            if (Hover(SkipX, SkipY, SkipW, SkipH))
            {
                Sfx("click");
                ReturnToMap();
            }
        }

        // ---------------- Evento ----------------

        void ClickEvent()
        {
            if (eventResolved)
            {
                if (Hover(ContinueX, ContinueY, ContinueW, ContinueH)) { Sfx("click"); ReturnToMap(); }
                return;
            }
            for (int i = 0; i < currentEvent.Choices.Length; i++)
            {
                if (!Hover(ChoiceX, ChoiceY(i), ChoiceW, ChoiceH)) continue;
                var ch = currentEvent.Choices[i];
                Sfx("click");
                if (!ch.HasTest)
                {
                    string g = run.Apply(ch.Success);
                    eventResult = ch.Success.Text + (g != null ? " (" + g + ")" : "");
                    Sfx(ch.Success.Kind == OutcomeKind.None ? "click" : "heal", 0.6f);
                    eventResolved = true;
                    return;
                }
                var roll = run.Dice.Test(run.Sheet.Mod(ch.TestAttr), ch.Dc, false, false);
                ShowRoll(roll, false, 0, CharacterSheet.AttrName(ch.TestAttr) + " CHECK");
                dice.X = 404; dice.Y = 150;
                Then(1.5f, () =>
                {
                    var outcome = roll.Success ? ch.Success : ch.Failure;
                    string gains = run.Apply(outcome);
                    eventResult = (roll.Success ? "SUCCESS. " : "FAILURE. ") + outcome.Text;
                    if (gains != null) eventResult += " (" + gains + ")";
                    if (outcome.Kind == OutcomeKind.AddCurse) { Sfx("curse"); lampDip = 1f; Say("NOW IT'S IN YOUR DECK."); }
                    else if (outcome.Kind == OutcomeKind.LoseHp) { Sfx("hit"); shake = 3f; }
                    else Sfx(roll.Success ? "heal" : "miss");
                    eventResolved = true;
                });
                return;
            }
        }

        // ---------------- Tesouro ----------------

        void ClickTreasure()
        {
            if (!Hover(ContinueX, ContinueY, ContinueW, ContinueH)) return;
            if (treasureRelic != null)
            {
                run.Sheet.Relics.Add(treasureRelic);
                Sfx("heal");
            }
            else Sfx("click");
            ReturnToMap();
        }

        // ---------------- Lanche ----------------

        void ClickTavern()
        {
            if (tavernResult != null)
            {
                if (Hover(ContinueX, ContinueY, ContinueW, ContinueH)) { Sfx("click"); ReturnToMap(); }
                return;
            }
            if (Hover(ChoiceX, ChoiceY(0), ChoiceW, ChoiceH))
            {
                int amount = (int)(run.Sheet.MaxHp * Homebrew.TavernHealFraction(run.RulesVersion));
                int before = run.Sheet.Hp;
                run.Sheet.Heal(amount);
                tavernResult = "YOU REST AND RECOVER " + (run.Sheet.Hp - before) + " HP. THE CHIPS ARE STALE, LIKE THE BAG HAS BEEN OPEN FOR DAYS.";
                Sfx("heal");
            }
            else if (Hover(ChoiceX, ChoiceY(1), ChoiceW, ChoiceH))
            {
                run.Sheet.Scores[(int)Attr.STR] += 2;
                tavernResult = "YOU TRAIN WITH THE SWORD. STR +2. YOUR ATTACKS HIT HARDER. YOUR REAL ARM IS SORE.";
                Sfx("pencil");
            }
        }

        // ---------------- Morte e vitoria ----------------

        void PlayerDied()
        {
            Sfx("death");
            deaths++;
            host.SaveInt("deaths", deaths);
            BuildStaticScene();
            screen = GameScreen.Death;
            lampDip = 1f;
            Then(1.8f, () =>
            {
                Script(EndRunToMenu,
                    L("YOUR WARRIOR DIED."),
                    L("IT'S FINE. IT HAPPENS TO EVERYONE WHO SITS THERE."),
                    L("CRUMPLE THE SHEET AND THROW IT ON THE FLOOR. WE START AGAIN."));
            });
        }

        void Victory()
        {
            wins++;
            host.SaveInt("wins", wins);
            newRulesUnlocked = false;
            if (run.RulesVersion >= unlockedRules && unlockedRules < Homebrew.MaxVersion)
            {
                unlockedRules++;
                selectedRules = unlockedRules;
                host.SaveInt("unlocked", unlockedRules);
                host.SaveInt("selected", selectedRules);
                newRulesUnlocked = true;
            }
            screen = GameScreen.Victory;
            lampFlash = 1f;
            Sfx("crit");
            Then(1.5f, () =>
            {
                string last = newRulesUnlocked
                    ? "I WROTE SOME NEW RULES. HOMEBREW V" + unlockedRules + ".0."
                    : "THERE ARE NO NEW RULES LEFT. I'LL THINK OF SOMETHING.";
                Script(EndRunToMenu,
                    L("..."),
                    L("YOU BEAT THE FACELESS KING."),
                    L("COOL. VERY COOL."),
                    L("BUT THE CAMPAIGN DOESN'T END HERE. IT NEVER ENDS."),
                    L(last));
            });
        }
    }
}
