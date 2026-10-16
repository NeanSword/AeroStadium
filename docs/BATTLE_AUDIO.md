# Battle music preparation

Source selected by the user: https://www.youtube.com/watch?v=XEKBrjtCdL8

Title: **Kanto Wild Pokémon Battle remix (From "Pokémon Red/Blue/Yellow")**. Uploader: **DeadmanPR**. Uploaded 2021-02-16. Channel: https://www.youtube.com/channel/UC646Cb7aUjii2ijAb70T2ww . The public video description identifies this as part of the uploader's Wild Pokémon battle themes medley. The source metadata was read directly from public YouTube responses; no account, cookies, paid tools or bypass was used.

## Ready to import

| File | Rate / encoding | Frames | Duration | Original interval |
|---|---|---:|---:|---|
| AeroStadiumBattleIntro.wav | 48 kHz stereo PCM16 | 1,885,461 | 39.2804375 s | [0, 39.2804375 s) |
| AeroStadiumBattleLoop.wav | 48 kHz stereo PCM16 | 1,502,609 | 31.3043541667 s | [39.2804375, 70.5847916667 s) |

The intro file includes the original intro and first musical statement. It begins at source sample 0 and preserves its leading silence and dynamics, without trimming or a fade. One common constant gain of -1.654051737 dB provides -1.2 dBFS peak headroom for both clips. The intro is identical to the corresponding decoded source after that gain and ordinary 16-bit quantization.

The main repeated section is 96 quarter-note beats at inferred 184 BPM, or 24 bars in 4/4. The period rounds to 1,502,609 samples, only 6.34 microseconds longer than the theoretical period. Multiple independent four-second waveform matches throughout the main section identify the same period. The cut uses a later inferred phrase re-entry on the bar grid (musical start around 0.150 s plus 30 bars), retaining the first statement before any repeat. This phrase-grid assessment is analytical; subjective listening has not been performed.

Only the final 960 frames / 20 ms of the loop are blended with a raised-cosine toward the source's pre-roll immediately before the loop's starting sample. This preserves the entire musical period. Tail/pre-roll waveform correlation is 0.88849, with a 0.793 dB RMS difference. Both the intro-to-loop and loop-to-loop steps match the natural adjacent source step to within 0.00001110 amplitude (less than one PCM16 quantization interval). No samples clip and no 100 ms loop block is silent.

## Integration

Resources names expected by the runtime sibling:

- `Audio/AeroStadiumBattleIntro`
- `Audio/AeroStadiumBattleLoop`

Use two 2D AudioSources and the same intended music volume. Schedule the intro with sample 0 at battle entrance, without an initial fade, and schedule the looping source with `PlayScheduled(introDspTime + (double)intro.samples / intro.frequency)`. Intro `loop=false`; body `loop=true`. The first loop wrap occurs at battle entrance plus 70.5847916667 s. PCM / DecompressOnLoad, preload enabled, background load disabled, 48 kHz preserved are appropriate for reliable sample-aligned playback. Combined PCM16 file size is about 13.55 MB; decompressed float stereo storage is about 27.1 MB.

The existing menu component `MainMenuAudio` applies a 1.2-second entrance fade and therefore should not directly supply the battle entrance behavior. `GameBootstrap.StartBattle()` is the entrance hook. Begin the battle music only after its battle/ busy guard passes, and keep it playing through normal turn redraws. Stop or fade both battle sources on title/menu/selection exit and reset them before the next battle. A short unscaled 0.4–0.7-second exit fade is appropriate; no fade is applied between turns or at the intro/body transition.

## Review material and reproducibility

- `entrance-review.wav`: first five seconds, starting at original source 0.
- `intro-to-loop-review.wav`: two seconds before the exact intro handoff plus four seconds after it. The boundary is at preview 2.000 seconds.
- `loop-seam-review.wav`: two seconds before a repeat plus four seconds after it. The boundary is at preview 2.000 seconds.
- `battle-music.json`: source credit, hashes, exact frames, gain, seam metrics and runtime guidance.
- `prepare_battle_music.py`: local-only deterministic preparation script; it consumes the downloaded source decoded as stereo 48 kHz float32 `source.f32` and its source metadata.

Media and source metadata remain local under `output/audio/battle-XEKBrjtCdL8-20261016`; runtime WAVs live in ignored `Resources/Audio`. Source code and credits can be committed without the recordings. Run `python Tools/prepare_battle_music.py PATH_TO_WORKING_DIRECTORY` with NumPy, `source.f32` decoded to stereo 48 kHz float32 and `XEKBrjtCdL8.info.json` in that directory. Runtime verification is recorded separately below.


## Runtime verification,2026-10-16

Actual front-end path test passed=True, exit=0, process duration=174.49s. JSON: `output/attacks/battle-music-20261016-v2/verified-summary.json`. Intro samples0, constant initial gain, exact scheduled DSP join, two completed loops, turn/pause continuity, fade exit, menu restoration, new battle intro and disable cleanup are checked individually. This does not measure acoustic output or certify subjective listening. Experimental build only; stable build preserved.
