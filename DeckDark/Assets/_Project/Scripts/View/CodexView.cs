using System;
using System.Collections.Generic;
using System.Reflection;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>
    /// Codex: o album de tudo o que ja apareceu. Cartas por classe (mais as neutras) e inimigos.
    /// O que nunca foi visto aparece como "???". O registro e salvo no host (codex_cards, codex_enemies).
    /// Uma carta conta como vista quando e oferecida como recompensa, entra no deck ou aparece na loja
    /// (a loja deve chamar MarkCardSeen). Um inimigo conta quando aparece em combate.
    /// </summary>
    public partial class GameApp
    {
        enum CodexTab { Warrior, Wizard, Neutral, Enemies }

        CodexTab codexTab;
        int codexPage;
        CharacterSheet codexSheet;     // ficha usada para escrever o texto das cartas no codex

        // ---- registro persistente ----
        HashSet<string> seenCards;
        Dictionary<string, int[]> enemyLog;   // nome -> { visto (0/1), vezes derrotado }

        // ---- acompanhamento da partida (so para saber quando registrar algo novo) ----
        RunState codexRun;
        int codexDeckCount = -1;
        CardDef[] codexRewards;
        Combat codexCombat;
        int codexEnemyCount;
        Combat codexKillsCounted;

        /// <summary>Descricao das cartas: a ficha da aba no codex, a ficha da run no resto.</summary>
        CharacterSheet CardTextSheet
        {
            get
            {
                if (screen == GameScreen.Codex) return codexSheet;
                return run != null ? run.Sheet : null;
            }
        }

        // =====================================================================
        // Registro
        // =====================================================================

        void LoadCodex()
        {
            if (seenCards != null) return;
            seenCards = new HashSet<string>();
            foreach (var id in host.LoadString("codex_cards", "").Split(',')) if (id.Length > 0) seenCards.Add(id);
            enemyLog = new Dictionary<string, int[]>();
            // formato: NOME:visto:derrotas|NOME:visto:derrotas
            foreach (var entry in host.LoadString("codex_enemies", "").Split('|'))
            {
                var p = entry.Split(':');
                if (p.Length < 3 || p[0].Length == 0) continue;
                int seen, kills;
                int.TryParse(p[1], out seen);
                int.TryParse(p[2], out kills);
                enemyLog[p[0]] = new[] { seen, kills };
            }
        }

        void SaveCodexCards() { host.SaveString("codex_cards", string.Join(",", seenCards)); }

        void SaveCodexEnemies()
        {
            var parts = new List<string>();
            foreach (var kv in enemyLog) parts.Add(kv.Key + ":" + kv.Value[0] + ":" + kv.Value[1]);
            host.SaveString("codex_enemies", string.Join("|", parts));
        }

        /// <summary>Marca uma carta como vista. Use na loja e em qualquer tela nova que mostre cartas.</summary>
        public void MarkCardSeen(CardDef c)
        {
            LoadCodex();
            if (c != null && c.Id != null && seenCards.Add(c.Id)) SaveCodexCards();
        }

        void MarkCardsSeen(IEnumerable<CardDef> cards)
        {
            LoadCodex();
            bool changed = false;
            foreach (var c in cards) if (c != null && c.Id != null && seenCards.Add(c.Id)) changed = true;
            if (changed) SaveCodexCards();
        }

        int[] EnemyEntry(string name)
        {
            int[] e;
            if (!enemyLog.TryGetValue(name, out e)) { e = new int[2]; enemyLog[name] = e; }
            return e;
        }

        bool CardSeen(CardDef c) { LoadCodex(); return seenCards.Contains(c.Id); }
        int[] EnemyInfo(EnemyDef e) { LoadCodex(); int[] v; return enemyLog.TryGetValue(e.Name, out v) ? v : new int[2]; }

        /// <summary>Chamado todo frame (via SaveHooksTick). So grava quando algo muda.</summary>
        void CodexTick()
        {
            if (run == null) { codexRun = null; return; }
            LoadCodex();
            if (run != codexRun || run.Sheet.Deck.Count != codexDeckCount)
            {
                codexRun = run;
                codexDeckCount = run.Sheet.Deck.Count;
                MarkCardsSeen(run.Sheet.Deck);
            }
            if (screen == GameScreen.Reward && rewardCards != null && rewardCards != codexRewards)
            {
                codexRewards = rewardCards;
                MarkCardsSeen(rewardCards);
            }
            if (combat != null)
            {
                if (combat != codexCombat || combat.Enemies.Count != codexEnemyCount)
                {
                    codexCombat = combat;
                    codexEnemyCount = combat.Enemies.Count;
                    bool changed = false;
                    foreach (var e in combat.Enemies)
                    {
                        var entry = EnemyEntry(e.Def.Name);
                        if (entry[0] == 0) { entry[0] = 1; changed = true; }
                    }
                    if (changed) SaveCodexEnemies();
                }
                // combate vencido: conta quem morreu (quem fugiu nao conta)
                if ((screen == GameScreen.Reward || screen == GameScreen.Victory) && combat != codexKillsCounted)
                {
                    codexKillsCounted = combat;
                    foreach (var e in combat.Enemies)
                        if (!e.Fled && (e.Gone || e.Hp <= 0)) EnemyEntry(e.Def.Name)[1]++;
                    SaveCodexEnemies();
                }
            }
        }

        // =====================================================================
        // Listas
        // =====================================================================

        static List<EnemyDef> allEnemies;

        static List<EnemyDef> AllEnemies
        {
            get
            {
                if (allEnemies == null)
                {
                    allEnemies = new List<EnemyDef>();
                    foreach (var f in typeof(EnemyLibrary).GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (f.FieldType != typeof(EnemyDef)) continue;
                        var e = f.GetValue(null) as EnemyDef;
                        if (e != null && !allEnemies.Contains(e)) allEnemies.Add(e);
                    }
                }
                return allEnemies;
            }
        }

        static List<CardDef>[] codexCardLists;

        /// <summary>Guerreiro = so do guerreiro, Mago = so do mago, Neutras = compartilhadas, de eventos e maldicoes.</summary>
        static List<CardDef> CardsForTab(CodexTab tab)
        {
            if (codexCardLists == null)
            {
                var warrior = new HashSet<CardDef>(CardLibrary.RewardPool);
                warrior.UnionWith(CharacterSheet.NewWarrior().Deck);
                var wizard = new HashSet<CardDef>(CardLibrary.WizardPool);
                wizard.UnionWith(CharacterSheet.NewWizard().Deck);
                codexCardLists = new[] { new List<CardDef>(), new List<CardDef>(), new List<CardDef>() };
                foreach (var c in RunSave.AllCards)
                {
                    bool w = warrior.Contains(c), z = wizard.Contains(c);
                    codexCardLists[w && !z ? 0 : (z && !w ? 1 : 2)].Add(c);
                }
                // dentro de cada aba: basicas, comuns, incomuns, raras, depois maldicoes
                foreach (var l in codexCardLists)
                {
                    var order = new List<CardDef>(l);
                    l.Sort((a, b) =>
                    {
                        int ka = a.Kind == CardKind.Curse ? 9 : (int)a.Rarity, kb = b.Kind == CardKind.Curse ? 9 : (int)b.Rarity;
                        return ka != kb ? ka.CompareTo(kb) : order.IndexOf(a).CompareTo(order.IndexOf(b));
                    });
                }
            }
            return codexCardLists[(int)tab];
        }

        static string EnemyNote(EnemyDef e)
        {
            switch (e.Name)
            {
                case "GIANT RAT": return "IT CAME FROM UNDER THE STAIRS. BITES MAKE YOU BLEED.";
                case "BONE SERVANT": return "THE KING KEEPS CALLING MORE OF THEM.";
                case "OGRE": return "RAFA DREW IT HIMSELF. HITS VERY HARD.";
                case "GHOUL": return "IT SMELLS LIKE THE BASEMENT.";
            }
            var notes = new List<string>();
            if (e.IsBoss) notes.Add("THE END OF THE DUNGEON.");
            if (e.FleesWhenHurt) notes.Add("RUNS AWAY WHEN HURT.");
            if (e.Reassembles) notes.Add("GETS BACK UP ONCE.");
            if (e.CurseScaling) notes.Add("STRONGER FOR EACH CURSE IN YOUR DECK.");
            foreach (var m in e.Pattern)
            {
                if (m.Kind == MoveKind.Summon) { notes.Add("CALLS FOR HELP."); break; }
                if (m.Kind == MoveKind.Curse) { notes.Add("WHISPERS CURSES INTO YOUR DECK."); break; }
            }
            if (e.IsElite && notes.Count == 0) notes.Add("ELITE. BIGGER THAN THE OTHERS.");
            if (notes.Count == 0) notes.Add("JUST A MONSTER. PROBABLY.");
            return string.Join(" ", notes);
        }

        // =====================================================================
        // Tela
        // =====================================================================

        const int CxX = 12, CxY = 6, CxW = 456, CxH = 258;
        const int CxTabY = CxY + 22, CxTabW = 74, CxTabH = 14;
        static int CxTabX(int i) { return CxX + 22 + i * (CxTabW + 4); }
        const int CxCols = 6, CxRows = 2, CxGap = 8;
        static int CxCardX(int col) { return CxX + (CxW - (CxCols * CardW + (CxCols - 1) * CxGap)) / 2 + 4 + col * (CardW + CxGap); }
        static int CxCardY(int row) { return CxY + 44 + row * (CardH + 8); }
        const int CxEnemyCols = 4, CxEnemyW = 100, CxEnemyH = 92;
        static int CxEnemyX(int col) { return CxX + (CxW - (CxEnemyCols * CxEnemyW + (CxEnemyCols - 1) * 8)) / 2 + 4 + col * (CxEnemyW + 8); }
        static int CxEnemyY(int row) { return CxY + 44 + row * (CxEnemyH + 6); }
        const int CxNavY = CxY + CxH - 22;
        const int CxPrevX = CxX + CxW / 2 - 60, CxNextX = CxX + CxW / 2 + 36, CxNavW = 24;
        const int CxBackX = CxX + CxW - 72, CxBackW = 60;

        int CodexPerPage { get { return codexTab == CodexTab.Enemies ? CxEnemyCols * 2 : CxCols * CxRows; } }
        int CodexCount { get { return codexTab == CodexTab.Enemies ? AllEnemies.Count : CardsForTab(codexTab).Count; } }
        int CodexPages { get { return Math.Max(1, (CodexCount + CodexPerPage - 1) / CodexPerPage); } }

        void OpenCodex()
        {
            Sfx("click");
            LoadCodex();
            codexPage = 0;
            codexTab = selectedClass == PlayerClass.Wizard ? CodexTab.Wizard : CodexTab.Warrior;
            UpdateCodexSheet();
            screen = GameScreen.Codex;
        }

        void CloseCodex()
        {
            Sfx("click", 0.5f);
            screen = GameScreen.Menu;
        }

        void UpdateCodexSheet()
        {
            codexSheet = codexTab == CodexTab.Wizard ? CharacterSheet.NewWizard()
                : (codexTab == CodexTab.Warrior ? CharacterSheet.NewWarrior() : CharacterSheet.New(selectedClass));
        }

        void ClickCodex()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!Hover(CxTabX(i), CxTabY, CxTabW, CxTabH)) continue;
                if ((CodexTab)i != codexTab) { codexTab = (CodexTab)i; codexPage = 0; UpdateCodexSheet(); Sfx("pencil", 0.6f); }
                return;
            }
            if (Hover(CxPrevX, CxNavY, CxNavW, 16))
            {
                if (codexPage > 0) { codexPage--; Sfx("card", 0.5f); } else Sfx("miss", 0.4f);
            }
            else if (Hover(CxNextX, CxNavY, CxNavW, 16))
            {
                if (codexPage < CodexPages - 1) { codexPage++; Sfx("card", 0.5f); } else Sfx("miss", 0.4f);
            }
            else if (Hover(CxBackX, CxNavY, CxBackW, 16)) CloseCodex();
        }

        void DrawCodex(PixelCanvas c)
        {
            LoadCodex();
            c.ResetTint();
            c.FillAlpha(0, 0, W, H, Palette.Black, 0.55f);
            DrawNotebook(c, CxX, CxY, CxW, CxH, "CODEX");

            // abas
            string[] names = { "WARRIOR", "WIZARD", "NEUTRAL", "MONSTERS" };
            for (int i = 0; i < 4; i++)
            {
                int x = CxTabX(i);
                bool sel = (int)codexTab == i;
                bool hov = Hover(x, CxTabY, CxTabW, CxTabH);
                c.Fill(x, CxTabY, CxTabW, CxTabH, sel ? Palette.Ink : (hov ? Rgb.Hex(0xf0d890) : Palette.Paper));
                c.Rect(x, CxTabY, CxTabW, CxTabH, Palette.Ink);
                PixelFont.Small.DrawCentered(c, names[i], x + CxTabW / 2, CxTabY + 4, sel ? Palette.Gold : Palette.Ink);
            }

            // contagem de vistos
            int seen = 0, total = CodexCount;
            if (codexTab == CodexTab.Enemies) { foreach (var e in AllEnemies) if (EnemyInfo(e)[0] > 0) seen++; }
            else foreach (var cd in CardsForTab(codexTab)) if (CardSeen(cd)) seen++;
            string count = "FOUND " + seen + "/" + total;
            PixelFont.Small.Draw(c, count, CxX + CxW - 12 - PixelFont.Small.Measure(count), CxTabY + 4, Palette.DarkRed);

            if (codexPage >= CodexPages) codexPage = CodexPages - 1;
            if (codexTab == CodexTab.Enemies) DrawCodexEnemies(c); else DrawCodexCards(c);

            // paginas
            DrawButton(c, CxPrevX, CxNavY, CxNavW, 16, "<");
            DrawButton(c, CxNextX, CxNavY, CxNavW, 16, ">");
            PixelFont.Small.DrawCentered(c, "PAGE " + (codexPage + 1) + "/" + CodexPages, (CxPrevX + CxNavW + CxNextX) / 2, CxNavY + 5, Palette.Ink);
            DrawButton(c, CxBackX, CxNavY, CxBackW, 16, "BACK");
        }

        void DrawCodexCards(PixelCanvas c)
        {
            var list = CardsForTab(codexTab);
            int start = codexPage * CodexPerPage;
            for (int k = 0; k < CodexPerPage && start + k < list.Count; k++)
            {
                var card = list[start + k];
                int x = CxCardX(k % CxCols), y = CxCardY(k / CxCols);
                if (!CardSeen(card)) { DrawUnknownCard(c, x, y); continue; }
                bool hov = Hover(x, y, CardW, CardH);
                DrawCard(c, card, x, y, true, hov);
                if (hov)
                {
                    var kws = card.Keywords();
                    if (kws.Count > 0)
                    {
                        var parts = new List<string>();
                        foreach (var kw in kws) parts.Add(Glossary.Explain(kw));
                        pendingTooltip = string.Join("\n", parts);
                        pendingTooltipX = x + CardW + 4 > W - 122 ? x - 122 : x + CardW + 4;
                        pendingTooltipY = y;
                    }
                }
            }
            c.ResetTint();
        }

        void DrawUnknownCard(PixelCanvas c, int x, int y)
        {
            c.ResetTint();
            c.FillAlpha(x + 2, y + 3, CardW, CardH, Palette.Black, 0.35f);
            c.Fill(x, y, CardW, CardH, Rgb.Hex(0x3a3440));
            c.Rect(x, y, CardW, CardH, Palette.Pencil);
            c.Rect(x + 3, y + 3, CardW - 6, CardH - 6, Rgb.Hex(0x4e4858));
            PixelFont.Big.DrawCentered(c, "?", x + CardW / 2, y + CardH / 2 - 10, Rgb.Hex(0x6a6478), 2);
            PixelFont.Small.DrawCentered(c, "???", x + CardW / 2, y + CardH - 14, Rgb.Hex(0x8a8498));
        }

        void DrawCodexEnemies(PixelCanvas c)
        {
            var list = AllEnemies;
            int start = codexPage * CodexPerPage;
            for (int k = 0; k < CodexPerPage && start + k < list.Count; k++)
            {
                var e = list[start + k];
                var info = EnemyInfo(e);
                bool known = info[0] > 0;
                int x = CxEnemyX(k % CxEnemyCols), y = CxEnemyY(k / CxEnemyCols);
                c.Fill(x + 2, y + 2, CxEnemyW, CxEnemyH, Rgb.Hex(0x2a1a10));
                c.Fill(x, y, CxEnemyW, CxEnemyH, known ? Rgb.Hex(0xd8d2bc) : Rgb.Hex(0x9a9486));
                c.Rect(x, y, CxEnemyW, CxEnemyH, e.IsBoss ? Palette.DarkRed : (e.IsElite ? Rgb.Hex(0xa02020) : Palette.Pencil));

                // miniatura (sem a base), ou a silhueta se nunca apareceu
                var sprite = e.Def();
                var rows = NoBase(EnemySpriteRows(sprite));
                int sw = rows[0].Length * 2, sh = rows.Length * 2;
                int sx = x + CxEnemyW / 2 - sw / 2, sy = y + 46 - sh;
                c.FillEllipse(x + CxEnemyW / 2, y + 46, rows[0].Length, 3, Rgb.Hex(0x7a7260));
                if (known) c.SpriteOutlined(rows, sx, sy, ch => EnemyPal(sprite, ch), true, 2, Rgb.Hex(0x2a2420));
                else c.Sprite(rows, sx, sy, ch => ch == '.' ? (Rgb?)null : Rgb.Hex(0x2a2630), true, 2);

                int ty = y + 50;
                PixelFont.Small.DrawCentered(c, known ? e.Name : "???", x + CxEnemyW / 2, ty, Palette.Ink);
                if (!known) continue;
                string stats = "HP " + e.MaxHp + "  AC " + e.ArmorClass;
                if (e.IsBoss) stats += "  BOSS";
                else if (e.IsElite) stats += "  ELITE";
                PixelFont.Small.DrawCentered(c, stats, x + CxEnemyW / 2, ty + 8, Palette.DarkRed);
                PixelFont.Small.DrawWrapped(c, EnemyNote(e), x + 4, ty + 17, CxEnemyW - 8, Palette.Pencil, true);
                string kills = "KILLED " + info[1];
                PixelFont.Small.Draw(c, kills, x + CxEnemyW - 4 - PixelFont.Small.Measure(kills), y + 3, info[1] > 0 ? Palette.Ink : Palette.Pencil);
            }
            c.ResetTint();
        }
    }

    static class CodexEnemyExt
    {
        public static EnemySprite Def(this EnemyDef e) { return e.Sprite; }
    }
}
