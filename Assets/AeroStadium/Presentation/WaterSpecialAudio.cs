using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Original mono liquid synthesis for Water Pulse, Scald and Surf. No sampled media.</summary>
    public sealed class WaterSpecialAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;
        struct CachedClip { public AudioClip clip; public PcmStats stats; }
        readonly Dictionary<long, CachedClip> clips = new Dictionary<long, CachedClip>();
        static float[] pulsePcm, scaldPcm, surfPcm;

        [Serializable]
        public struct PcmStats
        {
            public int sampleRate, samples;
            public float duration, peak, rms, seamJump;
            public bool finite;
            public override string ToString() => "original water special samples=" + samples + " rate=" + sampleRate
                + " duration=" + duration.ToString("F3") + " peak=" + peak.ToString("F4")
                + " rms=" + rms.ToString("F4") + " finite=" + finite;
        }

        public PcmStats LastPcmStats { get; private set; }

        public static float DefaultDuration(int moveId)
        {
            Check(moveId);
            return moveId == 352 ? 2.30f : moveId == 503 ? 2.45f : 2.90f;
        }

        public static AudioSource Play(Transform parent, Vector3 source, int moveId, float duration)
        {
            Check(moveId);
            if (parent == null || !Finite(source.x) || !Finite(source.y) || !Finite(source.z)) return null;
            var owner = parent.GetComponentInParent<WaterSpecialAudio>();
            if (owner == null) owner = parent.gameObject.AddComponent<WaterSpecialAudio>();
            int samples = Mathf.RoundToInt(SafeDuration(duration, moveId) * SampleRate);
            long key = ((long)moveId << 32) | (uint)samples;
            if (!owner.clips.TryGetValue(key, out CachedClip cached))
            {
                var pcm = CreateOriginalPcm(moveId, samples / (float)SampleRate);
                var clip = AudioClip.Create("Original water special " + moveId + " " + samples, pcm.Length, 1, SampleRate, false);
                clip.SetData(pcm, 0);
                cached = new CachedClip { clip = clip, stats = AnalyzePcm(pcm) };
                owner.clips.Add(key, cached);
            }
            owner.LastPcmStats = cached.stats;
            var go = new GameObject(moveId == 352 ? "Vibraqua · original liquid pulse audio"
                : moveId == 503 ? "Ébullition · original hot water audio" : "Surf · original breaking wave audio");
            go.transform.SetParent(parent, false); go.transform.position = source;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.clip = cached.clip;
            audio.volume = moveId == 352 ? .54f : moveId == 503 ? .56f : .60f;
            audio.spatialBlend = .50f; audio.dopplerLevel = 0;
            audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 2; audio.maxDistance = 32;
            audio.priority = moveId == 57 ? 70 : 74;
            if (Application.isPlaying) { audio.Play(); Destroy(go, cached.clip.length + .12f); }
            return audio;
        }

        /// <summary>Default arrays are shared caches. Callers must treat their PCM samples as read-only.</summary>
        public static float[] CreateOriginalPcm(int moveId)
        {
            Check(moveId);
            if (moveId == 352) return pulsePcm ?? (pulsePcm = Synthesize(moveId, DefaultDuration(moveId)));
            if (moveId == 503) return scaldPcm ?? (scaldPcm = Synthesize(moveId, DefaultDuration(moveId)));
            return surfPcm ?? (surfPcm = Synthesize(moveId, DefaultDuration(moveId)));
        }

        public static float[] CreateOriginalPcm(int moveId, float duration)
        {
            Check(moveId); duration = SafeDuration(duration, moveId);
            return Mathf.RoundToInt(duration * SampleRate) == Mathf.RoundToInt(DefaultDuration(moveId) * SampleRate)
                ? CreateOriginalPcm(moveId) : Synthesize(moveId, duration);
        }

        static float[] Synthesize(int moveId, float duration)
        {
            int samples = Mathf.RoundToInt(duration * SampleRate);
            var pcm = new float[samples];
            var water = new LiquidNoise(moveId == 352 ? 0x352a7109u : moveId == 503 ? 0x503be713u : 0x57a9da31u);
            var drops = new DropBank();
            float nominal = DefaultDuration(moveId), step = nominal / (samples - 1);
            float nextDrop = moveId == 352 ? .09f : moveId == 503 ? .06f : .18f;
            float dcInput = 0, dcOutput = 0, resonancePhase = 0, rumblePhase = 0;
            for (int i = 0; i < samples; i++)
            {
                float p = i * step, t = i / (float)SampleRate;
                water.Tick();
                float low = water.low, body = water.body, wet = water.wet, spray = water.spray;
                float value;
                if (moveId == 352)
                {
                    // Low watery pressure gathers; three wet pulses follow the paired globe launches.
                    float charge = Window(p, .015f, .045f, .115f, .08f);
                    float launch = Burst(p, .16f, .008f, 19f) + .75f * Burst(p, .32f, .008f, 19f)
                        + .58f * Burst(p, .48f, .008f, 19f);
                    float travel = Window(p, .17f, .04f, .65f, .08f);
                    float ripples = .63f + .22f * Mathf.Sin(2f * Mathf.PI * 8.1f * t)
                        + .15f * Mathf.Sin(2f * Mathf.PI * 13.7f * t + .6f);
                    resonancePhase += 2f * Mathf.PI * (320f + 90f * water.slow + 32f * Mathf.Sin(t * 11f)) / SampleRate;
                    float resonance = Mathf.Sin(resonancePhase) * (.45f + .55f * Mathf.Abs(water.wet));
                    float contact = Burst(p, .65f, .022f, 6.4f);
                    float rings = Burst(p, .72f, .018f, 13f) + .56f * Burst(p, .87f, .018f, 12f)
                        + .30f * Burst(p, 1.04f, .018f, 10f);
                    float tail = Window(p, .80f, .10f, 1.75f, .28f) * Mathf.Exp(-Mathf.Max(0, p - .80f) * 2.5f);
                    value = charge * (.30f * body + .11f * resonance)
                        + launch * (.56f * wet + .21f * body + .06f * resonance)
                        + travel * ripples * (.29f * wet + .16f * body + .035f * resonance)
                        + contact * (.85f * wet + .54f * body + .22f * spray)
                        + rings * (.26f * wet + .085f * resonance) + tail * (.15f * wet + .07f * body);
                }
                else if (moveId == 503)
                {
                    // Fine boiling bubbles under a hot gush; steam is filtered turbulent air, never a flame crackle.
                    float boil = Window(p, .015f, .05f, .18f, .10f);
                    float gush = Window(p, .14f, .025f, .79f, .22f);
                    float contact = Burst(p, .68f, .025f, 5.0f);
                    float steam = Window(p, .59f, .11f, 1.54f, .49f);
                    float flow = .80f + .13f * water.slow + .07f * Mathf.Sin(t * 41f);
                    value = boil * (.34f * body + .17f * wet)
                        + gush * flow * (.72f * wet + .46f * body + .14f * spray)
                        + contact * (.77f * wet + .51f * body + .23f * spray)
                        + steam * (.23f * spray + .10f * wet) * (.75f + .25f * water.slow);
                }
                else
                {
                    // A large moving water mass rises, folds into foam and drains in a long downward rush.
                    float rising = Window(p, .025f, .46f, .83f, .40f);
                    float crest = Window(p, .45f, .40f, 1.13f, .48f);
                    float crash = Burst(p, .98f, .07f, 2.8f);
                    float foam = Window(p, 1.07f, .12f, 1.55f, .62f);
                    float drain = Window(p, 1.48f, .17f, 2.32f, .39f) * Mathf.Exp(-Mathf.Max(0, p - 1.48f) * .9f);
                    rumblePhase += 2f * Mathf.PI * (47f + 9f * water.slow + 7f * Mathf.Sin(t * 5f)) / SampleRate;
                    float rumble = .042f * Mathf.Sin(rumblePhase) + .021f * Mathf.Sin(rumblePhase * 1.47f);
                    float swell = .84f + .16f * Mathf.Sin(2f * Mathf.PI * 2.7f * t);
                    value = rising * swell * (.83f * low + .41f * body + .14f * wet + rumble)
                        + crest * (.61f * body + .57f * wet + .10f * spray)
                        + crash * (1.04f * body + .92f * wet + .35f * spray + .55f * low)
                        + foam * (.33f * wet + .26f * spray + .16f * body)
                        + drain * (.40f * wet + .24f * body + .08f * spray);
                }

                // Overlapping inharmonic bubble resonances impart liquid detail to all three materials.
                if (p >= nextDrop && p < (moveId == 352 ? 1.95f : moveId == 503 ? 2.12f : 2.64f))
                {
                    float random = water.Random01();
                    float impact = moveId == 352 ? .65f : moveId == 503 ? .68f : .98f;
                    bool tail = p > impact;
                    float size = moveId == 352 ? .042f : moveId == 503 ? .026f : .038f;
                    float gain = size * (.45f + .55f * water.Random01());
                    if (tail) gain *= Mathf.Exp(-(p - impact) * (moveId == 57 ? .9f : 1.6f));
                    float frequency = moveId == 352 ? 430f + random * 1150f
                        : moveId == 503 ? 900f + random * 1950f : 280f + random * 1350f;
                    drops.Spawn(frequency, gain, moveId == 503 ? 43f : 24f);
                    nextDrop = p + (moveId == 352 ? .028f : moveId == 503 ? .013f : .021f)
                        + water.Random01() * (tail ? .073f : .023f);
                }
                value += drops.Tick(step);
                // Remove DC, leave transients intact, and use linear headroom instead of a compressor.
                float highPassed = value - dcInput + .997f * dcOutput;
                dcInput = value; dcOutput = highPassed;
                float fade = Smooth(t / .008f) * Smooth((samples - 1 - i) / (SampleRate * .075f));
                pcm[i] = highPassed * fade;
            }
            float peak = 0;
            for (int i = 0; i < samples; i++) peak = Mathf.Max(peak, Mathf.Abs(pcm[i]));
            // A fixed reduction preserves the relative dynamics; exceptional peaks get further headroom.
            float gainScale = peak > .82f ? .82f / peak : .92f;
            for (int i = 0; i < samples; i++) pcm[i] *= gainScale;
            return pcm;
        }

        public static PcmStats AnalyzePcm(float[] pcm)
        {
            var stats = new PcmStats { sampleRate = SampleRate, finite = pcm != null };
            if (pcm == null) return stats;
            stats.samples = pcm.Length; stats.duration = pcm.Length / (float)SampleRate;
            double sum = 0;
            for (int i = 0; i < pcm.Length; i++)
            {
                float value = pcm[i];
                if (!Finite(value)) { stats.finite = false; continue; }
                stats.peak = Mathf.Max(stats.peak, Mathf.Abs(value)); sum += (double)value * value;
            }
            stats.rms = pcm.Length == 0 ? 0 : (float)Math.Sqrt(sum / pcm.Length);
            stats.seamJump = pcm.Length < 2 ? 0 : Mathf.Abs(pcm[0] - pcm[pcm.Length - 1]);
            return stats;
        }

        static float Burst(float p, float start, float attack, float decay)
        {
            float age = p - start;
            return age < 0 ? 0 : Smooth(age / attack) * Mathf.Exp(-age * decay);
        }
        static float Window(float p, float start, float attack, float release, float fade)
            => Smooth((p - start) / attack) * (1f - Smooth((p - release) / fade));
        static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float SafeDuration(float duration, int moveId) => Finite(duration) ? Mathf.Clamp(duration, .5f, 5f) : DefaultDuration(moveId);
        static void Check(int moveId)
        {
            if (moveId != 352 && moveId != 503 && moveId != 57) throw new ArgumentOutOfRangeException(nameof(moveId));
        }

        struct LiquidNoise
        {
            uint state;
            float lpLow, lpBody, lpWet, lpAir;
            public float low, body, wet, spray, slow;
            public LiquidNoise(uint seed)
            {
                state = seed; lpLow = lpBody = lpWet = lpAir = 0;
                low = body = wet = spray = slow = 0;
            }
            public float Random01()
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (state & 0xffffffu) / 16777215f;
            }
            public void Tick()
            {
                float noise = Random01() * 2f - 1f;
                lpLow += .010f * (noise - lpLow); lpBody += .043f * (noise - lpBody);
                lpWet += .23f * (noise - lpWet); lpAir += .62f * (noise - lpAir);
                slow += .0021f * (noise - slow);
                low = lpLow * 2.7f; body = (lpBody - lpLow) * 2.0f;
                wet = (lpWet - lpBody) * 1.36f; spray = lpAir - lpWet;
            }
        }

        struct DropVoice
        {
            float phase, age, level, frequency, decay;
            public void Start(float hz, float amplitude, float decayRate)
            { phase = age = 0; frequency = hz; level = amplitude; decay = decayRate; }
            public float Tick(float nominalStep)
            {
                if (level < .000001f) return 0;
                age += nominalStep;
                phase += 2f * Mathf.PI * frequency / SampleRate;
                frequency *= Mathf.Exp(-5.0f * nominalStep);
                level *= Mathf.Exp(-decay * nominalStep);
                return level * Smooth(age / .0035f) * (Mathf.Sin(phase) + .19f * Mathf.Sin(phase * 1.71f));
            }
        }
        struct DropBank
        {
            DropVoice a, b, c, d;
            int next;
            public void Spawn(float frequency, float amplitude, float decay)
            {
                if (next == 0) a.Start(frequency, amplitude, decay);
                else if (next == 1) b.Start(frequency, amplitude, decay);
                else if (next == 2) c.Start(frequency, amplitude, decay);
                else d.Start(frequency, amplitude, decay);
                next = (next + 1) & 3;
            }
            public float Tick(float step) => a.Tick(step) + b.Tick(step) + c.Tick(step) + d.Tick(step);
        }

        void OnDestroy()
        {
            foreach (CachedClip cached in clips.Values)
                if (cached.clip != null)
                { if (Application.isPlaying) Destroy(cached.clip); else DestroyImmediate(cached.clip); }
            clips.Clear();
        }
    }
}
