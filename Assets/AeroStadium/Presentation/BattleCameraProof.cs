using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Captures settled shots only during the explicit camera review.</summary>
    public sealed class BattleCameraProof : MonoBehaviour
    {
        ArenaView arena;BattleDecisionCamera controller;string output;
        BattleDecisionCamera.Shot observed;float changed;bool busy;
        readonly HashSet<BattleDecisionCamera.Shot> saved=new HashSet<BattleDecisionCamera.Shot>();
        public void Initialize(ArenaView view,BattleDecisionCamera camera)
        {
            arena=view;controller=camera;output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../output/arenas/battle-camera"));
            string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--review-output");
            if(i>=0&&i+1<args.Length)output=Path.GetFullPath(args[i+1]);Directory.CreateDirectory(output);
            changed=Time.unscaledTime;
        }
        void Update()
        {
            if(controller==null||!controller.Active)return;
            if(observed!=controller.CurrentShot){observed=controller.CurrentShot;changed=Time.unscaledTime;}
            if(!busy&&!saved.Contains(observed)&&Time.unscaledTime-changed>2.5f)StartCoroutine(Capture(observed));
        }
        IEnumerator Capture(BattleDecisionCamera.Shot shot)
        {
            busy=true;yield return new WaitForEndOfFrame();
            if(controller.Active&&controller.CurrentShot==shot)
            {
                string name="camera-"+shot.ToString().ToLowerInvariant();
                ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,name+".png"));
                saved.Add(shot);
                Debug.Log("[battle-camera-capture] shot="+shot+" position="+arena.ArenaCamera.transform.position+" fov="+arena.ArenaCamera.fieldOfView);
            }
            busy=false;
        }
    }
}
