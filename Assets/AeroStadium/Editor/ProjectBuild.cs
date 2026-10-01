using System;
using System.IO;
using AeroStadium.Core;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AeroStadium.EditorTools
{
    /// <summary>Repeatable project preparation and Windows builds, including batch mode.</summary>
    public static class ProjectBuild
    {
        private const string ScenePath = "Assets/AeroStadium/Scenes/Arena.unity";
        private const string SettingsFolder = "Assets/AeroStadium/Settings";
        private const string RendererPath = SettingsFolder + "/ArenaRenderer.asset";
        private const string PipelinePath = SettingsFolder + "/ArenaPipeline.asset";

        [MenuItem("AeroStadium/Prepare Project")]
        public static void Prepare()
        {
            PlayerSettings.productName = "AeroStadium";
            PlayerSettings.companyName = "NeanSword";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            ConfigureInputHandling();

            EnsureAssetFolder(SettingsFolder);
            ConfigureRenderPipeline();
            LocalModelImporter.PrepareModels();
            EnsureAssetFolder("Assets/AeroStadium/Scenes");
            PrepareScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("AeroStadium project prepared: Arena scene, URP, Windows Mono, 1600x900 window.");
        }

        [MenuItem("AeroStadium/Validate Catalog")]
        public static void Validation()
        {
            var report = new CatalogValidationReport
            {
                utc = DateTime.UtcNow.ToString("o"),
                editorVersion = Application.unityVersion
            };
            try
            {
                TextAsset json = Resources.Load<TextAsset>("Data/catalog");
                if (json == null)
                    throw new InvalidOperationException("Missing Resources/Data/catalog.json.");
                Catalog catalog = JsonUtility.FromJson<Catalog>(json.text);
                if (catalog == null)
                    throw new InvalidOperationException("The catalog JSON could not be read.");
                catalog.Validate();
                report.valid = true;
                report.species = catalog.species.Length;
                report.moves = catalog.moves.Length;
                report.items = catalog.items == null ? 0 : catalog.items.Length;
                WriteReport("catalog-validation.json", report);
                Debug.Log($"Catalog validated: {report.species} species, {report.moves} moves, {report.items} items.");
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
                WriteReport("catalog-validation.json", report);
                throw;
            }
        }

        [MenuItem("AeroStadium/Build Windows")]
        public static void BuildWindows()
        {
            var report = new WindowsBuildReport
            {
                utc = DateTime.UtcNow.ToString("o"),
                editorVersion = Application.unityVersion,
                scene = ScenePath,
                backend = "Mono",
                outputPath = Path.Combine(ProjectRoot, "Builds", "Windows", "AeroStadium.exe"),
                result = "NotStarted"
            };
            try
            {
                Prepare();
                Validation();
                Directory.CreateDirectory(Path.GetDirectoryName(report.outputPath));
                BuildReport build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = report.outputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                BuildSummary summary = build.summary;
                report.result = summary.result.ToString();
                report.totalErrors = summary.totalErrors;
                report.totalWarnings = summary.totalWarnings;
                report.totalBytes = summary.totalSize;
                report.totalSeconds = summary.totalTime.TotalSeconds;
                if (summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException($"Windows build {summary.result}: {summary.totalErrors} errors. See the Unity build log.");
                WriteReport("build-windows.json", report);
                Debug.Log("Windows build succeeded: " + report.outputPath);
            }
            catch (Exception exception)
            {
                if (report.result == "NotStarted") report.result = "FailedBeforeBuild";
                report.error = exception.ToString();
                WriteReport("build-windows.json", report);
                throw;
            }
        }

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static void ConfigureInputHandling()
        {
            PlayerSettings[] settings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (settings.Length == 0)
            {
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset"))
                {
                    if (asset is PlayerSettings playerSettings)
                    {
                        settings = new[] { playerSettings };
                        break;
                    }
                }
            }
            if (settings.Length == 0)
                throw new InvalidOperationException("Cannot access PlayerSettings to configure input handling.");
            var serialized = new SerializedObject(settings[0]);
            SerializedProperty inputHandling = serialized.FindProperty("activeInputHandler");
            if (inputHandling == null)
                throw new InvalidOperationException("This editor does not expose the activeInputHandler setting.");
            // Both supports the new Input System UI while keeping editor templates compatible.
            inputHandling.intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRenderPipeline()
        {
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "ArenaRenderer";
                renderer.renderingMode = RenderingMode.Forward;
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "ArenaPipeline";
                pipeline.supportsHDR = true;
                pipeline.msaaSampleCount = 4;
                pipeline.renderScale = 1f;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void PrepareScene()
        {
            var scene = File.Exists(Path.Combine(ProjectRoot, ScenePath))
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameBootstrap bootstrap = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (GameBootstrap candidate in root.GetComponentsInChildren<GameBootstrap>(true))
                {
                    if (bootstrap != null)
                        throw new InvalidOperationException("Arena contains more than one GameBootstrap.");
                    bootstrap = candidate;
                }
            }
            if (bootstrap == null)
            {
                var root = new GameObject("Bootstrap");
                root.AddComponent<GameBootstrap>();
            }
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Unable to save the Arena scene.");
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureAssetFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(slash + 1))))
                throw new InvalidOperationException("Unable to create asset folder: " + path);
        }

        private static void WriteReport(string filename, object report)
        {
            string folder = Path.Combine(ProjectRoot, "Builds", "Reports");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, filename), JsonUtility.ToJson(report, true));
        }

        [Serializable]
        private sealed class CatalogValidationReport
        {
            public string utc, editorVersion, error;
            public bool valid;
            public int species, moves, items;
        }

        [Serializable]
        private sealed class WindowsBuildReport
        {
            public string utc, editorVersion, scene, backend, outputPath, result, error;
            public int totalErrors, totalWarnings;
            public ulong totalBytes;
            public double totalSeconds;
        }
    }
}
