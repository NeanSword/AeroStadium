using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class ArenaView
    {
        BattleActionDirector actionDirector;bool currentResolvedMiss;
        public float LastActionCameraTravel {get;private set;}
        public float LastActionDodgeTravel {get;private set;}
        public bool LastActionRestored {get;private set;} public bool LastAttackUsedBite {get;private set;}
        public IEnumerator Attack(int side,string type,string category="Physical",int moveId=0,bool targetHit=true,int protectedTarget=-1,float hitEffectiveness=1,bool allowDodge=false)
        {
            if(pokemon[side]==null||pokemon[1-side]==null)yield break;
            if(actionDirector==null){actionDirector=gameObject.AddComponent<BattleActionDirector>();actionDirector.Initialize(this);}
            float duration=moveId==53?3.15f:moveId==55?1.65f:moveId==56?2.2f:moveId==57?2.9f:moveId==61?1.95f:moveId==145?2.4f:moveId==352?2.3f:moveId==503?2.45f:moveId==22?1.68f:moveId==75?2.15f:moveId==424?4.2f:1.6f;
            LastAttackUsedBite=moveId==424&&shownSpecies[side]==59;currentResolvedMiss=allowDodge&&!targetHit&&protectedTarget<0;
            actionDirector.BeginAction(side,pokemon[side].transform,pokemon[1-side].transform,moveId,targetHit,protectedTarget>=0,currentResolvedMiss,duration);
            try{yield return AttackVisual(side,type,category,moveId,targetHit,protectedTarget,hitEffectiveness);}
            finally
            {
                actionDirector.EndAction();LastActionCameraTravel=actionDirector.CameraTravel;LastActionDodgeTravel=actionDirector.DodgeTravel;
                LastActionRestored=actionDirector.Restored;currentResolvedMiss=false;
            }
        }
        void FollowActionImpact(Vector3 point){if(actionDirector!=null)actionDirector.NotifyImpact(point);}
    }
}
