using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AeroStadium.EditorTools
{
    /// <summary>Analytic world-space regression cases; no game scene, physics or NUnit required.</summary>
    public static class EnergyBallGeometryValidation
    {
        const float Tolerance = .0001f;
        const string DefaultOutput = "output/ui/energy-ball-20261022-v1/geometry-tests.json";
        enum Fixture { Triangle, Transformed, NoGeometry, EmptyTopology }
        sealed class Case
        {
            public string name, failure;
            public Vector3 from, to, center, point, normal;
            public float radius;
            public bool hit, geometry = true, failed;
            public int minimumTriangleTests;
            public Fixture fixture;
        }
        [Serializable] sealed class State
        {
            public bool hit, geometryAvailable, queryFailed;
            public Vector3 center, point, normal;
            public float surfaceError;
            public string failure;
        }
        [Serializable] sealed class Result
        {
            public string name, input, error, meshName;
            public bool passed;
            public int expectedMinimumTriangleTests;
            public long actualTriangleTests;
            public State expected, actual;
        }
        [Serializable] sealed class Report
        {
            public string utc, unityVersion;
            public bool passed;
            public float tolerance = Tolerance;
            public List<Result> cases = new List<Result>();
        }

        public static void RunAndBuild()
        {
            Validate();
            BattleMusicImport.Configure();
            ModernArenaValidation.BuildWindows();
        }

        [MenuItem("AeroStadium/Validate Energy Ball Geometry")]
        public static void Validate()
        {
            var report = new Report { utc = DateTime.UtcNow.ToString("o"), unityVersion = Application.unityVersion, passed = true };
            var failures = new List<string>();
            foreach (var test in Cases())
            {
                Result result = Check(test);
                report.cases.Add(result);
                if (!result.passed) { report.passed = false; failures.Add(test.name + ": " + result.error); }
                Debug.Log("[energy-ball-geometry] " + test.name + " passed=" + result.passed);
            }
            string output = OutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, JsonUtility.ToJson(report, true));
            if (!report.passed) throw new InvalidOperationException("Energy Ball geometry failed: " + string.Join("; ", failures));
            Debug.Log("[energy-ball-geometry] All " + report.cases.Count + " cases passed. " + output);
        }

        static IEnumerable<Case> Cases()
        {
            // The sole triangle is (0,0,0), (4,0,0), (0,4,0); all expectations are analytic.
            yield return Hit("face-front-radius-offset", V(1,1,2), V(1,1,-2), .5f, V(1,1,.5f), V(1,1,0), Vector3.forward);
            yield return Hit("face-back-radius-offset", V(1,1,-2), V(1,1,2), .5f, V(1,1,-.5f), V(1,1,0), Vector3.back);
            // sqrt(.5^2-.3^2)=.4; the projected center is outside the face, above edge AB.
            yield return Hit("edge-only-contact", V(2,-.3f,2), V(2,-.3f,-2), .5f, V(2,-.3f,.4f), V(2,0,0), V(0,-.6f,.8f));
            // .2^2+.2^2+.1^2=.3^2; both adjacent edge projections lie beyond endpoint A.
            yield return Hit("vertex-only-contact", V(-.2f,-.2f,2), V(-.2f,-.2f,-2), .3f, V(-.2f,-.2f,.1f), Vector3.zero, V(-2f/3,-2f/3,1f/3));
            yield return Hit("grazing-edge-tangent", V(2,-.5f,2), V(2,-.5f,-2), .5f, V(2,-.5f,0), V(2,0,0), Vector3.down);
            yield return Miss("parallel-plane-miss", V(1,1,1), V(2,1,1), .5f);
            // (3,3) is within the triangle AABB but sqrt(2) away from its diagonal edge.
            var outside = Miss("bounds-overlap-outside-triangle", V(3,3,2), V(3,3,-2), .25f);
            outside.minimumTriangleTests = 1; yield return outside;
            yield return Hit("initial-overlap", V(1,1,.25f), V(1,1,-2), .5f, V(1,1,.25f), V(1,1,0), Vector3.forward);
            yield return Hit("zero-motion-overlap", V(1,1,.25f), V(1,1,.25f), .5f, V(1,1,.25f), V(1,1,0), Vector3.forward);
            yield return Miss("zero-motion-separated", V(1,1,1), V(1,1,1), .5f);
            yield return Hit("zero-radius-segment", V(1,1,2), V(1,1,-2), 0, V(1,1,0), V(1,1,0), Vector3.forward);
            var transformed = Hit("nonuniform-scale-and-translation", V(12,0,10), V(12,0,4), .5f, V(12,0,7.5f), V(12,0,7), Vector3.forward);
            transformed.fixture = Fixture.Transformed; yield return transformed;
            yield return Invalid("negative-radius", V(1,1,2), -.5f, Fixture.Triangle, "invalid radius");
            yield return Invalid("nan-radius", V(1,1,2), float.NaN, Fixture.Triangle, "invalid radius");
            yield return Invalid("infinite-radius", V(1,1,2), float.PositiveInfinity, Fixture.Triangle, "invalid radius");
            yield return Invalid("nan-position", V(float.NaN,1,2), .5f, Fixture.Triangle, "nonfinite position");
            yield return Invalid("infinite-position", V(float.PositiveInfinity,1,2), .5f, Fixture.Triangle, "nonfinite position");
            yield return Invalid("no-renderer-geometry", V(1,1,2), .5f, Fixture.NoGeometry, "No animated triangle surface");
            yield return Invalid("visible-mesh-without-triangles", V(1,1,2), .5f, Fixture.EmptyTopology, "no readable triangle geometry");
        }

        static Case Hit(string name, Vector3 from, Vector3 to, float radius, Vector3 center, Vector3 point, Vector3 normal)
            => new Case { name=name, from=from, to=to, radius=radius, hit=true, center=center, point=point, normal=normal, minimumTriangleTests=1 };
        static Case Miss(string name, Vector3 from, Vector3 to, float radius)
            => new Case { name=name, from=from, to=to, radius=radius, center=to, point=to };
        static Case Invalid(string name, Vector3 from, float radius, Fixture fixture, string failure)
        {
            var test = Miss(name, from, V(1,1,-2), radius);
            test.fixture=fixture; test.geometry=false; test.failed=true; test.failure=failure; return test;
        }
        static Vector3 V(float x, float y, float z) => new Vector3(x,y,z);
        static bool Near(Vector3 a, Vector3 b) => (a-b).sqrMagnitude <= Tolerance*Tolerance;

        static Result Check(Case test)
        {
            var expected = new State { hit=test.hit, center=test.center, point=test.point, normal=test.normal,
                geometryAvailable=test.geometry, queryFailed=test.failed, surfaceError=test.hit?0:-1, failure=test.failure };
            var result = new Result { name=test.name, input="from="+test.from.ToString("R")+" to="+test.to.ToString("R")+" radius="+test.radius.ToString("R",CultureInfo.InvariantCulture),
                expected=expected, expectedMinimumTriangleTests=test.minimumTriangleTests };
            GameObject owner=null; Mesh mesh=null; Material material=null; AnimatedSurfaceContact contact=null; Scene scene=default;
            try
            {
                scene=EditorSceneManager.NewPreviewScene();
                owner=new GameObject("Analytic triangle fixture") { hideFlags=HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(owner,scene);
                if (test.fixture != Fixture.NoGeometry)
                {
                    mesh=new Mesh { name="Readable analytic right triangle", hideFlags=HideFlags.HideAndDontSave };
                    mesh.vertices=new[] { Vector3.zero, V(4,0,0), V(0,4,0) };
                    mesh.triangles=test.fixture==Fixture.EmptyTopology?new int[0]:new[] { 0,1,2 }; mesh.RecalculateBounds();
                    owner.AddComponent<MeshFilter>().sharedMesh=mesh;
                    Shader shader=Shader.Find("Hidden/InternalErrorShader");
                    if (shader==null) throw new InvalidOperationException("Built-in fixture shader is missing.");
                    material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
                    owner.AddComponent<MeshRenderer>().sharedMaterial=material;
                    if (test.fixture==Fixture.Transformed) { owner.transform.position=V(10,-3,7); owner.transform.localScale=V(2,3,.5f); }
                }
                contact=new AnimatedSurfaceContact(owner);
                bool hit=contact.TrySweptSphere(test.from,test.to,test.radius,out Vector3 center,out Vector3 point,out Vector3 normal);
                var actual = new State { hit=hit, center=center, point=point, normal=normal, geometryAvailable=contact.SphereGeometryAvailable,
                    queryFailed=contact.SphereQueryFailed, surfaceError=contact.LastSurfaceError, failure=contact.LastFailure };
                result.actual=actual; result.meshName=contact.LastMeshName; result.actualTriangleTests=contact.TriangleTests;
                bool diagnostic=test.failed?!string.IsNullOrEmpty(actual.failure)&&actual.failure.Contains(test.failure):string.IsNullOrEmpty(actual.failure);
                result.passed=actual.hit==expected.hit && actual.geometryAvailable==expected.geometryAvailable && actual.queryFailed==expected.queryFailed
                    && Near(actual.center,expected.center) && Near(actual.point,expected.point) && Near(actual.normal,expected.normal)
                    && Mathf.Abs(actual.surfaceError-expected.surfaceError)<=Tolerance && diagnostic
                    && (!hit || !string.IsNullOrEmpty(result.meshName)) && result.actualTriangleTests>=test.minimumTriangleTests;
                if (!result.passed) result.error="Expected/actual state or triangle-test count differs; see recorded values.";
            }
            catch (Exception exception) { result.passed=false; result.error=exception.ToString(); }
            finally
            {
                contact?.Dispose();
                if (owner!=null) UnityEngine.Object.DestroyImmediate(owner);
                if (mesh!=null) UnityEngine.Object.DestroyImmediate(mesh);
                if (material!=null) UnityEngine.Object.DestroyImmediate(material);
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            }
            return result;
        }

        static string OutputPath()
        {
            string path=DefaultOutput;
            string[] args=Environment.GetCommandLineArgs();
            for (int i=0;i<args.Length;i++)
                if (args[i]=="--geometry-output")
                {
                    if (i+1==args.Length || string.IsNullOrWhiteSpace(args[i+1])) throw new ArgumentException("--geometry-output requires a path.");
                    path=args[++i];
                }
            return Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(Application.dataPath,"..",path));
        }
    }
}
