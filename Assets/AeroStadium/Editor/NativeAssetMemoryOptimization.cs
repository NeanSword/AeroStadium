using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;

namespace AeroStadium.EditorTools
{
    /// <summary>Separate authoring payloads from prepared runtime assets without reducing their quality.</summary>
    public static class NativeAssetMemoryOptimization
    {
        const string RuntimeRoot = "Assets/AeroStadium/Resources/NativeModels";
        public const string AuthoringRoot = "Assets/AeroStadium/NativeModelAuthoring";
        [Serializable] sealed class Manifest { public int species; public string modelFile; public Entry[] animations; }
        [Serializable] sealed class Entry { public string name; public float duration; }
        [Serializable] sealed class ModelReport { public int species, clips, renderers, transforms, beforeDependencies, afterDependencies; public string[] remainingSourceDependencies; }
        [Serializable] sealed class Report { public string utc; public int models, clips; public bool passed; public ModelReport[] results; }

        public static string ModelPath(string folder, string filename) => Resolve(folder, filename);
        public static string TexturePath(string folder, string filename) => Resolve(folder, "textures/" + filename);
        static string Resolve(string folder, string relative)
        {
            string original = folder + "/" + relative;
            if (File.Exists(original)) return original;
            return AuthoringRoot + "/" + Path.GetFileName(folder) + "/" + relative;
        }

        // Editor preparation is sequential. Never retain all151 imported GLBs in its cache.
        public static void ReleaseEditorCache()
        {
            AssetDatabase.SaveAssets();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            EditorUtility.UnloadUnusedAssetsImmediate(true);
        }

        public static void DetachModel(GameObject model, string folder)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(model))
                PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model),
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var savedMeshes = new Dictionary<Mesh, Mesh>();
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh input = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (input == null) throw new InvalidDataException("Maillage absent : " + renderer.name);
                if (!AssetDatabase.GetAssetPath(input).EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) continue;
                if (!savedMeshes.TryGetValue(input, out Mesh saved))
                {
                    string meshFolder = folder + "/DetachedMeshes"; EnsureFolder(meshFolder);
                    string meshPath = meshFolder + "/" + savedMeshes.Count.ToString("000") + ".asset";
                    saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (saved == null) { saved = UnityEngine.Object.Instantiate(input); AssetDatabase.CreateAsset(saved, meshPath); }
                    else EditorUtility.CopySerialized(input, saved);
                    EditorUtility.SetDirty(saved); savedMeshes.Add(input, saved);
                }
                if (renderer is SkinnedMeshRenderer target) target.sharedMesh = saved;
                else renderer.GetComponent<MeshFilter>().sharedMesh = saved;
            }
            foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            {
                Avatar input = animator.avatar;
                if (input == null || !AssetDatabase.GetAssetPath(input).EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) continue;
                string avatarPath = folder + "/NativeAvatar.asset";
                Avatar saved = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
                if (saved == null) { saved = UnityEngine.Object.Instantiate(input); AssetDatabase.CreateAsset(saved, avatarPath); }
                else EditorUtility.CopySerialized(input, saved);
                EditorUtility.SetDirty(saved); animator.avatar = saved;
            }
        }

        [MenuItem("AeroStadium/Optimiser les données natives sans perte")]
        public static void Optimize()
        {
            var results = new List<ModelReport>();
            for (int id = 1; id <= 151; id++)
            {
                string folder = RuntimeRoot + "/" + id.ToString("000");
                string prefabPath = folder + "/Pokemon.prefab";
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(folder + "/native-manifest.json"));
                if (manifest.species != id || manifest.animations == null || manifest.animations.Length == 0)
                    throw new InvalidDataException("Manifeste incomplet : " + id);
                string backup = "output/memory/20261010/before/" + id.ToString("000");
                Directory.CreateDirectory(backup);
                foreach (string suffix in new[] { "", ".meta" })
                    if (!File.Exists(backup + "/Pokemon.prefab" + suffix)) File.Copy(prefabPath + suffix, backup + "/Pokemon.prefab" + suffix);
                var result = new ModelReport { species = id, beforeDependencies = AssetDatabase.GetDependencies(prefabPath, true).Length };
                GameObject actor = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    var native = actor.GetComponent<NativePokemonModel>();
                    if (native == null || native.Species != id || native.ClipCount != manifest.animations.Length)
                        throw new InvalidDataException("Clips/prefab incomplets : " + id);
                    result.clips = native.ClipCount;
                    result.renderers = native.ModelRoot.GetComponentsInChildren<Renderer>(true).Length;
                    result.transforms = native.ModelRoot.GetComponentsInChildren<Transform>(true).Length;
                    DetachModel(native.ModelRoot.gameObject, folder);
                    if (native.ModelRoot.GetComponentsInChildren<Transform>(true).Length != result.transforms ||
                        native.ModelRoot.GetComponentsInChildren<Renderer>(true).Length != result.renderers)
                        throw new InvalidDataException("Hiérarchie modifiée : " + id);
                    // Curve targets must survive the removal of the source-prefab wrapper.
                    var clips = new SerializedObject(native).FindProperty("clips");
                    for (int i = 0; i < clips.arraySize; i++)
                    {
                        var clip = clips.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
                        if (clip == null || clip.name != manifest.animations[i].name || Mathf.Abs(clip.length - manifest.animations[i].duration) > .05f)
                            throw new InvalidDataException("Animation modifiée : " + id);
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                            if (!string.IsNullOrEmpty(binding.path) && native.ModelRoot.Find(binding.path) == null)
                                throw new InvalidDataException("Cible animée absente : " + id + "/" + binding.path);
                    }
                    PrefabUtility.SaveAsPrefabAsset(actor, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(actor); }
                AssetDatabase.SaveAssets();
                string authoringFolder = AuthoringRoot + "/" + id.ToString("000"); EnsureFolder(authoringFolder);
                MoveIfPresent(folder + "/" + manifest.modelFile, authoringFolder + "/" + manifest.modelFile);
                MoveIfPresent(folder + "/textures", authoringFolder + "/textures");
                string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
                result.afterDependencies = dependencies.Length;
                result.remainingSourceDependencies = dependencies.Where(p => p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (result.remainingSourceDependencies.Length > 0) throw new InvalidDataException("Dépendance GLB encore active : " + id);
                results.Add(result);
                if (id % 5 == 0) ReleaseEditorCache();
                Debug.Log($"[native-memory-optimize] species={id} clips={result.clips} dependencies={result.beforeDependencies}->{result.afterDependencies}");
            }
            ReleaseEditorCache();
            var report = new Report { utc = DateTime.UtcNow.ToString("o"), models = results.Count,
                clips = results.Sum(r => r.clips), passed = results.Count == 151, results = results.ToArray() };
            string destination = "output/memory/20261010/native-pack-optimization.json";
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllText(destination, JsonUtility.ToJson(report, true));
            Debug.Log($"[native-memory-optimize-result] models={report.models} clips={report.clips} passed={report.passed}");
        }

        static void MoveIfPresent(string source, string destination)
        {
            if (!File.Exists(source) && !Directory.Exists(source)) return;
            if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("Destination déjà présente : " + destination);
            string error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
        }
        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
