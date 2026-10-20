using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Owns 059's complete native bite after torso support (92), grounding (95) and mesh effects (100).</summary>
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class ContactBiteMotion : MonoBehaviour,IDisposable
    {
        // Source Physical_005 is 2.14 s; its native jaw performance closes by .533 s. Recover before its generic body-roll tail.
        const float BiteLength=.90f,TurnLength=.17f,ContactMargin=.008f;
        static readonly int BiteState=Animator.StringToHash("Base Layer.Native_005");
        // NativeModels/059/NativeClips/005.anim, Head/Jaw rotation curve at .53333336 s (also .55 s).
        // This is a reference measurement only: Animator continues to author the live jaw pose.
        static readonly Quaternion AuthoredClosedJaw=new Quaternion(0,.76258624f,0,.6468866f);
        Transform actor,mouth,jaw,head,nativeOrigin;
        NativePokemonModel native;Animator animator;
        ArenaView.BiteCast report;
        AnimatedSurfaceContact body;
        Func<Bounds> liveBounds;Func<ProtectShieldEffect> shield;
        Func<float> dodgeClear;Action<float> progress;Action<bool,Vector3,Vector3> impact;
        Vector3 home,arrival,retreatFrom,direction,lockedAim,lockedSurface,previousMouth,contactRoot;
        Quaternion homeRotation,attackRotation,returnRotation,jawRest;
        Vector3 originReference,previousOriginOffset;
        Bounds originalBounds;
        Vector3 upperLocal,lowerLocal,acrossLocal,forwardLocal,upLocal;
        float started,stageStarted,approachDuration,returnDuration,biteTime,previousBiteTime;
        float savedSpeed;bool savedEnabled,savedRootMotion,resolvedMiss,targetHit,initialized,disposed,planned,hasPrevious,hasLockedSurface,sounded,hasContactRoot;
        bool approachStarted;int stage;int[] savedStates;float[] savedTimes,savedWeights;
        BiteFlameEffect fireEffect;
        BiteTorsoPose torsoPose;
        readonly AudioClip[] clips=new AudioClip[3];AudioSource audioSource,fireAudio;
        NativeGrounding[] groundings;Vector3[] savedGroundingPositions;
        public bool Done {get;private set;}

        public void Initialize(Transform source,GameObject victim,Transform emitter,Transform lowerJaw,Transform upperHead,
            NativePokemonModel model,ArenaView.BiteCast telemetry,bool hit,bool miss,Bounds original,Material glow,
            Func<Bounds> bounds,Func<ProtectShieldEffect> activeShield,Action<float> setProgress,Func<float> dodgeCleared,
            Action<bool,Vector3,Vector3> onImpact)
        {
            actor=source;mouth=emitter;jaw=lowerJaw;head=upperHead;native=model;report=telemetry;targetHit=hit;resolvedMiss=miss;
            originalBounds=original;lockedAim=original.center;liveBounds=bounds;shield=activeShield;progress=setProgress;dodgeClear=dodgeCleared;impact=onImpact;
            home=actor.position;homeRotation=actor.rotation;started=stageStarted=Time.time;
            animator=actor.GetComponentInChildren<Animator>(true);
            if(animator!=null)
            {
                savedSpeed=animator.speed;savedEnabled=animator.enabled;savedRootMotion=animator.applyRootMotion;
                savedStates=new int[animator.layerCount];savedTimes=new float[savedStates.Length];savedWeights=new float[savedStates.Length];
                for(int i=0;i<savedStates.Length;i++)
                {var s=animator.GetCurrentAnimatorStateInfo(i);savedStates[i]=s.fullPathHash;savedTimes[i]=s.normalizedTime;savedWeights[i]=animator.GetLayerWeight(i);}
                animator.speed=1;animator.applyRootMotion=false;
            }
            groundings=actor.GetComponentsInChildren<NativeGrounding>(true);savedGroundingPositions=new Vector3[groundings.Length];
            for(int i=0;i<groundings.Length;i++)savedGroundingPositions[i]=groundings[i].transform.localPosition;
            direction=Vector3.ProjectOnPlane(lockedAim-home,Vector3.up).normalized;
            Vector3 facing=Vector3.ProjectOnPlane(mouth.position-home,Vector3.up).normalized;
            if(direction.sqrMagnitude<.5f)direction=actor.forward;
            attackRotation=Quaternion.FromToRotation(facing.sqrMagnitude>.5f?facing:actor.forward,direction)*homeRotation;
            returnRotation=Quaternion.AngleAxis(180,Vector3.up)*attackRotation;actor.rotation=attackRotation;
            foreach(var t in actor.GetComponentsInChildren<Transform>(true))if(t.name=="Origin"){nativeOrigin=t;break;}
            if(nativeOrigin!=null)originReference=actor.InverseTransformPoint(nativeOrigin.position);
            body=new AnimatedSurfaceContact(victim);report.blocked=shield()!=null;
            upperLocal=head.InverseTransformPoint(mouth.position);lowerLocal=jaw.InverseTransformPoint(mouth.position);
            acrossLocal=head.InverseTransformDirection(Vector3.Cross(Vector3.up,direction).normalized);
            forwardLocal=head.InverseTransformDirection(direction);upLocal=head.InverseTransformDirection(Vector3.up);
            torsoPose=BiteTorsoPose.Begin(actor,native);
            CreateFire(glow);initialized=true;report.phase="ignite";
        }

        void LateUpdate()
        {
            if(!initialized||disposed||Done)return;
            if(actor==null||mouth==null||jaw==null||head==null||native==null||animator==null||!actor.gameObject.activeInHierarchy)
            {report.reason="Native bite hierarchy was disabled or removed.";Dispose();return;}
            try
            {
                report.afterAnimatorSamples++;report.elapsed=Time.time-started;
                if(stage==0)Approach();else if(stage==1)Bite();else if(stage==2)Turn();else Return();
                if(resolvedMiss)MeasureMiss();
                previousMouth=mouth.position;hasPrevious=true;
                report.approachedDistance=Mathf.Max(report.approachedDistance,Vector3.Distance(home,actor.position));
                report.surfaceBakedFrames=body.BakedFrames;report.surfaceTriangleTests=body.TriangleTests;
                audioSource.transform.position=mouth.position; // Sound follows the actual sampled mouth.
            }
            catch(Exception error){report.reason=error.GetType().Name+": "+error.Message;report.geometryValid=false;Dispose();}
        }

        bool Mesh(Vector3 from,Vector3 to,out Vector3 point,out Vector3 normal)
        {report.meshQueries++;return body.TrySegment(from,to,out point,out normal);}

        void Approach()
        {
            actor.rotation=attackRotation;
            if(!approachStarted)
            {
                float charge=Mathf.Clamp01((Time.time-started)/.40f);
                fireEffect?.Render(.30f,false,Vector3.zero,Vector3.zero,false,Mathf.SmoothStep(0,1,charge));
                actor.position=home;progress(0);
                if(!sounded&&charge>=.12f){PlaySound(false);sounded=true;}
                UpdateFireAudio(Mathf.SmoothStep(0,1,charge));
                if(charge<1)return;
                approachStarted=true;stageStarted=Time.time;report.phase="approach";report.nativeRun=native.PlaySource(11);
            }
            fireEffect?.Render(.30f,false,Vector3.zero,Vector3.zero,false);UpdateFireAudio(1);
            if(!planned)
            {
                Vector3 point,normal;
                var shell=shield();
                bool found=shell!=null?shell.TryContact(mouth.position,shell.ShieldBounds.center,out point,out normal):Mesh(mouth.position,lockedAim,out point,out normal);
                if(found)
                {lockedSurface=point;hasLockedSurface=true;report.originalSurfacePoint=point;}
                else {report.reason="Original aim did not intersect a visible animated target triangle.";report.geometryValid=false;Done=true;return;}
                float travel=Mathf.Max(0,Vector3.Dot(lockedSurface-mouth.position,direction)-.24f);
                arrival=home+direction*travel;approachDuration=Mathf.Clamp(travel/7.4f,.45f,1.35f);planned=true;
            }
            float p=Mathf.Clamp01((Time.time-stageStarted)/approachDuration);
            actor.position=Vector3.Lerp(home,arrival,Mathf.SmoothStep(0,1,p));progress(.26f*p);
            if(p<1)return;
            stage=1;stageStarted=Time.time;report.phase="bite";jawRest=AuthoredClosedJaw;
            report.nativeBite=native.PlaySource(5);torsoPose?.BeginBite();previousBiteTime=0;hasPrevious=false;
        }

        float NativeBiteTime()
        {
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if(animator.IsInTransition(0))
            {var next=animator.GetNextAnimatorStateInfo(0);if(next.fullPathHash==BiteState)state=next;}
            return state.fullPathHash==BiteState?state.normalizedTime*state.length:-1;
        }

        void Bite()
        {
            actor.rotation=attackRotation;biteTime=NativeBiteTime();
            if(biteTime<0)
            {if(Time.time-stageStarted>.45f){report.reason="Native Physical_005 never evaluated.";report.geometryValid=false;Done=true;}return;}
            // Physical_005 contains source translation as well as the jaw performance. Cancel that translation
            // on the outside actor, keeping the full native pose while contact owns the actual arena travel.
            CancelNativeTranslation();
            // Never recapture a moving early frame: at .29 the authored jaw is already opening.
            // A fixed closed reference makes low frame rates and the native run crossfade harmless.
            float angle=Quaternion.Angle(jawRest,jaw.localRotation);report.jawPeakAngle=Mathf.Max(report.jawPeakAngle,angle);
            bool closing=report.jawPeakAngle>5&&angle<=4f&&biteTime>=.50f;
            float margin=Mathf.Lerp(.17f,ContactMargin,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.29f,.533f,biteTime)));
            bool contactWindow=biteTime<=.90f;
            Vector3 point=Vector3.zero,normal=Vector3.zero;float gap=1000;bool found=false;
            var shell=shield();
            if(contactWindow)
            {
                if(resolvedMiss)
                {
                    // Original real triangle lane is fixed. An evading target is never chased by a bounds proxy.
                    float advance=Vector3.Dot(lockedSurface-mouth.position,direction)+Mathf.Lerp(-margin,.035f,Mathf.InverseLerp(.50f,.55f,biteTime));
                    MoveAlong(advance);
                }
                else
                {
                    if(biteTime>.66f)margin=Mathf.Lerp(ContactMargin,.16f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.66f,.90f,biteTime)));
                    found=Fit(shell,margin,out point,out normal,out gap);
                }
                contactRoot=actor.position;hasContactRoot=true;
            }
            else if(!resolvedMiss)
            {
                // The entire remaining native clip is guarded. Recoil can move away freely and never pulls
                // the body back into the victim when the animated Origin/head continues its source lunge.
                report.outwardGuardFrames++;Fit(shell,.14f,out point,out normal,out gap,true);
            }
            else if(hasContactRoot)
                actor.position-=direction*Mathf.Max(0,.12f*(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.90f,1.1f,biteTime))-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.90f,1.1f,previousBiteTime))));
            if(closing){report.closedFrames++;if(found)report.minClosedGap=Mathf.Min(report.minClosedGap,gap);}
            if(biteTime>=.35f&&biteTime<=.85f&&report.closureSampleCount<report.closureSamples.Length)
                report.closureSamples[report.closureSampleCount++]=new ArenaView.BitePoseSample{nativeTime=biteTime,jawAngle=angle,gap=gap,closed=closing,found=found,mouth=mouth.position,triangle=point};
            UpdateFireAudio(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.65f,.90f,biteTime)));
            if(!report.actualContact&&!resolvedMiss&&found&&closing&&gap<=.02f&&gap>=-.002f)
            {
                report.actualContact=true;report.bodyContact=shell==null;report.jawClosedAtContact=true;
                report.contact=report.firstImpactPoint=point;report.normal=normal;
                report.contactTime=report.firstImpactTime=Time.time-started;report.contactFrame=Time.frameCount;
                report.correctedMouthPoint=mouth.position;report.correctedMouthGap=Vector3.Distance(mouth.position,point);
                if(shell==null)
                {report.meshContacts=1;report.surfaceMesh=body.LastMeshName;report.surfaceError=body.LastSurfaceError;report.surfaceFrame=body.LastFrame;}
                impact(shell!=null,point,normal);PlaySound(true);
            }
            DrawFire();
            float p=biteTime<.533f?Mathf.Lerp(.26f,.60f,biteTime/.533f):Mathf.Lerp(.60f,.69f,(biteTime-.533f)/(BiteLength-.533f));progress(p);
            previousBiteTime=biteTime;
            if(biteTime<BiteLength||(report.closedFrames==0&&biteTime<1.10f))return;
            stage=2;stageStarted=Time.time;report.phase="retreat";retreatFrom=actor.position;HideFire();torsoPose?.BeginReturn();native.PlaySource(11);
        }

        void CancelNativeTranslation()
        {
            if(nativeOrigin==null)return;
            Vector3 offset=Vector3.ProjectOnPlane(nativeOrigin.position-actor.TransformPoint(originReference),Vector3.up);
            actor.position-=offset-previousOriginOffset;previousOriginOffset=offset;
            report.maxNativeLunge=Mathf.Max(report.maxNativeLunge,offset.magnitude);
        }

        void MoveAlong(float distance)
        {if(!Finite(distance)){report.geometryValid=false;return;}actor.position+=direction*Mathf.Clamp(distance,-.38f,.38f);}

        bool Fit(ProtectShieldEffect shell,float margin,out Vector3 point,out Vector3 normal,out float gap,bool outwardOnly=false)
        {
            point=normal=Vector3.zero;gap=1000;
            Vector3 aim=shell!=null?shell.ShieldBounds.center:liveBounds().center;
            // Bracket an outside mouth position against the exact surface. Virtual source points let the
            // search cross a silhouette edge without ever leaving the rendered mouth inside the body.
            // Bounds choose a search length only; each decision and final witness uses real geometry.
            Bounds bounds=shell!=null?shell.ShieldBounds:liveBounds();
            Vector3 rootBefore=actor.position,initial=mouth.position;float low=-Mathf.Max(3,bounds.size.magnitude+1),high=Mathf.Max(low+.1f,Vector3.Dot(aim-initial,direction));
            bool outside=shell!=null?shell.TryContact(initial+direction*low,aim,out point,out normal):Mesh(initial+direction*low,aim,out point,out normal);
            if(!outside){report.fitFailures++;return false;}
            for(int i=0;i<13;i++)
            {
                float middle=(low+high)*.5f;Vector3 probe=initial+direction*middle;
                bool hit=shell!=null?shell.TryContact(probe,aim,out point,out normal):Mesh(probe,aim,out point,out normal);
                if(hit&&Vector3.Distance(probe,point)>margin)low=middle;else high=middle;
            }
            float correction=outwardOnly?Mathf.Min(0,low):low;
            if(!Finite(correction)){report.geometryValid=false;return false;}actor.position+=direction*correction;
            bool found=shell!=null?shell.TryContact(mouth.position,aim,out point,out normal):Mesh(mouth.position,aim,out point,out normal);
            gap=found?Vector3.Distance(mouth.position,point):1000;
            if(!outwardOnly&&shell==null&&biteTime>=.40f&&biteTime<=.66f&&(!found||gap>margin+.012f))
            {
                // The native head dives diagonally while the jaw closes. If its horizontal plane grazes an
                // arm silhouette, an axial-only search cannot reach that triangle and must not enter the body.
                // Make a small whole-actor lunge toward a real first surface; bones remain fully Animator-owned.
                actor.position=rootBefore;Vector3 origin=mouth.position;
                found=Mesh(origin,aim,out point,out normal);
                if(!found)
                {
                    // A late native sample may have entered the surface already. Start an outside real-mesh ray.
                    origin=mouth.position-direction*Mathf.Max(.65f,bounds.extents.magnitude);
                    found=Mesh(origin,aim,out point,out normal);
                }
                if(found)
                {
                    Vector3 incoming=(aim-origin).normalized;
                    Vector3 adjustment=point-incoming*margin-mouth.position;
                    // This is the complete physical source actor, including its feet; it reads as a brief
                    // native lunge/hop and the existing full-vector return eases it back to its world home.
                    if(adjustment.sqrMagnitude<=.85f*.85f)actor.position+=adjustment;
                    found=Mesh(mouth.position,aim,out point,out normal);
                }
            }
            if(!found){report.fitFailures++;return false;}
            gap=Vector3.Distance(mouth.position,point);
            float signedGap=Vector3.Dot(mouth.position-point,normal);
            report.maxPenetration=Mathf.Max(report.maxPenetration,Mathf.Max(0,-signedGap));
            report.closestMouthGap=Mathf.Min(report.closestMouthGap,gap);
            report.geometryValid&=Finite(gap)&&gap<10&&signedGap>=-.025f;
            return true;
        }

        void MeasureMiss()
        {
            if(!hasLockedSurface)return;
            // Measure when the actual marker crosses the plane of the original triangle, not a timer or ellipsoid.
            if(!report.originalLaneReached&&Vector3.Dot(mouth.position-lockedSurface,direction)>=-.018f)
            {report.originalLaneReached=true;report.firstOriginalThreatSeconds=Time.time-started;report.firstOriginalThreatRealtime=Time.time;}
            if(hasPrevious)
            {
                report.missSurfaceQueries++;
                if(Mesh(previousMouth,mouth.position,out _,out _))report.missSurfaceCrossings++;
            }
            // The jaw's physical reach in its fixed lane is queried even if there was little marker travel.
            if(stage==1&&biteTime>=.40f&&biteTime<=.71f)
            {
                report.missSurfaceQueries++;
                if(Mesh(mouth.position-direction*.025f,mouth.position+direction*.035f,out _,out _))report.missSurfaceCrossings++;
            }
            float cleared=dodgeClear();
            if(cleared>=0)report.dodgeClearSeconds=cleared-started;
            if(report.originalLaneReached&&report.dodgeClearSeconds>=0)
            {report.dodgeLeadSeconds=report.firstOriginalThreatSeconds-report.dodgeClearSeconds;report.avoidedBeforeArrival=report.dodgeLeadSeconds>.02f;}
        }

        void Turn()
        {
            float p=Mathf.Clamp01((Time.time-stageStarted)/TurnLength);
            actor.rotation=Quaternion.Slerp(attackRotation,returnRotation,Mathf.SmoothStep(0,1,p));CancelNativeTranslation();progress(Mathf.Lerp(.69f,.73f,p));
            if(p<1)return;
            stage=3;report.phase="return";stageStarted=Time.time;retreatFrom=actor.position;returnDuration=Mathf.Clamp(Vector3.Distance(retreatFrom,home)/7.4f,.45f,1.35f);
        }
        void Return()
        {
            float p=Mathf.Clamp01((Time.time-stageStarted)/returnDuration);actor.rotation=returnRotation;
            actor.position=Vector3.Lerp(retreatFrom,home,Mathf.SmoothStep(0,1,p));progress(Mathf.Lerp(.73f,1,p));
            if(p>=1){report.phase="complete";Done=true;}
        }

        void CreateFire(Material glow)
        {
            fireEffect=new BiteFlameEffect();fireEffect.Initialize(head,jaw,mouth,direction,glow);
            var audioObject=new GameObject("Native mouth following audio");audioObject.transform.SetParent(transform,false);
            audioSource=audioObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.volume=.51f;
            audioSource.spatialBlend=.5f;audioSource.dopplerLevel=0;audioSource.minDistance=3;audioSource.maxDistance=30;
            fireAudio=audioObject.AddComponent<AudioSource>();fireAudio.playOnAwake=false;fireAudio.loop=true;fireAudio.volume=0;
            fireAudio.spatialBlend=.5f;fireAudio.dopplerLevel=0;fireAudio.minDistance=3;fireAudio.maxDistance=30;
            LoadFireSamples();for(int i=0;i<3;i++)clips[i]=FireSamples[i];
            fireAudio.clip=clips[2];report.pcmFinite=FireFinite;report.audioPeak=FirePeak;
        }
        void DrawFire()
        {
            fireEffect?.Render(biteTime,report.actualContact,report.contact,report.normal,report.blocked);
        }
        void HideFire(){
            fireEffect?.Hide();if(fireAudio!=null){fireAudio.Stop();fireAudio.volume=0;}
        }

        static AudioClip[] FireSamples;static bool FireFinite;static float FirePeak;
        static void LoadFireSamples()
        {
            if(FireSamples!=null&&FireSamples[0]!=null&&FireSamples[1]!=null&&FireSamples[2]!=null)return;
            string[] resources={"FireFangIgnition","FireFangImpact","FireFangLoop"};
            string[] names={"Croc Feu ignition","Croc Feu impact","Croc Feu fire bed"};
            FireSamples=new AudioClip[3];FireFinite=true;FirePeak=0;
            for(int i=0;i<3;i++)
            {
                var clip=Resources.Load<AudioClip>("Audio/Attacks/CrocFeu/"+resources[i]);FireSamples[i]=clip;
                if(clip==null){FireFinite=false;continue;}
                clip.name=names[i];var pcm=new float[clip.samples*clip.channels];
                FireFinite&=clip.GetData(pcm,0);
                foreach(float sample in pcm){FireFinite&=Finite(sample);FirePeak=Mathf.Max(FirePeak,Mathf.Abs(sample));}
            }
        }
        void PlaySound(bool contact)
        {
            if(contact&&!report.actualContact)return;
            var clip=clips[contact?1:0];
            if(clip==null){report.reason="Missing local Croc Feu audio; restore Audio/Attacks/CrocFeu assets.";return;}
            audioSource.transform.position=mouth.position;audioSource.clip=clip;audioSource.volume=contact?.65f:.60f;audioSource.Play();
            report.sound|=audioSource.isPlaying&&report.pcmFinite&&report.audioPeak>0&&report.audioPeak<=1;
        }

        void UpdateFireAudio(float strength)
        {
            if(fireAudio==null)return;
            strength=Mathf.Clamp01(strength);fireAudio.volume=Mathf.MoveTowards(fireAudio.volume,.42f*strength,Time.deltaTime*8);
            if(strength>.02f&&!fireAudio.isPlaying)fireAudio.Play();
            if(strength<=.002f&&fireAudio.isPlaying)fireAudio.Stop();
        }

        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public void Dispose()
        {
            if(disposed)return;disposed=true;Done=true;
            torsoPose?.End();
            if(report!=null)
            {
                if(report.phase!="complete")report.phase="interrupted";
                if(actor!=null){actor.SetPositionAndRotation(home,homeRotation);report.returnedHome=Vector3.Distance(actor.position,home)<.001f&&Quaternion.Angle(actor.rotation,homeRotation)<.01f;}
                if(animator!=null&&savedStates!=null)
                {
                    for(int i=0;i<savedStates.Length;i++)
                    {if(savedStates[i]!=0&&animator.HasState(i,savedStates[i]))animator.Play(savedStates[i],i,savedTimes[i]);animator.SetLayerWeight(i,savedWeights[i]);}
                    animator.speed=savedSpeed;animator.applyRootMotion=savedRootMotion;
                    if(animator.enabled&&actor!=null&&actor.gameObject.activeInHierarchy)animator.Update(0);
                    animator.enabled=savedEnabled;report.animatorRestored=Mathf.Abs(animator.speed-savedSpeed)<.0001f&&animator.enabled==savedEnabled;
                }
                for(int i=0;groundings!=null&&i<groundings.Length;i++)if(groundings[i]!=null)groundings[i].transform.localPosition=savedGroundingPositions[i];
            }
            body?.Dispose();HideFire();fireEffect?.Dispose();if(audioSource!=null)audioSource.Stop();if(fireAudio!=null)fireAudio.Stop();
            // Resource clips are shared assets; only the action sources are owned here.
            enabled=false;
        }
        void OnDisable(){Dispose();}
        void OnDestroy(){Dispose();}
    }
}
