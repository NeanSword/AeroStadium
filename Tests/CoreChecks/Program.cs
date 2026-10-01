using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AeroStadium.Core;

internal static class Program
{
    private static int Main(string[] args)
    {
        var checks = new List<(string, Action)>
        {
            ("Physical/special categories select independent attack AND defense stats", Categories),
            ("IDs above 255 survive JSON and resolve by ID rather than array position", LargeIds),
            ("18-type chart: immunity, dual weakness and modern Steel interactions", Types),
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
            ("Unknown data and unsupported effects fail validation", InvalidCatalog)
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
        return string.Join("|", events.Select(e => e.Kind + ":" + e.Side + ":" + e.TargetSide + ":" + e.Amount + ":" + e.MoveId + ":" + e.TeamIndex + ":" + e.Message));
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
    private static void ActualCatalog(string path)
    {
        var options = new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true };
        Catalog catalog = JsonSerializer.Deserialize<Catalog>(File.ReadAllText(path), options);
        catalog.Validate();
        Require(catalog.species.Any(s => s.id > 255), "The actual catalog should exercise a modern ID.");
        int[] ids = catalog.species.Take(3).Select(s => s.id).ToArray();
        var left = new BattleEngine(catalog, ids, ids, 2026);
        var right = new BattleEngine(catalog, ids, ids, 2026);
        for (int turn = 0; turn < 30 && !left.IsFinished; turn++)
        {
            Require(Trace(left.ResolveTurn(left.ChooseAi(0), left.ChooseAi(1))) == Trace(right.ResolveTurn(right.ChooseAi(0), right.ChooseAi(1))), "Actual-catalog replay diverged.");
            Require(Snapshot(left) == Snapshot(right), "Actual-catalog state diverged.");
        }
    }
}
