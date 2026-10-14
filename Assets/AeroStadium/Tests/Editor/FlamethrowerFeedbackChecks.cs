using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AeroStadium.Tests
{
    public sealed class FlamethrowerFeedbackChecks
    {
        static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name, false)).First(t => t != null);
        static Type EventType => Find("AeroStadium.Core.BattleEvent");
        static Type KindType => Find("AeroStadium.Core.BattleEventKind");
        static object Event(string kind, int side = 0, int amount = 0, int target = -1, int move = 53)
            => Activator.CreateInstance(EventType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { Enum.Parse(KindType, kind), side, "feedback test", amount, target, move, -1, 1d }, null);
        static Array Events(params object[] entries)
        {
            Array result = Array.CreateInstance(EventType, entries.Length);
            for (int i = 0; i < entries.Length; i++) result.SetValue(entries[i], i);
            return result;
        }
        static bool Match(object events, int index, out int damageIndex)
        {
            object[] args = { events, index, -1 };
            bool found = (bool)Find("AeroStadium.Presentation.BattlePresentationHit")
                .GetMethod("TryGetDirectHit").Invoke(null, args);
            damageIndex = (int)args[2];
            return found;
        }
        static object Resolve(bool protect)
        {
            Type catalogType = Find("AeroStadium.Core.Catalog"), memberType = Find("AeroStadium.Core.TeamMember");
            TextAsset data = Resources.Load<TextAsset>("Data/catalog");
            Assert.That(data, Is.Not.Null);
            object catalog = JsonUtility.FromJson(data.text, catalogType);
            Array Team(int species, int move)
            {
                object member = Activator.CreateInstance(memberType);
                memberType.GetField("speciesId").SetValue(member, species);
                memberType.GetField("moveIds").SetValue(member, new[] { move });
                Array team = Array.CreateInstance(memberType, 1); team.SetValue(member, 0); return team;
            }
            object engine = Activator.CreateInstance(Find("AeroStadium.Core.BattleEngine"),
                catalog, Team(6, 53), Team(9, protect ? 182 : 33), 73, 50);
            object choice = Find("AeroStadium.Core.BattleChoice").GetMethod("Move").Invoke(null, new object[] { 0 });
            return engine.GetType().GetMethod("ResolveTurn").Invoke(engine, new[] { choice, choice });
        }
        static int FireMoveIndex(object events)
        {
            int index = 0;
            foreach (object item in (IEnumerable)events)
            {
                if (item.GetType().GetProperty("Kind").GetValue(item).ToString() == "MoveUsed" &&
                    (int)item.GetType().GetProperty("MoveId").GetValue(item) == 53) return index;
                index++;
            }
            Assert.Fail("The engine must resolve a Flamethrower launch."); return -1;
        }
        [Test] public void RealFlamethrowerFindsItsDamageEvent()
        {
            object events = Resolve(false); int used = FireMoveIndex(events);
            Assert.That(Match(events, used, out int damage), Is.True);
            Assert.That(damage, Is.GreaterThan(used));
        }
        [Test] public void RealProtectDoesNotTriggerDamageFeedback()
        {
            object events = Resolve(true);
            Assert.That(Match(events, FireMoveIndex(events), out int damage), Is.False);
            Assert.That(damage, Is.EqualTo(-1));
        }
        [TestCase("MoveUsed")] [TestCase("Switched")]
        public void LaterMoveOrReplacementCannotSupplyTheContact(string boundary)
        {
            var events = Events(Event("MoveUsed"), Event(boundary), Event("Damage", amount: 10, target: 1));
            Assert.That(Match(events, 0, out int damage), Is.False);
            Assert.That(damage, Is.EqualTo(-1));
        }
        [Test] public void OnlyMatchingPositiveOpponentDamageIsSelected()
        {
            var events = Events(Event("MoveUsed"), Event("Damage", amount: 10, target: 0),
                Event("Damage", side: 1, amount: 10, target: 0), Event("Damage", amount: 10, target: 1, move: 33),
                Event("Damage", amount: 0, target: 1), Event("Damage", amount: 10, target: 1));
            Assert.That(Match(events, 0, out int damage), Is.True);
            Assert.That(damage, Is.EqualTo(5));
        }
        [Test] public void InvalidMoveIndexHasNoContact()
        {
            var events = Events(Event("Damage", amount: 10, target: 1));
            foreach (int index in new[] { -1, 0, 1 }) {
                Assert.That(Match(events, index, out int damage), Is.False);
                Assert.That(damage, Is.EqualTo(-1));
            }
        }
    }
}
