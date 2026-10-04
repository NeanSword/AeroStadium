using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;

namespace AeroStadium.EditorTools
{
    /// <summary>Validate persisted Unity assets and sample every native clip after the glTF importer.</summary>
    public static class NativeModelValidation
    {
        const string Root = "Assets/AeroStadium/Resources/NativeModels";
        [Serializable] sealed class Manifest { public int species; public float targetHeight; public bool enableNativeGrounding; public Entry[] animations; }
        [Serializable] sealed class Entry { public string name, semantic; public float duration; public bool loop; }
        [Serializable] sealed class Metadata { public MaterialEntry[] materials; }
        [Serializable] sealed class MaterialEntry { public string name; public Binding[] textureBindings; }
        [Serializable] sealed class Binding { public string property, path; public int wrapU, wrapV, filterMode; }
        [Serializable] sealed class Report { public string utc; public int models, clips, sampledPoses, checkedVertices; public bool passed; public ModelResult[] results; public string[] errors; }
        [Serializable] sealed class ModelResult { public int species, clips, poses, renderers, materialSlots, zeroHeightPoses; public float targetHeight, referenceHeight, minFloor, maxHeight; public bool passed; public string error; }
        sealed class Geometry { public Renderer renderer; public Vector3[] vertices; public BoneWeight[] weights; public Matrix4x4[] bindposes; public Transform[] bones; }
        sealed class Pose { public Transform node; public Vector3 position, scale; public Quaternion rotation; public void Restore() { node.localPosition = position; node.localRotation = rotation; node.localScale = scale; } }

        [MenuItem("AeroStadium/Valider tous les modèles natifs")]
        public static void VerifyAll()
        {
            var results = new List<ModelResult>(); var errors = new List<string>();
            var report = new Report { utc = DateTime.UtcNow.ToString("o") };
            string[] paths = Directory.GetFiles(Root, "native-manifest.json", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            int expected = 151;
            string[] args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--native-expected-count");
            if (at >= 0 && at + 1 < args.Length) expected = int.Parse(args[at + 1]);
            if (paths.Length != expected) errors.Add($"Catalogue natif incomplet : {paths.Length}/{expected}.");
            foreach (string path in paths)
            {
                var result = new ModelResult(); GameObject instance = null;
                try
                {
                    Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
                    result.species = manifest.species; result.targetHeight = manifest.targetHeight;
                    string folder = Path.GetDirectoryName(path).Replace('\\', '/');
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Pokemon.prefab");
                    Require(prefab != null, "Prefab absent.");
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    var native = instance.GetComponent<NativePokemonModel>();
                    Require(native != null && native.Species == manifest.species && native.ClipCount == manifest.animations.Length, "Catalogue des clips incorrect.");
                    result.referenceHeight = native.RestBounds.size.y;
                    Require(Mathf.Abs(result.referenceHeight - manifest.targetHeight) <= .0001f * Mathf.Max(1f, manifest.targetHeight), "Hauteur de référence incorrecte.");
                    Require((instance.GetComponentInChildren<NativeGrounding>(true) != null) == manifest.enableNativeGrounding, "Politique de sol incorrecte.");
                    Transform normal = instance.transform.Find("NativeNormalization");
                    Require(normal != null, "Normalisation absente.");
                    Vector3 normalPosition = normal.localPosition, normalScale = normal.localScale; Quaternion normalRotation = normal.localRotation;
                    foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    var poses = native.ModelRoot.GetComponentsInChildren<Transform>(true).Select(t => new Pose { node = t, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
                    var geometry = new List<Geometry>();
                    foreach (Renderer renderer in native.ModelRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        var skin = renderer as SkinnedMeshRenderer;
                        Mesh mesh = skin != null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                        Require(mesh != null && mesh.isReadable && mesh.vertexCount > 0, "Maillage absent ou non lisible : " + renderer.name);
                        var data = new Geometry { renderer = renderer, vertices = mesh.vertices };
                        if (skin != null)
                        {
                            data.weights = mesh.boneWeights; data.bindposes = mesh.bindposes; data.bones = skin.bones;
                            Require(data.weights.Length == data.vertices.Length && data.bindposes.Length == data.bones.Length && data.bones.All(b => b != null && b.IsChildOf(native.ModelRoot)), "Peau invalide : " + renderer.name);
                        }
                        geometry.Add(data); result.renderers++;
                        var uv = new List<Vector2>(); mesh.GetUVs(2, uv);
                        Require(uv.Count == mesh.vertexCount, "UV natives absentes : " + renderer.name);
                        if (renderer.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name == "AeroStadium/NativeSmokeCloud"))
                        {
                            var controls = new List<Vector4>(); mesh.GetUVs(4, controls);
                            Require(mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord4) == 4 && controls.Count == mesh.vertexCount,
                                "Contrôles de fumée persistés absents : " + renderer.name);
                            Require(controls.All(c => c.x >= 0f && c.x <= 1f && c.y >= 0f && c.y <= 1f && c.z >= 0f && c.z <= 1f),
                                "Contrôles de fumée invalides : " + renderer.name);
                            if (renderer.name.Contains("SmokeGeom"))
                                Require(controls.All(c => Mathf.Abs(c.x - .5f) > .49f && Mathf.Abs(c.y - .5f) > .49f && c.z > 0f),
                                    "Taille des nuages absente : " + renderer.name);
                        }
                        foreach (Material material in renderer.sharedMaterials)
                        {
                            Require(material != null && material.shader != null && material.shader.name.StartsWith("AeroStadium/Native", StringComparison.Ordinal), "Matériau natif absent : " + renderer.name);
                            Require(!ShaderUtil.GetShaderMessages(material.shader).Any(m => m.severity.ToString() == "Error"), "Shader invalide : " + material.shader.name);
                            if (material.shader.name == "AeroStadium/NativeSmokeCloud")
                            {
                                foreach (string property in new[] { "_CloudAtlas", "_CloudFlow" })
                                {
                                    Texture2D texture = material.GetTexture(property) as Texture2D;
                                    Require(texture != null, "Texture de volutes absente : " + material.name + "/" + property);
                                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
                                    TextureWrapMode wrap = property == "_CloudAtlas" ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                                    Require(importer != null && !importer.sRGBTexture && !importer.alphaIsTransparency && importer.mipmapEnabled
                                        && importer.wrapModeU == wrap && importer.wrapModeV == wrap && importer.filterMode == FilterMode.Bilinear,
                                        "Données de volutes mal importées : " + material.name + "/" + property);
                                }
                                Require(material.HasProperty("_CloudWorldOffset"), "Ancrage de fumée absent du shader.");
                            }
                            result.materialSlots++;
                        }
                    }
                    if (manifest.species == 110)
                    {
                        var clouds = native.ModelRoot.GetComponent<NativeSmokeBillboardScale>();
                        Require(clouds != null, "Pilote de fumée de Smogogo absent.");
                        var cloudData = new SerializedObject(clouds).FindProperty("targets");
                        int anchored = 0;
                        for (int i = 0; i < cloudData.arraySize; i++)
                        {
                            var item = cloudData.GetArrayElementAtIndex(i);
                            var renderer = item.FindPropertyRelative("renderer").objectReferenceValue as Renderer;
                            if (renderer == null || !renderer.name.Contains("SmokeGeom")) continue;
                            var heads = item.FindPropertyRelative("headAnchors");
                            int expectedHeads = renderer.name.Contains("B2") ? 2 : 1;
                            Require(heads.arraySize == expectedHeads, "Ancrages natifs de fumée absents : " + renderer.name);
                            for (int h = 0; h < heads.arraySize; h++)
                                Require(heads.GetArrayElementAtIndex(h).objectReferenceValue != null, "Os de fumée absent : " + renderer.name);
                            anchored++;
                        }
                        Require(anchored == 3, "Groupes de fumée de Smogogo incomplets.");
                    }
                    var persistent = instance.GetComponent<NativePersistentSmoke>();
                    Require((manifest.species == 109 || manifest.species == 110) == (persistent != null),
                        "Configuration de fumée persistante incorrecte.");
                    if (persistent != null)
                    {
                        var settings = new SerializedObject(persistent);
                        Require(settings.FindProperty("nativeModel").objectReferenceValue == native,
                            "La fumée ne référence pas son modèle natif.");
                        var vents = settings.FindProperty("vents");
                        Require(vents.arraySize == (manifest.species == 109 ? 22 : 21), "Cheminées de fumée incomplètes.");
                        var usedVents = new HashSet<Transform>();
                        for (int v = 0; v < vents.arraySize; v++)
                        {
                            var item = vents.GetArrayElementAtIndex(v);
                            var head = item.FindPropertyRelative("head").objectReferenceValue as Transform;
                            Vector3 point = item.FindPropertyRelative("headLocalPoint").vector3Value;
                            Vector3 direction = item.FindPropertyRelative("headLocalDirection").vector3Value;
                            Require(head != null && head.IsChildOf(native.ModelRoot) && usedVents.Add(head), "Cheminée sans os natif unique.");
                            Require(Finite(point) && Finite(direction) && Mathf.Abs(direction.magnitude-1f) < .001f, "Direction de cheminée invalide.");
                            // Independently check the imported handedness/bind space against skinned body vertices.
                            float distance = VentSurfaceDistance(native.ModelRoot, head, head.TransformPoint(point));
                            Require(distance <= Mathf.Max(.004f,native.ModelHeight*.035f), "Cheminée décalée du corps : " + head.name + " distance=" + distance);
                        }
                        var template = settings.FindProperty("sourceMaterial").objectReferenceValue as Material;
                        Require(template != null && template.shader.name == "AeroStadium/NativeSmokeCloud"
                            && template.GetTexture("_CloudAtlas") != null && template.GetTexture("_CloudFlow") != null,
                            "Texture de fumée persistante absente.");
                    }
                    ValidateTextureSettings(folder);
                    var serialized = new SerializedObject(native); var clips = serialized.FindProperty("clips");
                    result.minFloor = float.PositiveInfinity;
                    for (int i = 0; i < manifest.animations.Length; i++)
                    {
                        Entry expectedClip = manifest.animations[i];
                        var clip = (AnimationClip)clips.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue;
                        Require(clip != null && clip.name == expectedClip.name && Mathf.Abs(clip.length - expectedClip.duration) <= .05f, "Nom/durée du clip incorrects : " + expectedClip.name);
                        Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime == expectedClip.loop, "Boucle incorrecte : " + expectedClip.name);
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        Require(bindings.Length > 0, "Clip sans courbes : " + expectedClip.name);
                        foreach (var binding in bindings)
                            Require(string.IsNullOrEmpty(binding.path) || native.ModelRoot.Find(binding.path) != null, "Chemin animé absent : " + expectedClip.name + "/" + binding.path);
                        foreach (float fraction in new[] { 0f, .25f, .5f, .75f, .999f })
                        {
                            foreach (Pose pose in poses) pose.Restore();
                            clip.SampleAnimation(native.ModelRoot.gameObject, clip.length * fraction);
                            foreach (Pose pose in poses)
                            {
                                Require(Finite(pose.node.localPosition) && Finite(pose.node.localScale) && Finite(pose.node.localRotation), "Transformation non finie : " + expectedClip.name);
                            }
                            foreach (var bridge in native.ModelRoot.GetComponentsInChildren<NativeMaterialBridge>(true)) bridge.Apply();
                            Bounds bounds = Measure(instance.transform, geometry, ref report.checkedVertices);
                            Require(Finite(bounds.size), "Dimensions non finies : " + expectedClip.name);
                            // Native disappearance/teleport clips may intentionally disable visible renderers.
                            // The nonzero imported reference height is checked independently above.
                            if (bounds.size.y <= .000001f) result.zeroHeightPoses++;
                            result.minFloor = Mathf.Min(result.minFloor, bounds.min.y); result.maxHeight = Mathf.Max(result.maxHeight, bounds.size.y);
                            Require(normal.localPosition == normalPosition && normal.localScale == normalScale && Quaternion.Angle(normal.localRotation, normalRotation) <= .001f, "Animation de la normalisation externe.");
                            result.poses++;
                        }
                        result.clips++;
                    }
                    result.passed = true;
                }
                catch (Exception exception) { result.error = exception.ToString(); errors.Add($"{result.species:000} : {exception.Message}"); }
                finally { if (instance != null) UnityEngine.Object.DestroyImmediate(instance); }
                results.Add(result);
                Debug.Log($"[native-unity-validation] species={result.species} clips={result.clips} poses={result.poses} passed={result.passed}");
            }
            report.results = results.ToArray(); report.errors = errors.ToArray(); report.models = results.Count;
            report.clips = results.Sum(r => r.clips); report.sampledPoses = results.Sum(r => r.poses); report.passed = errors.Count == 0;
            string destination = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "output", "animations", "bdsp-inspection", "unity-native-validation.json");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllText(destination, JsonUtility.ToJson(report, true));
            Debug.Log($"[native-unity-validation-result] models={report.models} clips={report.clips} poses={report.sampledPoses} passed={report.passed} report={destination}");
            if (!report.passed) throw new InvalidDataException(string.Join("\n", errors));
        }

        static float VentSurfaceDistance(Transform root, Transform anchor, Vector3 tip)
        {
            float nearest = float.PositiveInfinity;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.name.Contains("_BodySkin")) continue;
                Mesh mesh = skin.sharedMesh;
                Vector3[] points=mesh.vertices; BoneWeight[] weights=mesh.boneWeights;
                Transform[] bones=skin.bones; Matrix4x4[] bind=mesh.bindposes;
                int owner=Array.IndexOf(bones,anchor);
                if (owner < 0) continue;
                Matrix4x4[] matrices=bones.Select((bone,i)=>bone.localToWorldMatrix*bind[i]).ToArray();
                for (int i=0;i<points.Length;i++)
                {
                    BoneWeight w=weights[i];
                    float ownership=(w.boneIndex0==owner?w.weight0:0f)+(w.boneIndex1==owner?w.weight1:0f)
                        +(w.boneIndex2==owner?w.weight2:0f)+(w.boneIndex3==owner?w.weight3:0f);
                    if (ownership <= .5f) continue;
                    Vector3 Part(int b,float amount)=>amount==0f?Vector3.zero:matrices[b].MultiplyPoint3x4(points[i])*amount;
                    Vector3 world=Part(w.boneIndex0,w.weight0)+Part(w.boneIndex1,w.weight1)
                        +Part(w.boneIndex2,w.weight2)+Part(w.boneIndex3,w.weight3);
                    nearest=Mathf.Min(nearest,Vector3.Distance(world,tip));
                }
            }
            return nearest;
        }

        static void ValidateTextureSettings(string folder)
        {
            Metadata metadata = JsonUtility.FromJson<Metadata>(File.ReadAllText(folder + "/native_materials.json"));
            foreach (var material in metadata.materials)
                foreach (var binding in material.textureBindings ?? Array.Empty<Binding>())
                {
                    if (string.IsNullOrEmpty(binding.path)) continue;
                    string safe = new string(material.name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
                    var unityMaterial = AssetDatabase.LoadAssetAtPath<Material>(folder + "/Native_" + safe + ".mat");
                    Require(unityMaterial != null, "Matériau sauvegardé absent : " + material.name);
                    string property = binding.property == "_Col0Tex" ? "_BaseMap" : binding.property == "_L1Col0Tex" ? "_LayerMap" : binding.property == "_NormalMapTex" ? "_NormalMap" : binding.property;
                    if (!unityMaterial.HasProperty(property)) continue;
                    Texture texture = unityMaterial.GetTexture(property);
                    if (texture == null) continue;
                    string path = AssetDatabase.GetAssetPath(texture);
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue; // Built-in white/black defaults do not have an importer.
                    Require((int)importer.wrapModeU == binding.wrapU && (int)importer.wrapModeV == binding.wrapV && (int)importer.filterMode == binding.filterMode,
                        "Échantillonnage natif incorrect : " + material.name + "/" + binding.property);
                }
        }
        static Bounds Measure(Transform actor, List<Geometry> geometry, ref int vertices)
        {
            Bounds bounds = new Bounds(); bool found = false;
            foreach (var data in geometry)
            {
                if (!data.renderer.enabled || !data.renderer.gameObject.activeInHierarchy) continue;
                Matrix4x4[] matrices = data.bones?.Select((bone, i) => Relative(bone, actor) * data.bindposes[i]).ToArray();
                Matrix4x4 rigid = matrices == null ? Relative(data.renderer.transform, actor) : Matrix4x4.identity;
                for (int i = 0; i < data.vertices.Length; i++)
                {
                    Vector3 point;
                    if (matrices == null) point = rigid.MultiplyPoint3x4(data.vertices[i]);
                    else
                    {
                        BoneWeight weight = data.weights[i]; Vector3 value = data.vertices[i];
                        Vector3 Part(int bone, float amount) => amount == 0f ? Vector3.zero : matrices[bone].MultiplyPoint3x4(value) * amount;
                        point = Part(weight.boneIndex0, weight.weight0) + Part(weight.boneIndex1, weight.weight1) + Part(weight.boneIndex2, weight.weight2) + Part(weight.boneIndex3, weight.weight3);
                    }
                    Require(Finite(point), "Sommet animé non fini.");
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                    vertices++;
                }
            }
            return bounds;
        }
        static Matrix4x4 Relative(Transform node, Transform root)
        {
            if (node == root) return Matrix4x4.identity;
            Require(node != null && node.parent != null, "Os hors modèle.");
            return Relative(node.parent, root) * Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale);
        }
        static void Require(bool condition, string error) { if (!condition) throw new InvalidDataException(error); }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(Quaternion v) => Finite(v.x) && Finite(v.y) && Finite(v.z) && Finite(v.w);
    }
}
