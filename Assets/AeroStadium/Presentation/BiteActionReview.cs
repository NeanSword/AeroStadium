using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    [DefaultExecutionOrder(220)]
    public sealed class BiteActionReview : MonoBehaviour
    {
        [Serializable] public sealed class Case
        {
            public int side,seed,damage,hpBefore,hpAfter,damageReactions,shieldReactions;
            public string outcome;public bool resolved,passed,rootsRestored,cameraRestored,cleanup,noDuplicateReaction,noFloatingDamageText;
            public float dodgeTravel;public ArenaView.BiteCast bite;public BiteCameraTelemetry camera;public DodgePoseTelemetry dodge;
        }
        [Serializable] public sealed class Cancellation
        {public string phase;public float at;public bool returned,rootsRestored,cameraRestored,cleanup,passed;}
        [Serializable] public sealed class Report
        {
            public string scope="Croc Feu424, native Arcanin59 against Tortank9 in the modern Elite Four arena: actual BattleEngine hit/Protect/Miss on both sides; non-hit without Miss is a presentation control. Cancellation, replacement and orthographic checks are presentation scenarios. Mouth-marker vs animated body triangle proof, not full tooth/skin continuous collision or all species.";
            public bool passed,orthographic,replacement;public int errors,frames;public float seconds;public Case[] cases;public Cancellation[] cancellations;
        }
        [Serializable] sealed class MotionReport{public int width=1200,height=675,requestedFps=24;public float[] captureTimes;public string[] stages;}
        [Serializable] sealed class Still{public string file;public float actualSeconds;}
        [Serializable] sealed class Stills{public Still[] stills;}
        sealed class RootPose
        {
            readonly Transform root;readonly Vector3 position;readonly Quaternion rotation;
            public RootPose(Transform t){root=t;position=t.position;rotation=t.rotation;}
            public bool Restored=>root!=null&&Vector3.Distance(root.position,position)<.001f&&Quaternion.Angle(root.rotation,rotation)<.01f;
        }
        sealed class CameraPose
        {
            readonly Camera camera;readonly Vector3 position;readonly Quaternion rotation;readonly float fov;readonly bool orthographic;
            public CameraPose(Camera c){camera=c;position=c.transform.position;rotation=c.transform.rotation;fov=c.fieldOfView;orthographic=c.orthographic;}
            public bool Restored=>camera!=null&&Vector3.Distance(camera.transform.position,position)<.001f&&Quaternion.Angle(camera.transform.rotation,rotation)<.01f&&Mathf.Abs(camera.fieldOfView-fov)<.001f&&camera.orthographic==orthographic;
        }
        ArenaView arena;Catalog catalog;string output,phase="PRÉPARATION";Text caption;float started,requested;
        int frames,errors;bool captureLoop,captureEnabled,captureDone,orthographicPassed,replacementPassed;
        readonly List<Case> cases=new List<Case>();readonly List<Cancellation> cancellations=new List<Cancellation>();
        readonly List<float> captureTimes=new List<float>();readonly List<string> stages=new List<string>();readonly List<Still> stills=new List<Still>();
        RenderTexture captureTarget;Texture2D captureImage;float captureStarted;
        string diagnosticPrefix,livePrefix;bool diagnosticCaptured,liveCaptured;ArenaView.BiteCast previousBite;
        public void Begin(ArenaView view,string directory,float seconds)
        {
            arena=view;output=directory;requested=Mathf.Clamp(seconds,120,180);Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"cycle-motion"));
            catalog=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Data/catalog").text);catalog.Validate();
            arena.SetDecisionCameraActive(false);ModernArenaLighting.Apply(arena.ArenaCamera);Overlay();
            ModernArenaReview.WriteWave(Path.Combine(output,"croc-feu-contact.wav"),ArenaView.CreateFireFangPcm(true),.51f);
            ModernArenaReview.WriteWave(Path.Combine(output,"croc-feu-raté.wav"),ArenaView.CreateFireFangPcm(false),.51f);
            Application.logMessageReceived+=OnLog;started=Time.realtimeSinceStartup;StartCoroutine(Run());
        }
        IEnumerator Setup(int side)
        {
            arena.CancelCurrentAction();arena.EndProtection(true);arena.ShowPokemon(side,59);arena.ShowPokemon(1-side,9);yield return null;
            ModernArenaReview.CameraShot(arena.ArenaCamera,new Vector3(0,4.25f,-12.6f),new Vector3(0,1.65f,0),48);
        }
        RootPose[] Roots(int side)=>new[]{new RootPose(GameObject.Find("Pokemon_"+side+"_59").transform),new RootPose(GameObject.Find("Pokemon_"+(1-side)+"_9").transform)};
        static bool Restored(RootPose[] roots){foreach(var p in roots)if(!p.Restored)return false;return true;}
        bool Clean()
        {
            if(FindObjectsByType<ContactBiteMotion>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length>0)return false;
            foreach(var pose in FindObjectsByType<DodgePoseMotion>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!pose.IsClean)return false;
            foreach(var audio in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(audio.clip!=null&&(audio.clip.name.Contains("Original bite")||audio.clip.name.Contains("Original mouth")))return false;
            return true;
        }
        static bool NoFloatingDamageText()
        {foreach(var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!string.IsNullOrEmpty(text.text)&&text.text.StartsWith("−",StringComparison.Ordinal))return false;return true;}
        static int FindMove(IReadOnlyList<BattleEvent> events,int side,int move)
        {for(int i=0;i<events.Count;i++)if(events[i].Kind==BattleEventKind.MoveUsed&&events[i].Side==side&&events[i].MoveId==move)return i;return -1;}
        BattleEngine Scenario(int side,int outcome,out IReadOnlyList<BattleEvent> events,out int seed)
        {
            for(seed=1;seed<=4096;seed++)
            {
                var attack=new TeamMember(59){moveIds=new[]{424}};
                var defend=new TeamMember(9,"rawst-berry"){moveIds=new[]{334,182},evs=new StatValues{hp=252,defense=252}};
                var battle=new BattleEngine(catalog,new[]{side==0?attack:defend},new[]{side==1?attack:defend},seed);
                events=battle.ResolveTurn(BattleChoice.Move(outcome==1&&side==1?1:0),BattleChoice.Move(outcome==1&&side==0?1:0));
                int move=FindMove(events,side,424);bool valid=outcome==0?BattlePresentationHit.TryGetDirectHit(events,move,out _)
                    :outcome==1?BattlePresentationHit.TryGetBlockedAttack(events,move,out _,out int protectedSide)&&protectedSide==1-side:BattlePresentationHit.IsResolvedMiss(events,move);
                if(valid&&battle.Active(1-side).Hp>0)return battle;
            }
            throw new InvalidOperationException("Croc Feu : scénario réel introuvable.");
        }
        IEnumerator Run()
        {
            yield return Setup(0);SetPhase("CROC FEU · CONTACT DE LA MÂCHOIRE");yield return new WaitForSecondsRealtime(2);
            captureLoop=true;captureStarted=Time.realtimeSinceStartup;StartCoroutine(CaptureMotion());
            for(int side=0;side<2;side++)
            {
                for(int outcome=0;outcome<3;outcome++)yield return CheckCase(side,outcome);
                if(side==0){captureLoop=false;while(!captureDone)yield return null;}
            }
            yield return CheckCase(0,3);
            float[] at={.25f,1.25f,1.85f,2.50f,4.10f};string[] names={"entrée","approche","fermeture","recul","retour"};
            for(int i=0;i<at.Length;i++)yield return Cancel(at[i],names[i]);
            yield return Replacement();yield return Orthographic();
            arena.EndProtection(true);SetPhase("REVUE CROC FEU TERMINÉE");while(Time.realtimeSinceStartup-started<requested)yield return null;
            bool passed=errors==0&&cases.Count==7&&cancellations.Count==5&&orthographicPassed&&replacementPassed;
            foreach(var c in cases)passed&=c.passed;foreach(var c in cancellations)passed&=c.passed;
            var report=new Report{passed=passed,errors=errors,frames=frames,seconds=Time.realtimeSinceStartup-started,cases=cases.ToArray(),cancellations=cancellations.ToArray(),orthographic=orthographicPassed,replacement=replacementPassed};
            File.WriteAllText(Path.Combine(output,"bite-review.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(output,"stills.json"),JsonUtility.ToJson(new Stills{stills=stills.ToArray()},true));
            Debug.Log("[bite-review-result] "+JsonUtility.ToJson(report));Application.Quit(passed?0:1);
        }
        IEnumerator CheckCase(int side,int outcome)
        {
            float began=Time.realtimeSinceStartup;yield return Setup(side);
            bool block=outcome==1,miss=outcome==2,control=outcome==3;
            var battle=Scenario(side,control?2:outcome,out var events,out int seed);int move=FindMove(events,side,424);
            bool direct=!control&&BattlePresentationHit.TryGetDirectHitEffectiveness(events,move,out _,out _);
            BattlePresentationHit.TryGetDirectHitEffectiveness(events,move,out int damageIndex,out double efficacy);
            bool blocked=BattlePresentationHit.TryGetBlockedAttack(events,move,out _,out int protectedSide)&&protectedSide==1-side;
            bool missed=!control&&BattlePresentationHit.IsResolvedMiss(events,move);
            var c=new Case{side=side,seed=seed,outcome=control?"non-hit-control":block?"protect":miss?"accuracy-miss":"direct-hit",resolved=control||(block?blocked:miss?missed:direct),damage=damageIndex>=0?events[damageIndex].Amount:0,hpBefore=battle.Active(1-side).MaxHp,hpAfter=battle.Active(1-side).Hp};
            if(block)yield return arena.Protect(1-side);SetPhase("ARCANIN "+(side==0?"À GAUCHE":"À DROITE")+" · "+c.outcome.ToUpperInvariant());yield return new WaitForSecondsRealtime(.5f);
            var roots=Roots(side);var home=new CameraPose(arena.ArenaCamera);int reactions=arena.DamagePoseCount,blocks=arena.ProtectBlockCount;
            previousBite=arena.LastBite;livePrefix="side"+side+"-"+c.outcome;liveCaptured=false;captureEnabled=side==0&&!control;diagnosticPrefix=direct?"side"+side+"-direct":null;diagnosticCaptured=false;
            yield return Observe(arena.Attack(side,"Fire","Physical",424,direct,block?1-side:-1,(float)efficacy,missed),"side"+side+"-"+c.outcome);
            captureEnabled=false;diagnosticPrefix=livePrefix=null;c.bite=arena.LastBite;c.camera=arena.LastBiteCamera;c.dodge=arena.LastDodgePose;
            c.damageReactions=arena.DamagePoseCount-reactions;c.shieldReactions=arena.ProtectBlockCount-blocks;c.dodgeTravel=arena.LastActionDodgeTravel;
            c.rootsRestored=Restored(roots);c.cameraRestored=arena.LastActionRestored&&home.Restored;c.noDuplicateReaction=true;
            if(direct){int before=arena.DamagePoseCount;yield return arena.Hit(1-side,c.damage,true,(float)efficacy);c.noDuplicateReaction=before==arena.DamagePoseCount;}
            yield return null;c.cleanup=Clean();c.noFloatingDamageText=NoFloatingDamageText();var b=c.bite;var cam=c.camera;
            bool geometry=b.completed&&b.geometryValid&&b.nativeRun&&b.nativeBite&&b.sound&&b.pcmFinite&&b.afterAnimatorSamples>3&&b.approachedDistance>1&&b.returnedHome&&b.jawPeakAngle>8;
            bool body=b.actualContact&&b.bodyContact&&!b.blocked&&b.meshContacts>=1&&b.surfaceBakedFrames>0&&b.surfaceTriangleTests>0&&!string.IsNullOrEmpty(b.surfaceMesh)&&b.surfaceError>=0&&b.surfaceError<.001f&&b.correctedMouthGap>=0&&b.correctedMouthGap<.025f&&b.maxPenetration<.025f;
            bool contact=direct?body&&c.damageReactions==1&&c.shieldReactions==0&&c.damage>0&&c.hpAfter<c.hpBefore
                :block?b.actualContact&&b.blocked&&!b.bodyContact&&b.meshContacts==0&&c.damageReactions==0&&c.shieldReactions==1&&c.hpAfter==c.hpBefore&&arena.LastProtectBlockSound&&c.dodge==null
                :miss?!b.actualContact&&b.meshContacts==0&&b.missSurfaceQueries>0&&b.missSurfaceCrossings==0&&b.avoidedBeforeArrival&&b.dodgeLeadSeconds>.02f&&c.dodgeTravel>.5f&&c.damageReactions==0&&c.shieldReactions==0&&c.hpAfter==c.hpBefore&&c.dodge!=null&&c.dodge.restored
                :body&&c.damageReactions==0&&c.shieldReactions==0&&c.hpAfter==c.hpBefore&&c.dodge==null&&c.dodgeTravel<.001f;
            bool camera=cam!=null&&cam.planned&&cam.entered&&cam.recovered&&cam.safe&&!cam.interrupted&&!cam.recoveryBlocked&&cam.followFrames>3&&cam.axisFrames==cam.followFrames&&cam.actorVisibleFrames>=cam.followFrames*.95f&&cam.strikeFrames>0&&cam.strikeVisibleFrames>=cam.strikeFrames*.95f&&cam.maxSpeed<40&&cam.maxAngularSpeed<200&&cam.maxFovSpeed<35&&cam.travel>.02f;
            c.passed=c.resolved&&geometry&&contact&&camera&&c.rootsRestored&&c.cameraRestored&&c.cleanup&&c.noDuplicateReaction&&c.noFloatingDamageText;cases.Add(c);Debug.Log("[bite-review-case] "+JsonUtility.ToJson(c));arena.EndProtection();
            while(Time.realtimeSinceStartup-began<Mathf.Max(10,(requested-28)/7))yield return null;
        }
        IEnumerator Cancel(float at,string phaseName)
        {
            yield return Setup(0);SetPhase("INTERRUPTION · "+phaseName);var roots=Roots(0);var home=new CameraPose(arena.ArenaCamera);bool done=false;StartCoroutine(Perform());yield return new WaitForSecondsRealtime(at);arena.CancelCurrentAction();
            float deadline=Time.realtimeSinceStartup+1;while(!done&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            var c=new Cancellation{phase=phaseName,at=at,returned=done,rootsRestored=Restored(roots),cameraRestored=home.Restored,cleanup=Clean()};c.passed=c.returned&&c.rootsRestored&&c.cameraRestored&&c.cleanup&&!arena.ActionCameraActive;cancellations.Add(c);Debug.Log("[bite-review-cancel] "+JsonUtility.ToJson(c));
            IEnumerator Perform(){yield return arena.Attack(0,"Fire","Physical",424,true,-1,1);done=true;}
        }
        IEnumerator Replacement()
        {
            yield return Setup(0);var roots=Roots(0);var home=new CameraPose(arena.ArenaCamera);bool oldDone=false;StartCoroutine(Old());yield return new WaitForSecondsRealtime(1.3f);var old=arena.LastBite;
            yield return arena.Attack(0,"Fire","Physical",424,true,-1,1);yield return null;replacementPassed=oldDone&&!old.completed&&old.returnedHome&&arena.LastBite.completed&&Restored(roots)&&home.Restored&&Clean();Debug.Log("[bite-review-replacement] passed="+replacementPassed);
            IEnumerator Old(){yield return arena.Attack(0,"Fire","Physical",424,true,-1,1);oldDone=true;}
        }
        IEnumerator Orthographic()
        {
            yield return Setup(0);var camera=arena.ArenaCamera;camera.orthographic=true;var home=new CameraPose(camera);var roots=Roots(0);
            try{yield return arena.Attack(0,"Fire","Physical",424,true,-1,1);yield return null;orthographicPassed=arena.LastBite.completed&&arena.LastBite.meshContacts>0&&home.Restored&&Restored(roots)&&Clean()&&!arena.ActionCameraActive;Debug.Log("[bite-review-orthographic] passed="+orthographicPassed);}finally{arena.CancelCurrentAction();camera.orthographic=false;}
        }
        IEnumerator Observe(IEnumerator action,string prefix)
        {
            bool done=false;float began=Time.realtimeSinceStartup;int next=0;float[] at={1.2f,1.7f,2.15f,2.6f,4.1f,5.2f};string[] names={"approach","open-jaw","contact","recoil","retreat","settle"};StartCoroutine(Perform());
            while(!done){yield return new WaitForEndOfFrame();float elapsed=Time.realtimeSinceStartup-began;if(elapsed>15){Debug.LogError("Croc Feu timed out");arena.CancelCurrentAction();break;}if(next<at.Length&&elapsed>=at[next]){Capture(prefix+"-"+names[next]+".png",elapsed);next++;}}
            yield return new WaitForEndOfFrame();Capture(prefix+"-restored.png",Time.realtimeSinceStartup-began);
            IEnumerator Perform(){yield return action;done=true;}
        }
        void Capture(string file,float at){ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,file),1200,675);stills.Add(new Still{file=file,actualSeconds=at});}
        void LateUpdate()
        {
            if(ReferenceEquals(previousBite,arena.LastBite))return;
            if(livePrefix!=null&&!liveCaptured&&(arena.LastBite.actualContact||arena.LastBite.originalLaneReached)){liveCaptured=true;ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,livePrefix+"-live-event.png"),1440,900);}
            if(diagnosticPrefix==null||diagnosticCaptured||!arena.LastBite.actualContact||!arena.LastBite.bodyContact)return;diagnosticCaptured=true;
            var camera=arena.ArenaCamera;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try{Vector3 point=arena.LastBite.firstImpactPoint,back=Vector3.Cross(Vector3.up,arena.BattleAxis).normalized;ModernArenaReview.CameraShot(camera,point+arena.LastBite.normal*2.3f+back*1.6f+Vector3.up*.45f,point,45);ModernArenaReview.Capture(camera,Path.Combine(output,diagnosticPrefix+"-surface-diagnostic.png"),1440,900);}finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
        }
        IEnumerator CaptureMotion()
        {
            captureTarget=new RenderTexture(1200,675,24,RenderTextureFormat.ARGB32){antiAliasing=2};captureTarget.Create();captureImage=new Texture2D(1200,675,TextureFormat.RGB24,false);float next=0;
            while(captureLoop){yield return new WaitForEndOfFrame();float now=Time.realtimeSinceStartup;if(!captureEnabled||now<next)continue;var camera=arena.ArenaCamera;var previous=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
                try{camera.targetTexture=captureTarget;camera.aspect=1200f/675;camera.Render();RenderTexture.active=captureTarget;captureImage.ReadPixels(new Rect(0,0,1200,675),0,0);captureImage.Apply(false,false);File.WriteAllBytes(Path.Combine(output,"cycle-motion",captureTimes.Count.ToString("D5")+".png"),captureImage.EncodeToPNG());captureTimes.Add(now-captureStarted);stages.Add(phase);}finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.active=active;}next=now+1f/24;}
            File.WriteAllText(Path.Combine(output,"cycle-motion","frames.json"),JsonUtility.ToJson(new MotionReport{captureTimes=captureTimes.ToArray(),stages=stages.ToArray()},true));ReleaseCapture();captureDone=true;
        }
        void Overlay(){var go=new GameObject("Croc Feu review",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=arena.ArenaCamera;canvas.planeDistance=.5f;canvas.sortingOrder=100;var text=new GameObject("Phase",typeof(RectTransform),typeof(Text));text.transform.SetParent(go.transform,false);var rect=text.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(1400,75);rect.anchoredPosition=new Vector2(0,-15);caption=text.GetComponent<Text>();caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");caption.fontSize=28;caption.alignment=TextAnchor.MiddleCenter;caption.color=new Color(1,.82f,.3f);caption.raycastTarget=false;}
        void SetPhase(string value){phase=value;caption.text=value;}
        void Update(){frames++;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void ReleaseCapture(){if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);captureTarget=null;}if(captureImage!=null){Destroy(captureImage);captureImage=null;}}
        void OnDestroy(){Application.logMessageReceived-=OnLog;captureLoop=captureEnabled=false;ReleaseCapture();if(arena!=null){arena.CancelCurrentAction();arena.EndProtection(true);}}
    }
}
