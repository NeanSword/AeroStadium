using System;
using System.Collections.Generic;

namespace AeroStadium.Core
{
    public sealed partial class BattlePokemon
    {
        public int DefenseStage { get; internal set; }
        public int SpecialAttackStage { get; internal set; }
        public int SpecialDefenseStage { get; internal set; }
        public int AccuracyStage { get; internal set; }
        public int EvasionStage { get; internal set; }
        public string Status { get; internal set; } = "none";
        public bool ItemConsumed { get; internal set; }
        internal bool Flinched;
        internal int SleepTurns, ToxicTurns, ChoiceMoveId;
        private SpeciesDefinition originalSpecies;
        private IReadOnlyList<MoveSlot> originalMoves;
        private int originalAttack, originalDefense, originalSpecialAttack, originalSpecialDefense, originalSpeed;
        public bool IsTransformed { get; private set; }
        public int OriginalSpeciesId { get { return originalSpecies == null ? SpeciesId : originalSpecies.id; } }
        private void CaptureOriginal()
        {
            originalSpecies = Species; originalMoves = Moves;
            originalAttack = Attack; originalDefense = Defense; originalSpecialAttack = SpecialAttack; originalSpecialDefense = SpecialDefense; originalSpeed = Speed;
        }
        private void RestoreOriginal()
        {
            if (!IsTransformed) return;
            Species = originalSpecies; Moves = originalMoves;
            Attack = originalAttack; Defense = originalDefense; SpecialAttack = originalSpecialAttack; SpecialDefense = originalSpecialDefense; Speed = originalSpeed;
            IsTransformed = false;
        }
        internal void TransformInto(BattlePokemon target)
        {
            Species = target.Species; Attack = target.Attack; Defense = target.Defense;
            SpecialAttack = target.SpecialAttack; SpecialDefense = target.SpecialDefense; Speed = target.Speed;
            AttackStage = target.AttackStage; DefenseStage = target.DefenseStage; SpecialAttackStage = target.SpecialAttackStage;
            SpecialDefenseStage = target.SpecialDefenseStage; SpeedStage = target.SpeedStage; AccuracyStage = target.AccuracyStage; EvasionStage = target.EvasionStage;
            var slots = new MoveSlot[target.Moves.Count];
            for (int i = 0; i < slots.Length; i++) slots[i] = new MoveSlot(target.Moves[i].Definition, Math.Min(5, target.Moves[i].MaxPp));
            Moves = Array.AsReadOnly(slots); IsTransformed = true;
        }
        internal int EffectiveDefense { get { return ApplyStage(Defense, DefenseStage); } }
        internal int EffectiveSpecialAttack { get { return ItemStat(ApplyStage(SpecialAttack, SpecialAttackStage), "choicespecs"); } }
        internal int EffectiveSpecialDefense { get { return ItemStat(ApplyStage(SpecialDefense, SpecialDefenseStage), "assaultvest"); } }
        private int ItemStat(int value, string effect)
        {
            return !ItemConsumed && HeldItem != null && Effects.Normalize(HeldItem.effect) == effect ? Math.Max(1, value * 3 / 2) : value;
        }
        internal static string StatKey(string stat)
        {
            switch ((stat ?? "").Replace("-", "").Replace("_", "").ToLowerInvariant())
            {
                case "atk": case "attack": return "attack";
                case "def": case "defense": return "defense";
                case "spa": case "spatk": case "specialattack": return "specialAttack";
                case "spd": case "spdef": case "specialdefense": return "specialDefense";
                case "spe": case "speed": return "speed";
                case "accuracy": return "accuracy";
                case "evasion": return "evasion";
                default: return "";
            }
        }
        internal static bool IsStageStat(string stat) { return StatKey(stat) != ""; }
        internal int Stage(string stat)
        {
            switch (StatKey(stat))
            {
                case "attack": return AttackStage;
                case "defense": return DefenseStage;
                case "specialAttack": return SpecialAttackStage;
                case "specialDefense": return SpecialDefenseStage;
                case "speed": return SpeedStage;
                case "accuracy": return AccuracyStage;
                case "evasion": return EvasionStage;
                default: throw new ArgumentException("Unknown stat: " + stat);
            }
        }
        internal int ChangeStage(string stat, int change)
        {
            string key = StatKey(stat);
            int old = Stage(key), value = Math.Max(-6, Math.Min(6, old + change));
            switch (key)
            {
                case "attack": AttackStage = value; break;
                case "defense": DefenseStage = value; break;
                case "specialAttack": SpecialAttackStage = value; break;
                case "specialDefense": SpecialDefenseStage = value; break;
                case "speed": SpeedStage = value; break;
                case "accuracy": AccuracyStage = value; break;
                case "evasion": EvasionStage = value; break;
            }
            return value - old;
        }
    }

    public sealed partial class BattleEngine
    {
        private static void Transform(int side, BattlePokemon actor, BattlePokemon target, List<BattleEvent> events)
        {
            if (actor.IsTransformed || target.IsTransformed || target.IsFainted)
            {
                events.Add(new BattleEvent(BattleEventKind.Failed, side, "La transformation échoue."));
                return;
            }
            actor.TransformInto(target);
            events.Add(new BattleEvent(BattleEventKind.StageChanged, side, actor.Name + " prend la forme de " + target.Species.name + " !"));
        }
        private static bool IsChoiceItem(BattlePokemon actor)
        {
            string effect = ItemEffect(actor);
            return effect == "choiceband" || effect == "choicespecs" || effect == "choicescarf";
        }
        public bool CanUseMove(int side, int slot)
        {
            CheckSide(side);
            BattlePokemon actor = Active(side);
            return slot >= 0 && slot < actor.Moves.Count && actor.Moves[slot].Pp > 0 && CanSelectMove(actor, actor.Moves[slot].Definition);
        }
        private static bool CanSelectMove(BattlePokemon actor, MoveDefinition move)
        {
            if (ItemEffect(actor) == "assaultvest" && move.CategoryKind == MoveCategory.Status) return false;
            return !IsChoiceItem(actor) || actor.ChoiceMoveId == 0 || actor.ChoiceMoveId == move.id;
        }
        private bool CanAct(BattlePokemon actor, int side, List<BattleEvent> events)
        {
            string reason = null;
            if (actor.Flinched) reason = "a peur et ne peut pas agir";
            else if (actor.Status == "sleep")
            {
                actor.SleepTurns--;
                if (actor.SleepTurns <= 0) Cure(actor, side, events);
                else reason = "dort profondément";
            }
            else if (actor.Status == "freeze")
            {
                if (random.Next(100) < 20) Cure(actor, side, events);
                else reason = "est gelé";
            }
            else if (actor.Status == "paralysis" && random.Next(100) < 25) reason = "est paralysé et ne peut pas agir";
            if (reason == null) return true;
            events.Add(new BattleEvent(BattleEventKind.Skipped, side, actor.Name + " " + reason + "."));
            return false;
        }
        private bool AccuracyHits(BattlePokemon actor, BattlePokemon target, MoveDefinition move)
        {
            if (move.accuracy == 0) return true;
            int stage = Math.Max(-6, Math.Min(6, actor.AccuracyStage - target.EvasionStage));
            double multiplier = stage >= 0 ? (3.0 + stage) / 3 : 3.0 / (3 - stage);
            double accuracy = move.accuracy * multiplier;
            return accuracy >= 100 || random.Next(100) < accuracy;
        }
        private int HitDamagingMove(int side, BattlePokemon actor, BattlePokemon target, MoveDefinition move, List<BattleEvent> events)
        {
            MoveMechanics m = move.mechanics;
            if (move.power == 0 && (m == null || (m.fixedDamage == 0 && string.IsNullOrEmpty(m.fixedDamageKind))))
            {
                events.Add(new BattleEvent(BattleEventKind.Failed, side, "Le calcul spécialisé de " + move.name + " n'est pas encore pris en charge.", moveId: move.id));
                return 0;
            }
            int hits = 1;
            if (m != null && m.maxHits > 1)
            {
                if (m.minHits == m.maxHits) hits = m.maxHits;
                else if (m.minHits == 2 && m.maxHits == 5)
                {
                    int roll = random.Next(100);
                    hits = roll < 35 ? 2 : roll < 70 ? 3 : roll < 85 ? 4 : 5;
                }
                else hits = m.minHits + random.Next(m.maxHits - m.minHits + 1);
            }
            int actualDamage = 0;
            for (int hit = 0; hit < hits && !target.IsFainted && !actor.IsFainted; hit++)
            {
                // Snapshot the type interaction while the move resolves. Presentation must not
                // re-read a target that may transform or leave the field later in this turn.
                double hitEffectiveness = m != null && (m.fixedDamage > 0 ||
                    m.fixedDamageKind == "level" || m.fixedDamageKind == "half-hp")
                    ? 1d : TypeChart.Effectiveness(move.type, target.Species.types);
                int damage = Damage(actor, target, move, 85 + random.Next(16));
                int actual = Math.Min(target.Hp, damage);
                int before = target.Hp;
                ApplyDamage(target, 1 - side, damage, side, move.id, events, hitEffectiveness);
                actualDamage += Math.Min(actual, before - target.Hp);
                if (m != null && m.contact && ItemEffect(target) == "rockyhelmet")
                {
                    events.Add(new BattleEvent(BattleEventKind.ItemActivated, 1 - side, target.Name + " active son Casque Brut."));
                    ApplyDamage(actor, side, Math.Max(1, actor.MaxHp / 6), 1 - side, 0, events);
                }
            }
            if (actualDamage > 0 && !target.IsFainted) TriggerBerry(target, 1 - side, events);
            return actualDamage;
        }
        private void ExecuteMetadataStatus(int side, BattlePokemon actor, MoveDefinition move, List<BattleEvent> events)
        {
            MoveMechanics m = move.mechanics;
            if (m == null)
            {
                events.Add(new BattleEvent(BattleEventKind.Failed, side, "L'effet de " + move.name + " n'est pas pris en charge.", moveId: move.id));
                return;
            }
            bool self = m.selfTarget || move.target == "user" || move.target == "self" || move.target == "user-and-allies";
            BattlePokemon target = self ? actor : Active(1 - side);
            int targetSide = self ? side : 1 - side;
            if (target.IsFainted || (!self && target.Protected))
            {
                events.Add(new BattleEvent(BattleEventKind.Failed, side, "La capacité ne peut pas atteindre sa cible.", moveId: move.id));
                return;
            }
            if (!self && !AccuracyHits(actor, target, move))
            {
                events.Add(new BattleEvent(BattleEventKind.Miss, side, "L'attaque manque sa cible.", moveId: move.id));
                return;
            }
            if (!self && m.ailment == "paralysis" && move.type == "Electric" && HasType(target, "Ground"))
            {
                events.Add(new BattleEvent(BattleEventKind.Immune, targetSide, "Cela n'affecte pas " + target.Name + ".", moveId: move.id, hitEffectiveness: 0d));
                return;
            }
            bool changed = false;
            if (m.healing > 0)
            {
                if (target.Hp < target.MaxHp) { Heal(target, targetSide, Math.Max(1, target.MaxHp * m.healing / 100), events); changed = true; }
            }
            foreach (StatChange change in m.statChanges ?? Array.Empty<StatChange>())
                changed |= ChangeStat(target, targetSide, change, events);
            if (!string.IsNullOrEmpty(m.ailment) && m.ailment != "none") changed |= InflictStatus(target, targetSide, m.ailment, events);
            if (!changed) events.Add(new BattleEvent(BattleEventKind.Failed, side, "La capacité échoue : aucun effet applicable.", moveId: move.id));
        }
        private void ApplyMetadataSecondary(int side, BattlePokemon actor, BattlePokemon target, MoveDefinition move, int actualDamage, List<BattleEvent> events)
        {
            MoveMechanics m = move.mechanics;
            if (m == null || actualDamage <= 0) return;
            if (m.drain > 0 && !actor.IsFainted) Heal(actor, side, Math.Max(1, actualDamage * m.drain / 100), events);
            else if (m.drain < 0 && !actor.IsFainted) ApplyDamage(actor, side, Math.Max(1, actualDamage * -m.drain / 100), side, 0, events);
            if (m.healing < 0 && !actor.IsFainted) ApplyDamage(actor, side, Math.Max(1, actor.MaxHp * -m.healing / 100), side, 0, events);
            if (!target.IsFainted && m.ailmentChance > 0 && Chance(m.ailmentChance)) InflictStatus(target, 1 - side, m.ailment, events);
            if (!target.IsFainted && m.flinchChance > 0 && Chance(m.flinchChance)) target.Flinched = true;
            if (m.statChanges != null && Chance(m.statChance == 0 ? 100 : m.statChance))
            {
                BattlePokemon receiver = m.selfTarget ? actor : target;
                int targetSide = m.selfTarget ? side : 1 - side;
                if (!receiver.IsFainted) foreach (StatChange change in m.statChanges) ChangeStat(receiver, targetSide, change, events);
            }
        }
        private bool Chance(int percent) { return percent >= 100 || random.Next(100) < percent; }
        private static bool ChangeStat(BattlePokemon target, int side, StatChange change, List<BattleEvent> events)
        {
            int actual = target.ChangeStage(change.stat, change.change);
            if (actual == 0) return false;
            events.Add(new BattleEvent(BattleEventKind.StageChanged, side, BattlePokemon.StatKey(change.stat) + " de " + target.Name + (actual > 0 ? " augmente !" : " diminue !"), actual));
            return true;
        }
        private bool InflictStatus(BattlePokemon target, int side, string status, List<BattleEvent> events)
        {
            status = (status ?? "").ToLowerInvariant();
            if (status == "bad-poison" || status == "badly-poisoned") status = "toxic";
            if (target.Status != "none" || target.IsFainted) return false;
            if (status != "burn" && status != "paralysis" && status != "poison" && status != "toxic" && status != "sleep" && status != "freeze") return false;
            if ((status == "burn" && HasType(target, "Fire")) || (status == "paralysis" && HasType(target, "Electric")) || ((status == "poison" || status == "toxic") && (HasType(target, "Poison") || HasType(target, "Steel"))) || (status == "freeze" && HasType(target, "Ice"))) return false;
            target.Status = status;
            target.SleepTurns = status == "sleep" ? 2 + random.Next(3) : 0;
            target.ToxicTurns = 0;
            events.Add(new BattleEvent(BattleEventKind.StatusChanged, side, target.Name + " : " + StatusName(status) + "."));
            TriggerBerry(target, side, events);
            return true;
        }
        private static string StatusName(string status)
        {
            switch (status)
            {
                case "burn": return "brûlure";
                case "paralysis": return "paralysie";
                case "poison": return "empoisonnement";
                case "toxic": return "empoisonnement grave";
                case "sleep": return "sommeil";
                case "freeze": return "gel";
                default: return "statut normal";
            }
        }
        private static void Cure(BattlePokemon target, int side, List<BattleEvent> events)
        {
            target.Status = "none"; target.SleepTurns = target.ToxicTurns = 0;
            events.Add(new BattleEvent(BattleEventKind.StatusChanged, side, target.Name + " est guéri."));
        }
        private static bool HasType(BattlePokemon target, string type)
        {
            foreach (string value in target.Species.types) if (string.Equals(value, type, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        private static double StatusAiScore(BattlePokemon actor, BattlePokemon target, MoveDefinition move)
        {
            MoveMechanics m = move.mechanics;
            if (m.healing > 0 && actor.Hp * 2 < actor.MaxHp) return actor.MaxHp * m.healing / 100.0;
            if (m.ailment != null && m.ailment != "none" && target.Status == "none") return 20;
            if (m.statChanges != null) foreach (StatChange change in m.statChanges)
                if (m.selfTarget && actor.Stage(change.stat) < 2 && change.change > 0) return 15;
            return 0;
        }
        private static void ResidualStatus(int side, BattlePokemon pokemon, List<BattleEvent> events)
        {
            if (pokemon.IsFainted) return;
            int damage = 0;
            if (pokemon.Status == "burn") damage = Math.Max(1, pokemon.MaxHp / 16);
            else if (pokemon.Status == "poison") damage = Math.Max(1, pokemon.MaxHp / 8);
            else if (pokemon.Status == "toxic") { pokemon.ToxicTurns++; damage = Math.Max(1, pokemon.MaxHp * Math.Min(15, pokemon.ToxicTurns) / 16); }
            if (damage > 0) ApplyDamage(pokemon, side, damage, side, 0, events);
            if (!pokemon.IsFainted) TriggerBerry(pokemon, side, events);
        }
        private static void TriggerBerry(BattlePokemon pokemon, int side, List<BattleEvent> events)
        {
            ItemDefinition item = pokemon.HeldItem;
            if (item == null || pokemon.ItemConsumed || pokemon.IsFainted) return;
            string effect = ItemEffect(pokemon);
            if (effect == "lumberry" || effect == "statusberry")
            {
                if (pokemon.Status == "none") return;
                string cure = item.cureStatus ?? "all";
                if (effect != "lumberry" && cure != "all" && cure != pokemon.Status && !(cure == "poison" && pokemon.Status == "toxic")) return;
                pokemon.ItemConsumed = true;
                events.Add(new BattleEvent(BattleEventKind.ItemActivated, side, pokemon.Name + " utilise " + item.name + "."));
                Cure(pokemon, side, events);
            }
            else if ((effect == "sitrusberry" || effect == "oranberry" || effect == "healingberry") && pokemon.Hp * 2 <= pokemon.MaxHp)
            {
                pokemon.ItemConsumed = true;
                events.Add(new BattleEvent(BattleEventKind.ItemActivated, side, pokemon.Name + " utilise " + item.name + "."));
                int amount = item.fixedHeal > 0 ? item.fixedHeal : effect == "oranberry" ? 10 : Math.Max(1, pokemon.MaxHp * (item.recoverPercent > 0 ? item.recoverPercent : 25) / 100);
                Heal(pokemon, side, amount, events);
            }
        }
        private static void HeldEndTurn(int side, BattlePokemon pokemon, List<BattleEvent> events)
        {
            if (pokemon.IsFainted) return;
            if (ItemEffect(pokemon) == "blacksludge")
            {
                events.Add(new BattleEvent(BattleEventKind.ItemActivated, side, pokemon.Name + " active sa Boue Noire."));
                if (HasType(pokemon, "Poison")) Heal(pokemon, side, Math.Max(1, pokemon.MaxHp / 16), events);
                else ApplyDamage(pokemon, side, Math.Max(1, pokemon.MaxHp / 8), side, 0, events);
            }
        }
    }
}
