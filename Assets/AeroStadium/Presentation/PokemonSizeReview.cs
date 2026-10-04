using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>Explicit --size-review mode: shared-camera comparisons and independent world-space body measurements.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PokemonSizeReview : MonoBehaviour
    {
        const float PairSeconds = 20f;
        const float HorizontalLimit = 5.5f;
        const float SampleInterval = .25f;
        readonly int[,] pairs = { { 31, 34 }, { 50, 133 }, { 95, 130 }, { 6, 9 }, { 109, 110 } };
        // Independent acceptance fixtures for the intended presentation policy, in the same order as pairs.
        readonly float[,] expectedHeights = { { 1.55f, 1.55f }, { .75f, .75f }, { 3.3f, 3.3f }, { 1.95f, 1.95f }, { .95f, 1.55f } };
        readonly List<ModelResult> results = new List<ModelResult>();
        readonly List<string> errors = new List<string>();
        readonly List<BodyProbe> active = new List<BodyProbe>();
        ArenaView arena;
        Catalog catalog;
        Text caption;
        Material referenceMaterial;
        bool begun;
        float nextSample;
        int completedPairs;

        public void Begin(Catalog source, ArenaView arenaView)
        {
            if (begun) return;
            begun = true; catalog = source; arena = arenaView;
            Application.logMessageReceived += OnLog;
            Time.timeScale = 1f;
            BuildReference();
            BuildCaption();
            StartCoroutine(Review());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (referenceMaterial != null) Destroy(referenceMaterial);
        }
        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !errors.Contains(message)) errors.Add(message);
        }
        void Error(string message)
        {
            string full = "[size-review-error] " + message;
            if (errors.Contains(full)) return;
            errors.Add(full); Debug.LogError(full);
        }

        void LateUpdate()
        {
            if (Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + SampleInterval;
            foreach (BodyProbe probe in active)
                try { probe.Sample(arena.ArenaCamera, Error); }
                catch (Exception exception) { Error(probe.Result.species.ToString("000") + ": " + exception.Message); }
        }

        IEnumerator Review()
        {
            for (int pair = 0; pair < pairs.GetLength(0); pair++)
            {
                active.Clear();
                arena.ShowPokemon(0, pairs[pair, 0]);
                arena.ShowPokemon(1, pairs[pair, 1]);
                // Use one fixed combat camera throughout, so relative sizes remain visible.
                Camera camera = arena.ArenaCamera;
                camera.transform.position = new Vector3(-.3f, 7.4f, -15.5f);
                camera.transform.LookAt(new Vector3(0f, 1.25f, 0f));
                camera.fieldOfView = 43f;
                yield return null;
                yield return null;
                var drivers = new PokemonAnimationDriver[2];
                for (int side = 0; side < 2; side++)
                {
                    int id = pairs[pair, side];
                    try
                    {
                        GameObject actor = GameObject.Find("Pokemon_" + side + "_" + id);
                        if (actor == null) throw new InvalidDataException("Acteur absent.");
                        var native = actor.GetComponent<NativePokemonModel>();
                        drivers[side] = actor.GetComponent<PokemonAnimationDriver>();
                        if (native == null || drivers[side] == null) throw new InvalidDataException("Modèle natif ou pilote absent.");
                        drivers[side].PlayIdle();
                        var probe = new BodyProbe(actor.transform, native, catalog.GetSpecies(id), expectedHeights[pair, side]);
                        results.Add(probe.Result); active.Add(probe);
                        probe.CheckReference(Error);
                    }
                    catch (Exception exception) { Error(id.ToString("000") + ": " + exception.Message); }
                }
                nextSample = Time.unscaledTime;
                float started = Time.unscaledTime;
                int previousPhase = -1;
                while (Time.unscaledTime - started < PairSeconds)
                {
                    float elapsed = Time.unscaledTime - started;
                    int phase = elapsed < 4f ? 0 : elapsed < 8f ? 1 : elapsed < 12f ? 2 : elapsed < 16f ? 3 : elapsed < 18f ? 4 : 5;
                    if (phase != previousPhase)
                    {
                        previousPhase = phase;
                        string label = PhaseLabel(phase);
                        foreach (BodyProbe probe in active) probe.BeginPhase(label);
                        for (int side = 0; side < drivers.Length; side++)
                        {
                            PokemonAnimationDriver driver = drivers[side];
                            if (driver == null) continue;
                            float duration = phase == 0 || phase == 5 ? driver.PlayIdle() : phase == 1 ? driver.PlayAttack(false)
                                : phase == 2 ? driver.PlayAttack(true) : phase == 3 ? driver.PlayDamage() : driver.PlayFaint();
                            if (duration <= 0f) Error(pairs[pair, side].ToString("000") + ": animation absente pour " + label + ".");
                        }
                        caption.text = catalog.GetSpecies(pairs[pair, 0]).name.ToUpperInvariant() + "  /  "
                            + catalog.GetSpecies(pairs[pair, 1]).name.ToUpperInvariant()
                            + "     ·     " + label + "\nRepère central : 1 m     ·     Même point de vue pour les deux Pokémon";
                    }
                    yield return null;
                }
                foreach (BodyProbe probe in active)
                {
                    if (probe.Result.samples == 0) Error(probe.Result.species.ToString("000") + ": aucun échantillon de corps.");
                    foreach (PhaseResult phase in probe.Result.phases)
                        if (phase.samples + phase.invisibleSamples == 0) Error(probe.Result.species.ToString("000") + ": aucun échantillon pour " + phase.phase + ".");
                    Debug.Log("[size-review-model] species=" + probe.Result.species + " sourceHeight=" + probe.Result.pokedexHeight.ToString("F3")
                        + " referenceWorldHeight=" + probe.Result.referenceWorldHeight.ToString("F3") + " actualInitialWorldHeight="
                        + probe.Result.initialWorldHeight.ToString("F3") + " samples=" + probe.Result.samples
                        + " invisibleSamples=" + probe.Result.invisibleSamples + " normalizationStable=" + probe.Result.normalizationStable
                        + " cameraClippedSamples=" + probe.Result.cameraClippedSamples);
                }
                completedPairs++;
            }
            active.Clear();
            string output = Path.Combine(Application.persistentDataPath, "size-review-world-bounds.json");
            string[] args = Environment.GetCommandLineArgs();
            int outputAt = Array.IndexOf(args, "--size-review-report");
            if (outputAt >= 0 && outputAt + 1 < args.Length) output = args[outputAt + 1];
            try
            {
                File.WriteAllText(output, JsonUtility.ToJson(new Report { utc = DateTime.UtcNow.ToString("o"), pairs = completedPairs,
                    passed = errors.Count == 0 && results.Count == 10 && completedPairs == 5, models = results.ToArray(), errors = errors.ToArray() }, true));
                Debug.Log("[size-review-report] " + output);
            }
            catch (Exception exception) { Error("Rapport impossible : " + exception.Message); }
            bool passed = errors.Count == 0 && results.Count == 10 && completedPairs == 5;
            Debug.Log("[size-review-result] pairs=" + completedPairs + " models=" + results.Count + " errors=" + errors.Count + " passed=" + passed);
            Application.Quit(passed ? 0 : 1);
        }

        static string PhaseLabel(int phase) => phase == 0 ? "Repos" : phase == 1 ? "Attaques physiques"
            : phase == 2 ? "Attaques spéciales" : phase == 3 ? "Réactions aux impacts" : phase == 4 ? "KO" : "Retour au repos";

        void BuildReference()
        {
            Material template = Resources.Load<Material>("Materials/ArenaLit");
            if (template == null) { Error("Matériau du repère absent."); return; }
            referenceMaterial = new Material(template); referenceMaterial.SetColor("_BaseColor", new Color(.95f, .86f, .45f));
            ReferencePart("Size reference 1 metre", new Vector3(0f, .5f, 0f), new Vector3(.035f, 1f, .035f), referenceMaterial);
            for (int tick = 0; tick <= 4; tick++)
                ReferencePart("Size reference tick " + tick, new Vector3(0f, tick * .25f, 0f), new Vector3(tick % 2 == 0 ? .3f : .16f, .025f, .035f), referenceMaterial);
        }
        void ReferencePart(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(transform, false); part.transform.position = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
        }
        void BuildCaption()
        {
            var canvas = new GameObject("Size comparison", typeof(Canvas), typeof(CanvasScaler));
            canvas.transform.SetParent(transform, false); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            var strip = new GameObject("Comparison caption", typeof(RectTransform), typeof(Image)); strip.transform.SetParent(canvas.transform, false);
            var rect = strip.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(.5f, 0f); rect.sizeDelta = new Vector2(0f, 88f);
            strip.GetComponent<Image>().color = new Color(.03f, .06f, .12f, .94f);
            var label = new GameObject("Pokemon comparison names", typeof(RectTransform), typeof(Text)); label.transform.SetParent(strip.transform, false);
            var labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(24f, 8f); labelRect.offsetMax = new Vector2(-24f, -8f);
            caption = label.GetComponent<Text>(); caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); caption.fontSize = 22;
            caption.alignment = TextAnchor.MiddleCenter; caption.color = new Color(1f, .86f, .52f);
        }

        [Serializable] sealed class Report
        {
            public int schemaVersion = 1, pairs;
            public string utc;
            public string measurement = "Indexed visible NativeLayeredLit body vertices; bone.localToWorldMatrix * bindpose * weights; shared combat camera; initial runtime Idle height tolerance 10 percent or 2 cm; Renderer.bounds and FX excluded";
            public bool passed;
            public ModelResult[] models;
            public string[] errors;
        }
        [Serializable] sealed class ModelResult
        {
            public int species, bodyRenderers, bodyVertices, samples, invisibleSamples, cameraClippedSamples;
            public string name;
            public float pokedexHeight, bucketHeight, expectedWorldHeight, referenceWorldHeight, initialWorldHeight, initialHeightError;
            public float maxNormalizationPositionDrift, maxNormalizationScaleDrift, maxNormalizationRotationDrift;
            public bool normalizationStable = true;
            public Vector3 actorScale, referenceWorldMinimum, referenceWorldMaximum;
            public List<PhaseResult> phases = new List<PhaseResult>();
        }
        [Serializable] sealed class PhaseResult
        {
            public string phase;
            public int samples, invisibleSamples;
            public float minHeight, maxHeight, minFloor, maxFloor, maxWidth, maxDepth;
        }

        sealed class BodyProbe
        {
            sealed class Geometry
            {
                public Renderer renderer;
                public Vector3[] vertices;
                public int[] indices;
                public BoneWeight[] weights;
                public Transform[] bones;
                public Matrix4x4[] bindposes, worldSkin;
            }
            readonly Transform actor, normalization;
            readonly Vector3 normalPosition, normalScale, actorPosition, actorScale;
            readonly Quaternion normalRotation;
            readonly List<Geometry> geometry = new List<Geometry>();
            PhaseResult phase;
            public readonly ModelResult Result;

            public BodyProbe(Transform root, NativePokemonModel native, SpeciesDefinition species, float bucketHeight)
            {
                actor = root; normalization = actor.Find("NativeNormalization");
                if (normalization == null) throw new InvalidDataException("NativeNormalization absent.");
                normalPosition = normalization.localPosition; normalScale = normalization.localScale; normalRotation = normalization.localRotation;
                actorPosition = actor.position; actorScale = actor.localScale;
                Bounds local = native.RestBounds;
                Bounds world = new Bounds(actor.TransformPoint(local.center), Vector3.zero);
                for (int i = 0; i < 8; i++) world.Encapsulate(actor.TransformPoint(local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
                float horizontal = Mathf.Max(local.size.x, local.size.z);
                float expected = Mathf.Min(bucketHeight, HorizontalLimit * local.size.y / Mathf.Max(.00001f, horizontal));
                Result = new ModelResult { species = species.id, name = species.name, pokedexHeight = species.height, bucketHeight = bucketHeight,
                    expectedWorldHeight = expected, referenceWorldHeight = world.size.y, actorScale = actorScale,
                    referenceWorldMinimum = world.min, referenceWorldMaximum = world.max };
                foreach (Renderer renderer in native.ModelRoot.GetComponentsInChildren<Renderer>(true))
                {
                    var skin = renderer as SkinnedMeshRenderer;
                    Mesh mesh = skin != null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    var used = new HashSet<int>(); Material[] materials = renderer.sharedMaterials;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        if (sub < materials.Length && IsBodyMaterial(materials[sub])) foreach (int index in mesh.GetIndices(sub)) used.Add(index);
                    if (used.Count == 0) continue;
                    if (!mesh.isReadable) throw new InvalidDataException("Corps non lisible : " + renderer.name);
                    var data = new Geometry { renderer = renderer, vertices = mesh.vertices, indices = new int[used.Count] }; used.CopyTo(data.indices);
                    foreach (int index in data.indices) if (index < 0 || index >= data.vertices.Length) throw new InvalidDataException("Index de corps invalide.");
                    if (skin != null)
                    {
                        data.weights = mesh.boneWeights; data.bones = skin.bones; data.bindposes = mesh.bindposes;
                        if (data.weights.Length != data.vertices.Length || data.bones.Length != data.bindposes.Length) throw new InvalidDataException("Skin de corps invalide.");
                        data.worldSkin = new Matrix4x4[data.bones.Length];
                    }
                    geometry.Add(data); Result.bodyVertices += data.indices.Length;
                }
                Result.bodyRenderers = geometry.Count;
                if (geometry.Count == 0) throw new InvalidDataException("Aucune géométrie de corps visible.");
            }
            static bool IsBodyMaterial(Material material) => material != null && material.shader != null
                && material.shader.name == "AeroStadium/NativeLayeredLit" && material.HasProperty("_ColorMask") && material.GetFloat("_ColorMask") != 0f;
            public void CheckReference(Action<string> error)
            {
                string id = Result.species.ToString("000");
                if (Mathf.Abs(Result.referenceWorldHeight - Result.expectedWorldHeight) > .002f) error(id + ": hauteur de présentation inattendue.");
                if (!Finite(actorScale.x) || !Finite(actorScale.y) || !Finite(actorScale.z)
                    || Mathf.Abs(actorScale.x - actorScale.y) > .00001f || Mathf.Abs(actorScale.z - actorScale.y) > .00001f || actorScale.y <= 0f)
                    error(id + ": échelle extérieure non uniforme ou invalide.");
                if (Mathf.Abs(Result.referenceWorldMinimum.y) > .002f) error(id + ": sol de référence décalé.");
            }
            public void BeginPhase(string name) { phase = new PhaseResult { phase = name }; Result.phases.Add(phase); }
            public void Sample(Camera camera, Action<string> error)
            {
                if (phase == null) return;
                string id = Result.species.ToString("000");
                Result.maxNormalizationPositionDrift = Mathf.Max(Result.maxNormalizationPositionDrift, Vector3.Distance(normalPosition, normalization.localPosition));
                Result.maxNormalizationScaleDrift = Mathf.Max(Result.maxNormalizationScaleDrift, Vector3.Distance(normalScale, normalization.localScale));
                Result.maxNormalizationRotationDrift = Mathf.Max(Result.maxNormalizationRotationDrift, Quaternion.Angle(normalRotation, normalization.localRotation));
                Result.normalizationStable = Result.maxNormalizationPositionDrift <= .000001f && Result.maxNormalizationScaleDrift <= .000001f
                    && Result.maxNormalizationRotationDrift <= .001f;
                if (!Result.normalizationStable) error(id + ": normalisation modifiée pendant une animation.");
                if (Vector3.Distance(actorPosition, actor.position) > .0001f || Vector3.Distance(actorScale, actor.localScale) > .000001f)
                    error(id + ": déplacement ou échelle extérieure modifiés pendant une animation.");
                Bounds body = new Bounds(); bool found = false;
                foreach (Geometry data in geometry)
                {
                    if (!data.renderer.enabled || data.renderer.forceRenderingOff || !data.renderer.gameObject.activeInHierarchy) continue;
                    if (data.bones != null)
                        for (int bone = 0; bone < data.bones.Length; bone++)
                        {
                            if (data.bones[bone] == null) throw new InvalidDataException("Os de corps absent.");
                            data.worldSkin[bone] = data.bones[bone].localToWorldMatrix * data.bindposes[bone];
                        }
                    foreach (int index in data.indices)
                    {
                        Vector3 vertex = data.vertices[index], point;
                        if (data.bones == null) point = data.renderer.localToWorldMatrix.MultiplyPoint3x4(vertex);
                        else
                        {
                            BoneWeight weight = data.weights[index];
                            point = Weighted(data, vertex, weight.boneIndex0, weight.weight0) + Weighted(data, vertex, weight.boneIndex1, weight.weight1)
                                + Weighted(data, vertex, weight.boneIndex2, weight.weight2) + Weighted(data, vertex, weight.boneIndex3, weight.weight3);
                        }
                        if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) throw new InvalidDataException("Sommet de corps non fini.");
                        if (!found) { body = new Bounds(point, Vector3.zero); found = true; } else body.Encapsulate(point);
                    }
                }
                // A native faint visibility track may intentionally hide the body; retain that observation separately.
                if (!found && phase.phase == "KO") { Result.invisibleSamples++; phase.invisibleSamples++; return; }
                if (!found || body.size.y <= 0f) throw new InvalidDataException("Corps invisible ou hauteur invalide.");
                if (Result.samples == 0)
                {
                    Result.initialWorldHeight = body.size.y; Result.initialHeightError = Mathf.Abs(body.size.y - Result.referenceWorldHeight);
                    if (Result.initialHeightError > Mathf.Max(.02f, Result.referenceWorldHeight * .1f)) error(id + ": hauteur réelle initiale incompatible avec la référence.");
                }
                if (body.min.y < -.01f) error(id + ": corps sous le sol de l'arène.");
                bool clipped = false;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = body.center + Vector3.Scale(body.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 screen = camera.WorldToViewportPoint(point);
                    if (screen.z <= camera.nearClipPlane || screen.x < 0f || screen.x > 1f || screen.y < 0f || screen.y > 1f) clipped = true;
                }
                if (clipped) { Result.cameraClippedSamples++; error(id + ": corps coupé par la caméra commune."); }
                if (phase.samples == 0) { phase.minHeight = phase.maxHeight = body.size.y; phase.minFloor = phase.maxFloor = body.min.y; }
                else { phase.minHeight = Mathf.Min(phase.minHeight, body.size.y); phase.maxHeight = Mathf.Max(phase.maxHeight, body.size.y);
                    phase.minFloor = Mathf.Min(phase.minFloor, body.min.y); phase.maxFloor = Mathf.Max(phase.maxFloor, body.min.y); }
                phase.maxWidth = Mathf.Max(phase.maxWidth, body.size.x); phase.maxDepth = Mathf.Max(phase.maxDepth, body.size.z);
                phase.samples++; Result.samples++;
            }
            static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
            static Vector3 Weighted(Geometry data, Vector3 vertex, int index, float weight)
            {
                if (weight <= 0f) return Vector3.zero;
                if (index < 0 || index >= data.worldSkin.Length) throw new InvalidDataException("Index d'os invalide.");
                return data.worldSkin[index].MultiplyPoint3x4(vertex) * weight;
            }
        }
    }
}
