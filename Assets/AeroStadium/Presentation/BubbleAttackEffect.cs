using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Original detached soap-film bubbles; each bubble bursts separately on contact.</summary>
    public sealed class BubbleAttackEffect : MonoBehaviour
    {
        const int Capacity = 32, RingSegments = 20, DropsPerBubble = 4;
        struct Bubble
        {
            public Vector3 origin, position, previous, velocity, contact, normal;
            public float born, radius, phase, flight, popAt;
            public bool emitted, popped, shield, secondaryEmitter;
        }
        readonly Bubble[] bubbles = new Bubble[Capacity];
        readonly Mesh[] meshes = new Mesh[2];
        readonly Material[] materials = new Material[2];
        readonly MeshRenderer[] renderers = new MeshRenderer[2];
        Vector3[] bubbleVertices, surfaceVertices;
        Color[] bubbleColors, surfaceColors;
        Vector3 source, secondarySource, target, shieldPoint, shieldNormal, secondaryPoint, secondaryNormal;
        Bounds shieldBounds, victimBounds;
        float elapsed, width, floorY;
        int moveId, count;
        bool initialized, secondary, shield, secondaryShield, hasSurface, hasVictim;
        public bool ImpactEnabled { get; set; } = true;
        public float Duration => moveId == 61 ? 1.95f : 2.40f;
        public float EffectProgress => Mathf.Clamp01(elapsed / Duration);
        public bool ImpactReached { get; private set; }
        public bool Completed => initialized && elapsed >= Duration;
        public bool IsPlaying => initialized && !Completed;
        public float ImpactOpacity { get; private set; }
        public float ContactOpacity { get; private set; }
        public int RunoffDrops { get; private set; }
        public int PeakBubbles { get; private set; }
        public int EmissionCount { get; private set; }
        public int TotalContactPops { get; private set; }
        public int PrimaryShieldPops { get; private set; }
        public int SecondaryShieldPops { get; private set; }
        public int RendererCount => 2;
        public int TriangleCount => Capacity * (2 + RingSegments * 2 + DropsPerBubble * 2);

        public static BubbleAttackEffect Create(Transform parent, Vector3 source, Vector3 target, float width, int moveId)
        {
            if (moveId != 61 && moveId != 145) throw new ArgumentOutOfRangeException(nameof(moveId));
            if (!Finite(source) || !Finite(target) || (source - target).sqrMagnitude < .0001f)
                throw new ArgumentException("Bubbles need finite distinct world endpoints.");
            var go = new GameObject(moveId == 61 ? "Bulles d'O · directed bubble train" : "Écume · expanding bubbles");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<BubbleAttackEffect>();
            effect.source = source; effect.target = target; effect.moveId = moveId;
            effect.width = float.IsNaN(width) || float.IsInfinity(width) ? .18f : Mathf.Clamp(width, .055f, .55f);
            effect.floorY = Mathf.Min(source.y, target.y) - .6f;
            try { effect.Initialize(); }
            catch { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); throw; }
            return effect;
        }
        public void SetEndpoints(Vector3 source, Vector3 target)
        {
            if (!Finite(source) || !Finite(target) || (source - target).sqrMagnitude < .0001f) return;
            this.source = source; this.target = target; // Already emitted bubbles keep their captured origin and velocity.
        }
        public void SetSecondaryEmitter(Vector3 origin) { if (Finite(origin)) { secondarySource = origin; secondary = true; } }
        public void SetVictimBounds(Bounds bounds) { if (Finite(bounds.center) && Finite(bounds.size)) { victimBounds = bounds; hasVictim = true; } }
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
            if (!Finite(bounds.center) || !Finite(bounds.size) || float.IsNaN(floor) || float.IsInfinity(floor)) return;
            shieldBounds = bounds; floorY = floor + .02f; hasSurface = true;
        }
        public void SetShieldContact(Vector3 point, Vector3 normal, Bounds bounds, float floor)
        { SetShieldContact(point, normal); SetShieldSurface(bounds, floor); }
        public void SetSecondaryShieldContact(Vector3 point, Vector3 normal, Bounds bounds, float floor)
        { SetSecondaryShieldContact(point, normal); SetShieldSurface(bounds, floor); }

        void Initialize()
        {
            Material reference = Resources.Load<Material>("Materials/BubbleAttackReference");
            Shader shader = reference != null ? reference.shader : Shader.Find("AeroStadium/Original Bubble Film");
            if (shader == null) throw new InvalidOperationException("Original Bubble Film shader missing.");
            count = moveId == 61 ? 24 : 16;
            meshes[0] = CreateMesh(false); meshes[1] = CreateMesh(true);
            for (int i = 0; i < 2; i++)
            {
                materials[i] = reference != null ? new Material(reference) : new Material(shader);
                materials[i].SetFloat("_Surface", i);
                var go = new GameObject(i == 0 ? "Individual transparent bubbles" : "Local pops and falling water");
                go.transform.SetParent(transform, false); go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                renderers[i] = go.AddComponent<MeshRenderer>(); renderers[i].sharedMaterial = materials[i];
                renderers[i].shadowCastingMode = ShadowCastingMode.Off; renderers[i].receiveShadows = false;
            }
            initialized = true; RenderEffect();
        }
        Mesh CreateMesh(bool surface)
        {
            int ringVerts = (RingSegments + 1) * 2;
            int vertices = surface ? Capacity * (ringVerts + DropsPerBubble * 4) : Capacity * 4;
            var positions = new Vector3[vertices]; var colors = new Color[vertices]; var uv = new Vector2[vertices];
            var triangles = new int[surface ? Capacity * (RingSegments * 6 + DropsPerBubble * 6) : Capacity * 6];
            int ti = 0;
            for (int b = 0; b < Capacity; b++)
            {
                int start = surface ? b * (ringVerts + DropsPerBubble * 4) : b * 4;
                if (surface)
                {
                    for (int j = 0; j <= RingSegments; j++)
                    {
                        uv[start+j*2] = new Vector2(j/(float)RingSegments, 0);
                        uv[start+j*2+1] = new Vector2(j/(float)RingSegments, 1);
                        if (j == RingSegments) continue;
                        int v = start+j*2; AddQuad(triangles, ref ti, v, v+1, v+3, v+2);
                    }
                    start += ringVerts;
                }
                int quads = surface ? DropsPerBubble : 1;
                for (int j = 0; j < quads; j++)
                {
                    int v = start+j*4; uv[v]=Vector2.zero; uv[v+1]=Vector2.right;
                    uv[v+2]=Vector2.one; uv[v+3]=Vector2.up;
                    AddQuad(triangles, ref ti, v, v+1, v+2, v+3);
                }
            }
            var mesh = new Mesh { name = surface ? "Pooled bubble pops" : "Pooled bubble films" };
            mesh.MarkDynamic(); mesh.vertices = positions; mesh.colors = colors; mesh.uv = uv; mesh.triangles = triangles;
            if (surface) { surfaceVertices=positions; surfaceColors=colors; }
            else { bubbleVertices=positions; bubbleColors=colors; }
            return mesh;
        }
        static void AddQuad(int[] triangles, ref int i, int a, int b, int c, int d)
        { triangles[i++]=a; triangles[i++]=b; triangles[i++]=c; triangles[i++]=a; triangles[i++]=c; triangles[i++]=d; }
        void Update() { if (IsPlaying) { elapsed = Mathf.Min(Duration, elapsed + Time.deltaTime); RenderEffect(); } }
        void Emit(int i, ref Bubble b)
        {
            b.emitted = true; b.born = .12f + i * (moveId == 61 ? .024f : .052f);
            b.phase = i * 2.399963f; b.secondaryEmitter = secondary && (i & 1) != 0;
            b.origin = b.secondaryEmitter ? secondarySource : source; EmissionCount++;
            Vector3 axis = (target - b.origin).normalized; Basis(axis, out Vector3 side, out Vector3 up);
            float cone = moveId == 61 ? .52f : 1.22f;
            float radial = width * cone * Mathf.Sqrt((i % 7 + 1) / 7f);
            Vector3 aim = target + (side * Mathf.Cos(b.phase) + up * Mathf.Sin(b.phase)) * radial;
            b.flight = moveId == 61 ? .48f + .07f * Hash(i+5) : .68f + .13f * Hash(i+5);
            b.velocity = (aim - b.origin) / b.flight;
            b.radius = width * (moveId == 61 ? .38f + .22f * Hash(i+1) : .64f + .37f * Hash(i+1));
            b.position = b.previous = b.origin;
        }
        void RenderEffect()
        {
            ImpactOpacity = ContactOpacity = 0; int live = 0; RunoffDrops = 0;
            for (int i = 0; i < Capacity; i++)
            {
                Bubble b = bubbles[i]; float born = .12f + i * (moveId == 61 ? .024f : .052f);
                if (i < count && !b.emitted && elapsed >= born) Emit(i, ref b);
                float opacity = 0, radius = 0;
                if (b.emitted && !b.popped)
                {
                    float age = elapsed - b.born;
                    b.previous = b.position;
                    Vector3 axis = b.velocity.normalized; Basis(axis, out Vector3 side, out Vector3 up);
                    float flutter = Mathf.Sin(age * 11f + b.phase) * width * .055f * Mathf.Sin(Mathf.PI*Mathf.Clamp01(age/b.flight));
                    b.position = b.origin + b.velocity * age + up * flutter;
                    if (TryContact(i, b, out Vector3 contact, out Vector3 normal)) Pop(ref b, contact, normal, shield);
                    else if (age >= b.flight + (moveId == 61 ? .035f : .16f)) Pop(ref b, b.position, -axis, false, false);
                    else
                    {
                        radius = b.radius * (1f + age * (moveId == 61 ? .10f : .32f));
                        opacity = Mathf.Clamp01(age / .035f) * .88f; live++;
                    }
                }
                SetBillboard(bubbleVertices, bubbleColors, i*4, b.position, radius, opacity, b.phase);
                RenderPop(i, b); bubbles[i] = b;
            }
            PeakBubbles = Mathf.Max(PeakBubbles, live);
            Upload(meshes[0], bubbleVertices, bubbleColors); Upload(meshes[1], surfaceVertices, surfaceColors);
        }
        bool TryContact(int i, Bubble b, out Vector3 contact, out Vector3 normal)
        {
            contact = b.position; normal = -b.velocity.normalized;
            if (!shield && !ImpactEnabled) return false;
            if ((shield && hasSurface) || (!shield && hasVictim))
            {
                Bounds bounds = shield ? shieldBounds : victimBounds;
                Vector3 r = bounds.extents + Vector3.one * b.radius * .65f;
                r = Vector3.Max(r, Vector3.one * .08f);
                Vector3 segment = b.position - b.previous, o = b.previous - bounds.center;
                Vector3 q = new Vector3(o.x/r.x,o.y/r.y,o.z/r.z), d = new Vector3(segment.x/r.x,segment.y/r.y,segment.z/r.z);
                float a = Vector3.Dot(d,d), c = Vector3.Dot(q,q)-1f, h = Vector3.Dot(q,d), disc = h*h-a*c;
                if (a < .0000001f || disc < 0 || c < 0) return false;
                float t = (-h-Mathf.Sqrt(disc))/a; if (t < 0 || t > 1) return false;
                contact = b.previous + segment*t; Vector3 n = contact-bounds.center;
                normal = new Vector3(n.x/(r.x*r.x),n.y/(r.y*r.y),n.z/(r.z*r.z)).normalized;
                contact -= normal * b.radius * .50f; return true;
            }
            Vector3 point = shield ? secondaryShield && (i&1)!=0 ? secondaryPoint : shieldPoint : target;
            normal = shield ? secondaryShield && (i&1)!=0 ? secondaryNormal : shieldNormal : -b.velocity.normalized;
            float before=Vector3.Dot(b.previous-point,normal)-b.radius*.65f;
            float after=Vector3.Dot(b.position-point,normal)-b.radius*.65f;
            if (before < 0 || after > 0) return false;
            contact = Vector3.Lerp(b.previous,b.position,before/Mathf.Max(.00001f,before-after))-normal*b.radius*.65f;
            return true;
        }
        void Pop(ref Bubble b, Vector3 point, Vector3 normal, bool onShield, bool actualContact=true)
        {
            b.popped=true; b.popAt=elapsed; b.contact=point; b.normal=normal; b.shield=onShield;
            if (actualContact)
            {
                TotalContactPops++;
                if (onShield) { if (b.secondaryEmitter) SecondaryShieldPops++; else PrimaryShieldPops++; }
                if (onShield || ImpactEnabled) ImpactReached=true; // Caller owns the single body reaction.
            }
        }
        void RenderPop(int i, Bubble b)
        {
            int ringVerts=(RingSegments+1)*2, start=i*(ringVerts+DropsPerBubble*4);
            float age=b.popped ? elapsed-b.popAt : 100f;
            float life=moveId==61 ? .13f : .21f, pop=Mathf.Clamp01(1f-age/life);
            Vector3 n=b.normal.sqrMagnitude>.001f ? b.normal : Vector3.up; Basis(n,out Vector3 side,out Vector3 up);
            float radius=b.radius*(.72f+age*5f);
            for (int j=0;j<=RingSegments;j++)
            {
                float angle=j/(float)RingSegments*Mathf.PI*2;
                Vector3 dir=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                for (int edge=0;edge<2;edge++)
                {
                    int v=start+j*2+edge;
                    surfaceVertices[v]=transform.InverseTransformPoint(b.contact+n*.015f+dir*radius*(edge==0?.81f:1f));
                    surfaceColors[v]=new Color(0, b.phase, pop*.68f, 0);
                }
            }
            if (b.shield) ContactOpacity=Mathf.Max(ContactOpacity,pop); else ImpactOpacity=Mathf.Max(ImpactOpacity,pop);
            for (int j=0;j<DropsPerBubble;j++)
            {
                float a=b.phase+j*2.1f, dropAge=age-.018f*j;
                float alpha=dropAge>=0 ? Mathf.Clamp01(1f-dropAge/.62f)*.68f : 0;
                Vector3 velocity=(side*Mathf.Cos(a)+up*Mathf.Sin(a))*(.35f+Hash(i*7+j)*.65f)+n*.32f+Vector3.up*.28f;
                Vector3 p=b.contact+velocity*dropAge+Vector3.down*(2.6f*dropAge*dropAge);
                if (b.shield && hasSurface && j<2 && dropAge>=0)
                {
                    Vector3 r=Vector3.Max(shieldBounds.extents,Vector3.one*.08f);
                    Vector3 c=shieldBounds.center, local=b.contact-c;
                    float yaw=Mathf.Atan2(local.z/r.z,local.x/r.x)+Mathf.Sin(a)*dropAge*.12f;
                    float elevation=Mathf.Lerp(Mathf.Asin(Mathf.Clamp(local.y/r.y,-1f,1f)),-1.35f,Mathf.Clamp01(dropAge/.45f));
                    Vector3 radial=new Vector3(Mathf.Cos(elevation)*Mathf.Cos(yaw),Mathf.Sin(elevation),Mathf.Cos(elevation)*Mathf.Sin(yaw));
                    p=c+Vector3.Scale(radial,r)+radial*.025f;
                    if(dropAge>.45f)p+=Vector3.down*((dropAge-.45f)*2f);
                    if(alpha>.01f)RunoffDrops++;
                }
                if(p.y<floorY)alpha*=Mathf.Clamp01(1f-(floorY-p.y)*10f);
                p.y=Mathf.Max(floorY,p.y);
                SetBillboard(surfaceVertices,surfaceColors,start+ringVerts+j*4,p,b.radius*.10f,alpha,b.phase,true);
            }
        }
        void SetBillboard(Vector3[] vertices, Color[] colors, int start, Vector3 position, float radius, float alpha, float phase, bool drop=false)
        {
            Vector3 local=transform.InverseTransformPoint(position);
            for(int j=0;j<4;j++){vertices[start+j]=local;colors[start+j]=new Color(radius,phase,alpha,drop?1:0);}
        }
        void Upload(Mesh mesh, Vector3[] vertices, Color[] colors)
        {
            mesh.vertices=vertices;mesh.colors=colors;
            Bounds bounds=new Bounds(transform.InverseTransformPoint((source+target)*.5f),Vector3.one*.1f);
            for(int i=0;i<vertices.Length;i++)bounds.Encapsulate(vertices[i]);bounds.Expand(width*4f);mesh.bounds=bounds;
        }
        static void Basis(Vector3 normal,out Vector3 side,out Vector3 up)
        {side=Vector3.Cross(Mathf.Abs(normal.y)>.93f?Vector3.forward:Vector3.up,normal).normalized;up=Vector3.Cross(normal,side).normalized;}
        static float Hash(int i)=>Mathf.Repeat(Mathf.Sin(i*12.9898f+4.14f)*43758.54f,1f);
        static bool Finite(Vector3 v)=>!(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z));
        public bool Validate(out string error)
        {
            error=null;if(!initialized||meshes[0]==null||meshes[1]==null){error="Bubble mesh missing.";return false;}
            if(TriangleCount>=8000){error="Bubble triangle budget exceeded.";return false;}
            foreach(Vector3 v in bubbleVertices)if(!Finite(v)){error="Nonfinite bubble vertex.";return false;}
            foreach(Vector3 v in surfaceVertices)if(!Finite(v)){error="Nonfinite pop vertex.";return false;}
            return true;
        }
        void OnDestroy()
        {
            for(int i=0;i<2;i++)
            {
                if(meshes[i]!=null){if(Application.isPlaying)Destroy(meshes[i]);else DestroyImmediate(meshes[i]);}
                if(materials[i]!=null){if(Application.isPlaying)Destroy(materials[i]);else DestroyImmediate(materials[i]);}
            }
        }
    }
}
