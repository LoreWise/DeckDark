using System;
using System.Collections.Generic;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>Tudo o que e desenhado na tela: layout, ficha, cartas, mapa, dados.</summary>
    public partial class GameApp
    {
        // ---------------- Layout ----------------
        const int CardW = 62, CardH = 80;
        const int HandY = 190, HandHoverY = 172;
        const int HandLeft = 134, HandRight = 380;
        const int EndTurnX = 392, EndTurnY = 104, EndTurnW = 82, EndTurnH = 22;
        const int PlayerMiniX = 178, EnemyMiniX = 336, MiniFeetY = 176;
        const int RewardY = 146;
        const int SkipX = 224, SkipY = 240, SkipW = 62, SkipH = 16;
        const int ChoiceX = 142, ChoiceW = 236, ChoiceH = 17;
        const int ContinueX = 292, ContinueY = 246, ContinueW = 84, ContinueH = 16;
        const int SheetX = 8, SheetY = 104, SheetW = 118, SheetH = 162;

        static int ChoiceY(int i) { return 194 + i * 21; }
        static int RewardCardX(int i) { return 255 - CardW / 2 + (i - 1) * 74; }

        int CardX(int i, int n)
        {
            if (n <= 1) return (HandLeft + HandRight) / 2 - CardW / 2;
            int span = HandRight - HandLeft - CardW;
            int step = Math.Min(CardW + 4, span / (n - 1));
            int total = step * (n - 1) + CardW;
            int start = (HandLeft + HandRight) / 2 - total / 2;
            return start + i * step;
        }

        int CardCenterX(int i) { return CardX(i, combat.Hand.Count) + CardW / 2; }

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

        static int NodeX(int layer) { return 158 + layer * 50; }
        static int NodeY(int layer, int index, int count) { return 200 + (int)((index - (count - 1) / 2f) * 38); }

        MapNode HoveredNode()
        {
            if (run == null) return null;
            foreach (var layer in run.Layers)
                foreach (var n in layer)
                {
                    int dx = input.X - NodeX(n.Layer), dy = input.Y - NodeY(n.Layer, n.Index, layer.Count);
                    if (dx * dx + dy * dy <= 12 * 12) return n;
                }
            return null;
        }

        // =====================================================================

        void Render()
        {
            var c = Canvas;
            c.ResetTint();
            c.CopyFrom(bg);

            int sx = shake > 0 ? fxRng.Next(-(int)shake, (int)shake + 1) : 0;
            int sy = shake > 0 ? fxRng.Next(-(int)shake, (int)shake + 1) : 0;

            // ---- Mundo (recebe a luz da lampada) ----
            DrawClockHands(c);
            DrawFriend(c);
            DrawLampShade(c);
            DrawCrumpledSheets(c);
            DrawRulesNotebook(c);
            c.OffX = sx; c.OffY = sy;
            if (screen == GameScreen.Combat) DrawCombatTable(c);
            c.OffX = 0; c.OffY = 0;
            DrawDmScreen(c);
            ApplyLighting();
            DrawMask(c);
            DrawBulbGlow(c);

            // ---- Interface (objetos de papel com luz aproximada) ----
            pendingTooltip = null;
            if (InRun) DrawSheet(c);

            switch (screen)
            {
                case GameScreen.Menu: DrawMenu(c); break;
                case GameScreen.NewGame: DrawMenu(c, false); DrawNewGame(c); break;
                case GameScreen.Options: DrawMenu(c, false); DrawOptions(c); break;
                case GameScreen.Map: DrawMap(c); DrawMapLegend(c); break;
                case GameScreen.Combat: DrawCombatUi(c); break;
                case GameScreen.Reward: DrawReward(c); break;
                case GameScreen.Event: DrawEvent(c); break;
                case GameScreen.Treasure: DrawTreasure(c); break;
                case GameScreen.Tavern: DrawTavern(c); break;
                case GameScreen.Death: DrawDeath(c); break;
                case GameScreen.Victory: DrawVictory(c); break;
            }

            c.ResetTint();
            DrawDice(c);
            DrawFloaters(c);
            DrawSpeech(c);
            DrawMenuTag(c);
            // A descricao da reliquia e desenhada por ultimo, por cima de cartas e mapa
            if (pendingTooltip != null) Tooltip(c, pendingTooltip, pendingTooltipX, pendingTooltipY);
            if (paused) DrawPause(c);
            DrawGrain(c);
        }

        string pendingTooltip;
        int pendingTooltipX, pendingTooltipY;

        // ---------------- Ficha do personagem ----------------

        void DrawSheet(PixelCanvas c)
        {
            var s = run.Sheet;
            TintAt(SheetX + SheetW / 2, SheetY + 60, 0.62f);
            int x = SheetX, y = SheetY;
            c.Fill(x + 2, y + 2, SheetW, SheetH, Rgb.Hex(0x2a1a10));
            c.Fill(x, y, SheetW, SheetH, Palette.Paper);
            for (int ly = y + 10; ly < y + SheetH; ly += 8) c.HLine(x + 1, x + SheetW - 2, ly, Rgb.Hex(0xdcd4c0));
            c.VLine(x + 5, y, y + SheetH - 1, Rgb.Hex(0xe0a0a0));
            c.Rect(x, y, SheetW, SheetH, Palette.PaperDark);

            PixelFont.Small.Draw(c, "CHARACTER SHEET", x + 8, y + 3, Palette.Pencil);
            PixelFont.Big.Draw(c, s.Name.ToUpperInvariant(), x + 8, y + 11, Palette.Ink);
            PixelFont.Small.Draw(c, "LV " + s.Level, x + 88, y + 14, Palette.Pencil);

            for (int i = 0; i < 6; i++)
            {
                var a = (Attr)i;
                int bx = x + 8 + (i % 3) * 36, by = y + 24 + (i / 3) * 22;
                c.Rect(bx, by, 33, 20, Palette.Pencil);
                PixelFont.Small.DrawCentered(c, CharacterSheet.AttrName(a), bx + 16, by + 1, Palette.Pencil);
                PixelFont.Big.DrawCentered(c, s.Score(a).ToString(), bx + 16, by + 6, Palette.Ink);
                int m = s.Mod(a);
                PixelFont.Small.Draw(c, (m >= 0 ? "+" : "") + m, bx + 25, by + 12, m > 0 ? Rgb.Hex(0x3a7a3a) : (m < 0 ? Palette.DarkRed : Palette.Pencil));
            }

            int hp = screen == GameScreen.Combat ? shownPlayerHp : s.Hp;
            int yy = y + 72;
            PixelFont.Small.Draw(c, "HP", x + 8, yy + 1, Palette.Pencil);
            c.Fill(x + 20, yy, 90, 9, Rgb.Hex(0x3a1a1a));
            int fill = (int)(88f * Math.Max(0, hp) / s.MaxHp);
            c.Fill(x + 21, yy + 1, fill, 7, hp <= s.MaxHp / 4 ? Palette.Red : Rgb.Hex(0xb03a30));
            if (playerHurt > 0.5f) c.Fill(x + 21, yy + 1, fill, 7, Palette.White);
            PixelFont.Small.DrawCentered(c, hp + "/" + s.MaxHp, x + 65, yy + 2, Palette.White);

            yy += 13;
            int ac = combat != null ? combat.PlayerArmorClass : s.ArmorClass;
            c.Sprite(Sprites.IconShield, x + 8, yy, ch => ch == 'a' ? Rgb.Hex(0x8a98a8) : (ch == 'b' ? Palette.Ink : Palette.White));
            PixelFont.Big.Draw(c, "AC " + ac, x + 20, yy, Palette.Ink);
            if (screen == GameScreen.Combat && shownBlock > 0)
            {
                PixelFont.Big.Draw(c, "BLOCK " + shownBlock, x + 60, yy, Rgb.Hex(0x2a5a9a));
            }

            yy += 13;
            PixelFont.Small.Draw(c, "ENERGY", x + 8, yy + 2, Palette.Pencil);
            int energy = combat != null ? combat.Energy : s.EnergyPerTurn;
            for (int i = 0; i < s.EnergyPerTurn; i++)
            {
                int ex = x + 42 + i * 12;
                c.FillCircle(ex, yy + 4, 4, Palette.Ink);
                c.FillCircle(ex, yy + 4, 3, i < energy ? Palette.Gold : Rgb.Hex(0x8a8070));
                if (i < energy) c.Set(ex - 1, yy + 3, Palette.White);
            }

            yy += 13;
            PixelFont.Small.Draw(c, "RELICS", x + 8, yy, Palette.Pencil);
            yy += 8;
            if (s.Relics.Count == 0) PixelFont.Small.Draw(c, "- NONE -", x + 10, yy, Rgb.Hex(0xa8a090));
            RelicDef hoveredRelic = null;
            int hoveredRelicY = 0;
            foreach (var r in s.Relics)
            {
                bool hov = Hover(x + 6, yy - 1, SheetW - 12, 8);
                PixelFont.Small.Draw(c, "* " + r.Name, x + 10, yy, hov ? Palette.DarkRed : Palette.Ink);
                if (hov) { hoveredRelic = r; hoveredRelicY = yy; }
                yy += 8;
            }

            PixelFont.Small.Draw(c, "DECK: " + s.Deck.Count + " CARDS", x + 8, y + SheetH - 10, Palette.Pencil);

            if (hoveredRelic != null && !paused)
            {
                pendingTooltip = hoveredRelic.Name + ": " + hoveredRelic.Description;
                pendingTooltipX = x + SheetW + 4;
                pendingTooltipY = hoveredRelicY - 6;
            }
            c.ResetTint();
        }

        void Tooltip(PixelCanvas c, string text, int x, int y)
        {
            c.ResetTint();
            var lines = PixelFont.Small.Wrap(text, 110);
            int h = lines.Count * PixelFont.Small.LineHeight + 6;
            c.Fill(x, y, 118, h, Palette.Ink);
            c.Rect(x, y, 118, h, Palette.Gold);
            PixelFont.Small.DrawWrapped(c, text, x + 4, y + 3, 110, Palette.Paper);
        }

        void DrawRulesNotebook(PixelCanvas c)
        {
            if (!InRun) return;
            int x = 318, y = 102;
            c.Fill(x + 2, y + 2, 58, 22, Rgb.Hex(0x2a1a10));
            c.Fill(x, y, 58, 22, Rgb.Hex(0x2a3a5a));
            c.Fill(x + 3, y + 3, 52, 16, Rgb.Hex(0xe0d8c0));
            for (int i = 0; i < 6; i++) c.Set(x + 4 + i * 9, y + 1, Rgb.Hex(0xb0b0b8));
            PixelFont.Small.Draw(c, "HOMEBREW", x + 6, y + 4, Palette.Ink);
            PixelFont.Small.Draw(c, "V" + (run != null ? run.RulesVersion : selectedRules) + ".0", x + 6, y + 11, Palette.DarkRed);
        }

        // ---------------- Mapa ----------------

        void DrawMap(PixelCanvas c)
        {
            TintAt(255, 190, 0.7f);
            int x = 136, y = 130, w = 246, h = 134;
            c.Fill(x + 3, y + 3, w, h, Rgb.Hex(0x2a1a10));
            c.Fill(x, y, w, h, Rgb.Hex(0xe6e0cc));
            for (int gx = x + 6; gx < x + w; gx += 8) c.VLine(gx, y + 1, y + h - 2, Rgb.Hex(0xcfd8dc));
            for (int gy = y + 6; gy < y + h; gy += 8) c.HLine(x + 1, x + w - 2, gy, Rgb.Hex(0xcfd8dc));
            c.Rect(x, y, w, h, Palette.PaperDark);
            PixelFont.Small.Draw(c, "DUNGEON OF THE FACELESS KING", x + 6, y + 4, Palette.Pencil);
            PixelFont.Small.Draw(c, "FLOOR 1", x + w - 32, y + 4, Palette.Pencil);

            var hovered = HoveredNode();

            // caminhos
            foreach (var layer in run.Layers)
                foreach (var n in layer)
                    foreach (int ni in n.Next)
                    {
                        var m = run.Layers[n.Layer + 1][ni];
                        int x0 = NodeX(n.Layer), y0 = NodeY(n.Layer, n.Index, layer.Count);
                        int x1 = NodeX(m.Layer), y1 = NodeY(m.Layer, m.Index, run.Layers[m.Layer].Count);
                        bool walked = n.Visited && m.Visited;
                        bool open = n == run.Current;
                        if (walked) { c.Line(x0, y0, x1, y1, Palette.Ink); c.Line(x0, y0 + 1, x1, y1 + 1, Palette.Ink); }
                        else c.DashedLine(x0, y0, x1, y1, open ? Palette.Ink : Palette.Pencil, 3, 3);
                    }

            // salas
            foreach (var layer in run.Layers)
                foreach (var n in layer)
                {
                    int nx = NodeX(n.Layer), ny = NodeY(n.Layer, n.Index, layer.Count);
                    bool reachable = run.CanTravelTo(n) && !Busy;
                    bool hov = n == hovered && reachable;
                    int r = n.Type == NodeType.Boss ? 11 : 9;
                    if (reachable)
                    {
                        float pulse = 0.5f + 0.5f * (float)Math.Sin(time * 5);
                        c.FillCircle(nx, ny, r + 2 + (hov ? 1 : 0), Rgb.Lerp(Palette.Gold, Palette.Red, hov ? 1f : pulse * 0.3f));
                    }
                    c.FillCircle(nx, ny, r, n.Visited ? Rgb.Hex(0xb8b2a0) : Rgb.Hex(0xf2ecd8));
                    c.Circle(nx, ny, r, Palette.Ink);
                    DrawNodeIcon(c, n.Type, nx - 4, ny - 4, n.Visited && n != run.Current);
                    if (n == run.Current)
                    {
                        // o "voce esta aqui" rabiscado de vermelho
                        c.Circle(nx, ny, r + 3, Palette.Red);
                        c.Circle(nx + 1, ny, r + 3, Palette.Red);
                    }
                }

            if (hovered != null)
            {
                string label = NodeLabel(hovered.Type);
                int lw = PixelFont.Small.Measure(label) + 6;
                int lx = NodeX(hovered.Layer) - lw / 2, ly = NodeY(hovered.Layer, hovered.Index, run.Layers[hovered.Layer].Count) - 24;
                c.Fill(lx, ly, lw, 9, Palette.Ink);
                PixelFont.Small.Draw(c, label, lx + 3, ly + 2, Palette.Paper);
            }
            if (!scriptActive)
                PixelFont.Small.DrawCentered(c, "CHOOSE THE NEXT ROOM", x + w / 2, y + h - 10, Palette.DarkRed);
            c.ResetTint();
        }

        static string NodeLabel(NodeType t)
        {
            switch (t)
            {
                case NodeType.Start: return "ENTRANCE";
                case NodeType.Combat: return "COMBAT";
                case NodeType.Event: return "EVENT";
                case NodeType.Treasure: return "TREASURE";
                case NodeType.Tavern: return "SNACK";
                default: return "BOSS";
            }
        }

        void DrawNodeIcon(PixelCanvas c, NodeType t, int x, int y, bool faded)
        {
            string[] icon;
            Rgb main;
            switch (t)
            {
                case NodeType.Start: icon = Sprites.IconDoor; main = Rgb.Hex(0x8a6038); break;
                case NodeType.Combat: icon = Sprites.IconSwords; main = Palette.Ink; break;
                case NodeType.Event: icon = Sprites.IconQuestion; main = Rgb.Hex(0x3a5a9a); break;
                case NodeType.Treasure: icon = Sprites.IconChest; main = Rgb.Hex(0xc09030); break;
                case NodeType.Tavern: icon = Sprites.IconMug; main = Rgb.Hex(0xc08030); break;
                default: icon = Sprites.IconCrown; main = Palette.DarkRed; break;
            }
            if (faded) main = Palette.Pencil;
            var dark = Palette.Ink;
            c.Sprite(icon, x, y, ch => ch == 'a' ? main : (ch == 'b' ? dark : (Rgb?)Rgb.Hex(0xa8a8b0)));
        }

        void DrawMapLegend(PixelCanvas c)
        {
            TintAt(430, 150, 0.6f);
            int x = 392, y = 132;
            c.Fill(x, y, 82, 70, Rgb.Hex(0xe0d8c0));
            c.Rect(x, y, 82, 70, Palette.PaperDark);
            PixelFont.Small.Draw(c, "LEGEND", x + 4, y + 3, Palette.Pencil);
            var types = new[] { NodeType.Combat, NodeType.Event, NodeType.Treasure, NodeType.Tavern, NodeType.Boss };
            for (int i = 0; i < types.Length; i++)
            {
                DrawNodeIcon(c, types[i], x + 4, y + 12 + i * 11, false);
                PixelFont.Small.Draw(c, NodeLabel(types[i]), x + 17, y + 14 + i * 11, Palette.Ink);
            }
            c.ResetTint();
        }

        // ---------------- Combate ----------------

        /// <summary>A parte "fisica" do combate: papel quadriculado e as miniaturas.</summary>
        void DrawCombatTable(PixelCanvas c)
        {
            int x = 134, y = 128, w = 246, h = 66;
            c.Fill(x, y, w, h, Rgb.Hex(0xd8d2bc));
            for (int gx = x; gx < x + w; gx += 10) c.VLine(gx, y, y + h - 1, Rgb.Hex(0xb4c4c8));
            for (int gy = y; gy < y + h; gy += 10) c.HLine(x, x + w - 1, gy, Rgb.Hex(0xb4c4c8));
            c.Rect(x, y, w, h, Rgb.Hex(0x9a927a));

            // jogador
            int pj = playerHurt > 0 ? (int)(Math.Sin(time * 60) * 2 * playerHurt) : 0;
            c.FillEllipse(PlayerMiniX, MiniFeetY + 1, 15, 4, Rgb.Hex(0x7a7260));
            c.Sprite(Sprites.Knight, PlayerMiniX - 16 + pj, MiniFeetY - Sprites.Knight.Length * 2 + 2, KnightPal, false, 2);

            // inimigo
            if (enemyFade < 1f)
            {
                var e = combat.Enemy;
                string[] spr = EnemySpriteRows(e.Sprite);
                int sw = spr[0].Length;
                int ej = enemyHurt > 0 ? (int)(Math.Sin(time * 60) * 2 * enemyHurt) : 0;
                c.FillEllipse(EnemyMiniX, MiniFeetY + 1, sw, 4, Rgb.Hex(0x7a7260));
                if (enemyFade > 0)
                {
                    // miniatura derrubada
                    for (int j = 0; j < spr.Length; j++)
                        for (int i = 0; i < sw; i++)
                        {
                            char ch = spr[j][i];
                            if (ch == '.') continue;
                            var col = EnemyPal(e.Sprite, ch);
                            if (col.HasValue) c.Fill(EnemyMiniX - spr.Length + (spr.Length - j) * 2, MiniFeetY - sw + i * 2 - 2, 2, 2, col.Value.Mul(0.7f));
                        }
                }
                else
                {
                    var hurtFlash = enemyHurt > 0.6f;
                    c.Sprite(spr, EnemyMiniX - sw + ej, MiniFeetY - spr.Length * 2 + 2, ch => hurtFlash ? (Rgb?)Palette.White : EnemyPal(e.Sprite, ch), true, 2);
                }
            }
        }

        static string[] EnemySpriteRows(EnemySprite s)
        {
            switch (s)
            {
                case EnemySprite.Goblin: return Sprites.Goblin;
                case EnemySprite.Skeleton: return Sprites.Skeleton;
                case EnemySprite.Cultist: return Sprites.Cultist;
                default: return Sprites.Boss;
            }
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

        void DrawCombatUi(PixelCanvas c)
        {
            TintAt(255, 160, 0.75f);
            var e = combat.Enemy;

            // barras de vida debaixo das miniaturas
            if (enemyFade <= 0)
            {
                DrawHpBar(c, EnemyMiniX, 180, shownEnemyHp, e.MaxHp, shownEnemyBlock);
                PixelFont.Small.DrawCentered(c, e.Name + "  AC " + combat.EnemyArmorClass, EnemyMiniX, 186, Palette.Ink);
                DrawIntent(c);
            }
            DrawHpBar(c, PlayerMiniX, 180, shownPlayerHp, run.Sheet.MaxHp, shownBlock);
            PixelFont.Small.DrawCentered(c, "YOU  AC " + combat.PlayerArmorClass, PlayerMiniX, 186, Palette.Ink);
            if (combat.NextAttackAdvantage) PixelFont.Small.DrawCentered(c, "ADVANTAGE", PlayerMiniX, 131, Rgb.Hex(0x9a7010));

            // botao de fim de turno
            TintAt(430, 140, 0.7f);
            bool canEnd = !Busy && !combat.Over && !scriptActive;
            bool hov = canEnd && Hover(EndTurnX, EndTurnY, EndTurnW, EndTurnH);
            c.Fill(EndTurnX + 2, EndTurnY + 2, EndTurnW, EndTurnH, Rgb.Hex(0x2a1a10));
            c.Fill(EndTurnX, EndTurnY, EndTurnW, EndTurnH, hov ? Rgb.Hex(0xf0d890) : Palette.Paper);
            c.Rect(EndTurnX, EndTurnY, EndTurnW, EndTurnH, Palette.Ink);
            PixelFont.Big.DrawCentered(c, "END TURN", EndTurnX + EndTurnW / 2, EndTurnY + 5, canEnd ? Palette.Ink : Palette.Pencil);

            // pilhas de compra e descarte
            DrawPile(c, 398, 136, combat.DrawPile.Count, "DRAW", true);
            DrawPile(c, 438, 136, combat.Discard.Count, "DISCARD", false);
            PixelFont.Small.Draw(c, "TURN " + combat.Turn, 398, 188, Palette.Paper);
            if (combat.Exhausted.Count > 0) PixelFont.Small.Draw(c, "EXHAUSTED " + combat.Exhausted.Count, 398, 196, Palette.Paper);

            DrawHand(c);
            c.ResetTint();
        }

        void DrawHpBar(PixelCanvas c, int cx, int y, int hp, int max, int block)
        {
            int w = 44;
            int x = cx - w / 2;
            c.Fill(x, y, w, 4, Rgb.Hex(0x3a1a1a));
            c.Fill(x, y, (int)(w * (float)Math.Max(0, hp) / max), 4, Palette.Red);
            PixelFont.Small.Draw(c, hp + "/" + max, x + w + 3, y - 1, Palette.Ink);
            if (block > 0)
            {
                c.Sprite(Sprites.IconShield, x - 11, y - 3, ch => ch == 'a' ? Palette.Blue : (ch == 'b' ? Palette.Ink : Palette.White));
                PixelFont.Small.DrawCentered(c, block.ToString(), x - 7, y - 1, Palette.White);
            }
        }

        void DrawIntent(PixelCanvas c)
        {
            if (Busy || dice != null) return;
            var m = combat.Intent;
            string text;
            Rgb col;
            string[] icon;
            if (m.Kind == MoveKind.Attack)
            {
                int bonus = m.AttackBonus - combat.EnemyWeaken;
                text = m.Name + " " + m.Damage + (combat.HiddenRolls ? " (?)" : " (+" + bonus + ")");
                col = Palette.DarkRed; icon = Sprites.IconSword;
            }
            else if (m.Kind == MoveKind.Guard) { text = m.Name; col = Rgb.Hex(0x2a5a9a); icon = Sprites.IconShield; }
            else { text = "???"; col = Rgb.Hex(0x6a3a8a); icon = Sprites.IconEye; }
            int tw = PixelFont.Small.Measure(text) + 16;
            int x = EnemyMiniX - 24 - tw, y = 132;
            if (m.Kind == MoveKind.Curse) y += (int)(Math.Sin(time * 3) * 1.5);
            c.Fill(x, y, tw, 11, Palette.Paper);
            c.Rect(x, y, tw, 11, col);
            var ic = col;
            c.Sprite(icon, x + 2, y + 1, ch => ch == 'c' ? (Rgb?)Palette.White : ic);
            PixelFont.Small.Draw(c, text, x + 13, y + 3, col);
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
            int hov = (Busy || scriptActive) ? -1 : HoveredCard();
            for (int i = 0; i < n; i++)
            {
                if (i == hov) continue;
                var card = combat.Hand[i];
                DrawCard(c, card, CardX(i, n), HandY, combat.CanPlay(card) && !Busy, false);
            }
            if (hov >= 0)
            {
                var card = combat.Hand[hov];
                DrawCard(c, card, CardX(hov, n), HandHoverY, combat.CanPlay(card), true);
            }
        }

        void DrawCard(PixelCanvas c, CardDef card, int x, int y, bool playable, bool hovered)
        {
            c.ResetTint();
            if (!hovered) TintAt(x + CardW / 2, Math.Min(H - 1, y + 20), 0.7f);
            if (!playable && !hovered) c.SetTint(c.TintR * 0.7f, c.TintG * 0.7f, c.TintB * 0.75f);

            bool curse = card.Kind == CardKind.Curse;
            var paper = curse ? Rgb.Hex(0x6a5a6a) : Palette.Paper;
            Rgb band;
            string kind;
            switch (card.Kind)
            {
                case CardKind.Attack: band = Rgb.Hex(0xa83a3a); kind = "ATTACK"; break;
                case CardKind.Defense: band = Rgb.Hex(0x3a6aa8); kind = "DEFENSE"; break;
                case CardKind.Skill: band = Rgb.Hex(0x3a8a5a); kind = "SKILL"; break;
                default: band = Rgb.Hex(0x3a1a4a); kind = "CURSE"; break;
            }

            c.FillAlpha(x + 2, y + 3, CardW, CardH, Palette.Black, 0.5f);
            c.Fill(x, y, CardW, CardH, paper);
            c.Fill(x, y, CardW, 11, band);
            c.Rect(x, y, CardW, CardH, hovered ? Palette.Gold : Palette.Ink);
            if (hovered) c.Rect(x - 1, y - 1, CardW + 2, CardH + 2, Palette.Gold);

            // custo
            if (!card.Unplayable)
            {
                c.FillCircle(x + 7, y + 7, 6, Palette.Ink);
                c.FillCircle(x + 7, y + 7, 5, Palette.Gold);
                PixelFont.Big.DrawCentered(c, card.Cost.ToString(), x + 8, y + 2, Palette.Ink);
            }
            PixelFont.Small.Draw(c, kind, x + CardW - 3 - PixelFont.Small.Measure(kind), y + 3, Palette.White);

            // nome
            var ink = curse ? Rgb.Hex(0xf0d0e0) : Palette.Ink;
            PixelFont.Small.DrawWrapped(c, card.Name, x + 3, y + 13, CardW - 6, curse ? Palette.Red : ink, true);

            // arte
            string[] icon = CardIconRows(card.Icon);
            Rgb main = band;
            if (card.Icon == CardIcon.Heart || card.Icon == CardIcon.Potion) main = Palette.Red;
            if (card.Icon == CardIcon.Star) main = Rgb.Hex(0xd0a020);
            if (card.Icon == CardIcon.Skull) main = Palette.Mask;
            c.Fill(x + 6, y + 27, CardW - 12, 22, curse ? Rgb.Hex(0x2a1a2a) : Rgb.Hex(0xd8ccb0));
            c.Sprite(icon, x + CardW / 2 - 9, y + 29, ch => ch == 'a' ? main : (ch == 'b' ? Palette.Ink : Palette.White), false, 2);

            // descricao
            PixelFont.Small.DrawWrapped(c, card.Description(run.Sheet), x + 3, y + 52, CardW - 6, ink, true);
            c.ResetTint();
        }

        static string[] CardIconRows(CardIcon i)
        {
            switch (i)
            {
                case CardIcon.Sword: return Sprites.IconSword;
                case CardIcon.Shield: return Sprites.IconShield;
                case CardIcon.Star: return Sprites.IconStar;
                case CardIcon.Dagger: return Sprites.IconDagger;
                case CardIcon.Heart: return Sprites.IconHeart;
                case CardIcon.Eye: return Sprites.IconEye;
                case CardIcon.Potion: return Sprites.IconPotion;
                case CardIcon.Skull: return Sprites.IconSkull;
                default: return Sprites.IconBoot;
            }
        }

        // ---------------- Dados ----------------

        void DrawDice(PixelCanvas c)
        {
            if (dice == null) return;
            c.ResetTint();
            bool rolling = dice.Rolling > 0;
            var r = dice.Roll;

            if (dice.Hidden)
            {
                // o mestre rola atras do escudo: voce so ouve
                int bx = 240, by = 78;
                c.Fill(bx - 16, by - 9, 32, 16, Palette.Ink);
                c.Rect(bx - 16, by - 9, 32, 16, Palette.Red);
                string t = rolling ? "?" : dice.Announced.ToString();
                PixelFont.Big.DrawCentered(c, t, bx, by - 6, rolling ? Palette.Paper : Palette.Red);
                PixelFont.Small.DrawCentered(c, "BEHIND THE SCREEN", bx, by + 9, Palette.Paper);
                return;
            }

            int x = dice.X, y = dice.Y + (rolling ? (int)(-Math.Abs(Math.Sin(dice.Rolling * 14)) * 6) : 0);
            if (r.Advantage || r.Disadvantage)
            {
                DrawD20(c, x - 30, y + 4, rolling ? fxRng.Next(1, 21) : r.Other, false, rolling, 0.55f);
            }
            DrawD20(c, x, y, rolling ? dice.Flicker : r.Natural, true, rolling, 1f);

            int ty = dice.Y + 18;
            c.FillAlpha(x - 60, ty - 1, 120, rolling ? 9 : 17, Palette.Black, 0.6f);
            PixelFont.Small.DrawCentered(c, dice.Label + (r.Advantage ? " (ADVANTAGE)" : ""), x, ty, Palette.Paper);
            if (!rolling)
            {
                string vs = r.Target > 0 ? "  VS " + r.Target : "";
                string m = r.Modifier >= 0 ? "+" + r.Modifier : r.Modifier.ToString();
                string line = r.Natural + m + " = " + r.Total + vs;
                Rgb col = r.IsCrit ? Palette.Gold : (r.Success ? Palette.Green : Palette.Red);
                PixelFont.Small.DrawCentered(c, line + (r.Success ? "  SUCCESS" : "  FAIL"), x, ty + 8, col);
            }
        }

        void DrawD20(PixelCanvas c, int x, int y, int value, bool main, bool rolling, float bright)
        {
            var r = dice.Roll;
            Rgb face = Rgb.Hex(0xece6d4).Mul(bright);
            Rgb edge = Palette.Ink;
            if (main && !rolling)
            {
                if (r.IsCrit) face = Palette.Gold;
                else if (r.IsFumble) face = Rgb.Hex(0xd07060);
            }
            // hexagono com triangulo interno: a silhueta classica de um d20
            c.FillTriangle(x, y - 14, x + 12, y - 7, x - 12, y - 7, face);
            c.Fill(x - 12, y - 7, 25, 15, face);
            c.FillTriangle(x - 12, y + 7, x + 12, y + 7, x, y + 14, face);
            c.Line(x, y - 14, x + 12, y - 7, edge); c.Line(x + 12, y - 7, x + 12, y + 7, edge);
            c.Line(x + 12, y + 7, x, y + 14, edge); c.Line(x, y + 14, x - 12, y + 7, edge);
            c.Line(x - 12, y + 7, x - 12, y - 7, edge); c.Line(x - 12, y - 7, x, y - 14, edge);
            var inner = face.Mul(0.8f);
            c.Line(x - 8, y + 6, x + 8, y + 6, inner); c.Line(x - 8, y + 6, x, y - 8, inner); c.Line(x + 8, y + 6, x, y - 8, inner);
            PixelFont.Big.DrawCentered(c, value.ToString(), x + 1, y - 4, main ? edge : edge.Mul(0.8f));
        }

        void DrawFloaters(PixelCanvas c)
        {
            c.ResetTint();
            foreach (var f in floaters)
            {
                if (f.T > 1.0f && ((int)(f.T * 20) & 1) == 1) continue;
                PixelFont.Big.DrawShadowed(c, f.Text, (int)f.X - PixelFont.Big.Measure(f.Text) / 2, (int)f.Y, f.Color, Palette.Black);
            }
        }

        // ---------------- Fala do amigo ----------------

        void DrawSpeech(PixelCanvas c)
        {
            if (speech == null) return;
            c.ResetTint();
            int x = 292, y = 28, w = 182, h = 68;
            c.FillAlpha(x, y, w, h, Palette.Black, 0.7f);
            var border = maskOn ? Rgb.Lerp(Palette.PaperDark, Palette.Red, Dread) : Palette.PaperDark;
            c.Rect(x, y, w, h, border);
            // "rabinho" do balao apontando para o amigo
            c.FillTriangle(x, y + 30, x - 10, y + 38, x, y + 40, Palette.Black);

            string name = FriendName;
            int nw = PixelFont.Small.Measure(name) + 6;
            c.Fill(x + 4, y - 4, nw, 9, border);
            PixelFont.Small.Draw(c, name, x + 7, y - 2, Palette.Black);

            var textColor = maskOn ? Rgb.Lerp(Rgb.Hex(0xf0e6c8), Rgb.Hex(0xe07060), Dread * 0.8f) : Rgb.Hex(0xf0ead8);
            int jitter = (maskOn && Dread > 0.6f && fxRng.NextDouble() < 0.08) ? 1 : 0;
            PixelFont.Big.DrawWrapped(c, speech.Text, x + 6 + jitter, y + 8, w - 12, textColor, false, (int)speechTyped);

            if (speech.WaitClick && speechTyped >= speech.Text.Length && ((int)(time * 3) & 1) == 0)
                c.FillTriangle(x + w - 12, y + h - 9, x + w - 5, y + h - 9, x + w - 9, y + h - 5, Palette.Gold);
        }

        void DrawGrain(PixelCanvas c)
        {
            float d = Dread;
            int n = (int)(200 + d * 1400);
            for (int i = 0; i < n; i++)
            {
                int x = fxRng.Next(W), y = fxRng.Next(H);
                c.Blend(x, y, fxRng.Next(2) == 0 ? Palette.Black : Palette.White, 0.08f + d * 0.08f);
            }
            if (d > 0.5f && fxRng.NextDouble() < 0.03)
            {
                // linha de interferencia, como uma fita VHS velha
                int y = fxRng.Next(H);
                for (int x = 0; x < W; x++) c.Blend(x, y, Palette.White, 0.15f);
            }
        }

        // ---------------- Recompensa, evento, tesouro, lanche ----------------

        void DrawReward(PixelCanvas c)
        {
            c.ResetTint();
            c.FillAlpha(132, 128, 252, 136, Palette.Black, 0.45f);
            PixelFont.Big.DrawCentered(c, "WRITE A CARD ON YOUR SHEET", 258, 132, Palette.Paper);
            int hov = -1;
            for (int i = 0; i < 3; i++) if (Hover(RewardCardX(i), RewardY, CardW, CardH)) hov = i;
            for (int i = 0; i < 3; i++)
                DrawCard(c, rewardCards[i], RewardCardX(i), RewardY - (i == hov ? 4 : 0), true, i == hov);
            DrawButton(c, SkipX, SkipY, SkipW, SkipH, "SKIP");
        }

        void DrawButton(PixelCanvas c, int x, int y, int w, int h, string label)
        {
            c.ResetTint();
            bool hov = !Busy && Hover(x, y, w, h);
            c.Fill(x + 2, y + 2, w, h, Palette.Black);
            c.Fill(x, y, w, h, hov ? Rgb.Hex(0xf0d890) : Palette.Paper);
            c.Rect(x, y, w, h, Palette.Ink);
            PixelFont.Big.DrawCentered(c, label, x + w / 2, y + h / 2 - 5, Palette.Ink);
        }

        void DrawPaperSheet(PixelCanvas c, string title)
        {
            TintAt(255, 180, 0.75f);
            int x = 134, y = 110, w = 250, h = 156;
            c.Fill(x + 3, y + 3, w, h, Rgb.Hex(0x2a1a10));
            c.Fill(x, y, w, h, Palette.Paper);
            for (int ly = y + 18; ly < y + h; ly += 10) c.HLine(x + 2, x + w - 3, ly, Rgb.Hex(0xdcd4c0));
            c.Rect(x, y, w, h, Palette.PaperDark);
            PixelFont.Big.DrawCentered(c, title, x + w / 2, y + 5, Palette.DarkRed);
        }

        void DrawEvent(PixelCanvas c)
        {
            var ev = currentEvent;
            DrawPaperSheet(c, ev.Title);
            PixelFont.Big.DrawWrapped(c, ev.Body, 142, 128, 234, Palette.Ink);

            if (!eventResolved)
            {
                if (Busy) { c.ResetTint(); return; }
                for (int i = 0; i < ev.Choices.Length; i++)
                {
                    var ch = ev.Choices[i];
                    string label = ch.Label;
                    if (ch.HasTest)
                    {
                        int m = run.Sheet.Mod(ch.TestAttr);
                        label += "  [" + CharacterSheet.AttrName(ch.TestAttr) + " DC " + ch.Dc + ", " + (m >= 0 ? "+" : "") + m + "]";
                    }
                    DrawChoice(c, i, label);
                }
            }
            else
            {
                c.ResetTint();
                TintAt(255, 200, 0.75f);
                PixelFont.Big.DrawWrapped(c, eventResult, 142, 194, 234, Palette.DarkRed);
                DrawButton(c, ContinueX, ContinueY, ContinueW, ContinueH, "CONTINUE");
            }
            c.ResetTint();
        }

        void DrawChoice(PixelCanvas c, int i, string label)
        {
            int y = ChoiceY(i);
            bool hov = !Busy && Hover(ChoiceX, y, ChoiceW, ChoiceH);
            c.Fill(ChoiceX, y, ChoiceW, ChoiceH, hov ? Rgb.Hex(0xf0d890) : Rgb.Hex(0xe2d6b8));
            c.Rect(ChoiceX, y, ChoiceW, ChoiceH, hov ? Palette.DarkRed : Palette.Pencil);
            PixelFont.Big.Draw(c, (i + 1) + ". " + label, ChoiceX + 4, y + 3, Palette.Ink);
        }

        void DrawTreasure(PixelCanvas c)
        {
            DrawPaperSheet(c, "TREASURE");
            c.Sprite(Sprites.IconChest, 237, 132, ch => ch == 'a' ? Rgb.Hex(0xc09030) : (ch == 'b' ? Palette.Ink : Palette.Gold), false, 4);
            if (treasureRelic != null)
            {
                PixelFont.Big.DrawCentered(c, treasureRelic.Name, 259, 176, Palette.DarkRed);
                PixelFont.Big.DrawWrapped(c, treasureRelic.Description, 150, 192, 218, Palette.Ink, true);
                DrawButton(c, ContinueX, ContinueY, ContinueW, ContinueH, "TAKE");
            }
            else
            {
                PixelFont.Big.DrawWrapped(c, "THE CHEST IS EMPTY. SOMEONE GOT HERE FIRST.", 150, 184, 218, Palette.Ink, true);
                DrawButton(c, ContinueX, ContinueY, ContinueW, ContinueH, "MOVE ON");
            }
            c.ResetTint();
        }

        void DrawTavern(PixelCanvas c)
        {
            DrawPaperSheet(c, "SNACK BREAK");
            PixelFont.Big.DrawWrapped(c, "YOU STOP TO EAT CHIPS. YOU YELL UPSTAIRS, ASKING HIS MOM FOR SODA. NOBODY ANSWERS.", 142, 128, 234, Palette.Ink);
            if (tavernResult == null)
            {
                int pct = (int)(Homebrew.TavernHealFraction(run.RulesVersion) * 100);
                DrawChoice(c, 0, "REST  [HEAL " + pct + "% OF YOUR HP]");
                DrawChoice(c, 1, "TRAIN  [+2 STR, PERMANENT]");
            }
            else
            {
                PixelFont.Big.DrawWrapped(c, tavernResult, 142, 194, 234, Palette.DarkRed);
                DrawButton(c, ContinueX, ContinueY, ContinueW, ContinueH, "CONTINUE");
            }
            c.ResetTint();
        }

        // ---------------- Morte e vitoria ----------------

        void DrawDeath(PixelCanvas c)
        {
            c.ResetTint();
            c.FillAlpha(0, 0, W, H, Rgb.Hex(0x100406), 0.45f);
            // rabisco vermelho sobre a ficha
            for (int i = 0; i < 3; i++)
            {
                c.Line(SheetX + 6 + i, SheetY + 10, SheetX + SheetW - 8 + i, SheetY + SheetH - 12, Palette.Red);
                c.Line(SheetX + SheetW - 8 + i, SheetY + 10, SheetX + 6 + i, SheetY + SheetH - 12, Palette.Red);
            }
            PixelFont.Big.DrawCentered(c, "DEAD", SheetX + SheetW / 2 + 1, SheetY + 70, Palette.Black, 2);
            PixelFont.Big.DrawCentered(c, "DEAD", SheetX + SheetW / 2, SheetY + 69, Palette.Red, 2);
            PixelFont.Big.DrawCentered(c, "YOUR CHARACTER DIED", 256, 150, Palette.Red, 2);
            PixelFont.Small.DrawCentered(c, "SESSION " + sessions + " - ROOM " + run.Current.Layer + " OF 4", 256, 176, Palette.PaperDark);
        }

        void DrawVictory(PixelCanvas c)
        {
            c.ResetTint();
            TintAt(255, 180, 0.8f);
            int x = 160, y = 116, w = 196, h = 146;
            c.Fill(x + 3, y + 3, w, h, Palette.Black);
            c.Fill(x, y, w, h, Rgb.Hex(0xe8e0c8));
            for (int ly = y + 22; ly < y + h; ly += 10) c.HLine(x + 2, x + w - 3, ly, Palette.PaperLine);
            c.VLine(x + 12, y, y + h - 1, Rgb.Hex(0xe0a0a0));
            PixelFont.Big.DrawCentered(c, "HOMEBREW RULES", x + w / 2, y + 6, Palette.Ink);
            int newest = unlockedRules;
            for (int i = 0; i < newest && i < Homebrew.Texts.Length; i++)
            {
                bool isNew = newRulesUnlocked && i == newest - 1;
                var col = isNew ? Palette.DarkRed : Palette.Ink;
                int jx = isNew ? fxRng.Next(0, 2) : 0;
                PixelFont.Small.DrawWrapped(c, Homebrew.Texts[i], x + 16 + jx, y + 24 + i * 23, w - 24, col);
            }
            c.ResetTint();
        }
    }
}
