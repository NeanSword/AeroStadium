using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>An original Abri energy shell. The visible ellipsoid is also the attack contact surface.</summary>
    public sealed class ProtectShieldEffect : MonoBehaviour
    {
        public const float ActivationDuration=.65f;
        const float ReleaseDuration=.40f;
        const int Longitude=64, Latitude=32, RingSegments=96;
        static readonly int AgeId=Shader.PropertyToID("_Age"), FadeId=Shader.PropertyToID("_Fade"),
            ImpactAgeId=Shader.PropertyToID("_ImpactAge"), ImpactDirectionId=Shader.PropertyToID("_ImpactDirection");
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>(4);
        readonly MeshRenderer[] renderers=new MeshRenderer[2];
        readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        Transform shell,ring;
        Bounds victim;
        Vector3 center,extents,impactDirection=Vector3.up;
        float age,releaseAge,impactAge=10;
        bool initialized,releasing;
        public bool IsActive=>initialized&&!releasing;
        public bool IsReleasing=>releasing;
        public float ActivationTime=>age;
        public int ContactCount {get;private set;}
        public int RendererCount=>2;
        public int TriangleCount=>Longitude*Latitude*2+RingSegments*2;
        public Bounds ShieldBounds=>new Bounds(center,extents*2);

        public static ProtectShieldEffect Create(Transform parent,Bounds victim)
        {
            var go=new GameObject("Abri · living energy shield");
            go.transform.SetParent(parent,true);
            var effect=go.AddComponent<ProtectShieldEffect>();
            effect.Initialize();effect.SetBounds(victim);effect.ApplyProperties();return effect;
        }

        void Initialize()
        {
            Material reference=Resources.Load<Material>("Materials/ProtectShieldReference");
            Shader shader=reference!=null?reference.shader:Shader.Find("AeroStadium/Original Protect Shield");
            if(shader==null)throw new InvalidOperationException("The original Protect shield shader is missing.");
            Mesh[] meshes={CreateShell(),CreateRing()};
            for(int i=0;i<2;i++)
            {
                owned.Add(meshes[i]);
                Material material=reference!=null?new Material(reference):new Material(shader);
                material.SetFloat("_Mode",i);owned.Add(material);
                var part=new GameObject(i==0?"Abri · translucent energy shell":"Abri · forming ground halo");
                part.transform.SetParent(transform,false);
                part.AddComponent<MeshFilter>().sharedMesh=meshes[i];
                renderers[i]=part.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=material;
                renderers[i].shadowCastingMode=ShadowCastingMode.Off;renderers[i].receiveShadows=false;
                renderers[i].lightProbeUsage=LightProbeUsage.Off;renderers[i].reflectionProbeUsage=ReflectionProbeUsage.Off;
                if(i==0)shell=part.transform;else ring=part.transform;
            }
            initialized=true;
        }

        public void SetBounds(Bounds bounds)
        {
            victim=bounds;center=bounds.center;
            // Add breathing room around every appendage without normalising differently sized species.
            extents=new Vector3(Mathf.Max(.42f,bounds.extents.x*1.14f+.10f),
                Mathf.Max(.50f,bounds.extents.y*1.12f+.12f),Mathf.Max(.42f,bounds.extents.z*1.14f+.10f));
            transform.position=center;transform.rotation=Quaternion.identity;
            Vector3 parentScale=transform.parent!=null?transform.parent.lossyScale:Vector3.one;
            transform.localScale=new Vector3(1/Mathf.Max(.001f,Mathf.Abs(parentScale.x)),
                1/Mathf.Max(.001f,Mathf.Abs(parentScale.y)),1/Mathf.Max(.001f,Mathf.Abs(parentScale.z)));
            if(shell==null)return;
            shell.localScale=extents;
            ring.localScale=new Vector3(extents.x,1,extents.z);
            ring.localPosition=new Vector3(0,victim.min.y-center.y+.035f,0);
        }

        public void React(Vector3 point,Vector3 normal)
        {
            if(!IsActive)return;
            Vector3 p=point-center;
            impactDirection=new Vector3(p.x/extents.x,p.y/extents.y,p.z/extents.z).normalized;
            if(impactDirection.sqrMagnitude<.1f)impactDirection=normal.normalized;
            impactAge=0;ContactCount++;ApplyProperties();
        }

        public void Release()
        {
            if(releasing)return;
            releasing=true;releaseAge=0;
        }

        /// <summary>First intersection on the source-to-aim segment; an incoming source must be outside the shield.</summary>
        public bool TryContact(Vector3 source,Vector3 aim,out Vector3 hit,out Vector3 normal)
        {
            hit=aim;normal=Vector3.up;
            if(!IsActive)return false;
            Vector3 offset=source-center,direction=aim-source;
            Vector3 o=new Vector3(offset.x/extents.x,offset.y/extents.y,offset.z/extents.z);
            Vector3 d=new Vector3(direction.x/extents.x,direction.y/extents.y,direction.z/extents.z);
            float a=Vector3.Dot(d,d),b=2*Vector3.Dot(o,d),c=Vector3.Dot(o,o)-1;
            if(a<.000001f||c<=0)return false;
            float discriminant=b*b-4*a*c;
            if(discriminant<0)return false;
            float t=(-b-Mathf.Sqrt(discriminant))/(2*a);
            if(t<0||t>1)return false;
            hit=source+direction*t;
            Vector3 surface=hit-center;
            normal=new Vector3(surface.x/(extents.x*extents.x),surface.y/(extents.y*extents.y),
                surface.z/(extents.z*extents.z)).normalized;
            return true;
        }

        public static Vector3 DeflectionDirection(Vector3 incoming,Vector3 normal)
        {
            normal=normal.sqrMagnitude>.001f?normal.normalized:Vector3.up;
            Vector3 direction=incoming.sqrMagnitude>.001f?incoming.normalized:-normal;
            Vector3 reflected=Vector3.Reflect(direction,normal);
            // Keep the redirected stream outside the shell and lift it clear of the arena floor.
            Vector3 lifted=reflected+Vector3.up*.58f+normal*.24f;
            float outward=Vector3.Dot(lifted,normal);
            if(outward<.22f)lifted+=normal*(.22f-outward);
            return lifted.normalized;
        }

        void Update()
        {
            if(!initialized)return;
            age+=Time.deltaTime;impactAge+=Time.deltaTime;
            if(releasing){releaseAge+=Time.deltaTime;if(releaseAge>=ReleaseDuration){Destroy(gameObject);return;}}
            ApplyProperties();
        }

        void ApplyProperties()
        {
            properties.SetFloat(AgeId,age);properties.SetFloat(FadeId,releasing?1-Mathf.SmoothStep(0,1,releaseAge/ReleaseDuration):1);
            properties.SetFloat(ImpactAgeId,impactAge);properties.SetVector(ImpactDirectionId,impactDirection);
            for(int i=0;i<renderers.Length;i++)if(renderers[i]!=null)renderers[i].SetPropertyBlock(properties);
        }

        static Mesh CreateShell()
        {
            var vertices=new Vector3[(Longitude+1)*(Latitude+1)];var uv=new Vector2[vertices.Length];
            var indices=new int[Longitude*Latitude*6];int cursor=0;
            for(int y=0;y<=Latitude;y++)for(int x=0;x<=Longitude;x++)
            {
                float latitude=Mathf.PI*y/Latitude,longitude=Mathf.PI*2*x/Longitude;
                int index=y*(Longitude+1)+x;
                vertices[index]=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),-Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
                uv[index]=new Vector2(x/(float)Longitude,y/(float)Latitude);
            }
            for(int y=0;y<Latitude;y++)for(int x=0;x<Longitude;x++)
            {
                int a=y*(Longitude+1)+x,b=a+1,c=a+Longitude+1,d=c+1;
                indices[cursor++]=a;indices[cursor++]=b;indices[cursor++]=c;
                indices[cursor++]=b;indices[cursor++]=d;indices[cursor++]=c;
            }
            return new Mesh{name="Abri · ellipsoid contact shell",vertices=vertices,normals=vertices,uv=uv,triangles=indices};
        }

        static Mesh CreateRing()
        {
            var vertices=new Vector3[(RingSegments+1)*2];var uv=new Vector2[vertices.Length];var indices=new int[RingSegments*6];
            for(int i=0;i<=RingSegments;i++)
            {
                float angle=i*Mathf.PI*2/RingSegments;
                for(int j=0;j<2;j++){int v=i*2+j;float r=j==0?.70f:1.18f;
                    vertices[v]=new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);uv[v]=new Vector2(vertices[v].x,vertices[v].z);}
                if(i==RingSegments)continue;
                int t=i*6,a=i*2;indices[t]=a;indices[t+1]=a+2;indices[t+2]=a+1;
                indices[t+3]=a+1;indices[t+4]=a+2;indices[t+5]=a+3;
            }
            return new Mesh{name="Abri · activation halo",vertices=vertices,uv=uv,triangles=indices};
        }

        void OnDestroy(){for(int i=0;i<owned.Count;i++)if(owned[i]!=null)Destroy(owned[i]);owned.Clear();}
    }
}
