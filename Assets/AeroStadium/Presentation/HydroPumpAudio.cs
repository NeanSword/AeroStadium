using System;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Original deterministic pressure rumble, turbulent roar and heavy splash synthesis.</summary>
    public sealed class HydroPumpAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;
        public const float DefaultDuration = 2.2f, AttackVolume = .60f;
        static float[] originalPcm;
        AudioClip ownedClip;
        [Serializable] public struct PcmStats
        {
            public int sampleRate, samples; public float duration, peak, rms, seamJump; public bool finite;
            public override string ToString() => "original Hydro Pump PCM samples=" + samples + " peak=" + peak.ToString("F4") + " rms=" + rms.ToString("F4") + " finite=" + finite;
        }
        public PcmStats LastPcmStats { get; private set; }
        public static PcmStats DefaultPcmStats { get; private set; }
        /// <summary>Shared original mono PCM; callers must treat the cached samples as read-only.</summary>
        public static float[] OriginalPcm
        {
            get { if (originalPcm == null) { originalPcm = Synthesize(DefaultDuration); DefaultPcmStats = AnalyzePcm(originalPcm); } return originalPcm; }
        }
        public static AudioSource Play(Transform parent, Vector3 source, float duration = DefaultDuration)
        {
            if (!Safe(source.x) || !Safe(source.y) || !Safe(source.z)) return null;
            float seconds = Safe(duration) ? Mathf.Clamp(duration, .5f, 5f) : DefaultDuration;
            var go = new GameObject("Hydrocanon · original pressure roar audio"); go.transform.SetParent(parent, false); go.transform.position = source;
            var owner = go.AddComponent<HydroPumpAudio>(); float[] pcm = CreateOriginalPcm(seconds); owner.LastPcmStats = AnalyzePcm(pcm);
            owner.ownedClip = AudioClip.Create("Original Hydro Pump pressure and splash", pcm.Length, 1, SampleRate, false); owner.ownedClip.SetData(pcm, 0);
            var audio = go.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.clip = owner.ownedClip; audio.volume = AttackVolume;
            audio.spatialBlend = .50f; audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 2; audio.maxDistance = 32; audio.dopplerLevel = 0; audio.priority = 68;
            if (Application.isPlaying) { audio.Play(); Destroy(go, seconds + .12f); } return audio;
        }
        public static float[] CreateOriginalPcm(float duration = DefaultDuration)
        {
            duration = Safe(duration) ? Mathf.Clamp(duration, .5f, 5f) : DefaultDuration;
            return Mathf.RoundToInt(duration * SampleRate) == Mathf.RoundToInt(DefaultDuration * SampleRate) ? OriginalPcm : Synthesize(duration);
        }
        static float[] Synthesize(float seconds)
        {
            var pcm = new float[Mathf.RoundToInt(seconds * SampleRate)]; uint seed = 0x56a731d9u;
            float bass = 0, mid = 0, air = 0, bubblePhase = 0, bubble = 0, frequency = 600; int nextBubble = 0;
            for (int i = 0; i < pcm.Length; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                float n = (seed & 0xffffffu) / 8388607.5f - 1, t = i / (float)SampleRate, phase = t / seconds * DefaultDuration;
                bass += .018f * (n - bass); mid += .12f * (n - mid); air += .58f * (n - air);
                float charge = Smooth(phase / .08f) * (1 - Smooth((phase - .20f) / .07f));
                float roar = Smooth((phase - .20f) / .035f) * (1 - Smooth((phase - 1.68f) / .28f));
                float blast = phase >= .20f ? Mathf.Exp(-(phase - .20f) * 26) : 0;
                float splash = phase >= .65f ? Mathf.Exp(-(phase - .65f) * 13) : 0;
                float tail = Smooth((phase - 1.70f) / .08f) * Mathf.Exp(-Mathf.Max(0, phase - 1.70f) * 5);
                if (i >= nextBubble)
                {
                    nextBubble = i + SampleRate / 30 + (int)(seed % (SampleRate / 35));
                    bubble = .08f + (seed & 255) / 255f * .14f; frequency = 480 + (seed >> 8 & 2047) * .75f;
                }
                bubble *= .996f; frequency *= .99965f; bubblePhase += 2 * Mathf.PI * frequency / SampleRate;
                float droplets = bubble * Mathf.Sin(bubblePhase), churn = .86f + .10f * Mathf.Sin(2 * Mathf.PI * 14 * t) + .04f * Mathf.Sin(2 * Mathf.PI * 37 * t);
                float pressure = Mathf.Sin(2 * Mathf.PI * (80 * t + 95 * t * t)) * .075f;
                float value = charge * (bass * 1.1f + pressure) + blast * (bass * 2.4f + mid * 1.2f)
                    + roar * (bass * 1.65f + mid * 1.35f + (air - mid) * .46f) * churn
                    + splash * (mid * 1.65f + (air - mid) * .90f + bass * 1.2f)
                    + tail * (droplets * 1.1f + mid * .42f + bass * .70f);
                pcm[i] = .88f * (float)Math.Tanh(value * 1.4f) * Smooth(t / .006f) * Smooth((seconds - t) / .10f);
            }
            return pcm;
        }
        public static PcmStats AnalyzePcm(float[] pcm)
        {
            if (pcm == null) return new PcmStats { sampleRate = SampleRate, finite = false };
            var stats = new PcmStats { sampleRate = SampleRate, samples = pcm.Length, duration = pcm.Length / (float)SampleRate, finite = true }; double sum = 0;
            foreach (float v in pcm) { if (!Safe(v)) { stats.finite = false; continue; } stats.peak = Mathf.Max(stats.peak, Mathf.Abs(v)); sum += v * v; }
            stats.rms = pcm.Length == 0 ? 0 : (float)Math.Sqrt(sum / pcm.Length); stats.seamJump = pcm.Length < 2 ? 0 : Mathf.Abs(pcm[0] - pcm[pcm.Length - 1]); return stats;
        }
        static bool Safe(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3 - 2 * value); }
        void OnDestroy() { if (ownedClip != null) { if (Application.isPlaying) Destroy(ownedClip); else DestroyImmediate(ownedClip); } }
    }
}
