using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>A finite water packet, followed by a liquid splash and shield runoff.</summary>
    public sealed class WaterGunEffect : MonoBehaviour
    {
        const int Sheets = 3, Segments = 28, Across = 6;
        const int StreamDrops = 72, ImpactDrops = 72, MouthDrops = 8;
        const int RingSegments = 28, Rings = 2, Fans = 8, FanSegments = 7;
        const float TotalDuration = 1.65f, LaunchTime = .17f, TailTime = .25f, HitTime = .52f;
        readonly UnityEngine.Object[] owned = new UnityEngine.Object[6];
        readonly MeshRenderer[] renderers = new MeshRenderer[3];
        Mesh jetMesh, dropMesh, splashMesh;
        Shader shader;
        Vector3[] jetVertices, jetNormals, dropVertices, splashVertices, splashNormals;
        Color[] jetColors, dropColors, splashColors;
        Vector3 source, target, shieldContact, shieldNormal, launchOrigin, launchDirection;
        float radius, elapsed, victimRadius, victimHeight, groundY, flightLength, flightSpeed, impactAt = -1f;
        Bounds shieldBounds;
        bool initialized, released, shielded, launched;
        public int RunoffDrops { get; private set; }
        public bool ImpactEnabled { get; set; } = true;
        public float Duration => TotalDuration;
        public float EffectProgress => Mathf.Clamp01(elapsed / TotalDuration);
        public float AnimationPhase => elapsed * 3f;
        public bool IsPlaying => initialized && !released && elapsed < TotalDuration;
        public bool Completed => initialized && elapsed >= TotalDuration;
        public bool ImpactReached => impactAt >= 0f;
        public float ImpactOpacity { get; private set; }
        public float ContactOpacity { get; private set; }
        public int PeakDroplets { get; private set; }
        // Latched only after a visible packet has physically cleared its launch position.
        public bool ProjectileDetached { get; private set; }
        public bool SourceConnectionCleared { get; private set; }
        public float ProjectileTravelled { get; private set; }
        public float ProjectileLength { get; private set; }
        public Vector3 FlightFront { get; private set; }
        public Vector3 FlightRear { get; private set; }
        public Vector3 TrajectoryOrigin => launchOrigin;
        public Vector3 TrajectoryDirection => launchDirection;
        public bool HasLaunched => launched;
        public Vector3 ShieldContactPoint => shieldContact;
        public Vector3 ShieldContactNormal => shieldNormal;
        public int RendererCount => 3;
        public int TriangleCount => Sheets * Segments * Across * 2 + (StreamDrops + ImpactDrops + MouthDrops) * 2
            + Rings * RingSegments * 2 + Fans * FanSegments * 2;

        public static WaterGunEffect Create(Transform parent, Vector3 source, Vector3 target, float width = .18f)
        {
            if (!Finite(source) || !Finite(target) || (target - source).sqrMagnitude < .0001f)
                throw new ArgumentException("Water Gun needs distinct finite world endpoints.");
            var go = new GameObject("Pistolet à O · detached water packet"); go.transform.SetParent(parent, false);
            var effect = go.AddComponent<WaterGunEffect>(); effect.source = source; effect.target = target;
            effect.radius = float.IsNaN(width) || float.IsInfinity(width) ? .18f : Mathf.Clamp(width, .055f, .8f);
            effect.victimRadius = effect.radius * 2.8f; effect.victimHeight = effect.radius * 5f;
            try { effect.Initialize(); }
            catch { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); throw; }
            return effect;
        }
        public void SetEndpoints(Vector3 source, Vector3 target)
        {
            if (launched || !Finite(source) || !Finite(target) || (target - source).sqrMagnitude < .0001f) return;
            this.source = source; this.target = target;
        }
        public void SetVictimBounds(Bounds bounds)
        {
            victimHeight = Mathf.Clamp(bounds.size.y, .2f, 6f);
            victimRadius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z), radius, 2f);
        }
        public void SetShieldContact(Vector3 point, Vector3 outwardNormal, Bounds surface, float floorY)
        {
            if (!Finite(point) || !Finite(outwardNormal) || outwardNormal.sqrMagnitude < .0001f) return;
            if (!launched || !shielded) { shieldContact = point; shieldNormal = outwardNormal.normalized; }
            shielded = true;
            shieldBounds = surface; groundY = floorY + .025f; ImpactEnabled = false;
            if (launched) UpdateShieldIntersection();
        }
        void UpdateShieldIntersection()
        {
            // Intersect the moving shell with the captured flight ray, never the later muzzle pose.
            Vector3 r = shieldBounds.extents;
            r = new Vector3(Mathf.Max(.1f, r.x), Mathf.Max(.1f, r.y), Mathf.Max(.1f, r.z));
            Vector3 o = launchOrigin - shieldBounds.center;
            Vector3 q = new Vector3(o.x / r.x, o.y / r.y, o.z / r.z);
            Vector3 d = new Vector3(launchDirection.x / r.x, launchDirection.y / r.y, launchDirection.z / r.z);
            float a = Vector3.Dot(d, d), b = Vector3.Dot(q, d), c = Vector3.Dot(q, q) - 1f;
            float discriminant = b * b - a * c;
            if (a < .000001f || discriminant < 0f) return;
            float distance = (-b - Mathf.Sqrt(discriminant)) / a;
            if (distance <= .01f) return;
            flightLength = distance; shieldContact = launchOrigin + launchDirection * distance;
            Vector3 n = shieldContact - shieldBounds.center;
            shieldNormal = new Vector3(n.x / (r.x * r.x), n.y / (r.y * r.y), n.z / (r.z * r.z)).normalized;
        }
        Vector3 RunoffPoint(float p, float lateral)
        {
            Vector3 r = shieldBounds.extents;
            r = new Vector3(Mathf.Max(.1f, r.x), Mathf.Max(.1f, r.y), Mathf.Max(.1f, r.z));
            Vector3 c = shieldBounds.center;
            Vector3 n = new Vector3((shieldContact.x-c.x)/r.x, (shieldContact.y-c.y)/r.y, (shieldContact.z-c.z)/r.z).normalized;
            float yaw = Mathf.Atan2(n.z, n.x) + lateral * .18f * Mathf.Sin(p * Mathf.PI);
            float elevation = Mathf.Lerp(Mathf.Asin(Mathf.Clamp(n.y, -1, 1)), -1.20f, Mathf.Clamp01(p / .70f));
            Vector3 radial = new Vector3(Mathf.Cos(elevation)*Mathf.Cos(yaw), Mathf.Sin(elevation), Mathf.Cos(elevation)*Mathf.Sin(yaw));
            Vector3 point = c + Vector3.Scale(radial, r) + radial * .028f;
            if (p > .70f) point.y = Mathf.Lerp(point.y, groundY, (p - .70f) / .30f);
            point.y = Mathf.Max(groundY, point.y); return point;
        }
        void Initialize()
        {
            Material reference = Resources.Load<Material>("Materials/WaterGunReference");
            shader = reference != null ? reference.shader : Shader.Find("AeroStadium/Original Water Gun");
            if (shader == null) throw new InvalidOperationException("The original Water Gun shader is missing.");
            jetMesh = CreateJetMesh(); dropMesh = CreateDropletMesh(); splashMesh = CreateSplashMesh();
            Mesh[] meshes = { jetMesh, dropMesh, splashMesh };
            for (int i = 0; i < 3; i++)
            {
                owned[i * 2] = meshes[i]; var material = reference != null ? new Material(reference) : new Material(shader);
                material.name = "Pistolet à O · " + (i == 0 ? "water packet" : i == 1 ? "finite droplets" : "surface splash");
                material.SetFloat("_Mode", i); owned[i * 2 + 1] = material;
                var go = new GameObject(material.name); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                renderers[i] = go.AddComponent<MeshRenderer>(); renderers[i].sharedMaterial = material;
                renderers[i].shadowCastingMode = ShadowCastingMode.Off; renderers[i].receiveShadows = false;
            }
            initialized = true; UpdateEffect();
        }
        Mesh CreateJetMesh()
        {
            int count = Sheets * (Segments + 1) * (Across + 1), index = 0;
            jetVertices = new Vector3[count]; jetNormals = new Vector3[count]; jetColors = new Color[count];
            var uv = new Vector2[count]; var phase = new Vector2[count]; var triangles = new int[Sheets * Segments * Across * 6];
            for (int sheet = 0; sheet < Sheets; sheet++)
            {
                int start = sheet * (Segments + 1) * (Across + 1);
                for (int row = 0; row <= Segments; row++) for (int col = 0; col <= Across; col++)
                {
                    int v = start + row * (Across + 1) + col;
                    uv[v] = new Vector2(col / (float)Across, row / (float)Segments * 3f);
                    phase[v] = new Vector2(row / (float)Segments, sheet * 2.71f);
                }
                for (int row = 0; row < Segments; row++) for (int col = 0; col < Across; col++)
                { int a = start + row * (Across + 1) + col; AddQuad(triangles, ref index, a, a+1, a+Across+1, a+Across+2); }
            }
            var mesh = new Mesh { name = "Pistolet à O · finite tapered packet", vertices = jetVertices, normals = jetNormals, uv = uv, uv2 = phase, triangles = triangles };
            mesh.MarkDynamic(); return mesh;
        }
        Mesh CreateDropletMesh()
        {
            int count = StreamDrops + ImpactDrops + MouthDrops, index = 0;
            dropVertices = new Vector3[count*4]; dropColors = new Color[count*4];
            var uv = new Vector2[count*4]; var triangles = new int[count*6];
            for (int i = 0; i < count; i++)
            {
                int v = i*4; uv[v] = Vector2.zero; uv[v+1] = Vector2.right; uv[v+2] = Vector2.up; uv[v+3] = Vector2.one;
                AddQuad(triangles, ref index, v, v+1, v+2, v+3);
            }
            var mesh = new Mesh { name = "Pistolet à O · finite spray", vertices = dropVertices, uv = uv, triangles = triangles };
            mesh.MarkDynamic(); return mesh;
        }
        Mesh CreateSplashMesh()
        {
            int count = Rings*(RingSegments+1)*2 + Fans*(FanSegments+1)*2, index = 0, start = 0;
            splashVertices = new Vector3[count]; splashNormals = new Vector3[count]; splashColors = new Color[count];
            var uv = new Vector2[count]; var triangles = new int[(Rings*RingSegments+Fans*FanSegments)*6];
            for (int strip = 0; strip < Rings + Fans; strip++)
            {
                int segments = strip < Rings ? RingSegments : FanSegments;
                for (int row = 0; row <= segments; row++)
                {
                    uv[start+row*2] = new Vector2(0, row/(float)segments*(strip < Rings ? 4 : 1));
                    uv[start+row*2+1] = new Vector2(1, uv[start+row*2].y);
                    if (row < segments) AddQuad(triangles, ref index, start+row*2, start+row*2+1, start+row*2+2, start+row*2+3);
                }
                start += (segments+1)*2;
            }
            var mesh = new Mesh { name = "Pistolet à O · splash crown and runoff", vertices = splashVertices, normals = splashNormals, uv = uv, triangles = triangles };
            mesh.MarkDynamic(); return mesh;
        }
        static void AddQuad(int[] t, ref int i, int a, int b, int c, int d)
        { t[i++] = a; t[i++] = c; t[i++] = b; t[i++] = b; t[i++] = c; t[i++] = d; }
        void Update()
        {
            if (!initialized || released) return;
            elapsed = Mathf.Min(TotalDuration, elapsed + Time.deltaTime);
            if (elapsed >= TotalDuration) { ImpactOpacity = ContactOpacity = ProjectileLength = 0f; RunoffDrops = 0; ReleaseResources(); return; }
            UpdateEffect();
        }
        void UpdateEffect()
        {
            if (!launched && elapsed >= LaunchTime)
            {
                launched = true; launchOrigin = source; launchDirection = (target-source).normalized; flightLength = Vector3.Distance(source, target);
                if (shielded) UpdateShieldIntersection();
                flightSpeed = flightLength / (HitTime-LaunchTime);
            }
            if (launched && shielded) UpdateShieldIntersection();
            Vector3 forward = launched ? launchDirection : (target-source).normalized;
            Vector3 origin = launched ? launchOrigin : source;
            float front = launched ? Mathf.Min(flightLength, Mathf.Max(0, elapsed-LaunchTime)*flightSpeed) : 0;
            float rear = launched ? Mathf.Min(flightLength, Mathf.Max(0, elapsed-TailTime)*flightSpeed) : 0;
            if (launched && !ImpactReached && front >= flightLength) impactAt = elapsed;
            float strength = front-rear > .0001f ? Smooth(LaunchTime, LaunchTime+.025f, elapsed) : 0;
            float charge = Smooth(0, .07f, elapsed) * (1-Smooth(LaunchTime, TailTime, elapsed));
            float contact = ImpactReached ? Smooth(impactAt, impactAt+.055f, elapsed) * (1-Smooth(1.30f, TotalDuration, elapsed)) : 0;
            ImpactOpacity = ImpactEnabled && !shielded ? contact : 0; ContactOpacity = shielded ? contact : 0;
            Basis(forward, out Vector3 right, out Vector3 up);
            Matrix4x4 toLocal = transform.worldToLocalMatrix, normalToLocal = transform.localToWorldMatrix.transpose;
            float minProjection = float.PositiveInfinity, maxProjection = float.NegativeInfinity;
            for (int sheet = 0; sheet < Sheets; sheet++) for (int row = 0; row <= Segments; row++)
            {
                float f = row/(float)Segments, distance = Mathf.Lerp(rear, front, f), angle = (sheet*60f+18f)*Mathf.Deg2Rad;
                Vector3 center = PacketCenter(origin, forward, right, up, distance, f);
                Vector3 across = right*Mathf.Cos(angle)+up*Mathf.Sin(angle), depth = Vector3.Cross(forward, across);
                float envelope = Mathf.Pow(Mathf.Max(0, Mathf.Sin(f*Mathf.PI)), .55f);
                float width = radius * envelope * (.65f+.48f*f) * (1+.10f*Mathf.Sin(f*19-elapsed*30+sheet));
                float opacity = strength * envelope * (.72f+.08f*sheet);
                for (int col = 0; col <= Across; col++)
                {
                    float x = col/(float)Across*2-1; int v = sheet*(Segments+1)*(Across+1)+row*(Across+1)+col;
                    Vector3 point = center+across*(x*width)+depth*((1-x*x)*width*.43f);
                    if (shielded && launched) { float inside = Vector3.Dot(point-shieldContact, shieldNormal); if (inside < 0) point -= shieldNormal*inside; }
                    jetVertices[v] = toLocal.MultiplyPoint3x4(point); jetNormals[v] = normalToLocal.MultiplyVector((depth+across*x*.86f).normalized).normalized;
                    jetColors[v] = new Color(sheet*.317f, 1, opacity, 1);
                    if (opacity > .001f) { float p = Vector3.Dot(point-origin, forward); minProjection = Mathf.Min(minProjection, p); maxProjection = Mathf.Max(maxProjection, p); }
                }
            }
            jetMesh.vertices = jetVertices; jetMesh.normals = jetNormals; jetMesh.colors = jetColors;
            bool visiblePacket = minProjection <= maxProjection;
            ProjectileLength = visiblePacket ? maxProjection-minProjection : 0;
            if (visiblePacket) { ProjectileTravelled = Mathf.Max(ProjectileTravelled, maxProjection); FlightRear = origin+forward*minProjection; FlightFront = origin+forward*maxProjection; }
            float nearestLiquid = minProjection; int active = 0; RunoffDrops = 0;
            for (int i = 0; i < StreamDrops; i++)
            {
                float f = (i+.5f)/StreamDrops, distance = Mathf.Lerp(rear, front, f), angle = i*2.399963f+elapsed*1.6f;
                Vector3 center = PacketCenter(origin, forward, right, up, distance, f);
                center += (right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*(.40f+.85f*Mathf.Sin(f*Mathf.PI));
                float opacity = strength*Mathf.Sin(f*Mathf.PI)*.83f, size = radius*(.036f+(i%5)*.007f), elongation = 1.6f+f;
                SetDrop(i, toLocal.MultiplyPoint3x4(center), size, i*.73f, opacity, elongation);
                if (opacity > .02f) active++;
                if (opacity > .001f) nearestLiquid = Mathf.Min(nearestLiquid, distance-size*Mathf.Sqrt(1+elongation*elongation));
            }
            Vector3 contactPoint = (shielded ? shieldContact : target)-forward*radius*.08f;
            float splashRadius = Mathf.Min(Mathf.Max(radius*2.5f, victimRadius*.66f), radius*5.5f);
            for (int i = 0; i < ImpactDrops; i++)
            {
                float age = Mathf.Clamp01((elapsed-impactAt-(i%7)*.018f)/1.0f), angle = i*2.399963f;
                Vector3 radial = right*Mathf.Cos(angle)+up*Mathf.Sin(angle), center;
                if (shielded && i >= 24) center = RunoffPoint(age, (i%9-4)/4f);
                else if (shielded)
                {
                    Basis(shieldNormal, out Vector3 sr, out Vector3 su);
                    center = contactPoint+shieldNormal*age*radius*1.4f+(sr*Mathf.Cos(angle)+su*Mathf.Sin(angle))*age*radius*3+Vector3.down*age*age*victimHeight*.45f;
                    center.y = Mathf.Max(groundY, center.y);
                }
                else center = contactPoint+radial*age*splashRadius*1.5f-forward*age*radius*2+Vector3.down*age*age*Mathf.Min(victimHeight*.28f, radius*3.5f);
                float opacity = (ImpactOpacity+ContactOpacity)*Mathf.Sin(age*Mathf.PI)*.94f;
                SetDrop(StreamDrops+i, toLocal.MultiplyPoint3x4(center), radius*(.045f+(i%4)*.013f), i*.87f, opacity, 1.25f+age*1.8f);
                if (opacity > .02f) { active++; if (shielded && i >= 24) RunoffDrops++; }
            }
            for (int i = 0; i < MouthDrops; i++)
            {
                float angle = i*2.399963f+elapsed*7, opacity = charge*.9f;
                Vector3 center = origin+forward*radius*.20f+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*(.15f+charge*.23f);
                SetDrop(StreamDrops+ImpactDrops+i, toLocal.MultiplyPoint3x4(center), radius*.071f, i, opacity, 1.5f);
                if (opacity > .02f) active++;
                if (opacity > .001f) nearestLiquid = Mathf.Min(nearestLiquid, 0);
            }
            if (visiblePacket && nearestLiquid > radius*.70f) SourceConnectionCleared = ProjectileDetached = true;
            PeakDroplets = Mathf.Max(PeakDroplets, active); dropMesh.vertices = dropVertices; dropMesh.colors = dropColors;
            UpdateSplash(contactPoint, forward, right, up, splashRadius, toLocal, normalToLocal);
            var bounds = new Bounds(toLocal.MultiplyPoint3x4(origin), Vector3.zero); bounds.Encapsulate(toLocal.MultiplyPoint3x4(target));
            if (shielded) { bounds.Encapsulate(toLocal.MultiplyPoint3x4(shieldContact)); bounds.Encapsulate(toLocal.MultiplyPoint3x4(new Vector3(shieldContact.x, groundY, shieldContact.z))); }
            float padding = Mathf.Max(splashRadius*2, radius*7);
            bounds.Expand((Abs(toLocal.MultiplyVector(Vector3.right*padding))+Abs(toLocal.MultiplyVector(Vector3.up*padding))+Abs(toLocal.MultiplyVector(Vector3.forward*padding)))*2);
            jetMesh.bounds = dropMesh.bounds = splashMesh.bounds = bounds;
        }
        Vector3 PacketCenter(Vector3 origin, Vector3 forward, Vector3 right, Vector3 up, float distance, float fraction)
        {
            float bend = Mathf.Sin(fraction*Mathf.PI);
            return origin+forward*distance+right*Mathf.Sin(fraction*17-elapsed*18)*radius*bend*.13f
                +up*Mathf.Sin(fraction*23-elapsed*21)*radius*bend*.10f;
        }
        void UpdateSplash(Vector3 point, Vector3 forward, Vector3 right, Vector3 up, float spread, Matrix4x4 toLocal, Matrix4x4 normalToLocal)
        {
            float energy = ImpactOpacity+ContactOpacity;
            if (shielded) { spread = radius*2.1f; forward = -shieldNormal; Basis(forward, out right, out up); }
            float time = ImpactReached ? Mathf.Max(0, elapsed-impactAt) : 0; int start = 0;
            for (int ring = 0; ring < Rings; ring++)
            {
                float age = Mathf.Clamp01((time-ring*.055f)/.72f), ringRadius = spread*(.14f+age*.85f), ringWidth = radius*(.09f+(1-age)*.20f);
                float opacity = energy*Mathf.Sin(age*Mathf.PI)*.63f;
                for (int row = 0; row <= RingSegments; row++) for (int col = 0; col < 2; col++)
                {
                    float angle = row/(float)RingSegments*Mathf.PI*2, crown = .65f+.35f*Mathf.Sin(angle*7+ring+time*10);
                    Vector3 radial = right*Mathf.Cos(angle)+up*Mathf.Sin(angle); int v = start+row*2+col;
                    Vector3 center = point+radial*(ringRadius+(col-.5f)*ringWidth*2)-forward*(radius*.12f+Mathf.Sin(age*Mathf.PI)*radius*crown*(col == 0 ? .25f : 1.05f));
                    splashVertices[v] = toLocal.MultiplyPoint3x4(center); splashNormals[v] = normalToLocal.MultiplyVector((-forward+radial*.55f).normalized).normalized;
                    splashColors[v] = new Color(ring*2.7f, .85f, opacity, 1);
                }
                start += (RingSegments+1)*2;
            }
            for (int fan = 0; fan < Fans; fan++)
            {
                float angle = fan*Mathf.PI*2/Fans+.12f*Mathf.Sin(time*10+fan);
                Vector3 radial = right*Mathf.Cos(angle)+up*Mathf.Sin(angle), across = Vector3.Cross(forward, radial);
                for (int row = 0; row <= FanSegments; row++)
                {
                    float p = row/(float)FanSegments;
                    Vector3 center = point+radial*(spread*p)-forward*(Mathf.Sin(p*Mathf.PI*.88f)*radius*1.65f)+Vector3.down*p*p*radius*.45f;
                    if (shielded) { center = RunoffPoint(p, (fan-(Fans-1)*.5f)/Fans*2); across = right; }
                    float width = radius*(.07f+p*.20f)*(1-p*.68f), opacity = energy*(1-Smooth(.67f, 1, p))*.58f;
                    for (int col = 0; col < 2; col++)
                    {
                        int v = start+row*2+col; splashVertices[v] = toLocal.MultiplyPoint3x4(center+across*(col == 0 ? -width : width));
                        splashNormals[v] = normalToLocal.MultiplyVector((-forward+radial*p).normalized).normalized; splashColors[v] = new Color(fan*.71f, .85f, opacity, 1);
                    }
                }
                start += (FanSegments+1)*2;
            }
            splashMesh.vertices = splashVertices; splashMesh.normals = splashNormals; splashMesh.colors = splashColors;
        }
        void SetDrop(int index, Vector3 center, float size, float seed, float opacity, float elongation)
        { Color parameters = new Color(size, seed, opacity, elongation); int start = index*4; for (int k = 0; k < 4; k++) { dropVertices[start+k] = center; dropColors[start+k] = parameters; } }
        static void Basis(Vector3 forward, out Vector3 right, out Vector3 up)
        { right = Vector3.Cross(Mathf.Abs(forward.y) > .95f ? Vector3.forward : Vector3.up, forward).normalized; up = Vector3.Cross(forward, right).normalized; }
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.sqrMagnitude);
        public bool Validate(out string error)
        {
            error = null;
            if (!initialized || shader == null || !shader.isSupported) error = "Water Gun has no supported initialized shader.";
            else if (!Finite(source) || !Finite(target) || (source-target).sqrMagnitude < .0001f) error = "Water Gun endpoints must be distinct finite world positions.";
            else if (GetComponentsInChildren<Collider>().Length != 0) error = "Water Gun must not add physical walls.";
            return error == null;
        }
        void ReleaseResources()
        {
            if (released) return; released = true;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
            foreach (var obj in owned) if (obj != null) { if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        }
        void OnDestroy() => ReleaseResources();
    }
}
