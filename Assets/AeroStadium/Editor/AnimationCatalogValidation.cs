using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AeroStadium.EditorTools
{
    /// <summary>Checks authored animation invariants against every local Kanto prefab.</summary>
    public static class AnimationCatalogValidation
    {
        [Serializable] private sealed class CatalogReport
        {
            public string utc;
            public int expectedModels = 151;
            public int checkedModels;
            public int articulatedModels;
            public int rigidModels;
            public int authoredJoints;
            public int rigidDeformedMeshes;
            public int runtimeArticulatedModels;
            public bool passed;
            public ModelReport[] models;
            public string[] errors;
        }

        [Serializable] private sealed class ModelReport
        {
            public int species;
            public string name;
            public float targetHeight;
            public float staticHeight;
            public float idleHeight;
            public float staticMinimumY;
            public float minimumSampledY;
            public bool heightAgreement;
            public int skinRenderers;
            public int runtimeSkinRenderers;
            public int weightedBones;
            public int changedTransforms;
            public int changedVertices;
            public int idleChangedTransforms;
            public int idleChangedVertices;
            public int authoredJoints;
            public int deformedMeshes;
            public int groundedFeet;
            public int sampledActions;
            public float maximumRotationDegrees;
            public float maximumVertexDisplacement;
            public float maximumFootDrift;
            public float minimumFootFloorOffset;
            public float maximumRigidBaseDrift;
            public float maximumGeometryExtentRatio;
            public float maximumFloorProbeError;
            public bool deterministic;
            public bool finite;
            public bool rootPreserved;
            public bool sourceGeometryPreserved;
            public bool arbitraryPlacementValid;
            public string error;
        }

        [Serializable] private sealed class LocalManifest
        {
            public int species;
            public string name;
            public float targetHeight;
        }

        private sealed class Pose
        {
            public readonly Transform[] transforms;
            public readonly Vector3[] positions;
            public readonly Quaternion[] rotations;
            public readonly Vector3[] scales;
            public readonly MeshFilter[] filters;
            public readonly Vector3[][] vertices;

            public Pose(GameObject root)
            {
                transforms = root.GetComponentsInChildren<Transform>(true);
                positions = transforms.Select(t => t.localPosition).ToArray();
                rotations = transforms.Select(t => t.localRotation).ToArray();
                scales = transforms.Select(t => t.localScale).ToArray();
                filters = root.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.TryGetComponent<Renderer>(out var renderer) && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .ToArray();
                vertices = filters.Select(f => f.sharedMesh == null ? Array.Empty<Vector3>() : f.sharedMesh.vertices).ToArray();
            }
        }

        [MenuItem("AeroStadium/Valider les animations originales Kanto")]
        public static void ValidateAll()
        {
            var models = new List<ModelReport>();
            var errors = new List<string>();
            for (var species = 1; species <= 151; species++)
            {
                var model = ValidateModel(species);
                models.Add(model);
                if (!string.IsNullOrEmpty(model.error)) errors.Add($"#{species:000}: {model.error}");
            }

            var report = new CatalogReport
            {
                utc = DateTime.UtcNow.ToString("O"),
                checkedModels = models.Count,
                articulatedModels = models.Count(m => m.skinRenderers > 0),
                rigidModels = models.Count(m => m.skinRenderers == 0),
                passed = errors.Count == 0 && models.Count == 151,
                models = models.ToArray(),
                errors = errors.ToArray()
            };
            report.authoredJoints = models.Sum(m => m.authoredJoints);
            report.rigidDeformedMeshes = models.Sum(m => m.deformedMeshes);
            report.runtimeArticulatedModels = models.Count(m => m.runtimeSkinRenderers > 0);
            Directory.CreateDirectory("output/animations");
            File.WriteAllText("output/animations/gen1-authored-animation-validation.json", JsonUtility.ToJson(report, true));
            Debug.Log($"[authored-animation-catalog] checked={report.checkedModels} articulated={report.articulatedModels} rigid={report.rigidModels} errors={errors.Count} passed={report.passed}");
            if (!report.passed) throw new InvalidDataException(string.Join("\n", errors));
        }

        private static ModelReport ValidateModel(int species)
        {
            var result = new ModelReport { species = species };
            GameObject instance = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/AeroStadium/Resources/LocalModels/{species}/Pokemon.prefab");
                if (prefab == null) throw new InvalidDataException("Prefab absent.");
                var manifest = JsonUtility.FromJson<LocalManifest>(File.ReadAllText($"Assets/AeroStadium/Resources/LocalModels/{species}/manifest.json"));
                result.name = manifest.name;
                result.targetHeight = manifest.targetHeight;
                // Snapshots read-only source meshes. Sampling a clone must never modify an imported asset.
                var sourceFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
                var sourceMeshes = sourceFilters.Select(f => f.sharedMesh)
                    .Concat(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => s.sharedMesh))
                    .Where(m => m != null).Distinct().ToArray();
                var sourceVertices = sourceMeshes.Select(m => m.vertices).ToArray();
                instance = Object.Instantiate(prefab);
                instance.name = $"AnimationValidation_{species:000}";
                instance.hideFlags = HideFlags.HideAndDontSave;
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                var staticBounds = GeometryBounds(instance);
                result.staticHeight = staticBounds.size.y;
                result.staticMinimumY = staticBounds.min.y;
                result.minimumSampledY = staticBounds.min.y;
                result.heightAgreement = Mathf.Abs(result.staticHeight - result.targetHeight) <= Mathf.Max(.005f, result.targetHeight * .05f);
                result.skinRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                result.weightedBones = WeightedBoneCount(instance);

                var motion = instance.GetComponent<CinematicMotion>() ?? instance.AddComponent<CinematicMotion>();
                motion.Configure(species);
                result.authoredJoints = motion.DrivenJointCount;
                result.deformedMeshes = motion.RigidMeshCount;
                result.groundedFeet = motion.GroundedFeet;
                result.runtimeSkinRenderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Count(r => r.enabled && r.gameObject.activeInHierarchy);
                var skinFloorProbe = result.runtimeSkinRenderers > 0 ? new SkinnedFloorProbe(instance, false) : null;
                if (!motion.UsingOriginalMotion) throw new InvalidDataException("Les animations originales ne sont pas actives.");

                var rootPosition = instance.transform.localPosition;
                var rootRotation = instance.transform.localRotation;
                var rootScale = instance.transform.localScale;
                motion.SamplePose(0f);
                var initial = new Pose(instance);
                var initialBounds = GeometryBounds(instance);
                result.idleHeight = initialBounds.size.y;
                var plantedFeet = GetPlantedFeet(motion);
                var footRestTargets = GetFootRestTargets(motion);
                var baseVertices = new List<BaseVertex>();
                if (result.runtimeSkinRenderers == 0 && PokemonMotionProfile.ForSpecies(species).FloatHeight == 0f)
                {
                    for (var mesh = 0; mesh < initial.vertices.Length; mesh++)
                        for (var vertex = 0; vertex < initial.vertices[mesh].Length; vertex++)
                        {
                            var point = instance.transform.InverseTransformPoint(initial.filters[mesh].transform.TransformPoint(initial.vertices[mesh][vertex]));
                            if (point.y <= initialBounds.min.y + initialBounds.size.y * .06f)
                                baseVertices.Add(new BaseVertex { Mesh = mesh, Vertex = vertex, Anchor = point });
                        }
                }
                var changed = new HashSet<int>();
                var changedVertices = new HashSet<long>();
                var idleChanged = new HashSet<int>();
                var idleChangedVertices = new HashSet<long>();
                result.finite = true;
                result.rootPreserved = true;
                var actions = (PokemonMotionAction[])Enum.GetValues(typeof(PokemonMotionAction));
                foreach (var action in actions)
                {
                    result.sampledActions++;
                    foreach (var time in action == PokemonMotionAction.Idle
                        ? new[] { .37f, .93f, 1.61f, 2.79f, 4.13f, 6.07f }
                        : new[] { 0f, .15f, .4f, .7f, 1.3f, 2f })
                    {
                        motion.SamplePose(1.61f + time, action, time);
                        var pose = new Pose(instance);
                        if (pose.transforms.Length != initial.transforms.Length || pose.filters.Length != initial.filters.Length)
                            throw new InvalidDataException("La hiérarchie change pendant l’échantillonnage.");
                        result.rootPreserved &= Close(instance.transform.localPosition, rootPosition)
                            && Close(instance.transform.localRotation, rootRotation)
                            && Close(instance.transform.localScale, rootScale);
                        for (var i = 0; i < pose.transforms.Length; i++)
                        {
                            result.finite &= Finite(pose.positions[i]) && Finite(pose.scales[i]) && Finite(pose.rotations[i]);
                            var angle = RotationAngle(initial.rotations[i], pose.rotations[i]);
                            result.maximumRotationDegrees = Mathf.Max(result.maximumRotationDegrees, angle);
                            if (!Close(initial.rotations[i], pose.rotations[i]) || !Close(pose.positions[i], initial.positions[i]) || !Close(pose.scales[i], initial.scales[i]))
                            {
                                changed.Add(i);
                                if (action == PokemonMotionAction.Idle) idleChanged.Add(i);
                            }
                        }
                        for (var mesh = 0; mesh < pose.vertices.Length; mesh++)
                        {
                            if (pose.vertices[mesh].Length != initial.vertices[mesh].Length)
                                throw new InvalidDataException("Le nombre de sommets change pendant l’animation.");
                            for (var vertex = 0; vertex < pose.vertices[mesh].Length; vertex++)
                            {
                                var v = pose.vertices[mesh][vertex];
                                result.finite &= Finite(v);
                                var displacement = Vector3.Distance(v, initial.vertices[mesh][vertex]);
                                result.maximumVertexDisplacement = Mathf.Max(result.maximumVertexDisplacement, displacement);
                                if (displacement > 0.000001f)
                                {
                                    var id = ((long)mesh << 32) | (uint)vertex;
                                    changedVertices.Add(id);
                                    if (action == PokemonMotionAction.Idle) idleChangedVertices.Add(id);
                                }
                            }
                            foreach (var normal in pose.filters[mesh].sharedMesh.normals) result.finite &= Finite(normal);
                        }
                        for (var foot = 0; foot < plantedFeet.Length; foot++)
                        {
                            var actual = instance.transform.InverseTransformPoint(plantedFeet[foot].position);
                            var desired = instance.transform.InverseTransformPoint(motion.DesiredFootPosition(foot));
                            result.maximumFootDrift = Mathf.Max(result.maximumFootDrift,
                                Vector3.Distance(desired, actual));
                            result.minimumFootFloorOffset = Mathf.Min(result.minimumFootFloorOffset, actual.y - footRestTargets[foot].y);
                        }
                        foreach (var vertex in baseVertices)
                        {
                            var point = instance.transform.InverseTransformPoint(pose.filters[vertex.Mesh].transform.TransformPoint(pose.vertices[vertex.Mesh][vertex.Vertex]));
                            result.maximumRigidBaseDrift = Mathf.Max(result.maximumRigidBaseDrift, Vector3.Distance(point, vertex.Anchor));
                        }
                        var geometry = GeometryBounds(instance, out float bakedSkinMinimumY);
                        if (skinFloorProbe != null)
                        {
                            float cpuSkinMinimumY = skinFloorProbe.MinimumY(float.NegativeInfinity, out _);
                            if (float.IsNaN(cpuSkinMinimumY) || float.IsInfinity(cpuSkinMinimumY)
                                || float.IsNaN(bakedSkinMinimumY) || float.IsInfinity(bakedSkinMinimumY)) result.finite = false;
                            else result.maximumFloorProbeError = Mathf.Max(result.maximumFloorProbeError,
                                Mathf.Abs(cpuSkinMinimumY - bakedSkinMinimumY));
                        }
                        result.minimumSampledY = Mathf.Min(result.minimumSampledY, geometry.min.y);
                        var ratio = ((geometry.center - initialBounds.center).magnitude + geometry.extents.magnitude)
                            / Mathf.Max(.00001f, initialBounds.extents.magnitude);
                        result.maximumGeometryExtentRatio = Mathf.Max(result.maximumGeometryExtentRatio, ratio);
                        if (!Finite(geometry.center) || !Finite(geometry.size)) result.finite = false;
                    }
                }

                motion.SamplePose(1.61f);
                var repeatedA = new Pose(instance);
                // A different action between equal samples detects IK/state dependence as well as accumulation.
                motion.SamplePose(4.13f, PokemonMotionAction.Physical, .4f);
                motion.SamplePose(1.61f);
                var repeatedB = new Pose(instance);
                result.deterministic = SamePose(repeatedA, repeatedB);
                result.sourceGeometryPreserved = true;
                for (var i = 0; i < sourceMeshes.Length; i++)
                    result.sourceGeometryPreserved &= SameVertices(sourceMeshes[i].vertices, sourceVertices[i]);
                result.changedTransforms = changed.Count;
                result.changedVertices = changedVertices.Count;
                result.idleChangedTransforms = idleChanged.Count;
                result.idleChangedVertices = idleChangedVertices.Count;
                if (!result.finite) throw new InvalidDataException("Transformation ou sommet NaN/Infinity.");
                if (!result.rootPreserved) throw new InvalidDataException("L’animation déplace/redimensionne la racine de placement du combat.");
                if (!result.deterministic) throw new InvalidDataException("Échantillon répété différent : accumulation de la pose.");
                if (!result.sourceGeometryPreserved) throw new InvalidDataException("L’animation modifie une géométrie importée partagée.");
                if (result.maximumFloorProbeError > Mathf.Max(.00005f, motion.ModelHeight * .0001f))
                    throw new InvalidDataException($"La sonde de sol CPU diffère de la géométrie skinnée cuite de {result.maximumFloorProbeError:F6} m.");
                if (result.maximumGeometryExtentRatio > 3f) throw new InvalidDataException("La géométrie animée s’étend à plus de trois fois son volume initial.");
                if (result.maximumFootDrift > Mathf.Max(.002f, motion.ModelHeight * .015f))
                    throw new InvalidDataException($"Un pied s’écarte de sa cible planifiée de {result.maximumFootDrift:F5} m.");
                if (result.minimumFootFloorOffset < -Mathf.Max(.002f, motion.ModelHeight * .015f))
                    throw new InvalidDataException($"Un pied descend sous son contact initial de {-result.minimumFootFloorOffset:F5} m.");
                if (result.maximumRigidBaseDrift > Mathf.Max(.001f, motion.ModelHeight * .003f))
                    throw new InvalidDataException($"La base rigide posée au sol dérive de {result.maximumRigidBaseDrift:F5} m.");
                if (result.idleChangedTransforms == 0 && result.idleChangedVertices == 0)
                    throw new InvalidDataException("Aucun mouvement mesuré sur le cycle d’attente.");
                result.arbitraryPlacementValid = !new[] { 1, 6, 9, 18, 25, 65, 77, 94, 130, 131, 133, 143 }.Contains(species)
                    || ValidatePlacement(prefab, species);
                if (!result.arbitraryPlacementValid) throw new InvalidDataException("Pose invalide avec rotation/échelle de placement différentes.");
            }
            catch (Exception exception)
            {
                var actual = exception is TargetInvocationException && exception.InnerException != null ? exception.InnerException : exception;
                result.error = actual.GetType().Name + ": " + actual.Message;
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
            }
            return result;
        }

        private static int WeightedBoneCount(GameObject root)
        {
            var bones = new HashSet<Transform>();
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.sharedMesh == null) continue;
                var weights = skin.sharedMesh.boneWeights;
                var transforms = skin.bones;
                foreach (var weight in weights)
                {
                    AddWeightedBone(bones, transforms, weight.boneIndex0, weight.weight0);
                    AddWeightedBone(bones, transforms, weight.boneIndex1, weight.weight1);
                    AddWeightedBone(bones, transforms, weight.boneIndex2, weight.weight2);
                    AddWeightedBone(bones, transforms, weight.boneIndex3, weight.weight3);
                }
            }
            return bones.Count;
        }

        private static Transform[] GetPlantedFeet(CinematicMotion motion)
        {
            var list = typeof(CinematicMotion).GetField("feet", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(motion) as System.Collections.IEnumerable;
            if (list == null) return Array.Empty<Transform>();
            var feet = new List<Transform>();
            foreach (var value in list)
            {
                var transform = value.GetType().GetField("End", BindingFlags.Instance | BindingFlags.Public)
                    ?.GetValue(value) as Transform;
                if (transform != null) feet.Add(transform);
            }
            return feet.ToArray();
        }

        private static Vector3[] GetFootRestTargets(CinematicMotion motion)
        {
            var list = typeof(CinematicMotion).GetField("feet", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(motion) as System.Collections.IEnumerable;
            if (list == null) return Array.Empty<Vector3>();
            var targets = new List<Vector3>();
            foreach (var value in list)
                targets.Add((Vector3)value.GetType().GetField("Target", BindingFlags.Instance | BindingFlags.Public).GetValue(value));
            return targets.ToArray();
        }

        private sealed class BaseVertex
        {
            public int Mesh, Vertex;
            public Vector3 Anchor;
        }

        private static Bounds GeometryBounds(GameObject root) => GeometryBounds(root, out _);

        private static Bounds GeometryBounds(GameObject root, out float skinnedMinimumY)
        {
            var bounds = new Bounds();
            var found = false;
            skinnedMinimumY = float.PositiveInfinity;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh = null;
                var temporary = false;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                    // Unity 6: true compensates Transform scale; TransformPoint below applies it once.
                    skin.BakeMesh(mesh, true);
                    temporary = true;
                }
                else if (renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                try
                {
                    foreach (var vertex in mesh.vertices)
                    {
                        var point = root.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                        if (renderer is SkinnedMeshRenderer) skinnedMinimumY = Mathf.Min(skinnedMinimumY, point.y);
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                    foreach (var normal in mesh.normals)
                        if (!Finite(normal)) throw new InvalidDataException("Normale de géométrie NaN/Infinity.");
                }
                finally { if (temporary) Object.DestroyImmediate(mesh); }
            }
            if (!found) throw new InvalidDataException("Géométrie du modèle absente.");
            return bounds;
        }

        private static bool ValidatePlacement(GameObject prefab, int species)
        {
            GameObject instance = null;
            try
            {
                instance = Object.Instantiate(prefab, new Vector3(3.2f, 1.1f, -2.8f), Quaternion.Euler(8f, 37f, -4f));
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.localScale = Vector3.one * 1.23f;
                var position = instance.transform.localPosition;
                var rotation = instance.transform.localRotation;
                var scale = instance.transform.localScale;
                var motion = instance.GetComponent<CinematicMotion>() ?? instance.AddComponent<CinematicMotion>();
                motion.Configure(species);
                motion.SamplePose(.37f);
                var initial = GeometryBounds(instance);
                foreach (var action in (PokemonMotionAction[])Enum.GetValues(typeof(PokemonMotionAction)))
                {
                    motion.SamplePose(1.61f, action, .4f);
                    if (!Close(position, instance.transform.localPosition) || !Close(scale, instance.transform.localScale)
                        || !Close(rotation, instance.transform.localRotation)) return false;
                    var pose = new Pose(instance);
                    for (var i = 0; i < pose.transforms.Length; i++)
                        if (!Finite(pose.positions[i]) || !Finite(pose.rotations[i]) || !Finite(pose.scales[i])) return false;
                    var bounds = GeometryBounds(instance);
                    if (!Finite(bounds.center) || !Finite(bounds.size)
                        || bounds.extents.magnitude > initial.extents.magnitude * 3f) return false;
                }
                return true;
            }
            finally { if (instance != null) Object.DestroyImmediate(instance); }
        }

        private static void AddWeightedBone(HashSet<Transform> bones, Transform[] transforms, int index, float weight)
        {
            if (weight > .00001f && index >= 0 && index < transforms.Length && transforms[index] != null) bones.Add(transforms[index]);
        }

        private static bool SamePose(Pose a, Pose b)
        {
            if (a.transforms.Length != b.transforms.Length || a.vertices.Length != b.vertices.Length) return false;
            for (var i = 0; i < a.transforms.Length; i++)
                if (!Close(a.positions[i], b.positions[i]) || !Close(a.scales[i], b.scales[i]) || !Close(a.rotations[i], b.rotations[i])) return false;
            for (var i = 0; i < a.vertices.Length; i++) if (!SameVertices(a.vertices[i], b.vertices[i])) return false;
            return true;
        }

        private static bool SameVertices(Vector3[] a, Vector3[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++) if (!Close(a[i], b[i])) return false;
            return true;
        }

        private static bool Close(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 0.000000000001f;
        private static bool Close(Quaternion a, Quaternion b)
        {
            var sign = Quaternion.Dot(a, b) < 0f ? -1f : 1f;
            return Math.Abs(a.x - b.x * sign) < .000001f && Math.Abs(a.y - b.y * sign) < .000001f
                && Math.Abs(a.z - b.z * sign) < .000001f && Math.Abs(a.w - b.w * sign) < .000001f;
        }
        private static float RotationAngle(Quaternion a, Quaternion b)
        {
            double dot = (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z + (double)a.w * b.w;
            double normA = Math.Sqrt((double)a.x * a.x + (double)a.y * a.y + (double)a.z * a.z + (double)a.w * a.w);
            double normB = Math.Sqrt((double)b.x * b.x + (double)b.y * b.y + (double)b.z * b.z + (double)b.w * b.w);
            return (float)(Math.Acos(Math.Min(1d, Math.Abs(dot) / Math.Max(.00000000001d, normA * normB))) * 360d / Math.PI);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}
