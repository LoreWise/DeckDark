namespace DeckDark.Core
{
    public enum MoveKind { Attack, Curse, Guard, GuardAlly, Frighten, Summon }
    public enum EnemySprite { Goblin, Skeleton, Cultist, Boss, Rat, Ogre, Ghoul }

    public class EnemyMove
    {
        public string Name;
        public MoveKind Kind;
        public int AttackBonus;
        public DiceExpr Damage;
        public int Hits = 1;
        public int Guard;            // bloqueio (Guard e GuardAlly)
        public int ApplyBleed;       // a cada acerto
        public int ApplyPoison;      // turnos de Poisoned a cada acerto
        public bool KnockProne;      // acerto derruba (teste de STR do jogador)
        public int SaveDc = 12;      // Frighten e KnockProne
    }

    public class EnemyDef
    {
        public string Name;
        public EnemySprite Sprite;
        public int MaxHp;
        public int ArmorClass;
        public int SaveBonus;
        public EnemyMove[] Pattern;  // repete em ciclo
        public bool IsBoss;
        public bool IsElite;

        // Comportamentos especiais
        public bool FleesWhenHurt;   // foge com 30% da vida
        public bool Reassembles;     // volta uma vez com metade da vida
        public bool CurseScaling;    // +1 de dano por maldicao no seu deck
    }

    public static class EnemyLibrary
    {
        public static readonly EnemyDef Rat = new EnemyDef
        {
            Name = "GIANT RAT", Sprite = EnemySprite.Rat, MaxHp = 8, ArmorClass = 11, SaveBonus = 0,
            Pattern = new[]
            {
                new EnemyMove { Name = "BITE", Kind = MoveKind.Attack, AttackBonus = 3, Damage = new DiceExpr(1, 4, 1), ApplyBleed = 1 },
                new EnemyMove { Name = "BITE", Kind = MoveKind.Attack, AttackBonus = 3, Damage = new DiceExpr(1, 4, 1) },
            }
        };

        public static readonly EnemyDef Goblin = new EnemyDef
        {
            Name = "GOBLIN SCOUT", Sprite = EnemySprite.Goblin, MaxHp = 13, ArmorClass = 12, SaveBonus = 1, FleesWhenHurt = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "STAB", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 6, 2) },
                new EnemyMove { Name = "DIRTY TRICK", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 4, 1), KnockProne = true, SaveDc = 12 },
                new EnemyMove { Name = "HIDE", Kind = MoveKind.Guard, Guard = 6 },
            }
        };

        public static readonly EnemyDef Skeleton = new EnemyDef
        {
            Name = "SKELETON", Sprite = EnemySprite.Skeleton, MaxHp = 18, ArmorClass = 13, SaveBonus = 1, Reassembles = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "RUSTY SWORD", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 8, 2) },
                new EnemyMove { Name = "RATTLE BONES", Kind = MoveKind.Guard, Guard = 7 },
                new EnemyMove { Name = "HEAVY BLOW", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(2, 6, 2) },
            }
        };

        public static readonly EnemyDef Cultist = new EnemyDef
        {
            Name = "MASKED CULTIST", Sprite = EnemySprite.Cultist, MaxHp = 24, ArmorClass = 12, SaveBonus = 2, CurseScaling = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "WHISPER", Kind = MoveKind.Curse },
                new EnemyMove { Name = "RITUAL DAGGER", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 8, 3) },
                new EnemyMove { Name = "DARK WARD", Kind = MoveKind.GuardAlly, Guard = 8 },
                new EnemyMove { Name = "RITUAL DAGGER", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 8, 3) },
            }
        };

        /// <summary>Invocado pelo Rei. Fraco, mas se acumula.</summary>
        public static readonly EnemyDef BoneServant = new EnemyDef
        {
            Name = "BONE SERVANT", Sprite = EnemySprite.Skeleton, MaxHp = 9, ArmorClass = 11, SaveBonus = 0,
            Pattern = new[]
            {
                new EnemyMove { Name = "RUSTY SWORD", Kind = MoveKind.Attack, AttackBonus = 3, Damage = new DiceExpr(1, 6, 1) },
            }
        };

        // ---------------- Elites ----------------

        public static readonly EnemyDef Ogre = new EnemyDef
        {
            Name = "OGRE", Sprite = EnemySprite.Ogre, MaxHp = 44, ArmorClass = 11, SaveBonus = 5, IsElite = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "ROAR", Kind = MoveKind.Frighten, SaveDc = 12 },
                new EnemyMove { Name = "GREATCLUB", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(2, 6, 3) },
                new EnemyMove { Name = "STOMP", Kind = MoveKind.Attack, AttackBonus = 6, Damage = new DiceExpr(1, 10, 3), KnockProne = true, SaveDc = 14 },
                new EnemyMove { Name = "GREATCLUB", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(2, 6, 3) },
            }
        };

        public static readonly EnemyDef Ghoul = new EnemyDef
        {
            Name = "GHOUL", Sprite = EnemySprite.Ghoul, MaxHp = 38, ArmorClass = 13, SaveBonus = 2, IsElite = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "CLAWS", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 6, 2), Hits = 2, ApplyBleed = 2 },
                new EnemyMove { Name = "ROTTEN BITE", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 8, 2), ApplyPoison = 2 },
                new EnemyMove { Name = "CROUCH", Kind = MoveKind.Guard, Guard = 10 },
            }
        };

        // ---------------- Chefe ----------------

        public static readonly EnemyDef Boss = new EnemyDef
        {
            Name = "THE FACELESS KING", Sprite = EnemySprite.Boss, MaxHp = 60, ArmorClass = 14, SaveBonus = 4, IsBoss = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "CROWN STRIKE", Kind = MoveKind.Attack, AttackBonus = 6, Damage = new DiceExpr(1, 10, 4) },
                new EnemyMove { Name = "CALL THE COURT", Kind = MoveKind.Summon },
                new EnemyMove { Name = "CRUSH", Kind = MoveKind.Attack, AttackBonus = 6, Damage = new DiceExpr(2, 8, 4), KnockProne = true, SaveDc = 13 },
                new EnemyMove { Name = "MIST", Kind = MoveKind.Curse },
                new EnemyMove { Name = "NO FACE", Kind = MoveKind.Frighten, SaveDc = 13 },
            }
        };

        // ---------------- Encontros por fase do andar ----------------

        public static readonly EnemyDef[][] EasyEncounters =
        {
            new[] { Goblin, Goblin },
            new[] { Skeleton },
            new[] { Rat, Rat, Rat },
            new[] { Goblin, Rat },
        };

        public static readonly EnemyDef[][] HardEncounters =
        {
            new[] { Cultist, Skeleton },
            new[] { Goblin, Goblin, Rat },
            new[] { Cultist, Rat, Rat },
            new[] { Skeleton, Skeleton },
        };

        public static readonly EnemyDef[][] EliteEncounters =
        {
            new[] { Ogre },
            new[] { Ghoul, Rat },
        };

        public static readonly EnemyDef[] BossEncounter = { Boss };
    }
}
