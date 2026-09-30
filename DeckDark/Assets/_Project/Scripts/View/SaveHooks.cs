using System;
using System.Collections.Generic;
using DeckDark.Core;

namespace DeckDark.View
{
    /// <summary>
    /// Salvar e continuar a run, e os botoes do menu inicial.
    /// A run e salva sozinha no mapa (entre salas). Sair do jogo no meio da sala mantem o save;
    /// morrer, vencer ou abandonar apaga. Comecar um jogo novo sobrescreve (com confirmacao no menu).
    /// O formato fica em Core/RunSave.cs.
    /// </summary>
    public partial class GameApp
    {
        const string RunSaveKey = "run";

        string runSaveText;          // copia do que esta salvo ("" = nada), para nao ler o host todo frame
        bool runSaveLoaded;
        RunState savedRun;           // a run a que o save pertence
        string savedSignature;       // o que ja foi salvo desta run (sala, vida, dados...)
        bool newGameArmed;           // primeiro clique em NEW GAME com save existente so avisa
        bool abandonArmed;

        string RunSaveText
        {
            get
            {
                if (!runSaveLoaded) { runSaveText = host.LoadString(RunSaveKey, ""); runSaveLoaded = true; }
                return runSaveText;
            }
        }

        public bool HasRunSave { get { return !string.IsNullOrEmpty(RunSaveText); } }

        void SaveRun()
        {
            var d = RunSave.ToData(run);
            // TODO(estado da tela): chaves do GameApp que precisem voltar ao continuar entram aqui,
            // ex.: d.Set("mask", maskOn); e sao lidas em ContinueRun.
            runSaveText = d.ToString();
            runSaveLoaded = true;
            host.SaveString(RunSaveKey, runSaveText);
            savedRun = run;
            savedSignature = RunSignature();
        }

        void DeleteRunSave()
        {
            if (runSaveLoaded && runSaveText == "") return;
            host.DeleteKey(RunSaveKey);
            runSaveText = "";
            runSaveLoaded = true;
            savedRun = null;
            savedSignature = null;
        }

        /// <summary>Muda sempre que algo salvavel muda; evita serializar a run a cada frame.</summary>
        string RunSignature()
        {
            var s = run.Sheet;
            // TODO(campos novos): se um campo novo mudar no mapa sem gastar dados (ex.: ouro na loja), inclua aqui.
            return run.Current.Layer + "." + run.Current.Index + "|" + s.Hp + "/" + s.MaxHp + "|" + s.Deck.Count + "|" + s.Relics.Count + "|" + run.Dice.Calls;
        }

        /// <summary>Chamado todo frame por GameApp.Update.</summary>
        void SaveHooksTick()
        {
            CodexTick();
            if (!paused) abandonArmed = false;
            if (run == null) return;
            switch (screen)
            {
                case GameScreen.Death:
                case GameScreen.Victory:
                    // a run acabou: nao da para continuar
                    if (HasRunSave) DeleteRunSave();
                    break;
                case GameScreen.Intro:
                    // uma run nova comecou: o save antigo foi sobrescrito
                    if (run != savedRun && HasRunSave) DeleteRunSave();
                    break;
                case GameScreen.Map:
                    // ponto seguro: sala terminada, proxima ainda nao escolhida.
                    // Busy = ja clicou numa sala e esta indo para ela; espera.
                    if (Busy) break;
                    if (run != savedRun || RunSignature() != savedSignature) SaveRun();
                    break;
            }
        }

        void ContinueRun()
        {
            var loaded = RunSave.Deserialize(RunSaveText);
            if (loaded == null)
            {
                // save quebrado ou de uma versao antiga
                DeleteRunSave();
                Sfx("miss", 0.6f);
                return;
            }
            Sfx("click");
            ClearSpeech();
            beats.Clear();
            dice = null;
            floaters.Clear();
            paused = false;
            combat = null;
            run = loaded;
            savedRun = run;
            savedSignature = RunSignature();
            selectedClass = run.Sheet.Class;
            BuildStaticScene();
            shownPlayerHp = run.Sheet.Hp;
            mapScroll = MapScrollTarget;
            screen = GameScreen.Map;
            string[] lines =
            {
                "WHERE WERE WE? OH, RIGHT. RIGHT HERE.",
                "YOU CAME BACK. I KEPT YOUR SHEET.",
                "I DIDN'T TOUCH ANYTHING. I PROMISE.",
            };
            Say(lines[fxRng.Next(lines.Length)]);
        }

        void ClickAbandon()
        {
            if (!abandonArmed) { abandonArmed = true; Sfx("curse", 0.4f); return; }
            abandonArmed = false;
            Sfx("click");
            DeleteRunSave();
            EndRunToMenu();
        }

        // ---------------- Botoes do menu inicial ----------------

        struct MenuEntry
        {
            public string Label;
            public Action Act;
        }

        List<MenuEntry> MenuEntries()
        {
            var list = new List<MenuEntry>();
            bool save = HasRunSave;
            if (save) list.Add(new MenuEntry { Label = "CONTINUE", Act = ContinueRun });
            list.Add(new MenuEntry
            {
                Label = save && newGameArmed ? "OVERWRITE RUN?" : "NEW GAME",
                Act = () =>
                {
                    // com uma run salva, o primeiro clique so avisa que ela sera perdida
                    if (HasRunSave && !newGameArmed) { newGameArmed = true; Sfx("curse", 0.4f); return; }
                    newGameArmed = false;
                    Sfx("click");
                    screen = GameScreen.NewGame;
                }
            });
            list.Add(new MenuEntry { Label = "OPTIONS", Act = () => OpenOptions(false) });
            list.Add(new MenuEntry { Label = "CODEX", Act = OpenCodex });
            // outros botoes do menu entram aqui (antes de QUIT)
            list.Add(new MenuEntry { Label = "QUIT", Act = () => { Sfx("click"); host.Quit(); } });
            return list;
        }

        /// <summary>Com mais botoes, eles ficam mais juntos para caber abaixo do titulo.</summary>
        static void MenuRow(int i, int count, out int y, out int h)
        {
            if (count <= 3) { y = MenuBtnY(i); h = MenuBtnH; return; }
            if (count == 4) { y = 184 + i * 18; h = 15; return; }
            y = 179 + i * 15; h = 13;
        }

        void ClickMenuEntries()
        {
            var entries = MenuEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                int y, h;
                MenuRow(i, entries.Count, out y, out h);
                if (!Hover(MenuBtnX, y, MenuBtnW, h)) continue;
                bool wasArmed = newGameArmed;
                entries[i].Act();
                if (wasArmed && newGameArmed) newGameArmed = false;   // clicou em outra coisa: desarma
                return;
            }
            newGameArmed = false;
        }

        void DrawMenuEntries(PixelCanvas c)
        {
            var entries = MenuEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                int y, h;
                MenuRow(i, entries.Count, out y, out h);
                DrawButton(c, MenuBtnX, y, MenuBtnW, h, entries[i].Label);
            }
            if (HasRunSave)
            {
                int y, h;
                MenuRow(0, entries.Count, out y, out h);
                string summary = RunSave.Summary(RunSaveText) ?? "???";
                PixelFont.Small.Draw(c, "RUN IN PROGRESS", MenuBtnX + MenuBtnW + 8, y, Palette.Gold);
                PixelFont.Small.Draw(c, summary, MenuBtnX + MenuBtnW + 8, y + 8, Palette.PaperDark);
                if (newGameArmed)
                {
                    MenuRow(1, entries.Count, out y, out h);
                    PixelFont.Small.Draw(c, "CLICK AGAIN: THIS RUN IS LOST", MenuBtnX + MenuBtnW + 8, y + h / 2 - 3, Palette.Red);
                }
            }
        }
    }
}
