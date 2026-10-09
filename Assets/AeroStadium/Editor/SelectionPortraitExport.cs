using System;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AeroStadium.EditorTools
{
    public static class SelectionPortraitExport
    {
        const string Output = "Assets/AeroStadium/Resources/UI/PokemonPortraits";
        const string NativeRoot = "Assets/AeroStadium/Resources/NativeModels";
        const int Size = 256, Layer = 31;
        [Serializable] sealed class Manifest { public int species; public string modelFile; public Entry[] animations; }
        [Serializable] sealed class Entry { public string semantic; }
        [Serializable] sealed class Result { public int species, foregroundPixels, bytes; public string error; }
        [Serializable] sealed class Report { public string utc; public int generated; public bool passed; public Result[] results; }

        [MenuItem("AeroStadium/Exporter les portraits de sélection")]
        public static void Export()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Portrait export requires edit mode.");
            Directory.CreateDirectory(Output); Directory.CreateDirectory("output/ui");
            var results = new List<Result>(); int generated = 0;
            RenderTexture previous = RenderTexture.active, target = null;
            Texture2D pixels = null; Scene preview = default;
            try
            {
                preview = EditorSceneManager.NewPreviewScene();
                var camera = PreviewObject("Selection portrait camera", preview).AddComponent<Camera>();
                camera.enabled = false; camera.cameraType = CameraType.Preview; camera.scene = preview;
                camera.cullingMask = 1 << Layer; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.055f, .09f, .16f, 1f);
                camera.orthographic = true; camera.aspect = 1f; camera.allowHDR = false; camera.allowMSAA = false;
                camera.useOcclusionCulling = false;
                var data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = false; data.renderShadows = false; data.volumeLayerMask = 0;
                Light(preview, "Portrait key", new Vector3(35, -25, 0), 1.35f, Color.white);
                Light(preview, "Portrait fill", new Vector3(20, 145, 0), .75f, Color.white);
                target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.hideFlags = HideFlags.HideAndDontSave; if (!target.Create()) throw new InvalidOperationException("Portrait render target unavailable."); camera.targetTexture = target;
                pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
                pixels.hideFlags = HideFlags.HideAndDontSave;
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("The configured pipeline does not support URP SingleCameraRequest.");
                for (int id = 1; id <= 151; id++)
                {
                    GameObject actor = null; var result = new Result { species = id };
                    try
                    {
                        string folder = NativeRoot + "/" + id.ToString("000");
                        var prefab = Resources.Load<GameObject>("NativeModels/" + id.ToString("000") + "/Pokemon");
                        if (prefab == null) throw new InvalidDataException("Normal native prefab missing.");
                        actor = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(actor, preview);
                        actor.hideFlags = HideFlags.HideAndDontSave;
                        actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, 25, 0));
                        foreach (var node in actor.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = Layer;
                        var native = actor.GetComponent<NativePokemonModel>();
                        if (native == null || native.Species != id) throw new InvalidDataException("Incorrect native species.");
                        foreach (var a in actor.GetComponentsInChildren<Animator>(true)) a.enabled = false;
                        foreach (var g in actor.GetComponentsInChildren<NativeGrounding>(true)) g.enabled = false;
                        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(folder + "/native-manifest.json"));
                        if (manifest == null || manifest.species != id || manifest.animations == null || string.IsNullOrEmpty(manifest.modelFile) || !manifest.modelFile.EndsWith("normal_native.glb", StringComparison.Ordinal))
                            throw new InvalidDataException("Normal native manifest invalid.");
                        int idle = Array.FindIndex(manifest.animations, entry => entry != null && entry.semantic == "idle");
                        if (idle < 0) throw new InvalidDataException("Native idle missing.");
                        Sample(folder + "/NativeClips/" + idle.ToString("000") + ".anim", native.ModelRoot);
                        for (int i = 0; i < manifest.animations.Length; i++)
                            if (manifest.animations[i] != null && manifest.animations[i].semantic == "secondary")
                                Sample(folder + "/NativeSecondaryClips/" + i.ToString("000") + ".anim", native.ModelRoot);
                        foreach (var bridge in actor.GetComponentsInChildren<NativeMaterialBridge>(true)) bridge.Apply();
                        Bounds bounds = WorldBounds(actor, native);
                        Frame(camera, bounds);
                        RenderPipeline.SubmitRenderRequest(camera, request);
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false); pixels.Apply(false, false);
                        Color32[] colors = pixels.GetPixels32(); Color32 backdrop = colors[0];
                        for (int i = 0; i < colors.Length; i++)
                        {
                            Color32 c = colors[i];
                            if (Math.Abs(c.r - backdrop.r) + Math.Abs(c.g - backdrop.g) + Math.Abs(c.b - backdrop.b) > 30)
                                result.foregroundPixels++;
                            colors[i].a = 255;
                        }
                        if (result.foregroundPixels < 128) throw new InvalidDataException("Portrait contains no visible model.");
                        pixels.SetPixels32(colors); pixels.Apply(false, false);
                        byte[] png = pixels.EncodeToPNG();
                        if (png == null || png.Length < 512) throw new InvalidDataException("Empty PNG.");
                        result.bytes = png.Length; File.WriteAllBytes(Output + "/" + id.ToString("000") + ".png", png);
                        generated++;
                    }
                    catch (Exception e) { result.error = e.Message; Debug.LogError("[selection-portrait] #" + id + " " + e.Message); }
                    finally { if (actor != null) Object.DestroyImmediate(actor); results.Add(result); }
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                var report = new Report { utc = DateTime.UtcNow.ToString("o"), generated = generated, passed = generated == 151, results = results.ToArray() };
                File.WriteAllText("output/ui/selection-portraits.json", JsonUtility.ToJson(report, true));
                AssetDatabase.Refresh();
                foreach (var result in results) if (string.IsNullOrEmpty(result.error) && result.bytes > 0)
                {
                    var importer = AssetImporter.GetAtPath(Output + "/" + result.species.ToString("000") + ".png") as TextureImporter;
                    if (importer == null) throw new InvalidDataException("Portrait texture importer unavailable.");
                    importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = false;
                    importer.alphaSource = TextureImporterAlphaSource.None; importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.maxTextureSize = Size; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
            }
            Debug.Log("[selection-portraits] generated=" + generated + " passed=" + (generated == 151));
            if (generated != 151) throw new InvalidDataException("Selection portraits incomplete: " + generated + "/151.");
        }

        static GameObject PreviewObject(string name, Scene scene)
        {
            var obj = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
            SceneManager.MoveGameObjectToScene(obj, scene); return obj;
        }
        static void Light(Scene scene, string name, Vector3 rotation, float intensity, Color color)
        {
            var light = PreviewObject(name, scene).AddComponent<UnityEngine.Light>();
            light.type = LightType.Directional; light.cullingMask = 1 << Layer; light.intensity = intensity;
            light.color = color; light.shadows = LightShadows.None; light.transform.rotation = Quaternion.Euler(rotation);
        }
        static void Sample(string path, Transform root)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null || root == null) throw new InvalidDataException("Native pose missing: " + path);
            clip.SampleAnimation(root.gameObject, 0f);
        }
        static Bounds WorldBounds(GameObject actor, NativePokemonModel native)
        {
            Bounds local = native.RestBounds, world = new Bounds(); bool found = false;
            if (local.size.sqrMagnitude > .000001f)
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = actor.transform.TransformPoint(corner);
                    if (!found) { world = new Bounds(p, Vector3.zero); found = true; } else world.Encapsulate(p);
                }
            else foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
                if (renderer.enabled) { if (!found) { world = renderer.bounds; found = true; } else world.Encapsulate(renderer.bounds); }
            if (!found || world.size.sqrMagnitude < .000001f) throw new InvalidDataException("No model bounds.");
            return world;
        }
        static void Frame(Camera camera, Bounds bounds)
        {
            float distance = bounds.size.magnitude * 2f + 3f;
            camera.transform.position = bounds.center + new Vector3(0, .1f, -1).normalized * distance;
            camera.transform.LookAt(bounds.center); camera.nearClipPlane = .01f; camera.farClipPlane = distance + bounds.size.magnitude * 4f;
            float extent = .01f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 view = camera.transform.InverseTransformPoint(corner); extent = Mathf.Max(extent, Mathf.Abs(view.x), Mathf.Abs(view.y));
            }
            camera.orthographicSize = extent * 1.12f;
        }
    }
}