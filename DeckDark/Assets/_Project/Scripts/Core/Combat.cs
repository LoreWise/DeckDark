using System.Collections.Generic;

namespace DeckDark.Core
{
    public class AttackResult
    {
        public TestRoll Roll;
        public int Damage;
        public int Blocked;
    }

    public class CardResult
    {
        public CardDef Card;
        public readonly List<AttackResult> Attacks = new List<AttackResult>();
        public int BlockGained;
        public int Healed;
        public int Drawn;
    }

    public class EnemyTurnResult
    {
        public EnemyMove Move;
        public TestRoll Roll;
        public bool Hidden;          // o mestre rolou atras do escudo
        public bool Cheated;         // e mentiu o resultado
        public int AnnouncedTotal;
        public bool Hit;
        public int Damage;
        public int Blocked;
        public bool AddedCurse;
        public int GuardGained;
    }

    /// <summary>
    /// Regras de um combate contra um unico inimigo.
    /// Nao sabe nada de tela: so muda numeros e devolve o que aconteceu.
    /// </summary>
    public class Combat
    {
        public readonly CharacterSheet Sheet;
        public readonly EnemyDef Enemy;
        readonly Dice dice;

        public int EnemyHp;
        public int EnemyBlock;
        public int EnemyArmorClass;
        public int MoveIndex;
        public int EnemyWeaken;

        public int Energy;
        public int Block;
        public int ArmorBuff;
        public bool NextAttackAdvantage;
        bool firstAttackDone;
        public int Turn;

        public readonly List<CardDef> DrawPile = new List<CardDef>();
        public readonly List<CardDef> Hand = new List<CardDef>();
        public readonly List<CardDef> Discard = new List<CardDef>();
        public readonly List<CardDef> Exhausted = new List<CardDef>();

        public Combat(CharacterSheet sheet, EnemyDef enemy, Dice dice, int enemyArmorBonus)
        {
            Sheet = sheet;
            Enemy = enemy;
            this.dice = dice;
            EnemyHp = enemy.MaxHp;
            EnemyArmorClass = enemy.ArmorClass + enemyArmorBonus;
            DrawPile.AddRange(sheet.Deck);
            Shuffle(DrawPile);
        }

        public bool EnemyDead { get { return EnemyHp <= 0; } }
        public bool PlayerDead { get { return Sheet.Hp <= 0; } }
        public bool Over { get { return EnemyDead || PlayerDead; } }
        public int PlayerArmorClass { get { return Sheet.ArmorClass + ArmorBuff; } }
        public EnemyMove Intent { get { return Enemy.Pattern[MoveIndex % Enemy.Pattern.Length]; } }

        /// <summary>O chefe passa a rolar escondido quando esta com metade da vida.</summary>
        public bool HiddenRolls { get { return Enemy.IsBoss && EnemyHp <= Enemy.MaxHp / 2; } }

        public void StartPlayerTurn()
        {
            Turn++;
            Block = 0;
            ArmorBuff = 0;
            Energy = Sheet.EnergyPerTurn;
            DrawCards(Sheet.HandSize);
        }

        public int DrawCards(int n)
        {
            int drawn = 0;
            for (int i = 0; i < n; i++)
            {
                if (DrawPile.Count == 0)
                {
                    if (Discard.Count == 0) break;
                    DrawPile.AddRange(Discard);
                    Discard.Clear();
                    Shuffle(DrawPile);
                }
                if (Hand.Count >= 10) break;
                Hand.Add(DrawPile[DrawPile.Count - 1]);
                DrawPile.RemoveAt(DrawPile.Count - 1);
                drawn++;
            }
            return drawn;
        }

        public bool CanPlay(CardDef c)
        {
            return !Over && !c.Unplayable && Energy >= c.Cost;
        }

        public CardResult Play(int handIndex)
        {
            var card = Hand[handIndex];
            if (!CanPlay(card)) return null;

            Energy -= card.Cost;
            Hand.RemoveAt(handIndex);
            var res = new CardResult { Card = card };

            for (int h = 0; h < card.Hits && !EnemyDead; h++)
            {
                bool adv = card.AttackAdvantage || NextAttackAdvantage;
                if (!firstAttackDone && Sheet.HasRelic(RelicId.PetDie)) adv = true;
                NextAttackAdvantage = false;
                firstAttackDone = true;

                var ar = new AttackResult();
                int mod = Sheet.Mod(card.AttackAttr);
                ar.Roll = dice.Test(mod + card.AttackBonus, EnemyArmorClass, adv, false);
                if (ar.Roll.Success)
                {
                    var dmg = new DiceExpr(card.Damage.Count, card.Damage.Sides, card.Damage.Bonus + mod);
                    int raw = dice.Roll(dmg, ar.Roll.IsCrit);
                    int absorbed = System.Math.Min(EnemyBlock, raw);
                    EnemyBlock -= absorbed;
                    ar.Blocked = absorbed;
                    ar.Damage = raw - absorbed;
                    EnemyHp -= ar.Damage;
                    if (EnemyHp < 0) EnemyHp = 0;
                }
                res.Attacks.Add(ar);
            }

            if (card.Block > 0) { Block += card.Block; res.BlockGained = card.Block; }
            if (card.ArmorThisTurn > 0) ArmorBuff += card.ArmorThisTurn;
            if (card.HealDice.Count > 0)
            {
                var e = new DiceExpr(card.HealDice.Count, card.HealDice.Sides, card.HealDice.Bonus + Sheet.Mod(card.HealAttr));
                int before = Sheet.Hp;
                Sheet.Heal(dice.Roll(e, false));
                res.Healed = Sheet.Hp - before;
            }
            if (card.GrantAdvantage) NextAttackAdvantage = true;
            if (card.WeakenEnemy > 0) EnemyWeaken += card.WeakenEnemy;
            if (card.Draw > 0) res.Drawn = DrawCards(card.Draw);

            if (card.Exhaust) Exhausted.Add(card); else Discard.Add(card);
            return res;
        }

        public void DiscardHand()
        {
            Discard.AddRange(Hand);
            Hand.Clear();
        }

        public EnemyTurnResult EnemyAct()
        {
            var move = Intent;
            var r = new EnemyTurnResult { Move = move };
            EnemyBlock = 0;

            switch (move.Kind)
            {
                case MoveKind.Attack:
                {
                    r.Hidden = HiddenRolls;
                    r.Roll = dice.Test(move.AttackBonus - EnemyWeaken, PlayerArmorClass, false, false);
                    EnemyWeaken = 0;
                    r.Hit = r.Roll.Success;
                    r.AnnouncedTotal = r.Roll.Total;
                    // Atras do escudo, o mestre "ajusta" as rolagens que erraram por pouco.
                    if (r.Hidden && !r.Hit && !r.Roll.IsFumble && r.Roll.Target - r.Roll.Total <= 3)
                    {
                        r.Cheated = true;
                        r.Hit = true;
                        r.AnnouncedTotal = r.Roll.Target;
                    }
                    if (r.Hit)
                    {
                        int raw = dice.Roll(move.Damage, r.Roll.IsCrit);
                        int absorbed = System.Math.Min(Block, raw);
                        Block -= absorbed;
                        r.Blocked = absorbed;
                        r.Damage = raw - absorbed;
                        Sheet.Hp -= r.Damage;
                        if (Sheet.Hp < 0) Sheet.Hp = 0;
                    }
                    break;
                }
                case MoveKind.Curse:
                    Discard.Add(CardLibrary.Nightmare);
                    r.AddedCurse = true;
                    break;
                case MoveKind.Guard:
                    EnemyBlock += move.Guard;
                    r.GuardGained = move.Guard;
                    break;
            }

            MoveIndex++;
            return r;
        }

        void Shuffle(List<CardDef> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = dice.Range(0, i + 1);
                var t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
