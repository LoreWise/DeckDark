using System;

namespace DeckDark.Core
{
    /// <summary>
    /// Resultado de uma rolagem de teste (d20 + modificador).
    /// Guarda os dois dados quando ha vantagem ou desvantagem.
    /// </summary>
    public struct TestRoll
    {
        public int Natural;      // o dado que valeu
        public int Other;        // o outro dado (0 se rolou so um)
        public int Modifier;
        public int Target;       // CA ou CD a vencer
        public bool Advantage;
        public bool Disadvantage;
        public int CritOn;       // menor natural que conta como critico (0 = so 20)

        public int Total { get { return Natural + Modifier; } }
        public bool IsCrit { get { return Natural >= (CritOn > 0 ? CritOn : 20); } }
        public bool IsFumble { get { return Natural == 1; } }

        /// <summary>No D&D, 20 natural sempre acerta e 1 natural sempre erra.</summary>
        public bool Success
        {
            get
            {
                if (IsCrit) return true;
                if (IsFumble) return false;
                return Total >= Target;
            }
        }
    }

    /// <summary>Uma expressao de dano, como 1d8+3.</summary>
    public struct DiceExpr
    {
        public int Count;
        public int Sides;
        public int Bonus;

        public DiceExpr(int count, int sides, int bonus)
        {
            Count = count; Sides = sides; Bonus = bonus;
        }

        public override string ToString()
        {
            string s = Count + "d" + Sides;
            if (Bonus > 0) s += "+" + Bonus;
            else if (Bonus < 0) s += Bonus;
            return s;
        }
    }

    /// <summary>
    /// Todas as rolagens do jogo passam por aqui. Usa uma semente para
    /// que uma run possa ser reproduzida no futuro.
    /// </summary>
    public class Dice
    {
        readonly Random rng;

        public Dice(int seed) { rng = new Random(seed); }

        public int Roll(int sides) { return rng.Next(1, sides + 1); }

        public int Range(int minInclusive, int maxExclusive) { return rng.Next(minInclusive, maxExclusive); }

        public int Roll(DiceExpr e, bool crit)
        {
            int count = crit ? e.Count * 2 : e.Count;
            int sum = 0;
            for (int i = 0; i < count; i++) sum += Roll(e.Sides);
            return Math.Max(0, sum + e.Bonus);
        }

        public TestRoll Test(int modifier, int target, bool advantage, bool disadvantage, int forcedNatural = 0)
        {
            // vantagem e desvantagem se cancelam
            if (advantage && disadvantage) { advantage = false; disadvantage = false; }

            var r = new TestRoll { Modifier = modifier, Target = target, Advantage = advantage, Disadvantage = disadvantage };
            if (forcedNatural > 0)
            {
                // Steady Breath / Loaded Die: o dado ja esta decidido
                r.Natural = forcedNatural;
                r.Advantage = false;
                r.Disadvantage = false;
                return r;
            }
            int a = Roll(20);
            if (advantage || disadvantage)
            {
                int b = Roll(20);
                bool takeA = advantage ? a >= b : a <= b;
                r.Natural = takeA ? a : b;
                r.Other = takeA ? b : a;
            }
            else
            {
                r.Natural = a;
            }
            return r;
        }
    }
}
