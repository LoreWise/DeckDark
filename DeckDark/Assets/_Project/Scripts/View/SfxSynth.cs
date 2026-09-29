using System;
using System.Collections.Generic;

namespace DeckDark.View
{
    /// <summary>
    /// Gera os efeitos sonoros por codigo, para o prototipo nao depender de arquivos de audio.
    /// Depois, cada um destes vira um .wav de verdade em Audio/SFX.
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 22050;

        public static Dictionary<string, float[]> BuildAll()
        {
            var d = new Dictionary<string, float[]>();
            d["dice"] = DiceRoll();
            d["card"] = CardSwish();
            d["hit"] = Hit();
            d["block"] = Block();
            d["miss"] = Miss();
            d["click"] = Click();
            d["crit"] = Crit();
            d["heal"] = Heal();
            d["death"] = Death();
            d["curse"] = Curse();
            d["pencil"] = Pencil();
            d["flicker"] = Flicker();
            d["drone"] = Drone();
            return d;
        }

        static float[] Buf(float seconds) { return new float[(int)(Rate * seconds)]; }

        static float[] DiceRoll()
        {
            var b = Buf(0.7f);
            var rnd = new Random(7);
            // varias batidas curtas, cada vez mais espacadas, como um dado quicando na mesa
            float t = 0f, gap = 0.035f;
            while (t < 0.62f)
            {
                int start = (int)(t * Rate);
                float amp = 0.55f * (1f - t / 0.7f);
                float freq = 1800f + (float)rnd.NextDouble() * 1400f;
                for (int i = 0; i < 500 && start + i < b.Length; i++)
                {
                    float env = (float)Math.Exp(-i / 60.0);
                    float n = (float)(rnd.NextDouble() * 2 - 1);
                    b[start + i] += amp * env * (0.6f * n + 0.4f * (float)Math.Sin(2 * Math.PI * freq * i / Rate));
                }
                t += gap;
                gap *= 1.22f;
            }
            return b;
        }

        static float[] CardSwish()
        {
            var b = Buf(0.16f);
            var rnd = new Random(3);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float p = (float)i / b.Length;
                float env = (float)Math.Sin(Math.PI * p);
                float n = (float)(rnd.NextDouble() * 2 - 1);
                lp += (n - lp) * (0.15f + 0.5f * p);
                b[i] = lp * env * 0.5f;
            }
            return b;
        }

        static float[] Hit()
        {
            var b = Buf(0.3f);
            var rnd = new Random(5);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 140f * (float)Math.Exp(-t * 9);
                float body = (float)Math.Sin(2 * Math.PI * (60 + f) * t) * (float)Math.Exp(-t * 14);
                float n = (float)(rnd.NextDouble() * 2 - 1) * (float)Math.Exp(-t * 40);
                b[i] = 0.7f * body + 0.35f * n;
            }
            return b;
        }

        static float[] Block()
        {
            var b = Buf(0.4f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Exp(-t * 11);
                b[i] = env * 0.3f * (float)(Math.Sin(2 * Math.PI * 520 * t) + 0.6 * Math.Sin(2 * Math.PI * 1370 * t) + 0.3 * Math.Sin(2 * Math.PI * 2210 * t));
            }
            return b;
        }

        static float[] Miss()
        {
            var b = Buf(0.22f);
            var rnd = new Random(11);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float p = (float)i / b.Length;
                float n = (float)(rnd.NextDouble() * 2 - 1);
                lp += (n - lp) * 0.08f;
                b[i] = lp * (1 - p) * 0.8f;
            }
            return b;
        }

        static float[] Click()
        {
            var b = Buf(0.05f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                b[i] = 0.35f * (float)Math.Sin(2 * Math.PI * 1100 * t) * (float)Math.Exp(-t * 90);
            }
            return b;
        }

        static float[] Crit()
        {
            var b = Buf(0.6f);
            float[] notes = { 523f, 659f, 784f, 1047f };
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * 0.07f * Rate);
                for (int i = 0; start + i < b.Length; i++)
                {
                    float t = (float)i / Rate;
                    b[start + i] += 0.18f * Square(notes[n] * t) * (float)Math.Exp(-t * 6);
                }
            }
            return b;
        }

        static float[] Heal()
        {
            var b = Buf(0.5f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 400 + 500 * t;
                b[i] = 0.25f * (float)Math.Sin(2 * Math.PI * f * t) * (float)Math.Sin(Math.PI * t / 0.5f);
            }
            return b;
        }

        static float[] Death()
        {
            var b = Buf(2.2f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 220f * (float)Math.Pow(0.5, t / 1.2);
                float env = (float)Math.Exp(-t * 1.3);
                b[i] = 0.3f * env * (float)(Math.Sin(2 * Math.PI * f * t) + 0.5 * Math.Sin(2 * Math.PI * f * 1.01 * t + 1));
            }
            return b;
        }

        static float[] Curse()
        {
            var b = Buf(1.2f);
            var rnd = new Random(13);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Sin(Math.PI * t / 1.2f);
                float n = (float)(rnd.NextDouble() * 2 - 1);
                lp += (n - lp) * 0.03f;
                float tone = (float)(Math.Sin(2 * Math.PI * 110 * t) * Math.Sin(2 * Math.PI * 3.3 * t));
                b[i] = env * (0.6f * lp + 0.25f * tone);
            }
            return b;
        }

        static float[] Pencil()
        {
            var b = Buf(0.35f);
            var rnd = new Random(17);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float scratch = (float)Math.Abs(Math.Sin(2 * Math.PI * 18 * t));
                float n = (float)(rnd.NextDouble() * 2 - 1);
                b[i] = 0.18f * n * scratch * (1 - t / 0.35f);
            }
            return b;
        }

        static float[] Flicker()
        {
            var b = Buf(0.25f);
            var rnd = new Random(19);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float buzz = Square(120 * t) * 0.12f;
                float n = (float)(rnd.NextDouble() * 2 - 1) * 0.1f;
                b[i] = (buzz + n) * (rnd.NextDouble() < 0.7 ? 1f : 0f) * (1 - t / 0.25f);
            }
            return b;
        }

        /// <summary>Zumbido baixo e continuo do porao (lampada + geladeira distante). Em loop.</summary>
        static float[] Drone()
        {
            var b = Buf(4f);
            var rnd = new Random(23);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)(rnd.NextDouble() * 2 - 1);
                lp += (n - lp) * 0.01f;
                // frequencias multiplas de 0.25 Hz para o loop fechar sem estalo
                float hum = (float)(Math.Sin(2 * Math.PI * 60 * t) * 0.5 + Math.Sin(2 * Math.PI * 120 * t) * 0.25);
                float swell = 0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * 0.25 * t);
                b[i] = 0.12f * hum * swell + 0.35f * lp;
            }
            return b;
        }

        static float Square(float phase) { return (phase - (float)Math.Floor(phase)) < 0.5f ? 1f : -1f; }
    }
}
