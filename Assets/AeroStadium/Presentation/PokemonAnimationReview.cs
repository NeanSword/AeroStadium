using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>Explicit developer launch mode for a visible anatomical review, never shown in normal menus.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class PokemonAnimationReview : MonoBehaviour
    {
        ArenaView arena;
        Catalog catalog;
        Text caption;
        int errors, reviewed;
        readonly List<float> frameTimes = new List<float>();
        readonly List<NativeBoundsResult> nativeBounds = new List<NativeBoundsResult>();
        NativeBoundsProbe probe;
        float nextBoundsSample;
        public void Begin(ArenaView arenaView, Catalog source)
        {
            arena = arenaView; catalog = source;
            Application.logMessageReceived += OnLog;
            var canvas = new GameObject("Animation review", typeof(Canvas), typeof(CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            var strip = new GameObject("Review caption", typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(canvas.transform, false);
            var rect = strip.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 0); rect.anchorMax = new Vector2(1, 0);
            rect.pivot = new Vector2(.5f, 0); rect.sizeDelta = new Vector2(0, 75);
            strip.GetComponent<Image>().color = new Color(.03f, .06f, .12f, .94f);
            var label = new GameObject("Species and motion", typeof(RectTransform), typeof(Text)); label.transform.SetParent(strip.transform, false);
            var labelRect = label.GetComponent<RectTransform>(); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(36, 8); labelRect.offsetMax = new Vector2(-36, -8);
            caption = label.GetComponent<Text>(); caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); caption.fontSize = 23;
            caption.alignment = TextAnchor.MiddleCenter; caption.color = new Color(1f, .86f, .52f);
            StartCoroutine(Review());
        }

        void Update() { if (Time.unscaledDeltaTime > 0f) frameTimes.Add(Time.unscaledDeltaTime); }
        void LateUpdate()
        {
            if (probe == null || Time.unscaledTime < nextBoundsSample) return;
            nextBoundsSample = Time.unscaledTime + .1f;
            try { probe.Sample(); }
            catch (Exception exception) { errors++; Debug.LogError("[native-review-bounds-error] " + exception.Message); probe = null; }
        }
        void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        IEnumerator Review()
        {
            string[] args = Environment.GetCommandLineArgs();
            int repeats = 1;
            int repeatIndex = Array.IndexOf(args, "--review-action-repeats");
            if (repeatIndex >= 0 && repeatIndex + 1 < args.Length && int.TryParse(args[repeatIndex + 1], out int requestedRepeats))
                repeats = Mathf.Clamp(requestedRepeats, 1, 10);
            bool reviewFaint = Array.IndexOf(args, "--review-faint") >= 0;
            var species = new List<int> { 6, 18, 133, 25, 130, 94, 9, 65, 42, 77, 131, 143 };
            int index = Array.IndexOf(args, "--review-species");
            if (index >= 0 && index + 1 < args.Length)
            {
                species.Clear();
                foreach (string id in args[index + 1].Split(',')) if (int.TryParse(id, out int value) && value >= 1 && value <= 151) species.Add(value);
            }
            foreach (int id in species)
            {
                arena.ShowPokemon(0, id, true);
                yield return null;
                var actor = GameObject.Find("Pokemon_0_" + id);
                var motion = actor.GetComponent<CinematicMotion>();
                var native = actor.GetComponent<NativePokemonModel>();
                var driver = actor.GetComponent<PokemonAnimationDriver>();
                if (native != null)
                {
                    try { probe = new NativeBoundsProbe(actor.transform, native); }
                    catch (Exception exception) { errors++; Debug.LogError("[native-review-bounds-error] " + exception.Message); }
                }
                var profile = PokemonMotionProfile.ForSpecies(id);
                float actionDuration = native != null
                    ? native.GetActionDuration(PokemonMotionAction.Special) + native.GetActionDuration(PokemonMotionAction.Physical) + native.GetActionDuration(PokemonMotionAction.Damage)
                    : profile.AttackDuration * 2f + profile.HitDuration;
                float duration = 8f + repeats * (actionDuration + .65f) + 2f;
                if (reviewFaint) duration += (native != null ? native.GetActionDuration(PokemonMotionAction.Faint) : profile.HitDuration) + 1.5f;
                var shot = StartCoroutine(arena.PlayShowcase(0, duration));
                driver.PlayIdle();
                probe?.BeginPhase("idle");
                caption.text = $"{id:000} · {catalog.GetSpecies(id).name.ToUpperInvariant()}    —    Cycle complet : attention, déplacement, expression, retour au repos";
                yield return new WaitForSeconds(8f);
                for (int cycle = 0; cycle < repeats; cycle++)
                {
                    caption.text = $"{catalog.GetSpecies(id).name.ToUpperInvariant()}    —    Préparation et lancement d’une attaque spéciale ({cycle + 1}/{repeats})";
                    probe?.BeginPhase("special-" + (cycle + 1));
                    yield return new WaitForSeconds(driver.PlayAttack(true) + .35f);
                    caption.text = $"{catalog.GetSpecies(id).name.ToUpperInvariant()}    —    Geste physique puis réaction à un impact ({cycle + 1}/{repeats})";
                    probe?.BeginPhase("physical-" + (cycle + 1));
                    yield return new WaitForSeconds(driver.PlayAttack(false) + .15f);
                    probe?.BeginPhase("damage-" + (cycle + 1));
                    yield return new WaitForSeconds(driver.PlayDamage() + .15f);
                }
                if (reviewFaint)
                {
                    caption.text = $"{catalog.GetSpecies(id).name.ToUpperInvariant()}    —    Animation de KO puis pose finale tenue";
                    probe?.BeginPhase("faint");
                    yield return new WaitForSeconds(driver.PlayFaint() + 1.5f);
                }
                yield return new WaitForSeconds(.5f);
                StopCoroutine(shot);
                bool passed = native != null ? native.IsPlayingNative && native.ClipCount > 0 && native.ModelHeight > 0f
                    : motion != null && motion.UsingOriginalMotion && (motion.DrivenJointCount > 0 || motion.RigidMeshCount > 0);
                if (!passed) errors++;
                if (probe != null)
                {
                    var report = probe.Finish(); nativeBounds.Add(report);
                    if (!report.normalizationStable || !report.validSamples) { passed = false; errors++; }
                    foreach (var phase in report.phases)
                        Debug.Log($"[native-review-bounds] species={id} phase={phase.phase} samples={phase.samples} minHeight={phase.minHeight:F5} maxHeight={phase.maxHeight:F5} minFloor={phase.minFloor:F5} maxFloor={phase.maxFloor:F5} normalizationStable={report.normalizationStable}");
                    probe = null;
                }
                Debug.Log($"[animation-review-model] species={id} native={native != null} clips={(native != null ? native.ClipCount : 0)} joints={(motion != null ? motion.DrivenJointCount : 0)} rigidMeshes={(motion != null ? motion.RigidMeshCount : 0)} feet={(motion != null ? motion.GroundedFeet : 0)} referenceHeight={(native != null ? native.RestBounds.size.y : motion != null ? motion.ModelHeight : 0f):F3} passed={passed}");
                reviewed++;
            }
            frameTimes.Sort();
            string boundsPath = Path.Combine(Application.persistentDataPath, "animation-review-native-bounds.json");
            File.WriteAllText(boundsPath, JsonUtility.ToJson(new NativeBoundsReport { models = nativeBounds.ToArray() }, true));
            Debug.Log("[native-review-bounds-report] " + boundsPath);
            float median = frameTimes.Count > 0 ? frameTimes[frameTimes.Count / 2] : 0f;
            float p95 = frameTimes.Count > 0 ? frameTimes[Mathf.Min(frameTimes.Count - 1, Mathf.FloorToInt(frameTimes.Count * .95f))] : 0f;
            Debug.Log($"[animation-review-result] reviewed={reviewed} errors={errors} medianFrameMs={median * 1000f:F2} p95FrameMs={p95 * 1000f:F2} passed={errors == 0 && reviewed == species.Count}");
            Application.Quit(errors == 0 && reviewed == species.Count ? 0 : 1);
        }

        [Serializable] sealed class NativeBoundsReport { public int schemaVersion = 1; public NativeBoundsResult[] models; }
        [Serializable] sealed class NativeBoundsResult
        {
            public int species;
            public float targetHeight, importedReferenceHeight, maxPositionDrift, maxRotationDriftDegrees, maxScaleDrift;
            public bool normalizationStable = true, validSamples = true;
            public NativeBoundsPhase[] phases;
        }
        [Serializable] sealed class NativeBoundsPhase
        {
            public string phase;
            public int samples;
            public float minHeight, maxHeight, minFloor, maxFloor, maxWidth, maxDepth;
        }
        sealed class NativeBoundsProbe
        {
            sealed class Geometry
            {
                public Renderer renderer;
                public Vector3[] vertices;
                public BoneWeight[] weights;
                public Matrix4x4[] bindposes;
                public Transform[] bones;
            }
            readonly Transform actor, normalization;
            readonly Vector3 initialPosition, initialScale;
            readonly Quaternion initialRotation;
            readonly List<Geometry> geometry = new List<Geometry>();
            readonly List<NativeBoundsPhase> phases = new List<NativeBoundsPhase>();
            readonly NativeBoundsResult result;
            NativeBoundsPhase current;
            public NativeBoundsProbe(Transform root, NativePokemonModel model)
            {
                actor = root; normalization = root.Find("NativeNormalization");
                if (normalization == null) throw new InvalidDataException("NativeNormalization absent.");
                initialPosition = normalization.localPosition; initialRotation = normalization.localRotation; initialScale = normalization.localScale;
                result = new NativeBoundsResult { species = model.Species, targetHeight = model.ModelHeight, importedReferenceHeight = model.RestBounds.size.y };
                foreach (var renderer in model.ModelRoot.GetComponentsInChildren<Renderer>(true))
                {
                    var skin = renderer as SkinnedMeshRenderer;
                    Mesh mesh = skin != null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh == null) continue;
                    if (!mesh.isReadable) throw new InvalidDataException("Maillage de revue non lisible : " + renderer.name);
                    var data = new Geometry { renderer = renderer, vertices = mesh.vertices };
                    if (skin != null)
                    {
                        data.weights = mesh.boneWeights; data.bindposes = mesh.bindposes; data.bones = skin.bones;
                        if (data.weights.Length != data.vertices.Length || data.bindposes.Length != data.bones.Length)
                            throw new InvalidDataException("Skin de revue invalide : " + renderer.name);
                    }
                    geometry.Add(data);
                }
                if (geometry.Count == 0) throw new InvalidDataException("Géométrie de revue absente.");
            }
            public void BeginPhase(string name) { current = new NativeBoundsPhase { phase = name }; phases.Add(current); }
            public void Sample()
            {
                if (current == null) return;
                result.maxPositionDrift = Mathf.Max(result.maxPositionDrift, Vector3.Distance(initialPosition, normalization.localPosition));
                result.maxRotationDriftDegrees = Mathf.Max(result.maxRotationDriftDegrees, Quaternion.Angle(initialRotation, normalization.localRotation));
                result.maxScaleDrift = Mathf.Max(result.maxScaleDrift, Vector3.Distance(initialScale, normalization.localScale));
                Bounds bounds = new Bounds(); bool found = false;
                void Add(Vector3 point)
                {
                    if (float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) || float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z))
                        throw new InvalidDataException("Sommet animé non fini.");
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
                foreach (var data in geometry)
                {
                    if (!data.renderer.enabled || !data.renderer.gameObject.activeInHierarchy) continue;
                    if (data.bones == null)
                    {
                        Matrix4x4 matrix = RelativeMatrix(data.renderer.transform);
                        foreach (Vector3 point in data.vertices) Add(matrix.MultiplyPoint3x4(point));
                        continue;
                    }
                    var matrices = new Matrix4x4[data.bones.Length];
                    for (int i = 0; i < matrices.Length; i++) matrices[i] = RelativeMatrix(data.bones[i]) * data.bindposes[i];
                    for (int i = 0; i < data.vertices.Length; i++)
                    {
                        BoneWeight w = data.weights[i]; Vector3 vertex = data.vertices[i];
                        Vector3 Skin(int index, float weight) => weight <= 0f ? Vector3.zero : matrices[index].MultiplyPoint3x4(vertex) * weight;
                        Add(Skin(w.boneIndex0, w.weight0) + Skin(w.boneIndex1, w.weight1) + Skin(w.boneIndex2, w.weight2) + Skin(w.boneIndex3, w.weight3));
                    }
                }
                if (!found || bounds.size.y <= 0f) throw new InvalidDataException("Dimensions animées invalides.");
                float height = bounds.size.y, floor = bounds.min.y;
                if (current.samples == 0) { current.minHeight = current.maxHeight = height; current.minFloor = current.maxFloor = floor; }
                else
                {
                    current.minHeight = Mathf.Min(current.minHeight, height); current.maxHeight = Mathf.Max(current.maxHeight, height);
                    current.minFloor = Mathf.Min(current.minFloor, floor); current.maxFloor = Mathf.Max(current.maxFloor, floor);
                }
                current.maxWidth = Mathf.Max(current.maxWidth, bounds.size.x); current.maxDepth = Mathf.Max(current.maxDepth, bounds.size.z);
                current.samples++;
            }
            Matrix4x4 RelativeMatrix(Transform node)
            {
                if (node == actor) return Matrix4x4.identity;
                if (node == null || node.parent == null) throw new InvalidDataException("Os de revue hors modèle.");
                return RelativeMatrix(node.parent) * Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale);
            }
            public NativeBoundsResult Finish()
            {
                result.normalizationStable = result.maxPositionDrift <= .000001f && result.maxScaleDrift <= .000001f && result.maxRotationDriftDegrees <= .001f;
                foreach (var phase in phases) if (phase.samples == 0) result.validSamples = false;
                result.phases = phases.ToArray(); return result;
            }
        }
    }
}
