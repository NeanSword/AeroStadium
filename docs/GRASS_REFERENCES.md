# Grass attack reference notes

Checked 2026-10-15. Scope: move 22 Fouet Lianes / Vine Whip and move 75 Tranch'Herbe / Razor Leaf only. No downloaded game media, copied audio, third-party assets, paid generation, or external synthesis service.

## Primary sources actually inspected

1. [Official Pokémon UNITE Venusaur page (English)](https://unite.pokemon.com/en-us/pokemon/venusaur/) and [French Florizarre page](https://unite.pokemon.com/fr-fr/pokemon/venusaur/). Undated live pages, accessed 2026-10-15. Accessible page text describes Venusaur as a ranged attacker with substantial ranged damage and describes its seed-bomb Unite move. The French page confirms Florizarre and Fureur Végétale labels. These pages do **not** supply a move-22/move-75 animation breakdown, audio details, frame timings, or move IDs. Their evolution-level text also should not be treated as proof of the latest gameplay values.

2. [Venusaur Character Spotlight | Pokémon UNITE](https://www.youtube.com/watch?v=xahLahZppUY), verified Pokémon UNITE channel, published **2021-07-09**. Search-exposed official metadata/title/description were accessible: the description places the character in ranged area damage. Direct video fetching failed. **The video was not watched or heard in this task.** No frame-by-frame observations, timings, particular sounds, or exact leaf/vine counts are attributed to it. This is an archival official pointer, not recent visual proof.

3. [Official Japan balance update: Long Range Tactics Part 2](https://www.pokemonunite.jp/ja/topics/battle/20260319-1/), published **2026-03-19**, update time stated as 16:00. Current official text specifically says Venusaur's long-range Solar Beam and Sludge Bomb are being strengthened; it also changes Unite energy requirements. This is a recent official character check, **not** evidence for the animation, sound, timing, or mechanics of Vine Whip or Razor Leaf. No unrelated mechanics from this update were implemented.

The bounded primary-source search did not locate a recent official Venusaur demonstration exposing Vine Whip and Razor Leaf timing. It would be inaccurate to label the archival spotlight as a recent video or claim an exact UNITE reconstruction. Move IDs and French attack names are project inputs, not claims established by the sources above. No secondary guide is used as implementation authority.

## Original AeroStadium interpretation

The resulting effects are original stylized plant attacks guided by the project's requested identities and its own animation schedule. They are not imported or reconstructed official effects. Readable shaped leaves, flexible vines and discrete contact accents communicate the plant material. Sound is deterministic local 22050 Hz mono synthesis from filtered seeded noise, short resonances and shaped amplitude envelopes.

- **22 / Fouet Lianes:** gather 0.00–0.22 s; flexible extension 0.22–0.68 s; paired contact accents at 0.598 and 0.661 s; recoil approximately 0.71–0.88 s; diminishing retraction 0.86–1.43 s; nominal effect duration 1.68 s. Low creak, fibrous wet tension, two small sharp snaps and a descending recoil distinguish it from a sustained beam.
- **75 / Tranch'Herbe:** nominal effect duration 2.15 s. Source rustle supports thirteen staggered original pass sounds starting at 0.14 + i × 0.026 s, with varied flight intervals 0.48–0.57 s and short clipped leaf contacts. The short band-shaped whizzes have flutter, weak resonant detail and curved frequency motion. These numbers follow the project animation brief, not measured official footage.

No audio processing runs each frame. Attack clips are generated once per owner/move/duration and reused, transient playback objects clean themselves up, and owner destruction releases cached clips. Shared default PCM arrays are read-only by API contract. Both endpoints are exactly zero; a short amplitude fade and soft saturation prevent hard boundary clicks and excess peaks. Actual PCM statistics and WAV review are produced by the root task from the public generator, not asserted here without executing the Unity code.
