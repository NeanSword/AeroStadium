using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Original mono plant foley: flexible wet vines (22), cutting leaf volleys (75).</summary>
    public sealed class GrassAttackAudio : MonoBehaviour
    {
        public const int SampleRate = 22050;
        struct CachedClip { public AudioClip clip; public PcmStats stats; }
        readonly Dictionary<long, CachedClip> clips = new Dictionary<long, CachedClip>();
        static float[] vinePcm, leafPcm;

        [Serializable]
        public struct PcmStats
        {
            public int sampleRate, samples;
            public float duration, peak, rms, seamJump;
            public bool finite;
            public override string ToString() => "original grass PCM samples=" + samples + " rate=" + sampleRate
                + " duration=" + duration.ToString("F3") + " peak=" + peak.ToString("F4")
                + " rms=" + rms.ToString("F4") + " seam=" + seamJump.ToString("F6") + " finite=" + finite;
        }
        public PcmStats LastPcmStats { get; private set; }

        public static float DefaultDuration(int moveId)
        {
            Check(moveId); return moveId == 22 ? 2.50f : 2.15f;
        }

        public static AudioSource Play(Transform parent, Vector3 source, int moveId, float duration)
        {
            Check(moveId);
            if (parent == null || !Finite(source.x) || !Finite(source.y) || !Finite(source.z)) return null;
            var owner = parent.GetComponentInParent<GrassAttackAudio>();
            if (owner == null) owner = parent.gameObject.AddComponent<GrassAttackAudio>();
            int samples = Mathf.RoundToInt(SafeDuration(duration, moveId) * SampleRate);
            long key = ((long)moveId << 32) | (uint)samples;
            if (!owner.clips.TryGetValue(key, out CachedClip cached))
            {
                float[] pcm = CreateOriginalPcm(moveId, samples / (float)SampleRate);
                var clip = AudioClip.Create("Original grass " + moveId + " " + samples, pcm.Length, 1, SampleRate, false);
                clip.SetData(pcm, 0);
                cached = new CachedClip { clip = clip, stats = AnalyzePcm(pcm) };
                owner.clips.Add(key, cached);
            }
            owner.LastPcmStats = cached.stats;
            var go = new GameObject(moveId == 22 ? "Fouet Lianes · original vine foley" : "Tranch'Herbe · original leaf foley");
            go.transform.SetParent(parent, false); go.transform.position = source;
            var audio = go.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.clip = cached.clip; audio.volume = moveId == 22 ? .56f : .49f;
            audio.spatialBlend = .5f; audio.rolloffMode = AudioRolloffMode.Linear;
            audio.minDistance = 2f; audio.maxDistance = 30f; audio.dopplerLevel = 0f; audio.priority = 75;
            if (Application.isPlaying) { audio.Play(); Destroy(go, cached.clip.length + .1f); }
            return audio;
        }

        /// <summary>Shared default buffers are read-only; no external recording or service is used.</summary>
        public static float[] CreateOriginalPcm(int moveId)
        {
            Check(moveId);
            if (moveId == 22) return vinePcm ?? (vinePcm = Synthesize(22, DefaultDuration(22)));
            return leafPcm ?? (leafPcm = Synthesize(75, DefaultDuration(75)));
        }

        public static float[] CreateOriginalPcm(int moveId, float duration)
        {
            Check(moveId); duration = SafeDuration(duration, moveId);
            return Mathf.Abs(duration - DefaultDuration(moveId)) < .0001f
                ? CreateOriginalPcm(moveId) : Synthesize(moveId, duration);
        }

        static float[] Synthesize(int moveId, float duration)
        {
            int samples = Mathf.RoundToInt(duration * SampleRate);
            var pcm = new float[samples]; var noise = new Noise(moveId == 22 ? 0x22c1a9e5u : 0x75eaf917u);
            float low = 0f, mid = 0f, high = 0f, dc = 0f, nominal = DefaultDuration(moveId);
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate, p = i / (float)samples * nominal, n = noise.Next();
                low += .035f * (n - low); mid += .19f * (n - mid); high += .60f * (n - high);
                float fiber = mid - low, edge = high - mid;
                float value = moveId == 22 ? Vine(p, low, fiber, edge) : Leaves(p, low, fiber, edge);
                dc += .0015f * (value - dc);
                float fade = Smooth(t / .008f) * Smooth((duration - t) / .075f);
                pcm[i] = .78f * (float)Math.Tanh((value - dc) * 1.5f) * fade;
            }
            pcm[0] = 0f; pcm[samples - 1] = 0f;
            return pcm;
        }

        static float Vine(float p,float low,float fiber,float edge)
        {
            float gather=Window(p,.03f,.45f,.10f);
            float value=gather*(.28f*fiber+.16f*low);
            float backswing=Window(p,.35f,.92f,.12f);
            value+=backswing*(.25f*fiber+.10f*Mathf.Sin(2*Mathf.PI*(102*p+28*p*p)));
            for(int vine=0;vine<2;vine++)
            {
                float crack=1.14f+vine*.045f,age=p-crack;
                float rush=Window(p,.82f+vine*.045f,crack+.015f,.045f);
                float acceleration=Smooth((p-.82f-vine*.045f)/.30f);
                value+=rush*(.22f+.6f*acceleration)*(fiber*.55f+edge*.6f);
                if(age>=0&&age<.18f)
                {
                    float snap=Smooth(age/.001f)*Mathf.Exp(-age*75);
                    value+=snap*(1.75f*edge+.70f*fiber+.18f*Mathf.Sin(2*Mathf.PI*1320*age));
                }
            }
            value+=Window(p,1.24f,1.60f,.07f)*(.22f*fiber+.07f*low);
            value+=Window(p,1.65f,2.38f,.12f)*(.18f*fiber+.09f*low)*(1-Smooth((p-1.70f)/.68f));
            return value;
        }

        static float Leaves(float p, float low, float fiber, float edge)
        {
            // A fibrous source rustle supports separate curved passes and small clipped cuts.
            float rustle = Window(p, .015f, .57f, .06f);
            float value = rustle * (.29f * fiber + .08f * low)
                * (.6f + .4f * Mathf.Sin(2f * Mathf.PI * 32f * p));
            for (int leaf = 0; leaf < 13; leaf++)
            {
                float born = .14f + leaf * .026f, age = p - born;
                float variant = .5f + .5f * Mathf.Sin(leaf * 2.399963f);
                float flight = .48f + .09f * variant;
                if (age >= 0f && age < flight)
                {
                    float pass = Window(age, .015f, flight - .015f, .065f);
                    float flutter = .55f + .45f * Mathf.Sin(2f * Mathf.PI * (24f + 11f * variant) * age + leaf);
                    float near = Mathf.Exp(-Mathf.Pow((age - flight * .64f) / .075f, 2f));
                    float angle = 2f * Mathf.PI * ((590f + 180f * variant) * age
                        + 690f * age * age - 960f * age * age * age) + leaf * 1.9f;
                    value += pass * (.078f * fiber * flutter + .045f * edge * near
                        + .018f * Mathf.Sin(angle) * flutter * near);
                }
                float cutAge = age - flight;
                if (cutAge < 0f || cutAge > .21f) continue;
                float cut = Smooth(cutAge / .0018f) * Mathf.Exp(-cutAge * (46f + 12f * variant));
                float tick = Mathf.Sin(2f * Mathf.PI * (1320f + 280f * variant) * cutAge);
                float clipped = Mathf.Clamp(.75f * fiber + .49f * edge + .15f * tick, -.30f, .30f);
                value += cut * clipped;
                float settle = Window(cutAge, .045f, .20f, .025f) * Mathf.Exp(-cutAge * 18f);
                value += settle * .065f * fiber * (.5f + .5f * Mathf.Sin(2f * Mathf.PI * 43f * cutAge));
            }
            return value;
        }

        public static PcmStats AnalyzePcm(float[] pcm)
        {
            var stats = new PcmStats { sampleRate = SampleRate, finite = pcm != null };
            if (pcm == null) return stats;
            stats.samples = pcm.Length; stats.duration = pcm.Length / (float)SampleRate; double sum = 0;
            foreach (float value in pcm)
            {
                if (!Finite(value)) { stats.finite = false; continue; }
                stats.peak = Mathf.Max(stats.peak, Mathf.Abs(value)); sum += value * value;
            }
            stats.rms = pcm.Length == 0 ? 0f : (float)Math.Sqrt(sum / pcm.Length);
            stats.seamJump = pcm.Length < 2 ? 0f : Mathf.Abs(pcm[0] - pcm[pcm.Length - 1]); return stats;
        }

        static float Window(float t, float start, float end, float feather) => Smooth((t - start) / feather) * Smooth((end - t) / feather);
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        static float SafeDuration(float d, int moveId) => Finite(d) ? Mathf.Clamp(d, .5f, 5f) : DefaultDuration(moveId);
        static void Check(int moveId) { if (moveId != 22 && moveId != 75) throw new ArgumentOutOfRangeException(nameof(moveId)); }
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
