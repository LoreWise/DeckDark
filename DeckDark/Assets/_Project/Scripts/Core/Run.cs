using System.Collections.Generic;

namespace DeckDark.Core
{
    public enum NodeType { Start, Combat, Event, Treasure, Tavern, Boss }

    public class MapNode
    {
        public int Layer;
        public int Index;
        public NodeType Type;
        public readonly List<int> Next = new List<int>();
        public bool Visited;
    }

    public enum OutcomeKind { None, LoseHp, Heal, AddCard, AddRandomRelic, AddCurse, GainStrength }

    public class EventOutcome
    {
        public string Text;
        public OutcomeKind Kind;
        public int Amount;
        public CardDef Card;
    }

    public class EventChoice
    {
        public string Label;
        public bool HasTest;
        public Attr TestAttr;
        public int Dc;
        public EventOutcome Success;
        public EventOutcome Failure;   // usado so quando ha teste
    }

    public class EventDef
    {
        public string Id;
        public string Title;
        public string Body;
        public EventChoice[] Choices;
    }

    /// <summary>
    /// Homebrew: o sistema de dificuldade crescente. As regras que o Mestre inventa.
    /// Cada versao inclui todas as anteriores. Vencer com a versao N libera a N+1.
    /// </summary>
    public static class Homebrew
    {
        public static readonly string[] Texts =
        {
            "V1.0  THE NORMAL RULES. FOR NOW.",
            "V2.0  MONSTERS HAVE THICKER SKIN. (+1 AC)",
            "V3.0  RESTING DOESN'T HEAL EVERYTHING. (SNACK HEALS 20%)",
            "V4.0  EVERY HERO STARTS HURT. (START AT 80% HP)",
            "V5.0  THE MASTER CAN ROLL AGAIN. (ONCE PER COMBAT, A MISSED ENEMY ATTACK IS REROLLED)",
        };

        public static int MaxVersion { get { return Texts.Length; } }
        public static int EnemyArmorBonus(int version) { return version >= 2 ? 1 : 0; }
        public static float TavernHealFraction(int version) { return version >= 3 ? 0.2f : 0.35f; }
        public static float StartingHpFraction(int version) { return version >= 4 ? 0.8f : 1f; }
        public static bool EnemyReroll(int version) { return version >= 5; }
    }

    /// <summary>Estado de uma run: ficha, mapa, posicao atual.</summary>
    public class RunState
    {
        public readonly Dice Dice;
        public readonly CharacterSheet Sheet;
        public readonly List<List<MapNode>> Layers = new List<List<MapNode>>();
        public MapNode Current;
        public readonly int RulesVersion;
        readonly List<string> seenEvents = new List<string>();

        public RunState(int seed, int rulesVersion)
        {
            Dice = new Dice(seed);
            Sheet = CharacterSheet.NewWarrior();
            RulesVersion = rulesVersion;
            Sheet.Hp = (int)(Sheet.MaxHp * Homebrew.StartingHpFraction(rulesVersion));
            BuildMap();
            Current = Layers[0][0];
            Current.Visited = true;
        }

        void BuildMap()
        {
            // Formato fixo de 5 camadas; os tipos das salas sao sorteados.
            var shapes = new[]
            {
                new[] { NodeType.Start },
                Shuffled(new[] { NodeType.Combat, NodeType.Event }),
                Shuffled(new[] { NodeType.Combat, NodeType.Treasure, NodeType.Event }),
                Shuffled(new[] { NodeType.Tavern, NodeType.Combat }),
                new[] { NodeType.Boss },
            };
            for (int l = 0; l < shapes.Length; l++)
            {
                var layer = new List<MapNode>();
                for (int i = 0; i < shapes[l].Length; i++)
                    layer.Add(new MapNode { Layer = l, Index = i, Type = shapes[l][i] });
                Layers.Add(layer);
            }
            Link(0, 0, 0, 1);
            Link(1, 0, 0, 1);
            Link(1, 1, 1, 2);
            Link(2, 0, 0);
            Link(2, 1, 0, 1);
            Link(2, 2, 1);
            Link(3, 0, 0);
            Link(3, 1, 0);
        }

        void Link(int layer, int index, params int[] next)
        {
            Layers[layer][index].Next.AddRange(next);
        }

        NodeType[] Shuffled(NodeType[] a)
        {
            for (int i = a.Length - 1; i > 0; i--)
            {
                int j = Dice.Range(0, i + 1);
                var t = a[i]; a[i] = a[j]; a[j] = t;
            }
            return a;
        }

        public bool CanTravelTo(MapNode n)
        {
            return n.Layer == Current.Layer + 1 && Current.Next.Contains(n.Index);
        }

        public void TravelTo(MapNode n)
        {
            Current = n;
            n.Visited = true;
        }

        public EnemyDef EnemyForCurrent()
        {
            switch (Current.Layer)
            {
                case 1: return EnemyLibrary.Goblin;
                case 2: return Dice.Range(0, 2) == 0 ? EnemyLibrary.Skeleton : EnemyLibrary.Goblin;
                case 3: return EnemyLibrary.Cultist;
                default: return EnemyLibrary.Boss;
            }
        }

        public CardDef[] RollRewards()
        {
            var pool = new List<CardDef>(CardLibrary.RewardPool);
            var picks = new CardDef[3];
            for (int i = 0; i < 3; i++)
            {
                int j = Dice.Range(0, pool.Count);
                picks[i] = pool[j];
                pool.RemoveAt(j);
            }
            return picks;
        }

        public RelicDef RollRelic()
        {
            var options = new List<RelicDef>();
            foreach (var r in RelicLibrary.All) if (!Sheet.HasRelic(r.Id)) options.Add(r);
            if (options.Count == 0) return null;
            return options[Dice.Range(0, options.Count)];
        }

        public EventDef NextEvent()
        {
            var options = new List<EventDef>();
            foreach (var e in EventLibrary.All) if (!seenEvents.Contains(e.Id)) options.Add(e);
            if (options.Count == 0) options.AddRange(EventLibrary.All);
            var ev = options[Dice.Range(0, options.Count)];
            seenEvents.Add(ev.Id);
            return ev;
        }

        /// <summary>Aplica o resultado de um evento. Devolve a reliquia ganha, se houver.</summary>
        public RelicDef Apply(EventOutcome o)
        {
            switch (o.Kind)
            {
                case OutcomeKind.LoseHp:
                    Sheet.Hp -= o.Amount;
                    if (Sheet.Hp < 1) Sheet.Hp = 1; // eventos nao matam no prototipo
                    break;
                case OutcomeKind.Heal: Sheet.Heal(o.Amount); break;
                case OutcomeKind.AddCard: Sheet.Deck.Add(o.Card); break;
                case OutcomeKind.AddCurse: Sheet.Deck.Add(CardLibrary.Nightmare); break;
                case OutcomeKind.GainStrength: Sheet.Scores[(int)Attr.STR] += o.Amount; break;
                case OutcomeKind.AddRandomRelic:
                {
                    var r = RollRelic();
                    if (r != null) Sheet.Relics.Add(r);
                    return r;
                }
            }
            return null;
        }
    }

    public static class EventLibrary
    {
        public static readonly EventDef MossChest = new EventDef
        {
            Id = "chest",
            Title = "THE MOSSY CHEST",
            Body = "AN OLD CHEST SITS IN THE MIDDLE OF THE HALL. THE LOCK IS RUSTED AND SOMETHING INSIDE MAKES A LOW SOUND, LIKE BREATHING.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "OPEN IT CAREFULLY", HasTest = true, TestAttr = Attr.DEX, Dc = 12,
                    Success = new EventOutcome { Text = "AN INTACT POTION, WRAPPED IN CLOTH. YOU PUT IT IN YOUR BAG.", Kind = OutcomeKind.AddCard, Card = CardLibrary.HealingPotion },
                    Failure = new EventOutcome { Text = "A HIDDEN NEEDLE IN THE LOCK. YOUR FINGER TURNS PURPLE.", Kind = OutcomeKind.LoseHp, Amount = 5 },
                },
                new EventChoice
                {
                    Label = "FORCE IT OPEN", HasTest = true, TestAttr = Attr.STR, Dc = 13,
                    Success = new EventOutcome { Text = "THE LID GIVES WAY. INSIDE, SOMETHING SHINES.", Kind = OutcomeKind.AddRandomRelic },
                    Failure = new EventOutcome { Text = "THE LID SNAPS SHUT ON YOUR ARM LIKE A MOUTH.", Kind = OutcomeKind.LoseHp, Amount = 7 },
                },
                new EventChoice
                {
                    Label = "LEAVE IT",
                    Success = new EventOutcome { Text = "YOU MOVE ON. THE SOUND STOPS WHEN YOU TURN YOUR BACK.", Kind = OutcomeKind.None },
                },
            }
        };

        public static readonly EventDef Mirror = new EventDef
        {
            Id = "mirror",
            Title = "THE MIRROR",
            Body = "AN EMPTY ROOM WITH A MIRROR. THE REFLECTION DOESN'T SHOW YOUR WARRIOR. IT SHOWS A KID SITTING IN A BASEMENT, LOOKING DOWN.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "LOOK CLOSER", HasTest = true, TestAttr = Attr.WIS, Dc = 13,
                    Success = new EventOutcome { Text = "YOU UNDERSTAND SOMETHING YOU SHOULDN'T. IT'S HARD TO FORGET.", Kind = OutcomeKind.AddCard, Card = CardLibrary.Lucidity },
                    Failure = new EventOutcome { Text = "THE KID IN THE MIRROR LOOKS UP. HE IS WEARING A MASK.", Kind = OutcomeKind.AddCurse },
                },
                new EventChoice
                {
                    Label = "BREAK THE MIRROR", HasTest = true, TestAttr = Attr.STR, Dc = 10,
                    Success = new EventOutcome { Text = "THE SHARDS STOP WHISPERING. YOU FEEL STRONGER.", Kind = OutcomeKind.GainStrength, Amount = 2 },
                    Failure = new EventOutcome { Text = "THE GLASS CUTS DEEP. IT FEELS LIKE THE CUT IS ON YOUR REAL HAND.", Kind = OutcomeKind.LoseHp, Amount = 4 },
                },
                new EventChoice
                {
                    Label = "LEAVE THE ROOM",
                    Success = new EventOutcome { Text = "YOU CLOSE THE DOOR. ON THE OTHER SIDE, SOMEONE KNOCKS TWICE.", Kind = OutcomeKind.None },
                },
            }
        };

        public static readonly EventDef[] All = { MossChest, Mirror };
    }
}
