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
            Name = "GOBLIN BATEDOR", Sprite = EnemySprite.Goblin, MaxHp = 14, ArmorClass = 12,
            Pattern = new[]
            {
                new EnemyMove { Name = "FACADA", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 6, 2) },
                new EnemyMove { Name = "FACADA", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 6, 2) },
                new EnemyMove { Name = "SE ESCONDER", Kind = MoveKind.Guard, Guard = 5 },
            }
        };

        public static readonly EnemyDef Skeleton = new EnemyDef
        {
            Name = "ESQUELETO", Sprite = EnemySprite.Skeleton, MaxHp = 22, ArmorClass = 13,
            Pattern = new[]
            {
                new EnemyMove { Name = "ESPADA ENFERRUJADA", Kind = MoveKind.Attack, AttackBonus = 4, Damage = new DiceExpr(1, 8, 2) },
                new EnemyMove { Name = "ERGUER OSSOS", Kind = MoveKind.Guard, Guard = 6 },
                new EnemyMove { Name = "GOLPE PESADO", Kind = MoveKind.Attack, AttackBonus = 3, Damage = new DiceExpr(2, 6, 2) },
            }
        };

        public static readonly EnemyDef Cultist = new EnemyDef
        {
            Name = "CULTISTA MASCARADO", Sprite = EnemySprite.Cultist, MaxHp = 27, ArmorClass = 12,
            Pattern = new[]
            {
                new EnemyMove { Name = "SUSSURRAR", Kind = MoveKind.Curse },
                new EnemyMove { Name = "ADAGA RITUAL", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 8, 3) },
                new EnemyMove { Name = "ADAGA RITUAL", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(1, 8, 3) },
            }
        };

        public static readonly EnemyDef Boss = new EnemyDef
        {
            Name = "O REI SEM ROSTO", Sprite = EnemySprite.Boss, MaxHp = 56, ArmorClass = 14, IsBoss = true,
            Pattern = new[]
            {
                new EnemyMove { Name = "GOLPE DA COROA", Kind = MoveKind.Attack, AttackBonus = 6, Damage = new DiceExpr(1, 10, 4) },
                new EnemyMove { Name = "NÉVOA", Kind = MoveKind.Curse },
                new EnemyMove { Name = "ESMAGAR", Kind = MoveKind.Attack, AttackBonus = 5, Damage = new DiceExpr(2, 8, 4) },
            }
        };
    }
}
