using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AeroStadium.EditorTools
{
    /// <summary>Keep the two musical segments sample accurate, stereo and resident.</summary>
    public static class BattleMusicImport
    {
        public static void Configure()
        {
            foreach (string name in new[] { "AeroStadiumBattleIntro", "AeroStadiumBattleLoop" })
            {
                string path = "Assets/AeroStadium/Resources/Audio/" + name + ".wav";
                if (!File.Exists(path)) { Debug.LogWarning("Battle music asset is local and missing: " + path); continue; }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Could not import battle music: " + path);
                var settings = importer.defaultSampleSettings;
                bool changed = importer.forceToMono || importer.loadInBackground
                    || settings.compressionFormat != AudioCompressionFormat.PCM
                    || settings.loadType != AudioClipLoadType.DecompressOnLoad
                    || !settings.preloadAudioData || settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate;
                if (!changed) continue;
                importer.forceToMono = false; importer.loadInBackground = false;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            }
        }
    }
}
