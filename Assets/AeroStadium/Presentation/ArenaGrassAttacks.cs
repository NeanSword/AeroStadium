using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class ArenaView
    {
        [System.Serializable] public sealed class GrassCast
        {
            public int moveId,emissions,contacts,emitterContacts,reactions,blocks,peak;
            public bool completed,sound,impact,blocked,geometry=true,motion;
            public string source; public Vector3 contact,normal; public float impactSeconds=-1;
        }
        public GrassCast LastGrass {get;private set;}=new GrassCast();
        readonly Transform[,] grassRoots=new Transform[2,2];readonly Transform[] grassOwner=new Transform[2];
        void GrassSources(int side,int moveId,out Vector3 first,out Vector3 second,out Vector3 aim,out float width)
        {
            Bounds body=PokemonWorldBounds(side);aim=PokemonWorldBounds(1-side).center;
            width=Mathf.Clamp(PokemonWorldHeight(side)*.22f,.16f,.42f);
            Transform actor=pokemon[side].transform;
            if(grassOwner[side]!=actor)
            {
                grassOwner[side]=actor;grassRoots[side,0]=grassRoots[side,1]=null;
                if(shownSpecies[side]==1)foreach(var bone in actor.GetComponentsInChildren<Transform>(true))
                {if(bone.name=="trLFeelerC01")grassRoots[side,0]=bone;if(bone.name=="trRFeelerC01")grassRoots[side,1]=bone;}
            }
            Vector3 across=Vector3.Cross(Vector3.up,(aim-body.center).normalized).normalized;
            first=grassRoots[side,0]!=null?grassRoots[side,0].position:body.center+across*width*.65f;
            second=grassRoots[side,1]!=null?grassRoots[side,1].position:body.center-across*width*.65f;
            if(moveId==75){first+=Vector3.up*PokemonWorldHeight(side)*.12f;second+=Vector3.up*PokemonWorldHeight(side)*.12f;}
        }
        IEnumerator GrassMove(int side,int moveId,bool targetHit,int protectedTarget,float effectiveness)
        {
            var report=new GrassCast{moveId=moveId};LastGrass=report;
            if(pokemon[1-side]==null)yield break;
            GrassSources(side,moveId,out var source,out var second,out var aim,out float width);
            report.source=grassRoots[side,0]!=null&&grassRoots[side,1]!=null?"Bulbizarre trLFeelerC01 + trRFeelerC01; leaf sources just above bud base":"Body sides fallback, anatomy not audited";
            Vector3 lockedAim=aim;
            Vector3 offset=targetHit||protectedTarget>=0||currentResolvedMiss?Vector3.zero:Vector3.Cross(Vector3.up,(aim-source).normalized)*(PokemonWorldBounds(1-side).extents.magnitude+width*4);
            float native=PlayAttackAnimation(side,moveId==75);
            var animator=pokemon[side].GetComponentInChildren<Animator>();float speed=animator!=null?animator.speed:1;
            var effect=GrassAttackEffect.Create(EffectParent(),source,aim+offset,width,moveId);
            effect.SetSecondaryEmitter(second);effect.SetFloor(pokemon[side].transform.position.y);effect.ImpactEnabled=targetHit;
            var sound=GrassAttackAudio.Play(EffectParent(),(source+second)*.5f,moveId,effect.Duration);
            var pcm=GrassAttackAudio.AnalyzePcm(GrassAttackAudio.CreateOriginalPcm(moveId,effect.Duration));
            report.sound=sound!=null&&sound.clip!=null&&sound.isPlaying&&pcm.finite&&pcm.peak>0&&pcm.peak<=1;
            if(animator!=null&&native>.1f)animator.speed=Mathf.Clamp(native/effect.Duration,.35f,1.5f);
            bool reacted=false,protectedOnce=false;
            try
            {
                while(effect.IsPlaying)
                {
                    GrassSources(side,moveId,out source,out second,out aim,out width);
                    effect.SetEndpoints(source,(currentResolvedMiss?lockedAim:aim)+offset);effect.SetSecondaryEmitter(second);
                    effect.SetVictimBounds(PokemonWorldBounds(1-side));
                    var shield=protectedTarget>=0?GetActiveProtection(protectedTarget):null;
                    if(shield!=null&&shield.IsActive)effect.SetShieldSurface(shield.ShieldBounds,pokemon[protectedTarget].transform.position.y);
                    if(effect.PrimaryShieldContacts+effect.SecondaryShieldContacts>0)
                    {
                        report.blocked=true;report.contact=effect.ContactPoint;report.normal=effect.ContactNormal;
                        if(!protectedOnce){int before=ProtectBlockCount;ReactProtection(protectedTarget,report.contact,report.normal);report.blocks=ProtectBlockCount-before;protectedOnce=true;}
                    }
                    if(targetHit&&!reacted&&effect.ImpactReached)
                    {
                        int before=DamagePoseCount;PlayDamageAnimation(1-side,effectiveness);report.reactions=DamagePoseCount-before;
                        report.impactSeconds=effect.EffectProgress*effect.Duration;reacted=true;
                    }
                    report.impact|=targetHit&&effect.ImpactOpacity>.005f;report.geometry&=effect.Validate(out _);
                    report.emissions=effect.EmissionCount;report.contacts=effect.ActualContacts;
                    report.emitterContacts=(effect.PrimaryShieldContacts>0?1:0)+(effect.SecondaryShieldContacts>0?1:0);
                    report.peak=Mathf.Max(report.peak,effect.PeakParticles);
                    yield return null;
                }
                report.motion=moveId==22?effect.Anchored&&effect.RetractionComplete:effect.DetachedBeforeImpact;
                report.completed=effect.Completed&&effect.Validate(out _)&&report.geometry&&report.motion&&report.sound
                    &&report.emissions==effect.ExpectedEmissionCount&&report.reactions==(targetHit?1:0)
                    &&(!(targetHit||protectedTarget>=0)||effect.ImpactReached);
                Debug.Log("[grass] "+JsonUtility.ToJson(report));
            }
            finally{if(animator!=null)animator.speed=speed;if(effect!=null)Destroy(effect.gameObject);}
        }
    }
}
