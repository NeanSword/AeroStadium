# Combat core

Pure C# 9 / .NET Standard 2.1, with no Unity references. The console checks compile the same source under .NET 10 without external packages.

`Catalog` contains JSON-friendly arrays named `species`, `moves`, and `items`. Call `Validate()` after loading. IDs are stable integer identifiers, not array positions; no 255-species limit applies. Item IDs are strings. Unknown categories, types, IDs, or effects fail validation rather than silently doing something else. Treat definitions as immutable after validation.

```csharp
var battle = new BattleEngine(catalog,
    new[] { new TeamMember(250, "lifeorb"), new TeamMember(152, "leftovers") },
    new[] { new TeamMember(700) }, seed: 2026);
var events = battle.ResolveTurn(BattleChoice.Move(0), battle.ChooseAi(1));
```

Sides are 0/player and 1/opponent. Move and team slots are zero-based. `Active(side)`, `Team(side)`, `ActiveIndex(side)` expose the current state. `Turn` starts at zero. `IsFinished` and nullable `Winner` indicate the result (0/player, 1/opponent, -1/draw). `BattleEvent.Kind` is `BattleEventKind`; `Message` is French display text. Events carry source `Side`, `TargetSide`, `Amount`, `MoveId`, and `TeamIndex` when applicable. A Unity adapter sends commands, then animates these events; animation timing never changes the combat result.

Both actions are validated before a turn changes state or consumes randomness. Switches have priority 6, consume the turn and reset temporary stages. Move priority precedes speed; tied speed uses a seeded, runtime-independent random stream. A queued action is skipped if its actor faints. Knocked-out active Pokemon are replaced automatically by the first conscious reserve **after** both actions and end-of-turn items; a replacement does not get a free action. Teams may contain one to six members, including the planned 3-versus-3 duels.

Implemented subset:

- Physical/Special/Status belongs to the move independently of its type. Physical uses Attack/Defense, Special uses Special Attack/Special Defense.
- All 18 ordinary type matchups and dual typing, STAB, accuracy, move PP, priority, a seeded 85–100% damage roll.
- Fixed level (default 50), neutral nature, 31 IVs and zero EVs; positive base stats. Damage uses a simplified integer base formula followed by floating-point modifiers. This is **not** bit-exact Generation 9 damage rounding.
- Flame Charge (+1 Speed on a damaging hit), Swords Dance (+2 Attack), Recover (half maximum HP), Protect. Stages cap at +6 and reset on switching. Consecutive Protect deliberately always fails; the official diminishing probability is not implemented.
- RecoilThird (one third of actual HP removed, rounded down, minimum one), Leftovers (1/16 max HP at turn end), Life Orb (1.3× damage / 1/10 max HP recoil), Charcoal and configurable type boosters (default 1.2×).
- Struggle when all moves have zero PP: typeless physical damage and 1/4 max HP recoil after a hit.

This is a prototype, not a complete Generation 9 engine. No critical hits, abilities, major status conditions, weather, terrain, hazards, EV/nature customization, special form rules, Terastallization/Stellar, multi-hit attacks, doubles, or exhaustive item/move effects. Unsupported content must be added explicitly. The AI chooses a simple expected-damage score and limited recovery/setup; it is not a competitive opponent. Catalog data is not a full Pokedex. Persistent saves and cross-version replay serialization are not implemented.

Type matchups were checked against the primary Pokemon Showdown source: https://github.com/smogon/pokemon-showdown/blob/master/data/typechart.ts. The source includes Stellar; this prototype intentionally supports only the 18 ordinary types.

Run the checks from the project root:

```powershell
dotnet build Assets/AeroStadium/Core/AeroStadium.Core.csproj --artifacts-path Tests/CoreChecks/artifacts
dotnet run --project Tests/CoreChecks/CoreChecks.csproj -- Assets/AeroStadium/Resources/Data/catalog.json
```
