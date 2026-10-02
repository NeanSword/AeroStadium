using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Cached CPU skinning of the actor-local Y coordinate only. This examines
    /// rendered vertices, not bone pivots or Renderer.bounds, without BakeMesh
    /// allocations. Four influences match the authored Bone4 skin pipeline.
    /// Rebuild after adding/removing renderers; source meshes stay unchanged.
    /// </summary>
    public sealed class SkinnedFloorProbe
    {
        public readonly struct Contact
        {
            public readonly Renderer Renderer;
            public readonly int Vertex;
            public readonly float Y;
            public readonly Transform[] InfluencingBones;

            public Contact(Renderer renderer, int vertex, float y, Transform[] bones)
            { Renderer = renderer; Vertex = vertex; Y = y; InfluencingBones = bones; }
        }

        sealed class Surface
        {
            public Renderer Renderer;
            public Vector3[] Vertices;
            public BoneWeight[] Weights;
            public Matrix4x4[] Bindposes;
            public Transform[] Bones;
            public Vector4[] YRows;
            public bool Skinned;
        }

        readonly Transform actor;
        readonly Surface[] surfaces;

        public SkinnedFloorProbe(GameObject actorObject, bool includeRigid = true)
        {
            if (actorObject == null) throw new ArgumentNullException(nameof(actorObject));
            actor = actorObject.transform;
            var collected = new List<Surface>();
            foreach (Renderer renderer in actorObject.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh = null;
                var skin = renderer as SkinnedMeshRenderer;
                if (skin == null && !includeRigid) continue;
                if (skin != null) mesh = skin.sharedMesh;
                else if (renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null || !mesh.isReadable || mesh.vertexCount == 0) continue;
                var surface = new Surface { Renderer = renderer, Vertices = mesh.vertices, Skinned = skin != null };
                if (skin != null)
                {
                    surface.Weights = mesh.boneWeights;
                    surface.Bindposes = mesh.bindposes;
                    surface.Bones = skin.bones;
                    surface.YRows = new Vector4[surface.Bones.Length];
                    if (surface.Weights.Length != surface.Vertices.Length)
                        throw new InvalidOperationException("Skin vertex/weight count mismatch: " + skin.name);
                }
                collected.Add(surface);
            }
            surfaces = collected.ToArray();
        }

        public float MinimumY(out Contact contact)
            => MinimumY(float.PositiveInfinity, out contact);

        /// <summary>Only resolves influencing bones when geometry penetrates the supplied threshold.</summary>
        public float MinimumY(float contactBelowY, out Contact contact)
        {
            float minimum = float.PositiveInfinity;
            Surface lowest = null;
            int lowestVertex = -1;
            Matrix4x4 inverseActor = actor.worldToLocalMatrix;
            foreach (Surface surface in surfaces)
            {
                if (surface.Renderer == null || !surface.Renderer.enabled || !surface.Renderer.gameObject.activeInHierarchy) continue;
                Vector4 rigidRow = default;
                if (surface.Skinned)
                {
                    for (int bone = 0; bone < surface.Bones.Length; bone++)
                    {
                        if (surface.Bones[bone] == null || bone >= surface.Bindposes.Length)
                            throw new InvalidOperationException("Skin contains an absent bone/bindpose: " + surface.Renderer.name);
                        surface.YRows[bone] = (inverseActor * surface.Bones[bone].localToWorldMatrix * surface.Bindposes[bone]).GetRow(1);
                    }
                }
                else rigidRow = (inverseActor * surface.Renderer.transform.localToWorldMatrix).GetRow(1);
                for (int vertex = 0; vertex < surface.Vertices.Length; vertex++)
                {
                    Vector3 point = surface.Vertices[vertex];
                    float y;
                    if (surface.Skinned)
                    {
                        BoneWeight w = surface.Weights[vertex];
                        y = WeightedY(surface.YRows, point, w.boneIndex0, w.weight0)
                            + WeightedY(surface.YRows, point, w.boneIndex1, w.weight1)
                            + WeightedY(surface.YRows, point, w.boneIndex2, w.weight2)
                            + WeightedY(surface.YRows, point, w.boneIndex3, w.weight3);
                    }
                    else y = Y(rigidRow, point);
                    if (y >= minimum) continue;
                    minimum = y; lowest = surface; lowestVertex = vertex;
                }
            }
            if (lowest == null) { contact = default; return minimum; }
            if (minimum >= contactBelowY)
            {
                contact = new Contact(lowest.Renderer, lowestVertex, minimum, Array.Empty<Transform>());
                return minimum;
            }
            var influencing = new List<Transform>(4);
            if (lowest.Skinned)
            {
                BoneWeight w = lowest.Weights[lowestVertex];
                Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1);
                Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                void Add(int index, float weight)
                {
                    if (weight > .01f && index >= 0 && index < lowest.Bones.Length && !influencing.Contains(lowest.Bones[index]))
                        influencing.Add(lowest.Bones[index]);
                }
            }
            contact = new Contact(lowest.Renderer, lowestVertex, minimum, influencing.ToArray());
            return minimum;
        }

        static float WeightedY(Vector4[] rows, Vector3 p, int index, float weight)
        {
            if (weight <= 0f) return 0f;
            if (index < 0 || index >= rows.Length) throw new InvalidOperationException("Skin influence index out of range.");
            return Y(rows[index], p) * weight;
        }
        static float Y(Vector4 row, Vector3 p) => row.x * p.x + row.y * p.y + row.z * p.z + row.w;
    }
}
