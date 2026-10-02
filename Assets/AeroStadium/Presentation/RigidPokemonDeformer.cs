using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Small original secondary motions for meshes supplied without a skeleton.
    /// Feet/base stay still. This is deliberately not a substitute for a walking/action rig.</summary>
    internal sealed class RigidPokemonDeformer
    {
        readonly MeshFilter filter;
        readonly Mesh source, instance;
        readonly Vector3[] positions, normals, output, normalOutput;
        readonly Matrix4x4 modelToMesh;
        readonly Matrix4x4 normalsToMesh;
        readonly Bounds bounds;
        readonly int species;
        readonly float height, width, depth;

        public RigidPokemonDeformer(MeshFilter target, Transform model, Bounds modelBounds, int speciesId)
        {
            filter = target;
            source = target.sharedMesh;
            instance = Object.Instantiate(source);
            instance.name = source.name + " (original motion instance)";
            instance.MarkDynamic();
            filter.sharedMesh = instance;
            bounds = modelBounds;
            species = speciesId;
            height = Mathf.Max(.01f, bounds.size.y);
            width = Mathf.Max(.01f, bounds.size.x);
            depth = Mathf.Max(.01f, bounds.size.z);
            Matrix4x4 meshToModel = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            modelToMesh = meshToModel.inverse;
            normalsToMesh = modelToMesh.inverse.transpose;
            Vector3[] local = source.vertices;
            Vector3[] localNormals = source.normals;
            positions = new Vector3[local.Length]; normals = new Vector3[local.Length];
            output = new Vector3[local.Length]; normalOutput = new Vector3[local.Length];
            Matrix4x4 normalMatrix = meshToModel.inverse.transpose;
            for (int i = 0; i < local.Length; i++)
            {
                positions[i] = meshToModel.MultiplyPoint3x4(local[i]);
                normals[i] = normalMatrix.MultiplyVector(localNormals.Length == local.Length ? localNormals[i] : Vector3.up).normalized;
            }
        }

        public void Sample(float time, float phase, PokemonMotionProfile profile, PokemonMotionAction action,
            float anticipation, float strike, float hero, float hit, float faint)
        {
            float breathe = Mathf.Sin(time * profile.BreathRate * Mathf.PI * 2f + phase);
            float glance = Mathf.Pow(Mathf.Sin(time * .47f + phase), 3f);
            bool quadruped = profile.Anatomy == PokemonAnatomy.Quadruped;
            bool fish = profile.Anatomy == PokemonAnatomy.Fish;
            bool winged = profile.Anatomy == PokemonAnatomy.Bird || profile.Anatomy == PokemonAnatomy.Bat;
            float headAngle = -breathe * .7f - hero * 3f + anticipation * 2f - strike * 4f + hit * 3f + faint * 5f;
            float armAngle = hero * 3f + strike * 8f - anticipation * 3f;
            float wave = Mathf.Sin(time * 2.3f + phase);
            Vector3 headPivot = new Vector3(bounds.center.x, bounds.min.y + height * .63f, bounds.center.z);
            bool rigidSurface = profile.Anatomy == PokemonAnatomy.Shell || species == 81 || species == 82 || species == 109 || species == 110;
            float squish = rigidSurface ? 0f : profile.Anatomy == PokemonAnatomy.Blob ? .006f : .0025f;
            // Articulated gestures require a real rig. Here displacement is intentionally limited.
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 p = positions[i], n = normals[i];
                float y = (p.y - bounds.min.y) / height;
                float baseLock = Smooth(.12f, .3f, y);
                float chest = Smooth(.22f, .46f, y) * (1f - Smooth(.7f, .93f, y));
                Vector3 radial = new Vector3(p.x - bounds.center.x, 0f, p.z - bounds.center.z);
                p += radial * (breathe * squish * chest);
                if (!rigidSurface) p.y += breathe * height * .0015f * chest - faint * height * .004f * chest;
                float head = quadruped
                    ? Smooth(.62f, .92f, .5f - (p.z - bounds.center.z) / depth) * Smooth(.32f, .65f, y)
                    : Smooth(.63f, .86f, y);
                // Keep palms, antennae, shells and sphere surfaces intact unless anatomically applicable.
                if (profile.Anatomy != PokemonAnatomy.Float && profile.Anatomy != PokemonAnatomy.Shell && !fish && !winged)
                {
                    Quaternion turn = Quaternion.Euler(headAngle * head, glance * profile.LookSize * .28f * head, 0f);
                    p = headPivot + turn * (p - headPivot); n = turn * n;
                }
                if (profile.Anatomy == PokemonAnatomy.Biped && species != 143)
                {
                    float side = p.x < bounds.center.x ? -1f : 1f;
                    float arm = Smooth(.25f, .43f, Mathf.Abs(p.x - bounds.center.x) / width)
                        * Smooth(.28f, .44f, y) * (1f - Smooth(.69f, .84f, y));
                    Vector3 shoulder = new Vector3(bounds.center.x + side * width * .21f, bounds.min.y + height * .64f, bounds.center.z);
                    Quaternion gesture = Quaternion.Euler(-armAngle * arm, 0f, side * hero * 3f * arm);
                    p = shoulder + gesture * (p - shoulder); n = gesture * n;
                }
                if (fish)
                {
                    float tail = Smooth(.38f, .86f, .5f + (p.z - bounds.center.z) / depth);
                    Quaternion swim = Quaternion.AngleAxis(Mathf.Sin(time * 2.3f + phase - tail) * profile.TailSize * .35f * tail, Vector3.up);
                    p = bounds.center + swim * (p - bounds.center); n = swim * n;
                }
                if (winged)
                {
                    float wing = Smooth(.12f, .43f, Mathf.Abs(p.x - bounds.center.x) / width);
                    float side = p.x < bounds.center.x ? -1f : 1f;
                    Vector3 pivot = new Vector3(bounds.center.x + side * width * .1f, bounds.center.y, bounds.center.z);
                    Quaternion flap = Quaternion.AngleAxis(side * Mathf.Sin(time * profile.WingRate * Mathf.PI * 2f + phase) * profile.WingSize * .45f * wing, Vector3.forward);
                    p = pivot + flap * (p - pivot); n = flap * n;
                }
                // Preserve the supplied floor/base vertices exactly, including rigid Ponyta/Tauros feet.
                p = Vector3.Lerp(positions[i], p, baseLock);
                output[i] = modelToMesh.MultiplyPoint3x4(p);
                normalOutput[i] = normalsToMesh.MultiplyVector(n).normalized;
            }
            instance.vertices = output; instance.normals = normalOutput; instance.RecalculateBounds();
        }

        public void Restore() { if (instance != null) { instance.vertices = source.vertices; instance.normals = source.normals; instance.RecalculateBounds(); } }
        public void Dispose() { if (filter != null) filter.sharedMesh = source; if (instance != null) { if (Application.isPlaying) Object.Destroy(instance); else Object.DestroyImmediate(instance); } }
        static float Smooth(float from, float to, float value) { float t = Mathf.InverseLerp(from, to, value); return t * t * (3f - 2f * t); }
    }
}
