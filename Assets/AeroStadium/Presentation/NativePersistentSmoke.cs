using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Persistent atlas clouds emitted by native body chimneys, outside the animated model hierarchy.</summary>
    [DefaultExecutionOrder(160)]
    [DisallowMultipleComponent]
    public sealed class NativePersistentSmoke : MonoBehaviour
    {
        [Serializable] public sealed class Vent
        {
            public Transform head;
            public Vector3 headLocalPoint;
            public Vector3 headLocalDirection;
            [NonSerialized] public float emission;
        }

        [SerializeField] NativePokemonModel nativeModel;
        [SerializeField] Material sourceMaterial;
        [SerializeField] Renderer[] legacySmoke = Array.Empty<Renderer>();
        [SerializeField] Renderer[] bodyRenderers = Array.Empty<Renderer>();
        [SerializeField] Vent[] vents = Array.Empty<Vent>();
        [SerializeField] float opacity = .72f;
        [SerializeField] float emissionPerVent = .8f;
        [SerializeField] float lifetime = 4.5f;
        [SerializeField] float plateau = 3f;
        const int Capacity = 96;

        struct Cloud
        {
            public bool alive;
            public float born, radius, variation;
            public Vector3 origin, right, forward, drift, outward;
        }

        Cloud[] clouds;
        Vector3[] vertices;
        Vector4[] controls;
        Vector2[] lives;
        bool[] previousForceOff;
        Mesh cloudMesh;
        Material cloudMaterial;
        GameObject cloudObject;
        MeshRenderer cloudRenderer;
        System.Random random;
        int nextSlot;
        bool ready, diagnosed, diagnosticEnabled;
        float startedAt;
        static readonly Vector2[] Corners = { new Vector2(0,0), new Vector2(0,1), new Vector2(1,0), new Vector2(1,1) };

        // Called after NativePokemonModel.Configure, while the importer still owns Idle(0).
        public void Configure(NativePokemonModel model)
        {
            if (model == null || (model.Species != 109 && model.Species != 110))
                throw new ArgumentException("Persistent native smoke is configured only for 109/110.", nameof(model));
            ReleaseRuntime();
            nativeModel = model;
            Transform root = model.ModelRoot;
            var source = new List<Renderer>(); var bodies = new List<Renderer>();
            sourceMaterial = null;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.Contains("_BodySkin")) bodies.Add(renderer);
                if (!renderer.name.Contains("_SmokeGeom")) continue;
                foreach (Material material in renderer.sharedMaterials)
                    if (material != null && material.shader != null && material.shader.name == "AeroStadium/NativeSmokeCloud")
                    {
                        source.Add(renderer);
                        if (sourceMaterial == null) sourceMaterial = material;
                        break;
                    }
            }
            if (sourceMaterial == null || bodies.Count == 0)
                throw new InvalidOperationException("Native body/cloud material missing for persistent smoke " + model.Species);
            legacySmoke = source.ToArray(); bodyRenderers = bodies.ToArray();
            vents = NativeSmokeVentLayout.Resolve(root, model.Species);
            if (Application.isPlaying && isActiveAndEnabled) CreateRuntime();
        }

        void Awake() { if (Application.isPlaying && isActiveAndEnabled) CreateRuntime(); }
        void OnEnable() { if (Application.isPlaying) { CreateRuntime(); SuppressLegacy(true); if (cloudRenderer != null) cloudRenderer.enabled = true; } }
        void OnDisable()
        {
            SuppressLegacy(false);
            if (cloudRenderer != null) cloudRenderer.enabled = false;
            if (clouds != null) Array.Clear(clouds, 0, clouds.Length);
        }
        void OnDestroy() => ReleaseRuntime();

        void CreateRuntime()
        {
            if (ready || nativeModel == null || sourceMaterial == null || vents.Length == 0) return;
            clouds = new Cloud[Capacity]; vertices = new Vector3[Capacity*4];
            controls = new Vector4[Capacity*4]; lives = new Vector2[Capacity*4];
            previousForceOff = new bool[legacySmoke.Length];
            for (int i = 0; i < legacySmoke.Length; i++) previousForceOff[i] = legacySmoke[i] != null && legacySmoke[i].forceRenderingOff;
            random = new System.Random(nativeModel.Species*7919 + 317);
            diagnosticEnabled = Array.IndexOf(Environment.GetCommandLineArgs(),"--smoke-diagnostics") >= 0;
            startedAt = Time.time; diagnosed=false;
            for (int i = 0; i < vents.Length; i++) vents[i].emission = i / (float)vents.Length;
            cloudMaterial = new Material(sourceMaterial) { name = sourceMaterial.name + "_Persistent", hideFlags = HideFlags.DontSave };
            cloudMaterial.SetFloat("_Opacity", opacity);
            cloudMaterial.SetFloat("_BillboardScale", 1f); cloudMaterial.SetFloat("_PuffRadiusScale", 1f);
            cloudMaterial.SetVector("_CloudWorldOffset", Vector4.zero);
            cloudMaterial.SetFloat("_NativeSmokeBillboard", 0f);
            cloudMesh = new Mesh { name = "NativePersistentSmoke", hideFlags = HideFlags.DontSave };
            cloudMesh.MarkDynamic(); cloudMesh.vertices = vertices;
            int[] triangles = new int[Capacity*6];
            for (int i = 0; i < Capacity; i++)
            {
                int v=i*4,t=i*6; triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+2;
                triangles[t+3]=v+2; triangles[t+4]=v+1; triangles[t+5]=v+3;
            }
            cloudMesh.SetIndices(triangles, MeshTopology.Triangles, 0, false);
            cloudMesh.SetUVs(4, controls); cloudMesh.SetUVs(5, lives);
            cloudObject = new GameObject("PersistentSmoke");
            // Keep authored clouds out of ModelRoot so native body/bounds validation excludes them.
            cloudObject.transform.SetParent(nativeModel.transform, false);
            cloudObject.layer = bodyRenderers.Length > 0 && bodyRenderers[0] != null ? bodyRenderers[0].gameObject.layer : gameObject.layer;
            cloudObject.AddComponent<MeshFilter>().sharedMesh = cloudMesh;
            cloudRenderer = cloudObject.AddComponent<MeshRenderer>(); cloudRenderer.sharedMaterial = cloudMaterial;
            cloudRenderer.shadowCastingMode = ShadowCastingMode.Off; cloudRenderer.receiveShadows = false;
            ready = true; SuppressLegacy(true);
        }

        void SuppressLegacy(bool suppress)
        {
            if (!ready || previousForceOff == null) return;
            for (int i = 0; i < legacySmoke.Length; i++)
                if (legacySmoke[i] != null) legacySmoke[i].forceRenderingOff = suppress || previousForceOff[i];
        }

        bool BodyVisible()
        {
            foreach (Renderer renderer in bodyRenderers)
                if (renderer != null && renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeInHierarchy) return true;
            return false;
        }

        void LateUpdate()
        {
            if (!ready) return;
            SuppressLegacy(true);
            float now = Time.time;
            bool emit = BodyVisible();
            for (int i = 0; i < vents.Length; i++)
            {
                Vent vent = vents[i];
                if (!emit || vent.head == null) { vent.emission = 0f; continue; }
                vent.emission += Mathf.Min(Time.deltaTime, .5f)*emissionPerVent;
                while (vent.emission >= 1f) { vent.emission -= 1f; Spawn(vent, now); }
            }
            Bounds envelope = new Bounds(); bool hasCloud = false; int alive = 0;
            Matrix4x4 worldToMesh = cloudObject.transform.worldToLocalMatrix;
            for (int i = 0; i < Capacity; i++)
            {
                Cloud cloud = clouds[i]; float age = now-cloud.born;
                if (cloud.alive && age >= lifetime) { cloud.alive=false; clouds[i]=cloud; }
                float alpha = 0f, radius = 0f; Vector3 center = Vector3.zero;
                if (cloud.alive)
                {
                    alive++;
                    float fadeDuration = Mathf.Max(.01f,lifetime-plateau);
                    alpha = Mathf.SmoothStep(0f,1f,age/.22f)*(1f-Mathf.SmoothStep(0f,1f,(age-plateau)/fadeDuration));
                    float phase = cloud.variation*Mathf.PI*2f;
                    float curl = nativeModel.WorldModelHeight*.035f;
                    center = cloud.origin + cloud.drift*age
                        + cloud.outward*(1f-Mathf.Exp(-age*1.8f))
                        + cloud.right*((Mathf.Sin(age*1.3f+phase)-Mathf.Sin(phase))*curl)
                        + cloud.forward*((Mathf.Cos(age*1.1f+phase)-Mathf.Cos(phase))*curl);
                    radius = cloud.radius*(1f+1.4f*Mathf.SmoothStep(0f,1f,age/lifetime));
                    Vector3 localCenter = worldToMesh.MultiplyPoint3x4(center);
                    // Camera-plane corners fit inside a sqrt(2)*radius world envelope.
                    float extent = radius*1.414214f;
                    Vector3 half = Abs(worldToMesh.MultiplyVector(Vector3.right))*extent
                        + Abs(worldToMesh.MultiplyVector(Vector3.up))*extent + Abs(worldToMesh.MultiplyVector(Vector3.forward))*extent;
                    Bounds card = new Bounds(localCenter,half*2f);
                    if (!hasCloud) { envelope=card; hasCloud=true; } else { envelope.Encapsulate(card.min); envelope.Encapsulate(card.max); }
                }
                Vector3 meshCenter = worldToMesh.MultiplyPoint3x4(center);
                for (int corner = 0; corner < 4; corner++)
                {
                    int v=i*4+corner; Vector2 rg = Corners[corner];
                    vertices[v]=meshCenter; controls[v]=new Vector4(rg.x,rg.y,radius,cloud.variation);
                    lives[v]=new Vector2(alpha,1f);
                }
            }
            cloudMesh.SetVertices(vertices); cloudMesh.SetUVs(4,controls); cloudMesh.SetUVs(5,lives);
            cloudMesh.bounds = hasCloud ? envelope : new Bounds(Vector3.zero,Vector3.one*.01f);
            cloudRenderer.enabled = alive > 0;
            if (!diagnosed && diagnosticEnabled && Time.time-startedAt >= lifetime+.1f)
            {
                diagnosed=true;
                Debug.Log($"[smoke-persistent] species={nativeModel.Species} vents={vents.Length} alive={alive} bodyVisible={emit} legacySuppressed={legacySmoke.Length} rendererVisible={cloudRenderer.isVisible} opacity={opacity:F2} lifetime={lifetime:F1} plateau={plateau:F1} ratePerVent={emissionPerVent:F1} worldBounds={cloudRenderer.bounds.min.ToString("F4")}..{cloudRenderer.bounds.max.ToString("F4")}");
                for (int i = 0; i < vents.Length; i++)
                    Debug.Log($"[smoke-persistent-vent] species={nativeModel.Species} vent={i} head={vents[i].head.name} world={vents[i].head.TransformPoint(vents[i].headLocalPoint).ToString("F4")}");
            }
        }

        void Spawn(Vent vent, float now)
        {
            int slot = nextSlot; nextSlot=(nextSlot+1)%Capacity;
            for (int i = 0; i < Capacity; i++) if (!clouds[i].alive) { slot=i; break; }
            float height = nativeModel.WorldModelHeight;
            Vector3 up=nativeModel.transform.up, right=nativeModel.transform.right, forward=nativeModel.transform.forward;
            float variation=(float)random.NextDouble();
            Vector3 jitter=(right*((float)random.NextDouble()-.5f)+forward*((float)random.NextDouble()-.5f))*height*.006f;
            Vector3 outward=vent.head.TransformDirection(vent.headLocalDirection).normalized;
            clouds[slot]=new Cloud { alive=true, born=now, variation=variation,
                origin=vent.head.TransformPoint(vent.headLocalPoint)+outward*(height*.012f)+jitter,
                radius=height*(.036f+(float)random.NextDouble()*.008f), right=right, forward=forward,
                outward=outward*(height*.06f),
                drift=up*(height*.05f)+right*((variation-.5f)*height*.018f)+forward*(((float)random.NextDouble()-.5f)*height*.018f) };
        }

        static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x),Mathf.Abs(value.y),Mathf.Abs(value.z));

        void ReleaseRuntime()
        {
            SuppressLegacy(false);
            if (cloudObject != null) Destroy(cloudObject);
            if (cloudMesh != null) Destroy(cloudMesh);
            if (cloudMaterial != null) Destroy(cloudMaterial);
            cloudObject=null; cloudMesh=null; cloudMaterial=null; cloudRenderer=null; ready=false;
        }
    }
}
