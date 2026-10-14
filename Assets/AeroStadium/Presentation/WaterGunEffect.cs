using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Original pressure jet, liquid sheets, ballistic spray and a surface splash.</summary>
    public sealed class WaterGunEffect : MonoBehaviour
    {
        const int Sheets = 3, Segments = 28, Across = 6;
        const int StreamDrops = 72, ImpactDrops = 72, MouthDrops = 8;
        const int RingSegments = 28, Rings = 2, Fans = 8, FanSegments = 7;
        const float TotalDuration = 1.65f, HitTime = .52f, ReleaseTime = 1.24f;
        readonly UnityEngine.Object[] owned = new UnityEngine.Object[6];
        readonly MeshRenderer[] renderers = new MeshRenderer[3];
        Mesh jetMesh, dropMesh, splashMesh;
        Shader shader;
        Vector3[] jetVertices, jetNormals, dropVertices, splashVertices, splashNormals;
        Color[] jetColors, dropColors, splashColors;
        Vector3 source, target, shieldContact, shieldNormal;
        float radius, elapsed, victimRadius, victimHeight, groundY; Bounds shieldBounds;
        public int RunoffDrops {get;private set;}
        bool initialized, released, shielded;

        public bool ImpactEnabled { get; set; } = true;
        public float Duration => TotalDuration;
        public float EffectProgress => Mathf.Clamp01(elapsed / TotalDuration);
        public float AnimationPhase => elapsed * 3f;
        public bool IsPlaying => initialized && !released && elapsed < TotalDuration;
        public bool Completed => initialized && elapsed >= TotalDuration;
        public bool ImpactReached => initialized && elapsed >= HitTime;
        public float ImpactOpacity { get; private set; }
        public float ContactOpacity { get; private set; }
        public int PeakDroplets { get; private set; }
        public int RendererCount => 3;
        public int TriangleCount => Sheets * Segments * Across * 2
            + (StreamDrops + ImpactDrops + MouthDrops) * 2
            + Rings * RingSegments * 2 + Fans * FanSegments * 2;

        /// <param name="width">Outer liquid radius, scaled by the attacker's body height by the caller.</param>
        public static WaterGunEffect Create(Transform parent, Vector3 source, Vector3 target, float width = .18f)
        {
            if (!Finite(source) || !Finite(target) || (target - source).sqrMagnitude < .0001f)
                throw new ArgumentException("Water Gun needs distinct finite world endpoints.");
            var go = new GameObject("Pistolet à O · original pressure jet");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<WaterGunEffect>();
            effect.source = source; effect.target = target;
            effect.radius = float.IsNaN(width) || float.IsInfinity(width) ? .18f : Mathf.Clamp(width, .055f, .8f);
            effect.victimRadius = effect.radius * 2.8f; effect.victimHeight = effect.radius * 5f;
            try { effect.Initialize(); }
            catch { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); throw; }
            return effect;
        }

        public void SetEndpoints(Vector3 source, Vector3 target)
        {
            if (!Finite(source) || !Finite(target) || (target - source).sqrMagnitude < .0001f) return;
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
            shielded=true;shieldContact=point;shieldNormal=outwardNormal.normalized;
            shieldBounds=surface;groundY=floorY+.025f;ImpactEnabled=false;
        }

        Vector3 RunoffPoint(float p, float lateral)
        {
            Vector3 r=shieldBounds.extents;
            r=new Vector3(Mathf.Max(.1f,r.x),Mathf.Max(.1f,r.y),Mathf.Max(.1f,r.z));
            Vector3 c=shieldBounds.center;
            Vector3 n=new Vector3((shieldContact.x-c.x)/r.x,(shieldContact.y-c.y)/r.y,(shieldContact.z-c.z)/r.z).normalized;
            float yaw=Mathf.Atan2(n.z,n.x)+lateral*.18f*Mathf.Sin(p*Mathf.PI);
            float elevation=Mathf.Lerp(Mathf.Asin(Mathf.Clamp(n.y,-1,1)),-1.20f,Mathf.Clamp01(p/.70f));
            Vector3 radial=new Vector3(Mathf.Cos(elevation)*Mathf.Cos(yaw),Mathf.Sin(elevation),Mathf.Cos(elevation)*Mathf.Sin(yaw));
            Vector3 point=c+Vector3.Scale(radial,r)+radial*.028f;
            if(p>.70f)point.y=Mathf.Lerp(point.y,groundY,(p-.70f)/.30f);
            point.y=Mathf.Max(groundY,point.y);return point;
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
                owned[i * 2] = meshes[i];
                var material = reference != null ? new Material(reference) : new Material(shader);
                material.name = "Pistolet à O · " + (i == 0 ? "liquid sheets" : i == 1 ? "moving droplets" : "surface splash");
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
            int count = Sheets * (Segments + 1) * (Across + 1);
            jetVertices = new Vector3[count]; jetNormals = new Vector3[count]; jetColors = new Color[count];
            var uv = new Vector2[count]; var phase = new Vector2[count];
            var triangles = new int[Sheets * Segments * Across * 6]; int index = 0;
            for (int sheet = 0; sheet < Sheets; sheet++)
            {
                int start = sheet * (Segments + 1) * (Across + 1);
                for (int row = 0; row <= Segments; row++) for (int col = 0; col <= Across; col++)
                {
                    int v = start + row * (Across + 1) + col;
                    uv[v] = new Vector2(col / (float)Across, row / (float)Segments * 8f);
                    phase[v] = new Vector2(row / (float)Segments, sheet * 2.71f);
                }
                for (int row = 0; row < Segments; row++) for (int col = 0; col < Across; col++)
                {
                    int a = start + row * (Across + 1) + col;
                    AddQuad(triangles, ref index, a, a + 1, a + Across + 1, a + Across + 2);
                }
            }
            var mesh = new Mesh { name = "Pistolet à O · connected liquid sheets", vertices = jetVertices, normals = jetNormals, uv = uv, uv2 = phase, triangles = triangles };
            mesh.MarkDynamic(); return mesh;
        }

        Mesh CreateDropletMesh()
        {
            int count = StreamDrops + ImpactDrops + MouthDrops;
            dropVertices = new Vector3[count * 4]; dropColors = new Color[count * 4];
            var uv = new Vector2[count * 4]; var triangles = new int[count * 6]; int index = 0;
            for (int i = 0; i < count; i++)
            {
                int v = i * 4; uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.up; uv[v + 3] = Vector2.one;
                AddQuad(triangles, ref index, v, v + 1, v + 2, v + 3);
            }
            var mesh = new Mesh { name = "Pistolet à O · finite liquid spray", vertices = dropVertices, uv = uv, triangles = triangles };
            mesh.MarkDynamic(); return mesh;
        }

        Mesh CreateSplashMesh()
        {
            int count = Rings * (RingSegments + 1) * 2 + Fans * (FanSegments + 1) * 2;
            splashVertices = new Vector3[count]; splashNormals = new Vector3[count]; splashColors = new Color[count];
            var uv = new Vector2[count]; var triangles = new int[(Rings * RingSegments + Fans * FanSegments) * 6];
            int index = 0, start = 0;
            for (int strip = 0; strip < Rings + Fans; strip++)
            {
                int segments = strip < Rings ? RingSegments : FanSegments;
                for (int row = 0; row <= segments; row++)
                {
                    uv[start + row * 2] = new Vector2(0f, row / (float)segments * (strip < Rings ? 4f : 1f));
                    uv[start + row * 2 + 1] = new Vector2(1f, uv[start + row * 2].y);
                    if (row < segments) AddQuad(triangles, ref index, start + row * 2, start + row * 2 + 1, start + row * 2 + 2, start + row * 2 + 3);
                }
                start += (segments + 1) * 2;
            }
            var mesh = new Mesh { name = "Pistolet à O · splash crown and outgoing sheets", vertices = splashVertices, normals = splashNormals, uv = uv, triangles = triangles };
            mesh.MarkDynamic(); return mesh;
        }

        static void AddQuad(int[] triangles, ref int index, int a, int b, int c, int d)
        { triangles[index++] = a; triangles[index++] = c; triangles[index++] = b; triangles[index++] = b; triangles[index++] = c; triangles[index++] = d; }

        void Update()
        {
            if (!initialized || released) return;
            elapsed = Mathf.Min(TotalDuration, elapsed + Time.deltaTime);
            if (elapsed >= TotalDuration) { ImpactOpacity = ContactOpacity = 0f; RunoffDrops=0; ReleaseResources(); return; }
            UpdateEffect();
        }

        void UpdateEffect()
        {
            float strength = Smooth(.14f, .25f, elapsed) * (1f - Smooth(ReleaseTime, 1.48f, elapsed));
            float charge = Smooth(0f, .07f, elapsed) * (1f - Smooth(.13f, .25f, elapsed));
            float contact = Smooth(HitTime, HitTime + .075f, elapsed) * (1f - Smooth(1.33f, TotalDuration, elapsed));
            ImpactOpacity = ImpactEnabled && !shielded ? contact : 0f;
            ContactOpacity = shielded ? contact : 0f;
            Vector3 firstTarget = shielded ? shieldContact : target;
            Vector3 delta = firstTarget - source; float firstLength = Mathf.Max(.01f, delta.magnitude);
            Vector3 forward = delta.sqrMagnitude > .00001f ? delta.normalized : Vector3.forward;
            float length = firstLength;
            float head = Smooth(.14f, HitTime, elapsed);
            Basis(forward, out Vector3 right, out Vector3 up);
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            Matrix4x4 normalToLocal = transform.localToWorldMatrix.transpose;
            for (int sheet = 0; sheet < Sheets; sheet++)
            {
                float angle = (sheet * 60f + 18f) * Mathf.Deg2Rad;
                int start = sheet * (Segments + 1) * (Across + 1);
                for (int row = 0; row <= Segments; row++)
                {
                    float fraction = row / (float)Segments, p = fraction * head;
                    Vector3 center = JetCenter(p, forward, length, firstLength, out Vector3 tangent);
                    Basis(tangent, out Vector3 rowRight, out Vector3 rowUp);
                    Vector3 across = rowRight * Mathf.Cos(angle) + rowUp * Mathf.Sin(angle);
                    Vector3 depth = Vector3.Cross(tangent, across);
                    float wave = 1f + .13f * Mathf.Sin(p * 49f - elapsed * 30f + sheet) + .06f * Mathf.Sin(p * 93f - elapsed * 41f);
                    float width = radius * (.40f + p * .59f) * wave;
                    float opacity = strength * (1f - Smooth(.96f, 1f, fraction)) * (.65f + .10f * sheet);
                    for (int col = 0; col <= Across; col++)
                    {
                        float x = col / (float)Across * 2f - 1f; int v = start + row * (Across + 1) + col;
                        Vector3 point = center + across * (x * width) + depth * ((1f - x * x) * width * .43f);
                        jetVertices[v] = toLocal.MultiplyPoint3x4(point);
                        jetNormals[v] = normalToLocal.MultiplyVector((depth + across * x * .86f).normalized).normalized;
                        jetColors[v] = new Color(sheet * .317f, 1f, opacity, 1f);
                    }
                }
            }
            jetMesh.vertices = jetVertices; jetMesh.normals = jetNormals; jetMesh.colors = jetColors;

            int active = 0;RunoffDrops=0;
            for (int i = 0; i < StreamDrops; i++)
            {
                float p = Mathf.Repeat((elapsed - .14f) * 2.7f + i * .618034f, 1f);
                float angle = i * 2.399963f + elapsed * 1.6f;
                Vector3 center = JetCenter(p, forward, length, firstLength, out Vector3 tangent);
                Basis(tangent, out Vector3 dropRight, out Vector3 dropUp);
                float spread = radius * (.35f + p * 1.25f);
                center += (dropRight * Mathf.Cos(angle) + dropUp * Mathf.Sin(angle)) * spread;
                center += Vector3.down * p * p * radius * .48f;
                float opacity = p <= head ? strength * Mathf.Sin(p * Mathf.PI) * .83f : 0f;
                SetDrop(i, toLocal.MultiplyPoint3x4(center), radius * (.036f + (i % 5) * .007f), i * .73f, opacity, 1.6f + p);
                if (opacity > .02f) active++;
            }
            Vector3 contactPoint = firstTarget - forward * radius * .08f;
            float splashRadius = Mathf.Min(Mathf.Max(radius * 2.5f, victimRadius * .66f), radius * 5.5f);
            for (int i = 0; i < ImpactDrops; i++)
            {
                float age = Mathf.Repeat(Mathf.Max(0f, elapsed - HitTime) * 2.05f + i * .618034f, 1f);
                float angle = i * 2.399963f;
                Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                Vector3 center;
                if (shielded && i>=24)
                    center=RunoffPoint(age,(i%9-4)/4f);
                else if (shielded)
                {
                    Basis(shieldNormal,out Vector3 sr,out Vector3 su);
                    Vector3 surfaceRadial=sr*Mathf.Cos(angle)+su*Mathf.Sin(angle);
                    center=contactPoint+shieldNormal*age*radius*1.4f+surfaceRadial*age*radius*3f+Vector3.down*age*age*victimHeight*.45f;
                    center.y=Mathf.Max(groundY,center.y);
                }
                else
                    center = contactPoint + radial * age * splashRadius * 1.5f - forward * age * radius * 2f + Vector3.down * age * age * Mathf.Min(victimHeight * .28f, radius * 3.5f);
                float opacity = (ImpactOpacity + ContactOpacity) * Mathf.Sin(age * Mathf.PI) * .94f;
                SetDrop(StreamDrops + i, toLocal.MultiplyPoint3x4(center), radius * (.045f + (i % 4) * .013f), i * .87f, opacity, 1.25f + age * 1.8f);
                if (opacity > .02f) {active++;if(shielded&&i>=24)RunoffDrops++;}
            }
            for (int i = 0; i < MouthDrops; i++)
            {
                float angle = i * 2.399963f + elapsed * 7f;
                Vector3 center = source + forward * radius * .20f + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius * (.15f + charge * .23f);
                float opacity = charge * .9f + strength * .21f;
                SetDrop(StreamDrops + ImpactDrops + i, toLocal.MultiplyPoint3x4(center), radius * .071f, i, opacity, 1.5f);
                if (opacity > .02f) active++;
            }
            PeakDroplets = Mathf.Max(PeakDroplets, active);
            dropMesh.vertices = dropVertices; dropMesh.colors = dropColors;
            UpdateSplash(contactPoint, forward, right, up, splashRadius, toLocal, normalToLocal);
            var bounds = new Bounds(toLocal.MultiplyPoint3x4(source), Vector3.zero);
            bounds.Encapsulate(toLocal.MultiplyPoint3x4(target));
            if (shielded) { bounds.Encapsulate(toLocal.MultiplyPoint3x4(shieldContact));bounds.Encapsulate(toLocal.MultiplyPoint3x4(new Vector3(shieldContact.x,groundY,shieldContact.z))); }
            float padding = Mathf.Max(splashRadius * 2f, radius * 7f);
            Vector3 localPad = Abs(toLocal.MultiplyVector(Vector3.right * padding)) + Abs(toLocal.MultiplyVector(Vector3.up * padding)) + Abs(toLocal.MultiplyVector(Vector3.forward * padding));
            bounds.Expand(localPad * 2f); jetMesh.bounds = dropMesh.bounds = splashMesh.bounds = bounds;
        }

        void UpdateSplash(Vector3 point, Vector3 forward, Vector3 right, Vector3 up, float spread, Matrix4x4 toLocal, Matrix4x4 normalToLocal)
        {
            float energy = ImpactOpacity + ContactOpacity;
            if (shielded) {spread=radius*2.1f;forward=-shieldNormal;Basis(forward,out right,out up);}
            float time = Mathf.Max(0f, elapsed - HitTime); int start = 0;
            for (int ring = 0; ring < Rings; ring++)
            {
                float age = Mathf.Repeat(time * 2.2f + ring * .5f, 1f);
                float ringRadius = spread * (.14f + age * .85f);
                float ringWidth = radius * (.09f + (1f - age) * .20f);
                float opacity = energy * Mathf.Sin(age * Mathf.PI) * .63f;
                for (int row = 0; row <= RingSegments; row++)
                {
                    float angle = row / (float)RingSegments * Mathf.PI * 2f;
                    Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    float crown = .65f + .35f * Mathf.Sin(angle * 7f + ring + time * 10f);
                    for (int col = 0; col < 2; col++)
                    {
                        int v = start + row * 2 + col;
                        Vector3 center = point + radial * (ringRadius + (col - .5f) * ringWidth * 2f)
                            - forward * (radius * .12f + Mathf.Sin(age * Mathf.PI) * radius * crown * (col == 0 ? .25f : 1.05f));
                        splashVertices[v] = toLocal.MultiplyPoint3x4(center);
                        splashNormals[v] = normalToLocal.MultiplyVector((-forward + radial * .55f).normalized).normalized;
                        splashColors[v] = new Color(ring * 2.7f, .85f, opacity, 1f);
                    }
                }
                start += (RingSegments + 1) * 2;
            }
            for (int fan = 0; fan < Fans; fan++)
            {
                float angle = fan * Mathf.PI * 2f / Fans + .12f * Mathf.Sin(time * 10f + fan);
                Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                Vector3 across = Vector3.Cross(forward, radial);
                float pulse = .70f + .30f * Mathf.Sin(time * 17f + fan * 1.7f);
                for (int row = 0; row <= FanSegments; row++)
                {
                    float p = row / (float)FanSegments;
                    Vector3 center = point + radial * (spread * p * pulse)
                        - forward * (Mathf.Sin(p * Mathf.PI * .88f) * radius * 1.65f)
                        + Vector3.down * p * p * radius * .45f;
                    if (shielded) {center=RunoffPoint(p,(fan-(Fans-1)*.5f)/Fans*2f);across=right;}
                    float width = radius * (.07f + p * .20f) * (1f - p * .68f);
                    float opacity = energy * (1f - Smooth(.67f, 1f, p)) * .58f;
                    for (int col = 0; col < 2; col++)
                    {
                        int v = start + row * 2 + col;
                        splashVertices[v] = toLocal.MultiplyPoint3x4(center + across * (col == 0 ? -width : width));
                        splashNormals[v] = normalToLocal.MultiplyVector((-forward + radial * p).normalized).normalized;
                        splashColors[v] = new Color(fan * .71f, .85f, opacity, 1f);
                    }
                }
                start += (FanSegments + 1) * 2;
            }
            splashMesh.vertices = splashVertices; splashMesh.normals = splashNormals; splashMesh.colors = splashColors;
        }

        Vector3 JetCenter(float p, Vector3 forward, float length, float firstLength, out Vector3 tangent)
        {
            float distance = p * length;
            tangent=forward;Vector3 point=source+forward*distance;
            float bend=Mathf.Sin(p*Mathf.PI);
            Basis(tangent, out Vector3 right, out Vector3 up);
            return point + right * Mathf.Sin(distance * 5.8f - elapsed * 18f) * radius * bend * .10f
                + up * Mathf.Sin(distance * 8.3f - elapsed * 21f) * radius * bend * .08f;
        }

        void SetDrop(int index, Vector3 center, float size, float seed, float opacity, float elongation)
        {
            Color parameters = new Color(size, seed, opacity, elongation);
            int start = index * 4; for (int k = 0; k < 4; k++) { dropVertices[start + k] = center; dropColors[start + k] = parameters; }
        }
        static void Basis(Vector3 forward, out Vector3 right, out Vector3 up)
        { right = Vector3.Cross(Mathf.Abs(forward.y) > .95f ? Vector3.forward : Vector3.up, forward).normalized; up = Vector3.Cross(forward, right).normalized; }
        static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
        static bool Finite(Vector3 point) => !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) && !float.IsInfinity(point.sqrMagnitude);

        public bool Validate(out string error)
        {
            error = null;
            if (!initialized || shader == null || !shader.isSupported) error = "Water Gun has no supported initialized shader.";
            else if (!Finite(source) || !Finite(target) || (source - target).sqrMagnitude < .0001f) error = "Water Gun endpoints must be distinct finite world positions.";
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
