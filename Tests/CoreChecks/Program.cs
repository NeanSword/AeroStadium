using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AeroStadium.Core;
using AeroStadium.Presentation;

internal static class Program
{
    private static int Main(string[] args)
    {
        var checks = new List<(string, Action)>
        {
            ("Physical/special categories select independent attack AND defense stats", Categories),
            ("IDs above 255 survive JSON and resolve by ID rather than array position", LargeIds),
            ("18-type chart: immunity, dual weakness and modern Steel interactions", Types),
            ("Hit feedback records six effectiveness tiers without changing damage", EffectivenessFeedback),
            ("Hit feedback keeps fixed damage, recoil, residuals and Struggle neutral", EffectivenessExceptions),
            ("Hit feedback survives transformation and matches the exact event", EffectivenessSnapshot),
            ("KO skips the queued action and reserves replace only after the turn", Knockout),
            ("PP are consumed once and exhausted teams can use Struggle", PpAndStruggle),
            ("Invalid second-side command is atomic, including the random stream", AtomicInvalid),
            ("Seeded replay produces identical events and final states", Replay),
            ("Move priority outranks speed", Priority),
            ("Charcoal boosts only its matching type", Charcoal),
            ("Life Orb boosts damage and applies max-HP recoil", LifeOrb),
            ("Leftovers heals only after damage and never exceeds maximum HP", Leftovers),
            ("Flame Charge/Swords Dance apply stages; switching resets them", StagesAndSwitch),
            ("Recover clamps healing and Protect blocks damage", RecoveryAndProtect),
            ("RecoilThird uses damage actually inflicted, including a nearly KO target", MoveRecoil),
            ("Unknown data and unsupported effects fail validation", InvalidCatalog),
            ("IV/EV profiles preserve defaults and exact known stats", TrainingStats),
            ("IV/EV limits and total cap reject invalid teams", TrainingLimits),
            ("IV/EV profiles are isolated from caller mutations", TrainingIsolation),
            ("Training changes damage categories and battle initiative", TrainingCombat)
            ,("Custom moves are legal, distinct, bounded and isolated", CustomLoadouts)
            ,("Metadata supports always-hit, stat changes and ailment immunities", MetadataStatuses)
            ,("Metadata supports fixed damage, multi-hit, drain and recoil", MetadataDamage)
            ,("Choice items restrict moves and clear their lock on switching", ChoiceItems)
            ,("Held items: Focus Sash, berries, assault vest and contact helmet", ModernHeldItems)
            ,("Transform copies battle attributes without copying HP or item and resets", TransformCheck)
            ,("Specialized catalog moves fail explicitly without inventing effects", SpecializedFailure)
        };
        if (args.Length > 0) checks.Add(("Actual Unity seed JSON validates and replays", () => ActualCatalog(args[0])));
        int failed = 0;
        foreach (var check in checks)
        {
            try { check.Item2(); Console.WriteLine("PASS " + check.Item1); }
            catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + check.Item1 + ": " + ex.Message); }
        }
        Console.WriteLine((checks.Count - failed) + "/" + checks.Count + " checks passed.");
        return failed == 0 ? 0 : 1;
    }

    private static Catalog Fixture()
    {
        return new Catalog
        {
            moves = new[]
            {
                Move(1, "Physical fire", "Fire", "Physical", 50),
                Move(2, "Special fire", "Fire", "Special", 50),
                Move(3, "Recover", "Normal", "Status", 0, "Recover"),
                Move(4, "Flame Charge", "Fire", "Physical", 50, "FlameCharge"),
                Move(5, "Swords Dance", "Normal", "Status", 0, "SwordsDance"),
                Move(6, "Protect", "Normal", "Status", 0, "Protect", 4),
                Move(7, "Physical normal", "Normal", "Physical", 50),
                Move(8, "Brave Bird", "Flying", "Physical", 120, "RecoilThird")
            },
            species = new[]
            {
                Species(1025, "Attacker", new[] { "Fire" }, new[] { 1, 2, 4, 5 }, Stats(500, 200, 80, 40, 80, 100)),
                Species(4096, "Defender", new[] { "Normal" }, new[] { 1, 2, 3, 6 }, Stats(500, 40, 40, 40, 200, 10)),
                Species(700, "Reserve", new[] { "Fairy" }, new[] { 1, 3 }, Stats(500, 100, 80, 100, 80, 30))
            },
            items = new[]
            {
                new ItemDefinition { id = "none", name = "No item", effect = "" },
                new ItemDefinition { id = "charcoal", name = "Charcoal", effect = "Charcoal", type = "Fire", multiplier = 1.2f },
                new ItemDefinition { id = "lifeorb", name = "Life Orb", effect = "LifeOrb" },
                new ItemDefinition { id = "leftovers", name = "Leftovers", effect = "Leftovers" }
            }
        };
    }
    private static BaseStats Stats(int hp, int attack, int defense, int specialAttack, int specialDefense, int speed)
    {
        return new BaseStats { hp = hp, attack = attack, defense = defense, specialAttack = specialAttack, specialDefense = specialDefense, speed = speed };
    }
    private static SpeciesDefinition Species(int id, string name, string[] types, int[] moves, BaseStats stats)
    {
        return new SpeciesDefinition { id = id, name = name, types = types, moves = moves, stats = stats, color = "#ffffff" };
    }
    private static MoveDefinition Move(int id, string name, string type, string category, int power, string effect = "", int priority = 0)
    {
        return new MoveDefinition { id = id, name = name, type = type, category = category, power = power, effect = effect, accuracy = 100, pp = 10, priority = priority };
    }
    private static BattleEngine Engine(Catalog catalog = null, string item = null, int seed = 123)
    {
        return new BattleEngine(catalog ?? Fixture(), new[] { new TeamMember(1025, item) }, new[] { new TeamMember(4096) }, seed);
    }
    private static int Hit(Catalog catalog, int slot, string item = null)
    {
        return Engine(catalog, item).ResolveTurn(BattleChoice.Move(slot), BattleChoice.Move(2))
            .First(e => e.Kind == BattleEventKind.Damage && e.Side == 0 && e.TargetSide == 1).Amount;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void Throws(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        catch (InvalidOperationException) { return; }
        throw new Exception("Expected an invalid action/catalog to throw.");
    }
    private static string Trace(IEnumerable<BattleEvent> events)
    {
        return string.Join("|", events.Select(e => e.Kind + ":" + e.Side + ":" + e.TargetSide + ":" + e.Amount + ":" + e.MoveId + ":" + e.TeamIndex + ":" + e.HitEffectiveness + ":" + e.Message));
    }
    private static string Snapshot(BattleEngine engine)
    {
        return engine.Turn + ":" + engine.IsFinished + ":" + engine.Winner + ":" + string.Join("|", new[] { 0, 1 }.Select(side => engine.ActiveIndex(side) + ":" + string.Join(";", engine.Team(side).Select(p => p.SpeciesId + "," + p.Hp + "," + p.AttackStage + "," + p.SpeedStage + "," + string.Join(",", p.Moves.Select(m => m.Pp))))));
    }
    private static void Categories()
    {
        int physical = Hit(Fixture(), 0), special = Hit(Fixture(), 1);
        Require(physical > special * 4, "The two Fire moves should differ by their stat pair.");
        Catalog attackChange = Fixture(); attackChange.species[0].stats.attack = 40;
        Require(Hit(attackChange, 0) < physical && Hit(attackChange, 1) == special, "Changing Attack must not change Special damage.");
        Catalog specialChange = Fixture(); specialChange.species[0].stats.specialAttack = 200;
        Require(Hit(specialChange, 0) == physical && Hit(specialChange, 1) > special, "Changing Special Attack must not change Physical damage.");
        Catalog defenseChange = Fixture(); defenseChange.species[1].stats.defense = 200;
        Require(Hit(defenseChange, 0) < physical && Hit(defenseChange, 1) == special, "Changing Defense must not change Special damage.");
        Catalog spDefenseChange = Fixture(); spDefenseChange.species[1].stats.specialDefense = 40;
        Require(Hit(spDefenseChange, 0) == physical && Hit(spDefenseChange, 1) > special, "Changing Special Defense must not change Physical damage.");
    }
    private static void LargeIds()
    {
        var options = new JsonSerializerOptions { IncludeFields = true };
        Catalog original = Fixture();
        original.species = new[] { original.species[1], original.species[2], original.species[0] };
        Catalog loaded = JsonSerializer.Deserialize<Catalog>(JsonSerializer.Serialize(original, options), options);
        var engine = Engine(loaded, "none");
        Require(engine.Active(0).SpeciesId == 1025 && engine.Active(1).SpeciesId == 4096, "IDs were truncated or used as positions.");
        Require(engine.Active(0).Moves[0].Definition.id == 1, "Move lookup changed after JSON.");
    }
    private static void Types()
    {
        Require(TypeChart.Types.Count == 18, "Expected the 18 ordinary types.");
        Require(TypeChart.Effectiveness("Normal", "Ghost") == 0, "Normal/Ghost immunity.");
        Require(TypeChart.Effectiveness("Electric", "Ground", "Flying") == 0, "An immunity must dominate dual typing.");
        Require(TypeChart.Effectiveness("Fire", "Grass", "Steel") == 4, "Dual weakness.");
        Require(TypeChart.Effectiveness("Ghost", "Steel") == 1 && TypeChart.Effectiveness("Dark", "Steel") == 1, "Modern Steel interactions.");
        Require(TypeChart.Effectiveness("Poison", "Steel", "Fairy") == 0, "Steel immunity must dominate Fairy weakness.");
        Require(TypeChart.Effectiveness("Dragon", "Fairy") == 0, "Fairy immunity.");
        Catalog catalog = Fixture(); catalog.moves[0].type = "Normal"; catalog.species[1].types = new[] { "Ghost" };
        var engine = Engine(catalog); int before = engine.Active(1).Hp;
        var events = engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(2));
        Require(engine.Active(1).Hp == before && events.Any(e => e.Kind == BattleEventKind.Immune), "Immune defender lost HP.");
        Require(engine.Active(0).Moves[0].Pp == 9, "An immune target still consumes move PP.");
    }
    private static void EffectivenessFeedback()
    {
        var cases = new[] {
            ("Normal", new[] { "Ghost" }, 0d),
            ("Grass", new[] { "Grass", "Poison" }, .25d),
            ("Water", new[] { "Water" }, .5d),
            ("Normal", new[] { "Normal" }, 1d),
            ("Fire", new[] { "Bug" }, 2d),
            ("Rock", new[] { "Fire", "Flying" }, 4d)
        };
        foreach (var test in cases)
        {
            Catalog c = Fixture(); c.moves[0].type = test.Item1; c.species[1].types = test.Item2;
            var e = Engine(c, "lifeorb");
            var events = e.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(2));
            int used = events.ToList().FindIndex(x => x.Kind == BattleEventKind.MoveUsed && x.Side == 0);
            bool found = BattlePresentationHit.TryGetDirectHitEffectiveness(events, used, out int damage, out double effectiveness);
            if (test.Item3 == 0d)
            {
                Require(!found && effectiveness == 0d && damage == -1, "An immune target supplied damage feedback.");
                Require(events.Single(x => x.Kind == BattleEventKind.Immune).HitEffectiveness == 0d, "Immunity was not recorded as zero.");
            }
            else
            {
                Require(found && effectiveness == test.Item3, test.Item1 + " feedback has the wrong effectiveness.");
                Require(events[damage].HitEffectiveness == test.Item3, "STAB or the Life Orb altered type feedback.");
            }
        }
    }
    private static void EffectivenessExceptions()
    {
        var level = Generic(51, "Special", 0, new MoveMechanics { support = "implemented", fixedDamageKind = "level" }, "Ghost");
        var half = Generic(52, "Physical", 0, new MoveMechanics { support = "implemented", fixedDamageKind = "half-hp" }, "Ghost");
        var fixedValue = Generic(54, "Special", 0, new MoveMechanics { support = "implemented", fixedDamage = 20 }, "Ghost");
        Catalog c = MechanicsFixture(level, half, fixedValue); c.species[1].types = new[] { "Psychic" };
        foreach (int id in new[] { 51, 52, 54 })
        {
            var e = WithMoves(c, new[] { id }, new[] { 3 });
            var events = e.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
            Require(events.First(x => x.Kind == BattleEventKind.Damage).HitEffectiveness == 1d, "Fixed damage inherited the Ghost weakness.");
        }
        c.species[1].types = new[] { "Normal" };
        var immune = WithMoves(c, new[] { 51 }, new[] { 3 }).ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        Require(immune.Any(x => x.Kind == BattleEventKind.Immune && x.HitEffectiveness == 0d), "Fixed damage lost its existing immunity rule.");

        var burn = Generic(55, "Physical", 5, new MoveMechanics { support = "implemented", ailment = "burn", ailmentChance = 100, drain = -50, contact = true, minHits = 3, maxHits = 3 }, "Fire");
        c = MechanicsFixture(burn); c.species[1].types = new[] { "Bug", "Grass" };
        c.items = c.items.Concat(new[] { Held("helmet", "rockyhelmet") }).ToArray();
        var effects = WithMoves(c, new[] { 55 }, new[] { 3 }, "lifeorb", "helmet").ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        Require(effects.Count(x => x.Kind == BattleEventKind.Damage && x.MoveId == 55 && x.HitEffectiveness == 4d) == 3, "Multi-hit damage lost its effectiveness.");
        var independent = effects.Where(x => x.Kind == BattleEventKind.Damage && x.MoveId == 0).ToList();
        Require(independent.Count >= 6 && independent.All(x => x.HitEffectiveness == 1d), "Helmet, recoil, Life Orb or burn inherited the attack weakness.");

        c = Fixture(); c.species[0].moves = new[] { 7 }; c.species[1].moves = new[] { 3 };
        c.species[1].types = new[] { "Ghost" }; c.moves[6].pp = c.moves[2].pp = 1;
        var struggle = Engine(c); struggle.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        var struggleEvents = struggle.ResolveTurn(BattleChoice.Struggle(), BattleChoice.Struggle());
        Require(struggleEvents.Where(x => x.Kind == BattleEventKind.Damage).All(x => x.HitEffectiveness == 1d), "Struggle inherited a Normal/Ghost immunity.");
        int struggleUsed = struggleEvents.ToList().FindIndex(x => x.Kind == BattleEventKind.MoveUsed && x.Side == 0);
        Require(BattlePresentationHit.TryGetDirectHitEffectiveness(struggleEvents, struggleUsed, out _, out double struggleFeedback) && struggleFeedback == 1d, "Struggle's direct contact was confused with recoil.");
    }
    private static void EffectivenessSnapshot()
    {
        var transform = Generic(144, "Status", 0, new MoveMechanics { support = "partial" }); transform.slug = "transform";
        Catalog c = MechanicsFixture(transform); c.species[1].types = new[] { "Grass", "Steel" };
        var e = WithMoves(c, new[] { 1 }, new[] { 144 });
        var events = e.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        Require(e.Active(1).IsTransformed && e.Active(1).Types.SequenceEqual(new[] { "Fire" }), "The target did not transform after the hit.");
        int used = events.ToList().FindIndex(x => x.Kind == BattleEventKind.MoveUsed && x.Side == 0);
        Require(BattlePresentationHit.TryGetDirectHitEffectiveness(events, used, out int index, out double effectiveness) && effectiveness == 4d, "Feedback re-read transformed types instead of the resolved contact.");
        Require(BattlePresentationHit.GetDamageEffectiveness(events[index]) == 4d, "Damage playback lost its captured multiplier.");
        var mixed = new[] {
            new BattleEvent(BattleEventKind.MoveUsed, 0, "move", moveId: 53),
            new BattleEvent(BattleEventKind.Damage, 0, "recoil", 10, 0, 0, hitEffectiveness: 1d),
            new BattleEvent(BattleEventKind.Damage, 0, "wrong move", 10, 1, 33, hitEffectiveness: 4d),
            new BattleEvent(BattleEventKind.Damage, 0, "zero", 0, 1, 53, hitEffectiveness: 2d),
            new BattleEvent(BattleEventKind.Damage, 0, "contact", 10, 1, 53, hitEffectiveness: .25d)
        };
        Require(BattlePresentationHit.TryGetDirectHitEffectiveness(mixed, 0, out index, out effectiveness) && index == 4 && effectiveness == .25d, "Feedback selected the wrong damage event.");
        Require(BattlePresentationHit.GetDamageEffectiveness(mixed[3]) == 0d, "Zero damage supplied a hit response.");
        var later = new[] { mixed[0], new BattleEvent(BattleEventKind.Switched, 1, "switch"), mixed[4] };
        Require(!BattlePresentationHit.TryGetDirectHitEffectiveness(later, 0, out index, out effectiveness) && effectiveness == 0d, "A replacement supplied a previous move's response.");
    }
    private static void Knockout()
    {
        Catalog catalog = Fixture(); catalog.moves[0].power = 200;
        catalog.species[1].stats = Stats(1, 10, 1, 10, 1, 1);
        var engine = new BattleEngine(catalog, new[] { 1025, 700, 1025 }, new[] { 4096, 4096, 4096 }, 123);
        var first = engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        Require(first.Any(e => e.Kind == BattleEventKind.Skipped && e.Side == 1), "The fainted opponent executed its queued action.");
        Require(engine.Team(1)[0].Moves[0].Pp == 10 && engine.Active(1).Moves[0].Pp == 10, "Skipped/just-entered Pokemon consumed PP.");
        Require(engine.ActiveIndex(1) == 1 && !engine.IsFinished, "Reserve was not selected.");
        engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        Require(engine.IsFinished && engine.Winner == 0 && engine.Team(1).All(p => p.Hp == 0), "The three-member team did not finish correctly.");
    }
    private static void PpAndStruggle()
    {
        Catalog catalog = Fixture(); catalog.species[0].moves = new[] { 1 }; catalog.species[1].moves = new[] { 3 };
        catalog.moves[0].pp = 1; catalog.moves[2].pp = 1;
        var engine = Engine(catalog); engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        Require(engine.Active(0).Moves[0].Pp == 0 && engine.Active(1).Moves[0].Pp == 0, "PP were not consumed once.");
        Throws(() => engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Struggle()));
        var events = engine.ResolveTurn(BattleChoice.Struggle(), BattleChoice.Struggle());
        Require(events.Count(e => e.Kind == BattleEventKind.MoveUsed) == 2 && events.Any(e => e.Kind == BattleEventKind.Damage), "Struggle did not resolve.");
        Require(engine.Active(0).Moves[0].Pp == 0, "Struggle changed ordinary move PP.");
    }
    private static void AtomicInvalid()
    {
        var engine = Engine(); var control = Engine(); string before = Snapshot(engine);
        Throws(() => engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(99)));
        Require(Snapshot(engine) == before, "Invalid opponent choice changed state.");
        string actual = Trace(engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0)));
        string expected = Trace(control.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0)));
        Require(actual == expected && Snapshot(engine) == Snapshot(control), "Invalid action consumed randomness.");
        before = Snapshot(engine);
        Throws(() => engine.ResolveTurn(BattleChoice.Switch(0), BattleChoice.Move(0)));
        Require(Snapshot(engine) == before, "Invalid switch changed state.");
    }
    private static void Replay()
    {
        var left = Engine(seed: 772); var right = Engine(seed: 772);
        for (int turn = 0; turn < 8 && !left.IsFinished; turn++)
        {
            Require(Trace(left.ResolveTurn(left.ChooseAi(0), left.ChooseAi(1))) == Trace(right.ResolveTurn(right.ChooseAi(0), right.ChooseAi(1))), "Replay events diverged.");
            Require(Snapshot(left) == Snapshot(right), "Replay state diverged.");
        }
        Require(left.Turn > 0, "Replay did not run.");
    }
    private static void Priority()
    {
        Catalog catalog = Fixture(); catalog.moves[1].priority = 1;
        var engine = Engine(catalog);
        var events = engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(1));
        Require(events.First(e => e.Kind == BattleEventKind.MoveUsed).Side == 1, "Speed incorrectly overrode priority.");
    }
    private static void Charcoal()
    {
        Require(Hit(Fixture(), 0, "charcoal") > Hit(Fixture(), 0), "Charcoal did not boost Fire.");
        Catalog ordinary = Fixture(); ordinary.moves[0].type = "Normal";
        Require(Hit(ordinary, 0, "charcoal") == Hit(ordinary, 0), "Charcoal boosted a non-Fire move.");
    }
    private static void LifeOrb()
    {
        var engine = Engine(item: "lifeorb"); int hp = engine.Active(0).Hp;
        var events = engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(2));
        Require(engine.Active(0).Hp == hp - Math.Max(1, hp / 10), "Life Orb recoil was not based on max HP.");
        Require(events.Any(e => e.Kind == BattleEventKind.ItemActivated), "Missing held-item event.");
        Require(Hit(Fixture(), 0, "lifeorb") > Hit(Fixture(), 0), "Life Orb did not boost damage.");
    }
    private static void Leftovers()
    {
        Catalog catalog = Fixture(); catalog.species[0].moves = new[] { 3 };
        var plain = Engine(catalog); var held = Engine(catalog, "leftovers");
        plain.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        var events = held.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(0));
        int expected = Math.Min(held.Active(0).MaxHp, plain.Active(0).Hp + Math.Max(1, held.Active(0).MaxHp / 16));
        Require(held.Active(0).Hp == expected && events.Any(e => e.Kind == BattleEventKind.ItemActivated), "Leftovers recovery is wrong.");
        var full = Engine(catalog, "leftovers");
        full.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(2));
        Require(full.Active(0).Hp == full.Active(0).MaxHp, "Leftovers exceeded maximum HP.");
    }
    private static void StagesAndSwitch()
    {
        var engine = new BattleEngine(Fixture(), new[] { 1025, 700 }, new[] { 4096 }, 123);
        engine.ResolveTurn(BattleChoice.Move(2), BattleChoice.Move(2));
        Require(engine.Active(0).SpeedStage == 1, "Flame Charge did not raise speed.");
        engine.ResolveTurn(BattleChoice.Move(3), BattleChoice.Move(2));
        Require(engine.Active(0).AttackStage == 2, "Swords Dance did not raise Attack by two stages.");
        engine.ResolveTurn(BattleChoice.Switch(1), BattleChoice.Move(2));
        Require(engine.ActiveIndex(0) == 1 && engine.Team(0)[0].AttackStage == 0 && engine.Team(0)[0].SpeedStage == 0, "Switching did not reset stages.");
    }
    private static void RecoveryAndProtect()
    {
        var engine = Engine(); int before = engine.Active(1).Hp;
        var protectedEvents = engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(3));
        Require(engine.Active(1).Hp == before && protectedEvents.Any(e => e.Kind == BattleEventKind.Protected), "Protect failed to block damage.");
        engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(3));
        Require(engine.Active(1).Hp < before, "The documented consecutive-Protect simplification was not applied.");
        engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(2));
        Require(engine.Active(1).Hp <= engine.Active(1).MaxHp && engine.Active(1).Hp > 0, "Recover exceeded maximum HP.");
        Require(engine.Active(0).Moves[0].Pp == 7, "Protected attacks did not consume PP.");
    }
    private static void MoveRecoil()
    {
        Catalog catalog = Fixture(); catalog.species[0].moves = new[] { 8 }; catalog.species[1].stats.hp = 1;
        var engine = Engine(catalog); int hp = engine.Active(0).Hp;
        var events = engine.ResolveTurn(BattleChoice.Move(0), BattleChoice.Move(2));
        int actual = events.First(e => e.Kind == BattleEventKind.Damage && e.TargetSide == 1).Amount;
        Require(engine.Active(0).Hp == hp - Math.Max(1, actual / 3), "Move recoil used unclamped damage.");
    }
    private static void InvalidCatalog()
    {
        Catalog bad = Fixture(); bad.species[0].moves[0] = 9999; Throws(bad.Validate);
        bad = Fixture(); bad.moves[0].effect = "UnimplementedModernEffect"; Throws(bad.Validate);
        bad = Fixture(); bad.moves[0].category = "Fire"; Throws(bad.Validate);
        bad = Fixture(); bad.species[0].types = new[] { "Fire", "Fire" }; Throws(bad.Validate);
    }
    private static BattleEngine Trained(Catalog catalog, StatValues ivs=null, StatValues evs=null)
    {
        return new BattleEngine(catalog,new[] {new TeamMember(1025) {ivs=ivs,evs=evs}},new[] {new TeamMember(4096)},123);
    }
    private static string SixStats(BattlePokemon p)
    {
        return string.Join(",",p.MaxHp,p.Attack,p.Defense,p.SpecialAttack,p.SpecialDefense,p.Speed);
    }
    private static void TrainingStats()
    {
        Catalog c=Fixture();c.species[0].stats=Stats(78,84,78,109,85,100);
        Require(SixStats(Trained(c).Active(0))=="153,104,98,129,105,120","Legacy rental stats changed.");
        Require(SixStats(Trained(c,new StatValues(0)).Active(0))=="138,89,83,114,90,105","Explicit zero IVs were replaced by defaults.");
        Require(SixStats(Trained(c,null,new StatValues {hp=4,specialAttack=252,speed=252}).Active(0))=="154,104,98,161,105,152","Known level50 trained Charizard stats differ.");
        var a=new BattleEngine(c,new[]{new TeamMember(1025) {evs=new StatValues {attack=3}}},new[]{new TeamMember(4096)},1,100);
        var b=new BattleEngine(c,new[]{new TeamMember(1025) {evs=new StatValues {attack=4}}},new[]{new TeamMember(4096)},1,100);
        Require(a.Active(0).Attack==204&&b.Active(0).Attack==205,"EV remainder was used as a fractional point.");
    }
    private static void TrainingLimits()
    {
        foreach(int bad in new[]{-1,32,int.MaxValue})Throws(()=>Trained(Fixture(),new StatValues {speed=bad}));
        foreach(int bad in new[]{-1,253,int.MaxValue})Throws(()=>Trained(Fixture(),null,new StatValues {hp=bad}));
        Trained(Fixture(),null,new StatValues {attack=252,speed=252,hp=6});
        Throws(()=>Trained(Fixture(),null,new StatValues {attack=252,speed=252,hp=7}));
        string[] fields={"hp","attack","defense","specialAttack","specialDefense","speed"};
        foreach(string field in fields) {
            var ivs=new StatValues(31);typeof(StatValues).GetField(field).SetValue(ivs,32);
            Throws(()=>Trained(Fixture(),ivs));
            var evs=new StatValues();typeof(StatValues).GetField(field).SetValue(evs,253);
            Throws(()=>Trained(Fixture(),null,evs));
        }
    }
    private static void TrainingIsolation()
    {
        var ivs=new StatValues(31);var evs=new StatValues {attack=252};
        var shared=new TeamMember(1025) {ivs=ivs,evs=evs};
        var e=new BattleEngine(Fixture(),new[]{shared},new[]{shared},1);
        ivs.attack=0;evs.attack=0;e.Active(0).IndividualValues.speed=0;e.Active(0).EffortValues.attack=0;
        Require(e.Active(0).IndividualValues.attack==31&&e.Active(1).IndividualValues.attack==31,"Caller IV mutation leaked into a fighter.");
        Require(e.Active(0).EffortValues.attack==252&&e.Active(1).EffortValues.attack==252,"EV profiles were shared.");
        var options=new JsonSerializerOptions {IncludeFields=true};
        var restored=JsonSerializer.Deserialize<TeamMember>(JsonSerializer.Serialize(new TeamMember(1025) {ivs=new StatValues(0),evs=new StatValues {speed=252}},options),options);
        Require(Trained(Fixture(),restored.ivs,restored.evs).Active(0).IndividualValues.hp==0,"Team JSON lost explicit zero IVs.");
    }
    private static int TrainingHit(StatValues evs,int slot)
    {
        return Trained(Fixture(),null,evs).ResolveTurn(BattleChoice.Move(slot),BattleChoice.Move(2)).First(e=>e.Kind==BattleEventKind.Damage&&e.TargetSide==1).Amount;
    }
    private static void TrainingCombat()
    {
        Require(TrainingHit(new StatValues {attack=252},0)>TrainingHit(null,0),"Attack EVs did not affect physical damage.");
        Require(TrainingHit(new StatValues {attack=252},1)==TrainingHit(null,1),"Attack EVs leaked into special damage.");
        Require(TrainingHit(new StatValues {specialAttack=252},1)>TrainingHit(null,1),"Special Attack EVs did not affect special damage.");
        Require(TrainingHit(new StatValues {specialAttack=252},0)==TrainingHit(null,0),"Special Attack EVs leaked into physical damage.");
        Catalog c=Fixture();c.species[0].stats.speed=c.species[1].stats.speed=100;
        var slow=Trained(c,new StatValues(0));var fast=Trained(c,new StatValues(0),new StatValues {speed=252});
        Require(slow.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0)).First(e=>e.Kind==BattleEventKind.MoveUsed).Side==1,"Zero Speed IVs should act second.");
        Require(fast.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0)).First(e=>e.Kind==BattleEventKind.MoveUsed).Side==0,"Speed EVs did not change initiative.");
    }
    private static void ActualCatalog(string path)
    {
        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
        Catalog catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path), options);
        catalog.Validate();
        Require(catalog.species.Length == 151 && catalog.species.All(s => s.id >= 1 && s.id <= 151), "The actual catalog should contain only the 151 Generation I species.");
        Require(catalog.GetSpecies(1).id == 1 && catalog.GetSpecies(151).id == 151, "The Kanto Pokédex endpoints should resolve by ID.");
        foreach (SpeciesDefinition species in catalog.species)
        {
            int[] selected=catalog.GetLearnableMoves(species.id).Take(4).ToArray();
            var profile=new TeamMember(species.id) {moveIds=selected,ivs=new StatValues(0),evs=new StatValues {hp=6,attack=252,speed=252}};
            catalog.ValidateTeamMember(profile);
            Require(catalog.CreatePokemon(profile).Moves.Select(m=>m.Definition.id).SequenceEqual(selected),"A Kanto loadout lost legal chosen moves: "+species.name);
            Require(catalog.CreatePokemon(new TeamMember(species.id)).Moves.All(m=>m.Definition.IsBattleUsable),"A Kanto default has only unsupported mechanics: "+species.name);
        }
        foreach (ItemDefinition item in catalog.items.Where(i=>i.holdable)) catalog.ValidateTeamMember(new TeamMember(1,item.id));
        int[] ids = catalog.species.Take(3).Select(s => s.id).ToArray();
        var left = new BattleEngine(catalog, ids, ids, 2026);
        var right = new BattleEngine(catalog, ids, ids, 2026);
        for (int turn = 0; turn < 30 && !left.IsFinished; turn++)
        {
            Require(Trace(left.ResolveTurn(left.ChooseAi(0), left.ChooseAi(1))) == Trace(right.ResolveTurn(right.ChooseAi(0), right.ChooseAi(1))), "Actual-catalog replay diverged.");
            Require(Snapshot(left) == Snapshot(right), "Actual-catalog state diverged.");
        }
    }

    private static void CustomLoadouts()
    {
        Catalog c=Fixture(); c.species[0].learnableMoves=new[]{1,2,3,4,5,6,7,8};
        var ids=new[]{3,7,8,2};var member=new TeamMember(1025) {moveIds=ids,ivs=new StatValues(0),evs=new StatValues {hp=6,attack=252,speed=252}};
        c.Validate();c.ValidateTeamMember(member);
        var copy=member.Copy();copy.moveIds[0]=1;copy.ivs.hp=31;copy.evs.speed=0;
        Require(member.moveIds[0]==3&&member.ivs.hp==0&&member.evs.speed==252,"Copy shared mutable arrays or stat profiles.");
        var e=new BattleEngine(c,new[]{member},new[]{new TeamMember(4096)},1);
        ids[0]=1;member.evs.speed=0;
        Require(e.Active(0).Moves.Select(m=>m.Definition.id).SequenceEqual(new[]{3,7,8,2})&&e.Active(0).EffortValues.speed==252,"Battle did not snapshot the loadout.");
        foreach(var bad in new[]{new int[0],new[]{1,1},new[]{1,2,3,4,5},new[]{999}})
            Throws(()=>c.ValidateTeamMember(new TeamMember(1025) {moveIds=bad}));
        Throws(()=>c.ValidateTeamMember(new TeamMember(4096) {moveIds=new[]{8}}));
        int[] legal=c.GetLearnableMoves(1025);legal[0]=999;
        Require(c.GetLearnableMoves(1025)[0]==1,"Learnset API exposed catalog mutation.");
        Require(c.CreatePokemon(new TeamMember(1025),50).Moves.Count==4,"Null custom moves lost default rental moves.");
        c.species[0].learnableMoves=new[]{1};Throws(c.Validate);
    }
    private static MoveDefinition Generic(int id,string category,int power,MoveMechanics m,string type="Normal")
    {
        var move=Move(id,"Generic "+id,type,category,power,"metadata");move.mechanics=m;move.supported=m.support!="unsupported";return move;
    }
    private static Catalog MechanicsFixture(params MoveDefinition[] moves)
    {
        Catalog c=Fixture();c.moves=c.moves.Concat(moves).ToArray();
        c.species[0].learnableMoves=c.moves.Select(m=>m.id).ToArray();c.species[1].learnableMoves=c.species[0].learnableMoves;
        return c;
    }
    private static BattleEngine WithMoves(Catalog c,int[] player,int[] rival,string playerItem=null,string rivalItem=null)
    {
        return new BattleEngine(c,new[]{new TeamMember(1025,playerItem) {moveIds=player}},new[]{new TeamMember(4096,rivalItem) {moveIds=rival}},123);
    }
    private static void MetadataStatuses()
    {
        var always=Generic(20,"Physical",50,new MoveMechanics {support="implemented"});always.accuracy=0;
        var poison=Generic(21,"Status",0,new MoveMechanics {support="implemented",ailment="poison",ailmentChance=100},"Poison");
        var growth=Generic(22,"Status",0,new MoveMechanics {support="implemented",selfTarget=true,statChanges=new[]{new StatChange {stat="special-attack",change=2},new StatChange {stat="defense",change=1}}});
        var c=MechanicsFixture(always,poison,growth);var e=WithMoves(c,new[]{20,21,22},new[]{3});
        Require(e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0)).Any(x=>x.Kind==BattleEventKind.Damage),"Accuracy0 missed instead of always hitting.");
        e.ResolveTurn(BattleChoice.Move(1),BattleChoice.Move(0));Require(e.Active(1).Status=="poison"&&e.Active(1).Hp<e.Active(1).MaxHp,"Poison did not persist and deal residual damage.");
        e.ResolveTurn(BattleChoice.Move(2),BattleChoice.Move(0));Require(e.Active(0).SpecialAttackStage==2&&e.Active(0).DefenseStage==1,"Multiple modern stat stages were not applied.");
        c=MechanicsFixture(poison);c.species[1].types=new[]{"Steel"};e=WithMoves(c,new[]{21},new[]{3});
        var immune=e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));Require(e.Active(1).Status=="none"&&immune.Any(x=>x.Kind==BattleEventKind.Failed),"Steel was poisoned.");
        var burn=Generic(23,"Physical",1,new MoveMechanics {support="implemented",ailment="burn",ailmentChance=100},"Fire");
        c=MechanicsFixture(burn);e=WithMoves(c,new[]{23},new[]{3});e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));Require(e.Active(1).Status=="burn","Guaranteed damage-secondary burn failed.");
    }
    private static void MetadataDamage()
    {
        var fixedMove=Generic(30,"Special",0,new MoveMechanics {support="implemented",fixedDamageKind="level"},"Ghost");
        var multi=Generic(31,"Physical",5,new MoveMechanics {support="implemented",minHits=3,maxHits=3});
        var drain=Generic(32,"Special",50,new MoveMechanics {support="implemented",drain=50});
        var recoil=Generic(33,"Physical",30,new MoveMechanics {support="implemented",drain=-50});
        var c=MechanicsFixture(fixedMove,multi,drain,recoil);c.species[1].types=new[]{"Psychic"};
        var e=WithMoves(c,new[]{30},new[]{3});Require(e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0)).First(x=>x.Kind==BattleEventKind.Damage).Amount==50,"Fixed level damage used the ordinary damage formula.");
        e=WithMoves(c,new[]{31},new[]{3});Require(e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0)).Count(x=>x.Kind==BattleEventKind.Damage&&x.TargetSide==1)==3,"Fixed multi-hit count changed.");
        e=WithMoves(c,new[]{32,3},new[]{7});e.ResolveTurn(BattleChoice.Move(1),BattleChoice.Move(0));
        var events=e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));int inflicted=events.First(x=>x.Kind==BattleEventKind.Damage&&x.TargetSide==1).Amount;
        Require(events.Any(x=>x.Kind==BattleEventKind.Heal&&x.Side==0&&x.Amount==Math.Max(1,inflicted/2)),"Drain did not heal from actual damage.");
        e=WithMoves(c,new[]{33},new[]{3});events=e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));inflicted=events.First(x=>x.Kind==BattleEventKind.Damage&&x.TargetSide==1).Amount;
        Require(events.Any(x=>x.Kind==BattleEventKind.Damage&&x.TargetSide==0&&x.Amount==Math.Max(1,inflicted/2)),"Metadata recoil was not based on actual damage.");
    }
    private static ItemDefinition Held(string id,string effect) { return new ItemDefinition {id=id,name=id,effect=effect,holdable=true,support="implemented"}; }
    private static void ChoiceItems()
    {
        Catalog c=Fixture();c.items=c.items.Concat(new[]{Held("band","choiceband")}).ToArray();
        var e=new BattleEngine(c,new[]{new TeamMember(1025,"band"),new TeamMember(700)},new[]{new TeamMember(4096)},123);
        e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(2));Require(e.CanUseMove(0,0)&&!e.CanUseMove(0,1),"Choice lock did not restrict slots.");
        Throws(()=>e.ResolveTurn(BattleChoice.Move(1),BattleChoice.Move(2)));
        e.ResolveTurn(BattleChoice.Switch(1),BattleChoice.Move(2));e.ResolveTurn(BattleChoice.Switch(0),BattleChoice.Move(2));Require(e.CanUseMove(0,1),"Choice lock persisted after leaving the field.");
        Require(Hit(c,0,"band")>Hit(c,0),"Choice Band did not boost physical damage.");
    }
    private static void ModernHeldItems()
    {
        Catalog c=Fixture();c.items=c.items.Concat(new[]{Held("sash","focussash"),Held("sitrus","sitrusberry"),Held("lum","lumberry"),Held("vest","assaultvest"),Held("helmet","rockyhelmet"),new ItemDefinition {id="key",name="Key",support="unsupported",holdable=false}}).ToArray();
        c.moves[0].power=1000;c.species[1].stats.hp=1;
        var e=WithMoves(c,new[]{1},new[]{2},null,"sash");e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));
        Require(e.Active(1).Hp==1&&e.Active(1).ItemConsumed&&!e.IsFinished,"Focus Sash failed to preserve1HP at full health.");
        Throws(()=>c.ValidateTeamMember(new TeamMember(1025,"key")));
        c=Fixture();c.items=c.items.Concat(new[]{Held("vest","assaultvest")}).ToArray();e=WithMoves(c,new[]{1,5},new[]{3},"vest");
        Require(e.CanUseMove(0,0)&&!e.CanUseMove(0,1),"Assault Vest did not prevent status moves.");
        var poison=Generic(40,"Status",0,new MoveMechanics {support="implemented",ailment="poison"},"Poison");
        c=MechanicsFixture(poison);c.items=c.items.Concat(new[]{Held("lum","lumberry")}).ToArray();e=WithMoves(c,new[]{40},new[]{3},null,"lum");
        e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));Require(e.Active(1).Status=="none"&&e.Active(1).ItemConsumed,"Lum Berry did not cure and consume.");
        var contact=Generic(41,"Physical",5,new MoveMechanics {support="implemented",contact=true});c=MechanicsFixture(contact);c.items=c.items.Concat(new[]{Held("helmet","rockyhelmet")}).ToArray();
        e=WithMoves(c,new[]{41},new[]{3},null,"helmet");int hp=e.Active(0).Hp;e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));Require(e.Active(0).Hp==hp-Math.Max(1,hp/6),"Contact did not trigger Rocky Helmet.");
    }
    private static void TransformCheck()
    {
        var transform=Generic(144,"Status",0,new MoveMechanics {support="partial"});transform.slug="transform";
        Catalog c=MechanicsFixture(transform);var e=new BattleEngine(c,new[]{new TeamMember(1025,"leftovers") {moveIds=new[]{144}},new TeamMember(700)},new[]{new TeamMember(4096)},123);
        int hp=e.Active(0).MaxHp;var original=e.Active(0);
        e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(2));
        Require(original.IsTransformed&&original.SpeciesId==4096&&original.Name=="Attacker"&&original.MaxHp==hp&&original.ItemId=="leftovers","Transform copied HP/item/name or failed to copy species.");
        Require(original.Attack==e.Active(1).Attack&&original.Moves.All(m=>m.Pp==5&&m.MaxPp==5),"Transform did not copy stats and 5PP slots.");
        e.ResolveTurn(BattleChoice.Switch(1),BattleChoice.Move(2));
        Require(!original.IsTransformed&&original.SpeciesId==1025&&original.Moves.Count==1&&original.Moves[0].Definition.id==144&&original.Moves[0].Pp==9,"Switch did not restore original species, stats and spent Transform PP.");
    }
    private static void SpecializedFailure()
    {
        var specialized=Generic(50,"Status",0,new MoveMechanics {support="unsupported"});
        Catalog c=MechanicsFixture(specialized);var e=WithMoves(c,new[]{50},new[]{3});
        var events=e.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));
        Require(events.Any(x=>x.Kind==BattleEventKind.Failed&&x.MoveId==50)&&e.Active(0).Moves[0].Pp==9&&e.Active(0).AttackStage==0,"Unsupported move pretended to succeed or mutated stats.");
    }
}
