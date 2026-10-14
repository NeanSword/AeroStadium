using System;
using System.IO;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AeroStadium.EditorTools
{
    public static class ModernArenaValidation
    {
        const string Folder="Assets/AeroStadium/Resources/Materials";
        public static void Prepare()
        {
            var shader=Shader.Find("AeroStadium/Modern Arena Surface");if(shader==null)throw new InvalidOperationException("Modern surface shader missing");
            if(AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ModernArenaShaderReference.mat")==null)AssetDatabase.CreateAsset(new Material(shader),Folder+"/ModernArenaShaderReference.mat");
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/ModernArenaSky.mat");
            if(sky==null){sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_SunSize",.035f);sky.SetFloat("_AtmosphereThickness",1.65f);sky.SetColor("_SkyTint",new Color(.7f,.49f,.35f));sky.SetColor("_GroundColor",new Color(.18f,.12f,.08f));sky.SetFloat("_Exposure",1.1f);AssetDatabase.CreateAsset(sky,Folder+"/ModernArenaSky.mat");}
            sky.SetFloat("_AtmosphereThickness",2.4f);sky.SetColor("_SkyTint",new Color(.46f,.24f,.14f));sky.SetFloat("_Exposure",.9f);EditorUtility.SetDirty(sky);
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder+"/ModernArenaPipeline.asset");
            if(pipeline==null){pipeline=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/AeroStadium/Settings/ArenaPipeline.asset"));pipeline.name="Modern arena rendering";pipeline.mainLightShadowmapResolution=4096;pipeline.shadowDistance=90;pipeline.shadowCascadeCount=4;var serialized=new SerializedObject(pipeline);serialized.FindProperty("m_SoftShadowsSupported").boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(pipeline,Folder+"/ModernArenaPipeline.asset");}
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }
        public static void ValidateAndCapture()
        {
            Prepare();EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string output=Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath,"..")),"output/arenas/elite4-life-20261013/editor");Directory.CreateDirectory(output);
            var owner=new GameObject("Elite Four architecture review");var view=owner.AddComponent<ArenaView>();view.Build();
            view.Stadium.Load("stadium1_elite4");view.ShowPokemon(0,6);view.ShowPokemon(1,9);ModernArenaReview.Hero(view.ArenaCamera);ModernArenaReview.Capture(view.ArenaCamera,Path.Combine(output,"original-battle.png"));
            view.Stadium.Load("stadium1_elite4_modern");ModernArenaReview.Hero(view.ArenaCamera);
            var stage=view.Stadium.ModernStage;string report;
            if(!stage.ValidateGeometry(out report)||!ModernArenaReview.FloorRays(stage)||!view.BattlePositionsValid)throw new InvalidOperationException("Modern arena validation failed: "+report);
            foreach(var material in stage.GetComponentsInChildren<MeshRenderer>())foreach(var m in material.sharedMaterials)if(m==null||m.shader==null||!m.shader.isSupported)throw new InvalidOperationException("Unsupported modern arena material");
            ModernArenaReview.Capture(view.ArenaCamera,Path.Combine(output,"modern-battle.png"));
            ModernArenaReview.CameraShot(view.ArenaCamera,new Vector3(19,11,-24),new Vector3(0,2,0),46);ModernArenaReview.Capture(view.ArenaCamera,Path.Combine(output,"modern-architecture.png"));
            ModernArenaReview.CameraShot(view.ArenaCamera,new Vector3(0,26,-24),Vector3.zero,51);ModernArenaReview.Capture(view.ArenaCamera,Path.Combine(output,"modern-overview.png"));
            ModernArenaReview.CameraShot(view.ArenaCamera,new Vector3(47,13,-14),new Vector3(30,3,0),46);ModernArenaReview.Capture(view.ArenaCamera,Path.Combine(output,"connected-approach.png"));
            File.WriteAllText(Path.Combine(output,"validation.json"),"{\"passed\":true,\"triangles\":"+stage.TriangleCount+",\"renderers\":"+stage.RendererCount+",\"colliders\":"+stage.ColliderCount+"}");Debug.Log("[modern-arena-editor] "+report+" passed=True");
            UnityEngine.Object.DestroyImmediate(owner);
        }
        public static void BuildWindows()
        {
            Prepare();var original=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            try{var modern=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Folder+"/ModernArenaPipeline.asset");GraphicsSettings.defaultRenderPipeline=modern;QualitySettings.renderPipeline=modern;ProjectBuild.BuildWindows();}
            finally{GraphicsSettings.defaultRenderPipeline=original;QualitySettings.renderPipeline=quality;AssetDatabase.SaveAssets();}
        }
    }
}
