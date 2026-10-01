using System;
using System.Collections.Generic;

namespace AeroStadium.Core
{
    public enum MoveCategory { Physical, Special, Status }

    [Serializable]
    public sealed class BaseStats
    {
        public int hp, attack, defense, specialAttack, specialDefense, speed;
    }

    [Serializable]
    public sealed class SpeciesDefinition
    {
        public int id;
        public string name;
        public string[] types;
        public BaseStats stats;
        public int[] moves;
        public string color;
    }

    [Serializable]
    public sealed class MoveDefinition
    {
        public int id;
        public string name, type, category, effect;
        public int power, accuracy, pp, priority;

        public MoveCategory CategoryKind
        {
            get
            {
                MoveCategory parsed;
                if (!Enum.TryParse(category, true, out parsed) || !Enum.IsDefined(typeof(MoveCategory), parsed))
                    throw new InvalidOperationException("Unknown move category: " + category);
                return parsed;
            }
        }
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string id, name, effect, type;
        public float multiplier;
    }

    /// <summary>JSON-friendly definitions. Treat catalog data as immutable after validation.</summary>
    [Serializable]
    public sealed class Catalog
    {
        public SpeciesDefinition[] species;
        public MoveDefinition[] moves;
        public ItemDefinition[] items;

        private Dictionary<int, SpeciesDefinition> speciesById;
        private Dictionary<int, MoveDefinition> movesById;
        private Dictionary<string, ItemDefinition> itemsById;

        public void Validate()
        {
            if (species == null || species.Length == 0 || moves == null || moves.Length == 0)
                throw new InvalidOperationException("The catalog needs species and moves.");
            var speciesIndex = new Dictionary<int, SpeciesDefinition>();
            var moveIndex = new Dictionary<int, MoveDefinition>();
            var itemIndex = new Dictionary<string, ItemDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var move in moves)
            {
                if (move == null || move.id <= 0 || string.IsNullOrWhiteSpace(move.name) || moveIndex.ContainsKey(move.id))
                    throw new InvalidOperationException("Invalid or duplicate move ID.");
                if (!TypeChart.IsKnown(move.type) || move.accuracy < 0 || move.accuracy > 100 || move.pp < 1 || move.pp > 100 || move.power < 0 || move.power > 1000)
                    throw new InvalidOperationException("Invalid move: " + move.name);
                if (move.CategoryKind != MoveCategory.Status && move.power == 0)
                    throw new InvalidOperationException("A damaging move needs positive power: " + move.name);
                if (move.CategoryKind == MoveCategory.Status && move.power != 0)
                    throw new InvalidOperationException("A status move cannot have damage power: " + move.name);
                string effect = Effects.Normalize(move.effect);
                if (effect != "" && effect != "none" && effect != "damage" && effect != "flamecharge" && effect != "recoilthird" && effect != "swordsdance" && effect != "recover" && effect != "protect")
                    throw new InvalidOperationException("Unsupported move effect: " + move.effect);
                if (move.CategoryKind == MoveCategory.Status && effect != "swordsdance" && effect != "recover" && effect != "protect")
                    throw new InvalidOperationException("A status move needs a supported effect: " + move.name);
                if (move.CategoryKind != MoveCategory.Status && effect != "" && effect != "none" && effect != "damage" && effect != "flamecharge" && effect != "recoilthird")
                    throw new InvalidOperationException("Status effect on a damaging move: " + move.name);
                moveIndex.Add(move.id, move);
            }
            foreach (var entry in species)
            {
                if (entry == null || entry.id <= 0 || string.IsNullOrWhiteSpace(entry.name) || speciesIndex.ContainsKey(entry.id))
                    throw new InvalidOperationException("Invalid or duplicate species ID.");
                if (entry.types == null || entry.types.Length < 1 || entry.types.Length > 2)
                    throw new InvalidOperationException("A species needs one or two types: " + entry.name);
                foreach (string type in entry.types)
                    if (!TypeChart.IsKnown(type)) throw new InvalidOperationException("Unknown type: " + type);
                if (entry.types.Length == 2 && string.Equals(entry.types[0], entry.types[1], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Duplicate species type: " + entry.name);
                BaseStats s = entry.stats;
                if (s == null || !ValidStat(s.hp) || !ValidStat(s.attack) || !ValidStat(s.defense) || !ValidStat(s.specialAttack) || !ValidStat(s.specialDefense) || !ValidStat(s.speed))
                    throw new InvalidOperationException("Invalid base stats: " + entry.name);
                if (entry.moves == null || entry.moves.Length < 1 || entry.moves.Length > 4)
                    throw new InvalidOperationException("The prototype allows one to four moves: " + entry.name);
                var known = new HashSet<int>();
                foreach (int id in entry.moves)
                    if (!moveIndex.ContainsKey(id) || !known.Add(id)) throw new InvalidOperationException("Missing or duplicate move on " + entry.name);
                speciesIndex.Add(entry.id, entry);
            }
            foreach (var item in items ?? Array.Empty<ItemDefinition>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.name) || itemIndex.ContainsKey(item.id))
                    throw new InvalidOperationException("Invalid or duplicate item ID.");
                string effect = Effects.Normalize(item.effect);
                if (effect != "leftovers" && effect != "lifeorb" && effect != "charcoal" && effect != "typeboost" && effect != "none" && effect != "")
                    throw new InvalidOperationException("Unsupported held item effect: " + item.effect);
                if ((effect == "charcoal" || effect == "typeboost") && (!TypeChart.IsKnown(string.IsNullOrEmpty(item.type) ? "Fire" : item.type) || float.IsNaN(item.multiplier) || float.IsInfinity(item.multiplier) || item.multiplier < 0 || item.multiplier > 10))
                    throw new InvalidOperationException("Invalid type booster: " + item.name);
                itemIndex.Add(item.id, item);
            }
            speciesById = speciesIndex;
            movesById = moveIndex;
            itemsById = itemIndex;
        }

        private static bool ValidStat(int value) { return value > 0 && value <= 4096; }
        private void EnsureValidated() { if (speciesById == null) Validate(); }
        public SpeciesDefinition GetSpecies(int id)
        {
            EnsureValidated();
            SpeciesDefinition value;
            if (!speciesById.TryGetValue(id, out value)) throw new ArgumentException("Unknown species ID: " + id);
            return value;
        }
        public MoveDefinition GetMove(int id)
        {
            EnsureValidated();
            MoveDefinition value;
            if (!movesById.TryGetValue(id, out value)) throw new ArgumentException("Unknown move ID: " + id);
            return value;
        }
        public ItemDefinition GetItem(string id)
        {
            EnsureValidated();
            if (string.IsNullOrEmpty(id)) return null;
            ItemDefinition value;
            if (!itemsById.TryGetValue(id, out value)) throw new ArgumentException("Unknown item ID: " + id);
            return value;
        }
    }

    internal static class Effects
    {
        internal static string Normalize(string effect)
        {
            string value = (effect ?? "").Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
            switch (value)
            {
                case "speedup": case "speedup1": case "speed1": return "flamecharge";
                case "attackup": case "attackup2": case "attack2": return "swordsdance";
                case "heal": case "healhalf": return "recover";
                case "boostfire": return "charcoal";
                default: return value;
            }
        }
    }
}
