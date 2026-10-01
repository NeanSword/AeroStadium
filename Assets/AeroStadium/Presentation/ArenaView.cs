using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AeroStadium.Presentation
{
    /// <summary>Original procedural architecture, separate from combat rules.</summary>
    public sealed class ArenaView : MonoBehaviour
    {
        readonly GameObject[] pokemon = new GameObject[2];
        readonly int[] shownSpecies = new int[2];
        readonly Vector3[] homes = { new Vector3(-4.4f, 0, 0), new Vector3(4.4f, 0, 0) };
        public int LoadedModels { get; private set; }
        public Camera ArenaCamera { get; private set; }

        public void Build()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.58f, .7f, .9f);
            RenderSettings.ambientEquatorColor = new Color(.36f, .47f, .6f);
            RenderSettings.ambientGroundColor = new Color(.16f, .2f, .28f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.07f, .13f, .23f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .015f;
            var cameraObject = new GameObject("Arena camera");
            ArenaCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(-.3f, 7.4f, -15.5f);
            cameraObject.transform.LookAt(new Vector3(0, 1.25f, 0));
            ArenaCamera.fieldOfView = 43;
            ArenaCamera.backgroundColor = new Color(.06f, .12f, .22f);
            ArenaCamera.clearFlags = CameraClearFlags.SolidColor;
            ArenaCamera.farClipPlane = 120;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            Light key = new GameObject("Sun").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.7f;
            key.color = new Color(1f, .89f, .72f); key.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(38, -38, 0);
            Light fill = new GameObject("Sky fill").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .5f;
            fill.color = new Color(.52f, .72f, 1f); fill.transform.rotation = Quaternion.Euler(25, 135, 0);
            var volume = new GameObject("Arena color").AddComponent<Volume>();
            volume.isGlobal = true; volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = volume.profile.Add<Bloom>(); bloom.intensity.Override(.22f); bloom.threshold.Override(1.1f);
            volume.profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);

            var grass = Material(new Color(.09f, .34f, .31f), .16f);
            var metal = Material(new Color(.12f, .22f, .35f), .38f, .4f);
            var pale = Material(new Color(.69f, .79f, .86f), .35f, .35f);
            var gold = Material(new Color(.91f, .66f, .24f), .4f, .5f);
            var dark = Material(new Color(.055f, .10f, .18f), .25f);
            var cyan = Material(new Color(.18f, .72f, .8f), .5f, .2f, true);
            Part(PrimitiveType.Cylinder, "Raised arena", new Vector3(0, -.35f, 0), new Vector3(18.5f, .25f, 18.5f), metal);
            Part(PrimitiveType.Cylinder, "Playing field", new Vector3(0, -.10f, 0), new Vector3(18f, .02f, 18f), grass);
            Ring("Outer boundary", Vector3.zero, 8.6f, .07f, pale);
            Ring("Inner boundary", Vector3.zero, 7.95f, .035f, gold);
            Ring("Center mark", Vector3.zero, 1.6f, .04f, pale);
            Stroke("Midfield", new Vector3(0, .025f, -8.45f), new Vector3(0, .025f, 8.45f), .045f, pale);
            foreach (Vector3 home in homes) { Ring("Battle position", home, 1.5f, .045f, cyan); }
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2 / 32;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 9.35f, .05f, Mathf.Sin(angle) * 9.35f);
                Part(PrimitiveType.Cube, "Perimeter lamp", p, new Vector3(.2f, .16f, .35f), cyan,
                     Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0));
            }
            // Custom fan-shaped terraces and floating roof. No Stadium ROM mesh.
            for (int sector = 0; sector < 18; sector++)
            {
                float angle = (15 + sector * 9) * Mathf.Deg2Rad;
                for (int row = 0; row < 5; row++)
                {
                    float radius = 11.4f + row * 1.45f;
                    Vector3 p = new Vector3(Mathf.Cos(angle) * radius, .65f + row * .65f, Mathf.Sin(angle) * radius);
                    Part(PrimitiveType.Cube, "Terrace tier", p, new Vector3(1.3f, .38f, 2.0f),
                         row % 2 == 0 ? metal : dark, Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0));
                    for (int seat = 0; seat < 3; seat++)
                    {
                        Vector3 seatOffset = new Vector3(Mathf.Sin(angle), 0, -Mathf.Cos(angle)) * (seat - 1) * .47f;
                        Part(PrimitiveType.Cube, "Seat", p + seatOffset + Vector3.up * .35f,
                             new Vector3(.33f, .22f, .38f), sector % 3 == 0 ? gold : pale,
                             Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0));
                    }
                }
            }
            for (int i = 0; i < 7; i++)
            {
                float angle = (25 + i * 22) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 16, 4.6f, Mathf.Sin(angle) * 16);
                Part(PrimitiveType.Cube, "Roof support", p, new Vector3(.5f, 9, .6f), pale);
            }
            Ring("Floating roof edge", new Vector3(0, 9.2f, 0), 17.4f, .18f, cyan, 16, 168);
            Part(PrimitiveType.Cube, "Scoreboard", new Vector3(0, 4.6f, 18), new Vector3(7.6f, 1.7f, .35f), dark);
            Label("AEROSTADIUM", new Vector3(0, 4.85f, 17.78f), .18f, gold.color);
            Label("ARÈNE AÉRO", new Vector3(0, 4.15f, 17.78f), .085f, Color.white);
        }

        public void ShowPokemon(int side, int species, bool preview = false)
        {
            if (pokemon[side] != null) Destroy(pokemon[side]);
            var prefab = Resources.Load<GameObject>("LocalModels/" + species + "/Pokemon");
            if (prefab == null)
            {
                Debug.LogError("Model prefab missing for species " + species);
                return;
            }
            Vector3 pos = preview ? new Vector3(1.8f, 0, 0) : homes[side];
            // Prepared Switch meshes face local -Z. Turn the heads toward the
            // opposing battle position, keeping a slight view of both faces.
            pokemon[side] = Instantiate(prefab, pos, Quaternion.Euler(0, preview ? 25 : side == 0 ? -75 : 75, 0));
            pokemon[side].name = "Pokemon_" + side + "_" + species;
            shownSpecies[side] = species;
            pokemon[side].AddComponent<ModelIdle>();
            LoadedModels = (pokemon[0] != null ? 1 : 0) + (pokemon[1] != null ? 1 : 0);
            FramePokemon(preview);
        }

        public void ClearPokemon(int side)
        {
            if (pokemon[side] != null) Destroy(pokemon[side]);
            pokemon[side] = null;
            shownSpecies[side] = 0;
            LoadedModels = (pokemon[0] != null ? 1 : 0) + (pokemon[1] != null ? 1 : 0);
        }

        void FramePokemon(bool preview)
        {
            bool small = true;
            foreach (int species in shownSpecies) if (species != 0 && species != 152) small = false;
            // Move the camera for smaller species; preserve the model's actual
            // size so a future mixed-size matchup retains its proportions.
            ArenaCamera.transform.position = small
                ? (preview ? new Vector3(0, 2.4f, -6.5f) : new Vector3(0, 3.7f, -9.5f))
                : new Vector3(-.3f, 7.4f, -15.5f);
            ArenaCamera.transform.LookAt(new Vector3(0, small ? (preview ? .45f : .8f) : 1.25f, 0));
        }

        public IEnumerator Attack(int side, string type)
        {
            if (pokemon[side] == null) yield break;
            Vector3 origin = pokemon[side].transform.position;
            Vector3 direction = (homes[1 - side] - homes[side]).normalized;
            float height = shownSpecies[side] == 152 ? .6f : 2.4f;
            Color color = type == "Fire" ? new Color(1, .45f, .1f) : type == "Grass" ? new Color(.4f, 1, .5f) : new Color(.4f, .75f, 1);
            var particle = Part(PrimitiveType.Sphere, "Attack pulse", origin + Vector3.up * height,
                                Vector3.one * .22f, Material(color, .5f, 0, true));
            for (float time = 0; time < .45f; time += Time.deltaTime)
            {
                float t = time / .45f;
                if (pokemon[side] != null) pokemon[side].transform.position = origin + direction * (Mathf.Sin(t * Mathf.PI) * .32f);
                particle.transform.position = Vector3.Lerp(origin + Vector3.up * height, homes[1 - side] + Vector3.up * height, t);
                particle.transform.localScale = Vector3.one * Mathf.Lerp(.22f, .55f, t);
                yield return null;
            }
            if (pokemon[side] != null) pokemon[side].transform.position = origin;
            Destroy(particle);
        }

        public IEnumerator Hit(int side, int damage)
        {
            if (pokemon[side] == null) yield break;
            Transform root = pokemon[side].transform;
            Quaternion rotation = root.rotation;
            bool small = shownSpecies[side] == 152;
            Label("−" + damage, root.position + Vector3.up * (small ? 1.2f : 4.1f), small ? .08f : .14f,
                new Color(1, .72f, .4f), true);
            for (float time = 0; time < .22f; time += Time.deltaTime)
            {
                root.rotation = rotation * Quaternion.Euler(0, 0, Mathf.Sin(time * 60) * 3);
                yield return null;
            }
            root.rotation = rotation;
        }

        public IEnumerator Faint(int side)
        {
            if (pokemon[side] == null) yield break;
            // A provisional disappearance, independent of unavailable source clips.
            var renderers = pokemon[side].GetComponentsInChildren<Renderer>();
            for (int i = 0; i < 4; i++)
            {
                foreach (var renderer in renderers) renderer.enabled = i % 2 != 0;
                yield return new WaitForSeconds(.12f);
            }
            pokemon[side].SetActive(false);
        }

        static Material Material(Color color, float smoothness, float metallic = 0, bool glow = false)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_Metallic", metallic);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.5f); }
            return m;
        }

        GameObject Part(PrimitiveType primitive, string name, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name; part.transform.SetParent(transform); part.transform.position = position;
            part.transform.localScale = scale; part.transform.rotation = rotation ?? Quaternion.identity;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(part.GetComponent<Collider>());
            return part;
        }

        void Ring(string name, Vector3 origin, float radius, float width, Material material, float begin = 0, float end = 360)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(transform);
            line.sharedMaterial = material; line.useWorldSpace = true; line.startWidth = line.endWidth = width;
            line.positionCount = 129;
            for (int i = 0; i < 129; i++) { float a = Mathf.Lerp(begin, end, i / 128f) * Mathf.Deg2Rad; line.SetPosition(i, origin + new Vector3(Mathf.Cos(a) * radius, .025f, Mathf.Sin(a) * radius)); }
        }

        void Stroke(string name, Vector3 a, Vector3 b, float width, Material material)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(transform);
            line.sharedMaterial = material; line.positionCount = 2; line.startWidth = line.endWidth = width;
            line.SetPosition(0, a); line.SetPosition(1, b);
        }

        void Label(string text, Vector3 position, float size, Color color, bool temporary = false)
        {
            var label = new GameObject("Arena lettering").AddComponent<TextMesh>();
            label.transform.SetParent(transform); label.transform.position = position;
            label.transform.rotation = ArenaCamera.transform.rotation;
            label.text = text; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.fontSize = 72; label.characterSize = size; label.color = color;
            if (temporary) Destroy(label.gameObject, 1.1f);
        }

        sealed class ModelIdle : MonoBehaviour
        {
            Vector3 initialScale;
            void Start() { initialScale = transform.localScale; }
            void LateUpdate()
            {
                // Only a root breath; source geometry and source skeleton stay intact.
                // Attacks animate position independently, so this does not override them.
                var animator = GetComponentInChildren<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null) return;
                transform.localScale = initialScale * (1 + Mathf.Sin(Time.time * 1.65f) * .003f);
            }
        }
    }
}
