using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace GridSense.UI
{
    /// <summary>
    /// The GridSense engineering-console design system: an enumerated tone ramp, hues reserved
    /// entirely for meaning, a resolution-independent type scale, and the drawing primitives every
    /// surface is built from.
    ///
    /// Two rules the whole interface depends on:
    ///   1. Command versus actual. Anything the model recommends is drawn in Command magenta; what
    ///      the driver actually did is drawn in plain Ink. Never the reverse, never a third colour.
    ///   2. Enclosure discipline. A rule is drawn only where two coordinate systems meet. Panels do
    ///      not get borders because they are panels.
    ///
    /// Everything scales from screen height, so the interface holds at 1280x720 and at 4K rather
    /// than shrinking into the corner of a large display.
    /// </summary>
    public static class Theme
    {
        // ---------------------------------------------------------------- tone ramp (enumerated)

        /// <summary>The only six neutral values in the interface. No arbitrary greys exist.</summary>
        public static readonly Color Ground = Hex(0x07090B);   // the void behind everything
        public static readonly Color Panel = Hex(0x0E1217);    // a readable plate over the 3D scene
        public static readonly Color Rule = Hex(0x212A34);     // hairlines and axis lines
        public static readonly Color InkDim = Hex(0x7C8A98);   // labels, units, inactive (5.3:1 on Panel)
        public static readonly Color Ink = Hex(0xAEBAC5);      // body values
        public static readonly Color InkHi = Hex(0xEAF0F5);    // the actual reading, headlines

        // ---------------------------------------------------------------- hues (meaning only)

        /// <summary>What the model commands. Borrowed from the aviation command bug.</summary>
        public static readonly Color Command = Hex(0xFF2D9B);
        public static readonly Color Energy = Hex(0x35C8FF);   // ERS / electrical channel
        public static readonly Color Ok = Hex(0x2FD37A);
        public static readonly Color Caution = Hex(0xF0A62E);
        public static readonly Color Warning = Hex(0xFF4A4A);

        // F1 timing conventions the audience already knows by heart
        public static readonly Color SectorBest = Hex(0xB44BFF);      // session best
        public static readonly Color SectorPersonal = Hex(0x2FD37A);  // personal best
        public static readonly Color SectorSlower = Hex(0xF0C93A);

        // Track position along the lap. Deliberately distinct from Energy (ERS) and SectorBest
        // (session best), which previously collided with sector 1 and sector 3 exactly.
        public static readonly Color Sector1 = Hex(0x4FD6C0);
        public static readonly Color Sector2 = Hex(0xF0C93A);
        public static readonly Color Sector3 = Hex(0xE86AB4);

        public static readonly Color CompoundSoft = Hex(0xFF4A4A);
        public static readonly Color CompoundMedium = Hex(0xF0C93A);
        public static readonly Color CompoundHard = Hex(0xEAF0F5);

        public static Color Compound(Core.TyreCompound c)
        {
            if (c == Core.TyreCompound.Soft) return CompoundSoft;
            if (c == Core.TyreCompound.Medium) return CompoundMedium;
            return CompoundHard;
        }

        public static string CompoundName(Core.TyreCompound c)
        {
            if (c == Core.TyreCompound.Soft) return "SOFT";
            if (c == Core.TyreCompound.Medium) return "MEDIUM";
            return "HARD";
        }

        public static string CompoundCode(Core.TyreCompound c)
        {
            if (c == Core.TyreCompound.Soft) return "C3";
            if (c == Core.TyreCompound.Medium) return "C2";
            return "C1";
        }

        /// <summary>
        /// Relative peak grip against the softest compound, read from the physics tyre model rather
        /// than written into the UI. The previous build displayed 100/92/84, which matched nothing.
        /// </summary>
        public static int RelativeGripPct(Core.TyreCompound c)
        {
            float mine = GridSense.Physics.TyreCompoundProfile.GetDefault(c).BasePeakFriction;
            float soft = GridSense.Physics.TyreCompoundProfile.GetDefault(Core.TyreCompound.Soft).BasePeakFriction;
            if (soft <= 0.0001f) return 0;
            return Mathf.RoundToInt(mine / soft * 100f);
        }

        /// <summary>
        /// Wear rate multiplier as the simulation applies it. Deliberately NOT
        /// TyreCompoundProfile.BaseWearRateMultiplier, which holds different values and which
        /// nothing in the physics reads - showing that would describe a stint nobody will drive.
        /// </summary>
        public static float WearMultiplier(Core.TyreCompound c)
        {
            return GridSense.Physics.CarPhysicsController.WearMultiplierFor(c);
        }

        /// <summary>Wear reads green until it starts costing lap time, then amber, then red.</summary>
        public static Color WearInk(float wearPct)
        {
            if (wearPct >= 45f) return Warning;
            if (wearPct >= 22f) return Caution;
            return Ok;
        }

        // ---------------------------------------------------------------- scale

        private static float scale = 1f;
        private static int builtForScale = -1;

        /// <summary>Pixels per design unit, driven by screen height. 1.0 at 1080p.</summary>
        public static float S { get { return scale; } }

        public static float U(float designPixels) { return designPixels * scale; }

        // ---------------------------------------------------------------- type

        public static GUIStyle Micro;      //  9u  uppercase, dim: units and axis ticks
        public static GUIStyle Label;      // 11u  uppercase: field names
        public static GUIStyle Body;       // 13u  sentence text
        public static GUIStyle Value;      // 18u  bold: a single reading
        public static GUIStyle Metric;     // 30u  bold: the reading that matters in this zone
        public static GUIStyle Hero;       // 46u  bold: lap delta, speed
        public static GUIStyle Display;    // 64u  bold: the product name, once

        private static Texture2D px;

        /// <summary>Call once at the top of OnGUI. Rebuilds styles only when the scale changes.</summary>
        public static void Begin()
        {
            if (px == null)
            {
                px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                px.SetPixel(0, 0, Color.white);
                px.filterMode = FilterMode.Point;
                px.wrapMode = TextureWrapMode.Clamp;
                px.Apply();
            }

            // Floor chosen so the smallest face (9u) never falls below 8px: below that the text
            // would stop scaling while its container carried on, and clip.
            float target = Mathf.Clamp(Screen.height / 1080f, 0.89f, 2.60f);
            scale = target;

            int key = Mathf.RoundToInt(target * 100f);
            if (builtForScale == key && Micro != null) return;
            builtForScale = key;
            variants.Clear();   // base styles are about to be replaced, so their variants are stale
            wrapped.Clear();

            Micro = Style(9, FontStyle.Normal, TextAnchor.MiddleLeft, InkDim);
            Label = Style(11, FontStyle.Bold, TextAnchor.MiddleLeft, InkDim);
            Body = Style(13, FontStyle.Normal, TextAnchor.MiddleLeft, Ink);
            Value = Style(18, FontStyle.Bold, TextAnchor.MiddleLeft, InkHi);
            Metric = Style(30, FontStyle.Bold, TextAnchor.MiddleLeft, InkHi);
            Hero = Style(46, FontStyle.Bold, TextAnchor.MiddleLeft, InkHi);
            Display = Style(64, FontStyle.Bold, TextAnchor.MiddleLeft, InkHi);
        }

        private static GUIStyle Style(int designSize, FontStyle weight, TextAnchor anchor, Color ink)
        {
            GUIStyle s = new GUIStyle();
            s.font = GUI.skin != null ? GUI.skin.font : null;
            s.fontSize = Mathf.Max(8, Mathf.RoundToInt(designSize * scale));   // 9u * 0.89 = 8px
            s.fontStyle = weight;
            s.alignment = anchor;
            s.wordWrap = false;
            s.clipping = TextClipping.Clip;
            s.richText = false;
            s.normal.textColor = ink;
            s.hover.textColor = ink;
            s.active.textColor = ink;
            s.focused.textColor = ink;
            s.padding = new RectOffset(0, 0, 0, 0);
            s.margin = new RectOffset(0, 0, 0, 0);
            return s;
        }

        // Variants are cached rather than allocated. OnGUI runs every frame, so a `new GUIStyle`
        // per readout is tens of allocations per frame, which on integrated graphics is a real
        // line in the budget rather than a rounding error.
        private static readonly Dictionary<long, GUIStyle> variants = new Dictionary<long, GUIStyle>();

        /// <summary>The same style in a different ink. Allocated once, then reused.</summary>
        public static GUIStyle Tint(GUIStyle from, Color ink)
        {
            return Variant(from, ink, from.alignment, true);
        }

        /// <summary>The same style at a different alignment. Allocated once, then reused.</summary>
        public static GUIStyle Align(GUIStyle from, TextAnchor anchor)
        {
            return Variant(from, Color.clear, anchor, false);
        }

        /// <summary>Both at once, still one cached instance.</summary>
        public static GUIStyle Styled(GUIStyle from, Color ink, TextAnchor anchor)
        {
            return Variant(from, ink, anchor, true);
        }

        private static GUIStyle Variant(GUIStyle from, Color ink, TextAnchor anchor, bool recolour)
        {
            if (from == null) return null;

            long baseId = RuntimeHelpers.GetHashCode(from) & 0xFFFFFFL;
            long colourId = recolour
                ? (((long)(byte)(ink.r * 255f) << 16) | ((long)(byte)(ink.g * 255f) << 8) | (byte)(ink.b * 255f))
                : 0xFFFFFFL;
            long key = (baseId << 36) | (colourId << 4) | ((long)anchor & 0xF);

            GUIStyle s;
            if (variants.TryGetValue(key, out s) && s != null) return s;

            s = new GUIStyle(from) { alignment = anchor };
            if (recolour)
            {
                s.normal.textColor = ink;
                s.hover.textColor = ink;
                s.active.textColor = ink;
                s.focused.textColor = ink;
            }
            variants[key] = s;
            return s;
        }

        // ---------------------------------------------------------------- primitives

        public static void Fill(Rect r, Color c)
        {
            if (c.a <= 0.001f) return;
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, px);
            GUI.color = prev;
        }

        /// <summary>Horizontal hairline. Always exactly one device pixel at any scale.</summary>
        public static void HRule(float x, float y, float w, Color c)
        {
            Fill(new Rect(x, y, w, Mathf.Max(1f, Mathf.Round(scale))), c);
        }

        public static void VRule(float x, float y, float h, Color c)
        {
            Fill(new Rect(x, y, Mathf.Max(1f, Mathf.Round(scale)), h), c);
        }

        /// <summary>
        /// A readable plate over the 3D scene. No border: enclosure is earned by rules where
        /// coordinate systems meet, not granted because something is a panel.
        /// </summary>
        public static void Plate(Rect r, float alpha = 0.94f)
        {
            Color c = Panel;
            c.a = alpha;
            Fill(r, c);
        }

        public static void Text(Rect r, string s, GUIStyle style)
        {
            GUI.Label(r, s, style);
        }

        private static readonly Dictionary<long, GUIStyle> wrapped = new Dictionary<long, GUIStyle>();

        /// <summary>
        /// Text that wraps instead of clipping. Every base style sets wordWrap false so numeric
        /// readouts hold one line; prose that arrives from a model at unknown length (the Track 1
        /// rationale) has to be allowed to run on rather than truncate mid-word.
        /// </summary>
        public static void Wrapped(Rect r, string s, GUIStyle style)
        {
            if (style == null) return;
            long key = RuntimeHelpers.GetHashCode(style);
            GUIStyle w;
            if (!wrapped.TryGetValue(key, out w) || w == null)
            {
                w = new GUIStyle(style) { wordWrap = true, clipping = TextClipping.Clip };
                wrapped[key] = w;
            }
            GUI.Label(r, s, w);
        }

        public static void Text(Rect r, string s, GUIStyle style, Color ink)
        {
            GUI.Label(r, s, Tint(style, ink));
        }

        // ---------------------------------------------------------------- bars

        /// <summary>
        /// A track with a filled portion and, optionally, a command tick showing where the model
        /// wants the value to be. The tick is the whole point: a bar without it is just a bar.
        /// </summary>
        public static void Bar(Rect r, float value01, Color fill, float? command01 = null)
        {
            Fill(r, Rule);
            float w = Mathf.Clamp01(value01) * r.width;
            if (w > 0.5f) Fill(new Rect(r.x, r.y, w, r.height), fill);

            if (command01.HasValue)
            {
                float cx = r.x + Mathf.Clamp01(command01.Value) * r.width;
                float tw = Mathf.Max(2f, U(2f));
                Fill(new Rect(cx - tw * 0.5f, r.y - U(2f), tw, r.height + U(4f)), Command);
            }
        }

        // ---------------------------------------------------------------- glyphs (drawn, never emoji)

        /// <summary>A chevron stroke. Direction: 0 points right, 1 points left.</summary>
        public static void Chevron(Rect r, int direction, Color c)
        {
            float t = Mathf.Max(1f, U(1.6f));
            int steps = Mathf.Max(3, Mathf.RoundToInt(r.height * 0.5f));
            for (int i = 0; i < steps; i++)
            {
                float f = i / (float)(steps - 1);
                if (direction == 0)
                {
                    Fill(new Rect(r.x + f * r.width * 0.5f, r.y + f * r.height * 0.5f, t, t), c);
                    Fill(new Rect(r.x + f * r.width * 0.5f, r.y + r.height - f * r.height * 0.5f - t, t, t), c);
                }
                else
                {
                    Fill(new Rect(r.x + r.width - f * r.width * 0.5f - t, r.y + f * r.height * 0.5f, t, t), c);
                    Fill(new Rect(r.x + r.width - f * r.width * 0.5f - t, r.y + r.height - f * r.height * 0.5f - t, t, t), c);
                }
            }
        }

        /// <summary>A tick. Replaces the check-mark emoji the previous build used.</summary>
        public static void Tick(Rect r, Color c)
        {
            float t = Mathf.Max(1f, U(1.8f));
            int steps = Mathf.Max(3, Mathf.RoundToInt(r.width * 0.35f));
            for (int i = 0; i < steps; i++)
            {
                float f = i / (float)(steps - 1);
                Fill(new Rect(r.x + f * r.width * 0.38f, r.y + r.height * 0.45f + f * r.height * 0.5f, t, t), c);
            }
            steps = Mathf.Max(3, Mathf.RoundToInt(r.width * 0.7f));
            for (int i = 0; i < steps; i++)
            {
                float f = i / (float)(steps - 1);
                Fill(new Rect(r.x + r.width * 0.38f + f * r.width * 0.62f,
                              r.y + r.height * 0.95f - f * r.height * 0.9f, t, t), c);
            }
        }

        // ---------------------------------------------------------------- damping

        /// <summary>
        /// A readout that eases toward its target instead of flickering between frames. Physics
        /// values change every tick; a number a human is reading should not.
        /// </summary>
        [Serializable]
        public struct Damped
        {
            private float value;
            private bool started;

            public float Value { get { return value; } }

            public float Track(float target, float responsePerSecond = 9f)
            {
                if (!started) { value = target; started = true; return value; }
                if (float.IsNaN(target) || float.IsInfinity(target)) return value;
                value = Mathf.Lerp(value, target, 1f - Mathf.Exp(-responsePerSecond * Time.unscaledDeltaTime));
                return value;
            }

            public void Snap(float v) { value = v; started = true; }
        }

        // ---------------------------------------------------------------- formatting

        public static string LapTime(float seconds)
        {
            if (seconds <= 0f) return "--:--.---";
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds - m * 60f;
            return string.Format("{0}:{1:00.000}", m, s);
        }

        public static string Split(float seconds)
        {
            if (seconds <= 0f) return "--.---";
            return seconds.ToString("00.000");
        }

        /// <summary>A signed delta, always with its sign, so a gain never reads as a loss.</summary>
        public static string Delta(float seconds, int decimals = 3)
        {
            if (Mathf.Abs(seconds) < 0.0005f) return "0." + new string('0', decimals);
            string body = Mathf.Abs(seconds).ToString("F" + decimals);
            return (seconds > 0f ? "+" : "-") + body;
        }

        public static Color DeltaInk(float seconds)
        {
            if (seconds < -0.0005f) return SectorPersonal;
            if (seconds > 0.0005f) return SectorSlower;
            return Ink;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
