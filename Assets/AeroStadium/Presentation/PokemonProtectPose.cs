using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>A short defensive gesture layered onto the live native skeleton, without editing source clips.</summary>
    [DefaultExecutionOrder(230)]
    [DisallowMultipleComponent]
    public sealed class PokemonProtectPose : MonoBehaviour
    {
        public const float Duration = 1.15f;
        enum Kind { Head, Torso, Arm, ForeArm, Wing }
        sealed class Joint
        {
            public Transform bone, child;
            public Kind kind;
            public Quaternion original;
            public float side;
        }
        NativePokemonModel model;
        Joint[] joints = Array.Empty<Joint>();
        float started;
        bool running, applied;
        public bool Done => !running;
        public bool IsPlaying => running;
        public int MovedJointsCount { get; private set; }
        public float PeakAngle { get; private set; }

        public static PokemonProtectPose Create(NativePokemonModel native)
        {
            if (native == null || native.ModelRoot == null) return null;
            var result = native.GetComponent<PokemonProtectPose>();
            if (result == null) result = native.gameObject.AddComponent<PokemonProtectPose>();
            result.Configure(native);
            result.Begin();
            return result;
        }

        void Configure(NativePokemonModel native)
        {
            Restore(); model = native;
            var selected = new List<Joint>();
            Transform[] bones = native.ModelRoot.GetComponentsInChildren<Transform>(true);
            bool hasHead = Array.Exists(bones, t => string.Equals(t.name, "Head", StringComparison.OrdinalIgnoreCase));
            foreach (var bone in bones)
            {
                string name = bone.name.ToLowerInvariant();
                Kind kind;
                if (name == "head" || (!hasHead && (name == "neck" || name == "neck1"))) kind = Kind.Head;
                else if (name == "spine1" || name == "chest")
                {
                    // A shared root or a torso carrying legs would move the planted feet.
                    bool carriesLegs = false;
                    foreach (var child in bone.GetComponentsInChildren<Transform>(true))
                    {
                        string childName = child.name.ToLowerInvariant();
                        if (childName.Contains("thigh") || childName.Contains("foot") || childName == "lleg" || childName == "rleg")
                        { carriesLegs = true; break; }
                    }
                    if (carriesLegs) continue;
                    kind = Kind.Torso;
                }
                else if (name == "larm" || name == "rarm" || name == "lupperarm" || name == "rupperarm") kind = Kind.Arm;
                else if (name == "lforearm" || name == "rforearm") kind = Kind.ForeArm;
                else if (name == "lwing" || name == "rwing" || name == "lwing1" || name == "rwing1") kind = Kind.Wing;
                else continue;
                Transform aimChild = null;
                foreach (Transform child in bone)
                {
                    if (child.name.StartsWith("Eff", StringComparison.OrdinalIgnoreCase)) continue;
                    if ((child.position - bone.position).sqrMagnitude > .000001f) { aimChild = child; break; }
                }
                if ((kind == Kind.Arm || kind == Kind.ForeArm || kind == Kind.Wing) && aimChild == null) continue;
                float side = Mathf.Sign(Vector3.Dot(bone.position - native.ModelRoot.position, native.ModelRoot.right));
                if (side == 0f) side = name.StartsWith("l", StringComparison.Ordinal) ? -1f : 1f;
                selected.Add(new Joint { bone = bone, child = aimChild, kind = kind, side = side });
            }
            // Parents first so elbow targets are evaluated after the shoulder has moved.
            selected.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));
            joints = selected.ToArray();
        }
        static int Depth(Transform value) { int depth = 0; while (value.parent != null) { depth++; value = value.parent; } return depth; }

        public void Begin()
        {
            Restore(); MovedJointsCount = 0; PeakAngle = 0f;
            started = Time.time; running = model != null && joints.Length > 0;
            enabled = true;
        }

        // Unity evaluates the Animator after Update. Removing our previous layer first
        // also protects channels that the current source clip does not animate.
        void Update() { Restore(); }
        void LateUpdate()
        {
            if (!running || model == null || model.ModelRoot == null) return;
            float age = Time.time - started;
            if (age >= Duration) { running = false; return; }
            float prepare = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / .22f));
            float guard = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - .12f) / .18f));
            float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - .75f) / .40f));
            float settle = 1f + .055f * Mathf.Sin(Mathf.Clamp01((age - .3f) / .45f) * Mathf.PI);
            Vector3 up = model.ModelRoot.up, right = model.ModelRoot.right;
            Vector3 forward = -model.ModelRoot.forward;
            int moved = 0;
            foreach (var joint in joints)
            {
                if (joint.bone == null) continue;
                joint.original = joint.bone.localRotation;
                Quaternion delta;
                if (joint.kind == Kind.Head || joint.kind == Kind.Torso)
                {
                    float angle = -(joint.kind == Kind.Head ? 14f : 6f) * prepare * release;
                    delta = Quaternion.AngleAxis(angle, right);
                }
                else
                {
                    Vector3 current = joint.child.position - joint.bone.position;
                    Vector3 desired;
                    float limit;
                    if (joint.kind == Kind.Arm)
                    { desired = forward * .55f - up * .32f - right * joint.side * .34f; limit = 40f; }
                    else if (joint.kind == Kind.ForeArm)
                    { desired = forward * .45f + up * .65f - right * joint.side * .28f; limit = 62f; }
                    else
                    { desired = forward * .25f - right * joint.side * .8f + up * .10f; limit = 14f; }
                    Quaternion toward = current.sqrMagnitude > .000001f ? Quaternion.FromToRotation(current, desired) : Quaternion.identity;
                    float angle = Quaternion.Angle(Quaternion.identity, toward);
                    float fraction = angle > .0001f ? Mathf.Min(1f, limit / angle) : 0f;
                    delta = Quaternion.Slerp(Quaternion.identity, toward, fraction * guard * release * settle);
                }
                float changed = Quaternion.Angle(Quaternion.identity, delta);
                if (changed > .05f) moved++;
                PeakAngle = Mathf.Max(PeakAngle, changed);
                joint.bone.rotation = delta * joint.bone.rotation;
            }
            MovedJointsCount = Mathf.Max(MovedJointsCount, moved);
            applied = true;
        }

        void Restore()
        {
            if (!applied) return;
            foreach (var joint in joints) if (joint.bone != null) joint.bone.localRotation = joint.original;
            applied = false;
        }
        void OnDisable() { Restore(); running = false; }
        void OnDestroy() { Restore(); }
    }
}
