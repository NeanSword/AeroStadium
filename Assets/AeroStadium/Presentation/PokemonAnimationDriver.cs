using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Routes native model clips when available, otherwise keeps the authored local fallback.</summary>
    public sealed class PokemonAnimationDriver : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] float attackDuration, damageDuration, faintDuration;
        CinematicMotion original;
        [SerializeField] NativePokemonModel native;

        public void Configure(Animator target, float attackSeconds, float damageSeconds, float faintSeconds)
        {
            animator = target; attackDuration = attackSeconds; damageDuration = damageSeconds; faintDuration = faintSeconds;
        }
        public void ConfigureAuthored(CinematicMotion motion) { if (native == null) original = motion; }
        public void ConfigureNative(NativePokemonModel model) { native = model; original = null; }
        public float PlayIdle() => native != null ? native.Play(PokemonMotionAction.Idle) : original != null ? original.Play(PokemonMotionAction.Idle) : 0f;
        void Awake() { if (native == null) native = GetComponent<NativePokemonModel>(); if (animator == null) animator = GetComponentInChildren<Animator>(true); }
        void Start() { if (original != null) original.Play(PokemonMotionAction.Idle); }
        public float PlayAttack(bool special = false) => native != null
            ? native.Play(special ? PokemonMotionAction.Special : PokemonMotionAction.Physical) : original != null
            ? original.Play(special ? PokemonMotionAction.Special : PokemonMotionAction.Physical)
            : PlaySourceState("Base Layer.Attack") ? attackDuration : 0f;
        public float PlayDamage() => native != null ? native.Play(PokemonMotionAction.Damage) : original != null ? original.Play(PokemonMotionAction.Damage)
            : PlaySourceState("Base Layer.Damage") ? damageDuration : 0f;
        public float PlayFaint() => native != null ? native.Play(PokemonMotionAction.Faint) : original != null ? original.Play(PokemonMotionAction.Faint)
            : PlaySourceState("Base Layer.Faint") ? faintDuration : 0f;
        public bool PlaySourceAnimation(int clipIndex)
        {
            if (native != null) return native.PlaySource(clipIndex);
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            string state = $"Base Layer.Source_{clipIndex:000}";
            if (animator == null || animator.runtimeAnimatorController == null || !animator.HasState(0, Animator.StringToHash(state))) return false;
            if (original != null) original.EnableSourceMotion();
            return PlaySourceState(state);
        }
        bool PlaySourceState(string path)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            int hash = Animator.StringToHash(path);
            if (!animator.HasState(0, hash)) return false;
            animator.CrossFadeInFixedTime(hash, .12f, 0); return true;
        }
    }
}
