using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    [DefaultExecutionOrder(240)]
    public sealed class EnergyBallActionReview : MonoBehaviour
    {
        [Serializable] public sealed class Case
        {
            public int side,seed,damage,hpBefore,hpAfter,preparationAccuracyStage,damageReactions,shieldReactions,reactionTier,audioFrames,audioAdvanceFrames,impactAudioFrames;
            public string outcome,provenance;public float effectiveness,reactionEffectiveness,dodgeTravel,audioOutputPeak,impactOutputPeak;
            public bool resolved,passed,nativeModels,rootsRestored,cameraRestored,cleanup,noDuplicateReaction,noFloatingDamageText;
            public bool prelaunchImage,flightImage,eventImage,contactAudioOnly=true,precontactAudioObserved,shieldSound;
            public ArenaView.EnergyBallCast energyBall;public DodgePoseTelemetry dodge;
        }
        [Serializable] public sealed class Cancellation
        {public string phase;public float at;public bool reached,returned,cancelled,rootsRestored,cameraRestored,cleanup,passed;}
        [Serializable] public sealed class Report
        {
            public string scope="Energy Ball 412: native Bulbasaur 1 against Blastoise 9 in modern Elite Four; actual BattleEngine direct hit, Protect and accuracy miss on both sides. The 100%-accuracy miss follows a legal Muddy Water accuracy reduction and a deterministic seed search. Non-hit and effectiveness 0/.25/.5/1/4 are explicit presentation controls, not fabricated battle outcomes; Grass has no type immunity in these pairings. Actual-camera stills and cycle-motion frames retain the action camera. Separately named surface-diagnostic images temporarily reframe real triangle contacts. Technical checks do not certify subjective visual quality or all species.";
            public bool passed,orthographic,replacement,actorReplacement,recordedSound,soundtrack;public int errors,frames;public float seconds;
            public Case[] cases;public Cancellation[] cancellations;
        }
        [Serializable] sealed class MotionReport{public int width=1200,height=675,requestedFps=24;public float[] captureTimes;public string[] stages;}
        [Serializable] sealed class Still{public string file,phase,view;public int frame;public float actionSeconds,effectSeconds;}
        [Serializable] sealed class Stills{public Still[] stills;}
        sealed class Pose
        {
            readonly Transform root;readonly Vector3 position;readonly Quaternion rotation;readonly Animator animator;readonly float speed;
            public Pose(Transform value){root=value;position=value.position;rotation=value.rotation;animator=value.GetComponentInChildren<Animator>();speed=animator!=null?animator.speed:1;}
            public bool Restored=>root!=null&&Vector3.Distance(position,root.position)<.001f&&Quaternion.Angle(rotation,root.rotation)<.01f&&(animator==null||Mathf.Abs(speed-animator.speed)<.001f);
        }
        sealed class CameraPose
        {
            readonly Camera camera;readonly Vector3 position;readonly Quaternion rotation;readonly float fov,size;readonly bool orthographic;
            public CameraPose(Camera value){camera=value;position=value.transform.position;rotation=value.transform.rotation;fov=value.fieldOfView;size=value.orthographicSize;orthographic=value.orthographic;}
            public bool Restored=>camera!=null&&Vector3.Distance(position,camera.transform.position)<.001f&&Quaternion.Angle(rotation,camera.transform.rotation)<.01f&&Mathf.Abs(fov-camera.fieldOfView)<.001f&&Mathf.Abs(size-camera.orthographicSize)<.001f&&orthographic==camera.orthographic;
        }
        ArenaView arena;Catalog catalog;string output,phase="PRÉPARATION",prefix;Text caption;float started,requested,actionStarted,captureStarted;
        int errors,frames;bool captureLoop,captureEnabled,captureDone,orthographicPassed,replacementPassed,actorReplacementPassed,diagnosticCaptured;
        Case observed;EnergyBallEffect observedEffect;int previousAudioSample=-1;readonly float[] audioSamples=new float[64];RenderTexture captureTarget;Texture2D captureImage;EnergyBallReviewAudio audioRecorder;bool recordedSound;
        readonly List<Case> cases=new List<Case>();readonly List<Cancellation> cancellations=new List<Cancellation>();
        readonly List<float> captureTimes=new List<float>();readonly List<string> stages=new List<string>();readonly List<Still> stills=new List<Still>();
        public void Begin(ArenaView view,string directory,float seconds)
        {
            arena=view;output=directory;requested=Mathf.Clamp(seconds,120,180);Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"cycle-motion"));
            var source=Resources.Load<TextAsset>("Data/catalog");if(source==null)throw new InvalidOperationException("Energy Ball review catalog missing.");
            catalog=JsonUtility.FromJson<Catalog>(source.text);catalog.Validate();arena.SetDecisionCameraActive(false);ModernArenaLighting.Apply(arena.ArenaCamera);Overlay();
            ModernArenaReview.WriteWave(Path.Combine(output,"energy-ball-gathering.wav"),EnergyBallEffect.CreateOriginalPcm(),.48f);
            ModernArenaReview.WriteWave(Path.Combine(output,"energy-ball-contact.wav"),EnergyBallEffect.CreateOriginalPcm(true),.48f);
            var listener=FindFirstObjectByType<AudioListener>();
            if(listener==null)throw new InvalidOperationException("The review needs an active audio listener for video recording.");
            captureLoop=true;captureStarted=Time.realtimeSinceStartup;
            audioRecorder=listener.gameObject.AddComponent<EnergyBallReviewAudio>();
            audioRecorder.BeginCapture(output,AudioSettings.dspTime,captureStarted,120);
            StartCoroutine(CaptureMotion());
            var music=GetComponent<BattleMusicAudio>();if(music!=null)music.EnterBattle();
            Application.logMessageReceived+=OnLog;started=Time.realtimeSinceStartup;StartCoroutine(Run());
        }
        IEnumerator Setup(int side)
        {
            arena.CancelCurrentAction();arena.EndProtection(true);arena.ShowPokemon(side,1);arena.ShowPokemon(1-side,9);yield return null;
            ModernArenaReview.CameraShot(arena.ArenaCamera,new Vector3(0,4.25f,-12.6f),new Vector3(0,1.65f,0),48);
        }
        Pose[] Roots(int side)=>new[]{new Pose(GameObject.Find("Pokemon_"+side+"_1").transform),new Pose(GameObject.Find("Pokemon_"+(1-side)+"_9").transform)};
        static bool Restored(Pose[] values){foreach(var value in values)if(!value.Restored)return false;return true;}
        bool Native(int side){var a=GameObject.Find("Pokemon_"+side+"_1")?.GetComponent<NativePokemonModel>();var b=GameObject.Find("Pokemon_"+(1-side)+"_9")?.GetComponent<NativePokemonModel>();return a!=null&&b!=null&&a.IsPlayingNative&&b.IsPlayingNative;}
        bool Clean()
        {
            if(FindObjectsByType<EnergyBallEffect>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=0)return false;
            foreach(var sound in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(sound.clip!=null&&sound.clip.name.StartsWith("Original nature energy",StringComparison.Ordinal))return false;
            foreach(var pose in FindObjectsByType<DodgePoseMotion>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!pose.IsClean)return false;
            foreach(var reaction in FindObjectsByType<PokemonHitReaction>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!reaction.IsClean)return false;
            return true;
        }
        static bool NoFloatingDamageText(){foreach(var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!string.IsNullOrEmpty(text.text)&&text.text.StartsWith("−",StringComparison.Ordinal))return false;return true;}
        static int FindMove(IReadOnlyList<BattleEvent> events,int side,int move){for(int i=0;i<events.Count;i++)if(events[i].Kind==BattleEventKind.MoveUsed&&events[i].Side==side&&events[i].MoveId==move)return i;return -1;}
        BattleEngine Scenario(int side,int outcome,out IReadOnlyList<BattleEvent> events,out int seed,out int accuracyStage)
        {
            events=null;accuracyStage=0;
            for(seed=1;seed<=4096;seed++)
            {
                var attack=new TeamMember(1){moveIds=new[]{412,74},ivs=new StatValues(0),evs=new StatValues{hp=252,specialDefense=252}};
                var defend=new TeamMember(9){moveIds=new[]{334,182,330},evs=new StatValues{hp=252,specialDefense=252}};
                var battle=new BattleEngine(catalog,new[]{side==0?attack:defend},new[]{side==1?attack:defend},seed);
                if(outcome==2)
                {
                    // These are legal moves from the unchanged catalog. The engine, not this review, applies the accuracy stage.
                    battle.ResolveTurn(BattleChoice.Move(side==0?1:2),BattleChoice.Move(side==1?1:2));
                    if(battle.Active(side).AccuracyStage>=0||battle.Active(side).IsFainted)continue;
                }
                accuracyStage=battle.Active(side).AccuracyStage;
                events=battle.ResolveTurn(BattleChoice.Move(outcome==1&&side==1?1:0),BattleChoice.Move(outcome==1&&side==0?1:0));
                int used=FindMove(events,side,412);bool valid=outcome==0?BattlePresentationHit.TryGetDirectHit(events,used,out _):outcome==1?BattlePresentationHit.TryGetBlockedAttack(events,used,out _,out int target)&&target==1-side:BattlePresentationHit.IsResolvedMiss(events,used);
                if(valid&&battle.Active(1-side).Hp>0)return battle;
            }
            throw new InvalidOperationException("Energy Ball review cannot resolve requested legal battle outcome in 4096 seeds.");
        }
        IEnumerator Run()
        {
            yield return Setup(0);SetPhase("ÉCO-SPHÈRE · REVUE CIBLÉE");yield return new WaitForSecondsRealtime(2);
            for(int side=0;side<2;side++)for(int outcome=0;outcome<3;outcome++)yield return CheckCase(side,outcome,2);
            yield return CheckCase(0,3,2);
            foreach(float efficacy in new[]{0f,.25f,.5f,1f,4f})yield return CheckCase(0,4,efficacy);
            foreach(string stage in new[]{"charge","flight","impact"})yield return Cancel(stage);
            yield return Replacement(false);yield return Replacement(true);yield return Orthographic();
            arena.EndProtection(true);SetPhase("REVUE ÉCO-SPHÈRE TERMINÉE");while(Time.realtimeSinceStartup-started<requested)yield return null;
            captureLoop=false;while(!captureDone)yield return null;
            recordedSound=audioRecorder.FinishCapture(AudioSettings.dspTime);Destroy(audioRecorder);audioRecorder=null;
            var music=GetComponent<BattleMusicAudio>();bool soundtrack=music!=null&&music.IntroPlaybackVerified&&music.LoopPlaybackVerified&&music.CompletedLoops>=1&&System.Math.Abs(music.ScheduledJoinError)<.000001;
            Debug.Log("[energy-ball-review-music] passed="+soundtrack+(music!=null?music.Diagnostics:" missing"));
            bool passed=soundtrack&&errors==0&&cases.Count==12&&cancellations.Count==3&&orthographicPassed&&replacementPassed&&actorReplacementPassed&&recordedSound;
            foreach(var c in cases)passed&=c.passed;foreach(var c in cancellations)passed&=c.passed;
            var report=new Report{passed=passed,errors=errors,frames=frames,seconds=Time.realtimeSinceStartup-started,cases=cases.ToArray(),cancellations=cancellations.ToArray(),orthographic=orthographicPassed,replacement=replacementPassed,actorReplacement=actorReplacementPassed,recordedSound=recordedSound,soundtrack=soundtrack};
            File.WriteAllText(Path.Combine(output,"energy-ball-review.json"),JsonUtility.ToJson(report,true));File.WriteAllText(Path.Combine(output,"stills.json"),JsonUtility.ToJson(new Stills{stills=stills.ToArray()},true));
            Debug.Log("[energy-ball-review-result] "+JsonUtility.ToJson(report));Application.Quit(passed?0:1);
        }
        IEnumerator CheckCase(int side,int outcome,float efficacy)
        {
            yield return Setup(side);bool block=outcome==1,miss=outcome==2,control=outcome>=3,nonhit=outcome==3;
            var battle=Scenario(side,control?0:outcome,out var events,out int seed,out int accuracyStage);int used=FindMove(events,side,412);
            bool direct=BattlePresentationHit.TryGetDirectHitEffectiveness(events,used,out int damageIndex,out double resolvedEfficacy);
            bool blocked=BattlePresentationHit.TryGetBlockedAttack(events,used,out _,out int protectedSide)&&protectedSide==1-side;
            bool missed=BattlePresentationHit.IsResolvedMiss(events,used);if(!control)efficacy=(float)resolvedEfficacy;
            var c=new Case{side=side,seed=control?0:seed,outcome=nonhit?"non-hit-control":outcome==4?"effectiveness-control-"+efficacy.ToString(System.Globalization.CultureInfo.InvariantCulture):block?"protect":miss?"accuracy-miss":"direct-hit",
                provenance=control?"Presentation-only control; no claim of matching BattleEngine damage or immunity":miss?"Actual BattleEngine Miss after legal Growth74 / MuddyWater330 accuracy drop":"Actual BattleEngine resolved turn",preparationAccuracyStage=accuracyStage,
                resolved=control||(block?blocked:miss?missed:direct),effectiveness=efficacy,damage=control?0:damageIndex>=0?events[damageIndex].Amount:0,hpBefore=battle.Active(1-side).MaxHp,hpAfter=control?battle.Active(1-side).MaxHp:battle.Active(1-side).Hp,nativeModels=Native(side)};
            if(block)yield return arena.Protect(1-side);SetPhase("BULBIZARRE "+(side==0?"À GAUCHE":"À DROITE")+" · "+(block?"ABRI":miss?"ESQUIVE":nonhit?"SANS IMPACT":outcome==4?"RÉACTION · "+efficacy:"IMPACT"));yield return new WaitForSecondsRealtime(.3f);
            var roots=Roots(side);var home=new CameraPose(arena.ArenaCamera);int reactions=arena.DamagePoseCount,blocks=arena.ProtectBlockCount;
            prefix="side"+side+"-"+c.outcome;observed=c;observedEffect=null;previousAudioSample=-1;diagnosticCaptured=false;actionStarted=Time.realtimeSinceStartup;captureEnabled=!control;
            yield return Observe(arena.Attack(side,"Grass","Special",412,!block&&!miss&&!nonhit,block?1-side:-1,efficacy,miss));
            captureEnabled=false;observed=null;prefix=null;c.energyBall=arena.LastEnergyBall;c.dodge=arena.LastDodgePose;
            c.damageReactions=arena.DamagePoseCount-reactions;c.shieldReactions=arena.ProtectBlockCount-blocks;c.dodgeTravel=arena.LastActionDodgeTravel;c.rootsRestored=Restored(roots);c.cameraRestored=home.Restored&&arena.LastActionRestored&&!arena.ActionCameraActive;
            var reaction=arena.GetDamageReaction(1-side);c.reactionTier=reaction!=null?reaction.Tier:0;c.reactionEffectiveness=reaction!=null?reaction.Effectiveness:0;
            c.noDuplicateReaction=true;if(!block&&!miss&&!nonhit&&efficacy>0){int before=arena.DamagePoseCount;yield return arena.Hit(1-side,c.damage,true,efficacy);c.noDuplicateReaction=before==arena.DamagePoseCount;}
            yield return null;c.cleanup=Clean();c.noFloatingDamageText=NoFloatingDamageText();c.shieldSound=!block||arena.LastProtectBlockSound;
            var e=c.energyBall;bool contact=e.bodyContact||e.blocked;
            bool geometry=e.completed&&!e.cancelled&&e.geometry&&e.launched&&e.detached&&e.sound&&e.contactSourceAvailable&&!e.contactQueryFailed&&e.bodyGeometryAvailable&&(contact?e.triangleTests>0:e.missSurfaceQueries>0&&e.meshBakedFrames>0);
            bool exact=!contact||(Finite(e.surfaceError)&&e.surfaceError>=0&&e.surfaceError<=.002f&&Finite(e.contactRadiusError)&&e.contactRadiusError>=0&&e.contactRadiusError<=.002f&&Finite(e.nearestCoreVertexGap)&&e.nearestCoreVertexGap>=0&&!string.IsNullOrEmpty(e.contactMesh)&&e.impactSeconds>=EnergyBallEffect.ReleaseTime&&e.impactSound);
            bool outcomePassed=block?e.blocked&&!e.bodyContact&&e.shieldGeometryAvailable&&e.blocks==1&&e.reactions==0&&c.shieldReactions==1&&c.damageReactions==0&&c.damage==0&&c.hpAfter==c.hpBefore:
                miss||nonhit?!contact&&!e.impactSound&&e.reactions==0&&e.blocks==0&&e.missSurfaceQueries>0&&e.missSurfaceCrossings==0&&c.damageReactions==0&&c.shieldReactions==0&&c.hpAfter==c.hpBefore&&(!miss||(accuracyStage<0&&e.originalLaneReached&&e.avoidedBeforeArrival&&e.dodgeLeadSeconds>0&&c.dodgeTravel>.1f)):
                e.bodyContact&&!e.blocked&&e.blocks==0&&e.meshBakedFrames>0&&e.reactions==(efficacy>0?1:0)&&c.damageReactions==(efficacy>0?1:0)&&c.shieldReactions==0&&(control||c.damage>0&&c.hpAfter<c.hpBefore)&&(efficacy<=0||c.reactionTier==Tier(efficacy)&&Mathf.Abs(c.reactionEffectiveness-efficacy)<.001f);
            bool audio=c.audioFrames>=2&&c.audioAdvanceFrames>=2&&c.audioOutputPeak>0&&(!contact||c.impactAudioFrames>0&&c.impactOutputPeak>0);
            c.passed=c.resolved&&c.nativeModels&&geometry&&exact&&outcomePassed&&audio&&c.prelaunchImage&&c.flightImage&&c.eventImage&&c.precontactAudioObserved&&c.contactAudioOnly&&c.shieldSound&&c.rootsRestored&&c.cameraRestored&&c.cleanup&&c.noDuplicateReaction&&c.noFloatingDamageText;
            cases.Add(c);Debug.Log("[energy-ball-review-case] "+JsonUtility.ToJson(c));arena.EndProtection();
        }
        static int Tier(float efficacy)=>efficacy<=0?0:efficacy<=.25f?1:efficacy<=.5f?2:efficacy<2?3:efficacy<4?4:5;
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        EnergyBallEffect Live()=>FindFirstObjectByType<EnergyBallEffect>();
        IEnumerator Observe(IEnumerator action)
        {
            bool done=false;StartCoroutine(Perform());float deadline=Time.realtimeSinceStartup+12;
            while(!done&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!done){Debug.LogError("Energy Ball review action timed out.");arena.CancelCurrentAction();}
            yield return new WaitForEndOfFrame();Capture(prefix+"-actual-restored.png","restored","actual-camera",null);
            IEnumerator Perform(){yield return action;done=true;}
        }
        void LateUpdate()
        {
            if(observed==null||prefix==null)return;if(observedEffect==null)observedEffect=Live();var e=observedEffect;if(e==null)return;bool contact=e.BodyContact||e.ShieldContact;
            foreach(var sound in e.GetComponents<AudioSource>())
            {
                if(sound.clip==null||!sound.enabled||sound.mute||sound.volume<=0||!sound.isPlaying)continue;
                bool impact=sound.clip.name=="Original nature energy contact pulse";if(impact){observed.impactAudioFrames++;observed.contactAudioOnly&=contact;}
                else{observed.audioFrames++;int cursor=sound.timeSamples;if(previousAudioSample>=0&&previousAudioSample!=cursor)observed.audioAdvanceFrames++;previousAudioSample=cursor;}
                sound.GetOutputData(audioSamples,0);foreach(float value in audioSamples){if(!Finite(value)){observed.contactAudioOnly=false;continue;}if(impact)observed.impactOutputPeak=Mathf.Max(observed.impactOutputPeak,Mathf.Abs(value));else observed.audioOutputPeak=Mathf.Max(observed.audioOutputPeak,Mathf.Abs(value));}
            }
            if(!contact){observed.precontactAudioObserved=true;observed.contactAudioOnly&=!e.ImpactSoundStarted;}
            if(!observed.prelaunchImage&&e.EffectSeconds>=.30f&&!e.Launched){observed.prelaunchImage=true;Capture(prefix+"-actual-prelaunch.png","charge","actual-camera",e);}
            if(!observed.flightImage&&e.Launched&&e.Detached&&!contact){observed.flightImage=true;Capture(prefix+"-actual-flight.png","flight","actual-camera",e);}
            bool eventReady=contact||observed.outcome=="accuracy-miss"&&e.OriginalLaneReached||observed.outcome=="non-hit-control"&&e.EffectSeconds>1.35f;
            if(!observed.eventImage&&eventReady){observed.eventImage=true;Capture(prefix+"-actual-event.png",e.ShieldContact?"shield":e.BodyContact?"contact":"miss","actual-camera",e);}
            if(!contact||diagnosticCaptured)return;diagnosticCaptured=true;
            var camera=arena.ArenaCamera;Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try{Vector3 side=Vector3.Cross(Vector3.up,arena.BattleAxis).normalized;ModernArenaReview.CameraShot(camera,e.ContactPoint+e.ContactNormal*2.3f+side*1.4f+Vector3.up*.4f,e.ContactPoint,45);Capture(prefix+"-surface-diagnostic.png","contact","surface-diagnostic",e);}
            finally{camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
        }
        void Capture(string file,string stage,string view,EnergyBallEffect effect)
        {
            ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,file),1200,675);
            stills.Add(new Still{file=file,phase=stage,view=view,frame=Time.frameCount,actionSeconds=Time.realtimeSinceStartup-actionStarted,effectSeconds=effect!=null?effect.EffectSeconds:-1});
        }
        IEnumerator Cancel(string stage)
        {
            yield return Setup(0);SetPhase("INTERRUPTION · "+stage.ToUpperInvariant());var roots=Roots(0);var home=new CameraPose(arena.ArenaCamera);bool done=false,reached=false;float began=Time.realtimeSinceStartup;
            StartCoroutine(Perform());float deadline=began+10;
            while(!done&&Time.realtimeSinceStartup<deadline)
            {
                yield return new WaitForEndOfFrame();var e=Live();if(e==null)continue;
                reached=stage=="charge"?e.EffectSeconds>=.25f&&!e.Launched:stage=="flight"?e.Launched&&e.Detached&&!e.BodyContact:e.BodyContact;
                if(reached)break;
            }
            float at=Time.realtimeSinceStartup-began;arena.CancelCurrentAction();deadline=Time.realtimeSinceStartup+1;while(!done&&Time.realtimeSinceStartup<deadline)yield return null;yield return null;
            var c=new Cancellation{phase=stage,at=at,reached=reached,returned=done,cancelled=arena.LastEnergyBall.cancelled,rootsRestored=Restored(roots),cameraRestored=home.Restored,cleanup=Clean()};
            c.passed=c.reached&&c.returned&&c.cancelled&&c.rootsRestored&&c.cameraRestored&&c.cleanup&&!arena.ActionCameraActive;cancellations.Add(c);Debug.Log("[energy-ball-review-cancel] "+JsonUtility.ToJson(c));
            IEnumerator Perform(){yield return arena.Attack(0,"Grass","Special",412,true,-1,2);done=true;}
        }
        IEnumerator Replacement(bool actor)
        {
            yield return Setup(0);SetPhase(actor?"REMPLACEMENT DU POKÉMON":"REMPLACEMENT DE L’ACTION");var home=new CameraPose(arena.ArenaCamera);var roots=Roots(0);bool oldDone=false;StartCoroutine(Old());float deadline=Time.realtimeSinceStartup+8;
            while(Time.realtimeSinceStartup<deadline){var e=Live();if(e!=null&&e.Launched&&e.Detached&&!e.BodyContact)break;yield return null;}
            var old=arena.LastEnergyBall;bool flight=Live()!=null&&Live().Launched&&!Live().BodyContact;
            if(actor){arena.ShowPokemon(0,1);yield return null;yield return null;roots=Roots(0);}
            yield return arena.Attack(0,"Grass","Special",412,true,-1,2);yield return SettleReaction(1);yield return null;
            bool passed=flight&&oldDone&&old.cancelled&&!old.completed&&!ReferenceEquals(old,arena.LastEnergyBall)&&arena.LastEnergyBall.completed&&Restored(roots)&&home.Restored&&Clean()&&!arena.ActionCameraActive;
            if(actor)actorReplacementPassed=passed;else replacementPassed=passed;Debug.Log("[energy-ball-review-replacement] actor="+actor+" passed="+passed);
            IEnumerator Old(){yield return arena.Attack(0,"Grass","Special",412,true,-1,2);oldDone=true;}
        }
        IEnumerator SettleReaction(int side)
        {float deadline=Time.realtimeSinceStartup+2;while(arena.GetDamageReaction(side)!=null&&arena.GetDamageReaction(side).IsPlaying&&Time.realtimeSinceStartup<deadline)yield return null;}
        IEnumerator Orthographic()
        {
            yield return Setup(0);SetPhase("CAMÉRA ORTHOGRAPHIQUE");var camera=arena.ArenaCamera;bool original=camera.orthographic;camera.orthographic=true;var home=new CameraPose(camera);var roots=Roots(0);bool done=false,unchanged=true;
            try
            {
                StartCoroutine(Perform());float deadline=Time.realtimeSinceStartup+10;while(!done&&Time.realtimeSinceStartup<deadline){unchanged&=home.Restored&&!arena.ActionCameraActive;yield return null;}
                yield return SettleReaction(1);yield return null;var e=arena.LastEnergyBall;orthographicPassed=done&&unchanged&&home.Restored&&Restored(roots)&&Clean()&&e.completed&&e.bodyContact&&e.contactSourceAvailable&&!e.contactQueryFailed&&e.contactRadiusError>=0&&e.contactRadiusError<=.002f&&e.reactions==1;
                Debug.Log("[energy-ball-review-orthographic] passed="+orthographicPassed);
            }
            finally{arena.CancelCurrentAction();camera.orthographic=original;}
            IEnumerator Perform(){yield return arena.Attack(0,"Grass","Special",412,true,-1,2);done=true;}
        }
        IEnumerator CaptureMotion()
        {
            captureTarget=new RenderTexture(1200,675,24,RenderTextureFormat.ARGB32){antiAliasing=2};captureTarget.Create();captureImage=new Texture2D(1200,675,TextureFormat.RGB24,false);float next=0;
            while(captureLoop)
            {
                yield return new WaitForEndOfFrame();float now=Time.realtimeSinceStartup;if(now<next)continue;var camera=arena.ArenaCamera;var previous=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
                try{camera.targetTexture=captureTarget;camera.aspect=1200f/675;camera.Render();RenderTexture.active=captureTarget;captureImage.ReadPixels(new Rect(0,0,1200,675),0,0);captureImage.Apply(false,false);File.WriteAllBytes(Path.Combine(output,"cycle-motion",captureTimes.Count.ToString("D5")+".jpg"),captureImage.EncodeToJPG(96));captureTimes.Add(now-captureStarted);stages.Add(phase);}
                finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.active=active;}next=now+1f/24;
            }
            File.WriteAllText(Path.Combine(output,"cycle-motion","frames.json"),JsonUtility.ToJson(new MotionReport{captureTimes=captureTimes.ToArray(),stages=stages.ToArray()},true));ReleaseCapture();captureDone=true;
        }
        void Overlay()
        {
            var go=new GameObject("Energy Ball review",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=arena.ArenaCamera;canvas.planeDistance=.5f;canvas.sortingOrder=100;
            var text=new GameObject("Phase",typeof(RectTransform),typeof(Text));text.transform.SetParent(go.transform,false);var rect=text.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(1400,75);rect.anchoredPosition=new Vector2(0,-15);
            caption=text.GetComponent<Text>();caption.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");caption.fontSize=28;caption.alignment=TextAnchor.MiddleCenter;caption.color=new Color(.8f,1,.4f);caption.raycastTarget=false;
        }
        void SetPhase(string value){phase=value;caption.text=value;}
        void Update(){frames++;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void ReleaseCapture(){if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);captureTarget=null;}if(captureImage!=null){Destroy(captureImage);captureImage=null;}}
        void OnDestroy(){Application.logMessageReceived-=OnLog;captureLoop=captureEnabled=false;ReleaseCapture();if(audioRecorder!=null){audioRecorder.FinishCapture(AudioSettings.dspTime);Destroy(audioRecorder);audioRecorder=null;}if(arena!=null){arena.CancelCurrentAction();arena.EndProtection(true);}}
    }
}
