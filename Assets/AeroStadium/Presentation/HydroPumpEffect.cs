using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Original pressure columns, traveling collars, shield bowls and body-conforming surface flow.</summary>
    public sealed class HydroPumpEffect : MonoBehaviour
    {
        const int Emitters = 2, Rows = 36, Sides = 12, Collars = 5, RingSides = 24, BowlRows = 6;
        const int StreamDrops = 64, ImpactDrops = 80;
        const float Total = 2.2f, Hit = .65f, Launch = .20f, Release = 1.68f;
        readonly MeshData[] data = new MeshData[3];
        readonly MeshRenderer[] renderers = new MeshRenderer[3];
        readonly Material[] materials = new Material[3];
        Vector3 source, secondary, target, contact, secondaryContact, shieldNormal, secondaryNormal, victimCenter;
        float radius, elapsed, victimRadius, victimHeight, floorY;
        bool initialized, released, hasSecondary, blocked, hasSecondaryContact, hasVictimBounds;
        Shader shader; Bounds shieldSurface, victimSurface; bool hasShieldSurface, hasFloor;
        public int RunoffDrops {get;private set;}
        public bool BodySurfaceFlow { get; private set; }
        public int BodyRunoffDrops { get; private set; }
        public Vector3 BodyContactPoint { get; private set; }

        public bool ImpactEnabled { get; set; } = true;
        public float Duration => Total;
        public float EffectProgress => Mathf.Clamp01(elapsed / Total);
        public bool IsPlaying => initialized && !released && elapsed < Total;
        public bool Completed => initialized && elapsed >= Total;
        public bool ImpactReached => initialized && elapsed >= Hit;
        public float ImpactOpacity { get; private set; }
        public float ContactOpacity { get; private set; }
        public int PeakDroplets { get; private set; }
        public int RendererCount => 3;
        public int TriangleCount => Emitters * Rows * Sides * 2 + Emitters * Collars * RingSides * 2
            + Emitters * BowlRows * RingSides * 2 + (Emitters * StreamDrops + ImpactDrops) * 2;

        public static HydroPumpEffect Create(Transform parent, Vector3 source, Vector3 target, float width = .32f)
        {
            if (!Finite(source) || !Finite(target) || (target - source).sqrMagnitude < .0001f)
                throw new ArgumentException("Hydro Pump requires distinct finite world endpoints.");
            var go = new GameObject("Hydrocanon · original pressure columns"); go.transform.SetParent(parent, false);
            var effect = go.AddComponent<HydroPumpEffect>(); effect.source = source; effect.target = target;
            effect.radius = Safe(width) ? Mathf.Clamp(width, .07f, .95f) : .32f;
            effect.victimRadius = effect.radius * 3f; effect.victimHeight = effect.radius * 6f;
            try { effect.Initialize(); } catch { Dispose(go); throw; } return effect;
        }
        public void SetEndpoints(Vector3 source, Vector3 target)
        { if (Finite(source) && Finite(target) && (target - source).sqrMagnitude > .0001f) { this.source = source; this.target = target; } }
        public void SetSecondaryEmitter(Vector3 source)
        { if (Finite(source) && (target - source).sqrMagnitude > .0001f) { secondary = source; hasSecondary = true; } }
        public void SetVictimBounds(Bounds bounds)
        {
            if (!Finite(bounds.center) || !Finite(bounds.size)) return;
            victimSurface = bounds; victimCenter = bounds.center; if (!hasFloor) floorY = bounds.min.y + .02f; hasVictimBounds = true;
            victimRadius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), radius, 2.5f);
            victimHeight = Mathf.Clamp(bounds.size.y, .2f, 6f);
        }
        public void SetFloor(float ground) { if (Safe(ground)) { floorY = ground + .025f; hasFloor = true; } }
        public void SetShieldContact(Vector3 point, Vector3 outwardNormal)
        {
            if (!Finite(point) || !Finite(outwardNormal) || outwardNormal.sqrMagnitude < .0001f) return;
            blocked = true; contact = point; shieldNormal = outwardNormal.normalized; ImpactEnabled = false;
        }
        public void SetSecondaryShieldContact(Vector3 point, Vector3 outwardNormal)
        {
            if (!Finite(point) || !Finite(outwardNormal) || outwardNormal.sqrMagnitude < .0001f) return;
            secondaryContact = point; secondaryNormal = outwardNormal.normalized; hasSecondaryContact = true;
        }
        public void SetShieldSurface(Bounds surface,float ground)
        {shieldSurface=surface;floorY=ground+.025f;hasShieldSurface=true;}
        void Initialize()
        {
            var reference = Resources.Load<Material>("Materials/WaterGunReference");
            shader = reference != null ? reference.shader : Shader.Find("AeroStadium/Original Water Gun");
            if (shader == null) throw new InvalidOperationException("Hydro Pump requires the water reference material.");
            data[0] = new MeshData("Hydrocanon · cylindrical liquid", Emitters * (Rows + 1) * (Sides + 1), Emitters * Rows * Sides * 6);
            data[1] = new MeshData("Hydrocanon · foam collars and contact bowls", Emitters * Collars * (RingSides + 1) * 2 + Emitters * (BowlRows + 1) * (RingSides + 1), (Emitters * Collars + Emitters * BowlRows) * RingSides * 6);
            data[2] = new MeshData("Hydrocanon · ballistic fragments", (Emitters * StreamDrops + ImpactDrops) * 4, (Emitters * StreamDrops + ImpactDrops) * 6);
            for (int e = 0; e < Emitters; e++) Grid(data[0], e * (Rows + 1) * (Sides + 1), Rows, Sides, true);
            for (int r = 0; r < Emitters * Collars; r++) Grid(data[1], r * (RingSides + 1) * 2, RingSides, 1, false);
            for (int e = 0; e < Emitters; e++) Grid(data[1], Emitters * Collars * (RingSides + 1) * 2 + e * (BowlRows + 1) * (RingSides + 1), BowlRows, RingSides, true);
            for (int i = 0; i < Emitters * StreamDrops + ImpactDrops; i++)
            {
                Grid(data[2], i * 4, 1, 1, false);
                data[2].uv[i * 4] = Vector2.zero; data[2].uv[i * 4 + 1] = Vector2.right;
                data[2].uv[i * 4 + 2] = Vector2.up; data[2].uv[i * 4 + 3] = Vector2.one;
            }
            for (int i = 0; i < 3; i++)
            {
                data[i].Finish(); materials[i] = reference != null ? new Material(reference) : new Material(shader);
                materials[i].name = data[i].mesh.name; materials[i].SetFloat("_Mode", i == 1 ? 2 : i == 2 ? 1 : 0);
                var child = new GameObject(data[i].mesh.name); child.transform.SetParent(transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = data[i].mesh;
                renderers[i] = child.AddComponent<MeshRenderer>(); renderers[i].sharedMaterial = materials[i];
                renderers[i].shadowCastingMode = ShadowCastingMode.Off; renderers[i].receiveShadows = false;
            }
            initialized = true; UpdateEffect();
        }
        static void Grid(MeshData m, int start, int rows, int cols, bool cylindrical)
        {
            for (int r = 0; r <= rows; r++) for (int c = 0; c <= cols; c++)
            {
                int v = start + r * (cols + 1) + c;
                // Existing water shader: color.b=opacity; color.r=flow seed; UV2.y=phase.
                // Tube UV.x stays near .5 so a closed cylinder has no transparent seam.
                m.uv[v] = new Vector2(cylindrical ? .5f + .12f * Mathf.Sin(c / (float)cols * Mathf.PI * 2f) : c / (float)cols, r / (float)rows * (cylindrical ? 10f : 4f));
                m.phase[v] = new Vector2(r / (float)rows, start * .013f);
                if (r == rows || c == cols) continue;
                int a = v, b = v + 1, d = v + cols + 2, z = v + cols + 1;
                m.triangles[m.index++] = a; m.triangles[m.index++] = z; m.triangles[m.index++] = b;
                m.triangles[m.index++] = b; m.triangles[m.index++] = z; m.triangles[m.index++] = d;
            }
        }
        void Update()
        {
            if (!initialized || released) return; elapsed = Mathf.Min(Total, elapsed + Time.deltaTime);
            if (elapsed >= Total) { ImpactOpacity = ContactOpacity = 0; ReleaseResources(); return; } UpdateEffect();
        }
        void UpdateEffect()
        {
            float charge = Smooth(0, .08f, elapsed) * (1 - Smooth(Launch, .26f, elapsed));
            float power = Smooth(Launch, .255f, elapsed) * (1 - Smooth(Release, 1.98f, elapsed));
            float energy = Smooth(Hit, Hit + .035f, elapsed) * (1 - Smooth(1.80f, Total, elapsed));
            ImpactOpacity = ImpactEnabled && !blocked ? energy : 0; ContactOpacity = blocked ? energy : 0;
            Matrix4x4 local = transform.worldToLocalMatrix, normal = transform.localToWorldMatrix.transpose;
            float tubeRadius = radius * (hasSecondary ? .76f : 1f); int active = 0; RunoffDrops = BodyRunoffDrops = 0;
            BodySurfaceFlow = !blocked && hasVictimBounds && (BodyContact(source, out _, out _) || hasSecondary && BodyContact(secondary, out _, out _)) && ImpactOpacity > .02f;
            BodyContact(source, out Vector3 bodyPoint, out _); BodyContactPoint = bodyPoint;
            for (int e = 0; e < Emitters; e++)
            {
                Vector3 origin = e == 0 ? source : secondary;
                if (e == 1 && !hasSecondary) origin = source;
                bool body = ImpactContact(e, out Vector3 end, out _); Vector3 delta = end - origin;
                float length = Mathf.Max(.01f, delta.magnitude);
                Vector3 forward = delta.sqrMagnitude > .00001f ? delta.normalized : Vector3.forward;
                float head = Smooth(Launch, Hit, elapsed);
                float rear = Smooth(Release, Total, elapsed) * head, visible = e == 0 || hasSecondary ? 1 : 0;
                for (int row = 0; row <= Rows; row++)
                {
                    float u = row / (float)Rows, p = Mathf.Lerp(rear, head, u);
                    Vector3 center = Center(origin, forward, p * length, out Vector3 tangent);
                    Basis(tangent, out Vector3 right, out Vector3 up);
                    float corrugation = 1 + .085f * Mathf.Sin(p * 82 - elapsed * 62 + e * 2.1f);
                    float width = tubeRadius * (.82f + p * .25f) * corrugation;
                    float breakage = Mathf.Lerp(1, Smooth(.1f, .48f, Mathf.Sin(p * 107 - elapsed * 55) * .5f + .5f), Smooth(1.73f, 1.98f, elapsed));
                    float opacity = power * visible * breakage * (1 - Smooth(body ? .98f : .94f, 1, u));
                    for (int side = 0; side <= Sides; side++)
                    {
                        float angle = side / (float)Sides * Mathf.PI * 2; Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                        int v = e * (Rows + 1) * (Sides + 1) + row * (Sides + 1) + side;
                        Put(data[0], v, center + radial * width, radial, new Color(e * .41f + side * .03f, 1, opacity, 1), local, normal);
                    }
                }
                for (int ring = 0; ring < Collars; ring++)
                {
                    float p = Mathf.Repeat((elapsed - Launch) * 1.9f + ring / (float)Collars, 1);
                    float progress = Mathf.Lerp(0, p, Smooth(Launch, .28f, elapsed));
                    Vector3 center = Center(origin, forward, progress * length, out Vector3 tangent);
                    Basis(tangent, out Vector3 right, out Vector3 up);
                    float outer = tubeRadius * (1.15f + .16f * Mathf.Sin(elapsed * 24 + ring)) + charge * tubeRadius * .6f;
                    float opacity = visible * (charge * .7f + (progress <= head && progress >= rear ? power * .78f : 0));
                    int start = (e * Collars + ring) * (RingSides + 1) * 2;
                    for (int side = 0; side <= RingSides; side++)
                    {
                        float angle = side / (float)RingSides * Mathf.PI * 2; Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                        float crest = 1 + .09f * Mathf.Sin(angle * 7 + elapsed * 31 + ring);
                        for (int k = 0; k < 2; k++) Put(data[1], start + side * 2 + k,
                            center + radial * outer * crest * (k == 0 ? .78f : 1f) - tangent * tubeRadius * k * .25f,
                            (radial - tangent * .5f).normalized, new Color(ring * .71f, 1, opacity, 1), local, normal);
                    }
                }
                for (int i = 0; i < StreamDrops; i++)
                {
                    float p = Mathf.Repeat((elapsed - Launch) * 2.5f + i * .618034f, 1), angle = i * 2.399963f + elapsed * 2;
                    Vector3 center = Center(origin, forward, p * length, out Vector3 tangent);
                    Basis(tangent, out Vector3 right, out Vector3 up);
                    center += (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * tubeRadius * (1.15f + p * .7f);
                    center += Vector3.down * p * p * radius * .9f;
                    float opacity = visible * (p <= head && p >= rear ? power : 0) * (.52f + .45f * Smooth(1.7f, 1.9f, elapsed));
                    Drop(e * StreamDrops + i, center, radius * (.06f + i % 5 * .012f), i * .67f, opacity, 2.1f + p, local);
                    if (opacity > .02f) active++;
                }
            }
            float spread = blocked ? radius * 3 : Mathf.Min(Mathf.Max(victimRadius * .92f, radius * 3.6f), radius * 7);
            float age = Mathf.Max(0, elapsed - Hit), splash = ImpactOpacity + ContactOpacity;
            for (int e = 0; e < Emitters; e++)
            {
                bool body = ImpactContact(e, out Vector3 hit, out Vector3 outward);
                if (outward.sqrMagnitude < .001f) outward = Vector3.back;
                Basis(outward, out Vector3 impactRight, out Vector3 impactUp);
                float visible = e == 0 || hasSecondary && (!blocked || hasSecondaryContact && (secondaryContact - contact).sqrMagnitude > .0025f) ? 1 : 0;
                int bowl = Emitters * Collars * (RingSides + 1) * 2 + e * (BowlRows + 1) * (RingSides + 1);
                for (int row = 0; row <= BowlRows; row++) for (int side = 0; side <= RingSides; side++)
                {
                    float p = row / (float)BowlRows, a = side / (float)RingSides * Mathf.PI * 2;
                    Vector3 radial = impactRight * Mathf.Cos(a) + impactUp * Mathf.Sin(a);
                    float scallop = .88f + .12f * Mathf.Sin(a * 9 - elapsed * 21), beat = 1 + .13f * Mathf.Sin(age * 26);
                    Vector3 point = hit + radial * spread * p * scallop * beat + outward * radius * (p * p * (blocked ? .65f : 2.9f) + .15f);
                    if (blocked) point += Vector3.down * p * p * radius * .6f;
                    float opacity = visible * splash * (.83f - p * .34f) * (1 - Smooth(.87f, 1, p));
                    Vector3 surfaceNormal = (outward - radial * p).normalized;
                    if (body)
                    {
                        point = BodyFlow(hit, p, a, out surfaceNormal);
                        opacity *= .55f + .35f * Mathf.Abs(Mathf.Sin(a * 3 + elapsed * 5));
                    }
                    Put(data[1], bowl + row * (RingSides + 1) + side, point, surfaceNormal,
                        new Color(p * 2.1f, 1, opacity, 1), local, normal);
                }
            }
            for (int i = 0; i < ImpactDrops; i++)
            {
                int e = hasSecondary && (!blocked || hasSecondaryContact) ? i % Emitters : 0;
                bool body = ImpactContact(e, out Vector3 hit, out Vector3 outward);
                if (outward.sqrMagnitude < .001f) outward = Vector3.back;
                Basis(outward, out Vector3 impactRight, out Vector3 impactUp);
                float p = Mathf.Repeat(age * 1.65f + i * .618034f, 1), a = i * 2.399963f;
                Vector3 radial = impactRight * Mathf.Cos(a) + impactUp * Mathf.Sin(a);
                Vector3 point = hit + radial * p * spread * 1.45f + outward * p * radius * (blocked ? 1.2f : 3.4f)
                    + Vector3.down * p * p * Mathf.Min(victimHeight * .45f, radius * 5);
                float opacity = splash * Mathf.Sin(p * Mathf.PI), elongation = 1.4f + p * 2;
                if (blocked && i >= 56)
                {
                    p = Mathf.Repeat(age * .95f + (i - 56) / 24f, 1); point = Runoff(hit, outward, p, a);
                    opacity = ContactOpacity * Mathf.Sin(p * Mathf.PI) * Smooth(p * .8f, p * .8f + .1f, age);
                    elongation = Mathf.Lerp(4.5f, 1.1f, Smooth(.84f, 1, p));
                }
                else if (body && i >= 32)
                {
                    p = Mathf.Repeat(age * .85f + (i - 32) / 48f, 1); point = BodyFlow(hit, p, a, out _);
                    opacity = ImpactOpacity * Mathf.Sin(p * Mathf.PI) * Smooth(p * .8f, p * .8f + .1f, age);
                    elongation = Mathf.Lerp(2.5f, 5.5f, Smooth(.66f, .86f, p));
                    if (opacity > .02f && p > .72f) BodyRunoffDrops++;
                }
                Drop(Emitters * StreamDrops + i, point, radius * (.075f + i % 6 * .019f), i * .81f, opacity, elongation, local);
                if (opacity > .02f) {active++;if(blocked&&i>=56)RunoffDrops++;}
            }
            PeakDroplets = Mathf.Max(PeakDroplets, active);
            var bounds = new Bounds(local.MultiplyPoint3x4(source), Vector3.zero); bounds.Encapsulate(local.MultiplyPoint3x4(target));
            if (hasSecondary) bounds.Encapsulate(local.MultiplyPoint3x4(secondary));
            if (blocked) { bounds.Encapsulate(local.MultiplyPoint3x4(contact)); bounds.Encapsulate(local.MultiplyPoint3x4(ShieldPoint(1))); }
            if (hasVictimBounds) bounds.Encapsulate(local.MultiplyPoint3x4(new Vector3(victimCenter.x, floorY, victimCenter.z)));
            float pad = Mathf.Max(radius * 12, spread * 2);
            bounds.Expand((Abs(local.MultiplyVector(Vector3.right * pad)) + Abs(local.MultiplyVector(Vector3.up * pad)) + Abs(local.MultiplyVector(Vector3.forward * pad))) * 2);
            for (int i = 0; i < 3; i++) data[i].Upload(bounds);
        }
        Vector3 ShieldPoint(int e) => e == 1 && hasSecondaryContact ? secondaryContact : contact;
        Vector3 ShieldNormal(int e) => e == 1 && hasSecondaryContact ? secondaryNormal : shieldNormal;
        bool ImpactContact(int e, out Vector3 hit, out Vector3 outward)
        {
            if (blocked) { hit = ShieldPoint(e); outward = ShieldNormal(e); return false; }
            return BodyContact(e == 1 && hasSecondary ? secondary : source, out hit, out outward);
        }
        bool BodyContact(Vector3 origin, out Vector3 hit, out Vector3 outward)
        {
            hit = target; outward = (origin - target).normalized;
            if (!hasVictimBounds) return false;
            Vector3 c = victimSurface.center, r = BodyRadii(), d = target - origin;
            Vector3 q = new Vector3((origin.x - c.x) / r.x, (origin.y - c.y) / r.y, (origin.z - c.z) / r.z);
            Vector3 v = new Vector3(d.x / r.x, d.y / r.y, d.z / r.z);
            float a = Vector3.Dot(v, v), b = Vector3.Dot(q, v), discriminant = b * b - a * (Vector3.Dot(q, q) - 1);
            if (a < .00001f || discriminant < 0) return false;
            float t = (-b - Mathf.Sqrt(discriminant)) / a;
            if (t < 0 || t > 1) return false;
            hit = origin + d * t;
            outward = new Vector3((hit.x - c.x) / (r.x * r.x), (hit.y - c.y) / (r.y * r.y), (hit.z - c.z) / (r.z * r.z)).normalized;
            return true;
        }
        Vector3 BodyRadii() => Vector3.Max(victimSurface.extents, Vector3.one * .05f);
        Vector3 BodyFlow(Vector3 hit, float p, float seed, out Vector3 outward)
        {
            Vector3 c = victimSurface.center, r = BodyRadii();
            Vector3 n = new Vector3((hit.x - c.x) / r.x, (hit.y - c.y) / r.y, (hit.z - c.z) / r.z).normalized;
            float surface = Mathf.Clamp01(p / .72f), fan = Mathf.Sin(surface * Mathf.PI * .5f);
            float yaw = Mathf.Atan2(n.z, n.x) + Mathf.Sin(seed) * 1.70f * fan;
            float elevation = Mathf.Lerp(Mathf.Asin(Mathf.Clamp(n.y, -1, 1)), -1.28f, surface) + Mathf.Cos(seed) * .22f * Mathf.Sin(surface * Mathf.PI);
            Vector3 radial = new Vector3(Mathf.Cos(elevation) * Mathf.Cos(yaw), Mathf.Sin(elevation), Mathf.Cos(elevation) * Mathf.Sin(yaw));
            outward = new Vector3(radial.x / r.x, radial.y / r.y, radial.z / r.z).normalized;
            Vector3 point = c + Vector3.Scale(radial, r) + outward * .025f;
            if (p > .72f) point.y = Mathf.Lerp(point.y, floorY, (p - .72f) / .28f);
            point.y = Mathf.Max(floorY, point.y);
            Vector3 q = new Vector3((point.x - c.x) / r.x, (point.y - c.y) / r.y, (point.z - c.z) / r.z);
            float flat = q.x * q.x + q.z * q.z, skirt = Mathf.Max(0, 1 - q.y * q.y);
            if (flat > .00001f && flat < skirt) { float scale = Mathf.Sqrt(skirt / flat) + .015f; point.x = c.x + (point.x - c.x) * scale; point.z = c.z + (point.z - c.z) * scale; }
            return point;
        }
        static Vector3 Center(Vector3 origin, Vector3 forward, float distance, out Vector3 tangent)
        { tangent = forward; return origin + forward * distance; }
        Vector3 Runoff(Vector3 hit, Vector3 outward, float p, float seed)
        {
            if(hasShieldSurface)
            {
                Vector3 c=shieldSurface.center,r=shieldSurface.extents;
                r=new Vector3(Mathf.Max(.1f,r.x),Mathf.Max(.1f,r.y),Mathf.Max(.1f,r.z));
                Vector3 n=new Vector3((hit.x-c.x)/r.x,(hit.y-c.y)/r.y,(hit.z-c.z)/r.z).normalized;
                float yaw=Mathf.Atan2(n.z,n.x)+Mathf.Sin(seed)*.16f*Mathf.Sin(p*Mathf.PI);
                float elevation=Mathf.Lerp(Mathf.Asin(Mathf.Clamp(n.y,-1,1)),-1.20f,Mathf.Clamp01(p/.70f));
                Vector3 radial=new Vector3(Mathf.Cos(elevation)*Mathf.Cos(yaw),Mathf.Sin(elevation),Mathf.Cos(elevation)*Mathf.Sin(yaw));
                Vector3 q=c+Vector3.Scale(radial,r)+radial*.03f;
                if(p>.70f)q.y=Mathf.Lerp(q.y,floorY,(p-.70f)/.30f);
                q.y=Mathf.Max(floorY,q.y);return q;
            }
            Vector3 center = hasVictimBounds ? victimCenter : target;
            float ground = hasVictimBounds ? floorY : center.y - victimHeight * .5f;
            Vector3 flat = hit - center; flat.y = 0;
            if (flat.sqrMagnitude < .0001f) { flat = outward; flat.y = 0; }
            if (flat.sqrMagnitude < .0001f) flat = Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, flat.normalized);
            float halfHeight = Mathf.Max(victimHeight * .68f, Mathf.Abs(hit.y - center.y) + radius);
            float initial = Mathf.Clamp((hit.y - center.y) / halfHeight, -.98f, .98f), y = Mathf.Lerp(hit.y, ground, p);
            float level = Mathf.Clamp((y - center.y) / halfHeight, -.98f, .98f);
            float curve = Mathf.Sqrt(1 - level * level) / Mathf.Max(.15f, Mathf.Sqrt(1 - initial * initial));
            Vector3 point = center + flat * curve + side * Mathf.Sin(seed) * radius * .16f + outward * radius * .08f;
            point.y = Mathf.Max(ground, y); return point;
        }
        static void Put(MeshData m, int v, Vector3 point, Vector3 n, Color color, Matrix4x4 local, Matrix4x4 normal)
        { m.vertices[v] = local.MultiplyPoint3x4(point); m.normals[v] = normal.MultiplyVector(n).normalized; m.colors[v] = color; }
        void Drop(int i, Vector3 point, float size, float seed, float opacity, float elongation, Matrix4x4 local)
        {
            Vector3 p = local.MultiplyPoint3x4(point); Color c = new Color(size, seed, opacity, elongation);
            for (int k = 0; k < 4; k++) { data[2].vertices[i * 4 + k] = p; data[2].colors[i * 4 + k] = c; }
        }
        static void Basis(Vector3 forward, out Vector3 right, out Vector3 up)
        { right = Vector3.Cross(Mathf.Abs(forward.y) > .95f ? Vector3.forward : Vector3.up, forward).normalized; up = Vector3.Cross(forward, right).normalized; }
        static float Smooth(float from, float to, float t) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, t));
        static bool Safe(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Safe(v.x) && Safe(v.y) && Safe(v.z) && Safe(v.sqrMagnitude);
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        public bool Validate(out string error)
        {
            error = null;
            if (!initialized || shader == null || !shader.isSupported) error = "Hydro Pump shader is unavailable.";
            else if (!Finite(source) || !Finite(target) || (target - source).sqrMagnitude < .0001f) error = "Hydro Pump endpoints are invalid.";
            else if (hasSecondary && !Finite(secondary)) error = "Hydro Pump secondary emitter is invalid.";
            else if (GetComponentsInChildren<Collider>().Length != 0) error = "Hydro Pump cannot create physical colliders.";
            return error == null;
        }
        void ReleaseResources()
        {
            if (released) return; released = true; BodySurfaceFlow = false; BodyRunoffDrops = 0;
            for (int i = 0; i < 3; i++) { if (renderers[i] != null) renderers[i].enabled = false; if (data[i] != null) Dispose(data[i].mesh); Dispose(materials[i]); }
        }
        static void Dispose(UnityEngine.Object obj) { if (obj == null) return; if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        void OnDestroy() => ReleaseResources();
        sealed class MeshData
        {
            public readonly Mesh mesh; public readonly Vector3[] vertices, normals;
            public readonly Color[] colors; public readonly Vector2[] uv, phase; public readonly int[] triangles; public int index;
            public MeshData(string name, int count, int indices)
            { mesh = new Mesh { name = name }; vertices = new Vector3[count]; normals = new Vector3[count]; colors = new Color[count]; uv = new Vector2[count]; phase = new Vector2[count]; triangles = new int[indices]; }
            public void Finish() { mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.uv2 = phase; mesh.triangles = triangles; mesh.MarkDynamic(); }
            public void Upload(Bounds bounds) { mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.bounds = bounds; }
        }
    }
}
