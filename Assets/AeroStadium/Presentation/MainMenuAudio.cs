using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Menu soundtrack with a continuous PCM loop and unscaled fades.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuAudio : MonoBehaviour
    {
        const string MusicResource = "Audio/AeroStadiumMenuTheme";
        const float MusicVolume = .68f;
        const float FadeInSeconds = 1.2f;
        const float FadeOutSeconds = .9f;

        AudioSource musicSource;
        AudioListener listener;
        bool resourcesLoaded, menuActive, enteredMenu, stopReported;
        float fade;
        int previousSample = -1;

        public bool MusicReady => musicSource != null && musicSource.clip != null
            && musicSource.clip.loadState == AudioDataLoadState.Loaded;
        public bool ListenerReady => listener != null && listener.isActiveAndEnabled;
        public bool LoopEnabled => musicSource != null && musicSource.loop;
        public bool MusicPlaying => musicSource != null && musicSource.isPlaying;
        public int PlaybackSample => musicSource != null ? musicSource.timeSamples : 0;
        public bool PlaybackVerified { get; private set; }
        public bool FadeOutCompleted { get; private set; }
        public int CompletedLoops { get; private set; }

        public string Diagnostics => " menuMusicReady=" + MusicReady + " menuMusicPlaying=" + MusicPlaying
            + " menuMusicLoop=" + LoopEnabled + " menuMusicPlaybackVerified=" + PlaybackVerified
            + " menuMusicLoops=" + CompletedLoops + " menuAudioListener=" + ListenerReady
            + " menuMusicVolume=" + (musicSource != null ? musicSource.volume.ToString("F3") : "0")
            + " menuAudioStopped=" + FadeOutCompleted;

        public void EnterMenu()
        {
            EnsureSource();
            LoadResource();
            menuActive = true;
            FadeOutCompleted = stopReported = false;
            // A quick return during the outgoing fade keeps its existing phase.
            if (enteredMenu && MusicPlaying) return;
            enteredMenu = true;
            PlaybackVerified = false;
            CompletedLoops = 0;
            previousSample = -1;
            fade = 0f;
            musicSource.volume = 0f;
            musicSource.Stop();
            if (musicSource.clip != null) musicSource.Play();
            Debug.Log("[menu-audio-ready] resource=" + MusicResource
                + " seconds=" + (musicSource.clip != null ? musicSource.clip.length.ToString("F2") : "0") + Diagnostics);
        }

        public void LeaveMenu()
        {
            menuActive = false;
        }

        void Update()
        {
            if (musicSource == null) return;
            fade = Mathf.MoveTowards(fade, menuActive ? 1f : 0f,
                Time.unscaledDeltaTime / (menuActive ? FadeInSeconds : FadeOutSeconds));
            musicSource.volume = MusicVolume * Mathf.SmoothStep(0f, 1f, fade);
            if (menuActive && MusicPlaying)
            {
                int sample = musicSource.timeSamples;
                if (previousSample >= 0 && sample != previousSample)
                {
                    if (!PlaybackVerified)
                    {
                        PlaybackVerified = true;
                        Debug.Log("[menu-audio-playback] sample=" + sample + Diagnostics);
                    }
                    if (sample < previousSample)
                    {
                        CompletedLoops++;
                        Debug.Log("[menu-audio-loop] loop=" + CompletedLoops);
                    }
                }
                previousSample = sample;
            }
            else if (!menuActive && fade <= 0f && enteredMenu && !stopReported)
            {
                musicSource.Stop();
                FadeOutCompleted = !MusicPlaying;
                stopReported = true;
                Debug.Log("[menu-audio-stopped] fadeSeconds=" + FadeOutSeconds + Diagnostics);
            }
        }

        void EnsureSource()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
                musicSource.spatialBlend = 0f;
                musicSource.priority = 64;
                musicSource.dopplerLevel = 0f;
                musicSource.ignoreListenerPause = true;
            }
            foreach (AudioListener candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            {
                if (!candidate.isActiveAndEnabled) continue;
                listener = candidate;
                return;
            }
            listener = null;
            Debug.LogError("Main menu music has no active AudioListener.");
        }

        void LoadResource()
        {
            if (resourcesLoaded) return;
            resourcesLoaded = true;
            musicSource.clip = Resources.Load<AudioClip>(MusicResource);
            if (musicSource.clip == null)
                Debug.LogError("Main menu music missing: Resources/" + MusicResource + ".wav");
            else musicSource.clip.LoadAudioData();
        }

        void OnDisable()
        {
            menuActive = false;
            if (musicSource != null) musicSource.Stop();
        }
    }
}
