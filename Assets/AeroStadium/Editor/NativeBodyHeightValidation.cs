using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;

namespace AeroStadium.EditorTools
{
    /// <summary>Independent indexed body measurements of the persisted prefabs, without cached renderer bounds.</summary>
    public static class NativeBodyHeightValidation
    {
        const string Root = "Assets/AeroStadium/Resources/NativeModels";
        [Serializable] sealed class Manifest { public int species; public string name; public float targetHeight; public Entry[] animations; }
        [Serializable] sealed class Entry { public string name, semantic; public float duration; }
        [Serializable] sealed class Report { public string utc, measurement; public int models, poses; public bool passed, requiredNormalized; public Result[] results; public string[] errors; }
        [Serializable] sealed class Result { public int species, bodyRenderers, bodyVertices; public string name; public float targetHeight, measuredIdle0Height, measuredIdle0Floor, heightError, floorError, normalizationScale, maxNormalizationDrift; public bool normalized; public Sample[] samples; }
        [Serializable] sealed class Sample { public float idleFraction, time, bodyFloor, bodyHeight; public Vector3 bodyMinimum, bodyMaximum, allGeometryMinimum, allGeometryMaximum; }
        sealed class Pose { public Transform node; public Vector3 position, scale; public Quaternion rotation; public void Restore() { node.localPosition = position; node.localRotation = rotation; node.localScale = scale; } }

        public static void PrepareAndVerifyAll()
        {
            NativeModelImporter.PrepareModels();
            VerifyAll();
        }

        [MenuItem("AeroStadium/Valider les tailles des corps natifs")]
        public static void VerifyAll()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool requireNormalized = args.Contains("--native-body-require-normalized");
            string output = "NativeBodyHeightValidation.json";
            int outputAt = Array.IndexOf(args, "--native-body-report");
            if (outputAt >= 0 && outputAt + 1 < args.Length) output = args[outputAt + 1];
            var results = new List<Result>(); var errors = new List<string>();
            string[] paths = Directory.GetFiles(Root, "native-manifest.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (paths.Length != 151) errors.Add("Catalogue incomplet : " + paths.Length + "/151.");
            foreach (string path in paths)
            {
                GameObject actor = null; var result = new Result();
                try
                {
                    Manifest m = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
                    result.species = m.species; result.name = m.name; result.targetHeight = m.targetHeight;
                    string folder = Path.GetDirectoryName(path).Replace('\\', '/');
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Pokemon.prefab");
                    Require(prefab != null, "Prefab absent.");
                    actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    var native = actor.GetComponent<NativePokemonModel>();
                    Require(native != null && native.Species == m.species, "Composant natif incorrect.");
                    foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    foreach (var grounding in actor.GetComponentsInChildren<NativeGrounding>(true)) grounding.enabled = false;
                    Transform normal = actor.transform.Find("NativeNormalization");
                    Require(normal != null, "Normalisation absente.");
                    Vector3 normalPosition = normal.localPosition, normalScale = normal.localScale;
                    Quaternion normalRotation = normal.localRotation;
                    result.normalizationScale = normalScale.y;
                    var baseline = native.ModelRoot.GetComponentsInChildren<Transform>(true).Select(t => new Pose { node = t, position = t.localPosition, scale = t.localScale, rotation = t.localRotation }).ToArray();
                    int idleIndex = Array.FindIndex(m.animations, a => a.semantic == "idle");
                    Require(idleIndex >= 0, "Idle absent.");
                    AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/NativeClips/" + idleIndex.ToString("000") + ".anim");
                    Require(idle != null, "Clip Idle absent.");
                    var secondary = new List<AnimationClip>();
                    for (int i = 0; i < m.animations.Length; i++)
                        if (m.animations[i].semantic == "secondary")
                        {
                            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/NativeSecondaryClips/" + i.ToString("000") + ".anim");
                            Require(clip != null, "Clip auxiliaire filtré absent."); secondary.Add(clip);
                        }
                    var samples = new List<Sample>();
                    foreach (float fraction in new[] { 0f, .25f, .5f, .75f, 1f })
                    {
                        foreach (Pose pose in baseline) pose.Restore();
                        float time = idle.length * fraction; idle.SampleAnimation(native.ModelRoot.gameObject, time);
                        foreach (AnimationClip clip in secondary) clip.SampleAnimation(native.ModelRoot.gameObject, 0f);
                        foreach (var bridge in native.ModelRoot.GetComponentsInChildren<NativeMaterialBridge>(true)) bridge.Apply();
                        Bounds body = Measure(native.ModelRoot, actor.transform, true, out int rendererCount, out int vertexCount);
                        Bounds all = Measure(native.ModelRoot, actor.transform, false, out _, out _);
                        if (fraction == 0f) { result.bodyRenderers = rendererCount; result.bodyVertices = vertexCount; result.measuredIdle0Height = body.size.y; result.measuredIdle0Floor = body.min.y; }
                        samples.Add(new Sample { idleFraction = fraction, time = time, bodyFloor = body.min.y, bodyHeight = body.size.y, bodyMinimum = body.min, bodyMaximum = body.max, allGeometryMinimum = all.min, allGeometryMaximum = all.max });
                        result.maxNormalizationDrift = Mathf.Max(result.maxNormalizationDrift, Vector3.Distance(normalPosition, normal.localPosition), Vector3.Distance(normalScale, normal.localScale), Quaternion.Angle(normalRotation, normal.localRotation));
                    }
                    result.heightError = Mathf.Abs(result.measuredIdle0Height - m.targetHeight);
                    result.floorError = Mathf.Abs(result.measuredIdle0Floor);
                    result.normalized = result.heightError <= .0001f * Mathf.Max(1f, m.targetHeight) && result.floorError <= .0001f && result.maxNormalizationDrift <= .000001f;
                    result.samples = samples.ToArray();
                    if (requireNormalized) Require(result.normalized, "Hauteur/sol du corps non normalisés.");
                }
                catch (Exception exception) { errors.Add(result.species.ToString("000") + ": " + exception.Message); }
                finally { if (actor != null) UnityEngine.Object.DestroyImmediate(actor); }
                results.Add(result);
            }
            var report = new Report { utc = DateTime.UtcNow.ToString("o"), measurement = "Persisted prefab native Idle at five poses + filtered secondary at zero; explicit local TRS * bindpose * bone weights; visible color-writing NativeLayeredLit submesh indices only; cached RestBounds/Renderer.bounds not used", models = results.Count, poses = results.Sum(r => r.samples?.Length ?? 0), passed = errors.Count == 0, requiredNormalized = requireNormalized, results = results.ToArray(), errors = errors.ToArray() };
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidDataException("[native-body-height] " + string.Join(" | ", errors));
            Debug.Log("[native-body-height] passed=true models=" + report.models + " poses=" + report.poses + " normalized=" + results.Count(r => r.normalized) + " report=" + output);
        }

        internal static Bounds MeasureVisibleBodyWorld(Transform model, out int rendererCount, out int vertexCount)
            => Measure(model, null, true, out rendererCount, out vertexCount);

        static bool BodyMaterial(Material material) => material != null && material.shader != null && material.shader.name == "AeroStadium/NativeLayeredLit" && material.HasProperty("_ColorMask") && material.GetFloat("_ColorMask") != 0f;
        static Bounds Measure(Transform model, Transform root, bool bodyOnly, out int rendererCount, out int vertexCount)
        {
            Bounds result = new Bounds(); bool found = false; rendererCount = 0; vertexCount = 0;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var skin = renderer as SkinnedMeshRenderer;
                Mesh mesh = skin != null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                Require(mesh != null && mesh.isReadable, "Maillage non lisible : " + renderer.name);
                Vector3[] vertices = mesh.vertices;
                var indices = new HashSet<int>();
                if (bodyOnly)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        if (s < materials.Length && BodyMaterial(materials[s]))
                            foreach (int index in mesh.GetIndices(s)) indices.Add(index);
                }
                else for (int i = 0; i < vertices.Length; i++) indices.Add(i);
                if (indices.Count == 0) continue;
                rendererCount++; vertexCount += indices.Count;
                BoneWeight[] weights = null; Matrix4x4[] matrices = null;
                if (skin != null)
                {
                    weights = mesh.boneWeights; Matrix4x4[] bindposes = mesh.bindposes; Transform[] bones = skin.bones;
                    Require(weights.Length == vertices.Length && bindposes.Length == bones.Length, "Skin invalide.");
                    matrices = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++) matrices[i] = LocalChain(bones[i], root) * bindposes[i];
                }
                Matrix4x4 staticMatrix = skin == null ? LocalChain(renderer.transform, root) : Matrix4x4.identity;
                foreach (int i in indices)
                {
                    Require(i >= 0 && i < vertices.Length, "Index invalide.");
                    Vector3 vertex = vertices[i], point;
                    if (skin == null) point = staticMatrix.MultiplyPoint3x4(vertex);
                    else
                    {
                        BoneWeight weight = weights[i];
                        point = Weighted(weight.boneIndex0, weight.weight0) + Weighted(weight.boneIndex1, weight.weight1) + Weighted(weight.boneIndex2, weight.weight2) + Weighted(weight.boneIndex3, weight.weight3);
                        Vector3 Weighted(int index, float value) { if (value <= 0f) return Vector3.zero; Require(index >= 0 && index < matrices.Length, "Os invalide."); return matrices[index].MultiplyPoint3x4(vertex) * value; }
                    }
                    Require(!float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z), "Sommet non fini.");
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; } else result.Encapsulate(point);
                }
            }
            Require(found, "Aucune géométrie visible."); return result;
        }
        static Matrix4x4 LocalChain(Transform node, Transform root)
        {
            var chain = new Stack<Transform>();
            while (node != root) { Require(node != null, "Transform hors modèle."); chain.Push(node); node = node.parent; }
            Matrix4x4 result = Matrix4x4.identity;
            while (chain.Count > 0) { Transform part = chain.Pop(); result = result * Matrix4x4.TRS(part.localPosition, part.localRotation, part.localScale); }
            return result;
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    }
}
