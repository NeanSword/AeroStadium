using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Deterministic support planning independent of a particular imported rig.
    /// All coordinates and distances must use the same space (actor local is preferred).
    /// Projects a visual-root translation into the intersection of support reach
    /// spheres. It does not move planted contacts or extend segment lengths.
    /// </summary>
    public static class SupportReachConstraints
    {
        public readonly struct Limb
        {
            public readonly Vector3 Hip, Target;
            public readonly float UpperLength, LowerLength;

            public Limb(Vector3 hip, Vector3 target, float upperLength, float lowerLength)
            {
                Hip = hip; Target = target;
                UpperLength = upperLength; LowerLength = lowerLength;
            }

            public float Reach => UpperLength + LowerLength;
            public float InnerReach => Mathf.Abs(UpperLength - LowerLength);
        }

        /// <summary>
        /// Returns the nearest feasible root adjustment to zero, using Dykstra's
        /// projection onto closed convex reach balls and a root-adjustment ball.
        /// A false result means the authored shoulder/hip pose itself needs a
        /// support-aware reduction; clamping only a limb's desired target would
        /// slide that contact and conceal the planning error.
        /// </summary>
        public static bool TryProjectRoot(IReadOnlyList<Limb> limbs, float maxRootAdjustment,
            out Vector3 correction, out float worstReachError, int iterations = 64)
        {
            correction = Vector3.zero;
            worstReachError = 0f;
            if (limbs == null || limbs.Count == 0) return true;
            var residual = new Vector3[limbs.Count + 1];
            float slack = .00005f;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                Vector3 before = correction;
                for (int i = 0; i < limbs.Count; i++)
                {
                    Limb limb = limbs[i];
                    if (!Finite(limb.Hip) || !Finite(limb.Target) || limb.Reach < slack)
                        return false;
                    Vector3 supplied = correction + residual[i];
                    Vector3 center = limb.Target - limb.Hip;
                    Vector3 projected = ProjectBall(supplied, center, Mathf.Max(slack, limb.Reach - slack));
                    residual[i] = supplied - projected;
                    correction = projected;
                }
                int limit = limbs.Count;
                Vector3 limitedInput = correction + residual[limit];
                Vector3 limited = ProjectBall(limitedInput, Vector3.zero, Mathf.Max(0f, maxRootAdjustment));
                residual[limit] = limitedInput - limited;
                correction = limited;
                worstReachError = MaximumError(limbs, correction);
                if ((correction - before).sqrMagnitude < 1e-12f && worstReachError < .0002f)
                    return true;
            }
            worstReachError = MaximumError(limbs, correction);
            return Finite(correction) && worstReachError < .0002f;
        }

        /// <summary>
        /// A constant per-limb step scale determined from its rest chain length.
        /// Compute it when binding. Do not recompute from each animated hip pose:
        /// that would make a planted contact slide during the support phase.
        /// The caller keeps the original authored phase/hold timing.
        /// </summary>
        public static Vector3 ScaleStep(Vector3 authoredStep, float fittedHeight,
            float upperRestLength, float lowerRestLength)
        {
            float length = upperRestLength + lowerRestLength;
            float advanceLimit = length * .42f;
            float liftLimit = length * .32f;
            float advanceScale = Mathf.Min(1f, advanceLimit / Mathf.Max(.00001f, fittedHeight * .105f));
            float liftScale = Mathf.Min(1f, liftLimit / Mathf.Max(.00001f, fittedHeight * .085f));
            return new Vector3(authoredStep.x * advanceScale,
                Mathf.Max(0f, authoredStep.y) * liftScale, authoredStep.z * advanceScale);
        }

        public static float MaximumError(IReadOnlyList<Limb> limbs, Vector3 correction)
        {
            float error = 0f;
            foreach (Limb limb in limbs)
            {
                float distance = Vector3.Distance(limb.Hip + correction, limb.Target);
                error = Mathf.Max(error, distance - limb.Reach);
                // The inner forbidden ball is non-convex. Report it explicitly;
                // a folded pose needs an authored bend/root adjustment rather
                // than hiding it by stretching one segment.
                error = Mathf.Max(error, limb.InnerReach - distance);
            }
            return error;
        }

        static Vector3 ProjectBall(Vector3 point, Vector3 center, float radius)
        {
            Vector3 delta = point - center;
            float distance = delta.magnitude;
            return distance <= radius || distance <= .0000001f
                ? point : center + delta * (radius / distance);
        }

        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x)
            && !float.IsNaN(p.y) && !float.IsInfinity(p.y)
            && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}
