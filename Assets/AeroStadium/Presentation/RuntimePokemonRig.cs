using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Independently authored runtime articulation for selected unskinned Kanto models.
    /// The supplied bounds and all recipe coordinates are actor-local, after facing
    /// normalization: X lateral, Y up, -Z forward. Source assets are never modified.
    /// Recipe masks are provisional geometry authoring; visual/strain QA remains required.
    /// </summary>
    public static class RuntimePokemonRig
    {
        public enum Readiness { Unsupported, ExistingRigidParts, PendingAnatomy, CuratedForReview }

        public static Readiness ForSpecies(int species)
        {
            switch (species)
            {
                case 81: case 82: case 92: case 93: case 109: case 110:
                    return Readiness.ExistingRigidParts;
                case 42: case 77: case 99: case 106: case 128: case 131: case 143:
                    return Readiness.CuratedForReview;
                case 35: case 60: case 90: case 96: case 97: case 98: case 101:
                case 102: case 108: case 111: case 113: case 114: case 116:
                case 117: case 118: case 119: case 124: case 125: case 126:
                case 140: case 141:
                    return Readiness.PendingAnatomy;
                default: return Readiness.Unsupported;
            }
        }

        /// <summary>
        /// Build only a curated recipe, before CinematicMotion binds joints. Repeated
        /// calls are idempotent. A false result leaves the supplied actor untouched.
        /// </summary>
        public static bool TryBuild(GameObject actor, int species,
            PokemonMotionProfile profile, Bounds accurateModelBounds)
        {
            if (actor == null || actor.transform.childCount == 0) return false;
            if (actor.TryGetComponent<RuntimePokemonRigOwner>(out var existing))
                return existing.Species == species && existing.RendererCount > 0;
            if (ForSpecies(species) != Readiness.CuratedForReview) return false;
            if (!Finite(accurateModelBounds.min) || !Finite(accurateModelBounds.max)
                || accurateModelBounds.size.x < .0001f || accurateModelBounds.size.y < .0001f
                || accurateModelBounds.size.z < .0001f) return false;

            // Do not mix a generated rig into an existing imported skin, including
            // an actor already configured by a different skin authoring component.
            foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.sharedMesh != null) return false;

            var sources = new List<MeshFilter>();
            foreach (var filter in actor.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable
                    || filter.sharedMesh.vertexCount == 0) continue;
                if (!filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                sources.Add(filter);
            }
            if (sources.Count == 0) return false;

            Recipe recipe = BuildRecipe(species, accurateModelBounds);
            if (recipe == null) return false;
            var owner = actor.AddComponent<RuntimePokemonRigOwner>();
            owner.Initialize(species);
            try
            {
                Transform visual = actor.transform.GetChild(0);
                var bones = new Transform[recipe.Bones.Count];
                for (int i = 0; i < bones.Length; i++)
                {
                    BoneSpec spec = recipe.Bones[i];
                    var go = new GameObject(spec.Name);
                    go.hideFlags = HideFlags.DontSave;
                    Transform bone = go.transform;
                    bone.position = actor.transform.TransformPoint(recipe.ToActor(spec.Point));
                    bone.rotation = actor.transform.rotation;
                    // Preserve world placement and orientation under the normalized
                    // visual wrapper. Its imported scale can be far from one.
                    bone.SetParent(spec.Parent < 0 ? visual : bones[spec.Parent], true);
                    bones[i] = bone;
                    if (i == 0) owner.SetSkeletonRoot(bone);
                }

                foreach (MeshFilter filter in sources)
                {
                    MeshRenderer original = filter.GetComponent<MeshRenderer>();
                    Mesh source = filter.sharedMesh;
                    Mesh instance = UnityEngine.Object.Instantiate(source);
                    instance.name = source.name + " (original authored runtime skin)";
                    instance.hideFlags = HideFlags.DontSave;
                    instance.MarkDynamic();
                    owner.OwnMesh(instance);

                    Matrix4x4 meshToActor = actor.transform.worldToLocalMatrix
                        * filter.transform.localToWorldMatrix;
                    Vector3[] vertices = source.vertices;
                    var weights = new BoneWeight[vertices.Length];
                    var scratch = new float[bones.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 normalized = recipe.FromActor(meshToActor.MultiplyPoint3x4(vertices[i]));
                        weights[i] = recipe.Weight(normalized, scratch);
                    }
                    instance.boneWeights = weights;
                    var bindposes = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                        bindposes[i] = bones[i].worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    instance.bindposes = bindposes;

                    // Unity permits one renderer type on an object. Adding a skin
                    // beside a MeshRenderer can destroy that original component.
                    // An identity child has exactly the same mesh-to-world matrix,
                    // preserving the bind-space contract without touching its parent.
                    var rendered = new GameObject(filter.gameObject.name + " (authored runtime skin)");
                    rendered.hideFlags = HideFlags.DontSave;
                    rendered.layer = filter.gameObject.layer;
                    rendered.transform.SetParent(filter.transform, false);
                    var skin = rendered.AddComponent<SkinnedMeshRenderer>();
                    owner.OwnRenderer(skin, original);
                    skin.sharedMesh = instance;
                    skin.bones = bones;
                    skin.rootBone = bones[0];
                    skin.sharedMaterials = original.sharedMaterials;
                    skin.quality = SkinQuality.Bone4;
                    skin.shadowCastingMode = original.shadowCastingMode;
                    skin.receiveShadows = original.receiveShadows;
                    skin.lightProbeUsage = original.lightProbeUsage;
                    skin.reflectionProbeUsage = original.reflectionProbeUsage;
                    skin.probeAnchor = original.probeAnchor;
                    skin.allowOcclusionWhenDynamic = original.allowOcclusionWhenDynamic;
                    skin.sortingLayerID = original.sortingLayerID;
                    skin.sortingOrder = original.sortingOrder;
                    // A spread wing can rotate into an axis with very small original
                    // extent. This conservative local cube prevents pose-dependent
                    // frustum disappearance; it does not change the mesh itself.
                    skin.localBounds = new Bounds(source.bounds.center,
                        Vector3.one * Mathf.Max(.01f, source.bounds.size.magnitude * 1.6f));
                    skin.updateWhenOffscreen = false;
                    original.enabled = false;
                }
                owner.Complete(bones.Length);
                return owner.RendererCount > 0;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[original-animation] runtime rig could not be built for species "
                    + species + ": " + error.Message, actor);
                owner.Release();
                DestroyRuntime(owner);
                return false;
            }
        }

        /// <summary>Temporarily show source object animation during explicit source review.</summary>
        public static void SetEnabled(GameObject actor, bool enabled)
        {
            if (actor != null && actor.TryGetComponent<RuntimePokemonRigOwner>(out var owner))
                owner.SetRigEnabled(enabled);
        }

        internal static void DestroyRuntime(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x)
            && !float.IsNaN(p.y) && !float.IsInfinity(p.y)
            && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        static float Ease(float from, float to, float value)
        {
            float t = Mathf.InverseLerp(from, to, value);
            return t * t * (3f - 2f * t);
        }
        static Vector3 P(float x, float y, float front) => new Vector3(x, y, front);

        sealed class BoneSpec
        {
            public string Name;
            public int Parent;
            public Vector3 Point;
        }

        sealed class Region
        {
            public int[] Bones;
            public Vector3[] Points;
            public Func<Vector3, float> Mask;
            public float InnerRadius, OuterRadius;
            public bool Capsule;
            public float[] Distances, Projections;
        }

        sealed class Recipe
        {
            public readonly List<BoneSpec> Bones = new List<BoneSpec>();
            public readonly List<Region> Regions = new List<Region>();
            public readonly Bounds Bounds;
            public Func<Vector3, float[], bool> BodyWeights;
            readonly Vector3 metric;

            public Recipe(Bounds bounds)
            {
                Bounds = bounds;
                metric = new Vector3(bounds.size.x / bounds.size.y, 1f, bounds.size.z / bounds.size.y);
            }
            public Vector3 ToActor(Vector3 p) => new Vector3(Bounds.center.x + p.x * Bounds.size.x,
                Bounds.min.y + p.y * Bounds.size.y, Bounds.center.z - p.z * Bounds.size.z);
            public Vector3 FromActor(Vector3 p) => new Vector3((p.x - Bounds.center.x) / Bounds.size.x,
                (p.y - Bounds.min.y) / Bounds.size.y, (Bounds.center.z - p.z) / Bounds.size.z);
            public int Bone(string name, int parent, Vector3 point)
            {
                Bones.Add(new BoneSpec { Name = name, Parent = parent, Point = point });
                return Bones.Count - 1;
            }
            public void Chain(string[] names, int parent, Vector3[] points,
                Func<Vector3, float> mask, float inner = 0f, float outer = 0f)
            {
                var ids = new int[names.Length];
                for (int i = 0; i < ids.Length; i++)
                    ids[i] = Bone(names[i], i == 0 ? parent : ids[i - 1], points[i]);
                Regions.Add(new Region { Bones = ids, Points = points, Mask = mask,
                    InnerRadius = inner, OuterRadius = outer, Capsule = outer > 0f,
                    Distances = new float[points.Length - 1], Projections = new float[points.Length - 1] });
            }

            public BoneWeight Weight(Vector3 p, float[] weights)
            {
                Array.Clear(weights, 0, weights.Length);
                if (BodyWeights == null || !BodyWeights(p, weights)) weights[0] = 1f;
                Vector3 vertex = Vector3.Scale(p, metric);
                foreach (Region region in Regions)
                {
                    float mask = Mathf.Clamp01(region.Mask(p));
                    if (mask < .00001f) continue;
                    float distance = float.PositiveInfinity;
                    for (int i = 0; i + 1 < region.Points.Length; i++)
                    {
                        Vector3 a = Vector3.Scale(region.Points[i], metric);
                        Vector3 b = Vector3.Scale(region.Points[i + 1], metric);
                        Vector3 delta = b - a;
                        float projection = Mathf.Clamp01(Vector3.Dot(vertex - a, delta)
                            / Mathf.Max(.000001f, delta.sqrMagnitude));
                        float candidate = (vertex - (a + delta * projection)).sqrMagnitude;
                        region.Distances[i] = candidate;
                        region.Projections[i] = projection;
                        if (candidate < distance)
                        {
                            distance = candidate;
                        }
                    }
                    if (region.Capsule)
                        mask *= 1f - Ease(region.InnerRadius, region.OuterRadius, Mathf.Sqrt(distance));
                    if (mask < .00001f) continue;
                    for (int i = 0; i < weights.Length; i++) weights[i] *= 1f - mask;
                    // Most of each segment follows its proximal pivot rigidly; a
                    // compact smooth transition blends the adjoining hinge. Hard
                    // shell/hand regions can override this with a single bone.
                    // Blend adjacent segment fields smoothly rather than choose a
                    // nearest segment abruptly. Thick flames/tails and bent limb
                    // surfaces cross segment bisectors away from their centreline.
                    // Hard nearest-segment switches would stretch those edges.
                    const float softnessSquared = .055f * .055f;
                    float scoreSum = 0f;
                    for (int i = 0; i < region.Distances.Length; i++)
                    {
                        region.Distances[i] = Mathf.Exp(-Mathf.Max(0f, region.Distances[i] - distance) / softnessSquared);
                        scoreSum += region.Distances[i];
                    }
                    for (int i = 0; i < region.Distances.Length; i++)
                    {
                        float share = mask * region.Distances[i] / Mathf.Max(.000001f, scoreSum);
                        float blend = Ease(.68f, 1f, region.Projections[i]);
                        weights[region.Bones[i]] += share * (1f - blend);
                        weights[region.Bones[Mathf.Min(i + 1, region.Bones.Length - 1)]] += share * blend;
                    }
                }

                int a0 = 0, a1 = 0, a2 = 0, a3 = 0;
                float w0 = -1f, w1 = -1f, w2 = -1f, w3 = -1f;
                for (int i = 0; i < weights.Length; i++)
                {
                    float w = weights[i];
                    if (w > w0) { a3 = a2; w3 = w2; a2 = a1; w2 = w1; a1 = a0; w1 = w0; a0 = i; w0 = w; }
                    else if (w > w1) { a3 = a2; w3 = w2; a2 = a1; w2 = w1; a1 = i; w1 = w; }
                    else if (w > w2) { a3 = a2; w3 = w2; a2 = i; w2 = w; }
                    else if (w > w3) { a3 = i; w3 = w; }
                }
                w0 = Mathf.Max(0f, w0); w1 = Mathf.Max(0f, w1);
                w2 = Mathf.Max(0f, w2); w3 = Mathf.Max(0f, w3);
                float sum = w0 + w1 + w2 + w3;
                if (sum < .000001f) return new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                return new BoneWeight { boneIndex0 = a0, boneIndex1 = a1, boneIndex2 = a2, boneIndex3 = a3,
                    weight0 = w0 / sum, weight1 = w1 / sum, weight2 = w2 / sum, weight3 = w3 / sum };
            }

            public void AssignRigid(Vector3[] points, int bone, Func<Vector3, float> mask,
                float inner = 0f, float outer = 0f)
            {
                Regions.Add(new Region { Bones = new[] { bone, bone }, Points = points, Mask = mask,
                    InnerRadius = inner, OuterRadius = outer, Capsule = outer > 0f,
                    Distances = new float[points.Length - 1], Projections = new float[points.Length - 1] });
            }
        }

        static Recipe BuildRecipe(int species, Bounds bounds)
        {
            switch (species)
            {
                case 42: return Golbat(bounds);
                case 77: return Quadruped(bounds, false);
                case 99: return Kingler(bounds);
                case 106: return Hitmonlee(bounds);
                case 128: return Quadruped(bounds, true);
                case 131: return Lapras(bounds);
                case 143: return Snorlax(bounds);
                default: return null;
            }
        }

        static Recipe Golbat(Bounds bounds)
        {
            var r = new Recipe(bounds);
            int waist = r.Bone("AuthoredWaist", -1, P(0f, .30f, 0f));
            int head = r.Bone("Head", waist, P(0f, .34f, .03f));
            r.BodyWeights = (p, w) => { w[head] = 1f; return true; };
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side;
                string tag = s > 0 ? "L" : "R";
                r.Chain(new[] { tag + "WingBase", tag + "WingMid", tag + "WingTip" }, waist,
                    new[] { P(s * .058f, .344f, 0f), P(s * .205f, .594f, -.06f), P(s * .348f, .781f, -.06f) },
                    p => Ease(.060f, .095f, p.x * s) * Ease(.15f, .20f, p.y));
            }
            // The mouth forms most of the core mesh. Its exact upper/lower
            // topology has not been segmented; a guessed jaw would tear it.
            return r;
        }

        static Recipe Snorlax(Bounds bounds)
        {
            var r = new Recipe(bounds);
            int waist = r.Bone("AuthoredWaist", -1, P(0f, .20f, -.04f));
            int spine = r.Bone("Spine1", waist, P(0f, .39f, 0f));
            int chest = r.Bone("Spine2", spine, P(0f, .62f, -.02f));
            int head = r.Bone("Head", chest, P(0f, .77f, .03f));
            r.BodyWeights = (p, w) =>
            {
                float upper = Ease(.19f, .36f, p.y), top = Ease(.49f, .69f, p.y), face = Ease(.72f, .84f, p.y);
                w[waist] = 1f - upper; w[spine] = upper * (1f - top);
                w[chest] = upper * top * (1f - face); w[head] = upper * top * face;
                return true;
            };
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side; string tag = s > 0 ? "L" : "R";
                r.Chain(new[] { tag + "Arm", tag + "ForeArm", tag + "Hand" }, chest,
                    new[] { P(s * .24f, .62f, -.03f), P(s * .36f, .57f, -.025f), P(s * .47f, .54f, -.01f) },
                    p => Ease(.23f, .31f, p.x * s) * Ease(.39f, .47f, p.y)
                        * (1f - Ease(.67f, .73f, p.y)), .10f, .16f);
                r.Chain(new[] { tag + "Thigh", tag + "Leg", tag + "Foot" }, waist,
                    new[] { P(s * .16f, .20f, .10f), P(s * .17f, .10f, .14f), P(s * .17f, .025f, .18f) },
                    p => Ease(.04f, .10f, p.x * s) * (1f - Ease(.17f, .29f, p.y)), .10f, .17f);
            }
            return r;
        }

        static Recipe Hitmonlee(Bounds bounds)
        {
            var r = new Recipe(bounds);
            int waist = r.Bone("AuthoredWaist", -1, P(0f, .38f, -.03f));
            int torso = r.Bone("Spine1", waist, P(0f, .57f, 0f));
            // Hitmonlee has an integrated head/body. Preserve the entire eye-bearing
            // oval as one region instead of inventing a neck through its face.
            r.BodyWeights = (p, w) =>
            { float body = Ease(.34f, .45f, p.y); w[waist] = 1f - body; w[torso] = body; return true; };
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side; string tag = s > 0 ? "L" : "R";
                r.Chain(new[] { tag + "Arm", tag + "ForeArm", tag + "Hand" }, torso,
                    new[] { P(s * .112f, .787f, 0f), P(s * .254f, .773f, 0f), P(s * .404f, .773f, 0f) },
                    p => Ease(.11f, .16f, p.x * s) * Ease(.54f, .65f, p.y));
                r.Chain(new[] { tag + "Thigh", tag + "Leg", tag + "Foot" }, waist,
                    new[] { P(s * .058f, .39f, -.025f), P(s * .06f, .22f, -.02f), P(s * .06f, .045f, .06f) },
                    p => Ease(.008f, .035f, p.x * s) * (1f - Ease(.36f, .44f, p.y)), .08f, .125f);
            }
            return r;
        }

        static Recipe Quadruped(Bounds bounds, bool tauros)
        {
            var r = new Recipe(bounds);
            int waist = r.Bone("AuthoredWaist", -1, P(0f, .44f, -.10f));
            int spine = r.Bone("Spine1", waist, P(0f, .47f, -.035f));
            int chest = r.Bone("Spine2", spine, P(0f, tauros ? .55f : .52f, .12f));
            int neck = r.Bone("Neck1", chest, P(0f, tauros ? .53f : .58f, tauros ? .22f : .285f));
            int head = r.Bone("Head", neck, P(0f, tauros ? .50f : .78f, tauros ? .37f : .356f));
            r.BodyWeights = (p, w) =>
            {
                float anterior = Ease(-.11f, .17f, p.z);
                float headMask = Ease(tauros ? .24f : .19f, tauros ? .36f : .32f, p.z)
                    * Ease(tauros ? .23f : .40f, tauros ? .36f : .55f, p.y);
                float headEnd = Ease(tauros ? .31f : .24f, tauros ? .40f : .38f, p.z);
                w[spine] = (1f - anterior) * (1f - headMask);
                w[chest] = anterior * (1f - headMask);
                w[neck] = headMask * (1f - headEnd); w[head] = headMask * headEnd;
                return true;
            };
            float front = tauros ? .245f : .258f;
            float rear = tauros ? -.235f : -.249f;
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side; string tag = s > 0 ? "L" : "R";
                float lateral = tauros ? .255f : .285f;
                r.Chain(new[] { tag + "Arm", tag + "ForeArm", tag + "Hand" }, chest,
                    new[] { P(s * lateral, tauros ? .443f : .50f, front), P(s * lateral, .23f, front + .075f), P(s * lateral, .025f, front + .025f) },
                    p => Ease(.055f, .15f, p.x * s) * (1f - Ease(.35f, .48f, p.y))
                        * Ease(-.01f, .07f, p.z), tauros ? .085f : .070f, tauros ? .14f : .12f);
                int frontFoot = r.Bones.Count - 1;
                r.AssignRigid(new[] { P(s * lateral, .025f, front + .025f), P(s * lateral, .11f, front + .025f) }, frontFoot,
                    p => Ease(.005f, .04f, p.x * s) * (1f - Ease(.08f, .13f, p.y))
                        * Ease(-.01f, .07f, p.z));
                r.Chain(new[] { tag + "Thigh", tag + "Leg", tag + "Foot" }, waist,
                    new[] { P(s * lateral, tauros ? .457f : .51f, rear), P(s * lateral, .22f, rear + .075f), P(s * lateral, .025f, rear - .01f) },
                    p => Ease(.055f, .15f, p.x * s) * (1f - Ease(.35f, .48f, p.y))
                        * (1f - Ease(-.045f, .035f, p.z)), tauros ? .085f : .070f, tauros ? .14f : .12f);
                int rearFoot = r.Bones.Count - 1;
                r.AssignRigid(new[] { P(s * lateral, .025f, rear - .01f), P(s * lateral, .11f, rear - .01f) }, rearFoot,
                    p => Ease(.005f, .04f, p.x * s) * (1f - Ease(.08f, .13f, p.y))
                        * (1f - Ease(-.045f, .035f, p.z)));
            }
            int tailCount = tauros ? 3 : 1;
            var tails = new Vector3[tailCount][];
            for (int branch = 0; branch < tailCount; branch++)
            {
                float offset = tauros ? (branch - 1) * .085f : 0f;
                tails[branch] = new[] { P(offset * .4f, tauros ? .607f : .60f, tauros ? -.33f : -.294f),
                    P(offset, tauros ? .75f : .66f, tauros ? -.41f : -.36f),
                    P(offset * 2.6f, tauros ? .94f - branch * .11f : .85f, -.48f) };
            }
            for (int branch = 0; branch < tailCount; branch++)
            {
                int chosen = branch;
                string label = ((char)('A' + branch)).ToString();
                r.Chain(new[] { "Tail" + label + "1", "Tail" + label + "2", "Tail" + label + "3" }, waist,
                    tails[branch], p => (1f - Ease(tauros ? -.33f : -.26f, tauros ? -.275f : -.20f, p.z))
                        * Ease(tauros ? .38f : .18f, tauros ? .49f : .25f, p.y)
                        * (ClosestBranch(p, tails, bounds) == chosen ? 1f : 0f));
            }
            return r;
        }

        static Recipe Lapras(Bounds bounds)
        {
            var r = new Recipe(bounds);
            int waist = r.Bone("AuthoredWaist", -1, P(0f, .24f, -.05f));
            r.BodyWeights = (p, w) => { w[waist] = 1f; return true; };
            r.Chain(new[] { "Neck1", "Neck2", "Head" }, waist,
                new[] { P(0f, .26f, .200f), P(0f, .552f, .275f), P(0f, .808f, .307f) },
                p => Ease(.15f, .24f, p.z) * Ease(.13f, .25f, p.y));
            // Head and ears share a rigid final region; the lower shell remains
            // exclusively attached to waist except at the actual neck junction.
            int head = r.Bones.Count - 1;
            r.AssignRigid(new[] { P(0f, .81f, .307f), P(0f, .99f, .307f) }, head,
                p => Ease(.74f, .83f, p.y) * Ease(.16f, .24f, p.z));
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side; string tag = s > 0 ? "L" : "R";
                r.Chain(new[] { tag + "FlipperFrontBase", tag + "FlipperFrontTip" }, waist,
                    new[] { P(s * .188f, .072f, .191f), P(s * .45f, .035f, .15f) },
                    p => Ease(.18f, .29f, p.x * s) * (1f - Ease(.16f, .25f, p.y))
                        * Ease(-.05f, .02f, p.z));
                r.Chain(new[] { tag + "FlipperRearBase", tag + "FlipperRearTip" }, waist,
                    new[] { P(s * .198f, .072f, -.239f), P(s * .45f, .24f, -.32f) },
                    p => Ease(.18f, .29f, p.x * s) * (1f - Ease(.31f, .40f, p.y))
                        * (1f - Ease(-.17f, -.07f, p.z)));
            }
            r.Chain(new[] { "TailA1", "TailA2" }, waist,
                new[] { P(0f, .10f, -.30f), P(0f, .045f, -.46f) },
                p => (1f - Ease(-.39f, -.30f, p.z)) * (1f - Ease(.08f, .19f, p.y))
                    * (1f - Ease(.075f, .15f, Mathf.Abs(p.x))), .07f, .12f);
            return r;
        }

        static Recipe Kingler(Bounds bounds)
        {
            var r = new Recipe(bounds);
            int waist = r.Bone("AuthoredWaist", -1, P(0f, .19f, -.02f));
            int body = r.Bone("Spine1", waist, P(0f, .28f, -.04f));
            r.BodyWeights = (p, w) => { w[body] = 1f; return true; };
            for (int side = -1; side <= 1; side += 2)
            {
                int s = side; string tag = s > 0 ? "L" : "R";
                bool large = s > 0;
                float handY = large ? .692f : .515f;
                r.Chain(new[] { tag + "Arm", tag + "ForeArm", tag + "Hand" }, body,
                    new[] { P(s * (large ? .173f : .161f), large ? .462f : .40f, .095f),
                        P(s * (large ? .310f : .297f), large ? .562f : .469f, large ? .191f : .136f),
                        P(s * (large ? .278f : .328f), handY, large ? .109f : .136f) },
                    p => Ease(.16f, .23f, p.x * s) * Ease(.23f, .31f, p.y), .085f, .15f);
                int hand = r.Bones.Count - 1;
                r.AssignRigid(new[] { P(s * .30f, handY, .15f), P(s * .30f, large ? .99f : .65f, .15f) }, hand,
                    p => Ease(.03f, .10f, p.x * s) * Ease(large ? .57f : .39f, large ? .68f : .49f, p.y));
                for (int leg = 0; leg < 3; leg++)
                {
                    float front = .20f - leg * .27f;
                    string suffix = (leg + 1).ToString();
                    r.Chain(new[] { tag + "Thigh" + suffix, tag + "Leg" + suffix, tag + "Foot" + suffix }, waist,
                        new[] { P(s * .17f, .22f, front * .4f), P(s * .32f, .12f, front), P(s * .46f, .025f, front) },
                        p => Ease(.16f, .25f, p.x * s) * (1f - Ease(.19f, .30f, p.y)), .04f, .075f);
                    int foot = r.Bones.Count - 1;
                    int branch = leg;
                    r.AssignRigid(new[] { P(s * .46f, .025f, front), P(s * .46f, .08f, front) }, foot,
                        p => Ease(.25f, .34f, p.x * s) * (1f - Ease(.055f, .11f, p.y))
                            * (Mathf.Clamp(Mathf.RoundToInt((.20f - p.z) / .27f), 0, 2) == branch ? 1f : 0f));
                }
            }
            return r;
        }

        static int ClosestBranch(Vector3 p, Vector3[][] branches, Bounds bounds)
        {
            Vector3 metric = new Vector3(bounds.size.x / bounds.size.y, 1f, bounds.size.z / bounds.size.y);
            Vector3 v = Vector3.Scale(p, metric);
            int chosen = 0; float distance = float.PositiveInfinity;
            for (int branch = 0; branch < branches.Length; branch++)
                for (int segment = 0; segment + 1 < branches[branch].Length; segment++)
                {
                    Vector3 a = Vector3.Scale(branches[branch][segment], metric);
                    Vector3 d = Vector3.Scale(branches[branch][segment + 1] - branches[branch][segment], metric);
                    float t = Mathf.Clamp01(Vector3.Dot(v - a, d) / Mathf.Max(.000001f, d.sqrMagnitude));
                    float value = (v - a - d * t).sqrMagnitude;
                    if (value < distance) { distance = value; chosen = branch; }
                }
            return chosen;
        }
    }

    /// <summary>Owns only the temporary generated skeleton, renderers, and mesh clones.</summary>
    public sealed class RuntimePokemonRigOwner : MonoBehaviour
    {
        struct RendererPair
        {
            public SkinnedMeshRenderer Generated;
            public MeshRenderer Original;
            public bool WasEnabled;
        }
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<RendererPair> renderers = new List<RendererPair>();
        Transform skeletonRoot;
        bool released;
        public int Species { get; private set; }
        public int BoneCount { get; private set; }
        public int RendererCount => renderers.Count;
        public bool EnabledForOriginalMotion { get; private set; } = true;
        internal void Initialize(int species) { Species = species; }
        internal void SetSkeletonRoot(Transform root) { skeletonRoot = root; }
        internal void OwnMesh(Mesh mesh) { meshes.Add(mesh); }
        internal void OwnRenderer(SkinnedMeshRenderer generated, MeshRenderer original)
        { renderers.Add(new RendererPair { Generated = generated, Original = original, WasEnabled = original.enabled }); }
        internal void Complete(int bones) { BoneCount = bones; }
        public void SetRigEnabled(bool enabled)
        {
            if (released) return;
            EnabledForOriginalMotion = enabled;
            foreach (var pair in renderers)
            {
                if (pair.Generated != null) pair.Generated.enabled = enabled;
                if (pair.Original != null) pair.Original.enabled = enabled ? false : pair.WasEnabled;
            }
        }
        internal void Release()
        {
            if (released) return;
            released = true;
            foreach (var pair in renderers)
            {
                if (pair.Original != null) pair.Original.enabled = pair.WasEnabled;
                if (pair.Generated != null) RuntimePokemonRig.DestroyRuntime(pair.Generated.gameObject);
            }
            foreach (var mesh in meshes) RuntimePokemonRig.DestroyRuntime(mesh);
            if (skeletonRoot != null) RuntimePokemonRig.DestroyRuntime(skeletonRoot.gameObject);
            renderers.Clear(); meshes.Clear();
        }
        void OnDestroy() { Release(); }
    }
}

