using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DeckDark.Core
{
    /// <summary>
    /// Conjunto de pares chave=valor, uma linha por par. Formato simples e sem bibliotecas,
    /// para funcionar igual no Unity e nos testes. Chaves desconhecidas sao ignoradas na leitura
    /// e chaves ausentes usam o valor padrao: assim campos novos nao quebram saves antigos.
    /// Valores nao podem ter quebra de linha; listas usam ',' (e ids nao devem conter ',', ';', ':', '/' ou '=').
    /// </summary>
    public class SaveData
    {
        readonly Dictionary<string, string> values = new Dictionary<string, string>();
        readonly List<string> order = new List<string>();

        public bool Has(string key) { return values.ContainsKey(key); }

        public void Set(string key, string value)
        {
            value = (value ?? "").Replace("\n", " ").Replace("\r", " ");
            if (!values.ContainsKey(key)) order.Add(key);
            values[key] = value;
        }

        public void Set(string key, int value) { Set(key, value.ToString()); }
        public void Set(string key, long value) { Set(key, value.ToString()); }
        public void Set(string key, bool value) { Set(key, value ? "1" : "0"); }
        public void SetList(string key, IEnumerable<string> items) { Set(key, string.Join(",", items)); }

        public string Get(string key, string def = "")
        {
            string v;
            return values.TryGetValue(key, out v) ? v : def;
        }

        public int GetInt(string key, int def = 0)
        {
            int v;
            return int.TryParse(Get(key, null), out v) ? v : def;
        }

        public long GetLong(string key, long def = 0)
        {
            long v;
            return long.TryParse(Get(key, null), out v) ? v : def;
        }

        public bool GetBool(string key, bool def = false) { return Has(key) ? Get(key) == "1" : def; }

        public List<string> GetList(string key)
        {
            var r = new List<string>();
            foreach (var s in Get(key).Split(',')) if (s.Length > 0) r.Add(s);
            return r;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var k in order) sb.Append(k).Append('=').Append(values[k]).Append('\n');
            return sb.ToString();
        }

        public static SaveData Parse(string text)
        {
            var d = new SaveData();
            if (string.IsNullOrEmpty(text)) return d;
            foreach (var line in text.Split('\n'))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                d.Set(line.Substring(0, eq).Trim(), line.Substring(eq + 1).TrimEnd('\r'));
            }
            return d;
        }
    }

    /// <summary>
    /// Salvar e continuar a run. So salva em pontos seguros (no mapa, entre salas),
    /// entao nao precisa guardar combate, recompensa ou evento em andamento.
    ///
    /// COMO ADICIONAR UM CAMPO NOVO DA RUNSTATE (ouro, ato, loja...):
    ///   1. em RunState.WriteTo, grave: d.Set("gold", Gold);
    ///   2. no construtor RunState(SaveData d), leia: Gold = d.GetInt("gold", 0);
    ///   Saves antigos sem a chave recebem o padrao. So aumente FormatVersion se o
    ///   significado de uma chave existente mudar (ai saves antigos sao descartados).
    /// Estado que e da tela (GameApp) e nao da regra vai no SaveData tambem, pelo lado do View (SaveHooks.cs).
    /// </summary>
    public static class RunSave
    {
        public const int FormatVersion = 1;

        public static string Serialize(RunState run)
        {
            return ToData(run).ToString();
        }

        public static SaveData ToData(RunState run)
        {
            var d = new SaveData();
            d.Set("fmt", FormatVersion);
            run.WriteTo(d);
            return d;
        }

        /// <summary>Recria a run. Devolve null se o texto estiver vazio, corrompido ou for de um formato antigo.</summary>
        public static RunState Deserialize(string text)
        {
            return FromData(SaveData.Parse(text));
        }

        public static RunState FromData(SaveData d)
        {
            if (d == null || d.GetInt("fmt", -1) != FormatVersion) return null;
            try { return new RunState(d); }
            catch (Exception) { return null; }
        }

        /// <summary>Resumo curto para o menu sem carregar a run inteira, ex.: "WIZARD - ROOM 3/14". Null se nao houver save valido.</summary>
        public static string Summary(string text)
        {
            var d = SaveData.Parse(text);
            if (d.GetInt("fmt", -1) != FormatVersion) return null;
            var cur = d.Get("cur").Split('.');
            int layer;
            if (cur.Length < 1 || !int.TryParse(cur[0], out layer)) layer = 0;
            int last = d.Get("map").Split('/').Length - 1;
            string cls = d.Get("class", "WARRIOR").ToUpperInvariant();
            // TODO(ato): quando houver "act" no save, mostrar "ACT N - ROOM x/y"
            return cls + " - ROOM " + layer + "/" + last;
        }

        // ---------------- Busca por id ----------------

        static Dictionary<string, CardDef> cardsById;
        static List<CardDef> allCards;

        /// <summary>Todas as cartas declaradas em CardLibrary, na ordem do arquivo. Cartas novas entram sozinhas.</summary>
        public static List<CardDef> AllCards
        {
            get
            {
                if (allCards == null)
                {
                    allCards = new List<CardDef>();
                    cardsById = new Dictionary<string, CardDef>();
                    foreach (var f in typeof(CardLibrary).GetFields(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (f.FieldType != typeof(CardDef)) continue;
                        var c = f.GetValue(null) as CardDef;
                        if (c == null || c.Id == null || cardsById.ContainsKey(c.Id)) continue;
                        cardsById[c.Id] = c;
                        allCards.Add(c);
                    }
                }
                return allCards;
            }
        }

        public static CardDef FindCard(string id)
        {
            if (AllCards == null) return null;
            CardDef c;
            return cardsById.TryGetValue(id, out c) ? c : null;
        }

        public static RelicDef FindRelic(string id)
        {
            foreach (var r in RelicLibrary.All) if (r.Id.ToString() == id) return r;
            return null;
        }
    }

    public partial class RunState
    {
        /// <summary>Grava tudo o que a run precisa para continuar do mapa.</summary>
        internal void WriteTo(SaveData d)
        {
            d.Set("rules", RulesVersion);
            d.Set("seed", Dice.Seed);
            d.Set("calls", Dice.Calls);

            // ficha
            var s = Sheet;
            d.Set("class", s.Class.ToString());
            d.Set("name", s.Name);
            d.Set("level", s.Level);
            var scores = new List<string>();
            foreach (var v in s.Scores) scores.Add(v.ToString());
            d.SetList("scores", scores);
            d.Set("hp", s.Hp);
            d.Set("maxhp", s.MaxHp);
            d.Set("ac", s.BaseArmorClass);
            d.Set("energy", s.EnergyPerTurn);
            d.Set("hand", s.HandSize);
            var deck = new List<string>();
            foreach (var c in s.Deck) deck.Add(c.Id);
            d.SetList("deck", deck);
            var relics = new List<string>();
            foreach (var r in s.Relics) relics.Add(r.Id.ToString());
            d.SetList("relics", relics);

            // mapa: camadas separadas por '/', salas por ';', sala = Tipo:proximas(com '.'):visitada
            var layers = new List<string>();
            foreach (var layer in Layers)
            {
                var nodes = new List<string>();
                foreach (var n in layer)
                {
                    var next = new List<string>();
                    foreach (int i in n.Next) next.Add(i.ToString());
                    nodes.Add(n.Type + ":" + string.Join(".", next) + ":" + (n.Visited ? "1" : "0"));
                }
                layers.Add(string.Join(";", nodes));
            }
            d.Set("map", string.Join("/", layers));
            d.Set("cur", Current.Layer + "." + Current.Index);
            d.SetList("events", seenEvents);

            // TODO(campos novos da RunState): grave aqui. Ex.:
            //   d.Set("gold", Gold);
            //   d.Set("act", Act);
            //   d.SetList("shop", ...);   // se a loja tiver estado persistente
        }

        /// <summary>Recria uma run salva (ver RunSave.FromData). Lanca excecao se o save estiver quebrado.</summary>
        internal RunState(SaveData d)
        {
            RulesVersion = d.GetInt("rules", 1);
            Dice = new Dice(d.GetInt("seed", 0), d.GetLong("calls", 0));

            PlayerClass cls;
            if (!Enum.TryParse(d.Get("class"), out cls)) cls = PlayerClass.Warrior;
            Sheet = CharacterSheet.New(cls);
            var s = Sheet;
            s.Name = d.Get("name", s.Name);
            s.Level = d.GetInt("level", s.Level);
            var scores = d.GetList("scores");
            for (int i = 0; i < s.Scores.Length && i < scores.Count; i++) s.Scores[i] = int.Parse(scores[i]);
            s.MaxHp = d.GetInt("maxhp", s.MaxHp);
            s.Hp = d.GetInt("hp", s.Hp);
            s.BaseArmorClass = d.GetInt("ac", s.BaseArmorClass);
            s.EnergyPerTurn = d.GetInt("energy", s.EnergyPerTurn);
            s.HandSize = d.GetInt("hand", s.HandSize);
            if (d.Has("deck"))
            {
                s.Deck.Clear();
                foreach (var id in d.GetList("deck"))
                {
                    var c = RunSave.FindCard(id);
                    if (c != null) s.Deck.Add(c);   // carta removida do jogo: some do deck
                }
            }
            s.Relics.Clear();
            foreach (var id in d.GetList("relics"))
            {
                var r = RunSave.FindRelic(id);
                if (r != null) s.Relics.Add(r);
            }

            foreach (var ls in d.Get("map").Split('/'))
            {
                var layer = new List<MapNode>();
                foreach (var ns in ls.Split(';'))
                {
                    var parts = ns.Split(':');
                    var n = new MapNode { Layer = Layers.Count, Index = layer.Count };
                    n.Type = (NodeType)Enum.Parse(typeof(NodeType), parts[0]);
                    if (parts.Length > 1)
                        foreach (var k in parts[1].Split('.')) if (k.Length > 0) n.Next.Add(int.Parse(k));
                    n.Visited = parts.Length > 2 && parts[2] == "1";
                    layer.Add(n);
                }
                Layers.Add(layer);
            }
            if (Layers.Count < 2) throw new FormatException("mapa vazio");
            // links precisam apontar para salas que existem
            for (int l = 0; l < Layers.Count - 1; l++)
                foreach (var n in Layers[l])
                    foreach (int k in n.Next)
                        if (k < 0 || k >= Layers[l + 1].Count) throw new FormatException("link invalido");

            var cur = d.Get("cur", "0.0").Split('.');
            Current = Layers[int.Parse(cur[0])][int.Parse(cur[1])];
            Current.Visited = true;
            seenEvents.AddRange(d.GetList("events"));

            // TODO(campos novos da RunState): leia aqui, com padrao para saves antigos. Ex.:
            //   Gold = d.GetInt("gold", 0);
            //   Act = d.GetInt("act", 1);
        }
    }
}
