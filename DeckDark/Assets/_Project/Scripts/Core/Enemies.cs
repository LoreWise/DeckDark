namespace DeckDark.Core
{
    public enum MoveKind { Attack, Curse, Guard }
    public enum EnemySprite { Goblin, Skeleton, Cultist, Boss }

    public class EnemyMove
    {
        public string Name;
        public MoveKind Kind;
        public int AttackBonus;
        public DiceExpr Damage;
        public int Guard;           // bloqueio que o inimigo ganha
    }

    public class EnemyDef
    {
        public string Name;
        public EnemySprite Sprite;
        public int MaxHp;
        public int ArmorClass;
        public EnemyMove[] Pattern;  // repete em ciclo
        public bool IsBoss;
    }

    public static class EnemyLibrary
    {
        public static readonly EnemyDef Goblin = new EnemyDef
        {
            Name = "GOBLIN SCOUT", Sprite = EnemySprite.Goblin, MaxHp = 14, ArmorClass = 12,
            Pattern = new[]
            {
                new EnemyMove { Name = "STAB", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 6, 2) },
                new EnemyMove { Name = "STAB", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 6, 2) },
                new EnemyMove { Name = "HIDE", Kind = MoveKind.Guard, Guard = 5 },
            }
        };

        public static readonly EnemyDef Skeleton = new EnemyDef
        {
            Name = "SKELETON", Sprite = EnemySprite.Skeleton, MaxHp = 22, ArmorClass = 13,
            Pattern = new[]
            {
                new EnemyMove { Name = "RUSTY SWORD", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 8, 2) },
                new EnemyMove { Name = "RATTLE BONES", Kind = MoveKind.Guard, Guard = 6 },
                new EnemyMove { Name = "HEAVY BLOW", Kind = MoveKind.Attack, AttackBonus = 3, Damage = new DiceExpr(2, 6, 2) },
            }
        };

        public static readonly EnemyDef Cultist = new EnemyDef
        {
            Name = "MASKED CULTIST", Sprite = EnemySprite.Cultist, MaxHp = 27, ArmorClass = 12,
            Pattern = new[]
            {
                new EnemyMove { Name = "WHISPER", Kind = MoveKind.Curse },
                new EnemyMove { Name = "RITUAL DAGGER", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 8, 3) },
                new EnemyMove { Name = "RITUAL DAGGER", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 8, 3) },
            }
        };

        public static readonly EnemyDef Boss = new EnemyDef
        {
            Name = "THE FACELESS KING", Sprite = EnemySprite.Boss, MaxHp = 56, ArmorClass = 14, IsBoss = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "CROWN STRIKE", Kind = MoveKind.Attack, AttackBonus = 6, Damage = new DiceExpr(1, 10, 4) },
                new EnemyMove { Name = "MIST", Kind = MoveKind.Curse },
                new EnemyMove { Name = "CRUSH", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(2, 8, 4) },
            }
        };
    }
}
