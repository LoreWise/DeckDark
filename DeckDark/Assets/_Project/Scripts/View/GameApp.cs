using System;
using System.Collections.Generic;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>O que o jogo precisa do "mundo de fora" (Unity ou teste).</summary>
    public interface IGameHost
    {
        void PlaySfx(string id, float volume);
        int LoadInt(string key);
        void SaveInt(string key, int value);
    }

    public struct GameInput
    {
        public int X, Y;      // posicao do mouse em pixels virtuais
        public bool Click;
    }

    public enum GameScreen { Title, Intro, Map, Combat, Reward, Event, Treasure, Tavern, Death, Victory }

    /// <summary>
    /// O prototipo inteiro: telas, fluxo da run e animacoes.
    /// A logica de regras fica em DeckDark.Core; aqui so se decide o que mostrar e quando.
    /// Esta dividido em tres arquivos: GameApp (fluxo), GameRender (desenho) e Basement (cenario).
    /// </summary>
    public partial class GameApp
    {
        public const int W = 480, H = 270;

        readonly IGameHost host;
        public readonly PixelCanvas Canvas = new PixelCanvas(W, H);

        GameScreen screen = GameScreen.Title;
        float time;
        GameInput input;
        readonly Random fxRng = new Random(1234);

        // Progresso salvo entre sessoes
        int deaths, wins, sessions;

        RunState run;
        Combat combat;

        // ---------- Fala do amigo ----------
        class SpeechLine
        {
            public string Speaker;
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
        float enemyHurt, playerHurt, enemyFade;
        int shownEnemyHp, shownPlayerHp, shownBlock, shownEnemyBlock;
        float lampDip;
        float lampFlash;

        // Telas de escolha
        CardDef[] rewardCards;
        RelicDef treasureRelic;
        EventDef currentEvent;
        string eventResult;
        bool eventResolved;
        string tavernResult;

        public GameApp(IGameHost host)
        {
            this.host = host;
            deaths = host.LoadInt("deaths");
            wins = host.LoadInt("wins");
            sessions = host.LoadInt("sessions");
            maskOn = sessions > 0;
            BuildStaticScene();
        }

        int RulesVersion { get { return Math.Min(1 + wins, HouseRules.Texts.Length); } }

        /// <summary>0 = tudo normal, 1 = pesadelo. Cresce com o andar e com as mortes.</summary>
        float Dread
        {
            get
            {
                float d = 0.08f * Math.Min(deaths, 5);
                if (run != null && screen != GameScreen.Title) d += 0.12f * run.Current.Layer;
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
            time += dt;
            input = inp;

            UpdateBeats(dt);
            UpdateSpeech(dt);
            UpdateFx(dt);

            if (input.Click) HandleClick();
            Render();
        }

        void UpdateBeats(float dt)
        {
            if (beats.Count == 0) { beatCursor = 0; return; }
            beatCursor += dt;
            // executa em ordem; uma acao pode agendar outras
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
            speechTyped += dt * 45f;
            if (speechTyped >= speech.Text.Length)
            {
                speechHold += dt;
                if (!speech.WaitClick && speechHold > 2.2f + speech.Text.Length * 0.03f)
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

        string FriendName { get { return !maskOn ? "RAFA" : (Dread > 0.6f ? "???" : "O MESTRE"); } }

        /// <summary>Um comentario do amigo que some sozinho e nao trava o jogo.</summary>
        void Say(string text)
        {
            if (scriptActive) return;
            speechQueue.Clear();
            StartLine(new SpeechLine { Speaker = FriendName, Text = text });
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

        void UpdateFx(float dt)
        {
            if (dice != null)
            {
                if (dice.Rolling > 0)
                {
                    dice.Rolling -= dt;
                    dice.Flicker = fxRng.Next(1, 21);
                }
                else dice.Life -= dt;
                if (dice.Life <= 0) dice = null;
            }
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                floaters[i].T += dt;
                floaters[i].Y -= dt * 14f;
                if (floaters[i].T > 1.3f) floaters.RemoveAt(i);
            }
            shake = Math.Max(0, shake - dt * 12f);
            enemyHurt = Math.Max(0, enemyHurt - dt * 3f);
            playerHurt = Math.Max(0, playerHurt - dt * 3f);
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
            if (scriptActive) { AdvanceScript(); return; }
            if (Busy) return;

            switch (screen)
            {
                case GameScreen.Title: StartSession(); break;
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
            BuildStaticScene();
            run = new RunState(Environment.TickCount, RulesVersion);
            combat = null;
            screen = GameScreen.Intro;
            shownPlayerHp = run.Sheet.Hp;

            var lines = new List<SpeechLine>();
            if (sessions == 1)
            {
                lines.Add(L("BELEZA, SENTA AÍ. PASSEI A SEMANA INTEIRA FAZENDO ESSA CAMPANHA."));
                lines.Add(L("PERA. ACHEI UMA COISA NUMA CAIXA DO MEU PAI."));
                lines.Add(L("PRONTO. AGORA EU SOU O MESTRE.", () => { maskOn = true; lampDip = 1f; shake = 2f; Sfx("flicker", 0.8f); Sfx("curse", 0.6f); }));
            }
            else if (deaths > 0 && wins == 0)
            {
                string[] again =
                {
                    "DE NOVO.",
                    "VOCÊ SEMPRE ESCOLHE O GUERREIRO.",
                    "TEM MAIS FICHAS NA GAVETA. MUITAS.",
                    "SUA MÃE NÃO VEM TE BUSCAR. EU PERGUNTEI.",
                };
                lines.Add(L(again[Math.Min(deaths - 1, again.Length - 1)]));
            }
            else
            {
                lines.Add(L("OUTRA CAMPANHA. EU TROUXE REGRAS NOVAS."));
            }
            lines.Add(L("SEU GUERREIRO CHEGA NA ENTRADA DA MASMORRA DO REI SEM ROSTO."));
            lines.Add(L("TRÊS SALAS ATÉ ELE. ESCOLHE O CAMINHO."));
            foreach (var l in lines) l.Speaker = null; // o nome e resolvido na hora de desenhar
            Script(() => { screen = GameScreen.Map; }, lines.ToArray());
        }

        // ---------------- Mapa ----------------

        void ClickMap()
        {
            var node = HoveredNode();
            if (node == null || !run.CanTravelTo(node)) return;
            Sfx("pencil");
            run.TravelTo(node);
            Then(0.35f, () => EnterNode(node));
        }

        void EnterNode(MapNode node)
        {
            switch (node.Type)
            {
                case NodeType.Combat:
                case NodeType.Boss:
                    StartCombat(run.EnemyForCurrent());
                    break;
                case NodeType.Event:
                    currentEvent = run.NextEvent();
                    eventResult = null;
                    eventResolved = false;
                    screen = GameScreen.Event;
                    if (currentEvent.Id == "mirror") Say("ESSE EU NÃO ESCREVI.");
                    break;
                case NodeType.Treasure:
                    treasureRelic = run.RollRelic();
                    screen = GameScreen.Treasure;
                    Say("UM BAÚ. PODE ABRIR. DESSA VEZ NÃO É ARMADILHA.");
                    break;
                case NodeType.Tavern:
                    tavernResult = null;
                    screen = GameScreen.Tavern;
                    Say("PAUSA PRO LANCHE.");
                    break;
            }
        }

        void ReturnToMap()
        {
            screen = GameScreen.Map;
            combat = null;
            int layer = run.Current.Layer;
            if (layer == 1) Say("TÁ FICANDO TARDE, NÉ?");
            else if (layer == 2) Say("NINGUÉM VAI DESCER AQUI. PODE FICAR TRANQUILO.");
            else if (layer == 3) Say("ELE ESTÁ TE ESPERANDO.");
        }

        // ---------------- Combate ----------------

        void StartCombat(EnemyDef enemy)
        {
            combat = new Combat(run.Sheet, enemy, run.Dice, HouseRules.EnemyArmorBonus(run.RulesVersion));
            screen = GameScreen.Combat;
            enemyFade = 0;
            shownEnemyHp = combat.EnemyHp;
            shownEnemyBlock = 0;
            shownPlayerHp = run.Sheet.Hp;
            combat.StartPlayerTurn();
            shownBlock = 0;
            Sfx("card", 0.6f);
            if (enemy.IsBoss) Say("O REI SEM ROSTO. ELE NÃO TEM ROSTO PORQUE DEU O DELE PRA MIM.");
            else Say("UM " + enemy.Name + " APARECE. ROLA A INICIATIVA... BRINCADEIRA. VOCÊ COMEÇA.");
        }

        void ClickCombat()
        {
            if (combat == null || combat.Over) return;

            if (Hover(EndTurnX, EndTurnY, EndTurnW, EndTurnH))
            {
                EndTurn();
                return;
            }

            int idx = HoveredCard();
            if (idx < 0) return;
            var card = combat.Hand[idx];
            if (card.Unplayable)
            {
                Sfx("curse", 0.4f);
                Say("NÃO DÁ PRA USAR ISSO. NUNCA DÁ.");
                shake = 1.5f;
                return;
            }
            if (!combat.CanPlay(card))
            {
                Sfx("miss", 0.5f);
                Float(CardCenterX(idx), 180, "SEM ENERGIA", Palette.Red);
                return;
            }
            PlayCard(idx);
        }

        void PlayCard(int idx)
        {
            Sfx("card");
            var res = combat.Play(idx);
            if (res == null) return;

            foreach (var ar in res.Attacks)
            {
                var a = ar;
                Then(0.05f, () => ShowRoll(a.Roll, false, 0, "SEU ATAQUE"));
                Then(0.75f, () => ResolvePlayerAttack(a));
                Then(0.45f, () => { });
            }
            if (res.BlockGained > 0)
            {
                Then(0.05f, () =>
                {
                    Sfx("block");
                    shownBlock = combat.Block;
                    Float(PlayerMiniX, 142, "+" + res.BlockGained + " BLOQUEIO", Palette.Blue);
                });
            }
            if (res.Card.ArmorThisTurn > 0)
            {
                Then(0.05f, () => Float(PlayerMiniX, 132, "+" + res.Card.ArmorThisTurn + " CA", Palette.Blue));
            }
            if (res.Healed > 0)
            {
                Then(0.05f, () =>
                {
                    Sfx("heal");
                    shownPlayerHp = run.Sheet.Hp;
                    Float(PlayerMiniX, 142, "+" + res.Healed + " PV", Palette.Green);
                });
            }
            if (res.Card.GrantAdvantage) Then(0.05f, () => Float(PlayerMiniX, 142, "VANTAGEM!", Palette.Gold));
            if (res.Card.WeakenEnemy > 0) Then(0.05f, () => Float(EnemyMiniX, 132, "-" + res.Card.WeakenEnemy + " ACERTO", Palette.Gold));
            Then(0.1f, CheckCombatEnd);
        }

        void ShowRoll(TestRoll roll, bool hidden, int announced, string label)
        {
            dice = new DiceFx
            {
                X = 257, Y = 146, Roll = roll, Rolling = hidden ? 1.1f : 0.65f, Life = 1.2f,
                Hidden = hidden, Announced = announced, Label = label
            };
            Sfx("dice");
        }

        void ResolvePlayerAttack(AttackResult a)
        {
            if (a.Roll.Success)
            {
                if (a.Roll.IsCrit) { Sfx("crit"); Say("HM. SORTE."); Float(EnemyMiniX, 128, "CRÍTICO!", Palette.Gold); }
                Sfx("hit");
                shake = a.Roll.IsCrit ? 4f : 2f;
                enemyHurt = 1f;
                shownEnemyHp = Math.Max(0, shownEnemyHp - a.Damage);
                shownEnemyBlock = combat.EnemyBlock;
                if (a.Damage > 0) Float(EnemyMiniX, 138, "-" + a.Damage, Palette.Red);
                if (a.Blocked > 0) Float(EnemyMiniX + 14, 148, "(" + a.Blocked + " BLOQ.)", Palette.Blue);
            }
            else
            {
                Sfx("miss");
                if (a.Roll.IsFumble) { Say("HEHE. UM."); Float(EnemyMiniX, 138, "FALHA CRÍTICA", Palette.Red); }
                else Float(EnemyMiniX, 138, "ERROU", Palette.Paper);
            }
        }

        void EndTurn()
        {
            Sfx("click");
            combat.DiscardHand();
            bool wasHidden = combat.HiddenRolls;
            var move = combat.Intent;

            Then(0.35f, () => { });
            if (move.Kind == MoveKind.Attack && wasHidden)
            {
                Then(0.01f, () => Say("AGORA EU ROLO AQUI ATRÁS, TÁ?"));
            }
            EnemyTurnResult r = null;
            Then(0.4f, () =>
            {
                r = combat.EnemyAct();
                switch (r.Move.Kind)
                {
                    case MoveKind.Attack:
                        ShowRoll(r.Roll, r.Hidden, r.AnnouncedTotal, r.Move.Name);
                        break;
                    case MoveKind.Guard:
                        Sfx("block");
                        shownEnemyBlock = combat.EnemyBlock;
                        Float(EnemyMiniX, 138, "+" + r.GuardGained + " BLOQUEIO", Palette.Blue);
                        break;
                    case MoveKind.Curse:
                        Sfx("curse");
                        lampDip = 0.9f;
                        Float(PlayerMiniX, 132, "+PESADELO NO DESCARTE", Palette.Purple);
                        break;
                }
            });
            Then(move.Kind == MoveKind.Attack ? (wasHidden ? 1.2f : 0.8f) : 0.6f, () =>
            {
                if (r.Move.Kind != MoveKind.Attack) return;
                if (r.Hidden) Say(r.Cheated ? "DEU " + r.AnnouncedTotal + ". ACERTOU. EU VI." : "DEU " + r.AnnouncedTotal + ".");
                if (r.Hit)
                {
                    if (r.Roll.IsCrit) { Sfx("crit"); Float(PlayerMiniX, 128, "CRÍTICO!", Palette.Red); }
                    Sfx("hit");
                    shake = r.Roll.IsCrit ? 5f : 3f;
                    playerHurt = 1f;
                    shownPlayerHp = run.Sheet.Hp;
                    shownBlock = combat.Block;
                    if (r.Damage > 0) Float(PlayerMiniX, 138, "-" + r.Damage, Palette.Red);
                    if (r.Blocked > 0) Float(PlayerMiniX + 14, 148, "(" + r.Blocked + " BLOQ.)", Palette.Blue);
                }
                else
                {
                    Sfx("miss");
                    Float(PlayerMiniX, 138, "ERROU", Palette.Paper);
                }
            });
            Then(0.6f, () =>
            {
                if (combat.PlayerDead) { PlayerDied(); return; }
                combat.StartPlayerTurn();
                shownBlock = 0;
                Sfx("card", 0.5f);
            });
        }

        void CheckCombatEnd()
        {
            if (!combat.EnemyDead) return;
            Then(0.3f, () => { enemyFade = 0.01f; Sfx("death", 0.35f); });
            Then(1.0f, () =>
            {
                if (run.Sheet.HasRelic(RelicId.RabbitFoot)) run.Sheet.Heal(4);
                if (combat.Enemy.IsBoss) { Victory(); return; }
                rewardCards = run.RollRewards();
                screen = GameScreen.Reward;
                Say("ESCOLHE UMA CARTA. ESCREVE NA FICHA.");
            });
        }

        // ---------------- Recompensa ----------------

        void ClickReward()
        {
            for (int i = 0; i < 3; i++)
            {
                if (Hover(RewardCardX(i), RewardY, CardW, CardH))
                {
                    Sfx("pencil");
                    run.Sheet.Deck.Add(rewardCards[i]);
                    Then(0.4f, ReturnToMap);
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
                    eventResult = ch.Success.Text;
                    run.Apply(ch.Success);
                    eventResolved = true;
                    return;
                }
                var roll = run.Dice.Test(run.Sheet.Mod(ch.TestAttr), ch.Dc, false, false);
                ShowRoll(roll, false, 0, "TESTE DE " + CharacterSheet.AttrName(ch.TestAttr));
                dice.X = 400; dice.Y = 150;
                Then(0.9f, () =>
                {
                    var outcome = roll.Success ? ch.Success : ch.Failure;
                    var relic = run.Apply(outcome);
                    eventResult = (roll.Success ? "SUCESSO. " : "FALHA. ") + outcome.Text;
                    if (relic != null) eventResult += " (" + relic.Name + ")";
                    if (outcome.Kind == OutcomeKind.AddCurse) { Sfx("curse"); lampDip = 1f; Say("AGORA ELA ESTÁ NO SEU DECK."); }
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

        // ---------------- Taverna ----------------

        void ClickTavern()
        {
            if (tavernResult != null)
            {
                if (Hover(ContinueX, ContinueY, ContinueW, ContinueH)) { Sfx("click"); ReturnToMap(); }
                return;
            }
            if (Hover(ChoiceX, ChoiceY(0), ChoiceW, ChoiceH))
            {
                int amount = (int)(run.Sheet.MaxHp * HouseRules.TavernHealFraction(run.RulesVersion));
                int before = run.Sheet.Hp;
                run.Sheet.Heal(amount);
                tavernResult = "VOCÊ DESCANSA E RECUPERA " + (run.Sheet.Hp - before) + " PV. O SALGADINHO ESTÁ MURCHO, COMO SE O PACOTE ESTIVESSE ABERTO HÁ DIAS.";
                Sfx("heal");
            }
            else if (Hover(ChoiceX, ChoiceY(1), ChoiceW, ChoiceH))
            {
                run.Sheet.Scores[(int)Attr.FOR] += 2;
                tavernResult = "VOCÊ TREINA COM A ESPADA. FOR +2. SEUS ATAQUES FICAM MAIS FORTES. SEU BRAÇO DE VERDADE ESTÁ DOLORIDO.";
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
            Then(1.4f, () =>
            {
                Script(() => { screen = GameScreen.Title; run = null; combat = null; },
                    L("SEU GUERREIRO MORREU."),
                    L("TUDO BEM. ACONTECE COM TODO MUNDO QUE SENTA AÍ."),
                    L("AMASSA A FICHA E JOGA NO CHÃO. A GENTE COMEÇA DE NOVO."));
            });
        }

        void Victory()
        {
            wins++;
            host.SaveInt("wins", wins);
            screen = GameScreen.Victory;
            lampFlash = 1f;
            Sfx("crit");
            int newVersion = RulesVersion;
            Then(1.0f, () =>
            {
                Script(() => { screen = GameScreen.Title; run = null; combat = null; },
                    L("..."),
                    L("VOCÊ VENCEU O REI SEM ROSTO."),
                    L("LEGAL. MUITO LEGAL."),
                    L("MAS A CAMPANHA NÃO ACABA AQUI. ELA NUNCA ACABA."),
                    L("EU ESCREVI UMAS REGRAS NOVAS. REGRAS DA CASA V" + newVersion + ".0."));
            });
        }
    }
}
