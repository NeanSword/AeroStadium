using System;
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
        int playing = -1;
        bool allowLocomotion;
        Transform[] motionRoots = Array.Empty<Transform>();
        Vector3[] rootPositions = Array.Empty<Vector3>();

        public int Species => species;
        public float ModelHeight => targetHeight;
        public Bounds RestBounds => referenceBounds;
        public Transform ModelRoot => modelRoot;
        public int ClipCount => clips == null ? 0 : clips.Length;
        public bool IsPlayingNative => animator != null && animator.enabled && animator.runtimeAnimatorController != null;

        public void Configure(int id, float height, Bounds bounds, Transform root, Animator target, NativePokemonClip[] sourceClips)
        {
            species = id; targetHeight = height; referenceBounds = bounds;
            modelRoot = root; animator = target; clips = sourceClips;
            ConfigureAnimator();
        }

        void Awake() { ConfigureAnimator(); }
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
            playing = index;
            BindMotionRoots(clips[index]);
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

        void BindMotionRoots(NativePokemonClip clip)
        {
            var paths = clip.rootMotionPaths ?? Array.Empty<string>();
            motionRoots = new Transform[paths.Length]; rootPositions = new Vector3[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                motionRoots[i] = string.IsNullOrEmpty(paths[i]) ? modelRoot : modelRoot.Find(paths[i]);
                if (motionRoots[i] != null) rootPositions[i] = motionRoots[i].localPosition;
            }
        }

        void LateUpdate()
        {
            if (playing < 0 || clips == null || playing >= clips.Length || allowLocomotion || !clips[playing].hasRootMotion) return;
            // Prevent a run/walk curve from drifting outside its battle position. Vertical animation remains native.
            if (clips[playing].role != NativePokemonClipRole.Run && clips[playing].role != NativePokemonClipRole.Walk) return;
            for (int i = 0; i < motionRoots.Length; i++)
            {
                if (motionRoots[i] == null) continue;
                Vector3 position = motionRoots[i].localPosition;
                position.x = rootPositions[i].x; position.z = rootPositions[i].z;
                motionRoots[i].localPosition = position;
            }
        }
    }
}
