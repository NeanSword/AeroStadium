using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;
namespace AeroStadium.Presentation
{
    public sealed class SurfCameraReview:MonoBehaviour
    {
        [Serializable]public sealed class Case
        {
            public int side,damage;public bool blocked,resolved,completed,returnedHome,cameraRestored,passed;
            public float riderDistance;public SurfCameraTelemetry camera;
        }
        [Serializable]public sealed class Report
        {
            public bool passed,fallback,cameraRejected,interruptions;public int errors,frames;public float seconds,captureSeconds;
            public float[] captureTimes;public string[] stages;public Case[] cases;
            public string scope="Surf57 camera: real hit/Protect events for Tortank9→Dracaufeu6 from both sides; interruptions/fallback are presentation-only scenarios";
        }
        ArenaView arena;Catalog catalog;string output,phase="PRÉPARATION";float started,captureStarted,requestedSeconds;
        Text phaseText,detailText,healthText;Canvas canvas;RenderTexture captureTarget;Texture2D captureImage;
        int errors,frames;bool capturing;float[] captureTimes;
        readonly List<float> frameTimes=new List<float>();readonly List<string> frameStages=new List<string>();
        readonly List<Case> cases=new List<Case>();bool interruptionPassed=true,fallbackPassed,cameraRejectedPassed;
        public void Begin(ArenaView view,string directory,float seconds)
        {
            arena=view;output=directory;requestedSeconds=Mathf.Clamp(seconds,90,180);Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"cycle-motion"));
            catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Data/catalog").text);catalog.Validate();
            arena.SetDecisionCameraActive(false);ModernArenaLighting.Apply(arena.ArenaCamera);CreateOverlay();
            Application.logMessageReceived+=OnLog;started=Time.realtimeSinceStartup;StartCoroutine(Run());
        }
        IEnumerator Setup(int side)
        {
            arena.EndProtection(true);arena.ShowPokemon(side,9);arena.ShowPokemon(1-side,6);yield return null;
            ModernArenaReview.CameraShot(arena.ArenaCamera,new Vector3(0,4.25f,-12.6f),new Vector3(0,1.65f,0),48);
        }
        IEnumerator Run()
        {
            capturing=true;captureStarted=Time.realtimeSinceStartup;StartCoroutine(CaptureMotion());
            for(int side=0;side<2;side++)for(int block=0;block<2;block++)
            {
                yield return Setup(side);var defender=new TeamMember(6,"rawst-berry"){moveIds=new[]{14,182},evs=new StatValues{hp=252,specialDefense=252}};
                var attacker=new TeamMember(9){moveIds=new[]{57}};
                var battle=new BattleEngine(catalog,new[]{side==0?attacker:defender},new[]{side==1?attacker:defender},191+side*13+block);
                IReadOnlyList<BattleEvent> events=battle.ResolveTurn(BattleChoice.Move(block==1&&side==1?1:0),BattleChoice.Move(block==1&&side==0?1:0));
                int move=-1;for(int i=0;i<events.Count;i++)if(events[i].Kind==BattleEventKind.MoveUsed&&events[i].Side==side&&events[i].MoveId==57){move=i;break;}
                bool hit=BattlePresentationHit.TryGetDirectHitEffectiveness(events,move,out int damageIndex,out double effectiveness);
                bool protectedEvent=BattlePresentationHit.TryGetBlockedAttack(events,move,out _,out int protectedSide)&&protectedSide==1-side;
                var result=new Case{side=side,blocked=block==1,resolved=block==1?protectedEvent:hit,damage=damageIndex>=0?events[damageIndex].Amount:0};
                if(block==1)yield return arena.Protect(1-side);
                SetPhase("SURF · "+(side==0?"DÉPART À GAUCHE":"DÉPART À DROITE")+(block==1?" · ABRI":" · IMPACT"),"Placement arrière → vague et avancée → retour du Pokémon → retour caméra");
                Health(battle.Active(1-side).Hp,battle.Active(1-side).MaxHp);yield return new WaitForSecondsRealtime(1.5f);
                Vector3 cameraHome=arena.ArenaCamera.transform.position;Quaternion rotation=arena.ArenaCamera.transform.rotation;float fov=arena.ArenaCamera.fieldOfView;
                yield return Observe(arena.Attack(side,"Water","Special",57,hit,block==1?1-side:-1,(float)effectiveness),side+"-"+block);
                result.completed=arena.LastAquatic.completed;result.returnedHome=arena.LastAquatic.returnedHome;result.riderDistance=arena.LastAquatic.riderDistance;
                result.camera=arena.LastSurfCamera;result.cameraRestored=arena.LastActionRestored&&Vector3.Distance(cameraHome,arena.ArenaCamera.transform.position)<.001f&&Quaternion.Angle(rotation,arena.ArenaCamera.transform.rotation)<.01f&&Mathf.Abs(fov-arena.ArenaCamera.fieldOfView)<.001f;
                var c=result.camera;
                result.passed=result.resolved&&result.completed&&result.returnedHome&&result.riderDistance>4.3f&&result.cameraRestored&&c!=null&&c.planned&&c.entered&&c.recovered&&c.safe
                    &&c.followFrames>10&&c.rearFrames>=c.followFrames*.95f&&c.actorVisibleFrames>=c.followFrames*.90f&&c.maxSpeed<32&&c.maxAngularSpeed<200&&c.maxFovSpeed<35;
                cases.Add(result);Debug.Log("[surf-camera-case] "+JsonUtility.ToJson(result));yield return new WaitForSecondsRealtime(1.5f);
            }
            // A deliberately blocked rear position must hold the readable arena shot, not teleport through it.
            yield return Setup(0);Bounds body=arena.PokemonWorldBounds(0),victim=arena.PokemonWorldBounds(1);
            Vector3 forward=Vector3.ProjectOnPlane(victim.center-body.center,Vector3.up).normalized;
            float distance=Mathf.Clamp(Mathf.Max(body.extents.x,body.extents.z)*2.7f+2.8f,6f,8);
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Review-only blocked rear camera";obstacle.transform.SetParent(arena.Stadium.transform,true);
            obstacle.transform.position=body.center-forward*distance+Vector3.Cross(Vector3.up,arena.BattleAxis)*1.8f+Vector3.up*3.8f;obstacle.transform.localScale=new Vector3(3,10,4);Physics.SyncTransforms();
            SetPhase("TRAJECTOIRE OBSTRUÉE","Contrôle : conserver une vue dégagée lorsque la caméra arrière ne peut pas passer.");
            yield return arena.Attack(0,"Water","Special",57,true,-1,2);
            fallbackPassed=arena.LastActionRestored&&arena.LastSurfCamera!=null&&!arena.LastSurfCamera.planned&&arena.LastActionCameraTravel<.01f;
            Destroy(obstacle);yield return null;
            yield return CameraRejected();
            float[] stops={.3f,2.15f,2.65f,3.9f,5.1f};
            foreach(float at in stops)
            {
                yield return Setup(0);SetPhase("INTERRUPTION CONTRÔLÉE", "Restauration de l’acteur et de la caméra · "+at.ToString("F2")+"s");
                Vector3 actorHome=arena.PokemonWorldBounds(0).center,cameraHome=arena.ArenaCamera.transform.position;
                bool done=false;StartCoroutine(Perform());yield return new WaitForSecondsRealtime(at);arena.CancelCurrentAction();
                float deadline=Time.realtimeSinceStartup+2;while(!done&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return null;
                bool valid=done&&!arena.ActionCameraActive&&Vector3.Distance(actorHome,arena.PokemonWorldBounds(0).center)<.001f&&Vector3.Distance(cameraHome,arena.ArenaCamera.transform.position)<.001f&&arena.GetComponentsInChildren<SurfEffect>(true).Length==0;
                interruptionPassed&=valid;Debug.Log("[surf-camera-cancel] time="+at+" passed="+valid);
                IEnumerator Perform(){yield return arena.Attack(0,"Water","Special",57,true,-1,2);done=true;}
            }
            capturing=false;yield return null;SetPhase("REVUE TERMINÉE","Caméra arrière, suivi, retour, obstacles et interruptions.");
            while(Time.realtimeSinceStartup-started<requestedSeconds)yield return null;
            bool passed=errors==0&&cases.Count==4&&fallbackPassed&&cameraRejectedPassed&&interruptionPassed;foreach(var c in cases)passed&=c.passed;
            var report=new Report{passed=passed,errors=errors,cases=cases.ToArray(),fallback=fallbackPassed,cameraRejected=cameraRejectedPassed,interruptions=interruptionPassed,frames=frames,seconds=Time.realtimeSinceStartup-started};
            File.WriteAllText(Path.Combine(output,"surf-camera-review.json"),JsonUtility.ToJson(report,true));Application.Quit(passed?0:1);
        }
        IEnumerator CameraRejected()
        {
            yield return Setup(0);SetPhase("CAMÉRA ORTHOGRAPHIQUE","Contrôle : Surf reste complet lorsque la caméra d’action refuse la prise en charge.");
            var camera=arena.ArenaCamera;bool orthographic=camera.orthographic,done=false,unchanged=true;
            Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            camera.orthographic=true;
            try
            {
                StartCoroutine(Perform());
                do
                {
                    unchanged&=!arena.ActionCameraActive&&Vector3.Distance(position,camera.transform.position)<.001f&&Quaternion.Angle(rotation,camera.transform.rotation)<.01f&&Mathf.Abs(fov-camera.fieldOfView)<.001f;
                    yield return null;
                }while(!done);
                cameraRejectedPassed=unchanged&&arena.LastAquatic.completed&&arena.LastAquatic.returnedHome&&arena.LastAquatic.riderDistance>4.3f;
                Debug.Log("[surf-camera-rejected] passed="+cameraRejectedPassed+" riderDistance="+arena.LastAquatic.riderDistance);
            }
            finally{camera.orthographic=orthographic;}
            IEnumerator Perform(){yield return arena.Attack(0,"Water","Special",57,true,-1,2);done=true;}
        }
        IEnumerator Observe(IEnumerator action,string prefix)
        {
            bool done=false;float began=Time.realtimeSinceStartup;int next=0;float[] at={0,1.70f,2.35f,2.85f,3.90f,6.2f};string[] names={"overview","rear","advance","impact","return","restored"};
            StartCoroutine(Perform());while(!done){yield return new WaitForEndOfFrame();if(next<at.Length&&Time.realtimeSinceStartup-began>=at[next]){ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,prefix+"-"+names[next]+".png"));next++;}}
            ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,prefix+"-restored.png"));
            IEnumerator Perform(){yield return action;done=true;}
        }
        void CreateOverlay()
        {
            var root=new GameObject("Original water move demonstration captions",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=arena.ArenaCamera;canvas.planeDistance=.5f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Phase band",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;
            rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,126);rect.anchoredPosition=Vector2.zero;
            panel.GetComponent<Image>().color=new Color(.025f,.045f,.07f,.83f);
            phaseText=Caption(panel.transform,"Phase",36,new Color(1,.82f,.3f),new Vector2(38,-15),new Vector2(1500,45));
            detailText=Caption(panel.transform,"Explanation",25,new Color(.9f,.95f,1),new Vector2(40,-69),new Vector2(1650,36));
            healthText=Caption(root.transform,"Dracaufeu health",24,Color.white,new Vector2(38,-147),new Vector2(750,36));
        }
        static Text Caption(Transform parent,string name,int size,Color color,Vector2 position,Vector2 extent)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=position;rect.sizeDelta=extent;
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;
            return text;
        }
        void SetPhase(string title,string explanation){phase=title;phaseText.text=title;detailText.text=explanation;}
        void Health(int hp,int maximum){healthText.text="DRACAUFEU · PV "+hp+" / "+maximum;}

        IEnumerator CaptureMotion()
        {
            captureTarget=new RenderTexture(1200,675,24,RenderTextureFormat.ARGB32){antiAliasing=2};captureTarget.Create();
            captureImage=new Texture2D(1200,675,TextureFormat.RGB24,false);float next=Time.realtimeSinceStartup;
            while(capturing)
            {
                yield return new WaitForEndOfFrame();
                float now=Time.realtimeSinceStartup;if(now<next)continue;
                CaptureFrame();frameTimes.Add(now-captureStarted);frameStages.Add(phase);
                next=now+1f/24;
            }
            captureTimes=frameTimes.ToArray();
            File.WriteAllText(Path.Combine(output,"cycle-motion","frames.json"),JsonUtility.ToJson(new Report
                {captureTimes=captureTimes,stages=frameStages.ToArray(),frames=frameTimes.Count,captureSeconds=Time.realtimeSinceStartup-captureStarted},true));
            ReleaseCapture();
        }
        void CaptureFrame()
        {
            var camera=arena.ArenaCamera;RenderTexture previous=camera.targetTexture,active=RenderTexture.active;float aspect=camera.aspect;
            try
            {
                camera.targetTexture=captureTarget;camera.aspect=1200f/675;camera.Render();RenderTexture.active=captureTarget;
                captureImage.ReadPixels(new Rect(0,0,1200,675),0,0);captureImage.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,"cycle-motion",frameTimes.Count.ToString("D5")+".png"),captureImage.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.active=active;}
        }

        void Update(){frames++;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void ReleaseCapture(){if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);captureTarget=null;}if(captureImage!=null){Destroy(captureImage);captureImage=null;}}
        void OnDestroy(){Application.logMessageReceived-=OnLog;capturing=false;ReleaseCapture();if(arena!=null){arena.CancelCurrentAction();arena.EndProtection(true);}}
    }
}
