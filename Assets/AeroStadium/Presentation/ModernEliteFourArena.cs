using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Authored, open-air reconstruction of Stadium 1's eight-spire Elite Four courtyard.</summary>
    public sealed class ModernEliteFourArena : MonoBehaviour
    {
        public const string ArenaKey = "stadium1_elite4_modern";
        public Vector3 FloorCenter => transform.TransformPoint(Vector3.zero);
        public Vector3 BattleAxis => transform.TransformDirection(Vector3.right).normalized;
        public float BattleLength => 22f;
        public float BattleWidth => 14f;
        public int RendererCount { get; private set; }
        public int TriangleCount { get; private set; }
        public int ColliderCount { get; private set; }
        public BoxCollider PlayableFloor { get; private set; }
        public Bounds LocalArchitectureBounds { get; private set; }
        public bool GeometryValid => ValidateGeometry(out _);

        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly Dictionary<Material, Draft> batches = new Dictionary<Material, Draft>();
        readonly HashSet<Material> solidBatches = new HashSet<Material>();
        ModernArenaMaterials materials;
        public EliteFourFortifications Fortifications { get; private set; }
        public EliteFourMoat Moat { get; private set; }
        public EliteFourPillarFlames Flames { get; private set; }
        public ArenaFireAudio FireAudio {get;private set;}
        PhysicsMaterial floorPhysics;
        static readonly Vector2[] Footprint = {
            new Vector2(12.84f,4.33f),new Vector2(11.04f,4.57f),new Vector2(12.15f,6.02f),
            new Vector2(6.02f,12.15f),new Vector2(4.57f,11.04f),new Vector2(4.33f,12.84f),
            new Vector2(-4.33f,12.84f),new Vector2(-4.57f,11.04f),new Vector2(-6.02f,12.15f),
            new Vector2(-12.15f,6.02f),new Vector2(-11.04f,4.57f),new Vector2(-12.84f,4.33f),
            new Vector2(-12.84f,-4.33f),new Vector2(-11.04f,-4.57f),new Vector2(-12.15f,-6.02f),
            new Vector2(-6.02f,-12.15f),new Vector2(-4.57f,-11.04f),new Vector2(-4.33f,-12.84f),
            new Vector2(4.33f,-12.84f),new Vector2(4.57f,-11.04f),new Vector2(6.02f,-12.15f),
            new Vector2(12.15f,-6.02f),new Vector2(11.04f,-4.57f),new Vector2(12.84f,-4.33f)
        };

        public static ModernEliteFourArena Build(Transform parent)
        {
            var go = new GameObject("Elite Four · reconstructed courtyard");
            go.transform.SetParent(parent, false);
            var arena = go.AddComponent<ModernEliteFourArena>();
            arena.Create();
            return arena;
        }

        void Create()
        {
            materials = new ModernArenaMaterials();
            floorPhysics = new PhysicsMaterial("Elite Four honed stone contact") {
                dynamicFriction = .68f, staticFriction = .76f, bounciness = 0,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            BuildGround();
            BuildSpiresAndBarriers();
            BuildApproaches();
            Fortifications=EliteFourFortifications.Build(transform,materials);
            Moat=EliteFourMoat.Build(transform,materials.Lava);
            Flames=EliteFourPillarFlames.Build(transform);
            FireAudio=ArenaFireAudio.BuildAmbient(transform);
            foreach (var pair in batches)
            {
                Mesh mesh = pair.Value.Create("Elite Four · " + pair.Key.name);
                ownedMeshes.Add(mesh); TriangleCount += mesh.triangles.Length / 3;
                var go = new GameObject(mesh.name); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
                if (solidBatches.Contains(pair.Key))
                {
                    var collider = go.AddComponent<MeshCollider>(); collider.sharedMesh = mesh;
                    collider.sharedMaterial = floorPhysics; ColliderCount++;
                }
                if (RendererCount++ == 0) LocalArchitectureBounds = mesh.bounds;
                else { var bounds = LocalArchitectureBounds; bounds.Encapsulate(mesh.bounds); LocalArchitectureBounds = bounds; }
            }
            // Dedicated coplanar contact plane covers only the verified rectangle inside the native footprint.
            var floor = new GameObject("Playable floor · level 0"); floor.transform.SetParent(transform, false);
            PlayableFloor = floor.AddComponent<BoxCollider>();
            PlayableFloor.center = new Vector3(0, -.2f, 0);
            PlayableFloor.size = new Vector3(BattleLength, .4f, BattleWidth);
            PlayableFloor.sharedMaterial = floorPhysics; ColliderCount++;
            RendererCount+=Fortifications.RendererCount+Moat.RendererCount+Flames.RendererCount;
            TriangleCount+=Fortifications.TriangleCount+Moat.TriangleCount+Flames.TriangleCount;
            ColliderCount+=Fortifications.ColliderCount;
            batches.Clear(); solidBatches.Clear();
        }

        void BuildGround()
        {
            // Native platform is a notched octagon. Keep every perimeter point and its eight buttress sockets.
            var floor = Batch(materials.Floor);
            var skirt = Batch(materials.DarkStone, true);
            for (int i = 0; i < Footprint.Length; i++)
            {
                Vector2 a = Footprint[i], b = Footprint[(i + 1) % Footprint.Length];
                floor.Face(Vector3.down*.02f, At(b,-.02f), At(a,-.02f), Vector3.up);
                skirt.Face(At(a,0),At(b,0),At(b,-.65f),At(a,-.65f),At((a+b)*.5f,0));
                Beam(materials.Trim, At(a,-.11f), At(b,-.11f), .07f, .11f);
                Beam(materials.Gold, At(a,-.025f), At(b,-.025f), .025f, .025f);
            }
            // Flush stone paving: tight joints, bevelled edges, alternating close tones rather than painted texture.
            for (int row = -9; row <= 9; row++) for (int col = -10; col <= 10; col++)
            {
                float x = col * 1.19f + (row % 2 == 0 ? 0 : .595f), z = row * 1.4f;
                if (Mathf.Abs(x)<5.35f && Mathf.Abs(z)<5.35f) continue;
                if (!Inside(x-.58f,z-.685f) || !Inside(x+.58f,z+.685f) ||
                    !Inside(x-.58f,z+.685f) || !Inside(x+.58f,z-.685f)) continue;
                Box((row+col)%4==0 ? materials.FloorAccent : materials.Floor,
                    new Vector3(x,-.045f,z),new Vector3(1.17f,.09f,1.38f),.018f);
            }
            // True geometry inlaid crest: diameter and X-axis halves match the recovered UV mapping.
            Disk(materials.DarkStone, 5.29f, .002f, -90, 270);
            Disk(materials.Gold, 5.08f, .004f, 90, 270);
            Disk(materials.FloorAccent, 5.08f, .004f, -90, 90);
            Ring(materials.Trim, 5.13f,5.29f,.006f);
            Box(materials.DarkStone,new Vector3(0,.006f,0),new Vector3(.28f,.006f,10.18f),.003f);
            Ring(materials.DarkStone,.94f,1.18f,.01f);
            Disk(materials.Gold,.94f,.012f,0,360);
            Disk(materials.FloorAccent,.70f,.014f,0,360);
            // Full polygon has a single planar collider independent of the cosmetic paving and relief.
            var contact=new Draft();
            for(int i=0;i<Footprint.Length;i++)contact.Face(Vector3.zero,At(Footprint[i],0),At(Footprint[(i+1)%Footprint.Length],0),Vector3.up);
            Mesh contactMesh=contact.Create("Elite Four · full native polygon contact");ownedMeshes.Add(contactMesh);
            var contactObject=new GameObject(contactMesh.name);contactObject.transform.SetParent(transform,false);
            var contactCollider=contactObject.AddComponent<MeshCollider>();contactCollider.sharedMesh=contactMesh;
            contactCollider.sharedMaterial=floorPhysics;ColliderCount++;
            // Eight corners continue into a low structural apron, with an open horizon above it.
            for(int i=0;i<8;i++)
            {
                float angle=(22.5f+i*45)*Mathf.Deg2Rad;
                Vector3 radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var q=Quaternion.LookRotation(radial);
                Box(materials.DarkStone,radial*14.1f+Vector3.down*.68f,new Vector3(3.8f,.32f,3.5f),.11f,q,true);
                Box(materials.Stone,radial*14.1f+Vector3.down*.49f,new Vector3(3.65f,.13f,3.35f),.05f,q,true);
            }
        }

        void BuildSpiresAndBarriers()
        {
            var centers=new Vector3[8];
            for(int i=0;i<8;i++)
            {
                float a=(22.5f+i*45)*Mathf.Deg2Rad;
                Vector3 radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                centers[i]=radial*13.0f;
                Quaternion q=Quaternion.LookRotation(radial);
                Vector3 c=centers[i];
                Box(materials.Stone,c+Vector3.up*.19f,new Vector3(1.9f,.38f,2.15f),.09f,q,true);
                Box(materials.Trim,c+Vector3.up*.44f,new Vector3(1.78f,.13f,2.04f),.035f,q);
                // Segment breaks retain native y=2.5 / 5 divisions; pointed caps retain the 7.5m silhouette.
                Frustum(materials.Stone,c,q,.49f,2.47f,1.63f,1.18f,1.9f,1.56f,true);
                Box(materials.Gold,c+Vector3.up*2.5f,new Vector3(1.33f,.14f,1.7f),.045f,q);
                Frustum(materials.Stone,c,q,2.58f,4.93f,1.16f,.91f,1.55f,1.23f,true);
                Box(materials.Trim,c+Vector3.up*5,new Vector3(1.08f,.17f,1.38f),.04f,q);
                Frustum(materials.Stone,c,q,5.09f,7.5f,.89f,.04f,1.21f,.04f,true);
                // Recessed bronze channels and masonry course joints catch real sunset highlights.
                for(int band=1;band<=7;band++)
                    Box(materials.Trim,c+Vector3.up*(.65f+band*.55f)-radial*.73f,
                        new Vector3(Mathf.Lerp(1.25f,.76f,band/7f),.028f,.022f),.005f,q);
                Box(materials.Trim,c+Vector3.up*1.3f-radial*.91f,new Vector3(.49f,.64f,.035f),.018f,q);
                Box(materials.Gold,c+Vector3.up*1.3f-radial*.94f,new Vector3(.36f,.50f,.024f),.014f,q);
            }
            // Eight physical bronze lattice bays; the ±X sides retain central gateway openings.
            for(int i=0;i<8;i++)
            {
                Vector3 a=At(Footprint[(i*3+2)%Footprint.Length],0), b=At(Footprint[(i*3+3)%Footprint.Length],0);
                Vector3 dir=(b-a).normalized;
                if(i==3 || i==7)
                {
                    Vector3 center=(a+b)*.5f;
                    Barrier(a,center-dir*1.76f); Barrier(center+dir*1.76f,b);
                }
                else Barrier(a,b);
            }
        }

        void Barrier(Vector3 a,Vector3 b)
        {
            if((b-a).sqrMagnitude<.01f)return;
            Beam(materials.Trim,a+Vector3.up*.17f,b+Vector3.up*.17f,.14f,.14f,true);
            Beam(materials.Gold,a+Vector3.up*1.92f,b+Vector3.up*1.92f,.14f,.10f,true);
            Beam(materials.Trim,a+Vector3.up*1.82f,b+Vector3.up*1.82f,.06f,.05f);
            int count=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(a,b)/.85f));
            for(int i=0;i<count;i++)
            {
                Vector3 p=Vector3.Lerp(a,b,i/(float)count),n=Vector3.Lerp(a,b,(i+1)/(float)count);
                Beam(materials.Trim,p+Vector3.up*.28f,n+Vector3.up*1.76f,.045f,.045f,true);
                Beam(materials.Trim,p+Vector3.up*1.76f,n+Vector3.up*.28f,.045f,.045f,true);
                Box(materials.Gold,(p+n)*.5f+Vector3.up*1.02f,new Vector3(.075f,.075f,.075f),.015f);
            }
        }

        void BuildApproaches()
        {
            for(int side=-1;side<=1;side+=2)
            {
                Quaternion q=Quaternion.Euler(0,side*90,0);
                Vector3 gate=new Vector3(side*12.49f,0,0);
                for(int z=-1;z<=1;z+=2)
                {
                    Vector3 p=gate+Vector3.forward*z*1.35f;
                    Box(materials.Stone,p+Vector3.up*1.3f,new Vector3(.90f,2.6f,.56f),.055f,Quaternion.identity,true);
                    Box(materials.Gold,p+Vector3.up*2.55f,new Vector3(1.03f,.14f,.71f),.035f);
                    Frustum(materials.Stone,p,Quaternion.identity,2.64f,4.2f,.64f,.06f,.59f,.06f,true);
                    Box(materials.Trim,p+Vector3.up*1.5f+Vector3.left*side*.46f,new Vector3(.025f,1.4f,.33f),.007f);
                }
                Vector3 start=new Vector3(side*13.78f,.19f,0), end=new Vector3(side*23.58f,4.29f,0);
                Beam(materials.Stone,new Vector3(side*12.49f,-.075f,0),start-Vector3.up*.075f,2.12f,.15f,true);
                Box(materials.Stone,new Vector3(side*24.20f,4.10f,0),new Vector3(1.44f,.38f,2.12f),.035f,Quaternion.identity,true);
                // Solid ramp, not invented stair seating. Fine transverse joints are flush with the slope.
                var ramp=Batch(materials.Stone,true);
                Vector3 sz=Vector3.forward*1.06f;
                ramp.Face(start-sz,end-sz,end+sz,start+sz,Vector3.up);
                ramp.Face(start+sz,end+sz,new Vector3(end.x,-.50f,sz.z),new Vector3(start.x,-.50f,sz.z),Vector3.forward);
                ramp.Face(end-sz,start-sz,new Vector3(start.x,-.50f,-sz.z),new Vector3(end.x,-.50f,-sz.z),Vector3.back);
                ramp.Face(end+sz,end-sz,new Vector3(end.x,-.50f,-sz.z),new Vector3(end.x,-.50f,sz.z),Vector3.right*side);
                for(int joint=1;joint<17;joint++)
                {
                    Vector3 p=Vector3.Lerp(start,end,joint/17f)+Vector3.up*.004f;
                    Beam(materials.Trim,p-sz*.97f,p+sz*.97f,.018f,.018f);
                }
                for(int z=-1;z<=1;z+=2)
                {
                    Beam(materials.Trim,start+sz*z+Vector3.up*.05f,end+sz*z+Vector3.up*.05f,.13f,.13f,true);
                    Beam(materials.Gold,start+sz*z+Vector3.up*.93f,end+sz*z+Vector3.up*.93f,.065f,.065f,true);
                    for(int post=0;post<=8;post++)
                    {
                        Vector3 p=Vector3.Lerp(start,end,post/8f)+sz*z;
                        Beam(materials.Trim,p,p+Vector3.up*.93f,.075f,.075f,true);
                    }
                }
                Vector3 outer=new Vector3(side*24.7f,0,0);
                Box(materials.DarkStone,outer+Vector3.up*.3f,new Vector3(2.3f,.6f,4.7f),.12f,Quaternion.identity,true);
                for(int z=-1;z<=1;z+=2)
                {
                    Vector3 p=outer+Vector3.forward*z*1.57f;
                    Frustum(materials.Stone,p,Quaternion.identity,.6f,8.9f,1.52f,1.11f,1.05f,.83f,true);
                    Box(materials.Trim,p+Vector3.up*5.35f,new Vector3(1.43f,.18f,1.1f),.045f);
                    Box(materials.Gold,p+Vector3.up*8.96f,new Vector3(1.31f,.20f,1.02f),.055f);
                    Frustum(materials.DarkStone,p,Quaternion.identity,9.06f,z<0?9.7f:10.12f,1.2f,.16f,.94f,.13f,true);
                }
                Beam(materials.Stone,outer+Vector3.up*8.62f+Vector3.forward*-1.57f,
                    outer+Vector3.up*8.62f+Vector3.forward*1.57f,.58f,.55f,true);
                Beam(materials.Gold,outer+Vector3.up*8.95f+Vector3.forward*-1.62f,
                    outer+Vector3.up*8.95f+Vector3.forward*1.62f,.15f,.13f);
            }
        }

        Draft Batch(Material material,bool solid=false)
        {
            if(!batches.TryGetValue(material,out var d)){d=new Draft();batches.Add(material,d);}
            if(solid)solidBatches.Add(material); return d;
        }
        static Vector3 At(Vector2 p,float y)=>new Vector3(p.x,y,p.y);
        static bool Inside(float x,float z)
        {
            bool inside=false;
            for(int i=0,j=Footprint.Length-1;i<Footprint.Length;j=i++)
            {
                Vector2 a=Footprint[i],b=Footprint[j];
                if((a.y>z)!=(b.y>z)&&x<(b.x-a.x)*(z-a.y)/(b.y-a.y)+a.x)inside=!inside;
            }
            return inside;
        }
        void Frustum(Material m,Vector3 c,Quaternion q,float bottom,float top,float w0,float w1,float d0,float d1,bool solid)
        {
            Vector3[] low={new Vector3(-w0/2,bottom,-d0/2),new Vector3(w0/2,bottom,-d0/2),new Vector3(w0/2,bottom,d0/2),new Vector3(-w0/2,bottom,d0/2)};
            Vector3[] high={new Vector3(-w1/2,top,-d1/2),new Vector3(w1/2,top,-d1/2),new Vector3(w1/2,top,d1/2),new Vector3(-w1/2,top,d1/2)};
            var batch=Batch(m,solid);
            for(int i=0;i<4;i++){int n=(i+1)%4;batch.Face(c+q*low[i],c+q*low[n],c+q*high[n],c+q*high[i],q*((low[i]+low[n])*.5f-Vector3.up*bottom));}
            batch.Face(c+q*high[0],c+q*high[1],c+q*high[2],c+q*high[3],Vector3.up);
            batch.Face(c+q*low[3],c+q*low[2],c+q*low[1],c+q*low[0],Vector3.down);
        }
        void Beam(Material m,Vector3 a,Vector3 b,float width,float depth,bool solid=false)
        {
            Vector3 delta=b-a;if(delta.sqrMagnitude<.00001f)return;
            Box(m,(a+b)*.5f,new Vector3(width,depth,delta.magnitude),Mathf.Min(width,depth)*.2f,
                Quaternion.LookRotation(delta,Mathf.Abs(Vector3.Dot(delta.normalized,Vector3.up))>.99f?Vector3.forward:Vector3.up),solid);
        }
        void Box(Material m,Vector3 center,Vector3 size,float bevel,Quaternion rotation=default,bool solid=false)
        {
            if(rotation.w==0&&rotation.x==0&&rotation.y==0&&rotation.z==0)rotation=Quaternion.identity;
            var d=Batch(m,solid);Vector3 h=size*.5f;
            float e=Mathf.Min(bevel,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.85f);
            // Six inset planes, twelve chamfers, eight corner triangles. Flat faces remain genuinely flat.
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3;Vector3[] p=new Vector3[4];
                int[] us={-1,1,1,-1},vs={-1,-1,1,1};
                for(int k=0;k<4;k++){p[k][axis]=h[axis]*sign;p[k][u]=(h[u]-e)*us[k];p[k][v]=(h[v]-e)*vs[k];}
                Vector3 normal=Vector3.zero;normal[axis]=sign;
                d.Face(center+rotation*p[0],center+rotation*p[1],center+rotation*p[2],center+rotation*p[3],rotation*normal);
            }
            for(int axis=0;axis<3;axis++)for(int su=-1;su<=1;su+=2)for(int sv=-1;sv<=1;sv+=2)
            {
                int u=(axis+1)%3,v=(axis+2)%3;Vector3[] p=new Vector3[4];
                for(int k=0;k<4;k++){p[k][axis]=(k<2?-1:1)*(h[axis]-e);p[k][u]=su*(h[u]-(k==0||k==3?0:e));p[k][v]=sv*(h[v]-(k==0||k==3?e:0));}
                Vector3 normal=Vector3.zero;normal[u]=su;normal[v]=sv;
                d.Face(center+rotation*p[0],center+rotation*p[1],center+rotation*p[2],center+rotation*p[3],rotation*normal);
            }
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                Vector3 signs=new Vector3(x,y,z),a=Vector3.Scale(h-Vector3.one*e,signs),b=a,c=a;
                a.x=h.x*x;b.y=h.y*y;c.z=h.z*z;
                d.Face(center+rotation*a,center+rotation*b,center+rotation*c,rotation*signs);
            }
        }
        void Disk(Material m,float radius,float y,float start,float end)
        {
            int segments=Mathf.CeilToInt((end-start)/3);var d=Batch(m);
            for(int i=0;i<segments;i++)
            {float a=Mathf.Lerp(start,end,i/(float)segments)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1)/(float)segments)*Mathf.Deg2Rad;
                d.Face(new Vector3(0,y,0),new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius),new Vector3(Mathf.Cos(b)*radius,y,Mathf.Sin(b)*radius),Vector3.up);}
        }
        void Ring(Material m,float inner,float outer,float y)
        {
            var d=Batch(m);for(int i=0;i<128;i++)
            {float a=i*Mathf.PI/64,b=(i+1)*Mathf.PI/64;Vector3 p=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),q=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                d.Face(p*inner+Vector3.up*y,p*outer+Vector3.up*y,q*outer+Vector3.up*y,q*inner+Vector3.up*y,Vector3.up);}
        }

        public bool ValidateGeometry(out string report)
        {
            bool valid=PlayableFloor!=null&&Mathf.Abs(PlayableFloor.center.y+PlayableFloor.size.y*.5f)<.0001f;
            valid &= Inside(-BattleLength*.5f,-BattleWidth*.5f)&&Inside(BattleLength*.5f,BattleWidth*.5f);
            foreach(var mesh in ownedMeshes)
            {
                foreach(var p in mesh.vertices)if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.sqrMagnitude))valid=false;
                if(mesh.vertexCount==0||mesh.triangles.Length%3!=0)valid=false;
            }
            valid &= Fortifications!=null && Fortifications.ValidateGeometry(out _) && Moat!=null && Moat.ValidateMoat(out _) && Flames!=null && Flames.Validate(out _);
            report=$"Elite Four geometry valid={valid}; renderers={RendererCount}; triangles={TriangleCount}; colliders={ColliderCount}; level floor={FloorCenter}; field={BattleLength}x{BattleWidth}; eight 7.5m spires and two native sloped approaches.";
            return valid;
        }
        void OnDestroy()
        {
            foreach(var mesh in ownedMeshes)Release(mesh);
            ownedMeshes.Clear(); materials?.Dispose();Release(floorPhysics);
        }
        static void Release(UnityEngine.Object obj)
        {if(obj==null)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}

        sealed class Draft
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();readonly List<int> indices=new List<int>();
            void Vertex(Vector3 p,Vector3 n)
            {
                vertices.Add(p);normals.Add(n);
                Vector3 tangent=Mathf.Abs(n.y)>.8f?Vector3.right:Vector3.Cross(Vector3.up,n).normalized;
                if(tangent.sqrMagnitude<.01f)tangent=Vector3.right;
                uv.Add(new Vector2(Vector3.Dot(p,tangent),Vector3.Dot(p,Vector3.Cross(n,tangent)))*.5f);
            }
            public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 hint)
            {
                Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                if(Vector3.Dot(n,hint)<0){var swap=b;b=c;c=swap;n=-n;}
                if(n.sqrMagnitude<.5f)return;
                int k=vertices.Count;Vertex(a,n);Vertex(b,n);Vertex(c,n);
                indices.Add(k);indices.Add(k+1);indices.Add(k+2);
            }
            public void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 hint)
            {Face(a,b,c,hint);Face(a,c,d,hint);}
            public Mesh Create(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();return mesh;
            }
        }
    }
}
