using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Apply native billboard scale and body-following cloud placement after material animation.</summary>
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class NativeSmokeBillboardScale : MonoBehaviour
    {
        [Serializable] public sealed class Target
        {
            public Renderer renderer;
            public int materialIndex;
            public Material material;
            public Transform[] cloudBones = Array.Empty<Transform>();
            public Vector3[] bindCentroids = Array.Empty<Vector3>();
            public float[] boneWeights = Array.Empty<float>();
            public Transform[] headAnchors = Array.Empty<Transform>();
            public Vector3[] headLocalPoints = Array.Empty<Vector3>();
            public Vector3[] headLocalClearances = Array.Empty<Vector3>();
            public float[] headWeights = Array.Empty<float>();
            public Bounds sourceLocalBounds;
            public float maxEncodedRadius;
        }

        [SerializeField] Transform modelRoot;
        [SerializeField] Target[] targets = Array.Empty<Target>();
        MaterialPropertyBlock block;
        NativePokemonModel actor;
        Transform cachedRoot;
        bool diagnosticsEnabled, diagnosed, forceBoundsEnabled, forcedBoundsDiagnosed;
        static readonly int BillboardScale = Shader.PropertyToID("_BillboardScale");
        static readonly int CloudWorldOffset = Shader.PropertyToID("_CloudWorldOffset");
        static readonly int PuffRadiusScale = Shader.PropertyToID("_PuffRadiusScale");

        public void Configure(Transform root)
        {
            modelRoot = root;
            var found = new List<Target>();
            if (root != null)
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++)
                        if (materials[index] != null && materials[index].HasProperty("_NativeSmokeBillboard")
                            && materials[index].GetFloat("_NativeSmokeBillboard") > .5f)
                        {
                            var target = new Target { renderer = renderer, materialIndex = index, material = materials[index] };
                            // Configure is called by the importer at the fixed Idle(0) reference pose.
                            // These source names identify 110; the actor component is added later.
                            if (renderer.name.StartsWith("pm0110_00_00_SmokeGeom", StringComparison.Ordinal))
                                PrepareCloud(target, root);
                            found.Add(target);
                        }
                }
            targets = found.ToArray();
            CacheContext();
            Apply();
        }

        void Awake() { CacheContext(); Apply(); }

        void CacheContext()
        {
            cachedRoot = modelRoot;
            actor = modelRoot != null ? modelRoot.GetComponentInParent<NativePokemonModel>() : null;
            string[] args = Environment.GetCommandLineArgs();
            forceBoundsEnabled = Array.IndexOf(args, "--smoke-force-bounds") >= 0;
            diagnosticsEnabled = forceBoundsEnabled || Array.IndexOf(args, "--smoke-diagnostics") >= 0;
        }

        void LateUpdate()
        {
            Apply();
            if (diagnosed)
            {
                if (forceBoundsEnabled)
                {
                    ForceDiagnosticBounds();
                    if (!forcedBoundsDiagnosed)
                    {
                        forcedBoundsDiagnosed = true;
                        foreach (var target in targets)
                            if (target?.renderer != null && target.headAnchors.Length > 0)
                                Debug.Log($"[smoke-force-bounds] renderer={target.renderer.name} frame={Time.frameCount} enabled={target.renderer.enabled} isVisible={target.renderer.isVisible} worldBounds={Fmt(target.renderer.bounds.min)}..{Fmt(target.renderer.bounds.max)}");
                    }
                }
                return;
            }
            if (!diagnosticsEnabled || Time.frameCount < 120) return;
            diagnosed = true;
            foreach (var target in targets)
            {
                if (target?.renderer == null || target.material == null) continue;
                var skin = target.renderer as SkinnedMeshRenderer;
                Mesh mesh = skin != null ? skin.sharedMesh : target.renderer.GetComponent<MeshFilter>()?.sharedMesh;
                var controls = new List<Vector4>(); mesh?.GetUVs(4, controls);
                int seeds = 0; float maxSize = 0f;
                foreach (var control in controls)
                    if (IsSeed(control)) { seeds++; maxSize = Mathf.Max(maxSize, control.z); }
                float radius = target.material.HasProperty(PuffRadiusScale) ? target.material.GetFloat(PuffRadiusScale) : 0f;
                Debug.Log($"[smoke-live] species={(actor != null ? actor.Species : 0)} renderer={target.renderer.name} enabled={target.renderer.enabled} shader={target.material.shader.name} scale={modelRoot.lossyScale.x:F4} uv4={controls.Count} seedVertices={seeds} maxSize={maxSize:F4} radius={radius:F3} headAnchors={target.headAnchors.Length}");
                DiagnosePlacement(target, mesh, controls);
            }
            if (forceBoundsEnabled) ForceDiagnosticBounds();
        }

        void ForceDiagnosticBounds()
        {
            foreach (var target in targets)
            {
                if (target?.renderer == null || target.headAnchors.Length == 0
                    || !target.renderer.name.StartsWith("pm0110_00_00_SmokeGeom", StringComparison.Ordinal)) continue;
                var skin = target.renderer as SkinnedMeshRenderer;
                if (skin == null) continue;
                Vector3 center = Vector3.zero;
                for (int i = 0; i < target.headAnchors.Length; i++)
                    if (target.headAnchors[i] != null) center += target.headAnchors[i].TransformPoint(target.headLocalPoints[i]) * target.headWeights[i];
                Transform frame = skin.rootBone != null ? skin.rootBone : skin.transform;
                // Diagnostic-only 100 m world envelope. Renderer visibility drivers remain active.
                Bounds local = new Bounds(frame.InverseTransformPoint(center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            local.Encapsulate(frame.InverseTransformPoint(center + new Vector3(x,y,z)*50f));
                skin.localBounds = local;
            }
        }

        // Runs once, only with --smoke-diagnostics. It never changes placement or materials.
        void DiagnosePlacement(Target target, Mesh mesh, List<Vector4> controls)
        {
            // These measurements concern the authored cloud shader only.
            // Native masks on Gastly intentionally have no puff radius/opacity.
            if (target.material.shader == null || target.material.shader.name != "AeroStadium/NativeSmokeCloud") return;
            Renderer renderer = target.renderer;
            var skin = renderer as SkinnedMeshRenderer;
            renderer.GetPropertyBlock(block, target.materialIndex);
            Vector4 actualOffset4 = block.GetVector(CloudWorldOffset);
            Vector3 actualOffset = new Vector3(actualOffset4.x, actualOffset4.y, actualOffset4.z);
            Vector4 materialOffset4 = target.material.HasProperty(CloudWorldOffset) ? target.material.GetVector(CloudWorldOffset) : Vector4.zero;
            Vector3 materialOffset = new Vector3(materialOffset4.x, materialOffset4.y, materialOffset4.z);
            float actualScale = block.HasFloat(BillboardScale) ? block.GetFloat(BillboardScale) : target.material.GetFloat(BillboardScale);
            float actualRadius = block.HasFloat(PuffRadiusScale) ? block.GetFloat(PuffRadiusScale) : target.material.GetFloat(PuffRadiusScale);
            int opacityId = Shader.PropertyToID("_Opacity");
            float opacity = block.HasFloat(opacityId) ? block.GetFloat(opacityId) : target.material.GetFloat(opacityId);
            Vector3 cloudCenter = Vector3.zero, bodyAnchor = Vector3.zero;
            for (int i = 0; i < target.cloudBones.Length; i++)
                if (target.cloudBones[i] != null) cloudCenter += target.cloudBones[i].TransformPoint(target.bindCentroids[i]) * target.boneWeights[i];
            for (int i = 0; i < target.headAnchors.Length; i++)
                if (target.headAnchors[i] != null) bodyAnchor += target.headAnchors[i].TransformPoint(target.headLocalPoints[i]) * target.headWeights[i];
            Texture atlas = target.material.HasProperty("_CloudAtlas") ? target.material.GetTexture("_CloudAtlas") : null;
            Debug.Log($"[smoke-placement] renderer={renderer.name} frame={Time.frameCount} active={renderer.gameObject.activeInHierarchy} enabled={renderer.enabled} isVisible={renderer.isVisible} layer={renderer.gameObject.layer} cloudCenter={Fmt(cloudCenter)} bodyAnchor={Fmt(bodyAnchor)} intendedOffset={Fmt(bodyAnchor-cloudCenter)} actualMPBOffset={Fmt(actualOffset)} materialOffset={Fmt(materialOffset)} mpbHasOffset={block.HasVector(CloudWorldOffset)} shaderHasOffset={target.material.HasProperty(CloudWorldOffset)} actualScale={actualScale:F6} actualRadius={actualRadius:F6} opacity={opacity:F6} rendererWorldBounds={Fmt(renderer.bounds.min)}..{Fmt(renderer.bounds.max)} sourceLocalBounds={Fmt(target.sourceLocalBounds.min)}..{Fmt(target.sourceLocalBounds.max)} skinLocalBounds={(skin != null ? Fmt(skin.localBounds.min)+".."+Fmt(skin.localBounds.max) : "none")} rendererPos={Fmt(renderer.transform.position)} modelPos={Fmt(modelRoot.position)} rootBone={(skin != null && skin.rootBone != null ? skin.rootBone.name+" pos="+Fmt(skin.rootBone.position)+" scale="+Fmt(skin.rootBone.lossyScale) : "none")} queue={target.material.renderQueue} atlas={(atlas != null ? atlas.name+" "+atlas.width+"x"+atlas.height : "null")}");
            block.Clear();
            if (mesh == null || !mesh.isReadable || controls.Count != mesh.vertexCount)
            {
                Debug.Log($"[smoke-skin] renderer={renderer.name} cannotInspect mesh={(mesh != null ? mesh.name : "null")} readable={(mesh != null && mesh.isReadable)} controls={controls.Count}");
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] cpuWorld = new Vector3[vertices.Length], bakedWorld = new Vector3[vertices.Length];
            Vector3 cpuCenter = Vector3.zero, bakedCenter = Vector3.zero;
            Bounds rawLocalRange = new Bounds(), cpuRange = new Bounds(), bakedLocalRange = new Bounds(), bakedRange = new Bounds();
            bool found = false; float maxVertexDifference = 0f; int seedCount = 0;
            Mesh baked = null;
            try
            {
                Matrix4x4[] matrices = null; BoneWeight[] weights = null;
                Vector3[] bakedVertices = vertices;
                if (skin != null)
                {
                    Transform[] bones = skin.bones; Matrix4x4[] poses = mesh.bindposes; weights = mesh.boneWeights;
                    if (bones.Length != poses.Length || weights.Length != vertices.Length)
                    {
                        Debug.Log($"[smoke-skin] renderer={renderer.name} invalidSkin bones={bones.Length} bindposes={poses.Length} weights={weights.Length} vertices={vertices.Length}");
                        return;
                    }
                    matrices = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++) matrices[i] = bones[i].localToWorldMatrix * poses[i];
                    baked = new Mesh { name = "SmokeDiagnosticBake" }; skin.BakeMesh(baked, false); bakedVertices = baked.vertices;
                    if (bakedVertices.Length != vertices.Length)
                    {
                        Debug.Log($"[smoke-skin] renderer={renderer.name} bakedVertexCount={bakedVertices.Length} sourceVertexCount={vertices.Length}");
                        return;
                    }
                }
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (!IsSeed(controls[i])) continue;
                    Vector3 source = renderer.transform.TransformPoint(vertices[i]);
                    if (skin != null)
                    {
                        BoneWeight w = weights[i]; source = Vector3.zero;
                        Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                        void Add(int b, float value) { if (value > 0f) source += matrices[b].MultiplyPoint3x4(vertices[i]) * value; }
                    }
                    Vector3 bakedPoint = renderer.transform.TransformPoint(bakedVertices[i]);
                    cpuWorld[i] = source; bakedWorld[i] = bakedPoint;
                    cpuCenter += source; bakedCenter += bakedPoint; seedCount++;
                    maxVertexDifference = Mathf.Max(maxVertexDifference, Vector3.Distance(source, bakedPoint));
                    if (!found)
                    {
                        found = true;
                        rawLocalRange = new Bounds(vertices[i], Vector3.zero); cpuRange = new Bounds(source, Vector3.zero);
                        bakedLocalRange = new Bounds(bakedVertices[i], Vector3.zero); bakedRange = new Bounds(bakedPoint, Vector3.zero);
                    }
                    else
                    {
                        rawLocalRange.Encapsulate(vertices[i]); cpuRange.Encapsulate(source);
                        bakedLocalRange.Encapsulate(bakedVertices[i]); bakedRange.Encapsulate(bakedPoint);
                    }
                }
                if (!found) return;
                cpuCenter /= seedCount; bakedCenter /= seedCount;
                Debug.Log($"[smoke-skin] renderer={renderer.name} seedVertices={seedCount} sourceLocal={Fmt(rawLocalRange.min)}..{Fmt(rawLocalRange.max)} cpuWorld={Fmt(cpuRange.min)}..{Fmt(cpuRange.max)} bakeLocal={Fmt(bakedLocalRange.min)}..{Fmt(bakedLocalRange.max)} bakeWorld={Fmt(bakedRange.min)}..{Fmt(bakedRange.max)} cachedCentroid={Fmt(cloudCenter)} cpuCentroid={Fmt(cpuCenter)} bakeCentroid={Fmt(bakedCenter)} cachedVsCPU={Vector3.Distance(cloudCenter,cpuCenter):F6} cpuVsBake={Vector3.Distance(cpuCenter,bakedCenter):F6} maxVertexCPUvsBake={maxVertexDifference:F6} rendererScale={Fmt(renderer.transform.lossyScale)}");
                foreach (Camera camera in Camera.allCameras)
                {
                    if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy) continue;
                    Bounds expandedWorld = new Bounds(), viewport = new Bounds(); bool got = false;
                    int inDepth = 0, inViewport = 0, inFrustum = 0;
                    string firstCorners = string.Empty; int reportedCorners = 0;
                    for (int i = 0; i < controls.Count; i++)
                    {
                        if (!IsSeed(controls[i])) continue;
                        Vector4 c = controls[i];
                        Vector3 expanded = bakedWorld[i] + actualOffset
                            + camera.transform.right * ((c.x*2f-1f)*c.z*actualScale*actualRadius)
                            + camera.transform.up * ((c.y*2f-1f)*c.z*actualScale*actualRadius);
                        Vector3 projected = camera.WorldToViewportPoint(expanded);
                        bool depth = projected.z >= camera.nearClipPlane && projected.z <= camera.farClipPlane;
                        bool screen = projected.x >= 0f && projected.x <= 1f && projected.y >= 0f && projected.y <= 1f;
                        if (depth) inDepth++; if (screen) inViewport++; if (depth && screen) inFrustum++;
                        if (!got) { got = true; expandedWorld = new Bounds(expanded,Vector3.zero); viewport = new Bounds(projected,Vector3.zero); }
                        else { expandedWorld.Encapsulate(expanded); viewport.Encapsulate(projected); }
                        if (reportedCorners < 4)
                        {
                            firstCorners += (firstCorners.Length == 0 ? string.Empty : "|") + Fmt(projected);
                            reportedCorners++;
                        }
                    }
                    Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(camera);
                    bool projectedBoundsVisible = GeometryUtility.TestPlanesAABB(frustum, expandedWorld);
                    bool cullingBoundsVisible = GeometryUtility.TestPlanesAABB(frustum, renderer.bounds);
                    bool layerAllowed = (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0;
                    Debug.Log($"[smoke-project] renderer={renderer.name} camera={camera.name} cameraPos={Fmt(camera.transform.position)} cameraRight={Fmt(camera.transform.right)} cameraUp={Fmt(camera.transform.up)} near={camera.nearClipPlane:F4} far={camera.farClipPlane:F2} maskAllowsLayer={layerAllowed} seedCorners={seedCount} inDepth={inDepth} inViewport={inViewport} inFrustum={inFrustum} expandedBoundsInFrustum={projectedBoundsVisible} cullingBoundsInFrustum={cullingBoundsVisible} expandedWorld={Fmt(expandedWorld.min)}..{Fmt(expandedWorld.max)} viewportXYZ={Fmt(viewport.min)}..{Fmt(viewport.max)} firstQuadViewport={firstCorners}");
                }
            }
            catch (Exception exception) { Debug.LogWarning($"[smoke-skin] renderer={renderer.name} diagnosticError={exception.GetType().Name}: {exception.Message}"); }
            finally { if (baked != null) { if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked); } }
        }

        static string Fmt(Vector3 value) => value.ToString("F5");

        public void Apply()
        {
            if (modelRoot == null) return;
            if (cachedRoot != modelRoot) CacheContext();
            block ??= new MaterialPropertyBlock();
            // PokemonCustomNodeAnim supplies this same root scale to the source material.
            float scale = modelRoot.lossyScale.x;
            foreach (var target in targets)
            {
                if (target == null || target.renderer == null) continue;
                target.renderer.GetPropertyBlock(block, target.materialIndex);
                block.SetFloat(BillboardScale, scale);
                Vector3 offset = Vector3.zero;
                if (target.cloudBones.Length > 0 && target.headAnchors.Length > 0)
                {
                    Vector3 cloudCenter = Vector3.zero, bodyAnchor = Vector3.zero;
                    // Fixed bind-space moments reconstruct the actual animated seed centroid.
                    // Neither culling bounds nor a preceding GPU offset contributes to placement.
                    for (int i = 0; i < target.cloudBones.Length; i++)
                        if (target.cloudBones[i] != null)
                            cloudCenter += target.cloudBones[i].TransformPoint(target.bindCentroids[i]) * target.boneWeights[i];
                    for (int i = 0; i < target.headAnchors.Length; i++)
                        if (target.headAnchors[i] != null)
                            bodyAnchor += target.headAnchors[i].TransformPoint(target.headLocalPoints[i]) * target.headWeights[i];
                    offset = bodyAnchor - cloudCenter;
                    UpdateCloudBounds(target, offset, Mathf.Abs(scale));
                }
                block.SetVector(CloudWorldOffset, new Vector4(offset.x, offset.y, offset.z, 0f));
                target.renderer.SetPropertyBlock(block, target.materialIndex);
                block.Clear();
            }
        }

        static bool IsSeed(Vector4 control) => Mathf.Abs(control.x - .5f) > .49f && Mathf.Abs(control.y - .5f) > .49f;

        /// <summary>Read the fixed imported head surfaces without sampling the current animation.</summary>
        public bool TryGetEmissionSurface(Renderer source, out Transform[] heads, out Vector3[] localPoints)
        {
            foreach (var target in targets)
                if (target != null && target.renderer == source && target.headAnchors.Length > 0
                    && target.headAnchors.Length == target.headLocalPoints.Length)
                {
                    heads = target.headAnchors; localPoints = new Vector3[heads.Length];
                    float radius = target.material != null && target.material.HasProperty(PuffRadiusScale) ? target.material.GetFloat(PuffRadiusScale) : 0f;
                    Vector3 clearance = modelRoot.up * (target.maxEncodedRadius * radius * Mathf.Abs(modelRoot.lossyScale.x));
                    for (int i = 0; i < heads.Length; i++)
                    {
                        Vector3 localClearance = target.headLocalClearances.Length == heads.Length
                            ? target.headLocalClearances[i] : heads[i].InverseTransformVector(clearance);
                        localPoints[i] = target.headLocalPoints[i]-localClearance;
                    }
                    return true;
                }
            heads = Array.Empty<Transform>(); localPoints = Array.Empty<Vector3>(); return false;
        }

        static Transform NativeHead(Transform bone)
        {
            for (Transform node = bone; node != null; node = node.parent)
            {
                // The smoke feelers are siblings of SpineA2/SpineB2, under their tr nodes.
                if (node.name == "SpineA2") return node.Find("EffHeadCenter01");
                if (node.name == "SpineB2") return node.Find("EffHeadCenter02");
                if (node.name == "trSpineA2") return node.Find("SpineA2/EffHeadCenter01");
                if (node.name == "trSpineB2") return node.Find("SpineB2/EffHeadCenter02");
            }
            return null;
        }

        static void PrepareCloud(Target target, Transform root)
        {
            var skin = target.renderer as SkinnedMeshRenderer;
            Mesh mesh = skin != null ? skin.sharedMesh : null;
            if (mesh == null || !mesh.isReadable) return;
            Transform[] bones = skin.bones;
            Matrix4x4[] poses = mesh.bindposes;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            var controls = new List<Vector4>(); mesh.GetUVs(4, controls);
            if (bones.Length != poses.Length || weights.Length != vertices.Length || controls.Count != vertices.Length) return;
            var moments = new Vector3[bones.Length]; var totals = new float[bones.Length];
            int seedCount = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!IsSeed(controls[i])) continue;
                seedCount++; target.maxEncodedRadius = Mathf.Max(target.maxEncodedRadius, controls[i].z);
                BoneWeight weight = weights[i]; Vector3 vertex = vertices[i];
                Add(weight.boneIndex0, weight.weight0); Add(weight.boneIndex1, weight.weight1);
                Add(weight.boneIndex2, weight.weight2); Add(weight.boneIndex3, weight.weight3);
                void Add(int bone, float value)
                {
                    if (value <= 0f) return;
                    moments[bone] += poses[bone].MultiplyPoint3x4(vertex) * value; totals[bone] += value;
                }
            }
            if (seedCount == 0) return;
            var usedBones = new List<Transform>(); var points = new List<Vector3>(); var usedWeights = new List<float>();
            var heads = new List<Transform>(); var headWeights = new List<float>();
            for (int i = 0; i < bones.Length; i++)
            {
                if (totals[i] <= 0f) continue;
                Transform head = NativeHead(bones[i]);
                // Leave an unrecognized source skin untouched instead of inventing an anchor.
                if (head == null) return;
                usedBones.Add(bones[i]); points.Add(moments[i] / totals[i]); usedWeights.Add(totals[i] / seedCount);
                int h = heads.IndexOf(head);
                if (h < 0) { h = heads.Count; heads.Add(head); headWeights.Add(0f); }
                headWeights[h] += totals[i] / seedCount;
            }
            Vector3[] surfacePoints = ReferenceHeadSurface(root, heads);
            if (surfacePoints == null) return;
            float radius = target.material.HasProperty(PuffRadiusScale) ? target.material.GetFloat(PuffRadiusScale) : 0f;
            // One authored puff radius clears the body's native surface; no species-height multiplier.
            Vector3 clearance = root.up * (target.maxEncodedRadius * radius * Mathf.Abs(root.lossyScale.x));
            var localClearances = new Vector3[heads.Count];
            for (int i = 0; i < heads.Count; i++)
            {
                localClearances[i] = heads[i].InverseTransformVector(clearance);
                surfacePoints[i] = heads[i].InverseTransformPoint(surfacePoints[i] + clearance);
            }
            target.cloudBones = usedBones.ToArray(); target.bindCentroids = points.ToArray(); target.boneWeights = usedWeights.ToArray();
            target.headAnchors = heads.ToArray(); target.headLocalPoints = surfacePoints; target.headWeights = headWeights.ToArray();
            target.headLocalClearances = localClearances;
            target.sourceLocalBounds = skin.localBounds;
        }

        static Vector3[] ReferenceHeadSurface(Transform root, List<Transform> heads)
        {
            var points = new Vector3[heads.Count]; var top = new float[heads.Count]; var found = new bool[heads.Count];
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name != "pm0110_00_00_BodySkin") continue;
                Mesh mesh = skin.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Transform[] bones = skin.bones; Matrix4x4[] poses = mesh.bindposes;
                Vector3[] vertices = mesh.vertices; BoneWeight[] weights = mesh.boneWeights;
                if (bones.Length != poses.Length || weights.Length != vertices.Length) continue;
                var matrices = new Matrix4x4[bones.Length]; var ownership = new int[bones.Length];
                for (int b = 0; b < bones.Length; b++) { matrices[b] = bones[b].localToWorldMatrix * poses[b]; ownership[b] = heads.IndexOf(NativeHead(bones[b])); }
                for (int v = 0; v < vertices.Length; v++)
                {
                    BoneWeight w = weights[v]; Vector3 point = Vector3.zero;
                    int owner = -1; float strongest = 0f;
                    Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                    if (owner < 0) continue;
                    float y = root.InverseTransformPoint(point).y;
                    if (!found[owner] || y > top[owner]) { found[owner] = true; top[owner] = y; points[owner] = point; }
                    void Add(int b, float value)
                    {
                        if (value <= 0f) return;
                        point += matrices[b].MultiplyPoint3x4(vertices[v]) * value;
                        if (ownership[b] >= 0 && value > strongest) { strongest = value; owner = ownership[b]; }
                    }
                }
            }
            for (int i = 0; i < found.Length; i++) if (!found[i]) return null;
            return points;
        }

        static void UpdateCloudBounds(Target target, Vector3 offset, float scale)
        {
            var skin = target.renderer as SkinnedMeshRenderer;
            if (skin == null) return;
            Transform frame = skin.rootBone != null ? skin.rootBone : skin.transform;
            Bounds bounds = target.sourceLocalBounds;
            Bounds shifted = target.sourceLocalBounds; shifted.center += frame.InverseTransformVector(offset);
            bounds.Encapsulate(shifted.min); bounds.Encapsulate(shifted.max);
            float radius = target.material != null && target.material.HasProperty(PuffRadiusScale) ? target.material.GetFloat(PuffRadiusScale) : 0f;
            Vector3 frameScale = frame.lossyScale;
            float minScale = Mathf.Min(Mathf.Abs(frameScale.x), Mathf.Abs(frameScale.y), Mathf.Abs(frameScale.z));
            if (minScale > .000001f) bounds.Expand(2f * target.maxEncodedRadius * radius * scale / minScale);
            // Rebuild from a serialized reference every frame; expansion never accumulates.
            skin.localBounds = bounds;
        }
    }
}
