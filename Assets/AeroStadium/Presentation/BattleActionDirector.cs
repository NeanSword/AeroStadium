using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Owns one action's camera travel and an explicitly resolved miss's evasive step.</summary>
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class BattleActionDirector : MonoBehaviour
    {
        SurfCameraTrack surfTrack;WhipCameraTrack whipTrack;Vector3 attackerHome;Quaternion attackerRotation;bool restoreAttacker;
        public SurfCameraTelemetry LastSurfCamera {get;private set;}
        public WhipCameraTelemetry LastWhipCamera {get;private set;}
        ArenaView arena;
        Camera actionCamera;
        BattleDecisionCamera decision;
        Transform attacker, target;
        Vector3 cameraHome, targetHome, backward, axis, dodgeDirection, cameraVelocity, lookAt;
        Quaternion cameraRotation;
        Bounds attackerFrame, targetFrame;
        float cameraFov, fovVelocity, elapsed, duration, progress, dodgeDistance;
        int attackerSide;
        bool decisionEnabled, dodge, progressDriven, impactKnown;
        Vector3 impactPoint;
        public bool Active { get; private set; }
        public float CameraTravel {get;private set;} public float DodgeTravel {get;private set;}
        public bool Restored {get;private set;}=true;
        readonly Collider[] nearby=new Collider[32];readonly RaycastHit[] castHits=new RaycastHit[32];

        public void Initialize(ArenaView view) { arena = view; }

        // allowDodge must come from a resolved accuracy/evasion miss, never from immunity.
        public void BeginAction(int side, Transform source, Transform victim, int moveId,
            bool targetHit, bool protectedTarget, bool allowDodge = false, float actionDuration = 1.6f)
        {
            EndAction();
            if (arena == null || source == null || victim == null || side < 0 || side > 1
                || arena.ArenaCamera == null || arena.ArenaCamera.orthographic
                || !arena.TryGetCombatCameraFrame(side, out attackerFrame)
                || !arena.TryGetCombatCameraFrame(1 - side, out targetFrame)) return;
            actionCamera = arena.ArenaCamera; attacker = source; target = victim; attackerSide = side;
            attackerHome=source.position;attackerRotation=source.rotation;restoreAttacker=moveId==57||moveId==424;
            cameraHome = actionCamera.transform.position; cameraRotation = actionCamera.transform.rotation;
            cameraFov = actionCamera.fieldOfView; targetHome = target.position;
            decision = arena.DecisionCamera;
            decisionEnabled = decision != null && decision.enabled;
            if (decision != null) decision.enabled = false;
            axis = Vector3.ProjectOnPlane(arena.BattleAxis, Vector3.up).normalized;
            if (axis.sqrMagnitude < .5f) axis = Vector3.right;
            backward = Vector3.Cross(Vector3.up, axis).normalized;
            duration = Mathf.Max(.45f, actionDuration); elapsed = progress = 0;
            progressDriven = impactKnown = false; cameraVelocity = Vector3.zero; fovVelocity = 0;
            lookAt = cameraHome + actionCamera.transform.forward
                * Vector3.Distance(cameraHome, (attackerFrame.center + targetFrame.center) * .5f);
            dodge = allowDodge && !targetHit && !protectedTarget;
            CameraTravel=DodgeTravel=0;Restored=false;Active = true;
            if(moveId==57){surfTrack=new SurfCameraTrack(arena,side,source);LastSurfCamera=surfTrack.Stats;}
            if(moveId==22){whipTrack=new WhipCameraTrack(arena,side,source);LastWhipCamera=whipTrack.Stats;}
            if (dodge) PrepareDodge();
        }

        public void SetProgress(float normalizedProgress)
        {
            if (!Active || !Finite(normalizedProgress)) return;
            progressDriven = true;
            progress = Mathf.Max(progress, Mathf.Clamp01(normalizedProgress));
        }

        public void NotifyImpact(Vector3 point)
        {
            if (!Active || !Finite(point.x) || !Finite(point.y) || !Finite(point.z)) return;
            impactKnown = true; impactPoint = point;
            if(surfTrack==null&&whipTrack==null)progress = Mathf.Max(progress, .60f);
        }

        void PrepareDodge()
        {
            // Move the complete actor across its line of fire; do not bend its native rig or play damage.
            dodgeDirection = -backward;
            dodgeDistance = Mathf.Clamp(Mathf.Max(targetFrame.extents.x, targetFrame.extents.z) * .75f, .6f, 1.5f);
            bool fieldWasValid = arena.BattlePositionsValid;
            if (!fieldWasValid) { dodgeDistance = 0; return; }
            try {
                for (int attempt = 0; attempt < 6; attempt++) {
                    target.position = targetHome + dodgeDirection * dodgeDistance;
                    if (arena.BattlePositionsValid && DodgeClear()) return;
                    dodgeDistance *= .5f;
                }
                dodgeDistance = 0;
            } finally { if (target != null) target.position = targetHome; }
        }

        bool DodgeClear()
        {
            if (arena.Stadium == null) return true;
            Vector3 center = targetFrame.center + dodgeDirection * dodgeDistance;
            float radius = Mathf.Min(.4f, Mathf.Max(.15f, targetFrame.extents.y * .3f));
            int count=Physics.OverlapSphereNonAlloc(center,radius,nearby,~0,QueryTriggerInteraction.Ignore);if(count==nearby.Length)return false;
            for(int i=0;i<count;i++)if(nearby[i].transform.IsChildOf(arena.Stadium.transform))return false;
            return true;
        }

        void LateUpdate()
        {
            if (!Active) return;
            if (actionCamera == null || attacker == null || target == null) { EndAction(); return; }
            if((surfTrack!=null&&surfTrack.Transitioning)||(whipTrack!=null&&whipTrack.Transitioning))return;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            elapsed += dt;
            if (!progressDriven) progress = Mathf.Max(progress, Mathf.Clamp01(elapsed / duration));
            if (dodge) {
                float away = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .27f, progress));
                float home = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.70f, .96f, progress));
                target.position = targetHome + dodgeDirection * (dodgeDistance * away * (1 - home));DodgeTravel=Mathf.Max(DodgeTravel,Vector3.Distance(targetHome,target.position));
            }
            if(surfTrack!=null){if(surfTrack.Stats.entered)surfTrack.Follow(dt);CameraTravel=Mathf.Max(CameraTravel,surfTrack.Stats.travel);}
            else if(whipTrack!=null){whipTrack.Follow(progress,dt);CameraTravel=Mathf.Max(CameraTravel,whipTrack.Stats.travel);}
            else UpdateCamera(dt);
        }

        void UpdateCamera(float dt)
        {
            // Framing follows roots, never individual animated bones, to avoid camera jitter.
            Bounds source = attackerFrame, victim = targetFrame;
            if (arena.TryGetCombatCameraFrame(attackerSide, out Bounds currentSource)) source = currentSource;
            if (arena.TryGetCombatCameraFrame(1 - attackerSide, out Bounds currentTarget)) victim = currentTarget;
            Vector3 middle = (source.center + victim.center) * .5f;
            Vector3 flight = Vector3.Lerp(source.center, victim.center,
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.24f, .61f, progress)));
            Vector3 focus;
            if (progress < .22f) focus = Vector3.Lerp(middle, source.center, Mathf.SmoothStep(0, .25f, progress / .22f));
            else if (progress < .61f) focus = Vector3.Lerp(middle, flight, .25f);
            else focus = Vector3.Lerp(middle, impactKnown ? impactPoint : victim.center,
                .25f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.74f, 1, progress))));
            Bounds frame = source; frame.Encapsulate(victim.min); frame.Encapsulate(victim.max);
            if (impactKnown) frame.Encapsulate(impactPoint);
            frame.Expand(.7f);
            // A bounded lateral arc preserves screen direction and the complete travel corridor.
            float side = Vector3.Dot(source.center - victim.center, axis) < 0 ? -1 : 1;
            float arc = side * .10f * Mathf.Sin(progress * Mathf.PI);
            float fov = Mathf.Clamp(cameraFov - 3f * Mathf.Sin(progress * Mathf.PI), 36f, 48f);
            Vector3 desired = Vector3.zero;
            bool clear = false;
            for (int attempt = 0; attempt < 3; attempt++) {
                Vector3 viewBack = (backward + axis * arc + Vector3.up * (.40f + attempt * .35f)).normalized;
                desired = focus + viewBack * Fit(frame, focus, viewBack, fov);
                if (Safe(desired, focus)) { clear = true; break; }
            }
            if (!clear) return;
            Vector3 next = Vector3.SmoothDamp(actionCamera.transform.position, desired, ref cameraVelocity, .18f, 18f, dt);
            Vector3 nextLook = Vector3.Lerp(lookAt, focus, 1 - Mathf.Exp(-dt * 9f));
            float nextFov = Mathf.SmoothDamp(actionCamera.fieldOfView, fov, ref fovVelocity, .18f, 20f, dt);
            // Interpolation also needs a full-body fit while the focus travels across the action.
            Vector3 nextBack = (next - nextLook).normalized;
            float fitDistance = Fit(frame, nextLook, nextBack, nextFov);
            if (Vector3.Distance(next, nextLook) < fitDistance) next = nextLook + nextBack * fitDistance;
            if (!Safe(next, nextLook) || !TravelSafe(actionCamera.transform.position, next)) return;
            lookAt = nextLook;
            actionCamera.transform.SetPositionAndRotation(next, Quaternion.LookRotation(lookAt - next, Vector3.up));
            actionCamera.fieldOfView = nextFov;CameraTravel=Mathf.Max(CameraTravel,Vector3.Distance(cameraHome,next));
        }

        float Fit(Bounds frame, Vector3 focus, Vector3 back, float fov)
        {
            Vector3 right = Vector3.Cross(Vector3.up, -back).normalized;
            Vector3 up = Vector3.Cross(-back, right).normalized;
            float vertical = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            float horizontal = vertical * Mathf.Max(.1f, actionCamera.aspect);
            float distance = 4;
            for (int i = 0; i < 8; i++) {
                Vector3 corner = frame.center + Vector3.Scale(frame.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)) - focus;
                float depth = Vector3.Dot(corner, back);
                distance = Mathf.Max(distance, depth + Mathf.Abs(Vector3.Dot(corner, right)) / (horizontal * .85f),
                    depth + Mathf.Abs(Vector3.Dot(corner, up)) / (vertical * .82f));
            }
            return distance;
        }

        bool Safe(Vector3 position, Vector3 focus)
        {
            if (Vector3.Dot(position - arena.BattleCenter, backward) <= .2f) return false;
            if (arena.Stadium == null) return true;
            int count=Physics.OverlapSphereNonAlloc(position,.24f,nearby,~0,QueryTriggerInteraction.Ignore);if(count==nearby.Length)return false;
            for(int i=0;i<count;i++)if(nearby[i].transform.IsChildOf(arena.Stadium.transform))return false;
            return !arena.Stadium.CameraPathBlocked(position, focus);
        }

        bool TravelSafe(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (arena.Stadium == null || delta.sqrMagnitude < .00001f) return true;
            int count=Physics.SphereCastNonAlloc(from,.24f,delta.normalized,castHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);if(count==castHits.Length)return false;
            for(int i=0;i<count;i++)if(castHits[i].collider.transform.IsChildOf(arena.Stadium.transform))return false;
            return true;
        }

        public System.Collections.IEnumerator EnterAction()
        {if(surfTrack!=null){yield return surfTrack.Enter();elapsed=0;}
            else if(whipTrack!=null){yield return whipTrack.Enter();elapsed=0;}}
        public System.Collections.IEnumerator RecoverAction()
        {if(surfTrack!=null)yield return surfTrack.Recover();
            else if(whipTrack!=null)yield return whipTrack.Recover();}
        public void EndAction()
        {
            if (!Active) return;
            Active = false;
            try {
                // A newly blocked normal exit holds the safe pose; an explicit interruption still restores immediately.
                bool holdWhip = whipTrack!=null&&whipTrack.FinishedRecovery&&!whipTrack.Stats.recovered;
                if (actionCamera != null&&!holdWhip) {
                    actionCamera.transform.SetPositionAndRotation(cameraHome, cameraRotation);
                    actionCamera.fieldOfView = cameraFov;Restored=Vector3.Distance(actionCamera.transform.position,cameraHome)<.001f&&Quaternion.Angle(actionCamera.transform.rotation,cameraRotation)<.01f;
                }
            } finally {
                if(surfTrack!=null){surfTrack.Cancel();surfTrack=null;}
                bool rebaseDecision=whipTrack!=null&&whipTrack.FinishedRecovery&&!whipTrack.Stats.recovered;
                if(whipTrack!=null){whipTrack.Cancel();whipTrack=null;}
                if(restoreAttacker&&attacker!=null)attacker.SetPositionAndRotation(attackerHome,attackerRotation);
                if (dodge && target != null) target.position = targetHome;
                if (decision != null) {
                    if(rebaseDecision&&decisionEnabled&&decision.Active){decision.SetActive(false);decision.SetActive(true);}
                    decision.enabled = decisionEnabled;
                }
                actionCamera = null; attacker = target = null; decision = null; dodge = false;
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        void OnDisable() { EndAction(); }
        void OnDestroy() { EndAction(); }
    }
}
