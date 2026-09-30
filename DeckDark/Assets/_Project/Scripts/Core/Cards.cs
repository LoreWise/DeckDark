using System.Collections.Generic;

namespace DeckDark.Core
{
    public enum CardKind { Attack, Defense, Skill, Power, Curse }
    public enum CardRarity { Basic, Common, Uncommon, Rare, Special }
    public enum CardIcon { Sword, Shield, Star, Dagger, Heart, Eye, Potion, Skull, Boot, Blood, Fist, Mouth, Die, Flame }

    /// <summary>Efeitos que pedem um teste de resistencia do alvo.</summary>
    public enum SaveEffect { None, Prone, Grapple, Frighten, Stun, Chill }
    public enum ConcKind { None, Burn, Ward, Haste }

    /// <summary>
    /// Definicao de uma carta. No prototipo as cartas sao criadas em codigo (CardLibrary).
    /// Mais para frente isso vira ScriptableObject, editavel no Inspector.
    /// </summary>
    public class CardDef
    {
        public string Id;
        public string Name;
        public CardKind Kind;
        public CardRarity Rarity = CardRarity.Common;
        public CardIcon Icon;
        public int Cost;
        public bool Unplayable;
        public bool Exhaust;          // sai do combate depois de usada

        // Ataque
        public int Hits;              // 0 = nao ataca
        public Attr AttackAttr = Attr.STR;
        public int AttackBonus;       // bonus no teste de acerto
        public DiceExpr Damage;
        public bool AttackAdvantage;
        public bool IgnoreBlock;
        public int ApplyBleed;        // aplica Bleed a cada acerto

        // Sangue
        public bool DoubleBleed;      // dobra o Bleed do alvo
        public int BleedBurst;        // causa (Bleed x N) de dano e remove o Bleed

        // Testes de resistencia
        public SaveEffect Save;
        public Attr SaveAttr = Attr.STR;
        public int SaveDc = 13;
        public bool AllEnemies;
        public bool SaveOnlyOnHit;    // Shield Bash: so pede o teste se o ataque acertar

        // Defesa e utilidade
        public int Block;
        public int ArmorThisTurn;
        public DiceExpr HealDice;     // Count 0 = sem cura
        public Attr HealAttr = Attr.CON;
        public int Draw;
        public bool GrantAdvantage;   // proximo ataque com vantagem
        public int WeakenEnemy;       // penalidade no proximo ataque do alvo
        public bool Riposte;          // revida quando um inimigo erra
        public int ForceNatural;      // proximo d20 sai com esse valor
        public bool RerollNextMiss;
        public bool ClearConditions;

        // Furia e custo em vida
        public bool ActivateRage;
        public int HpCost;
        public int EnergyGain;
        public bool FreeAtFury3;
        public bool SelfProne;

        // Mecanicas novas: critico, combo e teimosia
        public int Keen;              // Power: seus ataques sao criticos com N a menos (19-20, 18-20...)
        public int OnCritDraw;        // se for critico: compra cartas
        public int OnCritEnergy;      // se for critico: ganha energia
        public int OnCritDamage;      // se for critico: dano extra
        public int ComboDamage;       // +N de dano por ataque ja jogado neste turno
        public bool Stubborn;         // Power: ao errar um ataque, ganha 3 de bloqueio e vantagem no proximo

        // Magias do mago: o alvo rola o teste de resistencia contra a CD de magia
        public bool SpellDamage;      // dano de magia: SaveAttr contra a CD; passou = metade do dano
        public bool UseSpellDc;       // efeitos de Save usam a CD de magia do mago
        public int AutoHits;          // dardos que nunca erram (Magic Missile)
        public ConcKind Concentrate;  // magia de concentracao (so uma por vez)
        public int ConcValue;
        public int SpellFocus;        // proxima magia com CD maior

        public string Flavor;

        public bool NeedsTarget
        {
            get
            {
                if (AllEnemies) return false;
                return Hits > 0 || SpellDamage || AutoHits > 0 || DoubleBleed || BleedBurst > 0 || WeakenEnemy > 0 || Save != SaveEffect.None;
            }
        }

        /// <summary>Texto curto da carta, com os modificadores da ficha ja somados.</summary>
        public string Description(CharacterSheet sheet)
        {
            var parts = new List<string>();
            if (Unplayable) parts.Add("UNPLAYABLE.");
            if (HpCost > 0) parts.Add("LOSE " + HpCost + " HP.");
            if (EnergyGain > 0) parts.Add("+" + EnergyGain + " ENERGY.");
            if (Hits > 0)
            {
                int mod = sheet != null ? sheet.Mod(AttackAttr) : 0;
                var dmg = new DiceExpr(Damage.Count, Damage.Sides, Damage.Bonus + mod);
                string hit = "HIT " + Signed(mod + AttackBonus);
                if (AttackAdvantage) hit += " W/ ADVANTAGE";
                parts.Add(hit + ".");
                parts.Add((AllEnemies ? "ALL: " : "") + (Hits > 1 ? Hits + "X " : "") + dmg + " DMG" + (IgnoreBlock ? ", IGNORES BLOCK." : "."));
                if (ComboDamage > 0) parts.Add("COMBO +" + ComboDamage + ".");
                if (ApplyBleed > 0) parts.Add("BLEED " + ApplyBleed + ".");
                if (OnCritDamage > 0 || OnCritDraw > 0 || OnCritEnergy > 0)
                {
                    var oc = new List<string>();
                    if (OnCritDamage > 0) oc.Add("+" + OnCritDamage + " DMG");
                    if (OnCritEnergy > 0) oc.Add("+" + OnCritEnergy + " ENERGY");
                    if (OnCritDraw > 0) oc.Add("DRAW " + OnCritDraw);
                    parts.Add("ON CRIT: " + string.Join(", ", oc) + ".");
                }
            }
            if (SpellDamage)
            {
                int mod = sheet != null ? sheet.Mod(Attr.INT) : 0;
                var dmg = new DiceExpr(Damage.Count, Damage.Sides, Damage.Bonus + mod);
                parts.Add((AllEnemies ? "ALL: " : "") + SaveAttr + " SAVE" + (sheet != null ? " DC" + sheet.SpellDc : "") + ".");
                parts.Add(dmg + " DMG, HALF ON SAVE.");
            }
            if (AutoHits > 0) parts.Add(AutoHits + " DARTS, " + Damage + " EACH. NEVER MISS.");
            if (DoubleBleed) parts.Add("DOUBLE TARGET'S BLEED.");
            if (BleedBurst > 0) parts.Add("DEAL " + BleedBurst + "X TARGET'S BLEED, THEN CLEAR IT.");
            if (Block > 0) parts.Add("BLOCK " + Block + ".");
            if (Save != SaveEffect.None)
            {
                string what = Save == SaveEffect.Prone ? "PRONE" : (Save == SaveEffect.Grapple ? "GRAPPLED" : (Save == SaveEffect.Stun ? "STUNNED" : (Save == SaveEffect.Chill ? "-2 TO HIT" : "FRIGHTENED")));
                if (SpellDamage) parts.Add("FAILED: " + what + ".");
                else
                {
                    string who = AllEnemies ? "ALL: " : (SaveOnlyOnHit ? "ON HIT: " : "");
                    int dc = UseSpellDc && sheet != null ? sheet.SpellDc : SaveDc;
                    parts.Add(who + SaveAttr + (UseSpellDc && sheet == null ? " SAVE" : " DC" + dc) + " OR " + what + ".");
                }
            }
            if (ArmorThisTurn > 0) parts.Add("+" + ArmorThisTurn + " AC TILL YOUR TURN.");
            if (Riposte) parts.Add("ENEMY MISSES: HIT BACK 1d8+STR.");
            if (HealDice.Count > 0)
            {
                int mod = sheet != null ? sheet.Mod(HealAttr) : 0;
                parts.Add("HEAL " + new DiceExpr(HealDice.Count, HealDice.Sides, HealDice.Bonus + mod) + ".");
            }
            if (GrantAdvantage) parts.Add("NEXT ATTACK: ADVANTAGE.");
            if (WeakenEnemy > 0) parts.Add("TARGET -" + WeakenEnemy + " TO HIT.");
            if (ForceNatural > 0) parts.Add("NEXT d20 = " + ForceNatural + ".");
            if (RerollNextMiss) parts.Add("REROLL NEXT MISS.");
            if (ClearConditions) parts.Add("CLEAR YOUR CONDITIONS.");
            if (ActivateRage) parts.Add("LOSING HP GIVES 1 FURY.");
            if (FreeAtFury3) parts.Add("FREE AT 3 FURY.");
            if (SelfProne) parts.Add("YOU FALL PRONE.");
            if (Keen > 0) parts.Add("YOUR ATTACKS CRIT " + (Keen == 1 ? "ON 19-20" : "ON " + (20 - Keen) + "+") + ". STACKS.");
            if (Concentrate != ConcKind.None)
            {
                string cw = Concentrate == ConcKind.Burn ? "EACH TURN: " + ConcValue + " DMG TO ALL ENEMIES." :
                            Concentrate == ConcKind.Ward ? "EACH TURN: BLOCK " + ConcValue + "." : "EACH TURN: +" + ConcValue + " ENERGY.";
                parts.Add("CONCENTRATION. " + cw);
            }
            if (SpellFocus > 0) parts.Add("NEXT SPELL DC +" + SpellFocus + ".");
            if (Stubborn) parts.Add("WHEN YOU MISS: BLOCK 3 AND ADVANTAGE NEXT.");
            if (Draw > 0) parts.Add("DRAW " + Draw + ".");
            if (Exhaust) parts.Add("EXHAUST.");
            if (Kind == CardKind.Curse && !string.IsNullOrEmpty(Flavor)) parts.Add(Flavor);
            return string.Join(" ", parts);
        }

        /// <summary>Palavras-chave usadas no texto, para a dica ao passar o mouse.</summary>
        public List<string> Keywords()
        {
            var k = new List<string>();
            string d = Description(null);
            foreach (var kw in Glossary.Keys) if (d.Contains(kw)) k.Add(kw);
            return k;
        }

        static string Signed(int v) { return v >= 0 ? "+" + v : v.ToString(); }
    }

    /// <summary>Explicacao das palavras-chave, mostrada nas dicas das cartas.</summary>
    public static class Glossary
    {
        public static readonly string[] Keys = { "BLEED", "PRONE", "GRAPPLED", "FRIGHTENED", "POISONED", "FURY", "EXHAUST", "ADVANTAGE", "BLOCK", "CRIT", "COMBO", "CONCENTRATION", "SAVE", "STUNNED" };

        public static string Explain(string k)
        {
            switch (k)
            {
                case "BLEED": return "BLEED: LOSES THAT MUCH HP AT THE END OF ITS TURN, THEN BLEED GOES DOWN BY 1.";
                case "PRONE": return "PRONE: ATTACKS AGAINST IT HAVE ADVANTAGE. ITS OWN ATTACKS HAVE DISADVANTAGE. ENDS AFTER ITS NEXT TURN.";
                case "GRAPPLED": return "GRAPPLED: YOUR ATTACKS AGAINST IT HAVE ADVANTAGE. IT CAN'T BLOCK. LASTS 2 TURNS.";
                case "FRIGHTENED": return "FRIGHTENED: ITS ATTACKS HAVE DISADVANTAGE. LASTS 2 TURNS.";
                case "POISONED": return "POISONED: -2 ON ATTACK ROLLS.";
                case "FURY": return "FURY: EACH POINT ADDS +1 DAMAGE TO YOUR ATTACKS THIS COMBAT.";
                case "EXHAUST": return "EXHAUST: REMOVED UNTIL THE END OF COMBAT.";
                case "CONCENTRATION": return "CONCENTRATION: ONLY ONE AT A TIME. WHEN YOU TAKE DAMAGE, CON SAVE (DC 10) OR IT ENDS.";
                case "SAVE": return "SAVE: THE ENEMY ROLLS d20 + ITS SAVE BONUS AGAINST YOUR SPELL DC (8 + 2 + INT).";
                case "STUNNED": return "STUNNED: LOSES ITS NEXT TURN.";
                case "CRIT": return "CRIT: A NATURAL 20 (OR LOWER WITH KEEN EDGE). ALWAYS HITS AND ROLLS DAMAGE DICE TWICE.";
                case "COMBO": return "COMBO: EXTRA DAMAGE FOR EACH ATTACK YOU ALREADY PLAYED THIS TURN.";
                case "ADVANTAGE": return "ADVANTAGE: ROLL TWO d20 AND KEEP THE HIGHER.";
                default: return "BLOCK: ABSORBS DAMAGE UNTIL YOUR NEXT TURN.";
            }
        }
    }

    public static class CardLibrary
    {
        // ---------------- Deck inicial ----------------

        public static readonly CardDef SwordStrike = new CardDef
        {
            Id = "sword", Name = "SWORD STRIKE", Kind = CardKind.Attack, Rarity = CardRarity.Basic, Icon = CardIcon.Sword, Cost = 1,
            Hits = 1, Damage = new DiceExpr(1, 8, 0)
        };

        public static readonly CardDef RaiseShield = new CardDef
        {
            Id = "shield", Name = "RAISE SHIELD", Kind = CardKind.Defense, Rarity = CardRarity.Basic, Icon = CardIcon.Shield, Cost = 1,
            Block = 4
        };

        public static readonly CardDef BrutalStrike = new CardDef
        {
            Id = "brutal", Name = "BRUTAL STRIKE", Kind = CardKind.Attack, Rarity = CardRarity.Basic, Icon = CardIcon.Sword, Cost = 2,
            Hits = 1, Damage = new DiceExpr(2, 6, 0), AttackAdvantage = true
        };

        public static readonly CardDef Inspiration = new CardDef
        {
            Id = "inspire", Name = "INSPIRATION", Kind = CardKind.Skill, Rarity = CardRarity.Basic, Icon = CardIcon.Star, Cost = 0,
            GrantAdvantage = true
        };

        // ---------------- Comuns ----------------

        public static readonly CardDef ThrowDagger = new CardDef
        {
            Id = "dagger", Name = "THROWN DAGGER", Kind = CardKind.Attack, Icon = CardIcon.Dagger, Cost = 1,
            Hits = 1, AttackAttr = Attr.DEX, Damage = new DiceExpr(1, 4, 0), Draw = 1
        };

        public static readonly CardDef Charge = new CardDef
        {
            Id = "charge", Name = "CHARGE", Kind = CardKind.Attack, Icon = CardIcon.Boot, Cost = 2,
            Hits = 1, AttackBonus = 2, Damage = new DiceExpr(1, 12, 0)
        };

        public static readonly CardDef DefensiveStance = new CardDef
        {
            Id = "stance", Name = "DEFENSIVE STANCE", Kind = CardKind.Defense, Icon = CardIcon.Shield, Cost = 1,
            ArmorThisTurn = 3, Block = 3
        };

        public static readonly CardDef DoubleStrike = new CardDef
        {
            Id = "double", Name = "DOUBLE STRIKE", Kind = CardKind.Attack, Icon = CardIcon.Sword, Cost = 2,
            Hits = 2, Damage = new DiceExpr(1, 6, 0)
        };

        public static readonly CardDef Taunt = new CardDef
        {
            Id = "taunt", Name = "TAUNT", Kind = CardKind.Skill, Icon = CardIcon.Mouth, Cost = 1,
            WeakenEnemy = 4, Block = 3
        };

        public static readonly CardDef SerratedBlade = new CardDef
        {
            Id = "serrated", Name = "SERRATED BLADE", Kind = CardKind.Attack, Icon = CardIcon.Blood, Cost = 1,
            Hits = 1, Damage = new DiceExpr(1, 6, 0), ApplyBleed = 3
        };

        public static readonly CardDef RecklessSwing = new CardDef
        {
            Id = "reckless", Name = "RECKLESS SWING", Kind = CardKind.Attack, Icon = CardIcon.Sword, Cost = 1,
            Hits = 1, Damage = new DiceExpr(2, 8, 0), SelfProne = true
        };

        public static readonly CardDef ShieldBash = new CardDef
        {
            Id = "bash", Name = "SHIELD BASH", Kind = CardKind.Attack, Icon = CardIcon.Shield, Cost = 1,
            Hits = 1, Damage = new DiceExpr(1, 4, 0), Block = 3,
            Save = SaveEffect.Prone, SaveAttr = Attr.STR, SaveDc = 13, SaveOnlyOnHit = true
        };

        public static readonly CardDef Grapple = new CardDef
        {
            Id = "grapple", Name = "GRAPPLE", Kind = CardKind.Skill, Icon = CardIcon.Fist, Cost = 1,
            Save = SaveEffect.Grapple, SaveAttr = Attr.STR, SaveDc = 13
        };

        public static readonly CardDef SteadyBreath = new CardDef
        {
            Id = "steady", Name = "STEADY BREATH", Kind = CardKind.Skill, Icon = CardIcon.Die, Cost = 0,
            ForceNatural = 10
        };

        public static readonly CardDef SecondChance = new CardDef
        {
            Id = "chance", Name = "SECOND CHANCE", Kind = CardKind.Skill, Icon = CardIcon.Die, Cost = 1,
            RerollNextMiss = true, Draw = 1
        };

        public static readonly CardDef TakeABreather = new CardDef
        {
            Id = "breather", Name = "TAKE A BREATHER", Kind = CardKind.Skill, Icon = CardIcon.Heart, Cost = 1,
            Draw = 2, ClearConditions = true
        };

        // ---------------- Incomuns ----------------

        public static readonly CardDef SecondWind = new CardDef
        {
            Id = "wind", Name = "SECOND WIND", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Heart, Cost = 1,
            HealDice = new DiceExpr(1, 10, 0), Exhaust = true
        };

        public static readonly CardDef OpenWounds = new CardDef
        {
            Id = "wounds", Name = "OPEN WOUNDS", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Blood, Cost = 1,
            DoubleBleed = true
        };

        public static readonly CardDef Hemorrhage = new CardDef
        {
            Id = "hemorrhage", Name = "HEMORRHAGE", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Blood, Cost = 2,
            BleedBurst = 2
        };

        public static readonly CardDef Rage = new CardDef
        {
            Id = "rage", Name = "RAGE", Kind = CardKind.Power, Rarity = CardRarity.Uncommon, Icon = CardIcon.Flame, Cost = 1,
            ActivateRage = true, Exhaust = true
        };

        public static readonly CardDef BloodForBlood = new CardDef
        {
            Id = "bloodforblood", Name = "BLOOD FOR BLOOD", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Blood, Cost = 0,
            HpCost = 4, EnergyGain = 2
        };

        public static readonly CardDef Unstoppable = new CardDef
        {
            Id = "unstoppable", Name = "UNSTOPPABLE", Kind = CardKind.Attack, Rarity = CardRarity.Uncommon, Icon = CardIcon.Flame, Cost = 2,
            Hits = 1, Damage = new DiceExpr(1, 10, 0), IgnoreBlock = true, FreeAtFury3 = true
        };

        public static readonly CardDef WarCry = new CardDef
        {
            Id = "warcry", Name = "WAR CRY", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Mouth, Cost = 1,
            Save = SaveEffect.Frighten, SaveAttr = Attr.WIS, SaveDc = 12, AllEnemies = true, Exhaust = true
        };

        public static readonly CardDef Riposte = new CardDef
        {
            Id = "riposte", Name = "RIPOSTE", Kind = CardKind.Defense, Rarity = CardRarity.Uncommon, Icon = CardIcon.Sword, Cost = 1,
            Block = 4, Riposte = true
        };

        // ---------------- Raras ----------------

        public static readonly CardDef LoadedDie = new CardDef
        {
            Id = "loaded", Name = "LOADED DIE", Kind = CardKind.Skill, Rarity = CardRarity.Rare, Icon = CardIcon.Die, Cost = 1,
            ForceNatural = 20, Exhaust = true
        };

        // ---------------- Especiais (eventos e maldicoes) ----------------

        public static readonly CardDef HealingPotion = new CardDef
        {
            Id = "potion", Name = "HEALING POTION", Kind = CardKind.Skill, Rarity = CardRarity.Special, Icon = CardIcon.Potion, Cost = 0,
            HealDice = new DiceExpr(2, 4, 2), Exhaust = true
        };

        public static readonly CardDef Lucidity = new CardDef
        {
            Id = "lucid", Name = "LUCIDITY", Kind = CardKind.Skill, Rarity = CardRarity.Special, Icon = CardIcon.Eye, Cost = 0,
            Draw = 2, Exhaust = true
        };

        public static readonly CardDef Nightmare = new CardDef
        {
            Id = "nightmare", Name = "NIGHTMARE", Kind = CardKind.Curse, Rarity = CardRarity.Special, Icon = CardIcon.Skull, Cost = 0,
            Unplayable = true, Flavor = "HE KNOWS YOUR NAME."
        };

        // ---------------- Critico, combo e teimosia ----------------

        public static readonly CardDef QuickJab = new CardDef
        {
            Id = "jab", Name = "QUICK JAB", Kind = CardKind.Attack, Icon = CardIcon.Fist, Cost = 0,
            Hits = 1, AttackAttr = Attr.DEX, AttackBonus = 1, Damage = new DiceExpr(1, 4, 0)
        };

        public static readonly CardDef PreciseCut = new CardDef
        {
            Id = "precise", Name = "PRECISE CUT", Kind = CardKind.Attack, Icon = CardIcon.Dagger, Cost = 1,
            Hits = 1, AttackAttr = Attr.DEX, Damage = new DiceExpr(1, 6, 0), OnCritDraw = 2, OnCritEnergy = 1
        };

        public static readonly CardDef Finisher = new CardDef
        {
            Id = "finisher", Name = "FLURRY FINISHER", Kind = CardKind.Attack, Icon = CardIcon.Sword, Cost = 1,
            Hits = 1, Damage = new DiceExpr(1, 6, 0), ComboDamage = 3
        };

        public static readonly CardDef Whirlwind = new CardDef
        {
            Id = "whirl", Name = "WHIRLWIND", Kind = CardKind.Attack, Rarity = CardRarity.Uncommon, Icon = CardIcon.Sword, Cost = 2,
            Hits = 1, AllEnemies = true, Damage = new DiceExpr(1, 8, 0)
        };

        public static readonly CardDef KeenEdge = new CardDef
        {
            Id = "keen", Name = "KEEN EDGE", Kind = CardKind.Power, Rarity = CardRarity.Uncommon, Icon = CardIcon.Eye, Cost = 1,
            Keen = 1
        };

        public static readonly CardDef StubbornCard = new CardDef
        {
            Id = "stubborn", Name = "STUBBORN", Kind = CardKind.Power, Rarity = CardRarity.Uncommon, Icon = CardIcon.Shield, Cost = 1,
            Stubborn = true
        };

        public static readonly CardDef Executioner = new CardDef
        {
            Id = "exec", Name = "EXECUTIONER", Kind = CardKind.Attack, Rarity = CardRarity.Rare, Icon = CardIcon.Skull, Cost = 2,
            Hits = 1, Damage = new DiceExpr(1, 12, 0), OnCritDamage = 12
        };

        public static readonly CardDef CalledShot = new CardDef
        {
            Id = "called", Name = "CALLED SHOT", Kind = CardKind.Skill, Rarity = CardRarity.Rare, Icon = CardIcon.Die, Cost = 1,
            ForceNatural = 19, Draw = 1, Exhaust = true
        };

        // ================= MAGO =================

        public static readonly CardDef FireBolt = new CardDef
        {
            Id = "firebolt", Name = "FIRE BOLT", Kind = CardKind.Attack, Rarity = CardRarity.Basic, Icon = CardIcon.Flame, Cost = 1,
            SpellDamage = true, SaveAttr = Attr.DEX, Damage = new DiceExpr(1, 8, 0)
        };
        public static readonly CardDef ShieldSpell = new CardDef
        {
            Id = "shieldspell", Name = "SHIELD", Kind = CardKind.Defense, Rarity = CardRarity.Basic, Icon = CardIcon.Shield, Cost = 1,
            Block = 5
        };
        public static readonly CardDef MagicMissile = new CardDef
        {
            Id = "missile", Name = "MAGIC MISSILE", Kind = CardKind.Attack, Rarity = CardRarity.Basic, Icon = CardIcon.Star, Cost = 1,
            AutoHits = 3, Damage = new DiceExpr(1, 4, 0)
        };
        public static readonly CardDef ArcaneFocus = new CardDef
        {
            Id = "focus", Name = "ARCANE FOCUS", Kind = CardKind.Skill, Rarity = CardRarity.Basic, Icon = CardIcon.Eye, Cost = 0,
            SpellFocus = 3, Draw = 1
        };

        public static readonly CardDef BurningHands = new CardDef
        {
            Id = "burning", Name = "BURNING HANDS", Kind = CardKind.Attack, Icon = CardIcon.Flame, Cost = 1,
            SpellDamage = true, AllEnemies = true, SaveAttr = Attr.DEX, Damage = new DiceExpr(2, 4, 0)
        };
        public static readonly CardDef RayOfFrost = new CardDef
        {
            Id = "frost", Name = "RAY OF FROST", Kind = CardKind.Attack, Icon = CardIcon.Star, Cost = 1,
            SpellDamage = true, SaveAttr = Attr.CON, Damage = new DiceExpr(1, 8, 0), Save = SaveEffect.Chill
        };
        public static readonly CardDef ChromaticOrb = new CardDef
        {
            Id = "orb", Name = "CHROMATIC ORB", Kind = CardKind.Attack, Icon = CardIcon.Potion, Cost = 1,
            Hits = 1, AttackAttr = Attr.INT, Damage = new DiceExpr(2, 6, 0)
        };
        public static readonly CardDef MistyStep = new CardDef
        {
            Id = "misty", Name = "MISTY STEP", Kind = CardKind.Defense, Icon = CardIcon.Boot, Cost = 0,
            Block = 3, Draw = 1
        };
        public static readonly CardDef Thunderwave = new CardDef
        {
            Id = "thunder", Name = "THUNDERWAVE", Kind = CardKind.Attack, Rarity = CardRarity.Uncommon, Icon = CardIcon.Fist, Cost = 2,
            SpellDamage = true, AllEnemies = true, SaveAttr = Attr.CON, Damage = new DiceExpr(2, 6, 0), Save = SaveEffect.Prone
        };
        public static readonly CardDef ScorchingRay = new CardDef
        {
            Id = "scorch", Name = "SCORCHING RAY", Kind = CardKind.Attack, Rarity = CardRarity.Uncommon, Icon = CardIcon.Flame, Cost = 2,
            Hits = 3, AttackAttr = Attr.INT, Damage = new DiceExpr(1, 6, 0)
        };
        public static readonly CardDef FlamingSphere = new CardDef
        {
            Id = "sphere", Name = "FLAMING SPHERE", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Flame, Cost = 2,
            Concentrate = ConcKind.Burn, ConcValue = 4
        };
        public static readonly CardDef MirrorImage = new CardDef
        {
            Id = "mirror", Name = "MIRROR IMAGE", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Eye, Cost = 1,
            Block = 3, Concentrate = ConcKind.Ward, ConcValue = 5
        };
        public static readonly CardDef HoldPerson = new CardDef
        {
            Id = "hold", Name = "HOLD PERSON", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Skull, Cost = 2,
            Save = SaveEffect.Stun, SaveAttr = Attr.WIS, UseSpellDc = true
        };
        public static readonly CardDef ArcaneRecovery = new CardDef
        {
            Id = "recovery", Name = "ARCANE RECOVERY", Kind = CardKind.Skill, Rarity = CardRarity.Uncommon, Icon = CardIcon.Potion, Cost = 0,
            EnergyGain = 2, Draw = 1, Exhaust = true
        };
        public static readonly CardDef Fireball = new CardDef
        {
            Id = "fireball", Name = "FIREBALL", Kind = CardKind.Attack, Rarity = CardRarity.Rare, Icon = CardIcon.Flame, Cost = 3,
            SpellDamage = true, AllEnemies = true, SaveAttr = Attr.DEX, Damage = new DiceExpr(6, 6, 0)
        };
        public static readonly CardDef ChainLightning = new CardDef
        {
            Id = "chain", Name = "CHAIN LIGHTNING", Kind = CardKind.Attack, Rarity = CardRarity.Rare, Icon = CardIcon.Star, Cost = 2,
            SpellDamage = true, AllEnemies = true, SaveAttr = Attr.DEX, Damage = new DiceExpr(3, 8, 0), Exhaust = true
        };
        public static readonly CardDef HasteSpell = new CardDef
        {
            Id = "haste", Name = "HASTE", Kind = CardKind.Skill, Rarity = CardRarity.Rare, Icon = CardIcon.Boot, Cost = 2,
            Concentrate = ConcKind.Haste, ConcValue = 1
        };

        public static readonly CardDef[] WizardPool =
        {
            BurningHands, RayOfFrost, ChromaticOrb, MistyStep, QuickJab,
            Thunderwave, ScorchingRay, FlamingSphere, MirrorImage, HoldPerson, ArcaneRecovery, KeenEdge,
            Fireball, ChainLightning, HasteSpell, CalledShot, LoadedDie,
        };

        public static CardDef[] PoolFor(PlayerClass c) { return c == PlayerClass.Wizard ? WizardPool : RewardPool; }

        /// <summary>Cartas que podem aparecer como recompensa.</summary>
        public static readonly CardDef[] RewardPool =
        {
            ThrowDagger, Charge, DefensiveStance, DoubleStrike, Taunt, SerratedBlade, RecklessSwing, ShieldBash,
            Grapple, SteadyBreath, SecondChance, TakeABreather,
            SecondWind, OpenWounds, Hemorrhage, Rage, BloodForBlood, Unstoppable, WarCry, Riposte, BrutalStrike,
            LoadedDie,
            QuickJab, PreciseCut, Finisher, Whirlwind, KeenEdge, StubbornCard, Executioner, CalledShot,
        };
    }
}
