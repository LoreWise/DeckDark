using System.Collections.Generic;

namespace DeckDark.Core
{
    public enum CardKind { Attack, Defense, Skill, Curse }
    public enum CardIcon { Sword, Shield, Star, Dagger, Heart, Eye, Potion, Skull, Boot }

    /// <summary>
    /// Definicao de uma carta. No prototipo as cartas sao criadas em codigo (CardLibrary).
    /// Mais para frente isso vira ScriptableObject, editavel no Inspector.
    /// </summary>
    public class CardDef
    {
        public string Id;
        public string Name;
        public CardKind Kind;
        public CardIcon Icon;
        public int Cost;
        public bool Unplayable;
        public bool Exhaust;          // "Esgota": sai do combate depois de usada

        // Ataque
        public int Hits;              // 0 = nao ataca
        public Attr AttackAttr = Attr.FOR;
        public int AttackBonus;       // bonus no teste de acerto
        public DiceExpr Damage;
        public bool AttackAdvantage;

        // Outros efeitos
        public int Block;
        public DiceExpr HealDice;     // Count 0 = sem cura
        public Attr HealAttr = Attr.CON;
        public int Draw;
        public bool GrantAdvantage;   // proximo ataque com vantagem
        public int ArmorThisTurn;     // CA extra ate seu proximo turno
        public int WeakenEnemy;       // penalidade no proximo ataque do inimigo

        public string Description(CharacterSheet sheet)
        {
            var parts = new List<string>();
            if (Unplayable) parts.Add("INJOGÁVEL.");
            if (Hits > 0)
            {
                int mod = sheet != null ? sheet.Mod(AttackAttr) : 0;
                var dmg = new DiceExpr(Damage.Count, Damage.Sides, Damage.Bonus + mod);
                string hit = "ACERTO d20" + Signed(mod + AttackBonus);
                if (AttackAdvantage) hit += " C/ VANTAGEM";
                parts.Add(hit);
                parts.Add((Hits > 1 ? Hits + "X " : "") + "DANO " + dmg);
            }
            if (Block > 0) parts.Add("BLOQUEIA " + Block);
            if (ArmorThisTurn > 0) parts.Add("+" + ArmorThisTurn + " CA ATÉ SEU TURNO");
            if (HealDice.Count > 0)
            {
                int mod = sheet != null ? sheet.Mod(HealAttr) : 0;
                parts.Add("CURA " + new DiceExpr(HealDice.Count, HealDice.Sides, HealDice.Bonus + mod));
            }
            if (Draw > 0) parts.Add("COMPRE " + Draw);
            if (GrantAdvantage) parts.Add("PRÓXIMO ATAQUE C/ VANTAGEM");
            if (WeakenEnemy > 0) parts.Add("INIMIGO -" + WeakenEnemy + " NO PRÓX. ATAQUE");
            if (Exhaust) parts.Add("ESGOTA.");
            if (Kind == CardKind.Curse && !string.IsNullOrEmpty(Flavor)) parts.Add(Flavor);
            return string.Join(" ", parts);
        }

        public string Flavor;

        static string Signed(int v) { return v >= 0 ? "+" + v : v.ToString(); }
    }

    public static class CardLibrary
    {
        public static readonly CardDef SwordStrike = new CardDef
        {
            Id = "sword", Name = "GOLPE DE ESPADA", Kind = CardKind.Attack, Icon = CardIcon.Sword, Cost = 1,
            Hits = 1, AttackAttr = Attr.FOR, Damage = new DiceExpr(1, 8, 0)
        };

        public static readonly CardDef RaiseShield = new CardDef
        {
            Id = "shield", Name = "ERGUER ESCUDO", Kind = CardKind.Defense, Icon = CardIcon.Shield, Cost = 1,
            Block = 5
        };

        public static readonly CardDef BrutalStrike = new CardDef
        {
            Id = "brutal", Name = "GOLPE BRUTAL", Kind = CardKind.Attack, Icon = CardIcon.Sword, Cost = 2,
            Hits = 1, AttackAttr = Attr.FOR, Damage = new DiceExpr(2, 6, 0), AttackAdvantage = true
        };

        public static readonly CardDef Inspiration = new CardDef
        {
            Id = "inspire", Name = "INSPIRAÇÃO", Kind = CardKind.Skill, Icon = CardIcon.Star, Cost = 0,
            GrantAdvantage = true
        };

        public static readonly CardDef ThrowDagger = new CardDef
        {
            Id = "dagger", Name = "ADAGA ARREMESSADA", Kind = CardKind.Attack, Icon = CardIcon.Dagger, Cost = 1,
            Hits = 1, AttackAttr = Attr.DES, Damage = new DiceExpr(1, 4, 0), Draw = 1
        };

        public static readonly CardDef Charge = new CardDef
        {
            Id = "charge", Name = "INVESTIDA", Kind = CardKind.Attack, Icon = CardIcon.Boot, Cost = 2,
            Hits = 1, AttackAttr = Attr.FOR, AttackBonus = 2, Damage = new DiceExpr(1, 12, 0)
        };

        public static readonly CardDef DefensiveStance = new CardDef
        {
            Id = "stance", Name = "POSTURA DEFENSIVA", Kind = CardKind.Defense, Icon = CardIcon.Shield, Cost = 1,
            ArmorThisTurn = 3, Block = 3
        };

        public static readonly CardDef SecondWind = new CardDef
        {
            Id = "wind", Name = "SEGUNDO FÔLEGO", Kind = CardKind.Skill, Icon = CardIcon.Heart, Cost = 1,
            HealDice = new DiceExpr(1, 10, 0), HealAttr = Attr.CON, Exhaust = true
        };

        public static readonly CardDef DoubleStrike = new CardDef
        {
            Id = "double", Name = "GOLPE DUPLO", Kind = CardKind.Attack, Icon = CardIcon.Sword, Cost = 2,
            Hits = 2, AttackAttr = Attr.FOR, Damage = new DiceExpr(1, 6, 0)
        };

        public static readonly CardDef Taunt = new CardDef
        {
            Id = "taunt", Name = "PROVOCAR", Kind = CardKind.Skill, Icon = CardIcon.Eye, Cost = 1,
            WeakenEnemy = 4, Block = 2
        };

        public static readonly CardDef HealingPotion = new CardDef
        {
            Id = "potion", Name = "POÇÃO DE CURA", Kind = CardKind.Skill, Icon = CardIcon.Potion, Cost = 0,
            HealDice = new DiceExpr(2, 4, 2), Exhaust = true
        };

        public static readonly CardDef Lucidity = new CardDef
        {
            Id = "lucid", Name = "LUCIDEZ", Kind = CardKind.Skill, Icon = CardIcon.Eye, Cost = 0,
            Draw = 2, Exhaust = true
        };

        public static readonly CardDef Nightmare = new CardDef
        {
            Id = "nightmare", Name = "PESADELO", Kind = CardKind.Curse, Icon = CardIcon.Skull, Cost = 0,
            Unplayable = true, Flavor = "ELE SABE O SEU NOME."
        };

        /// <summary>Cartas que podem aparecer como recompensa de combate.</summary>
        public static readonly CardDef[] RewardPool =
        {
            ThrowDagger, Charge, DefensiveStance, SecondWind, DoubleStrike, Taunt, BrutalStrike
        };
    }
}
