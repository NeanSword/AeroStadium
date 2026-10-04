using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AeroStadium.Presentation
{
    public enum NativePokemonClipRole { Other, Idle, Physical, Special, Damage, Faint, Showcase, Walk, Run, Secondary }

    [Serializable]
    public sealed class NativePokemonClip
    {
        public AnimationClip clip;
        public NativePokemonClipRole role;
        public bool sourceLoop;
        public bool hasRootMotion;
        public string[] rootMotionPaths;
    }

    /// <summary>A native skeleton owns its poses. Normalization is outside the animated hierarchy.</summary>
    [DefaultExecutionOrder(80)]
    [DisallowMultipleComponent]
    public sealed class NativePokemonModel : MonoBehaviour
    {
        [SerializeField] int species;
        [SerializeField] float targetHeight;
        [SerializeField] Bounds referenceBounds;
        [SerializeField] Transform modelRoot;
        [SerializeField] Animator animator;
        [SerializeField] NativePokemonClip[] clips;
        bool allowLocomotion;
        [SerializeField] Transform[] motionRoots = Array.Empty<Transform>();
        [SerializeField] Vector3[] rootPositions = Array.Empty<Vector3>();
        [SerializeField] string[] motionRootPaths = Array.Empty<string>();
        int[] locomotionStateHashes = Array.Empty<int>();
        int[][] locomotionRootIndices = Array.Empty<int[]>();

        public int Species => species;
        public float ModelHeight => targetHeight;
        public float WorldModelHeight => targetHeight * transform.TransformVector(Vector3.up).magnitude;
        public Bounds RestBounds => referenceBounds;
        public Transform ModelRoot => modelRoot;
        public int ClipCount => clips == null ? 0 : clips.Length;
        public bool IsPlayingNative => animator != null && animator.enabled && animator.runtimeAnimatorController != null;

        public void Configure(int id, float height, Bounds bounds, Transform root, Animator target, NativePokemonClip[] sourceClips)
        {
            species = id; targetHeight = height; referenceBounds = bounds;
            modelRoot = root; animator = target; clips = sourceClips;
            ConfigureAnimator();
            PrepareMotionRoots(true);
        }

        void Awake() { ConfigureAnimator(); PrepareMotionRoots(false); }
        void ConfigureAnimator()
        {
            if (animator == null && modelRoot != null) animator = modelRoot.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skin.updateWhenOffscreen = true;
                skin.forceMatrixRecalculationPerRender = true;
            }
        }

        public float Play(PokemonMotionAction action)
        {
            var role = RoleFor(action);
            int index = FindActionClip(role);
            if (index < 0) return 0f;
            string stateName = role == NativePokemonClipRole.Faint && clips[index].role == NativePokemonClipRole.Damage
                ? "Base Layer.Native_FaintFallback" : $"Base Layer.Native_{index:000}";
            return PlayIndex(index, stateName) ? GetActionDuration(action) : 0f;
        }

        public float GetActionDuration(PokemonMotionAction action)
        {
            var role = RoleFor(action);
            int index = FindActionClip(role);
            return index < 0 ? 0f : clips[index].clip.length + (role == NativePokemonClipRole.Idle ? 0f : .12f);
        }

        static NativePokemonClipRole RoleFor(PokemonMotionAction action) => action == PokemonMotionAction.Physical ? NativePokemonClipRole.Physical
                : action == PokemonMotionAction.Special ? NativePokemonClipRole.Special
                : action == PokemonMotionAction.Damage ? NativePokemonClipRole.Damage
                : action == PokemonMotionAction.Faint ? NativePokemonClipRole.Faint
                : action == PokemonMotionAction.Showcase ? NativePokemonClipRole.Showcase
                : NativePokemonClipRole.Idle;

        int FindActionClip(NativePokemonClipRole role)
        {
            int index = FindRole(role);
            if (index < 0 && role == NativePokemonClipRole.Faint) index = FindRole(NativePokemonClipRole.Damage);
            if (index < 0 && (role == NativePokemonClipRole.Physical || role == NativePokemonClipRole.Special))
                index = FindRole(role == NativePokemonClipRole.Physical ? NativePokemonClipRole.Special : NativePokemonClipRole.Physical);
            if (index < 0 && role == NativePokemonClipRole.Showcase) index = FindRole(NativePokemonClipRole.Idle);
            return index;
        }

        public bool PlaySource(int index)
        {
            return PlayIndex(index, $"Base Layer.Native_{index:000}");
        }

        bool PlayIndex(int index, string stateName)
        {
            if (clips == null || index < 0 || index >= clips.Length || animator == null || animator.runtimeAnimatorController == null) return false;
            int state = Animator.StringToHash(stateName);
            if (!animator.HasState(0, state)) return false;
            animator.enabled = true;
            animator.CrossFadeInFixedTime(state, .12f, 0, 0f);
            return true;
        }

        public void AllowLocomotion(bool value)
        {
            allowLocomotion = value;
            // Translation curves stay on the native origin bone. The arena actor is moved only by arena logic.
            if (animator != null) animator.applyRootMotion = value;
        }

        int FindRole(NativePokemonClipRole role)
        {
            if (clips == null) return -1;
            for (int i = 0; i < clips.Length; i++) if (clips[i].role == role && clips[i].clip != null) return i;
            return -1;
        }

        void PrepareMotionRoots(bool captureReferencePose)
        {
            if (clips == null || modelRoot == null) return;
            var indices = new Dictionary<string, int>(StringComparer.Ordinal);
            var paths = new List<string>();
            var roots = new List<Transform>();
            locomotionStateHashes = new int[clips.Length];
            locomotionRootIndices = new int[clips.Length][];
            for (int i = 0; i < clips.Length; i++)
            {
                NativePokemonClip clip = clips[i];
                if (!clip.hasRootMotion || (clip.role != NativePokemonClipRole.Run && clip.role != NativePokemonClipRole.Walk)) continue;
                locomotionStateHashes[i] = Animator.StringToHash($"Base Layer.Native_{i:000}");
                var used = new List<int>();
                foreach (string path in clip.rootMotionPaths ?? Array.Empty<string>())
                {
                    string key = path ?? string.Empty;
                    if (!indices.TryGetValue(key, out int index))
                    {
                        Transform root = key.Length == 0 ? modelRoot : modelRoot.Find(key);
                        if (root == null) throw new InvalidDataException("Racine de locomotion native absente : " + key);
                        index = paths.Count; indices.Add(key, index); paths.Add(key); roots.Add(root);
                    }
                    if (!used.Contains(index)) used.Add(index);
                }
                locomotionRootIndices[i] = used.ToArray();
            }
            bool validReference = motionRootPaths != null && motionRoots != null && rootPositions != null &&
                motionRootPaths.Length == paths.Count && motionRoots.Length == paths.Count && rootPositions.Length == paths.Count;
            for (int i = 0; validReference && i < paths.Count; i++)
                validReference = motionRootPaths[i] == paths[i] && motionRoots[i] == roots[i];
            if (!captureReferencePose && validReference) return;
            motionRootPaths = paths.ToArray(); motionRoots = roots.ToArray(); rootPositions = new Vector3[roots.Count];
            for (int i = 0; i < motionRoots.Length; i++) rootPositions[i] = motionRoots[i].localPosition;
        }

        void LateUpdate()
        {
            if (allowLocomotion || animator == null || !animator.enabled || animator.runtimeAnimatorController == null) return;
            // Incoming states own the presentation policy during a crossfade. Automatic returns to Idle release it.
            int active = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0).fullPathHash
                : animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
            for (int state = 0; state < locomotionStateHashes.Length; state++)
            {
                if (locomotionStateHashes[state] == 0 || locomotionStateHashes[state] != active) continue;
                // Anchors are serialized from the fixed import reference pose, never from a preceding attack.
                foreach (int i in locomotionRootIndices[state])
                {
                    if (motionRoots[i] == null) continue;
                    Vector3 position = motionRoots[i].localPosition;
                    position.x = rootPositions[i].x; position.z = rootPositions[i].z;
                    motionRoots[i].localPosition = position;
                }
                return;
            }
        }
    }
}
