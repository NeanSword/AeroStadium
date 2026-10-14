using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>An original continuous fire breath: ignition, turbulent propagation and expanding impact.</summary>
    public sealed class FlamethrowerEffect : MonoBehaviour
    {
        const int Sheets=3, Segments=36, Across=8;
        const int StreamFlames=80, ImpactFlames=32, ChargeFlames=12, Sparks=64;
        const float TotalDuration=3.15f, HitTime=.69f, ReleaseTime=2.32f;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly MeshRenderer[] renderers=new MeshRenderer[3];
        readonly Light[] lights=new Light[2];
        Vector3 source,target;
        float radius,elapsed,victimHeight,victimRadius;
        public bool ImpactEnabled {get;set;}=true;
        public float ImpactOpacity {get;private set;}
        public void SetVictimBounds(Bounds bounds){victimHeight=Mathf.Clamp(bounds.extents.y,.2f,2.6f);victimRadius=Mathf.Clamp(Mathf.Min(Mathf.Max(bounds.extents.x,bounds.extents.z),bounds.extents.y)*.60f,.25f,1.6f);}
        bool initialized,released,deflected;
        Vector3 shieldContact,deflectedDirection;float deflectedLength;
        public void SetDeflection(Vector3 point,Vector3 direction,float length){deflected=true;shieldContact=point;deflectedDirection=direction.normalized;deflectedLength=Mathf.Max(.1f,length);ImpactEnabled=false;}
        Shader shader;
        Mesh jetMesh,flameMesh,sparkMesh;
        Vector3[] jetVertices,flameVertices,sparkVertices;
        Color[] jetColors,flameColors,sparkColors;
        public float Duration=>TotalDuration;
        public float EffectProgress=>Mathf.Clamp01(elapsed/TotalDuration);
        public float AnimationPhase=>elapsed*1.8f;
        public bool IsPlaying=>initialized&&elapsed<TotalDuration;
        public bool Completed=>initialized&&!IsPlaying;
        public bool ImpactReached=>elapsed>=HitTime;
        public int PeakActiveFlames {get;private set;}
        public int RendererCount=>3;
        public int TriangleCount=>Sheets*Segments*Across*2+(StreamFlames+ImpactFlames+ChargeFlames+Sparks)*2;

        public static FlamethrowerEffect Create(Transform parent,Vector3 source,Vector3 target,float width=.65f)
        {
            var go=new GameObject("Lance-Flamme · original turbulent fire breath");
            go.transform.SetParent(parent,false);
            var effect=go.AddComponent<FlamethrowerEffect>();
            effect.source=source;effect.target=target;effect.radius=Mathf.Clamp(width,.25f,1.8f);
            effect.Initialize();return effect;
        }

        public void SetEndpoints(Vector3 source,Vector3 target)
        {this.source=source;this.target=target;}

        void Initialize()
        {
            Material reference=Resources.Load<Material>("Materials/FlamethrowerReference");
            shader=reference!=null?reference.shader:Shader.Find("AeroStadium/Original Flamethrower");
            if(shader==null)throw new InvalidOperationException("The original Flamethrower shader is missing.");
            jetMesh=CreateJetMesh();flameMesh=CreateBillboardMesh("Rolling fire volumes",StreamFlames+ImpactFlames+ChargeFlames,out flameVertices,out flameColors);
            sparkMesh=CreateBillboardMesh("Drifting fire breath cinders",Sparks,out sparkVertices,out sparkColors);
            Mesh[] meshes={jetMesh,flameMesh,sparkMesh};
            for(int i=0;i<3;i++)
            {
                owned.Add(meshes[i]);
                Material material=reference!=null?new Material(reference):new Material(shader);
                material.name="Lance-Flamme · "+(i==0?"fluid tongues":i==1?"rolling flame volumes":"cinders");
                material.SetFloat("_Mode",i);owned.Add(material);
                var go=new GameObject(material.name);go.transform.SetParent(transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=meshes[i];
                renderers[i]=go.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=material;
                renderers[i].shadowCastingMode=ShadowCastingMode.Off;renderers[i].receiveShadows=false;
            }
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Fire breath ignition light":"Fire breath impact light");
                go.transform.SetParent(transform,false);lights[i]=go.AddComponent<Light>();
                lights[i].type=LightType.Point;lights[i].color=new Color(1,.30f,.035f);
                lights[i].range=radius*8;lights[i].shadows=LightShadows.None;
            }
            initialized=true;UpdateEffect();
        }

        Mesh CreateJetMesh()
        {
            int count=Sheets*(Segments+1)*(Across+1);
            jetVertices=new Vector3[count];jetColors=new Color[count];
            var uv=new Vector2[count];var phase=new Vector2[count];
            var triangles=new int[Sheets*Segments*Across*6];int index=0;
            for(int sheet=0;sheet<Sheets;sheet++)
            {
                int start=sheet*(Segments+1)*(Across+1);
                for(int row=0;row<=Segments;row++)for(int column=0;column<=Across;column++)
                {
                    int vertex=start+row*(Across+1)+column;
                    uv[vertex]=new Vector2(column/(float)Across,row/(float)Segments*7);
                    phase[vertex]=new Vector2(row/(float)Segments,sheet*2.73f);
                }
                for(int row=0;row<Segments;row++)for(int column=0;column<Across;column++)
                {
                    int a=start+row*(Across+1)+column,b=a+1,c=a+Across+1,d=c+1;
                    triangles[index++]=a;triangles[index++]=c;triangles[index++]=b;
                    triangles[index++]=b;triangles[index++]=c;triangles[index++]=d;
                }
            }
            var mesh=new Mesh{name="Lance-Flamme · curved moving fire ribbons",vertices=jetVertices,uv=uv,uv2=phase,triangles=triangles};
            mesh.MarkDynamic();return mesh;
        }

        static Mesh CreateBillboardMesh(string name,int count,out Vector3[] vertices,out Color[] colors)
        {
            vertices=new Vector3[count*4];colors=new Color[count*4];
            var uv=new Vector2[count*4];var triangles=new int[count*6];
            for(int i=0;i<count;i++)
            {
                int v=i*4,t=i*6;uv[v]=Vector2.zero;uv[v+1]=Vector2.right;uv[v+2]=Vector2.up;uv[v+3]=Vector2.one;
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;
                triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            var mesh=new Mesh{name="Lance-Flamme · "+name,vertices=vertices,uv=uv,triangles=triangles};
            mesh.MarkDynamic();return mesh;
        }

        void Update()
        {
            if(!initialized||released)return;
            elapsed=Mathf.Min(TotalDuration,elapsed+Time.deltaTime);
            if(elapsed>=TotalDuration){ImpactOpacity=0;ReleaseResources();return;}
            UpdateEffect();
        }

        void UpdateEffect()
        {
            float head=Smooth(.19f,HitTime,elapsed);
            float strength=Smooth(.17f,.31f,elapsed)*(1-Smooth(ReleaseTime,2.77f,elapsed));
            float charge=(1-Smooth(.19f,.38f,elapsed))*Smooth(0,.11f,elapsed);
            float impact=ImpactEnabled?Smooth(HitTime,HitTime+.14f,elapsed)*(1-Smooth(2.57f,TotalDuration,elapsed)):0;
            ImpactOpacity=impact;
            Vector3 delta=(deflected?shieldContact:target)-source;float firstLength=Mathf.Max(.1f,delta.magnitude);
            float length=firstLength+(deflected?deflectedLength:0);
            if(deflected){float join=firstLength/length;head=elapsed<HitTime?Smooth(.19f,HitTime,elapsed)*join:Mathf.Lerp(join,1,Smooth(HitTime,HitTime+.35f,elapsed));}
            Vector3 forward=delta/firstLength;
            Vector3 right=Vector3.Cross(Mathf.Abs(forward.y)>.95f?Vector3.forward:Vector3.up,forward).normalized;
            Vector3 up=Vector3.Cross(forward,right).normalized;
            Matrix4x4 toLocal=transform.worldToLocalMatrix;
            for(int sheet=0;sheet<Sheets;sheet++)
            {
                float angle=(sheet*60+17)*Mathf.Deg2Rad;
                Vector3 across=right*Mathf.Cos(angle)+up*Mathf.Sin(angle),depth=Vector3.Cross(forward,across);
                int start=sheet*(Segments+1)*(Across+1);
                for(int row=0;row<=Segments;row++)
                {
                    float p=row/(float)Segments*head;
                    Vector3 center=JetCenter(p,forward,right,up,length);
                    Vector3 tangent=deflected&&p*length>firstLength?deflectedDirection:forward;
                    Vector3 localRight=Vector3.Cross(Mathf.Abs(tangent.y)>.95f?Vector3.forward:Vector3.up,tangent).normalized;
                    Vector3 localUp=Vector3.Cross(tangent,localRight).normalized;
                    across=localRight*Mathf.Cos(angle)+localUp*Mathf.Sin(angle);depth=Vector3.Cross(tangent,across);
                    float rolling=1+.21f*Mathf.Sin(p*27-elapsed*17+sheet)+.11f*Mathf.Sin(p*63-elapsed*25);
                    float width=radius*(.31f+1.45f*Smooth(0,1,p))*rolling;
                    float envelope=(1-Smooth(.83f,1,row/(float)Segments))*strength;
                    for(int col=0;col<=Across;col++)
                    {
                        float u=col/(float)Across;int vertex=start+row*(Across+1)+col;
                        Vector3 point=center+across*((u-.5f)*width*2)+depth*(Mathf.Sin(u*Mathf.PI)*width*.21f);
                        jetVertices[vertex]=toLocal.MultiplyPoint3x4(point);
                        jetColors[vertex]=new Color(1,0,envelope,1);
                    }
                }
            }
            jetMesh.vertices=jetVertices;jetMesh.colors=jetColors;
            int active=0;
            for(int i=0;i<StreamFlames;i++)
            {
                float p=Mathf.Repeat((elapsed-.19f)*1.85f+i*.618034f,1);
                float angle=i*2.399963f+elapsed*3.1f;
                float drift=radius*(.10f+p*.64f);
                Vector3 center=JetCenter(p,forward,right,up,length)+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*drift;
                float opacity=p<head?strength*(.38f+.43f*Mathf.Sin(p*Mathf.PI)):0;
                float size=radius*(.28f+p*.31f)*( .85f+.15f*Mathf.Sin(i+elapsed*8));
                SetBillboard(flameVertices,flameColors,i,toLocal.MultiplyPoint3x4(center),size,i*.731f,opacity,1.26f);
                if(opacity>.02f)active++;
            }
            for(int i=0;i<ImpactFlames;i++)
            {
                float age=Mathf.Repeat(Mathf.Max(0,elapsed-HitTime)*.96f+i*.618034f,1);
                float angle=i*2.399963f;
                float spread=Mathf.Max(radius,victimRadius)*(.28f+age*.95f);
                Vector3 center=target+right*Mathf.Cos(angle)*spread+up*Mathf.Sin(angle)*Mathf.Max(radius,victimHeight)*(.28f+age*.65f);
                center+=up*(age*Mathf.Max(radius,victimHeight)*.20f)-forward*(age*radius*.35f);
                float opacity=impact*Mathf.Sin(age*Mathf.PI)*.70f;
                SetBillboard(flameVertices,flameColors,StreamFlames+i,toLocal.MultiplyPoint3x4(center),Mathf.Max(radius,victimHeight*.65f)*(.50f+age*.50f),i*.837f+37,opacity,1.17f);
                if(opacity>.02f)active++;
            }
            for(int i=0;i<ChargeFlames;i++)
            {
                float angle=i*2.399963f+elapsed*5;
                Vector3 center=source+forward*radius*.18f+(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*.19f;
                SetBillboard(flameVertices,flameColors,StreamFlames+ImpactFlames+i,toLocal.MultiplyPoint3x4(center),radius*.31f,i*.473f,charge*.55f,1.0f);
                if(charge>.04f)active++;
            }
            PeakActiveFlames=Mathf.Max(PeakActiveFlames,active);
            flameMesh.vertices=flameVertices;flameMesh.colors=flameColors;
            for(int i=0;i<Sparks;i++)
            {
                float life=Mathf.Repeat(Mathf.Max(0,elapsed-.19f)*.84f+i*.618034f,1);
                float angle=i*2.399963f;
                bool atImpact=i%3==0;
                Vector3 center=atImpact?target:JetCenter(Mathf.Min(life,head),forward,right,up,length);
                center+=(right*Mathf.Cos(angle)+up*Mathf.Sin(angle))*radius*(.22f+life*(atImpact?2.0f:.75f));
                center+=up*life*radius*(atImpact?1.4f:.65f);
                float opacity=Mathf.Sin(life*Mathf.PI)*(atImpact?impact:strength)*.83f;
                SetBillboard(sparkVertices,sparkColors,i,toLocal.MultiplyPoint3x4(center),radius*(.014f+(i%4)*.005f),i,opacity,2.25f);
            }
            sparkMesh.vertices=sparkVertices;sparkMesh.colors=sparkColors;
            var bounds=new Bounds(toLocal.MultiplyPoint3x4(source),Vector3.one*radius*9.5f);
            bounds.Encapsulate(new Bounds(toLocal.MultiplyPoint3x4(target),Vector3.one*radius*9.5f));
            if(deflected)bounds.Encapsulate(new Bounds(toLocal.MultiplyPoint3x4(shieldContact+deflectedDirection*deflectedLength),Vector3.one*radius*9.5f));
            jetMesh.bounds=flameMesh.bounds=sparkMesh.bounds=bounds;
            lights[0].transform.position=source+forward*radius*.17f;
            lights[1].transform.position=target-forward*radius*.5f;
            float flicker=1+Mathf.Sin(elapsed*24)*.13f+Mathf.Sin(elapsed*43)*.07f;
            lights[0].intensity=(charge*2.3f+strength*3.0f)*flicker;
            lights[1].intensity=impact*4.2f*flicker;
        }

        Vector3 JetCenter(float p,Vector3 forward,Vector3 right,Vector3 up,float length)
        {
            float bend=Mathf.Sin(p*Mathf.PI)*radius;
            float distance=p*length,first=Vector3.Distance(source,shieldContact);
            Vector3 path=deflected?(distance<=first?Vector3.Lerp(source,shieldContact,distance/Mathf.Max(.01f,first)):shieldContact+deflectedDirection*(distance-first)):source+forward*distance;
            return path+right*(Mathf.Sin(p*15-elapsed*8)*bend*.13f)
                +up*(Mathf.Sin(p*23-elapsed*11)*bend*.10f);
        }

        static void SetBillboard(Vector3[] vertices,Color[] colors,int index,Vector3 center,float size,float seed,float opacity,float elongation)
        {
            Color parameters=new Color(size,seed,opacity,elongation);
            int first=index*4;for(int k=0;k<4;k++){vertices[first+k]=center;colors[first+k]=parameters;}
        }
        static float Smooth(float from,float to,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));

        public bool Validate(out string error)
        {
            error=null;
            if(!initialized||shader==null||!shader.isSupported)error="The fire breath has no supported initialized shader.";
            else if(!Finite(source)||!Finite(target)||(source-target).sqrMagnitude<.0001f)
                error="Fire breath endpoints must be distinct finite world positions.";
            else if(GetComponentsInChildren<Collider>().Length!=0)
                error="A fire breath effect must not add physical walls.";
            return error==null;
        }
        static bool Finite(Vector3 p)=>!float.IsNaN(p.x)&&!float.IsNaN(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.sqrMagnitude);

        void ReleaseResources()
        {
            if(released)return;released=true;
            foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=false;
            foreach(var light in lights)if(light!=null)light.enabled=false;
            foreach(var obj in owned)if(obj!=null)
            {if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
            owned.Clear();
        }
        void OnDestroy()=>ReleaseResources();
    }
}
