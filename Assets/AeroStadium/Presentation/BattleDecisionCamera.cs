using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Quiet camera choreography while the player chooses a battle action.</summary>
    public sealed class BattleDecisionCamera : MonoBehaviour
    {
        public enum Shot { Wide, PlayerAngle, PlayerClose, HighWide, OpponentClose }
        struct Pose { public Vector3 position, target; public float fov; }
        const float BlendDuration = 1.7f;
        readonly float[] holds = { 6.5f, 5.8f, 6.2f, 7.2f, 6f };
        ArenaView arena;
        Pose start, goal;
        float blend, hold;
        int visited;
        bool returning;
        public bool Active { get; private set; }
        public Shot CurrentShot { get; private set; }
        public int ShotVisits { get; private set; }
        public int ShotsVisited {
            get { int count = 0; for (int i = 0; i < 5; i++) if ((visited & (1 << i)) != 0) count++; return count; }
        }
        public bool CameraSafety { get; private set; } = true;

        public void Initialize(ArenaView view) { arena = view; if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--battle-camera-review")>=0)gameObject.AddComponent<BattleCameraProof>().Initialize(view,this); }
        public void SetActive(bool active)
        {
            if (Active == active) return;
            Active = active;
            returning = false;
            if (!active) return;
            visited = 0; ShotVisits = 0;
            Begin(Shot.Wide);
        }
        public IEnumerator ReturnToWide()
        {
            SetActive(false);
            if (!Ready || !Begin(Shot.Wide)) yield break;
            returning = true;
            while (returning && blend < BlendDuration) yield return null;
            returning = false;
        }
        bool Ready => arena != null && arena.ArenaCamera != null && !arena.ArenaCamera.orthographic
            && arena.TryGetCombatCameraFrame(0, out _) && arena.TryGetCombatCameraFrame(1, out _);

        void LateUpdate()
        {
            if ((!Active && !returning) || !Ready) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            blend = Mathf.Min(BlendDuration, blend + dt);
            float t = Mathf.SmoothStep(0f, 1f, blend / BlendDuration);
            Pose next = new Pose { position = Vector3.Lerp(start.position, goal.position, t),
                target = Vector3.Lerp(start.target, goal.target, t), fov = Mathf.Lerp(start.fov, goal.fov, t) };
            Camera camera = arena.ArenaCamera;
            CameraSafety = Safe(next.position, next.target) && TravelSafe(camera.transform.position, next.position);
            if (CameraSafety) Apply(next);
            else {
                // Hold the last clear view and replace the blocked route with the canonical wide view.
                Begin(Shot.Wide);
                if (returning) returning = false;
                return;
            }
            if (blend < BlendDuration) return;
            if (returning) { returning = false; return; }
            hold += dt;
            if (hold >= holds[(int)CurrentShot]) Begin((Shot)(((int)CurrentShot + 1) % 5));
        }
        bool Begin(Shot shot)
        {
            if (!Ready) return false;
            if (!CreatePose(shot, out goal)) {
                shot = Shot.Wide;
                if (!CreatePose(shot, out goal)) return false;
            }
            Camera camera = arena.ArenaCamera;
            start = new Pose { position = camera.transform.position,
                target = camera.transform.position + camera.transform.forward * Vector3.Distance(camera.transform.position, goal.target),
                fov = Mathf.Clamp(camera.fieldOfView, 32f, 48f) };
            blend = 0; hold = 0; CurrentShot = shot;
            visited |= 1 << (int)shot; ShotVisits++;
            return true;
        }
        bool CreatePose(Shot shot, out Pose pose)
        {
            pose = default;
            if (!arena.TryGetCombatCameraFrame(0, out Bounds player)
                || !arena.TryGetCombatCameraFrame(1, out Bounds opponent)) return false;
            bool wide = shot == Shot.Wide || shot == Shot.HighWide;
            Bounds subject = shot == Shot.OpponentClose ? opponent : player;
            if (wide) { subject.Encapsulate(opponent.min); subject.Encapsulate(opponent.max); }
            bool close = shot == Shot.PlayerClose || shot == Shot.OpponentClose;
            if (close) {
                Vector3 fullSize = subject.size;
                subject.center += Vector3.up * fullSize.y * .12f;
                subject.size = Vector3.Scale(fullSize, new Vector3(.60f, .76f, .62f));
            }
            Vector3 target = subject.center + (wide || close ? Vector3.zero : Vector3.up * subject.extents.y * .12f);
            Vector3 axis = arena.BattleAxis;
            Vector3 backward = Vector3.Cross(Vector3.up, axis).normalized;
            float angle = shot == Shot.PlayerAngle ? .42f : shot == Shot.PlayerClose ? .8f : shot == Shot.OpponentClose ? -.8f : 0f;
            float elevation = shot == Shot.HighWide ? .9f : wide ? .34f : .17f;
            float fov = wide ? 43f : shot == Shot.PlayerAngle ? 40f : 35f;
            // Always remain on the same side of the battle axis; wide shots keep the midfield stripe upright.
            for (int attempt = 0; attempt < 7; attempt++) {
                Vector3 back = (backward + axis * angle + Vector3.up * elevation).normalized;
                float distance = Fit(subject, target, back, fov, arena.ArenaCamera.aspect);
                if (wide) distance = Mathf.Max(12f, distance);
                Vector3 position = target + back * distance;
                if (Safe(position, target)) {
                    pose = new Pose { position = position, target = target, fov = fov }; return true;
                }
                elevation = elevation * 1.6f + .3f;
            }
            return false;
        }
        static float Fit(Bounds bounds, Vector3 target, Vector3 backward, float fov, float aspect)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -backward).normalized;
            Vector3 up = Vector3.Cross(-backward, right).normalized;
            float vertical = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            float horizontal = vertical * Mathf.Max(.1f, aspect);
            float distance = .8f;
            // Fit the framing region: complete models in wide views, upper bodies in close-ups.
            for (int i = 0; i < 8; i++) {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)) - target;
                float depth = Vector3.Dot(corner, backward);
                distance = Mathf.Max(distance, depth + (Mathf.Abs(Vector3.Dot(corner, right)) + .15f) / (horizontal * .88f),
                    depth + (Mathf.Abs(Vector3.Dot(corner, up)) + .15f) / (vertical * .85f));
            }
            return distance;
        }
        bool Safe(Vector3 position, Vector3 target)
        {
            Vector3 backward = Vector3.Cross(Vector3.up, arena.BattleAxis).normalized;
            if (Vector3.Dot(position - arena.BattleCenter, backward) <= .15f) return false;
            if (arena.Stadium == null) return true;
            foreach (Collider collider in Physics.OverlapSphere(position, .24f, ~0, QueryTriggerInteraction.Ignore))
                if (collider.transform.IsChildOf(arena.Stadium.transform)) return false;
            return !arena.Stadium.CameraPathBlocked(position, target);
        }
        bool TravelSafe(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .00001f || arena.Stadium == null) return true;
            foreach (RaycastHit hit in Physics.SphereCastAll(from, .24f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.transform.IsChildOf(arena.Stadium.transform)) return false;
            return true;
        }
        void Apply(Pose pose)
        {
            Camera camera = arena.ArenaCamera;
            camera.transform.SetPositionAndRotation(pose.position, Quaternion.LookRotation(pose.target - pose.position, Vector3.up));
            camera.fieldOfView = Mathf.Clamp(pose.fov, 32f, 48f);
        }
    }
}
