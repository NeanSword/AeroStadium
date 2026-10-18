using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>Native rig dodges, resolved accuracy misses, timing and action cleanup.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class DodgeActionReview : MonoBehaviour
    {
        [Serializable] public sealed class Case
        {
            public int side, defender, seed, damage, hpBefore, hpAfter, damageReactions, shieldReactions;
            public string outcome;
            public bool presentationOnly, resolved, protectEvent, passed, rootsRestored, cameraRestored, cleanup, noDuplicateReaction;
            public bool shieldSound, guardAnimated, avoidedBeforeArrival, noFloatingDamageText=true;
            public bool facialPassed,footworkPassed;
            public float dodgeTravel, firstThreatSeconds=-1, dodgeClearSeconds=-1, dodgeLeadSeconds;
            public ArenaView.GrassCast leaf;
            public LeafCameraTelemetry camera;
            public DodgePoseTelemetry dodge;
        }
        [Serializable] public sealed class Cancellation
        {
            public string phase; public float at, actualSeconds;
            public bool returned, rootsRestored, cameraRestored, poseRestored, cleanup, passed;
            public DodgePoseTelemetry dodge;
        }
        [Serializable] public sealed class Report
        {
            public string scope="Six real Razor Leaf 75 accuracy misses: Bulbizarre 1 attacks Tortank 9, Bulbizarre 1 and Arcanin 59 from both sides. One real direct hit and one real Protect are controls; a non-hit without a resolved miss is a presentation-only control, not an immunity event. Five interruptions cover camera entry, preparation, takeoff, landing and return. The threat time is the leading leaf tip entering the original unmoving body bounds, not an undeformed triangle-contact time. Avoidance is also checked against the actual moving native triangles. Foot lift and pose metrics describe the additive rig layer, not inverse-kinematics contact with every terrain.";
            public bool passed; public int errors, frames, captureWidth=1200, captureHeight=675, requestedCaptureFps=24;
            public float seconds; public Case[] cases; public Cancellation[] cancellations;
        }
        [Serializable] sealed class MotionReport
        {public int width=1200,height=675,requestedFps=24;public float[] captureTimes;public string[] stages;}
        [Serializable] sealed class Still
        {public string file;public float scheduledProgress,actualProgress,actualSeconds;}
        [Serializable] sealed class StillReport {public Still[] stills;}
        sealed class Pose
        {
            readonly Transform root;readonly Vector3 position;readonly Quaternion rotation;
            public Pose(Transform value){root=value;position=value.position;rotation=value.rotation;}
            public bool Restored=>root!=null&&Vector3.Distance(position,root.position)<.001f&&Quaternion.Angle(rotation,root.rotation)<.01f;
        }
        sealed class CameraPose
        {
            readonly Camera camera;readonly Vector3 position;readonly Quaternion rotation;readonly float fov;
            public CameraPose(Camera value){camera=value;position=value.transform.position;rotation=value.transform.rotation;fov=value.fieldOfView;}
            public bool Restored=>camera!=null&&Vector3.Distance(position,camera.transform.position)<.001f&&Quaternion.Angle(rotation,camera.transform.rotation)<.01f&&Mathf.Abs(fov-camera.fieldOfView)<.001f;
        }
        ArenaView arena;Catalog catalog;string output,phase="PRÉPARATION";Text phaseText,detailText,healthText;
        float started,requestedSeconds,captureStarted;int errors,frames;
        bool captureLoop,captureEnabled,captureFinished;
        RenderTexture captureTarget;Texture2D captureImage;
        readonly List<Case> cases=new List<Case>();readonly List<Cancellation> cancellations=new List<Cancellation>();
        readonly List<float> frameTimes=new List<float>();readonly List<string> frameStages=new List<string>();readonly List<Still> stills=new List<Still>();

        public void Begin(ArenaView view,string directory,float seconds)
        {
            arena=view;output=directory;requestedSeconds=Mathf.Clamp(seconds,120,180);
            Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"cycle-motion"));
            var source=Resources.Load<TextAsset>("Data/catalog");if(source==null)throw new InvalidOperationException("Dodge review catalog missing.");
            catalog=JsonUtility.FromJson<Catalog>(source.text);catalog.Validate();
            arena.SetDecisionCameraActive(false);ModernArenaLighting.Apply(arena.ArenaCamera);CreateOverlay();
            ModernArenaReview.WriteWave(Path.Combine(output,"tranch-herbe.wav"),GrassAttackAudio.CreateOriginalPcm(75),.56f);
            Application.logMessageReceived+=OnLog;started=Time.realtimeSinceStartup;StartCoroutine(Run());
        }
        IEnumerator Setup(int side,int defender)
        {
            arena.CancelCurrentAction();arena.EndProtection(true);arena.ShowPokemon(side,1);arena.ShowPokemon(1-side,defender);yield return null;
            ModernArenaReview.CameraShot(arena.ArenaCamera,new Vector3(0,4.25f,-12.6f),new Vector3(0,1.65f,0),48);
        }
        Pose[] Roots(int side,int defender)
        {
            var a=GameObject.Find("Pokemon_"+side+"_1");var b=GameObject.Find("Pokemon_"+(1-side)+"_"+defender);
            if(a==null||b==null)throw new InvalidOperationException("Dodge review cannot find both native actor roots.");
            return new[]{new Pose(a.transform),new Pose(b.transform)};
        }
        static bool Restored(Pose[] roots){foreach(var item in roots)if(!item.Restored)return false;return true;}
        bool Clean()
        {
            if(FindObjectsByType<GrassAttackEffect>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)return false;
            foreach(var layer in FindObjectsByType<DodgePoseMotion>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(!layer.IsClean)return false;
            foreach(var sound in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(sound.name.StartsWith("Tranch'Herbe",StringComparison.Ordinal))return false;
            return true;
        }
        static bool NoFloatingDamageText()
        {
            foreach(var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                string value=text.text;
                if(!string.IsNullOrEmpty(value)&&value.Length>1&&value[0]=='\u2212'&&char.IsDigit(value[1]))return false;
            }
            return true;
        }
        static int FindMove(IReadOnlyList<BattleEvent> events,int side,int move)
        {for(int i=0;i<events.Count;i++)if(events[i].Kind==BattleEventKind.MoveUsed&&events[i].Side==side&&events[i].MoveId==move)return i;return -1;}
        BattleEngine Scenario(int side,int defender,int outcome,out IReadOnlyList<BattleEvent> events,out int seed)
        {
            // Search actual battle seeds; the miss remains a normal 95%-accuracy event.
            for(seed=1;seed<=4096;seed++)
            {
                int quietMove=defender==9?334:defender==1?45:43;
                var attack=new TeamMember(1){moveIds=new[]{75}};
                var defend=new TeamMember(defender,"rawst-berry"){moveIds=new[]{quietMove,182},evs=new StatValues{hp=252,defense=252}};
                var battle=new BattleEngine(catalog,new[]{side==0?attack:defend},new[]{side==1?attack:defend},seed);
                events=battle.ResolveTurn(BattleChoice.Move(outcome==1&&side==1?1:0),BattleChoice.Move(outcome==1&&side==0?1:0));
                int move=FindMove(events,side,75);
                bool resolved=outcome==0?BattlePresentationHit.TryGetDirectHit(events,move,out _)
                    :outcome==1?BattlePresentationHit.TryGetBlockedAttack(events,move,out _,out int protectedSide)&&protectedSide==1-side
                    :BattlePresentationHit.IsResolvedMiss(events,move);
                if(resolved)return battle;
            }
            throw new InvalidOperationException("Dodge review could not resolve the requested actual battle event in 4096 seeds.");
        }
        IEnumerator Run()
        {
            yield return Setup(0,9);SetPhase("ESQUIVES · REVUE DES MODÈLES NATIFS","Appui, impulsion, déplacement articulé et réception avant le retour en garde.");
            yield return new WaitForSecondsRealtime(2);
            captureLoop=true;captureStarted=Time.realtimeSinceStartup;StartCoroutine(CaptureMotion());
            int[] defenders={9,1,59};
            for(int side=0;side<2;side++)
            {
                foreach(int defender in defenders)yield return CheckCase(side,defender,2);
                if(side==0){captureLoop=false;while(!captureFinished)yield return null;}
            }
            yield return CheckCase(0,9,0);yield return CheckCase(0,9,1);yield return CheckCase(0,9,3);
            float[] stops={.25f,.98f,1.23f,2.45f,2.82f};
            string[] names={"entrée caméra","préparation de l’appui","impulsion","réception","retour en garde"};
            for(int i=0;i<stops.Length;i++)yield return CheckCancellation(stops[i],names[i]);
            arena.EndProtection(true);SetPhase("REVUE TERMINÉE","Trois morphologies · six vrais ratés · contact et Abri conservés · cinq interruptions.");
            while(Time.realtimeSinceStartup-started<requestedSeconds)yield return null;
            bool passed=errors==0&&cases.Count==9&&cancellations.Count==5;
            foreach(var item in cases)passed&=item.passed;foreach(var item in cancellations)passed&=item.passed;
            var report=new Report{passed=passed,errors=errors,frames=frames,seconds=Time.realtimeSinceStartup-started,cases=cases.ToArray(),cancellations=cancellations.ToArray()};
            File.WriteAllText(Path.Combine(output,"dodge-review.json"),JsonUtility.ToJson(report,true));
            File.WriteAllText(Path.Combine(output,"stills.json"),JsonUtility.ToJson(new StillReport{stills=stills.ToArray()},true));
            Debug.Log("[dodge-review-result] "+JsonUtility.ToJson(report));Application.Quit(passed?0:1);
        }
        IEnumerator CheckCase(int side,int defender,int outcome)
        {
            float began=Time.realtimeSinceStartup;yield return Setup(side,defender);
            bool blocked=outcome==1,miss=outcome==2,presentationOnly=outcome==3;
            // The non-hit control deliberately supplies no event. It must not be described as immunity.
            var battle=Scenario(side,defender,presentationOnly?2:outcome,out var events,out int seed);
            int move=FindMove(events,side,75);
            bool direct=!presentationOnly&&BattlePresentationHit.TryGetDirectHitEffectiveness(events,move,out _,out _);
            BattlePresentationHit.TryGetDirectHitEffectiveness(events,move,out int damageIndex,out double effectiveness);
            bool blockedEvent=BattlePresentationHit.TryGetBlockedAttack(events,move,out _,out int protectedSide)&&protectedSide==1-side;
            bool missedEvent=!presentationOnly&&BattlePresentationHit.IsResolvedMiss(events,move);
            var result=new Case{side=side,defender=defender,seed=presentationOnly?0:seed,outcome=presentationOnly?"non-hit-without-resolved-miss":blocked?"protect":miss?"accuracy-miss":"direct-hit",
                presentationOnly=presentationOnly,resolved=presentationOnly||(blocked?blockedEvent:miss?missedEvent:direct),
                protectEvent=blocked&&BattlePresentationHit.TryGetProtectActivation(events,FindMove(events,1-side,182),out _),
                damage=damageIndex>=0?events[damageIndex].Amount:0,hpBefore=battle.Active(1-side).MaxHp,hpAfter=battle.Active(1-side).Hp};
            if(!result.resolved||(blocked&&!result.protectEvent))Debug.LogError("Dodge review did not resolve its requested real battle event.");
            string name=Name(defender);Health(name,result.hpBefore,battle.Active(1-side).MaxHp);
            SetPhase(name.ToUpperInvariant()+" "+(side==0?"À DROITE":"À GAUCHE")+(blocked?" · ABRI":miss?" · ESQUIVE":presentationOnly?" · SANS ÉVÉNEMENT RATÉ":" · IMPACT TÉMOIN"),
                miss?"Le corps quitte le couloir avant l’arrivée ; contrôle des triangles natifs en mouvement."
                :blocked?"Abri reste un blocage : aucune esquive et aucun dégât."
                :presentationOnly?"Contrôle de présentation : une absence de dégât seule ne déclenche pas d’esquive."
                :"L’impact normal et sa réaction restent inchangés.");
            if(blocked)yield return arena.Protect(1-side);
            result.shieldSound=!blocked||arena.LastProtectActivationSound;result.guardAnimated=!blocked||arena.LastProtectAnimated;
            yield return new WaitForSecondsRealtime(.7f);
            var roots=Roots(side,defender);var home=new CameraPose(arena.ArenaCamera);
            int damageBefore=arena.DamagePoseCount,blocksBefore=arena.ProtectBlockCount;
            captureEnabled=side==0&&miss;
            portraitActor=miss&&side==0?GameObject.Find("Pokemon_"+(1-side)+"_"+defender)?.transform:null;
            portraitHead=null;
            var portraitModel=portraitActor!=null?portraitActor.GetComponent<NativePokemonModel>():null;
            if(portraitModel!=null)foreach(var bone in portraitModel.ModelRoot.GetComponentsInChildren<Transform>(true))if(bone.name=="Head"){portraitHead=bone;break;}
            string prefix="side"+side+"-species"+defender+"-"+result.outcome;
            if(portraitHead!=null)CapturePortrait(prefix+"-portrait-native.png");
            yield return Observe(arena.Attack(side,"Grass","Physical",75,direct,blocked?1-side:-1,(float)effectiveness,missedEvent),prefix,miss);
            captureEnabled=false;result.leaf=arena.LastGrass;result.camera=arena.LastLeafCamera;result.dodge=arena.LastDodgePose;
            result.damageReactions=arena.DamagePoseCount-damageBefore;result.shieldReactions=arena.ProtectBlockCount-blocksBefore;
            result.rootsRestored=Restored(roots);result.cameraRestored=arena.LastActionRestored&&home.Restored;result.dodgeTravel=arena.LastActionDodgeTravel;
            if(direct)
            {
                int before=arena.DamagePoseCount;yield return arena.Hit(1-side,result.damage,true,(float)effectiveness);
                result.noDuplicateReaction=before==arena.DamagePoseCount;result.noFloatingDamageText=NoFloatingDamageText();
            }
            else result.noDuplicateReaction=true;
            Health(name,result.hpAfter,battle.Active(1-side).MaxHp);yield return null;result.cleanup=Clean();
            var w=result.leaf;var c=result.camera;var d=result.dodge;
            result.firstThreatSeconds=w.firstThreatSeconds;result.dodgeClearSeconds=w.dodgeClearSeconds;result.dodgeLeadSeconds=w.dodgeLeadSeconds;result.avoidedBeforeArrival=w.avoidedBeforeArrival;
            bool kinematics=w.moveId==75&&w.completed&&w.geometry&&w.motion&&w.sound&&w.emissions==12;
            bool exactBody=w.meshContacts>=1&&w.meshBakedFrames>0&&w.triangleTests>0&&!string.IsNullOrEmpty(w.contactMesh)
                &&Finite(w.meshError)&&w.meshError>=0&&w.meshError<.001f&&Finite(w.renderedTipGap)&&w.renderedTipGap<.02f;
            bool articulated=d!=null&&!string.IsNullOrEmpty(d.profile)&&(d.groundedSidestep||d.diagonalBound)
                &&d.jointCount>=1&&d.posedFrames>3&&d.maxJointDegrees>2&&d.maxFootLift>.025f
                &&(d.maxRootHeight>.015f||d.maxBodyYaw>2)&&d.restored&&d.finite&&d.nativeAnimatorPreserved
                &&d.maximumRestorePositionError<.0001f&&d.maximumRestoreAngleError<.1f;
            var f=d!=null?d.facial:null;
            result.facialPassed=!miss||(f!=null&&f.verifiedAtlas&&f.finite&&f.nativeAnimatorPreserved&&f.restored&&f.channels>=2
                &&f.posedFrames>3&&f.focusFrames>0&&f.blinkFrames>0&&f.reliefFrames>0&&f.expressionChanges>=3&&f.maxUvDelta>.001f
                &&f.maximumRestorePositionError<.0001f&&f.maximumRestoreAngleError<.1f);
            result.footworkPassed=!miss||(d!=null&&d.leadingStepFrames>0&&d.trailingStepFrames>0&&d.articulatedFrames>3
                &&d.maxBoneLengthError<.002f&&d.maxHeadCounterYaw>2&&(defender==1||d.maxTailDegrees>2)
                &&(defender!=9||d.maxArmAsymmetry>1));
            bool ahead=w.firstThreatSeconds>0&&w.dodgeClearSeconds>=0&&Finite(w.dodgeLeadSeconds)&&w.dodgeLeadSeconds>.02f&&w.avoidedBeforeArrival;
            bool untouched=w.meshContacts==0&&w.contacts==0&&w.reactions==0&&w.blocks==0&&!w.impact&&!w.blocked
                &&result.damageReactions==0&&result.shieldReactions==0&&result.damage==0&&result.hpAfter==result.hpBefore;
            bool contact=miss?untouched&&w.missSurfaceQueries>0&&w.missSurfaceCrossings==0&&result.dodgeTravel>.5f&&articulated&&ahead
                :blocked?w.blocked&&!w.impact&&w.contacts>=1&&w.blocks==1&&w.meshContacts==0&&w.reactions==0
                    &&result.damageReactions==0&&result.shieldReactions==1&&result.damage==0&&result.hpAfter==result.hpBefore&&arena.LastProtectBlockSound
                    &&d==null&&result.dodgeTravel<.001f
                :presentationOnly?untouched&&d==null&&result.dodgeTravel<.001f
                :exactBody&&!w.blocked&&w.impact&&w.reactions==1&&w.blocks==0&&result.damageReactions==1&&result.shieldReactions==0
                    &&result.damage>0&&result.hpAfter<result.hpBefore&&d==null&&result.dodgeTravel<.001f;
            // These are framing and continuity checks, not a size-dependent demand for a fixed subject height.
            bool camera=c!=null&&c.planned&&c.entered&&c.recovered&&c.safe&&!c.interrupted&&!c.recoveryBlocked&&c.followFrames>5
                &&c.axisFrames==c.followFrames&&c.targetVisibleFrames>=c.followFrames*.95f
                &&(miss?c.dodgeFacialFocus&&c.dodgeCloseFrames>0&&c.dodgeCloseVisibleFrames>=c.dodgeCloseFrames*.95f
                    &&c.dodgeTargetHeightSum/c.dodgeCloseFrames>=.35f&&c.strikeFrames>0&&c.strikeVisibleFrames>=c.strikeFrames*.95f
                    :c.actorVisibleFrames>=c.followFrames*.95f)
                &&c.flightFrames>0&&c.flightVisibleFrames>=c.flightFrames*.95f&&c.entrySeconds>=.80f&&c.recoverySeconds>=1f
                &&c.travel>.02f&&Finite(c.maxSpeed)&&c.maxSpeed<40&&Finite(c.maxAngularSpeed)&&c.maxAngularSpeed<200&&Finite(c.maxFovSpeed)&&c.maxFovSpeed<35;
            result.passed=result.resolved&&kinematics&&contact&&camera&&result.facialPassed&&result.footworkPassed&&result.shieldSound&&result.guardAnimated&&result.noDuplicateReaction&&result.noFloatingDamageText&&result.rootsRestored&&result.cameraRestored&&result.cleanup;
            cases.Add(result);Debug.Log("[dodge-review-case] "+JsonUtility.ToJson(result));arena.EndProtection();
            SetPhase(name.ToUpperInvariant()+" · "+(result.passed?"CONTRÔLES VALIDÉS":"CONTRÔLE À EXAMINER"),
                "Croisements : "+w.missSurfaceCrossings+" · avance : "+w.dodgeLeadSeconds.ToString("F3")+" s · réaction : "+result.damageReactions);
            float caseSeconds=Mathf.Max(7,(requestedSeconds-24)/9);
            while(Time.realtimeSinceStartup-began<caseSeconds)yield return null;
        }
        IEnumerator CheckCancellation(float at,string name)
        {
            yield return Setup(0,59);Scenario(0,59,2,out var events,out _);
            bool realMiss=BattlePresentationHit.IsResolvedMiss(events,FindMove(events,0,75));
            SetPhase("INTERRUPTION · "+name.ToUpperInvariant(),"Arrêt de la couche articulée, retour de l’acteur et de la caméra ; aucun effet résiduel.");
            var roots=Roots(0,59);var home=new CameraPose(arena.ArenaCamera);bool done=false;float began=Time.realtimeSinceStartup;
            StartCoroutine(Perform());yield return new WaitForSecondsRealtime(at);arena.CancelCurrentAction();
            float actual=Time.realtimeSinceStartup-began,deadline=Time.realtimeSinceStartup+1;
            while(!done&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            var pose=arena.LastDodgePose;
            var result=new Cancellation{phase=name,at=at,actualSeconds=actual,returned=done,rootsRestored=Restored(roots),cameraRestored=home.Restored,
                cleanup=Clean(),dodge=pose,poseRestored=pose!=null&&pose.restored&&pose.finite&&pose.nativeAnimatorPreserved&&pose.facial!=null&&pose.facial.restored
                    &&pose.maximumRestorePositionError<.0001f&&pose.maximumRestoreAngleError<.1f};
            result.passed=realMiss&&result.returned&&result.rootsRestored&&result.cameraRestored&&result.poseRestored&&result.cleanup&&!arena.ActionCameraActive;
            cancellations.Add(result);Debug.Log("[dodge-review-cancel] "+JsonUtility.ToJson(result));
            IEnumerator Perform(){yield return arena.Attack(0,"Grass","Physical",75,false,-1,0,realMiss);done=true;}
        }
        IEnumerator Observe(IEnumerator action,string prefix,bool dodge)
        {
            bool done=false;float began=Time.realtimeSinceStartup;int next=0,portraitNext=0;
            float[] portraits={.32f,.385f,.635f};string[] portraitNames={"focused","blink","relief"};
            float[] at=dodge?new[]{.035f,.11f,.22f,.32f,.65f,.82f,.98f}:new[]{.12f,.30f,.55f,.9f};
            string[] names=dodge?new[]{"anticipation","push-off","clear","passing-leaves","hold","landing","return"}:new[]{"release","flight","contact","scatter"};
            StartCoroutine(Perform());
            while(!done)
            {
                yield return new WaitForEndOfFrame();float elapsed=Time.realtimeSinceStartup-began;
                if(elapsed>12){Debug.LogError("Dodge review action timed out.");arena.CancelCurrentAction();break;}
                var effect=arena.GetComponentInChildren<GrassAttackEffect>();
                if(effect!=null&&portraitHead!=null&&portraitNext<portraits.Length&&effect.EffectProgress>=portraits[portraitNext]){
                    CapturePortrait(prefix+"-portrait-"+portraitNames[portraitNext]+".png");portraitNext++;
                }
                // At most one still per rendered frame. Actual progress is retained when a slow capture crosses a cue.
                if(effect!=null&&next<at.Length&&effect.EffectProgress>=at[next])
                {
                    string file=prefix+"-"+names[next]+".png";float progress=effect.EffectProgress;
                    ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,file),1200,675);
                    stills.Add(new Still{file=file,scheduledProgress=at[next],actualProgress=progress,actualSeconds=elapsed});next++;
                }
            }
            yield return new WaitForEndOfFrame();string restored=prefix+"-restored.png";
            ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,restored),1200,675);
            stills.Add(new Still{file=restored,scheduledProgress=1,actualProgress=1,actualSeconds=Time.realtimeSinceStartup-began});
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
        Transform portraitActor,portraitHead;Camera portraitCamera;
        void CapturePortrait(string file)
        {
            if(portraitHead==null||portraitActor==null)return;
            if(portraitCamera==null){var go=new GameObject("Dodge expression diagnostic camera");portraitCamera=go.AddComponent<Camera>();portraitCamera.enabled=false;}
            portraitCamera.CopyFrom(arena.ArenaCamera);portraitCamera.enabled=false;
            var model=portraitActor.GetComponent<NativePokemonModel>();float height=model!=null?model.WorldModelHeight:1.6f;
            Vector3 front=-portraitActor.forward,back=Vector3.Cross(Vector3.up,arena.BattleAxis).normalized;
            Vector3 focus=portraitHead.position+front*height*.025f;
            portraitCamera.transform.position=focus+front*height*.83f+back*height*.43f+Vector3.up*height*.06f;
            portraitCamera.transform.LookAt(focus);portraitCamera.fieldOfView=34;
            ModernArenaReview.Capture(portraitCamera,Path.Combine(output,file),960,960);
        }
        void CreateOverlay()
        {
            var root=new GameObject("Dodge review captions",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=arena.ArenaCamera;canvas.planeDistance=.5f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Phase band",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,126);
            panel.GetComponent<Image>().color=new Color(.025f,.045f,.07f,.83f);
            phaseText=Caption(panel.transform,"Phase",34,new Color(1,.82f,.3f),new Vector2(38,-15),new Vector2(1800,45));
            detailText=Caption(panel.transform,"Details",23,new Color(.9f,.95f,1),new Vector2(40,-69),new Vector2(1800,36));
            healthText=Caption(root.transform,"Defender health",24,Color.white,new Vector2(38,-147),new Vector2(750,36));
        }
        static Text Caption(Transform parent,string name,int size,Color color,Vector2 position,Vector2 extent)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=extent;
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;return text;
        }
        static string Name(int id)=>id==9?"Tortank":id==1?"Bulbizarre":"Arcanin";
        void SetPhase(string title,string detail){phase=title;phaseText.text=title;detailText.text=detail;}
        void Health(string name,int hp,int maximum){healthText.text=name.ToUpperInvariant()+" · PV "+hp+" / "+maximum;}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void Update(){frames++;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void ReleaseCapture(){if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);captureTarget=null;}if(captureImage!=null){Destroy(captureImage);captureImage=null;}}
        void OnDestroy(){if(portraitCamera!=null)Destroy(portraitCamera.gameObject);Application.logMessageReceived-=OnLog;captureLoop=captureEnabled=false;ReleaseCapture();if(arena!=null){arena.CancelCurrentAction();arena.EndProtection(true);}}
    }
}
