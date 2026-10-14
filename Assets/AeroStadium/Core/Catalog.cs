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
        public int[] learnableMoves;
        public string learnsetVersion;
        public string color;
        public float height;
    }

    [Serializable]
    public sealed class MoveDefinition
    {
        public int id;
        public string name, type, category, effect;
        public int power, accuracy, pp, priority;
        public string slug, description, target, availability;
        public int effectId, metaCategory;
        public bool supported;
        public MoveMechanics mechanics;
        public string Support { get { return mechanics == null ? "implemented" : mechanics.support ?? "unsupported"; } }
        public bool IsBattleUsable { get { return mechanics == null || Support == "implemented" || Support == "partial"; } }

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
    public sealed class StatChange
    {
        public string stat;
        public int change;
    }

    [Serializable]
    public sealed class MoveMechanics
    {
        public string support, ailment, fixedDamageKind;
        public int ailmentChance, flinchChance, statChance, healing, drain;
        public int minHits, maxHits, minTurns, maxTurns, fixedDamage, critRate;
        public bool selfTarget, contact;
        public StatChange[] statChanges;
    }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string id, name, effect, type;
        public float multiplier;
        public string slug, description, category, availability, support, cureStatus;
        public bool holdable;
        public int recoverPercent, fixedHeal;
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
                if (!TypeChart.IsKnown(move.type) || move.accuracy < 0 || move.accuracy > 100 || move.pp < 0 || move.pp > 100 || move.power < 0 || move.power > 1000 || move.priority < -7 || move.priority > 7)
                    throw new InvalidOperationException("Invalid move: " + move.name);
                if (move.CategoryKind != MoveCategory.Status && move.power == 0 && move.mechanics == null)
                    throw new InvalidOperationException("A damaging move needs positive power: " + move.name);
                if (move.CategoryKind == MoveCategory.Status && move.power != 0)
                    throw new InvalidOperationException("A status move cannot have damage power: " + move.name);
                string effect = Effects.Normalize(move.effect);
                if (move.mechanics != null) ValidateMechanics(move);
                else if (effect != "" && effect != "none" && effect != "damage" && effect != "flamecharge" && effect != "recoilthird" && effect != "swordsdance" && effect != "recover" && effect != "protect")
                    throw new InvalidOperationException("Unsupported move effect: " + move.effect);
                if (move.mechanics == null && move.CategoryKind == MoveCategory.Status && effect != "swordsdance" && effect != "recover" && effect != "protect")
                    throw new InvalidOperationException("A status move needs a supported effect: " + move.name);
                if (move.mechanics == null && move.CategoryKind != MoveCategory.Status && effect != "" && effect != "none" && effect != "damage" && effect != "flamecharge" && effect != "recoilthird")
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
                if (float.IsNaN(entry.height) || float.IsInfinity(entry.height) || entry.height < 0 || entry.height > 1000)
                    throw new InvalidOperationException("Invalid species height: " + entry.name);
                if (entry.moves == null || entry.moves.Length < 1 || entry.moves.Length > 4)
                    throw new InvalidOperationException("The prototype allows one to four moves: " + entry.name);
                var known = new HashSet<int>();
                foreach (int id in entry.moves)
                    if (!moveIndex.ContainsKey(id) || !known.Add(id)) throw new InvalidOperationException("Missing or duplicate move on " + entry.name);
                if (entry.learnableMoves != null)
                {
                    var legal = new HashSet<int>();
                    foreach (int id in entry.learnableMoves)
                        if (!moveIndex.ContainsKey(id) || !legal.Add(id)) throw new InvalidOperationException("Missing or duplicate learnable move on " + entry.name);
                    foreach (int id in entry.moves)
                        if (!legal.Contains(id)) throw new InvalidOperationException("An initial move is not in the legal learnset: " + entry.name);
                }
                speciesIndex.Add(entry.id, entry);
            }
            foreach (var item in items ?? Array.Empty<ItemDefinition>())
            {
                if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.name) || itemIndex.ContainsKey(item.id))
                    throw new InvalidOperationException("Invalid or duplicate item ID.");
                string effect = Effects.Normalize(item.effect);
                if (string.IsNullOrEmpty(item.support) && effect != "leftovers" && effect != "lifeorb" && effect != "charcoal" && effect != "typeboost" && effect != "none" && effect != "")
                    throw new InvalidOperationException("Unsupported held item effect: " + item.effect);
                if ((effect == "charcoal" || effect == "typeboost") && (!TypeChart.IsKnown(string.IsNullOrEmpty(item.type) ? "Fire" : item.type) || float.IsNaN(item.multiplier) || float.IsInfinity(item.multiplier) || item.multiplier < 0 || item.multiplier > 10))
                    throw new InvalidOperationException("Invalid type booster: " + item.name);
                itemIndex.Add(item.id, item);
            }
            speciesById = speciesIndex;
            movesById = moveIndex;
            itemsById = itemIndex;
        }

        private static void ValidateMechanics(MoveDefinition move)
        {
            MoveMechanics m = move.mechanics;
            if (m.support != "implemented" && m.support != "partial" && m.support != "unsupported")
                throw new InvalidOperationException("A catalog move needs an explicit support level: " + move.name);
            if (m.ailmentChance < 0 || m.ailmentChance > 100 || m.flinchChance < 0 || m.flinchChance > 100 || m.statChance < 0 || m.statChance > 100 || m.healing < -100 || m.healing > 100 || m.drain < -100 || m.drain > 100 || m.minHits < 0 || m.maxHits < 0 || m.minHits > m.maxHits || m.maxHits > 10 || m.fixedDamage < 0)
                throw new InvalidOperationException("Invalid move mechanics: " + move.name);
            foreach (StatChange change in m.statChanges ?? Array.Empty<StatChange>())
                if (change == null || !BattlePokemon.IsStageStat(change.stat) || change.change < -6 || change.change > 6 || change.change == 0)
                    throw new InvalidOperationException("Invalid stat change: " + move.name);
        }

        public int[] GetLearnableMoves(int speciesId)
        {
            SpeciesDefinition entry = GetSpecies(speciesId);
            return (int[])(entry.learnableMoves ?? entry.moves).Clone();
        }

        public void ValidateTeamMember(TeamMember member)
        {
            if (member == null) throw new ArgumentNullException(nameof(member));
            SpeciesDefinition entry = GetSpecies(member.speciesId);
            int[] selected = member.moveIds ?? entry.moves;
            if (selected.Length < 1 || selected.Length > 4) throw new ArgumentException("Select one to four moves.", nameof(member));
            var legal = new HashSet<int>(entry.learnableMoves ?? entry.moves);
            var distinct = new HashSet<int>();
            foreach (int id in selected)
                if (!legal.Contains(id) || !distinct.Add(id)) throw new ArgumentException("Unknown, duplicate or illegal move for " + entry.name + ": " + id, nameof(member));
            (member.ivs ?? new StatValues(31)).Validate(31, -1, nameof(member.ivs));
            (member.evs ?? new StatValues()).Validate(252, 510, nameof(member.evs));
            ItemDefinition item = GetItem(member.itemId);
            if (item != null && item.id != "none" && !string.IsNullOrEmpty(item.support) && !item.holdable)
                throw new ArgumentException("This item cannot be held in battle: " + item.name, nameof(member));
        }

        public BattlePokemon CreatePokemon(TeamMember member, int level = 50)
        {
            if (level < 1 || level > 100) throw new ArgumentOutOfRangeException(nameof(level));
            ValidateTeamMember(member);
            return new BattlePokemon(this, member.Copy(), level);
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
