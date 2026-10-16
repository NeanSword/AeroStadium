using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [System.Serializable]
    public sealed class WhipCameraTelemetry
    {
        public bool planned, entered, recovered, interrupted, fallback, recoveryBlocked, safe = true;
        public int followFrames, anticipationFrames, anticipationVisibleFrames, strikeFrames, actorVisibleFrames, targetVisibleFrames, corridorVisibleFrames, strikeVisibleFrames, axisFrames;
        public float maxSpeed, maxAngularSpeed, maxFovSpeed, travel, entrySeconds, recoverySeconds, anticipationHeightSum, strikeHeightSum;
    }

    /// <summary>Vine Whip's continuous three-quarter reveal, lash corridor and recovery. Owns no actors.</summary>
    internal sealed class WhipCameraTrack
    {
        struct Pose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
        }

        const float EntrySeconds = .95f, RecoverySeconds = 1.15f;
        readonly ArenaView arena;
        readonly Camera camera;
        readonly int side;
        readonly Pose home;
        readonly Vector3 axis, backward, forward;
        readonly Bounds corridor;
        readonly Vector3 firstRoot, secondRoot;
        readonly Bounds sourceFrame, targetFrame;
        readonly Pose[] shots = new Pose[5];
        readonly Collider[] nearby = new Collider[32];
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly List<Pose> history = new List<Pose>(240);
        bool canceled, transition;
        public bool Transitioning => transition;
        // A failed normal recovery must not be followed by an EndAction teleport.
        public bool FinishedRecovery { get; private set; }
        public WhipCameraTelemetry Stats { get; } = new WhipCameraTelemetry();

        public WhipCameraTrack(ArenaView view, int attackerSide, Transform actor)
        {
            arena = view; camera = view.ArenaCamera; side = attackerSide;
            home = Current();
            sourceFrame = arena.PokemonWorldBounds(side); targetFrame = arena.PokemonWorldBounds(1 - side);
            axis = Vector3.ProjectOnPlane(arena.BattleAxis, Vector3.up).normalized;
            if (axis.sqrMagnitude < .5f) axis = Vector3.right;
            backward = Vector3.Cross(Vector3.up, axis).normalized;
            forward = Vector3.ProjectOnPlane(targetFrame.center - sourceFrame.center, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) forward = side == 0 ? axis : -axis;
            firstRoot = sourceFrame.center + backward * .13f; secondRoot = sourceFrame.center - backward * .13f;
            if (actor != null) foreach (Transform bone in actor.GetComponentsInChildren<Transform>(true)) {
                if (bone.name == "trLFeelerC01") firstRoot = bone.position;
                if (bone.name == "trRFeelerC01") secondRoot = bone.position;
            }
            // Strike contains the bodies and modest lash bow, not an empty maximum-wind box over the field.
            corridor = sourceFrame; corridor.Encapsulate(targetFrame.min); corridor.Encapsulate(targetFrame.max);
            corridor.Encapsulate(Vector3.Lerp(sourceFrame.center, targetFrame.center, .5f) + Vector3.up * 1.1f);
            history.Add(home);
            // Low battle-height views only. A blocked route falls back safely instead of becoming a bird's-eye shot.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float amplitude = attempt < 3 ? 1f : .55f;
                float elevation = .15f + (attempt % 3) * .07f;
                shots[0] = Shot(.18f, false, -.38f * amplitude, elevation, 43f);
                shots[1] = Shot(.36f, true, -.12f * amplitude, elevation, 46f);
                shots[2] = Shot(.52f, true, .02f * amplitude, elevation, 45f);
                shots[3] = Shot(.80f, true, .16f * amplitude, elevation, 44f);
                shots[4] = Shot(1f, true, .06f * amplitude, elevation, 46f);
                if (PathClear(home, shots[0], false) && ActionClear() && PathClear(shots[4], home, false))
                { Stats.planned = true; Stats.fallback = attempt > 0; return; }
            }
            Stats.fallback = true;
        }

        Pose Shot(float progress, bool showTarget, float orbit, float elevation, float fov)
        {
            List<Vector3> points = FramingPoints(progress, showTarget);
            Bounds frame = GuideBounds(points);
            Vector3 focus = frame.center;
            Vector3 back = (backward + forward * orbit + Vector3.up * elevation).normalized;
            // Fit the real phase's guide points. Fitting AABB empty corners made the previous view unnecessarily remote.
            float distance = FitPoints(points, focus, back, fov) * 1.025f;
            Vector3 position = focus + back * distance;
            return new Pose { position = position, rotation = Quaternion.LookRotation(focus - position, Vector3.up), fov = fov };
        }

        static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }
        static Pose Blend(Pose a, Pose b, float t) => new Pose {
            position = Vector3.Lerp(a.position, b.position, t),
            rotation = Quaternion.Slerp(a.rotation, b.rotation, t), fov = Mathf.Lerp(a.fov, b.fov, t)
        };
        Pose ActionPose(float progress)
        {
            // Effect seconds: .85 backswing, 1.12 lash contact, 1.65 recoil, 2.50 gather.
            if (progress < .36f) return Blend(shots[0], shots[1], Ease(progress / .36f));
            if (progress < .56f) return Blend(shots[1], shots[2], Ease((progress - .36f) / .20f));
            if (progress < .80f) return Blend(shots[2], shots[3], Ease((progress - .56f) / .24f));
            return Blend(shots[3], shots[4], Ease((progress - .80f) / .20f));
        }
        bool ActionClear()
        {
            Pose previous = shots[0];
            for (int i = 0; i <= 48; i++)
            {
                float progress = i / 48f; Pose pose = ActionPose(progress);
                if (!Clear(pose) || !TravelClear(previous.position, pose.position)
                    || !Framed(sourceFrame, pose, .03f) || !GuidesVisible(progress, pose, .03f)
                    || (progress >= .36f && progress <= .56f && (!Framed(targetFrame, pose, .03f) || !Framed(corridor, pose, .03f)))) return false;
                previous = pose;
            }
            return true;
        }
        bool PathClear(Pose from, Pose to, bool requireFrame)
        {
            Pose previous = from;
            for (int i = 0; i <= 24; i++)
            {
                Pose pose = Blend(from, to, Ease(i / 24f));
                if (!Clear(pose) || !TravelClear(previous.position, pose.position)
                    || (requireFrame && !Framed(corridor, pose, .025f))) return false;
                previous = pose;
            }
            return true;
        }

        public IEnumerator Enter()
        {
            if (!Stats.planned || canceled) yield break;
            transition = true; float began = Time.unscaledTime;
            try
            {
                for (float t = 0; t < EntrySeconds && !canceled; t += Time.unscaledDeltaTime)
                {
                    if (!Move(Blend(home, shots[0], Ease(t / EntrySeconds)), true)) yield break;
                    yield return null;
                }
                if (!canceled && Move(shots[0], true)) Stats.entered = true;
            }
            finally { Stats.entrySeconds = Time.unscaledTime - began; transition = false; }
        }
        public void Follow(float progress, float dt)
        {
            if (canceled || transition || !Stats.entered || FinishedRecovery) return;
            Move(ActionPose(Mathf.Clamp01(progress)), true);
            Stats.followFrames++;
            Pose pose = Current();
            Bounds source = arena.PokemonWorldBounds(side), target = arena.PokemonWorldBounds(1 - side);
            bool sourceVisible = Framed(source, pose, .025f), targetVisible = Framed(target, pose, .025f);
            if (sourceVisible) Stats.actorVisibleFrames++;
            if (targetVisible) Stats.targetVisibleFrames++;
            if (Framed(corridor, pose, .025f)) Stats.corridorVisibleFrames++;
            if (Vector3.Dot(pose.position - arena.BattleCenter, backward) > .2f) Stats.axisFrames++;
            if (progress <= .26f)
            {
                Stats.anticipationFrames++;
                if (sourceVisible && GuidesVisible(progress, pose, .025f)) Stats.anticipationVisibleFrames++;
                Stats.anticipationHeightSum += ViewportHeight(source, pose);
            }
            if (progress >= .36f && progress <= .56f)
            {
                Stats.strikeFrames++;
                if (sourceVisible && targetVisible && Framed(corridor, pose, .025f)) Stats.strikeVisibleFrames++;
                Stats.strikeHeightSum += ViewportHeight(source, pose);
            }
        }
        public IEnumerator Recover()
        {
            if (canceled || !Stats.planned) yield break;
            transition = true; float began = Time.unscaledTime;
            Pose start = Current();
            bool direct = PathClear(start, home, false);
            // If an obstacle invalidates the direct exit, retrace the exact previously accepted route.
            bool reverse = !direct && HistoryClear(start);
            try
            {
                if (!direct && !reverse) { Stats.safe = false; Stats.recoveryBlocked = true; FinishedRecovery = true; yield break; }
                for (float t = 0; t < RecoverySeconds && !canceled; t += Time.unscaledDeltaTime)
                {
                    float q = Ease(t / RecoverySeconds);
                    Pose next = direct ? Blend(start, home, q) : ReversePose(start, q);
                    if (!Move(next, false)) { Stats.recoveryBlocked = true; FinishedRecovery = true; yield break; }
                    yield return null;
                }
                if (!canceled)
                {
                    Stats.recovered = Move(home, false);
                    Stats.recoveryBlocked = !Stats.recovered;
                    FinishedRecovery = true;
                }
            }
            finally { Stats.recoverySeconds = Time.unscaledTime - began; transition = false; }
        }

        bool HistoryClear(Pose start)
        {
            Pose previous = start;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (!PathClear(previous, history[i], false)) return false;
                previous = history[i];
            }
            return true;
        }
        Pose ReversePose(Pose start, float q)
        {
            float index = (1 - q) * history.Count;
            int low = Mathf.Min(history.Count - 1, Mathf.FloorToInt(index));
            Pose high = low + 1 >= history.Count ? start : history[low + 1];
            return Blend(history[low], high, index - low);
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
            if (remember) history.Add(pose);
            return true;
        }
        List<Vector3> FramingPoints(float progress, bool showTarget)
        {
            var points = new List<Vector3>(52);
            for (int i = 0; i < 8; i++) {
                points.Add(Corner(sourceFrame, i));
                if (showTarget) points.Add(Corner(targetFrame, i));
            }
            for (int vine = 0; vine < 2; vine++) for (int i = 0; i <= 16; i++)
                points.Add(GuidePoint(vine, i / 16f, progress * 2.5f));
            return points;
        }
        // Camera-only proxy of the staged effect's two continuous curves. Contact is still owned by the effect.
        Vector3 GuidePoint(int vine, float s, float time)
        {
            Vector3 root = vine == 0 ? firstRoot : secondRoot, aim = targetFrame.center;
            Vector3 direction = (aim - root).normalized, sideVector = Vector3.Cross(direction, Vector3.up).normalized;
            Vector3 upVector = Vector3.Cross(sideVector, direction).normalized;
            float range = Vector3.Distance(root, aim), sign = vine == 0 ? -1 : 1, local = time - vine * .045f;
            Vector3 outward = sideVector * sign;
            Vector3 wind = Bezier(root, root - direction * range * .15f + upVector * range * .60f + outward * range * .35f,
                root - direction * range * .20f + upVector * range * .40f + outward * range * .65f,
                root - direction * range * .22f + upVector * range * .16f + outward * range * .28f, s);
            float wave = Mathf.Clamp01((local - .60f) / .62f), spread = Mathf.Lerp(.24f, .10f, wave);
            float crest = Mathf.Exp(-Mathf.Pow((s - wave) / spread, 2)) * Mathf.Sin(Mathf.PI * s);
            Vector3 lash = Bezier(root, root + direction * range * .28f + upVector * range * .28f + outward * range * .17f,
                Vector3.Lerp(root, aim, .72f) + upVector * range * .10f + outward * range * .10f, aim, s)
                + (upVector + outward * .45f) * crest * range * .16f * (1 - wave * .7f);
            Vector3 point = Vector3.Lerp(wind, lash, Smooth(.55f + s * .40f, .91f + s * .28f, local));
            point = Vector3.Lerp(point, wind, Smooth(1.48f + (1 - s) * .10f, 1.96f + (1 - s) * .10f, local));
            return root + (point - root) * (Smooth(.03f, .32f, local) * (1 - Smooth(1.98f, 2.40f, local)));
        }
        static float Smooth(float a, float b, float t) { t = Mathf.Clamp01((t - a) / (b - a)); return t * t * (3 - 2 * t); }
        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        { float q = 1 - t; return q * q * q * a + 3 * q * q * t * b + 3 * q * t * t * c + t * t * t * d; }
        static Bounds GuideBounds(List<Vector3> points)
        {
            Bounds bounds = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++) bounds.Encapsulate(points[i]);
            return bounds;
        }
        bool GuidesVisible(float progress, Pose pose, float margin)
        {
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            float vertical = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect);
            foreach (Vector3 point in FramingPoints(progress, false)) {
                Vector3 c = inverse * (point - pose.position);
                if (c.z <= camera.nearClipPlane || Mathf.Abs(c.x / (c.z * horizontal)) > 1 - margin * 2
                    || Mathf.Abs(c.y / (c.z * vertical)) > 1 - margin * 2) return false;
            }
            return true;
        }
        float FitPoints(List<Vector3> points, Vector3 focus, Vector3 back, float fov)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -back).normalized, up = Vector3.Cross(-back, right).normalized;
            float vertical = Mathf.Tan(fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect), distance = 4;
            foreach (Vector3 point in points) {
                Vector3 c = point - focus; float depth = Vector3.Dot(c, back);
                distance = Mathf.Max(distance, depth + (Mathf.Abs(Vector3.Dot(c, right)) + .12f) / (horizontal * .90f),
                    depth + (Mathf.Abs(Vector3.Dot(c, up)) + .12f) / (vertical * .90f));
            }
            return distance;
        }
        static Vector3 Corner(Bounds b, int i) => b.center + Vector3.Scale(b.extents,
            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        bool Framed(Bounds b, Pose pose, float margin)
        {
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            float vertical = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), horizontal = vertical * Mathf.Max(.1f, camera.aspect);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = inverse * (Corner(b, i) - pose.position);
                if (c.z <= camera.nearClipPlane || Mathf.Abs(c.x / (c.z * horizontal)) > 1 - margin * 2
                    || Mathf.Abs(c.y / (c.z * vertical)) > 1 - margin * 2) return false;
            }
            return true;
        }
        float ViewportHeight(Bounds b, Pose pose)
        {
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            float tangent = Mathf.Tan(pose.fov * .5f * Mathf.Deg2Rad), low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = 0; i < 8; i++) {
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
                && !arena.Stadium.CameraPathBlocked(pose.position, corridor.center);
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
