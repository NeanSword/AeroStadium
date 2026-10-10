using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Tests
{
    public sealed class PokemonCardEnergyChecks
    {
        GameObject obj;
        Component energy;
        Type type;
        [SetUp] public void Setup()
        {
            type=Array.Find(AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType("AeroStadium.Presentation.PokemonCardEnergy")!=null)
                .GetType("AeroStadium.Presentation.PokemonCardEnergy");
            obj=new GameObject("Card energy check",typeof(RectTransform),typeof(CanvasRenderer));
            var rect=obj.GetComponent<RectTransform>(); rect.pivot=new Vector2(0,1); rect.sizeDelta=new Vector2(136,64);
            energy=obj.AddComponent(type);
        }
        [TearDown] public void Cleanup() { if(obj!=null) UnityEngine.Object.DestroyImmediate(obj); }
        void Call(string name,params object[] args) => type.GetMethod(name).Invoke(energy,args);
        void Populate(VertexHelper mesh) => type.GetMethod("OnPopulateMesh",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(VertexHelper)},null).Invoke(energy,new object[]{mesh});

        [TestCase("Fire")][TestCase("Grass")][TestCase("Water")][TestCase("Electric")]
        [TestCase("Ghost")][TestCase("Psychic")][TestCase("Normal")]
        public void FocusAndTeamBurstRemainBoundedAndDoNotInterceptNavigation(string pokemonType)
        {
            Call("Configure",pokemonType,Color.cyan,6);
            float now=Time.unscaledTime;
            Call("Tick",true,true,now-.1f); Call("Tick",true,true,now); Call("Celebrate");
            Call("Tick",true,true,now+.01f);
            Assert.That(((Graphic)energy).raycastTarget,Is.False);
            using(var mesh=new VertexHelper())
            {
                Populate(mesh);
                Assert.That(mesh.currentVertCount,Is.InRange(100,1200),"Active aura must be visible while preserving its memory budget.");
                var vertex=UIVertex.simpleVert;
                for(int i=0;i<mesh.currentVertCount;i++)
                {
                    mesh.PopulateUIVertex(ref vertex,i);
                    Assert.That(float.IsNaN(vertex.position.x)||float.IsInfinity(vertex.position.y),Is.False);
                    Assert.That(vertex.position.x,Is.InRange(-4f,140f));
                    Assert.That(vertex.position.y,Is.InRange(-68f,4f),"Effect should stay in the portrait area, away from labels.");
                }
            }
        }
        [Test] public void UnfocusedCardsSleepAndTeamMarkersRemainVisibleAfterFade()
        {
            Call("Configure","Water",Color.blue,7);
            using(var mesh=new VertexHelper())
            {
                Call("Tick",false,false,0f); Populate(mesh); Assert.That(mesh.currentVertCount,Is.Zero);
                Call("Tick",true,true,.1f); Call("Tick",true,true,.2f);
                for(int i=3;i<12;i++) Call("Tick",false,true,i*.1f);
                Populate(mesh); Assert.That(mesh.currentVertCount,Is.InRange(1,40),"Only static ready markers should remain.");
                Call("Tick",false,false,2f); Populate(mesh); Assert.That(mesh.currentVertCount,Is.Zero);
            }
        }
    }
}
