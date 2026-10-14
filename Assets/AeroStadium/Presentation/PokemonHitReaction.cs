using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AeroStadium.Presentation
{
    /// <summary>Scales a live native Damage performance and adds recoil without editing the source skeleton or clips.</summary>
    [DefaultExecutionOrder(220)]
    [DisallowMultipleComponent]
    public sealed class PokemonHitReaction : MonoBehaviour
    {
        enum JointKind { Head, Torso, Arm, ForeArm, Wing, Tail }
        sealed class Joint
        {
            public Transform bone;
            public JointKind kind;
            public Quaternion original;
            public float side;
        }

        NativePokemonModel model;
        Transform boundRoot;
        Joint[] joints = Array.Empty<Joint>();
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        float started;
        bool running, applied;

        public float Effectiveness { get; private set; }
        /// <summary>0: immune; 1: quarter; 2: half; 3: neutral; 4: double; 5: quadruple.</summary>
        public int Tier { get; private set; }
        public float DamageWeight { get; private set; }
        public float Duration { get; private set; }
        public float PeakAngle { get; private set; }
        public int MovedJointsCount { get; private set; }
        public bool UsesNativeDamage { get; private set; }
        public bool IsPlaying => running;
        public bool IsClean => !running && !applied && !graph.IsValid();

        public static float Play(NativePokemonModel native, float effectiveness)
        {
            if (native == null || native.ModelRoot == null) return 0f;
            var result = native.GetComponent<PokemonHitReaction>();
            // An immune hit does not create a pose, a playable graph, or a Damage animation.
            if (effectiveness <= 0f && result == null) return 0f;
            if (result == null) result = native.gameObject.AddComponent<PokemonHitReaction>();
            result.Configure(native);
            return result.Begin(effectiveness);
        }

        void Configure(NativePokemonModel native)
        {
            Cancel(); model = native;
            if (boundRoot == native.ModelRoot) return;
            boundRoot = native.ModelRoot;
            var selected = new List<Joint>();
            Transform[] bones = boundRoot.GetComponentsInChildren<Transform>(true);
            bool hasHead = Array.Exists(bones, t => string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase));
            foreach (var bone in bones)
            {
                string name = bone.name.ToLowerInvariant();
                JointKind kind;
                if (name == "head" || (!hasHead && (name == "neck" || name == "neck1"))) kind = JointKind.Head;
                else if (name == "spine1" || name == "chest") kind = JointKind.Torso;
                else if (name == "larm" || name == "rarm" || name == "lupperarm" || name == "rupperarm") kind = JointKind.Arm;
                else if (name == "lforearm" || name == "rforearm") kind = JointKind.ForeArm;
                else if (name == "lwing" || name == "rwing" || name == "lwing1" || name == "rwing1") kind = JointKind.Wing;
                else if (name == "tail1" || name == "tail") kind = JointKind.Tail;
                else continue;
                // Never rotate a support branch: the actor and planted feet keep their native location.
                if (CarriesFeet(bone)) continue;
                float side = Mathf.Sign(Vector3.Dot(bone.position - boundRoot.position, boundRoot.right));
                if (side == 0f) side = name.StartsWith("l", StringComparison.Ordinal) ? -1f : 1f;
                selected.Add(new Joint { bone = bone, kind = kind, side = side });
            }
            selected.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));
            joints = selected.ToArray();
        }

        static bool CarriesFeet(Transform bone)
        {
            foreach (var child in bone.GetComponentsInChildren<Transform>(true))
            {
                string name = child.name.ToLowerInvariant();
                if (name.Contains("foot") || name.Contains("thigh") || name.Contains("ankle") ||
                    name.Contains("toe") || name == "lleg" || name == "rleg") return true;
            }
            return false;
        }

        static int Depth(Transform bone) { int depth = 0; while (bone.parent != null) { depth++; bone = bone.parent; } return depth; }

        public float Begin(float effectiveness)
        {
            Cancel();
            if (float.IsNaN(effectiveness) || float.IsInfinity(effectiveness)) effectiveness = 1f;
            Effectiveness = effectiveness <= 0f ? 0f : effectiveness <= .25f ? .25f : effectiveness <= .5f ? .5f : effectiveness < 2f ? 1f : effectiveness < 4f ? 2f : 4f;
            Tier = Effectiveness == 0f ? 0 : Effectiveness == .25f ? 1 : Effectiveness == .5f ? 2 : Effectiveness == 1f ? 3 : Effectiveness == 2f ? 4 : 5;
            DamageWeight = Tier == 0 ? 0f : Tier == 1 ? .10f : Tier == 2 ? .28f : 1f;
            Duration = 0f; PeakAngle = 0f; MovedJointsCount = 0; UsesNativeDamage = false;
            if (Tier == 0 || model == null || !model.TryGetDamageStates(out var animator, out int idleState, out int damageState, out float nativeDuration)) return 0f;
            Duration = Tier == 1 ? .28f : Tier == 2 ? .44f : Tier == 3 ? nativeDuration
                : Tier == 4 ? Mathf.Max(.95f, nativeDuration * 1.18f) : Mathf.Max(1.20f, nativeDuration * 1.42f);

            graph = PlayableGraph.Create("Pokemon effectiveness reaction " + model.Species);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            // Both inputs contain the complete native controller, preserving its eye/smoke secondary layers.
            var idle = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            var damage = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            var state = animator.GetCurrentAnimatorStateInfo(0);
            idle.Play(idleState, 0, state.fullPathHash == idleState ? Mathf.Repeat(state.normalizedTime, 1f) : 0f);
            damage.Play(damageState, 0, 0f);
            damage.SetSpeed(Mathf.Max(.04f, nativeDuration - .12f) / Mathf.Max(.12f, Duration - .12f));
            mixer = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(idle, 0, mixer, 0); graph.Connect(damage, 0, mixer, 1);
            mixer.SetInputWeight(0, 1f); mixer.SetInputWeight(1, 0f);
            var output = AnimationPlayableOutput.Create(graph, "Native reaction pose", animator);
            output.SetSourcePlayable(mixer);
            started = Time.time; running = true; UsesNativeDamage = true; enabled = true;
            graph.Play();
            return Duration;
        }

        // Remove the preceding additive layer before Unity evaluates either source controller.
        void Update()
        {
            Restore();
            if (!running) return;
            float age = Time.time - started;
            if (model == null || model.ModelRoot == null || age >= Duration)
            {
                Cancel();
                if (model != null && model.isActiveAndEnabled) model.Play(PokemonMotionAction.Idle);
                return;
            }
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .045f));
            float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - Duration + .12f) / .12f));
            float weight = DamageWeight * attack * release;
            mixer.SetInputWeight(0, 1f - weight); mixer.SetInputWeight(1, weight);
        }

        void LateUpdate()
        {
            if (!running || model == null || boundRoot == null) return;
            float phase = Mathf.Clamp01((Time.time - started) / Duration);
            float recoil = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / .10f)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((phase - .30f) / .70f)));
            float stagger = Tier >= 4 ? Mathf.Sin(Mathf.Clamp01((phase - .30f) / .70f) * Mathf.PI * 2f) * (1f - phase) : 0f;
            float head = Tier == 1 ? 1.8f : Tier == 2 ? 4.5f : Tier == 4 ? 14f : Tier == 5 ? 28f : 0f;
            float body = Tier == 4 ? 7f : Tier == 5 ? 15f : 0f;
            float arms = Tier == 4 ? 8f : Tier == 5 ? 19f : 0f;
            Vector3 right = boundRoot.right, up = boundRoot.up, forward = -boundRoot.forward;
            int moved = 0;
            foreach (var joint in joints)
            {
                if (joint.bone == null) continue;
                joint.original = joint.bone.localRotation;
                Quaternion delta;
                if (joint.kind == JointKind.Head)
                    delta = Quaternion.AngleAxis(head * recoil, right) * Quaternion.AngleAxis(head * .28f * stagger, up);
                else if (joint.kind == JointKind.Torso)
                    delta = Quaternion.AngleAxis(body * recoil, right) * Quaternion.AngleAxis(body * .24f * stagger, forward);
                else if (joint.kind == JointKind.Tail)
                    delta = Quaternion.AngleAxis(arms * .7f * stagger, up);
                else
                {
                    float factor = joint.kind == JointKind.ForeArm ? .65f : joint.kind == JointKind.Wing ? .85f : 1f;
                    delta = Quaternion.AngleAxis(-joint.side * arms * factor * (recoil + .15f * stagger), forward);
                }
                float angle = Quaternion.Angle(Quaternion.identity, delta);
                if (angle > .05f) moved++;
                PeakAngle = Mathf.Max(PeakAngle, angle);
                joint.bone.rotation = delta * joint.bone.rotation;
            }
            MovedJointsCount = Mathf.Max(MovedJointsCount, moved); applied = joints.Length > 0;
        }

        void Restore()
        {
            if (!applied) return;
            foreach (var joint in joints) if (joint.bone != null) joint.bone.localRotation = joint.original;
            applied = false;
        }

        /// <summary>Interrupt cleanly; the caller owns the animation selected after this method.</summary>
        public void Cancel()
        {
            Restore(); running = false;
            if (graph.IsValid()) graph.Destroy();
            if (enabled) enabled = false;
        }
        void OnDisable() { Cancel(); }
        void OnDestroy() { Cancel(); }
    }
}
