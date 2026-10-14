using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Locally synthesized liquid pressure, spray and droplets, with reusable attack clips.</summary>
    public sealed class WaterAttackAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;
        public const float DefaultDuration = 1.65f;
        public const float AttackVolume = .52f;
        struct CachedClip { public AudioClip clip; public PcmStats stats; }
        readonly Dictionary<int, CachedClip> clips = new Dictionary<int, CachedClip>();
        static float[] originalPcm;

        [Serializable]
        public struct PcmStats
        {
            public int sampleRate, samples;
            public float duration, peak, rms, seamJump;
            public bool finite;
            public override string ToString() => "original water PCM samples=" + samples + " rate=" + sampleRate
                + " duration=" + duration.ToString("F3") + " peak=" + peak.ToString("F4")
                + " rms=" + rms.ToString("F4") + " finite=" + finite;
        }
        public PcmStats LastPcmStats { get; private set; }
        public static PcmStats DefaultPcmStats { get; private set; }
        /// <summary>Original mono samples for local WAV review; treat this shared cached array as read-only.</summary>
        public static float[] OriginalPcm
        {
            get
            {
                if (originalPcm == null)
                {
                    originalPcm = CreateWaterGunPcm(Mathf.RoundToInt(DefaultDuration * SampleRate));
                    DefaultPcmStats = AnalyzePcm(originalPcm);
                }
                return originalPcm;
            }
        }

        public static AudioSource Play(Transform parent, Vector3 source, float duration = DefaultDuration)
        {
            if (parent == null) return null;
            var owner = parent.GetComponentInParent<WaterAttackAudio>();
            if (owner == null) owner = parent.gameObject.AddComponent<WaterAttackAudio>();
            int samples = Mathf.RoundToInt(SafeDuration(duration) * SampleRate);
            CachedClip cached = owner.GetClip(samples); owner.LastPcmStats = cached.stats;
            var go = new GameObject("Pistolet à O · original liquid pressure audio");
            go.transform.SetParent(parent, false); go.transform.position = source;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.clip = cached.clip; audio.volume = AttackVolume;
            audio.spatialBlend = .50f; audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 2f; audio.maxDistance = 30f; audio.dopplerLevel = 0f; audio.priority = 75;
            if (Application.isPlaying) { audio.Play(); Destroy(go, cached.clip.length + .1f); }
            return audio;
        }

        CachedClip GetClip(int samples)
        {
            if (clips.TryGetValue(samples, out CachedClip cached)) return cached;
            float[] pcm = samples == Mathf.RoundToInt(DefaultDuration * SampleRate) ? OriginalPcm : CreateWaterGunPcm(samples);
            var clip = AudioClip.Create("Original Water Gun " + samples, pcm.Length, 1, SampleRate, false);
            clip.SetData(pcm, 0);
            cached = new CachedClip { clip = clip, stats = AnalyzePcm(pcm) }; clips.Add(samples, cached);
            return cached;
        }

        public static float[] CreateOriginalPcm(float duration = DefaultDuration)
        {
            int samples = Mathf.RoundToInt(SafeDuration(duration) * SampleRate);
            return samples == Mathf.RoundToInt(DefaultDuration * SampleRate) ? OriginalPcm : CreateWaterGunPcm(samples);
        }

        public static float[] CreateWaterGunPcm(int samples)
        {
            samples = Mathf.Clamp(samples, SampleRate / 2, SampleRate * 5);
            var pcm = new float[samples]; var noise = new Noise(0x2b49a16du);
            float seconds = samples / (float)SampleRate;
            float phaseStep = DefaultDuration / samples;
            float body = 0f, mid = 0f, air = 0f, dropPhase = 0f, dropEnvelope = 0f, dropFrequency = 1800f, dropAge = 1f;
            float nextDrop = .645f;
            float dropDecay = Mathf.Exp(-49f * phaseStep), dropFall = Mathf.Exp(-8.5f * phaseStep);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate, phase = t / seconds * DefaultDuration;
                float n = noise.Next();
                body += .045f * (n - body); mid += .16f * (n - mid); air += .38f * (n - air);

                // A small gulp gathers pressure, then one short squirt leaves the source.
                float charge = Smooth(phase / .025f) * (1f - Smooth((phase - .10f) / .055f));
                float launch = Smooth((phase - .155f) / .012f) * (1f - Smooth((phase - .225f) / .052f));
                float launchAge = Mathf.Max(0f, phase - .155f);
                float punch = Mathf.Sin(2f * Mathf.PI * (155f * launchAge + 350f * launchAge * launchAge))
                    * Mathf.Exp(-launchAge * 29f);
                float chargeBubbles = .055f * Mathf.Sin(2f * Mathf.PI * (390f * t + 1500f * t * t));
                float liquid = .92f * mid + .50f * body + .24f * (air - mid);

                // Detached flight loses energy and is quiet before the separate wet impact.
                float flightAge = Mathf.Max(0f, phase - .25f);
                float flight = Smooth((phase - .245f) / .025f) * Mathf.Exp(-flightAge * 6.5f)
                    * (1f - Smooth((phase - .44f) / .055f));
                float contactAge = Mathf.Max(0f, phase - .52f);
                float impact = Smooth((phase - .52f) / .006f) * Mathf.Exp(-contactAge * 25f);
                float splash = impact * (1.12f * mid + .72f * (air - mid)
                    + .24f * Mathf.Sin(2f * Mathf.PI * (172f * contactAge - 85f * contactAge * contactAge))
                        * Mathf.Exp(-contactAge * 21f));
                float falling = Smooth((phase - .55f) / .02f) * Mathf.Exp(-Mathf.Max(0f, phase - .55f) * 11f);

                // Individual falling drops continue after the short splash has decayed.
                if (phase >= nextDrop && phase < 1.26f)
                {
                    nextDrop = phase + .075f + .055f * (noise.Next() + 1f) * .5f;
                    dropEnvelope = (.055f + .075f * (noise.Next() + 1f) * .5f)
                        * Mathf.Exp(-Mathf.Max(0f, phase - .645f) * 1.9f);
                    dropFrequency = 1500f + 1300f * (noise.Next() + 1f) * .5f;
                    dropPhase = 0f; dropAge = 0f;
                }
                dropEnvelope *= dropDecay; dropFrequency *= dropFall; dropAge += phaseStep;
                dropPhase += 2f * Mathf.PI * dropFrequency / SampleRate;
                float droplet = dropEnvelope * Smooth(dropAge / .0025f) * (Mathf.Sin(dropPhase) + .23f * Mathf.Sin(dropPhase * 1.91f));
                float tailEnd = 1f - Smooth((phase - 1.26f) / .09f);
                float value = charge * (.30f * body + chargeBubbles)
                    + launch * (liquid + .20f * punch)
                    + flight * (.15f * (air - mid) + .035f * mid)
                    + splash + falling * (.14f * mid + .055f * body) + droplet * .78f;
                float fade = Smooth(t / .007f) * Smooth((seconds - t) / .08f);
                pcm[i] = .82f * (float)Math.Tanh(value * 1.5f) * fade * tailEnd;
            }
            return pcm;
        }
        public static PcmStats AnalyzePcm(float[] pcm)
        {
            if (pcm == null) return new PcmStats { sampleRate = SampleRate, finite = false };
            var stats = new PcmStats { sampleRate = SampleRate, samples = pcm.Length, duration = pcm.Length / (float)SampleRate, finite = true };
            double sum = 0;
            foreach (float value in pcm)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) { stats.finite = false; continue; }
                stats.peak = Mathf.Max(stats.peak, Mathf.Abs(value)); sum += value * value;
            }
            stats.rms = pcm.Length == 0 ? 0f : (float)Math.Sqrt(sum / pcm.Length);
            stats.seamJump = pcm.Length < 2 ? 0f : Mathf.Abs(pcm[0] - pcm[pcm.Length - 1]); return stats;
        }

        static float SafeDuration(float seconds) => float.IsNaN(seconds) || float.IsInfinity(seconds) ? DefaultDuration : Mathf.Clamp(seconds, .5f, 5f);
        static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
        struct Noise
        {
            uint state; public Noise(uint seed) { state = seed; }
            public float Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return (state & 0xffffffu) / 8388607.5f - 1f; }
        }
        void OnDestroy()
        {
            foreach (CachedClip cached in clips.Values)
                if (cached.clip != null) { if (Application.isPlaying) Destroy(cached.clip); else DestroyImmediate(cached.clip); }
            clips.Clear();
        }
    }
}
