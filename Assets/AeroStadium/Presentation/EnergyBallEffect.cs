using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Original finite nature orb. Collision scope is its leading rendered point, not sphere CCD.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class EnergyBallEffect : MonoBehaviour
    {
        public const float Duration=2.1f, ReleaseTime=.62f;
        public delegate bool ShieldTrace(Vector3 from,Vector3 to,out Vector3 point,out Vector3 normal);
        readonly Mesh[] meshes=new Mesh[2];readonly Material[] materials=new Material[2];
        readonly List<Vector3>[] vertices={new List<Vector3>(),new List<Vector3>()};
        readonly List<Color>[] colors={new List<Color>(),new List<Color>()};
        readonly List<Vector2>[] uvs={new List<Vector2>(),new List<Vector2>()};
        readonly List<int>[] indices={new List<int>(),new List<int>()};
        readonly AudioClip[] clips=new AudioClip[2];readonly AudioSource[] sounds=new AudioSource[2];
        Func<Vector3> emitter;ShieldTrace shieldTrace;AnimatedSurfaceContact surface;
        Vector3 aim,origin,position,axis,across,up,velocity;Bounds originalBounds;
        float elapsed,radius,contactAt=-1,flight;bool released,disposed,damageEnabled,verifyMiss;
        public bool Completed=>elapsed>=Duration;
        public bool IsPlaying=>!disposed&&!Completed;
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
        public int BakedFrames=>surface!=null?surface.BakedFrames:0;
        public long TriangleTests=>surface!=null?surface.TriangleTests:0;
        public string ContactMesh {get;private set;}
        public float SurfaceError {get;private set;}=-1;
        public bool GeometryFinite {get;private set;}=true;
        public bool SoundStarted {get;private set;}
        public bool ImpactSoundStarted {get;private set;}
        public float RenderedFrontGap {get;private set;}

        public static EnergyBallEffect Create(Transform parent,Func<Vector3> source,Vector3 lockedAim,GameObject victim,float size,bool hit,bool miss,Bounds original,ShieldTrace shield)
        {
            var go=new GameObject("Eco-Sphere · gathering nature orb");go.transform.SetParent(parent,false);
            var e=go.AddComponent<EnergyBallEffect>();e.emitter=source;e.aim=lockedAim;e.radius=Mathf.Clamp(size,.08f,.4f);
            e.damageEnabled=hit;e.verifyMiss=miss;e.originalBounds=original;e.shieldTrace=shield;
            try{e.surface=new AnimatedSurfaceContact(victim);e.Initialize();}catch{e.Cancel();Destroy(go);throw;}
            return e;
        }
        void Initialize()
        {
            var reference=Resources.Load<Material>("Materials/GrassAttackReference");
            Shader shader=reference!=null?reference.shader:Shader.Find("AeroStadium/Original Living Grass");
            if(shader==null)throw new InvalidOperationException("Energy orb needs the living grass shader.");
            for(int m=0;m<2;m++)
            {
                meshes[m]=new Mesh{name="Original energy orb geometry "+m};meshes[m].MarkDynamic();
                materials[m]=reference!=null?new Material(reference):new Material(shader);materials[m].SetFloat("_Mode",m==0?2:1);
                var part=new GameObject(m==0?"Luminous core, curved energy and pollen":"Orbiting leaf silhouettes");part.transform.SetParent(transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=meshes[m];var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=materials[m];
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
            position=emitter();PlaySound(0,position);SoundStarted=sounds[0].isPlaying;
        }
        public void SetShieldBounds(Bounds b)
        {
            foreach(var m in materials)if(m!=null){m.SetFloat("_ClipShield",1);m.SetVector("_ShieldCenter",b.center);m.SetVector("_ShieldRadii",b.extents);}
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
                float at=Mathf.Max(before,ReleaseTime),end=Mathf.Min(elapsed,ReleaseTime+flight+.3f);
                while(at<end-.000001f&&contactAt<0)
                {
                    float next=Mathf.Min(end,at+1f/120f);Vector3 from=origin+velocity*(at-ReleaseTime)+axis*radius,to=origin+velocity*(next-ReleaseTime)+axis*radius;
                    if(verifyMiss&&OriginalThreatSeconds<0)
                    {
                        Vector3 delta=to-from;float distance=delta.magnitude;
                        if(distance>.000001f&&originalBounds.IntersectRay(new Ray(from,delta/distance),out float entry)&&entry>=0&&entry<=distance)
                        {OriginalThreatSeconds=Mathf.Lerp(at,next,entry/distance);OriginalLaneReached=true;}
                    }
                    Vector3 p,n;
                    if(shieldTrace!=null&&shieldTrace(from,to,out p,out n))Contact(p,n,true,at,next,from,to);
                    else if(surface!=null)
                    {
                        if(!damageEnabled)MissSurfaceQueries++;
                        if(surface.TrySegment(from,to,out p,out n))
                        {
                            if(damageEnabled)Contact(p,n,false,at,next,from,to);else MissSurfaceCrossings++;
                        }
                    }
                    at=next;
                }
                if(contactAt<0)position=origin+velocity*Mathf.Max(0,elapsed-ReleaseTime);
                Detached|=Vector3.Distance(position,origin)>radius*2;
            }
            if(ShieldContact)
            {
                float age=elapsed-contactAt;Vector3 reflected=Vector3.Reflect(velocity,ContactNormal)*.28f;
                position=ContactPoint-axis*radius+ContactNormal*.02f+reflected*((1-Mathf.Exp(-4*age))/4)+Vector3.up*age*radius;
                float front=Vector3.Dot(position-ContactPoint,ContactNormal);if(front<radius+.01f)position+=ContactNormal*(radius+.01f-front);
            }
            Draw();
        }
        void Contact(Vector3 p,Vector3 n,bool shield,float at,float next,Vector3 from,Vector3 to)
        {
            if(contactAt>=0)return;ContactPoint=p;ContactNormal=n;contactAt=elapsed;
            ContactSeconds=Mathf.Lerp(at,next,Vector3.Distance(from,p)/Mathf.Max(.000001f,Vector3.Distance(from,to)));
            BodyContact=!shield;ShieldContact=shield;position=p-axis*radius;
            RenderedFrontGap=Vector3.Distance(position+axis*radius,p);Detached=Vector3.Distance(origin,p)>radius*2;
            if(!shield){ContactMesh=surface.LastMeshName;SurfaceError=surface.LastSurfaceError;}
            PlaySound(1,p);ImpactSoundStarted=sounds[1].isPlaying;
        }
        void Draw()
        {
            for(int m=0;m<2;m++){vertices[m].Clear();colors[m].Clear();uvs[m].Clear();indices[m].Clear();}
            float age=contactAt<0?0:elapsed-contactAt;
            float fade=contactAt<0?1-Smooth(ReleaseTime+flight+.12f,Duration,elapsed):1-Smooth(.06f,.46f,age);
            float r=radius*(released?1:Mathf.SmoothStep(.05f,1,elapsed/ReleaseTime));
            if(!BodyContact||age<.035f)for(int shell=0;shell<3;shell++)Sphere(position,r*(1-shell*.29f),new Color(.48f+shell*.19f,1,.07f+shell*.22f,fade*(.23f+shell*.15f)));
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
                    Quad(0,previous,next,axis*r*.035f,new Color(.53f,1,.15f,fade*.48f));previous=next;
                }
                if(released&&contactAt<0)for(int j=1;j<=4;j++)
                {
                    Vector3 pollen=position-axis*radius*(j*.9f+leaf*.04f)+radial*radius*(.4f+j*.15f);
                    Quad(0,pollen-up*r*.03f,pollen+up*r*.03f,across*r*.025f,new Color(.86f,1,.3f,fade*(1-j*.19f)));
                }
                if(contactAt>=0)Quad(0,ContactPoint+radial*age*.4f+ContactNormal*.025f,ContactPoint+radial*(r+age*2)+ContactNormal*.025f,Vector3.Cross(ContactNormal,radial)*r*.045f,new Color(.8f,1,.23f,fade));
            }
            for(int m=0;m<2;m++)
            {
                meshes[m].Clear();meshes[m].SetVertices(vertices[m]);meshes[m].SetColors(colors[m]);meshes[m].SetUVs(0,uvs[m]);meshes[m].SetTriangles(indices[m],0);meshes[m].RecalculateNormals();meshes[m].RecalculateBounds();
            }
        }
        void Sphere(Vector3 center,float r,Color color)
        {
            const int steps=12,rings=8;
            for(int j=0;j<rings;j++)for(int i=0;i<steps;i++)
            {
                int start=vertices[0].Count;
                for(int k=0;k<4;k++){float t=(j+(k>1?1:0))*Mathf.PI/rings,a=(i+(k==1||k==2?1:0))*Mathf.PI*2/steps;Add(0,center+r*(axis*Mathf.Cos(t)+(across*Mathf.Cos(a)+up*Mathf.Sin(a))*Mathf.Sin(t)),color,new Vector2(.5f,.5f));}
                Triangle(0,start,start+1,start+2);Triangle(0,start,start+2,start+3);
            }
        }
        void Leaf(Vector3 p,Vector3 forward,Vector3 side,float r,float alpha)
        {
            int start=vertices[1].Count;Color c=new Color(.25f,.77f,.075f,alpha);
            Add(1,p-forward*r,c,new Vector2(0,.5f));Add(1,p+side*r*.5f,c,new Vector2(.5f,0));Add(1,p+forward*r,c,new Vector2(1,.5f));Add(1,p-side*r*.5f,c,new Vector2(.5f,1));
            Triangle(1,start,start+1,start+2);Triangle(1,start,start+2,start+3);
        }
        void Quad(int m,Vector3 a,Vector3 b,Vector3 width,Color c)
        {
            int start=vertices[m].Count;Add(m,a-width,c,new Vector2(.05f,0));Add(m,a+width,c,new Vector2(.05f,1));Add(m,b+width,c,new Vector2(.95f,1));Add(m,b-width,c,new Vector2(.95f,0));
            Triangle(m,start,start+1,start+2);Triangle(m,start,start+2,start+3);
        }
        void Add(int m,Vector3 p,Color c,Vector2 uv){p=transform.InverseTransformPoint(p);GeometryFinite&=Finite(p.x)&&Finite(p.y)&&Finite(p.z);vertices[m].Add(p);colors[m].Add(c);uvs[m].Add(uv);}
        void Triangle(int m,int a,int b,int c){indices[m].Add(a);indices[m].Add(b);indices[m].Add(c);}
        static void Basis(Vector3 axis,out Vector3 side,out Vector3 up){side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.9f?Vector3.forward:Vector3.up).normalized;up=Vector3.Cross(side,axis).normalized;}
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        static float Smooth(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        void PlaySound(int slot,Vector3 point)
        {
            float[] pcm=CreateOriginalPcm(slot==1);clips[slot]=AudioClip.Create(slot==0?"Original nature energy gathering and leaf rush":"Original nature energy contact pulse",pcm.Length,1,22050,false);clips[slot].SetData(pcm,0);
            sounds[slot]=gameObject.AddComponent<AudioSource>();sounds[slot].clip=clips[slot];sounds[slot].playOnAwake=false;sounds[slot].volume=.48f;sounds[slot].spatialBlend=0;sounds[slot].dopplerLevel=0;sounds[slot].Play();
        }
        /// <summary>Deterministic original PCM; impact pulse is played only after actual surface contact.</summary>
        public static float[] CreateOriginalPcm(bool impact=false)
        {
            float duration=impact ? .48f : Duration;var pcm=new float[Mathf.CeilToInt(duration*22050)];uint seed=0x412e091u;float low=0,mid=0;
            for(int i=0;i<pcm.Length;i++)
            {
                seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;float n=(seed&0xffffffu)/8388607.5f-1;low+=(n-low)*.025f;mid+=(n-mid)*.12f;
                float t=i/22050f,envelope=impact?Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.045f))*Mathf.Exp(-t*9):Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/1.6f));
                if(impact&&t>.045f)envelope=Mathf.Exp(-(t-.045f)*9);
                float tone=impact?Mathf.Sin(2*Mathf.PI*(190*t-65*t*t)):Mathf.Sin(2*Mathf.PI*(235*t+92*t*t));
                float rush=impact ? .28f : Mathf.Exp(-Mathf.Pow((t-.82f)/.33f,2));
                pcm[i]=Mathf.Clamp(envelope*(tone*.12f+low*.5f+(mid-low)*rush*.65f),-.8f,.8f)*Mathf.Clamp01((duration-t)/.06f);
            }
            pcm[0]=pcm[pcm.Length-1]=0;return pcm;
        }
        public void Cancel()
        {
            if(disposed)return;disposed=true;surface?.Dispose();surface=null;
            for(int i=0;i<2;i++){if(sounds[i]!=null){sounds[i].Stop();Destroy(sounds[i]);}if(clips[i]!=null)Destroy(clips[i]);if(meshes[i]!=null)Destroy(meshes[i]);if(materials[i]!=null)Destroy(materials[i]);}
        }
        void OnDestroy(){Cancel();}
    }
}
