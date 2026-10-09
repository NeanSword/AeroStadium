using System;
using System.Collections;
using System.Collections.Generic;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        enum ScreenMode { Intro, Title, MainMenu, Selection, Battle, Result }
        readonly struct IntroEntry
        {
            public readonly int Species;
            public readonly int Generation;
            public readonly string Name;
            public IntroEntry(int species, int generation, string name) { Species = species; Generation = generation; Name = name; }
        }
        static readonly IntroEntry[] IntroRoster =
        {
            new IntroEntry(1, 1, "BULBIZARRE"), new IntroEntry(4, 1, "SALAMÈCHE"),
            new IntroEntry(7, 1, "CARAPUCE"), new IntroEntry(25, 1, "PIKACHU"),
            new IntroEntry(39, 1, "RONDOUDOU"), new IntroEntry(65, 1, "ALAKAZAM"),
            new IntroEntry(74, 1, "RACAILLOU"), new IntroEntry(94, 1, "ECTOPLASMA"),
            new IntroEntry(130, 1, "LÉVIATOR"), new IntroEntry(143, 1, "RONFLEX"),
            new IntroEntry(144, 1, "ARTIKODIN"), new IntroEntry(145, 1, "ÉLECTHOR"),
            new IntroEntry(146, 1, "SULFURA"), new IntroEntry(149, 1, "DRACOLOSSE"),
            new IntroEntry(150, 1, "MEWTWO"), new IntroEntry(151, 1, "MEW"),
            new IntroEntry(6, 1, "DRACAUFEU"), new IntroEntry(9, 1, "TORTANK")
        };
        readonly Color ink = new Color(.055f, .09f, .16f, .96f);
        readonly Color muted = new Color(.69f, .77f, .88f);
        readonly Color gold = new Color(1f, .78f, .33f);
        readonly Color blue = new Color(.12f, .32f, .63f);
        readonly List<Button> buttons = new List<Button>();
        readonly Image[] health = new Image[2];
        readonly Text[] healthText = new Text[2];
        RawImage introWipe;
        Material introWipeMaterial;
        readonly int[] visibleHp = new int[2];
        Catalog catalog;
        BattleEngine battle;
        ArenaView arena;
        TitleScreenAudio titleAudio;
        MainMenuAudio menuAudio;
        MainMenuView mainMenuView;
        PokemonSelectionView selectionView;
        RenderTexture selectionPreview;
        int inspectionCameraMask;
        Color inspectionCameraBackground;
        CameraClearFlags inspectionCameraClear;
        TeamMember[] selectedTeam;
        readonly int[] visibleMaximumHp = new int[2];
        readonly Text[] pokemonNames = new Text[2];
        bool selectionTest, selectionTestCompleted;
        int selectionSwitches;
        Button titleStartButton;
        Gamepad mainMenuVirtualPad;
        Keyboard mainMenuVirtualKeyboard;
        Mouse mainMenuVirtualMouse;
        readonly List<InputDevice> mainMenuSuppressedDevices = new List<InputDevice>();
        bool mainMenuInputsIsolated;
        ControllerHints controls;
        RectTransform canvasRoot, page, pausePanel, chromeHeader, controlFooter;
        Text hints, logText, turnText, introCaption, titleControlHint;
        RawImage titleArtwork, titleLogo, titleStart;
        Coroutine introRoutine;
        Font font;
        ScreenMode screen;
        int selectedSpecies = 6;
        int selectedRosterPage;
        int? requestedSeed;
        string selectedItem = "leftovers";
        bool busy, paused, smoke, smokeEnded, titleTest, titleAudioTest;
        bool mainMenuTest, menuAudioTest, mainMenuTestCompleted, mainMenuChecksPassed = true;
        bool mainMenuArtworkVerified, mainMenuGuardVerified, mainMenuMusicContinuity;
        bool mainMenuCursorVerified, mainMenuNavigationVerified, mainMenuPointerVerified, mainMenuMouseClickVerified;
        int mainMenuOpenedFrame, mainMenuRoutes, mainMenuRouteMask, mainMenuGuardBlocks, mainMenuMaxSelectionChanges;
        float mainMenuOpenedAt;
        bool titlePromptWasVisible, titlePromptWasHidden, titleAssetsVerified;
        int errors, titleBlinkCycles;
        float exitAt, titleShownAt;

        void Awake()
        {
            Application.logMessageReceived += OnLog;
            string[] args = Environment.GetCommandLineArgs();
            smoke = Array.IndexOf(args, "--smoke-test") >= 0;
            titleAudioTest = Array.IndexOf(args, "--title-audio-test") >= 0;
            mainMenuTest = Array.IndexOf(args, "--main-menu-test") >= 0;
            menuAudioTest = Array.IndexOf(args, "--menu-audio-test") >= 0;
            selectionTest = Array.IndexOf(args, "--selection-test") >= 0;
            if ((mainMenuTest || menuAudioTest || selectionTest) && !smoke) IsolateMainMenuTestInputs();
            titleTest = titleAudioTest || Array.IndexOf(args, "--title-test") >= 0;
            bool skipIntro = titleTest || mainMenuTest || menuAudioTest || selectionTest || Array.IndexOf(args, "--skip-intro") >= 0;
            int speciesIndex = Array.IndexOf(args, "--species");
            if (speciesIndex >= 0 && speciesIndex + 1 < args.Length && int.TryParse(args[speciesIndex + 1], out int species)
                && species >= 1 && species <= 151) selectedSpecies = species;
            int seedIndex = Array.IndexOf(args, "--seed");
            if (seedIndex >= 0 && seedIndex + 1 < args.Length && int.TryParse(args[seedIndex + 1], out int seed)) requestedSeed = seed;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var source = Resources.Load<TextAsset>("Data/catalog");
            if (source == null) throw new InvalidOperationException("Catalog missing.");
            catalog = JsonUtility.FromJson<Catalog>(source.text); catalog.Validate();
            selectedRosterPage = Mathf.Clamp((selectedSpecies - 1) / 10, 0, (catalog.species.Length - 1) / 10);
            arena = new GameObject("Original Aero arena").AddComponent<ArenaView>(); arena.Build();
            if (Array.IndexOf(args,"--stadium-review")>=0)
            {
                enabled=false;gameObject.AddComponent<StadiumReview>().Begin(arena,arena.Stadium);return;
            }
            if (Array.IndexOf(args, "--size-review") >= 0)
            {
                enabled = false;
                gameObject.AddComponent<PokemonSizeReview>().Begin(catalog, arena);
                return;
            }
            if (Array.IndexOf(args, "--animation-review") >= 0)
            {
                enabled = false;
                gameObject.AddComponent<PokemonAnimationReview>().Begin(arena, catalog);
                return;
            }
            CreateCanvas(); CreateInputs();
            int secondsIndex = Array.IndexOf(args, "--seconds");
            if (secondsIndex >= 0 && secondsIndex + 1 < args.Length && int.TryParse(args[secondsIndex + 1], out int seconds))
                exitAt = Time.realtimeSinceStartup + Mathf.Clamp(seconds, 10, 300);
            if (titleTest && exitAt <= 0) exitAt = Time.realtimeSinceStartup + (titleAudioTest ? 140f : 12f);
            if (mainMenuTest && exitAt <= 0) exitAt = Time.realtimeSinceStartup + 55f;
            if (menuAudioTest && exitAt <= 0) exitAt = Time.realtimeSinceStartup + 90f;
            if (selectionTest && exitAt <= 0) exitAt = Time.realtimeSinceStartup + 180f;
            if (smoke) StartCoroutine(SmokePlay());
            else if (skipIntro) ShowTitle();
            else introRoutine = StartCoroutine(PlayIntro());
            if (mainMenuTest && !smoke) StartCoroutine(TestMainMenu());
            else if (menuAudioTest && !smoke) StartCoroutine(TestMenuAudio());
            else if (selectionTest && !smoke) StartCoroutine(TestSelection());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            ReleaseSelectionPreview();
            if (introWipeMaterial != null) Destroy(introWipeMaterial);
            RemoveMainMenuTestDevices();
            RestoreMainMenuTestInputs();
        }
        void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        void Update()
        {
            bool titleAssetsReady = titleArtwork != null && titleArtwork.texture != null
                && titleLogo != null && titleLogo.texture != null && titleStart != null && titleStart.texture != null;
            if (screen == ScreenMode.Title) titleAssetsVerified |= titleAssetsReady;
            if (screen == ScreenMode.MainMenu && mainMenuView != null)
            {
                mainMenuArtworkVerified |= mainMenuView.ArtworkReady;
                mainMenuMaxSelectionChanges = Mathf.Max(mainMenuMaxSelectionChanges, mainMenuView.SelectionChangeCount);
            }
            if (exitAt > 0 && Time.realtimeSinceStartup >= exitAt)
            {
                int expectedModels = screen == ScreenMode.Title || screen == ScreenMode.MainMenu ? 0
                    : screen == ScreenMode.Intro || screen == ScreenMode.Selection ? 1 : 2;
                bool passed = errors == 0 && (!selectionTest || (selectionTestCompleted && selectionSwitches > 0)) && arena.LoadedModels == expectedModels && (!smoke || (smokeEnded && expectedModels == 2))
                    && (!titleTest || (titleAssetsVerified && titleBlinkCycles >= 2
                        && titlePromptWasVisible && titlePromptWasHidden))
                    && (!titleAudioTest || (titleAudio != null && titleAudio.MusicReady && titleAudio.ListenerReady
                        && titleAudio.PlaybackVerified && titleAudio.LoopEnabled && titleAudio.CompletedLoops >= 1
                        && screen != ScreenMode.Title && titleAudio.FadeOutCompleted
                        && (titleAudio.ReadyCries == 0 || titleAudio.CriesPlayed > 0)))
                    && (!mainMenuTest || (mainMenuTestCompleted && mainMenuChecksPassed && mainMenuArtworkVerified
                        && mainMenuGuardVerified && mainMenuMusicContinuity && mainMenuCursorVerified
                        && mainMenuNavigationVerified && mainMenuPointerVerified && mainMenuMouseClickVerified
                        && mainMenuRoutes == 4 && mainMenuRouteMask == 15))
                    && (!menuAudioTest || (menuAudio != null && menuAudio.MusicReady && menuAudio.ListenerReady
                        && menuAudio.PlaybackVerified && menuAudio.LoopEnabled && menuAudio.CompletedLoops >= 1
                        && screen == ScreenMode.Battle && menuAudio.FadeOutCompleted && !menuAudio.MusicPlaying
                        && titleAudio != null && titleAudio.FadeOutCompleted && !titleAudio.MusicPlaying));
                Debug.Log((smoke ? "[smoke-result]" : "[runtime-result]") + " errors=" + errors + " models=" + arena.LoadedModels
                    + " battleEnded=" + smokeEnded + " screen=" + screen + " titleTest=" + titleTest
                    + " titleTextures=" + titleAssetsReady + " titleAssetsVerified=" + titleAssetsVerified
                    + " blinkCycles=" + titleBlinkCycles + " blinkVisible=" + titlePromptWasVisible
                    + " blinkHidden=" + titlePromptWasHidden + " titleAudioTest=" + titleAudioTest
                    + (titleAudio != null ? titleAudio.Diagnostics : " titleAudio=uninitialized")
                    + " mainMenuTest=" + mainMenuTest + " mainMenuChecks=" + mainMenuChecksPassed
                    + " mainMenuCompleted=" + mainMenuTestCompleted + " menuArtwork=" + mainMenuArtworkVerified
                    + " menuGuard=" + mainMenuGuardVerified + " menuGuardBlocks=" + mainMenuGuardBlocks
                    + " menuMusicContinuity=" + mainMenuMusicContinuity + " menuCursor=" + mainMenuCursorVerified
                    + " menuNavigation=" + mainMenuNavigationVerified + " menuPointer=" + mainMenuPointerVerified
                    + " menuMouseClick=" + mainMenuMouseClickVerified + " menuAudioTest=" + menuAudioTest
                    + (menuAudio != null ? menuAudio.Diagnostics : " menuAudio=uninitialized")
                    + " menuSelectionChanges=" + mainMenuMaxSelectionChanges + " menuRoutes=" + mainMenuRoutes
                    + " menuRouteMask=" + mainMenuRouteMask + " selectionTest=" + selectionTest
                    + " selectionCompleted=" + selectionTestCompleted + " selectionSwitches=" + selectionSwitches + " passed=" + passed);
                Application.Quit(passed ? 0 : 1); exitAt = 0;
            }
            if (screen == ScreenMode.Title && titleStart != null)
            {
                float elapsed = Time.unscaledTime - titleShownAt;
                float phase = Mathf.Repeat(elapsed, 1.8f);
                titleBlinkCycles = Mathf.FloorToInt(elapsed / 1.8f);
                float visibility = phase < 1.35f ? 1f : phase < 1.45f ? 1f - (phase - 1.35f) / .1f
                    : phase < 1.65f ? 0f : (phase - 1.65f) / .15f;
                titleStart.color = new Color(1f, 1f, 1f, Mathf.Clamp01(visibility));
                float pulse = 1f + Mathf.Sin(elapsed * Mathf.PI * 2f / 1.8f) * .008f;
                titleStart.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
                titlePromptWasVisible |= visibility >= .999f;
                titlePromptWasHidden |= visibility <= .001f;
            }
            if (titleAudioTest && screen == ScreenMode.Title && exitAt > 0 && Time.realtimeSinceStartup >= exitAt - 2.5f)
            {
                Debug.Log("[title-audio-transition] leaving title to verify fade-out and source stop");
                StartBattle();
            }
            if (menuAudioTest && screen == ScreenMode.MainMenu && exitAt > 0
                && Time.realtimeSinceStartup >= exitAt - 3f)
            {
                Debug.Log("[menu-audio-transition] leaving menu to verify fade-out and source stop");
                OnModeSelected(0);
            }
            if (controls == null) return;
            if (screen == ScreenMode.Intro && controls.StartPressed)
            {
                if (introRoutine != null) StopCoroutine(introRoutine);
                introRoutine = null;
                if (introWipe != null) introWipe.gameObject.SetActive(false);
                ShowTitle();
            }
            else if (!busy && screen == ScreenMode.Title && controls.StartPressed) ShowMainMenu();
            else if (!busy && screen == ScreenMode.MainMenu && controls.CancelPressed) ShowTitle();
            else if (!busy && screen == ScreenMode.Selection && controls.CancelPressed) ShowMainMenu();
            else if (!busy && (screen == ScreenMode.Battle || screen == ScreenMode.Result)
                     && (controls.CancelPressed || controls.PausePressed)) TogglePause();
        }

        void CreateCanvas()
        {
            var canvasObject = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            canvasRoot = canvasObject.GetComponent<RectTransform>();
            chromeHeader = Rect(canvasRoot, "Brand header", 0, 0, 1600, 80);
            Panel(chromeHeader, "Top bar", 0, 0, 1600, 80, ink);
            Label(chromeHeader, "AEROSTADIUM", 44, 18, 700, 45, 32, Color.white, true);
            Label(chromeHeader, "COMBAT SOLO", 1150, 18, 405, 28, 18, gold, true, TextAnchor.MiddleRight);
            Label(chromeHeader, "Projet Pokémon indépendant et non officiel", 1030, 47, 525, 22, 13, muted, false, TextAnchor.MiddleRight);
            Panel(chromeHeader, "Gold rule", 44, 77, 1510, 2, gold);
            controlFooter = Rect(canvasRoot, "Control footer", 0, 846, 1600, 54);
            Panel(controlFooter, "Control strip", 0, 0, 1600, 54, ink);
            hints = Label(controlFooter, "", 44, 12, 1240, 28, 18, Color.white);

        }

        void CreateInputs()
        {
            var eventObject = new GameObject("Controls", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = eventObject.GetComponent<InputSystemUIInputModule>(); module.AssignDefaultActions();
            module.deselectOnBackgroundClick = false;
            controls = eventObject.AddComponent<ControllerHints>();
            controls.Changed += RefreshHints; controls.Initialize(module); RefreshHints();
        }

        void RefreshHints()
        {
            if (hints == null || controls == null) return;
            if (screen == ScreenMode.Intro)
                hints.text = controls.DeviceName + "   ·   [ " + controls.StartLabel + " ] Passer l’introduction";
            else if (screen == ScreenMode.Title)
            {
                hints.text = controls.DeviceName + "   ·   [ " + controls.StartLabel + " ] Ouvrir le menu";
                if (titleControlHint != null) titleControlHint.text = hints.text;
            }
            else if (screen == ScreenMode.MainMenu && mainMenuView != null)
            {
                bool gamepad = controls.Connected && controls.UsingGamepad;
                mainMenuView.SetInputPresentation(gamepad, gamepad ? controls.DeviceName : "Clavier / souris",
                    gamepad ? controls.Accept : "Entrée", gamepad ? controls.Back : "Échap", gamepad ? "Stick / croix directionnelle" : "Flèches");
            }
            else if (screen == ScreenMode.Selection && selectionView != null)
            {
                bool gamepad = controls.Connected && controls.UsingGamepad;
                selectionView.SetInputPresentation(gamepad, gamepad ? controls.DeviceName : "Clavier / souris",
                    gamepad ? controls.Accept : "Entrée", gamepad ? controls.Back : "Échap");
            }
            else
            {
                string navigation = controls.Connected ? "Stick / D-pad" : "Flèches";
                string acceptColor = controls.Family == ControllerFamily.PlayStation ? "77BAFF" : "8FE0AB";
                hints.text = controls.DeviceName + "   ·   <color=#" + acceptColor + ">[ " + controls.Accept + " ]</color> Choisir"
                    + "   ·   <color=#FFACA4>[ " + controls.Back + " ]</color> Retour   ·   " + navigation + " Naviguer";
            }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && buttons.Count > 0)
                EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
        }

        void ResetPage()
        {
            if (screen != ScreenMode.Title && titleAudio != null) titleAudio.LeaveTitle();
            if (screen != ScreenMode.MainMenu && screen != ScreenMode.Selection && menuAudio != null) menuAudio.LeaveMenu();
            ReleaseSelectionPreview();
            selectionView = null;
            if (page != null) { page.gameObject.SetActive(false); Destroy(page.gameObject); }
            if (pausePanel != null) Destroy(pausePanel.gameObject);
            paused = false; buttons.Clear();
            titleArtwork = titleLogo = titleStart = null;
            titleControlHint = null;
            titleStartButton = null; mainMenuView = null;
            page = Rect(canvasRoot, "Current screen", 0, 0, 1600, 900);
        }

        IEnumerator PlayIntro()
        {
            screen = ScreenMode.Intro; busy = true;
            chromeHeader.gameObject.SetActive(false); controlFooter.gameObject.SetActive(true);
            ResetPage();
            var slate = Panel(page, "Intro slate", 48, 56, 720, 152, new Color(.025f, .055f, .10f, .78f));
            Label(slate, "AEROSTADIUM  ·  INTRODUCTION", 24, 13, 660, 30, 17, gold, true);
            introCaption = Label(slate, "LE STADE S'ÉVEILLE", 24, 49, 660, 48, 32, Color.white, true);
            Label(slate, "151 POKÉMON DE KANTO · UNE NOUVELLE ARÈNE", 24, 106, 668, 24, 16, muted, true);
            var available = new List<IntroEntry>();
            foreach (var entry in IntroRoster) if (arena.HasPokemonModel(entry.Species)) available.Add(entry);
            Debug.Log("[intro-assets] available=" + available.Count + "/18");
            if (available.Count == 0)
            {
                introRoutine = null; ShowTitle(); yield break;
            }
            CreateIntroWipe();
            IntroEntry opening = available[0];
            arena.ShowPokemon(0, opening.Species, true);
            introCaption.text = "GÉNÉRATION " + opening.Generation + "  ·  " + opening.Name;
            yield return arena.PlayOpeningShot(2.55f);
            yield return arena.PlayShowcase(0, 1.15f);
            for (int i = 1; i < available.Count; i++)
            {
                IntroEntry entry = available[i];
                int wipeMode = (i - 1) % 3;
                yield return PlayIntroWipe(wipeMode, false, .24f);
                arena.ShowPokemon(0, entry.Species, true);
                introCaption.text = "GÉNÉRATION " + entry.Generation + "  ·  " + entry.Name;
                Debug.Log("[intro-cut] mode=" + (wipeMode == 0 ? "horizontal" : wipeMode == 1 ? "vertical" : "circular") + " species=" + entry.Species);
                yield return null;
                yield return PlayIntroWipe(wipeMode, true, .24f);
                yield return arena.PlayShowcase(0, .95f);
            }
            introRoutine = null;
            ShowTitle();
        }

        void CreateIntroWipe()
        {
            Shader shader = Shader.Find("AeroStadium/CinematicWipe");
            if (shader == null)
            {
                Debug.LogError("Cinematic wipe shader missing.");
                return;
            }
            introWipeMaterial = new Material(shader);
            var imageObject = new GameObject("Intro cinematic wipe", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            introWipe = imageObject.GetComponent<RawImage>();
            RectTransform rect = introWipe.rectTransform;
            rect.SetParent(canvasRoot, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            introWipe.texture = Texture2D.whiteTexture;
            introWipe.material = introWipeMaterial;
            introWipe.raycastTarget = false;
            introWipeMaterial.SetColor("_Color", new Color(.006f, .016f, .045f, 1f));
            introWipeMaterial.SetColor("_Accent", gold);
            introWipeMaterial.SetFloat("_Progress", 1f);
            introWipe.gameObject.SetActive(false);
        }

        IEnumerator PlayIntroWipe(int mode, bool reveal, float duration)
        {
            if (introWipe == null || introWipeMaterial == null) yield break;
            introWipe.gameObject.SetActive(true);
            introWipeMaterial.SetFloat("_Mode", mode);
            introWipeMaterial.SetColor("_Accent", mode == 1 ? new Color(.3f, .78f, 1f) : mode == 2 ? new Color(1f, .82f, .43f) : gold);
            float from = reveal ? 0f : 1f;
            float to = reveal ? 1f : 0f;
            introWipeMaterial.SetFloat("_Progress", from);
            duration = Mathf.Max(.05f, duration);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                introWipeMaterial.SetFloat("_Progress", Mathf.Lerp(from, to, t));
                yield return null;
            }
            introWipeMaterial.SetFloat("_Progress", to);
            if (reveal) introWipe.gameObject.SetActive(false);
        }

        void ShowTitle()
        {
            screen = ScreenMode.Title; busy = false;
            chromeHeader.gameObject.SetActive(false); controlFooter.gameObject.SetActive(false);
            ResetPage();
            arena.ClearPokemon(0); arena.ClearPokemon(1); arena.gameObject.SetActive(false);
            titleShownAt = Time.unscaledTime; titleBlinkCycles = 0;
            titlePromptWasVisible = titlePromptWasHidden = false;
            titleAssetsVerified = false;
            if (titleAudio == null) titleAudio = gameObject.AddComponent<TitleScreenAudio>();
            titleAudio.ContinueFrontEnd();

            titleArtwork = TitleImage(page, "Original title artwork", "UI/AeroStadiumTitle", 0, 0, 1600, 900,
                AspectRatioFitter.AspectMode.EnvelopeParent);
            // UV crops follow the visible bounds of the current transparent PNGs.
            titleLogo = TitleImage(page, "AeroStadium illustrated logo", "UI/AeroStadiumLogo", 430, 36, 740, 345,
                AspectRatioFitter.AspectMode.FitInParent, new Rect(10f / 1774f, 31f / 887f, 1763f / 1774f, 820f / 887f));
            var hitbox = Rect(page, "Start prompt hit target", 390, 768, 820, 112);
            var hitGraphic = hitbox.gameObject.AddComponent<Image>();
            hitGraphic.color = new Color(1f, 1f, 1f, .001f);
            hitGraphic.raycastTarget = true;
            var start = hitbox.gameObject.AddComponent<Button>();
            start.targetGraphic = hitGraphic; start.transition = Selectable.Transition.None;
            titleStartButton = start;
            start.onClick.AddListener(() => { if (screen == ScreenMode.Title && !busy) ShowMainMenu(); });
            titleStart = TitleImage(hitbox, "Illustrated Start prompt", "UI/AeroStadiumStart", 140, 24, 540, 64,
                AspectRatioFitter.AspectMode.FitInParent, new Rect(39f / 2172f, 231f / 724f, 2093f / 2172f, 274f / 724f));
            titleControlHint = Label(page, "", 420, 880, 760, 20, 13, Color.white, false, TextAnchor.MiddleCenter);
            var hintShadow = titleControlHint.gameObject.AddComponent<Shadow>();
            hintShadow.effectColor = new Color(.02f, .04f, .08f, .9f);
            hintShadow.effectDistance = new Vector2(1f, -1f);
            Select(start); RefreshHints();
            Debug.Log("[title-ready] separate background, AeroStadium logo and blinking Start artwork; cycle=1.8s");
        }

        void ShowMainMenu()
        {
            if (selectionView != null)
            {
                var draft = selectionView.GetTeam();
                selectedTeam = draft.Length == 0 ? null : draft;
            }
            screen = ScreenMode.MainMenu; busy = false;
            chromeHeader.gameObject.SetActive(false); controlFooter.gameObject.SetActive(false);
            ResetPage();
            arena.ClearPokemon(0); arena.ClearPokemon(1); arena.gameObject.SetActive(false);
            if (menuAudio == null) menuAudio = gameObject.AddComponent<MainMenuAudio>();
            menuAudio.EnterMenu();
            mainMenuOpenedFrame = Time.frameCount; mainMenuOpenedAt = Time.unscaledTime;
            mainMenuView = page.gameObject.AddComponent<MainMenuView>();
            mainMenuView.Build(page, font, OnModeSelected, ShowTitle);
            foreach (Button button in mainMenuView.Buttons) buttons.Add(button);
            RefreshHints();
            Debug.Log("[main-menu-ready] buttons=" + buttons.Count + " artwork=" + mainMenuView.ArtworkReady
                + " selected=" + mainMenuView.SelectedIndex + " menuMusicSample=" + menuAudio.PlaybackSample);
        }

        void OnModeSelected(int index)
        {
            if (screen != ScreenMode.MainMenu || mainMenuView == null || index < 0 || index >= 4) return;
            // Start/Enter opening this page cannot also submit its first button.
            if (Time.frameCount <= mainMenuOpenedFrame + 1 || Time.unscaledTime - mainMenuOpenedAt < .25f)
            {
                mainMenuGuardBlocks++;
                Debug.Log("[main-menu-guard] opening input ignored mode=" + index);
                return;
            }
            mainMenuRoutes++; mainMenuRouteMask |= 1 << index;
            // Legacy audio/menu reviews still use their established simulation routes.
            Debug.Log("[main-menu-route] mode=" + index + " destination=" + (mainMenuTest || menuAudioTest ? "Battle" : "Selection") + " routeMask=" + mainMenuRouteMask);
            if (mainMenuTest || menuAudioTest) StartBattle();
            else ShowSelection();
        }

        void MenuCheck(bool condition, string label)
        {
            if (condition) Debug.Log("[main-menu-check] " + label + " passed=True");
            else
            {
                mainMenuChecksPassed = false;
                Debug.LogError("[main-menu-check] " + label + " passed=False");
            }
        }

        IEnumerator TestMainMenu()
        {
            try
            {
                yield return new WaitForSecondsRealtime(4f);
                MenuCheck(screen == ScreenMode.Title && titleStartButton != null, "title Start button available");
                if (titleStartButton == null) yield break;
                titleStartButton.onClick.Invoke();
                MenuCheck(screen == ScreenMode.MainMenu && mainMenuView != null, "Start opens mode menu");
                if (mainMenuView == null) yield break;
                mainMenuView.Buttons[0].onClick.Invoke();
                mainMenuGuardVerified = screen == ScreenMode.MainMenu && mainMenuRoutes == 0 && mainMenuGuardBlocks > 0;
                MenuCheck(mainMenuGuardVerified, "opening input cannot start battle");
                yield return new WaitForSecondsRealtime(1.3f);
                MenuCheck(mainMenuView != null && mainMenuView.Buttons.Count == 4 && mainMenuView.ArtworkReady
                    && arena.LoadedModels == 0 && !chromeHeader.gameObject.activeSelf && !controlFooter.gameObject.activeSelf,
                    "four illustrated modes without battle HUD or models");
                mainMenuMusicContinuity = menuAudio != null && menuAudio.MusicReady && menuAudio.MusicPlaying
                    && menuAudio.PlaybackSample > 0 && menuAudio.PlaybackVerified
                    && titleAudio.FadeOutCompleted && !titleAudio.MusicPlaying;
                MenuCheck(mainMenuMusicContinuity, "menu soundtrack takes over from title music");

                mainMenuVirtualPad = InputSystem.AddDevice<Gamepad>();
                mainMenuView.SelectMode(0);
                InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState().WithButton(GamepadButton.DpadDown));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState());
                yield return new WaitForSecondsRealtime(.2f);
                mainMenuNavigationVerified = mainMenuView != null && mainMenuView.SelectedIndex == 1;
                mainMenuCursorVerified = controls.UsingGamepad && mainMenuView != null && mainMenuView.CursorVisible;
                MenuCheck(mainMenuNavigationVerified, "real UI D-pad navigation selects second mode");
                MenuCheck(mainMenuCursorVerified, "controller selection Poké Ball appears");
                yield return new WaitForSecondsRealtime(12f);

                mainMenuVirtualKeyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(mainMenuVirtualKeyboard, new KeyboardState(Key.UpArrow));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mainMenuVirtualKeyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(.2f);
                mainMenuPointerVerified = !controls.UsingGamepad && mainMenuView != null
                    && !mainMenuView.CursorVisible && mainMenuView.SelectedIndex == 0;
                MenuCheck(mainMenuPointerVerified, "keyboard navigation hides controller cursor");
                if (mainMenuView != null)
                    mainMenuMaxSelectionChanges = Mathf.Max(mainMenuMaxSelectionChanges, mainMenuView.SelectionChangeCount);

                InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState().WithButton(GamepadButton.East));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState());
                yield return new WaitForSecondsRealtime(1.3f);
                bool returnedToTitle = screen == ScreenMode.Title && titleStartButton != null
                    && titleAudio.MusicPlaying && titleAudio.PlaybackSample > 0 && titleAudio.PlaybackVerified
                    && menuAudio.FadeOutCompleted && !menuAudio.MusicPlaying;
                MenuCheck(returnedToTitle, "controller Back restores title music and stops menu music");
                mainMenuMusicContinuity &= returnedToTitle;
                RemoveMainMenuTestDevices();
                if (titleStartButton != null) titleStartButton.onClick.Invoke();
                else ShowMainMenu();
                yield return new WaitForSecondsRealtime(10f);

                for (int mode = 0; mode < 4; mode++)
                {
                    if (screen != ScreenMode.MainMenu) ShowMainMenu();
                    yield return new WaitForSecondsRealtime(2f);
                    if (mainMenuView == null) { MenuCheck(false, "menu exists for mode " + mode); yield break; }
                    mainMenuView.SelectMode(mode);
                    yield return new WaitForSecondsRealtime(1f);
                    if (mode == 3)
                    {
                        mainMenuView.SelectMode(0);
                        mainMenuVirtualMouse = InputSystem.AddDevice<Mouse>();
                        var buttonRect = mainMenuView.Buttons[mode].GetComponent<RectTransform>();
                        Vector2 position = RectTransformUtility.WorldToScreenPoint(null,
                            buttonRect.TransformPoint(buttonRect.rect.center));
                        LogMenuTestPointer("before-position", position);
                        InputSystem.QueueStateEvent(mainMenuVirtualMouse, new MouseState { position = position });
                        yield return null; yield return null;
                        LogMenuTestPointer("after-position", position);
                        MenuCheck(mainMenuView != null && mainMenuView.SelectedIndex == mode && !controls.UsingGamepad,
                            "mouse hover selects Options through UI raycast");
                        InputSystem.QueueStateEvent(mainMenuVirtualMouse,
                            new MouseState { position = position }.WithButton(MouseButton.Left));
                        yield return null; yield return null;
                        LogMenuTestPointer("after-press", position);
                        InputSystem.QueueStateEvent(mainMenuVirtualMouse, new MouseState { position = position });
                        yield return null; yield return null;
                        LogMenuTestPointer("after-release", position);
                        mainMenuMouseClickVerified = screen == ScreenMode.Battle && (mainMenuRouteMask & (1 << mode)) != 0;
                        MenuCheck(mainMenuMouseClickVerified, "mouse click activates Options through UI event module");
                    }
                    else mainMenuView.Buttons[mode].onClick.Invoke();
                    MenuCheck(screen == ScreenMode.Battle && arena.LoadedModels == 2 && (mainMenuRouteMask & (1 << mode)) != 0,
                        "mode " + mode + " launches simulation");
                    yield return new WaitForSecondsRealtime(1.5f);
                    MenuCheck(titleAudio.FadeOutCompleted && !titleAudio.MusicPlaying
                        && menuAudio.FadeOutCompleted && !menuAudio.MusicPlaying, "both soundtracks stop before battle mode " + mode);
                }
                mainMenuTestCompleted = true;
                MenuCheck(mainMenuRoutes == 4 && mainMenuRouteMask == 15, "all four buttons route to battle");
                Debug.Log("[main-menu-test-complete] routes=" + mainMenuRoutes + " routeMask=" + mainMenuRouteMask
                    + " artwork=" + mainMenuArtworkVerified + " cursor=" + mainMenuCursorVerified
                    + " navigation=" + mainMenuNavigationVerified + " checks=" + mainMenuChecksPassed);
            }
            finally
            {
                RemoveMainMenuTestDevices();
                RestoreMainMenuTestInputs();
            }
        }

        void LogMenuTestPointer(string stage, Vector2 position)
        {
            EventSystem eventSystem = EventSystem.current;
            var module = eventSystem != null ? eventSystem.GetComponent<InputSystemUIInputModule>() : null;
            var raycasts = new List<RaycastResult>();
            if (eventSystem != null)
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, raycasts);
            string hits = "";
            foreach (RaycastResult hit in raycasts)
            {
                if (hits.Length > 0) hits += " | ";
                hits += hit.gameObject.name + ":depth=" + hit.depth + ":sorting=" + hit.sortingOrder;
            }
            InputAction point = module != null ? module.point?.action : null;
            InputAction click = module != null ? module.leftClick?.action : null;
            Canvas canvas = canvasRoot != null ? canvasRoot.GetComponent<Canvas>() : null;
            Debug.Log("[main-menu-pointer-debug] stage=" + stage + " screen=" + screen
                + " position=" + position + " screenSize=" + Screen.width + "x" + Screen.height
                + " canvasScale=" + (canvas != null ? canvas.scaleFactor.ToString("F3") : "missing")
                + " focused=" + Application.isFocused + " eventFocused=" + (eventSystem != null && eventSystem.isFocused)
                + " virtualMouse=" + (mainMenuVirtualMouse != null ? mainMenuVirtualMouse.deviceId.ToString() : "missing")
                + " virtualEnabled=" + (mainMenuVirtualMouse != null && mainMenuVirtualMouse.enabled)
                + " virtualPosition=" + (mainMenuVirtualMouse != null ? mainMenuVirtualMouse.position.ReadValue().ToString() : "missing")
                + " virtualButton=" + (mainMenuVirtualMouse != null && mainMenuVirtualMouse.leftButton.isPressed)
                + " currentMouse=" + (Mouse.current != null ? Mouse.current.deviceId.ToString() : "missing")
                + " pointEnabled=" + (point != null && point.enabled)
                + " pointDevice=" + (point?.activeControl != null ? point.activeControl.device.deviceId.ToString() : "missing")
                + " pointValue=" + (point != null ? point.ReadValue<Vector2>().ToString() : "missing")
                + " clickEnabled=" + (click != null && click.enabled)
                + " clickDevice=" + (click?.activeControl != null ? click.activeControl.device.deviceId.ToString() : "missing")
                + " clickValue=" + (click != null ? click.ReadValue<float>().ToString("F1") : "missing")
                + " pointerOver=" + (eventSystem != null && mainMenuVirtualMouse != null && eventSystem.IsPointerOverGameObject(mainMenuVirtualMouse.deviceId))
                + " selected=" + (eventSystem != null && eventSystem.currentSelectedGameObject != null ? eventSystem.currentSelectedGameObject.name : "missing")
                + " hits=" + (hits.Length > 0 ? hits : "none"));
        }

        IEnumerator TestMenuAudio()
        {
            yield return new WaitForSecondsRealtime(4f);
            if (titleStartButton == null)
            {
                Debug.LogError("[menu-audio-test] title Start button missing");
                yield break;
            }
            titleStartButton.onClick.Invoke();
            Debug.Log("[menu-audio-test] main menu entered; waiting for full loop before battle");
        }

        void IsolateMainMenuTestInputs()
        {
            if ((!mainMenuTest && !menuAudioTest && !selectionTest) || mainMenuInputsIsolated) return;
            mainMenuInputsIsolated = true;
            // Only Unity's input frontend is suspended. Windows continues to
            // receive keyboard, mouse and controller events normally.
            InputSystem.onDeviceChange += OnMainMenuTestDeviceChange;
            var devices = new List<InputDevice>(InputSystem.devices);
            foreach (InputDevice device in devices) SuspendMainMenuTestDevice(device);
            Debug.Log("[main-menu-test-inputs-isolated] physicalDevices=" + mainMenuSuppressedDevices.Count
                + " frontendOnly=True");
        }

        void OnMainMenuTestDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (mainMenuInputsIsolated && (change == InputDeviceChange.Added || change == InputDeviceChange.Enabled
                || change == InputDeviceChange.Reconnected))
                SuspendMainMenuTestDevice(device);
        }

        void SuspendMainMenuTestDevice(InputDevice device)
        {
            if (device == null || !device.native || !device.added || !device.enabled) return;
            if (!(device is Gamepad || device is Keyboard || device is Mouse || device is Joystick
                || device is Touchscreen || device is Pen)) return;
            if (!mainMenuSuppressedDevices.Contains(device)) mainMenuSuppressedDevices.Add(device);
            InputSystem.DisableDevice(device, keepSendingEvents: true);
        }

        void RestoreMainMenuTestInputs()
        {
            if (!mainMenuInputsIsolated && mainMenuSuppressedDevices.Count == 0) return;
            mainMenuInputsIsolated = false;
            InputSystem.onDeviceChange -= OnMainMenuTestDeviceChange;
            int restored = 0;
            foreach (InputDevice device in mainMenuSuppressedDevices)
            {
                if (device == null || !device.added || device.enabled) continue;
                InputSystem.EnableDevice(device);
                restored++;
            }
            mainMenuSuppressedDevices.Clear();
            Debug.Log("[main-menu-test-inputs-restored] physicalDevices=" + restored + " frontendOnly=True");
        }

        void RemoveMainMenuTestDevices()
        {
            if (mainMenuVirtualPad != null && mainMenuVirtualPad.added) InputSystem.RemoveDevice(mainMenuVirtualPad);
            if (mainMenuVirtualKeyboard != null && mainMenuVirtualKeyboard.added) InputSystem.RemoveDevice(mainMenuVirtualKeyboard);
            if (mainMenuVirtualMouse != null && mainMenuVirtualMouse.added) InputSystem.RemoveDevice(mainMenuVirtualMouse);
            mainMenuVirtualPad = null; mainMenuVirtualKeyboard = null; mainMenuVirtualMouse = null;
        }

        RawImage TitleImage(Transform parent, string name, string resourcePath, float x, float y, float width, float height,
            AspectRatioFitter.AspectMode aspectMode = AspectRatioFitter.AspectMode.FitInParent, Rect? uvRect = null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogError("Title artwork missing: Resources/" + resourcePath + ".png");
                return null;
            }
            RectTransform container = Rect(parent, name + " layout", x, y, width, height);
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            RawImage image = imageObject.GetComponent<RawImage>();
            image.rectTransform.SetParent(container, false);
            image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = Vector2.zero; image.rectTransform.offsetMax = Vector2.zero;
            image.texture = texture; image.raycastTarget = false;
            image.uvRect = uvRect ?? new Rect(0f, 0f, 1f, 1f);
            var aspect = imageObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = aspectMode;
            aspect.aspectRatio = texture.width * image.uvRect.width / (texture.height * image.uvRect.height);
            return image;
        }
        IEnumerator TestSelection()
        {
            yield return new WaitForSecondsRealtime(3f);
            titleStartButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.5f);
            mainMenuView.Buttons[0].onClick.Invoke();
            MenuCheck(screen == ScreenMode.Selection && selectionView != null, "solo opens team selection");
            if (selectionView == null) yield break;
            MenuCheck(selectionView.FilteredCount == 151 && selectionView.PageCount == 7
                && selectionView.Cards.Count == 24 && !selectionView.CanLaunch, "151 rentals and seven pages");
            yield return new WaitForSecondsRealtime(4f);
            mainMenuVirtualMouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mainMenuVirtualMouse, new MouseState { position = new Vector2(-100, -100) });
            yield return null; yield return null;
            mainMenuVirtualPad = InputSystem.AddDevice<Gamepad>();
            EventSystem.current.SetSelectedGameObject(selectionView.Cards[0].gameObject);
            InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState().WithButton(GamepadButton.DpadRight));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState());
            yield return new WaitForSecondsRealtime(.3f);
            Debug.Log("[selection-pad-diagnostics] focus=" + selectionView.FocusedSpecies + " cursor=" + selectionView.CursorVisible
                + " gamepad=" + controls.UsingGamepad + " selected=" + EventSystem.current.currentSelectedGameObject?.name);
            MenuCheck(selectionView.FocusedSpecies == 2 && selectionView.CursorVisible && controls.UsingGamepad,
                "selection D-pad moves focus and displays controller cursor");
            InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState().WithButton(GamepadButton.South));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mainMenuVirtualPad, new GamepadState());
            yield return new WaitForSecondsRealtime(.3f);
            MenuCheck(selectionView.TeamCount == 1 && selectionView.GetTeam()[0].speciesId == 2,
                "controller confirmation adds the focused Pokémon");
            selectionView.RemoveAt(0);
            selectionView.ChangePage(6);
            MenuCheck(selectionView.PageIndex == 6 && selectionView.Cards.Count == 7, "last page covers IDs145 through151");
            yield return new WaitForSecondsRealtime(5f);
            selectionView.SetSearch("151");
            MenuCheck(selectionView.FilteredCount == 1 && selectionView.Cards.Count == 1, "Pokédex number search");
            selectionView.SetSearch("electhor");
            MenuCheck(selectionView.FilteredCount == 1, "French search ignores diacritics");
            selectionView.SetSearch("zz-no-result");
            MenuCheck(selectionView.FilteredCount == 0 && !selectionView.CanLaunch, "empty search keeps team validation safe");
            selectionView.SetSearch("");
            yield return null;
            if (mainMenuVirtualMouse == null) mainMenuVirtualMouse = InputSystem.AddDevice<Mouse>();
            Canvas.ForceUpdateCanvases();
            var card = selectionView.Cards[0].GetComponent<RectTransform>();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, card.TransformPoint(card.rect.center));
            InputSystem.QueueStateEvent(mainMenuVirtualMouse, new MouseState { position = position });
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mainMenuVirtualMouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(mainMenuVirtualMouse, new MouseState { position = position });
            yield return null; yield return null;
            MenuCheck(selectionView.TeamCount == 1 && selectionView.GetTeam()[0].speciesId == 1
                && !controls.UsingGamepad, "mouse click selects rental and updates input prompts");
            selectionView.RemoveAt(0);
            foreach (int id in new[] { 6, 9, 3, 25, 94, 149 }) selectionView.TryAdd(id);
            MenuCheck(selectionView.CanLaunch && selectionView.TeamCount == 6 && !selectionView.TryAdd(6)
                && !selectionView.TryAdd(150), "six unique rentals, duplicates and overflow rejected");
            selectionView.RemoveAt(5);
            MenuCheck(!selectionView.CanLaunch, "removing a rental disables battle confirmation");
            selectionView.TryAdd(149);
            selectionView.SetSearch("006");
            yield return new WaitForSecondsRealtime(7f);
            MenuCheck(arena.LoadedModels == 1 && selectionPreview != null && menuAudio.MusicPlaying,
                "one animated preview and continuous menu soundtrack");
            selectionView.StartButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            MenuCheck(screen == ScreenMode.Battle && battle.Team(0).Count == 6 && battle.Team(1).Count == 6
                && battle.Team(0)[0].SpeciesId == 6 && battle.Team(0)[5].SpeciesId == 149
                && selectionPreview == null && arena.ArenaCamera.targetTexture == null
                && !menuAudio.MusicPlaying, "chosen six reach the arena and inspection target is released");
            int turns = 0;
            while (!battle.IsFinished && selectionSwitches == 0 && turns < 35 && Time.realtimeSinceStartup < exitAt - 15f)
            {
                yield return PlayTurn(battle.ChooseAi(0)); turns++;
                yield return new WaitForSecondsRealtime(.3f);
            }
            MenuCheck(selectionSwitches > 0 && visibleMaximumHp[0] == battle.Active(0).MaxHp
                && visibleMaximumHp[1] == battle.Active(1).MaxHp, "KO replacement synchronizes models and health maximums");
            selectionTestCompleted = mainMenuChecksPassed;
            Debug.Log("[selection-review] turns=" + turns + " switches=" + selectionSwitches + " passed=" + selectionTestCompleted);
            // Devices are restored by OnDestroy when the automatic review ends.
            // Leave the resulting battle on screen until the requested review deadline.
        }

        void ReleaseSelectionPreview()
        {
            if (selectionPreview == null) return;
            if (arena != null && arena.ArenaCamera != null)
            {
                arena.ArenaCamera.targetTexture = null;
                arena.ArenaCamera.orthographic = false;
                arena.ArenaCamera.cullingMask = inspectionCameraMask;
                arena.ArenaCamera.backgroundColor = inspectionCameraBackground;
                arena.ArenaCamera.clearFlags = inspectionCameraClear;
                arena.ArenaCamera.ResetAspect();
            }
            selectionPreview.Release(); Destroy(selectionPreview); selectionPreview = null;
        }

        void ShowSelection()
        {
            screen = ScreenMode.Selection; busy = false; ResetPage();
            chromeHeader.gameObject.SetActive(false); controlFooter.gameObject.SetActive(false);
            arena.gameObject.SetActive(true); arena.ClearPokemon(1);
            if (menuAudio == null) menuAudio = gameObject.AddComponent<MainMenuAudio>();
            menuAudio.EnterMenu();
            inspectionCameraMask = arena.ArenaCamera.cullingMask;
            inspectionCameraBackground = arena.ArenaCamera.backgroundColor;
            inspectionCameraClear = arena.ArenaCamera.clearFlags;
            selectionPreview = new RenderTexture(1024, 534, 24, RenderTextureFormat.ARGB32);
            selectionPreview.name = "Animated Pokémon inspection"; selectionPreview.Create();
            arena.ArenaCamera.targetTexture = selectionPreview;
            arena.ArenaCamera.aspect = 1024f / 534f;
            selectionView = page.gameObject.AddComponent<PokemonSelectionView>();
            selectionView.PartnerAdded += id => {
                if (screen == ScreenMode.Selection) { selectedSpecies = id; arena.ShowPokemon(0, id, true); arena.FrameInspection(true); }
            };
            int[] saved = selectedTeam == null ? null : Array.ConvertAll(selectedTeam, member => member.speciesId);
            selectionView.Build(page, font, catalog, selectionPreview,
                id => { if (screen == ScreenMode.Selection) { selectedSpecies = id; arena.ShowPokemon(0, id, true); arena.FrameInspection(); } },
                team => { selectedTeam = team; StartBattle(); }, ShowMainMenu, saved);
            arena.ShowPokemon(0, selectedSpecies, true); arena.FrameInspection(); RefreshHints();
            Debug.Log("[selection-ready] species=" + catalog.species.Length + " team=" + selectionView.TeamCount);
        }

        void StartBattle()
        {
            if (screen == ScreenMode.Battle || busy) return;
            arena.gameObject.SetActive(true);
            int seed = requestedSeed ?? (Environment.TickCount & int.MaxValue);
            ReleaseSelectionPreview();
            var player = selectedTeam ?? new[] { new TeamMember(selectedSpecies, selectedItem) };
            var opponent = new TeamMember[player.Length];
            var rng = new System.Random(seed);
            var chosen = new HashSet<int>();
            for (int i = 0; i < opponent.Length; i++)
            {
                int id;
                do { id = catalog.species[rng.Next(catalog.species.Length)].id; } while (!chosen.Add(id));
                opponent[i] = new TeamMember(id, "none");
            }
            if (selectedTeam == null) opponent[0] = new TeamMember(selectedSpecies, "none");
            battle = new BattleEngine(catalog, player, opponent, seed);
            Debug.Log("[battle-start] species=" + selectedSpecies + " seed=" + seed);
            arena.ShowPokemon(0, battle.Active(0).SpeciesId); arena.ShowPokemon(1, battle.Active(1).SpeciesId);
            visibleMaximumHp[0] = battle.Active(0).MaxHp; visibleMaximumHp[1] = battle.Active(1).MaxHp;
            Debug.Log("[selection-battle-team] player=" + string.Join(",", Array.ConvertAll(player, member => member.speciesId.ToString()))
                + " opponent=" + string.Join(",", Array.ConvertAll(opponent, member => member.speciesId.ToString())));
            screen = ScreenMode.Battle; visibleHp[0] = battle.Active(0).Hp; visibleHp[1] = battle.Active(1).Hp;
            ShowBattle("À toi de jouer. Choisis une attaque.");
        }

        void ShowBattle(string message)
        {
            chromeHeader.gameObject.SetActive(true); controlFooter.gameObject.SetActive(true);
            ResetPage();
            Hud(0, 48, 106, "TON CAMP"); Hud(1, 1100, 106, "ADVERSAIRE · IA");
            turnText = Label(page, "TOUR " + (battle.Turn + 1), 650, 113, 300, 42, 18, gold, true, TextAnchor.MiddleCenter);
            var journal = Panel(page, "Battle message", 48, 660, 560, 153, ink);
            Label(journal, "DANS L’ARÈNE", 22, 13, 520, 26, 14, gold, true);
            logText = Label(journal, message, 22, 45, 514, 92, 22, Color.white);
            var moves = battle.Active(0).Moves;
            for (int i = 0; i < moves.Count; i++)
            {
                int slot = i; var move = moves[i]; string category = CategoryName(move.Definition.category);
                string stats = TypeName(move.Definition.type) + "  ·  " + category
                    + (move.Definition.power > 0 ? "  ·  PUI " + move.Definition.power : "")
                    + "  ·  PP " + move.Pp + "/" + move.MaxPp;
                var button = Button(page, move.Definition.name + "\n<size=15>" + stats + "</size>",
                                    650 + (i % 2) * 461, 660 + (i / 2) * 80, 445, 73, () => ChooseMove(slot), new Color(.075f, .16f, .28f, .98f));
                button.interactable = !busy && !smoke && move.Pp > 0;
            }
            bool exhausted = AllMovesExhausted();
            if (exhausted)
            {
                foreach (var button in buttons) Destroy(button.gameObject);
                buttons.Clear();
                Button(page, "LUTTE\n<size=15>Aucun PP restant · Attaque de secours</size>", 650, 660, 906, 73,
                    () => { if (!busy && !paused) StartCoroutine(PlayTurn(BattleChoice.Struggle())); }, blue).interactable = !busy && !smoke;
            }
            SelectFirstAvailable();
            RefreshHealth(); RefreshHints();
        }

        void Hud(int side, float x, float y, string sideName)
        {
            var pokemon = battle.Active(side);
            var card = Panel(page, "Health " + side, x, y, 452, 119, ink);
            Label(card, sideName, 18, 10, 260, 20, 13, side == 0 ? new Color(.58f, .91f, .71f) : gold, true);
            pokemonNames[side] = Label(card, pokemon.Name, 18, 31, 300, 35, 25, Color.white, true);
            Label(card, "N. " + pokemon.Level, 338, 35, 93, 28, 17, muted, false, TextAnchor.MiddleRight);
            Panel(card, "Health background", 18, 80, 285, 13, new Color(.18f, .23f, .31f));
            health[side] = Panel(card, "Health remaining", 18, 80, 285, 13, new Color(.32f, .87f, .61f)).GetComponent<Image>();
            healthText[side] = Label(card, "", 313, 70, 118, 30, 17, Color.white, false, TextAnchor.MiddleRight);
        }

        void RefreshHealth()
        {
            for (int side = 0; side < 2; side++)
            {
                if (health[side] == null) continue;
                int maximum = visibleMaximumHp[side]; float ratio = Mathf.Clamp01(visibleHp[side] / (float)maximum);
                health[side].rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 285 * ratio);
                health[side].color = ratio > .5f ? new Color(.32f, .87f, .61f) : ratio > .2f ? gold : new Color(.97f, .37f, .36f);
                healthText[side].text = visibleHp[side] + " / " + maximum;
            }
        }

        void ChooseMove(int slot)
        {
            if (smoke || busy || paused || battle.IsFinished) return;
            StartCoroutine(PlayTurn(BattleChoice.Move(slot)));
        }

        IEnumerator PlayTurn(BattleChoice choice)
        {
            busy = true; foreach (var button in buttons) button.interactable = false;
            var hpBeforeTurn = new int[2][];
            for (int side = 0; side < 2; side++)
            {
                hpBeforeTurn[side] = new int[battle.Team(side).Count];
                for (int i = 0; i < battle.Team(side).Count; i++) hpBeforeTurn[side][i] = battle.Team(side)[i].Hp;
            }
            IReadOnlyList<BattleEvent> events;
            try { events = battle.ResolveTurn(choice, battle.ChooseAi(1)); }
            catch (Exception exception) { Debug.LogException(exception); busy = false; ShowBattle("Choix impossible : " + exception.Message); yield break; }
            foreach (var e in events)
            {
                if (logText != null) logText.text = e.Message;
                Debug.Log("[battle-event] " + e.Kind + " " + e.Message);
                if (e.Kind == BattleEventKind.MoveUsed)
                {
                    var move = e.MoveId == 0 ? null : catalog.GetMove(e.MoveId);
                    if (move == null || move.category != "Status") yield return arena.Attack(e.Side, move == null ? "Normal" : move.type, move == null ? "Physical" : move.category);
                    else yield return new WaitForSeconds(Mathf.Clamp(arena.PlayAttackAnimation(e.Side), .35f, 1f));
                }
                else if (e.Kind == BattleEventKind.Damage)
                {
                    int target = e.TargetSide >= 0 ? e.TargetSide : e.Side;
                    visibleHp[target] = Mathf.Max(0, visibleHp[target] - e.Amount); RefreshHealth();
                    yield return arena.Hit(target, e.Amount);
                }
                else if (e.Kind == BattleEventKind.Heal)
                {
                    int target = e.TargetSide >= 0 ? e.TargetSide : e.Side;
                    visibleHp[target] = Mathf.Min(visibleMaximumHp[target], visibleHp[target] + e.Amount); RefreshHealth();
                    yield return new WaitForSeconds(.3f);
                }
                else if (e.Kind == BattleEventKind.Fainted) yield return arena.Faint(e.Side);
                else if (e.Kind == BattleEventKind.Switched)
                {
                    var entrant = battle.Team(e.Side)[e.TeamIndex];
                    visibleMaximumHp[e.Side] = entrant.MaxHp;
                    visibleHp[e.Side] = hpBeforeTurn[e.Side][e.TeamIndex];
                    if (pokemonNames[e.Side] != null) pokemonNames[e.Side].text = entrant.Name;
                    arena.ShowPokemon(e.Side, entrant.SpeciesId); RefreshHealth();
                    selectionSwitches++;
                    Debug.Log("[selection-replacement] side=" + e.Side + " species=" + entrant.SpeciesId
                        + " hp=" + visibleHp[e.Side] + " maxHp=" + visibleMaximumHp[e.Side]);
                    yield return new WaitForSeconds(.65f);
                }
                else yield return new WaitForSeconds(.23f);
            }
            visibleHp[0] = battle.Active(0).Hp; visibleHp[1] = battle.Active(1).Hp;
            visibleMaximumHp[0] = battle.Active(0).MaxHp; visibleMaximumHp[1] = battle.Active(1).MaxHp;
            busy = false;
            if (battle.IsFinished) { smokeEnded = true; ShowResult(); }
            else ShowBattle("Choisis la prochaine attaque.");
        }

        void ShowResult()
        {
            screen = ScreenMode.Result; chromeHeader.gameObject.SetActive(true); controlFooter.gameObject.SetActive(true); ResetPage();
            Hud(0, 48, 106, "TON CAMP"); Hud(1, 1100, 106, "ADVERSAIRE · IA"); RefreshHealth();
            var card = Panel(page, "Battle result", 510, 540, 580, 248, ink);
            Label(card, battle.Winner == 0 ? "VICTOIRE !" : battle.Winner == -1 ? "MATCH NUL" : "DÉFAITE", 30, 20, 520, 60, 42, gold, true, TextAnchor.MiddleCenter);
            Label(card, "Combat terminé en " + battle.Turn + " tours", 30, 91, 520, 32, 18, muted, false, TextAnchor.MiddleCenter);
            var replay = Button(card, "NOUVEAU COMBAT", 45, 152, 490, 65, ShowSelection, blue);
            replay.interactable = !smoke; Select(replay); RefreshHints();
            Debug.Log("[battle-ended] winner=" + battle.Winner + " turns=" + battle.Turn);
        }

        void TogglePause()
        {
            if (smoke) return;
            if (paused)
            {
                Destroy(pausePanel.gameObject); pausePanel = null; paused = false;
                for (int i = 0; i < buttons.Count; i++)
                    buttons[i].interactable = screen != ScreenMode.Battle || AllMovesExhausted() || battle.Active(0).Moves[i].Pp > 0;
                SelectFirstAvailable(); return;
            }
            paused = true; foreach (var button in buttons) button.interactable = false;
            pausePanel = Panel(canvasRoot, "Pause", 510, 275, 580, 335, ink);
            Label(pausePanel, "PAUSE", 35, 26, 510, 65, 35, gold, true, TextAnchor.MiddleCenter);
            var resume = Button(pausePanel, "CONTINUER", 40, 124, 500, 64, TogglePause, blue, false);
            Button(pausePanel, "RETOUR À LA SÉLECTION", 40, 208, 500, 64, ShowSelection, new Color(.17f, .23f, .34f), false);
            Select(resume);
        }

        IEnumerator SmokePlay()
        {
            yield return new WaitForSeconds(2); StartBattle();
            int index = 0;
            while (!battle.IsFinished)
            {
                while (busy) yield return null;
                if (battle.IsFinished) break;
                var choice = BattleChoice.Move(index % battle.Active(0).Moves.Count);
                if (battle.Active(0).Moves[index % battle.Active(0).Moves.Count].Pp == 0) choice = battle.ChooseAi(0);
                yield return PlayTurn(choice); index++;
                yield return new WaitForSeconds(.7f);
                if (index > 100) { Debug.LogError("Smoke battle did not finish"); break; }
            }
        }

        RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }

        RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var rt = Rect(parent, name, x, y, w, h); rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Rect(parent, "Text", x, y, w, h).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.supportRichText = true; label.raycastTarget = false; return label;
        }

        Button Button(Transform parent, string text, float x, float y, float w, float h, Action action, Color color, bool track = true)
        {
            var rt = Panel(parent, "Button " + text.Split('\n')[0], x, y, w, h, color);
            var button = rt.gameObject.AddComponent<Button>(); var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.selectedColor = new Color(1.27f, 1.22f, 1.03f); colors.pressedColor = new Color(.77f, .89f, 1);
            colors.disabledColor = new Color(.5f, .5f, .5f, .6f); button.colors = colors;
            button.targetGraphic = rt.GetComponent<Image>(); button.onClick.AddListener(() => action());
            Label(rt, text, 16, 2, w - 32, h - 4, 22, Color.white, true, TextAnchor.MiddleLeft);
            var outline = rt.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.55f, .73f, .94f, .24f); outline.effectDistance = new Vector2(1, -1);
            if (track) buttons.Add(button); return button;
        }

        static void Select(Button button) { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject); }
        void SelectFirstAvailable()
        {
            foreach (var button in buttons) if (button.interactable) { Select(button); return; }
        }
        bool AllMovesExhausted()
        {
            foreach (var move in battle.Active(0).Moves) if (move.Pp > 0) return false;
            return true;
        }
        static string CategoryName(string category) => category == "Physical" ? "PHYSIQUE" : category == "Special" ? "SPÉCIALE" : "STATUT";
        static string TypeName(string type)
        {
            switch (type) { case "Normal": return "Normal"; case "Fire": return "Feu"; case "Water": return "Eau"; case "Electric": return "Électrik"; case "Grass": return "Plante"; case "Ice": return "Glace"; case "Fighting": return "Combat"; case "Poison": return "Poison"; case "Ground": return "Sol"; case "Flying": return "Vol"; case "Psychic": return "Psy"; case "Bug": return "Insecte"; case "Rock": return "Roche"; case "Ghost": return "Spectre"; case "Dragon": return "Dragon"; case "Dark": return "Ténèbres"; case "Steel": return "Acier"; case "Fairy": return "Fée"; default: return type; }
        }
    }
}
