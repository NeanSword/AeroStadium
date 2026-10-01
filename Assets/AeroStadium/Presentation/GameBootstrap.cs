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
        enum ScreenMode { Selection, Battle, Result }
        readonly Color ink = new Color(.055f, .09f, .16f, .96f);
        readonly Color muted = new Color(.69f, .77f, .88f);
        readonly Color gold = new Color(1f, .78f, .33f);
        readonly Color blue = new Color(.12f, .32f, .63f);
        readonly List<Button> buttons = new List<Button>();
        readonly Image[] health = new Image[2];
        readonly Text[] healthText = new Text[2];
        readonly int[] visibleHp = new int[2];
        Catalog catalog;
        BattleEngine battle;
        ArenaView arena;
        ControllerHints controls;
        RectTransform canvasRoot, page, pausePanel;
        Text hints, logText, turnText;
        Font font;
        ScreenMode screen;
        int selectedSpecies = 250;
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
                && (species == 152 || species == 250)) selectedSpecies = species;
            int seedIndex = Array.IndexOf(args, "--seed");
            if (seedIndex >= 0 && seedIndex + 1 < args.Length && int.TryParse(args[seedIndex + 1], out int seed)) requestedSeed = seed;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var source = Resources.Load<TextAsset>("Data/catalog");
            if (source == null) throw new InvalidOperationException("Catalog missing.");
            catalog = JsonUtility.FromJson<Catalog>(source.text); catalog.Validate();
            arena = new GameObject("Original Aero arena").AddComponent<ArenaView>(); arena.Build();
            CreateCanvas(); CreateInputs(); ShowSelection();
            int secondsIndex = Array.IndexOf(args, "--seconds");
            if (secondsIndex >= 0 && secondsIndex + 1 < args.Length && int.TryParse(args[secondsIndex + 1], out int seconds))
                exitAt = Time.realtimeSinceStartup + Mathf.Clamp(seconds, 10, 300);
            if (smoke) StartCoroutine(SmokePlay());
        }

        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        void OnLog(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }

        void Update()
        {
            if (exitAt > 0 && Time.realtimeSinceStartup >= exitAt)
            {
                int expectedModels = screen == ScreenMode.Selection ? 1 : 2;
                bool passed = errors == 0 && arena.LoadedModels == expectedModels && (!smoke || (smokeEnded && expectedModels == 2));
                Debug.Log((smoke ? "[smoke-result]" : "[runtime-result]") + " errors=" + errors + " models=" + arena.LoadedModels
                    + " battleEnded=" + smokeEnded + " passed=" + passed);
                Application.Quit(passed ? 0 : 1); exitAt = 0;
            }
            if (!busy && controls != null && (controls.CancelPressed || controls.PausePressed) && screen != ScreenMode.Selection)
                TogglePause();
        }

        void CreateCanvas()
        {
            var canvasObject = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            canvasRoot = canvasObject.GetComponent<RectTransform>();
            Panel(canvasRoot, "Top bar", 0, 0, 1600, 80, ink);
            Label(canvasRoot, "AEROSTADIUM", 44, 18, 700, 45, 32, Color.white, true);
            Label(canvasRoot, "COMBAT SOLO", 1150, 18, 405, 28, 18, gold, true, TextAnchor.MiddleRight);
            Label(canvasRoot, "Projet Pokémon indépendant et non officiel", 1030, 47, 525, 22, 13, muted, false, TextAnchor.MiddleRight);
            Panel(canvasRoot, "Gold rule", 44, 77, 1510, 2, gold);
            Panel(canvasRoot, "Control strip", 0, 846, 1600, 54, ink);
            hints = Label(canvasRoot, "", 44, 858, 1240, 28, 18, Color.white);
            Label(canvasRoot, "PROTOTYPE 01", 1310, 858, 244, 28, 15, muted, true, TextAnchor.MiddleRight);
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
            string navigation = controls.Connected ? "Stick / D-pad" : "Flèches";
            string acceptColor = controls.Family == ControllerFamily.PlayStation ? "77BAFF" : "8FE0AB";
            hints.text = controls.DeviceName + "   ·   <color=#" + acceptColor + ">[ " + controls.Accept + " ]</color> Choisir"
                + "   ·   <color=#FFACA4>[ " + controls.Back + " ]</color> Retour   ·   " + navigation + " Naviguer";
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

        void ShowSelection()
        {
            ShowSelection(-1);
        }

        void ShowSelection(int focusIndex)
        {
            screen = ScreenMode.Selection; busy = false; ResetPage();
            arena.ClearPokemon(1); arena.ShowPokemon(0, selectedSpecies, true);
            var card = Panel(page, "Choose Pokemon", 48, 147, 452, 630, ink);
            Label(card, "TON POKÉMON", 28, 28, 395, 45, 28, Color.white, true);
            Label(card, "Combat d’entraînement · Niveau 50", 28, 76, 395, 25, 16, muted);
            int row = 0;
            foreach (int id in new[] { 152, 250 })
            {
                int choice = id; var species = catalog.GetSpecies(id);
                int choiceFocus = row;
                string subtitle = string.Join(" / ", Array.ConvertAll(species.types, TypeName));
                var button = Button(card, (id == selectedSpecies ? "● " : "") + species.name + "\n<size=17>" + subtitle + "</size>",
                                    28, 120 + row * 96, 395, 82, () => { selectedSpecies = choice; ShowSelection(choiceFocus); }, id == selectedSpecies ? blue : new Color(.1f, .16f, .25f));
                row++;
            }
            Label(card, "OBJET TENU", 28, 337, 395, 27, 17, gold, true);
            int itemIndex = 0;
            foreach (string id in new[] { "none", "leftovers", "lifeorb", "charcoal" })
            {
                string choice = id; var item = catalog.GetItem(id);
                int choiceFocus = itemIndex + 2;
                Button(card, item.name, 28 + (itemIndex % 2) * 202, 381 + (itemIndex / 2) * 58, 193, 47,
                       () => { selectedItem = choice; ShowSelection(choiceFocus); }, id == selectedItem ? blue : new Color(.1f, .16f, .25f));
                itemIndex++;
            }
            var start = Button(card, "ENTRER DANS L’ARÈNE", 28, 525, 395, 65, StartBattle, new Color(.83f, .59f, .21f));
            Label(page, catalog.GetSpecies(selectedSpecies).name.ToUpperInvariant(), 810, 712, 710, 55, 38, Color.white, true, TextAnchor.MiddleRight);
            Label(page, "Modèle Switch · Pokémon Écarlate / Violet", 810, 769, 710, 28, 17, muted, false, TextAnchor.MiddleRight);
            Select(focusIndex >= 0 && focusIndex < buttons.Count ? buttons[focusIndex] : start); RefreshHints();
            if (smoke) foreach (var button in buttons) button.interactable = false;
        }

        void StartBattle()
        {
            int seed = requestedSeed ?? (Environment.TickCount & int.MaxValue);
            battle = new BattleEngine(catalog, new[] { new TeamMember(selectedSpecies, selectedItem) }, new[] { new TeamMember(selectedSpecies, "none") }, seed);
            Debug.Log("[battle-start] species=" + selectedSpecies + " seed=" + seed);
            arena.ShowPokemon(0, selectedSpecies); arena.ShowPokemon(1, selectedSpecies);
            screen = ScreenMode.Battle; visibleHp[0] = battle.Active(0).Hp; visibleHp[1] = battle.Active(1).Hp;
            ShowBattle("À toi de jouer. Choisis une attaque.");
        }

        void ShowBattle(string message)
        {
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
            screen = ScreenMode.Result; ResetPage();
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
            switch (type) { case "Grass": return "Plante"; case "Fire": return "Feu"; case "Flying": return "Vol"; case "Fairy": return "Fée"; case "Ground": return "Sol"; case "Dragon": return "Dragon"; default: return type; }
        }
    }
}
