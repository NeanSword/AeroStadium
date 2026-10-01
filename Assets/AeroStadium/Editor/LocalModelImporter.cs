using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.EditorTools
{
    /// <summary>Turns ignored local FBX or GLB payloads into ready-to-load URP prefabs.</summary>
    public static class LocalModelImporter
    {
        private const string LocalRoot = "Assets/AeroStadium/Resources/LocalModels";

        [Serializable]
        private sealed class ModelManifest
        {
            public int species;
            public int generation;
            public string name;
            public string modelFile;
            public float targetHeight;
            public bool previewIdle;
            public MaterialEntry[] materials;
        }

        [Serializable]
        private sealed class MaterialEntry
        {
            public string name;
            public string albedo;
            public float[] baseColor;
            public float roughness;
            public bool doubleSided;
            public string alphaMode;
            public float alphaCutoff;
        }

        [MenuItem("AeroStadium/Préparer les modèles locaux")]
        public static void PrepareModels()
        {
            if (!Directory.Exists(LocalRoot))
            {
                throw new DirectoryNotFoundException("Les 151 modèles Generation I sont absents. Exécutez Tools/prepare_generation_one.ps1.");
            }

            var manifestPaths = Directory.GetFiles(LocalRoot, "manifest.json", SearchOption.AllDirectories);
            if (manifestPaths.Length != 151)
                throw new InvalidDataException($"151 manifestes Generation I sont requis, {manifestPaths.Length} trouvés. Exécutez Tools/prepare_generation_one.ps1.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Le shader URP Lit requis pour les modèles locaux est absent.");

            var preparedSpecies = new HashSet<int>();
            foreach (var manifestPath in manifestPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var manifest = JsonUtility.FromJson<ModelManifest>(File.ReadAllText(manifestPath));
                if (manifest == null || manifest.materials == null || manifest.generation != 1 ||
                    manifest.species < 1 || manifest.species > 151 || manifest.targetHeight <= 0)
                    throw new InvalidDataException("Manifeste local invalide : " + manifestPath);
                var directorySpecies = Path.GetFileName(Path.GetDirectoryName(manifestPath));
                if (!int.TryParse(directorySpecies, out var folderId) || folderId != manifest.species ||
                    !preparedSpecies.Add(manifest.species))
                    throw new InvalidDataException("Identifiant dupliqué ou dossier différent du manifeste : " + manifestPath);
                var folder = Path.GetDirectoryName(manifestPath).Replace('\\', '/');
                PrepareModel(folder, manifest, shader);
            }
            if (preparedSpecies.Count != 151 || Enumerable.Range(1, 151).Any(id => !preparedSpecies.Contains(id)))
                throw new InvalidDataException("Le lot local n’inclut pas exactement le Pokédex de Kanto (001–151).");
            AssetDatabase.SaveAssets();
        }

        private static void PrepareModel(string folder, ModelManifest manifest, Shader shader)
        {
            var modelPath = folder + "/" + manifest.modelFile;
            if (Path.GetExtension(modelPath).Equals(".glb", StringComparison.OrdinalIgnoreCase))
            {
                PrepareGlbModel(folder, manifest, modelPath);
                return;
            }
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Le FBX local n’existe pas.", modelPath);

            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Importateur FBX absent : " + modelPath);

            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.Import;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.weldVertices = false;
            importer.optimizeMeshPolygons = false;
            importer.optimizeMeshVertices = false;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 4;
            importer.minBoneWeight = 0f;
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = manifest.previewIdle;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            if (manifest.previewIdle)
            {
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.loopTime = true;
                    clip.loopPose = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }

            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var definition in manifest.materials)
            {
                var texturePath = folder + "/" + definition.albedo;
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (textureImporter == null)
                    throw new FileNotFoundException("Texture albédo absente.", texturePath);
                textureImporter.textureType = TextureImporterType.Default;
                textureImporter.sRGBTexture = true;
                textureImporter.wrapMode = TextureWrapMode.Repeat;
                textureImporter.filterMode = FilterMode.Bilinear;
                textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                textureImporter.maxTextureSize = 4096;
                textureImporter.mipmapEnabled = true;
                textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                textureImporter.alphaIsTransparency = definition.alphaMode != "OPAQUE";
                textureImporter.SaveAndReimport();

                var materialPath = folder + "/" + FileSafeName(definition.name) + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.name = definition.name;
                material.shader = shader;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero);
                var c = definition.baseColor;
                material.SetColor("_BaseColor", c != null && c.Length == 4
                    ? new Color(c[0], c[1], c[2], c[3]) : Color.white);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 1f - definition.roughness);
                material.SetFloat("_Cull", definition.doubleSided ? (float)CullMode.Off : (float)CullMode.Back);
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_AlphaClip", definition.alphaMode == "MASK" ? 1f : 0f);
                material.SetFloat("_Cutoff", definition.alphaCutoff);
                material.DisableKeyword("_ALPHATEST_ON");
                if (definition.alphaMode == "MASK") material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.renderQueue = definition.alphaMode == "MASK" ? (int)RenderQueue.AlphaTest : -1;
                EditorUtility.SetDirty(material);
                materials.Add(definition.name, material);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), definition.name), material);
            }
            importer.SaveAndReimport();

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null) throw new InvalidDataException("Le FBX n’a pas produit de GameObject.");
            var wrapper = new GameObject(manifest.name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                model.name = "Model";
                model.transform.SetParent(wrapper.transform, false);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var reassigned = renderer.sharedMaterials;
                    for (var i = 0; i < reassigned.Length; i++)
                    {
                        if (reassigned[i] == null || !materials.TryGetValue(reassigned[i].name, out var material))
                            throw new InvalidDataException("Correspondance de matériau absente pour " + renderer.name);
                        reassigned[i] = material;
                    }
                    renderer.sharedMaterials = reassigned;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                }
                var bounds = GeometryBounds(model, wrapper.transform);
                if (bounds.size.y <= 0.00001f) throw new InvalidDataException("Hauteur du modèle invalide.");
                var scale = manifest.targetHeight / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;

                var animator = model.GetComponentInChildren<Animator>();
                if (manifest.previewIdle && animator != null)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                        .FirstOrDefault(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal));
                    if (clip == null) throw new InvalidDataException("L’animation d’attente prévue est absente du FBX.");
                    var controllerPath = folder + "/PreviewIdle.controller";
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    if (controller == null)
                        controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                    var stateMachine = controller.layers[0].stateMachine;
                    var state = stateMachine.states.FirstOrDefault().state;
                    if (state == null) state = stateMachine.AddState("Preview idle (création locale)");
                    state.motion = clip;
                    stateMachine.defaultState = state;
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    EditorUtility.SetDirty(controller);
                }
                PrefabUtility.SaveAsPrefabAsset(wrapper, folder + "/Pokemon.prefab");
                var finalBounds = GeometryBounds(model, wrapper.transform);
                if (Mathf.Abs(finalBounds.size.y - manifest.targetHeight) > 0.002f || Mathf.Abs(finalBounds.min.y) > 0.002f)
                    throw new InvalidDataException("La hauteur ou le pivot au sol du prefab n’est pas conforme.");
                Debug.Log($"AeroStadium: {manifest.name} prêt ({finalBounds.size.y:F2} m), LocalModels/{manifest.species}/Pokemon.");
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }

        private static void PrepareGlbModel(string folder, ModelManifest manifest, string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Le GLB local n’existe pas.", modelPath);

            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(modelPath);
            var reportField = importer?.GetType().GetField(
                "reportItems", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var report = reportField?.GetValue(importer) as GLTFast.Logging.LogItem[];
            var errors = report?.Where(item => item.Type == LogType.Error || item.Type == LogType.Exception).ToArray();
            if (errors != null && errors.Length > 0)
                throw new InvalidDataException("glTFast import errors for " + modelPath + ": " +
                    string.Join(" | ", errors.Select(item => item.ToString())));

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (source == null)
                throw new InvalidDataException("glTFast n’a pas produit de GameObject pour " + modelPath);

            var wrapper = new GameObject(manifest.name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
                if (model == null)
                    throw new InvalidDataException("Impossible d’instancier le modèle GLB : " + modelPath);
                model.name = "Model";
                model.transform.SetParent(wrapper.transform, false);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null))
                        throw new InvalidDataException("Matériau glTF absent pour " + renderer.name);
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                }

                var bounds = GeometryBounds(model, wrapper.transform);
                if (bounds.size.y <= 0.00001f)
                    throw new InvalidDataException("Hauteur du modèle GLB invalide.");
                var scale = manifest.targetHeight / bounds.size.y;
                model.transform.localScale *= scale;
                model.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;

                var animator = model.GetComponentInChildren<Animator>(true);
                if (animator != null)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                var legacyAnimation = model.GetComponentInChildren<Animation>(true);
                if (legacyAnimation != null && legacyAnimation.clip != null)
                {
                    legacyAnimation.clip.wrapMode = WrapMode.Loop;
                    legacyAnimation.playAutomatically = true;
                }

                var prefabPath = folder + "/Pokemon.prefab";
                PrefabUtility.SaveAsPrefabAsset(wrapper, prefabPath);
                var finalBounds = GeometryBounds(model, wrapper.transform);
                if (Mathf.Abs(finalBounds.size.y - manifest.targetHeight) > 0.002f || Mathf.Abs(finalBounds.min.y) > 0.002f)
                    throw new InvalidDataException("La hauteur ou le pivot au sol du prefab GLB n’est pas conforme.");
                Debug.Log($"AeroStadium: {manifest.name} prêt via glTFast ({finalBounds.size.y:F2} m), LocalModels/{manifest.species}/Pokemon.");
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }

        private static Bounds GeometryBounds(GameObject model, Transform root)
        {
            var bounds = new Bounds();
            var found = false;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                var temporary = false;
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    mesh = new Mesh();
                    // Keep the snapshot in renderer-local units. TransformPoint
                    // below applies the hierarchy scale exactly once.
                    skinned.BakeMesh(mesh, true);
                    temporary = true;
                }
                else if (renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                try
                {
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = root.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                finally { if (temporary) UnityEngine.Object.DestroyImmediate(mesh); }
            }
            if (!found) throw new InvalidDataException("Le modèle ne contient aucun sommet.");
            return bounds;
        }

        private static string FileSafeName(string value)
        {
            foreach (var character in Path.GetInvalidFileNameChars()) value = value.Replace(character, '_');
            return value;
        }
    }
}
