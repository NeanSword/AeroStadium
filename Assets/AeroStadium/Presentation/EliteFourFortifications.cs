using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Bounded moat basin and connected, walkable outer fortress. Palette belongs to the arena.</summary>
    public sealed class EliteFourFortifications : MonoBehaviour
    {
        public const float WalkHeight = 4.29f;
        public const float GroundHeight = .35f;
        public const float WallApothem = 30f;
        public int RendererCount { get; private set; }
        public int TriangleCount { get; private set; }
        public int ColliderCount { get; private set; }
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<MeshCollider> contacts = new List<MeshCollider>();
        readonly Dictionary<Material, Draft> batches = new Dictionary<Material, Draft>();
        readonly Draft collision = new Draft();
        ModernArenaMaterials palette;

        public static EliteFourFortifications Build(Transform parent, ModernArenaMaterials materials)
        {
            if (materials == null) throw new ArgumentNullException(nameof(materials));
            var root = new GameObject("Elite Four · moat and fortified grounds");
            root.transform.SetParent(parent, false);
            var result = root.AddComponent<EliteFourFortifications>();
            result.palette = materials;
            result.Create();
            return result;
        }

        void Create()
        {
            // Closed channel floor with two retaining banks: fluid is authored separately above it.
            Ring(palette.DarkStone, 14.30f, 26.30f, -2.80f, -2.35f);
            Ring(palette.DarkStone, 14.20f, 14.52f, -2.80f, -.42f);
            Ring(palette.Stone, 14.16f, 14.58f, -.56f, -.38f);
            Ring(palette.Trim, 14.23f, 14.51f, -.41f, -.37f, false);
            Ring(palette.DarkStone, 26.08f, 32.35f, -2.80f, WalkHeight);
            Ring(palette.Stone, 26.04f, 26.46f, -2.60f, WalkHeight-.16f);
            Ring(palette.FloorAccent, 26.02f, 32.38f, WalkHeight-.16f, WalkHeight);
            Ring(palette.Trim, 26.03f, 26.19f, WalkHeight-.22f, WalkHeight-.15f, false);
            Ring(palette.DarkStone, 32.25f, 56f, -.65f, GroundHeight);
            Ring(palette.Floor, 32.30f, 55.90f, GroundHeight-.12f, GroundHeight);
            Ring(palette.Stone, 55.85f, 56.18f, .10f, .43f);

            for (int edge = 0; edge < 8; edge++)
            {
                Vector3 a = Corner(WallApothem, edge), b = Corner(WallApothem, edge+1);
                float length = Vector3.Distance(a,b);
                bool gateway = edge == 3 || edge == 7;
                if (gateway)
                {
                    float gap = 3.45f/length;
                    WallBay(a, b, 0, .5f-gap);
                    WallBay(a, b, .5f+gap, 1);
                }
                else WallBay(a,b,0,1);

                // Vertical foundations and repeated buttresses give the curtain a grounded silhouette.
                Vector3 outward = ((a+b)*.5f).normalized;
                for (int j=0; j<=7; j++)
                {
                    float t=(j+.5f)/8;
                    if (gateway && Mathf.Abs(t-.5f)*length < 4.3f) continue;
                    Vector3 p=Vector3.Lerp(a,b,t)+outward*2.15f;
                    Box(palette.Stone,p+Vector3.up*1.75f,new Vector3(.95f,4.2f,1.1f),Quaternion.LookRotation(outward),.09f);
                    Box(palette.Trim,p+Vector3.up*3.81f,new Vector3(1.08f,.11f,1.18f),Quaternion.LookRotation(outward),.025f,false);
                }
                Tower(Corner(WallApothem+.15f,edge));
            }
            for (int side=-1; side<=1; side+=2) Approach(side);
            Publish();
            batches.Clear();
        }

        void WallBay(Vector3 a, Vector3 b, float begin, float end)
        {
            Vector3 p=Vector3.Lerp(a,b,begin), q=Vector3.Lerp(a,b,end);
            Vector3 outward=((a+b)*.5f).normalized;
            float length=Vector3.Distance(p,q);
            Quaternion rotation=Quaternion.LookRotation(outward);
            // Masonry courses use real bevels and recessed joints, while the solid core seals collision.
            Box(palette.DarkStone,(p+q)*.5f+Vector3.up*5.39f,new Vector3(length,2.2f,1.22f),rotation,0);
            int blocks=Mathf.CeilToInt(length/2.6f);
            for(int course=0;course<3;course++)
            {
                for(int j=0;j<blocks;j++)
                {
                    float start=j/(float)blocks, finish=(j+1)/(float)blocks;
                    Vector3 c=Vector3.Lerp(p,q,(start+finish)*.5f);
                    Box(palette.Stone,c+Vector3.up*(4.66f+course*.73f),new Vector3(length/blocks-.032f,.70f,1.30f),rotation,.045f,false);
                }
            }
            Box(palette.Trim,(p+q)*.5f+Vector3.up*6.51f,new Vector3(length,.11f,1.42f),rotation,.028f,false);
            Box(palette.Stone,(p+q)*.5f+Vector3.up*6.64f,new Vector3(length+.08f,.18f,1.54f),rotation,.04f);
            int merlons=Mathf.Max(1,Mathf.FloorToInt(length/2.3f));
            for(int j=0;j<merlons;j++)
            {
                Vector3 c=Vector3.Lerp(p,q,(j+.5f)/merlons);
                Box(palette.Stone,c+Vector3.up*7.03f,new Vector3(1.04f,.78f,1.42f),rotation,.07f);
                Box(palette.Trim,c+Vector3.up*7.44f,new Vector3(1.11f,.07f,1.49f),rotation,.02f,false);
            }
        }

        void Tower(Vector3 c)
        {
            Polygon(palette.DarkStone,c,2.75f,-.65f,1.10f);
            Polygon(palette.Stone,c,2.48f,1.10f,7.62f);
            for(int k=0;k<6;k++) Polygon(palette.Trim,c,2.51f,1.20f+k*1.08f,1.25f+k*1.08f,false);
            Polygon(palette.Stone,c,2.70f,7.62f,7.91f);
            Polygon(palette.FloorAccent,c,2.40f,7.92f,7.99f);
            for(int j=0;j<8;j++)
            {
                float angle=j*Mathf.PI/4;
                Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Box(palette.Stone,c+radial*2.27f+Vector3.up*8.35f,new Vector3(1.00f,.80f,.70f),Quaternion.LookRotation(radial),.06f);
                Box(palette.Gold,c+radial*2.27f+Vector3.up*8.78f,new Vector3(1.04f,.06f,.74f),Quaternion.LookRotation(radial),.018f,false);
            }
        }

        void Approach(int side)
        {
            // Existing bridge finishes at x=23.58, y=4.29. The overlap is coplanar and supported.
            Box(palette.DarkStone,new Vector3(side*29.75f,.87f,0),new Vector3(12.50f,6.84f,6.65f),Quaternion.identity,.08f);
            Box(palette.FloorAccent,new Vector3(side*29.75f,WalkHeight-.12f,0),new Vector3(12.50f,.24f,6.65f),Quaternion.identity,0);
            for(int z=-1;z<=1;z+=2)
            {
                Vector3 c=new Vector3(side*30f,0,z*4.32f);
                Box(palette.Stone,c+Vector3.up*3.78f,new Vector3(2.20f,7.56f,1.82f),Quaternion.identity,.09f);
                Box(palette.Trim,c+Vector3.up*6.0f,new Vector3(2.35f,.18f,1.98f),Quaternion.identity,.035f,false);
                Box(palette.Gold,c+Vector3.up*7.64f,new Vector3(2.41f,.15f,2.07f),Quaternion.identity,.035f,false);
                Box(palette.Stone,new Vector3(side*34.0f,WalkHeight+.36f,z*3.37f),new Vector3(3.9f,.72f,.32f),Quaternion.identity,.035f);
                Box(palette.Trim,new Vector3(side*34.0f,WalkHeight+.77f,z*3.37f),new Vector3(3.94f,.10f,.40f),Quaternion.identity,.025f,false);
            }
            Box(palette.Stone,new Vector3(side*30f,7.34f,0),new Vector3(2.19f,.51f,10.56f),Quaternion.identity,.075f);
            Box(palette.Gold,new Vector3(side*30f,7.66f,0),new Vector3(2.27f,.13f,10.64f),Quaternion.identity,.035f,false);

            const int steps=22;
            const float run=9f;
            float rise=(WalkHeight-GroundHeight)/steps;
            for(int step=0;step<steps;step++)
            {
                float top=WalkHeight-(step+1)*rise;
                float x=36f+(step+.5f)*run/steps;
                Box(palette.Stone,new Vector3(side*x,(top+GroundHeight-.1f)*.5f,0),new Vector3(run/steps+.001f,top-GroundHeight+.1f,7.48f),Quaternion.identity,0);
                // Nosing is a flush bronze strip, never a raised lip on the traversal surface.
                Box(palette.Trim,new Vector3(side*(36f+step*run/steps+.035f),top-.006f,0),new Vector3(.045f,.012f,7.35f),Quaternion.identity,0,false);
                for(int z=-1;z<=1;z+=2)
                    Box(palette.Stone,new Vector3(side*x,top+.28f,z*3.98f),new Vector3(run/steps+.02f,.56f,.40f),Quaternion.identity,.028f);
            }
            Box(palette.FloorAccent,new Vector3(side*49f,GroundHeight-.06f,0),new Vector3(8f,.12f,17f),Quaternion.identity,0);
            for(int z=-1;z<=1;z+=2)
            {
                Box(palette.Stone,new Vector3(side*48f,.62f,z*8.5f),new Vector3(6f,.54f,.55f),Quaternion.identity,.07f);
                Box(palette.Trim,new Vector3(side*48f,.92f,z*8.5f),new Vector3(6.1f,.08f,.62f),Quaternion.identity,.025f,false);
            }
        }

        /// <summary>Ordered local samples from the bridge landing to the exterior forecourt.</summary>
        public Vector3[] RoutePoints(int side=1)
        {
            side=side<0?-1:1;
            var points=new List<Vector3>();
            for(int i=0;i<=25;i++) points.Add(new Vector3(side*(23.58f+i*(36f-23.58f)/25),WalkHeight,0));
            for(int i=0;i<22;i++) points.Add(new Vector3(side*(36f+(i+.5f)*9f/22),WalkHeight-(i+1)*(WalkHeight-GroundHeight)/22,0));
            points.Add(new Vector3(side*49f,GroundHeight,0));
            return points.ToArray();
        }

        public bool ValidateGeometry(out string report)
        {
            bool valid=RendererCount>0&&ColliderCount==1;
            foreach(Mesh mesh in ownedMeshes)
            {
                valid &= mesh!=null&&mesh.vertexCount>0&&mesh.triangles.Length%3==0;
                if(mesh==null)continue;
                foreach(Vector3 p in mesh.vertices) valid &= !float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude);
            }
            Physics.SyncTransforms();
            float largestContactError=0;
            for(int side=-1;side<=1;side+=2)
            foreach(Vector3 local in RoutePoints(side))
            {
                Vector3 origin=transform.TransformPoint(local+Vector3.up*.6f);
                var ray=new Ray(origin,-transform.up);
                bool found=false;
                foreach(var contact in contacts)
                if(contact.Raycast(ray,out RaycastHit hit,1.25f))
                {
                    float error=Mathf.Abs(transform.InverseTransformPoint(hit.point).y-local.y);
                    largestContactError=Mathf.Max(largestContactError,error);
                    found |= error<.025f;
                }
                valid &= found;
            }
            report=$"Fortifications valid={valid}; renderers={RendererCount}; triangles={TriangleCount}; colliders={ColliderCount}; two continuous bridge-to-forecourt routes; contact error={largestContactError:F4}m; bounded moat 14.5–26m apothems.";
            return valid;
        }

        Draft Batch(Material material)
        {
            if(!batches.TryGetValue(material,out var draft)){draft=new Draft();batches.Add(material,draft);}
            return draft;
        }
        static Vector3 Corner(float apothem,int index)
        {
            float angle=(22.5f+index*45)*Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(apothem/Mathf.Cos(Mathf.PI/8));
        }
        void Ring(Material material,float inner,float outer,float bottom,float top,bool solid=true)
        {
            var d=Batch(material);
            for(int i=0;i<8;i++)
            {
                Vector3 a=Corner(inner,i),b=Corner(inner,i+1),c=Corner(outer,i+1),e=Corner(outer,i);
                Prism(d,a,b,c,e,bottom,top);
                if(solid)Prism(collision,a,b,c,e,bottom,top);
            }
        }
        void Polygon(Material material,Vector3 center,float radius,float bottom,float top,bool solid=true)
        {
            // Eight triangular prisms make a closed, low-cost octagonal tower.
            for(int i=0;i<8;i++)
            {
                Vector3 a=center+Corner(radius*Mathf.Cos(Mathf.PI/8),i),b=center+Corner(radius*Mathf.Cos(Mathf.PI/8),i+1);
                Prism(Batch(material),center,b,a,a,bottom,top);
                if(solid)Prism(collision,center,b,a,a,bottom,top);
            }
        }
        static void Prism(Draft d,Vector3 a,Vector3 b,Vector3 c,Vector3 e,float bottom,float top)
        {
            Vector3[] p={a,b,c,e};
            d.Face(a+Vector3.up*top,b+Vector3.up*top,c+Vector3.up*top,e+Vector3.up*top,Vector3.up);
            d.Face(a+Vector3.up*bottom,e+Vector3.up*bottom,c+Vector3.up*bottom,b+Vector3.up*bottom,Vector3.down);
            for(int i=0;i<4;i++)
            {
                Vector3 u=p[i],v=p[(i+1)%4];
                Vector3 hint=Vector3.Cross(v-u,Vector3.up);
                d.Face(u+Vector3.up*bottom,v+Vector3.up*bottom,v+Vector3.up*top,u+Vector3.up*top,hint);
            }
        }
        void Box(Material material,Vector3 center,Vector3 size,Quaternion rotation,float bevel,bool solid=true)
        {
            var d=Batch(material); Vector3 h=size*.5f;
            // Chamfered rectangular body: compact real highlights without per-piece renderers.
            float e=Mathf.Min(bevel,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.8f);
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3;Vector3[] p=new Vector3[4];
                int[] us={-1,1,1,-1},vs={-1,-1,1,1};
                for(int k=0;k<4;k++){p[k][axis]=h[axis]*sign;p[k][u]=(h[u]-e)*us[k];p[k][v]=(h[v]-e)*vs[k];}
                Vector3 n=Vector3.zero;n[axis]=sign;
                d.Face(center+rotation*p[0],center+rotation*p[1],center+rotation*p[2],center+rotation*p[3],rotation*n);
                if(solid)collision.Face(center+rotation*p[0],center+rotation*p[1],center+rotation*p[2],center+rotation*p[3],rotation*n);
            }
            if(e<=0)return;
            for(int axis=0;axis<3;axis++)for(int su=-1;su<=1;su+=2)for(int sv=-1;sv<=1;sv+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3;Vector3[] p=new Vector3[4];
                for(int k=0;k<4;k++){p[k][axis]=(k<2?-1:1)*(h[axis]-e);p[k][u]=su*(h[u]-(k==0||k==3?0:e));p[k][v]=sv*(h[v]-(k==0||k==3?e:0));}
                Vector3 n=Vector3.zero;n[u]=su;n[v]=sv;
                d.Face(center+rotation*p[0],center+rotation*p[1],center+rotation*p[2],center+rotation*p[3],rotation*n);
                if(solid)collision.Face(center+rotation*p[0],center+rotation*p[1],center+rotation*p[2],center+rotation*p[3],rotation*n);
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                Vector3 signs=new Vector3(x,y,z),a=Vector3.Scale(h-Vector3.one*e,signs),b=a,c=a;
                a.x=h.x*x;b.y=h.y*y;c.z=h.z*z;
                d.Face(center+rotation*a,center+rotation*b,center+rotation*c,rotation*signs);
                if(solid)collision.Face(center+rotation*a,center+rotation*b,center+rotation*c,rotation*signs);
            }
        }
        void Publish()
        {
            foreach(var pair in batches)
            {
                Mesh mesh=pair.Value.Create("Elite Four fortifications · "+pair.Key.name);ownedMeshes.Add(mesh);
                var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                RendererCount++;TriangleCount+=mesh.triangles.Length/3;
            }
            Mesh contactMesh=collision.Create("Elite Four fortifications · continuous stone contact");ownedMeshes.Add(contactMesh);
            var contactObject=new GameObject(contactMesh.name);contactObject.transform.SetParent(transform,false);
            var contact=contactObject.AddComponent<MeshCollider>();contact.sharedMesh=contactMesh;
            contacts.Add(contact);ColliderCount++;
        }
        void OnDestroy()
        {
            foreach(var mesh in ownedMeshes)if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
            ownedMeshes.Clear();contacts.Clear();
        }
        sealed class Draft
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();readonly List<int> indices=new List<int>();
            public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 hint)
            {
                Vector3 n=Vector3.Cross(b-a,c-a).normalized;if(n.sqrMagnitude<.5f)return;
                if(Vector3.Dot(n,hint)<0){var temp=b;b=c;c=temp;n=-n;}
                int k=vertices.Count;Vector3 tangent=Mathf.Abs(n.y)>.8f?Vector3.right:Vector3.Cross(Vector3.up,n).normalized;
                Vector3 bitangent=Vector3.Cross(n,tangent);
                foreach(var p in new[]{a,b,c}){vertices.Add(p);normals.Add(n);uv.Add(new Vector2(Vector3.Dot(p,tangent),Vector3.Dot(p,bitangent))*.5f);}
                indices.Add(k);indices.Add(k+1);indices.Add(k+2);
            }
            public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 hint){Face(a,b,c,hint);Face(a,c,d,hint);}
            public Mesh Create(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
