# Battle loadouts and mechanics

Updated 11 October 2026. The imported database is broader than the battle rules currently implemented. Importing an attack or item does not imply that its specialized rule has been implemented.

## Team configuration

Each `TeamMember` has `speciesId`, optional `itemId`, optional `moveIds`, `ivs` and `evs`. Null move IDs use the species' initial rental moves. Custom selections contain one to four different moves from that species' `learnableMoves`. There is no restriction to a particular learning method: the data importer records the eligible modern source games and unions their documented methods. A move from another species, an unknown ID, duplicate moves, an empty selection and more than four moves are rejected before a battle is created.

Individual values accept 0–31 for each stat; effort values accept 0–252 per stat and no more than 510 total. Null profiles preserve the rental defaults (IV31, EV0); explicit zeros remain zero. Stats use the standard integer formula, EV contribution rounded down after division by four, level 50 by default, and a neutral nature. `TeamMember.Copy`, `Catalog.GetLearnableMoves`, `Catalog.CreatePokemon`, and the battle constructor isolate mutable arrays and stat profiles from caller changes. Species definitions remain immutable catalog references.

The complete item catalog includes inventory and key items. Team slots accept only held items (`holdable`), plus no item. Catalogued unsupported held items can be equipped for later implementation, but have no invented battle effect; their support label is shown by the team editor.

## Implemented families

- Physical and special damage use separate attacking and defensive stats, modern 18-type interactions, STAB, priority, PP and seeded damage rolls. Accuracy zero means an always-hit move; positive accuracy respects accuracy and evasion stages.
- All seven ordinary battle stages (Attack, Defense, Special Attack, Special Defense, Speed, accuracy and evasion) support direct and secondary changes and reset on switching.
- Burn, paralysis, poison, toxic poison, sleep and freeze support relevant type immunities, action prevention, speed/physical-damage changes and end-of-turn damage. Poison damage and badly poisoned damage differ; switching resets toxic escalation, not the underlying status.
- Healing, draining, recoil based on actual damage, recoil based on maximum HP, fixed damage, level damage, half-current-HP damage, ordinary multi-hit distributions and flinch are driven by catalog metadata.
- Protect retains the documented prototype simplification: using Protect on consecutive turns fails. It is not a complete implementation of all modern protection variants.
- Transform copies the target's species/types, five ordinary stats, stages and moves (5 PP per move), while retaining the user's HP, level, nickname, held item and status. Switching restores the original species, stats and original moves/PP. The prototype has no ability subsystem to copy.
- Splash correctly has no battle effect, and reports this explicitly.
- Supported held families: Leftovers, Life Orb, matching type boosters, Choice Band/Specs/Scarf (including move locking), Assault Vest, Focus Sash at full HP, healing berries, Lum/status berries, Black Sludge and Rocky Helmet on moves flagged as making contact. Consumed items stop applying their effects. Choice locking clears when the holder switches.

## Explicit limits

`MoveMechanics.support` is `implemented`, `partial` or `unsupported`. Unsupported moves remain available in a legal team to expose the complete database and enable later development, but consume a PP and report a specific failure instead of silently succeeding. Partial moves apply their generic supported metadata; their specialized part remains incomplete. Their distinction is displayed in the team editor. `ItemDefinition.support` follows the same catalog contract.

The engine is a singles prototype. It does not yet implement abilities, natures, critical-hit rules, weather, terrain, entry hazards, double battles, all volatile conditions, most move-specific power changes, charge/recharge and every specialized item. Protection probabilities and interactions with abilities, item removal/suppression and some secondary-effect ordering therefore still need a dedicated rules implementation. The imported database is not a claim of complete official battle compatibility.

`BattleEngine.CanUseMove(side, slot)` reports PP, Choice locking and Assault Vest constraints. Callers should use this API when enabling attack buttons. If no usable PP remain, Struggle is available; the AI uses this same eligibility logic. Presentation-specific new attack animations are not added by this change.

## Validation

The dependency-free core suite covers default behavior, customized legal moves and copied profiles, every IV/EV limit, status/stage effects, always-hit and fixed/multi-hit/drain/recoil moves, held restrictions, consumed items, Transform restoration, and explicit unsupported failure. It also validates the entire actual catalog, all 151 configured Kanto profiles and every eligible held item, then compares deterministic replays.
