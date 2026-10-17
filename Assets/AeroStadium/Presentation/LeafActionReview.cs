using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>Focused Razor Leaf 75 review: twelve detached blades, native contact and actual resolved events.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class LeafActionReview : MonoBehaviour
    {
        [Serializable] public sealed class Case
        {
            public int side, seed, damage, hpBefore, hpAfter, damageReactions, shieldReactions;
            public bool blocked, miss, resolved, protectEvent, passed, rootsRestored, cameraRestored, cleanup, noDuplicateReaction;
            public bool shieldSound, guardAnimated;
            public float dodgeTravel, anticipationHeight, strikeHeight;
            public ArenaView.GrassCast leaf;
            public LeafCameraTelemetry camera;
        }
        [Serializable] public sealed class Cancellation
        {
            public string phase; public float at, actualSeconds;
            public bool returned, rootsRestored, cameraRestored, cleanup, passed;
        }
        [Serializable] public sealed class Report
        {
            public string scope = "Razor Leaf 75 only: native Bulbizarre 1 against Tortank 9, real direct hit, Protect and resolved accuracy miss events on both sides. Five cancellations and orthographic fallback are presentation scenarios. Triangle contact uses baked native Body mesh; max one diagnostic close-up per direct case temporarily frames the first actual contact and then restores the ordinary live camera.";
            public bool passed, orthographicFallback; public int errors, frames, captureWidth=1200, captureHeight=675, requestedCaptureFps=24;
            public float seconds; public Case[] cases; public Cancellation[] cancellations;
        }
        [Serializable] sealed class MotionReport
        {
            public int width=1200,height=675,requestedFps=24; public float[] captureTimes; public string[] stages;
        }
        [Serializable] sealed class Still
        {
            public string file; public float scheduled,actual;
        }
        [Serializable] sealed class StillReport { public Still[] stills; }
        sealed class Pose
        {
            public Transform root; public Vector3 position; public Quaternion rotation;
            public Pose(Transform value){root=value;position=value.position;rotation=value.rotation;}
            public bool Restored=>root!=null&&Vector3.Distance(position,root.position)<.001f&&Quaternion.Angle(rotation,root.rotation)<.01f;
        }
        sealed class CameraPose
        {
            public Camera camera; public Vector3 position; public Quaternion rotation; public float fov; public bool orthographic;
            public CameraPose(Camera value){camera=value;position=value.transform.position;rotation=value.transform.rotation;fov=value.fieldOfView;orthographic=value.orthographic;}
            public bool Restored=>camera!=null&&Vector3.Distance(position,camera.transform.position)<.001f&&Quaternion.Angle(rotation,camera.transform.rotation)<.01f&&Mathf.Abs(fov-camera.fieldOfView)<.001f&&orthographic==camera.orthographic;
        }
        ArenaView arena; Catalog catalog; string output,phase="PRÉPARATION"; Text phaseText,detailText,healthText;
        float started,requestedSeconds,captureStarted; int errors,frames;
        bool captureLoop,captureEnabled,captureFinished,fallbackPassed;
        RenderTexture captureTarget; Texture2D captureImage;
        readonly List<Case> cases=new List<Case>(); readonly List<Cancellation> cancellations=new List<Cancellation>();
        readonly List<float> frameTimes=new List<float>(); readonly List<string> frameStages=new List<string>();
        readonly List<Still> stills=new List<Still>();

        public void Begin(ArenaView view,string directory,float seconds)
        {
            arena=view;output=directory;requestedSeconds=Mathf.Clamp(seconds,120,180);
            Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"cycle-motion"));
            var source=Resources.Load<TextAsset>("Data/catalog");if(source==null)throw new InvalidOperationException("Leaf review catalog missing.");
            catalog=JsonUtility.FromJson<Catalog>(source.text);catalog.Validate();
            arena.SetDecisionCameraActive(false);ModernArenaLighting.Apply(arena.ArenaCamera);CreateOverlay();
            ModernArenaReview.WriteWave(Path.Combine(output,"tranch-herbe.wav"),GrassAttackAudio.CreateOriginalPcm(75),.56f);
            Application.logMessageReceived+=OnLog;started=Time.realtimeSinceStartup;StartCoroutine(Run());
        }
        IEnumerator Setup(int side)
        {
            arena.CancelCurrentAction();arena.EndProtection(true);
            arena.ShowPokemon(side,1);arena.ShowPokemon(1-side,9);yield return null;
            ModernArenaReview.CameraShot(arena.ArenaCamera,new Vector3(0,4.25f,-12.6f),new Vector3(0,1.65f,0),48);
        }
        Pose[] Roots(int attackerSide)
        {
            var a=GameObject.Find("Pokemon_"+attackerSide+"_1");var b=GameObject.Find("Pokemon_"+(1-attackerSide)+"_9");
            if(a==null||b==null)throw new InvalidOperationException("Leaf review cannot find both actor roots.");
            return new[]{new Pose(a.transform),new Pose(b.transform)};
        }
        static bool Restored(Pose[] roots){foreach(var root in roots)if(!root.Restored)return false;return true;}
        bool Clean()
        {
            if(FindObjectsByType<GrassAttackEffect>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)return false;
            foreach(var sound in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(sound.name.StartsWith("Tranch'Herbe",StringComparison.Ordinal))return false;
            return true;
        }
        static int FindMove(IReadOnlyList<BattleEvent> events,int side,int move)
        {for(int i=0;i<events.Count;i++)if(events[i].Kind==BattleEventKind.MoveUsed&&events[i].Side==side&&events[i].MoveId==move)return i;return -1;}
        BattleEngine Scenario(int side,int outcome,out IReadOnlyList<BattleEvent> events,out int seed)
        {
            // Search seeds against the unchanged 95% accuracy rule; never manufacture a miss event.
            for(seed=1;seed<=4096;seed++)
            {
                bool block=outcome==1;
                var attack=new TeamMember(1){moveIds=new[]{75}};
                var defend=new TeamMember(9,"rawst-berry"){moveIds=new[]{334,182},evs=new StatValues{hp=252,defense=252}};
                var battle=new BattleEngine(catalog,new[]{side==0?attack:defend},new[]{side==1?attack:defend},seed);
                events=battle.ResolveTurn(BattleChoice.Move(block&&side==1?1:0),BattleChoice.Move(block&&side==0?1:0));
                int move=FindMove(events,side,75);
                bool resolved=outcome==0?BattlePresentationHit.TryGetDirectHit(events,move,out _)
                    :outcome==1?BattlePresentationHit.TryGetBlockedAttack(events,move,out _,out int defender)&&defender==1-side
                    :BattlePresentationHit.IsResolvedMiss(events,move);
                if(resolved)return battle;
            }
            throw new InvalidOperationException("Leaf review cannot resolve its requested hit/Protect/95%-accuracy miss in 4096 deterministic seeds.");
        }
        IEnumerator Run()
        {
            yield return Setup(0);SetPhase("TRANCH'HERBE · REVUE CIBLÉE","Douze feuilles : bourgeon → vol courbe → coupure → dispersion → retour.");
            yield return new WaitForSecondsRealtime(2);
            captureLoop=true;captureStarted=Time.realtimeSinceStartup;StartCoroutine(CaptureMotion());
            for(int side=0;side<2;side++)for(int outcome=0;outcome<3;outcome++)
            {
                float caseStart=Time.realtimeSinceStartup;yield return Setup(side);
                bool blocked=outcome==1,miss=outcome==2;var battle=Scenario(side,outcome,out var events,out int seed);
                int move=FindMove(events,side,75);
                bool direct=BattlePresentationHit.TryGetDirectHitEffectiveness(events,move,out int damageIndex,out double effectiveness);
                bool blockedEvent=BattlePresentationHit.TryGetBlockedAttack(events,move,out _,out int protectedSide)&&protectedSide==1-side;
                bool missedEvent=BattlePresentationHit.IsResolvedMiss(events,move);
                var result=new Case{side=side,seed=seed,blocked=blocked,miss=miss,resolved=blocked?blockedEvent:miss?missedEvent:direct,
                    protectEvent=blocked&&BattlePresentationHit.TryGetProtectActivation(events,FindMove(events,1-side,182),out _),
                    damage=damageIndex>=0?events[damageIndex].Amount:0,hpBefore=battle.Active(1-side).MaxHp,hpAfter=battle.Active(1-side).Hp};
                if(!result.resolved||(blocked&&!result.protectEvent)){Debug.LogError("Leaf review did not resolve its requested real battle event.");}
                Health(result.hpBefore,battle.Active(1-side).MaxHp);
                SetPhase("BULBIZARRE "+(side==0?"À GAUCHE":"À DROITE")+(blocked?" · TORTANK LANCE ABRI":miss?" · RATÉ RÉSOLU":" · COUPURE DIRECTE"),
                    blocked?"Les feuilles rencontrent Abri ; un seul blocage, aucun dégât."
                    :miss?"Tortank quitte la trajectoire : contrôle des croisements avec son maillage natif."
                    :"Une première coupure sur le corps natif déclenche une seule réaction de Tortank.");
                if(blocked)yield return arena.Protect(1-side);
                result.shieldSound=!blocked||arena.LastProtectActivationSound;result.guardAnimated=!blocked||arena.LastProtectAnimated;
                yield return new WaitForSecondsRealtime(2);
                var roots=Roots(side);var cameraHome=new CameraPose(arena.ArenaCamera);
                int damageBefore=arena.DamagePoseCount,blocksBefore=arena.ProtectBlockCount;
                captureEnabled=side==0;
                yield return Observe(arena.Attack(side,"Grass","Physical",75,direct,blocked?1-side:-1,(float)effectiveness,missedEvent),"side"+side+(blocked?"-abri":miss?"-miss":"-direct"));
                captureEnabled=false;
                result.leaf=arena.LastGrass;result.camera=arena.LastLeafCamera;
                result.damageReactions=arena.DamagePoseCount-damageBefore;result.shieldReactions=arena.ProtectBlockCount-blocksBefore;
                result.rootsRestored=Restored(roots);result.cameraRestored=arena.LastActionRestored&&cameraHome.Restored;
                result.dodgeTravel=arena.LastActionDodgeTravel;
                if(direct)
                {
                    int reactionCount=arena.DamagePoseCount;yield return arena.Hit(1-side,result.damage,true,(float)effectiveness);
                    result.noDuplicateReaction=arena.DamagePoseCount==reactionCount;
                }
                else result.noDuplicateReaction=true;
                Health(result.hpAfter,battle.Active(1-side).MaxHp);yield return null;
                result.cleanup=Clean();
                var w=result.leaf;var c=result.camera;
                result.anticipationHeight=c!=null&&c.anticipationFrames>0?c.anticipationHeightSum/c.anticipationFrames:0;
                result.strikeHeight=c!=null&&c.strikeFrames>0?c.strikeHeightSum/c.strikeFrames:0;
                bool kinematics=w.moveId==75&&w.completed&&w.geometry&&w.motion&&w.sound&&w.emissions==12;
                bool exactBody=w.meshContacts>=1&&w.meshBakedFrames>0&&w.triangleTests>0&&!string.IsNullOrEmpty(w.contactMesh)
                    &&Finite(w.meshError)&&w.meshError>=0&&w.meshError<.001f&&Finite(w.renderedTipGap)&&w.renderedTipGap<.02f;
                bool contact=blocked?w.blocked&&!w.impact&&w.contacts>=1&&w.blocks==1&&w.meshContacts==0&&w.reactions==0
                        &&result.damageReactions==0&&result.shieldReactions==1&&result.hpAfter==result.hpBefore&&result.damage==0&&arena.LastProtectBlockSound
                    :miss?!w.blocked&&!w.impact&&w.meshContacts==0&&w.reactions==0&&w.blocks==0&&w.missSurfaceQueries>0&&w.missSurfaceCrossings==0
                        &&result.damageReactions==0&&result.shieldReactions==0&&result.damage==0&&result.hpAfter==result.hpBefore&&result.dodgeTravel>.1f
                    :exactBody&&!w.blocked&&w.impact&&w.contacts>=1&&w.reactions==1&&w.blocks==0&&result.damageReactions==1
                        &&result.shieldReactions==0&&result.damage>0&&result.hpAfter<result.hpBefore;
                bool camera=c!=null&&c.planned&&c.entered&&c.recovered&&c.safe&&!c.interrupted&&!c.recoveryBlocked
                    &&c.followFrames>5&&c.axisFrames==c.followFrames&&c.actorVisibleFrames>=c.followFrames*.95f
                    &&c.anticipationFrames>0&&c.anticipationVisibleFrames>=c.anticipationFrames*.95f
                    &&c.strikeFrames>0&&c.strikeVisibleFrames>=c.strikeFrames*.95f
                    &&c.targetVisibleFrames>=c.followFrames*.95f&&c.flightFrames>0&&c.flightVisibleFrames>=c.flightFrames*.95f
                    &&c.impactFrames>0&&c.impactVisibleFrames>=c.impactFrames*.95f
                    &&result.strikeHeight>=.105f&&result.anticipationHeight>result.strikeHeight*1.10f
                    &&c.entrySeconds>=.80f&&c.recoverySeconds>=1f&&c.travel>.02f
                    &&Finite(c.maxSpeed)&&c.maxSpeed<40&&Finite(c.maxAngularSpeed)&&c.maxAngularSpeed<200&&Finite(c.maxFovSpeed)&&c.maxFovSpeed<35;
                result.passed=result.resolved&&kinematics&&contact&&camera&&result.shieldSound&&result.guardAnimated&&result.noDuplicateReaction&&result.rootsRestored&&result.cameraRestored&&result.cleanup;
                cases.Add(result);Debug.Log("[leaf-review-case] "+JsonUtility.ToJson(result));
                arena.EndProtection();
                SetPhase("RETOUR TERMINÉ · "+(result.passed?"CONTRÔLES VALIDÉS":"CONTRÔLE À EXAMINER"),"12 feuilles · contacts corps "+w.meshContacts+" · croisements raté "+w.missSurfaceCrossings+" · réaction "+result.damageReactions+" · graine "+seed);
                float caseSeconds=Mathf.Max(12,(requestedSeconds-24)/6);
                while(Time.realtimeSinceStartup-caseStart<caseSeconds)yield return null;
                if(side==0&&miss){captureLoop=false;while(!captureFinished)yield return null;}
            }
            float[] stops={.25f,1f,1.5f,2.4f,3.4f};string[] names={"entrée caméra","émission","coupures","dispersion","retour caméra"};
            for(int i=0;i<stops.Length;i++)yield return CheckCancellation(stops[i],names[i]);
            yield return CheckOrthographic();
            arena.EndProtection(true);SetPhase("REVUE TERMINÉE","Tranch'Herbe 75 · Bulbizarre 1 → Tortank 9 · deux côtés · impact / Abri / raté réel · cinq interruptions.");
            while(Time.realtimeSinceStartup-started<requestedSeconds)yield return null;
            bool passed=errors==0&&cases.Count==6&&cancellations.Count==5&&fallbackPassed;
            foreach(var item in cases)passed&=item.passed;foreach(var item in cancellations)passed&=item.passed;
            var report=new Report{passed=passed,orthographicFallback=fallbackPassed,errors=errors,frames=frames,seconds=Time.realtimeSinceStartup-started,cases=cases.ToArray(),cancellations=cancellations.ToArray()};
            File.WriteAllText(Path.Combine(output,"leaf-review.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"stills.json"),JsonUtility.ToJson(new StillReport{stills=stills.ToArray()},true));
            Debug.Log("[leaf-review-result] "+JsonUtility.ToJson(report));Application.Quit(passed?0:1);
        }
        IEnumerator CheckCancellation(float at,string name)
        {
            yield return Setup(0);SetPhase("INTERRUPTION · "+name.ToUpperInvariant(),"Retour des deux acteurs et de la caméra ; disparition des feuilles et du son.");
            var roots=Roots(0);var home=new CameraPose(arena.ArenaCamera);bool done=false;float began=Time.realtimeSinceStartup;
            StartCoroutine(Perform());yield return new WaitForSecondsRealtime(at);arena.CancelCurrentAction();
            float actual=Time.realtimeSinceStartup-began,deadline=Time.realtimeSinceStartup+1;
            while(!done&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            var result=new Cancellation{phase=name,at=at,actualSeconds=actual,returned=done,rootsRestored=Restored(roots),cameraRestored=home.Restored,cleanup=Clean()};
            result.passed=result.returned&&result.rootsRestored&&result.cameraRestored&&result.cleanup&&!arena.ActionCameraActive;
            cancellations.Add(result);Debug.Log("[leaf-review-cancel] "+JsonUtility.ToJson(result));
            IEnumerator Perform(){yield return arena.Attack(0,"Grass","Physical",75,true,-1,1);done=true;}
        }
        IEnumerator CheckOrthographic()
        {
            yield return Setup(0);SetPhase("CAMÉRA ORTHOGRAPHIQUE","Les douze feuilles restent complètes lorsque la caméra d’action ne prend pas la main.");
            var camera=arena.ArenaCamera;var roots=Roots(0);bool original=camera.orthographic,done=false,unchanged=true;
            camera.orthographic=true;var home=new CameraPose(camera);
            try
            {
                StartCoroutine(Perform());float deadline=Time.realtimeSinceStartup+8;
                while(!done&&Time.realtimeSinceStartup<deadline){unchanged&=home.Restored&&!arena.ActionCameraActive;yield return null;}
                yield return null;var w=arena.LastGrass;
                fallbackPassed=done&&unchanged&&home.Restored&&Restored(roots)&&Clean()&&w.moveId==75&&w.completed&&w.geometry&&w.motion
                    &&w.sound&&w.emissions==12&&w.contacts>=1&&w.meshContacts>=1&&w.meshBakedFrames>0&&w.triangleTests>0
                    &&!string.IsNullOrEmpty(w.contactMesh)&&Finite(w.meshError)&&w.meshError>=0&&w.meshError<.001f&&w.reactions==1;
                Debug.Log("[leaf-review-orthographic] passed="+fallbackPassed);
            }
            finally{arena.CancelCurrentAction();camera.orthographic=original;}
            IEnumerator Perform(){yield return arena.Attack(0,"Grass","Physical",75,true,-1,1);done=true;}
        }
        IEnumerator Observe(IEnumerator action,string prefix)
        {
            diagnosticPrefix=prefix.EndsWith("-direct",StringComparison.Ordinal)?prefix:null;diagnosticCaptured=false;
            bool done=false;float began=Time.realtimeSinceStartup;int next=0;
            float[] at={1.05f,1.35f,1.60f,2.25f,3f};string[] names={"release","flight","cuts","scatter","settled"};
            StartCoroutine(Perform());
            while(!done)
            {
                yield return new WaitForEndOfFrame();float elapsed=Time.realtimeSinceStartup-began;
                if(elapsed>12){Debug.LogError("Leaf review action timed out.");arena.CancelCurrentAction();break;}
                while(next<at.Length&&elapsed>=at[next])
                {
                    string file=prefix+"-"+names[next]+".png";ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,file),1200,675);
                    stills.Add(new Still{file=file,scheduled=at[next],actual=elapsed});next++;
                }
            }
            while(Time.realtimeSinceStartup-began<4.10f)yield return null;
            yield return new WaitForEndOfFrame();string restored=prefix+"-restored.png";
            ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,restored),1200,675);
            stills.Add(new Still{file=restored,scheduled=4.10f,actual=Time.realtimeSinceStartup-began});
            diagnosticPrefix=null;
            IEnumerator Perform(){yield return action;done=true;}
        }
        IEnumerator CaptureMotion()
        {
            captureTarget=new RenderTexture(1200,675,24,RenderTextureFormat.ARGB32){antiAliasing=2};captureTarget.Create();
            captureImage=new Texture2D(1200,675,TextureFormat.RGB24,false);float next=0;
            while(captureLoop)
            {
                yield return new WaitForEndOfFrame();float now=Time.realtimeSinceStartup;
                if(!captureEnabled||now<next)continue;
                CaptureFrame();frameTimes.Add(now-captureStarted);frameStages.Add(phase);next=now+1f/24;
            }
            File.WriteAllText(Path.Combine(output,"cycle-motion","frames.json"),JsonUtility.ToJson(new MotionReport{captureTimes=frameTimes.ToArray(),stages=frameStages.ToArray()},true));
            ReleaseCapture();captureFinished=true;
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
        void CreateOverlay()
        {
            var root=new GameObject("Razor Leaf review captions",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=arena.ArenaCamera;canvas.planeDistance=.5f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Phase band",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,126);
            panel.GetComponent<Image>().color=new Color(.025f,.045f,.07f,.83f);
            phaseText=Caption(panel.transform,"Phase",34,new Color(1,.82f,.3f),new Vector2(38,-15),new Vector2(1800,45));
            detailText=Caption(panel.transform,"Details",23,new Color(.9f,.95f,1),new Vector2(40,-69),new Vector2(1800,36));
            healthText=Caption(root.transform,"Tortank health",24,Color.white,new Vector2(38,-147),new Vector2(750,36));
        }
        static Text Caption(Transform parent,string name,int size,Color color,Vector2 position,Vector2 extent)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=extent;
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;return text;
        }
        void SetPhase(string title,string detail){phase=title;phaseText.text=title;detailText.text=detail;}
        void Health(int hp,int maximum){healthText.text="TORTANK · PV "+hp+" / "+maximum;}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        string diagnosticPrefix;bool diagnosticCaptured;
        void LateUpdate()
        {
            if(diagnosticPrefix==null||diagnosticCaptured)return;
            var effect=arena.GetComponentInChildren<GrassAttackEffect>();
            if(effect==null||effect.BodySurfaceContacts<1)return;
            diagnosticCaptured=true;
            // One diagnostic per direct case: the first actual native-body contact. Ordinary live camera is restored immediately.
            var camera=arena.ArenaCamera;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try
            {
                Vector3 point=effect.FirstBodyContactPoint,normal=effect.FirstBodyContactNormal;
                Vector3 back=Vector3.Cross(Vector3.up,arena.BattleAxis).normalized;
                Vector3 view=point+normal*2.7f+back*1.2f+Vector3.up*.45f;
                ModernArenaReview.CameraShot(camera,view,point,43);
                ModernArenaReview.Capture(camera,Path.Combine(output,diagnosticPrefix+"-surface-closeup-first.png"));
            }
            finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
        }
        void Update(){frames++;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void ReleaseCapture(){if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);captureTarget=null;}if(captureImage!=null){Destroy(captureImage);captureImage=null;}}
        void OnDestroy(){Application.logMessageReceived-=OnLog;captureLoop=captureEnabled=false;ReleaseCapture();if(arena!=null){arena.CancelCurrentAction();arena.EndProtection(true);}}
    }
}
