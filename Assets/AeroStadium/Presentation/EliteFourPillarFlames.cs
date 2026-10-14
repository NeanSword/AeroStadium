using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Four living gate torches, attached to the short inner approach pillars.</summary>
    public sealed class EliteFourPillarFlames : MonoBehaviour
    {
        const int Tongues = 3, Rows = 28, Columns = 12, Embers = 80;
        const int MantleRows = 14, MantleSegments = 48;
        const float Height = 4.95f, Width = 2.25f;
        static readonly Vector3[] Bases = {
            new Vector3(-12.49f,2.55f,-1.35f), new Vector3(-12.49f,2.55f,1.35f),
            new Vector3(12.49f,2.55f,-1.35f), new Vector3(12.49f,2.55f,1.35f)
        };
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly MeshRenderer[] flames = new MeshRenderer[4];
        readonly Light[] lights = new Light[4];
        readonly Vector3[] emberVertices = new Vector3[Embers*4];
        readonly Color[] emberColors = new Color[Embers*4];
        Mesh emberMesh;
        Material fire, ember;
        public int FlameCount => flames.Length;
        public int RendererCount => 5;
        public int TriangleCount => 4*(Tongues*Rows*Columns*2+MantleRows*MantleSegments*2)+Embers*2;
        public float AnimationPhase => Time.time*1.15f;
        public float AnimatedValue => Mathf.Sin(Time.time*2.3f)+Mathf.Sin(Time.time*5.7f)*.32f;
        public Material SurfaceMaterial => fire;

        public static EliteFourPillarFlames Build(Transform parent)
        {
            var go=new GameObject("Elite Four · four living inner gate flames");
            go.transform.SetParent(parent,false);
            var component=go.AddComponent<EliteFourPillarFlames>();
            component.Create();
            return component;
        }

        void Create()
        {
            // The resource owns the build reference, even though the geometry is generated at runtime.
            var reference=Resources.Load<Material>("Materials/EliteFourFireReference");
            Shader shader=reference!=null?reference.shader:Shader.Find("AeroStadium/Elite Four Living Fire");
            if(shader==null)throw new InvalidOperationException("The living fire shader is missing.");
            fire=reference!=null?new Material(reference):new Material(shader);
            fire.name="Elite Four · yellow-hearted orange flame";owned.Add(fire);
            ember=new Material(fire){name="Elite Four · floating gate sparks"};
            ember.SetFloat("_Ember",1);owned.Add(ember);
            Mesh mesh=CreateFlameMesh();owned.Add(mesh);
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("Living flame · short inner pillar "+(i+1));
                go.transform.SetParent(transform,false);go.transform.localPosition=Bases[i];
                go.transform.localRotation=Quaternion.Euler(0,i*39,0);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                flames[i]=go.AddComponent<MeshRenderer>();flames[i].sharedMaterial=fire;
                flames[i].shadowCastingMode=ShadowCastingMode.Off;flames[i].receiveShadows=false;
                var properties=new MaterialPropertyBlock();properties.SetFloat("_Seed",i*7.31f+1.9f);
                flames[i].SetPropertyBlock(properties);
                var lightObject=new GameObject("Warm gate firelight "+(i+1));
                lightObject.transform.SetParent(transform,false);lightObject.transform.localPosition=Bases[i]+Vector3.up*.42f;
                lights[i]=lightObject.AddComponent<Light>();lights[i].type=LightType.Point;
                lights[i].color=new Color(1,.31f,.055f);lights[i].range=6.5f;
                lights[i].intensity=2.8f;lights[i].shadows=LightShadows.None;
            }
            CreateEmbers();UpdateVisuals();
        }

        static Mesh CreateFlameMesh()
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var phase=new List<Vector2>();
            var triangles=new List<int>();
            for(int tongue=0;tongue<Tongues;tongue++)
            {
                int first=vertices.Count;
                float angle=(tongue*60+13)*Mathf.Deg2Rad;
                Vector3 across=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3 depth=new Vector3(-across.z,0,across.x);
                float height=Height*(tongue==1?.89f:tongue==2?.96f:1);
                for(int row=0;row<=Rows;row++)for(int column=0;column<=Columns;column++)
                {
                    float y=row/(float)Rows,u=column/(float)Columns;
                    // Broad curved skirts wrap the golden collar and its full stone point.
                    // Turbulent upper tongues continue above the cap's y=4.2m tip.
                    float x=(u-.5f)*Width;
                    vertices.Add(across*x+Vector3.up*(y*height-.035f)+depth*Mathf.Sin(u*Mathf.PI)*.18f);
                    uv.Add(new Vector2(u,y));phase.Add(new Vector2(tongue*2.71f,height));
                }
                for(int row=0;row<Rows;row++)for(int col=0;col<Columns;col++)
                {
                    int a=first+row*(Columns+1)+col,b=a+1,c=a+Columns+1,d=c+1;
                    triangles.Add(a);triangles.Add(c);triangles.Add(b);
                    triangles.Add(b);triangles.Add(c);triangles.Add(d);
                }
            }
            // A soft turbulent mantle lies OUTSIDE the stone cap, so the visible
            // front and sides are enveloped rather than masked by the solid pillar.
            int mantleStart=vertices.Count;
            for(int row=0;row<=MantleRows;row++)for(int side=0;side<=MantleSegments;side++)
            {
                float fraction=row/(float)MantleRows,angle=side/(float)MantleSegments*Mathf.PI*2;
                float elevation=fraction*1.72f-.025f;
                float radius=Mathf.Lerp(.67f,.19f,fraction)+Mathf.Sin(angle*6+fraction*4)*.035f;
                vertices.Add(new Vector3(Mathf.Cos(angle)*radius,elevation,Mathf.Sin(angle)*radius));
                uv.Add(new Vector2(.5f+Mathf.Sin(angle*3)*.16f,Mathf.Max(0,elevation)/Height));
                phase.Add(new Vector2(angle*1.21f,-1));
            }
            for(int row=0;row<MantleRows;row++)for(int side=0;side<MantleSegments;side++)
            {
                int a=mantleStart+row*(MantleSegments+1)+side,b=a+1,c=a+MantleSegments+1,d=c+1;
                triangles.Add(a);triangles.Add(c);triangles.Add(b);
                triangles.Add(b);triangles.Add(c);triangles.Add(d);
            }
            var mesh=new Mesh{name="Elite Four · curved turbulent fire sheets"};
            mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,phase);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var bounds=mesh.bounds;bounds.Expand(new Vector3(.7f,.3f,.7f));mesh.bounds=bounds;
            return mesh;
        }

        void CreateEmbers()
        {
            var uv=new Vector2[Embers*4];var triangles=new int[Embers*6];
            for(int i=0;i<Embers;i++)
            {
                int v=i*4,t=i*6;uv[v]=Vector2.zero;uv[v+1]=Vector2.right;
                uv[v+2]=Vector2.up;uv[v+3]=Vector2.one;
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;
                triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            emberMesh=new Mesh{name="Elite Four · ascending fire sparks",vertices=emberVertices,uv=uv,triangles=triangles};
            emberMesh.MarkDynamic();owned.Add(emberMesh);
            var go=new GameObject(emberMesh.name);go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=emberMesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=ember;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }

        void Update()=>UpdateVisuals();

        void UpdateVisuals()
        {
            for(int i=0;i<lights.Length;i++)if(lights[i]!=null)
                lights[i].intensity=2.8f+Mathf.Sin(Time.time*4.3f+i*2.1f)*.28f+Mathf.Sin(Time.time*9.1f+i)*.14f;
            if(emberMesh==null)return;
            for(int i=0;i<Embers;i++)
            {
                float life=Mathf.Repeat(Time.time*(.20f+(i%5)*.013f)+i*.618034f,1);
                float angle=i*2.399963f;
                Vector3 center=Bases[i%4]+new Vector3(Mathf.Cos(angle)*(.33f+life*.58f)+Mathf.Sin(life*5+i)*life*.30f,
                    1.60f+life*4.80f,Mathf.Sin(angle)*(.33f+life*.58f));
                float fade=Mathf.Sin(life*Mathf.PI);float size=.017f+(i%4)*.005f;
                for(int k=0;k<4;k++)
                {
                    emberVertices[i*4+k]=center;
                    emberColors[i*4+k]=new Color(fade,size,0,1);
                }
            }
            emberMesh.vertices=emberVertices;emberMesh.colors=emberColors;
            emberMesh.bounds=new Bounds(new Vector3(0,6.6f,0),new Vector3(29,6.4f,6.5f));
        }

        public bool Validate(out string error)
        {
            error=null;
            if(fire==null||fire.shader==null||!fire.shader.isSupported)error="The living fire shader is unavailable.";
            else if(FlameCount!=4||GetComponentsInChildren<Collider>().Length!=0)
                error="Four gate flames must be present without solid collision.";
            else for(int i=0;i<4;i++)
            {
                if(flames[i]==null||(flames[i].transform.localPosition-Bases[i]).sqrMagnitude>.0001f)
                {error="A fire is detached from its short pillar's golden collar.";break;}
                var bounds=flames[i].GetComponent<MeshFilter>().sharedMesh.bounds;
                if(bounds.size.y<4.9f||bounds.size.x<2.1f||Mathf.Abs(Bases[i].y-2.55f)>.0001f)
                {error="The flames must envelop the full pointed cap from its broad golden collar.";break;}
            }
            return error==null;
        }

        void OnDestroy()
        {
            foreach(var obj in owned)if(obj!=null)
            {if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
            owned.Clear();
        }
    }
}
