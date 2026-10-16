using System;
using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>One original introduction, followed by a sample-aligned, later musical loop.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleMusicAudio : MonoBehaviour
    {
        public const string IntroResource = "Audio/AeroStadiumBattleIntro";
        public const string LoopResource = "Audio/AeroStadiumBattleLoop";
        public const float MusicVolume = .68f;
        public const float FadeOutSeconds = .65f;

        AudioSource introSource, loopSource;
        Coroutine pendingEntry;
        bool battleActive, scheduled, stopReported;
        int lastIntroSample = -1, lastLoopSample = -1;
        float gain;

        public bool MusicReady => Ready(introSource) && Ready(loopSource);
        public bool MusicPlaying => (introSource != null && introSource.isPlaying)
            || (loopSource != null && loopSource.isPlaying);
        public bool BattleActive => battleActive;
        public bool EntryPending => pendingEntry != null;
        public bool IntroPlaybackVerified { get; private set; }
        public bool LoopPlaybackVerified { get; private set; }
        public bool FadeOutCompleted { get; private set; }
        public int EntryCount { get; private set; }
        public int CompletedLoops { get; private set; }
        public int ScheduledIntroSample { get; private set; }
        public int IntroSamples => introSource != null && introSource.clip != null ? introSource.clip.samples : 0;
        public int LoopSamples => loopSource != null && loopSource.clip != null ? loopSource.clip.samples : 0;
        public int IntroFrequency => introSource != null && introSource.clip != null ? introSource.clip.frequency : 0;
        public int LoopFrequency => loopSource != null && loopSource.clip != null ? loopSource.clip.frequency : 0;
        public double IntroDuration => IntroFrequency > 0 ? (double)IntroSamples / IntroFrequency : 0;
        public double LoopDuration => LoopFrequency > 0 ? (double)LoopSamples / LoopFrequency : 0;
        public double IntroScheduledDsp { get; private set; }
        public double LoopScheduledDsp { get; private set; }
        public double ScheduledJoinError => LoopScheduledDsp - (IntroScheduledDsp + IntroDuration);
        public float CurrentVolume => MusicVolume * gain;
        public string LoadFailure { get; private set; } = "";
        public string Diagnostics => " battleMusicReady=" + MusicReady + " battleMusicPlaying=" + MusicPlaying
            + " battleMusicActive=" + battleActive + " battleMusicEntries=" + EntryCount
            + " battleMusicIntroVerified=" + IntroPlaybackVerified + " battleMusicLoopVerified=" + LoopPlaybackVerified
            + " battleMusicLoops=" + CompletedLoops + " battleMusicStopped=" + FadeOutCompleted
            + " battleMusicJoinError=" + ScheduledJoinError.ToString("G17")
            + " battleMusicVolume=" + CurrentVolume.ToString("F3");

        /// <summary>Called on the front end so the complete first note is ready before battle entry.</summary>
        public void Prepare()
        {
            EnsureSources();
            LoadFailure = "";
            Load(introSource, IntroResource);
            Load(loopSource, LoopResource);
        }

        public void EnterBattle()
        {
            // HUD rebuilds and turn changes must never seek or restart the soundtrack.
            if (battleActive) return;
            CancelPending();
            StopSources();
            battleActive = true;
            stopReported = FadeOutCompleted = IntroPlaybackVerified = LoopPlaybackVerified = false;
            CompletedLoops = 0;
            lastIntroSample = lastLoopSample = -1;
            gain = 1f;
            Prepare();
            if (MusicReady) Schedule();
            else pendingEntry = StartCoroutine(WaitAndSchedule());
        }

        public void LeaveBattle()
        {
            if (!battleActive && !MusicPlaying && pendingEntry == null) return;
            battleActive = false;
            CancelPending();
            // Cancel future loop starts immediately, then fade only the currently audible source.
            if (scheduled && AudioSettings.dspTime < LoopScheduledDsp && loopSource != null) loopSource.Stop();
        }

        IEnumerator WaitAndSchedule()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (battleActive && !MusicReady && Time.realtimeSinceStartup < deadline && LoadFailure.Length == 0)
                yield return null;
            pendingEntry = null;
            if (!battleActive) yield break;
            if (MusicReady) Schedule();
            else
            {
                LoadFailure = LoadFailure.Length > 0 ? LoadFailure : "Audio data was not ready within five seconds.";
                battleActive = false; StopSources(); FadeOutCompleted = true;
                Debug.LogWarning("[battle-audio-unavailable] " + LoadFailure);
            }
        }

        void Schedule()
        {
            if (!battleActive || !MusicReady) return;
            AudioSettings.GetDSPBufferSize(out int bufferFrames, out _);
            double lead = Math.Max(.05, Math.Min(.15, bufferFrames * 2d / Math.Max(1, AudioSettings.outputSampleRate)));
            IntroScheduledDsp = AudioSettings.dspTime + lead;
            LoopScheduledDsp = IntroScheduledDsp + IntroDuration;
            introSource.timeSamples = loopSource.timeSamples = 0;
            ScheduledIntroSample = introSource.timeSamples;
            introSource.volume = loopSource.volume = MusicVolume;
            introSource.PlayScheduled(IntroScheduledDsp);
            introSource.SetScheduledEndTime(LoopScheduledDsp);
            loopSource.PlayScheduled(LoopScheduledDsp);
            scheduled = true; EntryCount++;
            Debug.Log("[battle-audio-scheduled] entry=" + EntryCount + " introSample=" + ScheduledIntroSample
                + " introFrames=" + IntroSamples + " loopFrames=" + LoopSamples
                + " introHz=" + IntroFrequency + " loopHz=" + LoopFrequency
                + " introDsp=" + IntroScheduledDsp.ToString("G17") + " loopDsp=" + LoopScheduledDsp.ToString("G17")
                + " gain=" + MusicVolume + " joinError=" + ScheduledJoinError.ToString("G17"));
        }

        void Update()
        {
            if (introSource == null || loopSource == null) return;
            if (!battleActive) gain = Mathf.MoveTowards(gain, 0f, Time.unscaledDeltaTime / FadeOutSeconds);
            introSource.volume = loopSource.volume = MusicVolume * gain;
            double now = AudioSettings.dspTime;
            if (battleActive && scheduled)
            {
                if (now >= IntroScheduledDsp && now < LoopScheduledDsp && introSource.isPlaying)
                {
                    int sample = introSource.timeSamples;
                    if (lastIntroSample >= 0 && sample > lastIntroSample && !IntroPlaybackVerified)
                    {
                        IntroPlaybackVerified = true;
                        Debug.Log("[battle-audio-intro] entry=" + EntryCount + " sample=" + sample);
                    }
                    lastIntroSample = sample;
                }
                if (now >= LoopScheduledDsp && loopSource.isPlaying)
                {
                    int sample = loopSource.timeSamples;
                    if (lastLoopSample >= 0 && sample != lastLoopSample && !LoopPlaybackVerified)
                    {
                        LoopPlaybackVerified = true;
                        Debug.Log("[battle-audio-loop-entered] entry=" + EntryCount + " sample=" + sample);
                    }
                    int loops = LoopDuration > 0 ? (int)Math.Floor((now - LoopScheduledDsp) / LoopDuration) : 0;
                    if (loops > CompletedLoops)
                    {
                        CompletedLoops = loops;
                        Debug.Log("[battle-audio-loop] entry=" + EntryCount + " completed=" + CompletedLoops + " sample=" + sample);
                    }
                    lastLoopSample = sample;
                }
            }
            else if (!battleActive && gain <= 0f && !stopReported)
            {
                StopSources(); FadeOutCompleted = true; stopReported = true;
                Debug.Log("[battle-audio-stopped] fadeSeconds=" + FadeOutSeconds + Diagnostics);
            }
        }

        static bool Ready(AudioSource source) => source != null && source.clip != null
            && source.clip.loadState == AudioDataLoadState.Loaded;

        void Load(AudioSource source, string resource)
        {
            if (source.clip == null) source.clip = Resources.Load<AudioClip>(resource);
            if (source.clip == null) { LoadFailure = "Missing Resources/" + resource; return; }
            if (source.clip.loadState == AudioDataLoadState.Unloaded) source.clip.LoadAudioData();
            if (source.clip.loadState == AudioDataLoadState.Failed) LoadFailure = "Failed to load Resources/" + resource;
        }

        void EnsureSources()
        {
            if (introSource == null) introSource = CreateSource(false);
            if (loopSource == null) loopSource = CreateSource(true);
        }

        AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0f;
            source.priority = 64; source.dopplerLevel = 0f; source.ignoreListenerPause = true;
            source.volume = MusicVolume;
            return source;
        }

        void CancelPending()
        {
            if (pendingEntry == null) return;
            StopCoroutine(pendingEntry); pendingEntry = null;
        }

        void StopSources()
        {
            if (introSource != null) introSource.Stop();
            if (loopSource != null) loopSource.Stop();
            scheduled = false;
        }

        void OnDisable()
        {
            battleActive = false; CancelPending(); StopSources(); gain = 0f;
            FadeOutCompleted = true;
        }
    }
}
