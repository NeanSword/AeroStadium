using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using AeroStadium.Presentation;

namespace AeroStadium.EditorTools
{
    public static class StadiumArenaImport
    {
        [Serializable] class Vertex { public float[] p,n,uv,color; }
        [Serializable] class Group { public string name,texture,normalTexture,alphaMode;public int wrapU,wrapV; public float[] rgba; public int[] triangles; }
        [Serializable] class Model { public string key,name,sourceUrl;public float floor,scale;public Vertex[] vertices; public Group[] groups; }
        [Serializable] class Report {public int arenas,triangles,materials;public bool passed;public string[] keys;}
        [MenuItem("AeroStadium/Import Stadium 2 Arenas")]
        public static void Prepare()
        {
            const string root="Assets/AeroStadium/Resources/Stadiums";
            Shader shader=Shader.Find("AeroStadium/StadiumSurface");if(shader==null)throw new InvalidOperationException("Stadium shader missing");
            var catalog=new StadiumEnvironment.Catalog();var entries=new List<StadiumEnvironment.Entry>();var report=new Report();var keys=new List<string>();
            foreach(string file in Directory.GetFiles(root,"mesh.json",SearchOption.AllDirectories))
            {
                Model data=JsonUtility.FromJson<Model>(File.ReadAllText(file));
                string folder=Path.GetDirectoryName(file).Replace('\\','/');
                if(data.vertices.Length==0||data.groups.Length==0)throw new InvalidOperationException(file);
                var mesh=new Mesh{name=data.key,indexFormat=IndexFormat.UInt32};
                var positions=new Vector3[data.vertices.Length];var normals=new Vector3[positions.Length];var uv=new Vector2[positions.Length];var colors=new Color[positions.Length];
                for(int i=0;i<positions.Length;i++){var v=data.vertices[i];positions[i]=new Vector3(v.p[0],v.p[1],v.p[2]);normals[i]=new Vector3(v.n[0],v.n[1],v.n[2]);uv[i]=new Vector2(v.uv[0],v.uv[1]);colors[i]=v.color==null?Color.white:new Color(v.color[0],v.color[1],v.color[2],v.color[3]);}
                mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.colors=colors;mesh.subMeshCount=data.groups.Length;
                var materials=new Material[data.groups.Length];
                for(int i=0;i<data.groups.Length;i++)
                {
                    var g=data.groups[i];foreach(int index in g.triangles)if(index<0||index>=positions.Length)throw new InvalidOperationException("Triangle index "+file);
                    mesh.SetTriangles(g.triangles,i);report.triangles+=g.triangles.Length/3;
                    var material=new Material(shader){name=g.name};material.SetColor("_BaseColor",new Color(g.rgba[0],g.rgba[1],g.rgba[2],g.rgba[3]));
                    if(g.alphaMode=="blend"){material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cutoff",.01f);material.renderQueue=3000;}
                    if(!string.IsNullOrEmpty(g.texture)){
                        string path=root+"/"+g.texture;ConfigureTexture(path,false);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(texture==null)throw new InvalidOperationException(path);material.SetTexture("_MainTex",texture);
                    }
                    if(!string.IsNullOrEmpty(g.normalTexture)){
                        string path=root+"/"+g.normalTexture;ConfigureTexture(path,true);material.SetTexture("_NormalTex",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                    }
                    Save(material,folder+"/Material_"+i+".mat");materials[i]=AssetDatabase.LoadAssetAtPath<Material>(folder+"/Material_"+i+".mat");report.materials++;
                }
                mesh.RecalculateBounds();mesh.RecalculateTangents();Save(mesh,folder+"/Geometry.asset");
                var obj=new GameObject(data.name);obj.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(folder+"/Geometry.asset");var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterials=materials;renderer.shadowCastingMode=ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(obj,folder+"/Arena.prefab");UnityEngine.Object.DestroyImmediate(obj);
                entries.Add(new StadiumEnvironment.Entry{key=data.key,name=data.name,sourceUrl=data.sourceUrl,floor=data.floor});keys.Add(data.key);report.arenas++;
            }
            entries.Sort((a,b)=>string.CompareOrdinal(a.key,b.key));catalog.arenas=entries.ToArray();File.WriteAllText(root+"/catalog.json",JsonUtility.ToJson(catalog,true));AssetDatabase.Refresh();AssetDatabase.SaveAssets();
            report.passed=report.arenas==30;report.keys=keys.ToArray();Directory.CreateDirectory("output/stadiums");File.WriteAllText("output/stadiums/unity-import.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Incomplete arena import");Debug.Log("[stadium-import] arenas="+report.arenas+" triangles="+report.triangles+" materials="+report.materials+" passed="+report.passed);
        }
        static void Save(UnityEngine.Object obj,string path){var existing=AssetDatabase.LoadMainAssetAtPath(path);if(existing==null)AssetDatabase.CreateAsset(obj,path);else{EditorUtility.CopySerialized(obj,existing);UnityEngine.Object.DestroyImmediate(obj);EditorUtility.SetDirty(existing);}}
        static void ConfigureTexture(string path,bool normal){var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new InvalidOperationException(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=!normal;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
    }
}
