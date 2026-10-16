using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class GameBootstrap
    {
        BattleMusicAudio battleAudio;
        bool battleMusicTest, battleMusicTestCompleted, battleMusicChecksPassed = true;
        [Serializable] sealed class BattleMusicReport
        {
            public string scope = "Actual GameBootstrap title/menu/team selection/battle routes; DSP scheduling and source playback telemetry. No acoustic-output or device-gap measurement.";
            public bool passed, realEntry, introPlayback, constantInitialGain, sampleZero, exactScheduledJoin;
            public bool loopPlayback, twoLoops, turnContinuity, pauseContinuity, exitedAndStopped, menuRestored, reentryFromZero, disableStops;
            public int introFrames, loopFrames, introHz, loopHz, loops, entries;
            public double introSeconds, loopSeconds, joinError;
        }
        BattleMusicReport battleMusicReport;

        void MusicCheck(bool value, string name)
        {
            battleMusicChecksPassed &= value;
            if (value) Debug.Log("[battle-music-check] " + name + " passed=True");
            else Debug.LogError("[battle-music-check] " + name + " passed=False");
        }

        IEnumerator TestBattleMusic()
        {
            battleMusicReport = new BattleMusicReport();
            yield return new WaitForSecondsRealtime(1.5f);
            if (titleStartButton == null) { MusicCheck(false,"title available"); SaveMusicReport(); yield break; }
            titleStartButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.5f);
            if (mainMenuView == null) { MusicCheck(false,"menu available"); SaveMusicReport(); yield break; }
            mainMenuView.Buttons[0].onClick.Invoke();
            yield return new WaitForSecondsRealtime(.4f);
            if (selectionView == null) { MusicCheck(false,"solo selection available"); SaveMusicReport(); yield break; }
            foreach (int id in new[] { 6, 9, 3, 25, 94, 149 }) selectionView.TryAdd(id);
            selectionView.StartButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            var audio = battleAudio;
            battleMusicReport.realEntry = screen == ScreenMode.Battle && audio != null && audio.MusicReady && audio.EntryCount == 1;
            MusicCheck(battleMusicReport.realEntry,"real selection launches battle soundtrack");
            if (!battleMusicReport.realEntry) { SaveMusicReport(); yield break; }
            battleMusicReport.introFrames = audio.IntroSamples; battleMusicReport.loopFrames = audio.LoopSamples;
            battleMusicReport.introHz = audio.IntroFrequency; battleMusicReport.loopHz = audio.LoopFrequency;
            battleMusicReport.introSeconds = audio.IntroDuration; battleMusicReport.loopSeconds = audio.LoopDuration;
            battleMusicReport.joinError = audio.ScheduledJoinError;
            battleMusicReport.sampleZero = audio.ScheduledIntroSample == 0;
            battleMusicReport.constantInitialGain = Mathf.Abs(audio.CurrentVolume - BattleMusicAudio.MusicVolume) < .00001f;
            battleMusicReport.exactScheduledJoin = Math.Abs(audio.ScheduledJoinError) < 1e-9;
            MusicCheck(battleMusicReport.sampleZero && battleMusicReport.constantInitialGain && battleMusicReport.exactScheduledJoin,
                "intro sample zero, full configured volume and exact sample-duration DSP join");
            double firstStart = audio.IntroScheduledDsp;
            yield return new WaitForSecondsRealtime(2f);
            battleMusicReport.introPlayback = audio.IntroPlaybackVerified;
            MusicCheck(battleMusicReport.introPlayback,"original introduction plays");
            yield return PlayTurn(battle.ChooseAi(0));
            battleMusicReport.turnContinuity = audio.EntryCount == 1 && audio.IntroScheduledDsp == firstStart && audio.BattleActive;
            MusicCheck(battleMusicReport.turnContinuity,"actual turn preserves soundtrack phase");
            TogglePause(); yield return new WaitForSecondsRealtime(.35f); TogglePause();
            battleMusicReport.pauseContinuity = audio.EntryCount == 1 && audio.IntroScheduledDsp == firstStart && audio.BattleActive;
            MusicCheck(battleMusicReport.pauseContinuity,"pause UI preserves soundtrack phase");
            double deadline = audio.LoopScheduledDsp + audio.LoopDuration * 2 + 7;
            while (audio.CompletedLoops < 2 && AudioSettings.dspTime < deadline) yield return null;
            battleMusicReport.loopPlayback = audio.LoopPlaybackVerified;
            battleMusicReport.twoLoops = audio.CompletedLoops >= 2;
            battleMusicReport.loops = audio.CompletedLoops;
            MusicCheck(battleMusicReport.loopPlayback && battleMusicReport.twoLoops,"later loop plays and repeats twice without intro restart");
            ShowSelection(); yield return new WaitForSecondsRealtime(1.2f);
            battleMusicReport.exitedAndStopped = !audio.BattleActive && !audio.MusicPlaying && audio.FadeOutCompleted;
            battleMusicReport.menuRestored = screen == ScreenMode.Selection && menuAudio != null && menuAudio.MusicPlaying;
            MusicCheck(battleMusicReport.exitedAndStopped && battleMusicReport.menuRestored,"battle exit fades/stops music and restores selection soundtrack");
            if (selectionView != null) selectionView.StartButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2f);
            battleMusicReport.reentryFromZero = screen == ScreenMode.Battle && audio.EntryCount == 2
                && audio.ScheduledIntroSample == 0 && audio.IntroPlaybackVerified && audio.IntroScheduledDsp > firstStart;
            MusicCheck(battleMusicReport.reentryFromZero,"new actual battle restarts introduction from zero");
            audio.enabled = false;
            battleMusicReport.disableStops = !audio.MusicPlaying && !audio.BattleActive && audio.FadeOutCompleted;
            MusicCheck(battleMusicReport.disableStops,"component disable cancels both scheduled sources");
            audio.enabled = true;
            ShowSelection(); yield return new WaitForSecondsRealtime(1.2f);
            battleMusicReport.entries = audio.EntryCount;
            battleMusicTestCompleted = true;
            SaveMusicReport();
        }

        void SaveMusicReport()
        {
            battleMusicReport.passed = battleMusicTestCompleted && battleMusicChecksPassed;
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args,"--review-output");
            string folder = index >= 0 && index + 1 < args.Length ? args[index + 1] : "output/audio/battle-music-20261016/runtime";
            Directory.CreateDirectory(folder);
            string json = JsonUtility.ToJson(battleMusicReport,true);
            File.WriteAllText(Path.Combine(folder,"battle-music-review.json"),json);
            Debug.Log("[battle-music-result] " + json);
        }
    }
}
