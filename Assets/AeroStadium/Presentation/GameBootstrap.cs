using System;
using System.Collections;
using System.Collections.Generic;
using AeroStadium.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        enum ScreenMode { Intro, Title, Selection, Battle, Result }
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
        ControllerHints controls;
        RectTransform canvasRoot, page, pausePanel, chromeHeader, controlFooter, promptRect;
        Text hints, logText, turnText, introCaption, startPrompt;
        Coroutine introRoutine;
        Font font;
        ScreenMode screen;
        int selectedSpecies = 6;
        int selectedRosterPage;
        int? requestedSeed;
        string selectedItem = "leftovers";
        bool busy, paused, smoke, smokeEnded;
        int errors;
        float exitAt;

        void Awake()
        {
            Application.logMessageReceived += OnLog;
            string[] args = Environment.GetCommandLineArgs();
            smoke = Array.IndexOf(args, "--smoke-test") >= 0;
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
            CreateCanvas(); CreateInputs();
            int secondsIndex = Array.IndexOf(args, "--seconds");
            if (secondsIndex >= 0 && secondsIndex + 1 < args.Length && int.TryParse(args[secondsIndex + 1], out int seconds))
                exitAt = Time.realtimeSinceStartup + Mathf.Clamp(seconds, 10, 300);
            if (smoke) StartCoroutine(SmokePlay());
            else introRoutine = StartCoroutine(PlayIntro());
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (introWipeMaterial != null) Destroy(introWipeMaterial);
        }
        void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        void Update()
        {
            if (exitAt > 0 && Time.realtimeSinceStartup >= exitAt)
            {
                int expectedModels = screen == ScreenMode.Intro || screen == ScreenMode.Title || screen == ScreenMode.Selection ? 1 : 2;
                bool passed = errors == 0 && arena.LoadedModels == expectedModels && (!smoke || (smokeEnded && expectedModels == 2));
                Debug.Log((smoke ? "[smoke-result]" : "[runtime-result]") + " errors=" + errors + " models=" + arena.LoadedModels
                    + " battleEnded=" + smokeEnded + " passed=" + passed);
                Application.Quit(passed ? 0 : 1); exitAt = 0;
            }
            if (screen == ScreenMode.Title && promptRect != null)
            {
                float wave = (Mathf.Sin(Time.unscaledTime * 3.2f) + 1f) * .5f;
                float pulse = .97f + wave * .035f;
                promptRect.localScale = new Vector3(pulse, pulse, 1);
                if (startPrompt != null)
                {
                    float visibility = .4f + wave * .6f;
                    startPrompt.color = Color.Lerp(new Color(.62f, .88f, 1f, visibility), new Color(1f, .83f, .42f, visibility), wave);
                }
            }
            if (controls == null) return;
            if (screen == ScreenMode.Intro && controls.StartPressed)
            {
                if (introRoutine != null) StopCoroutine(introRoutine);
                introRoutine = null;
                if (introWipe != null) introWipe.gameObject.SetActive(false);
                ShowTitle();
            }
            else if (!busy && screen == ScreenMode.Title && controls.StartPressed) StartBattle();
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
                hints.text = controls.DeviceName + "   ·   [ " + controls.StartLabel + " ] Lancer le combat";
                if (startPrompt != null) startPrompt.text = BuildStartPrompt();
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
            if (page != null) Destroy(page.gameObject);
            if (pausePanel != null) Destroy(pausePanel.gameObject);
            paused = false; buttons.Clear();
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

            Texture2D artwork = Resources.Load<Texture2D>("UI/AeroStadiumTitle");
            if (artwork == null)
                Debug.LogError("Title artwork missing: Resources/UI/AeroStadiumTitle.png");
            else
            {
                var backgroundObject = new GameObject("Original title artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                var background = backgroundObject.GetComponent<RawImage>();
                background.rectTransform.SetParent(page, false);
                background.rectTransform.anchorMin = Vector2.zero; background.rectTransform.anchorMax = Vector2.one;
                background.rectTransform.offsetMin = Vector2.zero; background.rectTransform.offsetMax = Vector2.zero;
                background.texture = artwork; background.raycastTarget = false;
                var aspect = backgroundObject.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspect.aspectRatio = (float)artwork.width / artwork.height;
            }

            Text aero = Label(page, "AERO", 350, 48, 900, 132, 112, new Color(1f, .87f, .49f), true, TextAnchor.MiddleCenter);
            StyleTitleLogo(aero, new Color(.035f, .12f, .31f), new Color(.2f, .78f, 1f));
            Text stadium = Label(page, "STADIUM", 290, 155, 1020, 142, 118, Color.white, true, TextAnchor.MiddleCenter);
            StyleTitleLogo(stadium, new Color(.035f, .12f, .31f), new Color(.32f, .86f, 1f));
            Label(page, "BATTLE ARENA", 450, 292, 700, 42, 25, new Color(.91f, .96f, 1f), true, TextAnchor.MiddleCenter);

            Panel(page, "Logo accent left", 340, 325, 180, 4, new Color(.31f, .82f, 1f, .92f));
            Panel(page, "Logo accent right", 1080, 325, 180, 4, new Color(1f, .79f, .35f, .92f));
            var hitbox = Rect(page, "Start prompt hit target", 390, 724, 820, 94);
            var hitGraphic = hitbox.gameObject.AddComponent<Image>();
            hitGraphic.color = new Color(1f, 1f, 1f, .001f);
            hitGraphic.raycastTarget = true;
            var start = hitbox.gameObject.AddComponent<Button>();
            start.targetGraphic = hitGraphic; start.transition = Selectable.Transition.None;
            start.onClick.AddListener(() => StartBattle());
            promptRect = hitbox;
            startPrompt = Label(hitbox, BuildStartPrompt(), 0, 0, 820, 94, 44, Color.white, true, TextAnchor.MiddleCenter);
            StyleTitleLogo(startPrompt, new Color(.015f, .09f, .25f), new Color(.33f, .8f, 1f));
            Panel(page, "Prompt glint left", 337, 770, 30, 4, new Color(.43f, .83f, 1f, .9f));
            Panel(page, "Prompt glint right", 1233, 770, 30, 4, new Color(.43f, .83f, 1f, .9f));
            Select(start); RefreshHints();
            Debug.Log("[title-ready] original artwork, custom AeroStadium logo, blinking text-only Start prompt");
        }

        string BuildStartPrompt()
        {
            return "APPUYEZ SUR " + (controls != null && !controls.Connected ? "ENTRÉE" : "START");
        }

        static void StyleTitleLogo(Text text, Color outlineColor, Color shadowColor)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = outlineColor; outline.effectDistance = new Vector2(4f, -4f);
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(shadowColor.r, shadowColor.g, shadowColor.b, .9f);
            shadow.effectDistance = new Vector2(7f, -8f);
        }
        void ShowSelection()
        {
            ShowSelection(-1);
        }

        void ShowSelection(int focusIndex)
        {
            screen = ScreenMode.Selection; busy = false; ResetPage();
            chromeHeader.gameObject.SetActive(true); controlFooter.gameObject.SetActive(true);
            arena.ClearPokemon(1); arena.ShowPokemon(0, selectedSpecies, true);
            selectedRosterPage = Mathf.Clamp(selectedRosterPage, 0, (catalog.species.Length - 1) / 10);
            var card = Panel(page, "Choose Pokemon", 48, 147, 650, 630, ink);
            Label(card, "CHOISIS UN POKÉMON", 28, 20, 594, 40, 28, Color.white, true);
            Label(card, "Pokédex de Kanto · 151 espèces · modèles 3D locaux", 28, 61, 594, 25, 16, muted);
            int totalPages = (catalog.species.Length + 9) / 10;
            Button previous = Button(card, "◀ PRÉC.", 28, 94, 122, 40,
                () => { selectedRosterPage = Mathf.Max(0, selectedRosterPage - 1); ShowSelection(0); },
                selectedRosterPage > 0 ? new Color(.12f, .25f, .42f) : new Color(.08f, .12f, .18f));
            previous.interactable = selectedRosterPage > 0;
            Label(card, "PAGE " + (selectedRosterPage + 1) + " / " + totalPages, 160, 94, 324, 40, 17, gold, true, TextAnchor.MiddleCenter);
            Button next = Button(card, "SUIV. ▶", 500, 94, 122, 40,
                () => { selectedRosterPage = Mathf.Min(totalPages - 1, selectedRosterPage + 1); ShowSelection(1); },
                selectedRosterPage + 1 < totalPages ? new Color(.12f, .25f, .42f) : new Color(.08f, .12f, .18f));
            next.interactable = selectedRosterPage + 1 < totalPages;

            Button selectedButton = null;
            int first = selectedRosterPage * 10;
            int last = Mathf.Min(catalog.species.Length, first + 10);
            for (int index = first; index < last; index++)
            {
                var species = catalog.species[index];
                int choice = species.id;
                int column = (index - first) % 2;
                int row = (index - first) / 2;
                int focus = buttons.Count;
                string subtitle = string.Join(" / ", Array.ConvertAll(species.types, TypeName));
                string title = (choice == selectedSpecies ? "● " : "") + "#" + choice.ToString("000") + "  " + species.name
                    + "\n<size=16>" + subtitle + " · " + species.height.ToString("0.0") + " m</size>";
                var button = Button(card, title, 28 + column * 296, 142 + row * 62, 286, 56,
                    () => { selectedSpecies = choice; ShowSelection(focus); },
                    choice == selectedSpecies ? blue : new Color(.1f, .16f, .25f));
                if (choice == selectedSpecies) selectedButton = button;
            }

            Label(card, "OBJET TENU", 28, 458, 594, 22, 16, gold, true);
            int itemIndex = 0;
            foreach (string id in new[] { "none", "leftovers", "lifeorb", "charcoal" })
            {
                string choice = id; var item = catalog.GetItem(id);
                int focus = buttons.Count;
                Button(card, item.name, 28 + (itemIndex % 2) * 296, 482 + (itemIndex / 2) * 45, 286, 40,
                    () => { selectedItem = choice; ShowSelection(focus); },
                    id == selectedItem ? blue : new Color(.1f, .16f, .25f));
                itemIndex++;
            }
            var start = Button(card, "ENTRER DANS L’ARÈNE", 28, 577, 594, 46, StartBattle, new Color(.83f, .59f, .21f));
            Label(page, catalog.GetSpecies(selectedSpecies).name.ToUpperInvariant(), 810, 712, 710, 55, 38, Color.white, true, TextAnchor.MiddleRight);
            Label(page, "Génération I · taille réelle · modèle animé", 810, 769, 710, 28, 17, muted, false, TextAnchor.MiddleRight);
            Select(focusIndex >= 0 && focusIndex < buttons.Count ? buttons[focusIndex] : selectedButton != null ? selectedButton : start);
            RefreshHints();
            if (smoke) foreach (var button in buttons) button.interactable = false;
        }

        void StartBattle()
        {
            if (screen == ScreenMode.Battle || busy) return;
            arena.gameObject.SetActive(true);
            int seed = requestedSeed ?? (Environment.TickCount & int.MaxValue);
            battle = new BattleEngine(catalog, new[] { new TeamMember(selectedSpecies, selectedItem) }, new[] { new TeamMember(selectedSpecies, "none") }, seed);
            Debug.Log("[battle-start] species=" + selectedSpecies + " seed=" + seed);
            arena.ShowPokemon(0, selectedSpecies); arena.ShowPokemon(1, selectedSpecies);
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
            Label(card, pokemon.Name, 18, 31, 300, 35, 25, Color.white, true);
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
                int maximum = battle.Active(side).MaxHp; float ratio = Mathf.Clamp01(visibleHp[side] / (float)maximum);
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
                    if (move == null || move.category != "Status") yield return arena.Attack(e.Side, move == null ? "Normal" : move.type);
                    else yield return new WaitForSeconds(.35f);
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
                    visibleHp[target] = Mathf.Min(battle.Active(target).MaxHp, visibleHp[target] + e.Amount); RefreshHealth();
                    yield return new WaitForSeconds(.3f);
                }
                else if (e.Kind == BattleEventKind.Fainted) yield return arena.Faint(e.Side);
                else yield return new WaitForSeconds(.23f);
            }
            visibleHp[0] = battle.Active(0).Hp; visibleHp[1] = battle.Active(1).Hp;
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
