using System;
using System.Collections.Generic;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>
    /// A historia escondida entre as runs:
    ///  - o caderno de sessao do Rafa (paginas liberadas por sessoes, mortes, vitorias e eventos);
    ///  - o estagio da historia, que muda objetos do porao (2D aqui e no Basement.cs, 3D no BasementStory.cs);
    ///  - momentos raros em que o jogo "quebra" (so visuais, nunca mudam as regras).
    /// Os ganchos nos outros arquivos sao linhas curtas que chamam funcoes daqui.
    /// </summary>
    public partial class GameApp
    {
        // ---------------- Estado salvo ----------------
        const int FlagElite = 1, FlagBoss = 2, FlagGlitch = 4;

        /// <summary>Chaves do PlayerPrefs usadas pela historia (sem o prefixo "deckdark_").</summary>
        public static readonly string[] StoryKeys = { "story_flags", "notes_unlocked", "notes_seen", "story_4th" };

        int storyFlags;      // eventos que ja aconteceram (elite, chefe, glitch)
        int notesUnlocked;   // bit por pagina liberada
        int notesSeen;       // bit por pagina ja lida
        int fourthWallIndex; // quantas falas "para voce" ja foram ditas

        // ---------------- Paginas do caderno ----------------
        // Hand: 0 = lapis do Rafa, 1 = caneta vermelha do Rafa, 2 = letra de outra crianca
        class NotePage
        {
            public string Title;
            public string Text;
            public string Later;              // anotacao feita depois, com outra caneta
            public int Hand;
            public Func<GameApp, bool> Unlock;
        }

        static readonly NotePage[] NotePages =
        {
            new NotePage
            {
                Title = "SESSION 1", Hand = 0, Unlock = g => g.sessions >= 1,
                Text = "SATURDAY. HE CAME OVER AT 5. HE PICKED THE WARRIOR LIKE I KNEW HE WOULD.\nMOM MADE CHIPS.\nI FOUND A MASK IN ONE OF DAD'S BOXES. IT FITS REALLY GOOD. LIKE SOMEBODY MEASURED MY FACE.",
            },
            new NotePage
            {
                Title = "HOUSE RULES", Hand = 0, Unlock = g => g.deaths >= 1,
                Text = "1. THE MASTER IS ALWAYS RIGHT.\n2. WHEN YOUR CHARACTER DIES YOU CRUMPLE THE SHEET.\n3. NOBODY TOUCHES THE MASTER'S DICE.",
                Later = "I KEEP THE SHEETS. DON'T TELL HIM I KEEP THE SHEETS.",
            },
            new NotePage
            {
                Title = "THE CAMPAIGN", Hand = 0, Unlock = g => g.sessions >= 3,
                Text = "THIS WAS MARCOS'S CAMPAIGN. MY BROTHER. SAME DUNGEON, SAME KING. HE MADE IT WHEN HE WAS 14.\nHE SAID THE FACELESS KING NEEDS A NEW FACE EVERY SO OFTEN OR HE GETS HUNGRY.\nHE WAS JOKING.",
            },
            new NotePage
            {
                Title = "THE RED DIE", Hand = 0, Unlock = g => (g.storyFlags & FlagElite) != 0 || g.deaths >= 3,
                Text = "THE RED D20 WAS MARCOS'S. I ROLLED IT 40 TIMES TO TEST IT.\nIT ONLY ROLLS WHAT THE MASK WANTS.\nHE ASKED TO USE IT TODAY. I SAID NO. I SAID IT TOO LOUD.",
            },
            new NotePage
            {
                Title = "MOM", Hand = 0, Unlock = g => g.deaths >= 3 && g.sessions >= 4,
                Text = "MOM ASKED WHY HE KEEPS COMING OVER EVERY SATURDAY.\nI SAID HE LIKES THE GAME.\nSHE SAID WHAT GAME.\nSHE SAID IT LIKE SHE WAS SCARED OF THE ANSWER.",
            },
            new NotePage
            {
                Title = "BOSS STATS", Hand = 0, Unlock = g => (g.storyFlags & FlagBoss) != 0,
                Text = "THE FACELESS KING\nHP 60   AC 14\nMOVES: CROWN STRIKE, CRUSH, NO FACE\nHE HAS NO FACE.",
                Later = "YET.",
            },
            new NotePage
            {
                Title = "HE WON", Hand = 1, Unlock = g => g.wins >= 1,
                Text = "HE BEAT THE KING. HE WASN'T SUPPOSED TO.\nI HAD TO WRITE NEW RULES REALLY FAST.\nTHE MASK WAS WARM ALL NIGHT. I DIDN'T TAKE IT OFF.\nI DON'T THINK I CAN.",
            },
            new NotePage
            {
                Title = "1989", Hand = 0, Unlock = g => g.deaths >= 5,
                Text = "MARCOS WENT DOWN TO THE BASEMENT TO FINISH A SESSION IN 1989. HE PLAYED ALONE THAT NIGHT. OR HE SAID HE DID.\nDAD TAPED THE BOX SHUT AFTER.\nTHE POLICE SAID HE RAN AWAY.",
                Later = "THE POLICE NEVER PLAYED.",
            },
            new NotePage
            {
                Title = "THE OTHER SHEET", Hand = 1, Unlock = g => g.sessions >= 8,
                Text = "THERE IS ANOTHER CHARACTER SHEET IN THE DRAWER. I DIDN'T WRITE IT.\nIT HAS HIS NAME ON IT. NOT HIS WARRIOR'S NAME. HIS REAL NAME.\nHP 0/0.\nIT HAS HIS HANDWRITING.",
            },
            new NotePage
            {
                Title = "3:17", Hand = 1, Unlock = g => (g.storyFlags & FlagGlitch) != 0 && g.sessions >= 5,
                Text = "THE CLOCK STOPPED AT 3:17 AGAIN. THAT'S WHEN MARCOS'S LAST SESSION ENDED.\nSOMETHING IS WATCHING THE GAME FROM HIS SIDE OF THE TABLE.\nNOT HIM.\nBEHIND HIM.",
            },
            new NotePage
            {
                Title = "TO YOU", Hand = 1, Unlock = g => g.deaths >= 9 || g.wins >= 2,
                Text = "IF YOU ARE READING THIS YOU ARE NOT HIM. HE CAN'T SEE MY NOTES FROM HIS CHAIR.\nYOU ARE THE ONE MOVING HIS HANDS.\nPLEASE STOP COMING BACK. EVERY TIME YOU COME BACK HE STAYS ONE MORE NIGHT.",
            },
            new NotePage
            {
                Title = "", Hand = 2, Unlock = g => g.deaths >= 12 || g.wins >= 3,
                Text = "RAFA IF YOU FIND THIS I'M STILL DOWN HERE.\nTHE LIGHT NEVER GOES OFF. I CAN'T FIND THE STAIRS.\nI CAN HEAR YOU PLAYING. WHO IS SITTING IN MY CHAIR\n\n- M.",
            },
        };

        // ---------------- Estagio (usado pelo cenario) ----------------

        /// <summary>Quantas paginas ja foram liberadas (0..12). O porao muda conforme isso cresce.</summary>
        public int StoryStage
        {
            get
            {
                int n = 0;
                for (int m = notesUnlocked; m != 0; m &= m - 1) n++;
                return n;
            }
        }

        /// <summary>A lampada fica um pouco mais fraca a cada pagina.</summary>
        public float StoryLampFactor { get { return Math.Max(0.72f, 1f - StoryStage * 0.025f); } }

        /// <summary>O desenho na parede mostra o personagem que o jogador escolheu.</summary>
        public bool StoryWizard { get { return run != null ? run.Sheet.Class == PlayerClass.Wizard : selectedClass == PlayerClass.Wizard; } }

        bool StoryClockStopped { get { return StoryStage >= 7; } }

        void LoadStory()
        {
            storyFlags = host.LoadInt("story_flags", 0);
            notesUnlocked = host.LoadInt("notes_unlocked", 0);
            notesSeen = host.LoadInt("notes_seen", 0);
            fourthWallIndex = host.LoadInt("story_4th", 0);
            StoryRefresh();
        }

        /// <summary>Apaga a historia. Chamado antes do LoadProgress, que recarrega tudo.</summary>
        void ResetStory()
        {
            foreach (var k in StoryKeys) host.DeleteKey(k);
            storyFlags = notesUnlocked = notesSeen = fourthWallIndex = 0;
        }

        void SetStoryFlag(int f)
        {
            if ((storyFlags & f) != 0) return;
            storyFlags |= f;
            host.SaveInt("story_flags", storyFlags);
        }

        /// <summary>Libera as paginas cujas condicoes ja foram cumpridas. So roda entre as runs.</summary>
        void StoryRefresh()
        {
            int m = notesUnlocked;
            for (int i = 0; i < NotePages.Length; i++)
                if ((m & (1 << i)) == 0 && NotePages[i].Unlock(this)) m |= 1 << i;
            if (m != notesUnlocked)
            {
                notesUnlocked = m;
                host.SaveInt("notes_unlocked", m);
            }
        }

        /// <summary>Gancho no fim de EndRunToMenu: se uma pagina nova apareceu, o caderno abre sozinho.</summary>
        void StoryAfterRun()
        {
            int before = notesUnlocked;
            StoryRefresh();
            if (notesUnlocked != before) BuildStaticScene();
            int fresh = notesUnlocked & ~notesSeen;
            if (fresh == 0) return;
            for (int i = 0; i < NotePages.Length; i++)
                if ((fresh & (1 << i)) != 0) { OpenNotes(i, true); return; }
        }

        // =====================================================================
        // Tela do caderno
        // =====================================================================

        const int NotesX = 104, NotesY = 26, NotesW = 272, NotesH = 216;
        const int NotesBtnY = NotesY + NotesH - 24;
        const int NotesMenuBtnW = 84, NotesMenuBtnX = MenuBtnX - 16 - NotesMenuBtnW;   // a esquerda, para nao cobrir o "RUN IN PROGRESS"

        int notesPage;           // indice em NotePages
        int notesFresh;          // paginas que ainda nao tinham sido lidas quando o caderno abriu

        List<int> UnlockedPages()
        {
            var l = new List<int>();
            for (int i = 0; i < NotePages.Length; i++) if ((notesUnlocked & (1 << i)) != 0) l.Add(i);
            return l;
        }

        void OpenNotes(int page, bool found)
        {
            notesPage = page;
            notesFresh = notesUnlocked & ~notesSeen;
            screen = GameScreen.Notes;
            Sfx(found ? "pencil" : "card", 0.7f);
        }

        void CloseNotes()
        {
            Sfx("click", 0.5f);
            screen = GameScreen.Menu;
        }

        /// <summary>Gancho no ClickMenu. Devolve true se o clique foi no botao do caderno.</summary>
        bool ClickNotesButton()
        {
            if (notesUnlocked == 0 || !Hover(NotesMenuBtnX, MenuBtnY(0), NotesMenuBtnW, MenuBtnH)) return false;
            var pages = UnlockedPages();
            int first = pages[0];
            foreach (int p in pages) if ((notesSeen & (1 << p)) == 0) { first = p; break; }
            OpenNotes(first, false);
            return true;
        }

        void DrawNotesButton(PixelCanvas c)
        {
            if (notesUnlocked == 0) return;
            DrawButton(c, NotesMenuBtnX, MenuBtnY(0), NotesMenuBtnW, MenuBtnH, "NOTEBOOK");
            if ((notesUnlocked & ~notesSeen) != 0 && ((int)(time * 2) & 1) == 0)
            {
                c.FillCircle(NotesMenuBtnX + NotesMenuBtnW - 1, MenuBtnY(0), 3, Palette.Red);
            }
        }

        void ClickNotes()
        {
            var pages = UnlockedPages();
            int k = Math.Max(0, pages.IndexOf(notesPage));
            if (Hover(NotesX + 18, NotesBtnY, 62, 16) && k > 0) { notesPage = pages[k - 1]; Sfx("card", 0.5f); }
            else if (Hover(NotesX + NotesW - 72, NotesBtnY, 62, 16) && k < pages.Count - 1) { notesPage = pages[k + 1]; Sfx("card", 0.5f); }
            else if (Hover(NotesX + NotesW / 2 - 28, NotesBtnY, 56, 16)) CloseNotes();
        }

        void DrawNotes(PixelCanvas c)
        {
            c.ResetTint();
            c.FillAlpha(0, 0, W, H, Palette.Black, 0.55f);
            var pages = UnlockedPages();
            if (pages.Count == 0) { screen = GameScreen.Menu; return; }
            if (!pages.Contains(notesPage)) notesPage = pages[0];
            var page = NotePages[notesPage];
            if ((notesSeen & (1 << notesPage)) == 0)
            {
                notesSeen |= 1 << notesPage;
                host.SaveInt("notes_seen", notesSeen);
            }

            int x = NotesX, y = NotesY, w = NotesW, h = NotesH;
            // capa escura por tras, como um caderno aberto
            c.Fill(x - 4, y - 3, w + 8, h + 7, Rgb.Hex(0x1e2a44));
            c.Fill(x + 3, y + 3, w, h, Palette.Black);
            var paper = page.Hand == 2 ? Rgb.Hex(0xd8d0b4) : Rgb.Hex(0xe8e0c8);
            c.Fill(x, y, w, h, paper);
            for (int ly = y + 30; ly < y + h - 28; ly += 11) c.HLine(x + 2, x + w - 3, ly + 9, Rgb.Hex(0xc8d4dc));
            c.VLine(x + 16, y, y + h - 1, Rgb.Hex(0xe0a0a0));
            for (int i = 0; i < h / 14; i++) c.FillCircle(x + 6, y + 8 + i * 14, 2, Rgb.Hex(0x1e1a1c));

            // a pagina de outra crianca e amassada e manchada
            if (page.Hand == 2)
            {
                var rnd = new Random(317);
                for (int i = 0; i < 6; i++)
                {
                    int sx = x + 20 + rnd.Next(w - 40), sy = y + 20 + rnd.Next(h - 60);
                    c.Line(sx, sy, sx + rnd.Next(30) - 15, sy + rnd.Next(30) - 15, Rgb.Hex(0xb8ae94));
                }
                for (int i = 0; i < 90; i++) c.Set(x + w - 40 + rnd.Next(30), y + 16 + rnd.Next(20), Rgb.Hex(0x6a3a30));
            }

            PixelFont.Small.Draw(c, "SESSION NOTES", x + 22, y + 5, Palette.Pencil);
            PixelFont.Small.Draw(c, "PG " + (notesPage + 1), x + w - 30, y + 5, Palette.Pencil);

            Rgb ink;
            switch (page.Hand)
            {
                case 1: ink = Rgb.Hex(0x8a1e22); break;
                case 2: ink = Rgb.Hex(0x2a4a8a); break;
                default: ink = Palette.Ink; break;
            }
            int ty = y + 16;
            if (page.Title.Length > 0)
            {
                PixelFont.Big.Draw(c, page.Title, x + 22, ty, ink);
                int tw = PixelFont.Big.Measure(page.Title);
                c.HLine(x + 22, x + 22 + tw, ty + 9, ink);
            }
            ty += 15;
            // a letra da outra crianca treme um pouco
            int jx = page.Hand == 2 && fxRng.NextDouble() < 0.1 ? 1 : 0;
            int used = PixelFont.Big.DrawWrapped(c, page.Text, x + 22 + jx, ty, w - 34, ink);
            if (page.Later != null)
            {
                int ly = ty + used + 4;
                PixelFont.Big.DrawWrapped(c, page.Later, x + 30, ly, w - 42, Rgb.Hex(0xb02020));
            }

            if ((notesFresh & (1 << notesPage)) != 0)
            {
                // carimbo "pagina nova" no canto
                c.Rect(x + w - 70, y + 16, 60, 13, Palette.Red);
                PixelFont.Small.DrawCentered(c, "NEW PAGE", x + w - 40, y + 20, Palette.Red);
            }

            int k = pages.IndexOf(notesPage);
            string count = (k + 1) + " / " + pages.Count;
            if (pages.Count < NotePages.Length) count += "   SOME PAGES ARE TORN OUT";
            PixelFont.Small.DrawCentered(c, count, x + w / 2 + 6, NotesBtnY - 11, Palette.Pencil);

            if (k > 0) DrawButton(c, x + 18, NotesBtnY, 62, 16, "< PREV");
            if (k < pages.Count - 1) DrawButton(c, x + w - 72, NotesBtnY, 62, 16, "NEXT >");
            DrawButton(c, x + w / 2 - 28, NotesBtnY, 56, 16, "CLOSE");
        }

        // =====================================================================
        // Objetos do porao 2D (a versao 3D fica em Test3D/BasementStory.cs)
        // =====================================================================

        /// <summary>Gancho no BuildStaticScene: foto na parede e o desenho sobre o poster.</summary>
        void DrawStoryProps(PixelCanvas c)
        {
            int stage = StoryStage;
            if (stage >= 2)
            {
                // foto dos dois irmaos, pendurada abaixo da janela
                int fx = 126, fy = 38, fw = 20, fh = 16;
                c.Fill(fx, fy, fw, fh, Rgb.Hex(0x3a2414));
                c.Fill(fx + 2, fy + 2, fw - 4, fh - 4, Rgb.Hex(0xb09a78));
                c.Fill(fx + 2, fy + 10, fw - 4, fh - 12, Rgb.Hex(0x8a7458));
                // Rafa (menor) e Marcos (maior)
                c.FillCircle(fx + 7, fy + 7, 2, Rgb.Hex(0xd0a880));
                c.Fill(fx + 5, fy + 9, 5, 4, Rgb.Hex(0x5a3a3a));
                c.FillCircle(fx + 13, fy + 6, 2, Rgb.Hex(0xd0a880));
                c.Fill(fx + 11, fy + 8, 5, 5, Rgb.Hex(0x3a4a5a));
                if (stage >= 4)
                {
                    // o rosto do irmao foi riscado
                    c.Line(fx + 11, fy + 4, fx + 15, fy + 8, Rgb.Hex(0xf0e8d8));
                    c.Line(fx + 15, fy + 4, fx + 11, fy + 8, Rgb.Hex(0xf0e8d8));
                    c.Line(fx + 11, fy + 5, fx + 15, fy + 6, Rgb.Hex(0xf0e8d8));
                }
                if (stage >= 9)
                {
                    // uma terceira pessoa no fundo da foto, com o rosto branco
                    c.Fill(fx + 3, fy + 5, 3, 7, Rgb.Hex(0x2a2020));
                    c.Set(fx + 4, fy + 4, Rgb.Hex(0xf4f0e8));
                    c.Set(fx + 4, fy + 3, Rgb.Hex(0xf4f0e8));
                }
                c.Set(fx + fw / 2, fy - 1, Rgb.Hex(0x1a1a1a)); // prego
            }
            if (stage >= 5)
            {
                // desenho de giz de cera colado por cima do poster de discoteca
                int px = 13, py = 17, pw = 42, ph = 56;
                c.Fill(px, py, pw, ph, Rgb.Hex(0xe4e0d4));
                c.Rect(px, py, pw, ph, Rgb.Hex(0xb0a890));
                c.Fill(px + 17, py - 2, 8, 4, Rgb.Hex(0xb0a890)); // fita
                var blue = Rgb.Hex(0x3a5aa8);
                int cx = px + 14, cy = py + 18;
                c.Circle(cx, cy, 4, blue);
                c.Line(cx, cy + 4, cx, cy + 18, blue);
                c.Line(cx, cy + 8, cx - 6, cy + 13, blue);
                c.Line(cx, cy + 8, cx + 6, cy + 11, blue);
                c.Line(cx, cy + 18, cx - 5, cy + 26, blue);
                c.Line(cx, cy + 18, cx + 5, cy + 26, blue);
                if (StoryWizard)
                {
                    c.FillTriangle(cx - 5, cy - 4, cx + 5, cy - 4, cx + 1, cy - 13, Rgb.Hex(0x7a3aa8));
                    c.Line(cx + 6, cy + 11, cx + 8, cy - 2, Rgb.Hex(0x8a5a2a));
                }
                else
                {
                    c.Line(cx + 6, cy + 11, cx + 12, cy + 1, Rgb.Hex(0x909098));
                    c.Line(cx + 4, cy + 9, cx + 8, cy + 12, Rgb.Hex(0x909098));
                }
                // a figura alta de mascara atras dele
                var black = Rgb.Hex(0x1a1418);
                int mx = px + 30, my = py + 10;
                c.Fill(mx - 4, my + 6, 9, 32, black);
                c.FillEllipse(mx, my + 2, 4, 5, Rgb.Hex(0xf4f0e8));
                c.Set(mx - 2, my + 1, black); c.Set(mx + 2, my + 1, black);
                c.HLine(mx - 1, mx + 1, my + 4, Palette.Red);
                c.Line(mx - 4, my + 12, cx + 4, cy + 4, black); // a mao no ombro dele
                PixelFont.Small.DrawCentered(c, "YOU", cx, py + ph - 9, Rgb.Hex(0xa82828));
            }
        }

        // =====================================================================
        // Momentos em que o jogo "quebra"
        // =====================================================================

        enum Glitch { None, CardName, PhantomRoom, FourthWall, Mimic }

        readonly Random storyRng = new Random(Environment.TickCount ^ 0x317);
        RunState storyRun;
        Glitch plannedGlitch, activeGlitch;
        int plannedLayer;
        float glitchT, glitchIdle, phantomHover;
        CardDef glitchCard;
        string glitchCardText;
        int glitchEnemySlot;
        int phantomLayer, phantomDx, phantomY;

        static readonly string[] GlitchCardNames = { "DON'T", "NOT YOURS", "GO HOME", "HIS SWORD", "STOP", "HELP ME", "YOU" };

        /// <summary>Sorteia no inicio de cada run se (e qual) quebra vai acontecer. No maximo uma por run.</summary>
        void StoryNewRun()
        {
            storyRun = run;
            plannedGlitch = Glitch.None;
            activeGlitch = Glitch.None;
            glitchT = 0;
            if (run == null) return;
            int stage = StoryStage;
            if (stage < 3) return;
            if (storyRng.NextDouble() > 0.3 + stage * 0.04) return;
            var options = new List<Glitch> { Glitch.CardName };
            if (stage >= 4) options.Add(Glitch.PhantomRoom);
            if (stage >= 5) options.Add(Glitch.FourthWall);
            if (stage >= 6) options.Add(Glitch.Mimic);
            plannedGlitch = options[storyRng.Next(options.Count)];
            plannedLayer = 1 + storyRng.Next(Math.Max(1, Math.Min(5, run.LastLayer - 1)));
            glitchIdle = 0;
        }

        /// <summary>Gancho no Update (fora da pausa).</summary>
        void StoryUpdate(float dt)
        {
            if (run != storyRun) StoryNewRun();
            if (run == null) return;

            if (screen == GameScreen.Combat)
            {
                if (combatNode == NodeType.Elite) SetStoryFlag(FlagElite);
                if (combatNode == NodeType.Boss) SetStoryFlag(FlagBoss);
            }

            if (activeGlitch != Glitch.None)
            {
                glitchT -= dt;
                bool gone = glitchT <= 0;
                if (activeGlitch == Glitch.PhantomRoom && (screen != GameScreen.Map || run.Current.Layer + 1 != phantomLayer)) gone = true;
                if (activeGlitch == Glitch.CardName && (combat == null || !combat.Hand.Contains(glitchCard))) gone = true;
                if (gone) activeGlitch = Glitch.None;
                return;
            }
            if (plannedGlitch == Glitch.None) return;
            if (scriptActive || Busy || selectedCard >= 0 || run.Current.Layer < plannedLayer) { glitchIdle = 0; return; }
            glitchIdle += dt;

            switch (plannedGlitch)
            {
                case Glitch.CardName:
                    if (screen != GameScreen.Combat || combat == null || combat.Hand.Count == 0 || glitchIdle < 2.5f) return;
                    glitchCard = combat.Hand[storyRng.Next(combat.Hand.Count)];
                    glitchCardText = GlitchCardNames[storyRng.Next(GlitchCardNames.Length)];
                    StartGlitch(2.4f);
                    Sfx("pencil", 0.5f);
                    break;

                case Glitch.Mimic:
                    if (screen != GameScreen.Combat || combat == null || glitchIdle < 1.8f) return;
                    glitchEnemySlot = storyRng.Next(8);
                    StartGlitch(0.45f);
                    Sfx("flicker", 0.6f);
                    lampDip = 0.7f;
                    break;

                case Glitch.PhantomRoom:
                    if (screen != GameScreen.Map || glitchIdle < 1.2f || !PlacePhantomRoom()) return;
                    phantomHover = 0;
                    StartGlitch(9f);
                    break;

                case Glitch.FourthWall:
                    if (screen != GameScreen.Map || speech != null || glitchIdle < 2.5f) return;
                    Say(FourthWallLine());
                    StartGlitch(0.1f);
                    lampDip = 0.9f;
                    break;
            }
        }

        void StartGlitch(float duration)
        {
            activeGlitch = plannedGlitch;
            plannedGlitch = Glitch.None;
            glitchT = duration;
            SetStoryFlag(FlagGlitch);
        }

        string FourthWallLine()
        {
            int hour = DateTime.Now.Hour;
            if (hour < 5 && (fourthWallIndex & 64) == 0)
            {
                fourthWallIndex |= 64;
                host.SaveInt("story_4th", fourthWallIndex);
                return "IT'S LATE WHERE YOU ARE TOO. ISN'T IT?";
            }
            string[] lines =
            {
                "NOT HIM. YOU. YOU CAN STOP, YOU KNOW.",
                "HE CAN'T SEE YOU. I CAN.",
                "YOU OPENED THIS " + sessions + " TIMES. THAT'S " + sessions + " SATURDAYS FOR HIM.",
                "WHEN YOU CLOSE THE GAME, THE LIGHT STAYS ON DOWN HERE.",
                "WHO IS SITTING BEHIND YOU RIGHT NOW?",
            };
            int i = fourthWallIndex & 63;
            string l = lines[i % lines.Length];
            fourthWallIndex = (fourthWallIndex & 64) | ((i + 1) & 63);
            host.SaveInt("story_4th", fourthWallIndex);
            return l;
        }

        /// <summary>Escolhe um lugar vazio no mapa, na proxima camada, para a sala que nao existe.</summary>
        bool PlacePhantomRoom()
        {
            int layer = run.Current.Layer + 1;
            if (layer > run.LastLayer || !NodeOnPaper(layer)) return false;
            int bestY = -1, bestDx = 0, bestDist = 0;
            for (int dx = -18; dx <= 18; dx += 6)
                for (int y = 148; y <= 242; y += 2)
                {
                    int px = NodeX(layer) + dx;
                    int d2 = int.MaxValue;
                    foreach (var l in run.Layers)
                        foreach (var n in l)
                        {
                            int ex = NodeX(n.Layer) - px, ey = NodeY(n.Layer, n.Index, l.Count) - y;
                            d2 = Math.Min(d2, ex * ex + ey * ey);
                        }
                    if (d2 > bestDist) { bestDist = d2; bestY = y; bestDx = dx; }
                }
            if (bestDist < 22 * 22) return false;
            phantomLayer = layer;
            phantomDx = bestDx;
            phantomY = bestY;
            return true;
        }

        /// <summary>Gancho no DrawCard: o nome da carta aparece riscado e reescrito por um instante.</summary>
        void StoryCardOverlay(PixelCanvas c, CardDef card, int x, int y)
        {
            if (activeGlitch != Glitch.CardName || card != glitchCard || screen != GameScreen.Combat) return;
            c.Fill(x + 2, y + 12, CardW - 4, 14, Palette.Paper);
            string name = card.Name;
            while (name.Length > 1 && PixelFont.Small.Measure(name) > CardW - 6) name = name.Substring(0, name.Length - 1);
            PixelFont.Small.Draw(c, name, x + 3, y + 13, Palette.Pencil);
            int nw = PixelFont.Small.Measure(name);
            c.HLine(x + 2, x + 4 + nw, y + 15, Palette.DarkRed);
            c.HLine(x + 2, x + 4 + nw, y + 16, Palette.DarkRed);
            int j = fxRng.NextDouble() < 0.15 ? 1 : 0;
            PixelFont.Small.DrawCentered(c, glitchCardText, x + CardW / 2 + j, y + 19, Rgb.Hex(0xb02020));
        }

        /// <summary>Gancho no fim do CollectMinis: por um instante um inimigo vira a sua miniatura.</summary>
        void StoryMinis()
        {
            if (activeGlitch != Glitch.Mimic || Minis.Count < 2) return;
            var me = Minis[0];
            var other = Minis[1 + glitchEnemySlot % (Minis.Count - 1)];
            if (other.Lying) return;
            other.Key = me.Key;
            other.Rows = me.Rows;
            other.Pal = me.Pal;
            other.Flash = false;
        }

        /// <summary>Gancho no Render, depois da tela atual: sala fantasma no mapa e a miniatura trocada no 2D.</summary>
        void DrawStoryOverlay(PixelCanvas c)
        {
            if (activeGlitch == Glitch.PhantomRoom && screen == GameScreen.Map && !paused) DrawPhantomRoom(c);
            if (activeGlitch == Glitch.Mimic && screen == GameScreen.Combat && !Use3D && combat != null) DrawMimic2D(c);
        }

        void DrawPhantomRoom(PixelCanvas c)
        {
            if (Busy || !NodeOnPaper(phantomLayer)) return;
            TintAt(255, 190, 0.7f);
            int nx = NodeX(phantomLayer) + phantomDx, ny = phantomY;
            int x0 = NodeX(run.Current.Layer);
            int y0 = NodeY(run.Current.Layer, run.Current.Index, run.Layers[run.Current.Layer].Count);
            c.DashedLine(x0, y0, nx, ny, Palette.DarkRed, 2, 4);
            // a sala e desenhada meio tremida, como se alguem tivesse acabado de desenhar
            int j = fxRng.NextDouble() < 0.2 ? fxRng.Next(-1, 2) : 0;
            c.FillCircle(nx + j, ny, 9, Rgb.Hex(0xd8d0bc));
            c.Circle(nx + j, ny, 9, Palette.DarkRed);
            c.Sprite(Sprites.IconDoor, nx - 4 + j, ny - 4, ch => ch == 'a' ? Rgb.Hex(0x2a1a1a) : (ch == 'b' ? Palette.Black : (Rgb?)Palette.DarkRed));

            int dx = input.X - nx, dy = input.Y - ny;
            if (dx * dx + dy * dy <= 12 * 12)
            {
                string label = "THE STAIRS";
                int lw = PixelFont.Small.Measure(label) + 6;
                c.ResetTint();
                c.Fill(nx - lw / 2, ny - 24, lw, 9, Palette.DarkRed);
                PixelFont.Small.Draw(c, label, nx - lw / 2 + 3, ny - 22, Palette.Paper);
                phantomHover += 1f / 60f;
                if (phantomHover > 0.35f)
                {
                    // chegou perto demais: a sala some
                    glitchT = 0;
                    activeGlitch = Glitch.None;
                    lampDip = 1f;
                    Sfx("flicker", 0.7f);
                }
            }
            c.ResetTint();
        }

        void DrawMimic2D(PixelCanvas c)
        {
            var shown = ShownEnemies();
            if (shown.Count == 0) return;
            int i = shown[glitchEnemySlot % shown.Count];
            var v = enemyViews[i];
            if (v.Dead || v.Fled) return;
            var spr = EnemySpriteRows(combat.Enemies[i].Def.Sprite);
            int ex = EnemyX(i);
            int sw = spr[0].Length, sh = spr.Length;
            TintAt(ex, MiniFeetY - 10, 0.6f);
            // cobre o inimigo com o papel do tabuleiro e desenha voce no lugar dele
            int top = Math.Max(MatY, MiniFeetY - sh * 2 - 2);
            c.Fill(ex - sw - 2, top, sw * 2 + 4, MiniFeetY - top, Rgb.Hex(0xd8d2bc));
            var me = PlayerSprite;
            c.SpriteOutlined(me, ex - me[0].Length, MiniFeetY - me.Length * 2 + 2, PlayerPal, true, 2, Rgb.Hex(0x2a2420));
            c.ResetTint();
        }
    }
}
