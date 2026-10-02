using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Continuous title and mode-menu music, independent of gameplay time.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleScreenAudio : MonoBehaviour
    {
        const string MusicResource = "Audio/AeroStadiumTitleTheme";
        const float MusicVolume = .68f;
        const float CryVolume = .24f;
        const float FadeInSeconds = 1.2f;
        const float FadeOutSeconds = .9f;
        static readonly string[] CryResources = { "Audio/Cries/pikachu", "Audio/Cries/umbreon", "Audio/Cries/lucario" };
        static readonly float[] CryIntervals = { 24f, 27f, 30f };

        readonly AudioClip[] cryClips = new AudioClip[CryResources.Length];
        AudioSource musicSource, crySource;
        AudioListener listener;
        bool resourcesLoaded, titleActive, enteredTitle, stopReported;
        float fade, musicDuck = 1f, duckUntil, nextCryAt;
        int nextCry, previousSample = -1;

        public bool MusicReady => musicSource != null && musicSource.clip != null
            && musicSource.clip.loadState == AudioDataLoadState.Loaded;
        public bool ListenerReady => listener != null && listener.isActiveAndEnabled;
        public bool LoopEnabled => musicSource != null && musicSource.loop;
        public bool MusicPlaying => musicSource != null && musicSource.isPlaying;
        public int PlaybackSample => musicSource != null ? musicSource.timeSamples : 0;
        public bool PlaybackVerified { get; private set; }
        public bool FadeOutCompleted { get; private set; }
        public int CompletedLoops { get; private set; }
        public int CriesPlayed { get; private set; }
        public int ReadyCries
        {
            get
            {
                int count = 0;
                foreach (AudioClip clip in cryClips)
                    if (clip != null && clip.loadState == AudioDataLoadState.Loaded) count++;
                return count;
            }
        }

        public string Diagnostics => " musicReady=" + MusicReady + " musicPlaying=" + MusicPlaying
            + " musicLoop=" + LoopEnabled + " musicPlaybackVerified=" + PlaybackVerified
            + " musicLoops=" + CompletedLoops + " cryClips=" + ReadyCries + " criesPlayed=" + CriesPlayed
            + " cryPlaying=" + (crySource != null && crySource.isPlaying)
            + " musicVolume=" + (musicSource != null ? musicSource.volume.ToString("F3") : "0")
            + " audioListener=" + ListenerReady + " titleAudioStopped=" + FadeOutCompleted;

        public void EnterTitle()
        {
            EnsureSources();
            LoadResources();
            titleActive = enteredTitle = true;
            FadeOutCompleted = stopReported = PlaybackVerified = false;
            CompletedLoops = CriesPlayed = nextCry = 0;
            previousSample = -1;
            fade = 0f; musicDuck = 1f; duckUntil = 0f;
            nextCryAt = Time.unscaledTime + 14f;
            musicSource.volume = crySource.volume = 0f;
            crySource.Stop();
            musicSource.Stop();
            if (musicSource.clip != null) musicSource.Play();
            Debug.Log("[title-audio-ready] resource=" + MusicResource
                + " seconds=" + (musicSource.clip != null ? musicSource.clip.length.ToString("F2") : "0") + Diagnostics);
        }

        public void LeaveTitle()
        {
            titleActive = false;
            nextCryAt = float.PositiveInfinity;
        }

        public void ContinueFrontEnd()
        {
            if (enteredTitle && MusicPlaying)
            {
                titleActive = true;
                FadeOutCompleted = stopReported = false;
                if (float.IsPositiveInfinity(nextCryAt)) nextCryAt = Time.unscaledTime + 14f;
                return;
            }
            EnterTitle();
        }

        void Update()
        {
            if (musicSource == null) return;
            float delta = Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, titleActive ? 1f : 0f,
                delta / (titleActive ? FadeInSeconds : FadeOutSeconds));
            float duckTarget = titleActive && Time.unscaledTime < duckUntil ? .78f : 1f;
            musicDuck = Mathf.MoveTowards(musicDuck, duckTarget, delta * (duckTarget < musicDuck ? 2f : .7f));
            float gain = Mathf.SmoothStep(0f, 1f, fade);
            musicSource.volume = MusicVolume * gain * musicDuck;
            crySource.volume = CryVolume * gain;

            if (titleActive && MusicPlaying)
            {
                int sample = musicSource.timeSamples;
                if (previousSample >= 0 && sample != previousSample)
                {
                    if (!PlaybackVerified)
                    {
                        PlaybackVerified = true;
                        Debug.Log("[title-audio-playback] sample=" + sample + Diagnostics);
                    }
                    if (sample < previousSample)
                    {
                        CompletedLoops++;
                        Debug.Log("[title-audio-loop] loop=" + CompletedLoops);
                    }
                }
                previousSample = sample;
                if (Time.unscaledTime >= nextCryAt && !crySource.isPlaying) PlayNextCry();
            }
            else if (!titleActive && fade <= 0f && enteredTitle && !stopReported)
            {
                musicSource.Stop(); crySource.Stop();
                FadeOutCompleted = !musicSource.isPlaying && !crySource.isPlaying;
                stopReported = true;
                Debug.Log("[title-audio-stopped] fadeSeconds=" + FadeOutSeconds + Diagnostics);
            }
        }

        void PlayNextCry()
        {
            int start = nextCry;
            for (int offset = 0; offset < cryClips.Length; offset++)
            {
                int index = (start + offset) % cryClips.Length;
                AudioClip clip = cryClips[index];
                if (clip == null || clip.loadState != AudioDataLoadState.Loaded) continue;
                crySource.panStereo = index == 0 ? -.12f : index == 2 ? .12f : 0f;
                crySource.PlayOneShot(clip);
                duckUntil = Time.unscaledTime + clip.length + .35f;
                nextCry = (index + 1) % cryClips.Length;
                nextCryAt = Time.unscaledTime + CryIntervals[index];
                CriesPlayed++;
                Debug.Log("[title-audio-cry] resource=" + CryResources[index] + " count=" + CriesPlayed);
                return;
            }
            nextCryAt = Time.unscaledTime + 30f;
        }

        void EnsureSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false; musicSource.loop = true;
                musicSource.spatialBlend = 0f; musicSource.priority = 64;
                musicSource.dopplerLevel = 0f; musicSource.ignoreListenerPause = true;
                crySource = gameObject.AddComponent<AudioSource>();
                crySource.playOnAwake = false; crySource.loop = false;
                crySource.spatialBlend = 0f; crySource.priority = 96;
                crySource.dopplerLevel = 0f; crySource.ignoreListenerPause = true;
            }
            foreach (AudioListener candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled) continue;
                listener = candidate;
                return;
            }
            listener = gameObject.AddComponent<AudioListener>();
        }

        void LoadResources()
        {
            if (resourcesLoaded) return;
            resourcesLoaded = true;
            musicSource.clip = Resources.Load<AudioClip>(MusicResource);
            if (musicSource.clip == null)
                Debug.LogError("Title music missing: Resources/" + MusicResource + ".wav");
            else musicSource.clip.LoadAudioData();
            for (int i = 0; i < CryResources.Length; i++)
            {
                cryClips[i] = Resources.Load<AudioClip>(CryResources[i]);
                if (cryClips[i] != null) cryClips[i].LoadAudioData();
                else Debug.LogWarning("Optional title cry missing: Resources/" + CryResources[i]);
            }
        }

        void OnDisable()
        {
            titleActive = false;
            if (musicSource != null) musicSource.Stop();
            if (crySource != null) crySource.Stop();
        }
    }
}
