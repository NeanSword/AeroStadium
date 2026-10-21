using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Segment contact with a victim's actual animated triangle surfaces. Caller owns Dispose.</summary>
    public sealed partial class AnimatedSurfaceContact : IDisposable
    {
        sealed class Surface
        {
            public Renderer renderer;
            public SkinnedMeshRenderer skin;
            public MeshFilter filter;
            public Mesh source, baked;
            public readonly List<Vector3> local = new List<Vector3>();
            public readonly List<int> indices = new List<int>();
            public readonly List<int> scratch = new List<int>();
            public readonly List<Material> materialSlots = new List<Material>();
            public Vector3[] world;
            public string label;
            public int frame = -1;
            public bool topologyReady, failed;
        }
        readonly List<Surface> surfaces = new List<Surface>();
        bool disposed;
        int lastBakeFrame = -1;
        public bool Ready => !disposed && surfaces.Count > 0;
        public int LastFrame { get; private set; } = -1;
        /// <summary>Distinct frames in which one or more skinned renderers were baked.</summary>
        public int BakedFrames { get; private set; }
        public int RendererBakes { get; private set; }
        /// <summary>Triangle intersection calculations after triangle AABB rejection.</summary>
        public long TriangleTests { get; private set; }
        public float LastSurfaceError { get; private set; } = -1;
        public string LastMeshName { get; private set; }
        public string LastFailure { get; private set; }

        public AnimatedSurfaceContact() { }
        public AnimatedSurfaceContact(GameObject victim) { Initialize(victim); }

        public void Initialize(GameObject victim)
        {
            Release();disposed=false;LastFrame=-1;lastBakeFrame=-1;BakedFrames=RendererBakes=0;TriangleTests=0;
            LastSurfaceError=-1;LastMeshName=LastFailure=null;
            if(victim==null){LastFailure="Victim is missing.";return;}
            // Discovery allocates once; no clones, hierarchy mesh objects or physics colliders are created.
            foreach(var renderer in victim.GetComponentsInChildren<Renderer>(true))
            {
                var skin=renderer as SkinnedMeshRenderer;
                var filter=renderer is MeshRenderer?renderer.GetComponent<MeshFilter>():null;
                Mesh source=skin!=null?skin.sharedMesh:filter!=null?filter.sharedMesh:null;
                if(source==null||source.vertexCount==0||!Visible(renderer))continue;
                if(skin==null&&!source.isReadable){LastFailure="A static victim mesh is not readable: "+source.name;continue;}
                var item=new Surface{renderer=renderer,skin=skin,filter=filter,source=source,label=renderer.name+" / "+source.name};
                item.local.Capacity=source.vertexCount;item.world=new Vector3[source.vertexCount];
                int allIndices=0,largestSubmesh=0;
                for(int submesh=0;submesh<source.subMeshCount;submesh++)
                {
                    int count=(int)source.GetIndexCount(submesh);allIndices+=count;largestSubmesh=Mathf.Max(largestSubmesh,count);
                }
                item.indices.Capacity=allIndices;item.scratch.Capacity=largestSubmesh;
                renderer.GetSharedMaterials(item.materialSlots);
                if(item.materialSlots.Count==0)continue;
                if(skin!=null)
                {
                    item.baked=new Mesh{name="Contact snapshot · "+source.name};item.baked.MarkDynamic();
                }
                // A baked skin mesh is readable even when the imported shared mesh is not CPU-readable.
                // Its topology is therefore captured lazily from the first needed animated snapshot.
                surfaces.Add(item);
            }
        }
        static bool Visible(Renderer renderer)
        {
            return renderer!=null&&renderer.enabled&&renderer.gameObject.activeInHierarchy&&!renderer.forceRenderingOff
                &&renderer.shadowCastingMode!=ShadowCastingMode.ShadowsOnly;
        }
        static Mesh CurrentMesh(Surface item)
        {
            if(item.skin!=null)return item.skin.sharedMesh;
            return item.filter!=null?item.filter.sharedMesh:null;
        }
        bool Refresh(Surface item)
        {
            if(item.failed)return false;
            if(item.frame==Time.frameCount)return item.world.Length>0&&item.indices.Count>0;
            if(CurrentMesh(item)!=item.source){item.failed=true;LastFailure="Victim mesh changed; initialize contact again.";return false;}
            try
            {
                Mesh readable=item.source;
                if(item.skin!=null)
                {
                    // false omits renderer scale from the snapshot; localToWorld below applies it exactly once.
                    item.skin.BakeMesh(item.baked,false);readable=item.baked;RendererBakes++;
                    if(lastBakeFrame!=Time.frameCount){lastBakeFrame=Time.frameCount;BakedFrames++;}
                }
                if(!item.topologyReady)
                {
                    for(int submesh=0;submesh<readable.subMeshCount;submesh++)
                    {
                        if(readable.GetTopology(submesh)!=MeshTopology.Triangles)continue;
                        // Material-less and shadow-only helper geometry must not manufacture a visible hit.
                        if(submesh>=item.materialSlots.Count||item.materialSlots[submesh]==null)continue;
                        readable.GetTriangles(item.scratch,submesh,true);item.indices.AddRange(item.scratch);
                    }
                    item.scratch.Clear();item.topologyReady=true;
                }
                readable.GetVertices(item.local);
                if(item.local.Count!=item.world.Length){item.failed=true;LastFailure="Victim vertex count changed; initialize contact again.";return false;}
                Matrix4x4 matrix=item.renderer.localToWorldMatrix;
                for(int v=0;v<item.world.Length;v++)
                {
                    item.world[v]=matrix.MultiplyPoint3x4(item.local[v]);
                    if(!Finite(item.world[v])){item.failed=true;LastFailure="Nonfinite animated victim vertex.";return false;}
                }
                item.frame=LastFrame=Time.frameCount;return item.indices.Count>0;
            }
            catch(Exception error)
            {
                item.failed=true;LastFailure=error.GetType().Name+": "+error.Message;return false;
            }
        }

        /// <summary>First double-sided triangle intersection. A miss has no proxy or bounds fallback.</summary>
        public bool TrySegment(Vector3 from,Vector3 to,out Vector3 point,out Vector3 normal)
        {
            point=to;normal=Vector3.zero;LastSurfaceError=-1;LastMeshName=null;
            if(!Ready||!Finite(from)||!Finite(to))return false;
            Vector3 delta=to-from;float distance=delta.magnitude;
            if(!Finite(distance)||distance<.000001f)return false;
            Vector3 direction=delta/distance;float nearest=distance+1;bool found=false;
            foreach(var item in surfaces)
            {
                if(!Visible(item.renderer)||item.failed)continue;
                // Renderer bounds only reject work; they can never produce a contact point.
                Bounds broad=item.renderer.bounds;broad.Expand(.002f);
                if(!SegmentAabb(from,direction,distance,broad.min,broad.max)||!Refresh(item))continue;
                var positions=item.world;var indices=item.indices;
                for(int triangle=0;triangle+2<indices.Count;triangle+=3)
                {
                    int ia=indices[triangle],ib=indices[triangle+1],ic=indices[triangle+2];
                    if((uint)ia>=(uint)positions.Length||(uint)ib>=(uint)positions.Length||(uint)ic>=(uint)positions.Length)continue;
                    Vector3 a=positions[ia],b=positions[ib],c=positions[ic];
                    Vector3 lo=Vector3.Min(a,Vector3.Min(b,c)),hi=Vector3.Max(a,Vector3.Max(b,c));
                    if(!SegmentAabb(from,direction,Mathf.Min(distance,nearest),lo,hi))continue;
                    TriangleTests++;
                    if(!Triangle(from,direction,distance,a,b,c,out float at,out Vector3 face)||at>=nearest)continue;
                    nearest=at;point=from+direction*at;
                    normal=Vector3.Dot(face,direction)>0?-face:face;
                    LastSurfaceError=Mathf.Abs(Vector3.Dot(point-a,face));LastMeshName=item.label;found=true;
                }
            }
            return found;
        }
        static bool Triangle(Vector3 origin,Vector3 direction,float length,Vector3 a,Vector3 b,Vector3 c,out float at,out Vector3 normal)
        {
            at=0;normal=Vector3.zero;Vector3 e1=b-a,e2=c-a;
            Vector3 p=Vector3.Cross(direction,e2);float determinant=Vector3.Dot(e1,p);
            if(!Finite(determinant)||Mathf.Abs(determinant)<.0000000001f)return false;
            float inverse=1/determinant;Vector3 offset=origin-a;
            float u=Vector3.Dot(offset,p)*inverse;
            const float edgeTolerance=.000001f;
            if(u< -edgeTolerance||u>1+edgeTolerance)return false;
            Vector3 q=Vector3.Cross(offset,e1);float v=Vector3.Dot(direction,q)*inverse;
            if(v< -edgeTolerance||u+v>1+edgeTolerance)return false;
            float hit=Vector3.Dot(e2,q)*inverse;
            if(!Finite(hit)||hit<0||hit>length)return false;
            normal=Vector3.Cross(e1,e2);float area=normal.magnitude;
            if(!Finite(area)||area<.00000001f)return false;
            normal/=area;at=hit;return true;
        }
        static bool SegmentAabb(Vector3 origin,Vector3 direction,float length,Vector3 lo,Vector3 hi)
        {
            float near=0,far=length;
            return Slab(origin.x,direction.x,lo.x,hi.x,ref near,ref far)
                &&Slab(origin.y,direction.y,lo.y,hi.y,ref near,ref far)
                &&Slab(origin.z,direction.z,lo.z,hi.z,ref near,ref far);
        }
        static bool Slab(float origin,float direction,float lo,float hi,ref float near,ref float far)
        {
            const float margin=.000001f;lo-=margin;hi+=margin;
            if(Mathf.Abs(direction)<.0000001f)return origin>=lo&&origin<=hi;
            float a=(lo-origin)/direction,b=(hi-origin)/direction;
            if(a>b){float swap=a;a=b;b=swap;}
            near=Mathf.Max(near,a);far=Mathf.Min(far,b);return near<=far;
        }
        static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void Release()
        {
            foreach(var item in surfaces)if(item.baked!=null)
            {if(Application.isPlaying)UnityEngine.Object.Destroy(item.baked);else UnityEngine.Object.DestroyImmediate(item.baked);}
            surfaces.Clear();
        }
        public void Dispose(){if(disposed)return;Release();disposed=true;}
    }
}
