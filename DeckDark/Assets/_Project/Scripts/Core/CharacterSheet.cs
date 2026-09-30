using System.Collections.Generic;

namespace DeckDark.Core
{
    public enum Attr { STR, DEX, CON, INT, WIS, CHA }
    public enum PlayerClass { Warrior, Wizard }

    /// <summary>
    /// A ficha do personagem: atributos, vida, classe de armadura, deck e reliquias.
    /// </summary>
    public class CharacterSheet
    {
        public string Name = "WARRIOR";
        public PlayerClass Class = PlayerClass.Warrior;
        /// <summary>CD das magias: 8 + proficiencia (2) + INT.</summary>
        public int SpellDc { get { return 10 + Mod(Attr.INT); } }
        public int Level = 1;
        public readonly int[] Scores = new int[6];
        public int MaxHp;
        public int Hp;
        public int BaseArmorClass = 14;
        public int EnergyPerTurn = 3;
        public int HandSize = 5;
        public readonly List<CardDef> Deck = new List<CardDef>();
        public readonly List<RelicDef> Relics = new List<RelicDef>();

        public int Score(Attr a) { return Scores[(int)a]; }

        /// <summary>Modificador no estilo D&D: (valor - 10) / 2, arredondando para baixo.</summary>
        public int Mod(Attr a)
        {
            int s = Score(a) - 10;
            return s >= 0 ? s / 2 : (s - 1) / 2;
        }

        public int ArmorClass
        {
            get
            {
                int ac = BaseArmorClass;
                foreach (var r in Relics) ac += r.ArmorClassBonus;
                return ac;
            }
        }

        public bool HasRelic(RelicId id)
        {
            foreach (var r in Relics) if (r.Id == id) return true;
            return false;
        }

        public void Heal(int amount)
        {
            Hp += amount;
            if (Hp > MaxHp) Hp = MaxHp;
        }

        public static CharacterSheet NewWarrior()
        {
            var c = new CharacterSheet();
            c.Name = "WARRIOR";
            c.Scores[(int)Attr.STR] = 16;
            c.Scores[(int)Attr.DEX] = 12;
            c.Scores[(int)Attr.CON] = 14;
            c.Scores[(int)Attr.INT] = 8;
            c.Scores[(int)Attr.WIS] = 10;
            c.Scores[(int)Attr.CHA] = 10;
            c.MaxHp = 34;
            c.Hp = c.MaxHp;
            c.BaseArmorClass = 14;
            for (int i = 0; i < 4; i++) c.Deck.Add(CardLibrary.SwordStrike);
            for (int i = 0; i < 4; i++) c.Deck.Add(CardLibrary.RaiseShield);
            c.Deck.Add(CardLibrary.BrutalStrike);
            c.Deck.Add(CardLibrary.Inspiration);
            return c;
        }

        public static CharacterSheet New(PlayerClass cls) { return cls == PlayerClass.Wizard ? NewWizard() : NewWarrior(); }

        public static CharacterSheet NewWizard()
        {
            var c = new CharacterSheet();
            c.Name = "WIZARD";
            c.Class = PlayerClass.Wizard;
            c.Scores[(int)Attr.STR] = 8;
            c.Scores[(int)Attr.DEX] = 14;
            c.Scores[(int)Attr.CON] = 12;
            c.Scores[(int)Attr.INT] = 16;
            c.Scores[(int)Attr.WIS] = 12;
            c.Scores[(int)Attr.CHA] = 10;
            c.MaxHp = 22;
            c.Hp = c.MaxHp;
            c.BaseArmorClass = 12;
            for (int i = 0; i < 4; i++) c.Deck.Add(CardLibrary.FireBolt);
            for (int i = 0; i < 4; i++) c.Deck.Add(CardLibrary.ShieldSpell);
            c.Deck.Add(CardLibrary.MagicMissile);
            c.Deck.Add(CardLibrary.ArcaneFocus);
            return c;
        }

        public static string AttrName(Attr a) { return a.ToString(); }
    }
}
