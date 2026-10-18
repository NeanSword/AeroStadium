using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [Serializable]
    public sealed class DodgeFacialTelemetry
    {
        public string profile = "unverified-native-face", phase = "inactive";
        public int species, channels, posedFrames, expressionChanges;
        public int alertFrames, focusFrames, blinkFrames, reliefFrames, returnFrames;
        public float maxUvDelta, maxJawDegrees;
        public float maximumRestorePositionError, maximumRestoreAngleError;
        public bool verifiedAtlas, finite = true, restored = true, nativeAnimatorPreserved = true;
    }

    /// <summary>
    /// Temporary expression controls, applied after the Animator and before NativeMaterialBridge (order 100).
    /// Only the three inspected BDSP eye atlases are supported. No materials or textures are modified.
    /// </summary>
    internal sealed class DodgeFacialPose : IDisposable
    {
        sealed class Channel
        {
            public Transform bone;
            public Vector3 position;
            public Quaternion rotation;
            public bool jaw;
        }

        readonly Channel[] channels = Array.Empty<Channel>();
        readonly NativeMaterialBridge[] bridges = Array.Empty<NativeMaterialBridge>();
        readonly Animator animator;
        readonly RuntimeAnimatorController controller;
        readonly bool animatorEnabled;
        readonly AnimationCurve jawBreath;
        bool applied, disposed;
        int lastExpression = -1;
        public DodgeFacialTelemetry Stats { get; } = new DodgeFacialTelemetry();
        public bool Verified => Stats.verifiedAtlas;

        // These are native control-node positions, not guessed texture cells.
        // eye01 enumerates 0/.005 in U and 0/.0025/.005/.0075 in V.
        // The atlas uses Mirror U: .005 becomes +1 transformed U and selects the other column.
        static readonly Vector3 Open = Vector3.zero;
        static readonly Vector3 Half = new Vector3(0f, .0025f, 0f);
        static readonly Vector3 Closed = new Vector3(0f, .005f, 0f);
        static readonly Vector3 Tight = new Vector3(0f, .0075f, 0f);
        static readonly Vector3 Focus = new Vector3(.005f, 0f, 0f);
        static readonly Vector3 Relief = new Vector3(.005f, .0025f, 0f);

        public DodgeFacialPose(Transform actor)
        {
            var model = actor == null ? null : actor.GetComponent<NativePokemonModel>();
            Stats.species = model == null ? 0 : model.Species;
            if (model == null || model.ModelRoot == null) return;
            int species = model.Species;
            if (species != 1 && species != 9 && species != 59) return;
            var names = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var bone in model.ModelRoot.GetComponentsInChildren<Transform>(true))
                if (!names.ContainsKey(bone.name)) names.Add(bone.name, bone);
            Transform Find(string name) => names.TryGetValue(name, out var bone) ? bone : null;
            Transform left = Find("CusAnimMat_LEye_UV_ColorUVTranslate");
            Transform right = Find("CusAnimMat_REye_UV_ColorUVTranslate");
            bridges = model.ModelRoot.GetComponentsInChildren<NativeMaterialBridge>(true);
            if (left == null || right == null || bridges.Length == 0) return;
            var selected = new List<Channel>
            {
                new Channel { bone = left }, new Channel { bone = right }
            };
            // Arcanin has an additional material for eye-adjacent geometry driven by the same native atlas.
            Transform extra = species == 59 ? Find("CusAnimMat_Eye_UV_ColorUVTranslate") : null;
            if (extra != null) selected.Add(new Channel { bone = extra });
            Transform jaw = Find("Jaw");
            if (jaw != null) selected.Add(new Channel { bone = jaw, jaw = true });
            channels = selected.ToArray();
            animator = model.ModelRoot.GetComponent<Animator>();
            controller = animator == null ? null : animator.runtimeAnimatorController;
            animatorEnabled = animator != null && animator.enabled;
            // Native happy/hate quaternion deltas confirm local Y is the hinge on these rigs.
            float inhale = species == 9 ? 2.8f : species == 59 ? 2.2f : 1.4f;
            jawBreath = new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(.045f, inhale), new Keyframe(.13f, .35f),
                new Keyframe(.35f, 0f), new Keyframe(.43f, .25f), new Keyframe(.57f, .15f),
                new Keyframe(.635f, inhale * 1.35f), new Keyframe(.72f, .2f),
                new Keyframe(.81f, inhale * .55f), new Keyframe(.92f, .15f), new Keyframe(1f, 0f));
            Stats.profile = species == 9 ? "Tortank-native-eye01-hate-happyB"
                : species == 1 ? "Bulbizarre-native-eye01-hate-happyB" : "Arcanin-native-eye01-hate-happyB";
            Stats.channels = channels.Length;
            Stats.verifiedAtlas = true;
        }

        /// <summary>Call in Update before the Animator, and on cancellation before removing the pose.</summary>
        public void Restore()
        {
            if (!applied) return;
            foreach (var channel in channels)
            {
                if (channel.bone == null) continue;
                if (channel.jaw) channel.bone.localRotation = channel.rotation;
                else channel.bone.localPosition = channel.position;
                Stats.maximumRestorePositionError = Mathf.Max(Stats.maximumRestorePositionError,
                    channel.jaw ? 0f : Vector3.Distance(channel.position, channel.bone.localPosition));
                Stats.maximumRestoreAngleError = Mathf.Max(Stats.maximumRestoreAngleError,
                    channel.jaw ? Quaternion.Angle(channel.rotation, channel.bone.localRotation) : 0f);
            }
            applied = false;
            Stats.restored = true;
        }

        public void Apply(float progress)
        {
            Restore();
            if (disposed || !Verified) return;
            if (!Finite(progress)) { Stats.finite = false; Dispose(); return; }
            float p = Mathf.Clamp01(progress);
            if (p <= 0f || p >= 1f) return;
            Vector3 eye = Expression(p, out int expression, out string phase);
            // The final ten percent deliberately releases the face to native animation.
            if (p >= .97f) return;
            foreach (var channel in channels)
            {
                if (channel.bone == null) { Stats.finite = false; Dispose(); return; }
                channel.position = channel.bone.localPosition;
                channel.rotation = channel.bone.localRotation;
            }
            applied = true; Stats.restored = false;
            float degrees = Mathf.Max(0f, jawBreath.Evaluate(p));
            foreach (var channel in channels)
            {
                if (channel.jaw)
                {
                    channel.bone.localRotation = channel.rotation * Quaternion.AngleAxis(degrees, Vector3.up);
                    Stats.maxJawDegrees = Mathf.Max(Stats.maxJawDegrees,
                        Quaternion.Angle(channel.rotation, channel.bone.localRotation));
                }
                else
                {
                    // Keep unrelated Z untouched. The bridge reads X/Y and quantizes native atlas steps.
                    channel.bone.localPosition = new Vector3(eye.x, eye.y, channel.position.z);
                    Stats.maxUvDelta = Mathf.Max(Stats.maxUvDelta,
                        Vector3.Distance(channel.position, channel.bone.localPosition));
                }
                if (!Finite(channel.bone.localPosition) || !Finite(channel.bone.localRotation)) Stats.finite = false;
            }
            Stats.phase = phase; Stats.posedFrames++;
            if (expression != lastExpression) { Stats.expressionChanges++; lastExpression = expression; }
            if (p < .075f) Stats.alertFrames++;
            else if (p >= .36f && p < .43f) Stats.blinkFrames++;
            else if (p >= .60f && p < .68f) Stats.reliefFrames++;
            else if (p >= .68f) Stats.returnFrames++;
            else Stats.focusFrames++;
            Stats.nativeAnimatorPreserved &= animator == null || animator.runtimeAnimatorController == controller
                && animator.enabled == animatorEnabled;
            if (!Stats.finite) Dispose();
        }

        static Vector3 Expression(float p, out int expression, out string phase)
        {
            if (p < .075f) { phase = "alert"; expression = 0; return Open; }
            if (p < .36f) { phase = "focused-push"; expression = 1; return Focus; }
            if (p < .375f) { phase = "protective-blink-closing"; expression = 2; return Half; }
            if (p < .395f) { phase = "protective-blink-closed"; expression = 3; return Closed; }
            if (p < .412f) { phase = "protective-blink-tight"; expression = 4; return Tight; }
            if (p < .43f) { phase = "protective-blink-opening"; expression = 2; return Half; }
            if (p < .60f) { phase = "tracking-past"; expression = 1; return Focus; }
            if (p < .62f) { phase = "relief-blink-closing"; expression = 2; return Half; }
            if (p < .655f) { phase = "relief-blink"; expression = 5; return Relief; }
            if (p < .68f) { phase = "relief-blink-opening"; expression = 2; return Half; }
            if (p < .90f) { phase = "focused-return"; expression = 1; return Focus; }
            if (p < .925f) { phase = "settling-blink"; expression = 3; return Closed; }
            if (p < .945f) { phase = "settling-blink-opening"; expression = 2; return Half; }
            phase = "native-release"; expression = 0; return Open;
        }

        public void Dispose()
        {
            Restore(); disposed = true; Stats.phase = "inactive";
            // Remove the last material-property-block eye pose on the same frame as cancellation/end.
            foreach (var bridge in bridges) if (bridge != null) bridge.Apply();
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static bool Finite(Quaternion value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
    }
}
