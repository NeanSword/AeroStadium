using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Original procedural fire sounds. No recordings, music, or external sound assets.</summary>
    public sealed class ArenaFireAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;
        public const float AmbientVolume = .008f;
        public const float FlamethrowerVolume = .48f;
        public const float ProtectActivationVolume = .42f;
        public const float ProtectBlockVolume = .58f;
        const float AmbientSeconds = 12f;

        [Serializable]
        public struct PcmStats
        {
            public int sampleRate, samples;
            public float duration, peak, rms, seamJump;
            public bool finite;
            public override string ToString()
            {
                return "original PCM samples=" + samples + " rate=" + sampleRate
                    + " duration=" + duration.ToString("F3") + " peak=" + peak.ToString("F4")
                    + " rms=" + rms.ToString("F4") + " seamJump=" + seamJump.ToString("F6")
                    + " finite=" + finite;
            }
        }

        AudioSource ambient;
        AudioClip ambientClip;
        PcmStats ambientStats, attackStats;
        struct CachedClip { public AudioClip clip; public PcmStats stats; }
        readonly Dictionary<int, CachedClip> attackClips = new Dictionary<int, CachedClip>();
        readonly Dictionary<bool, AudioClip> protectClips = new Dictionary<bool, AudioClip>();
        public PcmStats AmbientPcmStats => ambientStats;
        public PcmStats FlamethrowerPcmStats => attackStats;
        public AudioSource AmbientSource => ambient;
        public static PcmStats DefaultAttackPcmStats { get; private set; }
        public static float AttackPeak => DefaultAttackPcmStats.peak;

        /// <summary>One extremely quiet, seamless lava/ember bed owned by the stage.</summary>
        public static ArenaFireAudio BuildAmbient(Transform stage)
        {
            if (stage == null) return null;
            var owner = stage.GetComponent<ArenaFireAudio>();
            if (owner == null) owner = stage.gameObject.AddComponent<ArenaFireAudio>();
            owner.EnsureAmbient();
            return owner;
        }

        /// <summary>The caller destroys the returned source's GameObject when the visual effect ends.</summary>
        public static AudioSource PlayFlamethrower(Transform parent, Vector3 worldPosition, float duration = 3.15f)
        {
            if (parent == null) return null;
            var owner = parent.GetComponentInParent<ArenaFireAudio>();
            if (owner == null) owner = parent.gameObject.AddComponent<ArenaFireAudio>();
            if (float.IsNaN(duration) || float.IsInfinity(duration)) duration = 3.15f;
            int samples = Mathf.RoundToInt(Mathf.Clamp(duration, .5f, 10f) * SampleRate);
            CachedClip cached = owner.GetAttackClip(samples);
            owner.attackStats = cached.stats;
            var go = new GameObject("Original flamethrower audio");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = cached.clip;
            source.volume = FlamethrowerVolume;
            source.spatialBlend = .65f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 25f;
            source.dopplerLevel = 0f;
            source.priority = 80;
            if (Application.isPlaying) source.Play();
            return source;
        }

        /// <summary>Original forming-shield whoosh and ascending crystalline harmonics.</summary>
        public static AudioSource PlayProtectActivation(Transform parent, Vector3 position)
            => PlayProtect(parent, position, false);

        /// <summary>Original pressure impact with a short resonant shield tail.</summary>
        public static AudioSource PlayProtectBlock(Transform parent, Vector3 position)
            => PlayProtect(parent, position, true);

        static AudioSource PlayProtect(Transform parent, Vector3 position, bool blocked)
        {
            if (parent == null) return null;
            var owner = parent.GetComponentInParent<ArenaFireAudio>();
            if (owner == null) owner = parent.gameObject.AddComponent<ArenaFireAudio>();
            if (!owner.protectClips.TryGetValue(blocked, out AudioClip clip))
            {
                clip = MakeClip(blocked ? "Original shield deflection" : "Original shield formation", CreateProtectPcm(blocked));
                owner.protectClips.Add(blocked, clip);
            }
            var go = new GameObject(blocked ? "Protect block audio" : "Protect activation audio");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.clip = clip;
            audio.volume = blocked ? ProtectBlockVolume : ProtectActivationVolume;
            audio.spatialBlend = .40f;
            audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 4f;
            audio.maxDistance = 35f;
            audio.dopplerLevel = 0f;
            audio.priority = blocked ? 55 : 60;
            if (Application.isPlaying)
            {
                audio.Play();
                Destroy(go, clip.length + .1f);
            }
            return audio;
        }

        /// <summary>Original shield sound samples, also available for local review exports.</summary>
        public static float[] CreateProtectPcm(bool blocked)
        {
            float seconds = blocked ? .75f : .85f;
            var pcm = new float[Mathf.RoundToInt(seconds * SampleRate)];
            var noise = new Noise(blocked ? 0xa79285b1u : 0x859ac112u);
            float low = 0f, air = 0f, phase = 0f;
            for (int i = 0; i < pcm.Length; i++)
            {
                float t = i / (float)SampleRate, n = noise.Next();
                low += .035f * (n - low);
                air += .20f * (n - air);
                float frequency = blocked ? Mathf.Lerp(1040f, 490f, Smooth01(t / .35f))
                    : Mathf.Lerp(220f, 670f, Smooth01(t / .23f));
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float value;
                if (blocked)
                {
                    float impact = Mathf.Exp(-t * 21f), ring = Mathf.Exp(-t * 5.5f);
                    float transient = Mathf.Exp(-t * 45f);
                    value = impact * (1.1f * low + .54f * Mathf.Sin(2f * Mathf.PI * 76f * t))
                        + transient * .7f * air
                        + ring * (.23f * Mathf.Sin(phase) + .11f * Mathf.Sin(phase * 1.503f)
                            + .09f * Mathf.Sin(2f * Mathf.PI * 710f * t) + .18f * air);
                }
                else
                {
                    float gather = Smooth01(t / .045f) * (1f - Smooth01((t - .20f) / .12f));
                    float resonanceTime = Mathf.Max(0f, t - .22f);
                    float bloom = Smooth01((t - .22f) / .022f) * Mathf.Exp(-resonanceTime * 5f);
                    value = gather * (1.1f * low + .55f * air + .17f * Mathf.Sin(phase))
                        + bloom * (.31f * Mathf.Sin(2f * Mathf.PI * 640f * resonanceTime)
                            + .17f * Mathf.Sin(2f * Mathf.PI * 960f * resonanceTime)
                            + .09f * Mathf.Sin(2f * Mathf.PI * 1280f * resonanceTime) + .27f * low);
                }
                float fade = Smooth01(t / .005f) * Smooth01((seconds - t) / .08f);
                pcm[i] = .9f * (float)Math.Tanh(value * 1.35f) * fade;
            }
            return pcm;
        }

        CachedClip GetAttackClip(int samples)
        {
            if (attackClips.TryGetValue(samples, out CachedClip cached)) return cached;
            float[] pcm = CreateFlamethrowerPcm(samples);
            cached = new CachedClip { clip = MakeClip("Original flame breath " + samples, pcm), stats = AnalyzePcm(pcm) };
            if (samples == Mathf.RoundToInt(3.15f * SampleRate)) DefaultAttackPcmStats = cached.stats;
            attackClips.Add(samples, cached);
            return cached;
        }

        void EnsureAmbient()
        {
            if (ambient != null) return;
            float[] pcm = CreateAmbientPcm();
            ambientStats = AnalyzePcm(pcm);
            ambientClip = MakeClip("Original quiet lava and embers", pcm);
            var go = new GameObject("Very quiet arena fire ambience");
            go.transform.SetParent(transform, false);
            ambient = go.AddComponent<AudioSource>();
            ambient.playOnAwake = false;
            ambient.clip = ambientClip;
            ambient.loop = true;
            ambient.volume = AmbientVolume;
            ambient.spatialBlend = 0f;
            ambient.dopplerLevel = 0f;
            ambient.priority = 192;
            // Prewarm the standard attack so playback allocates no PCM arrays.
            attackStats = GetAttackClip(Mathf.RoundToInt(3.15f * SampleRate)).stats;
            if (Application.isPlaying) ambient.Play();
        }

        public bool ValidateAmbient(out string report)
        {
            bool ok = ambient != null && ambient.clip == ambientClip && ambient.loop
                && ambient.volume <= .012f && ambient.spatialBlend == 0f
                && ambientStats.finite && ambientStats.peak < .5f && ambientStats.seamJump < .03f;
            report = "ambient volume=" + (ambient == null ? "missing" : ambient.volume.ToString("F4"))
                + " " + ambientStats + " passed=" + ok;
            return ok;
        }

        static AudioClip MakeClip(string name, float[] pcm)
        {
            var clip = AudioClip.Create(name, pcm.Length, 1, SampleRate, false);
            clip.SetData(pcm, 0);
            return clip;
        }

        // Generate once. At the wrap, the start continues the end's noise sequence,
        // then smoothly crossfades to the original beginning over a quarter second.
        public static float[] CreateAmbientPcm()
        {
            int count = Mathf.RoundToInt(AmbientSeconds * SampleRate), overlap = SampleRate / 4;
            float[] raw = new float[count + overlap], pcm = new float[count];
            var noise = new Noise(0x6a7b928du);
            float low = 0f, mid = 0f, crackle = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float t = i / (float)SampleRate;
                float n = noise.Next();
                low += .019f * (n - low);
                mid += .11f * (n - mid);
                crackle *= .984f;
                if (noise.Next() > .9986f) crackle += noise.Next() * .035f;
                float drift = .85f + .15f * Mathf.Sin(2f * Mathf.PI * t / AmbientSeconds);
                // Every tonal frequency completes an integer number of loop cycles.
                raw[i] = .13f * low * drift + .045f * mid
                    + .020f * Mathf.Sin(2f * Mathf.PI * 43f * t)
                    + .010f * Mathf.Sin(2f * Mathf.PI * 67f * t)
                    + crackle * Mathf.Sin(2f * Mathf.PI * 530f * t);
            }
            Array.Copy(raw, pcm, count);
            for (int i = 0; i < overlap; i++)
            {
                float mix = Smooth01(i / (float)(overlap - 1));
                pcm[i] = Mathf.Lerp(raw[count + i], raw[i], mix);
            }
            return pcm;
        }

        // The envelope mirrors the original effect's charge, continuous breath,
        // contact burst, and release. Filtered noise avoids a harsh white hiss.
        public static float[] CreateFlamethrowerPcm(int samples)
        {
            samples = Mathf.Max(samples, SampleRate / 2);
            float[] pcm = new float[samples];
            var noise = new Noise(0x93ca5107u);
            float bass = 0f, middle = 0f, air = 0f, crackle = 0f, chargePhase = 0f;
            float seconds = samples / (float)SampleRate;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate, phase = t / seconds * 3.15f;
                float n = noise.Next();
                bass += .025f * (n - bass);
                middle += .14f * (n - middle);
                air += .30f * (n - air);
                float ignition = Smooth01((phase - .29f) / .15f);
                float breath = ignition * (1f - Smooth01((phase - 2.46f) / .32f));
                float charge = Smooth01(phase / .12f) * (1f - Smooth01((phase - .28f) / .16f));
                float tail = phase > 2.46f ? Mathf.Exp(-(phase - 2.46f) * 8f) : 0f;
                float impact = phase > 2.50f ? Mathf.Exp(-(phase - 2.50f) * 13f) : 0f;
                impact *= Smooth01((phase - 2.50f) / .012f);
                chargePhase += 2f * Mathf.PI * Mathf.Lerp(72f, 138f, Smooth01(phase / .35f)) / SampleRate;
                crackle *= .968f;
                if (noise.Next() > .993f) crackle += noise.Next() * .38f;
                float pulse = .76f + .17f * Mathf.Sin(2f * Mathf.PI * 8.6f * t)
                    + .07f * Mathf.Sin(2f * Mathf.PI * 13.3f * t);
                float rumble = .12f * Mathf.Sin(2f * Mathf.PI * 61f * t)
                    + .065f * Mathf.Sin(2f * Mathf.PI * 93f * t);
                float value = charge * (.18f * bass + .045f * Mathf.Sin(chargePhase))
                    + breath * pulse * (1.9f * bass + .57f * middle + .19f * (air - middle) + rumble)
                    + (breath + .28f * tail) * crackle * Mathf.Sin(2f * Mathf.PI * 760f * t)
                    + impact * (1.1f * bass + .3f * middle + .17f * Mathf.Sin(2f * Mathf.PI * 48f * t))
                    + tail * .30f * bass;
                float edgeFade = Smooth01(t / .012f) * Smooth01((seconds - t) / .075f);
                pcm[i] = .85f * (float)Math.Tanh(value * 1.3f) * edgeFade;
            }
            return pcm;
        }

        public static PcmStats AnalyzePcm(float[] pcm)
        {
            var stats = new PcmStats { sampleRate = SampleRate, samples = pcm.Length,
                duration = pcm.Length / (float)SampleRate, finite = true };
            double sum = 0;
            for (int i = 0; i < pcm.Length; i++)
            {
                float v = pcm[i];
                if (float.IsNaN(v) || float.IsInfinity(v)) { stats.finite = false; continue; }
                stats.peak = Mathf.Max(stats.peak, Mathf.Abs(v));
                sum += v * v;
            }
            stats.rms = pcm.Length == 0 ? 0f : (float)Math.Sqrt(sum / pcm.Length);
            stats.seamJump = pcm.Length < 2 ? 0f : Mathf.Abs(pcm[0] - pcm[pcm.Length - 1]);
            return stats;
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        struct Noise
        {
            uint state;
            public Noise(uint seed) { state = seed; }
            public float Next()
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (state & 0xffffffu) / 8388607.5f - 1f;
            }
        }

        void OnDestroy()
        {
            if (ambient != null) ambient.Stop();
            if (ambientClip != null) Release(ambientClip);
            foreach (var cached in attackClips.Values) if (cached.clip != null) Release(cached.clip);
            attackClips.Clear();
            foreach (var clip in protectClips.Values) if (clip != null) Release(clip);
            protectClips.Clear();
        }

        static void Release(AudioClip clip)
        {
            if (Application.isPlaying) Destroy(clip);
            else DestroyImmediate(clip);
        }
    }
}
