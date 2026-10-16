using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Original living vine whips (22) and captured, spinning leaf blades (75).
    /// Caller owns body poses, sound, damage and the single victim reaction.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class GrassAttackEffect : MonoBehaviour
    {
        const int Leaves = 12, TubeSteps = 48, TubeSides = 9, LeafSteps = 10, Slashes = 3, SlashSteps = 8;
        const int LeafStride = (LeafSteps + 1) * 3, TubeStride = (TubeSteps + 1) * (TubeSides + 1);
        struct Actor
        {
            public Vector3 origin, anchor, position, velocity, acceleration, axis, side, up, contact, normal, ricochet;
            public float born, flight, radius, phase, hitAt, sampledAt;
            public bool emitted, hit, shield, secondary;
        }
        readonly Actor[] actors = new Actor[Leaves];
        readonly Mesh[] meshes = new Mesh[3];
        readonly Material[] materials = new Material[3];
        readonly MeshRenderer[] renderers = new MeshRenderer[3];
        readonly Vector3[][] vertices = new Vector3[3][];
        readonly Vector3[][] normals = new Vector3[3][];
        readonly Color[][] colors = new Color[3][];
        Vector3 source, secondarySource, target;
        Bounds shieldBounds, victimBounds;
        Matrix4x4 toLocal, normalToLocal;
        float elapsed, width, floorY;
        int moveId;
        bool initialized, released, secondary, hasShield, hasVictim;
        Transform liveRoot,liveSecond;BattleActionDirector whipDirector;AnimatedSurfaceContact bodySurface;
        public int BodySurfaceContacts {get;private set;}
        public float RenderedContactGap {get;private set;}
        public int SurfaceBakedFrames=>bodySurface!=null?bodySurface.BakedFrames:0;
        public long SurfaceTriangleTests=>bodySurface!=null?bodySurface.TriangleTests:0;
        public string SurfaceMesh=>bodySurface!=null?bodySurface.LastMeshName:"";
        public float SurfaceError=>bodySurface!=null?bodySurface.LastSurfaceError:0;
        public void BindWhipVictim(GameObject victim){bodySurface?.Dispose();bodySurface=new AnimatedSurfaceContact(victim);}
        public void BindWhipRoots(Transform first,Transform second,BattleActionDirector director){liveRoot=first;liveSecond=second;whipDirector=director;}
        string geometryFailure;

        public bool ImpactEnabled { get; set; } = true;
        public float Duration => moveId == 22 ? 2.50f : 2.15f;
        public float EffectProgress => Mathf.Clamp01(elapsed / Duration);
        public bool ImpactReached { get; private set; }
        public bool Completed => initialized && elapsed >= Duration;
        public bool IsPlaying => initialized && !released && !Completed;
        public bool HasLaunched => EmissionCount > 0;
        public int ExpectedEmissionCount => moveId == 22 ? 2 : Leaves;
        public int EmissionCount { get; private set; }
        public int ActualContacts { get; private set; }
        public int TotalContacts => ActualContacts;
        public int PrimaryEmitterContacts { get; private set; }
        public int SecondaryEmitterContacts { get; private set; }
        public int EmitterContacts => PrimaryEmitterContacts + SecondaryEmitterContacts;
        public int PrimaryShieldContacts { get; private set; }
        public int SecondaryShieldContacts { get; private set; }
        public int ShieldContacts => PrimaryShieldContacts + SecondaryShieldContacts;
        public int BodyContacts => ActualContacts - ShieldContacts;
        public bool TwoSourceContacts => PrimaryEmitterContacts > 0 && SecondaryEmitterContacts > 0;
        public float MaximumAnchorError { get; private set; }
        public bool Anchored => initialized && moveId == 22 && MaximumAnchorError < .002f;
        public bool RetractionComplete { get; private set; }
        public float BackswingDistance { get; private set; }
        public float WhipTipPeakSpeed { get; private set; }
        public float WhipMiddlePeakSpeed { get; private set; }
        public float WhipRecoilDistance { get; private set; }
        public float WhipMinimumLength { get; private set; } = float.MaxValue;
        public float WhipMaximumLength { get; private set; }
        readonly Vector3[] previousTips=new Vector3[2],previousMiddles=new Vector3[2];
        readonly float[] previousWhipTimes=new float[2];
        readonly bool[] whipMeasured=new bool[2];
        public bool DetachedBeforeImpact { get; private set; }
        public bool ProjectileDetached => moveId == 75 && DetachedBeforeImpact;
        public Vector3 ContactPoint { get; private set; }
        public Vector3 ContactNormal { get; private set; }
        public Vector3 ShieldContactPoint => ContactPoint;
        public Vector3 ShieldContactNormal => ContactNormal;
        public Vector3 PrimaryTrajectoryOrigin { get; private set; }
        public Vector3 SecondaryTrajectoryOrigin { get; private set; }
        public float ImpactOpacity { get; private set; }
        public float ContactOpacity { get; private set; }
        public int PeakParticles { get; private set; }
        public int RendererCount => 3;
        public int TriangleCount => 2 * TubeSteps * TubeSides * 2 + Leaves * 4 * LeafSteps * 4 + Leaves * Slashes * SlashSteps * 2;

        public static GrassAttackEffect Create(Transform parent, Vector3 source, Vector3 target, float width, int moveId)
        {
            if (moveId != 22 && moveId != 75) throw new ArgumentOutOfRangeException(nameof(moveId));
            if (!Finite(source) || !Finite(target) || (source - target).sqrMagnitude < .0001f)
                throw new ArgumentException("Grass effects need finite, distinct world endpoints.");
            var go = new GameObject(moveId == 22 ? "Fouet Lianes · rooted twin whips" : "Tranch'Herbe · curved leaf blades");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<GrassAttackEffect>();
            effect.source = source; effect.target = target; effect.moveId = moveId;
            effect.width = Finite(width) ? Mathf.Clamp(width, .075f, .55f) : .18f;
            effect.floorY = Mathf.Min(source.y, target.y) - .6f;
            try { effect.Initialize(); }
            catch { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); throw; }
            return effect;
        }
        public void SetEndpoints(Vector3 origin, Vector3 aim)
        {
            if (!Finite(origin) || !Finite(aim) || (origin - aim).sqrMagnitude < .0001f) return;
            source = origin; target = aim;
        }
        public void SetSecondaryEmitter(Vector3 origin)
        { if (Finite(origin)) { secondarySource = origin; secondary = true; } }
        public void SetVictimBounds(Bounds bounds)
        { if (ValidBounds(bounds)) { victimBounds = bounds; hasVictim = true; } }
        public void SetShieldSurface(Bounds bounds, float floor)
        { if (ValidBounds(bounds) && Finite(floor)) { shieldBounds = bounds; hasShield = true; floorY = floor + .012f; } }
        public void SetFloor(float floor) { if (Finite(floor)) floorY = floor + .012f; }
        public Vector3 GetEmissionOrigin(int i) => i >= 0 && i < ExpectedEmissionCount && actors[i].emitted ? actors[i].origin : Vector3.zero;
        public bool IsSecondaryEmission(int i) => i >= 0 && i < ExpectedEmissionCount && actors[i].emitted && actors[i].secondary;

        void Initialize()
        {
            Material reference = Resources.Load<Material>("Materials/GrassAttackReference");
            Shader shader = reference != null ? reference.shader : Shader.Find("AeroStadium/Original Living Grass");
            if (shader == null) throw new InvalidOperationException("Original Living Grass shader missing.");
            for (int i = 0; i < 3; i++)
            {
                meshes[i] = CreateMesh(i);
                materials[i] = reference != null ? new Material(reference) : new Material(shader);
                materials[i].name = "Original grass · " + i; materials[i].SetFloat("_Mode", i);
                var go = new GameObject(i == 0 ? "Two continuous rooted vines" : i == 1 ? "Lobed blades and finite leaf fragments" : "Local cutting glints");
                go.transform.SetParent(transform, false); go.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                renderers[i] = go.AddComponent<MeshRenderer>(); renderers[i].sharedMaterial = materials[i];
                renderers[i].shadowCastingMode = ShadowCastingMode.Off; renderers[i].receiveShadows = false;
            }
            initialized = true; RenderEffect();
        }
        Mesh CreateMesh(int kind)
        {
            int count = kind == 0 ? 2 : kind == 1 ? Leaves * 4 : Leaves * Slashes;
            int steps = kind == 0 ? TubeSteps : kind == 1 ? LeafSteps : SlashSteps;
            int columns = kind == 0 ? TubeSides + 1 : kind == 1 ? 3 : 2;
            int stride = (steps + 1) * columns, ti = 0;
            var v = new Vector3[count * stride]; var n = new Vector3[v.Length]; var c = new Color[v.Length];
            var uv = new Vector2[v.Length]; var triangles = new int[count * steps * (columns - 1) * 6];
            for (int a = 0; a < count; a++) for (int s = 0; s <= steps; s++) for (int col = 0; col < columns; col++)
            {
                int at = a * stride + s * columns + col;
                uv[at] = kind == 0 ? new Vector2(col / (float)(columns - 1), s / (float)steps) : new Vector2(s / (float)steps, col / (float)(columns - 1));
                if (s == steps || col == columns - 1) continue;
                triangles[ti++] = at; triangles[ti++] = at + columns; triangles[ti++] = at + columns + 1;
                triangles[ti++] = at; triangles[ti++] = at + columns + 1; triangles[ti++] = at + 1;
            }
            vertices[kind] = v; normals[kind] = n; colors[kind] = c;
            var mesh = new Mesh { name = kind == 0 ? "Continuous tapered vine tubes" : kind == 1 ? "Curved lobed leaf geometry" : "Short cutting contact arcs" };
            mesh.MarkDynamic(); mesh.vertices = v; mesh.normals = n; mesh.colors = c; mesh.uv = uv; mesh.triangles = triangles;
            return mesh;
        }
        void Update(){if(moveId!=22)Advance();}
        void LateUpdate()
        {
            if(moveId!=22)return;
            if(liveRoot!=null)source=liveRoot.position;if(liveSecond!=null)secondarySource=liveSecond.position;
            Advance();if(whipDirector!=null)whipDirector.SetProgress(EffectProgress);
        }
        void Advance()
        {
            if (!IsPlaying) return;
            elapsed = Mathf.Min(Duration, elapsed + Time.deltaTime); RenderEffect();
            if (Completed) for (int i = 0; i < 3; i++) renderers[i].enabled = false;
        }
        float Birth(int i) => moveId == 22 ? .025f : .14f + i * .026f;
        void Emit(int i, ref Actor p)
        {
            p.emitted = true; p.born = Birth(i); p.sampledAt = p.born;
            p.secondary = secondary && (i & 1) != 0; p.origin = p.secondary ? secondarySource : source;
            p.position = p.origin; p.axis = (target - p.origin).normalized;
            Basis(p.axis, out p.side, out p.up); p.phase = i * 2.399963f;
            p.radius = width * (moveId == 22 ? .32f : .66f);
            p.flight = .48f + Hash(i + 4) * .09f;
            Vector3 aim = target + p.side * width * ((i % 3) - 1) * .32f + p.up * width * (Hash(i + 7) - .5f) * .45f;
            p.acceleration = -p.up * width * 2.2f;
            p.velocity = (aim - p.origin - p.acceleration * (.5f * p.flight * p.flight)) / p.flight;
            if (p.secondary && SecondaryTrajectoryOrigin == Vector3.zero) SecondaryTrajectoryOrigin = p.origin;
            if (!p.secondary && EmissionCount == 0) PrimaryTrajectoryOrigin = p.origin;
            EmissionCount++;
        }
        void RenderEffect()
        {
            toLocal = transform.worldToLocalMatrix; normalToLocal = transform.localToWorldMatrix.transpose;
            ImpactOpacity = ContactOpacity = 0; int live = 0;
            for (int m = 0; m < 3; m++)
            {
                Array.Clear(colors[m], 0, colors[m].Length);
                materials[m].SetFloat("_ClipShield", hasShield ? 1 : 0);
                materials[m].SetVector("_ShieldCenter", shieldBounds.center);
                materials[m].SetVector("_ShieldRadii", SafeRadii(shieldBounds.extents));
            }
            for (int i = 0; i < ExpectedEmissionCount; i++)
            {
                Actor p = actors[i]; if (!p.emitted && elapsed >= Birth(i)) Emit(i, ref p);
                if (!p.emitted) continue;
                if (moveId == 22) RenderVine(i, ref p); else RenderBlade(i, ref p);
                if (p.hit) RenderContact(i, p);
                if (moveId == 22 ? elapsed < 2.45f : elapsed < p.born + p.flight + .65f) live++;
                actors[i] = p;
            }
            PeakParticles = Mathf.Max(PeakParticles, live);
            for (int m = 0; m < 3; m++)
            {
                meshes[m].vertices = vertices[m]; meshes[m].normals = normals[m]; meshes[m].colors = colors[m];
                meshes[m].RecalculateBounds();
            }
            CheckGeometry();
        }
        // A full-length planar loop is raised before the lash. The bend travels from
        // shoulder to tip; it straightens existing slack rather than emitting a beam.
        float WhipTime(int i,float time)=>time-i*.045f;
        Vector3 WindCurve(Vector3 root,Actor p,int i,float s)
        {
            float range=Vector3.Distance(root,target),sign=i==0?-1:1;
            Vector3 outward=p.side*sign;
            Vector3 c1=root-p.axis*range*.15f+p.up*range*.60f+outward*range*.35f;
            Vector3 c2=root-p.axis*range*.20f+p.up*range*.40f+outward*range*.65f;
            Vector3 end=root-p.axis*range*.22f+p.up*range*.16f+outward*range*.28f;
            return Bezier(root,c1,c2,end,s);
        }
        Vector3 LashCurve(Vector3 root,Vector3 tip,Actor p,int i,float s,float time)
        {
            float range=Vector3.Distance(root,tip),sign=i==0?-1:1;
            float wave=Mathf.Clamp01((time-.60f)/.62f),spread=Mathf.Lerp(.24f,.10f,wave);
            float crest=Mathf.Exp(-Mathf.Pow((s-wave)/spread,2))*Mathf.Sin(Mathf.PI*s);
            Vector3 c1=root+p.axis*range*.28f+p.up*range*.28f+p.side*sign*range*.17f;
            Vector3 c2=Vector3.Lerp(root,tip,.72f)+p.up*range*.10f+p.side*sign*range*.10f;
            return Bezier(root,c1,c2,tip,s)+(p.up+p.side*sign*.45f)*crest*range*.16f*(1-wave*.7f);
        }
        Vector3 WhipPoint(Vector3 root,Actor p,int i,float s,float time)
        {
            float local=WhipTime(i,time),range=Vector3.Distance(root,target);
            Vector3 wind=WindCurve(root,p,i,s);
            float reveal=Smooth(.03f,.32f,local),swing=Smooth(.55f+s*.40f,.91f+s*.28f,local);
            Vector3 tip=target+p.side*(i==0?-1:1)*width*.35f;
            Vector3 point=Vector3.Lerp(wind,LashCurve(root,tip,p,i,s,local),swing);
            if(p.hit)
            {
                float recoil=Smooth(p.hitAt+.025f,p.hitAt+.225f,time);
                Vector3 tangent=Vector3.ProjectOnPlane(p.side*(i==0?-1:1)+p.up*.3f,p.normal).normalized;
                Vector3 desired=p.contact+p.normal*(p.radius*.035f+recoil*range*.065f)+tangent*Mathf.Sin(recoil*Mathf.PI)*range*.08f;
                float tipPhase=Smooth(.95f,1.19f,local);
                Vector3 rawEnd=Vector3.Lerp(WindCurve(root,p,i,1),tip,tipPhase);
                point+=(desired-rawEnd)*(s*s*(3-2*s));
            }
            float gather=Smooth(1.48f+(1-s)*.10f,1.96f+(1-s)*.10f,local);
            point=Vector3.Lerp(point,wind,gather);
            float retract=1-Smooth(1.98f,2.40f,local);
            return root+(point-root)*(reveal*retract);
        }
        static Vector3 Bezier(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float t)
        {float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        void RenderVine(int i,ref Actor p)
        {
            Vector3 root=p.secondary?secondarySource:source;p.anchor=root;
            // Sub-frame sweeps catch the short crack even when a render frame is slow.
            float at=p.sampledAt,end=Mathf.Min(elapsed,1.32f+i*.045f);
            while(!p.hit&&at<end-.000001f)
            {
                float next=Mathf.Min(end,at+1f/180f);
                Vector3 before=WhipPoint(root,p,i,1,at),after=WhipPoint(root,p,i,1,next);
                if(at>=.95f&&TryContact(before,after,p.radius*.10f,out Vector3 contact,out Vector3 normal,out float fraction))
                    Hit(ref p,contact,normal,Mathf.Lerp(at,next,fraction));
                at=next;
            }
            p.sampledAt=Mathf.Max(p.sampledAt,end);p.position=WhipPoint(root,p,i,1,elapsed);
            if(p.hit&&!p.shield&&elapsed-p.hitAt<.025f)RenderedContactGap=Mathf.Max(RenderedContactGap,Vector3.Distance(p.position,p.contact));
            float dt=elapsed-previousWhipTimes[i];Vector3 middle=WhipPoint(root,p,i,.55f,elapsed);
            if(whipMeasured[i]&&dt>.00001f)
            {
                if(!p.hit&&elapsed>.32f){WhipTipPeakSpeed=Mathf.Max(WhipTipPeakSpeed,Vector3.Distance(p.position,previousTips[i])/dt);WhipMiddlePeakSpeed=Mathf.Max(WhipMiddlePeakSpeed,Vector3.Distance(middle,previousMiddles[i])/dt);}
            }
            previousTips[i]=p.position;previousMiddles[i]=middle;previousWhipTimes[i]=elapsed;whipMeasured[i]=true;
            if(elapsed>.3f&&elapsed<.9f)BackswingDistance=Mathf.Max(BackswingDistance,-Vector3.Dot(p.position-root,p.axis));
            if(p.hit&&elapsed-p.hitAt<.28f)WhipRecoilDistance=Mathf.Max(WhipRecoilDistance,Vector3.Dot(p.position-p.contact,p.normal));
            float alpha=Smooth(.03f,.12f,WhipTime(i,elapsed))*(1-Smooth(2.34f,2.40f,WhipTime(i,elapsed)));
            Vector3 previous=root;float length=0;
            for(int j=0;j<=TubeSteps;j++)
            {
                float t=j/(float)TubeSteps;Vector3 center=WhipPoint(root,p,i,t,elapsed);
                Vector3 tangent=(WhipPoint(root,p,i,Mathf.Min(1,t+.008f),elapsed)-WhipPoint(root,p,i,Mathf.Max(0,t-.008f),elapsed)).normalized;
                if(tangent.sqrMagnitude<.01f)tangent=p.axis;
                Basis(tangent,out Vector3 side,out Vector3 up);
                length+=Vector3.Distance(center,previous);previous=center;
                float radius=p.radius*Mathf.Lerp(1,.035f,Mathf.Pow(t,1.55f))*(1-Smooth(2.30f,2.40f,WhipTime(i,elapsed)));
                for(int k=0;k<=TubeSides;k++)
                {
                    float angle=k*Mathf.PI*2/TubeSides;Vector3 radial=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                    Vector3 point=center+radial*radius;if(hasShield)point=OutsideShield(point,.006f);
                    Put(0,i*TubeStride+j*(TubeSides+1)+k,point,radial,new Color(.16f+t*.12f,.38f+t*.20f,.055f,alpha));
                }
            }
            if(elapsed>.34f&&elapsed<1.24f){WhipMinimumLength=Mathf.Min(WhipMinimumLength,length);WhipMaximumLength=Mathf.Max(WhipMaximumLength,length);}
            Vector3 anchorCenter=Vector3.zero;
            for(int k=0;k<TubeSides;k++)anchorCenter+=transform.TransformPoint(vertices[0][i*TubeStride+k]);
            MaximumAnchorError=Mathf.Max(MaximumAnchorError,Vector3.Distance(root,anchorCenter/TubeSides));
            if(elapsed>=2.45f)
            {
                bool retracted=true;for(int v=0;v<TubeStride;v++)if(Vector3.Distance(transform.TransformPoint(vertices[0][i*TubeStride+v]),root)>.002f)retracted=false;
                RetractionComplete=i==0?retracted:RetractionComplete&&retracted;
            }
        }
        Vector3 BladePosition(Actor p, float age)
        {
            float u = Mathf.Clamp01(age / p.flight);
            float fan = Mathf.Sin(Mathf.PI * u) * width * (1.1f + .5f * Hash((int)(p.phase * 8) + 2));
            return p.origin + p.velocity * age + p.acceleration * (.5f * age * age) +
                (p.side * Mathf.Cos(p.phase) + p.up * Mathf.Sin(p.phase) * .6f) * fan;
        }
        void RenderBlade(int i, ref Actor p)
        {
            float age = elapsed - p.born;
            if (!p.hit)
            {
                // Fixed-sized analytic sweep segments also cover a slow frame or an emission caught up late.
                float end = Mathf.Min(elapsed, p.born + p.flight + .12f), at = p.sampledAt;
                for (int step = 0; step < 256 && at < end - .000001f && !p.hit; step++)
                {
                    float next = Mathf.Min(end, at + 1f / 90f);
                    Vector3 nextPosition = BladePosition(p, next - p.born);
                    if (TryContact(p.position, nextPosition, p.radius, out Vector3 contact, out Vector3 normal, out float fraction))
                        Hit(ref p, contact, normal, Mathf.Lerp(at, next, fraction));
                    else p.position = nextPosition;
                    at = next;
                }
                p.sampledAt = end;
            }
            float opacity = Smooth(0, .035f, age) * (1 - Smooth(p.flight + .035f, p.flight + .16f, age));
            if (!p.hit && Vector3.Distance(p.position, p.origin) > p.radius * 2) DetachedBeforeImpact = true;
            Vector3 direction = p.velocity + p.acceleration * age;
            if (p.hit)
            {
                float hitAge = Mathf.Max(0, elapsed - p.hitAt);
                opacity = p.shield ? 1 - Smooth(.28f, .68f, hitAge) : 1 - Smooth(0, .055f, hitAge);
                if (p.shield)
                {
                    float drift = (1 - Mathf.Exp(-3.2f * hitAge)) / 3.2f;
                    Vector3 position = p.contact + p.normal * (p.radius + .014f) + p.ricochet * drift + Vector3.down * hitAge * hitAge * .85f;
                    float front = Vector3.Dot(position - p.contact, p.normal);
                    if (front < p.radius) position += p.normal * (p.radius - front);
                    p.position = OutsideShield(position, p.radius + .006f);
                    p.position.y = Mathf.Max(floorY + p.radius * .3f, p.position.y);
                    direction = p.ricochet * Mathf.Exp(-3.2f * hitAge) + Vector3.down * hitAge * 1.7f;
                }
            }
            RenderLeaf(i, p.position, direction, p.side, p.radius, p.phase + age * 19, opacity, i);
        }
        void RenderLeaf(int slot, Vector3 center, Vector3 direction, Vector3 fallbackSide, float radius, float spin, float opacity, int seed)
        {
            Vector3 axis = direction.sqrMagnitude > .0001f ? direction.normalized : Vector3.up;
            Vector3 side = Vector3.Cross(axis, Vector3.up).normalized;
            if (side.sqrMagnitude < .01f) side = fallbackSide;
            Vector3 up = Vector3.Cross(side, axis).normalized;
            Vector3 cross = side * Mathf.Cos(spin) + up * Mathf.Sin(spin);
            Vector3 normal = Vector3.Cross(axis, cross).normalized;
            Color color = new Color(.15f + Hash(seed + 3) * .13f, .48f + Hash(seed + 8) * .20f, .045f + Hash(seed + 7) * .08f, opacity);
            for (int j = 0; j <= LeafSteps; j++)
            {
                float t = j / (float)LeafSteps;
                float envelope = j == 0 || j == LeafSteps ? 0 : Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * Mathf.PI)), .85f);
                float lobes = 1 + .15f * Mathf.Sin(t * Mathf.PI * 6);
                Vector3 spine = center + axis * ((t - .5f) * radius * 2) + normal * Mathf.Sin(t * Mathf.PI) * radius * .16f;
                for (int k = 0; k < 3; k++)
                {
                    Vector3 point = spine + cross * ((k - 1) * radius * .41f * envelope * lobes);
                    if (k != 1) point -= normal * envelope * radius * .12f;
                    if (hasShield) point = OutsideShield(point, .006f);
                    Put(1, slot * LeafStride + j * 3 + k, point, normal, color);
                }
            }
        }
        bool TryContact(Vector3 previous, Vector3 next, float support, out Vector3 point, out Vector3 normal, out float fraction)
        {
            point = next; normal = Vector3.up; fraction = 0;
            if (!hasShield && (!ImpactEnabled || !hasVictim)) return false;
            if(moveId==22&&!hasShield)
            {
                if(bodySurface==null||!bodySurface.TrySegment(previous,next,out point,out normal))return false;
                float length=Vector3.Distance(previous,next);fraction=length>.000001f?Mathf.Clamp01(Vector3.Distance(previous,point)/length):0;
                return true;
            }
            Bounds bounds = hasShield ? shieldBounds : victimBounds;
            Vector3 radii = SafeRadii(bounds.extents), expanded = radii + Vector3.one * support;
            Vector3 q = Divide(previous - bounds.center, expanded), d = Divide(next - previous, expanded);
            float a = Vector3.Dot(d, d), b = Vector3.Dot(q, d), c = Vector3.Dot(q, q) - 1;
            if (c < 0)
            {
                // A moving shell may catch a live blade, but cannot count an outgoing fragment.
                if (Vector3.Dot(next - previous, previous - bounds.center) >= 0) return false;
            }
            else
            {
                float discriminant = b * b - a * c;
                if (a < .0000001f || discriminant < 0) return false;
                fraction = (-b - Mathf.Sqrt(discriminant)) / a;
                if (fraction < 0 || fraction > 1) return false;
            }
            Vector3 radial = Divide(Vector3.Lerp(previous, next, fraction) - bounds.center, radii).normalized;
            point = bounds.center + Vector3.Scale(radial, radii); normal = Divide(radial, radii).normalized;
            return true;
        }
        void Hit(ref Actor p, Vector3 point, Vector3 normal, float time)
        {
            p.hit = true; p.shield = hasShield; p.contact = point; p.normal = normal; p.hitAt = moveId == 22 ? elapsed : time;
            if (moveId == 75 && Vector3.Distance(p.origin, point) > p.radius * 2) DetachedBeforeImpact = true;
            p.position = point + normal * (p.radius + .014f);
            Vector3 incoming = p.velocity + p.acceleration * Mathf.Max(0, time - p.born);
            Vector3 reflected = Vector3.Reflect(incoming, normal);
            p.ricochet = Vector3.ProjectOnPlane(reflected, normal) * .31f + normal * Mathf.Max(width * 2, Vector3.Dot(reflected, normal) * .20f);
            ActualContacts++;if(moveId==22&&!hasShield)BodySurfaceContacts++; if (p.secondary) SecondaryEmitterContacts++; else PrimaryEmitterContacts++;
            if (p.shield) { if (p.secondary) SecondaryShieldContacts++; else PrimaryShieldContacts++; }
            ImpactReached = true; ContactPoint = point; ContactNormal = normal;
        }
        void RenderContact(int i, Actor p)
        {
            float age = Mathf.Max(0, elapsed - p.hitAt), flash = (1 - Smooth(.025f, .16f, age)) * Smooth(0, .012f, age);
            ContactOpacity = Mathf.Max(ContactOpacity, flash); if (!p.shield) ImpactOpacity = Mathf.Max(ImpactOpacity, flash);
            Basis(p.normal, out Vector3 side, out Vector3 up);
            for (int s = 0; s < Slashes; s++)
            {
                float angle = s * 2.094f + p.phase;
                Vector3 tangent = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                for (int j = 0; j <= SlashSteps; j++)
                {
                    float t = j / (float)SlashSteps;
                    Vector3 center = p.contact + p.normal * .024f + tangent * (t - .5f) * width * (1.8f + age * 6) +
                        up * Mathf.Sin(t * Mathf.PI) * width * .2f;
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 point = center + Vector3.Cross(p.normal, tangent) * ((k * 2 - 1) * width * .045f * Mathf.Sin(t * Mathf.PI));
                        if (p.shield) point = OutsideShield(point, .012f);
                        Put(2, ((i * Slashes + s) * (SlashSteps + 1) + j) * 2 + k, point, p.normal, new Color(.62f, .94f, .12f, flash));
                    }
                }
                if (moveId != 75 || p.shield) continue;
                float fragmentOpacity = (1 - Smooth(.15f, .48f, age)) * Smooth(0, .025f, age);
                Vector3 fragment = p.contact + p.normal * (.025f + age * width * 1.3f) + tangent * age * width * (2.1f + s * .7f) + Vector3.down * age * age * 1.8f;
                fragment.y = Mathf.Max(floorY + width * .08f, fragment.y);
                RenderLeaf(Leaves + i * 3 + s, fragment, tangent + p.normal * .45f + Vector3.down * age, side,
                    width * (.20f + .04f * s), p.phase + age * (11 + s * 4), fragmentOpacity, i * 3 + s);
            }
        }
        Vector3 OutsideShield(Vector3 point, float margin)
        {
            Vector3 radii = SafeRadii(shieldBounds.extents), q = Divide(point - shieldBounds.center, radii + Vector3.one * margin);
            if (q.sqrMagnitude >= 1) return point;
            if (q.sqrMagnitude < .000001f) q = (source - shieldBounds.center).normalized;
            return shieldBounds.center + Vector3.Scale(q.normalized, radii + Vector3.one * margin);
        }
        void Put(int mesh, int index, Vector3 point, Vector3 normal, Color color)
        { vertices[mesh][index] = toLocal.MultiplyPoint3x4(point); normals[mesh][index] = normalToLocal.MultiplyVector(normal).normalized; colors[mesh][index] = color; }
        static void Basis(Vector3 direction, out Vector3 side, out Vector3 up)
        { side = Vector3.Cross(direction, Mathf.Abs(direction.y) > .92f ? Vector3.forward : Vector3.up).normalized; up = Vector3.Cross(side, direction).normalized; }
        static float Smooth(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f + 31.7f) * 43758.5453f, 1);
        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
        static Vector3 SafeRadii(Vector3 r) => new Vector3(Mathf.Max(.02f, r.x), Mathf.Max(.02f, r.y), Mathf.Max(.02f, r.z));
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool ValidBounds(Bounds b) => Finite(b.center) && Finite(b.size) && b.size.x > .001f && b.size.y > .001f && b.size.z > .001f;
        void CheckGeometry()
        {
            if (geometryFailure != null) return;
            for (int m = 0; m < 3; m++) for (int v = 0; v < vertices[m].Length; v++)
            {
                if (!Finite(vertices[m][v]) || !Finite(normals[m][v])) { geometryFailure = "Nonfinite grass geometry during playback."; return; }
                if (!hasShield || colors[m][v].a < .001f) continue;
                Vector3 q = Divide(transform.TransformPoint(vertices[m][v]) - shieldBounds.center, SafeRadii(shieldBounds.extents));
                if (q.sqrMagnitude < .9998f) { geometryFailure = "Visible grass vertex entered the shield during playback."; return; }
            }
            if (moveId == 22 && MaximumAnchorError >= .002f) geometryFailure = "A vine disconnected from its root during playback.";
        }
        public bool Validate(out string error)
        {
            error = null;
            if (!initialized || released) { error = "Grass effect is not initialized."; return false; }
            if (geometryFailure != null) { error = geometryFailure; return false; }
            if (TriangleCount > 5000) { error = "Grass geometry budget exceeded."; return false; }
            for (int m = 0; m < 3; m++)
            {
                if (meshes[m] == null || materials[m] == null) { error = "Grass effect resource missing."; return false; }
                for (int v = 0; v < vertices[m].Length; v++)
                    if (!Finite(vertices[m][v]) || !Finite(normals[m][v])) { error = "Nonfinite grass geometry."; return false; }
            }
            if (EmissionCount > ExpectedEmissionCount || ActualContacts > EmissionCount || EmitterContacts != ActualContacts || ShieldContacts > ActualContacts)
            { error = "Grass contact accounting mismatch."; return false; }
            for (int i = 0; i < ExpectedEmissionCount; i++) if (actors[i].emitted && (!Finite(actors[i].origin) || !Finite(actors[i].position)))
            { error = "Nonfinite captured grass trajectory."; return false; }
            return true;
        }
        void OnDestroy()
        {
            if (released) return; released = true;bodySurface?.Dispose();bodySurface=null;
            for (int i = 0; i < 3; i++)
            {
                if (meshes[i] != null) { if (Application.isPlaying) Destroy(meshes[i]); else DestroyImmediate(meshes[i]); }
                if (materials[i] != null) { if (Application.isPlaying) Destroy(materials[i]); else DestroyImmediate(materials[i]); }
            }
        }
    }
}
