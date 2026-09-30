using System;
using System.Collections.Generic;
using DeckDark.Core;

namespace DeckDark.View
{
    public enum FxKind { None, Slash, Stab, Fire, Arcane, Frost, Smash, Punch, Bite, Claw, Curse, Shield, Heal, Buff, Summon, Fear, Thunder, Death, Crown, Blood, Whiff }

    /// <summary>
    /// Animacoes do combate: investidas das miniaturas, projeteis, cortes e particulas.
    /// Tudo desenhado com pixels na tela (por cima da mesa 3D).
    /// </summary>
    public partial class GameApp
    {
        class Fx
        {
            public FxKind Kind;
            public float X0, Y0, X1, Y1, T, Dur;
            public bool Travel;     // projetil: voa de (X0,Y0) ate (X1,Y1) e depois explode
        }

        class Spark
        {
            public float X, Y, VX, VY, Life, Max, G;
            public Rgb C;
            public int Size;
        }

        readonly List<Fx> fxList = new List<Fx>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly Random fxRand = new Random(7);
        FxKind playerFx = FxKind.Slash;
        FxKind enemyFx = FxKind.Slash;

        // investida (a miniatura avanca e volta)
        const float LungeTime = 0.32f;
        float playerLungeT = -1;
        readonly float[] enemyLungeT = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 };

        // ------------------------------------------------------------ escolha do efeito

        static FxKind FxForCard(CardDef c)
        {
            if (c.Id == "frost") return FxKind.Frost;
            if (c.Id == "thunder") return FxKind.Thunder;
            if (c.AutoHits > 0) return FxKind.Arcane;
            if (c.SpellDamage || (c.Hits > 0 && c.AttackAttr == Attr.INT))
                return c.Icon == CardIcon.Flame ? FxKind.Fire : (c.Icon == CardIcon.Fist ? FxKind.Thunder : FxKind.Arcane);
            if (c.Hits > 0)
            {
                switch (c.Icon)
                {
                    case CardIcon.Dagger: return FxKind.Stab;
                    case CardIcon.Fist: return FxKind.Punch;
                    case CardIcon.Shield: return FxKind.Smash;
                    case CardIcon.Blood: return FxKind.Blood;
                    default: return FxKind.Slash;
                }
            }
            if (c.Save != SaveEffect.None) return FxKind.Curse;
            return FxKind.Buff;
        }

        static FxKind FxForEnemy(EnemySprite s, MoveKind k)
        {
            switch (k)
            {
                case MoveKind.Guard: case MoveKind.GuardAlly: return FxKind.Shield;
                case MoveKind.Frighten: return FxKind.Fear;
                case MoveKind.Summon: return FxKind.Summon;
                case MoveKind.Curse: return FxKind.Curse;
            }
            switch (s)
            {
                case EnemySprite.Rat: return FxKind.Bite;
                case EnemySprite.Ogre: return FxKind.Smash;
                case EnemySprite.Ghoul: return FxKind.Claw;
                case EnemySprite.Cultist: return FxKind.Curse;
                case EnemySprite.Boss: return FxKind.Crown;
                default: return FxKind.Slash;
            }
        }

        static bool IsMelee(FxKind k)
        {
            return k == FxKind.Slash || k == FxKind.Punch || k == FxKind.Smash || k == FxKind.Bite || k == FxKind.Claw || k == FxKind.Crown || k == FxKind.Blood;
        }

        static bool IsProjectile(FxKind k)
        {
            return k == FxKind.Stab || k == FxKind.Fire || k == FxKind.Arcane || k == FxKind.Frost || k == FxKind.Curse;
        }

        // ------------------------------------------------------------ posicoes

        float PlayerCx { get { return PlayerMiniX; } }
        float PlayerCy { get { return MiniFeetY - 16; } }
        float EnemyCx(int i) { return EnemyX(i); }
        float EnemyCy(int i) { return MiniFeetY - EnemySpriteHeight(i) / 2; }

        static float LungeCurve(float t) { return t < 0 ? 0 : (float)Math.Sin(Math.PI * Math.Min(1f, t / LungeTime)); }
        int PlayerLungeX() { return (int)(LungeCurve(playerLungeT) * 14); }
        int PlayerLift() { return (int)(LungeCurve(playerLungeT) * 5); }
        int EnemyLungeX(int i) { return i < enemyLungeT.Length ? -(int)(LungeCurve(enemyLungeT[i]) * 14) : 0; }
        int EnemyLift(int i)
        {
            int hop = i < enemyLungeT.Length ? (int)(LungeCurve(enemyLungeT[i]) * 5) : 0;
            int idle = (int)Math.Round(Math.Sin(time * 2.1 + i * 1.7) * 0.7 + 0.7);   // respiracao
            return hop + idle;
        }

        // ------------------------------------------------------------ disparo

        void StartPlayerLunge() { playerLungeT = 0; }
        void StartEnemyLunge(int i) { if (i >= 0 && i < enemyLungeT.Length) enemyLungeT[i] = 0; }

        /// <summary>Efeito indo de um ponto a outro (projetil ou golpe no alvo).</summary>
        void SpawnFx(FxKind k, float x0, float y0, float x1, float y1)
        {
            if (k == FxKind.None) return;
            if (IsProjectile(k))
                fxList.Add(new Fx { Kind = k, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Dur = k == FxKind.Curse ? 0.3f : 0.2f, Travel = true });
            else
                Impact(k, x1, y1);
        }

        /// <summary>Efeito no proprio lugar (escudo, cura, fumaca...).</summary>
        void SpawnSelf(FxKind k, float x, float y) { Impact(k, x, y); }

        void Impact(FxKind k, float x, float y)
        {
            float dur = 0.35f;
            switch (k)
            {
                case FxKind.Fire: Burst(x, y, 14, new[] { 0xffe08a, 0xf0a040, 0xd04a2a }, 40, -20); dur = 0.3f; break;
                case FxKind.Arcane: Burst(x, y, 8, new[] { 0xe0c8ff, 0xb07ae0, 0x7a4ab0 }, 30, 0); break;
                case FxKind.Frost: Burst(x, y, 10, new[] { 0xf0ffff, 0x9ae0f0, 0x5aa0d0 }, 30, 10); break;
                case FxKind.Stab: Burst(x, y, 5, new[] { 0xffffff, 0xa8b0b8 }, 25, 20); break;
                case FxKind.Curse: Burst(x, y, 10, new[] { 0xc890f0, 0x7a3a9a, 0x3a1a4a }, 25, -15); break;
                case FxKind.Smash: Burst(x, y + 12, 14, new[] { 0xb8a888, 0x8a7a5a, 0x5a4a3a }, 45, 60); shake = Math.Max(shake, 4f); break;
                case FxKind.Punch: Burst(x, y, 6, new[] { 0xffffff, 0xf0c040 }, 30, 0); break;
                case FxKind.Blood: case FxKind.Bite: case FxKind.Claw: Burst(x, y, 7, new[] { 0xd24a3c, 0x7a2226 }, 30, 60); break;
                case FxKind.Crown: Burst(x, y, 10, new[] { 0xfff0a0, 0xf0c040, 0xa87a20 }, 40, 30); break;
                case FxKind.Thunder: Burst(x, y, 12, new[] { 0xffffff, 0x9ad0ff }, 50, 0); dur = 0.45f; break;
                case FxKind.Heal: Rise(x, y, 10, new[] { 0x9af08a, 0x5ac05a }); dur = 0.6f; break;
                case FxKind.Buff: Rise(x, y, 8, new[] { 0xfff0a0, 0xf0c040 }); dur = 0.5f; break;
                case FxKind.Summon: Rise(x, y + 10, 16, new[] { 0xc890f0, 0x6a3a8a, 0xe8e0cf }); dur = 0.7f; break;
                case FxKind.Death: Rise(x, y, 14, new[] { 0x8a8a90, 0x5a5a62, 0x3a3a40 }); dur = 0.6f; break;
                case FxKind.Shield: dur = 0.5f; break;
                case FxKind.Fear: dur = 0.6f; break;
                case FxKind.Whiff: dur = 0.25f; break;
            }
            fxList.Add(new Fx { Kind = k, X0 = x, Y0 = y, X1 = x, Y1 = y, Dur = dur });
        }

        void Burst(float x, float y, int n, int[] cols, float speed, float gravity)
        {
            for (int i = 0; i < n; i++)
            {
                double a = fxRand.NextDouble() * Math.PI * 2;
                float s = speed * (0.4f + (float)fxRand.NextDouble() * 0.8f);
                float life = 0.25f + (float)fxRand.NextDouble() * 0.3f;
                sparks.Add(new Spark
                {
                    X = x, Y = y, VX = (float)Math.Cos(a) * s, VY = (float)Math.Sin(a) * s * 0.7f, G = gravity,
                    Life = life, Max = life, C = Rgb.Hex(cols[fxRand.Next(cols.Length)]), Size = fxRand.Next(3) == 0 ? 2 : 1,
                });
            }
        }

        void Rise(float x, float y, int n, int[] cols)
        {
            for (int i = 0; i < n; i++)
            {
                float life = 0.4f + (float)fxRand.NextDouble() * 0.4f;
                sparks.Add(new Spark
                {
                    X = x + fxRand.Next(-10, 11), Y = y + fxRand.Next(-4, 12), VX = (float)(fxRand.NextDouble() - 0.5) * 6, VY = -18 - (float)fxRand.NextDouble() * 16,
                    Life = life, Max = life, C = Rgb.Hex(cols[fxRand.Next(cols.Length)]), Size = fxRand.Next(2) == 0 ? 2 : 1,
                });
            }
        }

        // ------------------------------------------------------------ gancho nos eventos do combate

        /// <summary>Chamado antes de aplicar cada evento, para disparar as animacoes certas.</summary>
        void FxForEvent(CombatEvent ev)
        {
            if (combat == null) return;
            switch (ev.Type)
            {
                case EvType.Actor:
                    if (ev.Actor >= 0 && ev.Actor < combat.Enemies.Count)
                    {
                        var kind = (MoveKind)ev.Amount;
                        enemyFx = FxForEnemy(combat.Enemies[ev.Actor].Def.Sprite, kind);
                        if (kind == MoveKind.Frighten) { StartEnemyLunge(ev.Actor); Impact(FxKind.Fear, EnemyCx(ev.Actor), EnemyCy(ev.Actor)); }
                        if (kind == MoveKind.Curse) SpawnFx(FxKind.Curse, EnemyCx(ev.Actor), EnemyCy(ev.Actor), PlayerCx, PlayerCy);
                    }
                    break;

                case EvType.Hit:
                case EvType.Miss:
                    bool hit = ev.Type == EvType.Hit;
                    if (ev.Target < 0)
                    {
                        // um inimigo ataca voce
                        if (ev.Actor < 0) break;
                        if (IsMelee(enemyFx) || !IsProjectile(enemyFx)) StartEnemyLunge(ev.Actor);
                        if (hit) SpawnFx(enemyFx, EnemyCx(ev.Actor), EnemyCy(ev.Actor), PlayerCx, PlayerCy);
                        else Impact(FxKind.Whiff, PlayerCx, PlayerCy);
                    }
                    else if (ev.Target < enemyViews.Count)
                    {
                        float tx = EnemyCx(ev.Target), ty = EnemyCy(ev.Target);
                        FxKind k = playerFx;
                        if (ev.Label == "FLAMES") k = FxKind.Fire;
                        else if (ev.Label == "DART") k = FxKind.Arcane;
                        else if (ev.Label == "HEMORRHAGE") k = FxKind.Blood;
                        else if (ev.Label != null) k = FxKind.Slash;
                        bool fromPlayer = ev.Label == null || ev.Label == "DART";
                        if (fromPlayer && IsMelee(k)) StartPlayerLunge();
                        if (!hit) { Impact(FxKind.Whiff, tx, ty); break; }
                        if (ev.Label == "FLAMES") Impact(FxKind.Fire, tx, ty);
                        else SpawnFx(k, PlayerCx + 10, PlayerCy, tx, ty);
                    }
                    break;

                case EvType.Block:
                    if (ev.Amount <= 0) break;
                    if (ev.Target < 0) SpawnSelf(FxKind.Shield, PlayerCx, PlayerCy);
                    else if (ev.Target < enemyViews.Count) SpawnSelf(FxKind.Shield, EnemyCx(ev.Target), EnemyCy(ev.Target));
                    break;

                case EvType.Heal: SpawnSelf(FxKind.Heal, PlayerCx, PlayerCy); break;
                case EvType.Energy: SpawnSelf(FxKind.Buff, PlayerCx, PlayerCy); break;
                case EvType.Die: if (ev.Target >= 0) SpawnSelf(FxKind.Death, EnemyCx(ev.Target), EnemyCy(ev.Target)); break;
                case EvType.Summon: SpawnSelf(FxKind.Summon, EnemyCx(ev.Target), EnemyCy(ev.Target)); break;
                case EvType.Rise: SpawnSelf(FxKind.Summon, EnemyCx(ev.Target), EnemyCy(ev.Target)); break;
                case EvType.Curse: SpawnSelf(FxKind.Curse, PlayerCx, PlayerCy); break;
                case EvType.Status:
                    if (ev.Target < 0 && (ev.Tone == Tone.Gold || ev.Tone == Tone.Blue)) SpawnSelf(FxKind.Buff, PlayerCx, PlayerCy);
                    else if (ev.Target >= 0 && ev.Target < enemyViews.Count && ev.Tone == Tone.Purple) SpawnSelf(FxKind.Curse, EnemyCx(ev.Target), EnemyCy(ev.Target));
                    break;
            }
        }

        /// <summary>Cartas sem alvo (buffs, concentracao) ganham um brilho ao serem jogadas.</summary>
        void FxForCardPlayed(CardDef c, int target)
        {
            playerFx = FxForCard(c);
            bool attacks = c.Hits > 0 || c.SpellDamage || c.AutoHits > 0;
            if (attacks) return;
            if (c.Save != SaveEffect.None && !c.AllEnemies && target >= 0 && target < enemyViews.Count)
                SpawnFx(FxKind.Curse, PlayerCx + 10, PlayerCy, EnemyCx(target), EnemyCy(target));
            else if (c.Save != SaveEffect.None && c.AllEnemies)
                Impact(FxKind.Fear, PlayerCx, PlayerCy);
            else if (c.Block <= 0 && c.HealDice.Count == 0)
                SpawnSelf(FxKind.Buff, PlayerCx, PlayerCy);
        }

        // ------------------------------------------------------------ atualizar e desenhar

        void UpdateFx(float dt)
        {
            if (playerLungeT >= 0) { playerLungeT += dt; if (playerLungeT > LungeTime) playerLungeT = -1; }
            for (int i = 0; i < enemyLungeT.Length; i++)
                if (enemyLungeT[i] >= 0) { enemyLungeT[i] += dt; if (enemyLungeT[i] > LungeTime) enemyLungeT[i] = -1; }

            for (int i = fxList.Count - 1; i >= 0; i--)
            {
                var f = fxList[i];
                f.T += dt;
                if (f.T >= f.Dur)
                {
                    fxList.RemoveAt(i);
                    if (f.Travel) Impact(f.Kind, f.X1, f.Y1);
                }
            }
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.Life -= dt;
                if (s.Life <= 0) { sparks.RemoveAt(i); continue; }
                s.VY += s.G * dt;
                s.X += s.VX * dt; s.Y += s.VY * dt;
            }
        }

        void ClearFx()
        {
            fxList.Clear(); sparks.Clear();
            playerLungeT = -1;
            for (int i = 0; i < enemyLungeT.Length; i++) enemyLungeT[i] = -1;
        }

        void DrawFx(PixelCanvas c)
        {
            c.ResetTint();
            foreach (var f in fxList)
            {
                float p = Math.Min(1f, f.T / f.Dur);
                if (f.Travel) { DrawProjectile(c, f, p); continue; }
                int x = (int)f.X0, y = (int)f.Y0;
                float fade = 1f - p;
                switch (f.Kind)
                {
                    case FxKind.Slash: DrawSlash(c, x, y, p, Palette.White, Rgb.Hex(0xc8d0d8), 1); break;
                    case FxKind.Crown: DrawSlash(c, x, y, p, Rgb.Hex(0xfff0a0), Palette.Gold, 2); break;
                    case FxKind.Claw:
                        for (int k = -1; k <= 1; k++) DrawSlash(c, x + k * 5, y, p, Rgb.Hex(0xf08070), Palette.Red, 0);
                        break;
                    case FxKind.Blood: DrawSlash(c, x, y, p, Palette.Red, Palette.DarkRed, 0); break;
                    case FxKind.Bite:
                    {
                        int gap = (int)(10 * (1 - p));
                        for (int k = -2; k <= 2; k++)
                        {
                            c.FillTriangle(x + k * 4 - 2, y - gap - 4, x + k * 4 + 2, y - gap - 4, x + k * 4, y - gap + 1, Palette.White);
                            c.FillTriangle(x + k * 4 - 2, y + gap + 4, x + k * 4 + 2, y + gap + 4, x + k * 4, y + gap - 1, Palette.White);
                        }
                        break;
                    }
                    case FxKind.Punch: case FxKind.Smash: case FxKind.Thunder:
                    {
                        int r = (int)(4 + p * (f.Kind == FxKind.Punch ? 10 : 22));
                        var col = f.Kind == FxKind.Thunder ? Rgb.Hex(0x9ad0ff) : (f.Kind == FxKind.Smash ? Rgb.Hex(0xd8c8a0) : Palette.Gold);
                        if (fade > 0.3f) { c.Circle(x, y, r, col); c.Circle(x, y, r - 1, Palette.White); }
                        else c.Circle(x, y, r, col);
                        break;
                    }
                    case FxKind.Shield:
                    {
                        var col = Rgb.Hex(0x7ab8ff);
                        int rx = 14 + (int)(p * 4), ry = 18 + (int)(p * 4);
                        if (((int)(f.T * 20)) % 2 == 0 || p < 0.5f)
                        {
                            DrawEllipseOutline(c, x, y, rx, ry, col);
                            DrawEllipseOutline(c, x, y, rx - 1, ry - 1, Palette.White);
                        }
                        break;
                    }
                    case FxKind.Fear:
                        for (int k = 0; k < 3; k++)
                        {
                            float q = (p + k * 0.33f) % 1f;
                            DrawEllipseOutline(c, x, y, (int)(6 + q * 26), (int)(4 + q * 16), q < 0.5f ? Palette.Purple : Rgb.Hex(0x6a3a8a));
                        }
                        break;
                    case FxKind.Whiff:
                        c.DashedLine(x - 10, y - 8, x + 10, y + 6, Rgb.Hex(0xa8a090), 2, 2);
                        break;
                    case FxKind.Fire:
                        c.FillCircle(x, y, (int)(3 + p * 8), p < 0.5f ? Rgb.Hex(0xffe08a) : Rgb.Hex(0xf0a040));
                        break;
                    case FxKind.Arcane:
                        c.Circle(x, y, (int)(2 + p * 8), Palette.Purple);
                        break;
                    case FxKind.Frost:
                        for (int k = 0; k < 4; k++)
                        {
                            double a = k * Math.PI / 4;
                            int len = (int)(4 + p * 8);
                            c.Line(x, y, x + (int)(Math.Cos(a) * len), y + (int)(Math.Sin(a) * len), Rgb.Hex(0xd8f8ff));
                            c.Line(x, y, x - (int)(Math.Cos(a) * len), y - (int)(Math.Sin(a) * len), Rgb.Hex(0xd8f8ff));
                        }
                        break;
                }
            }
            foreach (var s in sparks)
            {
                float a = Math.Max(0f, Math.Min(1f, s.Life / s.Max * 1.5f));
                c.FillAlpha((int)s.X, (int)s.Y, s.Size, s.Size, s.C, a);
            }
        }

        void DrawSlash(PixelCanvas c, int x, int y, float p, Rgb core, Rgb edge, int thick)
        {
            // um arco que se desenha rapido e some
            float grow = Math.Min(1f, p * 2.5f);
            float fadeStart = Math.Max(0f, (p - 0.4f) / 0.6f);
            int steps = 24;
            for (int i = (int)(fadeStart * steps); i < (int)(grow * steps); i++)
            {
                float t = i / (float)steps;
                int px = x - 17 + (int)(t * 34);
                int py = y - 16 + (int)(t * 30) - (int)(Math.Sin(t * Math.PI) * 9);
                c.Fill(px, py, 3, 3 + thick, core);
                c.Fill(px + 1, py + 3 + thick, 2, 1, edge);
                c.Fill(px - 1, py - 1, 2, 1, edge);
                // rastro fino ao lado
                c.Set(px + 2, py + 6 + thick, edge);
            }
        }

        static void DrawEllipseOutline(PixelCanvas c, int cx, int cy, int rx, int ry, Rgb col)
        {
            int n = Math.Max(16, (rx + ry) * 2);
            for (int i = 0; i < n; i++)
            {
                double a = i * Math.PI * 2 / n;
                c.Set(cx + (int)Math.Round(Math.Cos(a) * rx), cy + (int)Math.Round(Math.Sin(a) * ry), col);
            }
        }

        void DrawProjectile(PixelCanvas c, Fx f, float p)
        {
            Rgb head, tail;
            int size = 2;
            switch (f.Kind)
            {
                case FxKind.Fire: head = Rgb.Hex(0xffe08a); tail = Rgb.Hex(0xd04a2a); size = 3; break;
                case FxKind.Frost: head = Rgb.Hex(0xf0ffff); tail = Rgb.Hex(0x5aa0d0); break;
                case FxKind.Curse: head = Rgb.Hex(0xc890f0); tail = Rgb.Hex(0x3a1a4a); size = 3; break;
                case FxKind.Stab: head = Palette.White; tail = Rgb.Hex(0x80808a); size = 1; break;
                default: head = Rgb.Hex(0xe0c8ff); tail = Palette.Purple; break;
            }
            // arco leve no caminho
            for (int k = 4; k >= 0; k--)
            {
                float q = Math.Max(0f, p - k * 0.06f);
                float x = f.X0 + (f.X1 - f.X0) * q;
                float y = f.Y0 + (f.Y1 - f.Y0) * q - (float)Math.Sin(q * Math.PI) * 10;
                var col = k == 0 ? head : Rgb.Lerp(head, tail, k / 4f);
                int s = Math.Max(1, size - k / 2);
                if (f.Kind == FxKind.Stab && k == 0) { c.Line((int)x - 4, (int)y, (int)x + 2, (int)y, Rgb.Hex(0xc8d0d8)); c.Set((int)x + 3, (int)y, Palette.White); }
                else c.FillCircle((int)x, (int)y, s, col);
            }
        }
    }
}
