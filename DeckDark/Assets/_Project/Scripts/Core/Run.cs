using System.Collections.Generic;

namespace DeckDark.Core
{
    public enum NodeType { Start, Combat, Elite, Event, Treasure, Tavern, Boss }

    public class MapNode
    {
        public int Layer;
        public int Index;
        public NodeType Type;
        public readonly List<int> Next = new List<int>();
        public bool Visited;
    }

    public enum OutcomeKind { None, LoseHp, Heal, AddCard, AddRandomRelic, AddCurse, GainStrength, GainMaxHp, AddRandomCard, TradeBasicForRare }

    public class EventOutcome
    {
        public string Text;
        public OutcomeKind Kind;
        public int Amount;
        public CardDef Card;
        public EventOutcome Also;      // um segundo efeito, quando houver
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
        public string MasterLine;      // o que o mestre fala ao entrar
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
            "V5.0  THE MASTER CAN ROLL AGAIN. (ONE ENEMY REROLL PER COMBAT)",
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

        public int LastLayer { get { return Layers.Count - 1; } }

        void BuildMap()
        {
            // 7 camadas; os tipos das salas sao sorteados dentro de cada camada.
            var shapes = new[]
            {
                new[] { NodeType.Start },
                Shuffled(new[] { NodeType.Combat, NodeType.Combat, NodeType.Event }),
                Shuffled(new[] { NodeType.Event, NodeType.Combat, NodeType.Treasure }),
                Shuffled(new[] { NodeType.Elite, NodeType.Combat, NodeType.Event }),
                Shuffled(new[] { NodeType.Combat, NodeType.Tavern, NodeType.Event }),
                Shuffled(new[] { NodeType.Tavern, NodeType.Elite, NodeType.Treasure }),
                new[] { NodeType.Boss },
            };
            for (int l = 0; l < shapes.Length; l++)
            {
                var layer = new List<MapNode>();
                for (int i = 0; i < shapes[l].Length; i++)
                    layer.Add(new MapNode { Layer = l, Index = i, Type = shapes[l][i] });
                Layers.Add(layer);
            }

            // Caminhos: cada sala liga com a "de frente" e as vezes com a vizinha.
            for (int l = 0; l < Layers.Count - 1; l++)
            {
                var a = Layers[l];
                var b = Layers[l + 1];
                int n = a.Count, m = b.Count;
                var incoming = new bool[m];
                for (int i = 0; i < n; i++)
                {
                    if (n == 1 || m == 1)
                    {
                        for (int j = 0; j < m; j++) { AddLink(a[i], j); incoming[j] = true; }
                        continue;
                    }
                    int j0 = (int)System.Math.Round(i * (m - 1) / (double)(n - 1));
                    AddLink(a[i], j0); incoming[j0] = true;
                    int side = Dice.Range(0, 2) == 0 ? -1 : 1;
                    int j1 = j0 + side;
                    if (j1 >= 0 && j1 < m && Dice.Range(0, 100) < 55) { AddLink(a[i], j1); incoming[j1] = true; }
                }
                for (int j = 0; j < m; j++)
                {
                    if (incoming[j]) continue;
                    int i = (int)System.Math.Round(j * (n - 1) / (double)System.Math.Max(1, m - 1));
                    AddLink(a[i], j);
                }
            }
        }

        static void AddLink(MapNode from, int to)
        {
            if (!from.Next.Contains(to)) from.Next.Add(to);
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

        public EnemyDef[] EncounterForCurrent()
        {
            EnemyDef[][] table;
            switch (Current.Type)
            {
                case NodeType.Boss: return EnemyLibrary.BossEncounter;
                case NodeType.Elite: table = EnemyLibrary.EliteEncounters; break;
                default: table = Current.Layer <= 2 ? EnemyLibrary.EasyEncounters : EnemyLibrary.HardEncounters; break;
            }
            return table[Dice.Range(0, table.Length)];
        }

        /// <summary>Tres cartas diferentes, com raridade sorteada (comum, incomum, rara).</summary>
        public CardDef[] RollRewards(bool elite = false)
        {
            var picks = new List<CardDef>();
            int guard = 0;
            while (picks.Count < 3 && guard++ < 200)
            {
                int r = Dice.Range(0, 100);
                CardRarity want = r < (elite ? 18 : 8) ? CardRarity.Rare : (r < (elite ? 55 : 38) ? CardRarity.Uncommon : CardRarity.Common);
                var pool = new List<CardDef>();
                foreach (var c in CardLibrary.RewardPool)
                {
                    var rar = c.Rarity == CardRarity.Basic ? CardRarity.Common : c.Rarity;
                    if (rar == want && !picks.Contains(c)) pool.Add(c);
                }
                if (pool.Count == 0) continue;
                picks.Add(pool[Dice.Range(0, pool.Count)]);
            }
            return picks.ToArray();
        }

        public CardDef RandomCardOf(CardRarity rarity)
        {
            var pool = new List<CardDef>();
            foreach (var c in CardLibrary.RewardPool) if (c.Rarity == rarity) pool.Add(c);
            return pool[Dice.Range(0, pool.Count)];
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

        /// <summary>Aplica o resultado de um evento. Devolve um texto curto com o que foi ganho, ou null.</summary>
        public string Apply(EventOutcome o)
        {
            var gains = new List<string>();
            for (var cur = o; cur != null; cur = cur.Also)
            {
                switch (cur.Kind)
                {
                    case OutcomeKind.LoseHp:
                        Sheet.Hp -= cur.Amount;
                        if (Sheet.Hp < 1) Sheet.Hp = 1; // eventos nao matam
                        gains.Add("-" + cur.Amount + " HP");
                        break;
                    case OutcomeKind.Heal:
                    {
                        int before = Sheet.Hp;
                        Sheet.Heal(cur.Amount);
                        gains.Add("+" + (Sheet.Hp - before) + " HP");
                        break;
                    }
                    case OutcomeKind.GainMaxHp:
                        Sheet.MaxHp += cur.Amount;
                        Sheet.Hp += cur.Amount;
                        gains.Add("+" + cur.Amount + " MAX HP");
                        break;
                    case OutcomeKind.AddCard:
                        Sheet.Deck.Add(cur.Card);
                        gains.Add("CARD: " + cur.Card.Name);
                        break;
                    case OutcomeKind.AddRandomCard:
                    {
                        var c = RandomCardOf((CardRarity)cur.Amount);
                        Sheet.Deck.Add(c);
                        gains.Add("CARD: " + c.Name);
                        break;
                    }
                    case OutcomeKind.TradeBasicForRare:
                    {
                        int idx = Sheet.Deck.IndexOf(CardLibrary.RaiseShield);
                        if (idx < 0) idx = Sheet.Deck.IndexOf(CardLibrary.SwordStrike);
                        if (idx >= 0)
                        {
                            gains.Add("LOST: " + Sheet.Deck[idx].Name);
                            Sheet.Deck.RemoveAt(idx);
                        }
                        var c = RandomCardOf(CardRarity.Rare);
                        if (Dice.Range(0, 2) == 0) c = RandomCardOf(CardRarity.Uncommon);
                        Sheet.Deck.Add(c);
                        gains.Add("CARD: " + c.Name);
                        break;
                    }
                    case OutcomeKind.AddCurse:
                        Sheet.Deck.Add(CardLibrary.Nightmare);
                        gains.Add("CURSE: NIGHTMARE");
                        break;
                    case OutcomeKind.GainStrength:
                        Sheet.Scores[(int)Attr.STR] += cur.Amount;
                        gains.Add("+" + cur.Amount + " STR");
                        break;
                    case OutcomeKind.AddRandomRelic:
                    {
                        var r = RollRelic();
                        if (r != null) { Sheet.Relics.Add(r); gains.Add("RELIC: " + r.Name); }
                        break;
                    }
                }
            }
            return gains.Count > 0 ? string.Join(", ", gains) : null;
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
                    Success = new EventOutcome { Text = "YOU MOVE ON. THE SOUND STOPS WHEN YOU TURN YOUR BACK." },
                },
            }
        };

        public static readonly EventDef Mirror = new EventDef
        {
            Id = "mirror",
            Title = "THE MIRROR",
            MasterLine = "I DIDN'T WRITE THAT ONE.",
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
                    Success = new EventOutcome { Text = "YOU CLOSE THE DOOR. ON THE OTHER SIDE, SOMEONE KNOCKS TWICE." },
                },
            }
        };

        public static readonly EventDef Merchant = new EventDef
        {
            Id = "merchant",
            Title = "THE TRAVELING MERCHANT",
            MasterLine = "HE SELLS EVERYTHING. EVEN THINGS THAT AREN'T HIS.",
            Body = "A HUNCHED MAN SITS ON A PILE OF BAGS. HIS PRICES ARE WRITTEN ON HIS HANDS, AND THEY CHANGE WHEN YOU LOOK AWAY.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "TRADE A CARD",
                    Success = new EventOutcome { Text = "HE TAKES ONE OF YOUR OLD CARDS AND HANDS YOU SOMETHING BETTER. IT'S STILL WARM.", Kind = OutcomeKind.TradeBasicForRare },
                },
                new EventChoice
                {
                    Label = "BUY A TRINKET [PAY 7 HP]",
                    Success = new EventOutcome { Text = "HE DOESN'T WANT GOLD. HE WANTS A LITTLE OF YOUR BLOOD. IT SEEMS FAIR.", Kind = OutcomeKind.LoseHp, Amount = 7, Also = new EventOutcome { Kind = OutcomeKind.AddRandomRelic } },
                },
                new EventChoice
                {
                    Label = "WALK AWAY",
                    Success = new EventOutcome { Text = "HE WAVES. HE KNOWS YOU'LL BE BACK." },
                },
            }
        };

        public static readonly EventDef Well = new EventDef
        {
            Id = "well",
            Title = "THE WELL",
            Body = "A STONE WELL IN THE MIDDLE OF A ROOM WITH NO CEILING. COINS SHINE AT THE BOTTOM. SOMETHING DOWN THERE IS HUMMING A SONG YOU KNOW.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "TOSS A COIN", HasTest = true, TestAttr = Attr.CHA, Dc = 12,
                    Success = new EventOutcome { Text = "THE HUMMING STOPS. YOU FEEL BLESSED.", Kind = OutcomeKind.GainMaxHp, Amount = 5 },
                    Failure = new EventOutcome { Text = "A KID'S VOICE FROM THE BOTTOM SAYS THANK YOU. IT KNOWS YOUR NAME.", Kind = OutcomeKind.AddCurse },
                },
                new EventChoice
                {
                    Label = "LOOK DOWN", HasTest = true, TestAttr = Attr.WIS, Dc = 14,
                    Success = new EventOutcome { Text = "YOU SEE THE BOTTOM. IT'S A BASEMENT FLOOR. YOU REMEMBER SOMETHING USEFUL.", Kind = OutcomeKind.AddRandomCard, Amount = (int)CardRarity.Uncommon },
                    Failure = new EventOutcome { Text = "YOU LEAN TOO FAR. SOMETHING PULLS YOUR HAIR.", Kind = OutcomeKind.LoseHp, Amount = 5 },
                },
                new EventChoice
                {
                    Label = "LEAVE",
                    Success = new EventOutcome { Text = "THE SONG FOLLOWS YOU FOR A WHILE." },
                },
            }
        };

        public static readonly EventDef Brother = new EventDef
        {
            Id = "brother",
            Title = "FOOTSTEPS ON THE STAIRS",
            MasterLine = "DON'T LOOK AT HIM.",
            Body = "RAFA'S OLDER BROTHER COMES DOWN TO THE BASEMENT. HE LOOKS AT THE TABLE AND SAYS: THAT'S NOT THE REAL RULEBOOK.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "ASK FOR HELP",
                    Success = new EventOutcome { Text = "HE WRITES SOMETHING ON YOUR SHEET IN PEN. RAFA STARES AT HIM UNTIL HE LEAVES.", Kind = OutcomeKind.AddRandomCard, Amount = (int)CardRarity.Uncommon },
                },
                new EventChoice
                {
                    Label = "ASK HIM TO STAY",
                    Success = new EventOutcome { Text = "HE SAYS HE CAN'T. HE GOES BACK UP. THE STAIRS CREAK FOR A LONG TIME. TOO LONG.", Kind = OutcomeKind.Heal, Amount = 6 },
                },
                new EventChoice
                {
                    Label = "SAY NOTHING",
                    Success = new EventOutcome { Text = "HE SHRUGS AND LEAVES. RAFA DIDN'T BLINK THE WHOLE TIME." },
                },
            }
        };

        public static readonly EventDef Shrine = new EventDef
        {
            Id = "shrine",
            Title = "THE SHRINE OF BONES",
            Body = "A SMALL ALTAR MADE OF FINGER BONES. THE CANDLES ON IT ARE STILL LIT, AND THEY SMELL LIKE BIRTHDAY CAKE.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "PRAY", HasTest = true, TestAttr = Attr.WIS, Dc = 12,
                    Success = new EventOutcome { Text = "A WARM FEELING. YOUR WOUNDS CLOSE.", Kind = OutcomeKind.Heal, Amount = 12 },
                    Failure = new EventOutcome { Text = "THE CANDLES GO OUT ALL AT ONCE.", Kind = OutcomeKind.LoseHp, Amount = 3 },
                },
                new EventChoice
                {
                    Label = "DESECRATE IT", HasTest = true, TestAttr = Attr.STR, Dc = 13,
                    Success = new EventOutcome { Text = "YOU KICK THE ALTAR APART. THE BONES DON'T COMPLAIN. YOU FEEL STRONGER.", Kind = OutcomeKind.GainStrength, Amount = 2 },
                    Failure = new EventOutcome { Text = "THE BONES GRAB YOUR ANKLE. THEY DON'T LET GO FOR A LONG TIME.", Kind = OutcomeKind.AddCurse, Also = new EventOutcome { Kind = OutcomeKind.LoseHp, Amount = 3 } },
                },
                new EventChoice
                {
                    Label = "LEAVE",
                    Success = new EventOutcome { Text = "YOU BLOW OUT ONE CANDLE ON THE WAY OUT. NOBODY SINGS." },
                },
            }
        };

        public static readonly EventDef OtherSheet = new EventDef
        {
            Id = "othersheet",
            Title = "THE OTHER SHEET",
            MasterLine = "OH. YOU FOUND THAT.",
            Body = "A CHARACTER SHEET LIES ON THE FLOOR, SOAKED. THE NAME AT THE TOP IS YOURS, IN YOUR HANDWRITING. THE CHARACTER IS DEAD.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "TAKE HIS GEAR",
                    Success = new EventOutcome { Text = "YOU TAKE WHAT HE WAS CARRYING. HE WON'T NEED IT. HE LEAVES SOMETHING ELSE BEHIND, IN YOU.", Kind = OutcomeKind.AddRandomRelic, Also = new EventOutcome { Kind = OutcomeKind.AddCurse } },
                },
                new EventChoice
                {
                    Label = "BURN IT",
                    Success = new EventOutcome { Text = "IT BURNS SLOWLY. RAFA WATCHES THE WHOLE TIME. YOU FEEL LIGHTER.", Kind = OutcomeKind.Heal, Amount = 5 },
                },
                new EventChoice
                {
                    Label = "LEAVE IT",
                    Success = new EventOutcome { Text = "YOU STEP OVER IT. IT'S STILL THERE WHEN YOU LOOK BACK. IT ALWAYS WILL BE." },
                },
            }
        };

        public static readonly EventDef RiggedGame = new EventDef
        {
            Id = "rigged",
            Title = "A RIGGED GAME",
            MasterLine = "GO AHEAD. I'M NOT LOOKING.",
            Body = "A GOBLIN SITS BEHIND A TINY TABLE WITH THREE CUPS AND A DIE. HE SMILES WITH TOO MANY TEETH. DOUBLE OR NOTHING, HE SAYS.",
            Choices = new[]
            {
                new EventChoice
                {
                    Label = "PLAY FAIR", HasTest = true, TestAttr = Attr.CHA, Dc = 14,
                    Success = new EventOutcome { Text = "YOU WIN. THE GOBLIN PAYS, UPSET.", Kind = OutcomeKind.AddRandomRelic },
                    Failure = new EventOutcome { Text = "YOU LOSE. HE TAKES A LITTLE OF YOUR BLOOD AS PAYMENT.", Kind = OutcomeKind.LoseHp, Amount = 5 },
                },
                new EventChoice
                {
                    Label = "CHEAT", HasTest = true, TestAttr = Attr.DEX, Dc = 12,
                    Success = new EventOutcome { Text = "YOU SWAP THE DIE. THE GOBLIN NEVER NOTICES. THE MASTER DOES.", Kind = OutcomeKind.AddRandomRelic, Also = new EventOutcome { Kind = OutcomeKind.AddCard, Card = CardLibrary.LoadedDie } },
                    Failure = new EventOutcome { Text = "HE CATCHES YOUR HAND. I SAW THAT, SAYS THE MASTER.", Kind = OutcomeKind.LoseHp, Amount = 8 },
                },
                new EventChoice
                {
                    Label = "DECLINE",
                    Success = new EventOutcome { Text = "THE GOBLIN SHRUGS. HE WAS GOING TO CHEAT ANYWAY." },
                },
            }
        };

        public static readonly EventDef[] All = { MossChest, Mirror, Merchant, Well, Brother, Shrine, OtherSheet, RiggedGame };
    }
}
