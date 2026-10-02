using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public enum PokemonMotionAction { Idle, Showcase, Physical, Special, Damage, Faint }

    /// <summary>Original, rest-space poses. No curves or keyframes are taken from the reference GIFs.</summary>
    [DefaultExecutionOrder(60)]
    public sealed class CinematicMotion : MonoBehaviour
    {
        enum Role { Head, Neck, Body, Tail, Wing, Flipper, Arm, Ear, Jaw, Ornament }
        sealed class Joint
        {
            public Transform Bone;
            public Quaternion Rest;
            public Vector3 Right, Up, Forward;
            public Role Role;
            public float Side, Lag, Share;
            public bool Left, MovesSupport;
            public Quaternion Proposed;
            public float BranchDelay;
        }
        sealed class Foot
        {
            public Transform Upper, Knee, End;
            public Vector3 Target, Bend;
            public Quaternion Rotation;
            public Quaternion UpperRest, KneeRest, EndRest;
            public bool Left, Front;
            public Vector3 Planned;
            public float UpperLength, LowerLength;
        }

        readonly List<Joint> joints = new List<Joint>();
        readonly List<Foot> feet = new List<Foot>();
        readonly List<RigidPokemonDeformer> rigid = new List<RigidPokemonDeformer>();
        readonly List<SupportReachConstraints.Limb> supportLimbs = new List<SupportReachConstraints.Limb>();
        Transform visual;
        Vector3 visualPosition;
        Quaternion visualRotation;
        PokemonMotionProfile profile;
        PokemonMotionAction action;
        float born, actionStarted, actionDuration, height, phase;
        Bounds restBounds;
        bool configured, useSource, hasSkin, liveSample, hasFrame, runtimeRig;
        CreaturePerformance currentFrame, previousFrame;
        float fadeStarted, fadeDuration;
        int species;
        SkinnedFloorProbe floorProbe;

        public int DrivenJointCount => joints.Count;
        public int RigidMeshCount => rigid.Count;
        public int GroundedFeet => feet.Count;
        public bool UsingOriginalMotion => configured && !useSource;
        public float ModelHeight => height;
        public float StandingLift => profile.FloatHeight * height + (profile.FloatHeight > 0f && profile.WingSize > 5f ? restBounds.size.x * .42f : 0f);
        public PokemonMotionAction CurrentAction => action;
        public Bounds RestBounds => restBounds;
        public Vector3 DesiredFootPosition(int index) => feet[index].Planned;

        public void Configure(int speciesId)
        {
            if (configured) return;
            species = speciesId;
            profile = PokemonMotionProfile.ForSpecies(species);
            born = Time.time;
            phase = (species * .61803399f % 1f) * Mathf.PI * 2f;
            // Imported source clips are retained for inspection but never select our default pose.
            foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
            float facingYaw = PokemonFacing.NormalizationYaw(species);
            Quaternion facing = Quaternion.Euler(0f, facingYaw, 0f);
            visual.localPosition = facing * visual.localPosition;
            visual.localRotation = facing * visual.localRotation;
            visualPosition = visual.localPosition;
            visualRotation = visual.localRotation;
            Bounds bounds = GetLocalBounds();
            restBounds = bounds;
            height = Mathf.Max(.15f, bounds.size.y);
            runtimeRig = RuntimePokemonRig.TryBuild(gameObject, species, profile, bounds);
            hasSkin = GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0;
            CalibrateGuard();
            BindJoints(bounds);
            BindFeet();
            foreach (Joint joint in joints)
                foreach (Foot foot in feet)
                    if (foot.Upper.IsChildOf(joint.Bone) || foot.Upper == joint.Bone) joint.MovesSupport = true;
            BindRigidMeshes(bounds);
            if (hasSkin) floorProbe = new SkinnedFloorProbe(gameObject);
            configured = true;
            action = PokemonMotionAction.Idle;
            Debug.Log($"[original-animation] species={species} anatomy={profile.Anatomy} joints={joints.Count} feet={feet.Count} deformedMeshes={rigid.Count} height={height:F3}");
        }

        public float Play(PokemonMotionAction next)
        {
            if (!configured) return 0f;
            EnableOriginalMotion();
            previousFrame = currentFrame;
            fadeStarted = Time.time;
            fadeDuration = hasFrame ? .20f : 0f;
            action = next;
            actionStarted = Time.time + fadeDuration;
            actionDuration = next == PokemonMotionAction.Showcase ? 2.25f
                : next == PokemonMotionAction.Physical || next == PokemonMotionAction.Special ? profile.AttackDuration
                : next == PokemonMotionAction.Damage ? profile.HitDuration
                : next == PokemonMotionAction.Faint ? profile.FaintDuration : 0f;
            return actionDuration + fadeDuration;
        }

        public void BeginSpotlightAction() { Play(PokemonMotionAction.Showcase); }

        public void EnableSourceMotion()
        {
            RestorePose();
            useSource = true;
            foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = true;
        }

        void EnableOriginalMotion()
        {
            if (!useSource) return;
            foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            useSource = false;
        }

        void LateUpdate()
        {
            if (!configured || useSource) return;
            float age = Time.time - actionStarted;
            if (action != PokemonMotionAction.Idle && action != PokemonMotionAction.Faint && age > actionDuration)
            { Play(PokemonMotionAction.Idle); age = Time.time - actionStarted; }
            liveSample = true;
            SamplePose(Time.time - born, action, Mathf.Max(0f, age));
            liveSample = false;
        }

        /// <summary>Deterministic sampling used by the catalog audit and the visible animation review.</summary>
        public void SamplePose(float time, PokemonMotionAction sampledAction = PokemonMotionAction.Idle, float age = 0f)
        {
            if (!configured) return;
            foreach (Foot foot in feet)
            {
                foot.Upper.localRotation = foot.UpperRest;
                foot.Knee.localRotation = foot.KneeRest;
                foot.End.localRotation = foot.EndRest;
            }
            float breathe = Mathf.Sin(time * profile.BreathRate * Mathf.PI * 2f + phase);
            CreaturePerformance performance = CreaturePerformance.Sample(species, profile.Anatomy, sampledAction, time, age, profile);
            float blendPhase = liveSample && fadeDuration > 0f ? Mathf.Clamp01((Time.time - fadeStarted) / fadeDuration) : 1f;
            CreaturePerformance destination = performance;
            if (blendPhase < 1f) performance = CreaturePerformance.Blend(previousFrame, performance, Smooth(blendPhase));
            currentFrame = performance; hasFrame = true;
            // Separate, slower head attention, a pause between glances, and delayed secondary motion.
            float attention = Mathf.Sin(time * .47f + phase);
            float glance = attention * attention * attention;
            float anticipation = Envelope(age, .0f, .17f, .27f, .38f);
            float strike = Envelope(age, .22f, .35f, .45f, profile.AttackDuration);
            float call = Envelope(age, .36f, .64f, .96f, 1.6f);
            float hit = Envelope(age, 0f, .06f, .13f, profile.HitDuration);
            float faint = sampledAction == PokemonMotionAction.Faint ? Smooth(age / profile.FaintDuration) : 0f;
            bool attacking = sampledAction == PokemonMotionAction.Physical || sampledAction == PokemonMotionAction.Special;
            bool special = sampledAction == PokemonMotionAction.Special;
            float hero = sampledAction == PokemonMotionAction.Showcase ? call : 0f;
            if (!attacking) { anticipation = 0f; strike = 0f; }
            if (sampledAction != PokemonMotionAction.Damage) hit = 0f;
            float effort = hero + strike;

            // Move only the visual model; arena positions and game logic remain independent.
            float hover = StandingLift;
            float bob = 0f;
            float sink = faint * height * (profile.FloatHeight > 0f ? .1f : 0f);
            Vector3 travel = new Vector3(performance.RootOffset.x, performance.RootOffset.y, -performance.RootOffset.z) * height;
            bool unarticulatedGround = feet.Count == 0 && profile.FloatHeight == 0f && !hasSkin;
            if (unarticulatedGround) travel = Vector3.zero;
            if (profile.FloatHeight > 0f) travel.y = Mathf.Max(-hover, travel.y);
            visual.localPosition = visualPosition + travel + transform.InverseTransformVector(Vector3.up * (hover + bob));
            float pitch = (anticipation * 3f - strike * 4f + hit * 5f) / profile.Weight;
            float bank = profile.FloatHeight > 0f ? Mathf.Sin(time * 1.15f + phase) * 1.5f : 0f;
            // A grounded faint folds the pose rather than rotating the complete model through the floor.
            visual.localRotation = (unarticulatedGround ? Quaternion.identity : Quaternion.Euler(-performance.RootEuler.x, performance.RootEuler.y, performance.RootEuler.z)) * visualRotation;

            foreach (Joint joint in joints)
            {
                float wave = Mathf.Sin(time * 2.05f + phase - joint.Lag * .58f);
                float delayed = Mathf.Sin(time * 1.35f + phase - joint.Lag * .42f);
                float x = 0f, y = 0f, z = 0f;
                switch (joint.Role)
                {
                    case Role.Head:
                        x = (-performance.HeadPitch - breathe * profile.BreathSize * .5f) * joint.Share;
                        y = performance.HeadYaw * joint.Share;
                        z = delayed * 1.1f * joint.Share;
                        break;
                    case Role.Neck:
                        x = -performance.HeadPitch * .3f * joint.Share;
                        y = performance.HeadYaw * .18f * joint.Share;
                        break;
                    case Role.Body:
                        x = (-performance.BodyPitch + breathe * profile.BreathSize) * joint.Share;
                        z = performance.BodyRoll * joint.Share;
                        if (profile.Anatomy == PokemonAnatomy.Serpent)
                            y = CreaturePerformance.Sample(species, profile.Anatomy, sampledAction, Mathf.Max(0f, time - joint.Lag * .13f), age, profile).TailDrive * .5f * Mathf.Sqrt(joint.Share);
                        break;
                    case Role.Tail:
                        float tailDrive = CreaturePerformance.Sample(species, profile.Anatomy, sampledAction, Mathf.Max(0f, time - joint.Lag * .13f - joint.BranchDelay), age, profile).TailDrive;
                        y = (delayed * profile.TailSize * .3f + tailDrive) * joint.Share;
                        x = wave * profile.TailSize * .24f * joint.Share;
                        if (profile.Anatomy == PokemonAnatomy.Fish || profile.Anatomy == PokemonAnatomy.Serpent)
                            y = tailDrive * joint.Share;
                        break;
                    case Role.Wing:
                        z = joint.Side * performance.WingDrive * joint.Share;
                        x = -Mathf.Sin(time * profile.WingRate * Mathf.PI * 2f - joint.Lag * .1f) * Mathf.Abs(performance.WingDrive) * .12f * joint.Share;
                        break;
                    case Role.Arm:
                        x = -(joint.Left ? performance.ArmSwingLeft : performance.ArmSwingRight) * joint.Share;
                        z = joint.Side * performance.Intensity * 10f * joint.Share;
                        break;
                    case Role.Flipper:
                        y = joint.Side * (Mathf.Sin(time * 2.8f - joint.Lag * .2f + (joint.Left ? 0f : .9f)) * 19f + performance.Intensity * 8f) * joint.Share;
                        x = delayed * 2f;
                        break;
                    case Role.Ear:
                        // An isolated twitch followed by a soft return, separate from the body rhythm.
                        float twitchAge = Mathf.Repeat(time + species * .37f + (joint.Side > 0f ? .16f : 0f), 5.3f);
                        z = joint.Side * (Envelope(twitchAge, .06f, .13f, .19f, .52f) * profile.EarSize + (joint.Left ? performance.EarDriveLeft : performance.EarDriveRight)) * joint.Share;
                        x = delayed * 1.2f * joint.Share;
                        break;
                    case Role.Jaw:
                        x = -performance.JawOpen;
                        break;
                    case Role.Ornament:
                        x = delayed * 2f * joint.Share;
                        z = wave * 2.5f * joint.Share;
                        break;
                }
                if (runtimeRig)
                {
                    float limit = joint.Role == Role.Head ? 22f : joint.Role == Role.Neck ? 12f
                        : joint.Role == Role.Body ? 7f : joint.Role == Role.Flipper ? 8f : 90f;
                    if (species == 42 && joint.Role == Role.Wing) limit = joint.Lag == 0 ? 50f : joint.Lag == 1 ? 22f : 14f;
                    if (species == 99 && joint.Role == Role.Arm) limit = joint.Lag == 0 ? 20f : joint.Lag == 1 ? 15f : 8f;
                    x = Mathf.Clamp(x, -limit, limit); y = Mathf.Clamp(y, -limit, limit); z = Mathf.Clamp(z, -limit, limit);
                }
                // Axes were captured in bind space, including mirrored/imported joint orientations.
                joint.Bone.localRotation = joint.Rest * Quaternion.AngleAxis(x, joint.Right)
                    * Quaternion.AngleAxis(y, joint.Up) * Quaternion.AngleAxis(z, joint.Forward);
                joint.Proposed = joint.Bone.localRotation;
            }
            if (profile.FloatHeight == 0f) foreach (Foot foot in feet)
            {
                bool leftBeat = foot.Left;
                if (profile.Anatomy == PokemonAnatomy.Quadruped && !foot.Front) leftBeat = !leftBeat;
                float lift = leftBeat ? performance.FootLiftLeft : performance.FootLiftRight;
                float advance = leftBeat ? performance.FootAdvanceLeft : performance.FootAdvanceRight;
                if (blendPhase < 1f)
                {
                    float fromAdvance = leftBeat ? previousFrame.FootAdvanceLeft : previousFrame.FootAdvanceRight;
                    float toAdvance = leftBeat ? destination.FootAdvanceLeft : destination.FootAdvanceRight;
                    if (Mathf.Abs(fromAdvance - toAdvance) > .015f) lift += Mathf.Sin(blendPhase * Mathf.PI) * .035f;
                }
                Vector3 step = SupportReachConstraints.ScaleStep(new Vector3(0f, lift, -advance) * height,
                    height, foot.UpperLength, foot.LowerLength);
                foot.Planned = transform.TransformPoint(foot.Target + step);
            }
            ConstrainSupport();
            foreach (Foot foot in feet) PlantFoot(foot);
            ConstrainFloor();
            foreach (var mesh in rigid) mesh.Sample(time, phase, profile, sampledAction, anticipation, strike, hero, hit, faint);
        }

        void BindJoints(Bounds bounds)
        {
            var active = new HashSet<Transform>();
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = skin.sharedMesh;
                if (mesh == null) continue;
                var weights = mesh.boneWeights;
                var used = new bool[skin.bones.Length];
                foreach (var weight in weights)
                {
                    Mark(used, weight.boneIndex0, weight.weight0); Mark(used, weight.boneIndex1, weight.weight1);
                    Mark(used, weight.boneIndex2, weight.weight2); Mark(used, weight.boneIndex3, weight.weight3);
                }
                for (int i = 0; i < used.Length; i++) if (used[i] && skin.bones[i] != null)
                {
                    for (var bone = skin.bones[i]; bone != null && bone != visual && bone != transform; bone = bone.parent) active.Add(bone);
                }
                skin.updateWhenOffscreen = true;
            }
            // Rigid-part skeletons (e.g. Magneton) can animate mesh parents without skinning.
            foreach (var mesh in GetComponentsInChildren<MeshRenderer>(true))
                for (Transform parent = mesh.transform.parent; parent != null && parent != visual && parent != transform; parent = parent.parent)
                    if (IsSemanticJoint(Normalize(parent.name))) active.Add(parent);

            var roles = new Dictionary<Role, int>();
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                if (!active.Contains(bone)) continue;
                Vector3 position = transform.InverseTransformPoint(bone.position);
                if ((position - bounds.center).magnitude > bounds.size.magnitude * 2.5f) continue;
                string name = Normalize(bone.name);
                if (!Classify(bone, name, out Role role)) continue;
                Quaternion inModel = Quaternion.Inverse(transform.rotation) * bone.rotation;
                float side = position.x < -.005f ? -1f : position.x > .005f ? 1f : name.StartsWith("l") ? -1f : 1f;
                joints.Add(new Joint { Bone = bone, Rest = bone.localRotation, Role = role, Side = side,
                    Left = name.StartsWith("l") || (!name.StartsWith("r") && side < 0f),
                    BranchDelay = name.Contains("tailb") ? .22f : name.Contains("tailc") ? .44f : 0f,
                    Right = Quaternion.Inverse(inModel) * Vector3.right,
                    Up = Quaternion.Inverse(inModel) * Vector3.up,
                    Forward = Quaternion.Inverse(inModel) * Vector3.forward,
                    Lag = ChainDepth(bone, role), Share = 1f });
                roles.TryGetValue(role, out int count); roles[role] = count + 1;
            }
            foreach (Joint joint in joints)
            {
                int count = roles[joint.Role];
                if (joint.Role == Role.Body || joint.Role == Role.Neck || joint.Role == Role.Head)
                    joint.Share = 1f / Mathf.Max(1, count);
                if (joint.Role == Role.Tail) joint.Share = 1f / Mathf.Sqrt(Mathf.Max(1, count));
                if (joint.Role == Role.Wing) joint.Share = 1f / (1f + joint.Lag * .9f);
                if (joint.Role == Role.Arm) joint.Share = 1f / (1f + joint.Lag * .8f);
                if (joint.Role == Role.Ear) joint.Share = 1f / (1f + joint.Lag * .9f);
                // Keep the support of planted legs fixed, even on rigs with legs below a spine joint.
                if (profile.FloatHeight == 0f && joint.Role == Role.Body)
                    foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
                        if (Normalize(candidate.name).Contains("foot") && candidate.IsChildOf(joint.Bone)) { joint.Share = 0f; break; }
            }
            if (species == 95)
            {
                float tailLength = 0f;
                foreach (Joint joint in joints) if (joint.Role == Role.Tail) tailLength = Mathf.Max(tailLength, joint.Lag);
                foreach (Joint joint in joints) if (joint.Role == Role.Tail) joint.Lag = tailLength - joint.Lag;
            }
        }

        bool Classify(Transform bone, string n, out Role role)
        {
            role = default;
            if (n.Contains("armature") || n == "model" || n == "origin") return false;
            if (n.Contains("eye") || n.Contains("finger") || n.Contains("toe") || n.Contains("claw") || n.Contains("teeth") || n.Contains("tongue")) return false;
            if (species == 95 && n.StartsWith("bone"))
            { role = n == "bone014" ? Role.Head : n == "bone016" ? Role.Jaw : Role.Tail; return true; }
            if (species == 41 && n.StartsWith("bone"))
            {
                Vector3 p = transform.InverseTransformPoint(bone.position);
                if (Mathf.Abs(p.x) < height * .17f) return false;
                role = Role.Wing; return true;
            }
            if (n.Contains("tail")) { role = Role.Tail; return true; }
            if (n.Contains("jaw") || n.Contains("mouth") || n.Contains("lowerbeak")) { role = Role.Jaw; return true; }
            if (n.Contains("head")) { role = Role.Head; return true; }
            if (n.Contains("neck")) { role = Role.Neck; return true; }
            if (Regex.IsMatch(n, @"^(l|r)?_?ear\d*$")) { role = Role.Ear; return true; }
            if (n.Contains("wing") || (species == 6 && n.Contains("feeler"))) { role = Role.Wing; return true; }
            if (n.Contains("fin") || n.Contains("flipper")) { role = Role.Flipper; return true; }
            if (n.Contains("shoulder") || n.Contains("forearm") || n.Contains("arm") || n.Contains("hand"))
            {
                role = profile.Anatomy == PokemonAnatomy.Bird || profile.Anatomy == PokemonAnatomy.Bat ? Role.Wing : Role.Arm;
                return profile.Anatomy != PokemonAnatomy.Quadruped;
            }
            if (n.Contains("spine") || n.Contains("waist") || n.Contains("chest") || n.Contains("torso") || n == "body" || n.StartsWith("body")) { role = Role.Body; return true; }
            if (n.Contains("leaf") || n.Contains("hair") || n.Contains("frill") || n.Contains("cloth") || n.Contains("feather")) { role = Role.Ornament; return true; }
            return false;
        }

        int ChainDepth(Transform bone, Role role)
        {
            int depth = 0;
            for (var parent = bone.parent; parent != null && parent != visual && parent != transform; parent = parent.parent)
                if (ClassifyNoDepth(parent, out var parentRole) && parentRole == role) depth++;
            return depth;
        }
        bool ClassifyNoDepth(Transform bone, out Role role)
        {
            // Avoid recursive topology classification for the two anonymous rigs.
            if (species == 95) { role = Role.Tail; return Normalize(bone.name).StartsWith("bone"); }
            return Classify(bone, Normalize(bone.name), out role);
        }
        int ChainDepthRaw(Transform bone)
        {
            int depth = 0;
            for (var parent = bone.parent; parent != null && parent != visual && parent != transform; parent = parent.parent) depth++;
            return depth;
        }

        void BindFeet()
        {
            if (profile.FloatHeight != 0f || profile.Anatomy == PokemonAnatomy.Serpent || profile.Anatomy == PokemonAnatomy.Blob) return;
            foreach (Transform bone in GetComponentsInChildren<Transform>(true))
            {
                string name = Normalize(bone.name);
                bool quadruped = profile.Anatomy == PokemonAnatomy.Quadruped || species == 3;
                bool frontPaw = quadruped && Regex.IsMatch(name, @"^[lr]hand\d*$");
                if ((!name.Contains("foot") && !frontPaw) || name.Contains("toe") || bone.parent == null || bone.parent.parent == null) continue;
                Transform knee = bone.parent, upper = knee.parent;
                string kneeName = Normalize(knee.name), upperName = Normalize(upper.name);
                if (!(kneeName.Contains("leg") || kneeName.Contains("knee") || frontPaw && kneeName.Contains("arm"))
                    || !(upperName.Contains("leg") || upperName.Contains("hip") || upperName.Contains("thigh") || frontPaw && (upperName.Contains("arm") || upperName.Contains("shoulder")))) continue;
                if ((bone.position - knee.position).sqrMagnitude < .000001f || (knee.position - upper.position).sqrMagnitude < .000001f) continue;
                // Some plant rigs attach both knees straight to one weighted hip.
                // Give each leg an independent, rest-equivalent proximal pivot;
                // solving the second leg must not rotate the first leg again.
                if (upperName.Contains("hip"))
                {
                    var support = new GameObject("SupportThigh_" + name).transform;
                    support.SetPositionAndRotation(upper.position, upper.rotation);
                    support.SetParent(upper, true);
                    knee.SetParent(support, true);
                    upper = support;
                }
                feet.Add(new Foot { End = bone, Knee = knee, Upper = upper,
                    Target = transform.InverseTransformPoint(bone.position),
                    Bend = transform.InverseTransformVector(knee.position - upper.position),
                    UpperLength = transform.InverseTransformVector(knee.position - upper.position).magnitude,
                    LowerLength = transform.InverseTransformVector(bone.position - knee.position).magnitude,
                    UpperRest = upper.localRotation, KneeRest = knee.localRotation, EndRest = bone.localRotation,
                    Left = name.StartsWith("l"), Front = frontPaw,
                    Rotation = Quaternion.Inverse(transform.rotation) * bone.rotation });
            }
        }

        void CalibrateGuard()
        {
            if (profile.Anatomy != PokemonAnatomy.Biped) return;
            foreach (Transform upper in GetComponentsInChildren<Transform>(true))
            {
                string name = Normalize(upper.name);
                if (!Regex.IsMatch(name, @"^[lr]arm\d*$")) continue;
                Transform elbow = null, hand = null;
                foreach (Transform child in upper)
                    if (Normalize(child.name).Contains("forearm")) { elbow = child; break; }
                if (elbow == null) continue;
                foreach (Transform child in elbow)
                    if (Normalize(child.name).Contains("hand")) { hand = child; break; }
                Vector3 direction = transform.InverseTransformVector(elbow.position - upper.position).normalized;
                if (Mathf.Abs(direction.x) < .72f || Mathf.Abs(direction.y) > .45f) continue;
                float side = direction.x < 0f ? -1f : 1f;
                Vector3 guard = transform.TransformVector(new Vector3(side * .27f, -.81f, -.52f).normalized);
                upper.rotation = Quaternion.FromToRotation(elbow.position - upper.position, guard) * upper.rotation;
                if (hand != null)
                {
                    Vector3 foreGuard = transform.TransformVector(new Vector3(side * .08f, -.32f, -.94f).normalized);
                    elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, foreGuard) * elbow.rotation;
                }
            }
        }

        void ConstrainFloor()
        {
            if (floorProbe == null) return;
            float tolerance = Mathf.Max(.00015f, height * .001f);
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float minimum = floorProbe.MinimumY(-tolerance, out var contact);
                if (minimum >= -tolerance) return;
                bool corrected = false;
                if (contact.InfluencingBones != null) foreach (Joint joint in joints)
                {
                    if (joint.MovesSupport) continue;
                    bool influencesContact = false;
                    foreach (Transform bone in contact.InfluencingBones)
                        if (bone == joint.Bone || bone.IsChildOf(joint.Bone)) { influencesContact = true; break; }
                    if (!influencesContact || Quaternion.Angle(joint.Rest, joint.Bone.localRotation) < .05f) continue;
                    joint.Bone.localRotation = Quaternion.Slerp(joint.Rest, joint.Bone.localRotation, .72f);
                    corrected = true;
                }
                if (corrected) continue;
                if (feet.Count == 0)
                {
                    visual.localPosition += Vector3.up * (-minimum + tolerance);
                    return;
                }
                // The support stays on the floor while a crouch is prevented from
                // pushing the belly through it. Never offset the combat actor.
                visual.localPosition = Vector3.Lerp(visual.localPosition, visualPosition, .30f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, visualRotation, .30f);
                foreach (Joint joint in joints) if (joint.MovesSupport)
                    joint.Bone.localRotation = Quaternion.Slerp(joint.Rest, joint.Bone.localRotation, .30f);
                ProjectCurrentSupport();
                foreach (Foot foot in feet) PlantFoot(foot);
            }
            if (feet.Count == 0)
            {
                float minimum = floorProbe.MinimumY(-tolerance, out _);
                if (minimum < -tolerance) visual.localPosition += Vector3.up * (-minimum + tolerance);
            }
        }

        void ProjectCurrentSupport()
        {
            supportLimbs.Clear();
            foreach (Foot foot in feet)
                supportLimbs.Add(new SupportReachConstraints.Limb(transform.InverseTransformPoint(foot.Upper.position),
                    transform.InverseTransformPoint(foot.Planned), foot.UpperLength, foot.LowerLength));
            if (SupportReachConstraints.TryProjectRoot(supportLimbs, height * .32f, out Vector3 correction, out _))
                visual.localPosition += correction;
        }

        void ConstrainSupport()
        {
            if (feet.Count == 0) return;
            Vector3 proposedPosition = visual.localPosition;
            Quaternion proposedRotation = visual.localRotation;
            // Keep the authored contact clock. Only the pose that moves its support
            // is projected when a short imported limb cannot reach the planned paw.
            for (int attempt = 0; attempt < 7; attempt++)
            {
                float weight = attempt == 6 ? 0f : Mathf.Pow(.5f, attempt);
                visual.localPosition = Vector3.Lerp(visualPosition, proposedPosition, weight);
                visual.localRotation = Quaternion.Slerp(visualRotation, proposedRotation, weight);
                foreach (Joint joint in joints)
                    if (joint.MovesSupport) joint.Bone.localRotation = Quaternion.Slerp(joint.Rest, joint.Proposed, weight);
                supportLimbs.Clear();
                foreach (Foot foot in feet)
                    supportLimbs.Add(new SupportReachConstraints.Limb(transform.InverseTransformPoint(foot.Upper.position),
                        transform.InverseTransformPoint(foot.Planned), foot.UpperLength, foot.LowerLength));
                if (SupportReachConstraints.TryProjectRoot(supportLimbs, height * .32f, out Vector3 correction, out _))
                {
                    visual.localPosition += correction;
                    return;
                }
            }
        }

        void PlantFoot(Foot foot)
        {
            Vector3 target = foot.Planned;
            Vector3 start = foot.Upper.position;
            float a = Vector3.Distance(start, foot.Knee.position), b = Vector3.Distance(foot.Knee.position, foot.End.position);
            Vector3 delta = target - start;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(transform.TransformVector(foot.Bend), direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(transform.forward, direction);
            if (bend.sqrMagnitude < .000001f) return;
            float along = (a * a + distance * distance - b * b) / (2f * distance);
            Vector3 kneeTarget = start + direction * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
            foot.Upper.rotation = Quaternion.FromToRotation(foot.Knee.position - start, kneeTarget - start) * foot.Upper.rotation;
            foot.Knee.rotation = Quaternion.FromToRotation(foot.End.position - foot.Knee.position, target - foot.Knee.position) * foot.Knee.rotation;
            foot.End.rotation = transform.rotation * foot.Rotation;
        }

        void BindRigidMeshes(Bounds bounds)
        {
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
                if (!filter.TryGetComponent<MeshRenderer>(out var renderer) || !renderer.enabled) continue;
                if (filter.GetComponent<SkinnedMeshRenderer>() != null) continue;
                // Mesh parents with an articulated rigid skeleton are already driven above.
                bool drivenParent = false;
                foreach (Joint joint in joints) if (filter.transform.IsChildOf(joint.Bone)) { drivenParent = true; break; }
                if (!drivenParent) rigid.Add(new RigidPokemonDeformer(filter, transform, bounds, species));
            }
        }

        Bounds GetLocalBounds()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    mesh = skin.sharedMesh;
                    if (mesh == null) continue;
                    var bones = skin.bones;
                    var bindposes = mesh.bindposes;
                    var weights = mesh.boneWeights;
                    var vertices = mesh.vertices;
                    var matrices = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                        matrices[i] = transform.worldToLocalMatrix * bones[i].localToWorldMatrix * bindposes[i];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        BoneWeight w = weights[i]; Vector3 v = vertices[i];
                        Vector3 local = SkinPoint(w.boneIndex0, w.weight0) + SkinPoint(w.boneIndex1, w.weight1)
                            + SkinPoint(w.boneIndex2, w.weight2) + SkinPoint(w.boneIndex3, w.weight3);
                        Vector3 SkinPoint(int bone, float weight) => weight <= 0f ? Vector3.zero : matrices[bone].MultiplyPoint3x4(v) * weight;
                        if (first) { result = new Bounds(local, Vector3.zero); first = false; } else result.Encapsulate(local);
                    }
                    continue;
                }
                else if (renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 local = transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if (first) { result = new Bounds(local, Vector3.zero); first = false; } else result.Encapsulate(local);
                }
            }
            return result;
        }

        void RestorePose()
        {
            if (visual != null) { visual.localPosition = visualPosition; visual.localRotation = visualRotation; }
            foreach (Joint joint in joints) joint.Bone.localRotation = joint.Rest;
            foreach (Foot foot in feet)
            {
                foot.Upper.localRotation = foot.UpperRest;
                foot.Knee.localRotation = foot.KneeRest;
                foot.End.localRotation = foot.EndRest;
            }
            foreach (var mesh in rigid) mesh.Restore();
        }
        void OnDestroy() { foreach (var mesh in rigid) mesh.Dispose(); }
        static void Mark(bool[] used, int i, float weight) { if (weight > .00001f && i >= 0 && i < used.Length) used[i] = true; }
        static bool IsSemanticJoint(string n) => !n.Contains("armature") && (n.Contains("head") || n.Contains("arm") || n.Contains("tail") || n.Contains("wing") || n.Contains("spine") || n.Contains("jaw") || Regex.IsMatch(n, @"^(l|r)?_?ear\d*$"));
        internal static string Normalize(string name)
        {
            string n = Regex.Replace(name.ToLowerInvariant(), @"^\d+\s+", "");
            return Regex.Replace(n, @"_\d+$", "").Replace(" ", "");
        }
        static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
        internal static float Envelope(float time, float start, float peak, float hold, float end)
        {
            if (time <= start || time >= end) return 0f;
            return time < peak ? Smooth((time - start) / Mathf.Max(.001f, peak - start))
                : time <= hold ? 1f : 1f - Smooth((time - hold) / Mathf.Max(.001f, end - hold));
        }
    }
}
