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
        public BattleDecisionCamera DecisionCamera { get; private set; }
        public Vector3 BattleCenter => battleCenter;
        public Vector3 BattleAxis => battleAxis;
        public bool TryGetCombatCameraFrame(int side, out Bounds bounds)
        {
            bounds = default;
            if (previewMode || side < 0 || side > 1 || pokemon[side] == null) return false;
            bounds = PokemonWorldBounds(side);
            return true;
        }
        public void SetDecisionCameraActive(bool active)
        {
            if (DecisionCamera != null) DecisionCamera.SetActive(active);
        }
        public IEnumerator ReturnToBattleFrame()
        {
            if (DecisionCamera != null) yield return DecisionCamera.ReturnToWide();
            else FramePokemon(false);
        }
        public void FrameCombatCamera() => FramePokemon(false);
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
            DecisionCamera = cameraObject.AddComponent<BattleDecisionCamera>();
            DecisionCamera.Initialize(this);
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
            if(System.Array.IndexOf(args,"--elite4-modern-review")>=0)stadiumKey="stadium1_elite4_modern";
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
            ClearProtection(side,true);
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
            RuntimeAssetMemory.NotifyModelChanged();
        }

        public void ShowInspectionPokemon(int species, bool greeting = false)
        {
            bool changed = !previewMode || pokemon[0] == null || shownSpecies[0] != species;
            if (changed) ShowPokemon(0, species, true);
            if (changed || !ArenaCamera.orthographic) FrameInspection();
            if (greeting && pokemon[0] != null)
            {
                var native = pokemon[0].GetComponent<NativePokemonModel>();
                float duration = native != null ? native.Play(PokemonMotionAction.Showcase) : 0f;
                Debug.Log("[inspection-greeting] species=" + species + " native=" + (native != null) + " duration=" + duration);
            }
        }

        public void FrameInspection(bool greeting = false)
        {
            if (pokemon[0] == null) return;
            foreach (var node in pokemon[0].GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 31;
            Bounds bounds = PokemonWorldBounds(0);
            ArenaCamera.cullingMask = 1 << 31;
            ArenaCamera.orthographic = true;
            ArenaCamera.clearFlags = CameraClearFlags.SolidColor;
            ArenaCamera.backgroundColor = new Color(.025f, .08f, .18f);
            ArenaCamera.orthographicSize = Mathf.Max(.35f, bounds.extents.y + .1f,
                (bounds.extents.x + .1f) / ArenaCamera.aspect) * 1.6f;
            // Reserve the upper portion of the inspection image for its name and types.
            // The stable offset avoids following animated bones and pumping the camera.
            Vector3 inspectionCenter = bounds.center + Vector3.up * (ArenaCamera.orthographicSize * .295f);
            ArenaCamera.transform.position = inspectionCenter + new Vector3(0, .12f, -1).normalized * (bounds.size.magnitude * 2f + 5f);
            ArenaCamera.transform.LookAt(inspectionCenter);
            var native = pokemon[0].GetComponent<NativePokemonModel>();
            if (greeting && native != null) native.Play(PokemonMotionAction.Showcase);
        }

        public float PlayAttackAnimation(int side, bool special = false)
        {
            if (pokemon[side] == null) return 0f;
            var driver = pokemon[side].GetComponent<PokemonAnimationDriver>();
            return driver == null ? 0f : driver.PlayAttack(special);
        }

        float PlayDamageAnimation(int side,float hitEffectiveness=1f)
        {
            if (pokemon[side] == null) return 0f;
            var driver = pokemon[side].GetComponent<PokemonAnimationDriver>();
            float duration=driver==null?0f:driver.PlayDamage(hitEffectiveness);if(duration>0)DamagePoseCount++;return duration;
        }

        public PokemonHitReaction GetDamageReaction(int side)
            => side>=0&&side<2&&pokemon[side]!=null?pokemon[side].GetComponent<PokemonHitReaction>():null;

        float PlayFaintAnimation(int side)
        {
            if (pokemon[side] == null) return 0f;
            var driver = pokemon[side].GetComponent<PokemonAnimationDriver>();
            return driver == null ? 0f : driver.PlayFaint();
        }

        public void ClearPokemon(int side)
        {
            ClearProtection(side,true);
            bool removed = pokemon[side] != null;
            if (removed) Destroy(pokemon[side]);
            pokemon[side] = null;
            shownSpecies[side] = 0;
            LoadedModels = (pokemon[0] != null ? 1 : 0) + (pokemon[1] != null ? 1 : 0);
            if (removed) RuntimeAssetMemory.NotifyModelChanged(true);
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

        public IEnumerator Attack(int side, string type, string category = "Physical", int moveId = 0, bool targetHit = true, int protectedTarget = -1,float hitEffectiveness=1f)
        {
            if (pokemon[side] == null) yield break;
            if(moveId==61||moveId==145){yield return BubbleVolley(side,moveId,targetHit,protectedTarget,hitEffectiveness);yield break;}
            if(moveId==56){yield return HydroJet(side,targetHit,protectedTarget,hitEffectiveness);yield break;}
            if(moveId==55){yield return WaterJet(side,targetHit,protectedTarget,hitEffectiveness);yield break;}
            if(moveId==53) { yield return FireBreath(side,targetHit,protectedTarget,hitEffectiveness);yield break; }
            float duration = Mathf.Max(.45f, PlayAttackAnimation(side, category == "Special"));
            Vector3 origin = pokemon[side].transform.position;
            Vector3 direction = (homes[1 - side] - homes[side]).normalized;
            float height = Mathf.Max(.25f, PokemonWorldHeight(side) * .68f);
            float targetHeight = Mathf.Max(.25f, PokemonWorldHeight(1-side) * .68f);
            Color color = type == "Fire" ? new Color(1, .45f, .1f) : type == "Grass" ? new Color(.4f, 1, .5f) : new Color(.4f, .75f, 1);
            var particle = Part(PrimitiveType.Sphere, "Attack pulse", origin + Vector3.up * height,
                                Vector3.one * .22f, Material(color, .5f, 0, true));
            Vector3 start=origin+Vector3.up*height,end=homes[1-side]+Vector3.up*targetHeight;
            bool deflected=protectedTarget>=0&&TraceProtection(protectedTarget,start,end,out _,out _),shieldReaction=false;
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float t = time / duration;
                // The original rig pose supplies anticipation and release without sliding planted feet.
                if(deflected&&TraceProtection(protectedTarget,start,end,out Vector3 contact,out Vector3 normal)){
                    float fraction=Mathf.Clamp(Vector3.Distance(start,contact)/Mathf.Max(.01f,Vector3.Distance(start,end)),.15f,.85f);
                    if(t<fraction)particle.transform.position=Vector3.Lerp(start,contact,t/fraction);
                    else {
                        if(!shieldReaction){ReactProtection(protectedTarget,contact,normal);shieldReaction=true;}
                        float outTime=(t-fraction)/(1-fraction);
                        Vector3 reflected=ProtectShieldEffect.DeflectionDirection(end-start,normal);
                        particle.transform.position=contact+reflected*Mathf.Min(4.4f,Vector3.Distance(start,contact)*.55f)*outTime;
                    }
                }else particle.transform.position=Vector3.Lerp(start,end,t);
                particle.transform.localScale = Vector3.one * Mathf.Lerp(.22f, .55f, t);
                yield return null;
            }
            if (pokemon[side] != null) pokemon[side].transform.position = origin;
            Destroy(particle);
        }

        readonly ProtectShieldEffect[] protections=new ProtectShieldEffect[2];
        public bool LastProtectActivationSound {get;private set;}
        public bool LastProtectBlockSound {get;private set;}
        public bool LastProtectAnimated {get;private set;}
        public int LastProtectMovedJoints {get;private set;}
        public float LastProtectPeakAngle {get;private set;}
        public int ProtectActivationCount {get;private set;}
        public int ProtectBlockCount {get;private set;}
        public bool LastFireWasDeflected {get;private set;}
        public int LastFireShieldContacts {get;private set;}
        public Vector3 LastFireDeflectionPoint {get;private set;}
        public Vector3 LastFireDeflectionDirection {get;private set;}
        public ProtectShieldEffect GetActiveProtection(int side)
            => side>=0&&side<2&&protections[side]!=null&&protections[side].IsActive?protections[side]:null;
        Transform EffectParent(){
            var parent=Stadium!=null&&Stadium.ModernStage!=null?Stadium.ModernStage.transform:transform;
            if(parent==transform&&GetComponent<ArenaFireAudio>()==null)gameObject.AddComponent<ArenaFireAudio>();
            return parent;
        }
        public IEnumerator Protect(int side){
            if(side<0||side>1||pokemon[side]==null)yield break;
            ClearProtection(side,true);
            LastProtectAnimated=false;LastProtectMovedJoints=0;LastProtectPeakAngle=0;
            LastProtectActivationSound=LastProtectBlockSound=false;
            var driver=pokemon[side].GetComponent<PokemonAnimationDriver>();
            if(driver!=null)driver.PlayIdle();
            var pose=PokemonProtectPose.Create(pokemon[side].GetComponent<NativePokemonModel>());
            var activationSound=ArenaFireAudio.PlayProtectActivation(EffectParent(),PokemonWorldBounds(side).center);
            LastProtectActivationSound=activationSound!=null&&activationSound.clip!=null&&activationSound.isPlaying;
            try {
                // Anticipation precedes the energy shell; the planted feet remain under native control.
                if(pose!=null&&pose.IsPlaying)yield return new WaitForSeconds(.20f);
                protections[side]=ProtectShieldEffect.Create(EffectParent(),PokemonWorldBounds(side));
                ProtectActivationCount++;
                yield return new WaitForSeconds(ProtectShieldEffect.ActivationDuration);
                while(pose!=null&&pose.IsPlaying)yield return null;
                if(pose!=null){
                    LastProtectMovedJoints=pose.MovedJointsCount;LastProtectPeakAngle=pose.PeakAngle;
                    LastProtectAnimated=LastProtectMovedJoints>0&&LastProtectPeakAngle>1;
                }
            } finally {if(pose!=null){pose.enabled=false;Destroy(pose);}}
        }
        bool TraceProtection(int side,Vector3 from,Vector3 aim,out Vector3 hit,out Vector3 normal){
            hit=aim;normal=Vector3.up;
            if(side<0||side>1||pokemon[side]==null)return false;
            if(protections[side]==null||!protections[side].IsActive)
                protections[side]=ProtectShieldEffect.Create(EffectParent(),PokemonWorldBounds(side));
            protections[side].SetBounds(PokemonWorldBounds(side));
            return protections[side].TryContact(from,aim,out hit,out normal);
        }
        void ReactProtection(int side,Vector3 hit,Vector3 normal){
            if(side<0||side>1||protections[side]==null)return;
            if(!protections[side].IsActive)return;
            protections[side].React(hit,normal);ProtectBlockCount++;
            var blockSound=ArenaFireAudio.PlayProtectBlock(EffectParent(),hit);
            LastProtectBlockSound=blockSound!=null&&blockSound.clip!=null&&blockSound.isPlaying;
        }
        public void ClearProtection(int side,bool immediate=false){
            if(side<0||side>1||protections[side]==null)return;
            if(immediate){Destroy(protections[side].gameObject);protections[side]=null;}
            else protections[side].Release();
        }
        public void EndProtection(bool immediate=false){ClearProtection(0,immediate);ClearProtection(1,immediate);}
        void LateUpdate(){for(int side=0;side<2;side++)if(protections[side]!=null&&protections[side].IsActive&&pokemon[side]!=null)protections[side].SetBounds(PokemonWorldBounds(side));}
        void OnDisable(){EndProtection(true);}

        readonly Transform[] fireMouth=new Transform[2];
        public bool LastFlamethrowerCompleted {get;private set;}
        public int FlamethrowerCasts {get;private set;}
        public int LastFirePeakFlames {get;private set;}
        public string LastFireMouth {get;private set;}
        public float LastFireAudioPeak {get;private set;}
        public bool LastFireSound {get;private set;}
        public int DamagePoseCount {get;private set;}
        public int LastFireTargetReactions {get;private set;}
        public float LastFireImpactTime {get;private set;}
        public bool LastFireBurnTail {get;private set;}
        void FireEndpoints(int side,out Vector3 source,out Vector3 target,out float width)
        {
            Bounds victim=PokemonWorldBounds(1-side);
            target=victim.center;
            float height=PokemonWorldHeight(side);width=Mathf.Clamp(height*.22f,.28f,.72f);
            if(fireMouth[side]==null||!fireMouth[side].IsChildOf(pokemon[side].transform)) {
                fireMouth[side]=null;
                foreach(var bone in pokemon[side].GetComponentsInChildren<Transform>(true))
                    if(bone.name=="EffMouth01") {fireMouth[side]=bone;break;}
            }
            source=fireMouth[side]!=null?fireMouth[side].position:pokemon[side].transform.position+Vector3.up*height*.72f;
            source+=(target-source).normalized*.04f;
            LastFireMouth=fireMouth[side]!=null?fireMouth[side].name:"body-height fallback";
        }
        IEnumerator FireBreath(int side,bool targetHit,int protectedTarget,float hitEffectiveness)
        {
            LastFlamethrowerCompleted=false;LastFireSound=false;LastFireTargetReactions=0;LastFireImpactTime=-1;LastFireBurnTail=false;
            LastFireWasDeflected=false;LastFireShieldContacts=0;LastFireDeflectionPoint=LastFireDeflectionDirection=Vector3.zero;
            if(pokemon[1-side]==null)yield break;
            float nativeDuration=PlayAttackAnimation(side,true);
            FireEndpoints(side,out Vector3 source,out Vector3 target,out float width);
            Transform effectParent=Stadium!=null&&Stadium.ModernStage!=null?Stadium.ModernStage.transform:transform;
            if(effectParent==transform&&GetComponent<ArenaFireAudio>()==null)gameObject.AddComponent<ArenaFireAudio>();
            Bounds victim=PokemonWorldBounds(1-side);
            Vector3 missOffset=targetHit||protectedTarget>=0?Vector3.zero:Vector3.Cross(Vector3.up,(target-source).normalized).normalized*(victim.extents.magnitude+width*3);
            var effect=FlamethrowerEffect.Create(effectParent,source,target+missOffset,width);
            effect.ImpactEnabled=targetHit;effect.SetVictimBounds(victim);
            var sound=ArenaFireAudio.PlayFlamethrower(effect.transform,source,effect.Duration);
            LastFireAudioPeak=ArenaFireAudio.AttackPeak;
            LastFireSound=sound!=null&&sound.clip!=null;
            var animator=pokemon[side].GetComponentInChildren<Animator>();
            float speed=animator!=null?animator.speed:1;
            if(animator!=null&&nativeDuration>.1f)animator.speed=Mathf.Clamp(nativeDuration/effect.Duration,.35f,1.2f);
            bool impactReacted=false,shieldReacted=false;
            try {
                while(effect.IsPlaying) {
                    if(protectedTarget>=0&&TraceProtection(protectedTarget,source,target,out Vector3 contact,out Vector3 normal)) {
                        Vector3 diverted=ProtectShieldEffect.DeflectionDirection(target-source,normal);
                        effect.SetDeflection(contact,diverted,Mathf.Clamp(Vector3.Distance(source,contact)*.55f+width,1.0f,4.4f));
                        LastFireWasDeflected=true;LastFireDeflectionPoint=contact;LastFireDeflectionDirection=diverted;
                        if(!shieldReacted&&effect.ImpactReached){
                            int contacts=ProtectBlockCount;ReactProtection(protectedTarget,contact,normal);
                            LastFireShieldContacts+=ProtectBlockCount-contacts;shieldReacted=true;
                        }
                    }
                    if(targetHit&&!impactReacted&&effect.ImpactReached) {
                        int before=DamagePoseCount;PlayDamageAnimation(1-side,hitEffectiveness);impactReacted=true;LastFireTargetReactions=DamagePoseCount-before;LastFireImpactTime=effect.EffectProgress*effect.Duration;
                        Debug.Log("[flamethrower-impact] target="+(1-side)+" damage pose triggered at impact");
                    }
                    FireEndpoints(side,out source,out target,out width);effect.SetEndpoints(source,target+missOffset);effect.SetVictimBounds(PokemonWorldBounds(1-side));
                    if(targetHit&&effect.EffectProgress>.88f&&effect.ImpactOpacity>.005f)LastFireBurnTail=true;
                    if(sound!=null)sound.transform.position=source;
                    yield return null;
                }
                LastFlamethrowerCompleted=effect.Completed&&effect.ImpactReached&&effect.Validate(out _)&&LastFireSound&&(LastFireTargetReactions==(targetHit?1:0));
                LastFirePeakFlames=effect.PeakActiveFlames;FlamethrowerCasts++;
                Debug.Log("[flamethrower] side="+side+" mouth="+LastFireMouth+" completed="+LastFlamethrowerCompleted+" peak="+LastFirePeakFlames+" sound="+LastFireSound);
            } finally {if(animator!=null)animator.speed=speed;if(effect!=null)Destroy(effect.gameObject);}
        }

        readonly Transform[,] waterCannon=new Transform[2,2];
        void WaterEndpoints(int side,out Vector3 source,out Vector3 secondary,out bool paired,out Vector3 target,out float width)
        {
            FireEndpoints(side,out source,out target,out width);secondary=source;paired=false;
            if(shownSpecies[side]==9)
            {
                for(int i=0;i<2;i++)if(waterCannon[side,i]==null||!waterCannon[side,i].IsChildOf(pokemon[side].transform))
                {
                    waterCannon[side,i]=null;string marker=i==0?"EffWideUse01":"EffWideUse02";
                    foreach(var bone in pokemon[side].GetComponentsInChildren<Transform>(true))if(bone.name==marker){waterCannon[side,i]=bone;break;}
                }
                paired=waterCannon[side,0]!=null&&waterCannon[side,1]!=null;
                if(paired){source=waterCannon[side,0].position;secondary=waterCannon[side,1].position;}
            }
            LastWaterSource=paired?"EffWideUse01 + EffWideUse02 (paired cannons)":LastFireMouth;
        }
        public bool LastWaterCompleted {get;private set;}
        public bool LastWaterDetachedBeforeImpact {get;private set;}
        public float LastWaterMaxPacketLength {get;private set;}
        public float LastWaterPacketFraction {get;private set;}
        public bool LastWaterSound {get;private set;}
        public bool LastWaterSplash {get;private set;}
        public bool LastWaterWasBlocked {get;private set;}
        public bool LastWaterShieldRunoff {get;private set;}
        public int LastWaterEmitterCount {get;private set;} public int LastWaterEmitterContacts {get;private set;}
        public int LastWaterTargetReactions {get;private set;}
        public int LastWaterShieldContacts {get;private set;}
        public int LastWaterPeakDroplets {get;private set;}
        public int WaterCasts {get;private set;}
        public float LastWaterImpactTime {get;private set;}
        public float LastWaterAudioPeak {get;private set;}
        public string LastWaterSource {get;private set;}
        public Vector3 LastWaterContactPoint {get;private set;}
        public Vector3 LastWaterContactNormal {get;private set;}
        IEnumerator WaterJet(int side,bool targetHit,int protectedTarget,float hitEffectiveness)
        {
            LastWaterCompleted=LastWaterSound=LastWaterSplash=LastWaterWasBlocked=LastWaterShieldRunoff=false;
            LastWaterTargetReactions=LastWaterShieldContacts=LastWaterEmitterContacts=0;LastWaterImpactTime=-1;
            LastWaterContactPoint=LastWaterContactNormal=Vector3.zero;LastWaterDetachedBeforeImpact=false;LastWaterMaxPacketLength=LastWaterPacketFraction=0;
            if(pokemon[1-side]==null)yield break;
            float nativeDuration=PlayAttackAnimation(side,true);
            WaterEndpoints(side,out Vector3 source,out Vector3 secondary,out bool paired,out Vector3 target,out float width);
            LastWaterEmitterCount=paired?2:1;
            Transform effectParent=EffectParent();Bounds victim=PokemonWorldBounds(1-side);
            Vector3 missOffset=targetHit||protectedTarget>=0?Vector3.zero:Vector3.Cross(Vector3.up,(target-source).normalized).normalized*(victim.extents.magnitude+width*3);
            var effect=WaterGunEffect.Create(effectParent,source,target+missOffset,width*.45f);
            var second=paired?WaterGunEffect.Create(effectParent,secondary,target+missOffset,width*.45f):null;
            effect.ImpactEnabled=targetHit;effect.SetVictimBounds(victim);
            if(second!=null){second.ImpactEnabled=targetHit;second.SetVictimBounds(victim);}
            var sound=WaterAttackAudio.Play(effectParent,paired?(source+secondary)*.5f:source,effect.Duration);
            LastWaterAudioPeak=WaterAttackAudio.DefaultPcmStats.peak;
            LastWaterSound=sound!=null&&sound.clip!=null&&sound.isPlaying&&WaterAttackAudio.DefaultPcmStats.finite&&LastWaterAudioPeak>0&&LastWaterAudioPeak<=1;
            var animator=pokemon[side].GetComponentInChildren<Animator>();float speed=animator!=null?animator.speed:1;
            if(animator!=null&&nativeDuration>.1f)animator.speed=Mathf.Clamp(nativeDuration/effect.Duration,.35f,1.2f);
            bool impactReacted=false,shieldReacted=false;
            try {
                while(effect.IsPlaying) {
                    WaterEndpoints(side,out source,out secondary,out paired,out target,out width);
                    effect.SetEndpoints(source,target+missOffset);effect.SetVictimBounds(PokemonWorldBounds(1-side));
                    if(second!=null){second.SetEndpoints(secondary,target+missOffset);second.SetVictimBounds(PokemonWorldBounds(1-side));}
                    if(protectedTarget>=0&&TraceProtection(protectedTarget,source,target,out Vector3 contact,out Vector3 normal)) {
                        var shell=GetActiveProtection(protectedTarget);
                        effect.SetShieldContact(contact,normal,shell.ShieldBounds,pokemon[protectedTarget].transform.position.y);
                        if(second!=null&&TraceProtection(protectedTarget,secondary,target,out Vector3 secondContact,out Vector3 secondNormal))
                            {second.SetShieldContact(secondContact,secondNormal,shell.ShieldBounds,pokemon[protectedTarget].transform.position.y);if(effect.ImpactReached&&second.ImpactReached)LastWaterEmitterContacts=2;}
                        contact=effect.ShieldContactPoint;normal=effect.ShieldContactNormal;LastWaterWasBlocked=true;LastWaterContactPoint=contact;LastWaterContactNormal=normal;
                        if(!shieldReacted&&effect.ImpactReached){int contacts=ProtectBlockCount;ReactProtection(protectedTarget,contact,normal);LastWaterShieldContacts+=ProtectBlockCount-contacts;shieldReacted=true;}
                        if(effect.ContactOpacity>.005f&&effect.RunoffDrops>0)LastWaterShieldRunoff=true;
                    }
                    LastWaterMaxPacketLength=Mathf.Max(LastWaterMaxPacketLength,effect.ProjectileLength,second!=null?second.ProjectileLength:0);
                    LastWaterPacketFraction=Mathf.Max(LastWaterPacketFraction,effect.ProjectileLength/Mathf.Max(.1f,Vector3.Distance(effect.TrajectoryOrigin,target)),second!=null?second.ProjectileLength/Mathf.Max(.1f,Vector3.Distance(second.TrajectoryOrigin,target)):0);
                    if(!effect.ImpactReached&&effect.ProjectileLength>.01f&&effect.ProjectileDetached&&effect.SourceConnectionCleared
                        &&(second==null||(!second.ImpactReached&&second.ProjectileLength>.01f&&second.ProjectileDetached&&second.SourceConnectionCleared)))LastWaterDetachedBeforeImpact=true;
                    if(targetHit&&!impactReacted&&effect.ImpactReached) {
                        int before=DamagePoseCount;PlayDamageAnimation(1-side,hitEffectiveness);impactReacted=true;LastWaterTargetReactions=DamagePoseCount-before;LastWaterImpactTime=effect.EffectProgress*effect.Duration;
                    }
                    if(targetHit&&effect.EffectProgress>.45f&&effect.ImpactOpacity>.005f)LastWaterSplash=true;
                    if(sound!=null)sound.transform.position=paired?(source+secondary)*.5f:source;
                    yield return null;
                }
                LastWaterCompleted=LastWaterDetachedBeforeImpact&&LastWaterPacketFraction<.40f&&effect.Completed&&(second==null||second.Completed)&&effect.ImpactReached&&effect.Validate(out _)&&(second==null||second.Validate(out _))&&LastWaterSound&&(LastWaterTargetReactions==(targetHit?1:0));
                LastWaterPeakDroplets=effect.PeakDroplets+(second!=null?second.PeakDroplets:0);WaterCasts++;
                Debug.Log("[water-gun] side="+side+" emitters="+LastWaterSource+" completed="+LastWaterCompleted+" droplets="+LastWaterPeakDroplets+" runoff="+LastWaterShieldRunoff);
            } finally {if(animator!=null)animator.speed=speed;if(effect!=null)Destroy(effect.gameObject);if(second!=null)Destroy(second.gameObject);}
        }

        public bool LastHydroCompleted {get;private set;}
        public bool LastHydroSound {get;private set;}
        public bool LastHydroSplash {get;private set;}
        public bool LastHydroWasBlocked {get;private set;}
        public bool LastHydroShieldRunoff {get;private set;}
        public int LastHydroEmitterCount {get;private set;} public int LastHydroEmitterContacts {get;private set;}
        public int LastHydroTargetReactions {get;private set;}
        public int LastHydroShieldContacts {get;private set;}
        public int LastHydroPeakDroplets {get;private set;}
        public int HydroCasts {get;private set;}
        public float LastHydroImpactTime {get;private set;}
        public float LastHydroAudioPeak {get;private set;}
        public string LastHydroSource {get;private set;}
        public Vector3 LastHydroContactPoint {get;private set;}
        public Vector3 LastHydroContactNormal {get;private set;}
        IEnumerator HydroJet(int side,bool targetHit,int protectedTarget,float hitEffectiveness)
        {
            LastHydroCompleted=LastHydroSound=LastHydroSplash=LastHydroWasBlocked=LastHydroShieldRunoff=false;
            LastHydroTargetReactions=LastHydroShieldContacts=LastHydroEmitterContacts=0;LastHydroImpactTime=-1;
            LastHydroContactPoint=LastHydroContactNormal=Vector3.zero;
            if(pokemon[1-side]==null)yield break;
            float nativeDuration=PlayAttackAnimation(side,true);
            WaterEndpoints(side,out Vector3 source,out Vector3 secondary,out bool paired,out Vector3 target,out float width);
            LastHydroEmitterCount=paired?2:1;LastHydroSource=LastWaterSource;
            Transform effectParent=EffectParent();Bounds victim=PokemonWorldBounds(1-side);
            Vector3 missOffset=targetHit||protectedTarget>=0?Vector3.zero:Vector3.Cross(Vector3.up,(target-source).normalized).normalized*(victim.extents.magnitude+width*3);
            var effect=HydroPumpEffect.Create(effectParent,source,target+missOffset,width*.95f);
            if(paired)effect.SetSecondaryEmitter(secondary);
            effect.ImpactEnabled=targetHit;effect.SetVictimBounds(victim);
            var sound=HydroPumpAudio.Play(effectParent,paired?(source+secondary)*.5f:source,effect.Duration);
            LastHydroAudioPeak=HydroPumpAudio.DefaultPcmStats.peak;
            LastHydroSound=sound!=null&&sound.clip!=null&&sound.isPlaying&&HydroPumpAudio.DefaultPcmStats.finite&&LastHydroAudioPeak>0&&LastHydroAudioPeak<=1;
            var animator=pokemon[side].GetComponentInChildren<Animator>();float speed=animator!=null?animator.speed:1;
            if(animator!=null&&nativeDuration>.1f)animator.speed=Mathf.Clamp(nativeDuration/effect.Duration,.35f,1.2f);
            bool impactReacted=false,shieldReacted=false;
            try {
                while(effect.IsPlaying) {
                    WaterEndpoints(side,out source,out secondary,out paired,out target,out width);LastHydroSource=LastWaterSource;
                    effect.SetEndpoints(source,target+missOffset);effect.SetVictimBounds(PokemonWorldBounds(1-side));
                    if(paired)effect.SetSecondaryEmitter(secondary);
                    if(protectedTarget>=0&&TraceProtection(protectedTarget,source,target,out Vector3 contact,out Vector3 normal)) {
                        var shell=GetActiveProtection(protectedTarget);
                        effect.SetShieldContact(contact,normal);effect.SetShieldSurface(shell.ShieldBounds,pokemon[protectedTarget].transform.position.y);
                        if(paired&&TraceProtection(protectedTarget,secondary,target,out Vector3 secondContact,out Vector3 secondNormal))
                            {effect.SetSecondaryShieldContact(secondContact,secondNormal);if(effect.ImpactReached)LastHydroEmitterContacts=2;}
                        LastHydroWasBlocked=true;LastHydroContactPoint=contact;LastHydroContactNormal=normal;
                        if(!shieldReacted&&effect.ImpactReached){int contacts=ProtectBlockCount;ReactProtection(protectedTarget,contact,normal);LastHydroShieldContacts+=ProtectBlockCount-contacts;shieldReacted=true;}
                        if(effect.ContactOpacity>.005f&&effect.RunoffDrops>0)LastHydroShieldRunoff=true;
                    }
                    if(targetHit&&!impactReacted&&effect.ImpactReached) {
                        int before=DamagePoseCount;PlayDamageAnimation(1-side,hitEffectiveness);impactReacted=true;LastHydroTargetReactions=DamagePoseCount-before;LastHydroImpactTime=effect.EffectProgress*effect.Duration;
                    }
                    if(targetHit&&effect.EffectProgress>.45f&&effect.ImpactOpacity>.005f)LastHydroSplash=true;
                    if(sound!=null)sound.transform.position=paired?(source+secondary)*.5f:source;
                    yield return null;
                }
                LastHydroCompleted=effect.Completed&&effect.ImpactReached&&effect.Validate(out _)&&LastHydroSound&&(LastHydroTargetReactions==(targetHit?1:0));
                LastHydroPeakDroplets=effect.PeakDroplets;HydroCasts++;
                Debug.Log("[hydro-pump] side="+side+" emitters="+LastHydroSource+" completed="+LastHydroCompleted+" droplets="+LastHydroPeakDroplets+" runoff="+LastHydroShieldRunoff);
            } finally {if(animator!=null)animator.speed=speed;if(effect!=null)Destroy(effect.gameObject);}
        }

        public bool LastBubbleCompleted {get;private set;}
        public bool LastBubbleSound {get;private set;}
        public bool LastBubbleSplash {get;private set;}
        public bool LastBubbleWasBlocked {get;private set;}
        public bool LastBubbleShieldRunoff {get;private set;}
        public int LastBubbleEmitterCount {get;private set;} public int LastBubbleEmitterContacts {get;private set;}
        public int LastBubbleTargetReactions {get;private set;}
        public int LastBubbleShieldContacts {get;private set;}
        public int LastBubblePeakBubbles {get;private set;}
        public int BubbleCasts {get;private set;} public int LastBubbleContactPops {get;private set;} public int LastBubbleEmissions {get;private set;}
        public float LastBubbleImpactTime {get;private set;}
        public float LastBubbleAudioPeak {get;private set;}
        public string LastBubbleSource {get;private set;}
        public Vector3 LastBubbleContactPoint {get;private set;}
        public Vector3 LastBubbleContactNormal {get;private set;}
        IEnumerator BubbleVolley(int side,int moveId,bool targetHit,int protectedTarget,float hitEffectiveness)
        {
            LastBubbleCompleted=LastBubbleSound=LastBubbleSplash=LastBubbleWasBlocked=LastBubbleShieldRunoff=false;
            LastBubbleTargetReactions=LastBubbleShieldContacts=LastBubbleEmitterContacts=0;LastBubbleImpactTime=-1;
            LastBubbleContactPoint=LastBubbleContactNormal=Vector3.zero;LastBubbleContactPops=LastBubbleEmissions=0;
            if(pokemon[1-side]==null)yield break;
            float nativeDuration=PlayAttackAnimation(side,true);
            WaterEndpoints(side,out Vector3 source,out Vector3 secondary,out bool paired,out Vector3 target,out float width);
            LastBubbleEmitterCount=paired?2:1;LastBubbleSource=LastWaterSource;
            Transform effectParent=EffectParent();Bounds victim=PokemonWorldBounds(1-side);
            Vector3 missOffset=targetHit||protectedTarget>=0?Vector3.zero:Vector3.Cross(Vector3.up,(target-source).normalized).normalized*(victim.extents.magnitude+width*3);
            var effect=BubbleAttackEffect.Create(effectParent,source,target+missOffset,width*.75f,moveId);
            if(paired)effect.SetSecondaryEmitter(secondary);
            effect.ImpactEnabled=targetHit;effect.SetVictimBounds(victim);
            var sound=BubbleAttackAudio.Play(effectParent,paired?(source+secondary)*.5f:source,moveId,effect.Duration);
            var bubbleStats=BubbleAttackAudio.AnalyzePcm(BubbleAttackAudio.CreateOriginalPcm(moveId));LastBubbleAudioPeak=bubbleStats.peak;
            LastBubbleSound=sound!=null&&sound.clip!=null&&sound.isPlaying&&bubbleStats.finite&&LastBubbleAudioPeak>0&&LastBubbleAudioPeak<=1;
            var animator=pokemon[side].GetComponentInChildren<Animator>();float speed=animator!=null?animator.speed:1;
            if(animator!=null&&nativeDuration>.1f)animator.speed=Mathf.Clamp(nativeDuration/effect.Duration,.35f,1.2f);
            bool impactReacted=false,shieldReacted=false;
            try {
                while(effect.IsPlaying) {
                    WaterEndpoints(side,out source,out secondary,out paired,out target,out width);LastBubbleSource=LastWaterSource;
                    effect.SetEndpoints(source,target+missOffset);effect.SetVictimBounds(PokemonWorldBounds(1-side));
                    if(paired)effect.SetSecondaryEmitter(secondary);
                    if(protectedTarget>=0&&TraceProtection(protectedTarget,source,target,out Vector3 contact,out Vector3 normal)) {
                        var shell=GetActiveProtection(protectedTarget);
                        effect.SetShieldContact(contact,normal);effect.SetShieldSurface(shell.ShieldBounds,pokemon[protectedTarget].transform.position.y);
                        if(paired&&TraceProtection(protectedTarget,secondary,target,out Vector3 secondContact,out Vector3 secondNormal))
                            {effect.SetSecondaryShieldContact(secondContact,secondNormal);}
                        LastBubbleWasBlocked=true;LastBubbleContactPoint=contact;LastBubbleContactNormal=normal;
                        if(!shieldReacted&&effect.ImpactReached){int contacts=ProtectBlockCount;ReactProtection(protectedTarget,contact,normal);LastBubbleShieldContacts+=ProtectBlockCount-contacts;shieldReacted=true;}
                        if(effect.ContactOpacity>.005f&&effect.RunoffDrops>0)LastBubbleShieldRunoff=true;
                    }
                    LastBubbleEmitterContacts=(effect.PrimaryShieldPops>0?1:0)+(effect.SecondaryShieldPops>0?1:0);
                    LastBubbleContactPops=effect.TotalContactPops;LastBubbleEmissions=effect.EmissionCount;
                    if(targetHit&&!impactReacted&&effect.ImpactReached) {
                        int before=DamagePoseCount;PlayDamageAnimation(1-side,hitEffectiveness);impactReacted=true;LastBubbleTargetReactions=DamagePoseCount-before;LastBubbleImpactTime=effect.EffectProgress*effect.Duration;
                    }
                    if(targetHit&&effect.EffectProgress>.45f&&effect.ImpactOpacity>.005f)LastBubbleSplash=true;
                    if(sound!=null)sound.transform.position=paired?(source+secondary)*.5f:source;
                    yield return null;
                }
                LastBubbleCompleted=LastBubbleEmissions==(moveId==61?24:16)&&LastBubbleContactPops>0&&effect.Completed&&effect.ImpactReached&&effect.Validate(out _)&&LastBubbleSound&&(LastBubbleTargetReactions==(targetHit?1:0));
                LastBubblePeakBubbles=effect.PeakBubbles;BubbleCasts++;
                Debug.Log("[bubbles] side="+side+" emitters="+LastBubbleSource+" completed="+LastBubbleCompleted+" droplets="+LastBubblePeakBubbles+" runoff="+LastBubbleShieldRunoff);
            } finally {if(animator!=null)animator.speed=speed;if(effect!=null)Destroy(effect.gameObject);}
        }

        public IEnumerator Hit(int side, int damage, bool reactionAlreadyPlayed = false,float hitEffectiveness=1f)
        {
            if (pokemon[side] == null) yield break;
            float duration = reactionAlreadyPlayed ? .22f :Mathf.Max(.22f, PlayDamageAnimation(side,damage>0?hitEffectiveness:0));
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
            if(reactionAlreadyPlayed)while(GetDamageReaction(side)!=null&&GetDamageReaction(side).IsPlaying)yield return null;
            root.rotation = rotation;
        }

        public IEnumerator Faint(int side)
        {
            ClearProtection(side);
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
