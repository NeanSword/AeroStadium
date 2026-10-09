using System;
using System.Collections;
using UnityEngine;
namespace AeroStadium.Presentation
{
    public sealed class StadiumReview : MonoBehaviour
    {
        ArenaView arena; StadiumEnvironment environment; string label; int errors, visited, expected;
        public void Begin(ArenaView view, StadiumEnvironment stage)
        {
            arena=view; environment=stage; expected=StadiumEnvironment.Inventory.arenas.Length;
            Application.logMessageReceived+=OnLog; StartCoroutine(Run());
        }
        void OnLog(string message, string stack, LogType type)
        {
            if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors++;
        }
        IEnumerator Run()
        {
            arena.ShowPokemon(0,6); arena.ShowPokemon(1,9);
            string[] featured={"stadium1_blaine","stadium1_giovanni","stadium1_surge","arena_19","arena_21"};
            foreach(string key in featured) yield return Inspect(key,14);
            foreach(var entry in StadiumEnvironment.Inventory.arenas)
                if(Array.IndexOf(featured,entry.key)<0) yield return Inspect(entry.key,2);
            bool passed=errors==0&&visited==expected&&expected==48&&arena.LoadedModels==2;
            Debug.Log("[stadium-review-result] arenas="+visited+" models="+arena.LoadedModels+" errors="+errors+" passed="+passed);
            Application.Quit(passed?0:1);
        }
        IEnumerator Inspect(string key,float seconds)
        {
            environment.Load(key); label=Array.Find(StadiumEnvironment.Inventory.arenas,e=>e.key==key).name;
            yield return null;
            if(environment.RendererCount<=0){Debug.LogError("Stadium empty: "+key);yield break;}
            if(!arena.BattlePositionsValid)Debug.LogError("Pokemon outside verified field: "+key);
            visited++; Debug.Log("[stadium-review] "+visited+" "+key+" renderers="+environment.RendererCount);
            float duration=arena.PlayAttackAnimation(0,true);
            yield return new WaitForSeconds(Mathf.Max(seconds,duration));
        }
        void OnGUI()
        {
            GUI.color=Color.white;
            GUI.Box(new Rect(20,20,550,65),"STADIUM 1 & 2 — VÉRIFICATION DES DÉCORS\n"+label+"   "+visited+" / "+expected);
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
