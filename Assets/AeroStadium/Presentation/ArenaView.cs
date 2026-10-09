using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AeroStadium.Presentation
{
    /// <summary>Combat presentation with recovered Stadium 2 environments.</summary>
    public sealed class ArenaView : MonoBehaviour
    {
        static Material arenaLitTemplate;
        readonly GameObject[] pokemon = new GameObject[2];
        readonly int[] shownSpecies = new int[2];
        readonly Vector3[] homes = { new Vector3(-4.4f, 0, 0), new Vector3(4.4f, 0, 0) };
        Vector3 battleCenter,battleAxis=Vector3.right;
        float fieldLength,fieldWidth,battleYaw; bool previewMode;
        public bool BattlePositionsValid
        {
            get {
                if(fieldLength<=0)return true;
                Vector3 across=Vector3.Cross(Vector3.up,battleAxis);
                for(int side=0;side<2;side++)if(pokemon[side]!=null){
                    Bounds b=PokemonWorldBounds(side);Vector3 offset=b.center-battleCenter;
                    if(Mathf.Abs(Vector3.Dot(offset,battleAxis))+ProjectedRadius(b,battleAxis)>fieldLength*.5f+.01f||
                       Mathf.Abs(Vector3.Dot(offset,across))+ProjectedRadius(b,across)>fieldWidth*.5f+.01f)return false;
                }
                return true;
            }
        }
        static float ProjectedRadius(Bounds b,Vector3 axis)=>Mathf.Abs(axis.x)*b.extents.x+Mathf.Abs(axis.y)*b.extents.y+Mathf.Abs(axis.z)*b.extents.z;
        public void ConfigureBattleField(Vector3 center,Vector3 axis,float length,float width)
        {
            battleCenter=center;battleAxis=axis.normalized;fieldLength=length;fieldWidth=width;
            battleYaw=Mathf.Atan2(-battleAxis.z,battleAxis.x)*Mathf.Rad2Deg;
            PlaceBattleActors();if(!previewMode)FramePokemon(false);
        }
        void PlaceBattleActors()
        {
            float spacing=fieldLength>0?Mathf.Min(4.4f,fieldLength*.34f):4.4f;
            for(int side=0;side<2;side++){
                float distance=spacing;
                if(pokemon[side]!=null&&!previewMode){
                    pokemon[side].transform.rotation=Quaternion.Euler(0,battleYaw+(side==0?-75:75),0);
                    if(fieldLength>0){Bounds b=PokemonWorldBounds(side);
                        float radius=ProjectedRadius(b,battleAxis)+Mathf.Abs(Vector3.Dot(b.center-pokemon[side].transform.position,battleAxis));
                        distance=Mathf.Min(distance,Mathf.Max(.4f,fieldLength*.5f-radius-.3f));
                    }
                }
                homes[side]=battleCenter+battleAxis*(side==0?-distance:distance);
                if(pokemon[side]!=null&&!previewMode)pokemon[side].transform.position=homes[side];
            }
        }
        public int LoadedModels { get; private set; }
        public Camera ArenaCamera { get; private set; }
        readonly Light[] cinematicSpotlights = new Light[4];

        public StadiumEnvironment Stadium { get; private set; }

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

            Stadium = new GameObject("Recovered Stadium 2 environment").AddComponent<StadiumEnvironment>();
            Stadium.transform.SetParent(transform,false);
            string[] args=System.Environment.GetCommandLineArgs();int stadiumArg=System.Array.IndexOf(args,"--stadium");
            string stadiumKey=stadiumArg>=0&&stadiumArg+1<args.Length?args[stadiumArg+1]:"free_battle";
            Stadium.Load(stadiumKey);BuildCinematicSpotlights();
            if(System.Array.IndexOf(args,"--stadium-original")>=0)
                ArenaCamera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
        }

        void BuildLegacyArchitecture()
        {
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
            BuildCinematicSpotlights();
        }

        public void ShowPokemon(int side, int species, bool preview = false)
        {
            if (pokemon[side] != null) Destroy(pokemon[side]);
            var prefab = PokemonPrefabCatalog.Load(species);
            if (prefab == null)
            {
                Debug.LogError("Model prefab missing for species " + species);
                return;
            }
            previewMode=preview;
            Vector3 pos = preview ? new Vector3(1.8f, 0, 0) : homes[side];
            // Prepared Switch meshes face local -Z. Turn the heads toward the
            // opposing battle position, keeping a slight view of both faces.
            pokemon[side] = Instantiate(prefab, pos, Quaternion.Euler(0, preview ? 25 : battleYaw+(side == 0 ? -75 : 75), 0));
            pokemon[side].name = "Pokemon_" + side + "_" + species;
            shownSpecies[side] = species;
            var driver = pokemon[side].GetComponent<PokemonAnimationDriver>();
            if (driver == null) driver = pokemon[side].AddComponent<PokemonAnimationDriver>();
            var native = pokemon[side].GetComponent<NativePokemonModel>();
            if (native != null) driver.ConfigureNative(native);
            else
            {
                var motion = pokemon[side].AddComponent<CinematicMotion>();
                motion.Configure(species);
                driver.ConfigureAuthored(motion);
            }
            var importedMotion = pokemon[side].GetComponent<CinematicMotion>();
            Bounds local = native != null ? native.RestBounds : importedMotion.RestBounds;
            float height = native != null ? native.ModelHeight : importedMotion.ModelHeight;
            PokemonDisplaySize.Apply(pokemon[side], species, local, height);
            LoadedModels = (pokemon[0] != null ? 1 : 0) + (pokemon[1] != null ? 1 : 0);
            if(!preview)PlaceBattleActors();
            FramePokemon(preview);
        }

        public float PlayAttackAnimation(int side, bool special = false)
        {
            if (pokemon[side] == null) return 0f;
            var driver = pokemon[side].GetComponent<PokemonAnimationDriver>();
            return driver == null ? 0f : driver.PlayAttack(special);
        }

        float PlayDamageAnimation(int side)
        {
            if (pokemon[side] == null) return 0f;
            var driver = pokemon[side].GetComponent<PokemonAnimationDriver>();
            return driver == null ? 0f : driver.PlayDamage();
        }

        float PlayFaintAnimation(int side)
        {
            if (pokemon[side] == null) return 0f;
            var driver = pokemon[side].GetComponent<PokemonAnimationDriver>();
            return driver == null ? 0f : driver.PlayFaint();
        }

        public void ClearPokemon(int side)
        {
            if (pokemon[side] != null) Destroy(pokemon[side]);
            pokemon[side] = null;
            shownSpecies[side] = 0;
            LoadedModels = (pokemon[0] != null ? 1 : 0) + (pokemon[1] != null ? 1 : 0);
        }

        public bool HasPokemonModel(int species)
        {
            return PokemonPrefabCatalog.Load(species) != null;
        }

        void BuildCinematicSpotlights()
        {
            Color[] colors = { new Color(1f, .69f, .35f), new Color(.35f, .76f, 1f), new Color(1f, .88f, .56f), new Color(.52f, .64f, 1f) };
            for (int i = 0; i < cinematicSpotlights.Length; i++)
            {
                float angle = (35f + i * 90f) * Mathf.Deg2Rad;
                var lightObject = new GameObject("Cinematic spotlight " + (i + 1));
                var spot = lightObject.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.color = colors[i];
                spot.intensity = 0f;
                spot.range = 34f;
                spot.spotAngle = 48f;
                spot.shadows = LightShadows.None;
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.position = new Vector3(Mathf.Cos(angle) * 13f, 16f, Mathf.Sin(angle) * 13f);
                lightObject.transform.LookAt(new Vector3(1.8f, .9f, 0f));
                cinematicSpotlights[i] = spot;
            }
        }

        void AnimateCinematicSpotlights(float time)
        {
            for (int i = 0; i < cinematicSpotlights.Length; i++)
            {
                float sweep = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * 1.1f + i * 1.57f)), 3f);
                cinematicSpotlights[i].intensity = 1.25f + sweep * 2.25f;
                float angle = (35f + i * 90f + Mathf.Sin(time * .32f + i) * 7f) * Mathf.Deg2Rad;
                cinematicSpotlights[i].transform.position = new Vector3(Mathf.Cos(angle) * 13f, 16f, Mathf.Sin(angle) * 13f);
                cinematicSpotlights[i].transform.LookAt(new Vector3(1.8f, .9f, 0f));
            }
        }

        public IEnumerator PlayOpeningShot(float duration)
        {
            if (ArenaCamera == null) yield break;
            Vector3 start = new Vector3(-7f, 20f, -25f);
            Vector3 end = new Vector3(-.3f, 9.5f, -20f);
            duration = Mathf.Max(.1f, duration);
            ArenaCamera.fieldOfView = 56f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                ArenaCamera.transform.position = Vector3.Lerp(start, end, t);
                ArenaCamera.transform.LookAt(Vector3.Lerp(new Vector3(0f, 2.1f, 0f), new Vector3(0f, 1.25f, 0f), t));
                ArenaCamera.fieldOfView = Mathf.Lerp(56f, 47f, t);
                AnimateCinematicSpotlights(elapsed);
                yield return null;
            }
            AnimateCinematicSpotlights(duration);
        }
        public IEnumerator PlayShowcase(int side, float duration)
        {
            if (pokemon[side] == null || ArenaCamera == null) yield break;
            Transform subject = pokemon[side].transform;
            Vector3 origin = subject.position;
            Quaternion rotation = subject.rotation;
            var motion = subject.GetComponent<CinematicMotion>();
            var native = subject.GetComponent<NativePokemonModel>();
            Bounds localBounds = native != null ? native.RestBounds : motion != null ? motion.RestBounds : new Bounds(Vector3.up * .5f, Vector3.one);
            Bounds bounds = new Bounds(subject.TransformPoint(localBounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++) bounds.Encapsulate(subject.TransformPoint(localBounds.center + Vector3.Scale(localBounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            if (native != null) native.Play(PokemonMotionAction.Showcase);
            else if (motion != null) motion.BeginSpotlightAction();
            int species = shownSpecies[side];
            // The authored animation owns lift and body motion; the camera owns the cinematic travel.
            float height = Mathf.Max(bounds.size.y, .5f);
            float distance = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 1.9f, .9f, 18f);
            float travelMax = Mathf.Clamp(height * .28f, .24f, .9f);
            Vector3 focus = bounds.center;
            if (motion != null) focus += Vector3.up * motion.StandingLift;
            duration = Mathf.Max(.1f, duration);
            Vector3 cameraStart = ArenaCamera.transform.position;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = Mathf.SmoothStep(0f, 1f, t);
                float lift = 0f;
                subject.SetPositionAndRotation(origin, rotation);
                float orbit = Mathf.Sin(t * Mathf.PI * 1.35f) * .24f;
                Vector3 offset = new Vector3(orbit * distance,
                    height * (.23f + .025f * Mathf.Sin(t * Mathf.PI)) + lift,
                    -distance * Mathf.Lerp(1.22f, .88f, ease));
                Vector3 cameraTarget = focus + offset;
                float arrival = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .22f));
                ArenaCamera.transform.position = Vector3.Lerp(cameraStart, cameraTarget, arrival);
                ArenaCamera.transform.LookAt(focus + Vector3.up * lift);
                AnimateCinematicSpotlights(elapsed + side * .8f);
                yield return null;
            }
            // Hold the final pose; the next horizontal, vertical, or circular
            // wipe covers the swap before another model enters the shot.
        }

        public Bounds PokemonWorldBounds(int side)
        {
            if (pokemon[side] == null) return new Bounds(homes[side],Vector3.zero);
            var native=pokemon[side].GetComponent<NativePokemonModel>();
            var motion=pokemon[side].GetComponent<CinematicMotion>();
            Bounds local=native!=null?native.RestBounds:motion.RestBounds;
            return PokemonDisplaySize.WorldBounds(pokemon[side].transform,local);
        }

        float PokemonWorldHeight(int side)
        {
            if (pokemon[side]==null) return 1f;
            var native=pokemon[side].GetComponent<NativePokemonModel>();
            if (native!=null) return native.WorldModelHeight;
            var motion=pokemon[side].GetComponent<CinematicMotion>();
            return motion!=null?motion.ModelHeight*pokemon[side].transform.TransformVector(Vector3.up).magnitude:1f;
        }

        void FramePokemon(bool preview)
        {
            Bounds bounds=new Bounds();bool found=false;
            for (int side=0;side<2;side++)
            {
                if (pokemon[side]==null) continue;
                Bounds part=PokemonWorldBounds(side);
                if (!found) { bounds=part;found=true; }
                else { bounds.Encapsulate(part.min);bounds.Encapsulate(part.max); }
            }
            if (!found) return;
            ArenaCamera.fieldOfView=43f;
            Vector3 focus=bounds.center;
            float tanVertical=Mathf.Tan(ArenaCamera.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 horizontal=preview?Vector3.right:battleAxis;
            Vector3 backward=Vector3.Cross(Vector3.up,horizontal);
            float fit=Mathf.Max((ProjectedRadius(bounds,horizontal)+.75f)/(tanVertical*ArenaCamera.aspect),
                (bounds.extents.y+.65f)/tanVertical)+ProjectedRadius(bounds,backward);
            float distance=Mathf.Clamp(fit*1.1f,preview?4f:12f,24f);
            Vector3 offset=backward-horizontal*.018f+Vector3.up*.34f;
            if(!preview&&fieldLength>0&&Stadium!=null){
                foreach(float elevation in new[]{.34f,.7f,1.2f,2f}){
                    offset=backward-horizontal*.018f+Vector3.up*elevation;
                    if(!Stadium.CameraPathBlocked(focus+offset.normalized*distance,focus))break;
                }
            }
            ArenaCamera.transform.position=focus+offset.normalized*distance;
            ArenaCamera.transform.LookAt(focus);
        }

        public IEnumerator Attack(int side, string type, string category = "Physical")
        {
            if (pokemon[side] == null) yield break;
            float duration = Mathf.Max(.45f, PlayAttackAnimation(side, category == "Special"));
            Vector3 origin = pokemon[side].transform.position;
            Vector3 direction = (homes[1 - side] - homes[side]).normalized;
            float height = Mathf.Max(.25f, PokemonWorldHeight(side) * .68f);
            float targetHeight = Mathf.Max(.25f, PokemonWorldHeight(1-side) * .68f);
            Color color = type == "Fire" ? new Color(1, .45f, .1f) : type == "Grass" ? new Color(.4f, 1, .5f) : new Color(.4f, .75f, 1);
            var particle = Part(PrimitiveType.Sphere, "Attack pulse", origin + Vector3.up * height,
                                Vector3.one * .22f, Material(color, .5f, 0, true));
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float t = time / duration;
                // The original rig pose supplies anticipation and release without sliding planted feet.
                particle.transform.position = Vector3.Lerp(origin + Vector3.up * height, homes[1 - side] + Vector3.up * targetHeight, t);
                particle.transform.localScale = Vector3.one * Mathf.Lerp(.22f, .55f, t);
                yield return null;
            }
            if (pokemon[side] != null) pokemon[side].transform.position = origin;
            Destroy(particle);
        }

        public IEnumerator Hit(int side, int damage)
        {
            if (pokemon[side] == null) yield break;
            float duration = Mathf.Max(.22f, PlayDamageAnimation(side));
            Transform root = pokemon[side].transform;
            Quaternion rotation = root.rotation;
            float labelHeight = PokemonWorldHeight(side)+.35f;
            Label("−" + damage, root.position + Vector3.up * labelHeight, .10f,
                new Color(1, .72f, .4f), true);
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                // Recoil is authored on the rig, rather than shaking the complete actor at 60 radians/s.
                yield return null;
            }
            root.rotation = rotation;
        }

        public IEnumerator Faint(int side)
        {
            if (pokemon[side] == null) yield break;
            float faintDuration = PlayFaintAnimation(side);
            if (faintDuration > 0f)
                yield return new WaitForSeconds(faintDuration);
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
            if (arenaLitTemplate == null)
                arenaLitTemplate = Resources.Load<Material>("Materials/ArenaLit");
            if (arenaLitTemplate == null)
                throw new System.InvalidOperationException("The runtime URP material is missing from Resources/Materials/ArenaLit.");
            var m = new Material(arenaLitTemplate);
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

    }
}
