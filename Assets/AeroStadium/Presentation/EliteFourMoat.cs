using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>A bounded liquid channel with pooled, staggered boiling eruptions.</summary>
    public sealed class EliteFourMoat : MonoBehaviour
    {
        public const float InnerApothem = 14.5f;
        public const float OuterApothem = 26f;
        public const float SurfaceElevation = -1.15f;
        const int SideSegments = 32, RadialSegments = 24;
        const int Sources = 18, DomeRings = 5, DomeSides = 32, RippleSides = 48, RippleRings = 4;
        const int DropSides = 12, DropRings = 6, DropCount = 4;
        const int DomeVertices = (DomeRings + 1) * (DomeSides + 1);
        const int RippleVertices = (RippleSides + 1) * (RippleRings + 1);
        const int DropVertices = (DropSides + 1) * (DropRings + 1);
        const int DropletVertices = DropCount * DropVertices;
        const int SourceVertices = DomeVertices + RippleVertices + DropletVertices;
        Mesh fluidMesh, boilingMesh;
        MeshRenderer fluidRenderer;
        Material boilingMaterial;
        Matrix4x4 waveTransform;
        float waveTime, waveHeight;
        Vector3[] effectVertices, effectNormals;
        readonly Vector2[] sourcePositions = new Vector2[Sources];
        readonly int[] sourceCycles = new int[Sources];
        readonly Vector3[] dropDirections = new Vector3[DropVertices];
        public int RendererCount => 2;
        public int TriangleCount => 8 * SideSegments * RadialSegments * 2 + Sources *
            (DomeRings * DomeSides * 2 + RippleSides * RippleRings * 2 + DropCount * DropRings * DropSides * 2);
        public int ColliderCount => 0;
        public int BubbleCount => Sources;
        public int ActiveBubbles { get; private set; }
        public int BubblesPopped { get; private set; }
        public int PeakActiveBubbles { get; private set; }
        public float AnimationPhase => Time.time * .35f;
        public float AnimatedValue => Mathf.Sin(Time.time * .64f);
        public Material SurfaceMaterial => fluidRenderer != null ? fluidRenderer.sharedMaterial : null;

        public static EliteFourMoat Build(Transform parent, Material lava)
        {
            if (lava == null) throw new ArgumentNullException(nameof(lava));
            var go = new GameObject("Elite Four · bounded boiling lava moat");
            go.transform.SetParent(parent, false);
            var moat = go.AddComponent<EliteFourMoat>();
            moat.Create(lava);
            return moat;
        }

        void Create(Material lava)
        {
            int columns = 8 * SideSegments + 1;
            var vertices = new Vector3[columns * (RadialSegments + 1)];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[(columns - 1) * RadialSegments * 6];
            for (int ring = 0; ring <= RadialSegments; ring++)
            {
                float apothem = Mathf.Lerp(InnerApothem, OuterApothem, ring / (float)RadialSegments);
                for (int column = 0; column < columns; column++)
                {
                    int side = (column / SideSegments) % 8;
                    float fraction = (column % SideSegments) / (float)SideSegments;
                    int index = ring * columns + column;
                    vertices[index] = Vector3.Lerp(Corner(side, apothem), Corner(side + 1, apothem), fraction);
                    normals[index] = Vector3.up;
                }
            }
            int t = 0;
            for (int ring = 0; ring < RadialSegments; ring++)
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = ring * columns + column, b = a + 1, c = a + columns, d = c + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = d;
                    triangles[t++] = a; triangles[t++] = d; triangles[t++] = c;
                }
            fluidMesh = new Mesh { name = "Elite Four · octagonal liquid channel", vertices = vertices,
                normals = normals, triangles = triangles };
            fluidMesh.RecalculateBounds();
            var bounds = fluidMesh.bounds; bounds.Expand(new Vector3(0, 1f, 0)); fluidMesh.bounds = bounds;
            gameObject.AddComponent<MeshFilter>().sharedMesh = fluidMesh;
            fluidRenderer = gameObject.AddComponent<MeshRenderer>();
            fluidRenderer.sharedMaterial = lava;
            fluidRenderer.shadowCastingMode = ShadowCastingMode.Off;
            fluidRenderer.receiveShadows = true;
            CreateBoiling(lava);
        }

        static Vector3 Corner(int side, float apothem)
        {
            float angle = (22.5f + side * 45f) * Mathf.Deg2Rad;
            float radius = apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
            return new Vector3(Mathf.Cos(angle) * radius, SurfaceElevation, Mathf.Sin(angle) * radius);
        }

        void CreateBoiling(Material lava)
        {
            effectVertices = new Vector3[Sources * SourceVertices];
            effectNormals = new Vector3[effectVertices.Length];
            var colors = new Color[effectVertices.Length];
            var triangles = new int[Sources * (DomeRings * DomeSides * 6 + RippleSides * RippleRings * 6 +
                DropCount * DropRings * DropSides * 6)];
            for (int r = 0; r <= DropRings; r++)
                for (int s = 0; s <= DropSides; s++)
                {
                    float latitude = r / (float)DropRings * Mathf.PI;
                    float angle = s / (float)DropSides * Mathf.PI * 2;
                    dropDirections[r * (DropSides + 1) + s] = new Vector3(Mathf.Sin(latitude) * Mathf.Cos(angle),
                        Mathf.Cos(latitude), Mathf.Sin(latitude) * Mathf.Sin(angle));
                }
            int t = 0;
            for (int i = 0; i < Sources; i++)
            {
                // Golden-angle spacing, with clear gaps below both causeways.
                float angle = (31 + i * 137.5078f) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (Mathf.Abs(direction.y) < .34f)
                    direction = new Vector2(direction.x, Mathf.Sign(direction.y) * .34f).normalized;
                float distance = Mathf.Max(Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y)),
                    (Mathf.Abs(direction.x) + Mathf.Abs(direction.y)) / Mathf.Sqrt(2));
                sourcePositions[i] = direction * ((18.1f + (i % 4) * 1.05f) / distance);
                sourceCycles[i] = Mathf.FloorToInt(Cycle(i));
                int start = i * SourceVertices;
                for (int r = 0; r < DomeRings; r++)
                    for (int s = 0; s < DomeSides; s++)
                    {
                        int a = start + r * (DomeSides + 1) + s;
                        int b = a + 1, c = a + DomeSides + 1, d = c + 1;
                        AddFace(triangles, ref t, a, d, c); AddFace(triangles, ref t, a, b, d);
                    }
                int ripple = start + DomeVertices;
                for (int r = 0; r < RippleRings; r++)
                    for (int s = 0; s < RippleSides; s++)
                    {
                        int a = ripple + r * (RippleSides + 1) + s;
                        int b = a + 1, c = a + RippleSides + 1, d = c + 1;
                        AddFace(triangles, ref t, a, b, d); AddFace(triangles, ref t, a, d, c);
                    }
                int drops = ripple + RippleVertices;
                for (int d = 0; d < DropCount; d++)
                    for (int r = 0; r < DropRings; r++)
                        for (int s = 0; s < DropSides; s++)
                        {
                            int a = drops + d * DropVertices + r * (DropSides + 1) + s;
                            int b = a + 1, c = a + DropSides + 1, e = c + 1;
                            AddFace(triangles, ref t, a, e, c); AddFace(triangles, ref t, a, b, e);
                        }
                for (int r = 0; r <= DomeRings; r++)
                    for (int s = 0; s <= DomeSides; s++)
                    {
                        float heat = 1 - r / (float)DomeRings;
                        colors[start + r * (DomeSides + 1) + s] = new Color(.80f + heat * .19f,
                            .62f + heat * .32f, .32f + heat * .34f);
                    }
                for (int r = 0; r <= RippleRings; r++)
                    for (int s = 0; s <= RippleSides; s++)
                    {
                        float crest = Mathf.Sin(r / (float)RippleRings * Mathf.PI);
                        colors[ripple + r * (RippleSides + 1) + s] = new Color(.78f + crest * .18f,
                            .65f + crest * .30f, .35f + crest * .35f);
                    }
                for (int v = drops; v < start + SourceVertices; v++) colors[v] = new Color(.96f, .87f, .60f);
            }
            boilingMaterial = new Material(lava) { name = "Elite Four · molten bubble skins and splashes" };
            boilingMaterial.SetFloat("_SurfacePattern", 6);
            boilingMaterial.SetFloat("_Relief", 0);
            boilingMaterial.SetFloat("_Smoothness", .86f);
            boilingMaterial.SetColor("_BaseColor", new Color(.77f, .12f, .008f));
            boilingMaterial.SetColor("_EmissionColor", new Color(1.20f, .19f, .008f));
            boilingMaterial.SetFloat("_Cull", (float)CullMode.Off);
            boilingMesh = new Mesh { name = "Elite Four · pooled molten domes and burst ripples",
                vertices = effectVertices, normals = effectNormals, colors = colors, triangles = triangles };
            boilingMesh.MarkDynamic();
            boilingMesh.bounds = new Bounds(new Vector3(0, SurfaceElevation + .5f, 0), new Vector3(55, 5, 55));
            var go = new GameObject(boilingMesh.name); go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = boilingMesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = boilingMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            UpdateBoiling();
        }

        static void AddFace(int[] triangles, ref int t, int a, int b, int c)
        { triangles[t++] = a; triangles[t++] = b; triangles[t++] = c; }

        float Cycle(int i) => Time.time / (5.6f + (i % 4) * .43f) + i * .618034f;

        // Same three waves as the GPU surface: splashes follow the swell instead
        // of hovering above it. Arrays and meshes are retained across all cycles.
        float Height(float x, float z)
        {
            var world = waveTransform.MultiplyPoint3x4(new Vector3(x, SurfaceElevation, z));
            float time = waveTime;
            float a = world.x * .43f + world.z * .18f - time * .61f;
            float b = world.x * -.17f + world.z * .36f + time * .47f;
            float c = world.x * 1.25f + world.z * .74f - time * 1.32f;
            return SurfaceElevation + (Mathf.Sin(a) * .56f + Mathf.Sin(b) * .28f + Mathf.Sin(c) * .16f) *
                waveHeight;
        }

        void Update() => UpdateBoiling();

        void UpdateBoiling()
        {
            if (boilingMesh == null) return;
            waveTransform = transform.localToWorldMatrix;
            waveTime = Time.time;
            waveHeight = SurfaceMaterial.GetFloat("_LavaWaveHeight");
            ActiveBubbles = 0;
            for (int i = 0; i < Sources; i++)
            {
                float cycle = Cycle(i), phase = Mathf.Repeat(cycle, 1);
                int poppedCycle = Mathf.FloorToInt(cycle - .43f);
                if (poppedCycle >= sourceCycles[i])
                { BubblesPopped += poppedCycle - sourceCycles[i] + 1; sourceCycles[i] = poppedCycle + 1; }
                Vector2 center = sourcePositions[i];
                float radius = .43f + (i % 4) * .13f;
                int start = i * SourceVertices;
                float grow = Mathf.SmoothStep(0, 1, Mathf.Clamp01(phase / .34f));
                float collapse = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - .40f) / .05f));
                float domeSize = grow * collapse;
                if (domeSize > .1f) ActiveBubbles++;
                float domeHeight = radius * .68f * domeSize;
                for (int r = 0; r <= DomeRings; r++)
                {
                    float latitude = r / (float)DomeRings * Mathf.PI * .5f;
                    float width = Mathf.Sin(latitude) * radius * domeSize;
                    float cosine = Mathf.Cos(latitude);
                    float elevation = cosine * cosine * cosine * domeHeight;
                    for (int s = 0; s <= DomeSides; s++)
                    {
                        float angle = s / (float)DomeSides * Mathf.PI * 2;
                        float dx = Mathf.Cos(angle), dz = Mathf.Sin(angle);
                        float irregularity = 1 + Mathf.Sin(latitude) * (.065f * Mathf.Sin(angle * 3 + i + phase * 9)
                            + .035f * Mathf.Cos(angle * 5 - phase * 7));
                        float x = center.x + dx * width * irregularity;
                        float z = center.y + dz * width * irregularity;
                        int v = start + r * (DomeSides + 1) + s;
                        float pressure = 1 + .08f * Mathf.Sin(angle * 2 + i + phase * 6) * Mathf.Sin(latitude);
                        effectVertices[v] = new Vector3(x, Height(x, z) + elevation * pressure + .008f, z);
                        float slope = 3 * .68f * Mathf.Sin(latitude) * cosine * pressure;
                        effectNormals[v] = new Vector3(dx * slope, 1, dz * slope).normalized;
                    }
                }
                float burst = Mathf.Clamp01((phase - .43f) / .40f);
                float envelope = phase >= .43f && phase < .83f ? Mathf.Sin(burst * Mathf.PI) : 0;
                float ringRadius = radius + burst * 2.05f;
                int ripple = start + DomeVertices;
                float halfWidth = (.24f - burst * .15f) * envelope;
                float crestHeight = .15f * envelope * (1 - burst * .72f);
                for (int r = 0; r <= RippleRings; r++)
                    for (int s = 0; s <= RippleSides; s++)
                    {
                        float u = r / (float)RippleRings;
                        float angle = s / (float)RippleSides * Mathf.PI * 2;
                        float ring = envelope > .001f ? ringRadius + (u * 2 - 1) * halfWidth : 0;
                        float x = center.x + Mathf.Cos(angle) * ring;
                        float z = center.y + Mathf.Sin(angle) * ring;
                        float irregularity = .86f + .14f * Mathf.Sin(angle * 5 + i);
                        float crest = Mathf.Sin(u * Mathf.PI) * crestHeight * irregularity;
                        int v = ripple + r * (RippleSides + 1) + s;
                        effectVertices[v] = new Vector3(x, Height(x, z) + crest + .007f * envelope, z);
                        float slope = halfWidth > .001f ? Mathf.Cos(u * Mathf.PI) * Mathf.PI *
                            crestHeight * irregularity / (halfWidth * 2) : 0;
                        effectNormals[v] = new Vector3(-Mathf.Cos(angle) * slope, 1,
                            -Mathf.Sin(angle) * slope).normalized;
                    }
                int drops = ripple + RippleVertices;
                float flight = Mathf.Clamp01((phase - .43f) / .17f);
                float dropletSize = phase >= .43f && phase < .60f ? Mathf.Sin(flight * Mathf.PI) * .12f : 0;
                for (int d = 0; d < DropCount; d++)
                {
                    float angle = (i * 1.71f + d * Mathf.PI * .5f);
                    float x = center.x + Mathf.Cos(angle) * (radius * .5f + flight * .95f);
                    float z = center.y + Mathf.Sin(angle) * (radius * .5f + flight * .95f);
                    var position = new Vector3(x, Height(x, z) + 4 * flight * (1 - flight) *
                        (.65f + d * .12f), z);
                    var velocity = new Vector3(Mathf.Cos(angle) * .95f, (1 - flight * 2) *
                        4 * (.65f + d * .12f), Mathf.Sin(angle) * .95f);
                    Quaternion orientation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
                    int startDrop = drops + d * DropVertices;
                    for (int v = 0; v < DropVertices; v++)
                    {
                        Vector3 direction = dropDirections[v];
                        var elongated = new Vector3(direction.x, direction.y * 1.45f, direction.z);
                        effectVertices[startDrop + v] = position + orientation * elongated * dropletSize;
                        effectNormals[startDrop + v] = orientation *
                            new Vector3(direction.x, direction.y / 1.45f, direction.z).normalized;
                    }
                }
            }
            boilingMesh.SetVertices(effectVertices, 0, effectVertices.Length, MeshUpdateFlags.DontRecalculateBounds);
            boilingMesh.SetNormals(effectNormals, 0, effectNormals.Length, MeshUpdateFlags.DontRecalculateBounds);
            PeakActiveBubbles = Mathf.Max(PeakActiveBubbles, ActiveBubbles);
        }

        public bool ValidateMoat(out string error)
        {
            error = null;
            Material lava = SurfaceMaterial;
            if (fluidMesh == null || lava == null) error = "The bounded moat has no surface.";
            else if (GetComponent<Collider>() != null) error = "The molten surface must not be a solid floor.";
            else if (lava.shader == null || !lava.shader.isSupported || !lava.HasProperty("_LavaFlowSpeed") ||
                !lava.HasProperty("_LavaWaveHeight") || Mathf.Abs(lava.GetFloat("_SurfacePattern") - 5) > .1f ||
                lava.GetFloat("_LavaFlowSpeed") <= 0) error = "The moat material must support animated lava.";
            else if (fluidMesh.bounds.size.x > OuterApothem * 2 + .02f ||
                fluidMesh.bounds.size.z > OuterApothem * 2 + .02f)
                error = "The molten surface exceeds its retaining channel.";
            else foreach (Vector3 vertex in fluidMesh.vertices)
            {
                float octagonalDistance = Mathf.Max(Mathf.Max(Mathf.Abs(vertex.x), Mathf.Abs(vertex.z)),
                    (Mathf.Abs(vertex.x) + Mathf.Abs(vertex.z)) / Mathf.Sqrt(2));
                if (octagonalDistance < InnerApothem - .02f)
                { error = "The moat mesh fills the central courtyard."; break; }
            }
            if (error == null && !ValidateBoiling(out error)) return false;
            return error == null;
        }

        public bool ValidateBoiling(out string error)
        {
            error = null;
            if (boilingMesh == null || boilingMaterial == null || !boilingMaterial.shader.isSupported)
                error = "Boiling effects are missing or unsupported.";
            else if (GetComponentsInChildren<Renderer>().Length != RendererCount)
                error = "The moat effects must remain batched into two renderers.";
            else if (GetComponentsInChildren<Collider>().Length != 0)
                error = "Boiling lava must not add a solid collision surface.";
            else for (int i = 0; i < effectVertices.Length; i++)
            {
                Vector3 vertex = effectVertices[i];
                if (float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) || float.IsNaN(vertex.y) ||
                    float.IsInfinity(vertex.y) || float.IsNaN(vertex.z) || float.IsInfinity(vertex.z))
                { error = "A molten effect vertex is not finite."; break; }
            }
            return error == null;
        }

        void OnDestroy()
        {
            Release(fluidMesh); Release(boilingMesh); Release(boilingMaterial);
        }

        static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
