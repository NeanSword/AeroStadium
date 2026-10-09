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
        [Serializable] class Group { public string name,texture,normalTexture,alphaMode;public int wrapU,wrapV,secondaryWrapU,secondaryWrapV,nativeCycles,nativeSubmissionClass;
            public bool nativeSampler;public float[] samplerMask,samplerClampTexels,samplerTextureSize,samplerTileOriginTexels;
            public bool nativeMaterial,nativeAlphaOutputZero,decalDepthBias,nativeCull;
            public bool nativeIntensityAlpha,secondaryIntensityAlpha,secondaryNativeSampler,nativeAlphaCompareNone;
            public float[] secondarySamplerMask,secondarySamplerClampTexels,secondarySamplerTextureSize,secondarySamplerTileOriginTexels;
            public int[] colorMux0,colorMux1,alphaMux0,alphaMux1;
            public float[] nativePrimitiveColor,nativeEnvironmentColor,secondaryUvScale;
            public float nativeLodFraction;public string secondaryTexture;public float[] rgba; public int[] triangles; }
        [Serializable] class Model { public string key,name,sourceUrl;public float floor,scale;public Vertex[] vertices; public Group[] groups; }
        [Serializable] class Report {public int arenas,triangles,materials;public bool passed;public string[] keys;}
        [MenuItem("AeroStadium/Import Stadium 1 and 2 Arenas")]
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
                    material.SetVector("_Wrap",new Vector4(g.wrapU,g.wrapV,g.secondaryWrapU,g.secondaryWrapV));
                    if(g.nativeSampler){
                        material.SetFloat("_NativeSampler",1);
                        material.SetVector("_SamplerMask",new Vector4(g.samplerMask[0],g.samplerMask[1],0,0));
                        material.SetVector("_SamplerClamp",new Vector4(g.samplerClampTexels[0],g.samplerClampTexels[1],g.samplerTextureSize[0],g.samplerTextureSize[1]));
                        material.SetVector("_SamplerOrigin",new Vector4(g.samplerTileOriginTexels[0],g.samplerTileOriginTexels[1],0,0));
                    }
                    material.SetVector("_IntensityAlpha",new Vector4(g.nativeIntensityAlpha?1:0,g.secondaryIntensityAlpha?1:0,0,0));
                    material.SetFloat("_AlphaCompareDisabled",g.nativeAlphaCompareNone?1:0);
                    if(g.secondaryNativeSampler){
                        material.SetFloat("_SecondaryNativeSampler",1);
                        material.SetVector("_SecondarySamplerMask",Pair(g.secondarySamplerMask,"secondary mask",file));
                        var clamp=Pair(g.secondarySamplerClampTexels,"secondary clamp",file);
                        var size=Pair(g.secondarySamplerTextureSize,"secondary texture size",file);
                        if(clamp.x<=0||clamp.y<=0||size.x<=0||size.y<=0)throw new InvalidOperationException("Invalid secondary sampler dimensions: "+file);
                        material.SetVector("_SecondarySamplerClamp",new Vector4(clamp.x,clamp.y,size.x,size.y));
                        material.SetVector("_SecondarySamplerOrigin",Pair(g.secondarySamplerTileOriginTexels,"secondary origin",file));
                    }
                    if(g.nativeMaterial) ConfigureNative(material,g,root,file);
                    if(g.alphaMode=="blend" && !(g.nativeMaterial&&g.nativeAlphaOutputZero&&g.nativeSubmissionClass!=6)){material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cutoff",.01f);material.renderQueue=3000;}
                    if(g.nativeMaterial&&g.nativeAlphaOutputZero&&g.nativeSubmissionClass!=6){material.SetFloat("_CoverageAlpha",1);material.SetFloat("_Cutoff",.01f);}
                    if(g.decalDepthBias){material.SetFloat("_OffsetFactor",-1);material.SetFloat("_OffsetUnits",-2);}
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
            report.passed=report.arenas==48&&new HashSet<string>(keys).Count==48;report.keys=keys.ToArray();Directory.CreateDirectory("output/stadiums");File.WriteAllText("output/stadiums/unity-import.json",JsonUtility.ToJson(report,true));
            if(!report.passed)throw new InvalidOperationException("Incomplete arena import");Debug.Log("[stadium-import] arenas="+report.arenas+" triangles="+report.triangles+" materials="+report.materials+" passed="+report.passed);
        }
        static void ConfigureNative(Material material,Group g,string root,string source)
        {
            ValidateMux(g.colorMux0,g.alphaMux0,source);ValidateMux(g.colorMux1,g.alphaMux1,source);
            material.SetFloat("_NativeMaterial",1);material.SetFloat("_NativeCycles",g.nativeCycles);
            material.SetVector("_PrimitiveColor",FloatVector(g.nativePrimitiveColor));material.SetVector("_EnvironmentColor",FloatVector(g.nativeEnvironmentColor));
            material.SetFloat("_PrimitiveLOD",g.nativeLodFraction);
            material.SetVector("_ColorMux0",IntVector(g.colorMux0));material.SetVector("_ColorMux1",IntVector(g.colorMux1));
            material.SetVector("_AlphaMux0",IntVector(g.alphaMux0));material.SetVector("_AlphaMux1",IntVector(g.alphaMux1));
            material.SetVector("_SecondaryUV",new Vector4(g.secondaryUvScale[0],g.secondaryUvScale[1],0,0));
            material.SetVector("_Wrap",new Vector4(g.wrapU,g.wrapV,g.secondaryWrapU,g.secondaryWrapV));
            // Source draw culling remains disabled until every source face orientation is reviewed.
            if(!string.IsNullOrEmpty(g.secondaryTexture))
            {
                string path=root+"/"+g.secondaryTexture;ConfigureTexture(path,false);
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(tex==null)throw new InvalidOperationException(path);material.SetTexture("_SecondaryTex",tex);
            }
        }
        static Vector4 Pair(float[] v,string name,string source){if(v==null||v.Length!=2)throw new InvalidOperationException("Missing "+name+": "+source);return new Vector4(v[0],v[1],0,0);}
        static Vector4 FloatVector(float[] v){if(v==null||v.Length!=4)throw new InvalidOperationException("Native color requires4 values");return new Vector4(v[0],v[1],v[2],v[3]);}
        static Vector4 IntVector(int[] v){if(v==null||v.Length!=4)throw new InvalidOperationException("Native mux requires4 values");return new Vector4(v[0],v[1],v[2],v[3]);}
        static void ValidateMux(int[] color,int[] alpha,string source)
        {
            int[][] supported={new[]{0,1,2,3,4,5,6,31},new[]{0,1,2,3,4,5,31},new[]{0,1,2,3,4,5,7,8,9,10,11,12,14,31},new[]{0,1,2,3,4,5,6,7,31}};
            if(color==null||alpha==null||color.Length!=4||alpha.Length!=4)throw new InvalidOperationException("Missing native mux: "+source);
            for(int role=0;role<4;role++)
            {
                if(Array.IndexOf(supported[role],color[role])<0)throw new InvalidOperationException("Unsupported native color mux "+color[role]+" role"+role+" "+source);
                if(alpha[role]<0||alpha[role]>7||(role==2&&alpha[role]==0))throw new InvalidOperationException("Unsupported native alpha/LOD mux "+alpha[role]+" "+source);
            }
        }
        static void Save(UnityEngine.Object obj,string path){var existing=AssetDatabase.LoadMainAssetAtPath(path);if(existing==null)AssetDatabase.CreateAsset(obj,path);else{EditorUtility.CopySerialized(obj,existing);UnityEngine.Object.DestroyImmediate(obj);EditorUtility.SetDirty(existing);}}
        static void ConfigureTexture(string path,bool normal){var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new InvalidOperationException(path);importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=!normal;importer.mipmapEnabled=true;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
    }
}
