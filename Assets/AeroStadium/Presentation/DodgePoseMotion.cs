using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [Serializable]
    public sealed class DodgePoseTelemetry
    {
        public DodgeFacialTelemetry facial;
        public string profile;
        public int species, jointCount, supportLimbs, posedFrames, articulatedFrames;
        public int anticipationFrames, airborneFrames, landingFrames, returnFrames;
        public float maxJointDegrees, maxLegDegrees, maxArmDegrees, maxFootLift;
        public float maxRootHeight, maxCrouch, maxBodyYaw, maxLean, maxFootGoalError;
        public float outboundReadyProgress = -1f, maximumRestorePositionError, maximumRestoreAngleError;
        public bool nativeAnimatorPreserved, finite = true, restored = true;
        public int plantedFrames, leadingStepFrames, trailingStepFrames, diagonalSupportFrames, reachLimitedFrames;
        public float maxReachCorrection, maxPlantDrift, maxFootSeparation, maxBoneLengthError;
        public float maxHeadCounterYaw, maxTailDegrees, maxArmAsymmetry;
        public bool groundedSidestep, diagonalBound;
    }

    /// <summary>The actor owns horizontal travel. This layer performs an anatomical, temporary evasive step after the native Animator.</summary>
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class DodgePoseMotion : MonoBehaviour
    {
        sealed class Joint { public Transform bone; public Vector3 position; public Quaternion rotation; }
        sealed class Limb
        {
            public Transform upper, middle, tip;
            public Vector3 anchor, baseline, bend, previousPlant;
            public Quaternion orientation;
            public float side, upperLength, lowerLength;
            public bool front, support, planted;
        }

        DodgeFacialPose face;
        NativePokemonModel native;
        Animator animator;
        Transform actor, origin, waist, chest, neck, head;
        readonly List<Transform> tails = new List<Transform>();
        Joint[] joints = Array.Empty<Joint>();
        Limb[] limbs = Array.Empty<Limb>();
        Vector3 direction, home, facing, right;
        float distance, height, sideSign;
        bool applied, active, quadruped, grounded, bounding, power;
        RuntimeAnimatorController controller;
        bool animatorWasEnabled;
        public DodgePoseTelemetry Stats { get; private set; } = new DodgePoseTelemetry();
        public bool Active => active;
        public bool IsClean => !active && !applied;

        // PCHIP tangents preserve authored extrema and avoid the repeated sine-wave bounce of a generic jump.
        static AnimationCurve C(params float[] pairs)
        {
            int n = pairs.Length / 2; var keys = new Keyframe[n];
            for (int i = 0; i < n; i++)
            {
                float tangent = 0f;
                if (i > 0 && i < n - 1)
                {
                    float a = (pairs[i * 2 + 1] - pairs[i * 2 - 1]) / (pairs[i * 2] - pairs[i * 2 - 2]);
                    float b = (pairs[i * 2 + 3] - pairs[i * 2 + 1]) / (pairs[i * 2 + 2] - pairs[i * 2]);
                    if (a * b > 0f) tangent = 2f * a * b / (a + b);
                }
                keys[i] = new Keyframe(pairs[i * 2], pairs[i * 2 + 1], tangent, tangent);
            }
            return new AnimationCurve(keys);
        }
        static readonly AnimationCurve HipY = C(0,0, .035f,-.025f, .078f,-.080f, .14f,-.040f,
            .20f,-.018f, .248f,-.092f, .32f,-.028f, .61f,-.018f, .70f,-.075f, .83f,-.025f, .95f,-.067f, 1,0);
        static readonly AnimationCurve BoundY = C(0,0, .06f,-.064f, .105f,.030f, .155f,.105f,
            .205f,.025f, .26f,-.070f, .34f,-.012f, .64f,0, .71f,-.050f, .82f,.075f, .93f,-.045f, 1,0);
        static readonly AnimationCurve Yaw = C(0,0, .035f,-3, .083f,11, .145f,24, .218f,14,
            .29f,8, .60f,5, .68f,-4, .78f,-19, .88f,-12, .956f,3, 1,0);
        static readonly AnimationCurve Lean = C(0,0, .035f,-2, .09f,12, .16f,17, .218f,5,
            .265f,-5, .34f,2, .60f,1, .70f,-10, .80f,-14, .93f,3, 1,0);
        static readonly AnimationCurve Pitch = C(0,0, .06f,8, .14f,5, .22f,1, .258f,9,
            .34f,3, .62f,1, .71f,7, .83f,3, .95f,6, 1,0);
        static readonly AnimationCurve Guard = C(0,0, .032f,.3f, .09f,1, .22f,.86f,
            .34f,.64f, .57f,.5f, .68f,.75f, .79f,.96f, .94f,.55f, 1,0);
        static readonly AnimationCurve LeadTravel = C(0,0, .035f,0, .07f,.11f, .12f,.48f,
            .17f,.87f, .208f,1, .30f,1);
        static readonly AnimationCurve TrailTravel = C(0,0, .075f,0, .11f,.12f, .155f,.44f,
            .20f,.86f, .254f,1, .30f,1);
        static readonly AnimationCurve LeadLift = C(0,0, .028f,0, .072f,.72f, .12f,1,
            .176f,.38f, .208f,0, .30f,0);
        static readonly AnimationCurve TrailLift = C(0,0, .078f,0, .116f,.58f, .17f,1,
            .225f,.3f, .254f,0, .30f,0);
        static readonly AnimationCurve LeadRoll = C(0,0, .04f,8, .09f,19, .15f,7,
            .195f,12, .23f,0, .30f,0);
        static readonly AnimationCurve TrailRoll = C(0,0, .055f,-13, .085f,-27, .15f,-7,
            .215f,10, .26f,0, .30f,0);

        public static DodgePoseMotion Begin(Transform actor, Vector3 dodgeWorld, float distance)
        {
            if (actor == null) return null;
            var layer = actor.GetComponent<DodgePoseMotion>();
            if (layer == null) layer = actor.gameObject.AddComponent<DodgePoseMotion>();
            layer.Configure(actor, dodgeWorld, distance); return layer;
        }
        public static float MaxExpectedLift(Transform actor)
        {
            var model = actor == null ? null : actor.GetComponent<NativePokemonModel>();
            return model == null ? .15f : Mathf.Clamp(model.WorldModelHeight * (model.Species == 1 ? .12f : .035f), .025f, .30f);
        }

        void Configure(Transform target, Vector3 dodgeWorld, float travel)
        {
            End(); actor = target; native = actor.GetComponent<NativePokemonModel>();
            Stats = new DodgePoseTelemetry { species = native == null ? 0 : native.Species };
            if (native == null || native.ModelRoot == null || !Finite(travel) || travel <= .001f) return;
            direction = Vector3.ProjectOnPlane(dodgeWorld, Vector3.up).normalized;
            if (direction.sqrMagnitude < .5f) return;
            distance = travel; home = actor.position; height = Mathf.Max(.2f, native.WorldModelHeight);
            // PokemonFacing normalizes the anatomical front to actor-local -Z.
            facing = Vector3.ProjectOnPlane(-actor.forward, Vector3.up).normalized;
            if (facing.sqrMagnitude < .5f) facing = Vector3.back;
            right = Vector3.Cross(Vector3.up, facing).normalized;
            sideSign = Vector3.Dot(direction, right) < 0f ? -1f : 1f;
            animator = native.ModelRoot.GetComponent<Animator>();
            controller = animator == null ? null : animator.runtimeAnimatorController;
            animatorWasEnabled = animator != null && animator.enabled;
            var names = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var bone in native.ModelRoot.GetComponentsInChildren<Transform>(true))
                if (!names.ContainsKey(bone.name)) names.Add(bone.name, bone);
            Transform Find(string name) => names.TryGetValue(name, out var bone) ? bone : null;
            origin = Find("Origin"); waist = Find("Waist") ?? origin;
            chest = Find("Spine2") ?? Find("Spine1"); neck = Find("Neck"); head = Find("Head");
            tails.Clear(); for (int i = 1; i <= 5; i++) if (Find("Tail" + i) != null) tails.Add(Find("Tail" + i));
            if (origin == null || waist == null) { Stats.profile = "unavailable-skeleton"; return; }
            var selected = new List<Limb>();
            Add("LLeg", "LFoot", false); Add("RLeg", "RFoot", false);
            int hind = selected.Count;
            Add("LForeArm", "LHand", true); Add("RForeArm", "RHand", true);
            float footY = 0f, handY = 0f;
            foreach (var limb in selected) if (limb.front) handY += limb.tip.position.y; else footY += limb.tip.position.y;
            quadruped = hind == 2 && selected.Count == 4 && (handY - footY) * .5f / height < .26f;
            if (native.Species == 1 || native.Species == 59) quadruped = true;
            if (native.Species == 9) quadruped = false;
            grounded = hind == 2 && footY * .5f - home.y < height * .32f;
            if (native.Species == 1 || native.Species == 9 || native.Species == 59) grounded = true;
            bounding = quadruped && native.Species != 59; power = quadruped && native.Species == 59;
            Stats.profile = !grounded ? "unsupported-aerial-or-serpentine" : bounding ? "diagonal-bound" : power ? "low-power-sidestep" : "weight-transfer-sidestep";
            Stats.groundedSidestep = grounded && !bounding; Stats.diagonalBound = grounded && bounding;
            foreach (var limb in selected) { limb.support = grounded && (!limb.front || quadruped); if (limb.support) Stats.supportLimbs++; }
            limbs = selected.ToArray();
            var affected = new List<Joint>(); var unique = new HashSet<Transform>();
            AddJoint(origin); AddJoint(waist); AddJoint(chest); AddJoint(neck); AddJoint(head);
            foreach (var tail in tails) AddJoint(tail);
            foreach (var limb in limbs) { AddJoint(limb.upper); AddJoint(limb.middle); AddJoint(limb.tip); }
            affected.Sort((a, b) => Depth(a.bone).CompareTo(Depth(b.bone)));
            joints = affected.ToArray(); Stats.jointCount = Mathf.Max(0, joints.Length - 1);
            face=new DodgeFacialPose(actor);Stats.facial=face.Stats;
            Stats.nativeAnimatorPreserved = true; Stats.restored = false; active = true; enabled = true;

            void Add(string middleName, string tipName, bool front)
            {
                Transform middle = Find(middleName), tip = Find(tipName), upper = middle == null ? null : middle.parent;
                // Tortank's actual thigh hinge is trLThigh/trRThigh; the named Thigh is a zero-length sibling.
                if (upper == null || tip == null || upper == origin || !tip.IsChildOf(middle) || !middle.IsChildOf(origin)) return;
                float a = Vector3.Distance(upper.position, middle.position), b = Vector3.Distance(middle.position, tip.position);
                if (a < .001f || b < .001f) return;
                selected.Add(new Limb { upper = upper, middle = middle, tip = tip, front = front,
                    anchor = tip.position, orientation = tip.rotation, upperLength = a, lowerLength = b,
                    side = Vector3.Dot(tip.position - home, direction) >= 0f ? 1f : -1f });
            }
            void AddJoint(Transform bone) { if (bone != null && unique.Add(bone)) affected.Add(new Joint { bone = bone }); }
        }

        // Remove only this frame's additive layer before the next native Animator evaluation.
        void Update() { RestoreLayer(); }

        public void Apply(float progress)
        {
            RestoreLayer();
            if (!active || actor == null || origin == null) return;
            if (!Finite(progress)) { Stats.finite = false; End(); return; }
            float p = Mathf.Clamp01(progress); if (p >= 1f) return;
            float weight = Smooth(0, .026f, p) * (1f - Smooth(.975f, 1, p));
            foreach (var joint in joints)
            {
                if (joint.bone == null) { Stats.finite = false; End(); return; }
                joint.position = joint.bone.localPosition; joint.rotation = joint.bone.localRotation;
            }
            foreach (var limb in limbs) { limb.baseline = limb.tip.position; limb.bend = limb.middle.position - limb.upper.position; }
            applied = true;
            float vertical = height * (bounding ? BoundY.Evaluate(p) : HipY.Evaluate(p) * (power ? .68f : 1f));
            if (!grounded) vertical *= .3f;
            vertical = Mathf.Clamp(vertical, -.17f, MaxExpectedLift(actor));
            float yaw = Yaw.Evaluate(p) * sideSign * (bounding ? .6f : power ? .78f : 1f);
            float lean = Lean.Evaluate(p) * sideSign * (bounding ? .65f : power ? .6f : 1f);
            float pitch = Pitch.Evaluate(p) * (power ? 1.25f : 1f);
            origin.position += Vector3.up * (vertical * weight);
            // Rotate the hips above their support chains; feet and hands then articulate independently.
            Turn(waist, yaw, lean, pitch, weight);
            if (chest != null && chest != waist) Turn(chest, -yaw * .42f, -lean * .30f, -pitch * .25f, weight);
            float counter = quadruped ? .48f : .86f;
            if (neck != null) Turn(neck, -yaw * counter * .44f, -lean * .24f, -pitch * .35f, weight);
            if (head != null) Turn(head, -yaw * counter * (neck == null ? 1f : .56f), -lean * .35f, -pitch * .26f, weight);
            float tailDegrees = 0f;
            for (int i = 0; i < tails.Count; i++)
            {
                float delay = .025f + i * .013f;
                float lag = -Yaw.Evaluate(Mathf.Max(0, p - delay)) * sideSign * (i == 0 ? .57f : .18f);
                Turn(tails[i], lag, 0, -Pitch.Evaluate(Mathf.Max(0, p - delay)) * .16f, weight);
                tailDegrees += Mathf.Abs(lag * weight);
            }
            int contacts = 0; bool leadSwing = false, trailSwing = false; float armA = 0f, armB = 0f;
            float actorTravel = Mathf.Clamp01(Vector3.Dot(actor.position - home, direction) / distance);
            foreach (var limb in limbs)
            {
                if (limb.support)
                {
                    Step(limb, p, out float footTravel, out float lift, out float roll, out bool first);
                    float reach = limb.upperLength + limb.lowerLength;
                    float stride = Mathf.Clamp(distance * (footTravel - actorTravel), -reach * .40f, reach * .40f);
                    float stance = height * .026f * limb.side * Smooth(.12f, .25f, p) * (1 - Smooth(.69f, .97f, p));
                    Vector3 goal = limb.anchor + actor.position - home + direction * (stride + stance);
                    goal.y = limb.anchor.y + actor.position.y - home.y + lift;
                    Solve(limb, goal, weight);
                    limb.tip.rotation = Quaternion.Slerp(limb.tip.rotation,
                        Quaternion.AngleAxis(roll, right) * Quaternion.AngleAxis(sideSign * stance / height * 180f, Vector3.up) * limb.orientation, weight);
                    bool planted = lift < height * .004f;
                    if (planted) { contacts++; if (limb.planted) Stats.maxPlantDrift = Mathf.Max(Stats.maxPlantDrift, Vector3.Distance(limb.tip.position, limb.previousPlant)); limb.previousPlant = limb.tip.position; }
                    limb.planted = planted;
                    if (lift > height * .015f) { if (first) leadSwing = true; else trailSwing = true; }
                    Stats.maxFootLift = Mathf.Max(Stats.maxFootLift, limb.tip.position.y - limb.anchor.y - actor.position.y + home.y);
                }
                else if (grounded && limb.front)
                {
                    // Outside hand checks the threat; inside elbow drives the step, then joins the guard later.
                    float exchange = Smooth(.64f, .73f, p);
                    float outside = Mathf.Lerp(limb.side > 0 ? 1 : 0, limb.side > 0 ? 0 : 1, exchange);
                    float guard = Guard.Evaluate(Mathf.Max(0, p - (1 - outside) * .025f));
                    float drive = Mathf.Lerp(1, -1, exchange);
                    Vector3 goal = limb.baseline + Vector3.up * (vertical * .65f + height * Mathf.Lerp(.095f, .15f, outside) * guard)
                        + facing * (height * Mathf.Lerp(.035f, .105f, outside) * guard)
                        + direction * (height * Mathf.Lerp(.05f, -.065f, outside) * guard * drive);
                    Solve(limb, goal, weight);
                    limb.tip.rotation = Quaternion.AngleAxis(Mathf.Lerp(-9, 16, outside) * drive * sideSign * guard * weight, facing) * limb.tip.rotation;
                    if (limb.side > 0) armA = JointAngle(limb.upper) + JointAngle(limb.middle); else armB = JointAngle(limb.upper) + JointAngle(limb.middle);
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
                if (limb.support) Stats.maxLegDegrees = Mathf.Max(Stats.maxLegDegrees, angle); else Stats.maxArmDegrees = Mathf.Max(Stats.maxArmDegrees, angle);
                Stats.maxBoneLengthError = Mathf.Max(Stats.maxBoneLengthError, Mathf.Abs(Vector3.Distance(limb.upper.position, limb.middle.position) - limb.upperLength), Mathf.Abs(Vector3.Distance(limb.middle.position, limb.tip.position) - limb.lowerLength));
            }
            if (limbs.Length >= 2) Stats.maxFootSeparation = Mathf.Max(Stats.maxFootSeparation, Vector3.Distance(limbs[0].tip.position, limbs[1].tip.position));
            Stats.posedFrames++; if (articulated >= 3) Stats.articulatedFrames++;
            if (p < .055f) Stats.anticipationFrames++;
            if (contacts == 0 && grounded) Stats.airborneFrames++; else if (contacts > 0) Stats.plantedFrames++;
            if (leadSwing) Stats.leadingStepFrames++; if (trailSwing) Stats.trailingStepFrames++;
            if (bounding && contacts == 2) Stats.diagonalSupportFrames++;
            if (p >= .208f && p <= .34f || p >= .93f) Stats.landingFrames++;
            if (p >= .70f) Stats.returnFrames++;
            if (Stats.outboundReadyProgress < 0 && p >= .23f && p < .70f && actorTravel >= .96f) Stats.outboundReadyProgress = p;
            Stats.maxRootHeight = Mathf.Max(Stats.maxRootHeight, vertical * weight); Stats.maxCrouch = Mathf.Max(Stats.maxCrouch, -vertical * weight);
            Stats.maxBodyYaw = Mathf.Max(Stats.maxBodyYaw, Mathf.Abs(yaw * weight)); Stats.maxLean = Mathf.Max(Stats.maxLean, Mathf.Abs(lean * weight));
            Stats.maxHeadCounterYaw = Mathf.Max(Stats.maxHeadCounterYaw, Mathf.Abs(yaw * counter * weight)); Stats.maxTailDegrees = Mathf.Max(Stats.maxTailDegrees, tailDegrees);
            Stats.maxArmAsymmetry = Mathf.Max(Stats.maxArmAsymmetry, Mathf.Abs(armA - armB));
            Stats.nativeAnimatorPreserved &= animator == null || animator.runtimeAnimatorController == controller && animator.enabled == animatorWasEnabled;
            face?.Apply(p);
            if (!Stats.finite) End();
        }

        void Step(Limb limb, float p, out float travel, out float lift, out float roll, out bool first)
        {
            bool returning = p >= .66f;
            first = (returning ? -limb.side : limb.side) > 0;
            if (bounding && !limb.front) first = !first; // Diagonal pairs, never four identical paws.
            float t = returning ? .055f + (p - .70f) * (.175f / .26f) : p;
            if (power && !limb.front) t -= .014f; // Hindquarters supply the push before the forepaw catch.
            if (bounding && limb.front) t += .008f;
            float f = (first ? LeadTravel : TrailTravel).Evaluate(t);
            travel = returning ? 1f - f : f;
            float scale = bounding ? .15f : power ? .075f : first ? .102f : .068f;
            lift = height * scale * (first ? LeadLift : TrailLift).Evaluate(t) * (returning ? .82f : 1f);
            roll = (first ? LeadRoll : TrailRoll).Evaluate(t) * (returning ? -.8f : 1f) * (quadruped ? .7f : 1f);
        }
        void Turn(Transform bone, float yaw, float roll, float pitch, float weight)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(yaw * weight, Vector3.up)
                * Quaternion.AngleAxis(-roll * weight, facing) * Quaternion.AngleAxis(pitch * weight, right) * bone.rotation;
        }
        float JointAngle(Transform bone)
        {
            foreach (var joint in joints) if (joint.bone == bone) return Quaternion.Angle(joint.rotation, bone.localRotation);
            return 0;
        }
        void Solve(Limb limb, Vector3 requested, float weight)
        {
            Vector3 root = limb.upper.position, knee = limb.middle.position, tip = limb.tip.position;
            float a = Vector3.Distance(root, knee), b = Vector3.Distance(knee, tip);
            Vector3 vector = requested - root; float length = vector.magnitude;
            if (length < .00001f || a < .00001f || b < .00001f) return;
            Vector3 aim = vector / length;
            float reach = Mathf.Clamp(length, Mathf.Abs(a - b) + .00001f, a + b - .00001f);
            Vector3 goal = root + aim * reach;
            float correction = Mathf.Abs(length - reach);
            if (limb.support && correction > .001f) { Stats.reachLimitedFrames++; Stats.maxReachCorrection = Mathf.Max(Stats.maxReachCorrection, correction); }
            Vector3 bend = Vector3.ProjectOnPlane(limb.bend, aim);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(knee - root, aim);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(facing, aim);
            if (bend.sqrMagnitude < .000001f) return;
            float along = (a * a + reach * reach - b * b) / (2f * reach);
            Vector3 hinge = root + aim * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            limb.upper.rotation = Quaternion.Slerp(limb.upper.rotation, Quaternion.FromToRotation(knee - root, hinge - root) * limb.upper.rotation, weight);
            limb.middle.rotation = Quaternion.Slerp(limb.middle.rotation, Quaternion.FromToRotation(limb.tip.position - limb.middle.position, goal - limb.middle.position) * limb.middle.rotation, weight);
            if (limb.support) Stats.maxFootGoalError = Mathf.Max(Stats.maxFootGoalError, Vector3.Distance(limb.tip.position, goal) * weight);
        }
        void RestoreLayer()
        {
            face?.Restore();
            if (!applied) return;
            foreach (var joint in joints)
            {
                if (joint.bone == null) continue;
                joint.bone.SetLocalPositionAndRotation(joint.position, joint.rotation);
                Stats.maximumRestorePositionError = Mathf.Max(Stats.maximumRestorePositionError, Vector3.Distance(joint.bone.localPosition, joint.position));
                Stats.maximumRestoreAngleError = Mathf.Max(Stats.maximumRestoreAngleError, Quaternion.Angle(joint.bone.localRotation, joint.rotation));
            }
            applied = false;
        }
        public void End() { active = false; RestoreLayer();face?.Dispose();face=null; Stats.restored = true; if (enabled) enabled = false; }
        void OnDisable() { End(); }
        void OnDestroy() { End(); }
        static int Depth(Transform bone) { int count = 0; for (; bone.parent != null; bone = bone.parent) count++; return count; }
        static float Smooth(float a, float b, float p) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, p));
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}
