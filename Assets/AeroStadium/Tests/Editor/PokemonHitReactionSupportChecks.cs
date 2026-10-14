using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;

namespace AeroStadium.Tests
{
    public sealed class PokemonHitReactionSupportChecks
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(name, false)).First(type => type != null);

        [Test]
        public void ExtremeWeedleRecoilLeavesSupportFeetAtNativePose()
        {
            GameObject actor = null;
            object reaction = null;
            Type reactionType = Find("AeroStadium.Presentation.PokemonHitReaction");
            try
            {
                var prefab = (GameObject)Find("AeroStadium.Presentation.PokemonPrefabCatalog")
                    .GetMethod("Load").Invoke(null, new object[] { 13 });
                Assert.That(prefab, Is.Not.Null, "The prepared native Weedle model is required.");
                actor = UnityEngine.Object.Instantiate(prefab);
                actor.name = "Weedle support recoil check";
                var native = actor.GetComponent(Find("AeroStadium.Presentation.NativePokemonModel"));
                Assert.That(native, Is.Not.Null);
                var animator = actor.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);

                float duration = (float)reactionType.GetMethod("Play")
                    .Invoke(null, new object[] { native, 4f });
                Assert.That(duration, Is.GreaterThan(0f));
                reaction = actor.GetComponent(reactionType);
                Assert.That(reaction, Is.Not.Null);
                var bones = actor.GetComponentsInChildren<Transform>(true);
                var tail = bones.Single(bone => bone.name == "Tail1");
                var feet = bones.Where(bone => bone.name.IndexOf("Foot", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    !bone.name.StartsWith("Eff", StringComparison.OrdinalIgnoreCase)).ToArray();
                Assert.That(feet.Length, Is.GreaterThan(0));
                Assert.That(feet.Any(foot => foot.IsChildOf(tail)), Is.True,
                    "This real skeleton must retain the Tail1 support branch that exposed the defect.");

                // At 55% the original unfiltered Tail1 stagger is nonzero (about 4.68 degrees).
                reactionType.GetField("started", PrivateInstance).SetValue(reaction, Time.time - duration * .55f);
                reactionType.GetMethod("Update", PrivateInstance).Invoke(reaction, null);
                var graph = (PlayableGraph)reactionType.GetField("graph", PrivateInstance).GetValue(reaction);
                Assert.That(graph.IsValid(), Is.True);
                graph.Evaluate(.2f);
                Vector3[] positions = feet.Select(foot => foot.position).ToArray();
                Quaternion[] rotations = feet.Select(foot => foot.rotation).ToArray();

                // Compare to the live native Damage pose, not the rest pose or source-file transforms.
                reactionType.GetMethod("LateUpdate", PrivateInstance).Invoke(reaction, null);
                for (int index = 0; index < feet.Length; index++)
                {
                    Assert.That(Vector3.Distance(feet[index].position, positions[index]), Is.LessThanOrEqualTo(.0001f),
                        feet[index].name + " position must not move through additive recoil.");
                    Assert.That(Quaternion.Angle(feet[index].rotation, rotations[index]), Is.LessThanOrEqualTo(.0001f),
                        feet[index].name + " rotation must not move through additive recoil.");
                }
                Assert.That((int)reactionType.GetProperty("MovedJointsCount").GetValue(reaction), Is.GreaterThan(0),
                    "The corrected reaction must still move non-support joints.");
                reactionType.GetMethod("Cancel").Invoke(reaction, null);
                Assert.That((bool)reactionType.GetProperty("IsClean").GetValue(reaction), Is.True);
                Assert.That((bool)reactionType.GetProperty("IsPlaying").GetValue(reaction), Is.False);
            }
            finally
            {
                if (reaction is UnityEngine.Object live && live != null)
                    reactionType.GetMethod("Cancel").Invoke(reaction, null);
                if (actor != null) UnityEngine.Object.DestroyImmediate(actor);
            }
        }
    }
}
