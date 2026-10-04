using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    public sealed class StadiumEnvironment : MonoBehaviour
    {
        [Serializable] public class Entry { public string key,name,sourceUrl; public float floor; }
        [Serializable] public class Catalog { public Entry[] arenas; }
        public static Catalog Inventory => JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Stadiums/catalog").text);
        GameObject current;
        public string CurrentKey { get; private set; }
        public int RendererCount => current == null ? 0 : current.GetComponentsInChildren<MeshRenderer>().Length;
        public bool Load(string key)
        {
            Entry entry=Array.Find(Inventory.arenas,e=>e.key==key);
            if(entry==null) throw new InvalidOperationException("Unknown stadium: "+key);
            var prefab=Resources.Load<GameObject>("Stadiums/"+key+"/Arena");
            if(prefab==null) throw new InvalidOperationException("Missing stadium prefab: "+key);
            if(current!=null){current.SetActive(false);Destroy(current);}
            current=Instantiate(prefab,transform);current.transform.localPosition=Vector3.up*-entry.floor;
            CurrentKey=key;
            RenderSettings.fogDensity=.0035f;
            RenderSettings.fogColor=new Color(.31f,.43f,.57f);
            RenderSettings.ambientSkyColor=new Color(.6f,.69f,.78f);
            RenderSettings.ambientEquatorColor=new Color(.43f,.48f,.52f);
            RenderSettings.ambientGroundColor=new Color(.21f,.23f,.26f);
            if(key=="free_battle") AddFreeBattleDetails();
            Debug.Log("[stadium] "+key+" name="+entry.name+" renderers="+RendererCount+" source="+entry.sourceUrl);
            return true;
        }
        void AddFreeBattleDetails()
        {
            var template=Resources.Load<Material>("Materials/ArenaLit");
            var steel=new Material(template);steel.SetColor("_BaseColor",new Color(.18f,.25f,.32f));steel.SetFloat("_Metallic",.75f);steel.SetFloat("_Smoothness",.62f);
            var light=new Material(template);light.SetColor("_BaseColor",new Color(.23f,.76f,.87f));light.EnableKeyword("_EMISSION");light.SetColor("_EmissionColor",new Color(.23f,.76f,.87f)*1.3f);
            // Authored additions follow the original central field, not a replacement arena.
            Tube("Modern perimeter lighting",9.65f,.14f,.055f,light);
            Tube("Metal perimeter rail",10.25f,.65f,.04f,steel);
            Tube("Metal perimeter rail base",10.25f,.3f,.04f,steel);
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64;
                var post=GameObject.CreatePrimitive(PrimitiveType.Cylinder);post.name="Rail baluster";
                post.transform.SetParent(current.transform,false);
                post.transform.localPosition=new Vector3(Mathf.Sin(a)*10.25f,.35f,Mathf.Cos(a)*10.25f);
                post.transform.localScale=new Vector3(.045f,.35f,.045f);
                post.GetComponent<Renderer>().sharedMaterial=steel;Destroy(post.GetComponent<Collider>());
            }
        }
        void Tube(string name,float radius,float y,float thickness,Material material)
        {
            const int segments=192,sides=8;
            var vertices=new Vector3[segments*sides];var normals=new Vector3[vertices.Length];var triangles=new int[segments*sides*6];
            for(int i=0;i<segments;i++)for(int j=0;j<sides;j++)
            {
                float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/sides;
                Vector3 radial=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a));
                normals[i*sides+j]=radial*Mathf.Cos(b)+Vector3.up*Mathf.Sin(b);
                vertices[i*sides+j]=radial*radius+Vector3.up*y+normals[i*sides+j]*thickness;
                int k=(i*sides+j)*6,n=(i+1)%segments,q=(j+1)%sides;
                triangles[k]=i*sides+j;triangles[k+1]=n*sides+j;triangles[k+2]=n*sides+q;
                triangles[k+3]=i*sides+j;triangles[k+4]=n*sides+q;triangles[k+5]=i*sides+q;
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=triangles;mesh.RecalculateBounds();
            var obj=new GameObject(name);obj.transform.SetParent(current.transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
