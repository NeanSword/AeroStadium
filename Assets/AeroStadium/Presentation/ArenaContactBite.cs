using System;
using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class ArenaView
    {
        [Serializable] public sealed class BiteCast
        {
            public int moveId=424, species, damageReactions, shieldReactions;
            public float approachedDistance, closestMouthGap=1000, contactTime=-1, elapsed, jawPeakAngle, audioPeak;
            public bool nativeRun, nativeBite, actualContact, bodyContact, blocked, returnedHome, completed, sound, pcmFinite, geometryValid=true;
            public string phase="prepare", source, reason;
            public Vector3 contact, normal;
            public int meshQueries, meshContacts, afterAnimatorSamples, surfaceBakedFrames, missSurfaceQueries, missSurfaceCrossings;
            public long surfaceTriangleTests;
            public float correctedMouthGap=-1, maxPenetration, surfaceError=-1, firstImpactTime=-1;
            public float firstOriginalThreatSeconds=-1, firstOriginalThreatRealtime=-1, dodgeClearSeconds=-1, dodgeLeadSeconds=-1;
            public bool jawClosedAtContact, originalLaneReached, avoidedBeforeArrival, animatorRestored;
            public int contactFrame=-1, surfaceFrame=-1;
            public string surfaceMesh, mouthName="EffMouth01";
            public Vector3 firstImpactPoint, correctedMouthPoint, originalSurfacePoint;
            public int closureSampleCount, closedFrames, fitFailures, outwardGuardFrames;
            public float minClosedGap=1000, maxNativeLunge;
            public BitePoseSample[] closureSamples=new BitePoseSample[40];
        }
        [Serializable] public struct BitePoseSample
        {public float nativeTime,jawAngle,gap;public bool closed,found;public Vector3 mouth,triangle;}
        public BiteCast LastBite { get; private set; } = new BiteCast();

        // The coroutine owns lifetime only. All pose, geometry, sound and fire work runs after Animator/grounding.
        public IEnumerator ContactFireFang(int side,bool targetHit,int protectedTarget,float efficacy)
        {
            var report=new BiteCast();LastBite=report;
            if(side<0||side>1||pokemon[side]==null||pokemon[1-side]==null)yield break;
            report.species=shownSpecies[side];
            var native=pokemon[side].GetComponent<NativePokemonModel>();
            Transform mouth=null,jaw=null,head=null;
            foreach(var bone in pokemon[side].GetComponentsInChildren<Transform>(true))
            {if(bone.name=="EffMouth01")mouth=bone;else if(bone.name=="Jaw")jaw=bone;else if(bone.name=="Head")head=bone;}
            if(report.species!=59||native==null||mouth==null||jaw==null||head==null)
            {report.reason="Contact bite requires native Arcanin 059, Head, EffMouth01 and Jaw.";yield break;}
            report.source="059/Model/pm0059_00_00/Origin/Waist/Spine1/Spine2/Neck/Head/EffMouth01; Physical_005; Run_011; LateUpdate_110";
            Bounds original=PokemonWorldBounds(1-side);
            // Create an explicitly resolved Abri shell before the motion owner captures its exact surface.
            if(protectedTarget>=0)TraceProtection(protectedTarget,mouth.position,original.center,out _,out _);
            var go=new GameObject("Crocs Feu · native contact owner");go.transform.SetParent(EffectParent(),true);
            var motion=go.AddComponent<ContactBiteMotion>();
            Material glow=null;
            try
            {
                glow=Material(new Color(1,.30f,.025f),.3f,0,true);
                motion.Initialize(pokemon[side].transform,pokemon[1-side],mouth,jaw,head,native,report,
                    targetHit,currentResolvedMiss,original,glow,
                    ()=>PokemonWorldBounds(1-side),()=>GetActiveProtection(protectedTarget),
                    p=>{if(actionDirector!=null)actionDirector.SetProgress(p);},
                    ()=>actionDirector!=null?actionDirector.DodgeClearRealtime:-1,
                    (isShield,point,normal)=>
                    {
                        if(isShield)
                        {int before=ProtectBlockCount;ReactProtection(protectedTarget,point,normal);report.shieldReactions=ProtectBlockCount-before;}
                        else
                        {
                            FollowActionImpact(point);
                            if(targetHit&&efficacy>0)
                            {int before=DamagePoseCount;PlayDamageAnimation(1-side,efficacy);report.damageReactions=DamagePoseCount-before;}
                        }
                    });
                while(motion!=null&&!motion.Done)yield return null;
            }
            finally
            {
                if(motion!=null)motion.Dispose();
                if(go!=null)Destroy(go);if(glow!=null)Destroy(glow);
                bool contactExpected=!currentResolvedMiss;
                int expected=targetHit&&efficacy>0&&!report.blocked?1:0;
                report.completed=report.phase=="complete"&&report.returnedHome&&report.animatorRestored&&report.nativeRun&&report.nativeBite
                    &&report.geometryValid&&report.sound&&report.pcmFinite&&report.damageReactions==expected
                    &&report.shieldReactions==(report.blocked?1:0)&&(!contactExpected||report.actualContact)
                    &&(!currentResolvedMiss||(report.missSurfaceCrossings==0&&!report.actualContact&&report.originalLaneReached&&report.avoidedBeforeArrival));
                Debug.Log("[contact-fire-fang] "+JsonUtility.ToJson(report));
            }
        }

        // Original deterministic synthesis, kept as a public API for exported review audio.
        public static float[] CreateFireFangPcm(bool contact=true)
        {
            var pcm=new float[14112];uint rng=0x42459a31;float low=0,air=0,phase=0;
            for(int i=0;i<pcm.Length;i++)
            {
                rng^=rng<<13;rng^=rng>>17;rng^=rng<<5;
                float n=(rng&0xffffff)/8388607.5f-1,t=i/22050f;
                low+=.07f*(n-low);air+=.42f*(n-air);
                float jaw=Mathf.Max(0,t-.11f),snap=t>=.11f?Mathf.Exp(-jaw*55):0;
                float hiss=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.64f))*Mathf.Exp(-t*4.7f);
                phase+=2*Mathf.PI*Mathf.Lerp(230,70,Mathf.Clamp01(jaw/.12f))/22050f;
                float crunch=snap*((contact?.57f:.18f)*air+.23f*Mathf.Sin(phase))+(contact?.18f:.05f)*Mathf.Exp(-jaw*16)*low*(t>=.11f?1:0);
                float fade=Mathf.Clamp01(t/.005f)*Mathf.Clamp01((.64f-t)/.035f);
                pcm[i]=.88f*(float)Math.Tanh((crunch+.32f*hiss*(air-low))*2.3f)*fade;
            }
            return pcm;
        }
    }
}
