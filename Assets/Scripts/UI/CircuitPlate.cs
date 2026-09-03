using System;
using System.Collections.Generic;
using UnityEngine;
using GridSense.Environment;

namespace GridSense.UI
{
    /// <summary>
    /// Renders a circuit from its real centreline: the outline, its sector colouring, the corners
    /// the geometry actually contains, and the elevation profile along the lap.
    ///
    /// Nothing here is decorative. Every tick, colour boundary and height comes out of the same
    /// spline the car drives on, so the plate on the starting screen is the circuit, not a picture
    /// of one. The rasterised outline is cached per circuit and size, so the in-race minimap costs
    /// one texture draw per frame instead of several hundred rotated quads.
    /// </summary>
    public static class CircuitPlate
    {
        public sealed class Data
        {
            public CircuitType Circuit;
            public List<Vector3> Centre;        // world-space, y = elevation
            public Vector2[] Norm;              // centreline normalised into 0..1 with aspect preserved
            public float[] Elevation;           // y per sample
            public int[] CornerIndex;           // indices of detected corner apexes
            public int[] CornerRadius;          // metres, rounded
            public bool[] CornerRight;
            public float LengthM;
            public float MinY, MaxY;
            public float MinX, MaxX, MinZ, MaxZ, Span, PadX, PadZ;
            public float AspectW, AspectH;      // 0..1 footprint inside the unit square
            public int SectorEnd1, SectorEnd2;  // sample indices where sector 1 and 2 end
        }

        private static readonly Dictionary<CircuitType, Data> dataCache = new Dictionary<CircuitType, Data>();
        private static readonly Dictionary<long, Texture2D> outlineCache = new Dictionary<long, Texture2D>();

        // ------------------------------------------------------------------ data

        public static Data Get(CircuitType circuit)
        {
            Data d;
            if (dataCache.TryGetValue(circuit, out d)) return d;

            d = new Data { Circuit = circuit };
            d.Centre = TrackVisualBuilder.GenerateCenterline(circuit);

            int n = d.Centre.Count - 1;   // last sample repeats the first
            if (n < 8)
            {
                d.Norm = new Vector2[0];
                d.Elevation = new float[0];
                d.CornerIndex = new int[0];
                d.CornerRadius = new int[0];
                d.CornerRight = new bool[0];
                dataCache[circuit] = d;
                return d;
            }

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            d.MinY = float.MaxValue; d.MaxY = float.MinValue;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = d.Centre[i];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
                if (p.y < d.MinY) d.MinY = p.y;
                if (p.y > d.MaxY) d.MaxY = p.y;
            }

            float spanX = Mathf.Max(1f, maxX - minX);
            float spanZ = Mathf.Max(1f, maxZ - minZ);
            float span = Mathf.Max(spanX, spanZ);
            d.AspectW = spanX / span;
            d.AspectH = spanZ / span;
            float padX = (1f - d.AspectW) * 0.5f;
            float padZ = (1f - d.AspectH) * 0.5f;
            d.MinX = minX; d.MaxX = maxX; d.MinZ = minZ; d.MaxZ = maxZ;
            d.Span = span; d.PadX = padX; d.PadZ = padZ;

            d.Norm = new Vector2[n];
            d.Elevation = new float[n];
            float length = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = d.Centre[i];
                d.Norm[i] = new Vector2(padX + (p.x - minX) / span, padZ + (p.z - minZ) / span);
                d.Elevation[i] = p.y;
                length += Vector3.Distance(new Vector3(p.x, 0f, p.z),
                    new Vector3(d.Centre[(i + 1) % n].x, 0f, d.Centre[(i + 1) % n].z));
            }
            d.LengthM = length;

            DetectCorners(d, n);

            // sector boundaries at the conventional thirds of the lap
            d.SectorEnd1 = Mathf.RoundToInt(n * 0.3333f);
            d.SectorEnd2 = Mathf.RoundToInt(n * 0.6667f);

            dataCache[circuit] = d;
            return d;
        }

        private static void DetectCorners(Data d, int n)
        {
            // signed curvature in degrees per metre, from the same central differences the track
            // geometry uses; a corner is a contiguous run above threshold with enough total turn
            float[] curv = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 a = d.Centre[(i - 2 + n) % n], b = d.Centre[i], c = d.Centre[(i + 2) % n];
                Vector2 v1 = new Vector2(b.x - a.x, b.z - a.z);
                Vector2 v2 = new Vector2(c.x - b.x, c.z - b.z);
                if (v1.sqrMagnitude < 1e-6f || v2.sqrMagnitude < 1e-6f) continue;
                float h1 = Mathf.Atan2(v1.x, v1.y) * Mathf.Rad2Deg;
                float h2 = Mathf.Atan2(v2.x, v2.y) * Mathf.Rad2Deg;
                float span = Mathf.Max(1f, v1.magnitude + v2.magnitude);
                curv[i] = Mathf.DeltaAngle(h1, h2) / span;
            }

            List<int> idx = new List<int>();
            List<int> rad = new List<int>();
            List<bool> right = new List<bool>();
            bool[] used = new bool[n];
            const float threshold = 0.22f;   // radius under roughly 260 m

            for (int start = 0; start < n; start++)
            {
                if (used[start] || Mathf.Abs(curv[start]) < threshold) continue;
                float sign = Mathf.Sign(curv[start]);
                int i = start, len = 0, peak = start;
                float total = 0f, peakAbs = 0f;
                while (len < n)
                {
                    if (used[i] || Mathf.Sign(curv[i]) != sign || Mathf.Abs(curv[i]) < threshold * 0.35f) break;
                    used[i] = true;
                    total += curv[i];
                    if (Mathf.Abs(curv[i]) > peakAbs) { peakAbs = Mathf.Abs(curv[i]); peak = i; }
                    len++;
                    i = (i + 1) % n;
                }
                if (len >= 3 && Mathf.Abs(total) >= 14f && peakAbs > 0.0001f)
                {
                    idx.Add(peak);
                    rad.Add(Mathf.RoundToInt(1f / (peakAbs * Mathf.Deg2Rad)));
                    right.Add(sign > 0f);
                }
            }

            d.CornerIndex = idx.ToArray();
            d.CornerRadius = rad.ToArray();
            d.CornerRight = right.ToArray();
        }

        // ------------------------------------------------------------------ colour by position

        public static Color SectorInk(Data d, int sample)
        {
            if (sample < d.SectorEnd1) return Theme.Sector1;
            if (sample < d.SectorEnd2) return Theme.Sector2;
            return Theme.Sector3;
        }

        // ------------------------------------------------------------------ projection

        /// <summary>Maps a normalised centreline point into a screen rect, preserving aspect.</summary>
        public static Vector2 ToRect(Vector2 norm, Rect r)
        {
            return new Vector2(r.x + norm.x * r.width, r.y + r.height - norm.y * r.height);
        }

        /// <summary>
        /// Maps an arbitrary world XZ through the same projection the outline used, for the live
        /// car blip. Uses the bounds cached at build time rather than rescanning the centreline,
        /// because this runs every frame.
        /// </summary>
        public static Vector2 WorldToRect(Data d, Vector3 world, Rect r)
        {
            if (d.Norm.Length == 0 || d.Span <= 0f) return r.center;
            Vector2 norm = new Vector2(
                d.PadX + (world.x - d.MinX) / d.Span,
                d.PadZ + (world.z - d.MinZ) / d.Span);
            return ToRect(norm, r);
        }

        // ------------------------------------------------------------------ rasterised outline

        /// <summary>
        /// The sector-coloured outline baked into a texture. Cached: the in-race minimap redraws it
        /// as a single blit rather than walking the spline every frame.
        /// </summary>
        public static Texture2D Outline(CircuitType circuit, int size, float strokePx)
        {
            long key = ((long)circuit << 40) ^ ((long)size << 8) ^ (long)Mathf.RoundToInt(strokePx * 10f);
            Texture2D tex;
            if (outlineCache.TryGetValue(key, out tex) && tex != null) return tex;

            Data d = Get(circuit);
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color32[] buf = new Color32[size * size];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < buf.Length; i++) buf[i] = clear;

            if (d.Norm.Length > 1)
            {
                float inset = strokePx + 2f;
                float usable = size - inset * 2f;
                int n = d.Norm.Length;
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = d.Norm[i], b = d.Norm[(i + 1) % n];
                    Vector2 pa = new Vector2(inset + a.x * usable, inset + a.y * usable);
                    Vector2 pb = new Vector2(inset + b.x * usable, inset + b.y * usable);
                    Stroke(buf, size, pa, pb, strokePx, SectorInk(d, i));
                }
            }

            tex.SetPixels32(buf);
            tex.Apply(false, false);
            outlineCache[key] = tex;
            return tex;
        }

        private static void Stroke(Color32[] buf, int size, Vector2 a, Vector2 b, float radius, Color col)
        {
            float dist = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist));
            for (int s = 0; s <= steps; s++)
            {
                Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                Disc(buf, size, p, radius, col);
            }
        }

        private static void Disc(Color32[] buf, int size, Vector2 c, float radius, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - radius - 1f));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.x + radius + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - radius - 1f));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.y + radius + 1f));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                    float dd = Mathf.Sqrt(dx * dx + dy * dy);
                    // one pixel of feathering, so the outline is not a staircase
                    float cover = Mathf.Clamp01(radius - dd + 0.5f);
                    if (cover <= 0.002f) continue;

                    int idx = y * size + x;
                    Color32 prev = buf[idx];
                    float pa = prev.a / 255f;
                    float na = Mathf.Max(pa, cover);
                    if (na <= 0.002f) continue;
                    float lerp = cover / Mathf.Max(0.0001f, na);
                    buf[idx] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(prev.r, col.r * 255f, lerp)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(prev.g, col.g * 255f, lerp)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(prev.b, col.b * 255f, lerp)),
                        (byte)Mathf.RoundToInt(na * 255f));
                }
            }
        }

        // ------------------------------------------------------------------ live drawing

        /// <summary>
        /// Draws the outline directly, up to <paramref name="progress01"/> of the lap. Used for the
        /// starting screen's sweep: the full shape sits behind as a ghost so the circuit is legible
        /// immediately, and the bright stroke traces it from the start/finish line.
        /// </summary>
        public static void DrawSweep(Data d, Rect r, float progress01, float strokePx, float ghostAlpha)
        {
            int n = d.Norm.Length;
            if (n < 2) return;

            float t = Mathf.Max(1f, strokePx);
            int drawn = Mathf.Clamp(Mathf.RoundToInt(progress01 * n), 0, n);

            // ghost: every other sample is enough to read the shape and halves the cost
            if (ghostAlpha > 0.002f)
            {
                for (int i = 0; i < n; i += 2)
                {
                    Vector2 p = ToRect(d.Norm[i], r);
                    Color c = SectorInk(d, i);
                    c.a = ghostAlpha;
                    Theme.Fill(new Rect(p.x - t * 0.5f, p.y - t * 0.5f, t, t), c);
                }
            }

            for (int i = 0; i < drawn; i++)
            {
                Vector2 p = ToRect(d.Norm[i], r);
                Theme.Fill(new Rect(p.x - t * 0.5f, p.y - t * 0.5f, t, t), SectorInk(d, i));
            }
        }

        /// <summary>Corner ticks, drawn outward from the racing line as the sweep reaches them.</summary>
        public static void DrawCornerTicks(Data d, Rect r, float progress01, float lengthPx)
        {
            int n = d.Norm.Length;
            if (n < 4) return;

            for (int k = 0; k < d.CornerIndex.Length; k++)
            {
                int i = d.CornerIndex[k];
                if (i > progress01 * n) continue;

                Vector2 here = ToRect(d.Norm[i], r);
                Vector2 next = ToRect(d.Norm[(i + 2) % n], r);
                Vector2 fwd = (next - here).normalized;
                if (fwd.sqrMagnitude < 0.0001f) continue;
                Vector2 outward = new Vector2(fwd.y, -fwd.x) * (d.CornerRight[k] ? 1f : -1f);

                Vector2 tip = here + outward * lengthPx;
                int steps = Mathf.Max(2, Mathf.RoundToInt(lengthPx));
                float t = Mathf.Max(1f, Theme.U(1f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 p = Vector2.Lerp(here, tip, s / (float)steps);
                    Theme.Fill(new Rect(p.x - t * 0.5f, p.y - t * 0.5f, t, t), Theme.InkDim);
                }
            }
        }

        /// <summary>The elevation profile along the lap, as a filled area with its real range.</summary>
        public static void DrawElevation(Data d, Rect r)
        {
            int n = d.Elevation.Length;
            if (n < 2) return;

            float lo = d.MinY, hi = d.MaxY;
            float range = Mathf.Max(1f, hi - lo);
            float stepX = r.width / (n - 1);

            for (int i = 0; i < n; i++)
            {
                float f = (d.Elevation[i] - lo) / range;
                float h = Mathf.Max(1f, f * r.height);
                Color c = SectorInk(d, i);
                c.a = 0.35f;
                Theme.Fill(new Rect(r.x + i * stepX, r.y + r.height - h, Mathf.Max(1f, stepX + 1f), h), c);
            }

            // the crest line reads the shape; the fill alone reads as a block
            float prevY = 0f;
            for (int i = 0; i < n; i++)
            {
                float f = (d.Elevation[i] - lo) / range;
                float y = r.y + r.height - f * r.height;
                if (i > 0)
                {
                    float top = Mathf.Min(prevY, y);
                    float hh = Mathf.Abs(y - prevY);
                    Theme.Fill(new Rect(r.x + (i - 1) * stepX, top, Mathf.Max(1f, stepX + 1f), hh + Mathf.Max(1f, Theme.U(1f))),
                        SectorInk(d, i));
                }
                prevY = y;
            }
        }

        public static void Clear()
        {
            foreach (KeyValuePair<long, Texture2D> kv in outlineCache)
            {
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value);
            }
            outlineCache.Clear();
            dataCache.Clear();
        }
    }
}
