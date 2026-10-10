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
        void Build(int[] saved = null)
        {
            launchCount = 0;
            var callback = GetType().GetMethod(nameof(CreateLaunchCallback), BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(memberType).Invoke(this, null);
            Invoke("Build", canvasObject.GetComponent<RectTransform>(), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                catalog, null, (Action<int>)(_ => { }), callback, (Action)(() => { }), saved);
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



