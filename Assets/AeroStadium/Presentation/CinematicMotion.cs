using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Authored preview and idle motion for the imported, skinned Pokémon rigs.
    /// The source model archives contain rigs but generally no usable action clips.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class CinematicMotion : MonoBehaviour
    {
        enum JointRole { Head, Neck, Torso, Tail, Wing, Arm, Leg, Ear, Jaw, Ornament }

        sealed class Joint
        {
            public Transform transform;
            public Quaternion rest;
            public JointRole role;
            public float phase;
            public float side;
            public float segment;
        }

        readonly List<Joint> joints = new List<Joint>();
        int species;
        bool airborne;
        float bornAt;
        float showcaseStartedAt = -100f;

        void Awake()
        {
            bornAt = Time.time;
            var seen = new HashSet<Transform>();
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                foreach (var bone in renderer.bones)
                {
                    if (bone == null || !seen.Add(bone)) continue;
                    if (!TryClassify(bone.name, out var role)) continue;
                    string lower = bone.name.ToLowerInvariant();
                    joints.Add(new Joint
                    {
                        transform = bone,
                        rest = bone.localRotation,
                        role = role,
                        phase = StablePhase(lower),
                        side = lower.Contains("left") ? -1f : lower.Contains("right") ? 1f : 0f,
                        segment = SegmentNumber(lower)
                    });
                }
            }
        }

        public void Configure(int speciesId)
        {
            species = speciesId;
            airborne = species == 6 || species == 250 || species == 384 || species == 635 || species == 823;
            int wingCount = 0;
            foreach (var joint in joints) if (joint.role == JointRole.Wing) wingCount++;
            Debug.Log("[animation-rig] species=" + species + " skinnedBones=" + GetSkinnedBoneCount() + " articulated=" + joints.Count + " wingJoints=" + wingCount);
        }

        public void BeginSpotlightAction()
        {
            showcaseStartedAt = Time.time;
        }

        void LateUpdate()
        {
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController != null) return;

            float time = Time.time - bornAt;
            float breath = Mathf.Sin(time * 2.15f);
            float secondBreath = Mathf.Sin(time * 1.08f + .7f);
            float look = Mathf.Sin(time * .62f);
            float actionAge = Time.time - showcaseStartedAt;
            float heroBeat = actionAge < 0f || actionAge > 1.4f
                ? 0f
                : Mathf.Exp(-Mathf.Pow((actionAge - .48f) / .19f, 2f));
            float call = actionAge < .24f || actionAge > .9f
                ? 0f
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.24f, .42f, actionAge))
                    * (1f - Mathf.SmoothStep(.62f, .9f, actionAge));

            foreach (var joint in joints)
            {
                float wave = Mathf.Sin(time * 1.9f + joint.phase);
                float delayedWave = Mathf.Sin(time * 1.35f + joint.phase + .9f);
                Vector3 motion;
                switch (joint.role)
                {
                    case JointRole.Head:
                        motion = new Vector3(-breath * 5.5f - heroBeat * 13f,
                            look * 10f + heroBeat * (species == 250 ? 11f : 7f),
                            wave * 3.2f);
                        break;
                    case JointRole.Neck:
                        motion = new Vector3(-breath * 3.5f - heroBeat * 7f,
                            look * 3.5f, wave * 1.8f);
                        break;
                    case JointRole.Torso:
                        motion = new Vector3(breath * 3.2f + heroBeat * 9f,
                            0f, secondBreath * 2.5f - heroBeat * 3f);
                        break;
                    case JointRole.Tail:
                        motion = new Vector3(wave * 5f,
                            delayedWave * 13f + heroBeat * 8f,
                            Mathf.Sin(time * 1.55f + joint.phase) * 12f);
                        break;
                    case JointRole.Wing:
                        float flapRate = airborne ? 7.4f : 2.25f;
                        float flapSize = airborne ? 34f : 11f;
                        float flap = Mathf.Sin(time * flapRate + joint.phase * .12f) * flapSize;
                        float segmentFade = joint.segment > 0 ? Mathf.Clamp(1.15f - (joint.segment - 1f) * .13f, .58f, 1.15f) : 1f;
                        motion = new Vector3(flap * .28f + heroBeat * (airborne ? 13f : 5f),
                            joint.side * 3f,
                            flap * segmentFade + heroBeat * (airborne ? 19f : 8f));
                        break;
                    case JointRole.Arm:
                        motion = new Vector3(wave * 9f - heroBeat * (joint.side < 0f ? 23f : 13f),
                            joint.side * (4f + heroBeat * 7f),
                            wave * (joint.side == 0f ? 7f : joint.side * 12f) + heroBeat * (joint.side < 0f ? -19f : 19f));
                        break;
                    case JointRole.Leg:
                        motion = new Vector3(Mathf.Sin(time * 2.1f + joint.phase) * 5f + heroBeat * 15f,
                            0f,
                            joint.side * (Mathf.Sin(time * 1.3f + joint.phase) * 4f + heroBeat * 7f));
                        break;
                    case JointRole.Ear:
                        motion = new Vector3(wave * 3f, 0f, wave * 16f + heroBeat * 7f);
                        break;
                    case JointRole.Jaw:
                        motion = new Vector3(-call * 28f + wave * 1.5f, 0f, 0f);
                        break;
                    default:
                        float clothWave = Mathf.Sin(time * 2.8f + joint.phase);
                        motion = new Vector3(clothWave * 8f, delayedWave * 8f, clothWave * 14f + heroBeat * 8f);
                        break;
                }
                joint.transform.localRotation = joint.rest * Quaternion.Euler(motion);
            }
        }

        int GetSkinnedBoneCount()
        {
            int count = 0;
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.bones != null) count += renderer.bones.Length;
            return count;
        }

        static float StablePhase(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 16777619;
                return (hash % 6283) * .001f;
            }
        }

        static float SegmentNumber(string value)
        {
            int end = value.Length - 1;
            while (end >= 0 && !char.IsDigit(value[end])) end--;
            if (end < 0) return 0f;
            int start = end;
            while (start >= 0 && char.IsDigit(value[start])) start--;
            return float.TryParse(value.Substring(start + 1, end - start), out float segment) ? segment : 0f;
        }

        static bool TryClassify(string name, out JointRole role)
        {
            string value = name.ToLowerInvariant();
            if (value.Contains("eyelid") || value.Contains("eyeball") || value.Contains("pupil")
                || value.Contains("finger") || value.Contains("toe") || value.Contains("claw"))
            { role = default; return false; }
            if (value.Contains("wing") || value.Contains("flipper") || value.Contains("fin"))
            { role = JointRole.Wing; return true; }
            if (value.Contains("tail")) { role = JointRole.Tail; return true; }
            if (value.Contains("jaw") || value.Contains("mouth")) { role = JointRole.Jaw; return true; }
            if (value.Contains("head")) { role = JointRole.Head; return true; }
            if (value.Contains("neck")) { role = JointRole.Neck; return true; }
            if (value.Contains("ear") || value.Contains("feeler")) { role = JointRole.Ear; return true; }
            if (value.Contains("spine") || value.Contains("waist") || value.Contains("body")
                || value.Contains("chest") || value.Contains("torso"))
            { role = JointRole.Torso; return true; }
            if (value.Contains("arm") || value.Contains("hand") || value.Contains("shoulder"))
            { role = JointRole.Arm; return true; }
            if (value.Contains("leg") || value.Contains("foot") || value.Contains("hip"))
            { role = JointRole.Leg; return true; }
            if (value.Contains("leaf") || value.Contains("hair") || value.Contains("feather")
                || value.Contains("frill") || value.Contains("cloth") || value.Contains("vine"))
            { role = JointRole.Ornament; return true; }
            role = default;
            return false;
        }
    }
}
