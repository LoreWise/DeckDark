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
    /// Regras da Casa: o sistema de dificuldade crescente.
    /// Cada versao inclui as anteriores.
    /// </summary>
    public static class HouseRules
    {
        public static readonly string[] Texts =
        {
            "v1.0  AS REGRAS NORMAIS. POR ENQUANTO.",
            "v2.0  OS MONSTROS TÊM A PELE MAIS GROSSA. (+1 CA)",
            "v3.0  DESCANSAR NÃO CURA TUDO.",
        };

        public static int EnemyArmorBonus(int version) { return version >= 2 ? 1 : 0; }
        public static float TavernHealFraction(int version) { return version >= 3 ? 0.2f : 0.35f; }
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
                case OutcomeKind.GainStrength: Sheet.Scores[(int)Attr.FOR] += o.Amount; break;
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
            Title = "O BAÚ COBERTO DE MUSGO",
            Body = "NO MEIO DO CORREDOR HÁ UM BAÚ VELHO. A FECHADURA ESTÁ ENFERRUJADA E ALGO LÁ DENTRO FAZ UM BARULHO BAIXINHO, COMO SE RESPIRASSE.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "ABRIR COM CUIDADO", HasTest = true, TestAttr = Attr.DES, Dc = 12,
                    Success = new EventOutcome { Text = "UMA POÇÃO INTACTA, ENROLADA EM PANO. VOCÊ GUARDA NA MOCHILA.", Kind = OutcomeKind.AddCard, Card = CardLibrary.HealingPotion },
                    Failure = new EventOutcome { Text = "UMA AGULHA ESCONDIDA NA FECHADURA. SEU DEDO FICA ROXO.", Kind = OutcomeKind.LoseHp, Amount = 5 },
                },
                new EventChoice
                {
                    Label = "ARROMBAR", HasTest = true, TestAttr = Attr.FOR, Dc = 13,
                    Success = new EventOutcome { Text = "A TAMPA CEDE. LÁ DENTRO, ALGO QUE BRILHA.", Kind = OutcomeKind.AddRandomRelic },
                    Failure = new EventOutcome { Text = "A TAMPA FECHA NO SEU BRAÇO COMO UMA BOCA.", Kind = OutcomeKind.LoseHp, Amount = 7 },
                },
                new EventChoice
                {
                    Label = "DEIXAR PRA LÁ",
                    Success = new EventOutcome { Text = "VOCÊ SEGUE EM FRENTE. O BARULHO PARA QUANDO VOCÊ VIRA AS COSTAS.", Kind = OutcomeKind.None },
                },
            }
        };

        public static readonly EventDef Mirror = new EventDef
        {
            Id = "mirror",
            Title = "O ESPELHO",
            Body = "UMA SALA VAZIA COM UM ESPELHO. O REFLEXO NÃO MOSTRA SEU GUERREIRO. MOSTRA UMA CRIANÇA SENTADA NUM PORÃO, OLHANDO PARA BAIXO.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "OLHAR MAIS DE PERTO", HasTest = true, TestAttr = Attr.SAB, Dc = 13,
                    Success = new EventOutcome { Text = "VOCÊ ENTENDE ALGO QUE NÃO DEVIA. É DIFÍCIL ESQUECER.", Kind = OutcomeKind.AddCard, Card = CardLibrary.Lucidity },
                    Failure = new EventOutcome { Text = "A CRIANÇA NO ESPELHO LEVANTA A CABEÇA. ELA ESTÁ USANDO UMA MÁSCARA.", Kind = OutcomeKind.AddCurse },
                },
                new EventChoice
                {
                    Label = "QUEBRAR O ESPELHO", HasTest = true, TestAttr = Attr.FOR, Dc = 10,
                    Success = new EventOutcome { Text = "OS CACOS PARAM DE SUSSURRAR. VOCÊ SE SENTE MAIS FORTE.", Kind = OutcomeKind.GainStrength, Amount = 2 },
                    Failure = new EventOutcome { Text = "O VIDRO CORTA FUNDO. PARECE QUE O CORTE ESTÁ NA SUA MÃO DE VERDADE.", Kind = OutcomeKind.LoseHp, Amount = 4 },
                },
                new EventChoice
                {
                    Label = "SAIR DA SALA",
                    Success = new EventOutcome { Text = "VOCÊ FECHA A PORTA. DO OUTRO LADO, ALGUÉM BATE DUAS VEZES.", Kind = OutcomeKind.None },
                },
            }
        };

        public static readonly EventDef[] All = { MossChest, Mirror };
    }
}
