using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

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
        }
        public BiteCast LastBite { get; private set; } = new BiteCast();

        // Deliberately bounded: 059's actual hierarchy and native source table were inspected.
        // Physical 005 opens Jaw 29.43 degrees at .467 s, then closes by .533 s.
        public IEnumerator ContactFireFang(int side,bool targetHit,int protectedTarget,float efficacy)
        {
            var report=new BiteCast(); LastBite=report;
            if(side<0||side>1||pokemon[side]==null||pokemon[1-side]==null)yield break;
            report.species=shownSpecies[side];
            var native=pokemon[side].GetComponent<NativePokemonModel>();
            Transform actor=pokemon[side].transform, mouth=null, jaw=null;
            foreach(var bone in actor.GetComponentsInChildren<Transform>(true))
            { if(bone.name=="EffMouth01")mouth=bone; if(bone.name=="Jaw")jaw=bone; }
            if(report.species!=59||native==null||mouth==null||jaw==null)
            { report.reason="Prototype requires native Arcanin 059, EffMouth01 and Jaw"; yield break; }
            report.source="059/Model/pm0059_00_00/Origin/Waist/Spine1/Spine2/Neck/Head/EffMouth01; Physical_005; Run_011";
            Vector3 home=actor.position; Quaternion homeRotation=actor.rotation;
            var animator=actor.GetComponentInChildren<Animator>(true); float speed=animator!=null?animator.speed:1;
            var driver=actor.GetComponent<PokemonAnimationDriver>();
            Bounds aimBounds=PokemonWorldBounds(1-side); // A miss keeps its original aim while the defender dodges.
            bool shield=protectedTarget>=0&&protectedTarget<2&&TraceProtection(protectedTarget,mouth.position,aimBounds.center,out _,out _);
            if(shield)aimBounds=GetActiveProtection(protectedTarget).ShieldBounds;
            report.blocked=shield;
            Vector3 direction=aimBounds.center-home; direction.y=0; direction.Normalize();
            Vector3 facing=mouth.position-home; facing.y=0;
            Quaternion attackRotation=Quaternion.FromToRotation(facing.normalized,direction)*homeRotation;
            var fx=new GameObject("Crocs Feu · local jaw fire"); fx.transform.SetParent(EffectParent(),true);
            Material glow=Material(new Color(1,.30f,.025f),.3f,0,true);
            var arcs=new LineRenderer[2]; var embers=new Transform[14]; AudioSource sound=null; AudioClip clip=null;
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"Upper jaw fire arc":"Lower jaw fire arc"); go.transform.SetParent(fx.transform,false);
                var line=go.AddComponent<LineRenderer>(); arcs[i]=line; line.sharedMaterial=glow;
                line.useWorldSpace=true; line.positionCount=13; line.numCapVertices=3;
                line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
                line.startColor=new Color(1,.86f,.22f); line.endColor=new Color(1,.25f,.01f); line.enabled=false;
            }
            for(int i=0;i<embers.Length;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name="Original bite ember";
                Destroy(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial=glow;
                go.transform.SetParent(fx.transform,true); go.transform.localScale=Vector3.zero; embers[i]=go.transform;
            }
            bool finished=false; float total=0;
            try
            {
                if(animator!=null)animator.speed=1;
                report.phase="approach"; report.nativeRun=native.PlaySource(11);
                actor.rotation=attackRotation;
                BiteSurface(aimBounds,mouth.position,out Vector3 surface,out Vector3 normal);
                float travel=Mathf.Max(0,Vector3.Dot(surface+normal*.11f-mouth.position,direction));
                Vector3 arrival=home+direction*travel;
                float approach=Mathf.Clamp(travel/7.4f,.45f,1.05f);
                for(float t=0;t<approach;t+=Time.deltaTime)
                {
                    actor.position=Vector3.Lerp(home,arrival,Mathf.SmoothStep(0,1,t/approach));
                    if(actionDirector!=null)actionDirector.SetProgress(.26f*t/approach);
                    report.approachedDistance=Mathf.Max(report.approachedDistance,Vector3.Distance(home,actor.position));
                    report.elapsed=total+=Time.deltaTime; yield return null;
                }
                actor.position=arrival;
                // Only now start the native attack: there is no distant jaw snap or pulse projectile.
                report.phase="bite"; report.nativeBite=native.PlaySource(5); Quaternion jawRest=jaw.localRotation;
                bool reacted=false, sounded=false; float biteDuration=2.14f;
                for(float t=0;t<biteDuration;t+=Time.deltaTime)
                {
                    if(actionDirector!=null)actionDirector.SetProgress(t<.54f?Mathf.Lerp(.26f,.60f,t/.54f):Mathf.Lerp(.60f,.69f,(t-.54f)/(biteDuration-.54f)));
                    BiteSurface(aimBounds,mouth.position,out surface,out normal);
                    // Keep the actual marker outside the contact ellipsoid through the native head lunge.
                    // Correction is along the approach axis only, so native feet/height remain authoritative.
                    float desired=shield?.115f:.095f;
                    float correction=Vector3.Dot(surface+normal*desired-mouth.position,direction);
                    actor.position+=direction*correction;
                    BiteSurface(aimBounds,mouth.position,out surface,out normal);
                    float gap=BiteGap(aimBounds,mouth.position,surface);
                    report.closestMouthGap=Mathf.Min(report.closestMouthGap,gap);
                    report.geometryValid&=gap>=.045f&&!float.IsNaN(gap)&&!float.IsInfinity(gap);
                    report.jawPeakAngle=Mathf.Max(report.jawPeakAngle,Quaternion.Angle(jawRest,jaw.localRotation));
                    if(!sounded&&t>=.43f)
                    {
                        float[] pcm=CreateFireFangPcm(targetHit||shield); report.pcmFinite=true;
                        foreach(float sample in pcm){report.pcmFinite&=!float.IsNaN(sample)&&!float.IsInfinity(sample);report.audioPeak=Mathf.Max(report.audioPeak,Mathf.Abs(sample));}
                        clip=AudioClip.Create("Original Crocs Feu ignition and crunch",pcm.Length,1,22050,false); clip.SetData(pcm,0);
                        sound=fx.AddComponent<AudioSource>(); sound.playOnAwake=false; sound.clip=clip;
                        sound.volume=.51f; sound.spatialBlend=.5f; sound.dopplerLevel=0; sound.minDistance=3; sound.maxDistance=30;
                        sound.transform.position=mouth.position; sound.Play();
                        report.sound=sound.isPlaying&&report.pcmFinite&&report.audioPeak>0&&report.audioPeak<=1; sounded=true;
                    }
                    if(!reacted&&t>=.54f)
                    {
                        reacted=true; report.contact=surface; report.normal=normal; report.contactTime=total;
                        report.actualContact=(targetHit||shield)&&gap>=.045f&&gap<=.16f;
                        report.bodyContact=report.actualContact&&!shield;
                        if(!report.bodyContact)FollowActionImpact(surface);
                        if(report.actualContact&&shield)
                        { int before=ProtectBlockCount; ReactProtection(protectedTarget,surface,normal); report.shieldReactions=ProtectBlockCount-before; }
                        else if(report.bodyContact&&efficacy>0)
                        { int before=DamagePoseCount; PlayDamageAnimation(1-side,efficacy); report.damageReactions=DamagePoseCount-before; }
                    }
                    DrawBiteFire(arcs,embers,mouth.position,direction,t,report.actualContact?report.contact:mouth.position,report.actualContact);
                    report.approachedDistance=Mathf.Max(report.approachedDistance,Vector3.Distance(home,actor.position));
                    report.elapsed=total+=Time.deltaTime; yield return null;
                }
                report.phase="retreat"; Vector3 from=actor.position;
                Quaternion retreatRotation=Quaternion.AngleAxis(180,Vector3.up)*attackRotation;
                for(float t=0;t<.17f;t+=Time.deltaTime)
                { actor.rotation=Quaternion.Slerp(attackRotation,retreatRotation,t/.17f);if(actionDirector!=null)actionDirector.SetProgress(Mathf.Lerp(.69f,.73f,t/.17f));report.elapsed=total+=Time.deltaTime;yield return null; }
                native.PlaySource(11); actor.rotation=retreatRotation;
                float retreat=Mathf.Clamp(Vector3.Distance(from,home)/7.4f,.45f,1.05f);
                for(float t=0;t<retreat;t+=Time.deltaTime)
                { actor.position=Vector3.Lerp(from,home,Mathf.SmoothStep(0,1,t/retreat));if(actionDirector!=null)actionDirector.SetProgress(Mathf.Lerp(.73f,1,t/retreat));report.elapsed=total+=Time.deltaTime;yield return null; }
                finished=true;
            }
            finally
            {
                if(actor!=null){actor.position=home;actor.rotation=homeRotation;report.returnedHome=Vector3.Distance(actor.position,home)<.001f&&Quaternion.Angle(actor.rotation,homeRotation)<.01f;}
                if(animator!=null)animator.speed=speed; if(driver!=null)driver.PlayIdle();
                if(fx!=null)Destroy(fx); if(clip!=null)Destroy(clip); if(glow!=null)Destroy(glow);
                report.phase=finished?"complete":"interrupted";
                int expected=targetHit&&efficacy>0&&!shield?1:0;
                report.completed=finished&&report.returnedHome&&report.nativeRun&&report.nativeBite&&report.geometryValid&&report.sound
                    &&report.damageReactions==expected&&report.shieldReactions==(shield?1:0)&&(!(targetHit||shield)||report.actualContact);
                Debug.Log("[contact-fire-fang] "+JsonUtility.ToJson(report));
            }
        }

        static void BiteSurface(Bounds bounds,Vector3 mouth,out Vector3 point,out Vector3 normal)
        {
            Vector3 e=bounds.extents; e=new Vector3(Mathf.Max(.05f,e.x),Mathf.Max(.05f,e.y),Mathf.Max(.05f,e.z));
            Vector3 d=mouth-bounds.center, q=new Vector3(d.x/e.x,d.y/e.y,d.z/e.z);
            if(q.sqrMagnitude<.0001f)q=Vector3.left;
            point=bounds.center+Vector3.Scale(e,q.normalized); Vector3 p=point-bounds.center;
            normal=new Vector3(p.x/(e.x*e.x),p.y/(e.y*e.y),p.z/(e.z*e.z)).normalized;
        }
        static float BiteGap(Bounds bounds,Vector3 mouth,Vector3 surface)
        {
            Vector3 e=bounds.extents,d=mouth-bounds.center;
            float q=d.x*d.x/(e.x*e.x)+d.y*d.y/(e.y*e.y)+d.z*d.z/(e.z*e.z);
            return Vector3.Distance(mouth,surface)*(q>=1?1:-1);
        }
        static void DrawBiteFire(LineRenderer[] arcs,Transform[] embers,Vector3 mouth,Vector3 direction,float t,Vector3 contact,bool touching)
        {
            Vector3 across=Vector3.Cross(Vector3.up,direction).normalized;
            float fire=Mathf.Sin(Mathf.PI*Mathf.Clamp01(t/.82f)), open=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.40f,.55f,t));
            for(int k=0;k<arcs.Length;k++)
            {
                arcs[k].enabled=t<.82f; arcs[k].startWidth=arcs[k].endWidth=.035f*fire;
                for(int i=0;i<13;i++)
                { float a=i/12f; arcs[k].SetPosition(i,mouth+across*((a-.5f)*.43f)+Vector3.up*((k==0?1:-1)*(.025f+Mathf.Sin(a*Mathf.PI)*(.04f+.13f*open)))+direction*(Mathf.Sin(a*Mathf.PI)*.10f)); }
            }
            float age=t-.54f;
            for(int i=0;i<embers.Length;i++)
            {
                float a=i*2.39996f;
                float life=touching?Mathf.Clamp01(age/.40f):Mathf.Clamp01(t/.7f);
                Vector3 origin=touching?contact:mouth;
                embers[i].position=origin+across*(Mathf.Cos(a)*life*.32f)+Vector3.up*(Mathf.Sin(a)*life*.20f+life*.23f)-direction*life*.22f;
                embers[i].localScale=Vector3.one*(touching&&age>=0&&age<.4f?.032f*(1-life):t<.54f?.018f*fire:0);
            }
        }

        // Original deterministic synthesis: a short ignition hiss, dry jaw snap and fading embers.
        public static float[] CreateFireFangPcm(bool contact=true)
        {
            var pcm=new float[14112]; uint rng=0x42459a31; float low=0,air=0,phase=0;
            for(int i=0;i<pcm.Length;i++)
            {
                rng^=rng<<13;rng^=rng>>17;rng^=rng<<5;
                float n=(rng&0xffffff)/8388607.5f-1,t=i/22050f;
                low+=.07f*(n-low);air+=.42f*(n-air);
                float jaw=Mathf.Max(0,t-.11f), snap=t>=.11f?Mathf.Exp(-jaw*55):0;
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
