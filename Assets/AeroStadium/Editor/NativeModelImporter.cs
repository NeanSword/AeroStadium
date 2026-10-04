using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.EditorTools
{
    /// <summary>Prepare native payloads separately; old LocalModels and authored rigs are not rewritten.</summary>
    public static class NativeModelImporter
    {
        const string Root = "Assets/AeroStadium/Resources/NativeModels";
        [Serializable] sealed class Manifest
        {
            public int schemaVersion, species;
            public string name, modelFile;
            public float targetHeight, normalizationYaw;
            public bool restoreNativeTangents;
            public bool enableNativeGrounding;
            public string[] groundingExcludedRenderers;
            public ClipEntry[] animations;
        }
        [Serializable] sealed class ClipEntry
        {
            public string name, semantic;
            public float duration;
            public bool loop, hasRootMotion;
            public string[] rootMotionPaths;
            public ChangingTarget[] changingTargets;
        }
        [Serializable] sealed class ChangingTarget { public int node; public string path, nativePath; }
        [Serializable] sealed class ImportReport
        {
            public int species, sourceClipCount;
            public float targetHeight, referenceHeight, referenceFloor, normalizationScale;
            public string normalizationBoundsPolicy;
            public float sourceReferenceHeight, sourceReferenceFloor;
            public int normalizationBodyRenderers, normalizationBodyVertices;
            public TangentReport[] tangentCorrections;
            public FallbackReport[] semanticFallbacks;
            public SecondaryReport[] secondaryLayers;
            public bool groundingEnabled;
            public int groundingBodyRenderers, groundingBodyVertices;
        }
        [Serializable] sealed class TangentReport
        {
            public string importedMesh, savedMesh;
            public int vertices, tangents;
            public float beforeMaxAbsDotNormal, beforeMeanAbsDotNormal, afterMaxAbsDotNormal, afterMeanAbsDotNormal;
            public bool shaderNormalsUnused, shaderTangentsUnused, strictThresholdPassed;
            public string thresholdExemptionReason;
        }
        [Serializable] sealed class FallbackReport
        {
            public string requestedRole, sourceClip, sourceState, reason;
            public float sourceDuration;
            public bool sourceLoop, holdsFinalPose;
        }
        [Serializable] sealed class SecondaryReport
        {
            public string sourceClip, filteredClip, layerName;
            public float sourceDuration, filteredDuration;
            public int sourceBindingCount, keptBindingCount;
            public bool sourceLoop;
            public string[] targetPaths, keptProperties;
        }

        [MenuItem("AeroStadium/Préparer les modèles natifs")]
        public static void PrepareModels()
        {
            if (!Directory.Exists(Root)) throw new DirectoryNotFoundException("Le catalogue NativeModels local est absent.");
            string[] manifests = Directory.GetFiles(Root, "native-manifest.json", SearchOption.AllDirectories);
            if (manifests.Length == 0) throw new InvalidDataException("Aucun manifeste natif présent.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            HashSet<int> requested = null;
            string[] args = Environment.GetCommandLineArgs();
            int selection = Array.IndexOf(args, "--native-species");
            if (selection >= 0)
            {
                if (selection + 1 >= args.Length) throw new ArgumentException("Liste native absente.");
                requested = new HashSet<int>();
                foreach (string item in args[selection + 1].Split(','))
                    if (!int.TryParse(item, out int id) || id < 1 || id > 151 || !requested.Add(id))
                        throw new ArgumentException("Espèce native invalide ou dupliquée : " + item);
            }
            var ids = new HashSet<int>();
            foreach (string manifestPath in manifests.OrderBy(p => p, StringComparer.Ordinal))
            {
                Manifest m = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
                if (m != null && requested != null && !requested.Contains(m.species)) continue;
                if (m == null || m.schemaVersion != 1 || m.species < 1 || m.species > 151 || m.targetHeight <= 0f || m.animations == null || m.animations.Length == 0 || !ids.Add(m.species))
                    throw new InvalidDataException("Manifeste natif invalide : " + manifestPath);
                string folder = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
                if (Path.GetFileName(folder) != m.species.ToString("000") || Path.GetFileName(m.modelFile) != m.modelFile)
                    throw new InvalidDataException("Dossier ou chemin du modèle invalide : " + manifestPath);
                PrepareModel(folder, m);
            }
            if (requested != null && !requested.SetEquals(ids)) throw new InvalidDataException("Catalogue natif sélectionné incomplet.");
            AssetDatabase.SaveAssets();
            Debug.Log($"[native-import] prepared={ids.Count} species={string.Join(",", ids.OrderBy(i => i))}");
        }

        static void PrepareModel(string folder, Manifest m)
        {
            string path = folder + "/" + m.modelFile;
            if (!File.Exists(path)) throw new FileNotFoundException("GLB natif absent", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path);
            var reportField = importer?.GetType().GetField("reportItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var importReport = reportField?.GetValue(importer) as GLTFast.Logging.LogItem[];
            var errors = importReport?.Where(i => i.Type == LogType.Error || i.Type == LogType.Exception).ToArray();
            if (errors != null && errors.Length > 0) throw new InvalidDataException("glTFast : " + string.Join(" | ", errors.Select(i => i.ToString())));
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) throw new InvalidDataException("GameObject natif absent : " + path);
            AnimationClip[] imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().ToArray();
            var clips = new NativePokemonClip[m.animations.Length];
            string clipFolder = folder + "/NativeClips";
            if (!AssetDatabase.IsValidFolder(clipFolder)) AssetDatabase.CreateFolder(folder, "NativeClips");
            for (int i = 0; i < clips.Length; i++)
            {
                ClipEntry entry = m.animations[i];
                AnimationClip input = imported.SingleOrDefault(c => c.name == entry.name);
                if (input == null) throw new InvalidDataException("Clip natif absent : " + entry.name);
                if (entry.duration <= 0f || Mathf.Abs(input.length - entry.duration) > .05f)
                    throw new InvalidDataException("Durée du clip incorrecte : " + entry.name);
                string clipPath = clipFolder + "/" + i.ToString("000") + ".anim";
                AnimationClip saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (saved == null) { saved = UnityEngine.Object.Instantiate(input); AssetDatabase.CreateAsset(saved, clipPath); }
                else EditorUtility.CopySerialized(input, saved);
                saved.name = entry.name;
                var settings = AnimationUtility.GetAnimationClipSettings(saved);
                settings.loopTime = entry.loop; settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(saved, settings);
                EditorUtility.SetDirty(saved);
                clips[i] = new NativePokemonClip { clip = saved, role = ParseRole(entry.semantic), sourceLoop = entry.loop,
                    hasRootMotion = entry.hasRootMotion, rootMotionPaths = entry.rootMotionPaths };
            }
            int idleIndex = Array.FindIndex(clips, c => c.role == NativePokemonClipRole.Idle);
            if (idleIndex < 0) throw new InvalidDataException("Animation d’attente native non définie pour " + m.species);

            GameObject actor = new GameObject(string.IsNullOrEmpty(m.name) ? $"Pokemon {m.species:000}" : m.name);
            try
            {
                var normalization = new GameObject("NativeNormalization"); normalization.transform.SetParent(actor.transform, false);
                Transform presentationParent = normalization.transform;
                NativeGrounding grounding = null;
                if (m.enableNativeGrounding)
                {
                    var offset = new GameObject("NativeGrounding"); offset.transform.SetParent(normalization.transform, false);
                    presentationParent = offset.transform;
                }
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Model"; model.transform.SetParent(presentationParent, false);
                var animators = model.GetComponentsInChildren<Animator>(true);
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) throw new InvalidDataException("La racine importée doit porter son Animator natif.");
                foreach (var a in animators) a.enabled = false;
                // Sample a fixed native reference pose; normalization lives outside every animation path.
                clips[idleIndex].clip.SampleAnimation(model, 0f);
                // Secondary native layers contribute only their changing channels to the reference pose.
                var controller = BuildController(folder, clips, idleIndex, m.animations, model, out SecondaryReport[] secondary);
                // The native visibility drivers define which geometry contributes to reference height.
                NativeMaterialImport.Prepare(model, folder + "/native_materials.json", folder);
                NativeEffectGeometry.Restore(model, folder, path);
                TangentReport[] tangents = m.restoreNativeTangents ? RestoreNativeTangents(model, folder) : Array.Empty<TangentReport>();
                normalization.transform.localRotation = Quaternion.Euler(0f, m.normalizationYaw, 0f);
                Bounds initial = MeasureNativeBounds(model, actor.transform, out int bodyRenderers, out int bodyVertices);
                if (initial.size.y < .00001f) throw new InvalidDataException("Hauteur native invalide.");
                float scale = m.targetHeight / initial.size.y;
                normalization.transform.localScale = Vector3.one * scale;
                normalization.transform.localPosition = new Vector3(-initial.center.x, -initial.min.y, -initial.center.z) * scale;
                Bounds normalized = MeasureNativeBounds(model, actor.transform, out _, out _);
                if (Mathf.Abs(normalized.size.y - m.targetHeight) > .002f || Mathf.Abs(normalized.min.y) > .002f)
                    throw new InvalidDataException("Normalisation native incohérente.");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterials.Any(mat => mat == null)) throw new InvalidDataException("Matériau absent : " + renderer.name);
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skin) { skin.updateWhenOffscreen = true; skin.forceMatrixRecalculationPerRender = true; }
                }
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = true;
                NativePokemonModel native = actor.AddComponent<NativePokemonModel>();
                native.Configure(m.species, m.targetHeight, normalized, model.transform, animator, clips);
                var driver = actor.AddComponent<PokemonAnimationDriver>(); driver.ConfigureNative(native);
                if (m.enableNativeGrounding)
                {
                    grounding = presentationParent.gameObject.AddComponent<NativeGrounding>();
                    grounding.Configure(actor.transform, model.transform, m.groundingExcludedRenderers);
                }
                if (m.species == 109 || m.species == 110)
                    actor.AddComponent<NativePersistentSmoke>().Configure(native);
                PrefabUtility.SaveAsPrefabAsset(actor, folder + "/Pokemon.prefab");
                var report = new ImportReport { species = m.species, sourceClipCount = clips.Length,
                    targetHeight = m.targetHeight, referenceHeight = normalized.size.y, referenceFloor = normalized.min.y,
                    normalizationBoundsPolicy = "VisibleNativeLayeredLitReferencedVertices",
                    sourceReferenceHeight = initial.size.y, sourceReferenceFloor = initial.min.y,
                    normalizationBodyRenderers = bodyRenderers, normalizationBodyVertices = bodyVertices,
                    normalizationScale = scale, tangentCorrections = tangents, semanticFallbacks = DescribeFallbacks(clips), secondaryLayers = secondary,
                    groundingEnabled = grounding != null, groundingBodyRenderers = grounding != null ? grounding.BodyRendererCount : 0,
                    groundingBodyVertices = grounding != null ? grounding.BodyVertexCount : 0 };
                File.WriteAllText(folder + "/native-import-report.json", JsonUtility.ToJson(report, true));
                Debug.Log($"[native-import-model] species={m.species} clips={clips.Length} targetHeight={m.targetHeight:F3} referenceHeight={normalized.size.y:F3} normalizationScale={scale:F6}");
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        static AnimatorController BuildController(string folder, NativePokemonClip[] clips, int idleIndex, ClipEntry[] entries, GameObject model, out SecondaryReport[] secondaryReports)
        {
            string path = folder + "/NativeAnimations.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            while (controller.layers.Length > 1) controller.RemoveLayer(controller.layers.Length - 1);
            var machine = controller.layers[0].stateMachine;
            foreach (var state in machine.states) machine.RemoveState(state.state);
            var states = new AnimatorState[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                states[i] = machine.AddState($"Native_{i:000}", new Vector3(200 * (i % 6), 70 * (i / 6)));
                states[i].motion = clips[i].clip;
            }
            machine.defaultState = states[idleIndex];
            if (!clips.Any(c => c.role == NativePokemonClipRole.Faint))
            {
                NativePokemonClip damage = clips.FirstOrDefault(c => c.role == NativePokemonClipRole.Damage);
                if (damage != null)
                {
                    if (damage.sourceLoop) throw new InvalidDataException("Le secours KO nécessite un clip Damage natif non bouclé.");
                    var hold = machine.AddState("Native_FaintFallback", new Vector3(0, -80));
                    hold.motion = damage.clip;
                    // This derived role holds the native damage pose when the source has no dedicated KO clip.
                    // The original native Damage state retains its normal return to Idle.
                }
            }
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].sourceLoop || clips[i].role == NativePokemonClipRole.Faint || i == idleIndex) continue;
                var transition = states[i].AddTransition(states[idleIndex]);
                transition.hasExitTime = true; transition.exitTime = 1f;
                transition.hasFixedDuration = true; transition.duration = .12f;
            }
            var secondary = new List<SecondaryReport>();
            for (int i = 0; i < clips.Length; i++)
                if (clips[i].role == NativePokemonClipRole.Secondary)
                    secondary.Add(BuildSecondaryLayer(folder, controller, clips[i], entries[i], i, model));
            secondaryReports = secondary.ToArray();
            EditorUtility.SetDirty(controller); return controller;
        }

        static SecondaryReport BuildSecondaryLayer(string folder, AnimatorController controller, NativePokemonClip source, ClipEntry entry, int index, GameObject model)
        {
            if (!source.sourceLoop || entry.changingTargets == null || entry.changingTargets.Length == 0)
                throw new InvalidDataException("Clip auxiliaire sans boucle native ou cibles variables : " + source.clip.name);
            foreach (var target in entry.changingTargets)
            {
                if (string.IsNullOrEmpty(target.nativePath) || model.transform.Find(target.nativePath) == null)
                    throw new InvalidDataException("Cible auxiliaire absente : " + target.nativePath);
                if (target.path != "translation" && target.path != "rotation" && target.path != "scale")
                    throw new InvalidDataException("Canal auxiliaire invalide : " + target.path);
            }
            string auxiliaryFolder = folder + "/NativeSecondaryClips";
            if (!AssetDatabase.IsValidFolder(auxiliaryFolder)) AssetDatabase.CreateFolder(folder, "NativeSecondaryClips");
            string path = auxiliaryFolder + "/" + index.ToString("000") + ".anim";
            AnimationClip filtered = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (filtered == null) { filtered = UnityEngine.Object.Instantiate(source.clip); AssetDatabase.CreateAsset(filtered, path); }
            else EditorUtility.CopySerialized(source.clip, filtered);
            filtered.name = source.clip.name + "_NativeSecondary";
            var bindings = AnimationUtility.GetCurveBindings(filtered);
            bool Matches(EditorCurveBinding binding, ChangingTarget target)
            {
                if (binding.type != typeof(Transform)) return false;
                string property = binding.propertyName.StartsWith("m_", StringComparison.Ordinal) ? binding.propertyName.Substring(2) : binding.propertyName;
                return target.nativePath == binding.path && property.StartsWith(target.path == "translation" ? "localPosition." : target.path == "rotation" ? "localRotation." : "localScale.", StringComparison.OrdinalIgnoreCase);
            }
            bool Keep(EditorCurveBinding binding) => entry.changingTargets.Any(target => Matches(binding, target));
            foreach (var binding in bindings) if (!Keep(binding)) AnimationUtility.SetEditorCurve(filtered, binding, null);
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(filtered)) AnimationUtility.SetObjectReferenceCurve(filtered, binding, null);
            var kept = AnimationUtility.GetCurveBindings(filtered);
            if (kept.Length == 0) throw new InvalidDataException("Aucune courbe auxiliaire conservée : " + source.clip.name);
            foreach (var target in entry.changingTargets)
                if (!kept.Any(binding => Matches(binding, target)))
                    throw new InvalidDataException("Courbe auxiliaire non résolue : " + target.nativePath + " / " + target.path);
            if (Mathf.Abs(filtered.length - source.clip.length) > .001f)
                throw new InvalidDataException("Durée auxiliaire modifiée par le filtrage : " + source.clip.name);
            string[] allowedPaths = entry.changingTargets.Select(target => target.nativePath).Distinct(StringComparer.Ordinal).ToArray();
            string maskPath = auxiliaryFolder + "/" + index.ToString("000") + ".mask";
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, maskPath); }
            mask.name = "NativeSecondary_" + index.ToString("000");
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            Transform[] transforms = model.GetComponentsInChildren<Transform>(true);
            mask.transformCount = transforms.Length;
            for (int i = 0; i < transforms.Length; i++)
            {
                string transformPath = AnimationUtility.CalculateTransformPath(transforms[i], model.transform);
                mask.SetTransformPath(i, transformPath);
                mask.SetTransformActive(i, allowedPaths.Contains(transformPath, StringComparer.Ordinal));
            }
            string layerName = "NativeAux_" + index.ToString("000");
            controller.AddLayer(layerName);
            var layers = controller.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f; layer.avatarMask = mask; layer.blendingMode = AnimatorLayerBlendingMode.Override;
            var state = layer.stateMachine.AddState("Loop"); state.motion = filtered; layer.stateMachine.defaultState = state;
            layers[layers.Length - 1] = layer; controller.layers = layers;
            filtered.SampleAnimation(model, 0f);
            EditorUtility.SetDirty(filtered); EditorUtility.SetDirty(mask);
            return new SecondaryReport { sourceClip = source.clip.name, filteredClip = path, layerName = layerName,
                sourceDuration = source.clip.length, filteredDuration = filtered.length, sourceLoop = source.sourceLoop,
                sourceBindingCount = bindings.Length, keptBindingCount = kept.Length, targetPaths = allowedPaths,
                keptProperties = kept.Select(binding => binding.path + " :: " + binding.propertyName).ToArray() };
        }

        static NativePokemonClipRole ParseRole(string semantic)
        {
            return Enum.TryParse(semantic, true, out NativePokemonClipRole role) ? role : NativePokemonClipRole.Other;
        }

        static FallbackReport[] DescribeFallbacks(NativePokemonClip[] clips)
        {
            var result = new List<FallbackReport>();
            void Add(NativePokemonClipRole wanted, NativePokemonClipRole fallback, bool hold)
            {
                if (clips.Any(c => c.role == wanted)) return;
                var source = clips.FirstOrDefault(c => c.role == fallback);
                int index = source == null ? -1 : Array.IndexOf(clips, source);
                result.Add(new FallbackReport { requestedRole = wanted.ToString(), sourceClip = source?.clip?.name,
                    sourceState = source == null ? null : hold ? "Native_FaintFallback" : $"Native_{index:000}",
                    sourceDuration = source?.clip?.length ?? 0f, sourceLoop = source != null && source.sourceLoop,
                    reason = source == null ? "No dedicated or compatible native clip; retain current pose" : "Dedicated native role absent",
                    holdsFinalPose = source != null && hold });
            }
            Add(NativePokemonClipRole.Physical, NativePokemonClipRole.Special, false);
            Add(NativePokemonClipRole.Special, NativePokemonClipRole.Physical, false);
            Add(NativePokemonClipRole.Faint, NativePokemonClipRole.Damage, true);
            Add(NativePokemonClipRole.Showcase, NativePokemonClipRole.Idle, false);
            return result.ToArray();
        }

        static TangentReport[] RestoreNativeTangents(GameObject model, string folder)
        {
            string meshFolder = folder + "/NativeMeshes";
            if (!AssetDatabase.IsValidFolder(meshFolder)) AssetDatabase.CreateFolder(folder, "NativeMeshes");
            var savedMeshes = new Dictionary<Mesh, Mesh>();
            var reports = new List<TangentReport>();
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            Mesh MeshFor(Renderer renderer) => renderer is SkinnedMeshRenderer skin ? skin.sharedMesh
                : renderer.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
            foreach (var renderer in renderers)
            {
                Mesh input = MeshFor(renderer);
                if (input == null) continue;
                if (!savedMeshes.TryGetValue(input, out var saved))
                {
                    if (!input.isReadable) throw new InvalidDataException("Maillage natif non lisible pour tangentes : " + input.name);
                    var t = input.tangents;
                    if (t.Length != input.vertexCount) throw new InvalidDataException("Tangentes sources absentes : " + input.name);
                    var report = new TangentReport { importedMesh = input.name, vertices = input.vertexCount, tangents = t.Length };
                    TangentDotStats(t, input.normals, out report.beforeMaxAbsDotNormal, out report.beforeMeanAbsDotNormal);
                    // This pipeline exports native tangent X reflected to glTF; glTFast 6.20 reflects tangent Z on import.
                    // Correct only the derived Unity mesh. UV2 and native source tangents then share their original basis.
                    for (int i = 0; i < t.Length; i++) { t[i].x = -t[i].x; t[i].z = -t[i].z; }
                    TangentDotStats(t, input.normals, out report.afterMaxAbsDotNormal, out report.afterMeanAbsDotNormal);
                    // Native mask/core adaptations never read normals or tangents. Some of their source meshes
                    // intentionally contain nonorthogonal bases. Every renderer sharing a mesh must qualify.
                    report.shaderNormalsUnused = renderers.Where(r => MeshFor(r) == input).All(r => r.sharedMaterials.Length > 0 &&
                        r.sharedMaterials.All(mat => mat != null && mat.shader != null &&
                            (mat.shader.name == "AeroStadium/NativeEffectMask" || mat.shader.name == "AeroStadium/NativeEffectCore" || mat.shader.name == "AeroStadium/NativeSmokeCloud")));
                    bool gasOnly = renderers.Where(r => MeshFor(r) == input).All(r => r.sharedMaterials.Length > 0 &&
                        r.sharedMaterials.All(mat => mat != null && mat.shader != null && mat.shader.name == "AeroStadium/NativeGasSurface"));
                    if (gasOnly && (input.normals.Length != input.vertexCount || input.normals.Any(n => !float.IsFinite(n.sqrMagnitude) || Mathf.Abs(n.sqrMagnitude - 1f) > .002f)))
                        throw new InvalidDataException("Normales du gaz invalides : " + input.name);
                    report.shaderTangentsUnused = report.shaderNormalsUnused || gasOnly;
                    report.strictThresholdPassed = report.afterMaxAbsDotNormal <= .01f;
                    if (report.shaderNormalsUnused) report.thresholdExemptionReason = "All material slots use native Mask/Core or authored Cloud shaders; normals and tangents are not consumed";
                    if (gasOnly) report.thresholdExemptionReason = "Gas surface uses validated unit normals for alpha; tangents are not consumed";
                    if (!report.strictThresholdPassed && !report.shaderTangentsUnused)
                        throw new InvalidDataException("Base tangent/normale native incohérente : " + input.name);
                    string path = meshFolder + "/" + reports.Count.ToString("000") + ".asset";
                    saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) { saved = UnityEngine.Object.Instantiate(input); AssetDatabase.CreateAsset(saved, path); }
                    else EditorUtility.CopySerialized(input, saved);
                    // Mesh CopySerialized did not persist the newly added extra UV
                    // stream in reused assets. Copy controls through the mesh API.
                    if (input.HasVertexAttribute(VertexAttribute.TexCoord4))
                    {
                        var smokeControls = new List<Vector4>(); input.GetUVs(4, smokeControls);
                        if (smokeControls.Count != input.vertexCount) throw new InvalidDataException("Contrôles de fumée incomplets.");
                        saved.SetUVs(4, smokeControls);
                    }
                    saved.name = input.name; saved.tangents = t;
                    EditorUtility.SetDirty(saved);
                    report.savedMesh = path; reports.Add(report); savedMeshes.Add(input, saved);
                }
                if (renderer is SkinnedMeshRenderer skinned) skinned.sharedMesh = saved;
                else if (renderer.TryGetComponent<MeshFilter>(out var filter)) filter.sharedMesh = saved;
            }
            return reports.ToArray();
        }

        static void TangentDotStats(Vector4[] tangents, Vector3[] normals, out float maximum, out float mean)
        {
            if (tangents.Length != normals.Length) throw new InvalidDataException("Normales ou tangentes manquantes.");
            maximum = 0f; mean = 0f; int count = 0;
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                if (tangent.sqrMagnitude < .0000001f || normals[i].sqrMagnitude < .0000001f) continue;
                float dot = Mathf.Abs(Vector3.Dot(tangent.normalized, normals[i].normalized));
                maximum = Mathf.Max(maximum, dot); mean += dot; count++;
            }
            mean /= Mathf.Max(1, count);
        }

        static Matrix4x4 RelativeMatrix(Transform node, Transform root)
        {
            if (node == root) return Matrix4x4.identity;
            if (node == null || node.parent == null) throw new InvalidDataException("Os hors du modèle natif.");
            return RelativeMatrix(node.parent, root) * Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale);
        }

        static bool IsBodyMaterial(Material material) => material != null && material.shader != null &&
            material.shader.name == "AeroStadium/NativeLayeredLit" && material.HasProperty("_ColorMask") && material.GetFloat("_ColorMask") != 0f;

        static int[] BodyVertexIndices(Mesh mesh, Renderer renderer)
        {
            // A glTF mesh can duplicate its complete vertex array for each primitive. Only vertices
            // referenced by lit color-writing body submeshes define the physical reference size.
            // Smoke/flame Mask/Core and depth-only geometry retain their native animation/rendering.
            var indices = new HashSet<int>();
            Material[] materials = renderer.sharedMaterials;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (submesh >= materials.Length || !IsBodyMaterial(materials[submesh])) continue;
                foreach (int index in mesh.GetIndices(submesh))
                {
                    if (index < 0 || index >= mesh.vertexCount) throw new InvalidDataException("Index de géométrie native invalide : " + renderer.name);
                    indices.Add(index);
                }
            }
            return indices.ToArray();
        }

        static Bounds MeasureNativeBounds(GameObject model, Transform root, out int bodyRenderers, out int bodyVertices)
        {
            Bounds bounds = new Bounds(); bool found = false;
            bodyRenderers = 0; bodyVertices = 0;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!renderer.sharedMaterials.Any(IsBodyMaterial)) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !mesh.isReadable) throw new InvalidDataException("Maillage natif non lisible : " + renderer.name);
                int[] indices = BodyVertexIndices(mesh, renderer);
                if (indices.Length == 0) continue;
                bodyRenderers++; bodyVertices += indices.Length;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    Matrix4x4[] poses = mesh.bindposes; Transform[] bones = skin.bones;
                    if (bones.Length != poses.Length) throw new InvalidDataException("Nombre d’os différent des bind poses.");
                    Matrix4x4[] matrices = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++) matrices[i] = RelativeMatrix(bones[i], root) * poses[i];
                    Vector3[] vertices = mesh.vertices; BoneWeight[] weights = mesh.boneWeights;
                    if (vertices.Length != weights.Length) throw new InvalidDataException("Poids de skin absents.");
                    foreach (int i in indices)
                    {
                        BoneWeight w = weights[i]; Vector3 v = vertices[i];
                        Vector3 point = Skin(w.boneIndex0, w.weight0) + Skin(w.boneIndex1, w.weight1) + Skin(w.boneIndex2, w.weight2) + Skin(w.boneIndex3, w.weight3);
                        Vector3 Skin(int index, float weight) => weight <= 0f ? Vector3.zero : matrices[index].MultiplyPoint3x4(v) * weight;
                        Add(point);
                    }
                }
                else
                {
                    Matrix4x4 matrix = RelativeMatrix(renderer.transform, root);
                    Vector3[] vertices = mesh.vertices;
                    foreach (int i in indices) Add(matrix.MultiplyPoint3x4(vertices[i]));
                }
            }
            void Add(Vector3 point) { if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point); }
            if (!found) throw new InvalidDataException("Aucun sommet Body natif visible.");
            return bounds;
        }
    }
}
