using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>A layered mode selection screen, independent of the battle UI.</summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        static readonly string[] Titles = { "SOLO", "MULTIJOUEUR LOCAL", "MULTIJOUEUR EN LIGNE", "OPTIONS" };
        static readonly string[] Captions = {
            "Écris ta légende, un combat à la fois.",
            "Partage l’arène. Défie tes amis.",
            "La prochaine rencontre t’attend.",
            "Prépare ton jeu à ta façon."
        };
        static readonly Color Ink = new Color(.025f, .066f, .145f, 1f);
        static readonly Color[] Accents = {
            new Color(1f, .68f, .17f), new Color(.35f, .88f, .68f),
            new Color(.31f, .76f, 1f), new Color(.76f, .66f, 1f)
        };

        readonly List<Button> buttons = new List<Button>();
        readonly List<Card> cards = new List<Card>();
        readonly List<Texture2D> ownedTextures = new List<Texture2D>();
        RectTransform root;
        RectTransform cursor;
        CanvasGroup cursorOpacity;
        Text controlHints;
        Text welcome;
        Font font;
        Action<int> selectAction;
        Action backAction;
        int selectedIndex = -1;
        int hoveredIndex = -1;
        bool gamepadActive;
        float age;

        public IReadOnlyList<Button> Buttons => buttons;
        public int SelectedIndex => selectedIndex;
        public int SelectionChangeCount { get; private set; }
        public bool ArtworkReady { get; private set; }
        public bool CursorVisible => cursorOpacity != null && cursorOpacity.alpha > .5f;
        public event Action<int> SelectionChanged;

        sealed class Card
        {
            public RectTransform Rect;
            public MainMenuPanel Surface;
            public MainMenuPanel Halo;
            public MainMenuPanel IconBackground;
            public MainMenuIcon Icon;
            public MainMenuIcon Chevron;
            public Text Title;
            public Text Caption;
            public Text Number;
            public Color Accent;
            public Vector2 Position;
            public float Focus;
            public bool Pressed;
        }

        public void Build(RectTransform parent, Font uiFont, Action<int> onModeSelected, Action onBack)
        {
            if (root != null) Destroy(root.gameObject);
            ReleaseTextures();
            buttons.Clear(); cards.Clear();
            font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            selectAction = onModeSelected; backAction = onBack;
            selectedIndex = hoveredIndex = -1;
            SelectionChangeCount = 0;
            age = 0;
            root = Rect(parent, "Mode selection", 0, 0, 1600, 900);

            Texture2D background = Resources.Load<Texture2D>("UI/AeroStadiumMenuBackground");
            Texture2D logo = Resources.Load<Texture2D>("UI/AeroStadiumLogo");
            ArtworkReady = background != null && logo != null;
            if (background != null)
            {
                RawImage art = Image(root, "Pokémon arena illustration", 0, 0, 1600, 900, background, Color.white);
                art.uvRect = CoverUv(background.width, background.height, 1600f / 900f);
            }
            else Image(root, "Arena backdrop", 0, 0, 1600, 900, null, new Color(.12f, .30f, .53f));

            // The illustration stays exposed on the right; the scrim protects every label.
            Image(root, "Left readability gradient", 0, 0, 1050, 900, GradientTexture(true), Color.white);
            Image(root, "Lower vignette", 0, 756, 1600, 144, GradientTexture(false), new Color(.013f, .037f, .09f, .94f));
            if (logo != null)
            {
                RawImage brand = Image(root, "AeroStadium logo", 100, 24, 320, 149, logo, Color.white);
                brand.uvRect = new Rect(10f / 1774f, 31f / 887f, 1763f / 1774f, 820f / 887f);
            }
            welcome = Label(root, "BIENVENUE, DRESSEUR", 103, 169, 610, 24, 14, new Color(.54f, .82f, 1f), true);
            Label(root, "CHOISIS TON ARÈNE", 98, 195, 700, 54, 38, Color.white, true);
            ThinRule(root, "Header blue rule", 100, 246, 52, 3, new Color(.27f, .77f, 1f));
            ThinRule(root, "Header gold rule", 156, 246, 20, 3, new Color(1f, .73f, .23f));

            for (int i = 0; i < Titles.Length; i++) BuildCard(i);
            for (int i = 0; i < buttons.Count; i++)
            {
                Navigation navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count];
                navigation.selectOnDown = buttons[(i + 1) % buttons.Count];
                buttons[i].navigation = navigation;
            }

            cursor = Rect(root, "Controller Poké Ball cursor", 31, 278, 48, 48);
            cursorOpacity = cursor.gameObject.AddComponent<CanvasGroup>();
            cursorOpacity.blocksRaycasts = false;
            cursorOpacity.interactable = false;
            cursorOpacity.alpha = 0;
            MainMenuIcon ball = cursor.gameObject.AddComponent<MainMenuIcon>();
            ball.Kind = MainMenuIcon.Symbol.PokeBall;
            ball.raycastTarget = false;

            ThinRule(root, "Footer rule", 98, 792, 640, 1, new Color(.60f, .82f, 1f, .22f));
            controlHints = Label(root, "", 100, 807, 1440, 34, 18, new Color(.90f, .95f, 1f));
            Label(root, "L’ARÈNE T’ATTEND", 100, 848, 450, 22, 12, new Color(.57f, .70f, .85f), true);
            SetInputPresentation(false, "Clavier / souris", "Entrée", "Échap", "Flèches");
            SelectMode(0);
        }

        void BuildCard(int index)
        {
            float y = 258 + index * 120;
            RectTransform shadow = Rect(root, "Card shadow " + index, 100, y + 7, 640, 104);
            MainMenuPanel shadowShape = shadow.gameObject.AddComponent<MainMenuPanel>();
            shadowShape.color = new Color(0, .01f, .04f, .46f);
            shadowShape.raycastTarget = false;
            RectTransform halo = Rect(root, "Selection halo " + index, 95, y - 3, 646, 110);
            MainMenuPanel haloShape = halo.gameObject.AddComponent<MainMenuPanel>();
            haloShape.color = new Color(.38f, .74f, 1f, 0);
            haloShape.raycastTarget = false;

            RectTransform rt = Rect(root, "Mode " + index + " " + Titles[index], 98, y, 640, 104);
            // Scale about the centre without changing the parent coordinate system.
            rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = new Vector2(418, -y - 52);
            MainMenuPanel surface = rt.gameObject.AddComponent<MainMenuPanel>();
            surface.color = new Color(.038f, .103f, .21f, .94f);
            surface.BorderColor = new Color(.50f, .73f, 1f, .36f);
            surface.BorderWidth = 1.4f;
            Button button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.None;
            int modeIndex = index;
            button.onClick.AddListener(() => selectAction?.Invoke(modeIndex));
            MainMenuFeedback feedback = rt.gameObject.AddComponent<MainMenuFeedback>();
            feedback.Initialize(this, index);
            buttons.Add(button);

            RectTransform content = Rect(rt, "Content", 0, 0, 640, 104);
            content.anchorMin = content.anchorMax = new Vector2(0, 1);
            MainMenuPanel iconBackground = Rect(content, "Mode emblem", 22, 22, 60, 60).gameObject.AddComponent<MainMenuPanel>();
            iconBackground.Corner = 9;
            iconBackground.color = new Color(Accents[index].r, Accents[index].g, Accents[index].b, .14f);
            iconBackground.BorderColor = new Color(Accents[index].r, Accents[index].g, Accents[index].b, .52f);
            iconBackground.BorderWidth = 1;
            iconBackground.raycastTarget = false;
            MainMenuIcon icon = Rect(content, "Mode icon", 32, 32, 40, 40).gameObject.AddComponent<MainMenuIcon>();
            icon.Kind = (MainMenuIcon.Symbol)index;
            icon.color = Accents[index]; icon.raycastTarget = false;

            Text title = Label(content, Titles[index], 108, 17, 466, 40, index == 0 || index == 3 ? 30 : 25, Color.white, true);
            Text caption = Label(content, Captions[index], 109, 57, 465, 28, 17, new Color(.72f, .82f, .93f));
            Text number = Label(content, (index + 1).ToString("00"), 584, 15, 34, 24, 12, new Color(.54f, .68f, .84f), true, TextAnchor.MiddleCenter);
            MainMenuIcon chevron = Rect(content, "Enter chevron", 586, 44, 29, 29).gameObject.AddComponent<MainMenuIcon>();
            chevron.Kind = MainMenuIcon.Symbol.Chevron;
            chevron.color = Accents[index]; chevron.raycastTarget = false;
            cards.Add(new Card {
                Rect = rt, Surface = surface, Halo = haloShape, IconBackground = iconBackground,
                Icon = icon, Chevron = chevron, Title = title, Caption = caption, Number = number,
                Accent = Accents[index], Position = rt.anchoredPosition
            });
        }

        public void SetInputPresentation(bool useGamepad, string device, string confirm, string back, string navigation)
        {
            gamepadActive = useGamepad;
            if (controlHints != null)
                controlHints.text = device + "    ·    <color=#FFD077>[ " + confirm + " ]</color> Choisir"
                    + "    ·    <color=#BEDFFF>[ " + back + " ]</color> Retour    ·    " + navigation + " Naviguer";
        }

        public void SelectMode(int index)
        {
            if (buttons.Count == 0) return;
            index = Mathf.Clamp(index, 0, buttons.Count - 1);
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);
            RecordSelection(index);
        }

        public void ClearFocus()
        {
            if (EventSystem.current != null)
            {
                GameObject current = EventSystem.current.currentSelectedGameObject;
                for (int i = 0; i < buttons.Count; i++)
                    if (current == buttons[i].gameObject) { EventSystem.current.SetSelectedGameObject(null); break; }
            }
            hoveredIndex = -1;
            RecordSelection(-1);
        }

        public void RequestBack() => backAction?.Invoke();

        internal void Hover(int index, bool present)
        {
            if (present)
            {
                hoveredIndex = index;
                if (index >= 0 && index < buttons.Count && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);
                else RecordSelection(index);
            }
            else if (hoveredIndex == index) hoveredIndex = -1;
        }

        internal void Focus(int index, bool present)
        {
            if (present) RecordSelection(index);
            else if (selectedIndex == index) RecordSelection(-1);
        }

        internal void Press(int index, bool present)
        {
            if (index >= 0 && index < cards.Count) cards[index].Pressed = present;
        }

        void RecordSelection(int index)
        {
            if (selectedIndex == index) return;
            selectedIndex = index;
            if (index >= 0) SelectionChangeCount++;
            SelectionChanged?.Invoke(index);
        }

        void Update()
        {
            if (root == null) return;
            age += Time.unscaledDeltaTime;
            float smooth = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
            for (int i = 0; i < cards.Count; i++)
            {
                Card card = cards[i];
                bool active = gamepadActive ? selectedIndex == i
                    : hoveredIndex >= 0 ? hoveredIndex == i : selectedIndex == i;
                card.Focus = Mathf.Lerp(card.Focus, active ? 1 : 0, smooth);
                float focus = card.Focus;
                float scale = Mathf.Lerp(1f, card.Pressed ? .987f : 1.022f, focus);
                card.Rect.localScale = new Vector3(scale, scale, 1);
                card.Rect.anchoredPosition = card.Position + new Vector2(focus * 7f, 0);
                Color selected = new Color(.91f, .97f, 1f, .99f);
                card.Surface.color = Color.Lerp(new Color(.038f, .103f, .21f, .94f), selected, focus);
                card.Surface.SetBorder(Color.Lerp(new Color(.50f, .73f, 1f, .36f), card.Accent, focus));
                card.Halo.color = new Color(card.Accent.r, card.Accent.g, card.Accent.b, focus * (.15f + .035f * Mathf.Sin(age * 2.4f)));
                card.Title.color = Color.Lerp(Color.white, Ink, focus);
                card.Caption.color = Color.Lerp(new Color(.72f, .82f, .93f), new Color(.19f, .29f, .41f), focus);
                card.IconBackground.color = Color.Lerp(new Color(card.Accent.r, card.Accent.g, card.Accent.b, .14f), Ink, focus);
                card.IconBackground.SetBorder(Color.Lerp(new Color(card.Accent.r, card.Accent.g, card.Accent.b, .52f), card.Accent, focus));
                card.Icon.color = Color.Lerp(card.Accent, Color.white, focus);
                card.Chevron.color = Color.Lerp(card.Accent, Ink, focus);
                card.Number.color = Color.Lerp(new Color(.54f, .68f, .84f), new Color(.35f, .48f, .60f), focus);
            }
            if (cursor != null)
            {
                bool visible = gamepadActive && selectedIndex >= 0 && selectedIndex < cards.Count;
                cursorOpacity.alpha = Mathf.Lerp(cursorOpacity.alpha, visible ? 1f : 0f, smooth);
                if (visible)
                {
                    Vector2 target = new Vector2(31 + Mathf.Sin(age * 3f) * 2.5f, -(258 + selectedIndex * 120 + 28));
                    cursor.anchoredPosition = Vector2.Lerp(cursor.anchoredPosition, target, smooth);
                    cursor.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(age * 1.4f) * 7f);
                }
            }
        }

        void OnDisable()
        {
            if (cursorOpacity != null) cursorOpacity.alpha = 0;
            for (int i = 0; i < cards.Count; i++) cards[i].Pressed = false;
        }

        void OnDestroy() { ReleaseTextures(); }
        void ReleaseTextures()
        {
            foreach (Texture2D texture in ownedTextures) if (texture != null) Destroy(texture);
            ownedTextures.Clear();
        }

        Texture2D GradientTexture(bool horizontal)
        {
            const int length = 128;
            Texture2D texture = new Texture2D(horizontal ? length : 1, horizontal ? 1 : length, TextureFormat.RGBA32, false);
            texture.name = horizontal ? "Menu left scrim" : "Menu lower vignette";
            texture.wrapMode = TextureWrapMode.Clamp; texture.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)(length - 1);
                Color pixel = horizontal ? new Color(.008f, .023f, .065f, Mathf.Lerp(.96f, 0f,
                        Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, t))))
                    : new Color(1f, 1f, 1f, 1f - t);
                texture.SetPixel(horizontal ? i : 0, horizontal ? 0 : i, pixel);
            }
            texture.Apply(false, true); ownedTextures.Add(texture); return texture;
        }

        static Rect CoverUv(int width, int height, float targetAspect)
        {
            float aspect = width / (float)height;
            if (aspect > targetAspect)
            {
                float w = targetAspect / aspect; return new Rect((1 - w) * .5f, 0, w, 1);
            }
            float h = aspect / targetAspect; return new Rect(0, (1 - h) * .5f, 1, h);
        }

        static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            RectTransform rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        static RawImage Image(Transform parent, string name, float x, float y, float w, float h, Texture texture, Color color)
        {
            RawImage image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<RawImage>();
            image.texture = texture; image.color = color; image.raycastTarget = false; return image;
        }

        static void ThinRule(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            Image image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
        }

        Text Label(Transform parent, string text, float x, float y, float width, float height, int size, Color color,
            bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            Text label = Rect(parent, text, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.color = color; label.alignment = alignment; label.supportRichText = true;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false; return label;
        }
    }

    sealed class MainMenuFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        MainMenuView view;
        int index;
        public void Initialize(MainMenuView owner, int modeIndex) { view = owner; index = modeIndex; }
        public void OnPointerEnter(PointerEventData data) { if (view != null) view.Hover(index, true); }
        public void OnPointerExit(PointerEventData data) { if (view != null) { view.Hover(index, false); view.Press(index, false); } }
        public void OnSelect(BaseEventData data) { if (view != null) view.Focus(index, true); }
        public void OnDeselect(BaseEventData data) { if (view != null) view.Focus(index, false); }
        public void OnPointerDown(PointerEventData data) { if (view != null && data.button == PointerEventData.InputButton.Left) view.Press(index, true); }
        public void OnPointerUp(PointerEventData data) { if (view != null) view.Press(index, false); }
    }

    /// <summary>Bevelled vector card with an inset coloured rim, rendered at native canvas resolution.</summary>
    sealed class MainMenuPanel : MaskableGraphic
    {
        public Color BorderColor = Color.clear;
        public float BorderWidth;
        public float Corner = 13f;
        public void SetBorder(Color next)
        {
            if (BorderColor == next) return;
            BorderColor = next; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float cut = Mathf.Min(Corner, Mathf.Min(r.width, r.height) * .25f);
            Vector2[] points = {
                new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin),
                new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax, r.yMax - cut),
                new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax),
                new Vector2(r.xMin, r.yMax - cut), new Vector2(r.xMin, r.yMin + cut)
            };
            Vector2 centre = r.center;
            AddVertex(vh, centre, color);
            for (int i = 0; i < points.Length; i++)
            {
                Color shade = color;
                float lighting = Mathf.Lerp(.92f, 1.035f, Mathf.InverseLerp(r.yMin, r.yMax, points[i].y));
                shade.r *= lighting; shade.g *= lighting; shade.b *= lighting;
                AddVertex(vh, points[i], shade);
            }
            for (int i = 0; i < points.Length; i++) vh.AddTriangle(0, i + 1, (i + 1) % points.Length + 1);
            if (BorderWidth <= 0 || BorderColor.a <= 0) return;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Length];
                Vector2 inward = new Vector2(-(b - a).y, (b - a).x).normalized * BorderWidth;
                int start = vh.currentVertCount;
                AddVertex(vh, a, BorderColor); AddVertex(vh, b, BorderColor);
                AddVertex(vh, b + inward, BorderColor); AddVertex(vh, a + inward, BorderColor);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            }
        }

        internal static void AddVertex(VertexHelper vh, Vector2 p, Color tint)
        {
            UIVertex vertex = UIVertex.simpleVert; vertex.position = p; vertex.color = tint; vh.AddVert(vertex);
        }
    }

    sealed class MainMenuIcon : MaskableGraphic
    {
        public enum Symbol { Trophy, Local, Online, Gear, Chevron, PokeBall }
        public Symbol Kind;
        Vector2 Origin => rectTransform.rect.position;
        Vector2 Size => rectTransform.rect.size;
        Vector2 Point(float x, float y) => Origin + Vector2.Scale(Size, new Vector2(x, y));

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch (Kind)
            {
                case Symbol.Trophy:
                    Quad(vh, .27f, .45f, .73f, .45f, .65f, .80f, .35f, .80f, color);
                    Ring(vh, .25f, .64f, .14f, .038f, color, false, 0, Mathf.PI * 1.55f);
                    Ring(vh, .75f, .64f, .14f, .038f, color, false, -.55f * Mathf.PI, Mathf.PI);
                    Line(vh, .5f, .46f, .5f, .22f, .08f, color);
                    Line(vh, .31f, .19f, .69f, .19f, .08f, color);
                    break;
                case Symbol.Local:
                    Ball(vh, .32f, .60f, .24f, color, false);
                    Ball(vh, .69f, .37f, .24f, color, false);
                    break;
                case Symbol.Online:
                    Ring(vh, .5f, .5f, .37f, .033f, color);
                    Ellipse(vh, .5f, .5f, .19f, .37f, .033f, color);
                    Line(vh, .14f, .5f, .86f, .5f, .032f, color);
                    Line(vh, .21f, .69f, .79f, .69f, .027f, color);
                    Line(vh, .21f, .31f, .79f, .31f, .027f, color);
                    break;
                case Symbol.Gear:
                    Ring(vh, .5f, .5f, .29f, .11f, color);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Vector2 side = new Vector2(-direction.y, direction.x) * .063f;
                        Vector2 a = new Vector2(.5f, .5f) + direction * .25f;
                        Vector2 b = new Vector2(.5f, .5f) + direction * .39f;
                        Polygon(vh, new[] { a - side, b - side, b + side, a + side }, color);
                    }
                    break;
                case Symbol.Chevron:
                    Line(vh, .31f, .23f, .62f, .5f, .075f, color);
                    Line(vh, .62f, .5f, .31f, .77f, .075f, color);
                    break;
                case Symbol.PokeBall:
                    Ball(vh, .5f, .5f, .43f, color, true);
                    break;
            }
        }

        void Ball(VertexHelper vh, float x, float y, float radius, Color tint, bool filled)
        {
            Color dark = new Color(.016f, .051f, .13f);
            if (filled)
            {
                Disk(vh, x, y, radius, new Color(.99f, .26f, .25f), 0, Mathf.PI);
                Disk(vh, x, y, radius, Color.white, Mathf.PI, Mathf.PI * 2);
                Ring(vh, x, y, radius, .037f, dark);
                Line(vh, x - radius, y, x + radius, y, .075f, dark);
                Disk(vh, x, y, .12f, dark);
                Disk(vh, x, y, .072f, Color.white);
                Disk(vh, x - .10f, y + .23f, .038f, new Color(1, 1, 1, .84f));
            }
            else
            {
                Ring(vh, x, y, radius, .032f, tint);
                Line(vh, x - radius, y, x + radius, y, .032f, tint);
                Disk(vh, x, y, .074f, tint);
            }
        }

        void Ring(VertexHelper vh, float x, float y, float radius, float width, Color tint, bool unused = false,
            float start = 0, float end = Mathf.PI * 2)
        {
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)segments), b = Mathf.Lerp(start, end, (i + 1f) / segments);
                Polygon(vh, new[] {
                    new Vector2(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius),
                    new Vector2(x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius),
                    new Vector2(x + Mathf.Cos(b) * (radius - width), y + Mathf.Sin(b) * (radius - width)),
                    new Vector2(x + Mathf.Cos(a) * (radius - width), y + Mathf.Sin(a) * (radius - width))
                }, tint);
            }
        }

        void Ellipse(VertexHelper vh, float x, float y, float rx, float ry, float width, Color tint)
        {
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Line(vh, x + Mathf.Cos(a) * rx, y + Mathf.Sin(a) * ry, x + Mathf.Cos(b) * rx, y + Mathf.Sin(b) * ry, width, tint);
            }
        }

        void Disk(VertexHelper vh, float x, float y, float radius, Color tint, float start = 0, float end = Mathf.PI * 2)
        {
            const int segments = 48;
            int centre = vh.currentVertCount;
            MainMenuPanel.AddVertex(vh, Point(x, y), tint);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)segments);
                MainMenuPanel.AddVertex(vh, Point(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius), tint);
                if (i > 0) vh.AddTriangle(centre, centre + i, centre + i + 1);
            }
        }

        void Line(VertexHelper vh, float ax, float ay, float bx, float by, float width, Color tint)
        {
            Vector2 a = new Vector2(ax, ay), b = new Vector2(bx, by);
            Vector2 offset = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Polygon(vh, new[] { a - offset, b - offset, b + offset, a + offset }, tint);
        }

        void Quad(VertexHelper vh, float ax, float ay, float bx, float by, float cx, float cy, float dx, float dy, Color tint)
            => Polygon(vh, new[] { new Vector2(ax, ay), new Vector2(bx, by), new Vector2(cx, cy), new Vector2(dx, dy) }, tint);

        void Polygon(VertexHelper vh, Vector2[] polygon, Color tint)
        {
            int start = vh.currentVertCount;
            foreach (Vector2 point in polygon) MainMenuPanel.AddVertex(vh, Point(point.x, point.y), tint);
            for (int i = 1; i < polygon.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
