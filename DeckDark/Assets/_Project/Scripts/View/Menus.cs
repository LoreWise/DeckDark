using System;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>Menu inicial, escolha de Homebrew, opcoes e pausa.</summary>
    public partial class GameApp
    {
        // Etiqueta "ESC MENU" no canto da tela durante a partida
        const int MenuTagX = 4, MenuTagY = 4, MenuTagW = 50, MenuTagH = 11;

        // Botoes do menu inicial
        const int MenuBtnX = 180, MenuBtnW = 120, MenuBtnH = 17;
        static int MenuBtnY(int i) { return 188 + i * 22; }

        // Painel central (pausa, opcoes, homebrew)
        const int PanelX = 150, PanelY = 96, PanelW = 180, PanelH = 168;

        bool paused;
        bool optionsFromPause;
        bool resetArmed;

        // ---------------- Teclado ----------------

        void HandleEscape()
        {
            if (selectedCard >= 0 && !paused) { CancelTargeting(); return; }
            if (screen == GameScreen.Options || (paused && optionsFromPause))
            {
                CloseOptions();
                return;
            }
            if (screen == GameScreen.NewGame) { screen = GameScreen.Menu; Sfx("click", 0.5f); return; }
            if (screen == GameScreen.Notes) { CloseNotes(); return; }
            if (screen == GameScreen.Codex) { CloseCodex(); return; }
            if (screen == GameScreen.Menu) return;
            if (paused) { paused = false; Sfx("click", 0.5f); }
            else OpenPause();
        }

        void OpenPause()
        {
            paused = true;
            optionsFromPause = false;
            Sfx("click", 0.5f);
        }

        void OpenOptions(bool fromPause)
        {
            optionsFromPause = fromPause;
            resetArmed = false;
            if (!fromPause) screen = GameScreen.Options;
            Sfx("click", 0.5f);
        }

        void CloseOptions()
        {
            Sfx("click", 0.5f);
            if (optionsFromPause) { optionsFromPause = false; return; }
            screen = GameScreen.Menu;
        }

        // ---------------- Menu inicial ----------------

        void ClickMenu()
        {
            if (ClickNotesButton()) return;
            // botoes montados em SaveHooks.MenuEntries (CONTINUE, NEW GAME, OPTIONS, CODEX, QUIT)
            ClickMenuEntries();
        }

        void DrawMenu(PixelCanvas c, bool buttons = true)
        {
            c.ResetTint();
            c.FillAlpha(0, 118, W, 152, Palette.Black, 0.6f);
            int y = 128;
            PixelFont.Big.DrawCentered(c, Title, 242, y + 2, Palette.DarkRed, 4);
            PixelFont.Big.DrawCentered(c, Title, 240, y, Palette.Paper, 4);
            PixelFont.Small.DrawCentered(c, "A CARD GAME IN A BASEMENT. SATURDAY, 1991.", 240, y + 42, Palette.PaperDark);

            if (!buttons) return;
            DrawMenuEntries(c);
            DrawNotesButton(c);

            string stats = "SESSIONS " + sessions + "   DEATHS " + deaths + "   WINS " + wins;
            PixelFont.Small.DrawCentered(c, stats, 240, 258, Palette.Pencil);
            PixelFont.Small.Draw(c, "PROTOTYPE 0.2", 6, 260, Palette.Pencil);
        }

        // ---------------- Escolha de Homebrew ----------------

        static int RuleRowY(int i) { return PanelY + 24 + i * 23; }
        const int NewGameBtnY = PanelY + PanelH - 22;

        // escolha de classe: um cartaozinho a esquerda do caderno de regras
        const int ClassX = PanelX - 94, ClassY = PanelY + 10, ClassW = 86;
        static int ClassBtnY(int i) { return ClassY + 22 + i * 22; }

        void ClickNewGame()
        {
            for (int i = 0; i < 2; i++)
                if (Hover(ClassX + 8, ClassBtnY(i), ClassW - 16, 16))
                {
                    selectedClass = (PlayerClass)i;
                    host.SaveInt("class", i);
                    Sfx("pencil", 0.6f);
                    return;
                }
            for (int i = 0; i < Homebrew.MaxVersion; i++)
            {
                if (!Hover(PanelX + 4, RuleRowY(i) - 2, PanelW - 8, 22)) continue;
                if (i + 1 <= unlockedRules) { selectedRules = i + 1; Sfx("pencil", 0.6f); }
                else Sfx("miss", 0.5f);
                return;
            }
            if (Hover(PanelX + 18, NewGameBtnY, 64, 16)) { Sfx("click"); screen = GameScreen.Menu; }
            else if (Hover(PanelX + PanelW - 72, NewGameBtnY, 64, 16)) StartSession();
        }

        void DrawNewGame(PixelCanvas c)
        {
            c.ResetTint();
            c.FillAlpha(0, 0, W, H, Palette.Black, 0.35f);
            DrawNotebook(c, PanelX, PanelY, PanelW, PanelH, "HOMEBREW RULES");
            for (int i = 0; i < Homebrew.MaxVersion; i++)
            {
                int v = i + 1;
                int y = RuleRowY(i);
                bool unlocked = v <= unlockedRules;
                bool selected = v == selectedRules;
                bool hov = unlocked && Hover(PanelX + 4, y - 2, PanelW - 8, 22);
                if (hov && !selected) c.FillAlpha(PanelX + 14, y - 2, PanelW - 18, 21, Palette.Gold, 0.25f);
                if (selected)
                {
                    // circulo de lapis vermelho em volta da regra escolhida
                    c.Rect(PanelX + 13, y - 3, PanelW - 16, 23, Palette.Red);
                    c.Rect(PanelX + 14, y - 2, PanelW - 18, 21, Palette.Red);
                }
                string text = unlocked ? Homebrew.Texts[i] : "V" + v + ".0  ???  (WIN ON V" + (v - 1) + ".0 TO UNLOCK)";
                var col = unlocked ? (selected ? Palette.DarkRed : Palette.Ink) : Rgb.Hex(0xa8a090);
                PixelFont.Small.DrawWrapped(c, text, PanelX + 18, y, PanelW - 26, col);
            }
            // classe
            c.Fill(ClassX + 3, ClassY + 3, ClassW, 70, Palette.Black);
            c.Fill(ClassX, ClassY, ClassW, 70, Rgb.Hex(0xe8e0c8));
            c.Rect(ClassX, ClassY, ClassW, 70, Palette.PaperDark);
            PixelFont.Big.DrawCentered(c, "CLASS", ClassX + ClassW / 2, ClassY + 5, Palette.Ink);
            for (int i = 0; i < 2; i++)
            {
                DrawButton(c, ClassX + 8, ClassBtnY(i), ClassW - 16, 16, i == 0 ? "WARRIOR" : "WIZARD");
                if ((int)selectedClass == i)
                {
                    c.Rect(ClassX + 6, ClassBtnY(i) - 2, ClassW - 12, 20, Palette.Red);
                    c.Rect(ClassX + 5, ClassBtnY(i) - 3, ClassW - 10, 22, Palette.Red);
                }
            }
            DrawButton(c, PanelX + 18, NewGameBtnY, 64, 16, "BACK");
            DrawButton(c, PanelX + PanelW - 72, NewGameBtnY, 64, 16, "START");
        }

        void DrawNotebook(PixelCanvas c, int x, int y, int w, int h, string title)
        {
            c.Fill(x + 3, y + 3, w, h, Palette.Black);
            c.Fill(x, y, w, h, Rgb.Hex(0xe8e0c8));
            for (int ly = y + 20; ly < y + h; ly += 8) c.HLine(x + 2, x + w - 3, ly, Rgb.Hex(0xc8d4dc));
            c.VLine(x + 11, y, y + h - 1, Rgb.Hex(0xe0a0a0));
            for (int i = 0; i < h / 14; i++) c.FillCircle(x + 5, y + 8 + i * 14, 2, Rgb.Hex(0x2a2322));
            PixelFont.Big.DrawCentered(c, title, x + w / 2 + 4, y + 5, Palette.Ink);
        }

        // ---------------- Opcoes ----------------

        static int SpeedBtnX(int i) { return PanelX + 14 + i * 54; }
        const int SpeedBtnY = PanelY + 44, SpeedBtnW = 50, SpeedBtnH = 16;
        const int ResetBtnY = PanelY + 96;
        const int OptBackY = PanelY + PanelH - 24;

        void ClickOptions()
        {
            for (int i = 0; i < 3; i++)
            {
                if (Hover(SpeedBtnX(i), SpeedBtnY, SpeedBtnW, SpeedBtnH))
                {
                    speed = (GameSpeed)i;
                    host.SaveInt("speed", i);
                    Sfx("click");
                    return;
                }
            }
            if (!optionsFromPause && Hover(PanelX + 14, ResetBtnY, PanelW - 28, 16))
            {
                if (!resetArmed) { resetArmed = true; Sfx("curse", 0.4f); return; }
                ResetProgress();
                return;
            }
            if (Hover(PanelX + PanelW / 2 - 35, OptBackY, 70, 16)) CloseOptions();
        }

        void ResetProgress()
        {
            foreach (var k in new[] { "deaths", "wins", "sessions", "unlocked", "selected" }) host.DeleteKey(k);
            ResetStory();
            LoadProgress();
            resetArmed = false;
            BuildStaticScene();
            Sfx("death", 0.4f);
        }

        void DrawOptions(PixelCanvas c)
        {
            c.ResetTint();
            c.FillAlpha(0, 0, W, H, Palette.Black, 0.45f);
            DrawNotebook(c, PanelX, PanelY, PanelW, PanelH, "OPTIONS");

            PixelFont.Big.Draw(c, "GAME SPEED", PanelX + 16, PanelY + 28, Palette.Ink);
            string[] names = { "SLOW", "NORMAL", "FAST" };
            for (int i = 0; i < 3; i++)
            {
                bool sel = (int)speed == i;
                int x = SpeedBtnX(i);
                bool hov = Hover(x, SpeedBtnY, SpeedBtnW, SpeedBtnH);
                c.Fill(x, SpeedBtnY, SpeedBtnW, SpeedBtnH, sel ? Palette.Ink : (hov ? Rgb.Hex(0xf0d890) : Palette.Paper));
                c.Rect(x, SpeedBtnY, SpeedBtnW, SpeedBtnH, Palette.Ink);
                PixelFont.Small.DrawCentered(c, names[i], x + SpeedBtnW / 2, SpeedBtnY + 5, sel ? Palette.Gold : Palette.Ink);
            }
            PixelFont.Small.DrawWrapped(c, "HOW FAST DICE ROLL AND THE MASTER TALKS.", PanelX + 16, SpeedBtnY + 22, PanelW - 30, Palette.Pencil);

            if (!optionsFromPause)
            {
                int x = PanelX + 14, w = PanelW - 28;
                bool hov = Hover(x, ResetBtnY, w, 16);
                c.Fill(x, ResetBtnY, w, 16, resetArmed ? Palette.DarkRed : (hov ? Rgb.Hex(0xf0c0b0) : Palette.Paper));
                c.Rect(x, ResetBtnY, w, 16, Palette.DarkRed);
                PixelFont.Small.DrawCentered(c, resetArmed ? "CLICK AGAIN TO ERASE EVERYTHING" : "RESET PROGRESS", x + w / 2, ResetBtnY + 5, resetArmed ? Palette.White : Palette.DarkRed);
                PixelFont.Small.DrawWrapped(c, "DEATHS, WINS AND UNLOCKED HOMEBREW.", PanelX + 16, ResetBtnY + 22, PanelW - 30, Palette.Pencil);
            }

            DrawButton(c, PanelX + PanelW / 2 - 35, OptBackY, 70, 16, "BACK");
        }

        // ---------------- Pausa ----------------

        static int PauseBtnY(int i) { return PanelY + 34 + i * 26; }
        const int PauseBtnX = PanelX + 25, PauseBtnW = PanelW - 50;

        void ClickPause()
        {
            if (optionsFromPause) { ClickOptions(); return; }
            if (Hover(PauseBtnX, PauseBtnY(0), PauseBtnW, 17)) { paused = false; Sfx("click", 0.5f); }
            else if (Hover(PauseBtnX, PauseBtnY(1), PauseBtnW, 17)) OpenOptions(true);
            else if (Hover(PauseBtnX, PauseBtnY(2), PauseBtnW, 17)) { Sfx("click"); abandonArmed = false; EndRunToMenu(); }   // o save do mapa continua
            else if (Hover(PauseBtnX, PauseBtnY(3), PauseBtnW, 17)) { Sfx("click"); host.Quit(); }
            else if (Hover(PauseBtnX, PauseBtnY(4), PauseBtnW, 17)) ClickAbandon();   // SaveHooks.cs
        }

        void DrawPause(PixelCanvas c)
        {
            if (optionsFromPause) { DrawOptions(c); return; }
            c.ResetTint();
            c.FillAlpha(0, 0, W, H, Palette.Black, 0.55f);
            DrawNotebook(c, PanelX, PanelY, PanelW, 164, "PAUSED");
            string[] labels = { "RESUME", "OPTIONS", "SAVE & MENU", "QUIT GAME", abandonArmed ? "REALLY ABANDON?" : "ABANDON RUN" };
            for (int i = 0; i < labels.Length; i++) DrawButton(c, PauseBtnX, PauseBtnY(i), PauseBtnW, 17, labels[i]);
        }

        void DrawMenuTag(PixelCanvas c)
        {
            if (!InRun || paused) return;
            c.ResetTint();
            bool hov = Hover(MenuTagX, MenuTagY, MenuTagW, MenuTagH);
            c.FillAlpha(MenuTagX, MenuTagY, MenuTagW, MenuTagH, Palette.Black, hov ? 0.85f : 0.55f);
            c.Rect(MenuTagX, MenuTagY, MenuTagW, MenuTagH, hov ? Palette.Gold : Palette.Pencil);
            PixelFont.Small.Draw(c, "ESC  MENU", MenuTagX + 5, MenuTagY + 3, hov ? Palette.Gold : Palette.PaperDark);
        }
    }
}
