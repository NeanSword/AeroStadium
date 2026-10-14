using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AeroStadium.Tests
{
    // Presentation stays in Assembly-CSharp. Reflection exercises the public
    // selection API without making an Editor test assembly depend on it.
    public sealed class PokemonSelectionChecks
    {
        Type viewType, memberType;
        Component view;
        GameObject canvasObject, viewObject, eventObject;
        object catalog;
        int launchCount;
        bool manuallyEnabledEvents;

        [SetUp]
        public void Setup()
        {
            viewType = FindType("AeroStadium.Presentation.PokemonSelectionView");
            memberType = FindType("AeroStadium.Core.TeamMember");
            var catalogType = FindType("AeroStadium.Core.Catalog");
            var data = Resources.Load<TextAsset>("Data/catalog");
            Assert.That(data, Is.Not.Null, "Tests use the actual game catalog.");
            catalog = JsonUtility.FromJson(data.text, catalogType);
            catalogType.GetMethod("Validate").Invoke(catalog, null);
            canvasObject = new GameObject("Selection check canvas", typeof(RectTransform), typeof(Canvas));
            viewObject = new GameObject("Selection check view");
            view = viewObject.AddComponent(viewType);
            eventObject = new GameObject("Selection check events", typeof(EventSystem));
            var events = eventObject.GetComponent<EventSystem>();
            // Ordinary MonoBehaviour lifecycle does not run in synchronous
            // EditMode tests. Register through the real lifecycle, never inject it.
            var registered = (IList<EventSystem>)typeof(EventSystem).GetField("m_EventSystems",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            manuallyEnabledEvents = !registered.Contains(events);
            if (manuallyEnabledEvents) InvokeEventLifecycle(events, "OnEnable");
            EventSystem.current = events;
            Build();
        }

        [TearDown]
        public void TearDown()
        {
            if (viewObject != null) UnityEngine.Object.DestroyImmediate(viewObject);
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
            if (eventObject != null)
            {
                if (manuallyEnabledEvents) InvokeEventLifecycle(eventObject.GetComponent<EventSystem>(), "OnDisable");
                manuallyEnabledEvents = false;
                UnityEngine.Object.DestroyImmediate(eventObject);
            }
        }

        [Test]
        public void GenerationOneIsPaginatedWithoutMissingOrDuplicateCards()
        {
            Assert.That(Read<int>("FilteredCount"), Is.EqualTo(151));
            Assert.That(Read<int>("PageCount"), Is.EqualTo(7));
            Assert.That(Read<int>("PageIndex"), Is.Zero);
            var all = new List<int>();
            for (int page = 0; page < 7; page++)
            {
                Assert.That(Read<int>("PageIndex"), Is.EqualTo(page));
                var ids = Read<IReadOnlyList<int>>("VisibleSpeciesIds");
                Assert.That(Read<IReadOnlyList<Button>>("Cards").Count, Is.EqualTo(ids.Count));
                Assert.That(ids.Count, Is.EqualTo(page == 6 ? 7 : 24));
                all.AddRange(ids);
                if (page < 6) Invoke("ChangePage", 1);
            }
            CollectionAssert.AreEqual(Enumerable.Range(1, 151).ToArray(), all);
            CollectionAssert.AreEqual(Enumerable.Range(145, 7).ToArray(), Read<IReadOnlyList<int>>("VisibleSpeciesIds"));
            Invoke("ChangePage", 1);
            Assert.That(Read<int>("PageIndex"), Is.EqualTo(6), "Cannot advance beyond the final page.");
            Invoke("ChangePage", -100);
            Assert.That(Read<int>("PageIndex"), Is.Zero, "Cannot move before the first page.");
        }

        [TestCase("145")]
        [TestCase("electhor")]
        [TestCase("ÉLECTHOR")]
        public void SearchMatchesIdOrFrenchNameWithoutCaseOrAccentSensitivity(string query)
        {
            Invoke("ChangePage", 6);
            Invoke("SetSearch", query);
            Assert.That(Read<int>("FilteredCount"), Is.EqualTo(1));
            Assert.That(Read<int>("PageIndex"), Is.Zero);
            CollectionAssert.AreEqual(new[] { 145 }, Read<IReadOnlyList<int>>("VisibleSpeciesIds"));
        }

        [Test]
        public void EmptySearchResultCanReturnToTheFullCatalog()
        {
            var search = Read<InputField>("SearchField");
            Assert.That(search.targetGraphic, Is.Not.Null);
            Assert.That(search.targetGraphic.raycastTarget, Is.True, "The search surface must receive pointer input.");
            Invoke("SetSearch", "aucun-pokemon-avec-ce-nom");
            Assert.That(Read<int>("FilteredCount"), Is.Zero);
            Assert.That(Read<IReadOnlyList<Button>>("Cards"), Is.Empty);
            Assert.That(Read<bool>("CanLaunch"), Is.False);
            Assert.That(Read<Button>("StartButton").interactable, Is.False);
            Invoke("SetSearch", "");
            Assert.That(Read<int>("FilteredCount"), Is.EqualTo(151));
            Assert.That(Read<int>("PageCount"), Is.EqualTo(7));
            Assert.That(Read<int>("PageIndex"), Is.Zero);
            CollectionAssert.AreEqual(Enumerable.Range(1, 24).ToArray(), Read<IReadOnlyList<int>>("VisibleSpeciesIds"));
        }

        [Test]
        public void TeamRequiresSixUniqueMembersAndRemovalDisablesLaunchAgain()
        {
            Assert.That(Read<int>("TeamCount"), Is.Zero);
            Assert.That(Read<Button>("StartButton").interactable, Is.False);
            Read<Button>("StartButton").onClick.Invoke();
            Assert.That(launchCount, Is.Zero, "The callback must also guard incomplete teams.");
            Assert.That(Add(1), Is.True);
            Assert.That(Add(1), Is.False, "Duplicates are not allowed.");
            Assert.That(Add(999), Is.False, "An unavailable species is not selectable.");
            for (int id = 2; id <= 5; id++) Assert.That(Add(id), Is.True);
            Assert.That(Read<bool>("CanLaunch"), Is.False);
            Assert.That(Read<Button>("StartButton").interactable, Is.False);
            Assert.That(Add(6), Is.True);
            Assert.That(Read<int>("TeamCount"), Is.EqualTo(6));
            Assert.That(Read<bool>("CanLaunch"), Is.True);
            Assert.That(Read<Button>("StartButton").interactable, Is.True);
            Assert.That(Add(7), Is.False, "A seventh member is rejected.");
            Invoke("RemoveAt", 2);
            Assert.That(Read<int>("TeamCount"), Is.EqualTo(5));
            Assert.That(Read<bool>("CanLaunch"), Is.False);
            Assert.That(Read<Button>("StartButton").interactable, Is.False);
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 6 }, TeamIds(GetTeam()));
            Assert.That(Add(7), Is.True);
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 5, 6, 7 }, TeamIds(GetTeam()));
            Assert.That(Read<Button>("StartButton").interactable, Is.True);
            // Opening guards intentionally reject immediate UI callbacks in EditMode.
        }

        [Test]
        public void TeamOutputPreservesOrderAndRentalDefaultsAndIsIsolated()
        {
            int[] ids = { 6, 3, 9, 25, 1, 150 };
            foreach (int id in ids) Assert.That(Add(id), Is.True);
            Array first = GetTeam();
            CollectionAssert.AreEqual(ids, TeamIds(first));
            foreach (object member in first)
            {
                AssertDefaultStats(member, "ivs", 31);
                AssertDefaultStats(member, "evs", 0);
            }
            object detached = first.GetValue(0);
            memberType.GetField("speciesId").SetValue(detached, 999);
            var statsType = FindType("AeroStadium.Core.StatValues");
            memberType.GetField("ivs").SetValue(detached, Activator.CreateInstance(statsType, new object[] { 7 }));
            memberType.GetField("evs").SetValue(detached, Activator.CreateInstance(statsType, new object[] { 8 }));
            first.SetValue(null, 1);
            Array fresh = GetTeam();
            Assert.That(fresh, Is.Not.SameAs(first));
            Assert.That(fresh.GetValue(0), Is.Not.SameAs(detached));
            CollectionAssert.AreEqual(ids, TeamIds(fresh));
            AssertDefaultStats(fresh.GetValue(0), "ivs", 31);
            AssertDefaultStats(fresh.GetValue(0), "evs", 0);
        }

        [Test]
        public void SavedTeamIsRestoredInOrderWithoutSharingTheSavedArray()
        {
            int[] saved = { 6, 3, 9, 25, 1, 150 };
            Build(saved);
            Assert.That(Read<int>("TeamCount"), Is.EqualTo(6));
            Assert.That(Read<bool>("CanLaunch"), Is.True);
            CollectionAssert.AreEqual(saved, TeamIds(GetTeam()));
            saved[0] = 999;
            Assert.That(TeamIds(GetTeam())[0], Is.EqualTo(6));
        }

        [Test]
        public void RestoringSavedTeamRejectsDuplicatesUnknownSpeciesAndOverflow()
        {
            Build(new[] { 1, 1, 999, 2, 3, 4, 5, 6, 7 });
            Assert.That(Read<int>("TeamCount"), Is.EqualTo(6));
            CollectionAssert.AreEqual(Enumerable.Range(1, 6).ToArray(), TeamIds(GetTeam()));
        }

        [Test]
        public void MouseHoverEnergyFollowsTheInspectedPartnerInsteadOfThePreviousSelectedButton()
        {
            var visible=Read<IReadOnlyList<Button>>("Cards");
            EventSystem.current.SetSelectedGameObject(visible[0].gameObject);
            ExecuteEvents.Execute(visible[23].gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            Assert.That(Read<int>("FocusedSpeciesId"),Is.EqualTo(24));
            viewType.GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
            var energyType=FindType("AeroStadium.Presentation.PokemonCardEnergy");
            var active=energyType.GetField("focused",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That((bool)active.GetValue(visible[23].GetComponentInChildren(energyType)),Is.True);
            Assert.That((bool)active.GetValue(visible[0].GetComponentInChildren(energyType)),Is.False);
        }
        [Test]
        public void WorkshopCancellationKeepsMovesItemAndTrainingUnchanged()
        {
            Add(6);Array before=GetTeam();Assert.That(Invoke("OpenEditor",6),Is.True);
            object editor=viewType.GetProperty("LoadoutEditor").GetValue(view);
            Assert.That(EditorInvoke(editor,"SetIndividualValue",1,0),Is.True);
            Assert.That(EditorInvoke(editor,"SetEffortValue",5,252),Is.True);
            int[] learnable=Learnable(6);int replacement=learnable.First(id=>!Moves(before.GetValue(0)).Contains(id));
            Assert.That(EditorInvoke(editor,"SetMove",0,replacement),Is.True);
            EditorInvoke(editor,"Cancel");
            Assert.That(Read<bool>("EditorOpen"),Is.False);
            object actual=GetTeam().GetValue(0);CollectionAssert.AreEqual(Moves(before.GetValue(0)),Moves(actual));
            AssertDefaultStats(actual,"ivs",31);AssertDefaultStats(actual,"evs",0);
        }
        [Test]
        public void WorkshopSavesLegalMovesAndExactValuesAndCopiesTheResult()
        {
            Assert.That(Invoke("OpenEditor",25),Is.True);object editor=viewType.GetProperty("LoadoutEditor").GetValue(view);
            int[] before=Moves(editor.GetType().GetProperty("Draft").GetValue(editor));
            int legal=Learnable(25).First(id=>!before.Contains(id));
            Assert.That(EditorInvoke(editor,"SetMove",0,legal),Is.True);
            Assert.That(EditorInvoke(editor,"SetIndividualValue",1,0),Is.True);
            Assert.That(EditorInvoke(editor,"SetEffortValue",1,252),Is.True);
            Assert.That(EditorInvoke(editor,"SetEffortValue",5,252),Is.True);
            Assert.That(EditorInvoke(editor,"SetEffortValue",0,6),Is.True);
            Assert.That(EditorInvoke(editor,"Save"),Is.True);
            Assert.That(Read<bool>("EditorOpen"),Is.False);Assert.That(Add(25),Is.True);
            object actual=GetTeam().GetValue(0);Assert.That(Moves(actual)[0],Is.EqualTo(legal));
            Assert.That(Stat(actual,"ivs","attack"),Is.Zero);Assert.That(Stat(actual,"evs","hp"),Is.EqualTo(6));
            Assert.That(Stat(actual,"evs","attack"),Is.EqualTo(252));Assert.That(Stat(actual,"evs","speed"),Is.EqualTo(252));
            Moves(actual)[0]=999;object fresh=GetTeam().GetValue(0);Assert.That(Moves(fresh)[0],Is.EqualTo(legal));
        }
        [Test]
        public void WorkshopRejectsIllegalDuplicateMovesAndExcessTrainingWithoutMutation()
        {
            Add(1);Assert.That(Invoke("OpenEditor",1),Is.True);object editor=viewType.GetProperty("LoadoutEditor").GetValue(view);
            int[] original=Moves(editor.GetType().GetProperty("Draft").GetValue(editor));
            int[] allMoves=((System.Collections.IEnumerable)catalog.GetType().GetField("moves").GetValue(catalog)).Cast<object>().Select(m=>(int)m.GetType().GetField("id").GetValue(m)).ToArray();
            int illegal=allMoves.First(id=>!Learnable(1).Contains(id));
            Assert.That(EditorInvoke(editor,"SetMove",0,illegal),Is.False);
            Assert.That(EditorInvoke(editor,"SetMove",0,original[1]),Is.False);
            Assert.That(EditorInvoke(editor,"SetIndividualValue",1,32),Is.False);
            Assert.That(EditorInvoke(editor,"SetIndividualValue",2,-1),Is.False);
            Assert.That(EditorInvoke(editor,"SetEffortValue",1,253),Is.False);
            Assert.That(EditorInvoke(editor,"SetEffortValue",1,252),Is.True);
            Assert.That(EditorInvoke(editor,"SetEffortValue",5,252),Is.True);
            Assert.That(EditorInvoke(editor,"SetEffortValue",0,6),Is.True);
            Assert.That(EditorInvoke(editor,"SetEffortValue",0,7),Is.False);
            object draft=editor.GetType().GetProperty("Draft").GetValue(editor);
            CollectionAssert.AreEqual(original,Moves(draft));Assert.That(Stat(draft,"ivs","attack"),Is.EqualTo(31));Assert.That(Stat(draft,"evs","hp"),Is.EqualTo(6));
            Assert.That((int)editor.GetType().GetProperty("RemainingEvBudget").GetValue(editor),Is.Zero);
            Assert.That(Invoke("HandleBack"),Is.True);Assert.That(Invoke("HandleBack"),Is.False);
        }
        [Test]
        public void RemovingAConfiguredPartnerClearsItsProfileForTheNextRental()
        {
            Add(6);object profile=Invoke("GetMemberConfiguration",6);profile.GetType().GetField("evs").SetValue(profile,Stats(0,252));
            Assert.That(Invoke("TryConfigureMember",profile),Is.True);Assert.That(Stat(GetTeam().GetValue(0),"evs","attack"),Is.EqualTo(252));
            Invoke("RemoveAt",0);Assert.That(Add(6),Is.True);AssertDefaultStats(GetTeam().GetValue(0),"evs",0);
        }
        [Test]
        public void SavedProfilesSurviveRebuildingTheDeskWithoutSharingArrays()
        {
            Add(6);Add(25);object profile=Invoke("GetMemberConfiguration",6);profile.GetType().GetField("evs").SetValue(profile,Stats(0,252));
            Assert.That(Invoke("TryConfigureMember",profile),Is.True);Array saved=GetTeam();Build(null,saved);
            CollectionAssert.AreEqual(new[]{6,25},TeamIds(GetTeam()));Assert.That(Stat(GetTeam().GetValue(0),"evs","attack"),Is.EqualTo(252));
            Moves(saved.GetValue(0))[0]=999;Assert.That(Moves(GetTeam().GetValue(0))[0],Is.Not.EqualTo(999));
        }
        [Test]
        public void WorkshopOnlyOffersLearnableMovesAndRejectsKeyItems()
        {
            Invoke("OpenEditor",1);object editor=viewType.GetProperty("LoadoutEditor").GetValue(view);
            var choices=(IReadOnlyList<Button>)editor.GetType().GetProperty("VisibleChoices").GetValue(editor);
            foreach(var choice in choices){int id=int.Parse(choice.name.Substring("Capacité ".Length));Assert.That(Learnable(1).Contains(id),Is.True);}
            var items=((System.Collections.IEnumerable)catalog.GetType().GetField("items").GetValue(catalog)).Cast<object>();
            object key=items.First(i=>!(bool)i.GetType().GetField("holdable").GetValue(i) && (string)i.GetType().GetField("id").GetValue(i)!="none");
            string keyId=(string)key.GetType().GetField("id").GetValue(key);Assert.That(EditorInvoke(editor,"SetHeldItem",keyId),Is.False);
            EditorInvoke(editor,"SetTab",2);choices=(IReadOnlyList<Button>)editor.GetType().GetProperty("VisibleChoices").GetValue(editor);
            Assert.That(choices.Any(c=>c.name=="Objet "+keyId),Is.False);
            EditorInvoke(editor,"Cancel");
        }
        [Test]
        public void EveryKantoSpeciesAcceptsItsOwnLearnsetAndIndividualTraining()
        {
            for(int id=1;id<=151;id++)
            {
                object profile=Invoke("GetMemberConfiguration",id);profile.GetType().GetField("moveIds").SetValue(profile,Learnable(id).Take(4).ToArray());
                profile.GetType().GetField("ivs").SetValue(profile,Stats(0,31));profile.GetType().GetField("evs").SetValue(profile,Stats(6,252));
                Assert.That(Invoke("TryConfigureMember",profile),Is.True,"Configurable species "+id);
                Assert.That(Stat(Invoke("GetMemberConfiguration",id),"evs","attack"),Is.EqualTo(252));
            }
        }
        [Test]
        public void EquippingIncompleteEffectsKeepsTheirWarningVisibleBeforeSaving()
        {
            Invoke("OpenEditor",150);object editor=viewType.GetProperty("LoadoutEditor").GetValue(view);
            int[] current=Moves(editor.GetType().GetProperty("Draft").GetValue(editor));
            object move=Learnable(150).Select(id=>catalog.GetType().GetMethod("GetMove").Invoke(catalog,new object[]{id}))
                .First(m=>(string)m.GetType().GetProperty("Support").GetValue(m)!="implemented" && !current.Contains((int)m.GetType().GetField("id").GetValue(m)));
            string support=(string)move.GetType().GetProperty("Support").GetValue(move);
            Assert.That(EditorInvoke(editor,"SetMove",0,(int)move.GetType().GetField("id").GetValue(move)),Is.True);
            var labels=(IReadOnlyList<Text>)editor.GetType().GetField("moveLabels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
            Assert.That(labels[0].text,Does.Contain(support=="partial"?"PARTIEL":"À COMPLÉTER"));
            var items=((System.Collections.IEnumerable)catalog.GetType().GetField("items").GetValue(catalog)).Cast<object>();
            object item=items.First(i=>(bool)i.GetType().GetField("holdable").GetValue(i) && (string)i.GetType().GetField("support").GetValue(i)=="unsupported");
            Assert.That(EditorInvoke(editor,"SetHeldItem",(string)item.GetType().GetField("id").GetValue(item)),Is.True);
            var itemText=(Text)editor.GetType().GetField("itemLabel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor);
            Assert.That(itemText.text,Does.Contain("EFFET À COMPLÉTER"));
            Assert.That(Read<bool>("EditorOpen"),Is.True,"Warnings are visible before the user commits the draft.");
            EditorInvoke(editor,"Cancel");
        }
        object EditorInvoke(object editor,string method,params object[] args)=>editor.GetType().GetMethod(method).Invoke(editor,args);
        int[] Learnable(int species)=>(int[])catalog.GetType().GetMethod("GetLearnableMoves").Invoke(catalog,new object[]{species});
        int[] Moves(object member)=>(int[])memberType.GetField("moveIds").GetValue(member);
        int Stat(object member,string group,string name){object value=memberType.GetField(group).GetValue(member);return (int)value.GetType().GetField(name).GetValue(value);}
        object Stats(int hp,int attack){object value=Activator.CreateInstance(FindType("AeroStadium.Core.StatValues"));value.GetType().GetField("hp").SetValue(value,hp);value.GetType().GetField("attack").SetValue(value,attack);return value;}
        void Build(int[] saved = null,Array savedProfiles = null)
        {
            launchCount = 0;
            var callback = GetType().GetMethod(nameof(CreateLaunchCallback), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(memberType).Invoke(this, null);
            Invoke("Build", canvasObject.GetComponent<RectTransform>(), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                catalog, null, (Action<int>)(_ => { }), callback, (Action)(() => { }), saved, savedProfiles);
        }

        Action<T[]> CreateLaunchCallback<T>() => team => { launchCount++; };
        bool Add(int id) => (bool)Invoke("TryAdd", id);
        Array GetTeam() => (Array)Invoke("GetTeam");
        int[] TeamIds(Array members) => members.Cast<object>()
            .Select(member => (int)memberType.GetField("speciesId").GetValue(member)).ToArray();

        void AssertDefaultStats(object member, string profile, int expected)
        {
            object stats = memberType.GetField(profile).GetValue(member);
            if (stats == null) return; // Core's documented rental defaults are IV31 / EV0.
            foreach (string stat in new[] { "hp", "attack", "defense", "specialAttack", "specialDefense", "speed" })
                Assert.That((int)stats.GetType().GetField(stat).GetValue(stats), Is.EqualTo(expected), profile + "." + stat);
        }

        static void InvokeEventLifecycle(EventSystem events, string name) => typeof(EventSystem)
            .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);

        T Read<T>(string name) => (T)viewType.GetProperty(name).GetValue(view);
        object Invoke(string name, params object[] arguments) => viewType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
            .Invoke(view, arguments);
        static Type FindType(string name)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, "Required compiled type: " + name);
            return type;
        }
    }
}



