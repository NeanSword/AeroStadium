using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AeroStadium.EditorTools
{
    public static class AnimationSupportDiagnostics
    {
        [Serializable] sealed class Report { public string utc; public Model[] models; }
        [Serializable] sealed class Model { public int species; public LimbFailure reach; public FloorFailure floor; }
        [Serializable] sealed class LimbFailure
        {
            public string action, upper, knee, end;
            public float time, age, error, reach, requestedDistance, innerReach;
            public Vector3 hip, requested, actual, visualPosition;
        }
        [Serializable] sealed class FloorFailure
        {
            public string action, renderer;
            public int vertex;
            public float time, age, minimumY;
            public Vector3 point;
            public Influence[] influences;
        }
        [Serializable] sealed class Influence
        {
            public string bone;
            public float weight;
            public Vector3 currentBonePosition;
        }

        public static void Diagnose()
        {
            var result = new List<Model>();
            foreach (int species in new[] { 1, 3, 25, 45, 53, 78, 106, 133, 134, 143, 150 })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/AeroStadium/Resources/LocalModels/{species}/Pokemon.prefab");
                if (prefab == null) continue;
                GameObject actor = Object.Instantiate(prefab);
                actor.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                    var motion = actor.GetComponent<CinematicMotion>() ?? actor.AddComponent<CinematicMotion>();
                    motion.Configure(species);
                    var model = new Model { species = species };
                    var feet = typeof(CinematicMotion).GetField("feet", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(motion) as System.Collections.IEnumerable;
                    foreach (PokemonMotionAction action in Enum.GetValues(typeof(PokemonMotionAction)))
                    foreach (float sample in action == PokemonMotionAction.Idle
                        ? new[] { .37f, .93f, 1.61f, 2.79f, 4.13f, 6.07f }
                        : new[] { 0f, .15f, .4f, .7f, 1.3f, 2f })
                    {
                        float time = 1.61f + sample;
                        motion.SamplePose(time, action, sample);
                        int index = 0;
                        foreach (object foot in feet)
                        {
                            Type type = foot.GetType();
                            var upper = (Transform)type.GetField("Upper").GetValue(foot);
                            var knee = (Transform)type.GetField("Knee").GetValue(foot);
                            var end = (Transform)type.GetField("End").GetValue(foot);
                            Vector3 hip = actor.transform.InverseTransformPoint(upper.position);
                            Vector3 requested = actor.transform.InverseTransformPoint(motion.DesiredFootPosition(index++));
                            Vector3 actual = actor.transform.InverseTransformPoint(end.position);
                            float a = actor.transform.InverseTransformVector(knee.position - upper.position).magnitude;
                            float b = actor.transform.InverseTransformVector(end.position - knee.position).magnitude;
                            float error = Vector3.Distance(actual, requested);
                            if (model.reach == null || error > model.reach.error)
                                model.reach = new LimbFailure { action = action.ToString(), time = time, age = sample,
                                    upper = upper.name, knee = knee.name, end = end.name,
                                    hip = hip, requested = requested, actual = actual, error = error,
                                    reach = a + b, requestedDistance = Vector3.Distance(hip, requested), innerReach = Mathf.Abs(a - b),
                                    visualPosition = actor.transform.GetChild(0).localPosition };
                        }
                        foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>(true))
                        {
                            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                            Mesh mesh = null;
                            bool temporary = renderer is SkinnedMeshRenderer;
                            var skin = renderer as SkinnedMeshRenderer;
                            if (skin != null) { mesh = new Mesh(); skin.BakeMesh(mesh, true); }
                            else if (renderer.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                            if (mesh == null) continue;
                            Vector3[] vertices = mesh.vertices;
                            try
                            {
                                for (int i = 0; i < vertices.Length; i++)
                                {
                                    Vector3 point = actor.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertices[i]));
                                    if (model.floor != null && point.y >= model.floor.minimumY) continue;
                                    model.floor = new FloorFailure { action = action.ToString(), time = time, age = sample,
                                        renderer = renderer.name, vertex = i, minimumY = point.y, point = point,
                                        influences = skin == null ? Array.Empty<Influence>() : DescribeWeights(actor, skin, i) };
                                }
                            }
                            finally { if (temporary) Object.DestroyImmediate(mesh); }
                        }
                    }
                    result.Add(model);
                }
                finally { Object.DestroyImmediate(actor); }
            }
            Directory.CreateDirectory("output/animations");
            File.WriteAllText("output/animations/gen1-support-diagnostics.json", JsonUtility.ToJson(new Report
                { utc = DateTime.UtcNow.ToString("O"), models = result.ToArray() }, true));
            Debug.Log("[support-diagnostics] output/animations/gen1-support-diagnostics.json");
        }

        static Influence[] DescribeWeights(GameObject actor, SkinnedMeshRenderer skin, int vertex)
        {
            var weights = skin.sharedMesh.boneWeights;
            if (vertex >= weights.Length) return Array.Empty<Influence>();
            var w = weights[vertex];
            var result = new List<Influence>();
            Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1);
            Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
            void Add(int index, float weight)
            {
                if (weight <= 0f || index < 0 || index >= skin.bones.Length || skin.bones[index] == null) return;
                result.Add(new Influence { bone = skin.bones[index].name, weight = weight,
                    currentBonePosition = actor.transform.InverseTransformPoint(skin.bones[index].position) });
            }
            return result.ToArray();
        }
    }
}
