using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>Visible comparison of six resolved type matchups; isolated from the ordinary menu flow.</summary>
    public sealed class EffectivenessReactionReview : MonoBehaviour
    {
        [Serializable] public sealed class CaseReport
        {
            public int index, attacker, defender, move, damage, hpBefore, hpAfter, reactions, tier, movedJoints;
            public string description, eventKind;
            public double expectedMultiplier, resolvedMultiplier;
            public float damageWeight, peakAngle, reactionDuration;
            public bool resolvedExpected, survived, clean, usesNativeDamage, immuneHasNoReaction, passed;
        }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public string scope="Presentation of six real BattleEngine event matchups, not a complete GameBootstrap battle", directory;
            public int errors, frames, captureFrames, captureWidth=1080, captureHeight=608, requestedCaptureFps=16;
            public float seconds, captureSeconds;
            public float[] captureTimes;
            public string[] stages;
            public CaseReport[] cases;
        }
        sealed class Matchup
        {
            public int attacker, defender, move;
            public float multiplier;
            public string title, explanation;
            public Matchup(int source,int target,int attack,float factor,string title,string explanation)
            {attacker=source;defender=target;move=attack;multiplier=factor;this.title=title;this.explanation=explanation;}
        }
        readonly Matchup[] matchups={
            new Matchup(9,92,33,0,"IMMUNITÉ · × 0","Normal → Spectre / Poison · Fantominus ne réagit pas aux dégâts."),
            new Matchup(3,3,22,.25f,"TRÈS PEU EFFICACE · × 0,25","Plante → Plante / Poison · Florizarre accuse à peine le coup."),
            new Matchup(9,9,55,.5f,"PEU EFFICACE · × 0,5","Eau → Eau · Tortank a une réaction légère."),
            new Matchup(9,143,33,1,"EFFICACITÉ NORMALE · × 1","Normal → Normal · Ronflex a une réaction normale."),
            new Matchup(6,127,53,2,"TRÈS EFFICACE · × 2","Feu → Insecte · Scarabrute subit une réaction plus marquée."),
            new Matchup(9,6,479,4,"EXTRÊMEMENT EFFICACE · × 4","Roche → Feu / Vol · Dracaufeu subit une réaction extrême.")
        };
        ArenaView arena; Catalog catalog; string output,phase="PRÉPARATION";
        float started,requestedSeconds,captureStarted; int errors,renderedFrames; bool capturing,finishing;
        Text titleText,detailText,healthText;
        readonly List<CaseReport> results=new List<CaseReport>(6);
        readonly List<float> frameTimes=new List<float>(2048);
        readonly List<string> frameStages=new List<string>(2048);
        RenderTexture captureTarget; Texture2D captureImage;

        public void Begin(ArenaView view,string directory,float seconds)
        {
            arena=view;output=directory;requestedSeconds=Mathf.Clamp(seconds,120,180);
            Directory.CreateDirectory(output);Directory.CreateDirectory(Path.Combine(output,"reaction-motion"));
            TextAsset data=Resources.Load<TextAsset>("Data/catalog");
            if(data==null)throw new InvalidOperationException("Missing battle catalog for effectiveness reaction comparison.");
            catalog=JsonUtility.FromJson<Catalog>(data.text);catalog.Validate();
            arena.SetDecisionCameraActive(false);ModernArenaLighting.Apply(arena.ArenaCamera);
            CreateOverlay();Application.logMessageReceived+=OnLog;started=Time.realtimeSinceStartup;
            StartCoroutine(Run());
        }
        IEnumerator Run()
        {
            arena.EndProtection(true);arena.ShowPokemon(0,9);arena.ShowPokemon(1,92);yield return null;
            Overview();SetPhase("RÉACTIONS AUX ATTAQUES","Six efficacités réelles, de l’immunité à la double faiblesse.");
            captureStarted=Time.realtimeSinceStartup;capturing=true;StartCoroutine(CaptureMotion());
            yield return new WaitForSecondsRealtime(3);
            float caseSeconds=(requestedSeconds-6)/matchups.Length;
            for(int index=0;index<matchups.Length;index++)
            {
                float began=Time.realtimeSinceStartup;Matchup setup=matchups[index];
                arena.EndProtection(true);arena.ShowPokemon(0,setup.attacker);arena.ShowPokemon(1,setup.defender);
                yield return new WaitForSecondsRealtime(.8f);
                var attacker=new TeamMember(setup.attacker){moveIds=new[]{setup.move},ivs=new StatValues(0)};
                // Two identical legal defenders let a switch consume their choice without another attack.
                // Every case gets a fresh engine. Defensive EVs keep the target alive, including the ×4 case.
                var defender=new TeamMember(setup.defender,"rawst-berry"){
                    ivs=new StatValues(31),evs=new StatValues{hp=252,defense=126,specialDefense=132}};
                var battle=new BattleEngine(catalog,new[]{attacker},new[]{defender,defender.Copy()},901+index,50);
                int before=battle.Active(1).Hp;
                IReadOnlyList<BattleEvent> events=battle.ResolveTurn(BattleChoice.Move(0),BattleChoice.Switch(1));
                int used=FindMove(events,setup.move);
                bool direct=BattlePresentationHit.TryGetDirectHitEffectiveness(events,used,out int damageIndex,out double multiplier);
                int immuneIndex=FindImmune(events,used,setup.move);
                if(!direct&&immuneIndex>=0)multiplier=events[immuneIndex].HitEffectiveness;
                var result=new CaseReport{index=index+1,attacker=setup.attacker,defender=setup.defender,move=setup.move,
                    description=setup.explanation,expectedMultiplier=setup.multiplier,resolvedMultiplier=multiplier,
                    damage=direct?events[damageIndex].Amount:0,hpBefore=before,hpAfter=battle.Active(1).Hp,
                    eventKind=direct?"Damage":immuneIndex>=0?"Immune":"Missing",
                    resolvedExpected=used>=0&&Math.Abs(multiplier-setup.multiplier)<.0001&&
                        (setup.multiplier==0?immuneIndex>=0:direct),survived=battle.Active(1).Hp>0};
                results.Add(result);
                if(!result.resolvedExpected||!result.survived)
                {
                    Debug.LogError("Effectiveness review did not resolve the requested legal non-KO matchup: "+JsonUtility.ToJson(result));
                    SetPhase("COMPARAISON INTERROMPUE","Le scénario n’a pas produit le contact attendu.");
                    yield return StopCapture();Finish();yield break;
                }
                Overview();SetPhase((index+1)+" / 6 · "+setup.title,setup.explanation);
                Health(catalog.GetSpecies(setup.defender).name,result.hpBefore,battle.Active(1).MaxHp);
                yield return new WaitForSecondsRealtime(2);
                TargetShot();yield return new WaitForSecondsRealtime(1.5f);
                yield return new WaitForEndOfFrame();CaptureStill(index,"before");
                int poses=arena.DamagePoseCount;MoveDefinition move=catalog.GetMove(setup.move);
                yield return arena.Attack(0,move.type,move.category,move.id,direct,-1,(float)multiplier);
                if(direct)yield return arena.Hit(1,result.damage,move.id==53||move.id==55,(float)multiplier);
                result.reactions=arena.DamagePoseCount-poses;
                Health(catalog.GetSpecies(setup.defender).name,result.hpAfter,battle.Active(1).MaxHp);
                yield return new WaitForSecondsRealtime(3);
                PokemonHitReaction reaction=arena.GetDamageReaction(1);
                if(reaction!=null)
                {
                    result.tier=reaction.Tier;result.damageWeight=reaction.DamageWeight;
                    result.peakAngle=reaction.PeakAngle;result.movedJoints=reaction.MovedJointsCount;
                    result.reactionDuration=reaction.Duration;result.usesNativeDamage=reaction.UsesNativeDamage;
                    result.clean=reaction.IsClean&&!reaction.IsPlaying;
                }
                else result.clean=true;
                result.immuneHasNoReaction=setup.multiplier==0&&result.reactions==0&&
                    (reaction==null||(!reaction.IsPlaying&&reaction.DamageWeight==0&&reaction.PeakAngle==0));
                result.passed=result.resolvedExpected&&result.survived&&result.clean&&
                    (setup.multiplier==0?result.immuneHasNoReaction&&result.hpAfter==result.hpBefore:
                        result.reactions==1&&result.damage>0&&result.hpAfter<result.hpBefore&&
                        result.tier==index&&result.damageWeight>0&&result.reactionDuration>0);
                yield return new WaitForEndOfFrame();CaptureStill(index,"recovered");
                Debug.Log("[effectiveness-review-case] "+JsonUtility.ToJson(result));
                while(Time.realtimeSinceStartup-began<caseSeconds)yield return null;
            }
            SetPhase("COMPARAISON TERMINÉE","Immunité → très légère → légère → normale → forte → extrême");
            while(Time.realtimeSinceStartup-started<requestedSeconds)yield return null;
            yield return StopCapture();Finish();
        }
        static int FindMove(IReadOnlyList<BattleEvent> events,int move)
        {
            for(int i=0;i<events.Count;i++)if(events[i].Kind==BattleEventKind.MoveUsed&&events[i].Side==0&&events[i].MoveId==move)return i;
            return -1;
        }
        static int FindImmune(IReadOnlyList<BattleEvent> events,int used,int move)
        {
            if(used<0)return -1;
            for(int i=used+1;i<events.Count;i++)
            {
                if(events[i].Kind==BattleEventKind.MoveUsed||events[i].Kind==BattleEventKind.Switched)break;
                if(events[i].Kind==BattleEventKind.Immune&&events[i].Side==1&&events[i].MoveId==move)return i;
            }
            return -1;
        }
        void Overview()=>ModernArenaReview.CameraShot(arena.ArenaCamera,new Vector3(0,4.25f,-12.6f),new Vector3(0,1.65f,0),48);
        void TargetShot()
        {
            Bounds b=arena.PokemonWorldBounds(1);float radius=Mathf.Max(.65f,b.extents.y,b.extents.x/(1080f/608));
            float distance=Mathf.Max(4.5f,radius/Mathf.Tan(21*Mathf.Deg2Rad)*1.65f);
            Vector3 target=b.center+Vector3.up*(radius*.09f);
            ModernArenaReview.CameraShot(arena.ArenaCamera,target+new Vector3(-.55f,.22f,-1).normalized*distance,target,42);
        }
        void CreateOverlay()
        {
            var root=new GameObject("Effectiveness comparison captions",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=arena.ArenaCamera;canvas.planeDistance=.5f;canvas.sortingOrder=100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var band=new GameObject("Comparison phase",typeof(RectTransform),typeof(Image));band.transform.SetParent(root.transform,false);
            var rect=band.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;
            rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,132);rect.anchoredPosition=Vector2.zero;
            band.GetComponent<Image>().color=new Color(.025f,.045f,.07f,.84f);
            titleText=Caption(band.transform,"Phase",35,new Color(1,.82f,.3f),new Vector2(38,-14),new Vector2(1840,48));
            detailText=Caption(band.transform,"Matchup",25,new Color(.9f,.95f,1),new Vector2(40,-74),new Vector2(1810,37));
            healthText=Caption(root.transform,"Target health",24,Color.white,new Vector2(38,-153),new Vector2(1500,36));
        }
        static Text Caption(Transform parent,string name,int size,Color color,Vector2 position,Vector2 extent)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=position;rect.sizeDelta=extent;
            var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize=size;text.color=color;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;return text;
        }
        void SetPhase(string title,string detail){phase=title;titleText.text=title;detailText.text=detail;}
        void Health(string name,int hp,int max)=>healthText.text=name.ToUpperInvariant()+" · PV "+hp+" / "+max;
        void CaptureStill(int index,string suffix)=>ModernArenaReview.Capture(arena.ArenaCamera,Path.Combine(output,(index+1).ToString("D2")+"-"+suffix+".png"),1080,608);
        IEnumerator CaptureMotion()
        {
            captureTarget=new RenderTexture(1080,608,24,RenderTextureFormat.ARGB32){antiAliasing=2};captureTarget.Create();
            captureImage=new Texture2D(1080,608,TextureFormat.RGB24,false);float next=Time.realtimeSinceStartup;
            while(capturing)
            {
                yield return new WaitForEndOfFrame();if(!capturing)break;
                float now=Time.realtimeSinceStartup;if(now<next)continue;
                var camera=arena.ArenaCamera;RenderTexture previous=camera.targetTexture,active=RenderTexture.active;float aspect=camera.aspect;
                try
                {
                    camera.targetTexture=captureTarget;camera.aspect=1080f/608;camera.Render();RenderTexture.active=captureTarget;
                    captureImage.ReadPixels(new Rect(0,0,1080,608),0,0);captureImage.Apply(false,false);
                    File.WriteAllBytes(Path.Combine(output,"reaction-motion",frameTimes.Count.ToString("D5")+".png"),captureImage.EncodeToPNG());
                }
                finally{camera.targetTexture=previous;camera.aspect=aspect;RenderTexture.active=active;}
                frameTimes.Add(now-captureStarted);frameStages.Add(phase);next=now+1f/16;
            }
            File.WriteAllText(Path.Combine(output,"reaction-motion","frames.json"),JsonUtility.ToJson(new Report{
                captureTimes=frameTimes.ToArray(),stages=frameStages.ToArray(),captureFrames=frameTimes.Count,
                captureSeconds=Time.realtimeSinceStartup-captureStarted},true));
            ReleaseCapture();
        }
        IEnumerator StopCapture(){capturing=false;yield return new WaitForEndOfFrame();yield return null;}
        void Finish()
        {
            if(finishing)return;finishing=true;arena.EndProtection(true);
            bool passed=errors==0&&results.Count==6;foreach(CaseReport c in results)passed&=c.passed;
            if(results.Count==6)passed&=results[1].damageWeight<results[2].damageWeight&&results[2].damageWeight<results[3].damageWeight
                &&results[4].peakAngle>results[2].peakAngle&&results[5].peakAngle>results[4].peakAngle;
            var report=new Report{passed=passed,errors=errors,frames=renderedFrames,captureFrames=frameTimes.Count,
                seconds=Time.realtimeSinceStartup-started,captureSeconds=Time.realtimeSinceStartup-captureStarted,
                directory=output,cases=results.ToArray(),captureTimes=frameTimes.ToArray(),stages=frameStages.ToArray()};
            File.WriteAllText(Path.Combine(output,"effectiveness-review.json"),JsonUtility.ToJson(report,true));
            Debug.Log("[effectiveness-review-result] "+JsonUtility.ToJson(report));Application.Quit(passed?0:1);
        }
        void Update(){renderedFrames++;}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        void ReleaseCapture()
        {
            if(captureTarget!=null){captureTarget.Release();Destroy(captureTarget);captureTarget=null;}
            if(captureImage!=null){Destroy(captureImage);captureImage=null;}
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;capturing=false;ReleaseCapture();if(arena!=null)arena.EndProtection(true);}
    }
}
