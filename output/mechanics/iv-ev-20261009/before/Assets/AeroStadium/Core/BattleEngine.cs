using System;
using System.Collections.Generic;

namespace AeroStadium.Core
{
    [Serializable]
    public sealed class TeamMember
    {
        public int speciesId;
        public string itemId;
        public TeamMember() { }
        public TeamMember(int speciesId, string itemId = null) { this.speciesId = speciesId; this.itemId = itemId; }
    }

    public enum BattleChoiceKind { Move, Switch, Struggle }

    public sealed class BattleChoice
    {
        public BattleChoiceKind Kind { get; private set; }
        public int Index { get; private set; }
        private BattleChoice(BattleChoiceKind kind, int index) { Kind = kind; Index = index; }
        public static BattleChoice Move(int slot) { return new BattleChoice(BattleChoiceKind.Move, slot); }
        public static BattleChoice Switch(int teamIndex) { return new BattleChoice(BattleChoiceKind.Switch, teamIndex); }
        public static BattleChoice Struggle() { return new BattleChoice(BattleChoiceKind.Struggle, -1); }
    }

    public enum BattleEventKind
    {
        TurnStarted, MoveUsed, Damage, Heal, Miss, Immune, Protected, Failed,
        StageChanged, Fainted, Switched, Skipped, ItemActivated, BattleEnded
    }

    public sealed class BattleEvent
    {
        public BattleEventKind Kind { get; private set; }
        public int Side { get; private set; }
        public int TargetSide { get; private set; }
        public int Amount { get; private set; }
        public int MoveId { get; private set; }
        public int TeamIndex { get; private set; }
        public string Message { get; private set; }
        internal BattleEvent(BattleEventKind kind, int side, string message, int amount = 0, int targetSide = -1, int moveId = 0, int teamIndex = -1)
        {
            Kind = kind; Side = side; Message = message; Amount = amount;
            TargetSide = targetSide; MoveId = moveId; TeamIndex = teamIndex;
        }
        public override string ToString() { return Message; }
    }

    public sealed class MoveSlot
    {
        public MoveDefinition Definition { get; private set; }
        public int Pp { get; internal set; }
        public int MaxPp { get { return Definition.pp; } }
        internal MoveSlot(MoveDefinition definition) { Definition = definition; Pp = definition.pp; }
    }

    public sealed class BattlePokemon
    {
        public SpeciesDefinition Species { get; private set; }
        public int SpeciesId { get { return Species.id; } }
        public string Name { get { return Species.name; } }
        public string[] Types { get { return (string[])Species.types.Clone(); } }
        public int Level { get; private set; }
        public int MaxHp { get; private set; }
        public int Hp { get; internal set; }
        public int Attack { get; private set; }
        public int Defense { get; private set; }
        public int SpecialAttack { get; private set; }
        public int SpecialDefense { get; private set; }
        public int Speed { get; private set; }
        public int AttackStage { get; internal set; }
        public int SpeedStage { get; internal set; }
        public string ItemId { get { return HeldItem == null ? null : HeldItem.id; } }
        public IReadOnlyList<MoveSlot> Moves { get; private set; }
        public bool IsFainted { get { return Hp == 0; } }
        internal ItemDefinition HeldItem;
        internal bool Protected;
        internal bool UsedProtectLastTurn;

        internal BattlePokemon(Catalog catalog, TeamMember member, int level)
        {
            Species = catalog.GetSpecies(member.speciesId);
            HeldItem = catalog.GetItem(member.itemId);
            Level = level;
            BaseStats b = Species.stats;
            MaxHp = ((2 * b.hp + 31) * level / 100) + level + 10;
            Hp = MaxHp;
            Attack = Stat(b.attack, level); Defense = Stat(b.defense, level);
            SpecialAttack = Stat(b.specialAttack, level); SpecialDefense = Stat(b.specialDefense, level);
            Speed = Stat(b.speed, level);
            var slots = new MoveSlot[Species.moves.Length];
            for (int i = 0; i < slots.Length; i++) slots[i] = new MoveSlot(catalog.GetMove(Species.moves[i]));
            Moves = Array.AsReadOnly(slots);
        }
        private static int Stat(int basis, int level) { return ((2 * basis + 31) * level / 100) + 5; }
        internal int EffectiveAttack { get { return ApplyStage(Attack, AttackStage); } }
        internal int EffectiveSpeed { get { return ApplyStage(Speed, SpeedStage); } }
        private static int ApplyStage(int stat, int stage)
        {
            return Math.Max(1, stage >= 0 ? stat * (2 + stage) / 2 : stat * 2 / (2 - stage));
        }
        internal void LeaveField() { AttackStage = 0; SpeedStage = 0; Protected = false; UsedProtectLastTurn = false; }
    }

    /// <summary>Deterministic singles battle with small teams; no Unity dependencies.</summary>
    public sealed class BattleEngine
    {
        private readonly BattlePokemon[][] teams;
        private readonly IReadOnlyList<BattlePokemon>[] teamViews;
        private readonly int[] activeIndices = { 0, 0 };
        private readonly DeterministicRandom random;
        public int Turn { get; private set; }
        public bool IsFinished { get; private set; }
        /// <summary>Null while playing; 0 player, 1 opponent, -1 simultaneous knockout draw.</summary>
        public int? Winner { get; private set; }

        public BattleEngine(Catalog catalog, int[] playerTeam, int[] opponentTeam, int seed = 1, int level = 50)
            : this(catalog, Members(playerTeam), Members(opponentTeam), seed, level) { }

        public BattleEngine(Catalog catalog, TeamMember[] playerTeam, TeamMember[] opponentTeam, int seed = 1, int level = 50)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (level < 1 || level > 100) throw new ArgumentOutOfRangeException(nameof(level));
            catalog.Validate();
            teams = new[] { CreateTeam(catalog, playerTeam, level), CreateTeam(catalog, opponentTeam, level) };
            teamViews = new IReadOnlyList<BattlePokemon>[] { Array.AsReadOnly(teams[0]), Array.AsReadOnly(teams[1]) };
            random = new DeterministicRandom(seed);
        }

        public BattlePokemon Active(int side) { CheckSide(side); return teams[side][activeIndices[side]]; }
        public IReadOnlyList<BattlePokemon> Team(int side) { CheckSide(side); return teamViews[side]; }
        public int ActiveIndex(int side) { CheckSide(side); return activeIndices[side]; }

        /// <summary>Validates BOTH choices before changing state or consuming random values.</summary>
        public IReadOnlyList<BattleEvent> ResolveTurn(BattleChoice playerChoice, BattleChoice opponentChoice)
        {
            if (IsFinished) throw new InvalidOperationException("The battle has ended.");
            ValidateChoice(0, playerChoice);
            ValidateChoice(1, opponentChoice);
            var events = new List<BattleEvent>();
            BattleChoice[] choices = { playerChoice, opponentChoice };
            BattlePokemon[] actors = { Active(0), Active(1) };
            int p0 = Priority(actors[0], playerChoice), p1 = Priority(actors[1], opponentChoice);
            int first;
            if (p0 != p1) first = p0 > p1 ? 0 : 1;
            else if (actors[0].EffectiveSpeed != actors[1].EffectiveSpeed) first = actors[0].EffectiveSpeed > actors[1].EffectiveSpeed ? 0 : 1;
            else first = random.Next(2);
            Turn++;
            events.Add(new BattleEvent(BattleEventKind.TurnStarted, -1, "Tour " + Turn));
            actors[0].Protected = false; actors[1].Protected = false;
            Execute(first, actors[first], choices[first], events);
            Execute(1 - first, actors[1 - first], choices[1 - first], events);
            for (int side = 0; side < 2; side++) EndTurn(side, events);
            for (int side = 0; side < 2; side++) AutoReplace(side, events);
            bool playerAlive = HasAlive(0), opponentAlive = HasAlive(1);
            if (!playerAlive || !opponentAlive)
            {
                IsFinished = true;
                Winner = playerAlive ? 0 : opponentAlive ? 1 : -1;
                events.Add(new BattleEvent(BattleEventKind.BattleEnded, Winner.Value,
                    Winner == -1 ? "Match nul !" : Winner == 0 ? "Victoire !" : "Défaite !"));
            }
            return events.AsReadOnly();
        }

        /// <summary>Simple deterministic AI, without mutating state or advancing the random stream.</summary>
        public BattleChoice ChooseAi(int side)
        {
            CheckSide(side);
            if (IsFinished) throw new InvalidOperationException("The battle has ended.");
            BattlePokemon pokemon = Active(side);
            int best = -1; double bestScore = double.NegativeInfinity;
            for (int slot = 0; slot < pokemon.Moves.Count; slot++)
            {
                MoveSlot candidate = pokemon.Moves[slot];
                if (candidate.Pp == 0) continue;
                MoveDefinition move = candidate.Definition;
                string effect = Effects.Normalize(move.effect);
                double score = 0;
                if (move.CategoryKind != MoveCategory.Status)
                    score = Damage(pokemon, Active(1 - side), move, 100) * move.accuracy / 100.0;
                else if (effect == "recover" && pokemon.Hp * 2 < pokemon.MaxHp)
                    score = pokemon.MaxHp / 2.0;
                else if (effect == "swordsdance" && pokemon.AttackStage < 2 && HasPhysicalMove(pokemon))
                    score = 15;
                if (score > bestScore) { best = slot; bestScore = score; }
            }
            return best < 0 ? BattleChoice.Struggle() : BattleChoice.Move(best);
        }

        private static bool HasPhysicalMove(BattlePokemon pokemon)
        {
            foreach (MoveSlot slot in pokemon.Moves) if (slot.Pp > 0 && slot.Definition.CategoryKind == MoveCategory.Physical) return true;
            return false;
        }
        private void ValidateChoice(int side, BattleChoice choice)
        {
            if (choice == null) throw new ArgumentNullException(nameof(choice));
            BattlePokemon actor = Active(side);
            if (actor.IsFainted) throw new InvalidOperationException("No conscious active Pokemon.");
            switch (choice.Kind)
            {
                case BattleChoiceKind.Move:
                    if (choice.Index < 0 || choice.Index >= actor.Moves.Count || actor.Moves[choice.Index].Pp == 0)
                        throw new ArgumentException("Invalid or exhausted move slot for side " + side);
                    break;
                case BattleChoiceKind.Switch:
                    if (choice.Index < 0 || choice.Index >= teams[side].Length || choice.Index == activeIndices[side] || teams[side][choice.Index].IsFainted)
                        throw new ArgumentException("Invalid switch for side " + side);
                    break;
                case BattleChoiceKind.Struggle:
                    foreach (MoveSlot slot in actor.Moves) if (slot.Pp > 0) throw new ArgumentException("Struggle is only available when all PP are exhausted.");
                    break;
                default: throw new ArgumentException("Unknown action.");
            }
        }
        private static int Priority(BattlePokemon actor, BattleChoice choice)
        {
            return choice.Kind == BattleChoiceKind.Switch ? 6 : choice.Kind == BattleChoiceKind.Move ? actor.Moves[choice.Index].Definition.priority : 0;
        }
        private void Execute(int side, BattlePokemon actor, BattleChoice choice, List<BattleEvent> events)
        {
            if (actor.IsFainted)
            {
                events.Add(new BattleEvent(BattleEventKind.Skipped, side, actor.Name + " est K.O. et ne peut pas agir."));
                return;
            }
            if (choice.Kind == BattleChoiceKind.Switch)
            {
                SwitchTo(side, choice.Index, events);
                return;
            }
            if (choice.Kind == BattleChoiceKind.Struggle)
            {
                actor.UsedProtectLastTurn = false;
                ExecuteStruggle(side, actor, events);
                return;
            }
            MoveSlot slot = actor.Moves[choice.Index];
            MoveDefinition move = slot.Definition;
            string effect = Effects.Normalize(move.effect);
            bool priorProtect = actor.UsedProtectLastTurn;
            actor.UsedProtectLastTurn = effect == "protect";
            slot.Pp--;
            events.Add(new BattleEvent(BattleEventKind.MoveUsed, side, actor.Name + " utilise " + move.name + " !", moveId: move.id));
            if (move.CategoryKind == MoveCategory.Status)
            {
                if (effect == "swordsdance") RaiseAttack(actor, side, events);
                else if (effect == "recover") Heal(actor, side, Math.Max(1, actor.MaxHp / 2), events);
                else if (effect == "protect")
                {
                    // Deliberately simplified: a consecutive Protect fails instead of using Gen 9's probability.
                    if (priorProtect) events.Add(new BattleEvent(BattleEventKind.Failed, side, "La protection consécutive échoue."));
                    else { actor.Protected = true; events.Add(new BattleEvent(BattleEventKind.Protected, side, actor.Name + " se protège !")); }
                }
                return;
            }
            BattlePokemon target = Active(1 - side);
            if (target.IsFainted)
            {
                events.Add(new BattleEvent(BattleEventKind.Failed, side, "Il n'y a plus de cible."));
                return;
            }
            if (target.Protected)
            {
                events.Add(new BattleEvent(BattleEventKind.Protected, 1 - side, target.Name + " bloque l'attaque !"));
                return;
            }
            if (move.accuracy < 100 && random.Next(100) >= move.accuracy)
            {
                events.Add(new BattleEvent(BattleEventKind.Miss, side, "L'attaque manque sa cible."));
                return;
            }
            if (TypeChart.Effectiveness(move.type, target.Species.types) == 0)
            {
                events.Add(new BattleEvent(BattleEventKind.Immune, 1 - side, "Cela n'affecte pas " + target.Name + "."));
                return;
            }
            int damage = Damage(actor, target, move, 85 + random.Next(16));
            int actualDamage = Math.Min(target.Hp, damage);
            ApplyDamage(target, 1 - side, damage, side, move.id, events);
            if (effect == "recoilthird")
                ApplyDamage(actor, side, Math.Max(1, actualDamage / 3), side, 0, events);
            if (effect == "flamecharge")
            {
                int increase = Math.Min(6, actor.SpeedStage + 1) - actor.SpeedStage;
                actor.SpeedStage += increase;
                if (increase > 0) events.Add(new BattleEvent(BattleEventKind.StageChanged, side, "La Vitesse de " + actor.Name + " augmente !", increase));
            }
            if (!actor.IsFainted && ItemEffect(actor) == "lifeorb")
            {
                events.Add(new BattleEvent(BattleEventKind.ItemActivated, side, actor.Name + " subit le recul de l'Orbe Vie."));
                ApplyDamage(actor, side, Math.Max(1, actor.MaxHp / 10), side, 0, events);
            }
        }
        private static int Damage(BattlePokemon actor, BattlePokemon target, MoveDefinition move, int roll)
        {
            int attack = move.CategoryKind == MoveCategory.Physical ? actor.EffectiveAttack : actor.SpecialAttack;
            int defense = move.CategoryKind == MoveCategory.Physical ? target.Defense : target.SpecialDefense;
            long baseDamage = ((2L * actor.Level / 5 + 2) * move.power * attack / defense) / 50 + 2;
            double modifier = TypeChart.Effectiveness(move.type, target.Species.types);
            if (modifier == 0) return 0;
            foreach (string type in actor.Species.types)
                if (string.Equals(type, move.type, StringComparison.OrdinalIgnoreCase)) { modifier *= 1.5; break; }
            string effect = ItemEffect(actor);
            if (effect == "lifeorb") modifier *= 1.3;
            else if (effect == "charcoal" || effect == "typeboost")
            {
                string type = string.IsNullOrEmpty(actor.HeldItem.type) ? "Fire" : actor.HeldItem.type;
                if (string.Equals(type, move.type, StringComparison.OrdinalIgnoreCase)) modifier *= actor.HeldItem.multiplier > 0 ? actor.HeldItem.multiplier : 1.2;
            }
            return Math.Max(1, (int)Math.Floor(baseDamage * modifier * roll / 100.0));
        }
        private void ExecuteStruggle(int side, BattlePokemon actor, List<BattleEvent> events)
        {
            events.Add(new BattleEvent(BattleEventKind.MoveUsed, side, actor.Name + " utilise Lutte !"));
            BattlePokemon target = Active(1 - side);
            if (!target.IsFainted && !target.Protected)
            {
                // Struggle ignores types and STAB. Its recoil applies only after a hit.
                long basis = ((2L * actor.Level / 5 + 2) * 50 * actor.EffectiveAttack / target.Defense) / 50 + 2;
                int damage = Math.Max(1, (int)(basis * (85 + random.Next(16)) / 100));
                ApplyDamage(target, 1 - side, damage, side, 0, events);
                ApplyDamage(actor, side, Math.Max(1, actor.MaxHp / 4), side, 0, events);
            }
            else events.Add(new BattleEvent(BattleEventKind.Failed, side, "Lutte ne touche pas sa cible."));
        }
        private static void RaiseAttack(BattlePokemon actor, int side, List<BattleEvent> events)
        {
            int increase = Math.Min(6, actor.AttackStage + 2) - actor.AttackStage;
            actor.AttackStage += increase;
            events.Add(new BattleEvent(increase == 0 ? BattleEventKind.Failed : BattleEventKind.StageChanged, side,
                increase == 0 ? "L'Attaque ne peut plus augmenter." : "L'Attaque de " + actor.Name + " augmente fortement !", increase));
        }
        private static void ApplyDamage(BattlePokemon target, int targetSide, int damage, int sourceSide, int moveId, List<BattleEvent> events)
        {
            int actual = Math.Min(target.Hp, Math.Max(0, damage));
            target.Hp -= actual;
            events.Add(new BattleEvent(BattleEventKind.Damage, sourceSide, target.Name + " perd " + actual + " PV.", actual, targetSide, moveId));
            if (target.IsFainted) events.Add(new BattleEvent(BattleEventKind.Fainted, targetSide, target.Name + " est K.O. !"));
        }
        private static void Heal(BattlePokemon target, int side, int amount, List<BattleEvent> events)
        {
            int actual = Math.Min(target.MaxHp - target.Hp, amount);
            target.Hp += actual;
            events.Add(new BattleEvent(BattleEventKind.Heal, side, target.Name + " récupère " + actual + " PV.", actual, side));
        }
        private void EndTurn(int side, List<BattleEvent> events)
        {
            BattlePokemon pokemon = Active(side);
            if (!pokemon.IsFainted && pokemon.Hp < pokemon.MaxHp && ItemEffect(pokemon) == "leftovers")
            {
                events.Add(new BattleEvent(BattleEventKind.ItemActivated, side, "Les Restes de " + pokemon.Name + " restaurent ses PV."));
                Heal(pokemon, side, Math.Max(1, pokemon.MaxHp / 16), events);
            }
        }
        private void SwitchTo(int side, int index, List<BattleEvent> events)
        {
            Active(side).LeaveField();
            activeIndices[side] = index;
            events.Add(new BattleEvent(BattleEventKind.Switched, side, "En avant, " + Active(side).Name + " !", teamIndex: index));
        }
        private void AutoReplace(int side, List<BattleEvent> events)
        {
            if (!Active(side).IsFainted) return;
            for (int i = 0; i < teams[side].Length; i++)
                if (!teams[side][i].IsFainted) { SwitchTo(side, i, events); return; }
        }
        private bool HasAlive(int side)
        {
            foreach (BattlePokemon pokemon in teams[side]) if (!pokemon.IsFainted) return true;
            return false;
        }
        private static string ItemEffect(BattlePokemon pokemon) { return pokemon.HeldItem == null ? "" : Effects.Normalize(pokemon.HeldItem.effect); }
        private static void CheckSide(int side) { if (side != 0 && side != 1) throw new ArgumentOutOfRangeException(nameof(side)); }
        private static TeamMember[] Members(int[] ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var members = new TeamMember[ids.Length];
            for (int i = 0; i < ids.Length; i++) members[i] = new TeamMember(ids[i]);
            return members;
        }
        private static BattlePokemon[] CreateTeam(Catalog catalog, TeamMember[] members, int level)
        {
            if (members == null || members.Length < 1 || members.Length > 6) throw new ArgumentException("A team needs one to six Pokemon.");
            var team = new BattlePokemon[members.Length];
            for (int i = 0; i < team.Length; i++)
            {
                if (members[i] == null) throw new ArgumentException("Null team member.");
                team[i] = new BattlePokemon(catalog, members[i], level);
            }
            return team;
        }
    }

    // Xorshift32 gives reproducible replays across Unity and .NET runtime versions.
    internal sealed class DeterministicRandom
    {
        private uint state;
        internal DeterministicRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x6D2B79F5; }
        internal int Next(int maximum)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (int)(state % (uint)maximum);
        }
    }
}
