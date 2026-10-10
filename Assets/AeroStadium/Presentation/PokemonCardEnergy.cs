using UnityEngine;
using UnityEngine.UI;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// A portrait's living type energy. The middle stays transparent, including over opaque portraits.
    /// Only the focused card and a short selection/focus transition rebuild their mesh, at 30 Hz.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PokemonCardEnergy : MaskableGraphic
    {
        enum Motif { Ember, Leaf, Bubble, Lightning, Wisp, Star, Facet }
        Motif motif;
        Color accent = new Color(.18f, .59f, 1f);
        float seedPhase, focus, time, lastTick = -1, nextDraw, focusedAt = -100, burstAt = -100;
        bool focused, member, drawn;
        Vector2 centre;
        float sx, sy;
        public int LastVertexCount { get; private set; }

        public void Configure(string type, Color typeAccent, int seed)
        {
            accent = typeAccent;
            // A little added luminance makes darker types legible against the navy portrait backdrop.
            accent = Color.Lerp(accent, Color.white, .16f);
            seedPhase = Mathf.Repeat(seed * .61803399f, 1f);
            switch (type)
            {
                case "Fire": motif = Motif.Ember; break;
                case "Grass": case "Bug": motif = Motif.Leaf; break;
                case "Water": case "Ice": motif = Motif.Bubble; break;
                case "Electric": motif = Motif.Lightning; break;
                case "Ghost": case "Poison": case "Dark": motif = Motif.Wisp; break;
                case "Psychic": case "Fairy": case "Dragon": motif = Motif.Star; break;
                default: motif = Motif.Facet; break;
            }
            raycastTarget = false;
            SetVerticesDirty();
        }

        public void Celebrate()
        {
            burstAt = Time.unscaledTime;
            nextDraw = 0;
            SetVerticesDirty();
        }

        public void Tick(bool isFocused, bool isMember, float unscaledTime)
        {
            float delta = lastTick < 0 ? 0 : Mathf.Clamp(unscaledTime - lastTick, 0, .1f);
            lastTick = unscaledTime;
            bool changed = member != isMember || focused != isFocused;
            if (isFocused && !focused) focusedAt = unscaledTime;
            focused = isFocused;
            member = isMember;
            focus = Mathf.MoveTowards(focus, focused ? 1 : 0, delta / .18f);
            bool animated = focused || focus > .001f || unscaledTime - burstAt < .72f;
            if (changed || animated && unscaledTime >= nextDraw || drawn && !animated)
            {
                time = unscaledTime;
                nextDraw = unscaledTime + 1f / 30f;
                drawn = animated;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            centre = bounds.center + new Vector2(0, -3 * bounds.height / 64);
            sx = bounds.width / 136;
            sy = bounds.height / 64;
            float strength = focus * focus * (3 - 2 * focus);
            float burstAge = time - burstAt;
            bool burst = burstAge >= 0 && burstAge < .72f;
            if (strength <= .001f && !burst)
            {
                if (member) ReadyBrackets(mesh);
                LastVertexCount = mesh.currentVertCount;
                return;
            }

            float pulse = .88f + .12f * Mathf.Sin(time * 4.1f + seedPhase * 6.28f);
            // Side-lit glows: no full-card wash, and no alteration of the face/body in the middle.
            Glow(mesh, new Vector2(-49, 1), 18, 28, Tint(accent, .28f * strength * pulse), 18);
            Glow(mesh, new Vector2(49, 1), 18, 28, Tint(accent, .28f * strength * pulse), 18);

            float entry = Mathf.Clamp01((time - focusedAt) / .25f);
            float settle = 1 - Mathf.Pow(1 - entry, 3);
            float radius = 1.06f - .06f * settle;
            float spin = time * .46f + seedPhase * 6.28f;
            SegmentedRing(mesh, 56 * radius, 27 * radius, spin, 18, .72f, 3.8f,
                Tint(accent, .19f * strength));
            SegmentedRing(mesh, 56 * radius, 27 * radius, spin, 18, .72f, 1.15f,
                Tint(Color.Lerp(accent, Color.white, .52f), .87f * strength * pulse));
            SegmentedRing(mesh, 49, 22, -spin * .72f, 12, .45f, 1.3f,
                Tint(accent, .49f * strength));

            // Four luminous cardinals echo the capture capsule rather than a rotating spotlight strip.
            for (int i = 0; i < 4; i++)
            {
                float a = spin * .5f + i * Mathf.PI * .5f;
                Vector2 p = Orbit(a, 56, 25);
                Glow(mesh, p, 4.4f, 3.4f, Tint(accent, .34f * strength), 10);
                Diamond(mesh, p, 1.7f, Tint(Color.Lerp(accent, Color.white, .8f), .94f * strength));
            }

            for (int i = 0; i < 6; i++)
            {
                float life = Mathf.Repeat(time * TypeSpeed() + i * .233f + seedPhase, 1);
                float envelope = Mathf.Sin(life * Mathf.PI);
                float side = i % 2 == 0 ? -1 : 1;
                Vector2 p = new Vector2(side * (45 + Mathf.Sin(life * Mathf.PI * 2 + i) * 8), -21 + life * 45);
                if(motif==Motif.Wisp) p.y=-12+life*37;
                float opacity = strength * Mathf.Sqrt(Mathf.Max(0, envelope));
                DrawMotif(mesh, p, life, i, opacity);
            }

            if (burst) SelectionBurst(mesh, burstAge);
            if (member) ReadyBrackets(mesh);
            LastVertexCount = mesh.currentVertCount;
        }

        float TypeSpeed()
        {
            switch (motif)
            {
                case Motif.Lightning: return .83f;
                case Motif.Ember: return .58f;
                case Motif.Wisp: return .27f;
                case Motif.Bubble: return .39f;
                default: return .36f;
            }
        }

        void DrawMotif(VertexHelper mesh, Vector2 p, float life, int index, float opacity)
        {
            Color bright = Tint(Color.Lerp(accent, Color.white, .65f), opacity);
            switch (motif)
            {
                case Motif.Ember:
                {
                    float size = .65f + .45f * Mathf.Sin(life * Mathf.PI);
                    Glow(mesh, p, 5 * size, 8 * size, Tint(accent, opacity * .35f), 10);
                    Flame(mesh, p, size, Tint(accent, opacity * .85f));
                    Flame(mesh, p + new Vector2(0, -1.2f), size * .48f,
                        Tint(new Color(1, .91f, .34f), opacity));
                    break;
                }
                case Motif.Leaf:
                {
                    float angle = Mathf.Sin(time * 1.8f + index) * .65f + (index % 2 == 0 ? -.4f : .4f);
                    Vector2 axis = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    Vector2 edge = new Vector2(axis.y, -axis.x);
                    Vector2 tip = p + axis * 5.5f, root = p - axis * 5.5f;
                    Glow(mesh, p, 5, 6, Tint(accent, opacity * .23f), 10);
                    Quad(mesh, tip, p + edge * 2.8f, root, p - edge * 2.8f, Tint(accent, opacity));
                    Line(mesh, root, tip, .8f, Tint(Color.Lerp(accent, Color.white, .62f), opacity * .9f));
                    break;
                }
                case Motif.Bubble:
                {
                    float radius = 2.4f + life * 2.6f;
                    Glow(mesh, p, radius + 2, radius + 2, Tint(accent, opacity * .21f), 10);
                    Circle(mesh, p, radius, .85f, Tint(accent, opacity * .88f), 10);
                    Line(mesh, p + new Vector2(-radius * .48f, radius * .20f),
                        p + new Vector2(-radius * .12f, radius * .63f), 1.2f, bright);
                    break;
                }
                case Motif.Lightning:
                {
                    float flicker = .58f + .42f * Mathf.Abs(Mathf.Sin(time * 16 + index * 1.7f));
                    Vector2 a = p + new Vector2(-3, 7), b = p + new Vector2(2, 2),
                        c = p + new Vector2(-2, -1), d = p + new Vector2(3, -7);
                    Color glow = Tint(accent, opacity * flicker * .28f);
                    Color core = Tint(new Color(1, .96f, .61f), opacity * flicker);
                    Line(mesh, a, b, 4.5f, glow); Line(mesh, b, c, 4.5f, glow); Line(mesh, c, d, 4.5f, glow);
                    Line(mesh, a, b, 1.35f, core); Line(mesh, b, c, 1.35f, core); Line(mesh, c, d, 1.35f, core);
                    break;
                }
                case Motif.Wisp:
                {
                    // The trail curls and narrows, with no opaque particle or rectangular plume.
                    Vector2 previous = p;
                    for (int j = 1; j <= 5; j++)
                    {
                        float n = j / 5f;
                        Vector2 q = p + new Vector2(Mathf.Sin(time * 2.1f + index + n * 3.2f) * n * 6, -n * 15);
                        Line(mesh, previous, q, 4 * (1 - n) + .7f, Tint(accent, opacity * (1 - n) * .52f));
                        previous = q;
                    }
                    Glow(mesh, p, 4.5f, 6.5f, Tint(accent, opacity * .68f), 12);
                    Glow(mesh, p, 1.8f, 3.2f, Tint(Color.Lerp(accent, Color.white, .48f), opacity * .56f), 10);
                    break;
                }
                case Motif.Star:
                    Glow(mesh, p, 6, 6, Tint(accent, opacity * .32f), 10);
                    Star(mesh, p, 4.3f * (.7f + .3f * Mathf.Sin(life * Mathf.PI)), bright);
                    break;
                default:
                    Glow(mesh, p, 4.2f, 4.2f, Tint(accent, opacity * .24f), 10);
                    Diamond(mesh, p, 3.2f, Tint(accent, opacity * .82f));
                    Line(mesh, p + new Vector2(-1, 1.4f), p + new Vector2(1, 1.4f), .7f, bright);
                    break;
            }
        }

        void SelectionBurst(VertexHelper mesh, float age)
        {
            float t = Mathf.Clamp01(age / .72f), fade = (1 - t) * (1 - t);
            float spread = 1 - Mathf.Pow(1 - t, 3);
            Color gold = Color.Lerp(accent, new Color(1, .89f, .32f), .73f);
            SegmentedRing(mesh, 39 + spread * 23, 16 + spread * 12, seedPhase * 6.28f,
                24, .82f, 2.5f * (1 - t) + .6f, Tint(gold, fade));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f + .20f;
                Vector2 p = Orbit(a, 41 + spread * 20, 18 + spread * 9);
                Glow(mesh, p, 5 * fade + 1, 4 * fade + 1, Tint(gold, fade * .45f), 8);
                Star(mesh, p, 4.5f * fade + .3f, Tint(Color.Lerp(gold, Color.white, .55f), fade));
            }
        }

        void ReadyBrackets(VertexHelper mesh)
        {
            Color gold = new Color(1, .85f, .29f, .68f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int top = -1; top <= 1; top += 2)
                {
                    Vector2 p = new Vector2(side * 62, top * 25);
                    Line(mesh, p, p + new Vector2(-side * 7, 0), 1.2f, gold);
                    Line(mesh, p, p + new Vector2(0, -top * 5), 1.2f, gold);
                }
            }
        }

        void SegmentedRing(VertexHelper mesh, float xRadius, float yRadius, float rotation,
            int count, float filled, float width, Color tint)
        {
            for (int i = 0; i < count; i++)
            {
                float a = rotation + i * Mathf.PI * 2 / count;
                float b = a + filled * Mathf.PI * 2 / count;
                Line(mesh, Orbit(a, xRadius, yRadius), Orbit(b, xRadius, yRadius), width, tint);
            }
        }

        static Vector2 Orbit(float angle, float xRadius, float yRadius)
        {
            return new Vector2(Mathf.Cos(angle) * xRadius, Mathf.Sin(angle) * yRadius);
        }

        void Glow(VertexHelper mesh, Vector2 p, float rx, float ry, Color tint, int segments)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, p, tint);
            Color edge = tint; edge.a = 0;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                Vertex(mesh, p + Orbit(a, rx, ry), edge);
                if (i > 0) mesh.AddTriangle(first, first + i, first + i + 1);
            }
        }

        void Circle(VertexHelper mesh, Vector2 p, float radius, float width, Color tint, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments, b = (i + 1) * Mathf.PI * 2 / segments;
                Line(mesh, p + Orbit(a, radius, radius), p + Orbit(b, radius, radius), width, tint);
            }
        }

        void Flame(VertexHelper mesh, Vector2 p, float scale, Color tint)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, p, tint);
            Vertex(mesh, p + new Vector2(0, 8) * scale, tint);
            Vertex(mesh, p + new Vector2(2.8f, 2.4f) * scale, tint);
            Vertex(mesh, p + new Vector2(3.5f, -1.4f) * scale, tint);
            Vertex(mesh, p + new Vector2(0, -3.6f) * scale, tint);
            Vertex(mesh, p + new Vector2(-3.5f, -1.4f) * scale, tint);
            Vertex(mesh, p + new Vector2(-2.6f, 2.2f) * scale, tint);
            for (int i = 1; i < 6; i++) mesh.AddTriangle(first, first + i, first + i + 1);
            mesh.AddTriangle(first, first + 6, first + 1);
        }

        void Star(VertexHelper mesh, Vector2 p, float size, Color tint)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, p, tint);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f;
                float length = i % 2 == 0 ? size : size * .27f;
                Vertex(mesh, p + Orbit(a, length, length), tint);
            }
            for (int i = 1; i < 8; i++) mesh.AddTriangle(first, first + i, first + i + 1);
            mesh.AddTriangle(first, first + 8, first + 1);
        }

        void Diamond(VertexHelper mesh, Vector2 p, float size, Color tint)
        {
            Quad(mesh, p + new Vector2(0, size), p + new Vector2(size * .67f, 0),
                p + new Vector2(0, -size), p + new Vector2(-size * .67f, 0), tint);
        }

        void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 d = b - a;
            float inverseLength = 1 / Mathf.Max(.001f, d.magnitude);
            Vector2 side = new Vector2(-d.y, d.x) * (width * .5f * inverseLength);
            Quad(mesh, a - side, a + side, b + side, b - side, tint);
        }

        void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            int first = mesh.currentVertCount;
            Vertex(mesh, a, tint); Vertex(mesh, b, tint); Vertex(mesh, c, tint); Vertex(mesh, d, tint);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }

        void Vertex(VertexHelper mesh, Vector2 p, Color tint)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = centre + new Vector2(p.x * sx, p.y * sy);
            v.color = tint;
            v.uv0 = Vector2.zero;
            mesh.AddVert(v);
        }

        static Color Tint(Color color, float opacity)
        {
            color.a = Mathf.Clamp01(opacity);
            return color;
        }
    }
}
