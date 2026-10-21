using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class ArenaView
    {
        [System.Serializable] public sealed class EnergyBallCast
        {
            public string status="implemented_pending_validation",source="Floating charge in front of transformed rest bounds; no anatomical mouth claim",contactMesh,queryFailure;
            public bool completed,cancelled,launched,detached,bodyContact,blocked,sound,impactSound,geometry,bodyGeometryAvailable,shieldGeometryAvailable,contactSourceAvailable,contactQueryFailed,originalLaneReached,avoidedBeforeArrival;
            public int reactions,blocks,meshBakedFrames,missSurfaceQueries,missSurfaceCrossings;
            public long triangleTests;
            public float effectSeconds,effectProgress,impactSeconds=-1,surfaceError=-1,contactRadiusError=-1,nearestCoreVertexGap=-1,renderedFrontGap=-1,firstThreatSeconds=-1,dodgeClearSeconds=-1,dodgeLeadSeconds;
            public Vector3 contact,normal;
        }
        public EnergyBallCast LastEnergyBall {get;private set;}=new EnergyBallCast();
        Vector3 EnergyBallSource(int side,Vector3 lockedAim)
        {
            Bounds body=PokemonWorldBounds(side);Vector3 front=lockedAim-body.center;front.y=0;front.Normalize();
            float support=Mathf.Abs(front.x)*body.extents.x+Mathf.Abs(front.z)*body.extents.z;
            return body.center+Vector3.up*body.size.y*.14f+front*(support+PokemonWorldHeight(side)*.2f);
        }
        IEnumerator EnergyBallMove(int side,bool targetHit,int protectedTarget,float effectiveness)
        {
            var report=new EnergyBallCast();LastEnergyBall=report;
            if(side<0||side>1||pokemon[side]==null||pokemon[1-side]==null||shownSpecies[side]!=1){report.status="invalid_source";yield break;}
            if(protectedTarget<0||protectedTarget>1||pokemon[protectedTarget]==null)protectedTarget=-1;
            GameObject owner=pokemon[side],victim=pokemon[1-side];bool resolvedMiss=currentResolvedMiss;
            Bounds original=PokemonWorldBounds(1-side);Vector3 aim=original.center;
            if(!targetHit&&protectedTarget<0&&!resolvedMiss)aim+=Vector3.Cross(Vector3.up,(aim-EnergyBallSource(side,aim)).normalized)*(original.extents.magnitude+.7f);
            Vector3 initialSource=EnergyBallSource(side,aim);
            var animator=owner.GetComponentInChildren<Animator>();float savedSpeed=animator!=null?animator.speed:1;
            var ownerDriver=owner.GetComponent<PokemonAnimationDriver>();var victimDriver=victim.GetComponent<PokemonAnimationDriver>();
            PokemonHitReaction victimReaction=null;EnergyBallEffect effect=null;bool reacted=false,blocked=false,finished=false;
            try
            {
                float native=PlayAttackAnimation(side,true); // The model driver's native Special role, without an assumed clip index.
                if(animator!=null&&native>.1f)animator.speed=Mathf.Clamp(native/EnergyBallEffect.Duration,.35f,1.5f);
                GameObject shieldSurface=null;
                if(protectedTarget>=0)
                {
                    TraceProtection(protectedTarget,initialSource,aim,out _,out _);
                    var shield=GetActiveProtection(protectedTarget);shieldSurface=shield!=null?shield.ContactSurface:null;
                }
                effect=EnergyBallEffect.Create(EffectParent(),()=>owner!=null&&pokemon[side]==owner?EnergyBallSource(side,aim):initialSource,aim,victim,PokemonWorldHeight(side)*.19f,targetHit&&protectedTarget<0,resolvedMiss,original,shieldSurface);
                while(effect.IsPlaying)
                {
                    if(owner==null||victim==null||pokemon[side]!=owner||pokemon[1-side]!=victim)yield break;
                    if(actionDirector!=null)actionDirector.SetProgress(effect.EffectProgress);
                    var shield=protectedTarget>=0?GetActiveProtection(protectedTarget):null;if(shield!=null&&shield.IsActive)effect.SetShieldBounds(shield.ShieldBounds);
                    ReactToContact();yield return null;
                }
                if(owner==null||victim==null||pokemon[side]!=owner||pokemon[1-side]!=victim)yield break;
                ReactToContact();if(actionDirector!=null)actionDirector.SetProgress(effect.EffectProgress);finished=true;
            }
            finally
            {
                try{Snapshot();Debug.Log("[energy-ball] "+JsonUtility.ToJson(report));}
                finally
                {
                    if(effect!=null){effect.Cancel();Destroy(effect.gameObject);}
                    if(owner!=null&&pokemon[side]==owner){if(animator!=null)animator.speed=savedSpeed;ownerDriver?.PlayIdle();}
                    if(!finished&&reacted&&victim!=null&&pokemon[1-side]==victim){victimReaction?.Cancel();victimDriver?.PlayIdle();}
                }
            }
            void ReactToContact()
            {
                if(effect.ShieldContact&&!blocked){int before=ProtectBlockCount;ReactProtection(protectedTarget,effect.ContactPoint,effect.ContactNormal);report.blocks=ProtectBlockCount-before;blocked=true;}
                if(effect.BodyContact&&targetHit&&!reacted)
                {
                    if(effectiveness>0){int before=DamagePoseCount;PlayDamageAnimation(1-side,effectiveness);report.reactions=DamagePoseCount-before;victimReaction=GetDamageReaction(1-side);}
                    FollowActionImpact(effect.ContactPoint);reacted=true;
                }
            }
            void Snapshot()
            {
                report.cancelled=!finished;if(effect==null){report.status="cancelled_before_creation";return;}
                report.effectSeconds=effect.EffectSeconds;report.effectProgress=effect.EffectProgress;
                report.launched=effect.Launched;report.detached=effect.Detached;report.bodyContact=effect.BodyContact;report.blocked=effect.ShieldContact;report.geometry=effect.GeometryFinite;report.sound=effect.SoundStarted;report.impactSound=effect.ImpactSoundStarted;
                report.bodyGeometryAvailable=effect.BodyGeometryAvailable;report.shieldGeometryAvailable=effect.ShieldGeometryAvailable;report.contactSourceAvailable=protectedTarget>=0?report.shieldGeometryAvailable:report.bodyGeometryAvailable;report.contactQueryFailed=effect.QueryFailed;report.queryFailure=effect.QueryFailure;
                report.contact=effect.ContactPoint;report.normal=effect.ContactNormal;report.impactSeconds=effect.ContactSeconds;report.contactMesh=effect.ContactMesh;report.surfaceError=effect.SurfaceError;report.contactRadiusError=effect.ContactRadiusError;report.nearestCoreVertexGap=effect.NearestCoreVertexGap;report.renderedFrontGap=effect.RenderedFrontGap;
                report.meshBakedFrames=effect.BakedFrames;report.triangleTests=effect.TriangleTests;report.missSurfaceQueries=effect.MissSurfaceQueries;report.missSurfaceCrossings=effect.MissSurfaceCrossings;
                report.originalLaneReached=effect.OriginalLaneReached;report.firstThreatSeconds=effect.OriginalThreatSeconds;report.dodgeClearSeconds=actionDirector!=null?actionDirector.DodgeClearSeconds:-1;report.dodgeLeadSeconds=report.firstThreatSeconds-report.dodgeClearSeconds;
                report.avoidedBeforeArrival=report.firstThreatSeconds>=0&&report.dodgeClearSeconds>=0&&report.dodgeLeadSeconds>.02f;
                bool contact=report.bodyContact||report.blocked;int expectedReactions=effectiveness>0?1:0;
                bool outcome=protectedTarget>=0?report.blocked&&report.blocks==1&&!report.bodyContact:targetHit?report.bodyContact&&report.reactions==expectedReactions:!report.bodyContact&&report.reactions==0&&report.missSurfaceCrossings==0&&report.missSurfaceQueries>0;
                report.completed=finished&&effect.Completed&&report.geometry&&report.launched&&report.detached&&report.sound&&report.contactSourceAvailable&&!report.contactQueryFailed&&outcome&&(contact?report.impactSound&&report.contactRadiusError<=.002f&&report.nearestCoreVertexGap>=0:!report.impactSound)&&(!resolvedMiss||(report.originalLaneReached&&report.avoidedBeforeArrival));
                report.status=!finished?"cancelled":!report.contactSourceAvailable?"contact_geometry_unavailable":report.contactQueryFailed?"contact_query_failed":report.completed?"implemented_pending_validation":"presentation_incomplete";
            }
        }
    }
}
