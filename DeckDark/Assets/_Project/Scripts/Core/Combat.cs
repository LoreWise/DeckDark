using System;
using System.Collections.Generic;

namespace DeckDark.Core
{
    /// <summary>Condicoes no estilo D&D. Valem para o jogador e para os inimigos.</summary>
    public class Conditions
    {
        public int Bleed;
        public int Frightened;   // turnos
        public int Poisoned;     // turnos
        public int Grappled;     // turnos
        public bool Prone;
        public bool Stunned;

        public bool Any { get { return Bleed > 0 || Frightened > 0 || Poisoned > 0 || Grappled > 0 || Prone || Stunned; } }

        public void Clear()
        {
            Bleed = Frightened = Poisoned = Grappled = 0;
            Prone = Stunned = false;
        }
    }

    public class EnemyUnit
    {
        public EnemyDef Def;
        public int Hp;
        public int Block;
        public int ArmorClass;
        public int MoveIndex;
        public int Weaken;
        public readonly Conditions C = new Conditions();
        public bool Gone;          // morreu ou fugiu
        public bool Fled;
        public bool Reassembled;
        public bool Summoned;

        public bool Alive { get { return !Gone && Hp > 0; } }
        public EnemyMove Intent { get { return Def.Pattern[MoveIndex % Def.Pattern.Length]; } }
    }

    public enum Tone { Neutral, Bad, Good, Gold, Blue, Purple }
    public enum EvType { Actor, Roll, Hit, Miss, Block, Heal, Status, Curse, Flee, Rise, Summon, Die, Say, Draw, Energy }

    /// <summary>
    /// Uma coisa que aconteceu no combate. A logica decide tudo de uma vez;
    /// a tela reproduz estes eventos um por um, com animacao.
    /// Target: -1 = jogador, 0 ou mais = indice do inimigo.
    /// </summary>
    public class CombatEvent
    {
        public EvType Type;
        public int Target = -1;
        public int Actor = -1;
        public TestRoll Roll;
        public string Label;
        public bool Hidden;
        public int Announced;
        public bool Cheated;
        public int Amount;
        public int Blocked;
        public int HpAfter;
        public int BlockAfter;
        public bool Crit;
        public string Text;
        public Tone Tone;
    }

    /// <summary>
    /// Regras de um combate contra um ou mais inimigos.
    /// Nao sabe nada de tela: muda numeros e devolve a lista do que aconteceu.
    /// </summary>
    public class Combat
    {
        public readonly CharacterSheet Sheet;
        readonly Dice dice;
        readonly int armorBonus;

        public readonly List<EnemyUnit> Enemies = new List<EnemyUnit>();

        public int Energy;
        public int Block;
        public int ArmorBuff;
        public int Turn;
        public readonly Conditions PC = new Conditions();
        public int Fury;
        public bool RageActive;
        public bool NextAttackAdvantage;
        public int ForcedNatural;
        public bool RerollNextMiss;
        public bool RiposteActive;
        public bool EnemyCanReroll;
        bool luckyCoinUsed;
        bool firstAttackDone;
        public int Keen;               // critico com N a menos
        public bool StubbornActive;
        public int AttacksThisTurn;
        public ConcKind Conc;          // magia de concentracao ativa
        public int ConcValue;
        public string ConcName;
        public int SpellFocusBonus;

        public readonly List<CardDef> DrawPile = new List<CardDef>();
        public readonly List<CardDef> Hand = new List<CardDef>();
        public readonly List<CardDef> Discard = new List<CardDef>();
        public readonly List<CardDef> Exhausted = new List<CardDef>();

        public Combat(CharacterSheet sheet, EnemyDef[] encounter, Dice dice, int enemyArmorBonus)
        {
            Sheet = sheet;
            this.dice = dice;
            armorBonus = enemyArmorBonus;
            foreach (var d in encounter) AddEnemy(d, false);
            DrawPile.AddRange(sheet.Deck);
            Shuffle(DrawPile);
        }

        EnemyUnit AddEnemy(EnemyDef d, bool summoned)
        {
            var e = new EnemyUnit { Def = d, Hp = d.MaxHp, ArmorClass = d.ArmorClass + armorBonus, Summoned = summoned };
            Enemies.Add(e);
            return e;
        }

        public bool PlayerDead { get { return Sheet.Hp <= 0; } }
        public bool EnemiesDefeated
        {
            get
            {
                foreach (var e in Enemies) if (e.Alive) return false;
                return true;
            }
        }
        public bool Over { get { return PlayerDead || EnemiesDefeated; } }
        public int PlayerArmorClass { get { return Sheet.ArmorClass + ArmorBuff; } }
        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (var e in Enemies) if (e.Alive) n++;
                return n;
            }
        }

        /// <summary>Com o chefe pela metade da vida, o mestre passa a rolar escondido.</summary>
        public bool HiddenRolls
        {
            get
            {
                foreach (var e in Enemies) if (e.Alive && e.Def.IsBoss && e.Hp <= e.Def.MaxHp / 2) return true;
                return false;
            }
        }

        public int CurseCount
        {
            get
            {
                int n = 0;
                foreach (var c in DrawPile) if (c.Kind == CardKind.Curse) n++;
                foreach (var c in Hand) if (c.Kind == CardKind.Curse) n++;
                foreach (var c in Discard) if (c.Kind == CardKind.Curse) n++;
                return n;
            }
        }

        public int CostOf(CardDef c) { return c.FreeAtFury3 && Fury >= 3 ? 0 : c.Cost; }

        public bool CanPlay(CardDef c)
        {
            return !Over && !c.Unplayable && Energy >= CostOf(c) && Sheet.Hp > c.HpCost;
        }

        public int FirstAliveEnemy()
        {
            for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Alive) return i;
            return -1;
        }

        // =====================================================================
        // Turno do jogador
        // =====================================================================

        public List<CombatEvent> StartCombat()
        {
            var ev = new List<CombatEvent>();
            StartPlayerTurn(ev);
            if (Sheet.HasRelic(RelicId.IronFlask))
            {
                Block += 6;
                ev.Add(new CombatEvent { Type = EvType.Block, Target = -1, Amount = 6, BlockAfter = Block });
            }
            return ev;
        }

        public void StartPlayerTurn(List<CombatEvent> ev)
        {
            Turn++;
            Block = 0;
            ArmorBuff = 0;
            RiposteActive = false;
            AttacksThisTurn = 0;
            if (PC.Prone) { PC.Prone = false; ev.Add(Status(-1, "YOU STAND UP", Tone.Neutral)); }
            Energy = Sheet.EnergyPerTurn;
            if (Conc == ConcKind.Ward) { Block += ConcValue; ev.Add(new CombatEvent { Type = EvType.Block, Target = -1, Amount = ConcValue, BlockAfter = Block }); }
            if (Conc == ConcKind.Haste) { Energy += ConcValue; ev.Add(new CombatEvent { Type = EvType.Energy, Amount = ConcValue }); }
            if (Conc == ConcKind.Burn)
                for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Alive) DamageEnemy(i, ConcValue, false, false, "FLAMES", ev);
            int n = DrawCards(Sheet.HandSize);
            ev.Add(new CombatEvent { Type = EvType.Draw, Amount = n });
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

        public List<CombatEvent> Play(int handIndex, int target)
        {
            var card = Hand[handIndex];
            if (!CanPlay(card)) return null;
            var ev = new List<CombatEvent>();

            Energy -= CostOf(card);
            Hand.RemoveAt(handIndex);
            if (card.NeedsTarget && (target < 0 || target >= Enemies.Count || !Enemies[target].Alive)) target = FirstAliveEnemy();
            var t = target >= 0 ? Enemies[target] : null;

            if (card.HpCost > 0) LosePlayerHp(card.HpCost, "BLOOD", ev);
            if (card.EnergyGain > 0)
            {
                Energy += card.EnergyGain;
                ev.Add(new CombatEvent { Type = EvType.Energy, Amount = card.EnergyGain });
            }

            if (card.Hits > 0 && card.AllEnemies)
            {
                for (int i = 0; i < Enemies.Count; i++)
                    for (int h = 0; h < card.Hits && Enemies[i].Alive; h++) PlayerAttack(i, card, ev);
            }
            else
                for (int h = 0; h < card.Hits && t != null && t.Alive; h++) PlayerAttack(target, card, ev);
            if (card.SpellDamage)
            {
                if (card.AllEnemies) { for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Alive) SpellHit(i, card, ev); }
                else if (t != null && t.Alive) SpellHit(target, card, ev);
                SpellFocusBonus = 0;
            }
            for (int h = 0; h < card.AutoHits && t != null && t.Alive; h++)
                DamageEnemy(target, dice.Roll(card.Damage, false), false, false, "DART", ev);
            if (card.Hits > 0 || card.SpellDamage || card.AutoHits > 0) AttacksThisTurn++;
            if (card.SpellFocus > 0) { SpellFocusBonus += card.SpellFocus; ev.Add(Status(-1, "SPELL DC " + (Sheet.SpellDc + SpellFocusBonus), Tone.Blue)); }
            if (card.Concentrate != ConcKind.None)
            {
                if (Conc != ConcKind.None) ev.Add(Status(-1, ConcName + " ENDS", Tone.Neutral));
                Conc = card.Concentrate; ConcValue = card.ConcValue; ConcName = card.Name;
                ev.Add(Status(-1, "CONCENTRATING: " + card.Name, Tone.Blue));
            }

            if (card.Keen > 0) { Keen += card.Keen; ev.Add(Status(-1, "CRIT ON " + (20 - Keen) + "+", Tone.Gold)); }
            if (card.Stubborn) { StubbornActive = true; ev.Add(Status(-1, "STUBBORN", Tone.Blue)); }

            if (card.Save != SaveEffect.None && !card.SaveOnlyOnHit && !card.SpellDamage)
            {
                int dc = card.UseSpellDc ? Sheet.SpellDc + SpellFocusBonus : card.SaveDc;
                if (card.UseSpellDc) SpellFocusBonus = 0;
                if (card.AllEnemies)
                {
                    for (int i = 0; i < Enemies.Count; i++) if (Enemies[i].Alive) EnemySave(i, card.Save, card.SaveAttr, dc, ev);
                }
                else if (t != null && t.Alive) EnemySave(target, card.Save, card.SaveAttr, dc, ev);
            }

            if (card.DoubleBleed && t != null && t.Alive)
            {
                t.C.Bleed *= 2;
                ev.Add(Status(target, "BLEED " + t.C.Bleed, Tone.Bad));
            }
            if (card.BleedBurst > 0 && t != null && t.Alive)
            {
                int dmg = t.C.Bleed * card.BleedBurst;
                t.C.Bleed = 0;
                if (dmg > 0) DamageEnemy(target, dmg, true, false, "HEMORRHAGE", ev);
                else ev.Add(Status(target, "NO BLEED", Tone.Neutral));
            }

            if (card.Block > 0)
            {
                Block += card.Block;
                ev.Add(new CombatEvent { Type = EvType.Block, Target = -1, Amount = card.Block, BlockAfter = Block });
            }
            if (card.ArmorThisTurn > 0)
            {
                ArmorBuff += card.ArmorThisTurn;
                ev.Add(Status(-1, "+" + card.ArmorThisTurn + " AC", Tone.Blue));
            }
            if (card.Riposte) { RiposteActive = true; ev.Add(Status(-1, "RIPOSTE READY", Tone.Blue)); }
            if (card.HealDice.Count > 0)
            {
                var e = new DiceExpr(card.HealDice.Count, card.HealDice.Sides, card.HealDice.Bonus + Sheet.Mod(card.HealAttr));
                int before = Sheet.Hp;
                Sheet.Heal(dice.Roll(e, false));
                ev.Add(new CombatEvent { Type = EvType.Heal, Target = -1, Amount = Sheet.Hp - before, HpAfter = Sheet.Hp });
            }
            if (card.GrantAdvantage) { NextAttackAdvantage = true; ev.Add(Status(-1, "ADVANTAGE!", Tone.Gold)); }
            if (card.WeakenEnemy > 0 && t != null && t.Alive)
            {
                t.Weaken += card.WeakenEnemy;
                ev.Add(Status(target, "-" + card.WeakenEnemy + " TO HIT", Tone.Gold));
            }
            if (card.ForceNatural > 0) { ForcedNatural = card.ForceNatural; ev.Add(Status(-1, "NEXT d20: " + card.ForceNatural, Tone.Gold)); }
            if (card.RerollNextMiss) { RerollNextMiss = true; ev.Add(Status(-1, "SECOND CHANCE", Tone.Gold)); }
            if (card.ClearConditions && PC.Any) { PC.Clear(); ev.Add(Status(-1, "CONDITIONS REMOVED", Tone.Good)); }
            if (card.ActivateRage) { RageActive = true; ev.Add(Status(-1, "RAGE!", Tone.Bad)); }
            if (card.SelfProne) { PC.Prone = true; ev.Add(Status(-1, "YOU FALL PRONE", Tone.Bad)); }
            if (card.Draw > 0)
            {
                int n = DrawCards(card.Draw);
                ev.Add(new CombatEvent { Type = EvType.Draw, Amount = n });
            }

            if (card.Exhaust || card.Kind == CardKind.Power) Exhausted.Add(card); else Discard.Add(card);
            return ev;
        }

        void PlayerAttack(int ti, CardDef card, List<CombatEvent> ev)
        {
            var t = Enemies[ti];
            bool adv = card.AttackAdvantage || NextAttackAdvantage || t.C.Prone || t.C.Grappled > 0;
            if (!firstAttackDone && Sheet.HasRelic(RelicId.PetDie)) adv = true;
            bool dis = PC.Prone || PC.Frightened > 0;
            int mod = Sheet.Mod(card.AttackAttr);
            int toHit = mod + card.AttackBonus - (PC.Poisoned > 0 ? 2 : 0);
            int forced = ForcedNatural;
            ForcedNatural = 0;
            NextAttackAdvantage = false;
            firstAttackDone = true;

            var roll = dice.Test(toHit, t.ArmorClass, adv, dis, forced);
            roll.CritOn = 20 - Keen;
            ev.Add(new CombatEvent { Type = EvType.Roll, Target = ti, Actor = -1, Roll = roll, Label = "YOUR ATTACK" });

            if (!roll.Success)
            {
                bool reroll = false;
                if (RerollNextMiss) { RerollNextMiss = false; reroll = true; }
                else if (!luckyCoinUsed && Sheet.HasRelic(RelicId.LuckyCoin)) { luckyCoinUsed = true; reroll = true; }
                if (reroll)
                {
                    roll = dice.Test(toHit, t.ArmorClass, adv, dis);
                    roll.CritOn = 20 - Keen;
                    ev.Add(new CombatEvent { Type = EvType.Roll, Target = ti, Actor = -1, Roll = roll, Label = "REROLL" });
                }
            }

            if (!roll.Success)
            {
                ev.Add(new CombatEvent { Type = EvType.Miss, Target = ti, Roll = roll });
                if (StubbornActive)
                {
                    Block += 3;
                    NextAttackAdvantage = true;
                    ev.Add(new CombatEvent { Type = EvType.Block, Target = -1, Amount = 3, BlockAfter = Block });
                }
                return;
            }

            var dmgExpr = new DiceExpr(card.Damage.Count, card.Damage.Sides, card.Damage.Bonus + mod);
            int dmg = dice.Roll(dmgExpr, roll.IsCrit) + Fury + (Sheet.HasRelic(RelicId.Whetstone) ? 1 : 0)
                + card.ComboDamage * AttacksThisTurn + (roll.IsCrit ? card.OnCritDamage : 0);
            DamageEnemy(ti, dmg, card.IgnoreBlock, roll.IsCrit, null, ev);
            if (roll.IsCrit && card.OnCritEnergy > 0)
            {
                Energy += card.OnCritEnergy;
                ev.Add(new CombatEvent { Type = EvType.Energy, Amount = card.OnCritEnergy });
            }
            if (roll.IsCrit && card.OnCritDraw > 0)
            {
                int n = DrawCards(card.OnCritDraw);
                ev.Add(new CombatEvent { Type = EvType.Draw, Amount = n });
            }

            if (t.Alive)
            {
                int bleed = card.ApplyBleed + (Sheet.HasRelic(RelicId.RustyHook) ? 1 : 0);
                if (bleed > 0)
                {
                    t.C.Bleed += bleed;
                    ev.Add(Status(ti, "BLEED " + t.C.Bleed, Tone.Bad));
                }
                if (card.Save != SaveEffect.None && card.SaveOnlyOnHit) EnemySave(ti, card.Save, card.SaveAttr, card.SaveDc, ev);
            }
        }

        void EnemySave(int ti, SaveEffect effect, Attr attr, int dc, List<CombatEvent> ev)
        {
            var t = Enemies[ti];
            var r = dice.Test(t.Def.SaveBonus, dc, false, false);
            if (r.Success)
            {
                ev.Add(Status(ti, attr + " SAVE: " + r.Total + " SAVED", Tone.Neutral));
                return;
            }
            ApplySaveEffect(ti, effect, ev);
        }

        /// <summary>Magia de dano: o alvo rola resistencia contra a CD de magia. Passou = metade do dano.</summary>
        void SpellHit(int ti, CardDef card, List<CombatEvent> ev)
        {
            var t = Enemies[ti];
            int dc = Sheet.SpellDc + SpellFocusBonus;
            var r = dice.Test(t.Def.SaveBonus, dc, false, false);
            int dmg = dice.Roll(new DiceExpr(card.Damage.Count, card.Damage.Sides, card.Damage.Bonus + Sheet.Mod(Attr.INT)), false);
            if (r.Success)
            {
                ev.Add(Status(ti, card.SaveAttr + " SAVE " + r.Total + " VS " + dc + ": HALF", Tone.Neutral));
                dmg /= 2;
            }
            else ev.Add(Status(ti, card.SaveAttr + " SAVE " + r.Total + " VS " + dc + ": FAILED", Tone.Gold));
            DamageEnemy(ti, dmg, false, false, null, ev);
            if (!r.Success && t.Alive && card.Save != SaveEffect.None) ApplySaveEffect(ti, card.Save, ev);
        }

        void ApplySaveEffect(int ti, SaveEffect effect, List<CombatEvent> ev)
        {
            var t = Enemies[ti];
            switch (effect)
            {
                case SaveEffect.Stun: t.C.Stunned = true; ev.Add(Status(ti, "STUNNED!", Tone.Gold)); break;
                case SaveEffect.Chill: t.Weaken += 2; ev.Add(Status(ti, "CHILLED -2 TO HIT", Tone.Blue)); break;
                case SaveEffect.Prone: t.C.Prone = true; ev.Add(Status(ti, "PRONE!", Tone.Gold)); break;
                case SaveEffect.Grapple: t.C.Grappled = 2; t.Block = 0; ev.Add(Status(ti, "GRAPPLED!", Tone.Gold)); break;
                case SaveEffect.Frighten: t.C.Frightened = 2; ev.Add(Status(ti, "FRIGHTENED!", Tone.Purple)); break;
            }
        }

        void DamageEnemy(int ti, int dmg, bool ignoreBlock, bool crit, string label, List<CombatEvent> ev)
        {
            var t = Enemies[ti];
            int absorbed = ignoreBlock ? 0 : Math.Min(t.Block, dmg);
            t.Block -= absorbed;
            int real = dmg - absorbed;
            t.Hp = Math.Max(0, t.Hp - real);
            ev.Add(new CombatEvent { Type = EvType.Hit, Target = ti, Amount = real, Blocked = absorbed, HpAfter = t.Hp, BlockAfter = t.Block, Crit = crit, Label = label });
            CheckEnemyDown(ti, ev);
        }

        void CheckEnemyDown(int ti, List<CombatEvent> ev)
        {
            var t = Enemies[ti];
            if (t.Hp > 0 || t.Gone) return;
            if (t.Def.Reassembles && !t.Reassembled)
            {
                t.Reassembled = true;
                t.Hp = t.Def.MaxHp / 2;
                t.Block = 0;
                t.C.Clear();
                ev.Add(new CombatEvent { Type = EvType.Rise, Target = ti, HpAfter = t.Hp, Text = "REASSEMBLES!" });
                return;
            }
            t.Gone = true;
            ev.Add(new CombatEvent { Type = EvType.Die, Target = ti });
        }

        void LosePlayerHp(int amount, string label, List<CombatEvent> ev)
        {
            Sheet.Hp = Math.Max(0, Sheet.Hp - amount);
            ev.Add(new CombatEvent { Type = EvType.Hit, Target = -1, Amount = amount, HpAfter = Sheet.Hp, BlockAfter = Block, Label = label });
            GainFuryFromPain(amount, ev);
        }

        void GainFuryFromPain(int amount, List<CombatEvent> ev)
        {
            if (!RageActive || amount <= 0 || PlayerDead) return;
            Fury++;
            ev.Add(Status(-1, "FURY " + Fury, Tone.Bad));
        }

        public List<CombatEvent> EndPlayerTurn()
        {
            var ev = new List<CombatEvent>();
            if (PC.Bleed > 0)
            {
                LosePlayerHp(PC.Bleed, "BLEED", ev);
                PC.Bleed--;
            }
            if (PC.Frightened > 0) PC.Frightened--;
            if (PC.Poisoned > 0) PC.Poisoned--;
            Discard.AddRange(Hand);
            Hand.Clear();
            return ev;
        }

        // =====================================================================
        // Turno dos inimigos
        // =====================================================================

        public List<CombatEvent> EnemyPhase()
        {
            var ev = new List<CombatEvent>();
            int count = Enemies.Count; // invocados neste turno so agem no proximo
            for (int i = 0; i < count && !PlayerDead; i++)
            {
                var e = Enemies[i];
                if (!e.Alive) continue;
                ev.Add(new CombatEvent { Type = EvType.Actor, Actor = i, Target = i, Amount = (int)e.Intent.Kind, Label = e.Intent.Name });
                e.Block = 0;
                ev.Add(new CombatEvent { Type = EvType.Block, Target = i, Amount = 0, BlockAfter = 0 });

                if (e.C.Stunned)
                {
                    e.C.Stunned = false;
                    ev.Add(Status(i, "STUNNED", Tone.Gold));
                }
                else if (e.Def.FleesWhenHurt && e.Hp <= e.Def.MaxHp * 3 / 10)
                {
                    e.Gone = true;
                    e.Fled = true;
                    ev.Add(new CombatEvent { Type = EvType.Flee, Target = i });
                    continue;
                }
                else
                {
                    DoMove(i, ev);
                    e.MoveIndex++;
                }

                // fim do turno do inimigo
                if (e.Alive && e.C.Bleed > 0)
                {
                    int b = e.C.Bleed;
                    e.C.Bleed--;
                    DamageEnemy(i, b, true, false, "BLEED", ev);
                }
                e.C.Prone = false;
                if (e.C.Frightened > 0) e.C.Frightened--;
                if (e.C.Poisoned > 0) e.C.Poisoned--;
                if (e.C.Grappled > 0) e.C.Grappled--;
            }
            if (!PlayerDead && !EnemiesDefeated) StartPlayerTurn(ev);
            return ev;
        }

        void DoMove(int i, List<CombatEvent> ev)
        {
            var e = Enemies[i];
            var move = e.Intent;
            switch (move.Kind)
            {
                case MoveKind.Attack:
                    for (int h = 0; h < move.Hits && !PlayerDead && e.Alive; h++) EnemyAttack(i, move, ev);
                    break;
                case MoveKind.Guard:
                    if (e.C.Grappled > 0) { ev.Add(Status(i, "CAN'T BLOCK", Tone.Neutral)); break; }
                    e.Block += move.Guard;
                    ev.Add(new CombatEvent { Type = EvType.Block, Target = i, Amount = move.Guard, BlockAfter = e.Block, Label = move.Name });
                    break;
                case MoveKind.GuardAlly:
                {
                    int best = i;
                    for (int j = 0; j < Enemies.Count; j++)
                        if (j != i && Enemies[j].Alive && Enemies[j].C.Grappled <= 0 && (best == i || Enemies[j].Hp < Enemies[best].Hp)) best = j;
                    Enemies[best].Block += move.Guard;
                    ev.Add(new CombatEvent { Type = EvType.Block, Target = best, Amount = move.Guard, BlockAfter = Enemies[best].Block, Label = move.Name });
                    break;
                }
                case MoveKind.Curse:
                    Discard.Add(CardLibrary.Nightmare);
                    ev.Add(new CombatEvent { Type = EvType.Curse, Target = -1, Actor = i, Label = move.Name });
                    break;
                case MoveKind.Frighten:
                {
                    var r = dice.Test(Sheet.Mod(Attr.WIS), move.SaveDc, false, false);
                    ev.Add(new CombatEvent { Type = EvType.Roll, Actor = i, Target = -1, Roll = r, Label = move.Name + ": WIS SAVE" });
                    if (!r.Success) { PC.Frightened = 2; ev.Add(Status(-1, "FRIGHTENED!", Tone.Purple)); }
                    else ev.Add(Status(-1, "YOU RESIST", Tone.Good));
                    break;
                }
                case MoveKind.Summon:
                {
                    if (AliveCount < 2)
                    {
                        var s = AddEnemy(EnemyLibrary.BoneServant, true);
                        ev.Add(new CombatEvent { Type = EvType.Summon, Target = Enemies.Count - 1, Actor = i, HpAfter = s.Hp, Label = move.Name });
                    }
                    else
                    {
                        e.Block += 10;
                        ev.Add(new CombatEvent { Type = EvType.Block, Target = i, Amount = 10, BlockAfter = e.Block, Label = move.Name });
                    }
                    break;
                }
            }
        }

        void EnemyAttack(int i, EnemyMove move, List<CombatEvent> ev)
        {
            var e = Enemies[i];
            bool hidden = HiddenRolls;
            bool adv = PC.Prone;
            bool dis = e.C.Prone || e.C.Frightened > 0;
            int bonus = move.AttackBonus - e.Weaken - (e.C.Poisoned > 0 ? 2 : 0);
            e.Weaken = 0;

            var roll = dice.Test(bonus, PlayerArmorClass, adv, dis);
            if (!roll.Success && EnemyCanReroll)
            {
                // Homebrew v5: uma vez por combate, o mestre rola de novo
                EnemyCanReroll = false;
                ev.Add(new CombatEvent { Type = EvType.Roll, Actor = i, Target = -1, Roll = roll, Label = move.Name, Hidden = hidden, Announced = roll.Total });
                ev.Add(new CombatEvent { Type = EvType.Say, Text = "NO. I'M ROLLING THAT AGAIN. HOMEBREW RULES." });
                roll = dice.Test(bonus, PlayerArmorClass, adv, dis);
            }

            bool hit = roll.Success;
            int announced = roll.Total;
            bool cheated = false;
            // Atras do escudo, o mestre "ajusta" as rolagens que erraram por pouco.
            if (hidden && !hit && !roll.IsFumble && roll.Target - roll.Total <= 3)
            {
                cheated = true;
                hit = true;
                announced = roll.Target;
            }
            ev.Add(new CombatEvent { Type = EvType.Roll, Actor = i, Target = -1, Roll = roll, Label = move.Name, Hidden = hidden, Announced = announced, Cheated = cheated });

            if (!hit)
            {
                ev.Add(new CombatEvent { Type = EvType.Miss, Target = -1, Actor = i, Roll = roll });
                if (RiposteActive && e.Alive)
                {
                    int dmg = dice.Roll(new DiceExpr(1, 8, Sheet.Mod(Attr.STR)), false) + Fury;
                    DamageEnemy(i, dmg, false, false, "RIPOSTE", ev);
                }
                return;
            }

            int raw = dice.Roll(move.Damage, roll.IsCrit) + (e.Def.CurseScaling ? CurseCount : 0);
            int absorbed = Math.Min(Block, raw);
            Block -= absorbed;
            int real = raw - absorbed;
            Sheet.Hp = Math.Max(0, Sheet.Hp - real);
            ev.Add(new CombatEvent { Type = EvType.Hit, Target = -1, Actor = i, Amount = real, Blocked = absorbed, HpAfter = Sheet.Hp, BlockAfter = Block, Crit = roll.IsCrit });
            GainFuryFromPain(real, ev);
            if (PlayerDead) return;
            if (Conc != ConcKind.None && real > 0)
            {
                var cs = dice.Test(Sheet.Mod(Attr.CON), Math.Max(10, real / 2), false, false);
                if (!cs.Success) { ev.Add(Status(-1, "CONCENTRATION LOST: " + ConcName, Tone.Bad)); Conc = ConcKind.None; }
                else ev.Add(Status(-1, "CON SAVE: STILL FOCUSED", Tone.Good));
            }

            if (move.ApplyBleed > 0)
            {
                PC.Bleed += move.ApplyBleed;
                ev.Add(Status(-1, "BLEED " + PC.Bleed, Tone.Bad));
            }
            if (move.ApplyPoison > 0)
            {
                PC.Poisoned = Math.Max(PC.Poisoned, move.ApplyPoison);
                ev.Add(Status(-1, "POISONED!", Tone.Bad));
            }
            if (move.KnockProne && !PC.Prone)
            {
                var s = dice.Test(Sheet.Mod(Attr.STR), move.SaveDc, false, false);
                if (!s.Success) { PC.Prone = true; ev.Add(Status(-1, "KNOCKED PRONE!", Tone.Bad)); }
                else ev.Add(Status(-1, "YOU KEEP YOUR FEET", Tone.Good));
            }
        }

        static CombatEvent Status(int target, string text, Tone tone)
        {
            return new CombatEvent { Type = EvType.Status, Target = target, Text = text, Tone = tone };
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
