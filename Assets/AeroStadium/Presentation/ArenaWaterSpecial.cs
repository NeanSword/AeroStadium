using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class ArenaView
    {
        [System.Serializable] public sealed class AquaticCast
        {
            public int moveId, emitters, emitterContacts, emissions, actualContacts, peakParticles, targetReactions, shieldReactions;
            public bool completed, sound, splash, blocked, runoff, detached;
            public bool geometryValid=true; public bool returnedHome=true; public float riderDistance;
            public float impactSeconds=-1, audioPeak;
            public string source;
            public Vector3 contact, normal;
        }
        public AquaticCast LastAquatic { get; private set; } = new AquaticCast();
        public int AquaticCasts {get;private set;}

        IEnumerator SpecialWater(int side,int moveId,bool targetHit,int protectedTarget,float hitEffectiveness)
        {
            var report=new AquaticCast{moveId=moveId};LastAquatic=report;
            if(pokemon[1-side]==null)yield break;
            float nativeDuration=PlayAttackAnimation(side,true);
            WaterEndpoints(side,out Vector3 source,out Vector3 secondary,out bool paired,out Vector3 target,out float width);
            report.emitters=paired?2:1;report.source=LastWaterSource;
            Transform parent=EffectParent();Bounds victim=PokemonWorldBounds(1-side);
            Vector3 miss=targetHit||protectedTarget>=0?Vector3.zero:Vector3.Cross(Vector3.up,(target-source).normalized).normalized*(victim.extents.magnitude+width*4);
            var effect=WaterSpecialEffect.Create(parent,source,target+miss,width*.8f,moveId);
            if(paired)effect.SetSecondaryEmitter(secondary);
            effect.SetFloor(pokemon[1-side].transform.position.y);
            effect.ImpactEnabled=targetHit;effect.SetVictimBounds(victim);
            var sound=WaterSpecialAudio.Play(parent,paired?(source+secondary)*.5f:source,moveId,effect.Duration);
            var stats=WaterSpecialAudio.AnalyzePcm(WaterSpecialAudio.CreateOriginalPcm(moveId,effect.Duration));
            report.audioPeak=stats.peak;report.sound=sound!=null&&sound.clip!=null&&sound.isPlaying&&stats.finite&&stats.peak>0&&stats.peak<=1;
            var animator=pokemon[side].GetComponentInChildren<Animator>();float speed=animator!=null?animator.speed:1;
            if(animator!=null&&nativeDuration>.1f)animator.speed=Mathf.Clamp(nativeDuration/effect.Duration,.35f,1.2f);
            bool reacted=false,shieldReacted=false;
            try
            {
                while(effect.IsPlaying)
                {
                    WaterEndpoints(side,out source,out secondary,out paired,out target,out width);
                    effect.SetEndpoints(source,target+miss);effect.SetVictimBounds(PokemonWorldBounds(1-side));
                    if(paired)effect.SetSecondaryEmitter(secondary);
                    var shield=protectedTarget>=0?GetActiveProtection(protectedTarget):null;
                    if(shield!=null&&shield.IsActive)
                    {
                        effect.SetShieldSurface(shield.ShieldBounds,pokemon[protectedTarget].transform.position.y);
                        if(TraceProtection(protectedTarget,source,target,out Vector3 contact,out Vector3 normal))effect.SetShieldContact(contact,normal);
                        if(paired&&TraceProtection(protectedTarget,secondary,target,out Vector3 secondContact,out Vector3 secondNormal))effect.SetSecondaryShieldContact(secondContact,secondNormal);
                    }
                    int shieldContacts=effect.PrimaryShieldContacts+effect.SecondaryShieldContacts;
                    if(shieldContacts>0)
                    {
                        report.blocked=true;report.contact=effect.ShieldContactPoint;report.normal=effect.ShieldContactNormal;
                        if(!shieldReacted){int before=ProtectBlockCount;ReactProtection(protectedTarget,report.contact,report.normal);report.shieldReactions=ProtectBlockCount-before;shieldReacted=true;}
                        report.runoff|=effect.ContactOpacity>.005f&&effect.RunoffDrops>0;
                    }
                    if(targetHit&&!reacted&&effect.ImpactReached)
                    {
                        int before=DamagePoseCount;PlayDamageAnimation(1-side,hitEffectiveness);report.targetReactions=DamagePoseCount-before;
                        report.impactSeconds=effect.EffectProgress*effect.Duration;reacted=true;
                    }
                    report.splash|=targetHit&&effect.ImpactOpacity>.005f;
                    report.emissions=effect.EmissionCount;report.actualContacts=effect.ActualContacts;
                    report.emitterContacts=(effect.PrimaryShieldContacts>0?1:0)+(effect.SecondaryShieldContacts>0?1:0);
                    report.peakParticles=Mathf.Max(report.peakParticles,effect.PeakParticles);report.detached|=effect.DetachedBeforeImpact;
                    report.geometryValid&=effect.Validate(out _);
                    if(sound!=null)sound.transform.position=paired?(source+secondary)*.5f:source;
                    yield return null;
                }
                bool contactExpected=targetHit||protectedTarget>=0;
                report.completed=effect.Completed&&effect.Validate(out _)&&report.geometryValid&&report.sound&&report.emissions==(moveId==352?6:24)
                    &&(!contactExpected||effect.ImpactReached)&&report.targetReactions==(targetHit?1:0);
                AquaticCasts++;Debug.Log("[water-special] "+JsonUtility.ToJson(report));
            }
            finally{if(animator!=null)animator.speed=speed;if(effect!=null)Destroy(effect.gameObject);}
        }

        IEnumerator SurfWave(int side,bool targetHit,int protectedTarget,float hitEffectiveness)
        {
            var report=new AquaticCast{moveId=57,emitters=1,source="Ground swell in front of attacker (no cannon emission)"};LastAquatic=report;
            if(pokemon[1-side]==null)yield break;
            float nativeDuration=PlayAttackAnimation(side,true);
            Bounds attacker=PokemonWorldBounds(side),victim=PokemonWorldBounds(1-side);
            float floor=pokemon[side].transform.position.y;
            Vector3 target=victim.center,source=new Vector3(attacker.center.x,floor,attacker.center.z);
            Vector3 toward=target-source;toward.y=0;toward.Normalize();source+=toward*Mathf.Min(.7f,attacker.extents.magnitude*.6f);
            Vector3 miss=targetHit||protectedTarget>=0?Vector3.zero:Vector3.Cross(Vector3.up,toward)*(victim.extents.magnitude+3.2f);
            var effect=SurfEffect.Create(EffectParent(),source,target+miss,Mathf.Clamp(victim.size.z*2f,4.2f,4.8f));
            effect.SetFloor(floor);effect.SetVictimBounds(victim);effect.ImpactEnabled=targetHit;
            var sound=WaterSpecialAudio.Play(EffectParent(),source,57,effect.Duration);
            var stats=WaterSpecialAudio.AnalyzePcm(WaterSpecialAudio.CreateOriginalPcm(57,effect.Duration));
            report.audioPeak=stats.peak;report.sound=sound!=null&&sound.clip!=null&&sound.isPlaying&&stats.finite&&stats.peak>0&&stats.peak<=1;
            var animator=pokemon[side].GetComponentInChildren<Animator>();float speed=animator!=null?animator.speed:1;
            if(animator!=null&&nativeDuration>.1f)animator.speed=Mathf.Clamp(nativeDuration/effect.Duration,.35f,1.2f);
            Transform rider=pokemon[side].transform;Vector3 home=rider.position;
            Vector3 centre=(homes[0]+homes[1])*.5f;centre.y=home.y;
            bool reacted=false,shieldReacted=false;
            try
            {
                while(effect.IsPlaying)
                {
                    float t=effect.EffectProgress*effect.Duration;
                    float outbound=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.76f,t));
                    float back=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.20f,2.55f,t));
                    float ride=outbound*(1-back);
                    rider.position=Vector3.Lerp(home,centre,ride)+Vector3.up*(Mathf.Sin(Mathf.PI*ride)*.24f);
                    report.riderDistance=Mathf.Max(report.riderDistance,Vector3.Distance(home,rider.position));
                    effect.SetVictimBounds(PokemonWorldBounds(1-side));
                    var shield=protectedTarget>=0?GetActiveProtection(protectedTarget):null;
                    if(shield!=null&&shield.IsActive)effect.SetShieldSurface(shield.ShieldBounds,pokemon[protectedTarget].transform.position.y);
                    if(effect.ShieldContacts>0)
                    {
                        report.blocked=true;report.contact=effect.ActualContact;report.normal=effect.ContactNormal;report.emitterContacts=1;
                        if(!shieldReacted){int before=ProtectBlockCount;ReactProtection(protectedTarget,report.contact,report.normal);report.shieldReactions=ProtectBlockCount-before;shieldReacted=true;}
                        report.runoff|=effect.ContactOpacity>.005f&&effect.RunoffDrops>0;
                    }
                    if(targetHit&&!reacted&&effect.ImpactReached)
                    {
                        int before=DamagePoseCount;PlayDamageAnimation(1-side,hitEffectiveness);report.targetReactions=DamagePoseCount-before;
                        report.impactSeconds=effect.EffectProgress*effect.Duration;reacted=true;
                    }
                    report.splash|=targetHit&&effect.ImpactOpacity>.005f;
                    report.emissions=effect.IsDetached?1:0;report.actualContacts=effect.HasActualContact?1:0;
                    report.peakParticles=Mathf.Max(report.peakParticles,effect.PeakDroplets);report.detached|=effect.IsDetached;
                    report.geometryValid&=effect.Validate(out _);
                    yield return null;
                }
                bool contactExpected=targetHit||protectedTarget>=0;
                report.completed=effect.Completed&&effect.Validate(out _)&&report.geometryValid&&report.sound&&report.detached
                    &&(!contactExpected||effect.ImpactReached)&&report.targetReactions==(targetHit?1:0);
                AquaticCasts++;Debug.Log("[surf] "+JsonUtility.ToJson(report));
            }
            finally{if(rider!=null){rider.position=home;report.returnedHome=Vector3.Distance(rider.position,home)<.001f;}if(animator!=null)animator.speed=speed;if(effect!=null)Destroy(effect.gameObject);}
        }
    }
}
