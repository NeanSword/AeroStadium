# Original water special audio — 2026-10-15

Staged files only: `WaterSpecialAudio.cs`, `WaterSpecialAudio.cs.meta`, and this note. No repository file, synced `sources/` file, build, runtime, or commit was changed by this subtask. Runtime WAV export and listening verification belong to the integrating task.

## Audio contract

- Namespace/class: `AeroStadium.Presentation.WaterSpecialAudio`.
- Public static APIs: `Play(Transform parent, Vector3 source, int moveId, float duration)`, `CreateOriginalPcm(int moveId)`, `CreateOriginalPcm(int moveId, float duration)`, `AnalyzePcm(float[] pcm)`, `DefaultDuration(int moveId)`.
- Move IDs: 352 Vibraqua / Water Pulse; 503 Ébullition / Scald; 57 Surf. Unknown IDs throw `ArgumentOutOfRangeException`.
- Mono, 22,050 Hz. Non-finite duration falls back to that move's default; finite duration clamps to 0.5–5 seconds. Non-finite positions and null parent return no source.
- `PcmStats`: public fields `sampleRate`, `samples`, `duration`, `peak`, `rms`, `seamJump`, `finite`. `LastPcmStats` records the clip used by the owner.
- Each move's default PCM is lazily cached; callers must treat the returned cached arrays as read-only. Generated Unity clips are cached per owner/move/sample count. They are destroyed when the owner is destroyed. A source child destroys itself after its clip ends. No `Update`, per-frame allocation, imported media, paid tool or external audio service.
- Playback levels: Vibraqua 0.54, Ébullition 0.56, Surf 0.60. Linear distance rolloff, partial spatial blend, no doppler. Original synthesis combines deterministic colored turbulence, overlapping inharmonic bubble ring-downs and material envelopes. DC removal and smooth starts/ends are applied. A linear peak guard leaves headroom at 0.82 without a compressor or waveshaper.

## Synchronization

All following times are authored nominal times. The envelope/event timeline scales proportionally if the requested duration differs from the default. Oscillator cycles remain at the audio sample rate, so changing duration does not globally pitch-shift the material.

| Move | Default duration | Charge / launch | Main contact | Follow-through / tail |
| --- | --- | --- | --- | --- |
| 352 Vibraqua | 2.30 s | Gather 0.015–0.195 s; quieter wet launches at 0.16, 0.32 and 0.48 s; rippled travelling body 0.17–0.73 s | Splash envelope begins 0.65 s (`u=0.28261`) with 0.022 s rise; strongest initial contact about 0.67–0.70 s | Diminishing resonant water rings at 0.72, 0.87 and 1.04 s; liquid falls away through ~2.03 s |
| 503 Ébullition | 2.45 s | Boil gather 0.015–0.28 s; hot gush begins 0.14 s and releases after 0.79 s | Wet contact begins 0.68 s (`u=0.27755`) with 0.025 s rise, about 0.70 s peak | Fine turbulent steam-like hiss 0.59–2.03 s; small wet bubbles/drops through ~2.2 s |
| 57 Surf | 2.90 s | Broad low water mass rises from 0.025 s; crest grows from 0.45 s | Heavy water/foam crash begins 0.98 s (`u=0.33793`), rises over 0.07 s, main crest ~1.04–1.08 s | Foam after 1.07 s; receding/draining body 1.48–2.71 s; sparse droplets through ~2.7 s |

The final sample is exactly faded to zero. The first sample is zero; the first 8 ms are smoothly gated. Numerical PCM and subjective playback have not been claimed as verified by this subtask.

## Official reference evidence

All sources below were searched/read on 2026-10-15. Only official publisher pages are used as evidence. No downloaded or copied sound, video, texture, or other game media appears in the implementation. No video frames or soundtrack were inspected, so these notes make no frame-by-frame or sound-matching claim.

1. **Water Pulse: a forward travelling pulsation with a distinct contact.** [Azumarill Offers Bubble Trouble in Pokémon UNITE](https://unite.pokemon.com/en-gb/news/azumarill-offers-bubble-trouble-in-pokemon-unite/). Official UNITE news; the official [news index](https://unite.pokemon.com/en-au/news/) dates this article to **2022-04-08**. The article text states that Water Pulse shoots a pulsing water blast forward, damages an area around the opponent it hits, can bounce to another target, and has a travelling phase. This supports the forward liquid pulse and wet contact. It does **not** specify a spherical shape, sound design, timing in seconds, or our three launch pulses. Those are original staging choices.

2. **Water Pulse can create spreading water rings in another official implementation.** [Lapras Is Now Available in Pokémon UNITE](https://www.pokemon.com/us/pokemon-news/lapras-is-now-available-in-pokemon-unite), **2023-04-13**. Official Pokémon article text describes a pulse centered on Lapras which spreads outward, with additional pulses under stated gameplay conditions. This supports water-wave/ripple vocabulary. It does **not** establish that all Water Pulse uses are radial, or prescribe the current project's projectile shape.

3. **A recent check confirms the move names remain in official Pokémon coverage.** [The Pokémon GO December 2025 Community Day Event Sees Featured Pokémon Return](https://www.pokemon.com/uk/news/the-pokemon-go-december-2025-community-day-event-sees-featured-pokemon-return), **2025-12-03**. The official article lists Scald for Vaporeon and Water Pulse for Glaceon among featured attacks. This confirms recent official naming only; it supplies no audiovisual or timing detail. The web search extraction exposed the article body/date; opening the URL directly returned only a wrapper, so the exact supported claims here are limited to that retrieved publisher text.

4. **Scald has a burn-associated identity in a current official UNITE patch.** [10月13日（火）バランス調整「星雷のヴォルテックス」のお知らせ](https://www.pokemonunite.jp/ja/topics/battle/20261013-2/), **2026-10-13**. Under ヤドラン (Slowbro), the publisher names ねっとう (Scald) and changes its burn duration from 3 to 4 seconds; it also names なみのり (Surf) and changes cooldown from 7 to 6.5 seconds. This supports the current presence of both moves and Scald's heat/burn cue. The steam-like hiss, boil and hot gush are original interpretations of that cue and the requested hot-water direction; the patch does not specify steam, gush geometry, color, sound or animation duration. Its gameplay seconds are not used as FX timings.

5. **Surf's defining large-wave silhouette.** [Waxing Nostalgic About Surfing Pikachu](https://www.pokemon.com/us/pokemon-news/waxing-nostalgic-about-surfing-pikachu), **2024-06-12**. The official article introduces Surf as a Water-type move that summons a large wave over the opponent. This directly supports a rising water mass and broad wave crash, while foam, drainage, rumble, exact timing and audio timbre remain original authoring choices. The search extraction exposed the publisher article body/date; direct opening returned only a wrapper.

The recent sources substantiate continued move naming/mechanics, while the official move descriptions provide the stable material signatures. None of the cited sources is presented as proof of the exact procedural FX, shield behavior, floor deformation, acoustic waveform, or timings authored for AeroStadium.
