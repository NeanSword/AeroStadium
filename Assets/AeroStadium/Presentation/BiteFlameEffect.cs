using UnityEngine;
using UnityEngine.Rendering;

namespace AeroStadium.Presentation
{
    /// <summary>Living fire attached to the two physical jaws; it never launches a projectile.</summary>
    internal sealed class BiteFlameEffect
    {
        const int Rows = 8, OuterTongues = 12, CoreTongues = 8, Sparks = 18;
        sealed class Surface
        {
            public GameObject go;
            public Mesh mesh;
            public MeshRenderer renderer;
            public Vector3[] vertices;
            public Color[] colors;
        }
        Transform head, jaw, mouth;
        Vector3 upperLocal, lowerLocal, forwardLocal;
        GameObject root;
        Surface outer, core, sparks;
        Material outerMaterial, coreMaterial;
        Texture2D coreTexture;
        Light glow;
        float width, createdAt, impactAt = -1;
        Vector3 impactPoint, impactNormal;
        bool disposed, impacted;

        public void Initialize(Transform upperHead, Transform lowerJaw, Transform emitter, Vector3 direction, Material sharedGlow)
        {
            createdAt=Time.time;head = upperHead; jaw = lowerJaw; mouth = emitter;
            upperLocal = head.InverseTransformPoint(mouth.position); lowerLocal = jaw.InverseTransformPoint(mouth.position);
            forwardLocal = head.InverseTransformDirection(direction.normalized);
            width = Mathf.Clamp(Vector3.Distance(head.position, jaw.position) * 2.5f, .44f, .58f);
            root = new GameObject("Croc Feu - living jaw flames");
            coreTexture = CreateCoreTexture(); coreMaterial = CreateCoreMaterial(sharedGlow);
            Shader livingFire = Shader.Find("AeroStadium/Elite Four Living Fire");
            outerMaterial = livingFire != null ? new Material(livingFire) : new Material(coreMaterial);
            outerMaterial.name = "Croc Feu flowing orange mantle";
            if (outerMaterial.HasProperty("_Seed")) outerMaterial.SetFloat("_Seed", 4.24059f);
            if (outerMaterial.HasProperty("_Ember")) outerMaterial.SetFloat("_Ember", 0);
            outerMaterial.renderQueue = 3018;
            outer = CreateSurface("Flowing flame tongues", OuterTongues, Rows, outerMaterial);
            core = CreateSurface("White yellow burning fangs", CoreTongues, Rows, coreMaterial);
            sparks = CreateSurface("Surface contact sparks", Sparks, 2, coreMaterial);
            glow = root.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 1.9f;
            glow.color = new Color(1, .34f, .035f); glow.shadows = LightShadows.None;
            Hide();
        }

        public void Render(float nativeTime, bool impact, Vector3 point, Vector3 normal, bool blocked, float strength=1)
        {
            if (disposed || root == null || head == null || jaw == null || mouth == null) return;
            Vector3 upper = head.TransformPoint(upperLocal), lower = jaw.TransformPoint(lowerLocal);
            Vector3 forward = head.TransformDirection(forwardLocal).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < .5f) right = head.right;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            root.transform.SetPositionAndRotation((upper + lower) * .5f, Quaternion.LookRotation(forward, up));
            if (impact && !impacted)
            {
                impacted = true; impactAt = Time.time; impactPoint = point;
                impactNormal = normal.sqrMagnitude > .1f ? normal.normalized : -forward;
                if (Vector3.Dot(impactNormal, mouth.position - point) < 0) impactNormal = -impactNormal;
            }
            float fire = Mathf.Clamp01(strength) * (1 - Smooth(.65f, .90f, nativeTime));
            float flow=Time.time-createdAt;
            float flicker = .93f + .045f * Mathf.Sin(flow * 31) + .025f * Mathf.Sin(flow * 67 + 1.4f);
            Vector3 upperRoot = root.transform.InverseTransformPoint(upper), lowerRoot = root.transform.InverseTransformPoint(lower);
            DrawTongues(outer, OuterTongues, upperRoot, lowerRoot, flow, fire * flicker, false, impact && blocked);
            DrawTongues(core, CoreTongues, upperRoot, lowerRoot, flow, fire, true, impact && blocked);
            DrawSparks();
            glow.enabled = fire > .01f; glow.intensity = fire * (1.45f + .25f * flicker);
        }

        void DrawTongues(Surface surface, int count, Vector3 upper, Vector3 lower, float time, float fire, bool hot, bool clip)
        {
            surface.renderer.enabled = fire > .005f;
            if (fire <= .005f) return;
            for (int tongue = 0; tongue < count; tongue++)
            {
                bool fang = hot && tongue >= count - 2;
                bool bottom = !fang && (tongue & 1) != 0;
                float seed = tongue * 2.399963f + (hot ? 1.7f : .3f);
                float across = fang ? (tongue == count - 2 ? -.24f : .24f) : Mathf.Sin(seed) * .48f;
                Vector3 origin = (bottom ? lower : upper) + Vector3.right * across * width;
                origin.z += .025f + .025f * Mathf.Cos(seed);
                float height = (hot ? .23f : .36f) + .10f * Hash(tongue + 7);
                if (fang) height = .19f;
                float broad = (hot ? .073f : .105f) * (.82f + .32f * Hash(tongue + 4));
                // Crossed curved sheets retain volume from either battle direction and remain translucent.
                Vector3 sheet = tongue % 3 == 0 ? Vector3.right : new Vector3(.18f * Mathf.Cos(seed), 0, 1).normalized;
                for (int row = 0; row < Rows; row++)
                {
                    float p = row / (float)(Rows - 1);
                    float wave = Mathf.Sin(p * 8 - time * 13 + seed) * .026f
                        + Mathf.Sin(p * 17 - time * 23 + seed * 1.7f) * .010f;
                    float bend = p * p;
                    Vector3 center = origin + Vector3.up * (p * height * (fang ? -1 : 1))
                        + Vector3.back * (bend * .12f + wave * p)
                        + Vector3.right * (wave * p + bend * .035f * Mathf.Sin(seed));
                    // Real taper and asynchronous tips remove the old wire/cube silhouette.
                    float taper = Mathf.Pow(1 - p, .72f) * (.78f + .22f * Mathf.Sin(p * 10 - time * 19 + seed));
                    float half = broad * taper;
                    center = origin + (center - origin) * fire;
                    int index = (tongue * Rows + row) * 2;
                    surface.vertices[index] = Clip(center - sheet * half * fire, clip);
                    surface.vertices[index + 1] = Clip(center + sheet * half * fire, clip);
                    Color heat = hot ? Color.Lerp(new Color(1, .98f, .67f, .90f), new Color(1, .22f, .012f, .12f), p)
                        : Color.Lerp(new Color(1, .56f, .025f, .78f), new Color(.9f, .055f, .002f, .10f), p);
                    heat.a *= fire;
                    surface.colors[index] = surface.colors[index + 1] = heat;
                }
            }
            Upload(surface);
        }

        Vector3 Clip(Vector3 local, bool clip)
        {
            if (!clip) return local;
            Vector3 world = root.transform.TransformPoint(local);
            float distance = Vector3.Dot(world - impactPoint, impactNormal);
            if (distance < .012f) world += impactNormal * (.012f - distance);
            return root.transform.InverseTransformPoint(world);
        }

        void DrawSparks()
        {
            float age = impactAt < 0 ? -1 : Time.time - impactAt;
            sparks.renderer.enabled = age >= 0 && age < .31f;
            if (!sparks.renderer.enabled) return;
            Vector3 tangent = Vector3.Cross(impactNormal, Vector3.up).normalized;
            if (tangent.sqrMagnitude < .5f) tangent = Vector3.right;
            Vector3 vertical = Vector3.Cross(tangent, impactNormal).normalized;
            float fade = 1 - Mathf.Clamp01(age / .31f);
            for (int i = 0; i < Sparks; i++)
            {
                float angle = i * 2.399963f;
                Vector3 direction = (tangent * Mathf.Cos(angle) + vertical * Mathf.Sin(angle) * .72f
                    + impactNormal * (.28f + .54f * Hash(i + 2)) + Vector3.up * .22f).normalized;
                float distance = age * (.7f + .85f * Hash(i + 9));
                Vector3 tip = impactPoint + impactNormal * .018f + direction * distance + Vector3.down * age * age * .65f;
                float plane = Vector3.Dot(tip - impactPoint, impactNormal);
                if (plane < .018f) tip += impactNormal * (.018f - plane);
                Vector3 tail = tip - direction * (.040f + .055f * fade);
                Vector3 side = Vector3.Cross(direction, impactNormal).normalized * (.009f + .007f * fade) * fade;
                int k = i * 4;
                sparks.vertices[k] = root.transform.InverseTransformPoint(tail - side);
                sparks.vertices[k + 1] = root.transform.InverseTransformPoint(tail + side);
                sparks.vertices[k + 2] = root.transform.InverseTransformPoint(tip - side * .15f);
                sparks.vertices[k + 3] = root.transform.InverseTransformPoint(tip + side * .15f);
                Color color = new Color(1, .68f + .27f * Hash(i), .12f, fade * .92f);
                for (int j = 0; j < 4; j++) sparks.colors[k + j] = color;
            }
            Upload(sparks);
        }

        Surface CreateSurface(string name, int strips, int rows, Material material)
        {
            var surface = new Surface(); surface.go = new GameObject(name); surface.go.transform.SetParent(root.transform, false);
            surface.mesh = new Mesh { name = name + " mesh" }; surface.mesh.MarkDynamic();
            surface.vertices = new Vector3[strips * rows * 2]; surface.colors = new Color[surface.vertices.Length];
            var uv = new Vector2[surface.vertices.Length]; var phase = new Vector2[surface.vertices.Length];
            var triangles = new int[strips * (rows - 1) * 6]; int triangle = 0;
            for (int strip = 0; strip < strips; strip++) for (int row = 0; row < rows; row++)
            {
                int k = (strip * rows + row) * 2; float p = row / (float)(rows - 1);
                uv[k] = new Vector2(0, p); uv[k + 1] = new Vector2(1, p);
                phase[k] = phase[k + 1] = new Vector2(strip * 1.719f, 1);
                surface.colors[k] = surface.colors[k + 1] = Color.white;
                if (row == rows - 1) continue;
                triangles[triangle++] = k; triangles[triangle++] = k + 2; triangles[triangle++] = k + 1;
                triangles[triangle++] = k + 1; triangles[triangle++] = k + 2; triangles[triangle++] = k + 3;
            }
            surface.mesh.vertices = surface.vertices; surface.mesh.colors = surface.colors;
            surface.mesh.uv = uv; surface.mesh.uv2 = phase; surface.mesh.triangles = triangles;
            surface.go.AddComponent<MeshFilter>().sharedMesh = surface.mesh;
            surface.renderer = surface.go.AddComponent<MeshRenderer>(); surface.renderer.sharedMaterial = material;
            surface.renderer.shadowCastingMode = ShadowCastingMode.Off; surface.renderer.receiveShadows = false;
            surface.renderer.lightProbeUsage = LightProbeUsage.Off; surface.renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return surface;
        }
        static void Upload(Surface surface)
        { surface.mesh.vertices = surface.vertices; surface.mesh.colors = surface.colors; surface.mesh.RecalculateBounds(); }
        Material CreateCoreMaterial(Material shared)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive");
            Material material = shader != null ? new Material(shader) : new Material(shared);
            material.name = "Croc Feu incandescent core";
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", coreTexture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", coreTexture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(2.2f, 1.8f, 1.15f, 1));
            if (material.HasProperty("_Color")) material.SetColor("_Color", new Color(2.2f, 1.8f, 1.15f, 1));
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3020; return material;
        }
        static Texture2D CreateCoreTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "Original Croc Feu soft flame", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1), v = y / (float)(size - 1);
                float side = Mathf.Abs((u - .5f) * 2);
                float grain = Mathf.PerlinNoise(u * 7.3f + 4.2f, v * 11.6f + 5.9f) * .65f
                    + Mathf.PerlinNoise(u * 17.7f, v * 23.3f) * .35f;
                float alpha = Mathf.Pow(Mathf.Clamp01(1 - side), .65f) * (.72f + grain * .28f);
                alpha *= Smooth(0, .055f, v) * (1 - Smooth(.78f, 1, v));
                Color heat = Color.Lerp(new Color(1, .98f, .70f), new Color(1, .19f, .005f), v * v);
                heat.a = alpha; pixels[y * size + x] = heat;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); return texture;
        }
        static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f + 31.7f) * 43758.5453f, 1);
        static float Smooth(float a, float b, float t) { t = Mathf.InverseLerp(a, b, t); return t * t * (3 - 2 * t); }
        public void Hide()
        {
            if (outer != null) outer.renderer.enabled = false;
            if (core != null) core.renderer.enabled = false;
            if (sparks != null) sparks.renderer.enabled = false;
            if (glow != null) glow.enabled = false;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; Hide();
            if (outer != null && outer.mesh != null) Object.Destroy(outer.mesh);
            if (core != null && core.mesh != null) Object.Destroy(core.mesh);
            if (sparks != null && sparks.mesh != null) Object.Destroy(sparks.mesh);
            if (outerMaterial != null) Object.Destroy(outerMaterial);
            if (coreMaterial != null) Object.Destroy(coreMaterial);
            if (coreTexture != null) Object.Destroy(coreTexture);
            if (root != null) Object.Destroy(root);
        }
    }
}
