using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>A visible, isolated review of actual resolved hit/protect/block events.</summary>
    public sealed class OriginalWaterReview : MonoBehaviour
    {
        [Serializable] public sealed class CycleReport
        {
            public int emissions, actualContacts, peakParticles, blockedEmissions, blockedContacts; public bool detached, blockedDetached;
            public int cycle, directDamage, hpBefore, hpAfterHit, hpAfterBlock, directReactions, blockedReactions;
            public bool directEvent, protectEvent, blockedEvent, directCompleted, directSplash, shieldFormed;
            public bool blockedCompleted, stoppedAtShield, blockedSplash, surfaceContact, cleanup, shieldRunoff, pairedEmitters, pairedContacts;
            public bool activationSound,blockSound,directSound,blockedSound;
            public bool guardAnimated;public int guardJoints;public float guardPeakAngle;
            public int activations, contacts, blocks, hydroCasts;
            public float activationSeconds, directImpactSeconds;
            public Vector3 contact, direction;
        }
        [Serializable] public sealed class Report
        {
            public bool passed; public int moveId;
            public string scope="Presentation of real BattleEngine events; not a complete GameBootstrap turn playback", directory;
            public string targetHeldItem="rawst-berry (isolates direct contact from residual damage)";
            public int errors, frames, captureWidth=1200, captureHeight=675, requestedCaptureFps=24;
            public float seconds, captureSeconds;
            public string[] stages;
            public float[] captureTimes;
            public CycleReport[] cycles;
        }
        int moveId; string moveName;
        ArenaView arena; Catalog catalog; string output; float requestedSeconds, started, captureStarted;
        int errors, frames; bool capturing; float[] captureTimes; readonly List<CycleReport> cycles=new List<CycleReport>(3);
        readonly List<float> frameTimes=new List<float>(900);
        readonly List<string> frameStages=new List<string>(900);
        string phase="PRÉPARATION"; Text phaseText, detailText, healthText; Canvas canvas;
        RenderTexture captureTarget; Texture2D captureImage;

        public void Begin(ArenaView view,string directory,float seconds)
        {
            var args=Environment.GetCommandLineArgs();int idx=Array.IndexOf(args,"--review-move");
            moveId=idx>=0&&idx+1<args.Length?int.Parse(args[idx+1]):352;
            if(moveId!=352&&moveId!=503&&moveId!=57)throw new ArgumentOutOfRangeException("review move");
            moveName=moveId==352?"VIBRAQUA":moveId==503?"ÉBULLITION":"SURF";
            arena=view; output=directory; requestedSeconds=Mathf.Clamp(seconds,45,180);
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"cycle-motion"));
            var source=Resources.Load<TextAsset>("Data/catalog");
            if(source==null)throw new InvalidOperationException("Catalog missing for the Original water move review.");
            catalog=JsonUtility.FromJson<Catalog>(source.text);catalog.Validate();
            arena.SetDecisionCameraActive(false); ModernArenaLighting.Apply(arena.ArenaCamera);
            CreateOverlay(); Application.logMessageReceived+=OnLog; started=Time.realtimeSinceStartup;
            ModernArenaReview.WriteWave(Path.Combine(output,"abri-activation.wav"),ArenaFireAudio.CreateProtectPcm(false),ArenaFireAudio.ProtectActivationVolume);
            ModernArenaReview.WriteWave(Path.Combine(output,"abri-impact.wav"),ArenaFireAudio.CreateProtectPcm(true),ArenaFireAudio.ProtectBlockVolume);
            ModernArenaReview.WriteWave(Path.Combine(output,"attack.wav"),WaterSpecialAudio.CreateOriginalPcm(moveId),moveId==57?.60f:moveId==503?.56f:.54f);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            arena.EndProtection(true); arena.ShowPokemon(0,9);arena.ShowPokemon(1,6);
            yield return null;
            Overview(); SetPhase("DÉMONSTRATION · "+moveName,"Tortank attaque · Dracaufeu reçoit, puis se protège");
            yield return new WaitForSecondsRealtime(3);
            for(int iteration=0;iteration<3;iteration++)
            {
                float cycleStart=Time.realtimeSinceStartup;
                arena.EndProtection(true);
                // Restart the same legal two-turn battle per cycle so repetition never changes the test conditions.
                BattleEngine battle=null;IReadOnlyList<BattleEvent> directEvents=null;
                for(int trial=0;trial<32;trial++)
                {
                battle=new BattleEngine(catalog,
                    new[]{new TeamMember(9){moveIds=new[]{moveId}}},
                    new[]{new TeamMember(6,"rawst-berry"){moveIds=new[]{14,182},evs=new StatValues{hp=252,specialDefense=252}}},191+iteration+trial*31);
                directEvents=battle.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));
                int chosen=FindMove(directEvents,0,moveId);
                if(BattlePresentationHit.TryGetDirectHitEffectiveness(directEvents,chosen,out _,out _)&&battle.Active(1).Hp>0)break;
                }
                var result=new CycleReport{cycle=iteration+1,hpBefore=battle.Active(1).MaxHp};
                int directMove=FindMove(directEvents,0,moveId),damageEvent=-1;
                result.directEvent=BattlePresentationHit.TryGetDirectHitEffectiveness(directEvents,directMove,out damageEvent,out double directEffectiveness);
                result.directDamage=damageEvent>=0?directEvents[damageEvent].Amount:0;
                result.hpAfterHit=battle.Active(1).Hp;
                IReadOnlyList<BattleEvent> protectEvents=battle.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(1));
                int protectMove=FindMove(protectEvents,1,182),blockedMove=FindMove(protectEvents,0,moveId);
                result.protectEvent=BattlePresentationHit.TryGetProtectActivation(protectEvents,protectMove,out _);
                result.blockedEvent=BattlePresentationHit.TryGetBlockedAttack(protectEvents,blockedMove,out _,out int protectedSide)&&protectedSide==1;
                result.hpAfterBlock=battle.Active(1).Hp;
                cycles.Add(result);
                if(!result.directEvent||!result.protectEvent||!result.blockedEvent)
                {
                    Debug.LogError("Original water move review cannot demonstrate the requested resolved events.");
                    SetPhase("DÉMONSTRATION INTERROMPUE","Le scénario de combat n’a pas produit les événements attendus.");
                    yield return new WaitForSecondsRealtime(2);Finish();yield break;
                }

                Overview(); Health(result.hpBefore,battle.Active(1).MaxHp);
                SetPhase("1 / 3 · "+moveName+" TOUCHE","Dracaufeu reçoit l’attaque sans protection.");
                if(iteration==0){capturing=true;captureStarted=Time.realtimeSinceStartup;StartCoroutine(CaptureMotion());}
                yield return Pause(iteration==0?3:4);
                yield return Observe(arena.Attack(0,"Water","Special",moveId,true,-1,(float)directEffectiveness),"01-direct-impact.png",moveId==57?1.17f:.85f,iteration==0);
                result.emissions=arena.LastAquatic.emissions;result.actualContacts=arena.LastAquatic.actualContacts;result.peakParticles=arena.LastAquatic.peakParticles;result.detached=arena.LastAquatic.detached;
                result.directCompleted=arena.LastAquatic.completed;result.directSound=arena.LastAquatic.sound;result.pairedEmitters=arena.LastAquatic.emitters==(moveId==57?1:2);
                result.directReactions=arena.LastAquatic.targetReactions;result.directSplash=arena.LastAquatic.splash;
                result.directImpactSeconds=arena.LastAquatic.impactSeconds;
                yield return arena.Hit(1,result.directDamage,true,(float)directEffectiveness); Health(result.hpAfterHit,battle.Active(1).MaxHp);
                yield return Pause(5);

                Overview();SetPhase("2 / 3 · DRACAUFEU LANCE ABRI","Le bouclier se forme autour de Dracaufeu.");
                yield return Pause(2);
                int activationBefore=arena.ProtectActivationCount;
                yield return Observe(arena.Protect(1),"02-shield-formed.png",.84f,iteration==0);
                yield return null;
                var shield=arena.GetActiveProtection(1);
                result.activationSeconds=shield!=null?shield.ActivationTime:0;
                result.shieldFormed=shield!=null&&shield.IsActive&&shield.ActivationTime>=ProtectShieldEffect.ActivationDuration;
                result.activations=arena.ProtectActivationCount-activationBefore;
                result.activationSound=arena.LastProtectActivationSound;
                result.guardAnimated=arena.LastProtectAnimated;result.guardJoints=arena.LastProtectMovedJoints;
                result.guardPeakAngle=arena.LastProtectPeakAngle;
                yield return Pause(4);

                Overview();SetPhase("3 / 3 · "+moveName+" RENCONTRE ABRI","L’eau rencontre Abri : impact, éclaboussures et ruissellement.");
                yield return Pause(2);
                int blockBefore=arena.ProtectBlockCount,damageBefore=arena.DamagePoseCount;
                yield return Observe(arena.Attack(0,"Water","Special",moveId,false,1),"03-shield-runoff.png",moveId==57?1.28f:.95f,iteration==0);
                result.blockedEmissions=arena.LastAquatic.emissions;result.blockedContacts=arena.LastAquatic.actualContacts;result.blockedDetached=arena.LastAquatic.detached;
                result.blockedCompleted=arena.LastAquatic.completed;result.pairedEmitters &= arena.LastAquatic.emitters==(moveId==57?1:2);result.pairedContacts=arena.LastAquatic.emitterContacts==(moveId==57?1:2);result.blockedSound=arena.LastAquatic.sound;
                result.stoppedAtShield=arena.LastAquatic.blocked;result.shieldRunoff=arena.LastAquatic.runoff;result.contacts=arena.LastAquatic.shieldReactions;
                result.blockedReactions=arena.DamagePoseCount-damageBefore;result.blockedSplash=arena.LastAquatic.splash;
                result.blocks=arena.ProtectBlockCount-blockBefore;result.blockSound=arena.LastProtectBlockSound;
                result.contact=arena.LastAquatic.contact;result.direction=arena.LastAquatic.normal;
                shield=arena.GetActiveProtection(1);
                result.surfaceContact=shield!=null&&Vector3.Dot(result.direction,result.contact-shield.ShieldBounds.center)>0;
                Health(result.hpAfterBlock,battle.Active(1).MaxHp);
                yield return Pause(5);
                arena.EndProtection();yield return Pause(.65f);
                result.cleanup=arena.GetComponentsInChildren<ProtectShieldEffect>(true).Length==0
                    &&arena.GetComponentsInChildren<WaterSpecialEffect>(true).Length==0&&arena.GetComponentsInChildren<SurfEffect>(true).Length==0;
                result.hydroCasts=arena.AquaticCasts;
                if(iteration==0)capturing=false;
                Debug.Log("[water-special-review-cycle] "+JsonUtility.ToJson(result));
                SetPhase("CYCLE "+(iteration+1)+" / 3 TERMINÉ","Impact direct → formation d’Abri → impact liquide sur le bouclier");
                float cycleTarget=Mathf.Max(30,(requestedSeconds-8)/3);
                while(Time.realtimeSinceStartup-cycleStart<cycleTarget)yield return null;
            }
            SetPhase("DÉMONSTRATION TERMINÉE","Les trois phases ont été présentées trois fois.");
            while(Time.realtimeSinceStartup-started<requestedSeconds)yield return null;
            Finish();
        }

        static int FindMove(IReadOnlyList<BattleEvent> events,int side,int move)
        {
            for(int i=0;i<events.Count;i++)if(events[i].Kind==BattleEventKind.MoveUsed&&events[i].Side==side&&events[i].MoveId==move)return i;
            return -1;
        }
        IEnumerator Pause(float seconds){yield return new WaitForSecondsRealtime(seconds);}
        IEnumerator Observe(IEnumerator action,string still,float at,bool save)
        {
            bool finished=false,captured=false,flightSaved=false;float began=Time.realtimeSinceStartup;
            StartCoroutine(Perform());
            while(!finished)
            {
                yield return new WaitForEndOfFrame();
                if(save&&!flightSaved&&still=="01-direct-impact.png"&&Time.realtimeSinceStartup-began>=(moveId==57?.65f:.39f))
                {ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,"00-flight.png"));flightSaved=true;}
                if(save&&!captured&&Time.realtimeSinceStartup-began>=at)
                {ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,still));captured=true;}
            }
            // Formation's final frame is also useful if the machine only renders a few frames during activation.
            if(save&&!captured){yield return new WaitForEndOfFrame();ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,still));}
            IEnumerator Perform(){yield return action;finished=true;}
        }
        void Overview()
        {
            // The full jet and the full target shell stay in the same shot: no cut hides the point of contact.
            ModernArenaReview.CameraShot(arena.ArenaCamera,new Vector3(0,4.25f,-12.6f),new Vector3(0,1.65f,0),48);
        }
        void CreateOverlay()
        {
            var root=new GameObject("Original water move demonstration captions",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;
            canvas.worldCamera=arena.ArenaCamera;canvas.planeDistance=.5f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Phase band",typeof(RectTransform),typeof(Image));panel.transform.SetParent(root.transform,false);
            var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;
            rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,126);rect.anchoredPosition=Vector2.zero;
            panel.GetComponent<Image>().color=new Color(.025f,.045f,.07f,.83f);
            phaseText=Caption(panel.transform,"Phase",36,new Color(1,.82f,.3f),new Vector2(38,-15),new Vector2(1500,45));
            detailText=Caption(panel.transform,"Explanation",25,new Color(.9f,.95f,1),new Vector2(40,-69),new Vector2(1650,36));
            healthText=Caption(root.transform,"Dracaufeu health",24,Color.white,new Vector2(38,-147),new Vector2(750,36));
        }
        static Text Caption(Transform parent,string name,int size,Color color,Vector2 position,Vector2 extent)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=position;rect.sizeDelta=extent;
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;
            return text;
        }
        void SetPhase(string title,string explanation){phase=title;phaseText.text=title;detailText.text=explanation;}
        void Health(int hp,int maximum){healthText.text="DRACAUFEU · PV "+hp+" / "+maximum;}

        IEnumerator CaptureMotion()
        {
            captureTarget=new RenderTexture(1200,675,24,RenderTextureFormat.ARGB32){antiAliasing=2};captureTarget.Create();
            captureImage=new Texture2D(1200,675,TextureFormat.RGB24,false);float next=Time.realtimeSinceStartup;
            while(capturing)
            {
                yield return new WaitForEndOfFrame();
                float now=Time.realtimeSinceStartup;if(now<next)continue;
                CaptureFrame();frameTimes.Add(now-captureStarted);frameStages.Add(phase);
                next=now+1f/24;
            }
            captureTimes=frameTimes.ToArray();
            File.WriteAllText(Path.Combine(output,"cycle-motion","frames.json"),JsonUtility.ToJson(new Report
                {captureTimes=captureTimes,stages=frameStages.ToArray(),frames=frameTimes.Count,captureSeconds=Time.realtimeSinceStartup-captureStarted},true));
            ReleaseCapture();
        }
        void CaptureFrame()
        {
            var camera=arena.ArenaCamera;RenderTexture previous=camera.targetTexture,active=RenderTexture.active;float aspect=camera.aspect;
            try
            {
                camera.targetTexture=captureTarget;camera.aspect=1200f/675;camera.Render();RenderTexture.active=captureTarget;
                captureImage.ReadPixels(new Rect(0,0,1200,675),0,0);captureImage.Apply(false,false);
                File.WriteAllBytes(Path.Combine(output,"cycle-motion",frameTimes.Count.ToString("D5")+".png"),captureImage.EncodeToPNG());
            }
            finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.active=active;}
        }
        void Finish()
        {
            capturing=false;arena.EndProtection(true);
            bool passed=cycles.Count==3&&errors==0;
            foreach(var c in cycles)passed &= c.directEvent&&c.protectEvent&&c.blockedEvent&&c.directDamage>0
                &&c.hpAfterHit<c.hpBefore&&c.hpAfterBlock==c.hpAfterHit&&c.directCompleted&&c.directReactions==1&&c.directSplash
                &&c.directSound&&c.blockedSound&&c.activationSound&&c.blockSound&&c.guardAnimated&&c.guardJoints>0&&c.guardPeakAngle>1&&c.shieldFormed&&c.activations==1&&c.blockedCompleted&&c.stoppedAtShield&&c.contacts==1&&c.blocks==1
                &&c.blockedReactions==0&&!c.blockedSplash&&c.surfaceContact&&c.shieldRunoff&&c.pairedEmitters&&c.pairedContacts&&c.emissions==(moveId==352?6:moveId==503?24:1)&&c.blockedEmissions==c.emissions&&c.actualContacts>0&&c.blockedContacts>0&&c.detached&&c.blockedDetached&&c.cleanup;
            var report=new Report{moveId=moveId,passed=passed,errors=errors,frames=frames,seconds=Time.realtimeSinceStartup-started,
                directory=output,cycles=cycles.ToArray(),captureTimes=captureTimes??frameTimes.ToArray(),stages=frameStages.ToArray(),
                captureSeconds=frameTimes.Count>0?frameTimes[frameTimes.Count-1]:0};
            File.WriteAllText(Path.Combine(output,"water-special-review.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[water-special-review-result] "+JsonUtility.ToJson(report));Application.Quit(passed?0:1);
        }
        void Update(){frames++;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void ReleaseCapture()
        {
            if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);captureTarget=null;}
            if(captureImage!=null){Destroy(captureImage);captureImage=null;}
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;capturing=false;ReleaseCapture();if(arena!=null)arena.EndProtection(true);}
    }
}
