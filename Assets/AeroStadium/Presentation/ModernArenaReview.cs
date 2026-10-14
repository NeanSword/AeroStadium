using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AeroStadium.Presentation
{
    public static class ModernArenaLighting
    {
        public static void Apply(Camera camera)
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.43f,.48f,.56f);
            RenderSettings.ambientEquatorColor=new Color(.34f,.29f,.24f);
            RenderSettings.ambientGroundColor=new Color(.17f,.16f,.15f);
            RenderSettings.fog=true;RenderSettings.fogDensity=.003f;
            RenderSettings.fogColor=new Color(.56f,.31f,.20f);
            var sun=GameObject.Find("Sun")?.GetComponent<Light>();
            if(sun!=null){sun.intensity=1.8f;sun.color=new Color(1f,.79f,.56f);sun.transform.rotation=Quaternion.Euler(27,-32,0);sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;sun.shadowNormalBias=.25f;RenderSettings.sun=sun;}
            var fill=GameObject.Find("Sky fill")?.GetComponent<Light>();
            if(fill!=null){fill.intensity=.48f;fill.color=new Color(.64f,.76f,1f);}
            var sky=Resources.Load<Material>("Materials/ModernArenaSky");
            if(sky!=null){RenderSettings.skybox=sky;camera.clearFlags=CameraClearFlags.Skybox;}
            camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            camera.backgroundColor=new Color(.67f,.36f,.23f);camera.farClipPlane=250;
            var pipeline=Resources.Load<UniversalRenderPipelineAsset>("Materials/ModernArenaPipeline");
            if(pipeline!=null)QualitySettings.renderPipeline=pipeline;
        }
    }

    public sealed class ModernArenaReview : MonoBehaviour
    {
        ArenaView arena;ModernEliteFourArena stage;int errors,frames;float frameSeconds,seconds=120;
        bool physicsPassed,orientationPassed,positionsPassed,moatPassed,moatAnimationPassed,routesPassed,lifePassed;bool fireRequested,firePassed;string output;GameObject probe;
        [Serializable] public class Report {public bool passed,physics,orientation,positions,moat,moatAnimation,routes,life,flamethrower;public int errors,triangles,renderers,colliders,frames,flames,bubblesPopped,fireCasts;public float seconds,meanFrameMilliseconds;public string directory;}
        public void Begin(ArenaView view)
        {
            arena=view;stage=arena.Stadium.ModernStage;
            output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../output/arenas/elite4-life-20261013/runtime"));
            string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--review-output");
            if(i>=0&&i+1<args.Length)output=Path.GetFullPath(args[i+1]);
            i=Array.IndexOf(args,"--seconds");if(i>=0&&i+1<args.Length&&float.TryParse(args[i+1],out float requested))seconds=Mathf.Clamp(requested,30,180);
            if(Array.IndexOf(args,"--review-hydro")>=0){enabled=false;gameObject.AddComponent<HydroPumpReview>().Begin(arena,output,seconds);return;}
            if(Array.IndexOf(args,"--review-water")>=0){enabled=false;gameObject.AddComponent<WaterAttackReview>().Begin(arena,output,seconds);return;}
            fireRequested=Array.IndexOf(args,"--review-flamethrower")>=0;
            if(Array.IndexOf(args,"--review-effectiveness")>=0)
            {
                enabled=false;gameObject.AddComponent<EffectivenessReactionReview>().Begin(arena,output,seconds);return;
            }
            if(Array.IndexOf(args,"--review-protect")>=0)
            {
                enabled=false;gameObject.AddComponent<ProtectAttackReview>().Begin(arena,output,seconds);return;
            }
            Directory.CreateDirectory(output);
            if(fireRequested){WriteWave(Path.Combine(output,"flamethrower.wav"),ArenaFireAudio.CreateFlamethrowerPcm(Mathf.RoundToInt(3.15f*ArenaFireAudio.SampleRate)),ArenaFireAudio.FlamethrowerVolume);WriteWave(Path.Combine(output,"arena-ambient.wav"),ArenaFireAudio.CreateAmbientPcm(),ArenaFireAudio.AmbientVolume);}
            Application.logMessageReceived+=OnLog;
            StartCoroutine(Run());
        }
        void OnLog(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void Update(){frames++;frameSeconds+=Time.unscaledDeltaTime;}
        IEnumerator Run()
        {
            if(stage==null){Debug.LogError("Modern Elite Four stage missing");Application.Quit(1);yield break;}
            arena.ShowPokemon(0,6);arena.ShowPokemon(1,9);yield return null;
            positionsPassed=arena.BattlePositionsValid;
            string validation;bool geometry=stage.ValidateGeometry(out validation);
            Debug.Log("[modern-arena-geometry] "+validation+" passed="+geometry);
            routesPassed=stage.Fortifications.ValidateGeometry(out string routesReport);
            moatPassed=stage.Moat.ValidateMoat(out string moatReport);
            moatPassed &= stage.FireAudio!=null && stage.FireAudio.ValidateAmbient(out _);
            Debug.Log("[modern-arena-routes] "+routesReport);Debug.Log("[modern-arena-moat] "+moatReport);
            physicsPassed=FloorRays(stage);
            probe=GameObject.CreatePrimitive(PrimitiveType.Sphere);probe.name="Collision verification sphere";
            probe.transform.position=stage.FloorCenter+new Vector3(0,3,-3);probe.transform.localScale=Vector3.one*.44f;
            var body=probe.AddComponent<Rigidbody>();body.mass=.3f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            yield return new WaitForSeconds(3f);
            physicsPassed &= Mathf.Abs(probe.transform.position.y-stage.FloorCenter.y-.22f)<.055f && body.linearVelocity.magnitude<.12f;
            Debug.Log("[modern-arena-physics] dropped sphere y="+probe.transform.position.y+" speed="+body.linearVelocity.magnitude+" passed="+physicsPassed);
            Destroy(probe);probe=null;
            Hero(arena.ArenaCamera);
            var a=arena.ArenaCamera.WorldToViewportPoint(stage.FloorCenter+Vector3.forward*3);
            var b=arena.ArenaCamera.WorldToViewportPoint(stage.FloorCenter-Vector3.forward*3);
            orientationPassed=Mathf.Abs(a.x-b.x)<.015f && Mathf.Abs(a.y-b.y)>.02f;
            yield return new WaitForSeconds(3f);yield return new WaitForEndOfFrame();Capture(arena.ArenaCamera,Path.Combine(output,"modern-battle.png"));
            if(fireRequested) {
                CameraShot(arena.ArenaCamera,new Vector3(0,3.8f,-10.5f),new Vector3(0,1.4f,0),48);
                firePassed=true;
                yield return CaptureFire(0,"flamethrower-left");
                arena.ShowPokemon(0,9);arena.ShowPokemon(1,6);yield return new WaitForSeconds(1);
                yield return CaptureFire(1,"flamethrower-right");
                arena.ShowPokemon(0,4);arena.ShowPokemon(1,9);yield return new WaitForSeconds(1);
                yield return CaptureFire(0,"flamethrower-small");
                yield return CaptureFire(0,"flamethrower-miss",false);
                arena.ShowPokemon(0,6);arena.ShowPokemon(1,9);Hero(arena.ArenaCamera);
            }
            float duration=Mathf.Max(arena.PlayAttackAnimation(0,true),arena.PlayAttackAnimation(1,false));
            yield return new WaitForSeconds(Mathf.Max(10,duration));
            CameraShot(arena.ArenaCamera,new Vector3(19,11,-24),new Vector3(0,2,0),46);
            yield return new WaitForSeconds(6);yield return new WaitForEndOfFrame();Capture(arena.ArenaCamera,Path.Combine(output,"modern-architecture.png"));
            yield return new WaitForSeconds(9);
            CameraShot(arena.ArenaCamera,new Vector3(0,26,-24),Vector3.zero,51);
            yield return new WaitForSeconds(6);yield return new WaitForEndOfFrame();Capture(arena.ArenaCamera,Path.Combine(output,"modern-overview.png"));
            yield return new WaitForSeconds(9);
            CameraShot(arena.ArenaCamera,new Vector3(1.3f,2.6f,-24.7f),new Vector3(1.3f,-1.1f,-21.25f),45);
            yield return new WaitForSeconds(3);yield return new WaitForEndOfFrame();
            float moatPhase=stage.Moat.AnimationPhase;Capture(arena.ArenaCamera,Path.Combine(output,"moat-flow-a.png"));
            yield return new WaitForSeconds(6);yield return new WaitForEndOfFrame();Capture(arena.ArenaCamera,Path.Combine(output,"moat-flow-b.png"));
            moatAnimationPassed=stage.Moat.AnimationPhase-moatPhase>.35f;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--review-motion")>=0)
                yield return CaptureMotion(arena.ArenaCamera,"lava-motion");
            CameraShot(arena.ArenaCamera,new Vector3(-18.5f,7.4f,-9.5f),new Vector3(-12.49f,5.4f,-1.35f),46);
            yield return new WaitForSeconds(2);yield return new WaitForEndOfFrame();
            Capture(arena.ArenaCamera,Path.Combine(output,"pillar-flames.png"));
            float flameStart=stage.Flames.AnimatedValue;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--review-motion")>=0)
                yield return CaptureMotion(arena.ArenaCamera,"fire-motion");
            else yield return new WaitForSeconds(6);
            lifePassed=stage.Flames.Validate(out string fireReport) && stage.Flames.FlameCount==4
                && Mathf.Abs(stage.Flames.AnimatedValue-flameStart)>.003f && stage.Moat.BubblesPopped>0;
            Debug.Log("[modern-arena-life] "+fireReport+" popped="+stage.Moat.BubblesPopped+" active="+stage.Moat.ActiveBubbles+" passed="+lifePassed);

            CameraShot(arena.ArenaCamera,new Vector3(47,13,-14),new Vector3(30,3,0),46);
            yield return new WaitForSeconds(4);yield return new WaitForEndOfFrame();Capture(arena.ArenaCamera,Path.Combine(output,"connected-approach.png"));
            Hero(arena.ArenaCamera);
            float finish=Time.realtimeSinceStartup+Mathf.Max(5,seconds-frameSeconds);
            while(Time.realtimeSinceStartup<finish){arena.PlayAttackAnimation(0,true);arena.PlayAttackAnimation(1,false);yield return new WaitForSeconds(8);}
            var report=new Report{passed=geometry&&physicsPassed&&orientationPassed&&positionsPassed&&routesPassed&&moatPassed&&moatAnimationPassed&&lifePassed&&(!fireRequested||firePassed)&&errors==0,flamethrower=fireRequested&&firePassed,fireCasts=arena.FlamethrowerCasts,life=lifePassed,flames=stage.Flames.FlameCount,bubblesPopped=stage.Moat.BubblesPopped,moat=moatPassed,moatAnimation=moatAnimationPassed,routes=routesPassed,physics=physicsPassed,orientation=orientationPassed,positions=positionsPassed,errors=errors,triangles=stage.TriangleCount,renderers=stage.RendererCount,colliders=stage.ColliderCount,frames=frames,seconds=frameSeconds,meanFrameMilliseconds=frameSeconds*1000/Mathf.Max(1,frames),directory=output};
            File.WriteAllText(Path.Combine(output,"review.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[modern-arena-result] "+JsonUtility.ToJson(report));Application.Quit(report.passed?0:1);
        }
        public static bool FloorRays(ModernEliteFourArena stage)
        {
            Physics.SyncTransforms();bool passed=true;
            foreach(float x in new[]{-8f,0,8f})foreach(float z in new[]{-5f,0,5f})
            {
                Vector3 point=stage.FloorCenter+new Vector3(x,0,z);
                bool hit=Physics.Raycast(point+Vector3.up*10,Vector3.down,out RaycastHit found,12,~0,QueryTriggerInteraction.Ignore);
                passed &= hit && found.collider.transform.IsChildOf(stage.transform) && Mathf.Abs(found.point.y-point.y)<.06f;
            }
            return passed;
        }
        public static void Hero(Camera camera)=>CameraShot(camera,new Vector3(0,9.4f,-22.5f),new Vector3(0,1.2f,0),46);
        public static void CameraShot(Camera camera,Vector3 position,Vector3 target,float fieldOfView){camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=fieldOfView;camera.orthographic=false;}
        public static void Capture(Camera camera,string path,int width=2560,int height=1440)
        {
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();
            RenderTexture previous=camera.targetTexture,active=RenderTexture.active;float aspect=camera.aspect;
            try{camera.targetTexture=target;camera.aspect=width/(float)height;camera.Render();RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());Release(image);}
            finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.active=active;target.Release();Release(target);}
        }
                                internal static void WriteWave(string path,float[] samples,float gain)
        {
            using(var writer=new BinaryWriter(File.Create(path))) {
                int bytes=samples.Length*2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                writer.Write((short)1);writer.Write((short)1);writer.Write(ArenaFireAudio.SampleRate);
                writer.Write(ArenaFireAudio.SampleRate*2);writer.Write((short)2);writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
                foreach(float sample in samples)writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample*gain,-1,1)*32767));
            }
        }
        IEnumerator CaptureFire(int side,string name,bool targetHit=true)
        {
            string directory=Path.Combine(output,name);Directory.CreateDirectory(directory);
            var times=new System.Collections.Generic.List<float>();float start=Time.realtimeSinceStartup;
            bool finished=false;StartCoroutine(Cast());
            while(!finished) {
                yield return new WaitForEndOfFrame();times.Add(Time.realtimeSinceStartup-start);
                Capture(arena.ArenaCamera,Path.Combine(directory,(times.Count-1).ToString("D3")+".png"),1280,720);
                yield return new WaitForSecondsRealtime(1f/15);
            }
            File.WriteAllText(Path.Combine(directory,"frames.json"),JsonUtility.ToJson(new MotionReport{width=1280,height=720,sampleTimes=times.ToArray()},true));
            firePassed &= arena.LastFlamethrowerCompleted&&arena.LastFirePeakFlames>20&&arena.LastFireSound&&arena.LastFireMouth=="EffMouth01"&&arena.LastFireAudioPeak>0&&arena.LastFireAudioPeak<=1&&arena.LastFireTargetReactions==(targetHit?1:0)
                &&(!targetHit||(arena.LastFireImpactTime>=.69f&&arena.LastFireImpactTime<.80f&&arena.LastFireBurnTail));
            if(targetHit){int before=arena.DamagePoseCount;yield return arena.Hit(1-side,30,true);firePassed &= arena.DamagePoseCount==before;}
            firePassed &= arena.GetComponentsInChildren<FlamethrowerEffect>(true).Length==0;
            Debug.Log("[flamethrower-review] "+name+" passed="+firePassed+" audioPeak="+arena.LastFireAudioPeak+" reactions="+arena.LastFireTargetReactions+" impact="+arena.LastFireImpactTime+" burnTail="+arena.LastFireBurnTail);
            yield return new WaitForSeconds(.6f);
            System.Collections.IEnumerator Cast(){yield return arena.Attack(side,"Fire","Special",53,targetHit);finished=true;}
        }
        [Serializable] class MotionReport { public float[] sampleTimes; public int width=960,height=540; }
        IEnumerator CaptureMotion(Camera camera,string name)
        {
            string directory=Path.Combine(output,name);Directory.CreateDirectory(directory);
            var report=new MotionReport{sampleTimes=new float[72]};float start=Time.realtimeSinceStartup;
            for(int frame=0;frame<report.sampleTimes.Length;frame++) {
                yield return new WaitForEndOfFrame();report.sampleTimes[frame]=Time.realtimeSinceStartup-start;
                Capture(camera,Path.Combine(directory,frame.ToString("D3")+".png"),report.width,report.height);
                yield return new WaitForSecondsRealtime(1f/12);
            }
            File.WriteAllText(Path.Combine(directory,"frames.json"),JsonUtility.ToJson(report,true));
        }
        static void Release(UnityEngine.Object value){if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDestroy(){Application.logMessageReceived-=OnLog;if(probe!=null)Destroy(probe);}
    }
}
