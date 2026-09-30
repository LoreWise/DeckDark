using System;
using System.Collections.Generic;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>
    /// Tudo o que e do combate na tela: miniaturas, intencoes, condicoes, mao de cartas,
    /// escolha de alvo e a reproducao animada dos eventos que a logica devolve.
    /// </summary>
    public partial class GameApp
    {
        const int PlayerMiniX = 170, MiniFeetY = 178;
        const int MatX = 134, MatY = 128, MatW = 246, MatH = 66;
        const int CardW = 62, CardH = 86;
        const int HandY = 190, HandHoverY = 172;
        const int HandLeft = 134, HandRight = 380;
        const int EndTurnX = 392, EndTurnY = 104, EndTurnW = 82, EndTurnH = 22;

        /// <summary>O que a tela mostra de cada inimigo (atrasado em relacao a logica, por causa das animacoes).</summary>
        class UnitView
        {
            public int Hp, Block, MaxHp;
            public bool Visible, Dead, Fled;
            public float Hurt, Fade;
        }

        readonly List<UnitView> enemyViews = new List<UnitView>();
        int shownPlayerHp, shownBlock;
        float playerHurt;
        int selectedCard = -1;      // carta esperando um alvo
        int actingEnemy = -1;
        bool enemyPhase;
        bool announcedHidden;
        TestRoll lastRoll;
        bool lastRollHidden, lastRollCheated;
        int lastRollAnnounced;
        NodeType combatNode;
        CardDef[] rewardCards;
        string rewardRelic;

        // =====================================================================
        // Inicio e fim
        // =====================================================================

        void StartCombat(EnemyDef[] encounter, NodeType node)
        {
            combatNode = node;
            combat = new Combat(run.Sheet, encounter, run.Dice, Homebrew.EnemyArmorBonus(run.RulesVersion));
            combat.EnemyCanReroll = Homebrew.EnemyReroll(run.RulesVersion);
            enemyViews.Clear();
            foreach (var e in combat.Enemies)
                enemyViews.Add(new UnitView { Hp = e.Hp, MaxHp = e.Def.MaxHp, Visible = true });
            screen = GameScreen.Combat;
            selectedCard = -1;
            actingEnemy = -1;
            announcedHidden = false;
            shownPlayerHp = run.Sheet.Hp;
            shownBlock = 0;
            var ev = combat.StartCombat();
            Sfx("card", 0.6f);
            PlayEvents(ev, null);

            var first = encounter[0];
            if (first.IsBoss) Say("THE FACELESS KING. HE HAS NO FACE BECAUSE HE GAVE IT TO ME.");
            else if (first.IsElite) Say("THIS ONE IS BIGGER. I DREW IT MYSELF.");
            else if (encounter.Length > 1) Say("THEY CAME IN A GROUP. ROLL FOR INITIATIVE... JUST KIDDING. YOU GO FIRST.");
            else Say("A " + first.Name + " APPEARS. YOU GO FIRST.");
        }

        void OnCombatWon()
        {
            Then(0.8f, () =>
            {
                if (run.Sheet.HasRelic(RelicId.RabbitFoot)) run.Sheet.Heal(5);
                if (combatNode == NodeType.Boss) { Victory(); return; }
                rewardRelic = null;
                if (combatNode == NodeType.Elite)
                {
                    var r = run.RollRelic();
                    if (r != null) { run.Sheet.Relics.Add(r); rewardRelic = r.Name + ": " + r.Description; }
                }
                rewardCards = run.RollRewards(combatNode == NodeType.Elite);
                screen = GameScreen.Reward;
                Say(combatNode == NodeType.Elite ? "FINE. TAKE SOMETHING SHINY. AND A CARD." : "PICK A CARD. WRITE IT ON YOUR SHEET.");
            });
        }

        // =====================================================================
        // Reproducao dos eventos
        // =====================================================================

        void PlayEvents(List<CombatEvent> events, Action after)
        {
            bool afterRoll = false, prevHidden = false;
            foreach (var e in events)
            {
                var ev = e;
                float wait;
                switch (ev.Type)
                {
                    case EvType.Actor: wait = 0.35f; break;
                    case EvType.Roll: wait = afterRoll ? 1.5f : 0.15f; break;
                    case EvType.Hit: wait = afterRoll ? (prevHidden ? 1.9f : 1.35f) : 0.45f; break;
                    case EvType.Miss: wait = afterRoll ? 1.35f : 0.3f; break;
                    case EvType.Status: wait = afterRoll ? 1.35f : 0.45f; break;
                    case EvType.Say: wait = afterRoll ? 1.2f : 0.2f; break;
                    case EvType.Draw: case EvType.Energy: wait = 0.1f; break;
                    case EvType.Die: case EvType.Flee: wait = 0.5f; break;
                    case EvType.Rise: case EvType.Summon: case EvType.Curse: wait = 0.7f; break;
                    default: wait = afterRoll ? 1.35f : 0.35f; break;
                }
                if (ev.Type == EvType.Block && ev.Amount == 0) wait = 0.05f;
                afterRoll = ev.Type == EvType.Roll;
                if (afterRoll) prevHidden = ev.Hidden;
                Then(wait, () => ApplyEvent(ev));
            }
            Then(afterRoll ? 1.4f : 0.3f, () =>
            {
                actingEnemy = -1;
                if (after != null) after();
            });
        }

        int EnemyFloatY(int i) { return MiniFeetY - EnemySpriteHeight(i) - 4; }

        void ApplyEvent(CombatEvent ev)
        {
            switch (ev.Type)
            {
                case EvType.Actor:
                    actingEnemy = ev.Actor;
                    break;

                case EvType.Roll:
                    lastRoll = ev.Roll;
                    lastRollHidden = ev.Hidden;
                    lastRollCheated = ev.Cheated;
                    lastRollAnnounced = ev.Announced;
                    if (ev.Hidden && !announcedHidden) { announcedHidden = true; Say("I'LL ROLL BACK HERE NOW, OK?"); }
                    ShowRoll(ev.Roll, ev.Hidden, ev.Announced, ev.Label);
                    break;

                case EvType.Hit:
                    if (ev.Target < 0)
                    {
                        if (ev.Label == null && lastRollHidden) Say(lastRollCheated ? "IT'S A " + lastRollAnnounced + ". HIT. I SAW IT." : "IT'S A " + lastRollAnnounced + ".");
                        shownPlayerHp = ev.HpAfter;
                        shownBlock = ev.BlockAfter;
                        playerHurt = 1f;
                        shake = ev.Crit ? 5f : 3f;
                        Sfx(ev.Crit ? "crit" : "hit");
                        if (ev.Crit) Float(PlayerMiniX, 124, "CRITICAL!", Palette.Red);
                        if (ev.Amount > 0) Float(PlayerMiniX, 138, (ev.Label != null ? ev.Label + " " : "") + "-" + ev.Amount, Palette.Red);
                        if (ev.Blocked > 0) Float(PlayerMiniX + 10, 150, "(" + ev.Blocked + " BLOCKED)", Palette.Blue);
                    }
                    else if (ev.Target < enemyViews.Count)
                    {
                        var v = enemyViews[ev.Target];
                        v.Hp = ev.HpAfter;
                        v.Block = ev.BlockAfter;
                        v.Hurt = 1f;
                        shake = ev.Crit ? 4f : 2f;
                        Sfx(ev.Crit ? "crit" : "hit");
                        int x = EnemyX(ev.Target), y = EnemyFloatY(ev.Target);
                        if (ev.Crit) { Float(x, y - 10, "CRITICAL!", Palette.Gold); Say("HM. LUCKY."); }
                        if (ev.Amount > 0) Float(x, y, (ev.Label != null ? ev.Label + " " : "") + "-" + ev.Amount, Palette.Red);
                        if (ev.Blocked > 0) Float(x, y + 12, "(" + ev.Blocked + " BLOCKED)", Palette.Blue);
                    }
                    break;

                case EvType.Miss:
                    Sfx("miss");
                    if (ev.Target < 0)
                    {
                        if (lastRollHidden) Say("IT'S A " + lastRollAnnounced + ". MISS.");
                        Float(PlayerMiniX, 138, "MISS", Palette.Paper);
                    }
                    else
                    {
                        if (ev.Roll.IsFumble) { Say("HEHE. A ONE."); Float(EnemyX(ev.Target), EnemyFloatY(ev.Target), "CRITICAL FAIL", Palette.Red); }
                        else Float(EnemyX(ev.Target), EnemyFloatY(ev.Target), "MISS", Palette.Paper);
                    }
                    break;

                case EvType.Block:
                    if (ev.Target < 0)
                    {
                        shownBlock = ev.BlockAfter;
                        if (ev.Amount > 0) { Sfx("block"); Float(PlayerMiniX, 140, "+" + ev.Amount + " BLOCK", Palette.Blue); }
                    }
                    else if (ev.Target < enemyViews.Count)
                    {
                        enemyViews[ev.Target].Block = ev.BlockAfter;
                        if (ev.Amount > 0) { Sfx("block"); Float(EnemyX(ev.Target), EnemyFloatY(ev.Target), "+" + ev.Amount + " BLOCK", Palette.Blue); }
                    }
                    break;

                case EvType.Heal:
                    shownPlayerHp = ev.HpAfter;
                    Sfx("heal");
                    Float(PlayerMiniX, 140, "+" + ev.Amount + " HP", Palette.Green);
                    break;

                case EvType.Status:
                {
                    Rgb col = ToneColor(ev.Tone);
                    if (ev.Target < 0) Float(PlayerMiniX, 132, ev.Text, col);
                    else Float(EnemyX(ev.Target), EnemyFloatY(ev.Target) - 6, ev.Text, col);
                    if (ev.Tone == Tone.Purple) Sfx("curse", 0.4f);
                    else if (ev.Tone == Tone.Gold) Sfx("click", 0.6f);
                    break;
                }

                case EvType.Curse:
                    Sfx("curse");
                    lampDip = 0.9f;
                    Float(PlayerMiniX, 128, "+NIGHTMARE IN DISCARD", Palette.Purple);
                    break;

                case EvType.Flee:
                    if (ev.Target < enemyViews.Count) { enemyViews[ev.Target].Fled = true; }
                    Sfx("miss");
                    Float(EnemyX(ev.Target), EnemyFloatY(ev.Target), "FLEES!", Palette.Paper);
                    break;

                case EvType.Rise:
                    if (ev.Target < enemyViews.Count) { enemyViews[ev.Target].Hp = ev.HpAfter; enemyViews[ev.Target].Hurt = 1f; }
                    Sfx("curse", 0.5f);
                    Float(EnemyX(ev.Target), EnemyFloatY(ev.Target), ev.Text, Palette.Purple);
                    break;

                case EvType.Summon:
                    while (enemyViews.Count <= ev.Target) enemyViews.Add(new UnitView());
                    var sv = enemyViews[ev.Target];
                    sv.Visible = true;
                    sv.Hp = ev.HpAfter;
                    sv.MaxHp = combat.Enemies[ev.Target].Def.MaxHp;
                    Sfx("curse", 0.6f);
                    lampDip = 0.6f;
                    Float(EnemyX(ev.Target), EnemyFloatY(ev.Target), "A SERVANT RISES", Palette.Purple);
                    break;

                case EvType.Die:
                    if (ev.Target < enemyViews.Count) enemyViews[ev.Target].Dead = true;
                    Sfx("death", 0.35f);
                    break;

                case EvType.Say:
                    Say(ev.Text);
                    Float(PlayerMiniX, 124, "REROLL!", Palette.Red);
                    break;

                case EvType.Draw:
                    if (ev.Amount > 0) Sfx("card", 0.5f);
                    break;

                case EvType.Energy:
                    Float(PlayerMiniX, 132, "+" + ev.Amount + " ENERGY", Palette.Gold);
                    Sfx("heal", 0.5f);
                    break;
            }
        }

        static Rgb ToneColor(Tone t)
        {
            switch (t)
            {
                case Tone.Bad: return Palette.Red;
                case Tone.Good: return Palette.Green;
                case Tone.Gold: return Palette.Gold;
                case Tone.Blue: return Palette.Blue;
                case Tone.Purple: return Palette.Purple;
                default: return Palette.Paper;
            }
        }

        // =====================================================================
        // Cliques
        // =====================================================================

        void ClickCombat()
        {
            if (combat == null || combat.Over || enemyPhase) return;

            // Escolhendo o alvo de uma carta
            if (selectedCard >= 0)
            {
                int t = HoveredEnemy();
                if (t >= 0) { int idx = selectedCard; selectedCard = -1; PlayCard(idx, t); return; }
                selectedCard = -1;
                Sfx("click", 0.3f);
                return;
            }

            if (Hover(EndTurnX, EndTurnY, EndTurnW, EndTurnH)) { EndTurn(); return; }

            int ci = HoveredCard();
            if (ci < 0) return;
            var card = combat.Hand[ci];
            if (card.Unplayable)
            {
                Sfx("curse", 0.4f);
                Say("YOU CAN'T USE THAT. YOU NEVER CAN.");
                shake = 1.5f;
                return;
            }
            if (!combat.CanPlay(card))
            {
                Sfx("miss", 0.5f);
                Float(CardX(ci, combat.Hand.Count) + CardW / 2, 176, combat.Energy < combat.CostOf(card) ? "NO ENERGY" : "NOT ENOUGH HP", Palette.Red);
                return;
            }
            if (card.NeedsTarget && combat.AliveCount > 1)
            {
                selectedCard = ci;
                Sfx("click", 0.6f);
                return;
            }
            PlayCard(ci, combat.FirstAliveEnemy());
        }

        void CancelTargeting()
        {
            if (selectedCard < 0) return;
            selectedCard = -1;
            Sfx("click", 0.3f);
        }

        void PlayCard(int idx, int target)
        {
            Sfx("card");
            var ev = combat.Play(idx, target);
            if (ev == null) return;
            PlayEvents(ev, () =>
            {
                if (combat.PlayerDead) { PlayerDied(); return; }
                if (combat.EnemiesDefeated) OnCombatWon();
            });
        }

        void EndTurn()
        {
            Sfx("click");
            selectedCard = -1;
            enemyPhase = true;
            var ev = combat.EndPlayerTurn();
            if (!combat.PlayerDead) ev.AddRange(combat.EnemyPhase());
            PlayEvents(ev, () =>
            {
                enemyPhase = false;
                if (combat.PlayerDead) { PlayerDied(); return; }
                if (combat.EnemiesDefeated) { OnCombatWon(); return; }
                Sfx("card", 0.5f);
            });
        }

        // =====================================================================
        // Layout
        // =====================================================================

        List<int> ShownEnemies()
        {
            var list = new List<int>();
            for (int i = 0; i < enemyViews.Count && i < combat.Enemies.Count; i++)
            {
                var v = enemyViews[i];
                if (v.Visible && v.Fade < 1f) list.Add(i);
            }
            return list;
        }

        int EnemyX(int i)
        {
            if (combat == null) return 330;
            var list = ShownEnemies();
            int slot = list.IndexOf(i);
            if (slot < 0) slot = 0;
            int k = Math.Max(1, list.Count);
            if (k == 1) return 330;
            if (k == 2) return slot == 0 ? 298 : 350;
            return 272 + slot * 44;
        }

        static string[] EnemySpriteRows(EnemySprite s)
        {
            switch (s)
            {
                case EnemySprite.Goblin: return Sprites.Goblin;
                case EnemySprite.Skeleton: return Sprites.Skeleton;
                case EnemySprite.Cultist: return Sprites.Cultist;
                case EnemySprite.Rat: return Sprites.Rat;
                case EnemySprite.Ogre: return Sprites.Ogre;
                case EnemySprite.Ghoul: return Sprites.Ghoul;
                default: return Sprites.Boss;
            }
        }

        int EnemySpriteHeight(int i)
        {
            if (combat == null || i < 0 || i >= combat.Enemies.Count) return 32;
            return EnemySpriteRows(combat.Enemies[i].Def.Sprite).Length * 2 - 2;
        }

        int HoveredEnemy()
        {
            if (combat == null) return -1;
            foreach (int i in ShownEnemies())
            {
                if (!combat.Enemies[i].Alive || enemyViews[i].Dead) continue;
                var spr = EnemySpriteRows(combat.Enemies[i].Def.Sprite);
                int w = spr[0].Length * 2, h = spr.Length * 2;
                int x = EnemyX(i) - w / 2, y = MiniFeetY - h + 2;
                if (Hover(x - 2, y - 2, w + 4, h + 12)) return i;
            }
            return -1;
        }

        int CardX(int i, int n)
        {
            if (n <= 1) return (HandLeft + HandRight) / 2 - CardW / 2;
            int span = HandRight - HandLeft - CardW;
            int step = Math.Min(CardW + 4, span / (n - 1));
            int total = step * (n - 1) + CardW;
            int start = (HandLeft + HandRight) / 2 - total / 2;
            return start + i * step;
        }

        int HoveredCard()
        {
            if (combat == null) return -1;
            int n = combat.Hand.Count;
            for (int i = n - 1; i >= 0; i--)
            {
                int x = CardX(i, n);
                if (input.X >= x && input.X < x + CardW && input.Y >= HandHoverY && input.Y < H) return i;
            }
            return -1;
        }

        // =====================================================================
        // Desenho
        // =====================================================================

        /// <summary>A parte "fisica": papel quadriculado e as miniaturas. Recebe a luz da lampada.</summary>
        // ---- miniaturas para a mesa 3D (o Unity desenha, o jogo so informa onde) ----
        public class MiniView
        {
            public string Key;
            public string[] Rows;          // sprite sem a base
            public Func<char, Rgb?> Pal;
            public int X, FeetY;           // posicao na tela 480x270
            public bool Lying, FaceLeft, Flash;
            public int Ring;               // 0 nenhum, 1 alvo (vermelho), 2 agindo (dourado)
            public int Wobble;             // tremida ao levar dano, em pixels
        }
        public readonly List<MiniView> Minis = new List<MiniView>();
        public bool ShowMat3D;
        public int MatLeft { get { return MatX; } }
        public int MatTop { get { return MatY; } }
        public int MatWidth { get { return MatW; } }
        public int MatHeight { get { return MatH; } }

        static string[] NoBase(string[] spr)
        {
            var r = new string[spr.Length - 3];
            Array.Copy(spr, r, r.Length);
            return r;
        }

        void CollectMinis()
        {
            int pj = playerHurt > 0 ? (int)(Math.Sin(time * 60) * 2 * playerHurt) : 0;
            Minis.Add(new MiniView { Key = IsWizard ? "wizard" : "knight", Rows = NoBase(PlayerSprite), Pal = PlayerPal, X = PlayerMiniX, FeetY = MiniFeetY, Lying = combat.PC.Prone, Wobble = pj });
            int hovered = selectedCard >= 0 ? HoveredEnemy() : -1;
            foreach (int i in ShownEnemies())
            {
                var e = combat.Enemies[i];
                var v = enemyViews[i];
                if (v.Fled) continue;
                var sprite = e.Def.Sprite;
                Minis.Add(new MiniView
                {
                    Key = sprite.ToString(), Rows = NoBase(EnemySpriteRows(sprite)), Pal = ch => EnemyPal(sprite, ch),
                    X = EnemyX(i), FeetY = MiniFeetY, FaceLeft = true,
                    Lying = v.Dead || (e.C.Prone && !enemyPhase), Flash = v.Hurt > 0.6f && !v.Dead,
                    Ring = i == actingEnemy ? 2 : (i == hovered ? 1 : 0),
                    Wobble = v.Hurt > 0 ? (int)(Math.Sin(time * 60) * 2 * v.Hurt) : 0,
                });
            }
        }

        void DrawCombatTable(PixelCanvas c)
        {
            if (Use3D) { ShowMat3D = true; CollectMinis(); return; }
            c.Fill(MatX, MatY, MatW, MatH, Rgb.Hex(0xd8d2bc));
            for (int gx = MatX; gx < MatX + MatW; gx += 10) c.VLine(gx, MatY, MatY + MatH - 1, Rgb.Hex(0xb4c4c8));
            for (int gy = MatY; gy < MatY + MatH; gy += 10) c.HLine(MatX, MatX + MatW - 1, gy, Rgb.Hex(0xb4c4c8));
            c.Rect(MatX, MatY, MatW, MatH, Rgb.Hex(0x9a927a));

            // jogador (deitado se estiver PRONE)
            int pj = playerHurt > 0 ? (int)(Math.Sin(time * 60) * 2 * playerHurt) : 0;
            c.FillEllipse(PlayerMiniX, MiniFeetY + 1, 15, 4, Rgb.Hex(0x7a7260));
            if (combat.PC.Prone) DrawLyingSprite(c, PlayerSprite, PlayerMiniX, PlayerPal, false);
            else c.SpriteOutlined(PlayerSprite, PlayerMiniX - 16 + pj, MiniFeetY - PlayerSprite.Length * 2 + 2, PlayerPal, false, 2, Rgb.Hex(0x2a2420));

            int hovered = selectedCard >= 0 ? HoveredEnemy() : -1;
            foreach (int i in ShownEnemies())
            {
                var e = combat.Enemies[i];
                var v = enemyViews[i];
                var spr = EnemySpriteRows(e.Def.Sprite);
                int sw = spr[0].Length;
                int ex = EnemyX(i);
                int ej = v.Hurt > 0 ? (int)(Math.Sin(time * 60) * 2 * v.Hurt) : 0;
                var sprite = e.Def.Sprite;
                c.FillEllipse(ex, MiniFeetY + 1, sw, 4, Rgb.Hex(0x7a7260));
                if (v.Dead || v.Fled)
                {
                    if (v.Fled) continue;
                    DrawLyingSprite(c, spr, ex, ch => EnemyPal(sprite, ch), true);
                    continue;
                }
                bool lying = e.C.Prone && !enemyPhase;
                // alvo sob o mouse: base vermelha; inimigo agindo: base dourada
                if (i == hovered) { c.FillEllipse(ex, MiniFeetY + 1, sw + 3, 6, Palette.Red); c.FillEllipse(ex, MiniFeetY + 1, sw, 4, Rgb.Hex(0x7a7260)); }
                if (i == actingEnemy) { c.FillEllipse(ex, MiniFeetY + 1, sw + 3, 6, Palette.Gold); c.FillEllipse(ex, MiniFeetY + 1, sw, 4, Rgb.Hex(0x7a7260)); }
                bool flash = v.Hurt > 0.6f;
                if (lying) DrawLyingSprite(c, spr, ex, ch => flash ? (Rgb?)Palette.White : EnemyPal(sprite, ch), true);
                else c.SpriteOutlined(spr, ex - sw + ej, MiniFeetY - spr.Length * 2 + 2, ch => flash ? (Rgb?)Palette.White : EnemyPal(sprite, ch), true, 2, Rgb.Hex(0x2a2420));
            }
        }

        /// <summary>Miniatura tombada de lado (caida ou morta).</summary>
        void DrawLyingSprite(PixelCanvas c, string[] spr, int cx, Func<char, Rgb?> pal, bool faceLeft)
        {
            int sw = spr[0].Length;
            int rows = spr.Length - 3; // sem a base
            for (int j = 0; j < rows; j++)
                for (int i = 0; i < sw; i++)
                {
                    char ch = spr[j][faceLeft ? sw - 1 - i : i];
                    if (ch == '.') continue;
                    var col = pal(ch);
                    if (!col.HasValue) continue;
                    int x = faceLeft ? cx - rows + j * 2 : cx + rows - j * 2;
                    c.Fill(x, MiniFeetY - sw * 2 / 2 + i, 2, 1, col.Value.Mul(0.85f));
                }
        }

        void DrawCombatUi(PixelCanvas c)
        {
            TintAt(255, 160, 0.78f);

            // inimigos: vida, bloqueio, condicoes e intencao
            foreach (int i in ShownEnemies())
            {
                var v = enemyViews[i];
                if (v.Dead || v.Fled) continue;
                var e = combat.Enemies[i];
                int ex = EnemyX(i);
                DrawHpBar(c, ex, 181, v.Hp, v.MaxHp, v.Block, 34);
                var spr = EnemySpriteRows(e.Def.Sprite);
                DrawConditions(c, e.C, 0, ex + spr[0].Length + 1, MiniFeetY - 4, true);
                if (!Busy && !enemyPhase && dice == null) DrawIntent(c, i, ex);
            }

            // jogador
            DrawHpBar(c, PlayerMiniX, 181, shownPlayerHp, run.Sheet.MaxHp, shownBlock, 40);
            DrawConditions(c, combat.PC, combat.Fury, PlayerMiniX + 18, MiniFeetY - 4, false, combat.Conc != ConcKind.None ? "C" : null);
            if (combat.NextAttackAdvantage) PixelFont.Small.DrawCentered(c, "ADVANTAGE", PlayerMiniX, 131, Rgb.Hex(0x9a7010));
            else if (combat.ForcedNatural > 0) PixelFont.Small.DrawCentered(c, "NEXT d20: " + combat.ForcedNatural, PlayerMiniX, 131, Rgb.Hex(0x9a7010));

            // botao de fim de turno
            TintAt(430, 140, 0.7f);
            bool canEnd = !Busy && !combat.Over && !scriptActive && !enemyPhase;
            bool hov = canEnd && Hover(EndTurnX, EndTurnY, EndTurnW, EndTurnH);
            c.Fill(EndTurnX + 2, EndTurnY + 2, EndTurnW, EndTurnH, Rgb.Hex(0x2a1a10));
            c.Fill(EndTurnX, EndTurnY, EndTurnW, EndTurnH, hov ? Rgb.Hex(0xf0d890) : Palette.Paper);
            c.Rect(EndTurnX, EndTurnY, EndTurnW, EndTurnH, Palette.Ink);
            PixelFont.Big.DrawCentered(c, enemyPhase ? "ENEMY TURN" : "END TURN", EndTurnX + EndTurnW / 2, EndTurnY + 5, canEnd ? Palette.Ink : Palette.Pencil);

            DrawPile(c, 398, 136, combat.DrawPile.Count, "DRAW", true);
            DrawPile(c, 438, 136, combat.Discard.Count, "DISCARD", false);
            PixelFont.Small.Draw(c, "TURN " + combat.Turn, 398, 188, Palette.Paper);
            if (combat.Exhausted.Count > 0) PixelFont.Small.Draw(c, "EXHAUSTED " + combat.Exhausted.Count, 398, 196, Palette.Paper);

            if (!enemyPhase) DrawHand(c);

            // painel de detalhes do inimigo sob o mouse
            int he = HoveredEnemy();
            if (he >= 0 && dice == null) DrawEnemyDetails(c, he);
            if (selectedCard >= 0)
            {
                c.ResetTint();
                c.FillAlpha(MatX + 2, MatY + 2, 72, 18, Palette.Black, 0.75f);
                PixelFont.Small.DrawCentered(c, "CHOOSE A TARGET", MatX + 38, MatY + 4, Palette.Gold);
                PixelFont.Small.DrawCentered(c, "ESC TO CANCEL", MatX + 38, MatY + 12, Palette.PaperDark);
                // seta sobre o inimigo sob o mouse
                int ht = HoveredEnemy();
                if (ht >= 0)
                {
                    int ax = EnemyX(ht), ay = MiniFeetY - EnemySpriteHeight(ht) - 8;
                    c.FillTriangle(ax - 4, ay - 5, ax + 4, ay - 5, ax, ay, Palette.Red);
                }
            }
            c.ResetTint();
        }

        void DrawHpBar(PixelCanvas c, int cx, int y, int hp, int max, int block, int w)
        {
            int x = cx - w / 2;
            c.Fill(x - 1, y - 1, w + 2, 9, Palette.Ink);
            c.Fill(x, y, w, 7, Rgb.Hex(0x3a1a1a));
            c.Fill(x, y, (int)(w * (float)Math.Max(0, hp) / Math.Max(1, max)), 7, block > 0 ? Rgb.Hex(0x4a78b0) : Palette.Red);
            PixelFont.Small.DrawCentered(c, hp + "/" + max, cx, y, Palette.White);
            if (block > 0)
            {
                c.Sprite(Sprites.IconShield, x - 11, y - 1, ch => ch == 'a' ? Palette.Blue : (ch == 'b' ? Palette.Ink : Palette.White));
                PixelFont.Small.DrawCentered(c, block.ToString(), x - 6, y + 1, Palette.White);
            }
        }

        /// <summary>Etiquetas pequenas de condicao, empilhadas ao lado da miniatura.</summary>
        void DrawConditions(PixelCanvas c, Conditions cond, int fury, int x, int bottomY, bool enemy, string conc = null)
        {
            var tags = new List<KeyValuePair<string, Rgb>>();
            if (conc != null) tags.Add(new KeyValuePair<string, Rgb>(conc, Palette.Blue));
            if (cond.Bleed > 0) tags.Add(new KeyValuePair<string, Rgb>("B" + cond.Bleed, Palette.Red));
            if (cond.Prone) tags.Add(new KeyValuePair<string, Rgb>("PR", Rgb.Hex(0x8a7a5a)));
            if (cond.Grappled > 0) tags.Add(new KeyValuePair<string, Rgb>("GR", Rgb.Hex(0xc08030)));
            if (cond.Frightened > 0) tags.Add(new KeyValuePair<string, Rgb>("FR", Palette.Purple));
            if (cond.Poisoned > 0) tags.Add(new KeyValuePair<string, Rgb>("PO", Rgb.Hex(0x6aa040)));
            if (cond.Stunned) tags.Add(new KeyValuePair<string, Rgb>("ST", Palette.Gold));
            if (fury > 0) tags.Add(new KeyValuePair<string, Rgb>("F" + fury, Rgb.Hex(0xe06020)));
            int y = bottomY;
            foreach (var t in tags)
            {
                int w = PixelFont.Small.Measure(t.Key) + 4;
                c.Fill(x, y - 7, w, 8, t.Value);
                c.Rect(x, y - 7, w, 8, Palette.Ink);
                PixelFont.Small.Draw(c, t.Key, x + 2, y - 7, Palette.White);
                y -= 9;
            }
        }

        string IntentShort(EnemyUnit e, out string[] icon, out Rgb col)
        {
            var m = e.Intent;
            switch (m.Kind)
            {
                case MoveKind.Attack:
                    icon = Sprites.IconSword; col = Palette.DarkRed;
                    if (combat.HiddenRolls) return "?";
                    int bonus = e.Def.CurseScaling ? combat.CurseCount : 0;
                    var d = new DiceExpr(m.Damage.Count, m.Damage.Sides, m.Damage.Bonus + bonus);
                    return (m.Hits > 1 ? m.Hits + "X" : "") + d;
                case MoveKind.Guard: icon = Sprites.IconShield; col = Rgb.Hex(0x2a5a9a); return m.Guard.ToString();
                case MoveKind.GuardAlly: icon = Sprites.IconShield; col = Rgb.Hex(0x2a5a9a); return "ALLY";
                case MoveKind.Frighten: icon = Sprites.IconMouth; col = Rgb.Hex(0x6a3a8a); return "WIS";
                case MoveKind.Summon: icon = Sprites.IconSkull; col = Rgb.Hex(0x6a3a8a); return "+1";
                default: icon = Sprites.IconEye; col = Rgb.Hex(0x6a3a8a); return "?";
            }
        }

        string IntentLong(EnemyUnit e)
        {
            var m = e.Intent;
            switch (m.Kind)
            {
                case MoveKind.Attack:
                {
                    if (combat.HiddenRolls) return m.Name + ": ??? (BEHIND THE SCREEN)";
                    int bonus = m.AttackBonus - e.Weaken - (e.C.Poisoned > 0 ? 2 : 0);
                    int extra = e.Def.CurseScaling ? combat.CurseCount : 0;
                    var d = new DiceExpr(m.Damage.Count, m.Damage.Sides, m.Damage.Bonus + extra);
                    string s = m.Name + ": HIT +" + bonus + ", " + (m.Hits > 1 ? m.Hits + "X " : "") + d + " DMG";
                    if (m.ApplyBleed > 0) s += ", BLEED " + m.ApplyBleed;
                    if (m.ApplyPoison > 0) s += ", POISON";
                    if (m.KnockProne) s += ", KNOCKS PRONE (STR DC" + m.SaveDc + ")";
                    return s;
                }
                case MoveKind.Guard: return m.Name + ": BLOCK " + m.Guard;
                case MoveKind.GuardAlly: return m.Name + ": GIVES AN ALLY BLOCK " + m.Guard;
                case MoveKind.Frighten: return m.Name + ": WIS DC" + m.SaveDc + " OR YOU ARE FRIGHTENED";
                case MoveKind.Summon: return m.Name + ": SUMMONS A SERVANT";
                default: return m.Name + ": ???";
            }
        }

        void DrawIntent(PixelCanvas c, int i, int ex)
        {
            var e = combat.Enemies[i];
            string[] icon;
            Rgb col;
            string text = IntentShort(e, out icon, out col);
            int tw = PixelFont.Small.Measure(text) + 14;
            int x = ex - tw / 2, y = 130 + (int)(Math.Sin(time * 2 + i) * 1.2);
            c.Fill(x, y, tw, 11, Palette.Paper);
            c.Rect(x, y, tw, 11, col);
            var ic = col;
            c.Sprite(icon, x + 2, y + 1, ch => ch == 'c' ? (Rgb?)Palette.White : (ch == 'b' ? Palette.Ink : ic));
            PixelFont.Small.Draw(c, text, x + 12, y + 3, col);
        }

        void DrawEnemyDetails(PixelCanvas c, int i)
        {
            c.ResetTint();
            var e = combat.Enemies[i];
            var lines = new List<string>();
            lines.Add(e.Def.Name + "   AC " + e.ArmorClass + "   HP " + enemyViews[i].Hp + "/" + e.Def.MaxHp);
            lines.Add("NEXT: " + IntentLong(e));
            var conds = new List<string>();
            if (e.C.Bleed > 0) conds.Add("BLEED " + e.C.Bleed);
            if (e.C.Prone) conds.Add("PRONE");
            if (e.C.Grappled > 0) conds.Add("GRAPPLED");
            if (e.C.Frightened > 0) conds.Add("FRIGHTENED");
            if (e.C.Poisoned > 0) conds.Add("POISONED");
            if (e.Weaken > 0) conds.Add("-" + e.Weaken + " TO HIT");
            if (conds.Count > 0) lines.Add(string.Join(", ", conds));
            if (e.Def.FleesWhenHurt) lines.Add("FLEES WHEN BADLY HURT.");
            if (e.Def.Reassembles && !e.Reassembled) lines.Add("PULLS ITSELF BACK TOGETHER ONCE.");
            if (e.Def.CurseScaling) lines.Add("+1 DMG FOR EACH CURSE YOU CARRY.");
            string text = string.Join("\n", lines);
            int w = 200;
            var wrapped = PixelFont.Small.Wrap(text, w - 8);
            int h = wrapped.Count * PixelFont.Small.LineHeight + 6;
            int x = 176, y = MatY - h - 2;
            c.Fill(x, y, w, h, Palette.Ink);
            c.Rect(x, y, w, h, Palette.Gold);
            PixelFont.Small.DrawWrapped(c, text, x + 4, y + 3, w - 8, Palette.Paper);
        }

        void DrawPile(PixelCanvas c, int x, int y, int count, string label, bool back)
        {
            int layers = Math.Min(4, count);
            for (int i = layers - 1; i >= 0; i--)
            {
                int ox = x + i, oy = y - i;
                c.Fill(ox, oy, 28, 38, back ? Rgb.Hex(0x4a2a5a) : Palette.PaperDark);
                c.Rect(ox, oy, 28, 38, Palette.Ink);
                if (back) { c.Rect(ox + 3, oy + 3, 22, 32, Palette.Gold); c.Set(ox + 14, oy + 19, Palette.Gold); }
            }
            if (count == 0) { c.Rect(x, y, 28, 38, Palette.Pencil); }
            PixelFont.Big.DrawCentered(c, count.ToString(), x + 14, y + 12, back ? Palette.Paper : Palette.Ink);
            PixelFont.Small.DrawCentered(c, label, x + 14, y + 42, Palette.Paper);
        }

        void DrawHand(PixelCanvas c)
        {
            int n = combat.Hand.Count;
            int hov = (Busy || scriptActive || paused) ? -1 : HoveredCard();
            if (selectedCard >= 0) hov = selectedCard;
            for (int i = 0; i < n; i++)
            {
                if (i == hov) continue;
                var card = combat.Hand[i];
                DrawCard(c, card, CardX(i, n), HandY, combat.CanPlay(card) && !Busy, false, combat.CostOf(card));
            }
            if (hov >= 0 && hov < n)
            {
                var card = combat.Hand[hov];
                int x = CardX(hov, n);
                DrawCard(c, card, x, HandHoverY, combat.CanPlay(card), true, combat.CostOf(card));
                var kws = card.Keywords();
                if (kws.Count > 0 && selectedCard < 0)
                {
                    var parts = new List<string>();
                    foreach (var k in kws) parts.Add(Glossary.Explain(k));
                    pendingTooltip = string.Join("\n", parts);
                    pendingTooltipX = x + CardW + 4 > W - 122 ? x - 122 : x + CardW + 4;
                    pendingTooltipY = HandHoverY;
                }
            }
        }

        bool IsWizard { get { return run != null && run.Sheet.Class == PlayerClass.Wizard; } }
        string[] PlayerSprite { get { return IsWizard ? Sprites.Wizard : Sprites.Knight; } }
        Func<char, Rgb?> PlayerPal { get { return IsWizard ? (Func<char, Rgb?>)WizardPal : KnightPal; } }

        static Rgb? WizardPal(char ch)
        {
            switch (ch)
            {
                case 'k': return Rgb.Hex(0x1a1418);
                case 'u': return Rgb.Hex(0x3a5a9a);
                case 'v': return Rgb.Hex(0x243a66);
                case 'y': return Rgb.Hex(0xe0b040);
                case 'f': return Rgb.Hex(0xd09a70);
                case 'e': return Rgb.Hex(0xd8d4cc);
                case 'o': return Rgb.Hex(0x8ae0f0);
                case 'h': return Rgb.Hex(0x7a5030);
                case 'b': return Rgb.Hex(0x2f4a3a);
            }
            return null;
        }

        static Rgb? KnightPal(char ch)
        {
            switch (ch)
            {
                case 'k': return Rgb.Hex(0x1a1418);
                case 's': return Rgb.Hex(0xa8b0b8);
                case 'd': return Rgb.Hex(0x303840);
                case 'r': return Rgb.Hex(0xa83030);
                case 'y': return Rgb.Hex(0xe0b040);
                case 'w': return Rgb.Hex(0xe8eef0);
                case 'g': return Rgb.Hex(0xc09030);
                case 'h': return Rgb.Hex(0x6a4020);
                case 'b': return Rgb.Hex(0x2f4a3a);
            }
            return null;
        }

        static Rgb? EnemyPal(EnemySprite s, char ch)
        {
            if (ch == 'b') return Rgb.Hex(0x2f4a3a);
            if (ch == 'k') return Rgb.Hex(0x141014);
            switch (s)
            {
                case EnemySprite.Goblin:
                    switch (ch)
                    {
                        case 'g': return Rgb.Hex(0x6aa040);
                        case 'y': return Rgb.Hex(0xf0e040);
                        case 't': return Rgb.Hex(0xf0f0e0);
                        case 'r': return Rgb.Hex(0x7a5030);
                        case 'w': return Rgb.Hex(0xc0c8d0);
                        case 'h': return Rgb.Hex(0x6a4020);
                    }
                    break;
                case EnemySprite.Skeleton:
                    switch (ch)
                    {
                        case 'w': return Rgb.Hex(0xe0dac8);
                        case 'r': return Rgb.Hex(0x9a6040);
                        case 'h': return Rgb.Hex(0x5a3a20);
                    }
                    break;
                case EnemySprite.Cultist:
                    switch (ch)
                    {
                        case 'p': return Rgb.Hex(0x3e2c50);
                        case 'm': return Palette.Mask;
                        case 'y': return Rgb.Hex(0xc09030);
                        case 'd': return Rgb.Hex(0xc0c8d0);
                    }
                    break;
                case EnemySprite.Rat:
                    switch (ch)
                    {
                        case 'g': return Rgb.Hex(0x7a6a5a);
                        case 'y': return Rgb.Hex(0xe03030);
                        case 't': return Rgb.Hex(0xf0f0e0);
                    }
                    break;
                case EnemySprite.Ogre:
                    switch (ch)
                    {
                        case 'g': return Rgb.Hex(0x8a9a6a);
                        case 'y': return Rgb.Hex(0xf0d040);
                        case 't': return Rgb.Hex(0xf0e8d0);
                        case 'r': return Rgb.Hex(0x7a5a3a);
                        case 'w': return Rgb.Hex(0x4a3020);
                        case 'h': return Rgb.Hex(0x6a4a2a);
                    }
                    break;
                case EnemySprite.Ghoul:
                    switch (ch)
                    {
                        case 'p': return Rgb.Hex(0x9aa890);
                        case 'y': return Rgb.Hex(0xd0f060);
                        case 't': return Rgb.Hex(0xe8e0c8);
                        case 'w': return Rgb.Hex(0xe0e0e0);
                    }
                    break;
                default:
                    switch (ch)
                    {
                        case 'y': return Rgb.Hex(0xe0b040);
                        case 'r': return Rgb.Hex(0xc02030);
                        case 'm': return Rgb.Hex(0xefe8da);
                        case 'c': return Rgb.Hex(0x2e1a36);
                    }
                    break;
            }
            return Rgb.Hex(0xff00ff);
        }
    }
}
