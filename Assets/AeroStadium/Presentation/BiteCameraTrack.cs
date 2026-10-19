using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [System.Serializable]
    public sealed class BiteCameraTelemetry
    {
        public bool planned, entered, recovered, interrupted, fallback, recoveryBlocked, safe = true;
        public int followFrames, axisFrames, actorVisibleFrames, targetVisibleFrames, strikeFrames, strikeVisibleFrames;
        public float maxSpeed, maxAngularSpeed, maxFovSpeed, travel, entrySeconds, recoverySeconds;
        public float contactSubjectHeightSum, strikeHeightSum, targetHeightSum;
        public bool animatedBoundsValid = true, actualHeadTracked;
        public int sourceBakedFrames, targetBakedFrames, boundsFallbackFrames, sourceBoundsFrame = -1, targetBoundsFrame = -1;
        public string boundsFailure;
        public List<BiteCameraFramingMiss> strikeFramingMisses = new List<BiteCameraFramingMiss>(12);
    }

    [System.Serializable]
    public sealed class BiteCameraFramingMiss
    {
        public float progress, dt, desiredFov, cameraFov;
        public bool sourceVisible, targetVisible, headVisible, moveAccepted;
        public Vector3 sourceCenter, sourceSize, targetCenter, targetSize, head;
        public Vector3 previousPosition, desiredPosition, cameraPosition;
        public Quaternion desiredRotation, cameraRotation;
        public float requestedSpeed, requestedAngularSpeed, cappedSpeed, cappedAngularSpeed;
        // Bounds projected to viewport coordinates: left, bottom, right, top.
        public Vector4 sourceViewport;
    }

    /// <summary>One owned, cached snapshot of the actual animated body geometry per rendered frame.</summary>
    internal sealed class BiteFrame : System.IDisposable
    {
        sealed class Part
        {
            public Renderer renderer;
            public SkinnedMeshRenderer skin;
            public Mesh source, baked;
        }
        readonly List<Part> parts = new List<Part>();
        Bounds cached;
        bool valid, disposed;
        public int LastFrame { get; private set; } = -1;
        public int BakedFrames { get; private set; }
        public string LastFailure { get; private set; }

        public BiteFrame(Transform owner)
        {
            if (owner == null) { LastFailure = "Actor model is missing."; return; }
            NativePokemonModel native = owner.GetComponent<NativePokemonModel>();
            Transform model = native != null && native.ModelRoot != null ? native.ModelRoot : owner;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                MeshFilter filter = renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>() : null;
                Mesh mesh = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
                if (mesh == null || mesh.vertexCount == 0 || renderer.sharedMaterial == null) continue;
                Part part = new Part { renderer = renderer, skin = skin, source = mesh };
                if (skin != null)
                {
                    part.baked = new Mesh { name = "Bite camera snapshot · " + mesh.name };
                    part.baked.MarkDynamic();
                }
                parts.Add(part);
            }
            if (parts.Count == 0) LastFailure = "Actor has no body mesh renderers.";
        }
        static bool Visible(Renderer renderer) => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
            && !renderer.forceRenderingOff && renderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        public bool TryBounds(out Bounds bounds)
        {
            bounds = cached;
            if (disposed) return false;
            if (LastFrame == Time.frameCount) return valid;
            LastFrame = Time.frameCount; valid = false;
            bool found = false, baked = false, failed = false;
            Bounds measured = default;
            foreach (Part part in parts)
            {
                if (!Visible(part.renderer)) continue;
                try
                {
                    Bounds world;
                    if (part.skin != null)
                    {
                        if (part.skin.sharedMesh != part.source) throw new System.InvalidOperationException("Body mesh changed.");
                        // Omit renderer scale in the bake; localToWorld below applies it exactly once.
                        part.skin.BakeMesh(part.baked, false); part.baked.RecalculateBounds(); baked = true;
                        if (part.baked.vertexCount == 0) throw new System.InvalidOperationException("Empty animated body snapshot.");
                        Bounds local = part.baked.bounds;
                        Matrix4x4 matrix = part.renderer.localToWorldMatrix;
                        world = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
                        for (int i = 0; i < 8; i++)
                        {
                            Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                            if (!Finite(point)) throw new System.InvalidOperationException("Nonfinite animated body bounds.");
                            world.Encapsulate(point);
                        }
                    }
                    else world = part.renderer.bounds;
                    if (!Finite(world.center) || !Finite(world.size)) throw new System.InvalidOperationException("Nonfinite body bounds.");
                    if (!found) { measured = world; found = true; } else measured.Encapsulate(world);
                }
                catch (System.Exception error) { failed = true; LastFailure = error.GetType().Name + ": " + error.Message; }
            }
            if (baked) BakedFrames++;
            valid = found && !failed;
            if (valid) { cached = bounds = measured; return true; }
            if (!failed) LastFailure = "No visible body geometry was measured.";
            return false;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Part part in parts) if (part.baked != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(part.baked);
                else UnityEngine.Object.DestroyImmediate(part.baked);
            }
            parts.Clear();
        }
    }

    /// <summary>A continuous, low three-quarter follow of the physical run, jaw contact and return.</summary>
    internal sealed class BiteCameraTrack
    {
        struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
        }

        const float EntrySeconds = 1f, RecoverySeconds = 1.2f;
        readonly ArenaView arena;
        readonly Camera camera;
        readonly Transform actor;
        readonly Transform headBone;
        readonly BiteFrame sourceFrame, targetFrame;
        readonly int side;
        readonly Pose home;
        readonly Bounds sourceHome, targetHome;
        readonly Vector3 backward, forward, rootHome, headLocal, plannedTravel, dodgeTravel;
        readonly Quaternion rootRotation;
        readonly float dodgeLift;
        readonly Pose[] history = new Pose[384];
        readonly Collider[] nearby = new Collider[32];
        readonly RaycastHit[] hits = new RaycastHit[32];
        int historyCount, lastBoundsFallbackFrame = -1;
        float orbitScale = 1, elevation = .13f;
        bool canceled, transition;
        public bool Transitioning => transition;
        public bool FinishedRecovery { get; private set; }
        public BiteCameraTelemetry Stats { get; } = new BiteCameraTelemetry();

        public BiteCameraTrack(ArenaView view, int attackerSide, Transform source, Vector3 dodgeOffset)
        {
            arena = view; camera = view.ArenaCamera; actor = source; side = attackerSide;
            home = Current();
            sourceFrame = new BiteFrame(actor);
            NativePokemonModel defender = arena.DodgeModel(1 - side);
            targetFrame = new BiteFrame(defender != null ? defender.transform : null);
            LiveBounds(out Bounds initialSource, out Bounds initialTarget);
            sourceHome = initialSource; targetHome = initialTarget;
            rootHome = actor.position; rootRotation = actor.rotation;
            Vector3 axis = Vector3.ProjectOnPlane(arena.BattleAxis, Vector3.up).normalized;
            if (axis.sqrMagnitude < .5f) axis = Vector3.right;
            backward = Vector3.Cross(Vector3.up, axis).normalized;
            forward = Vector3.ProjectOnPlane(targetHome.center - sourceHome.center, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = side == 0 ? axis : -axis;
            Vector3 head = sourceHome.center + forward * PlanarRadius(sourceHome, forward) * .67f + Vector3.up * sourceHome.size.y * .16f;
            Vector3 mouth = head;
            // Keep the actual animated head for live framing; root-local coordinates are only a planning estimate.
            foreach (Transform bone in actor.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "Head") { head = bone.position; headBone = bone; }
                if (bone.name == "EffMouth01") mouth = bone.position;
            }
            headLocal = actor.InverseTransformPoint(head);
            Stats.actualHeadTracked = headBone != null;
            float lane = Mathf.Max(0, Vector3.Dot(targetHome.center - mouth, forward)
                - PlanarRadius(targetHome, forward) - .10f);
            plannedTravel = forward * lane;
            dodgeTravel = Vector3.ProjectOnPlane(dodgeOffset, Vector3.up); dodgeLift = Mathf.Abs(dodgeOffset.y);
            Remember(home);
            // Keep every candidate at battle height; geometry can reduce the orbit, never turn it into an aerial cut.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                orbitScale = attempt < 3 ? 1f : .6f;
                elevation = .13f + (attempt % 3) * .04f;
                Pose first = PlannedPose(0), last = PlannedPose(1);
                if (!MotionWithinLimits(home, first, EntrySeconds / 1.875f)
                    || !MotionWithinLimits(last, home, RecoverySeconds / 1.875f)) continue;
                if (PathClear(home, first) && ActionClear() && PathClear(last, home))
                { Stats.planned = true; Stats.fallback = attempt > 0; return; }
            }
            Stats.fallback = true;
            ReleaseFrames();
        }

        Vector3 LiveHead => headBone != null ? headBone.position : actor.TransformPoint(headLocal);
        void LiveBounds(out Bounds source, out Bounds victim)
        {
            bool sourceMeasured = sourceFrame.TryBounds(out source), targetMeasured = targetFrame.TryBounds(out victim);
            Stats.sourceBakedFrames = sourceFrame.BakedFrames; Stats.targetBakedFrames = targetFrame.BakedFrames;
            Stats.sourceBoundsFrame = sourceFrame.LastFrame; Stats.targetBoundsFrame = targetFrame.LastFrame;
            if (sourceMeasured && targetMeasured) return;
            Stats.animatedBoundsValid = false;
            if (lastBoundsFallbackFrame != Time.frameCount) { Stats.boundsFallbackFrames++; lastBoundsFallbackFrame = Time.frameCount; }
            if (!sourceMeasured) { source = arena.PokemonWorldBounds(side); Stats.boundsFailure = "Source: " + sourceFrame.LastFailure; }
            if (!targetMeasured) { victim = arena.PokemonWorldBounds(1 - side); Stats.boundsFailure = "Target: " + targetFrame.LastFailure; }
        }
        void ReleaseFrames() { sourceFrame.Dispose(); targetFrame.Dispose(); }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }
        static float Smooth(float a, float b, float t) { t = Mathf.InverseLerp(a, b, t); return t * t * (3 - 2 * t); }
        static Pose Blend(Pose a, Pose b, float t) => new Pose {
            position = Vector3.Lerp(a.position, b.position, t), rotation = Quaternion.Slerp(a.rotation, b.rotation, t), fov = Mathf.Lerp(a.fov, b.fov, t)
        };
        static float PlanarRadius(Bounds frame, Vector3 direction) => Mathf.Abs(direction.x) * frame.extents.x + Mathf.Abs(direction.z) * frame.extents.z;

        Bounds PlannedSource(float progress)
        {
            float travel = Smooth(0, .26f, progress) * (1 - Smooth(.73f, 1, progress));
            float turn = Smooth(.69f, .73f, progress) * (1 - Smooth(.97f, 1, progress));
            Quaternion rotation = Quaternion.AngleAxis(180f * turn, Vector3.up);
            Vector3 center = rootHome + plannedTravel * travel;
            Bounds frame = new Bounds(center + rotation * (sourceHome.center - rootHome), Vector3.zero);
            for (int i = 0; i < 8; i++) frame.Encapsulate(center + rotation * (Corner(sourceHome, i) - rootHome));
            return frame;
        }
        Bounds PlannedTarget(float progress)
        {
            Bounds frame = targetHome;
            frame.center += dodgeTravel * Smooth(.055f, .23f, progress) * (1 - Smooth(.86f, 1, progress));
            frame.Expand(new Vector3(.10f, Mathf.Max(.10f, dodgeLift * .7f), .10f));
            return frame;
        }
        Pose PlannedPose(float progress)
        {
            Bounds source = PlannedSource(progress), victim = PlannedTarget(progress);
            Vector3 head = rootHome + rootRotation * Vector3.Scale(headLocal, actor.lossyScale);
            head += source.center - sourceHome.center;
            return ActionPose(progress, source, victim, head);
        }
        Pose ActionPose(float progress, Bounds source, Bounds victim, Vector3 head)
        {
            // Live root travel brings the pair together, so the framing naturally closes with the running attacker.
            // Every phase uses the same continuous formula: closure at .60 does not select a new camera pose.
            float contact = Smooth(.02f, .19f, progress) * (1 - Smooth(.70f, .92f, progress));
            float returning = Smooth(.73f, 1, progress);
            // Stay slightly behind the attacker at contact: the defender's near arm then falls behind the muzzle.
            float orbit = Mathf.Lerp(.12f, -.32f, contact) + .02f * returning;
            Vector3 back = (backward + forward * orbit * orbitScale + Vector3.up * elevation).normalized;
            // Finish opening the pair before arrival, leaving the capped follow time to settle before the bite.
            float targetReveal = Smooth(.02f, .12f, progress) * (1 - Smooth(.73f, .96f, progress));
            Vector3 bodyFocus = Vector3.Lerp(source.center, victim.center, (.24f + .20f * contact) * targetReveal);
            Vector3 contactFocus = Vector3.Lerp(head, victim.center, .48f);
            Vector3 focus = Vector3.Lerp(bodyFocus, contactFocus, .20f * contact);
            float fov = 43f - 2f * contact + returning;
            // Reveal the defender continuously as the runner reaches it, then follow the runner home.
            // This fits occupied subjects rather than keeping the empty original run corridor on screen.
            Bounds revealedTarget = victim;
            revealedTarget.center = Vector3.Lerp(source.center, victim.center, targetReveal);
            revealedTarget.extents *= targetReveal;
            source.Encapsulate(head);
            // The fit uses measured animated body geometry, including its current native pose.
            float distance = Fit(source, revealedTarget, focus, back, fov) * 1.08f;
            Vector3 position = focus + back * distance;
            return new Pose { position = position, rotation = Quaternion.LookRotation(focus - position, Vector3.up), fov = fov };
        }
        bool ActionClear()
        {
            Pose previous = PlannedPose(0);
            for (int i = 0; i <= 80; i++)
            {
                float p = i / 80f; Pose pose = PlannedPose(p);
                Bounds source = PlannedSource(p), victim = PlannedTarget(p);
                if (!Clear(pose, source, victim) || !TravelClear(previous.position, pose.position)
                    || !Framed(source, pose, .025f)
                    || (p >= .26f && p <= .69f && !Framed(victim, pose, .025f))) return false;
                previous = pose;
            }
            return true;
        }
        static bool MotionWithinLimits(Pose from, Pose to, float seconds) =>
            Vector3.Distance(from.position, to.position) <= 39.5f * seconds
            && Quaternion.Angle(from.rotation, to.rotation) <= 198f * seconds
            && Mathf.Abs(from.fov - to.fov) <= 34.5f * seconds;
        bool PathClear(Pose from, Pose to)
        {
            Pose previous = from;
            LiveBounds(out Bounds source, out Bounds victim);
            for (int i = 0; i <= 32; i++)
            {
                Pose pose = Blend(from, to, Ease(i / 32f));
                if (!Clear(pose, source, victim) || !TravelClear(previous.position, pose.position)) return false;
                previous = pose;
            }
            return true;
        }

        public IEnumerator Enter()
        {
            if (!Stats.planned || canceled) yield break;
            transition = true; float began = Time.unscaledTime; Pose start = Current(), end = PlannedPose(0);
            try
            {
                for (float t = 0; t < EntrySeconds && !canceled; t += Time.unscaledDeltaTime)
                {
                    if (!Move(Blend(start, end, Ease(t / EntrySeconds)), true)) yield break;
                    yield return null;
                }
                if (!canceled && Move(end, true)) Stats.entered = true;
            }
            finally { Stats.entrySeconds = Time.unscaledTime - began; transition = false; }
        }
        public void Follow(float progress, float dt)
        {
            if (canceled || transition || !Stats.entered || FinishedRecovery || actor == null) return;
            progress = Mathf.Clamp01(progress);
            LiveBounds(out Bounds source, out Bounds victim);
            Pose desired = ActionPose(progress, source, victim, LiveHead);
            Pose previous = Current(), requested = desired;
            float step = Mathf.Max(.001f, dt);
            desired.position = Vector3.MoveTowards(previous.position, desired.position, 39.5f * step);
            desired.rotation = Quaternion.RotateTowards(previous.rotation, desired.rotation, 198f * step);
            desired.fov = Mathf.MoveTowards(previous.fov, desired.fov, 34.5f * step);
            bool moveAccepted = Move(desired, true);
            Stats.followFrames++;
            Pose pose = Current();
            bool sourceVisible = Framed(source, pose, .025f), targetVisible = Framed(victim, pose, .025f);
            if (sourceVisible) Stats.actorVisibleFrames++;
            if (targetVisible) Stats.targetVisibleFrames++;
            if (SameHalfspace(pose.position)) Stats.axisFrames++;
            Stats.targetHeightSum += ViewportHeight(victim, pose);
            if (progress >= .26f && progress <= .69f)
            {
                Stats.strikeFrames++;
                Vector3 head = LiveHead;
                bool headVisible = PointVisible(head, pose, .06f);
                if (sourceVisible && targetVisible && headVisible) Stats.strikeVisibleFrames++;
                else if (Stats.strikeFramingMisses.Count < 12)
                    Stats.strikeFramingMisses.Add(new BiteCameraFramingMiss {
                        progress = progress, dt = step,
                        sourceVisible = sourceVisible, targetVisible = targetVisible, headVisible = headVisible, moveAccepted = moveAccepted,
                        sourceCenter = source.center, sourceSize = source.size, targetCenter = victim.center, targetSize = victim.size, head = head,
                        previousPosition = previous.position, desiredPosition = requested.position, cameraPosition = pose.position,
                        desiredRotation = requested.rotation, cameraRotation = pose.rotation, desiredFov = requested.fov, cameraFov = pose.fov,
                        requestedSpeed = Vector3.Distance(previous.position, requested.position) / step,
                        requestedAngularSpeed = Quaternion.Angle(previous.rotation, requested.rotation) / step,
                        cappedSpeed = Vector3.Distance(previous.position, pose.position) / step,
                        cappedAngularSpeed = Quaternion.Angle(previous.rotation, pose.rotation) / step,
                        sourceViewport = ViewportRect(source, pose)
                    });
                Stats.strikeHeightSum += ViewportHeight(source, pose);
                Bounds subject = victim; subject.Encapsulate(head);
                Stats.contactSubjectHeightSum += ViewportHeight(subject, pose);
            }
        }
        public IEnumerator Recover()
        {
            if (canceled || !Stats.planned) yield break;
            transition = true; float began = Time.unscaledTime; Pose start = Current();
            bool direct = PathClear(start, home), reverse = !direct && HistoryClear(start);
            try
            {
                if (!direct && !reverse)
                { Stats.safe = false; Stats.recoveryBlocked = true; FinishedRecovery = true; yield break; }
                for (float t = 0; t < RecoverySeconds && !canceled; t += Time.unscaledDeltaTime)
                {
                    float q = Ease(t / RecoverySeconds);
                    if (!Move(direct ? Blend(start, home, q) : ReversePose(start, q), false))
                    { Stats.recoveryBlocked = true; FinishedRecovery = true; yield break; }
                    yield return null;
                }
                if (!canceled)
                {
                    Stats.recovered = Move(home, false); Stats.recoveryBlocked = !Stats.recovered; FinishedRecovery = true;
                }
            }
            finally { Stats.recoverySeconds = Time.unscaledTime - began; transition = false; ReleaseFrames(); }
        }
        bool HistoryClear(Pose start)
        {
            Pose previous = start;
            for (int i = historyCount - 1; i >= 0; i--)
            { if (!PathClear(previous, history[i])) return false; previous = history[i]; }
            return true;
        }
        Pose ReversePose(Pose start, float q)
        {
            float index = (1 - q) * historyCount; int low = Mathf.Min(historyCount - 1, Mathf.FloorToInt(index));
            Pose high = low + 1 >= historyCount ? start : history[low + 1];
            return Blend(history[low], high, Mathf.Clamp01(index - low));
        }
        void Remember(Pose pose)
        {
            if (historyCount == history.Length)
            {
                for (int i = 1; i < historyCount / 2; i++) history[i] = history[i * 2];
                historyCount /= 2;
            }
            history[historyCount++] = pose;
        }
        Pose Current() => new Pose { position = camera.transform.position, rotation = camera.transform.rotation, fov = camera.fieldOfView };
        bool Move(Pose pose, bool remember)
        {
            Pose previous = Current();
            LiveBounds(out Bounds source, out Bounds victim);
            if (!Clear(pose, source, victim) || !TravelClear(previous.position, pose.position)) { Stats.safe = false; return false; }
            float dt = Mathf.Max(.001f, Time.unscaledDeltaTime);
            Stats.maxSpeed = Mathf.Max(Stats.maxSpeed, Vector3.Distance(previous.position, pose.position) / dt);
            Stats.maxAngularSpeed = Mathf.Max(Stats.maxAngularSpeed, Quaternion.Angle(previous.rotation, pose.rotation) / dt);
            Stats.maxFovSpeed = Mathf.Max(Stats.maxFovSpeed, Mathf.Abs(previous.fov - pose.fov) / dt);
            Stats.travel = Mathf.Max(Stats.travel, Vector3.Distance(home.position, pose.position));
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation); camera.fieldOfView = pose.fov;
            if (remember) Remember(pose);
            return true;
        }
        float Fit(Bounds source, Bounds victim, Vector3 focus, Vector3 back, float fov)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -back).normalized, up = Vector3.Cross(-back, right).normalized;
            float vertical = Mathf.Tan(fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect), distance = 1.5f;
            for (int body = 0; body < 2; body++) for (int i = 0; i < 8; i++)
            {
                Vector3 c = Corner(body == 0 ? source : victim, i) - focus; float depth = Vector3.Dot(c, back);
                float horizontalFit = depth + (Mathf.Abs(Vector3.Dot(c, right)) + .16f) / (horizontal * .90f);
                float verticalFit = depth + (Mathf.Abs(Vector3.Dot(c, up)) + .16f) / (vertical * .88f);
                distance = Mathf.Max(distance, Mathf.Max(horizontalFit, verticalFit));
            }
            return distance;
        }
        static Vector3 Corner(Bounds frame, int i) => frame.center + Vector3.Scale(frame.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        bool PointVisible(Vector3 point, Pose pose, float margin)
        {
            Vector3 c = Quaternion.Inverse(pose.rotation) * (point - pose.position);
            float vertical = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect);
            return c.z > camera.nearClipPlane && Mathf.Abs(c.x / (c.z * horizontal)) <= 1 - margin * 2
                && Mathf.Abs(c.y / (c.z * vertical)) <= 1 - margin * 2;
        }
        bool Framed(Bounds frame, Pose pose, float margin)
        { for (int i = 0; i < 8; i++) if (!PointVisible(Corner(frame, i), pose, margin)) return false; return true; }
        Vector4 ViewportRect(Bounds frame, Pose pose)
        {
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            float vertical = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect);
            Vector4 rect = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = inverse * (Corner(frame, i) - pose.position);
                float depth = Mathf.Max(camera.nearClipPlane, c.z);
                float x = .5f + .5f * c.x / (depth * horizontal), y = .5f + .5f * c.y / (depth * vertical);
                rect.x = Mathf.Min(rect.x, x); rect.y = Mathf.Min(rect.y, y);
                rect.z = Mathf.Max(rect.z, x); rect.w = Mathf.Max(rect.w, y);
            }
            return rect;
        }
        float ViewportHeight(Bounds frame, Pose pose)
        {
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            float tangent = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = inverse * (Corner(frame, i) - pose.position); if (c.z <= camera.nearClipPlane) return 0;
                float y = .5f + .5f * c.y / (c.z * tangent); low = Mathf.Min(low, y); high = Mathf.Max(high, y);
            }
            return high - low;
        }
        bool SameHalfspace(Vector3 position) => Vector3.Dot(position - arena.BattleCenter, backward) > .2f;
        bool Clear(Pose pose, Bounds source, Bounds victim)
        {
            if (!SameHalfspace(pose.position)) return false;
            if (arena.Stadium == null) return true;
            int n = Physics.OverlapSphereNonAlloc(pose.position, .24f, nearby, ~0, QueryTriggerInteraction.Ignore);
            if (n == nearby.Length) return false;
            for (int i = 0; i < n; i++) if (nearby[i].transform.IsChildOf(arena.Stadium.transform)) return false;
            return !arena.Stadium.CameraPathBlocked(pose.position, source.center)
                && !arena.Stadium.CameraPathBlocked(pose.position, victim.center)
                && !arena.Stadium.CameraPathBlocked(pose.position, Vector3.Lerp(source.center, victim.center, .5f));
        }
        bool TravelClear(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (arena.Stadium == null || delta.sqrMagnitude < .000001f) return true;
            int n = Physics.SphereCastNonAlloc(from, .24f, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (n == hits.Length) return false;
            for (int i = 0; i < n; i++) if (hits[i].collider.transform.IsChildOf(arena.Stadium.transform)) return false;
            return true;
        }
        public void Cancel()
        {
            canceled = true; Stats.interrupted = Stats.planned && !Stats.recovered && !FinishedRecovery;
            ReleaseFrames();
        }
    }
}
