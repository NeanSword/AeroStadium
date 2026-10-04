using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Optional presentation lift outside the native Animator hierarchy; does not modify any clip or bone.</summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class NativeGrounding : MonoBehaviour
    {
        [SerializeField] Transform actorRoot, modelRoot;
        [SerializeField] string[] excludedRenderers = Array.Empty<string>();
        [SerializeField] Vector3 restLocalPosition;
        [SerializeField] float groundLevelActorY;

        sealed class Geometry
        {
            public Renderer renderer;
            public Vector3[] vertices;
            public BoneWeight[] weights;
            public Matrix4x4[] bindposes, skinMatrices;
            public int[] boneNodes, indices;
            public int rendererNode;
        }
        Geometry[] geometry = Array.Empty<Geometry>();
        Transform[] nodes = Array.Empty<Transform>();
        int[] parents = Array.Empty<int>();
        Matrix4x4[] pose = Array.Empty<Matrix4x4>();
        bool ready;
        float scaleToActorY, parentGroundPlaneY;
        public int BodyVertexCount { get; private set; }
        public int BodyRendererCount => geometry.Length;
        public int Samples { get; private set; }
        public float LastCpuMilliseconds { get; private set; }
        public float MaximumCpuMilliseconds { get; private set; }
        public float LastUncorrectedFloor { get; private set; }
        public float LastGroundedFloor { get; private set; }
        public float LastRaiseMetres { get; private set; }
        public float MaximumRaiseMetres { get; private set; }

        public void Configure(Transform actor, Transform model, string[] exclusions, float groundY = 0f)
        {
            actorRoot = actor; modelRoot = model;
            excludedRenderers = exclusions ?? Array.Empty<string>();
            restLocalPosition = transform.localPosition; groundLevelActorY = groundY;
            Prepare();
            enabled = true;
        }

        void Awake()
        {
            // AddComponent can run Awake before a runtime caller has supplied its hierarchy.
            if (actorRoot == null || modelRoot == null) return;
            try { Prepare(); }
            catch (Exception exception) { Fail(exception); }
        }

        void Prepare()
        {
            ready = false; BodyVertexCount = 0;
            Samples = 0; LastCpuMilliseconds = 0f; MaximumCpuMilliseconds = 0f;
            LastUncorrectedFloor = 0f; LastGroundedFloor = 0f; LastRaiseMetres = 0f; MaximumRaiseMetres = 0f;
            if (actorRoot == null || modelRoot == null || transform.parent == null || !modelRoot.IsChildOf(transform))
                throw new InvalidDataException("Hiérarchie NativeGrounding invalide.");
            if (Quaternion.Angle(transform.localRotation, Quaternion.identity) > .001f || Vector3.Distance(transform.localScale, Vector3.one) > .000001f)
                throw new InvalidDataException("NativeGrounding doit être un parent sans rotation ni échelle.");
            if (Vector3.Dot(transform.parent.up, actorRoot.up) < .99999f)
                throw new InvalidDataException("NativeGrounding nécessite une normalisation verticale.");
            scaleToActorY = actorRoot.InverseTransformVector(transform.parent.TransformVector(Vector3.up)).y;
            if (scaleToActorY <= .000001f) throw new InvalidDataException("Échelle verticale NativeGrounding invalide.");
            parentGroundPlaneY = transform.parent.InverseTransformPoint(actorRoot.TransformPoint(new Vector3(0f, groundLevelActorY, 0f))).y;

            var transformList = new List<Transform>();
            var parentList = new List<int>();
            var nodeIndex = new Dictionary<Transform, int>();
            var pending = new Queue<Transform>();
            pending.Enqueue(modelRoot);
            while (pending.Count > 0)
            {
                Transform node = pending.Dequeue();
                int index = transformList.Count;
                int parentIndex = node.parent == transform ? -1 : nodeIndex.TryGetValue(node.parent, out int p) ? p : -2;
                if (parentIndex == -2) throw new InvalidDataException("Nœud de grounding hors hiérarchie : " + node.name);
                transformList.Add(node); parentList.Add(parentIndex); nodeIndex.Add(node, index);
                for (int i = 0; i < node.childCount; i++) pending.Enqueue(node.GetChild(i));
            }
            nodes = transformList.ToArray(); parents = parentList.ToArray(); pose = new Matrix4x4[nodes.Length];

            var meshes = new List<Geometry>();
            foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                bool excluded = false;
                foreach (string name in excludedRenderers)
                    if (renderer.name == name) { excluded = true; break; }
                if (excluded || !IsBody(renderer)) continue;
                var skin = renderer as SkinnedMeshRenderer;
                Mesh mesh = skin != null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || !mesh.isReadable) throw new InvalidDataException("Maillage de grounding non lisible : " + renderer.name);
                var data = new Geometry { renderer = renderer, vertices = mesh.vertices, rendererNode = nodeIndex[renderer.transform] };
                var used = new HashSet<int>();
                Material[] materialSlots = renderer.sharedMaterials;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (submesh >= materialSlots.Length || !IsBodyMaterial(materialSlots[submesh])) continue;
                    foreach (int vertexIndex in mesh.GetIndices(submesh))
                    {
                        if (vertexIndex < 0 || vertexIndex >= data.vertices.Length) throw new InvalidDataException("Index de géométrie de grounding invalide.");
                        used.Add(vertexIndex);
                    }
                }
                if (used.Count == 0) continue;
                data.indices = new int[used.Count]; used.CopyTo(data.indices);
                if (skin != null)
                {
                    data.weights = mesh.boneWeights; data.bindposes = mesh.bindposes;
                    Transform[] bones = skin.bones;
                    if (data.weights.Length != data.vertices.Length || data.bindposes.Length != bones.Length)
                        throw new InvalidDataException("Skin de grounding invalide : " + renderer.name);
                    data.boneNodes = new int[bones.Length]; data.skinMatrices = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                        if (!nodeIndex.TryGetValue(bones[i], out data.boneNodes[i])) throw new InvalidDataException("Os de grounding hors modèle.");
                    foreach (var weight in data.weights)
                        if (!ValidIndex(weight.boneIndex0, weight.weight0) || !ValidIndex(weight.boneIndex1, weight.weight1) ||
                            !ValidIndex(weight.boneIndex2, weight.weight2) || !ValidIndex(weight.boneIndex3, weight.weight3))
                            throw new InvalidDataException("Index de skin de grounding invalide.");
                    bool ValidIndex(int index, float weight) => weight <= 0f || index >= 0 && index < bones.Length;
                }
                BodyVertexCount += data.indices.Length; meshes.Add(data);
            }
            geometry = meshes.ToArray();
            if (geometry.Length == 0) throw new InvalidDataException("Aucun maillage Body natif pour NativeGrounding.");
            ready = true;
        }

        static bool IsBody(Renderer renderer)
        {
            // Only the established native lit presentation participates. Mask/Core and depth-only slots are excluded.
            foreach (var material in renderer.sharedMaterials)
                if (IsBodyMaterial(material)) return true;
            return false;
        }
        static bool IsBodyMaterial(Material material) => material != null && material.shader != null &&
            material.shader.name == "AeroStadium/NativeLayeredLit" && material.HasProperty("_ColorMask") && material.GetFloat("_ColorMask") != 0f;

        void LateUpdate()
        {
            if (!ready) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    Transform node = nodes[i];
                    Matrix4x4 local = Matrix4x4.TRS(node.localPosition, node.localRotation, node.localScale);
                    pose[i] = parents[i] < 0 ? local : pose[parents[i]] * local;
                }
                float minimum = float.PositiveInfinity;
                foreach (var data in geometry)
                {
                    if (!data.renderer.enabled || !data.renderer.gameObject.activeInHierarchy) continue;
                    if (data.boneNodes == null)
                    {
                        Matrix4x4 matrix = pose[data.rendererNode];
                        foreach (int index in data.indices) minimum = Mathf.Min(minimum, Y(matrix, data.vertices[index]));
                        continue;
                    }
                    for (int i = 0; i < data.skinMatrices.Length; i++) data.skinMatrices[i] = pose[data.boneNodes[i]] * data.bindposes[i];
                    foreach (int i in data.indices)
                    {
                        Vector3 vertex = data.vertices[i]; BoneWeight weight = data.weights[i];
                        float y = WeightedY(data, vertex, weight.boneIndex0, weight.weight0) + WeightedY(data, vertex, weight.boneIndex1, weight.weight1) +
                                  WeightedY(data, vertex, weight.boneIndex2, weight.weight2) + WeightedY(data, vertex, weight.boneIndex3, weight.weight3);
                        minimum = Mathf.Min(minimum, y);
                    }
                }
                if (float.IsPositiveInfinity(minimum)) { transform.localPosition = restLocalPosition; return; }
                if (float.IsNaN(minimum) || float.IsInfinity(minimum)) throw new InvalidDataException("Sol animé natif non fini.");
                float raise = Mathf.Max(0f, parentGroundPlaneY - restLocalPosition.y - minimum);
                transform.localPosition = restLocalPosition + Vector3.up * raise;
                LastUncorrectedFloor = groundLevelActorY + (restLocalPosition.y + minimum - parentGroundPlaneY) * scaleToActorY;
                LastGroundedFloor = LastUncorrectedFloor + raise * scaleToActorY;
                LastRaiseMetres = raise * scaleToActorY;
                MaximumRaiseMetres = Mathf.Max(MaximumRaiseMetres, LastRaiseMetres); Samples++;
            }
            catch (Exception exception) { Fail(exception); }
            finally
            {
                LastCpuMilliseconds = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                MaximumCpuMilliseconds = Mathf.Max(MaximumCpuMilliseconds, LastCpuMilliseconds);
            }
        }
        static float Y(Matrix4x4 matrix, Vector3 vertex) => matrix.m10 * vertex.x + matrix.m11 * vertex.y + matrix.m12 * vertex.z + matrix.m13;
        static float WeightedY(Geometry data, Vector3 vertex, int index, float weight) => weight <= 0f ? 0f : Y(data.skinMatrices[index], vertex) * weight;
        void Fail(Exception exception) { Debug.LogError("[native-grounding-error] " + exception.Message); ready = false; enabled = false; }
        void OnDisable() { transform.localPosition = restLocalPosition; }
    }
}
