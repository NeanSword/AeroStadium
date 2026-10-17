using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [Serializable]
    public sealed class DodgePoseTelemetry
    {
        public string profile;
        public int species, jointCount, supportLimbs, posedFrames, articulatedFrames;
        public int anticipationFrames, airborneFrames, landingFrames, returnFrames;
        public float maxJointDegrees, maxLegDegrees, maxArmDegrees, maxFootLift;
        public float maxRootHeight, maxCrouch, maxBodyYaw, maxLean, maxFootGoalError;
        public float outboundReadyProgress = -1f, maximumRestorePositionError, maximumRestoreAngleError;
        public bool nativeAnimatorPreserved, finite = true, restored = true;
    }

    /// <summary>A temporary post-Animator pose. The arena owns the actor's travel; this layer owns its articulated performance.</summary>
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class DodgePoseMotion : MonoBehaviour
    {
        sealed class Joint
        {
            public Transform bone;
            public Vector3 position;
            public Quaternion rotation;
        }

        sealed class Limb
        {
            public Transform upper, middle, tip;
            public Vector3 anchor, baseline;
            public Quaternion orientation;
            public bool front, support;
            public float side;
        }

        NativePokemonModel native;
        Animator animator;
        Transform actor, origin, head, tail, spine;
        Joint[] joints = Array.Empty<Joint>();
        Limb[] limbs = Array.Empty<Limb>();
        Vector3 direction, home, facing, right;
        float distance, height, sideSign;
        bool applied, active, quadruped, grounded;
        RuntimeAnimatorController controller;
        bool animatorWasEnabled;
        public DodgePoseTelemetry Stats { get; private set; } = new DodgePoseTelemetry();
        public bool Active => active;
        public bool IsClean => !active && !applied;

        public static DodgePoseMotion Begin(Transform actor, Vector3 dodgeWorld, float distance)
        {
            if (actor == null) return null;
            var layer = actor.GetComponent<DodgePoseMotion>();
            if (layer == null) layer = actor.gameObject.AddComponent<DodgePoseMotion>();
            layer.Configure(actor, dodgeWorld, distance);
            return layer;
        }

        public static float MaxExpectedLift(Transform actor)
        {
            var model = actor == null ? null : actor.GetComponent<NativePokemonModel>();
            return model == null ? .15f : Mathf.Clamp(model.WorldModelHeight * .18f, .06f, .38f);
        }

        void Configure(Transform target, Vector3 dodgeWorld, float travel)
        {
            End(); actor = target; native = actor.GetComponent<NativePokemonModel>();
            Stats = new DodgePoseTelemetry { species = native == null ? 0 : native.Species };
            if (native == null || native.ModelRoot == null || !Finite(travel) || travel <= .001f) return;
            direction = Vector3.ProjectOnPlane(dodgeWorld, Vector3.up).normalized;
            if (direction.sqrMagnitude < .5f) return;
            distance = travel; home = actor.position;
            facing = Vector3.ProjectOnPlane(actor.forward, Vector3.up).normalized;
            if (facing.sqrMagnitude < .5f) facing = Vector3.forward;
            right = Vector3.Cross(Vector3.up, facing).normalized;
            sideSign = Vector3.Dot(direction, right) < 0f ? -1f : 1f;
            height = Mathf.Max(.2f, native.WorldModelHeight);
            animator = native.ModelRoot.GetComponent<Animator>();
            controller = animator == null ? null : animator.runtimeAnimatorController;
            animatorWasEnabled = animator != null && animator.enabled;

            var names = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var bone in native.ModelRoot.GetComponentsInChildren<Transform>(true))
                if (!names.ContainsKey(bone.name)) names.Add(bone.name, bone);
            Transform Find(string name) => names.TryGetValue(name, out var bone) ? bone : null;
            origin = Find("Origin") ?? Find("Waist");
            head = Find("Head"); tail = Find("Tail1"); spine = Find("Spine1");
            if (origin == null) { Stats.profile = "unavailable-skeleton"; return; }

            var selected = new List<Limb>();
            Add("LLeg", "LFoot", false); Add("RLeg", "RFoot", false);
            int hind = selected.Count;
            Add("LForeArm", "LHand", true); Add("RForeArm", "RHand", true);
            float footY = 0f, handY = 0f;
            foreach (var limb in selected) if (limb.front) handY += limb.tip.position.y; else footY += limb.tip.position.y;
            quadruped = hind == 2 && selected.Count == 4 && (handY * .5f - footY * .5f) / height < .26f;
            if (native.Species == 1 || native.Species == 59) quadruped = true;
            if (native.Species == 9) quadruped = false;
            grounded = hind == 2 && footY * .5f - home.y < height * .32f;
            if (native.Species == 1 || native.Species == 9 || native.Species == 59) grounded = true;
            Stats.profile = grounded ? quadruped ? "grounded-quadruped" : "grounded-biped" : "unsupported-aerial-or-serpentine";
            foreach (var limb in selected)
            {
                limb.support = grounded && (!limb.front || quadruped);
                if (limb.support) Stats.supportLimbs++;
            }
            limbs = selected.ToArray();
            var affected = new List<Joint>(); var unique = new HashSet<Transform>();
            AddJoint(origin); AddJoint(head); AddJoint(tail); AddJoint(spine);
            foreach (var limb in limbs) { AddJoint(limb.upper); AddJoint(limb.middle); AddJoint(limb.tip); }
            affected.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));
            joints = affected.ToArray(); Stats.jointCount = Mathf.Max(0, joints.Length - 1);
            Stats.nativeAnimatorPreserved = true; Stats.restored = false;
            active = true; enabled = true;

            void Add(string middleName, string tipName, bool front)
            {
                Transform middle = Find(middleName), tip = Find(tipName);
                if (middle == null || tip == null || middle.parent == null || !tip.IsChildOf(middle)
                    || !middle.IsChildOf(origin)) return;
                // Several native rigs have Leg and Thigh as siblings under trThigh. Leg.parent is the actual hinge.
                Transform upper = middle.parent;
                if (upper == origin || Vector3.Distance(upper.position, middle.position) < .001f
                    || Vector3.Distance(middle.position, tip.position) < .001f) return;
                selected.Add(new Limb { upper = upper, middle = middle, tip = tip, front = front,
                    anchor = tip.position, orientation = tip.rotation,
                    side = Vector3.Dot(tip.position - home, direction) >= 0f ? 1f : -1f });
            }
            void AddJoint(Transform bone)
            {
                if (bone != null && unique.Add(bone)) affected.Add(new Joint { bone = bone });
            }
        }

        // Unity evaluates the native Animator after Update. Even unanimated bones regain their saved base pose first.
        void Update() { RestoreLayer(); }

        /// <summary>Call after native Animator/normalization, before grounding and any animated-mesh collision query.</summary>
        public void Apply(float progress)
        {
            RestoreLayer();
            if (!active || actor == null || origin == null) return;
            if (!Finite(progress)) { Stats.finite = false; End(); return; }
            float p = Mathf.Clamp01(progress);
            if (p >= 1f) return;
            float weight = Smooth(0f, .035f, p) * (1f - Smooth(.96f, 1f, p));
            foreach (var joint in joints)
            {
                if (joint.bone == null) { Stats.finite = false; End(); return; }
                joint.position = joint.bone.localPosition; joint.rotation = joint.bone.localRotation;
            }
            foreach (var limb in limbs) limb.baseline = limb.tip.position;
            applied = true;

            float outPhase = Mathf.InverseLerp(.055f, .23f, p);
            float returnPhase = Mathf.InverseLerp(.70f, .96f, p);
            float outHop = Arc(.055f, .23f, p), backHop = Arc(.70f, .96f, p);
            float anticipation = Arc(0f, .11f, p);
            float landing = Arc(.23f, .32f, p), finalLanding = Arc(.96f, 1f, p);
            float returnLoad = Arc(.66f, .72f, p);
            float lift = Mathf.Clamp(height * (quadruped ? .16f : .13f), .055f, .34f);
            float crouch = Mathf.Clamp(height * .075f, .025f, .13f);
            float vertical = grounded ? lift * (outHop + backHop * .86f)
                - crouch * (anticipation + landing * .72f + returnLoad * .65f + finalLanding * .55f)
                : Mathf.Min(.11f, height * .05f) * (outHop + backHop);
            float outboundDrive = Arc(.025f, .32f, p), returnDrive = Arc(.67f, 1f, p);
            float yaw = sideSign * (17f * outboundDrive - 14f * returnDrive);
            float lean = sideSign * (14f * outboundDrive - 11f * returnDrive);
            origin.position += Vector3.up * (vertical * weight);
            origin.rotation = Quaternion.AngleAxis(yaw * weight, Vector3.up)
                * Quaternion.AngleAxis(-lean * weight, facing) * origin.rotation;
            // Keep the eyes toward the opponent while the body banks into its evasive jump.
            if (head != null) head.rotation = Quaternion.AngleAxis(-yaw * weight * .78f, Vector3.up)
                * Quaternion.AngleAxis(lean * weight * .38f, facing) * head.rotation;
            if (tail != null) tail.rotation = Quaternion.AngleAxis(-yaw * weight * .48f, Vector3.up) * tail.rotation;
            if (spine != null) spine.rotation = Quaternion.AngleAxis(-yaw * weight * .13f, Vector3.up) * spine.rotation;

            foreach (var limb in limbs)
            {
                if (limb.support)
                {
                    bool leading = limb.side > 0f;
                    float offset = quadruped && limb.front ? .009f : 0f;
                    float outStart = (leading ? .055f : .083f) + offset;
                    float outEnd = (leading ? .208f : .23f);
                    float backStart = (leading ? .735f : .70f) + offset;
                    float backEnd = leading ? .96f : .936f;
                    float outStep = Smooth(outStart, outEnd, p), backStep = Smooth(backStart, backEnd, p);
                    float footLift = Mathf.Clamp(height * .14f, .035f, .29f)
                        * (Arc(outStart, outEnd, p) + Arc(backStart, backEnd, p) * .88f);
                    Vector3 goal = limb.anchor + direction * (distance * (outStep - backStep));
                    goal.y += actor.position.y - home.y + footLift;
                    Solve(limb, goal, weight);
                    float toeOff = 12f * (Arc(outStart, outEnd, p) - Arc(backStart, backEnd, p));
                    limb.tip.rotation = Quaternion.Slerp(limb.tip.rotation,
                        Quaternion.AngleAxis(toeOff, right) * limb.orientation, weight);
                    Stats.maxFootLift = Mathf.Max(Stats.maxFootLift,
                        limb.tip.position.y - limb.anchor.y - actor.position.y + home.y);
                    Stats.maxFootGoalError = Mathf.Max(Stats.maxFootGoalError, Vector3.Distance(limb.tip.position, goal) * weight);
                }
                else if (grounded && limb.front)
                {
                    float guard = Mathf.Clamp01(outboundDrive + returnDrive * .85f + landing * .2f);
                    Vector3 goal = limb.baseline + Vector3.up * (vertical + height * .10f * guard)
                        + facing * (height * .045f * guard)
                        - right * (Vector3.Dot(limb.baseline - actor.position, right) < 0f ? -1f : 1f) * height * .055f * guard;
                    Solve(limb, goal, weight);
                }
            }
            int articulated = 0;
            foreach (var joint in joints)
            {
                float angle = Quaternion.Angle(joint.rotation, joint.bone.localRotation);
                if (joint.bone != origin && angle > .5f) articulated++;
                Stats.maxJointDegrees = Mathf.Max(Stats.maxJointDegrees, angle);
                if (!Finite(joint.bone.localPosition) || !Finite(joint.bone.localRotation)) Stats.finite = false;
            }
            foreach (var limb in limbs)
            {
                float angle = JointAngle(limb.upper) + JointAngle(limb.middle);
                if (limb.support) Stats.maxLegDegrees = Mathf.Max(Stats.maxLegDegrees, angle);
                else Stats.maxArmDegrees = Mathf.Max(Stats.maxArmDegrees, angle);
            }
            Stats.posedFrames++; if (articulated >= 3) Stats.articulatedFrames++;
            if (p < .055f) Stats.anticipationFrames++;
            if (outHop > .1f || backHop > .1f) Stats.airborneFrames++;
            if (p >= .23f && p <= .32f || p >= .96f) Stats.landingFrames++;
            if (p >= .70f) Stats.returnFrames++;
            if (Stats.outboundReadyProgress < 0f && p >= .23f && p < .70f
                && Vector3.Dot(actor.position - home, direction) >= distance * .96f) Stats.outboundReadyProgress = p;
            Stats.maxRootHeight = Mathf.Max(Stats.maxRootHeight, vertical * weight);
            Stats.maxCrouch = Mathf.Max(Stats.maxCrouch, -vertical * weight);
            Stats.maxBodyYaw = Mathf.Max(Stats.maxBodyYaw, Mathf.Abs(yaw * weight));
            Stats.maxLean = Mathf.Max(Stats.maxLean, Mathf.Abs(lean * weight));
            Stats.nativeAnimatorPreserved &= animator == null || animator.runtimeAnimatorController == controller
                && animator.enabled == animatorWasEnabled;
            if (!Stats.finite) End();
        }

        float JointAngle(Transform bone)
        {
            foreach (var joint in joints) if (joint.bone == bone) return Quaternion.Angle(joint.rotation, bone.localRotation);
            return 0f;
        }

        static void Solve(Limb limb, Vector3 goal, float weight)
        {
            Vector3 root = limb.upper.position, knee = limb.middle.position, tip = limb.tip.position;
            float upperLength = Vector3.Distance(root, knee), lowerLength = Vector3.Distance(knee, tip);
            Vector3 vector = goal - root; float targetDistance = vector.magnitude;
            if (targetDistance < .00001f || upperLength < .00001f || lowerLength < .00001f) return;
            Vector3 direction = vector / targetDistance;
            float reach = Mathf.Clamp(targetDistance, Mathf.Abs(upperLength - lowerLength) + .00001f,
                upperLength + lowerLength - .00001f);
            Vector3 bend = Vector3.ProjectOnPlane(knee - root, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(limb.upper.forward, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(Vector3.up, direction);
            if (bend.sqrMagnitude < .000001f) return;
            bend.Normalize();
            float along = (upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2f * reach);
            float perpendicular = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 desiredKnee = root + direction * along + bend * perpendicular;
            Quaternion upper = Quaternion.FromToRotation(knee - root, desiredKnee - root) * limb.upper.rotation;
            limb.upper.rotation = Quaternion.Slerp(limb.upper.rotation, upper, weight);
            Vector3 target = root + direction * reach;
            Quaternion middle = Quaternion.FromToRotation(limb.tip.position - limb.middle.position,
                target - limb.middle.position) * limb.middle.rotation;
            limb.middle.rotation = Quaternion.Slerp(limb.middle.rotation, middle, weight);
        }

        void RestoreLayer()
        {
            if (!applied) return;
            foreach (var joint in joints)
            {
                if (joint.bone == null) continue;
                joint.bone.SetLocalPositionAndRotation(joint.position, joint.rotation);
                Stats.maximumRestorePositionError = Mathf.Max(Stats.maximumRestorePositionError,
                    Vector3.Distance(joint.bone.localPosition, joint.position));
                Stats.maximumRestoreAngleError = Mathf.Max(Stats.maximumRestoreAngleError,
                    Quaternion.Angle(joint.bone.localRotation, joint.rotation));
            }
            applied = false;
        }

        public void End()
        {
            active = false; RestoreLayer(); Stats.restored = true;
            if (enabled) enabled = false;
        }
        void OnDisable() { End(); }
        void OnDestroy() { End(); }
        static int Depth(Transform bone) { int result = 0; for (; bone.parent != null; bone = bone.parent) result++; return result; }
        static float Smooth(float from, float to, float p) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, p));
        static float Arc(float from, float to, float p)
        {
            if (p <= from || p >= to) return 0f;
            float t = (p - from) / (to - from);
            return Mathf.Sin(t * Mathf.PI);
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}
