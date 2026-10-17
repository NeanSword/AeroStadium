using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [System.Serializable]
    public sealed class LeafCameraTelemetry
    {
        public bool planned, entered, recovered, interrupted, fallback, recoveryBlocked, safe = true;
        public int followFrames, axisFrames, actorVisibleFrames, targetVisibleFrames, corridorVisibleFrames;
        public int anticipationFrames, anticipationVisibleFrames, flightFrames, flightVisibleFrames;
        public int impactFrames, impactVisibleFrames, strikeFrames, strikeVisibleFrames;
        public float anticipationHeightSum, strikeHeightSum, targetHeightSum;
        public float maxSpeed, maxAngularSpeed, maxFovSpeed, travel, entrySeconds, recoverySeconds;
    }

    /// <summary>A continuous low three-quarter orbit from bud release through the curved leaf volley.</summary>
    internal sealed class LeafCameraTrack
    {
        struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
        }

        const float EntrySeconds = .85f, RecoverySeconds = 1.05f, EffectSeconds = 2.15f;
        readonly ArenaView arena;
        readonly Camera camera;
        readonly int side;
        readonly Pose home;
        readonly Vector3 backward, forward, across, firstRoot, secondRoot, leafAim;
        readonly Bounds sourceFrame, targetFrame;
        readonly float width;
        // Fixed buffers are reused by planning, following and telemetry; no per-frame lists or bone searches.
        readonly Vector3[] guides = new Vector3[128];
        readonly Pose[] history = new Pose[768];
        readonly Collider[] nearby = new Collider[32];
        readonly RaycastHit[] hits = new RaycastHit[32];
        int guideCount, historyCount;
        float orbitScale = 1, elevation = .15f;
        bool canceled, transition;
        public bool Transitioning => transition;
        public bool FinishedRecovery { get; private set; }
        public LeafCameraTelemetry Stats { get; } = new LeafCameraTelemetry();

        public LeafCameraTrack(ArenaView view, int attackerSide, Transform actor, Vector3 dodgeOffset)
        {
            arena = view; camera = view.ArenaCamera; side = attackerSide;
            home = Current();
            sourceFrame = arena.PokemonWorldBounds(side);
            Bounds target = arena.PokemonWorldBounds(1 - side); leafAim = target.center;
            targetFrame = target;
            if (dodgeOffset.sqrMagnitude > .0001f)
            {
                targetFrame.Encapsulate(target.min + dodgeOffset);
                targetFrame.Encapsulate(target.max + dodgeOffset);
            }
            Vector3 axis = Vector3.ProjectOnPlane(arena.BattleAxis, Vector3.up).normalized;
            if (axis.sqrMagnitude < .5f) axis = Vector3.right;
            backward = Vector3.Cross(Vector3.up, axis).normalized;
            forward = Vector3.ProjectOnPlane(target.center - sourceFrame.center, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = side == 0 ? axis : -axis;
            across = Vector3.Cross(Vector3.up, forward).normalized;
            width = Mathf.Clamp(sourceFrame.size.y * .22f, .16f, .42f);
            Vector3 first = sourceFrame.center + across * width * .65f;
            Vector3 second = sourceFrame.center - across * width * .65f;
            if (actor != null) foreach (Transform bone in actor.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "trLFeelerC01") first = bone.position;
                if (bone.name == "trRFeelerC01") second = bone.position;
            }
            firstRoot = first + Vector3.up * sourceFrame.size.y * .12f;
            secondRoot = second + Vector3.up * sourceFrame.size.y * .12f;
            Remember(home);
            // Retry a smaller low orbit when stadium geometry blocks the preferred route. Never raise to an aerial view.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                orbitScale = attempt < 3 ? 1f : .65f;
                elevation = .14f + (attempt % 3) * .045f;
                if (PathClear(home, ActionPose(0)) && ActionClear() && PathClear(ActionPose(1), home))
                { Stats.planned = true; Stats.fallback = attempt > 0; return; }
            }
            Stats.fallback = true;
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }
        static float Smooth(float a, float b, float t) { t = Mathf.Clamp01((t - a) / (b - a)); return t * t * (3 - 2 * t); }
        static Pose Blend(Pose a, Pose b, float t) => new Pose {
            position = Vector3.Lerp(a.position, b.position, t),
            rotation = Quaternion.Slerp(a.rotation, b.rotation, t), fov = Mathf.Lerp(a.fov, b.fov, t)
        };

        Pose ActionPose(float progress)
        {
            // A single differentiable move: source foreground, travelling volley, then a target-biased contact view.
            // Both full actors remain fitted, so the .14-second first release never forces a sudden pull-back.
            float travel = Smooth(0, .46f, progress);
            float settle = Smooth(.57f, 1, progress);
            float orbit = Mathf.Lerp(-.62f, .22f, travel) - .10f * settle;
            Vector3 back = (backward + forward * orbit * orbitScale + Vector3.up * elevation).normalized;
            Vector3 focus = Vector3.Lerp(sourceFrame.center, targetFrame.center, Mathf.Lerp(.39f, .54f, travel));
            focus += Vector3.up * width * .20f;
            float fov = Mathf.Lerp(43f, 45f, travel) - settle;
            FillGuides(progress);
            float distance = FitPoints(focus, back, fov) * 1.025f;
            Vector3 position = focus + back * distance;
            return new Pose { position = position, rotation = Quaternion.LookRotation(focus - position, Vector3.up), fov = fov };
        }

        bool ActionClear()
        {
            Pose previous = ActionPose(0);
            for (int i = 0; i <= 80; i++)
            {
                float progress = i / 80f;
                Pose pose = ActionPose(progress);
                if (!Clear(pose) || !TravelClear(previous.position, pose.position)
                    || !Framed(sourceFrame, pose, .025f) || !Framed(targetFrame, pose, .025f)
                    || !GuidesVisible(progress, pose, .025f)) return false;
                previous = pose;
            }
            return true;
        }

        bool PathClear(Pose from, Pose to)
        {
            Pose previous = from;
            for (int i = 0; i <= 32; i++)
            {
                Pose pose = Blend(from, to, Ease(i / 32f));
                if (!Clear(pose) || !TravelClear(previous.position, pose.position)) return false;
                previous = pose;
            }
            return true;
        }

        public IEnumerator Enter()
        {
            if (!Stats.planned || canceled) yield break;
            transition = true; float began = Time.unscaledTime;
            Pose start = Current(), end = ActionPose(0);
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
            if (canceled || transition || !Stats.entered || FinishedRecovery) return;
            progress = Mathf.Clamp01(progress);
            Move(ActionPose(progress), true);
            Stats.followFrames++;
            Pose pose = Current();
            Bounds source = arena.PokemonWorldBounds(side), target = arena.PokemonWorldBounds(1 - side);
            bool sourceVisible = Framed(source, pose, .025f), targetVisible = Framed(target, pose, .025f);
            bool corridorVisible = GuidesVisible(progress, pose, .025f);
            if (sourceVisible) Stats.actorVisibleFrames++;
            if (targetVisible) Stats.targetVisibleFrames++;
            if (corridorVisible) Stats.corridorVisibleFrames++;
            if (Vector3.Dot(pose.position - arena.BattleCenter, backward) > .2f) Stats.axisFrames++;
            Stats.targetHeightSum += ViewportHeight(target, pose);
            if (progress <= .16f)
            {
                Stats.anticipationFrames++;
                if (sourceVisible && corridorVisible) Stats.anticipationVisibleFrames++;
                Stats.anticipationHeightSum += ViewportHeight(source, pose);
            }
            if (progress >= .065f && progress < .29f)
            {
                Stats.flightFrames++;
                if (sourceVisible && targetVisible && corridorVisible) Stats.flightVisibleFrames++;
            }
            if (progress >= .29f && progress <= .47f)
            {
                Stats.impactFrames++;
                if (sourceVisible && targetVisible && corridorVisible) Stats.impactVisibleFrames++;
            }
            // Strike aliases cover the volley arriving at the body, preserving the common review telemetry contract.
            if (progress >= .22f && progress <= .47f)
            {
                Stats.strikeFrames++;
                if (sourceVisible && targetVisible && corridorVisible) Stats.strikeVisibleFrames++;
                Stats.strikeHeightSum += ViewportHeight(source, pose);
            }
        }

        public IEnumerator Recover()
        {
            if (canceled || !Stats.planned) yield break;
            transition = true; float began = Time.unscaledTime;
            Pose start = Current();
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
                    Stats.recovered = Move(home, false); Stats.recoveryBlocked = !Stats.recovered;
                    FinishedRecovery = true;
                }
            }
            finally { Stats.recoverySeconds = Time.unscaledTime - began; transition = false; }
        }

        bool HistoryClear(Pose start)
        {
            Pose previous = start;
            for (int i = historyCount - 1; i >= 0; i--)
            {
                if (!PathClear(previous, history[i])) return false;
                previous = history[i];
            }
            return true;
        }
        Pose ReversePose(Pose start, float q)
        {
            float index = (1 - q) * historyCount;
            int low = Mathf.Min(historyCount - 1, Mathf.FloorToInt(index));
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
            if (!Clear(pose) || !TravelClear(previous.position, pose.position)) { Stats.safe = false; return false; }
            float dt = Mathf.Max(.001f, Time.unscaledDeltaTime);
            Stats.maxSpeed = Mathf.Max(Stats.maxSpeed, Vector3.Distance(previous.position, pose.position) / dt);
            Stats.maxAngularSpeed = Mathf.Max(Stats.maxAngularSpeed, Quaternion.Angle(previous.rotation, pose.rotation) / dt);
            Stats.maxFovSpeed = Mathf.Max(Stats.maxFovSpeed, Mathf.Abs(previous.fov - pose.fov) / dt);
            Stats.travel = Mathf.Max(Stats.travel, Vector3.Distance(home.position, pose.position));
            camera.transform.SetPositionAndRotation(pose.position, pose.rotation); camera.fieldOfView = pose.fov;
            if (remember) Remember(pose);
            return true;
        }

        void FillGuides(float progress)
        {
            guideCount = 0;
            for (int i = 0; i < 8; i++) { guides[guideCount++] = Corner(sourceFrame, i); guides[guideCount++] = Corner(targetFrame, i); }
            // Curves contain occupied leaf trajectories rather than empty corners of a field-sized AABB.
            // Keep the guide envelope continuous across release/contact so fitting cannot cause a camera pop.
            for (int leaf = 0; leaf < 12; leaf++) for (int sample = 0; sample <= 6; sample++)
                guides[guideCount++] = LeafGuide(leaf, sample / 6f);
        }
        Vector3 LeafGuide(int leaf, float t)
        {
            Vector3 origin = (leaf & 1) == 0 ? firstRoot : secondRoot;
            Vector3 direction = (leafAim - origin).normalized;
            Vector3 leafSide = Vector3.Cross(direction, Mathf.Abs(direction.y) > .92f ? Vector3.forward : Vector3.up).normalized;
            Vector3 leafUp = Vector3.Cross(leafSide, direction).normalized;
            Vector3 aim = leafAim + leafSide * width * ((leaf % 3) - 1) * .32f
                + leafUp * width * (Hash(leaf + 7) - .5f) * .45f;
            float flight = .48f + Hash(leaf + 4) * .09f, age = t * flight;
            Vector3 gravity = -leafUp * width * 2.2f;
            Vector3 velocity = (aim - origin - gravity * .5f * flight * flight) / flight;
            float phase = leaf * 2.399963f;
            Vector3 curl = (leafSide * Mathf.Cos(phase) + leafUp * Mathf.Sin(phase) * .6f)
                * Mathf.Sin(Mathf.PI * t) * width * (1.1f + .5f * Hash((int)(phase * 8) + 2));
            return origin + velocity * age + gravity * .5f * age * age + curl;
        }
        static float Hash(int value) { return Mathf.Repeat(Mathf.Sin(value * 127.1f + 31.7f) * 43758.5453f, 1); }

        float FitPoints(Vector3 focus, Vector3 back, float fov)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -back).normalized, up = Vector3.Cross(-back, right).normalized;
            float vertical = Mathf.Tan(fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect), distance = 4;
            for (int i = 0; i < guideCount; i++)
            {
                Vector3 c = guides[i] - focus; float depth = Vector3.Dot(c, back);
                distance = Mathf.Max(distance, depth + (Mathf.Abs(Vector3.Dot(c, right)) + width * .72f) / (horizontal * .91f),
                    depth + (Mathf.Abs(Vector3.Dot(c, up)) + width * .72f) / (vertical * .91f));
            }
            return distance;
        }
        bool GuidesVisible(float progress, Pose pose, float margin)
        {
            FillGuides(progress);
            for (int i = 0; i < guideCount; i++) if (!PointVisible(guides[i], pose, margin)) return false;
            return true;
        }
        static Vector3 Corner(Bounds b, int i) => b.center + Vector3.Scale(b.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        bool PointVisible(Vector3 point, Pose pose, float margin)
        {
            Vector3 c = Quaternion.Inverse(pose.rotation) * (point - pose.position);
            float vertical = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect);
            return c.z > camera.nearClipPlane && Mathf.Abs(c.x / (c.z * horizontal)) <= 1 - margin * 2
                && Mathf.Abs(c.y / (c.z * vertical)) <= 1 - margin * 2;
        }
        bool Framed(Bounds b, Pose pose, float margin)
        {
            for (int i = 0; i < 8; i++) if (!PointVisible(Corner(b, i), pose, margin)) return false;
            return true;
        }
        float ViewportHeight(Bounds b, Pose pose)
        {
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            float tangent = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = inverse * (Corner(b, i) - pose.position);
                if (c.z <= camera.nearClipPlane) return 0;
                float y = .5f + .5f * c.y / (c.z * tangent); low = Mathf.Min(low, y); high = Mathf.Max(high, y);
            }
            return high - low;
        }
        bool Clear(Pose pose)
        {
            if (Vector3.Dot(pose.position - arena.BattleCenter, backward) <= .2f) return false;
            if (arena.Stadium == null) return true;
            int n = Physics.OverlapSphereNonAlloc(pose.position, .24f, nearby, ~0, QueryTriggerInteraction.Ignore);
            if (n == nearby.Length) return false;
            for (int i = 0; i < n; i++) if (nearby[i].transform.IsChildOf(arena.Stadium.transform)) return false;
            return !arena.Stadium.CameraPathBlocked(pose.position, sourceFrame.center)
                && !arena.Stadium.CameraPathBlocked(pose.position, targetFrame.center)
                && !arena.Stadium.CameraPathBlocked(pose.position, Vector3.Lerp(firstRoot, targetFrame.center, .5f));
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
        public void Cancel() { canceled = true; Stats.interrupted = Stats.planned && !Stats.recovered && !FinishedRecovery; }
    }
}
