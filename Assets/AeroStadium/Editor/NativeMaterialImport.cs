using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.EditorTools
{
    public static class NativeMaterialImport
    {
        [Serializable] sealed class Metadata
        {
            public MaterialEntry[] materials;
            public DriverEntry[] drivers;
            public VisibilityEntry[] visibility;
        }
        [Serializable] sealed class MaterialEntry
        {
            public string name, nativeShaderName, baseTexture, layerTexture, normalTexture, normalDecodedTexture;
            public float[] baseScale, baseOffset, baseUV, layerScale, layerOffset, layerUV;
            public float[] normalScale, normalOffset;
            public bool layerEnabled, nativeDepthOnly, depthOnly;
            public int layerUvChannel;
            public FloatProperty[] floatProperties;
            public ColorProperty[] colorProperties;
            public TextureBinding[] textureBindings;
        }
        [Serializable] sealed class FloatProperty { public string name; public float value; }
        [Serializable] sealed class ColorProperty { public string name; public float[] value; }
        [Serializable] sealed class TextureBinding { public string property, path; public float[] scale, offset; public int wrapU, wrapV, filterMode; }
        [Serializable] sealed class DriverEntry
        {
            public string boneName, rendererNodeName, materialName, propertyName;
            public int materialIndex, pType;
            public float[] baseScale;
        }
        [Serializable] sealed class VisibilityEntry
        {
            public string boneName, rendererNodeName;
        }

        public static void Prepare(GameObject model, string metadataPath, string folder)
        {
            if (!File.Exists(metadataPath)) throw new FileNotFoundException("Métadonnées des expressions natives absentes", metadataPath);
            Metadata metadata = JsonUtility.FromJson<Metadata>(File.ReadAllText(metadataPath));
            if (metadata?.materials == null) throw new InvalidDataException("Matériaux natifs invalides");
            Shader shader = Shader.Find("AeroStadium/NativeLayeredLit");
            if (shader == null) throw new InvalidOperationException("Shader natif manquant");
            bool puffSpecies = Path.GetFileName(folder) == "109" || Path.GetFileName(folder) == "110";
            bool gastly = Path.GetFileName(folder) == "092";
            var materials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var entry in metadata.materials)
            {
                bool mask = entry.nativeShaderName == "Mitake/FireMask1st" || entry.nativeShaderName == "Mitake/SmokeMask_MT";
                bool core = entry.nativeShaderName == "Mitake/FireCore1st" || entry.nativeShaderName == "Mitake/MitakeStandardPetrifyFire";
                bool effect = mask || core;
                bool cloud = puffSpecies && entry.nativeShaderName == "Mitake/SmokeMask_MT";
                bool gas = gastly && entry.name == "pm0092_00_00-SmokeCore" && entry.nativeShaderName == "Mitake/FireCore1st";
                Shader selected = cloud ? Shader.Find("AeroStadium/NativeSmokeCloud") : gas ? Shader.Find("AeroStadium/NativeGasSurface")
                    : effect ? Shader.Find(mask ? "AeroStadium/NativeEffectMask" : "AeroStadium/NativeEffectCore") : shader;
                if (selected == null) throw new InvalidOperationException("Shader d'effet natif absent : " + entry.nativeShaderName);
                string safe = new string(entry.name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());
                string path = folder + "/Native_" + safe + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.name = entry.name;
                material.shader = selected;
                foreach (var property in entry.floatProperties ?? Array.Empty<FloatProperty>())
                    if (material.HasProperty(property.name)) material.SetFloat(property.name, property.value);
                foreach (var property in entry.colorProperties ?? Array.Empty<ColorProperty>())
                    if (material.HasProperty(property.name) && property.value?.Length == 4)
                        material.SetColor(property.name, new Color(property.value[0], property.value[1], property.value[2], property.value[3]));
                foreach (var binding in entry.textureBindings ?? Array.Empty<TextureBinding>())
                {
                    if (!material.HasProperty(binding.property)) continue;
                    Texture2D defaultTexture = binding.property == "_FireTex" ? Texture2D.whiteTexture : Texture2D.blackTexture;
                    // Emission is a data mask, even when its owning material is lit.
                    bool colorTexture = !effect && binding.property != "_EmissionMaskTex";
                    material.SetTexture(binding.property, string.IsNullOrEmpty(binding.path) ? defaultTexture : LoadTexture(folder,binding.path,false,false,colorTexture,binding));
                    material.SetTextureScale(binding.property,Vec(binding.scale,Vector2.one));
                    material.SetTextureOffset(binding.property,Vec(binding.offset,Vector2.zero));
                }
                if (effect)
                {
                    if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", SourceFloat(entry,"_CullMode",mask ? 0f : 2f));
                    if (mask)
                    {
                        bool smoke = entry.nativeShaderName == "Mitake/SmokeMask_MT";
                        if (material.HasProperty("_SecondMask")) material.SetFloat("_SecondMask", smoke ? 0f : 1f);
                        material.SetFloat("_NativeSmokeBillboard", smoke ? 1f : 0f);
                    }
                    if (material.HasProperty("_SmokeHidden")) material.SetFloat("_SmokeHidden", puffSpecies && core ? 1f : 0f);
                    if (cloud)
                    {
                        material.SetColor("_BaseColor", new Color(.850006878f,.820407391f,.614557326f,1f));
                        material.SetColor("_LayerColor", new Color(.596955299f,.550061822f,.264090389f,1f));
                        material.SetFloat("_Opacity", .48f); material.SetFloat("_PuffRadiusScale", Path.GetFileName(folder) == "110" ? 1.05f : .70f);
                        material.SetTexture("_CloudAtlas", LoadSmokeTexture("NativeSmokeAtlas.png", TextureWrapMode.Clamp, 1024));
                        material.SetTexture("_CloudFlow", LoadSmokeTexture("NativeSmokeFlow.png", TextureWrapMode.Repeat, 256));
                        material.SetVector("_CloudWorldOffset", Vector4.zero);
                        material.SetFloat("_CloudFlowSpeed", .7f);
                        material.SetFloat("_CloudDetail", .7f);
                    }
                    if (gas)
                    {
                        material.SetFloat("_GasOpacity", .85f);
                        material.SetFloat("_GasDensityFloor", .55f);
                        material.SetFloat("_GasEdgeSoftness", .45f);
                    }
                    material.renderQueue = cloud || gas ? 3000 : mask ? 2001 : 2002;
                    EditorUtility.SetDirty(material);
                    materials.Add(entry.name, material);
                    continue;
                }
                bool depthOnly = entry.nativeDepthOnly || entry.depthOnly;
                material.SetTexture("_BaseMap", depthOnly ? Texture2D.whiteTexture : LoadTexture(folder, entry.baseTexture, settings: Binding(entry,"_Col0Tex")));
                material.SetTextureScale("_BaseMap", Vec(entry.baseScale, Vector2.one));
                material.SetTextureOffset("_BaseMap", Vec(entry.baseOffset, Vector2.zero));
                material.SetVector("_BaseUv", Vec4(entry.baseUV));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Cull", SourceFloat(entry, "_CullMode", 2f));
                material.SetFloat("_Smoothness", .18f);
                material.SetFloat("_ColorMask", depthOnly ? 0f : 15f);
                material.SetFloat("_LayerEnabled", !depthOnly && entry.layerEnabled ? 1f : 0f);
                material.SetFloat("_NormalEnabled", depthOnly || string.IsNullOrEmpty(entry.normalDecodedTexture) ? 0f : 1f);
                if (!depthOnly && !string.IsNullOrEmpty(entry.normalDecodedTexture))
                {
                    material.SetTexture("_NormalMap", LoadTexture(folder, entry.normalDecodedTexture, true, true, settings: Binding(entry,"_NormalMapTex")));
                    material.SetTextureScale("_NormalMap", Vec(entry.normalScale, Vector2.one));
                    material.SetTextureOffset("_NormalMap", Vec(entry.normalOffset, Vector2.zero));
                }
                if (!depthOnly && entry.layerEnabled)
                {
                    material.SetTexture("_LayerMap", LoadTexture(folder, entry.layerTexture, settings: Binding(entry,"_L1Col0Tex")));
                    material.SetTextureScale("_LayerMap", Vec(entry.layerScale, Vector2.one));
                    material.SetTextureOffset("_LayerMap", Vec(entry.layerOffset, Vector2.zero));
                    material.SetVector("_LayerUv", Vec4(entry.layerUV));
                    material.SetFloat("_LayerUvChannel", entry.layerUvChannel);
                }
                EditorUtility.SetDirty(material);
                materials.Add(entry.name, material);
            }
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) throw new InvalidDataException("Maillage natif absent : " + renderer.name);
                Material[] current = renderer.sharedMaterials;
                for (int i = 0; i < current.Length; i++)
                {
                    string name = current[i]?.name?.Replace(" (Instance)", "");
                    if (name != null && materials.TryGetValue(name, out var material)) current[i] = material;
                    else throw new InvalidDataException("Matériau natif sans correspondance : " + name);
                }
                if (!mesh.HasVertexAttribute(VertexAttribute.TexCoord2) && current.Any(m => !m.HasProperty("_ColorMask") || m.GetFloat("_ColorMask") != 0f))
                    throw new InvalidDataException("UV natifs absents du maillage " + renderer.name);
                renderer.sharedMaterials = current;
            }
            var transforms = model.GetComponentsInChildren<Transform>(true);
            Transform Bone(string name) => transforms.Single(t => t.name == name);
            Renderer RendererFor(string name, string materialName = null) => renderers.Single(r =>
                (r.name == name || r.name.StartsWith(name + "_", StringComparison.Ordinal)) &&
                (materialName == null || r.sharedMaterials.Any(m => m != null && m.name == materialName)));
            var uv = (metadata.drivers ?? Array.Empty<DriverEntry>()).Select(d =>
            {
                Renderer renderer = RendererFor(d.rendererNodeName, d.materialName);
                int index = Array.FindIndex(renderer.sharedMaterials, m => m != null && m.name == d.materialName);
                if (index < 0) throw new InvalidDataException("Slot de matériau natif absent");
                Material material = renderer.sharedMaterials[index];
                bool smoke = metadata.materials.Single(m => m.name == d.materialName).nativeShaderName == "Mitake/SmokeMask_MT";
                string target = d.pType == 0 ? "_" + d.propertyName : d.pType == 1
                    ? smoke ? "_Mask0UVTranslateU" : "_" + d.propertyName + "U"
                    : d.pType == 3 || d.pType == 7 || d.pType == 8 ? "_LayerMap" : "_BaseMap";
                if (!material.HasProperty(target)) throw new InvalidDataException("Commande native absente du shader : " + d.materialName + "." + target);
                if (d.pType == 1 && !smoke && !material.HasProperty("_" + d.propertyName + "V"))
                    throw new InvalidDataException("Commande UV native incomplète : " + d.materialName + "." + d.propertyName);
                return new NativeMaterialBridge.UvControl { bone = Bone(d.boneName), renderer = renderer,
                    materialIndex = index, propertyType = d.pType, propertyName = d.propertyName,
                    baseScale = Vec(d.baseScale, Vector2.one), smokeMask = smoke };
            }).ToArray();
            var visibility = (metadata.visibility ?? Array.Empty<VisibilityEntry>()).Select(v =>
                new NativeMaterialBridge.VisibilityControl { bone = Bone(v.boneName), renderer = RendererFor(v.rendererNodeName) }).ToArray();
            model.AddComponent<NativeMaterialBridge>().Configure(uv, visibility);
        }
        static readonly Dictionary<string, Texture2D> SmokeTextures = new Dictionary<string, Texture2D>();
        static Texture2D LoadSmokeTexture(string name, TextureWrapMode wrap, int maximumSize)
        {
            string path = "Assets/AeroStadium/Resources/NativeModels/SharedSmoke/" + name;
            if (SmokeTextures.TryGetValue(path, out Texture2D saved) && saved != null) return saved;
            if (!File.Exists(path)) throw new FileNotFoundException("Texture locale de fumée absente", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Import de la fumée impossible : " + path);
            // RGBA encodes density, normals and shadow, rather than color/opacity.
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = maximumSize;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new InvalidDataException("Données de fumée absentes : " + path);
            SmokeTextures[path] = texture;
            return texture;
        }
        static Vector2 Vec(float[] a, Vector2 fallback) => a != null && a.Length == 2 ? new Vector2(a[0], a[1]) : fallback;
        static float SourceFloat(MaterialEntry entry, string name, float fallback) => entry.floatProperties?.FirstOrDefault(p => p.name == name)?.value ?? fallback;
        static TextureBinding Binding(MaterialEntry entry, string property) => entry.textureBindings?.FirstOrDefault(b => b.property == property);
        static Vector4 Vec4(float[] a) { Vector2 v = Vec(a, Vector2.zero); return new Vector4(v.x, v.y, 0, 0); }
        static Texture2D LoadTexture(string folder, string file, bool normalMap = false, bool flipGreen = false, bool srgb = true, TextureBinding settings = null)
        {
            if (string.IsNullOrEmpty(file)) throw new InvalidDataException("Texture native sans chemin");
            string path = folder + "/textures/" + Path.GetFileName(file);
            if (!Path.HasExtension(path)) path += ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Texture native absente", path);
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap && srgb;
            importer.flipGreenChannel = flipGreen;
            importer.wrapModeU = (TextureWrapMode)(settings?.wrapU ?? 0);
            importer.wrapModeV = (TextureWrapMode)(settings?.wrapV ?? 0);
            importer.filterMode = (FilterMode)(settings?.filterMode ?? 1);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
