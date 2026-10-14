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
            float body = 0f, mid = 0f, air = 0f, dropPhase = 0f, dropEnvelope = 0f, dropFrequency = 1800f;
            int nextDrop = 0;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate, phase = t / seconds * DefaultDuration;
                float n = noise.Next();
                body += .045f * (n - body); mid += .16f * (n - mid); air += .38f * (n - air);
                float charge = Smooth(phase / .045f) * (1f - Smooth((phase - .13f) / .12f));
                float jet = Smooth((phase - .14f) / .10f) * (1f - Smooth((phase - 1.24f) / .24f));
                float contactAge = Mathf.Max(0f, phase - .52f);
                float impact = Smooth((phase - .52f) / .01f) * Mathf.Exp(-contactAge * 18f);
                float tail = Smooth((phase - 1.25f) / .04f) * Mathf.Exp(-Mathf.Max(0f, phase - 1.25f) * 9f);
                if (i >= nextDrop)
                {
                    nextDrop = i + Mathf.RoundToInt(SampleRate * (.024f + .035f * (noise.Next() + 1f) * .5f));
                    dropEnvelope = .11f + .15f * (noise.Next() + 1f) * .5f;
                    dropFrequency = 1350f + 1350f * (noise.Next() + 1f) * .5f;
                }
                dropEnvelope *= .994f; dropFrequency *= .9997f;
                dropPhase += 2f * Mathf.PI * dropFrequency / SampleRate;
                float wetPulse = .87f + .08f * Mathf.Sin(2f * Mathf.PI * 19f * t) + .05f * Mathf.Sin(2f * Mathf.PI * 31f * t);
                float liquid = .88f * mid + .42f * body + .30f * (air - mid);
                float droplet = dropEnvelope * (Mathf.Sin(dropPhase) + .23f * Mathf.Sin(dropPhase * 1.91f));
                float chargeBubbles = Mathf.Sin(2f * Mathf.PI * (540f * t + 1600f * t * t)) * .09f;
                float splash = impact * (1.05f * mid + .65f * (air - mid) + .12f * Mathf.Sin(2f * Mathf.PI * 180f * contactAge) * Mathf.Exp(-contactAge * 23f));
                float value = charge * (.32f * body + chargeBubbles)
                    + jet * (liquid * wetPulse + droplet * .67f) + splash + tail * (mid * .25f + droplet * .9f);
                float fade = Smooth(t / .007f) * Smooth((seconds - t) / .08f);
                pcm[i] = .82f * (float)Math.Tanh(value * 1.5f) * fade;
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
