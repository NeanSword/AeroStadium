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
    public sealed class GrassActionReview : MonoBehaviour
    {
        [Serializable] public sealed class CycleReport
        {
            public float riderDistance,cameraTravel; public bool returnedHome,cameraRestored;
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
            public bool passed; public int moveId; public bool missChecked,missEvent,missNoDamage,missNoReaction,missReturned;public float dodgeTravel;
            public string scope="Presentation of real BattleEngine events; not a complete GameBootstrap turn playback", directory;
            public string targetHeldItem="rawst-berry (isolates direct contact from residual damage)";
            public int errors, frames, captureWidth=1200, captureHeight=675, requestedCaptureFps=24;
            public float seconds, captureSeconds;
            public string[] stages;
            public float[] captureTimes;
            public CycleReport[] cycles;
        }
        int moveId; string moveName;
        bool missChecked,missEvent,missNoDamage,missNoReaction,missReturned;float dodgeTravel;
        ArenaView arena; Catalog catalog; string output; float requestedSeconds, started, captureStarted;
        int errors, frames; bool capturing; float[] captureTimes; readonly List<CycleReport> cycles=new List<CycleReport>(3);
        readonly List<float> frameTimes=new List<float>(900);
        readonly List<string> frameStages=new List<string>(900);
        string phase="PRÉPARATION"; Text phaseText, detailText, healthText; Canvas canvas;
        RenderTexture captureTarget; Texture2D captureImage;

        public void Begin(ArenaView view,string directory,float seconds)
        {
            var args=Environment.GetCommandLineArgs();int idx=Array.IndexOf(args,"--review-move");
            moveId=idx>=0&&idx+1<args.Length?int.Parse(args[idx+1]):22;
            if(moveId!=22&&moveId!=75&&moveId!=424)throw new ArgumentOutOfRangeException("review move");
            moveName=moveId==22?"FOUET LIANES":moveId==75?"TRANCH’HERBE":"CROC FEU";
            arena=view; output=directory; requestedSeconds=Mathf.Clamp(seconds,45,180);
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output,"cycle-motion"));
            var source=Resources.Load<TextAsset>("Data/catalog");
            if(source==null)throw new InvalidOperationException("Catalog missing for the Grass/contact action review.");
            catalog=JsonUtility.FromJson<Catalog>(source.text);catalog.Validate();
            arena.SetDecisionCameraActive(false); ModernArenaLighting.Apply(arena.ArenaCamera);
            CreateOverlay(); Application.logMessageReceived+=OnLog; started=Time.realtimeSinceStartup;
            ModernArenaReview.WriteWave(Path.Combine(output,"abri-activation.wav"),ArenaFireAudio.CreateProtectPcm(false),ArenaFireAudio.ProtectActivationVolume);
            ModernArenaReview.WriteWave(Path.Combine(output,"abri-impact.wav"),ArenaFireAudio.CreateProtectPcm(true),ArenaFireAudio.ProtectBlockVolume);
            if(moveId==424)ModernArenaReview.WriteWave(Path.Combine(output,"attack.wav"),ArenaView.CreateFireFangPcm(),.51f);
            if(moveId!=424)ModernArenaReview.WriteWave(Path.Combine(output,"attack.wav"),GrassAttackAudio.CreateOriginalPcm(moveId),moveId==22?.56f:.49f);
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            arena.EndProtection(true); arena.ShowPokemon(0,moveId==424?59:1);arena.ShowPokemon(1,9);
            yield return null;
            Overview(); SetPhase("DÉMONSTRATION · "+moveName,"Bulbizarre / Arcanin attaque · Tortank reçoit, puis se protège");
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
                    new[]{new TeamMember(moveId==424?59:1){moveIds=new[]{moveId}}},
                    new[]{new TeamMember(9,"rawst-berry"){moveIds=new[]{334,182},evs=new StatValues{hp=252,defense=252}}},191+iteration+trial*31);
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
                    Debug.LogError("Grass/contact action review cannot demonstrate the requested resolved events.");
                    SetPhase("DÉMONSTRATION INTERROMPUE","Le scénario de combat n’a pas produit les événements attendus.");
                    yield return new WaitForSecondsRealtime(2);Finish();yield break;
                }

                Overview(); Health(result.hpBefore,battle.Active(1).MaxHp);
                SetPhase("1 / 3 · "+moveName+" TOUCHE","Tortank reçoit l’attaque sans protection.");
                if(iteration==0){capturing=true;captureStarted=Time.realtimeSinceStartup;StartCoroutine(CaptureMotion());}
                yield return Pause(iteration==0?3:4);
                yield return Observe(arena.Attack(0,moveId==424?"Fire":"Grass","Physical",moveId,true,-1,(float)directEffectiveness),"01-direct-impact.png",moveId==424?1.60f:.69f,iteration==0);
                result.cameraTravel=arena.LastActionCameraTravel;result.cameraRestored=arena.LastActionRestored;result.riderDistance=ReviewCast.riderDistance;result.returnedHome=arena.LastActionRestored;result.emissions=ReviewCast.emissions;result.actualContacts=ReviewCast.contacts;result.peakParticles=ReviewCast.peak;result.detached=ReviewCast.motion;
                result.directCompleted=ReviewCast.completed;result.directSound=ReviewCast.sound;result.pairedEmitters=true;
                result.directReactions=ReviewCast.reactions;result.directSplash=ReviewCast.impact;
                result.directImpactSeconds=ReviewCast.impactSeconds;
                yield return arena.Hit(1,result.directDamage,true,(float)directEffectiveness); Health(result.hpAfterHit,battle.Active(1).MaxHp);
                yield return Pause(5);

                Overview();SetPhase("2 / 3 · TORTANK LANCE ABRI","Le bouclier se forme autour de Tortank.");
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

                Overview();SetPhase("3 / 3 · "+moveName+" RENCONTRE ABRI","Abri arrête le contact : recul des lianes, ricochet des feuilles ou morsure bloquée.");
                yield return Pause(2);
                int blockBefore=arena.ProtectBlockCount,damageBefore=arena.DamagePoseCount;
                yield return Observe(arena.Attack(0,moveId==424?"Fire":"Grass","Physical",moveId,false,1),"03-shield-contact.png",moveId==424?1.60f:.74f,iteration==0);
                result.blockedEmissions=ReviewCast.emissions;result.blockedContacts=ReviewCast.contacts;result.blockedDetached=ReviewCast.motion;
                result.blockedCompleted=ReviewCast.completed;result.pairedEmitters &= true;result.pairedContacts=ReviewCast.emitterContacts==(moveId==424?1:2);result.blockedSound=ReviewCast.sound;
                result.stoppedAtShield=ReviewCast.blocked;result.shieldRunoff=ReviewCast.motion;result.contacts=ReviewCast.blocks;
                result.blockedReactions=arena.DamagePoseCount-damageBefore;result.blockedSplash=ReviewCast.impact;
                result.blocks=arena.ProtectBlockCount-blockBefore;result.blockSound=arena.LastProtectBlockSound;
                result.contact=ReviewCast.contact;result.direction=ReviewCast.normal;
                shield=arena.GetActiveProtection(1);
                result.surfaceContact=shield!=null&&Vector3.Dot(result.direction,result.contact-shield.ShieldBounds.center)>0;
                Health(result.hpAfterBlock,battle.Active(1).MaxHp);
                yield return Pause(5);
                arena.EndProtection();yield return Pause(.65f);
                result.cleanup=arena.GetComponentsInChildren<ProtectShieldEffect>(true).Length==0
                    &&arena.GetComponentsInChildren<GrassAttackEffect>(true).Length==0;
                result.hydroCasts=arena.AquaticCasts;
                if(iteration==0&&moveId==75)yield return MissReview();
                if(iteration==0)capturing=false;
                Debug.Log("[grass-review-cycle] "+JsonUtility.ToJson(result));
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
                if(save&&!flightSaved&&still=="01-direct-impact.png"&&Time.realtimeSinceStartup-began>=(moveId==424?.55f:.40f))
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
            var root=new GameObject("Grass/contact action demonstration captions",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
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
            healthText=Caption(root.transform,"Tortank health",24,Color.white,new Vector2(38,-147),new Vector2(750,36));
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
        void Health(int hp,int maximum){healthText.text="TORTANK · PV "+hp+" / "+maximum;}

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
        [Serializable] sealed class ReviewState
        {public int emissions,contacts,peak,reactions,emitterContacts,blocks;public bool motion,completed,sound,impact,blocked;public float impactSeconds,riderDistance;public Vector3 contact,normal;}
        ReviewState ReviewCast
        {
            get{
                if(moveId!=424){var g=arena.LastGrass;return new ReviewState{emissions=g.emissions,contacts=g.contacts,peak=g.peak,reactions=g.reactions,emitterContacts=g.emitterContacts,blocks=g.blocks,motion=g.motion,completed=g.completed,sound=g.sound,impact=g.impact,blocked=g.blocked,impactSeconds=g.impactSeconds,contact=g.contact,normal=g.normal};}
                var b=arena.LastBite;return new ReviewState{emissions=1,contacts=b.actualContact?1:0,reactions=b.damageReactions,emitterContacts=b.blocked?1:0,blocks=b.shieldReactions,motion=b.returnedHome&&b.nativeRun&&b.nativeBite&&b.jawPeakAngle>5,completed=b.completed,sound=b.sound&&b.pcmFinite,impact=b.bodyContact,blocked=b.blocked,impactSeconds=b.contactTime,contact=b.contact,normal=b.normal,riderDistance=b.approachedDistance};
            }
        }
        IEnumerator MissReview()
        {
            missChecked=true;IReadOnlyList<BattleEvent> events=null;BattleEngine candidate=null;
            for(int seed=1;seed<=256;seed++){
                candidate=new BattleEngine(catalog,new[]{new TeamMember(1){moveIds=new[]{75}}},new[]{new TeamMember(9){moveIds=new[]{334}}},seed);
                events=candidate.ResolveTurn(BattleChoice.Move(0),BattleChoice.Move(0));
                if(BattlePresentationHit.IsResolvedMiss(events,FindMove(events,0,75))){missEvent=true;break;}
            }
            if(!missEvent){Debug.LogError("No resolved miss in deterministic seed search");yield break;}
            missNoDamage=candidate.Active(1).Hp==candidate.Active(1).MaxHp;
            SetPhase("4 / 4 · ESQUIVE RÉELLE","Tortank quitte la trajectoire des feuilles, puis revient à sa place.");
            int before=arena.DamagePoseCount;
            yield return Observe(arena.Attack(0,"Grass","Physical",75,false,-1,0,true),"04-dodge.png",.65f,true);
            dodgeTravel=arena.LastActionDodgeTravel;missReturned=arena.LastActionRestored;
            missNoReaction=arena.DamagePoseCount==before&&arena.LastGrass.reactions==0&&arena.LastGrass.contacts==0;
            yield return Pause(2);
        }
        void Finish()
        {
            capturing=false;arena.EndProtection(true);
            bool passed=cycles.Count==3&&errors==0&&(moveId!=75||(missChecked&&missEvent&&missNoDamage&&missNoReaction&&missReturned&&dodgeTravel>.3f));
            foreach(var c in cycles)passed &= c.directEvent&&c.protectEvent&&c.blockedEvent&&c.directDamage>0
                &&c.hpAfterHit<c.hpBefore&&c.hpAfterBlock==c.hpAfterHit&&c.directCompleted&&c.directReactions==1&&c.directSplash
                &&c.directSound&&c.blockedSound&&c.activationSound&&c.blockSound&&c.guardAnimated&&c.guardJoints>0&&c.guardPeakAngle>1&&c.shieldFormed&&c.activations==1&&c.blockedCompleted&&c.stoppedAtShield&&c.contacts==1&&c.blocks==1
                &&c.blockedReactions==0&&!c.blockedSplash&&c.surfaceContact&&c.shieldRunoff&&c.pairedEmitters&&c.pairedContacts&&c.emissions==(moveId==22?2:moveId==75?12:1)&&c.blockedEmissions==c.emissions&&c.actualContacts>0&&c.blockedContacts>0&&c.detached&&c.blockedDetached&&(moveId!=424||c.riderDistance>2f)&&c.cameraTravel>.02f&&c.cameraRestored&&c.cleanup;
            var report=new Report{moveId=moveId,missChecked=missChecked,missEvent=missEvent,missNoDamage=missNoDamage,missNoReaction=missNoReaction,missReturned=missReturned,dodgeTravel=dodgeTravel,passed=passed,errors=errors,frames=frames,seconds=Time.realtimeSinceStartup-started,
                directory=output,cycles=cycles.ToArray(),captureTimes=captureTimes??frameTimes.ToArray(),stages=frameStages.ToArray(),
                captureSeconds=frameTimes.Count>0?frameTimes[frameTimes.Count-1]:0};
            File.WriteAllText(Path.Combine(output,"grass-review.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[grass-review-result] "+JsonUtility.ToJson(report));Application.Quit(passed?0:1);
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
