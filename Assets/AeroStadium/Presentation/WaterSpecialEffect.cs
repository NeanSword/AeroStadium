using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Water Pulse: detached, resonating liquid globes. Scald: a short hot-water gush and steam.
    /// Emitted water owns a captured world trajectory; subsequent muzzle poses cannot steer it.
    /// The caller owns animation, sound, damage and the single victim reaction.
    /// </summary>
    public sealed class WaterSpecialEffect : MonoBehaviour
    {
        const int Capacity = 24, SphereLat = 6, SphereLon = 16, RingSegments = 30;
        const int RunoffLanes = 3, RunoffSegments = 8, DropsPerPacket = 8, SteamPerPacket = 3;
        const float SurfaceOffset = .018f;
        struct Packet
        {
            public Vector3 origin, position, previous, velocity, acceleration, axis, side, up;
            public Vector3 contact, normal, shellRadial;
            public float born, radius, phase, flight, hitAt;
            public bool emitted, hit, actualContact, shield, secondary;
        }
        readonly Packet[] packets = new Packet[Capacity];
        readonly Mesh[] meshes = new Mesh[4];
        readonly Material[] materials = new Material[4];
        readonly MeshRenderer[] renderers = new MeshRenderer[4];
        Vector3[] liquidVertices, liquidNormals, surfaceVertices, surfaceNormals, dropVertices, steamVertices;
        Color[] liquidColors, surfaceColors, dropColors, steamColors;
        Vector3 source, secondarySource, target, shieldPoint, shieldNormal, secondaryPoint, secondaryNormal;
        Bounds shieldBounds, victimBounds;
        Matrix4x4 toLocal, normalToLocal;
        float elapsed, width, floorY;
        int moveId, count, flightRings, contactRings, surfaceRingVertices;
        bool initialized, secondary, shield, secondaryShield, hasSurface, hasVictim, released;

        public bool ImpactEnabled { get; set; } = true;
        public float Duration => moveId == 352 ? 2.30f : 2.45f;
        public float EffectProgress => Mathf.Clamp01(elapsed / Duration);
        public bool ImpactReached { get; private set; }
        public bool Completed => initialized && elapsed >= Duration;
        public bool IsPlaying => initialized && !released && !Completed;
        public bool HasLaunched => EmissionCount > 0;
        public float ImpactOpacity { get; private set; }
        public float ContactOpacity { get; private set; }
        public int EmissionCount { get; private set; }
        public int ActualContacts { get; private set; }
        public int TotalContacts => ActualContacts;
        public int PrimaryEmitterContacts { get; private set; }
        public int SecondaryEmitterContacts { get; private set; }
        public int EmitterContacts => PrimaryEmitterContacts + SecondaryEmitterContacts;
        public int PrimaryShieldContacts { get; private set; }
        public int SecondaryShieldContacts { get; private set; }
        public int ShieldContacts => PrimaryShieldContacts + SecondaryShieldContacts;
        public int RunoffDrops { get; private set; }
        public int PeakParticles { get; private set; }
        public int PeakSteamPuffs { get; private set; }
        public bool DetachedBeforeImpact { get; private set; }
        public bool ProjectileDetached => DetachedBeforeImpact;
        public bool SourceConnectionCleared => DetachedBeforeImpact;
        public Vector3 ContactPoint { get; private set; }
        public Vector3 ContactNormal { get; private set; }
        public Vector3 ShieldContactPoint => ContactPoint;
        public Vector3 ShieldContactNormal => ContactNormal;
        public Vector3 PrimaryTrajectoryOrigin { get; private set; }
        public Vector3 SecondaryTrajectoryOrigin { get; private set; }
        public int RendererCount => moveId == 503 ? 4 : 3;
        public int TriangleCount => count * (SphereLat * SphereLon * 2 +
            (flightRings + contactRings) * RingSegments * 2 + RunoffLanes * RunoffSegments * 2 +
            DropsPerPacket * 2 + (moveId == 503 ? SteamPerPacket * 2 : 0));

        public static WaterSpecialEffect Create(Transform parent, Vector3 source, Vector3 target, float width, int moveId)
        {
            if (moveId != 352 && moveId != 503) throw new ArgumentOutOfRangeException(nameof(moveId));
            if (!Finite(source) || !Finite(target) || (source - target).sqrMagnitude < .0001f)
                throw new ArgumentException("Special water effects need finite distinct world endpoints.");
            var go = new GameObject(moveId == 352 ? "Vibraqua · resonant liquid globes" : "Ébullition · hot-water gush and steam");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<WaterSpecialEffect>();
            effect.source = source; effect.target = target; effect.moveId = moveId;
            effect.width = Finite(width) ? Mathf.Clamp(width, .06f, .55f) : .18f;
            effect.floorY = Mathf.Min(source.y, target.y) - .6f;
            try { effect.Initialize(); }
            catch { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); throw; }
            return effect;
        }
        public void SetEndpoints(Vector3 source, Vector3 target)
        {
            if (!Finite(source) || !Finite(target) || (source - target).sqrMagnitude < .0001f) return;
            this.source = source; this.target = target;
        }
        public void SetSecondaryEmitter(Vector3 origin)
        { if (Finite(origin)) { secondarySource = origin; secondary = true; } }
        public void SetVictimBounds(Bounds bounds)
        {
            if (!ValidBounds(bounds)) return;
            victimBounds = bounds; hasVictim = true;
        }
        public void SetShieldContact(Vector3 point, Vector3 outwardNormal)
        {
            if (!Finite(point) || !Finite(outwardNormal) || outwardNormal.sqrMagnitude < .0001f) return;
            shieldPoint = point; shieldNormal = outwardNormal.normalized; shield = true; ImpactEnabled = false;
        }
        public void SetSecondaryShieldContact(Vector3 point, Vector3 outwardNormal)
        {
            if (!Finite(point) || !Finite(outwardNormal) || outwardNormal.sqrMagnitude < .0001f) return;
            secondaryPoint = point; secondaryNormal = outwardNormal.normalized; secondaryShield = true;
        }
        public void SetShieldSurface(Bounds bounds, float floor)
        {
            if (!ValidBounds(bounds) || !Finite(floor)) return;
            shieldBounds = bounds; floorY = floor + .012f; hasSurface = true;
        }
        public void SetFloor(float floor)
        { if (Finite(floor)) floorY = floor + .012f; }
        public void SetShieldContact(Vector3 point, Vector3 normal, Bounds bounds, float floor)
        { SetShieldContact(point, normal); SetShieldSurface(bounds, floor); }
        public void SetSecondaryShieldContact(Vector3 point, Vector3 normal, Bounds bounds, float floor)
        { SetSecondaryShieldContact(point, normal); SetShieldSurface(bounds, floor); }

        // Exposed for diagnostics without exposing or allocating the packet pool.
        public Vector3 GetEmissionOrigin(int index) => index >= 0 && index < count && packets[index].emitted ? packets[index].origin : Vector3.zero;
        public bool IsSecondaryEmission(int index) => index >= 0 && index < count && packets[index].emitted && packets[index].secondary;

        void Initialize()
        {
            count = moveId == 352 ? 6 : 24;
            flightRings = moveId == 352 ? 2 : 0;
            contactRings = moveId == 352 ? 3 : 1;
            Material reference = Resources.Load<Material>("Materials/WaterSpecialReference");
            Shader shader = reference != null ? reference.shader : Shader.Find("AeroStadium/Original Resonant and Hot Water");
            if (shader == null) throw new InvalidOperationException("Original Resonant and Hot Water shader missing.");
            meshes[0] = CreateLiquidMesh(); meshes[1] = CreateSurfaceMesh();
            meshes[2] = CreateSpriteMesh(false); meshes[3] = CreateSpriteMesh(true);
            for (int i = 0; i < 4; i++)
            {
                materials[i] = reference != null ? new Material(reference) : new Material(shader);
                materials[i].name = (moveId == 352 ? "Vibraqua · " : "Ébullition · ") + i;
                materials[i].SetFloat("_Mode", i); materials[i].SetFloat("_Heat", moveId == 503 ? 1f : 0f);
                var go = new GameObject(i == 0 ? "Captured liquid volumes" : i == 1 ? "Pressure rings and shell rivulets" : i == 2 ? "Finite liquid spray" : "Rising contact steam");
                go.transform.SetParent(transform, false); go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                renderers[i] = go.AddComponent<MeshRenderer>(); renderers[i].sharedMaterial = materials[i];
                renderers[i].shadowCastingMode = ShadowCastingMode.Off; renderers[i].receiveShadows = false;
                renderers[i].enabled = i != 3 || moveId == 503;
            }
            initialized = true; RenderEffect();
        }
        Mesh CreateLiquidMesh()
        {
            int stride = (SphereLat + 1) * (SphereLon + 1), ti = 0;
            liquidVertices = new Vector3[count * stride]; liquidNormals = new Vector3[liquidVertices.Length];
            liquidColors = new Color[liquidVertices.Length]; var uv = new Vector2[liquidVertices.Length];
            var triangles = new int[count * SphereLat * SphereLon * 6];
            for (int p = 0; p < count; p++) for (int lat = 0; lat <= SphereLat; lat++) for (int lon = 0; lon <= SphereLon; lon++)
            {
                int v = p * stride + lat * (SphereLon + 1) + lon;
                uv[v] = new Vector2(lon / (float)SphereLon, lat / (float)SphereLat);
                if (lat < SphereLat && lon < SphereLon) AddQuad(triangles, ref ti, v, v + SphereLon + 1, v + SphereLon + 2, v + 1);
            }
            return NewMesh("Special water · independent liquid volumes", liquidVertices, liquidNormals, liquidColors, uv, triangles);
        }
        Mesh CreateSurfaceMesh()
        {
            int rings = count * (flightRings + contactRings);
            surfaceRingVertices = rings * (RingSegments + 1) * 2;
            int vertices = surfaceRingVertices + count * RunoffLanes * (RunoffSegments + 1) * 2;
            surfaceVertices = new Vector3[vertices]; surfaceNormals = new Vector3[vertices]; surfaceColors = new Color[vertices];
            var uv = new Vector2[vertices]; var triangles = new int[(rings * RingSegments + count * RunoffLanes * RunoffSegments) * 6];
            int start = 0, ti = 0;
            for (int strip = 0; strip < rings + count * RunoffLanes; strip++)
            {
                int segments = strip < rings ? RingSegments : RunoffSegments;
                for (int j = 0; j <= segments; j++)
                {
                    uv[start + j * 2] = new Vector2(0, j / (float)segments);
                    uv[start + j * 2 + 1] = new Vector2(1, j / (float)segments);
                    if (j < segments) AddQuad(triangles, ref ti, start + j * 2, start + j * 2 + 1, start + j * 2 + 3, start + j * 2 + 2);
                }
                start += (segments + 1) * 2;
            }
            return NewMesh("Special water · oscillating waves and liquid runoff", surfaceVertices, surfaceNormals, surfaceColors, uv, triangles);
        }
        Mesh CreateSpriteMesh(bool steam)
        {
            int sprites = steam ? moveId == 503 ? count * SteamPerPacket : 0 : count * DropsPerPacket;
            var vertices = new Vector3[sprites * 4]; var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length]; var triangles = new int[sprites * 6]; int ti = 0;
            for (int i = 0; i < sprites; i++)
            {
                int v = i * 4; uv[v] = Vector2.zero; uv[v + 1] = Vector2.right;
                uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
                AddQuad(triangles, ref ti, v, v + 1, v + 2, v + 3);
            }
            if (steam) { steamVertices = vertices; steamColors = colors; }
            else { dropVertices = vertices; dropColors = colors; }
            return NewMesh(steam ? "Scald · finite rising steam" : "Special water · scattered liquid", vertices, null, colors, uv, triangles);
        }
        static Mesh NewMesh(string name, Vector3[] vertices, Vector3[] normals, Color[] colors, Vector2[] uv, int[] triangles)
        {
            var mesh = new Mesh { name = name }; mesh.MarkDynamic();
            mesh.vertices = vertices; if (normals != null) mesh.normals = normals;
            mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles; return mesh;
        }
        static void AddQuad(int[] t, ref int ti, int a, int b, int c, int d)
        { t[ti++] = a; t[ti++] = b; t[ti++] = c; t[ti++] = a; t[ti++] = c; t[ti++] = d; }

        void Update()
        {
            if (!IsPlaying) return;
            elapsed = Mathf.Min(Duration, elapsed + Time.deltaTime); RenderEffect();
            if (Completed) for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = false;
        }
        float BirthTime(int index) => moveId == 352 ? .16f + (index / 2) * .16f : .14f + (index / 2) * .028f;
        void Emit(int i, ref Packet p)
        {
            p.emitted = true; p.born = BirthTime(i); p.phase = i * 2.399963f;
            p.secondary = secondary && (i & 1) != 0; p.origin = p.secondary ? secondarySource : source;
            if (p.secondary && SecondaryTrajectoryOrigin == Vector3.zero) SecondaryTrajectoryOrigin = p.origin;
            if (!p.secondary && EmissionCount == 0) PrimaryTrajectoryOrigin = p.origin;
            EmissionCount++;
            Vector3 aimAxis = (target - p.origin).normalized; Basis(aimAxis, out p.side, out p.up);
            float spread = width * (moveId == 352 ? .34f : .62f);
            Vector3 aim = target + (p.side * Mathf.Cos(p.phase) + p.up * Mathf.Sin(p.phase)) * spread;
            p.flight = (moveId == 352 ? .62f : .60f) + Hash(i + 3) * .035f;
            p.acceleration = moveId == 503 ? Vector3.down * .80f : Vector3.zero;
            p.velocity = (aim - p.origin - p.acceleration * (.5f * p.flight * p.flight)) / p.flight;
            p.axis = p.velocity.normalized; Basis(p.axis, out p.side, out p.up);
            p.radius = width * (moveId == 352 ? 1.06f + Hash(i + 11) * .17f : .48f + Hash(i + 11) * .17f);
            p.position = p.previous = p.origin;
        }
        void RenderEffect()
        {
            toLocal = transform.worldToLocalMatrix; normalToLocal = transform.localToWorldMatrix.transpose;
            ImpactOpacity = ContactOpacity = 0; RunoffDrops = 0; int active = 0, steam = 0;
            for (int i = 0; i < count; i++)
            {
                Packet p = packets[i];
                if (!p.emitted && elapsed >= BirthTime(i)) Emit(i, ref p);
                if (p.emitted && !p.hit)
                {
                    float age = Mathf.Max(0, elapsed - p.born); p.previous = p.position;
                    p.position = p.origin + p.velocity * age + p.acceleration * (.5f * age * age);
                    float travelled = Vector3.Distance(p.origin, p.position);
                    if (!ImpactReached && travelled > p.radius * (moveId == 352 ? 3.6f : 6.5f)) DetachedBeforeImpact = true;
                    if (TryContact(p, out Vector3 contact, out Vector3 normal)) Hit(ref p, contact, normal);
                    else if (age > p.flight + .15f) { p.hit = true; p.hitAt = elapsed; p.contact = p.position; p.normal = -p.axis; }
                }
                float liquidAlpha = p.emitted && !p.hit && !Completed ? Mathf.Clamp01((elapsed - p.born) / .028f) : 0;
                if (liquidAlpha > .01f) active++;
                RenderLiquid(i, p, liquidAlpha); RenderWaves(i, p, liquidAlpha);
                RenderRunoff(i, p); active += RenderDrops(i, p, liquidAlpha);
                if (moveId == 503) steam += RenderSteam(i, p, liquidAlpha);
                packets[i] = p;
            }
            PeakParticles = Mathf.Max(PeakParticles, active + steam); PeakSteamPuffs = Mathf.Max(PeakSteamPuffs, steam);
            Vector3 clipCenter = hasSurface ? shieldBounds.center : Vector3.zero;
            Vector3 clipRadii = hasSurface ? SafeRadii(shieldBounds.extents) : Vector3.one;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetFloat("_Clock", elapsed); materials[i].SetFloat("_ClipShield", shield && hasSurface ? 1f : 0f);
                materials[i].SetVector("_ShieldCenter", new Vector4(clipCenter.x, clipCenter.y, clipCenter.z, 0));
                materials[i].SetVector("_ShieldRadii", new Vector4(clipRadii.x, clipRadii.y, clipRadii.z, 0));
            }
            Upload(meshes[0], liquidVertices, liquidNormals, liquidColors, width * 1.5f);
            Upload(meshes[1], surfaceVertices, surfaceNormals, surfaceColors, width);
            Upload(meshes[2], dropVertices, null, dropColors, width * 2f);
            Upload(meshes[3], steamVertices, null, steamColors, width * 6f);
        }
        bool TryContact(Packet p, out Vector3 contact, out Vector3 normal)
        {
            contact = p.position; normal = -p.axis;
            if (!shield && !ImpactEnabled) return false;
            if ((shield && hasSurface) || (!shield && hasVictim))
            {
                Bounds bounds = shield ? shieldBounds : victimBounds;
                // Conservative swept volume: the leading liquid surface stops before its centre crosses a shell.
                float support = p.radius * (moveId == 352 ? 1.08f : 2.5f);
                Vector3 r = SafeRadii(bounds.extents), expanded = r + Vector3.one * support;
                Vector3 o = p.previous - bounds.center, segment = p.position - p.previous;
                Vector3 q = Divide(o, expanded), d = Divide(segment, expanded);
                float a = Vector3.Dot(d, d), b = Vector3.Dot(q, d), c = Vector3.Dot(q, q) - 1f;
                float disc = b * b - a * c;
                if (c < 0)
                {
                    // A moving shell may catch an already emitted lobe between frames.
                    if (Vector3.Dot(p.position - bounds.center, p.velocity) >= 0) return false;
                    contact = ProjectSurface(p.previous, bounds, 0, out normal); return true;
                }
                if (a < .0000001f || disc < 0) return false;
                float t = (-b - Mathf.Sqrt(disc)) / a; if (t < 0 || t > 1) return false;
                contact = ProjectSurface(p.previous + segment * t, bounds, 0, out normal); return true;
            }
            Vector3 point = shield ? p.secondary && secondaryShield ? secondaryPoint : shieldPoint : target;
            normal = shield ? p.secondary && secondaryShield ? secondaryNormal : shieldNormal : -p.axis;
            float supportPlane = p.radius * (moveId == 352 ? 1.08f : 2.5f);
            float before = Vector3.Dot(p.previous - point, normal) - supportPlane;
            float after = Vector3.Dot(p.position - point, normal) - supportPlane;
            if (before < 0 || after > 0) return false;
            contact = Vector3.Lerp(p.previous, p.position, before / Mathf.Max(.00001f, before - after)) - normal * supportPlane;
            return true;
        }
        void Hit(ref Packet p, Vector3 contact, Vector3 normal)
        {
            p.hit = p.actualContact = true; p.hitAt = elapsed; p.contact = contact; p.normal = normal;
            p.shield = shield; p.position = contact + normal * p.radius;
            if (p.shield && hasSurface) p.shellRadial = Divide(contact - shieldBounds.center, SafeRadii(shieldBounds.extents)).normalized;
            ActualContacts++; if (p.secondary) SecondaryEmitterContacts++; else PrimaryEmitterContacts++;
            if (p.shield) { if (p.secondary) SecondaryShieldContacts++; else PrimaryShieldContacts++; }
            ImpactReached = true; ContactPoint = contact; ContactNormal = normal;
        }
        void RenderLiquid(int index, Packet p, float alpha)
        {
            int stride = (SphereLat + 1) * (SphereLon + 1), start = index * stride;
            Vector3 center = p.emitted ? p.position : source;
            float age = p.emitted ? Mathf.Max(0, elapsed - p.born) : 0;
            float stretch = moveId == 352 ? 1f + .045f * Mathf.Sin(age * 20 + p.phase) : 2.2f;
            for (int lat = 0; lat <= SphereLat; lat++) for (int lon = 0; lon <= SphereLon; lon++)
            {
                float theta = lat / (float)SphereLat * Mathf.PI, phi = lon / (float)SphereLon * Mathf.PI * 2;
                float sn = Mathf.Sin(theta), cs = Mathf.Cos(theta);
                Vector3 radial = p.side * Mathf.Cos(phi) + p.up * Mathf.Sin(phi);
                float turbulence = 1f + (moveId == 352 ? .035f : .17f) * Mathf.Sin(phi * 3 + theta * 5 - age * 24 + p.phase) * sn;
                Vector3 n = (radial * sn + p.axis * (cs / stretch)).normalized;
                Vector3 point = center + (radial * sn * turbulence + p.axis * cs * stretch) * p.radius;
                PutSurface(liquidVertices, liquidNormals, liquidColors, start + lat * (SphereLon + 1) + lon,
                    point, n, new Color(p.phase, age, alpha, 1));
            }
        }
        Vector3 CurrentContact(Packet p, out Vector3 normal)
        {
            if (p.shield && hasSurface)
            {
                Vector3 r = SafeRadii(shieldBounds.extents);
                Vector3 point = shieldBounds.center + Vector3.Scale(p.shellRadial, r);
                normal = Divide(p.shellRadial, r).normalized; return point;
            }
            normal = p.normal.sqrMagnitude > .0001f ? p.normal : Vector3.up; return p.contact;
        }
        void RenderWaves(int index, Packet p, float liquidAlpha)
        {
            int ringsPerPacket = flightRings + contactRings, stride = (RingSegments + 1) * 2;
            float age = p.emitted ? elapsed - p.born : 0;
            Vector3 contact = CurrentContact(p, out Vector3 contactNormal);
            Basis(contactNormal, out Vector3 contactSide, out Vector3 contactUp);
            for (int ring = 0; ring < ringsPerPacket; ring++)
            {
                bool flying = ring < flightRings; float waveAge = p.actualContact ? elapsed - p.hitAt - (ring - flightRings) * .075f : -1;
                float life = moveId == 352 ? .51f : .25f;
                float alpha = flying ? liquidAlpha * .78f : waveAge >= 0 && !Completed ? Mathf.Clamp01(1 - waveAge / life) : 0;
                if (!flying && p.actualContact)
                {
                    if (p.shield) ContactOpacity = Mathf.Max(ContactOpacity, alpha);
                    else if (ImpactEnabled) ImpactOpacity = Mathf.Max(ImpactOpacity, alpha);
                }
                Vector3 center = flying ? p.position : contact + contactNormal * SurfaceOffset;
                Vector3 normal = flying ? (p.axis + p.side * Mathf.Sin(age * 8 + ring * 2.4f) * .38f + p.up * Mathf.Cos(age * 7 + ring) * .30f).normalized : contactNormal;
                Basis(normal.sqrMagnitude > .0001f ? normal : Vector3.forward, out Vector3 side, out Vector3 up);
                float radius = flying ? p.radius * (1.15f + .13f * Mathf.Sin(age * 24 + p.phase + ring * Mathf.PI)) :
                    p.radius * (moveId == 352 ? .8f : .55f) + Mathf.Max(0, waveAge) * width * (moveId == 352 ? 7.5f : 5.8f);
                float band = flying ? p.radius * .065f : width * (moveId == 352 ? .10f : .22f) * Mathf.Max(.2f, alpha);
                int start = (index * ringsPerPacket + ring) * stride;
                for (int j = 0; j <= RingSegments; j++)
                {
                    float a = j / (float)RingSegments * Mathf.PI * 2;
                    Vector3 dir = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    float wobble = 1f + .035f * Mathf.Sin(a * 5 + age * 17 + p.phase);
                    for (int edge = 0; edge < 2; edge++)
                    {
                        Vector3 point = center + dir * (radius * wobble + (edge == 0 ? -band : band));
                        Vector3 vertexNormal = normal;
                        if (!flying && p.shield && hasSurface) point = ProjectSurface(point, shieldBounds, SurfaceOffset + .004f * (ring - flightRings), out vertexNormal);
                        PutSurface(surfaceVertices, surfaceNormals, surfaceColors, start + j * 2 + edge,
                            point, vertexNormal, new Color(p.phase + ring, Mathf.Max(0, waveAge), alpha, 1));
                    }
                }
            }
        }
        void RenderRunoff(int index, Packet p)
        {
            float age = p.actualContact ? elapsed - p.hitAt : -1;
            // Scald merges neighbouring lobe contacts into a few coherent wet rivulets.
            bool visible = p.actualContact && age > .025f && age < 1.08f && !Completed && (moveId == 352 || index % 3 == 0);
            Vector3 contact = CurrentContact(p, out Vector3 n); Basis(n, out Vector3 side, out Vector3 up);
            float opacity = visible ? Mathf.Clamp01(age / .07f) * Mathf.Clamp01((1.08f - age) / .22f) * .66f : 0;
            if (p.shield) ContactOpacity = Mathf.Max(ContactOpacity, opacity * .55f);
            for (int lane = 0; lane < RunoffLanes; lane++)
            {
                int start = surfaceRingVertices + (index * RunoffLanes + lane) * (RunoffSegments + 1) * 2;
                for (int row = 0; row <= RunoffSegments; row++)
                {
                    float u = row / (float)RunoffSegments;
                    float flowAge = Mathf.Max(0, age - (1 - u) * .23f - lane * .021f);
                    Vector3 normal = n;
                    Vector3 point = p.shield && hasSurface ? RunoffPoint(p, flowAge, lane - 1, out normal) :
                        contact + side * (lane - 1) * width * .24f + n * .03f + Vector3.down * (flowAge * flowAge * 3.1f);
                    point.y = Mathf.Max(floorY, point.y);
                    Vector3 tangent = Vector3.Cross(normal, Vector3.down);
                    if (tangent.sqrMagnitude < .001f) tangent = side; else tangent.Normalize();
                    float thickness = width * (.055f + .025f * Mathf.Sin(u * Mathf.PI)) * (1 - .45f * u);
                    float alpha = opacity * Mathf.Sin(u * Mathf.PI) * (flowAge > .001f ? 1 : 0);
                    for (int edge = 0; edge < 2; edge++) PutSurface(surfaceVertices, surfaceNormals, surfaceColors,
                        start + row * 2 + edge, point + tangent * (edge == 0 ? -thickness : thickness), normal,
                        new Color(p.phase + lane, flowAge, alpha, 1));
                }
            }
        }
        int RenderDrops(int index, Packet p, float liquidAlpha)
        {
            int active = 0; float hitAge = p.actualContact ? elapsed - p.hitAt : -1;
            Vector3 contact = CurrentContact(p, out Vector3 normal); Basis(normal, out Vector3 side, out Vector3 up);
            for (int j = 0; j < DropsPerPacket; j++)
            {
                float angle = p.phase + j * 2.399963f, size = p.radius * (.065f + Hash(index * 13 + j) * .05f);
                Vector3 point = p.emitted ? p.position : source; float alpha = 0;
                if (!p.hit && p.emitted)
                {
                    float lag = .35f + j * .34f;
                    point -= p.axis * (p.radius * lag);
                    point += (p.side * Mathf.Cos(angle) + p.up * Mathf.Sin(angle)) * p.radius * (.75f + j * .07f);
                    alpha = liquidAlpha * (moveId == 352 ? .55f : .80f);
                }
                else if (p.actualContact)
                {
                    float age = hitAge - j * .012f;
                    alpha = age >= 0 && !Completed ? Mathf.Clamp01(1 - age / .93f) * .86f : 0;
                    if (p.shield && hasSurface && j >= 4)
                    {
                        point = RunoffPoint(p, Mathf.Max(0, age), (j - 5.5f) * .55f, out normal);
                        if (alpha > .01f) RunoffDrops++;
                    }
                    else
                    {
                        Vector3 tangent = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                        Vector3 velocity = tangent * (.55f + Hash(index * 11 + j) * .8f) + normal * .32f + Vector3.up * .45f;
                        point = contact + velocity * Mathf.Max(0, age) + Vector3.down * (2.7f * Mathf.Max(0, age) * Mathf.Max(0, age));
                    }
                    point.y = Mathf.Max(floorY + size * 1.6f, point.y);
                }
                if (alpha > .01f) active++;
                SetSprite(dropVertices, dropColors, (index * DropsPerPacket + j) * 4, point, size, alpha, p.phase + j, 1.6f);
            }
            return active;
        }
        int RenderSteam(int index, Packet p, float liquidAlpha)
        {
            int active = 0; Vector3 contact = CurrentContact(p, out Vector3 normal); Basis(normal, out Vector3 side, out Vector3 up);
            for (int j = 0; j < SteamPerPacket; j++)
            {
                Vector3 point = p.emitted ? p.position : source; float alpha = 0, size = p.radius;
                float phase = p.phase + j * 1.7f;
                if (p.actualContact)
                {
                    float age = elapsed - p.hitAt - j * .11f, life = .96f;
                    if (age >= 0 && age < life && !Completed)
                    {
                        float t = age / life; size = p.radius * (1.1f + t * 3.2f);
                        point = contact + normal * (p.radius * .4f + t * .12f) + Vector3.up * (t * .92f + t * t * .28f);
                        point += side * (Mathf.Sin(phase + t * 2.5f) * t * .24f);
                        alpha = Mathf.Sin(t * Mathf.PI) * .28f;
                    }
                }
                else if (p.emitted && !p.hit && index % 4 == 0 && j == 0)
                {
                    float age = elapsed - p.born;
                    point = p.position - p.axis * p.radius * 1.8f + Vector3.up * (age * .12f);
                    size = p.radius * 1.5f; alpha = liquidAlpha * .14f;
                }
                if (alpha > .01f) active++;
                SetSprite(steamVertices, steamColors, (index * SteamPerPacket + j) * 4, point, size, alpha, phase, 1.22f);
            }
            return active;
        }
        Vector3 RunoffPoint(Packet p, float age, float lane, out Vector3 normal)
        {
            Vector3 r = SafeRadii(shieldBounds.extents), radial = p.shellRadial;
            float yaw = Mathf.Atan2(radial.z, radial.x), elevation = Mathf.Asin(Mathf.Clamp(radial.y, -1, 1));
            float bottom = Mathf.Asin(Mathf.Clamp((floorY - shieldBounds.center.y) / r.y, -1, 1));
            bottom = Mathf.Min(elevation, Mathf.Max(-Mathf.PI * .5f + .018f, bottom));
            float flow = Mathf.Clamp01(age / .64f); float down = flow * flow * (3 - 2 * flow);
            elevation = Mathf.Lerp(elevation, bottom, down);
            yaw += lane * .08f * Mathf.Sin(flow * Mathf.PI) + Mathf.Sin(p.phase + flow * 6) * .013f * flow;
            radial = new Vector3(Mathf.Cos(elevation) * Mathf.Cos(yaw), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(yaw));
            normal = Divide(radial, r).normalized;
            Vector3 point = shieldBounds.center + Vector3.Scale(radial, r) + normal * SurfaceOffset;
            if (age > .64f) point.y = Mathf.Max(floorY, point.y - (age - .64f) * (age - .64f) * 5f);
            return OutsideShield(point, SurfaceOffset, out normal);
        }
        void PutSurface(Vector3[] vertices, Vector3[] normals, Color[] colors, int v, Vector3 point, Vector3 normal, Color color)
        {
            if (shield && hasSurface && color.b > .001f)
            {
                Vector3 projectedNormal; point = OutsideShield(point, SurfaceOffset, out projectedNormal);
                if (EllipsoidLevel(point) < 1.025f) normal = projectedNormal;
            }
            vertices[v] = toLocal.MultiplyPoint3x4(point);
            normals[v] = normalToLocal.MultiplyVector(normal).normalized; colors[v] = color;
        }
        void SetSprite(Vector3[] vertices, Color[] colors, int start, Vector3 point, float radius, float alpha, float phase, float stretch)
        {
            if (shield && hasSurface && alpha > .001f) point = OutsideShield(point, radius * Mathf.Sqrt(1 + stretch * stretch) + SurfaceOffset, out Vector3 ignored);
            Vector3 local = toLocal.MultiplyPoint3x4(point); Color color = new Color(radius, phase, alpha, stretch);
            for (int j = 0; j < 4; j++) { vertices[start + j] = local; colors[start + j] = color; }
        }
        float EllipsoidLevel(Vector3 point) => Divide(point - shieldBounds.center, SafeRadii(shieldBounds.extents)).magnitude;
        Vector3 OutsideShield(Vector3 point, float clearance, out Vector3 normal)
        {
            Vector3 surface = ProjectSurface(point, shieldBounds, 0, out normal);
            float distance = Vector3.Dot(point - surface, normal);
            return distance < clearance ? surface + normal * clearance : point;
        }
        static Vector3 ProjectSurface(Vector3 point, Bounds bounds, float offset, out Vector3 normal)
        {
            Vector3 r = SafeRadii(bounds.extents), local = point - bounds.center;
            Vector3 q = Divide(local, r);
            if (q.sqrMagnitude < .000001f) q = Vector3.up; else q.Normalize();
            normal = Divide(q, r).normalized;
            return bounds.center + Vector3.Scale(q, r) + normal * offset;
        }
        void Upload(Mesh mesh, Vector3[] vertices, Vector3[] normals, Color[] colors, float padding)
        {
            mesh.vertices = vertices; if (normals != null) mesh.normals = normals; mesh.colors = colors;
            Bounds bounds = new Bounds(toLocal.MultiplyPoint3x4(source), Vector3.one * .01f);
            for (int i = 0; i < vertices.Length; i++) bounds.Encapsulate(vertices[i]);
            // Billboard sizes are world units, so a scaled effect parent needs corresponding local padding.
            Vector3 scale = toLocal.MultiplyVector(Vector3.one);
            float localPadding = padding * Mathf.Max(1f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
            bounds.Expand(localPadding * 2); mesh.bounds = bounds;
        }
        static void Basis(Vector3 normal, out Vector3 side, out Vector3 up)
        {
            side = Vector3.Cross(Mathf.Abs(normal.y) > .93f ? Vector3.forward : Vector3.up, normal).normalized;
            up = Vector3.Cross(normal, side).normalized;
        }
        static Vector3 SafeRadii(Vector3 r) => new Vector3(Mathf.Max(.08f, r.x), Mathf.Max(.08f, r.y), Mathf.Max(.08f, r.z));
        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + 4.14f) * 43758.54f, 1f);
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool ValidBounds(Bounds b) => Finite(b.center) && Finite(b.size) && b.size.x > .001f && b.size.y > .001f && b.size.z > .001f;
        public bool Validate(out string error)
        {
            error = null;
            if (!initialized || released) { error = "Special water effect is not initialized."; return false; }
            if (TriangleCount >= 8000) { error = "Special water triangle budget exceeded."; return false; }
            for (int i = 0; i < 4; i++) if (meshes[i] == null || materials[i] == null) { error = "Special water resource missing."; return false; }
            if (!FiniteVertices(liquidVertices) || !FiniteVertices(surfaceVertices) || !FiniteVertices(dropVertices) || !FiniteVertices(steamVertices))
            { error = "Nonfinite special water geometry."; return false; }
            for (int i = 0; i < count; i++) if (packets[i].emitted && (!Finite(packets[i].origin) || !Finite(packets[i].position)))
            { error = "Nonfinite captured water trajectory."; return false; }
            if (ActualContacts > EmissionCount || ShieldContacts > ActualContacts || EmitterContacts != ActualContacts)
            { error = "Special water contact accounting mismatch."; return false; }
            return true;
        }
        static bool FiniteVertices(Vector3[] vertices)
        { for (int i = 0; i < vertices.Length; i++) if (!Finite(vertices[i])) return false; return true; }
        void OnDestroy()
        {
            if (released) return; released = true;
            for (int i = 0; i < 4; i++)
            {
                if (meshes[i] != null) { if (Application.isPlaying) Destroy(meshes[i]); else DestroyImmediate(meshes[i]); }
                if (materials[i] != null) { if (Application.isPlaying) Destroy(materials[i]); else DestroyImmediate(materials[i]); }
            }
        }
    }
}
