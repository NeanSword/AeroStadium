using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace AeroStadium.Presentation
{
    public sealed class StadiumReview:MonoBehaviour
    {
        ArenaView arena;StadiumEnvironment environment;string label;int errors,visited;
        public void Begin(ArenaView view,StadiumEnvironment stage){arena=view;environment=stage;Application.logMessageReceived+=OnLog;StartCoroutine(Run());}
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        IEnumerator Run()
        {
            arena.ShowPokemon(0,6);arena.ShowPokemon(1,9);
            string[] featured={"free_battle","ecruteak_gym","elite_four_5"};
            foreach(string key in featured){yield return Inspect(key,16);}
            foreach(var entry in StadiumEnvironment.Inventory.arenas){if(Array.IndexOf(featured,entry.key)<0)yield return Inspect(entry.key,3);}
            bool passed=errors==0&&visited==30&&arena.LoadedModels==2;
            Debug.Log("[stadium-review-result] arenas="+visited+" models="+arena.LoadedModels+" errors="+errors+" passed="+passed);
            Application.Quit(passed?0:1);
        }
        IEnumerator Inspect(string key,float seconds)
        {
            environment.Load(key);label=Array.Find(StadiumEnvironment.Inventory.arenas,e=>e.key==key).name;
            yield return null;if(environment.RendererCount<=0){Debug.LogError("Stadium empty: "+key);yield break;}
            visited++;Debug.Log("[stadium-review] "+visited+" "+key+" renderers="+environment.RendererCount);
            float duration=arena.PlayAttackAnimation(0,true);yield return new WaitForSeconds(Mathf.Max(seconds,duration));
        }
        void OnGUI(){GUI.color=Color.white;GUI.Box(new Rect(20,20,550,65),"STADIUM 2 — MODERNISATION\n"+label+"   "+visited+" / 30");}
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
