using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>An original detached, ground-borne breaker. Shield contact is against the visible ellipsoid.</summary>
    public sealed class SurfEffect : MonoBehaviour
    {
        const int Columns = 40, BodyRows = 24, CrestRows = 5, DrainRows = 8, WakeRows = 12;
        const int CrestDrops = 72, ContactDrops = 56, DropCount = CrestDrops + ContactDrops;
        const float Total = 2.90f, Launch = .18f, TargetTime = 1.12f, Skin = .026f;
        readonly Surface[] surfaces = new Surface[4];
        readonly Material[] materials = new Material[4];
        readonly MeshRenderer[] renderers = new MeshRenderer[4];
        readonly bool[] contacted = new bool[Columns + 1];
        readonly Vector3[] contacts = new Vector3[Columns + 1], contactNormals = new Vector3[Columns + 1];
        readonly float[] contactTimes = new float[Columns + 1];
        Vector3 source, target, origin, forward, side;
        Bounds victim, shield;
        float elapsed, floorY, width, height, depth, distance, speed, travel;
        bool initialized, detached, released, hasVictim, hasShield;
        Camera viewCamera;

        public bool ImpactEnabled { get; set; } = true;
        public float Duration => Total;
        public float EffectProgress => Mathf.Clamp01(elapsed / Total);
        public bool ImpactReached { get; private set; }
        public bool Completed => initialized && elapsed >= Total;
        public bool IsPlaying => initialized && !released && !Completed;
        public bool IsDetached => detached;
        public bool HasActualContact { get; private set; }
        public Vector3 ActualContact { get; private set; }
        public Vector3 ContactNormal { get; private set; }
        public float ImpactOpacity { get; private set; }
        public float ContactOpacity { get; private set; }
        public int RunoffDrops { get; private set; }
        public int ShieldContacts { get; private set; }
        public int PeakDroplets { get; private set; }
        public int RejectedShieldTriangles { get; private set; }
        public int RendererCount => 4;
        public int TriangleCount => BodyRows * Columns * 2 + CrestRows * Columns * 2
            + (Columns + 1) * DrainRows * 2 + WakeRows * Columns * 2 + DropCount * 2;
        public float WaveTravelled => travel;
        public float PeakHeight { get; private set; }
        public float PeakWidth => width;

        public static SurfEffect Create(Transform parent, Vector3 source, Vector3 target, float width = 2.6f)
        {
            if (!Finite(source) || !Finite(target) || Flat(target - source).sqrMagnitude < .0001f)
                throw new ArgumentException("Surf requires distinct finite horizontal endpoints.");
            var go = new GameObject("Surf · original rolling breaker");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<SurfEffect>();
            effect.source = source; effect.target = target;
            effect.width = Safe(width) ? Mathf.Clamp(width, 3.6f, 4.8f) : 4.2f;
            effect.height = Mathf.Clamp(effect.width * .64f, 2.3f, 3.05f);
            effect.depth = effect.width * .72f;
            // The presentation supplies the creature root floor explicitly; zero is the arena's default floor.
            effect.floorY = .018f;
            try { effect.Initialize(); }
            catch { Dispose(go); throw; }
            return effect;
        }

        public void SetEndpoints(Vector3 source, Vector3 target)
        {
            if (!Finite(source) || !Finite(target) || Flat(target - source).sqrMagnitude < .0001f) return;
            this.source = source; this.target = target;
            // Origin, direction and speed are captured once on emission, so a moving mouth cannot drag the wave.
        }
        public void SetVictimBounds(Bounds bounds)
        {
            if (!ValidBounds(bounds)) return;
            victim = bounds; hasVictim = true;
        }
        public void SetFloor(float ground)
        {
            if (Safe(ground)) floorY = ground + .018f;
        }
        public void SetShieldSurface(Bounds bounds, float ground)
        {
            if (!ValidBounds(bounds) || !Safe(ground)) return;
            shield = bounds; hasShield = true; floorY = ground + .018f;
        }
        // Bounds, rather than a single shield plane, are authoritative for this broad attack.
        public void SetShieldContact(Vector3 point, Vector3 normal)
        {
            if (!Finite(point) || !Finite(normal) || normal.sqrMagnitude < .0001f) return;
            // Kept for presentation API compatibility. SetShieldSurface is required to arrest the breaker.
        }

        void Initialize()
        {
            Material reference = Resources.Load<Material>("Materials/SurfReference");
            Shader shader = reference != null ? reference.shader : Shader.Find("AeroStadium/Original Surf Breaker");
            if (shader == null) throw new InvalidOperationException("The original Surf material is missing.");
            surfaces[0] = new Surface("Surf · translucent curled body", (BodyRows + 1) * (Columns + 1), BodyRows * Columns * 6);
            surfaces[1] = new Surface("Surf · aerated lip and shield drainage", (CrestRows + 1) * (Columns + 1)
                + (Columns + 1) * (DrainRows + 1) * 2, (CrestRows * Columns + (Columns + 1) * DrainRows) * 6);
            surfaces[2] = new Surface("Surf · crest spray and contact droplets", DropCount * 4, DropCount * 6);
            surfaces[3] = new Surface("Surf · receding floor wash", (WakeRows + 1) * (Columns + 1), WakeRows * Columns * 6);
            Grid(surfaces[0], 0, BodyRows, Columns);
            Grid(surfaces[1], 0, CrestRows, Columns);
            int drains = (CrestRows + 1) * (Columns + 1);
            for (int c = 0; c <= Columns; c++) Grid(surfaces[1], drains + c * (DrainRows + 1) * 2, DrainRows, 1);
            for (int d = 0; d < DropCount; d++) Grid(surfaces[2], d * 4, 1, 1);
            Grid(surfaces[3], 0, WakeRows, Columns);
            for (int i = 0; i < surfaces.Length; i++)
            {
                surfaces[i].Finish();
                materials[i] = reference != null ? new Material(reference) : new Material(shader);
                materials[i].name = surfaces[i].mesh.name; materials[i].SetFloat("_Mode", i);
                var go = new GameObject(surfaces[i].mesh.name); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = surfaces[i].mesh;
                renderers[i] = go.AddComponent<MeshRenderer>(); renderers[i].sharedMaterial = materials[i];
                renderers[i].shadowCastingMode = ShadowCastingMode.Off; renderers[i].receiveShadows = false;
                renderers[i].lightProbeUsage = LightProbeUsage.Off; renderers[i].reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            viewCamera = Camera.main; initialized = true; RenderEffect();
        }
        static void Grid(Surface s, int start, int rows, int cols)
        {
            for (int row = 0; row <= rows; row++) for (int col = 0; col <= cols; col++)
            {
                int v = start + row * (cols + 1) + col;
                s.uv[v] = new Vector2(col / (float)cols, row / (float)rows);
                if (row == rows || col == cols) continue;
                int a = v, b = a + 1, c = a + cols + 1, d = c + 1;
                s.indices[s.cursor++] = a; s.indices[s.cursor++] = c; s.indices[s.cursor++] = b;
                s.indices[s.cursor++] = b; s.indices[s.cursor++] = c; s.indices[s.cursor++] = d;
            }
        }
        void Update()
        {
            if (!IsPlaying) return;
            elapsed = Mathf.Min(Total, elapsed + Time.deltaTime);
            if (elapsed >= Total) { ImpactOpacity = ContactOpacity = 0; ReleaseResources(); return; }
            RenderEffect();
        }
        void Capture()
        {
            forward = Flat(target - source).normalized; side = Vector3.Cross(Vector3.up, forward);
            origin = new Vector3(source.x, floorY, source.z) + forward * .62f;
            distance = Mathf.Max(.6f, Vector3.Dot(target - origin, forward));
            speed = distance / (TargetTime - Launch); detached = true;
        }
        void RenderEffect()
        {
            if (!detached && elapsed >= Launch) Capture();
            if (!detached)
            {
                forward = Flat(target - source).normalized; side = Vector3.Cross(Vector3.up, forward);
                origin = new Vector3(source.x, floorY, source.z) + forward * .62f;
            }
            travel = detached ? Mathf.Min(distance + 2.4f, Mathf.Max(0, elapsed - Launch) * speed) : 0;
            float grow = Smooth(Launch, .38f, elapsed), fade = 1 - Smooth(1.58f, 2.20f, elapsed);
            float amplitude = grow * (1 - Smooth(1.55f, 2.25f, elapsed) * .72f);
            ImpactOpacity = HasActualContact && !hasShield && ImpactEnabled ? ContactEnvelope() : 0;
            ContactOpacity = 0; RunoffDrops = 0; RejectedShieldTriangles = 0;
            Matrix4x4 local = transform.worldToLocalMatrix, normalMatrix = transform.localToWorldMatrix.transpose;
            for (int row = 0; row <= BodyRows; row++) for (int col = 0; col <= Columns; col++)
            {
                float u = row / (float)BodyRows, lateral = col / (float)Columns * 2 - 1;
                WavePoint(u, lateral, amplitude, out Vector3 p, out Vector3 n);
                float alpha = grow * fade * (1 - Smooth(.77f, 1, Mathf.Abs(lateral))) * Smooth(0, .10f, u);
                p = Arrest(p, col, true);
                float foam = Smooth(.55f, .80f, u) * .32f;
                Put(surfaces[0], row * (Columns + 1) + col, p, n, new Color(foam, 1 - u, alpha, lateral * 3), local, normalMatrix);
            }
            for (int row = 0; row <= CrestRows; row++) for (int col = 0; col <= Columns; col++)
            {
                float u = Mathf.Lerp(.69f, 1, row / (float)CrestRows), lateral = col / (float)Columns * 2 - 1;
                WavePoint(u, lateral, amplitude, out Vector3 p, out Vector3 n);
                p += n * .012f;
                float ragged = .82f + .18f * Mathf.Sin(col * 2.37f + elapsed * 14 + row * .6f);
                float alpha = grow * fade * ragged * (1 - Smooth(.75f, 1, Mathf.Abs(lateral)));
                p = Arrest(p, col, false);
                Put(surfaces[1], row * (Columns + 1) + col, p, n, new Color(1, .25f, alpha * .88f, lateral * 3), local, normalMatrix);
            }
            RenderDrains(local, normalMatrix);
            RenderDrops(grow, fade, amplitude, local, normalMatrix);
            RenderWake(grow, local, normalMatrix);
            for (int i = 0; i < surfaces.Length; i++) Upload(surfaces[i]);
        }
        void WavePoint(float u, float lateral, float amplitude, out Vector3 p, out Vector3 n)
        {
            float edge = 1 - .35f * Mathf.Pow(Mathf.Abs(lateral), 2.5f);
            float h = height * amplitude * edge;
            float x, y, dx, dy;
            if (u <= .72f)
            {
                float a = u / .72f;
                x = -depth * (1 - a); y = h * (.5f - .5f * Mathf.Cos(a * Mathf.PI));
                dx = depth / .72f; dy = h * Mathf.PI / 1.44f * Mathf.Sin(a * Mathf.PI);
            }
            else
            {
                float a = (u - .72f) / .28f * 1.70f, curl = h * .34f;
                x = curl * Mathf.Sin(a); y = h - curl * (1 - Mathf.Cos(a));
                dx = curl * Mathf.Cos(a) * 1.70f / .28f; dy = -curl * Mathf.Sin(a) * 1.70f / .28f;
            }
            float flutter = Mathf.Sin(lateral * 17 + elapsed * 11 + u * 4) * .023f * amplitude;
            y += flutter * Mathf.Sin(Mathf.PI * u);
            float bow = (1 - lateral * lateral) * width * .055f;
            p = origin + forward * Mathf.Max(0, travel + x + bow) + side * (lateral * width * .5f);
            p.y = Mathf.Max(floorY, floorY + y);
            n = (-forward * dy + Vector3.up * dx).normalized;
            if (n.sqrMagnitude < .01f) n = Vector3.up;
            PeakHeight = Mathf.Max(PeakHeight, p.y - floorY);
        }

        Vector3 Arrest(Vector3 p, int column, bool detectContact)
        {
            if (!detached) return p;
            if (hasShield)
            {
                Vector3 ray = p - forward * Vector3.Dot(p - origin, forward);
                if (RayEllipsoid(ray, forward, shield, out float entry, out Vector3 hit, out Vector3 normal)
                    && Vector3.Dot(p - ray, forward) >= entry - .003f)
                {
                    if (!contacted[column] && detectContact)
                    {
                        contacted[column] = true; contacts[column] = hit; contactNormals[column] = normal;
                        contactTimes[column] = elapsed; ShieldContacts++;
                        RegisterContact(hit, normal);
                    }
                    p = hit + normal * Skin;
                }
                return Outside(p, .005f);
            }
            if (detectContact && ImpactEnabled && !ImpactReached)
            {
                Bounds body = hasVictim ? victim : new Bounds(target, new Vector3(.8f, 1.5f, .8f));
                Vector3 ray = p - forward * Vector3.Dot(p - origin, forward);
                if (RayEllipsoid(ray, forward, body, out float entry, out Vector3 hit, out Vector3 normal)
                    && Vector3.Dot(p - ray, forward) >= entry) RegisterContact(hit, normal);
            }
            return p;
        }
        void RegisterContact(Vector3 point, Vector3 normal)
        {
            if (HasActualContact) return;
            HasActualContact = true; ActualContact = point; ContactNormal = normal; ImpactReached = true;
            // Presentation owns the single body reaction; this flag never resets when water passes the target.
            firstContactAt = elapsed;
        }
        float firstContactAt;
        float ContactEnvelope() => (1 - Smooth(.35f, .85f, elapsed - firstContactAt))
            * Smooth(0, .055f, elapsed - firstContactAt);

        void RenderDrains(Matrix4x4 local, Matrix4x4 normalMatrix)
        {
            int start = (CrestRows + 1) * (Columns + 1);
            for (int col = 0; col <= Columns; col++)
            {
                float age = elapsed - contactTimes[col];
                float active = hasShield && contacted[col] ? Smooth(0, .06f, age) * (1 - Smooth(.65f, 1.35f, age)) : 0;
                ContactOpacity = Mathf.Max(ContactOpacity, active);
                for (int row = 0; row <= DrainRows; row++) for (int edge = 0; edge < 2; edge++)
                {
                    float u = row / (float)DrainRows;
                    Vector3 p = Drain(col, Mathf.Clamp01(u * Mathf.Clamp01(age / .42f)), out Vector3 n);
                    Vector3 tangent = Vector3.Cross(n, Vector3.up).normalized;
                    if (tangent.sqrMagnitude < .001f) tangent = side;
                    p = Outside(p + tangent * ((edge == 0 ? -1 : 1) * width * .014f), Skin);
                    float alpha = active * Mathf.Sin(Mathf.PI * Mathf.Clamp(u, .02f, .98f)) * .72f;
                    Put(surfaces[1], start + col * (DrainRows + 1) * 2 + row * 2 + edge,
                        p, n, new Color(.8f, .1f, alpha, col * .67f), local, normalMatrix);
                }
            }
        }
        Vector3 Drain(int col, float u, out Vector3 normal)
        {
            if (!hasShield || !contacted[col]) { normal = Vector3.up; return origin; }
            Vector3 r = shield.extents, q = Divide(contacts[col] - shield.center, r).normalized;
            float elevation = Mathf.Asin(Mathf.Clamp(q.y, -1, 1));
            Vector3 horizontal = Flat(q).normalized;
            if (horizontal.sqrMagnitude < .001f) horizontal = -forward;
            // Both halves peel around the corresponding shoulder of the shell, then run down to the floor.
            float sign = Vector3.Dot(contacts[col] - shield.center, side) >= 0 ? 1 : -1;
            Vector3 tangent = Vector3.Cross(Vector3.up, horizontal);
            float yawSign = Vector3.Dot(tangent, side) * sign >= 0 ? 1 : -1;
            float angle = yawSign * .84f * Smooth(0, .80f, u);
            horizontal = horizontal * Mathf.Cos(angle) + tangent * Mathf.Sin(angle);
            elevation = Mathf.Lerp(elevation, -1.28f, Smooth(.06f, .90f, u));
            q = horizontal * Mathf.Cos(elevation) + Vector3.up * Mathf.Sin(elevation);
            Vector3 p = shield.center + Vector3.Scale(q, r);
            normal = Divide(q, r).normalized; p += normal * Skin;
            if (u > .85f)
            {
                float fall = (u - .85f) / .15f;
                p += side * sign * fall * .18f;
                p.y = Mathf.Lerp(p.y, floorY, fall);
            }
            p.y = Mathf.Max(floorY, p.y); return Outside(p, Skin);
        }
        void RenderDrops(float grow, float fade, float amplitude, Matrix4x4 local, Matrix4x4 normalMatrix)
        {
            if (viewCamera == null) viewCamera = Camera.main;
            Vector3 cameraRight = viewCamera != null ? viewCamera.transform.right : side;
            Vector3 cameraUp = viewCamera != null ? viewCamera.transform.up : Vector3.up;
            int active = 0;
            for (int i = 0; i < DropCount; i++)
            {
                float alpha = 0, size = .018f + Hash(i + 9) * .027f, stretch = 1;
                Vector3 p = origin, n = Vector3.up;
                if (i < CrestDrops)
                {
                    float phase = Mathf.Repeat((elapsed - Launch) * 1.18f + i * .618034f, 1);
                    float lateral = Mathf.Lerp(-.94f, .94f, Hash(i * 3 + 1));
                    WavePoint(.79f, lateral, amplitude, out p, out n);
                    float age = phase * .58f;
                    p += forward * age * (.48f + Hash(i + 6) * .65f) + side * age * Mathf.Sign(lateral) * .34f
                        + Vector3.up * (age * (1.25f + Hash(i) * .9f) - age * age * 3.5f);
                    alpha = grow * fade * Mathf.Sin(Mathf.PI * phase) * .78f;
                    stretch = 1.2f + phase * 1.7f;
                    p = Arrest(p, Mathf.RoundToInt((lateral * .5f + .5f) * Columns), false);
                }
                else if (hasShield)
                {
                    int col = (i - CrestDrops) * 17 % (Columns + 1);
                    float age = elapsed - contactTimes[col];
                    float phase = Mathf.Repeat(age * 1.18f + Hash(i) * .68f, 1);
                    alpha = contacted[col] ? Smooth(.02f, .10f, age) * (1 - Smooth(.60f, 1.35f, age))
                        * Mathf.Sin(Mathf.PI * phase) * .82f : 0;
                    if ((i & 1) == 0)
                    {
                        p = Drain(col, phase, out n); stretch = 2.6f;
                        if (alpha > .02f) RunoffDrops++;
                    }
                    else
                    {
                        n = contactNormals[col]; float t = phase * .62f;
                        Vector3 lateral = Vector3.ProjectOnPlane(side * (col < Columns / 2 ? -1 : 1), n).normalized;
                        p = contacts[col] + n * (.05f + t * .18f) + lateral * t * (1.0f + Hash(i))
                            + Vector3.up * (t * (1.5f + Hash(i + 2)) - t * t * 4);
                        stretch = 1.1f + phase * 1.5f;
                    }
                }
                else if (HasActualContact && ImpactEnabled)
                {
                    float age = elapsed - firstContactAt, phase = Mathf.Clamp01((age - Hash(i) * .10f) / .65f);
                    float angle = i * 2.399963f;
                    p = ActualContact + side * Mathf.Cos(angle) * phase * .78f + forward * phase * .72f
                        + Vector3.up * (phase * (1.05f + Hash(i)) - phase * phase * 1.8f);
                    alpha = age >= 0 ? Mathf.Sin(Mathf.PI * phase) * .74f : 0;
                    stretch = 1.3f + phase * 1.2f;
                }
                if (!detached) alpha = 0;
                // CPU quads keep their complete footprint above the arena and outside Protect.
                float footprint = size * Mathf.Max(1, stretch);
                p.y = Mathf.Max(floorY + footprint, p.y);
                p = Outside(p, footprint * 1.45f + .012f);
                for (int corner = 0; corner < 4; corner++)
                {
                    float x = corner % 2 == 0 ? -1 : 1, y = corner < 2 ? -1 : 1;
                    Vector3 vertex = p + cameraRight * x * size + cameraUp * y * size * stretch;
                    vertex.y = Mathf.Max(floorY, vertex.y);
                    Put(surfaces[2], i * 4 + corner, vertex, -forward, new Color(.8f, 0, alpha, i * .71f), local, normalMatrix);
                }
                if (alpha > .02f) active++;
            }
            PeakDroplets = Mathf.Max(PeakDroplets, active);
        }
        void RenderWake(float grow, Matrix4x4 local, Matrix4x4 normalMatrix)
        {
            float fade = grow * (1 - Smooth(1.85f, Total, elapsed));
            float length = Mathf.Min(3.6f, Mathf.Max(.02f, travel + depth * .55f));
            float front = Mathf.Max(0, travel - depth * .35f);
            for (int row = 0; row <= WakeRows; row++) for (int col = 0; col <= Columns; col++)
            {
                float u = row / (float)WakeRows, lateral = col / (float)Columns * 2 - 1;
                float taper = Mathf.Sqrt(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)));
                Vector3 p = origin + forward * Mathf.Max(0, front - length * (1 - u))
                    + side * lateral * width * (.50f + .10f * Mathf.Sin(u * Mathf.PI)) * taper;
                p.y = floorY + .003f + .002f * Mathf.Sin(lateral * 24 + elapsed * 8);
                p = Arrest(p, col, false);
                float alpha = fade * Mathf.Sin(Mathf.PI * u) * (1 - Smooth(.6f, 1, Mathf.Abs(lateral))) * .68f;
                Put(surfaces[3], row * (Columns + 1) + col, p, Vector3.up,
                    new Color(.12f + .30f * Smooth(.65f, 1, Mathf.Abs(lateral)), .7f, alpha, u * 3), local, normalMatrix);
            }
        }

        Vector3 Outside(Vector3 p, float margin)
        {
            if (!hasShield) return p;
            Vector3 q = Divide(p - shield.center, shield.extents);
            float minimum = 1 + margin / Mathf.Min(shield.extents.x, Mathf.Min(shield.extents.y, shield.extents.z));
            if (q.sqrMagnitude < minimum * minimum)
            {
                if (q.sqrMagnitude < .0001f) q = -forward;
                q = q.normalized * minimum; p = shield.center + Vector3.Scale(q, shield.extents);
            }
            p.y = Mathf.Max(floorY, p.y);
            q = Divide(p - shield.center, shield.extents);
            if (q.sqrMagnitude < minimum * minimum)
            {
                // A radial projection below a ground-intersecting shell must not re-enter when clamped to the floor.
                float horizontal = Mathf.Sqrt(q.x * q.x + q.z * q.z);
                float required = Mathf.Sqrt(Mathf.Max(0, minimum * minimum - q.y * q.y));
                Vector3 flat = horizontal > .00001f ? new Vector3(q.x / horizontal, 0, q.z / horizontal) : -forward;
                q.x = flat.x * required; q.z = flat.z * required;
                p = shield.center + Vector3.Scale(q, shield.extents);
            }
            return p;
        }
        static bool RayEllipsoid(Vector3 origin, Vector3 direction, Bounds bounds, out float entry, out Vector3 hit, out Vector3 normal)
        {
            Vector3 r = bounds.extents, q = Divide(origin - bounds.center, r), d = Divide(direction, r);
            float a = Vector3.Dot(d, d), b = Vector3.Dot(q, d), c = Vector3.Dot(q, q) - 1;
            float disc = b * b - a * c;
            entry = 0; hit = origin; normal = -direction;
            if (a < .0000001f || disc < 0 || c <= 0) return false;
            entry = (-b - Mathf.Sqrt(disc)) / a;
            if (entry < 0) return false;
            hit = origin + direction * entry; normal = Divide(hit - bounds.center, Vector3.Scale(r, r)).normalized;
            return true;
        }
        void Put(Surface s, int v, Vector3 world, Vector3 normal, Color color, Matrix4x4 local, Matrix4x4 normalMatrix)
        {
            s.world[v] = world; s.vertices[v] = local.MultiplyPoint3x4(world);
            s.normals[v] = normalMatrix.MultiplyVector(normal).normalized; s.colors[v] = color;
        }
        void Upload(Surface s)
        {
            Bounds bounds = new Bounds(s.vertices[0], Vector3.zero);
            for (int v = 0; v < s.vertices.Length; v++) bounds.Encapsulate(s.vertices[v]);
            bounds.Expand(.08f);
            // Chords joining separate ellipsoid contact points can cut through the convex shell.
            // Reject the whole triangle using exact point-to-triangle distance in unit-sphere space.
            // Sampling only vertices or an average contact plane would leave through-shield wedges.
            for (int t = 0; t < s.indices.Length; t += 3)
            {
                int a = s.indices[t], b = s.indices[t + 1], c = s.indices[t + 2];
                bool reject = hasShield && TriangleEntersShield(s.world[a], s.world[b], s.world[c]);
                s.visibleIndices[t] = a; s.visibleIndices[t + 1] = reject ? a : b; s.visibleIndices[t + 2] = reject ? a : c;
                if (reject) RejectedShieldTriangles++;
            }
            s.mesh.vertices = s.vertices; s.mesh.normals = s.normals; s.mesh.colors = s.colors;
            s.mesh.triangles = s.visibleIndices; s.mesh.bounds = bounds;
        }
        bool TriangleEntersShield(Vector3 a, Vector3 b, Vector3 c)
        {
            a = Divide(a - shield.center, shield.extents); b = Divide(b - shield.center, shield.extents);
            c = Divide(c - shield.center, shield.extents);
            return ClosestTriangleToOrigin(a, b, c).sqrMagnitude < 1.0001f;
        }
        // Ericson's closest-point regions, including degenerate input triangles.
        static Vector3 ClosestTriangleToOrigin(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a;
            if (Vector3.Cross(ab, ac).sqrMagnitude < .0000000001f)
            {
                Vector3 p = ClosestSegment(a, b), q = ClosestSegment(b, c), r = ClosestSegment(c, a);
                return p.sqrMagnitude < q.sqrMagnitude ? p.sqrMagnitude < r.sqrMagnitude ? p : r : q.sqrMagnitude < r.sqrMagnitude ? q : r;
            }
            Vector3 ap = -a; float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return a;
            Vector3 bp = -b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = -c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / (d4 - d3 + d5 - d6));
            float inverse = 1 / (va + vb + vc); return a + ab * (vb * inverse) + ac * (vc * inverse);
        }
        static Vector3 ClosestSegment(Vector3 a, Vector3 b)
        { Vector3 d = b - a; return a + d * Mathf.Clamp01(-Vector3.Dot(a, d) / Mathf.Max(.000000001f, d.sqrMagnitude)); }
        public bool Validate(out string error)
        {
            error = null;
            if (!initialized) { error = "Surf is not initialized."; return false; }
            if (released) return Completed;
            if (TriangleCount >= 8000 || width > 4.8f || PeakHeight > 3.4f) { error = "Surf exceeded its geometry or arena scale budget."; return false; }
            foreach (Surface s in surfaces)
            {
                if (s == null || s.mesh == null) { error = "A Surf mesh is missing."; return false; }
                for (int v = 0; v < s.world.Length; v++)
                    if (!Finite(s.world[v]) || s.world[v].y < floorY - .003f)
                    { error = "Surf has an invalid or below-floor vertex."; return false; }
                if (hasShield) for (int t = 0; t < s.visibleIndices.Length; t += 3)
                {
                    int a = s.visibleIndices[t], b = s.visibleIndices[t + 1], c = s.visibleIndices[t + 2];
                    if (a != b && a != c && TriangleEntersShield(s.world[a], s.world[b], s.world[c]))
                    { error = "Surf has a triangle entering Protect."; return false; }
                }
            }
            return true;
        }
        void ReleaseResources()
        {
            if (released) return; released = true;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (renderers[i] != null) { renderers[i].enabled = false; renderers[i].sharedMaterial = null; }
                if (surfaces[i] != null && surfaces[i].mesh != null) Dispose(surfaces[i].mesh);
                if (materials[i] != null) Dispose(materials[i]);
            }
        }
        void OnDestroy() { ReleaseResources(); }
        static void Dispose(UnityEngine.Object obj) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        static bool ValidBounds(Bounds b) => Finite(b.center) && Finite(b.size) && b.size.x > .02f && b.size.y > .02f && b.size.z > .02f;
        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0, p.z);
        static bool Safe(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        static bool Finite(Vector3 p) => Safe(p.x) && Safe(p.y) && Safe(p.z);
        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + 4.14f) * 43758.54f, 1);
        static float Smooth(float a, float b, float t) { float u = Mathf.Clamp01((t - a) / Mathf.Max(.0001f, b - a)); return u * u * (3 - 2 * u); }
        sealed class Surface
        {
            public Mesh mesh;
            public readonly Vector3[] vertices, normals, world;
            public readonly Color[] colors;
            public readonly Vector2[] uv;
            public readonly int[] indices, visibleIndices;
            public int cursor;
            public Surface(string name, int count, int triangleIndices)
            {
                mesh = new Mesh { name = name }; mesh.MarkDynamic();
                vertices = new Vector3[count]; normals = new Vector3[count]; world = new Vector3[count];
                colors = new Color[count]; uv = new Vector2[count];
                indices = new int[triangleIndices]; visibleIndices = new int[triangleIndices];
            }
            public void Finish() { mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.uv = uv; mesh.triangles = indices; }
        }
    }
}
