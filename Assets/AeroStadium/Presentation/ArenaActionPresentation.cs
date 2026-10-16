using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public sealed partial class ArenaView
    {
        sealed class ActionPlayback
        {
            public int generation;public bool canceled,finished;public Coroutine coroutine;
            public readonly Stack<IEnumerator> routines=new Stack<IEnumerator>();
        }
        BattleActionDirector actionDirector;bool currentResolvedMiss;int actionGeneration;ActionPlayback actionPlayback;
        public SurfCameraTelemetry LastSurfCamera=>actionDirector!=null?actionDirector.LastSurfCamera:null;
        public bool ActionCameraActive=>actionDirector!=null&&actionDirector.Active;
        bool IsActionPlaybackCurrent(int generation)=>isActiveAndEnabled&&actionPlayback!=null&&!actionPlayback.canceled&&actionPlayback.generation==generation;
        static void DisposeActionRoutines(ActionPlayback playback)
        {while(playback.routines.Count>0)(playback.routines.Pop() as IDisposable)?.Dispose();}
        void FinishActionPlayback(ActionPlayback playback)
        {
            playback.canceled=true;
            try{if(playback.coroutine!=null)StopCoroutine(playback.coroutine);}
            finally
            {
                try{DisposeActionRoutines(playback);}
                finally
                {
                    playback.finished=true;
                    if(actionPlayback==playback)
                    {
                        actionPlayback=null;
                        if(actionDirector!=null){actionDirector.EndAction();LastActionCameraTravel=actionDirector.CameraTravel;LastActionDodgeTravel=actionDirector.DodgeTravel;LastActionRestored=actionDirector.Restored;}
                        currentResolvedMiss=false;
                    }
                }
            }
        }
        public void CancelCurrentAction()
        {if(actionPlayback!=null)FinishActionPlayback(actionPlayback);else if(actionDirector!=null)actionDirector.EndAction();}
        public float LastActionCameraTravel {get;private set;}
        public float LastActionDodgeTravel {get;private set;}
        public bool LastActionRestored {get;private set;} public bool LastAttackUsedBite {get;private set;}
        public IEnumerator Attack(int side,string type,string category="Physical",int moveId=0,bool targetHit=true,int protectedTarget=-1,float hitEffectiveness=1,bool allowDodge=false)
        {
            if(pokemon[side]==null||pokemon[1-side]==null)yield break;
            CancelCurrentAction();
            if(actionDirector==null){actionDirector=gameObject.AddComponent<BattleActionDirector>();actionDirector.Initialize(this);}
            var playback=new ActionPlayback{generation=++actionGeneration};actionPlayback=playback;
            float duration=moveId==53?3.15f:moveId==55?1.65f:moveId==56?2.2f:moveId==57?2.9f:moveId==61?1.95f:moveId==145?2.4f:moveId==352?2.3f:moveId==503?2.45f:moveId==22?1.68f:moveId==75?2.15f:moveId==424?4.2f:1.6f;
            LastAttackUsedBite=moveId==424&&shownSpecies[side]==59;currentResolvedMiss=allowDodge&&!targetHit&&protectedTarget<0;
            try
            {
                actionDirector.BeginAction(side,pokemon[side].transform,pokemon[1-side].transform,moveId,targetHit,protectedTarget>=0,currentResolvedMiss,duration);
                playback.coroutine=StartCoroutine(PlayAction(playback,Stages()));
                while(!playback.finished&&IsActionPlaybackCurrent(playback.generation))yield return null;
            }
            finally{FinishActionPlayback(playback);}
            IEnumerator Stages()
            {
                yield return actionDirector.EnterAction();
                if(IsActionPlaybackCurrent(playback.generation))yield return AttackVisual(side,type,category,moveId,targetHit,protectedTarget,hitEffectiveness);
                if(IsActionPlaybackCurrent(playback.generation))yield return actionDirector.RecoverAction();
            }
        }
        IEnumerator PlayAction(ActionPlayback playback,IEnumerator routine)
        {
            playback.routines.Push(routine);
            try
            {
                while(IsActionPlaybackCurrent(playback.generation)&&playback.routines.Count>0)
                {
                    var current=playback.routines.Peek();
                    if(!current.MoveNext()){playback.routines.Pop();(current as IDisposable)?.Dispose();continue;}
                    // Flatten nested iterators so cancellation can dispose every active visual finally.
                    // Native Unity yield instructions retain their normal timing and frame phase.
                    if(current.Current is IEnumerator nested)playback.routines.Push(nested);
                    else yield return current.Current;
                }
            }
            finally{try{DisposeActionRoutines(playback);}finally{playback.finished=true;}}
        }
        void FollowActionImpact(Vector3 point){if(actionDirector!=null)actionDirector.NotifyImpact(point);}
    }
}
