using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>A finite nature orb; its outer core radius sweeps actual current-frame body and shield triangles. Leaves and trails are decorative.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class EnergyBallEffect : MonoBehaviour
    {
        public const float Duration=2.1f, ReleaseTime=.62f;
        readonly Mesh[] meshes=new Mesh[3];readonly Material[] materials=new Material[3];
        readonly List<Vector3>[] vertices={new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
        readonly List<Color>[] colors={new List<Color>(),new List<Color>(),new List<Color>()};
        readonly List<Vector2>[] uvs={new List<Vector2>(),new List<Vector2>(),new List<Vector2>()};
        readonly List<int>[] indices={new List<int>(),new List<int>(),new List<int>()};
        readonly List<Vector3> coreNormals=new List<Vector3>();
        readonly AudioClip[] clips=new AudioClip[2];readonly AudioSource[] sounds=new AudioSource[2];
        Func<Vector3> emitter;AnimatedSurfaceContact surface,shieldSurface;
        Vector3 aim,origin,position,axis,across,up,velocity,contactCenter;Bounds originalBounds;
        float elapsed,radius,contactAt=-1,flight;bool released,disposed,damageEnabled,verifyMiss,contactGeometryMeasured;
        public bool Completed=>elapsed>=Duration;
        public bool IsPlaying=>!disposed&&!Completed;
        public float EffectSeconds=>elapsed;
        public float EffectProgress=>Mathf.Clamp01(elapsed/Duration);
        public bool Launched=>released;
        public bool Detached {get;private set;}
        public bool BodyContact {get;private set;}
        public bool ShieldContact {get;private set;}
        public Vector3 ContactPoint {get;private set;}
        public Vector3 ContactNormal {get;private set;}
        public float ContactSeconds {get;private set;}=-1;
        public int MissSurfaceQueries {get;private set;}
        public int MissSurfaceCrossings {get;private set;}
        public float OriginalThreatSeconds {get;private set;}=-1;
        public bool OriginalLaneReached {get;private set;}
        public bool BodyGeometryAvailable {get;private set;}
        public bool ShieldGeometryAvailable {get;private set;}
        public bool QueryFailed {get;private set;}
        public string QueryFailure {get;private set;}
        public int BakedFrames=>surface!=null?surface.BakedFrames:0;
        public long TriangleTests=>(surface!=null?surface.TriangleTests:0)+(shieldSurface!=null?shieldSurface.TriangleTests:0);
        public string ContactMesh {get;private set;}
        public float SurfaceError {get;private set;}=-1;
        public float ContactRadiusError {get;private set;}=-1;
        public bool GeometryFinite {get;private set;}=true;
        public bool SoundStarted {get;private set;}
        public bool ImpactSoundStarted {get;private set;}
        /// <summary>Nearest generated core vertex to contact on the first contact draw; tessellation and shield separation can make this nonzero.</summary>
        public float NearestCoreVertexGap {get;private set;}=-1;
        public float RenderedFrontGap=>NearestCoreVertexGap;

        public static EnergyBallEffect Create(Transform parent,Func<Vector3> source,Vector3 lockedAim,GameObject victim,float size,bool hit,bool miss,Bounds original,GameObject shield)
        {
            var go=new GameObject("Eco-Sphere · gathering nature orb");go.transform.SetParent(parent,false);
            var e=go.AddComponent<EnergyBallEffect>();e.emitter=source;e.aim=lockedAim;e.radius=Mathf.Clamp(size,.08f,.4f);
            e.damageEnabled=hit;e.verifyMiss=miss;e.originalBounds=original;
            try
            {
                e.surface=new AnimatedSurfaceContact();e.surface.Initialize(victim);e.BodyGeometryAvailable=e.surface.Ready;
                if(shield!=null){e.shieldSurface=new AnimatedSurfaceContact();e.shieldSurface.Initialize(shield);e.ShieldGeometryAvailable=e.shieldSurface.Ready;}
                e.Initialize();
            }
            catch{e.Cancel();Destroy(go);throw;}
            return e;
        }
        void Initialize()
        {
            var reference=Resources.Load<Material>("Materials/EnergyBallReference");
            Shader shader=reference!=null?reference.shader:Shader.Find("AeroStadium/Original Nature Energy");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Energy orb needs the original nature energy shader.");
            string[] names={"Luminous nature sphere","Curved energy and pollen","Orbiting leaf silhouettes"};
            for(int m=0;m<3;m++)
            {
                meshes[m]=new Mesh{name="Original energy orb geometry "+m};meshes[m].MarkDynamic();
                materials[m]=reference!=null?new Material(reference):new Material(shader);materials[m].SetFloat("_Layer",m);
                var part=new GameObject(names[m]);part.transform.SetParent(transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=meshes[m];var renderer=part.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[m];
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            position=emitter();PlaySound(0);SoundStarted=sounds[0].isPlaying;
        }
        public void SetShieldBounds(Bounds bounds)
        {
            foreach(var material in materials)if(material!=null){material.SetFloat("_ClipShield",1);material.SetVector("_ShieldCenter",bounds.center);material.SetVector("_ShieldRadii",bounds.extents);}
        }
        void LateUpdate()
        {
            if(disposed||Completed)return;
            float before=elapsed;elapsed=Mathf.Min(Duration,elapsed+Time.deltaTime);
            if(!released)
            {
                position=emitter();axis=(aim-position).normalized;Basis(axis,out across,out up);
                if(elapsed>=ReleaseTime){released=true;origin=position;flight=Mathf.Clamp(Vector3.Distance(origin,aim)/8f,.35f,.65f);velocity=axis*Vector3.Distance(origin,aim)/flight;}
            }
            if(released&&contactAt<0)
            {
                float at=Mathf.Max(before,ReleaseTime);Vector3 from=origin+velocity*(at-ReleaseTime),to=origin+velocity*(elapsed-ReleaseTime);
                // A conservative expanded rest-bounds threat diagnostic, never a body contact source.
                if(verifyMiss&&OriginalThreatSeconds<0)
                {
                    Bounds threat=originalBounds;threat.Expand(radius*2);Vector3 delta=to-from;float distance=delta.magnitude;
                    if(threat.Contains(from)){OriginalThreatSeconds=at;OriginalLaneReached=true;}
                    else if(distance>.000001f&&threat.IntersectRay(new Ray(from,delta/distance),out float entry)&&entry>=0&&entry<=distance)
                    {OriginalThreatSeconds=Mathf.Lerp(at,elapsed,entry/distance);OriginalLaneReached=true;}
                }
                Vector3 center,p,n;bool shieldHit=false;
                if(shieldSurface!=null)
                {
                    shieldHit=shieldSurface.TrySweptSphere(from,to,radius,out center,out p,out n);
                    ShieldGeometryAvailable=shieldSurface.SphereGeometryAvailable;RecordQueryFailure(shieldSurface);
                    if(shieldHit)Contact(center,p,n,true,at,elapsed,from,to,shieldSurface);
                }
                if(!shieldHit&&surface!=null)
                {
                    if(!damageEnabled)MissSurfaceQueries++;
                    bool hit=surface.TrySweptSphere(from,to,radius,out center,out p,out n);
                    BodyGeometryAvailable=surface.SphereGeometryAvailable;RecordQueryFailure(surface);
                    if(hit){if(damageEnabled)Contact(center,p,n,false,at,elapsed,from,to,surface);else MissSurfaceCrossings++;}
                }
                if(contactAt<0)position=to;
                Detached|=Vector3.Distance(position,origin)>radius*2;
            }
            if(ShieldContact)
            {
                float age=elapsed-contactAt;Vector3 reflected=Vector3.Reflect(velocity,ContactNormal)*.28f;
                position=contactCenter+ContactNormal*.012f+reflected*((1-Mathf.Exp(-4*age))/4)+Vector3.up*age*radius;
            }
            Draw();
        }
        void RecordQueryFailure(AnimatedSurfaceContact contact)
        {
            if(!contact.SphereQueryFailed)return;
            QueryFailed=true;QueryFailure=contact.LastFailure??"Sphere surface query failed.";
        }
        void Contact(Vector3 center,Vector3 point,Vector3 normal,bool shield,float at,float end,Vector3 from,Vector3 to,AnimatedSurfaceContact source)
        {
            if(contactAt>=0)return;ContactPoint=point;ContactNormal=normal;contactCenter=center;contactAt=elapsed;
            ContactSeconds=Mathf.Lerp(at,end,Vector3.Distance(from,center)/Mathf.Max(.000001f,Vector3.Distance(from,to)));
            BodyContact=!shield;ShieldContact=shield;position=center;Detached=Vector3.Distance(origin,center)>radius*2;
            ContactMesh=source.LastMeshName;SurfaceError=source.LastSurfaceError;ContactRadiusError=Mathf.Abs(Vector3.Distance(center,point)-radius);
            PlaySound(1);ImpactSoundStarted=sounds[1].isPlaying;
        }
        void Draw()
        {
            coreNormals.Clear();
            for(int m=0;m<3;m++){vertices[m].Clear();colors[m].Clear();uvs[m].Clear();indices[m].Clear();materials[m].SetFloat("_Age",elapsed);}
            float age=contactAt<0?0:elapsed-contactAt;
            float fade=contactAt<0?1-Smooth(ReleaseTime+flight+.12f,Duration,elapsed):1-Smooth(.06f,.46f,age);
            float r=radius*(released?1:Mathf.SmoothStep(.05f,1,elapsed/ReleaseTime));
            if(!BodyContact||age<.035f||!contactGeometryMeasured)Sphere(position,r,new Color(.55f,1,.12f,fade*.68f));
            for(int leaf=0;leaf<12;leaf++)
            {
                float theta=leaf*2.399963f+elapsed*(3.5f+leaf%3),lat=Mathf.Sin(leaf*1.71f+elapsed*1.8f);
                Vector3 radial=across*Mathf.Cos(theta)+up*Mathf.Sin(theta),center=position+radial*r*1.25f+axis*lat*r*.7f;
                if(contactAt>=0)center=ContactPoint+ContactNormal*(.04f+age*.7f)+radial*age*(1+leaf*.08f);
                Leaf(center,radial,Vector3.Cross(axis,radial),r*.3f,fade);
                Vector3 previous=position+radial*r;
                for(int j=1;j<=9;j++)
                {
                    float a=theta+j*.12f;Vector3 next=position+(across*Mathf.Cos(a)+up*Mathf.Sin(a))*r*(1.05f+.13f*Mathf.Sin(a*3))+axis*r*Mathf.Sin(a*2+leaf)*.5f;
                    Quad(1,previous,next,axis*r*.035f,new Color(.53f,1,.15f,fade*.48f));previous=next;
                }
                if(released&&contactAt<0)for(int j=1;j<=4;j++)
                {
                    Vector3 pollen=position-axis*radius*(j*.9f+leaf*.04f)+radial*radius*(.4f+j*.15f);
                    Quad(1,pollen-up*r*.03f,pollen+up*r*.03f,across*r*.025f,new Color(.86f,1,.3f,fade*(1-j*.19f)));
                }
                if(contactAt>=0)Quad(1,ContactPoint+radial*age*.4f+ContactNormal*.025f,ContactPoint+radial*(r+age*2)+ContactNormal*.025f,Vector3.Cross(ContactNormal,radial)*r*.045f,new Color(.8f,1,.23f,fade));
            }
            for(int m=0;m<3;m++)
            {
                meshes[m].Clear();meshes[m].SetVertices(vertices[m]);meshes[m].SetColors(colors[m]);meshes[m].SetUVs(0,uvs[m]);meshes[m].SetTriangles(indices[m],0);
                if(m==0)meshes[m].SetNormals(coreNormals);else meshes[m].RecalculateNormals();meshes[m].RecalculateBounds();
            }
            if(contactAt>=0&&!contactGeometryMeasured&&vertices[0].Count>0)
            {
                float nearest=float.PositiveInfinity;foreach(Vector3 vertex in vertices[0])nearest=Mathf.Min(nearest,Vector3.Distance(transform.TransformPoint(vertex),ContactPoint));
                NearestCoreVertexGap=nearest;contactGeometryMeasured=true;
            }
        }
        void Sphere(Vector3 center,float r,Color color)
        {
            const int steps=20,rings=12;Matrix4x4 normalToLocal=transform.localToWorldMatrix.transpose;
            for(int j=0;j<rings;j++)for(int i=0;i<steps;i++)
            {
                int start=vertices[0].Count;
                for(int k=0;k<4;k++)
                {
                    float u=(i+(k==1||k==2?1:0))/(float)steps,v=(j+(k>1?1:0))/(float)rings,t=v*Mathf.PI,a=u*Mathf.PI*2;
                    Vector3 radial=axis*Mathf.Cos(t)+(across*Mathf.Cos(a)+up*Mathf.Sin(a))*Mathf.Sin(t);
                    Add(0,center+r*radial,color,new Vector2(u,v));coreNormals.Add(normalToLocal.MultiplyVector(radial).normalized);
                }
                Triangle(0,start,start+1,start+2);Triangle(0,start,start+2,start+3);
            }
        }
        void Leaf(Vector3 point,Vector3 forward,Vector3 side,float r,float alpha)
        {
            int start=vertices[2].Count;Color color=new Color(.25f,.77f,.075f,alpha);
            Add(2,point-forward*r,color,new Vector2(0,.5f));Add(2,point+side*r*.5f,color,new Vector2(.5f,0));Add(2,point+forward*r,color,new Vector2(1,.5f));Add(2,point-side*r*.5f,color,new Vector2(.5f,1));
            Triangle(2,start,start+1,start+2);Triangle(2,start,start+2,start+3);
        }
        void Quad(int mesh,Vector3 a,Vector3 b,Vector3 width,Color color)
        {
            int start=vertices[mesh].Count;Add(mesh,a-width,color,new Vector2(.05f,0));Add(mesh,a+width,color,new Vector2(.05f,1));Add(mesh,b+width,color,new Vector2(.95f,1));Add(mesh,b-width,color,new Vector2(.95f,0));
            Triangle(mesh,start,start+1,start+2);Triangle(mesh,start,start+2,start+3);
        }
        void Add(int mesh,Vector3 point,Color color,Vector2 uv){point=transform.InverseTransformPoint(point);GeometryFinite&=Finite(point.x)&&Finite(point.y)&&Finite(point.z);vertices[mesh].Add(point);colors[mesh].Add(color);uvs[mesh].Add(uv);}
        void Triangle(int mesh,int a,int b,int c){indices[mesh].Add(a);indices[mesh].Add(b);indices[mesh].Add(c);}
        static void Basis(Vector3 direction,out Vector3 side,out Vector3 vertical){side=Vector3.Cross(direction,Mathf.Abs(direction.y)>.9f?Vector3.forward:Vector3.up).normalized;vertical=Vector3.Cross(side,direction).normalized;}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static float Smooth(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        void PlaySound(int slot)
        {
            float[] pcm=CreateOriginalPcm(slot==1);clips[slot]=AudioClip.Create(slot==0?"Original nature energy gathering and leaf rush":"Original nature energy contact pulse",pcm.Length,1,22050,false);clips[slot].SetData(pcm,0);
            sounds[slot]=gameObject.AddComponent<AudioSource>();sounds[slot].clip=clips[slot];sounds[slot].playOnAwake=false;sounds[slot].volume=.48f;sounds[slot].spatialBlend=0;sounds[slot].dopplerLevel=0;sounds[slot].Play();
        }
        /// <summary>Deterministic original PCM; impact pulse starts only after a triangle surface contact.</summary>
        public static float[] CreateOriginalPcm(bool impact=false)
        {
            float duration=impact ? .48f : Duration;var pcm=new float[Mathf.CeilToInt(duration*22050)];uint seed=0x412e091u;float low=0,mid=0;
            for(int i=0;i<pcm.Length;i++)
            {
                seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;float noise=(seed&0xffffffu)/8388607.5f-1;low+=(noise-low)*.025f;mid+=(noise-mid)*.12f;
                float t=i/22050f,envelope=impact?Mathf.Sin(Mathf.PI*.5f*Mathf.Clamp01(t/.045f))*Mathf.Exp(-Mathf.Max(0,t-.045f)*9):Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/1.6f));
                float tone=impact?Mathf.Sin(2*Mathf.PI*(190*t-65*t*t)):Mathf.Sin(2*Mathf.PI*(235*t+92*t*t));
                float rush=impact ? .28f : Mathf.Exp(-Mathf.Pow((t-.82f)/.33f,2));
                pcm[i]=Mathf.Clamp(envelope*(tone*.12f+low*.5f+(mid-low)*rush*.65f),-.8f,.8f)*Mathf.Clamp01((duration-t)/.06f);
            }
            pcm[0]=pcm[pcm.Length-1]=0;return pcm;
        }
        public void Cancel()
        {
            if(disposed)return;disposed=true;surface?.Dispose();surface=null;shieldSurface?.Dispose();shieldSurface=null;
            for(int i=0;i<2;i++){if(sounds[i]!=null){sounds[i].Stop();Destroy(sounds[i]);}if(clips[i]!=null)Destroy(clips[i]);}
            for(int i=0;i<3;i++){if(meshes[i]!=null)Destroy(meshes[i]);if(materials[i]!=null)Destroy(materials[i]);}
        }
        void OnDestroy(){Cancel();}
    }
}
