using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using GridSense.Track;
using GridSense.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridSense.Environment
{
    public enum CircuitType
    {
        Bahrain = 0, // Bahrain International Circuit (Sakhir - 5.412 km, 15 Turns, 15m elevation)
        Spa = 1,     // Circuit de Spa-Francorchamps (Ardennes - 7.004 km, 19 Turns, 100m elevation)
        Monaco = 2,  // Circuit de Monaco (Monte Carlo - 3.337 km, 19 Turns, 42m elevation)
        Monza = 3    // Autodromo Nazionale Monza (Temple of Speed - 5.793 km, 11 Turns, 8m elevation)
    }

    /// <summary>
    /// Builds the four Grand Prix circuits as continuous, watertight geometry.
    ///
    /// Centreline pipeline (identical for all four circuits):
    ///   raw layout source (intrinsic segment chain, or a closed anchor polygon)
    ///     -> exact loop closure by weighted minimum-norm curvature correction
    ///     -> uniform arc-length resample (no degenerate or oversized segments)
    ///     -> periodic smoothing
    ///     -> scale calibration to the official lap length
    ///     -> periodic elevation profile
    /// The closure step is what removes the torn ribbon at the start/finish line: the loop is closed
    /// geometrically to floating-point tolerance instead of snapping the final vertex to the origin.
    ///
    /// Surface, kerbs, barriers, pit lane, grandstands and the landscape are all continuous meshes
    /// generated from a shared lateral-profile extruder, so there are no gaps between segments and
    /// no seam at the lap join (frames use wrapped central differences).
    /// </summary>
    public static class TrackVisualBuilder
    {
        /// <summary>
        /// Centreline index of the start/finish line. Index 0 for every circuit: each layout is
        /// authored so the line sits on the main straight with real straight on both sides of it.
        /// </summary>
        public const int StartFinishIndex = 0;

        private const float StationSpacing = 6.0f;   // metres between centreline samples

        // ------------------------------------------------------------------ circuit specification

        private struct CircuitSpec
        {
            public string DisplayName;
            public float LengthM;
            public float HalfWidth;      // half of the racing surface width
            public float KerbPad;        // paved strip outside the white line
            public float RunoffWidth;    // gravel / sand / asphalt run-off
            public float VergeWidth;     // graded verge falling away to the landscape
            public float VergeDrop;
            public float BarrierGap;     // gap between run-off edge and the barrier line
            public int PitSide;          // +1 = pit lane on the right of the S/F straight
            public float PitU0, PitU1;   // pit lane extent as arc-length fraction (may wrap)
            public bool NightRace;
            public bool StreetCircuit;
            public Color Terrain, Runoff, Verge;
            public int Seed;
        }

        private static CircuitSpec GetSpec(CircuitType t)
        {
            switch (t)
            {
                case CircuitType.Spa:
                    return new CircuitSpec
                    {
                        DisplayName = "Circuit de Spa-Francorchamps",
                        LengthM = 7004f, HalfWidth = 8.0f, KerbPad = 1.0f, RunoffWidth = 17f,
                        VergeWidth = 14f, VergeDrop = 2.6f, BarrierGap = 3.0f,
                        PitSide = 1, PitU0 = 0.972f, PitU1 = 0.055f,
                        NightRace = false, StreetCircuit = false,
                        Terrain = new Color(0.235f, 0.412f, 0.204f),
                        Runoff = new Color(0.478f, 0.463f, 0.427f),
                        Verge = new Color(0.290f, 0.451f, 0.239f),
                        Seed = 20240701
                    };
                case CircuitType.Monaco:
                    return new CircuitSpec
                    {
                        DisplayName = "Circuit de Monaco",
                        LengthM = 3337f, HalfWidth = 6.0f, KerbPad = 0.7f, RunoffWidth = 0.9f,
                        VergeWidth = 1.6f, VergeDrop = 0.18f, BarrierGap = 0.5f,
                        PitSide = 1, PitU0 = 0.955f, PitU1 = 0.070f,
                        NightRace = false, StreetCircuit = true,
                        Terrain = new Color(0.325f, 0.318f, 0.302f),
                        Runoff = new Color(0.365f, 0.357f, 0.345f),
                        Verge = new Color(0.451f, 0.443f, 0.427f),
                        Seed = 19290414
                    };
                case CircuitType.Monza:
                    return new CircuitSpec
                    {
                        DisplayName = "Autodromo Nazionale Monza",
                        LengthM = 5793f, HalfWidth = 8.0f, KerbPad = 1.1f, RunoffWidth = 21f,
                        VergeWidth = 12f, VergeDrop = 1.4f, BarrierGap = 3.5f,
                        PitSide = 1, PitU0 = 0.955f, PitU1 = 0.085f,
                        NightRace = false, StreetCircuit = false,
                        Terrain = new Color(0.271f, 0.412f, 0.220f),
                        Runoff = new Color(0.529f, 0.502f, 0.451f),
                        Verge = new Color(0.310f, 0.443f, 0.239f),
                        Seed = 19221010
                    };
                default:
                    return new CircuitSpec
                    {
                        DisplayName = "Bahrain International Circuit",
                        // Sakhir has the widest sand run-off of the four; the verge is kept shorter
                        // so the graded ground never reaches the road where the lap doubles back.
                        LengthM = 5412f, HalfWidth = 8.0f, KerbPad = 1.1f, RunoffWidth = 24f,
                        VergeWidth = 12f, VergeDrop = 1.1f, BarrierGap = 4.0f,
                        PitSide = 1, PitU0 = 0.960f, PitU1 = 0.090f,
                        NightRace = true, StreetCircuit = false,
                        Terrain = new Color(0.706f, 0.612f, 0.475f),
                        Runoff = new Color(0.769f, 0.667f, 0.510f),
                        Verge = new Color(0.678f, 0.588f, 0.451f),
                        Seed = 20040404
                    };
            }
        }

        // ------------------------------------------------------------------ public entry point

        public static GameObject BuildCircuitVisuals(Transform parent, CircuitType circuitType)
        {
            Transform existing = parent.Find("F1_Circuit_Geometry");
            if (existing != null) DestroyNow(existing.gameObject);

            CircuitSpec spec = GetSpec(circuitType);
            UnityEngine.Random.InitState(spec.Seed);   // identical layout on every load

            GameObject root = new GameObject("F1_Circuit_Geometry");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            Pal pal = new Pal(circuitType, spec);

            List<Vector3> centre = GenerateCenterline(circuitType);
            Frame[] fr = BuildFrames(centre);

            BuildLandscape(root.transform, fr, spec, pal);
            BuildTrackSurface(root.transform, fr, spec, pal);
            BuildKerbs(root.transform, fr, spec, pal);
            BuildTrackMarkings(root.transform, fr, spec, pal);
            BuildBarriers(root.transform, fr, spec, pal);
            BuildStartLineAndGrid(root.transform, fr, spec, pal);
            BuildPitComplex(root.transform, fr, spec, pal);
            BuildBrakingMarkersAndMarshalPosts(root.transform, fr, spec, pal);

            switch (circuitType)
            {
                case CircuitType.Bahrain: BuildBahrain(root.transform, fr, spec, pal); break;
                case CircuitType.Spa: BuildSpa(root.transform, fr, spec, pal); break;
                case CircuitType.Monza: BuildMonza(root.transform, fr, spec, pal); break;
                default: BuildMonaco(root.transform, fr, spec, pal); break;
            }

            Debug.Log(string.Format(
                "[TrackVisualBuilder] {0}: {1} stations, lap {2:F1} m, closure {3:F4} m, elevation {4:F1} m to {5:F1} m",
                spec.DisplayName, fr.Length, LapLength(fr),
                Vector3.Distance(centre[0], centre[centre.Count - 1]), MinY(centre), MaxY(centre)));

            return root;
        }

        // ------------------------------------------------------------------ centreline generation

        // Grid layout, shared with BuildStartLineAndGrid so a spawned car sits inside its painted box.
        private const float GridFirstSlotBack = 10.0f;
        private const float GridSlotPitch = 8.0f;
        private const float GridLateralFraction = 0.42f;
        public const int GridSlotCount = 20;

        /// <summary>
        /// Position and orientation of a starting-grid slot (0 = pole), on the centreline returned
        /// by GenerateCenterline. Matches the painted grid boxes exactly.
        /// </summary>
        public static void GetGridSlot(List<Vector3> centre, CircuitType circuit, int slot,
                                       out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (centre == null || centre.Count < 8) return;

            int n = centre.Count - 1;          // last element repeats the first
            float halfWidth = GetSpec(circuit).HalfWidth;
            float back = GridFirstSlotBack + Mathf.Max(0, slot) * GridSlotPitch;
            int idx = ((StartFinishIndex - Mathf.RoundToInt(back / StationSpacing)) % n + n) % n;

            Vector3 pos = centre[idx];
            Vector3 fwd = (centre[(idx + 1) % n] - centre[((idx - 1) % n + n) % n]).normalized;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            right.y = 0f;
            right.Normalize();

            float lateral = (slot % 2 == 0 ? 1f : -1f) * halfWidth * GridLateralFraction;
            position = pos + right * lateral;
            rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        public static List<Vector3> GenerateBahrainGrandPrixSpline() { return GenerateCenterline(CircuitType.Bahrain); }
        public static List<Vector3> GenerateSpaFrancorchampsSpline() { return GenerateCenterline(CircuitType.Spa); }
        public static List<Vector3> GenerateMonzaGrandPrixSpline() { return GenerateCenterline(CircuitType.Monza); }
        public static List<Vector3> GenerateMonacoGrandPrixSpline() { return GenerateCenterline(CircuitType.Monaco); }

        /// <summary>
        /// Returns the finished centreline: uniformly spaced, exactly closed, calibrated to the
        /// official lap length, with the elevation profile applied. The final element repeats the
        /// first so consumers can walk segment i -> i+1 across the whole lap.
        /// </summary>
        public static List<Vector3> GenerateCenterline(CircuitType circuitType)
        {
            CircuitSpec spec = GetSpec(circuitType);
            List<Vector2> raw;
            switch (circuitType)
            {
                case CircuitType.Bahrain: raw = RawBahrain(); break;
                case CircuitType.Spa: raw = RawSpa(); break;
                case CircuitType.Monza: raw = RawFromAnchors(MonzaAnchors); break;
                default: raw = RawFromAnchors(MonacoAnchors); break;
            }

            List<Vector2> loop = CloseLoopExact(raw);
            loop = ResampleClosed(loop, StationSpacing);
            loop = SmoothClosed(loop, 2, 0.35f);
            loop = ScaleToLength(loop, spec.LengthM);
            loop = ResampleClosed(loop, StationSpacing);
            loop = ScaleToLength(loop, spec.LengthM);

            List<Vector3> pts = new List<Vector3>(loop.Count + 1);
            for (int i = 0; i < loop.Count; i++) pts.Add(new Vector3(loop[i].x, 0f, loop[i].y));
            ApplyElevationProfile(pts, ElevationProfile(circuitType));
            pts.Add(pts[0]);   // explicit closure vertex
            return pts;
        }

        // ---- raw layout sources -------------------------------------------------------------

        // Bahrain and Spa are authored as intrinsic chains (straight lengths + corner radii), which is
        // how the corner geometry is actually specified. The chain does not return exactly to the
        // origin; CloseLoopExact resolves that instead of teleporting the last vertex.

        private static List<Vector2> RawBahrain()
        {
            List<Vector2> p = new List<Vector2> { Vector2.zero };
            float h = 0f;
            Vector2 c = Chain.Line(p, Vector2.zero, 0f, 1040f);          // main straight
            c = Chain.Arc(p, c, ref h, 130f, 42f);                       // T1 Michael Schumacher
            c = Chain.Arc(p, c, ref h, -70f, 58f);                       // T2
            c = Chain.Arc(p, c, ref h, 40f, 85f);                        // T3
            c = Chain.Line(p, c, h, 450f);
            c = Chain.Arc(p, c, ref h, 90f, 75f);                        // T4
            c = Chain.Line(p, c, h, 135f);
            c = Chain.Arc(p, c, ref h, -45f, 115f);                      // T5
            c = Chain.Arc(p, c, ref h, 90f, 100f);                       // T6
            c = Chain.Arc(p, c, ref h, -45f, 115f);                      // T7
            c = Chain.Line(p, c, h, 195f);
            c = Chain.Arc(p, c, ref h, 130f, 45f);                       // T8
            c = Chain.Line(p, c, h, 295f);
            c = Chain.Arc(p, c, ref h, -55f, 75f);                       // T9
            c = Chain.Arc(p, c, ref h, -90f, 45f);                       // T10
            c = Chain.Line(p, c, h, 715f);                               // back straight
            c = Chain.Arc(p, c, ref h, -65f, 90f);                       // T11
            c = Chain.Line(p, c, h, 175f);
            c = Chain.Arc(p, c, ref h, 45f, 145f);                       // T12
            c = Chain.Line(p, c, h, 135f);
            c = Chain.Arc(p, c, ref h, 80f, 70f);                        // T13
            c = Chain.AimAt(p, c, ref h, new Vector2(85f, -85f), 70f);    // run down to the T14 braking zone
            c = Chain.TurnTo(p, c, ref h, 270f, 55f);                    // T14
            c = Chain.Arc(p, c, ref h, 90f, 85f);                        // T15 onto the main straight
            return p;
        }

        private static List<Vector2> RawSpa()
        {
            List<Vector2> p = new List<Vector2> { Vector2.zero };
            float h = 0f;
            Vector2 c = Chain.Line(p, Vector2.zero, 0f, 420f);           // pit straight
            c = Chain.Arc(p, c, ref h, 140f, 30f);                       // T1 La Source
            c = Chain.Line(p, c, h, 420f);
            c = Chain.Arc(p, c, ref h, -45f, 110f);                      // T2 Eau Rouge
            c = Chain.Arc(p, c, ref h, 70f, 130f);                       // T3 Raidillon
            c = Chain.Arc(p, c, ref h, -125f, 150f);                     // T4 crest
            c = Chain.Line(p, c, h, 1000f);                              // Kemmel straight
            c = Chain.Arc(p, c, ref h, 85f, 48f);                        // T5 Les Combes
            c = Chain.Arc(p, c, ref h, -85f, 48f);                       // T6 Les Combes
            c = Chain.Arc(p, c, ref h, 75f, 75f);                        // T7 Malmedy
            c = Chain.Line(p, c, h, 240f);
            c = Chain.Arc(p, c, ref h, 170f, 44f);                       // T8 Bruxelles / Rivage
            c = Chain.Line(p, c, h, 130f);
            c = Chain.Arc(p, c, ref h, -80f, 58f);                       // T9 Speaker's corner
            c = Chain.Line(p, c, h, 220f);
            c = Chain.Arc(p, c, ref h, -110f, 115f);                     // T10/T11 Pouhon
            c = Chain.Line(p, c, h, 250f);
            c = Chain.Arc(p, c, ref h, 85f, 52f);                        // T12 Fagnes
            c = Chain.Arc(p, c, ref h, -85f, 52f);                       // T13 Fagnes
            c = Chain.Line(p, c, h, 150f);
            c = Chain.Arc(p, c, ref h, 80f, 62f);                        // T14 Campus
            c = Chain.Arc(p, c, ref h, 95f, 80f);                        // T15/T16 Stavelot
            c = Chain.Line(p, c, h, 600f);
            c = Chain.Arc(p, c, ref h, -30f, 220f);                      // T17 Blanchimont
            c = Chain.Line(p, c, h, 320f);
            c = Chain.Arc(p, c, ref h, -35f, 240f);                      // T18 Blanchimont 2
            c = Chain.AimAt(p, c, ref h, new Vector2(-20f, -60f), 90f);   // run down to the Bus Stop
            c = Chain.TurnTo(p, c, ref h, 65f, 26f);                     // T19 Bus Stop entry
            c = Chain.Arc(p, c, ref h, -65f, 26f);                       // Bus Stop exit
            float lead = c.magnitude;
            if (lead > 0.5f) c = Chain.Line(p, c, 0f, lead);             // back onto the pit straight
            return p;
        }

        // Monza and Monaco are authored as closed anchor polygons, which close by construction.

        private static readonly float[,] MonzaAnchors = {
            // Rettifilo Tribune - main straight
            {0,0}, {-120,0}, {-250,0}, {-340,0},
            // T1/T2 Variante del Rettifilo
            {-375,4}, {-400,20}, {-420,32}, {-445,38}, {-485,40},
            // T3 Curva Grande / Curva Biassono
            {-555,52}, {-625,85}, {-675,150}, {-695,230}, {-710,315},
            // T4/T5 Variante della Roggia
            {-722,355}, {-732,372}, {-726,392}, {-732,412},
            // T6 Curva di Lesmo 1
            {-740,475}, {-745,530}, {-735,555}, {-705,575}, {-665,580},
            // T7 Curva di Lesmo 2
            {-605,585}, {-560,590}, {-525,582}, {-500,560}, {-475,520},
            // Curva del Serraglio - passes beneath the historic banked oval
            {-410,435}, {-340,345}, {-270,255}, {-210,180}, {-160,125},
            // T8/T9/T10 Variante Ascari
            {-130,98}, {-112,88}, {-85,94}, {-58,80}, {-32,66},
            // Rettifilo Centrale - back straight
            {50,62}, {160,60}, {270,58}, {370,55},
            // T11 Curva Parabolica / Curva Alboreto
            {420,50}, {455,30}, {465,-5}, {450,-35}, {410,-50}, {340,-45}, {240,-25}, {130,-5}
        };

        private static readonly float[,] MonacoAnchors = {
            // Boulevard Albert 1er - the start/finish line has real straight on both sides of it so
            // the lap join is dead straight (the previous anchors cusped 83 degrees here).
            {0,0}, {0,60}, {0,120}, {0,175},
            // T1 Sainte Devote
            {8,205}, {30,222}, {60,226},
            // Beau Rivage - the uphill climb away from the harbour
            {115,240}, {170,260}, {220,288}, {258,322},
            // T3 Massenet
            {278,356}, {280,392}, {268,420},
            // T4 Casino Square - the +42 m summit
            {282,448}, {310,464}, {342,464},
            // Avenue d'Ostende, turning south and descending
            {368,456}, {384,438}, {388,414},
            // T5 Mirabeau Haute
            {388,398}, {382,384}, {368,376},
            // T6 Fairmont Hairpin
            {352,370}, {338,368}, {330,354}, {338,342}, {352,338},
            // T7 Mirabeau Bas
            {370,338}, {386,342}, {398,352},
            // T8 Portier - onto the seafront
            {410,338}, {420,324}, {422,308}, {412,298},
            // The Tunnel - long right along the seafront, well below Beau Rivage
            {392,286}, {362,270}, {330,252}, {296,232}, {260,212}, {224,192},
            // T10/T11 Nouvelle Chicane
            {198,178}, {182,166}, {166,168}, {150,174},
            // Quai des Etats-Unis
            {120,168}, {90,160},
            // T12 Tabac
            {66,146}, {52,128}, {48,108},
            // Piscine - swimming pool complex
            {50,86}, {40,68}, {52,52}, {66,38}, {66,16}, {56,0}, {64,-18}, {64,-38},
            // run down towards La Rascasse
            {60,-64}, {52,-94}, {42,-126},
            // T17 La Rascasse
            {30,-152}, {12,-162}, {-4,-152},
            // T18 Antony Noghes
            {-10,-132}, {-4,-106}, {0,-76}, {0,-40}
        };

        private static List<Vector2> RawFromAnchors(float[,] a)
        {
            int n = a.GetLength(0);
            const int samplesPerSpan = 24;
            List<Vector2> pts = new List<Vector2>(n * samplesPerSpan);
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = Anchor(a, (i - 1 + n) % n);
                Vector2 p1 = Anchor(a, i);
                Vector2 p2 = Anchor(a, (i + 1) % n);
                Vector2 p3 = Anchor(a, (i + 2) % n);
                for (int s = 0; s < samplesPerSpan; s++)
                    pts.Add(CatmullRom2D(p0, p1, p2, p3, s / (float)samplesPerSpan));
            }
            // Close the chain explicitly: an anchor polygon is already closed, and without this the
            // closure solver would see a phantom one-step residual and warp the layout to "fix" it.
            pts.Add(pts[0]);
            return pts;
        }

        private static Vector2 Anchor(float[,] a, int i) { return new Vector2(a[i, 0], a[i, 1]); }

        private static class Chain
        {
            private const float Step = 2.0f;

            public static Vector2 Line(List<Vector2> pts, Vector2 start, float headingDeg, float length)
            {
                float r = headingDeg * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
                int n = Mathf.Max(1, Mathf.RoundToInt(length / Step));
                float s = length / n;
                for (int i = 1; i <= n; i++) pts.Add(start + d * (i * s));
                return pts[pts.Count - 1];
            }

            /// <summary>Turn onto an absolute heading through an arc of the given radius.</summary>
            public static Vector2 TurnTo(List<Vector2> pts, Vector2 start, ref float headingDeg, float absHeadingDeg, float radius)
            {
                return Arc(pts, start, ref headingDeg, Mathf.DeltaAngle(headingDeg, absHeadingDeg), radius);
            }

            /// <summary>Turn to face a target point, then run straight to it.</summary>
            public static Vector2 AimAt(List<Vector2> pts, Vector2 start, ref float headingDeg, Vector2 target, float radius)
            {
                float aim = Mathf.Atan2(target.x - start.x, target.y - start.y) * Mathf.Rad2Deg;
                Vector2 c = Arc(pts, start, ref headingDeg, Mathf.DeltaAngle(headingDeg, aim), radius);
                float d = Vector2.Distance(target, c);
                if (d > 1f) c = Line(pts, c, headingDeg, d);
                return c;
            }

            public static Vector2 Arc(List<Vector2> pts, Vector2 start, ref float headingDeg, float turnDeg, float radius)
            {
                bool right = turnDeg > 0f;
                float cr = (headingDeg + (right ? 90f : -90f)) * Mathf.Deg2Rad;
                Vector2 centre = start + new Vector2(Mathf.Sin(cr), Mathf.Cos(cr)) * radius;
                int n = Mathf.Max(2, Mathf.RoundToInt(Mathf.Abs(turnDeg) * Mathf.Deg2Rad * radius / Step));
                for (int i = 1; i <= n; i++)
                {
                    float t = i / (float)n;
                    float pr = (headingDeg + turnDeg * t + (right ? -90f : 90f)) * Mathf.Deg2Rad;
                    pts.Add(centre + new Vector2(Mathf.Sin(pr), Mathf.Cos(pr)) * radius);
                }
                headingDeg += turnDeg;
                return pts[pts.Count - 1];
            }
        }

        // ---- exact loop closure --------------------------------------------------------------

        /// <summary>
        /// Closes an open chain whose end should return to its start. Solves for the smallest
        /// per-step turn-rate change that drives the position and heading residual to zero, weighted
        /// so the correction lands on straights rather than inside tight corners. Minimum-norm means
        /// the shape change is the smallest one that closes the loop, so it is not visible.
        /// </summary>
        private static List<Vector2> CloseLoopExact(List<Vector2> src)
        {
            int n = src.Count - 1;
            if (n < 8) return src;

            float[] step = new float[n];
            float[] kappa = new float[n];
            float baseHeading = 0f;
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = src[i + 1] - src[i];
                step[i] = d.magnitude;
                float head = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                if (i == 0) { baseHeading = head; kappa[0] = 0f; }
                else kappa[i] = Mathf.DeltaAngle(prev, head);
                prev = head;
            }

            float[] weight = new float[n];
            for (int i = 0; i < n; i++)
            {
                float perMetre = Mathf.Abs(kappa[i]) / Mathf.Max(0.01f, step[i]);
                weight[i] = 1f / (1f + 900f * perMetre * perMetre);
            }
            weight[0] = 0f;   // hold the start/finish straight's heading

            float[] hx = new float[n + 1];
            Vector2[] pos = new Vector2[n + 1];

            for (int iter = 0; iter < 24; iter++)
            {
                float h = baseHeading;
                Vector2 p = Vector2.zero;
                pos[0] = p; hx[0] = baseHeading;
                for (int i = 0; i < n; i++)
                {
                    h += kappa[i];
                    float r = h * Mathf.Deg2Rad;
                    p += new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * step[i];
                    pos[i + 1] = p; hx[i + 1] = h;
                }

                float rx = p.x, rz = p.y, rh = Mathf.DeltaAngle(baseHeading + 360f, h);
                if (new Vector2(rx, rz).magnitude < 1e-4f && Mathf.Abs(rh) < 1e-5f) break;

                // Jacobian of the endpoint w.r.t. each step's turn angle (suffix rotation)
                float[] jx = new float[n];
                float[] jz = new float[n];
                float sx = 0f, sz = 0f;
                for (int i = n - 1; i >= 0; i--)
                {
                    float r = hx[i + 1] * Mathf.Deg2Rad;
                    sx += Mathf.Sin(r) * step[i];
                    sz += Mathf.Cos(r) * step[i];
                    jx[i] = sz * Mathf.Deg2Rad;
                    jz[i] = -sx * Mathf.Deg2Rad;
                }

                // A = J W J^T  (3x3), solve A * lambda = -residual
                double[,] A = new double[3, 3];
                for (int i = 0; i < n; i++)
                {
                    double w = weight[i];
                    double g0 = jx[i], g1 = jz[i], g2 = 1.0;
                    A[0, 0] += w * g0 * g0; A[0, 1] += w * g0 * g1; A[0, 2] += w * g0 * g2;
                    A[1, 0] += w * g1 * g0; A[1, 1] += w * g1 * g1; A[1, 2] += w * g1 * g2;
                    A[2, 0] += w * g2 * g0; A[2, 1] += w * g2 * g1; A[2, 2] += w * g2 * g2;
                }
                double[] lam;
                if (!Solve3(A, new double[] { -rx, -rz, -rh }, out lam)) break;
                for (int i = 0; i < n; i++)
                    kappa[i] += weight[i] * (float)(jx[i] * lam[0] + jz[i] * lam[1] + lam[2]);
            }

            List<Vector2> outPts = new List<Vector2>(n);
            {
                float h = baseHeading;
                Vector2 p = Vector2.zero;
                outPts.Add(p);
                for (int i = 0; i < n - 1; i++)
                {
                    h += kappa[i];
                    float r = h * Mathf.Deg2Rad;
                    p += new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * step[i];
                    outPts.Add(p);
                }
            }
            return outPts;
        }

        private static bool Solve3(double[,] a, double[] b, out double[] x)
        {
            x = new double[3];
            double[,] m = new double[3, 4];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++) m[i, j] = a[i, j];
                m[i, 3] = b[i];
            }
            for (int i = 0; i < 3; i++)
            {
                int piv = i;
                for (int k = i + 1; k < 3; k++) if (Math.Abs(m[k, i]) > Math.Abs(m[piv, i])) piv = k;
                if (Math.Abs(m[piv, i]) < 1e-16) return false;
                if (piv != i) for (int j = 0; j < 4; j++) { double tmp = m[i, j]; m[i, j] = m[piv, j]; m[piv, j] = tmp; }
                for (int k = i + 1; k < 3; k++)
                {
                    double f = m[k, i] / m[i, i];
                    for (int j = i; j < 4; j++) m[k, j] -= f * m[i, j];
                }
            }
            for (int i = 2; i >= 0; i--)
            {
                double s = m[i, 3];
                for (int j = i + 1; j < 3; j++) s -= m[i, j] * x[j];
                x[i] = s / m[i, i];
            }
            return true;
        }

        // ---- resample / smooth / scale -------------------------------------------------------

        private static List<Vector2> ResampleClosed(List<Vector2> loop, float targetStep)
        {
            int n = loop.Count;
            float[] seg = new float[n];
            float[] cum = new float[n + 1];
            for (int i = 0; i < n; i++)
            {
                seg[i] = (loop[(i + 1) % n] - loop[i]).magnitude;
                cum[i + 1] = cum[i] + seg[i];
            }
            float total = cum[n];
            int count = Mathf.Max(48, Mathf.RoundToInt(total / targetStep));
            float s = total / count;

            List<Vector2> outPts = new List<Vector2>(count);
            int k = 0;
            for (int i = 0; i < count; i++)
            {
                float target = i * s;
                while (k < n - 1 && cum[k + 1] < target) k++;
                float t = seg[k] > 1e-6f ? (target - cum[k]) / seg[k] : 0f;
                outPts.Add(Vector2.LerpUnclamped(loop[k], loop[(k + 1) % n], t));
            }
            return outPts;
        }

        private static List<Vector2> SmoothClosed(List<Vector2> loop, int passes, float alpha)
        {
            int n = loop.Count;
            List<Vector2> cur = new List<Vector2>(loop);
            List<Vector2> next = new List<Vector2>(new Vector2[n]);
            for (int p = 0; p < passes; p++)
            {
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = cur[(i - 1 + n) % n], b = cur[i], c = cur[(i + 1) % n];
                    next[i] = b + alpha * ((a + c) * 0.5f - b);
                }
                List<Vector2> t = cur; cur = next; next = t;
            }
            return cur;
        }

        private static List<Vector2> ScaleToLength(List<Vector2> loop, float targetLength)
        {
            int n = loop.Count;
            float len = 0f;
            for (int i = 0; i < n; i++) len += (loop[(i + 1) % n] - loop[i]).magnitude;
            if (len < 1f) return loop;
            float f = targetLength / len;
            List<Vector2> outPts = new List<Vector2>(n);
            for (int i = 0; i < n; i++) outPts.Add(loop[i] * f);
            return outPts;
        }

        private static Vector2 CatmullRom2D(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ---- elevation ------------------------------------------------------------------------

        private static Vector2[] ElevationProfile(CircuitType t)
        {
            switch (t)
            {
                case CircuitType.Spa:
                    // 100 m of relief. Eau Rouge is the low point, Les Combes the high point.
                    return new[] {
                        new Vector2(0.000f,   0f), new Vector2(0.040f,   6f), new Vector2(0.070f,   2f),
                        new Vector2(0.100f, -22f), new Vector2(0.135f, -52f), new Vector2(0.160f, -34f),
                        new Vector2(0.190f,  -6f), new Vector2(0.230f,  10f), new Vector2(0.290f,  32f),
                        new Vector2(0.340f,  44f), new Vector2(0.380f,  48f), new Vector2(0.430f,  40f),
                        new Vector2(0.470f,  28f), new Vector2(0.530f,   4f), new Vector2(0.580f,  10f),
                        new Vector2(0.630f,  -4f), new Vector2(0.680f, -14f), new Vector2(0.740f, -12f),
                        new Vector2(0.800f,  -8f), new Vector2(0.860f,  -2f), new Vector2(0.920f,   2f),
                        new Vector2(0.970f,   1f), new Vector2(1.000f,   0f)
                    };
                case CircuitType.Monaco:
                    // 42 m: sea level along the harbour, Casino Square at the summit.
                    return new[] {
                        new Vector2(0.000f,  0f), new Vector2(0.090f,  1f), new Vector2(0.115f,  4f),
                        new Vector2(0.180f, 18f), new Vector2(0.250f, 32f), new Vector2(0.300f, 40f),
                        new Vector2(0.325f, 42f), new Vector2(0.380f, 34f), new Vector2(0.430f, 24f),
                        new Vector2(0.465f, 16f), new Vector2(0.500f,  8f), new Vector2(0.530f,  3f),
                        new Vector2(0.560f,  1f), new Vector2(0.660f,  0f), new Vector2(0.740f,  0f),
                        new Vector2(0.820f,  0f), new Vector2(0.910f,  0f), new Vector2(0.960f,  0f),
                        new Vector2(1.000f,  0f)
                    };
                case CircuitType.Monza:
                    // Royal Park is nearly flat; a gentle rise through the Lesmos.
                    return new[] {
                        new Vector2(0.000f, 0.0f), new Vector2(0.120f, 0.0f), new Vector2(0.210f, 1.5f),
                        new Vector2(0.310f, 3.0f), new Vector2(0.365f, 5.5f), new Vector2(0.440f, 7.5f),
                        new Vector2(0.520f, 5.0f), new Vector2(0.600f, 3.0f), new Vector2(0.670f, 2.0f),
                        new Vector2(0.750f, 1.0f), new Vector2(0.850f, 0.5f), new Vector2(0.930f, 0.0f),
                        new Vector2(1.000f, 0.0f)
                    };
                default:
                    return new[] {
                        new Vector2(0.000f,  0.0f), new Vector2(0.100f,  1.5f), new Vector2(0.190f,  4.0f),
                        new Vector2(0.240f,  8.0f), new Vector2(0.330f, 12.0f), new Vector2(0.400f, 14.5f),
                        new Vector2(0.480f, 10.0f), new Vector2(0.560f,  5.0f), new Vector2(0.620f,  3.0f),
                        new Vector2(0.700f,  6.0f), new Vector2(0.770f,  8.5f), new Vector2(0.820f,  6.0f),
                        new Vector2(0.900f,  2.5f), new Vector2(0.960f,  0.5f), new Vector2(1.000f,  0.0f)
                    };
            }
        }

        /// <summary>
        /// Applies a periodic height profile by arc-length fraction. Smoothstep between control
        /// points gives zero slope at each one, so the profile is C1 continuous across the lap join.
        /// </summary>
        private static void ApplyElevationProfile(List<Vector3> pts, Vector2[] control)
        {
            int n = pts.Count;
            float[] cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(pts[i], pts[(i + 1) % n]);
            float total = Mathf.Max(1f, cum[n]);

            for (int i = 0; i < n; i++)
            {
                float u = cum[i] / total;
                float y = control[0].y;
                for (int k = 0; k < control.Length - 1; k++)
                {
                    if (u < control[k].x || u > control[k + 1].x) continue;
                    float span = Mathf.Max(1e-4f, control[k + 1].x - control[k].x);
                    float t = (u - control[k].x) / span;
                    y = Mathf.Lerp(control[k].y, control[k + 1].y, t * t * (3f - 2f * t));
                    break;
                }
                pts[i] = new Vector3(pts[i].x, y, pts[i].z);
            }
        }

        // ------------------------------------------------------------------ frames

        private struct Frame
        {
            public Vector3 Pos;
            public Vector3 Fwd;      // unit, follows elevation
            public Vector3 Right;    // unit, horizontal
            public float S;          // arc length from the start/finish line
            public float U;          // arc-length fraction
            public float Curv;       // signed curvature, degrees per metre (+ = right-hand corner)
        }

        /// <summary>
        /// Builds a per-station frame using wrapped central differences, so the frame at the last
        /// station matches the frame at the first exactly and the surface has no seam at the lap join.
        /// </summary>
        private static Frame[] BuildFrames(List<Vector3> centre)
        {
            int n = centre.Count - 1;                 // last element repeats the first
            Frame[] fr = new Frame[n];

            float[] cum = new float[n + 1];
            for (int i = 0; i < n; i++) cum[i + 1] = cum[i] + Vector3.Distance(centre[i], centre[(i + 1) % n]);
            float total = Mathf.Max(1f, cum[n]);

            for (int i = 0; i < n; i++)
            {
                Vector3 prev = centre[(i - 1 + n) % n];
                Vector3 next = centre[(i + 1) % n];
                Vector3 fwd = (next - prev);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward;
                fwd.Normalize();

                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                right.y = 0f;
                if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
                right.Normalize();

                Vector2 a = new Vector2(centre[i].x - prev.x, centre[i].z - prev.z);
                Vector2 b = new Vector2(next.x - centre[i].x, next.z - centre[i].z);
                float turn = 0f;
                if (a.sqrMagnitude > 1e-8f && b.sqrMagnitude > 1e-8f)
                {
                    float h1 = Mathf.Atan2(a.x, a.y) * Mathf.Rad2Deg;
                    float h2 = Mathf.Atan2(b.x, b.y) * Mathf.Rad2Deg;
                    turn = Mathf.DeltaAngle(h1, h2);
                }
                float span = Mathf.Max(0.5f, (next - prev).magnitude * 0.5f);

                fr[i] = new Frame
                {
                    Pos = centre[i],
                    Fwd = fwd,
                    Right = right,
                    S = cum[i],
                    U = cum[i] / total,
                    Curv = turn / span
                };
            }
            return fr;
        }

        private static float LapLength(Frame[] fr)
        {
            return fr[fr.Length - 1].S + Vector3.Distance(fr[fr.Length - 1].Pos, fr[0].Pos);
        }

        private static int IndexAtU(Frame[] fr, float u)
        {
            u -= Mathf.Floor(u);
            return Mathf.Clamp(Mathf.RoundToInt(u * fr.Length), 0, fr.Length - 1);
        }

        /// <summary>Local corner radius in metres (very large on a straight).</summary>
        private static float LocalRadius(Frame f)
        {
            float k = Mathf.Abs(f.Curv) * Mathf.Deg2Rad;
            return k < 1e-5f ? 1e5f : 1f / k;
        }

        // ------------------------------------------------------------------ mesh plumbing

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> verts = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int>[] tris;

            public MeshBuilder(int subMeshes)
            {
                tris = new List<int>[subMeshes];
                for (int i = 0; i < subMeshes; i++) tris[i] = new List<int>();
            }

            public int VertexCount { get { return verts.Count; } }

            public int Add(Vector3 p, Vector3 n, Vector2 uv)
            {
                verts.Add(p); normals.Add(n); uvs.Add(uv);
                return verts.Count - 1;
            }

            public void Tri(int sub, int a, int b, int c)
            {
                List<int> t = tris[sub];
                t.Add(a); t.Add(b); t.Add(c);
            }

            public void Quad(int sub, int a, int b, int c, int d)
            {
                Tri(sub, a, b, c);
                Tri(sub, a, c, d);
            }

            public bool IsEmpty
            {
                get
                {
                    for (int i = 0; i < tris.Length; i++) if (tris[i].Count > 0) return false;
                    return true;
                }
            }

            public Mesh Build(string name)
            {
                Mesh m = new Mesh { name = name };
                if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(verts);
                m.SetNormals(normals);
                m.SetUVs(0, uvs);
                m.subMeshCount = tris.Length;
                for (int i = 0; i < tris.Length; i++) m.SetTriangles(tris[i], i, false);
                m.RecalculateBounds();
                return m;
            }
        }

        private static GameObject Emit(Transform parent, string name, MeshBuilder mb, Material[] mats, bool collider)
        {
            if (mb == null || mb.IsEmpty) return null;
            Mesh mesh = mb.Build(name);
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        /// <summary>One point of a lateral cross-section: offset across the track and height above it.</summary>
        private struct Prof
        {
            public float Lat, Vert;
            public Prof(float lat, float vert) { Lat = lat; Vert = vert; }
        }

        /// <summary>
        /// Extrudes a lateral cross-section along a run of frames. This single routine builds the
        /// road surface, kerbs, barriers, pit lane, grandstand tiers and the landscape apron, which
        /// is why none of them can develop gaps between segments.
        /// bandSub[i] selects the submesh for the band between prof[i] and prof[i+1] (-1 skips it).
        /// stripe, when supplied, overrides that per station for alternating kerb/panel colours.
        /// </summary>
        private static void Extrude(MeshBuilder mb, Frame[] fr, int from, int count, bool wrap,
                                    Prof[] prof, int[] bandSub, float uvScale, bool doubleSided,
                                    Func<int, int, int> stripe, Func<int, float> latScale)
        {
            int n = fr.Length;
            int stations = wrap ? count + 1 : count;
            if (stations < 2 || prof.Length < 2) return;

            int bands = prof.Length - 1;

            // One vertex strip per band, so material boundaries stay crisp and each band keeps its
            // own normal. Bands whose profile runs outward-to-inward are wound the other way round,
            // otherwise they would render back-facing (invisible from the track).
            for (int b = 0; b < bands; b++)
            {
                int baseSub = bandSub[b];
                if (baseSub < 0) continue;

                bool reversed = (prof[b + 1].Lat - prof[b].Lat) < -1e-6f;

                int[] inner = new int[stations];
                int[] outer = new int[stations];

                for (int s = 0; s < stations; s++)
                {
                    int idx = wrap ? (from + s) % n : Mathf.Clamp(from + s, 0, n - 1);
                    Frame f = fr[idx];
                    float scale = latScale != null ? latScale(idx) : 1f;

                    Vector3 pi = f.Pos + f.Right * (prof[b].Lat * scale) + Vector3.up * prof[b].Vert;
                    Vector3 po = f.Pos + f.Right * (prof[b + 1].Lat * scale) + Vector3.up * prof[b + 1].Vert;
                    Vector3 nrm = Vector3.Cross(f.Fwd, po - pi).normalized;
                    if (reversed) nrm = -nrm;

                    float v = f.S * uvScale;
                    inner[s] = mb.Add(pi, nrm, new Vector2(0f, v));
                    outer[s] = mb.Add(po, nrm, new Vector2(1f, v));
                }

                for (int s = 0; s < stations - 1; s++)
                {
                    int idx = wrap ? (from + s) % n : Mathf.Clamp(from + s, 0, n - 1);
                    int sub = stripe != null ? stripe(idx, b) : baseSub;
                    if (sub < 0) continue;
                    if (reversed) mb.Quad(sub, outer[s], outer[s + 1], inner[s + 1], inner[s]);
                    else mb.Quad(sub, inner[s], inner[s + 1], outer[s + 1], outer[s]);
                    if (doubleSided)
                    {
                        if (reversed) mb.Quad(sub, inner[s], inner[s + 1], outer[s + 1], outer[s]);
                        else mb.Quad(sub, outer[s], outer[s + 1], inner[s + 1], inner[s]);
                    }
                }
            }
        }

        private static void Extrude(MeshBuilder mb, Frame[] fr, int from, int count, bool wrap,
                                    Prof[] prof, int[] bandSub, float uvScale)
        {
            Extrude(mb, fr, from, count, wrap, prof, bandSub, uvScale, false, null, null);
        }

        // ---- batched primitive helpers (props are merged into few meshes, not thousands of objects)

        private static void AddBox(MeshBuilder mb, int sub, Vector3 centre, Vector3 half, Quaternion rot)
        {
            Vector3[] c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 s = new Vector3((i & 1) == 0 ? -half.x : half.x,
                                        (i & 2) == 0 ? -half.y : half.y,
                                        (i & 4) == 0 ? -half.z : half.z);
                c[i] = centre + rot * s;
            }
            int[][] faces = {
                new[]{0,2,3,1}, new[]{4,5,7,6}, new[]{0,1,5,4},
                new[]{2,6,7,3}, new[]{0,4,6,2}, new[]{1,3,7,5}
            };
            foreach (int[] f in faces)
            {
                Vector3 nrm = Vector3.Cross(c[f[1]] - c[f[0]], c[f[2]] - c[f[0]]).normalized;
                int a = mb.Add(c[f[0]], nrm, new Vector2(0f, 0f));
                int b = mb.Add(c[f[1]], nrm, new Vector2(1f, 0f));
                int d = mb.Add(c[f[2]], nrm, new Vector2(1f, 1f));
                int e = mb.Add(c[f[3]], nrm, new Vector2(0f, 1f));
                mb.Quad(sub, a, b, d, e);
            }
        }

        private static void AddTaperedCylinder(MeshBuilder mb, int sub, Vector3 basePos, float rBottom, float rTop, float height, int sides)
        {
            int[] lo = new int[sides + 1];
            int[] hi = new int[sides + 1];
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                lo[i] = mb.Add(basePos + dir * rBottom, dir, new Vector2(i / (float)sides, 0f));
                hi[i] = mb.Add(basePos + Vector3.up * height + dir * rTop, dir, new Vector2(i / (float)sides, 1f));
            }
            for (int i = 0; i < sides; i++) mb.Quad(sub, lo[i], lo[i + 1], hi[i + 1], hi[i]);

            if (rTop > 0.01f)
            {
                int capC = mb.Add(basePos + Vector3.up * height, Vector3.up, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                    int v0 = mb.Add(basePos + Vector3.up * height + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * rTop, Vector3.up, Vector2.zero);
                    int v1 = mb.Add(basePos + Vector3.up * height + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * rTop, Vector3.up, Vector2.one);
                    mb.Tri(sub, capC, v1, v0);
                }
            }
        }

        private static void AddBlob(MeshBuilder mb, int sub, Vector3 centre, Vector3 radii, int seg)
        {
            int rings = seg, cols = seg * 2;
            int[,] v = new int[rings + 1, cols + 1];
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI;
                for (int c = 0; c <= cols; c++)
                {
                    float th = c / (float)cols * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    v[r, c] = mb.Add(centre + Vector3.Scale(dir, radii), dir.normalized, new Vector2(c / (float)cols, r / (float)rings));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int c = 0; c < cols; c++)
                    mb.Quad(sub, v[r, c], v[r, c + 1], v[r + 1, c + 1], v[r + 1, c]);
        }

        // ------------------------------------------------------------------ landscape

        /// <summary>
        /// Two-part terrain so the circuit never floats: an apron that follows the road exactly out
        /// to 30 m, then a height field sampled from the nearest centreline elevation. The old build
        /// used a single flat plane, which left Spa's Raidillon and Monaco's Casino Square hanging
        /// tens of metres in the air.
        /// </summary>
        private static void BuildLandscape(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Circuit_Landscape");
            holder.transform.SetParent(parent, false);

            float edge = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.VergeWidth;

            // --- apron: welded to the verge so the road always meets real ground.
            // Its outward reach is clamped per station: where the lap doubles back on itself the
            // apron would otherwise be laid straight over the road on the far side.
            MeshBuilder apron = new MeshBuilder(1);
            {
                int n = fr.Length;
                TrackGrid trackGrid = new TrackGrid(fr);
                int window = Mathf.RoundToInt(160f / StationSpacing);
                float clearOf = spec.HalfWidth + spec.KerbPad + 4f;

                for (int side = -1; side <= 1; side += 2)
                {
                    float[] reach = new float[n];
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 edgePt = fr[i].Pos + fr[i].Right * (side * edge);
                        float ty;
                        float far = trackGrid.Distance(edgePt, i, window, out ty);
                        reach[i] = Mathf.Clamp(far - clearOf, 0f, 30f);
                    }
                    // smooth the reach so the apron edge does not step
                    for (int pass = 0; pass < 3; pass++)
                    {
                        float[] next = new float[n];
                        for (int i = 0; i < n; i++)
                            next[i] = Mathf.Min(reach[i], (reach[(i - 1 + n) % n] + reach[(i + 1) % n]) * 0.5f + 1.5f);
                        reach = next;
                    }

                    int[] inner = new int[n + 1];
                    int[] outer = new int[n + 1];
                    for (int s = 0; s <= n; s++)
                    {
                        int i = s % n;
                        Frame f = fr[i];
                        Vector3 pi = f.Pos + f.Right * (side * edge) + Vector3.down * spec.VergeDrop;
                        Vector3 po = f.Pos + f.Right * (side * (edge + reach[i]))
                                   + Vector3.down * (spec.VergeDrop + 0.10f * reach[i]);
                        Vector3 nrm = Vector3.Cross(f.Fwd, po - pi).normalized;
                        if (nrm.y < 0f) nrm = -nrm;
                        inner[s] = apron.Add(pi, nrm, new Vector2(0f, f.S * 0.02f));
                        outer[s] = apron.Add(po, nrm, new Vector2(1f, f.S * 0.02f));
                    }
                    for (int s = 0; s < n; s++)
                    {
                        if (side < 0) apron.Quad(0, outer[s], outer[s + 1], inner[s + 1], inner[s]);
                        else apron.Quad(0, inner[s], inner[s + 1], outer[s + 1], outer[s]);
                    }
                }
            }
            Emit(holder.transform, "Terrain_Apron", apron, new[] { pal.Terrain }, false);

            // --- height field for everything beyond the apron
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            float sumY = 0f;
            for (int i = 0; i < fr.Length; i++)
            {
                Vector3 p = fr[i].Pos;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
                sumY += p.y;
            }
            float meanY = sumY / fr.Length;
            float margin = 700f;
            minX -= margin; maxX += margin; minZ -= margin; maxZ += margin;

            int cx = Mathf.Clamp(Mathf.RoundToInt((maxX - minX) / 34f), 24, 120);
            int cz = Mathf.Clamp(Mathf.RoundToInt((maxZ - minZ) / 34f), 24, 120);

            // subsample the centreline for the nearest-point query
            int stride = Mathf.Max(1, fr.Length / 320);
            List<Vector3> probe = new List<Vector3>();
            for (int i = 0; i < fr.Length; i += stride) probe.Add(fr[i].Pos);

            float relief = spec.StreetCircuit ? 0f : (pal.Relief);
            MeshBuilder field = new MeshBuilder(1);
            int[,] gv = new int[cx + 1, cz + 1];
            for (int ix = 0; ix <= cx; ix++)
            {
                float x = Mathf.Lerp(minX, maxX, ix / (float)cx);
                for (int iz = 0; iz <= cz; iz++)
                {
                    float z = Mathf.Lerp(minZ, maxZ, iz / (float)cz);
                    float best = float.MaxValue, bestY = meanY;
                    for (int k = 0; k < probe.Count; k++)
                    {
                        float dx = probe[k].x - x, dz = probe[k].z - z;
                        float d = dx * dx + dz * dz;
                        if (d < best) { best = d; bestY = probe[k].y; }
                    }
                    float dist = Mathf.Sqrt(best);
                    float blend = Mathf.Clamp01((dist - 40f) / 320f);
                    float y = Mathf.Lerp(bestY - spec.VergeDrop - 2.6f, meanY - 10f, blend);
                    if (relief > 0.01f)
                        y += relief * blend * (Mathf.PerlinNoise(x * 0.0011f + 13.7f, z * 0.0011f + 4.1f) - 0.5f) * 2f;
                    gv[ix, iz] = field.Add(new Vector3(x, y, z), Vector3.up, new Vector2(x * 0.004f, z * 0.004f));
                }
            }
            for (int ix = 0; ix < cx; ix++)
                for (int iz = 0; iz < cz; iz++)
                    field.Quad(0, gv[ix, iz], gv[ix, iz + 1], gv[ix + 1, iz + 1], gv[ix + 1, iz]);

            GameObject fieldGo = Emit(holder.transform, "Terrain_HeightField", field, new[] { pal.Terrain }, false);
            if (fieldGo != null) fieldGo.GetComponent<MeshFilter>().sharedMesh.RecalculateNormals();
        }

        // ------------------------------------------------------------------ racing surface

        private static void BuildTrackSurface(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            float hw = spec.HalfWidth;
            float kp = hw + spec.KerbPad;
            float ro = kp + spec.RunoffWidth;
            float vg = ro + spec.VergeWidth;
            const float line = 0.15f;

            // submeshes: 0 asphalt, 1 white line, 2 kerb pad, 3 run-off, 4 verge
            Prof[] prof = {
                new Prof(-vg, -spec.VergeDrop),
                new Prof(-ro, -0.30f),
                new Prof(-kp, -0.05f),
                new Prof(-hw,  0.00f),
                new Prof(-hw + line, 0.00f),
                new Prof( hw - line, 0.00f),
                new Prof( hw,  0.00f),
                new Prof( kp, -0.05f),
                new Prof( ro, -0.30f),
                new Prof( vg, -spec.VergeDrop)
            };
            int[] bands = { 4, 3, 2, 1, 0, 1, 2, 3, 4 };

            MeshBuilder mb = new MeshBuilder(5);
            Extrude(mb, fr, 0, fr.Length, true, prof, bands, 0.09f);
            Emit(parent, "Track_Surface", mb,
                new[] { pal.Asphalt, pal.WhiteLine, pal.KerbPad, pal.Runoff, pal.Verge }, false);

            // Separate, lighter mesh for physics: racing surface plus run-off only.
            MeshBuilder col = new MeshBuilder(1);
            Prof[] cp = { new Prof(-ro, -0.05f), new Prof(-hw, 0f), new Prof(hw, 0f), new Prof(ro, -0.05f) };
            Extrude(col, fr, 0, fr.Length, true, cp, new[] { 0, 0, 0 }, 0.05f);
            GameObject cgo = Emit(parent, "Track_Collision", col, new[] { pal.Asphalt }, true);
            if (cgo != null) cgo.GetComponent<MeshRenderer>().enabled = false;
        }

        /// <summary>
        /// Contiguous runs of real curvature, used for kerbs, braking boards and grandstand siting.
        /// </summary>
        private static List<int[]> FindCorners(Frame[] fr, float minDegPerMetre, float minTotalTurn)
        {
            int n = fr.Length;
            List<int[]> corners = new List<int[]>();
            bool[] used = new bool[n];

            for (int start = 0; start < n; start++)
            {
                if (used[start] || Mathf.Abs(fr[start].Curv) < minDegPerMetre) continue;
                float sign = Mathf.Sign(fr[start].Curv);

                int i0 = start;
                while (true)
                {
                    int prev = (i0 - 1 + n) % n;
                    if (used[prev] || Mathf.Sign(fr[prev].Curv) != sign ||
                        Mathf.Abs(fr[prev].Curv) < minDegPerMetre * 0.35f) break;
                    i0 = prev;
                    if (i0 == start) break;
                }

                int len = 0;
                float total = 0f;
                int i = i0;
                while (len < n)
                {
                    if (used[i] || Mathf.Sign(fr[i].Curv) != sign ||
                        Mathf.Abs(fr[i].Curv) < minDegPerMetre * 0.35f) break;
                    used[i] = true;
                    total += fr[i].Curv * StationSpacing;
                    len++;
                    i = (i + 1) % n;
                }

                if (len >= 3 && Mathf.Abs(total) >= minTotalTurn)
                    corners.Add(new[] { i0, len, (int)sign });
            }
            return corners;
        }

        /// <summary>
        /// Kerbs as continuous striped strips at real corners only. The previous build placed a
        /// 2.5 m block every other station wherever the turn exceeded one degree, which produced
        /// floating dashes with roughly 8 m gaps, all the way down the straights.
        /// </summary>
        private static void BuildKerbs(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            float hw = spec.HalfWidth;
            float w = Mathf.Min(1.05f, spec.KerbPad);
            const float rise = 0.07f;

            // radius below ~260 m is where F1 circuits actually lay kerbs
            List<int[]> corners = FindCorners(fr, 0.22f, 14f);
            if (corners.Count == 0) return;

            MeshBuilder mb = new MeshBuilder(2);
            Func<int, int, int> stripe = (idx, band) =>
                (Mathf.FloorToInt(fr[idx].S / 1.0f) % 2 == 0) ? 0 : 1;

            foreach (int[] c in corners)
            {
                int i0 = c[0], len = c[1], sign = c[2];
                int pad = 5;
                int from = (i0 - pad + fr.Length) % fr.Length;
                int count = Mathf.Min(fr.Length - 1, len + pad * 2);

                // apex-side kerb (wrap so a corner sitting on the lap join is not clipped)
                float s = sign;   // +1 = right-hand corner -> kerb on the right
                Prof[] apex = {
                    new Prof(s * hw, 0.005f),
                    new Prof(s * (hw + 0.22f), rise),
                    new Prof(s * (hw + w - 0.20f), rise),
                    new Prof(s * (hw + w), 0.005f)
                };
                Extrude(mb, fr, from, count, true, apex, new[] { 0, 0, 0 }, 1f, false, stripe, null);

                // exit kerb on the outside of the corner, over the second half only
                int exitFrom = (i0 + len / 2) % fr.Length;
                int exitCount = Mathf.Min(fr.Length - 1, Mathf.Max(4, len / 2 + pad));
                Prof[] exit = {
                    new Prof(-s * hw, 0.005f),
                    new Prof(-s * (hw + 0.22f), rise),
                    new Prof(-s * (hw + w - 0.20f), rise),
                    new Prof(-s * (hw + w), 0.005f)
                };
                Extrude(mb, fr, exitFrom, exitCount, true, exit, new[] { 0, 0, 0 }, 1f, false, stripe, null);
            }

            Emit(parent, "Circuit_Kerbs", mb, new[] { pal.KerbRed, pal.KerbWhite }, false);
        }

        private static void BuildTrackMarkings(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            MeshBuilder mb = new MeshBuilder(1);
            float hw = spec.HalfWidth;

            // 50 m distance ticks on both edges
            int tickStride = Mathf.Max(1, Mathf.RoundToInt(50f / StationSpacing));
            for (int i = 0; i < fr.Length; i += tickStride)
            {
                Frame f = fr[i];
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 p = f.Pos + f.Right * (side * (hw - 0.5f)) + Vector3.up * 0.012f;
                    AddBox(mb, 0, p, new Vector3(0.45f, 0.006f, 0.09f), Quaternion.LookRotation(f.Fwd, Vector3.up));
                }
            }
            Emit(parent, "Track_Markings", mb, new[] { pal.WhiteLine }, false);
        }

        // ------------------------------------------------------------------ barriers

        /// <summary>
        /// One continuous Armco run with posts and debris fencing, as a single mesh with a single
        /// collider. Previously this was a 5.8 m cube every sixth station, i.e. a dotted line of
        /// floating slabs with ~26 m gaps, each carrying its own box collider.
        /// </summary>
        private static void BuildBarriers(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            float edge = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap;
            bool fence = !spec.StreetCircuit;

            // On the inside of a corner the offset curve folds if the offset exceeds the radius, so
            // clamp it per station. This is what keeps Monaco's hairpin barriers from self-crossing.
            // The floor is the track edge itself: a barrier is pulled in, never onto the circuit.
            float minOffset = spec.HalfWidth + spec.KerbPad + 0.6f;
            Func<int, float> shrink = idx =>
            {
                float r = LocalRadius(fr[idx]);
                float allowed = Mathf.Max(minOffset, 0.82f * r);
                return Mathf.Min(1f, allowed / edge);
            };

            MeshBuilder mb = new MeshBuilder(3);   // 0 armco, 1 sponsor, 2 post/fence
            // band 0 is the concrete footing (always structural); band 1 is the Armco panel, which
            // carries a sponsor livery every third 14 m block.
            Func<int, int, int> panel = (idx, band) =>
                band == 0 ? 2 : ((Mathf.FloorToInt(fr[idx].S / 14f) % 3 == 0) ? 1 : 0);

            for (int side = -1; side <= 1; side += 2)
            {
                float off = side * edge;
                Prof[] wall = {
                    new Prof(off, -0.10f),
                    new Prof(off, 0.32f),
                    new Prof(off, 1.06f)
                };
                Extrude(mb, fr, 0, fr.Length, true, wall, new[] { 2, 0 }, 0.10f, true, panel, shrink);

                if (fence)
                {
                    Prof[] mesh = { new Prof(off, 1.06f), new Prof(off, 3.70f) };
                    Extrude(mb, fr, 0, fr.Length, true, mesh, new[] { 2 }, 0.10f, true, null, shrink);
                }

                for (int i = 0; i < fr.Length; i++)
                {
                    Frame f = fr[i];
                    float sc = shrink(i);
                    Vector3 p = f.Pos + f.Right * (off * sc);
                    AddBox(mb, 2, p + Vector3.up * (fence ? 1.85f : 0.55f),
                        new Vector3(0.07f, fence ? 1.95f : 0.65f, 0.07f),
                        Quaternion.LookRotation(f.Fwd, Vector3.up));
                }
            }

            GameObject go = Emit(parent, "Circuit_Barriers", mb,
                new[] { pal.Armco, pal.Sponsor, pal.Gantry }, false);

            // dedicated low-poly collision wall
            MeshBuilder cb = new MeshBuilder(1);
            for (int side = -1; side <= 1; side += 2)
            {
                float off = side * edge;
                Prof[] wall = { new Prof(off, 0f), new Prof(off, 1.10f) };
                Extrude(cb, fr, 0, fr.Length, true, wall, new[] { 0 }, 0.1f, true, null, shrink);
            }
            GameObject cgo = Emit(parent, "Barrier_Collision", cb, new[] { pal.Armco }, true);
            if (cgo != null) cgo.GetComponent<MeshRenderer>().enabled = false;
        }

        // ------------------------------------------------------------------ start line, grid, pits

        private static void BuildStartLineAndGrid(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Circuit_Starting_Grid");
            holder.transform.SetParent(parent, false);

            float hw = spec.HalfWidth;
            MeshBuilder paint = new MeshBuilder(2);   // 0 white, 1 yellow

            // start/finish line
            {
                Frame f = fr[StartFinishIndex];
                AddBox(paint, 0, f.Pos + Vector3.up * 0.014f,
                    new Vector3(hw, 0.007f, 0.30f), Quaternion.LookRotation(f.Fwd, Vector3.up));
            }

            // 20 staggered grid slots running back from the line, following the actual road surface
            for (int i = 0; i < GridSlotCount; i++)
            {
                float back = GridFirstSlotBack + i * GridSlotPitch;
                int idx = ((StartFinishIndex - Mathf.RoundToInt(back / StationSpacing)) % fr.Length + fr.Length) % fr.Length;
                Frame f = fr[idx];
                float lateral = (i % 2 == 0 ? 1f : -1f) * hw * GridLateralFraction;
                Vector3 c = f.Pos + f.Right * lateral + Vector3.up * 0.014f;
                Quaternion rot = Quaternion.LookRotation(f.Fwd, Vector3.up);

                // an F1 grid box: a transverse line at the front and two rails along it
                AddBox(paint, 0, c + f.Fwd * 2.1f, new Vector3(1.05f, 0.006f, 0.09f), rot);
                AddBox(paint, 0, c + f.Right * 1.0f, new Vector3(0.09f, 0.006f, 2.1f), rot);
                AddBox(paint, 0, c - f.Right * 1.0f, new Vector3(0.09f, 0.006f, 2.1f), rot);
            }

            // yellow line across the pit lane exit
            {
                int idx = IndexAtU(fr, spec.PitU1);
                Frame f = fr[idx];
                float laneCentre = spec.HalfWidth + 1.6f + 1.0f + 4.75f;
                AddBox(paint, 1, f.Pos + f.Right * (spec.PitSide * laneCentre) + Vector3.up * 0.014f,
                    new Vector3(4.75f, 0.006f, 0.12f), Quaternion.LookRotation(f.Fwd, Vector3.up));
            }
            Emit(holder.transform, "Grid_Markings", paint, new[] { pal.WhiteLine, pal.YellowLine }, false);

            // FIA start light gantry
            MeshBuilder g = new MeshBuilder(2);   // 0 steel, 1 lamp
            Frame sf = fr[StartFinishIndex];
            Quaternion grot = Quaternion.LookRotation(sf.Fwd, Vector3.up);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 footing = sf.Pos + sf.Right * (side * (hw + 2.6f));
                AddTaperedCylinder(g, 0, footing, 0.34f, 0.26f, 8.4f, 10);
            }
            AddBox(g, 0, sf.Pos + Vector3.up * 8.1f, new Vector3(hw + 3.2f, 0.42f, 0.55f), grot);
            for (int k = 0; k < 5; k++)
            {
                float x = -3.2f + k * 1.6f;
                Vector3 pod = sf.Pos + sf.Right * x + Vector3.up * 7.1f - sf.Fwd * 0.42f;
                AddBox(g, 0, pod, new Vector3(0.44f, 0.66f, 0.18f), grot);
                AddBox(g, 1, pod - sf.Fwd * 0.20f + Vector3.up * 0.24f, new Vector3(0.24f, 0.24f, 0.06f), grot);
                AddBox(g, 1, pod - sf.Fwd * 0.20f - Vector3.up * 0.24f, new Vector3(0.24f, 0.24f, 0.06f), grot);
            }
            Emit(holder.transform, "FIA_Start_Gantry", g, new[] { pal.Gantry, pal.StartLight }, false);
        }

        /// <summary>
        /// Pit lane, pit wall and garages built as ribbons that follow the start/finish straight,
        /// so they stay parallel to the track instead of being one long stretched box.
        /// </summary>
        private static void BuildPitComplex(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Circuit_Pit_Complex");
            holder.transform.SetParent(parent, false);

            int i0 = IndexAtU(fr, spec.PitU0);
            int count = Mathf.RoundToInt(Mathf.Repeat(spec.PitU1 - spec.PitU0, 1f) * fr.Length);
            if (count < 8) return;

            float side = spec.PitSide;
            float hw = spec.HalfWidth;
            float wall = hw + 1.6f;
            float lane = wall + 1.0f;
            float laneOuter = lane + 9.5f;
            float garage = laneOuter + 1.0f;

            MeshBuilder mb = new MeshBuilder(4);   // 0 concrete, 1 pit asphalt, 2 building, 3 glass/roof

            // pit wall: vertical panel plus a capped top
            Prof[] wallProf = { new Prof(side * wall, 0f), new Prof(side * wall, 1.05f) };
            Extrude(mb, fr, i0, count, true, wallProf, new[] { 0 }, 0.1f, true, null, null);
            Prof[] wallCap = { new Prof(side * (wall - 0.18f), 1.05f), new Prof(side * (wall + 0.18f), 1.05f) };
            Extrude(mb, fr, i0, count, true, wallCap, new[] { 0 }, 0.1f);

            // pit lane surface
            Prof[] laneProf = { new Prof(side * lane, -0.02f), new Prof(side * laneOuter, -0.02f) };
            Extrude(mb, fr, i0, count, true, laneProf, new[] { 1 }, 0.08f);

            // garage frontage and roof
            Prof[] front = { new Prof(side * garage, 0f), new Prof(side * garage, 5.2f) };
            Extrude(mb, fr, i0, count, true, front, new[] { 2 }, 0.1f, true, null, null);
            Prof[] roof = { new Prof(side * garage, 5.2f), new Prof(side * (garage + 16f), 5.6f) };
            Extrude(mb, fr, i0, count, true, roof, new[] { 3 }, 0.05f);
            Prof[] back = { new Prof(side * (garage + 16f), 0f), new Prof(side * (garage + 16f), 5.6f) };
            Extrude(mb, fr, i0, count, true, back, new[] { 2 }, 0.1f, true, null, null);

            // upper hospitality / paddock club storey set back from the garages
            Prof[] upper = { new Prof(side * (garage + 2f), 5.6f), new Prof(side * (garage + 2f), 11.4f) };
            Extrude(mb, fr, i0, count, true, upper, new[] { 3 }, 0.1f, true, null, null);
            Prof[] upperRoof = { new Prof(side * (garage + 2f), 11.4f), new Prof(side * (garage + 15f), 11.4f) };
            Extrude(mb, fr, i0, count, true, upperRoof, new[] { 0 }, 0.05f);

            // pit boxes down the length of the pit lane
            int boxStride = Mathf.Max(1, Mathf.RoundToInt(20f / StationSpacing));
            for (int s = 0; s < count; s += boxStride)
            {
                int idx = (i0 + s) % fr.Length;
                Frame f = fr[idx];
                Quaternion rot = Quaternion.LookRotation(f.Fwd, Vector3.up);
                AddBox(mb, 0, f.Pos + f.Right * (side * (wall + 0.9f)) + Vector3.up * 1.9f,
                    new Vector3(0.9f, 0.85f, 1.6f), rot);
            }

            Emit(holder.transform, "Pit_Lane_And_Garages", mb,
                new[] { pal.PitWall, pal.PitAsphalt, pal.PitBuilding, pal.GrandstandRoof }, false);

            // main grandstand opposite the pits
            BuildGrandstand(holder.transform, fr, spec, pal, spec.PitU0, spec.PitU1, -side,
                14f, 16, 0.55f, 0.85f, true, "Main_Grandstand");
        }

        /// <summary>
        /// Tiered grandstand that follows the track's curve and elevation over a u range, rather
        /// than a single stretched cube.
        /// </summary>
        private static void BuildGrandstand(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal,
                                            float u0, float u1, float side, float standOff,
                                            int rows, float rise, float depth, bool roof, string name)
        {
            int i0 = IndexAtU(fr, u0);
            int count = Mathf.RoundToInt(Mathf.Repeat(u1 - u0, 1f) * fr.Length);
            if (count < 6) return;

            float edge = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + standOff;

            // Trim the tier count to whatever room this stretch actually has before the structure
            // would reach a different part of the circuit. Bahrain's tightest gap between distant
            // parts of the lap is 64 m, and a full-depth stand plus run-off would otherwise cross it.
            {
                TrackGrid probe = new TrackGrid(fr);
                int window = Mathf.RoundToInt(140f / StationSpacing);
                float need = spec.HalfWidth + spec.KerbPad + 6f;
                float room = float.MaxValue;
                for (int s = 0; s < count; s += 2)
                {
                    int idx = (i0 + s) % fr.Length;
                    Vector3 basePoint = fr[idx].Pos + fr[idx].Right * (side * edge);
                    float ty;
                    float far = probe.Distance(basePoint, idx, window, out ty);
                    if (far - need < room) room = far - need;
                }
                if (room < float.MaxValue)
                {
                    int maxRows = Mathf.FloorToInt(Mathf.Max(0f, room - 3.5f) / depth);
                    if (maxRows < 3) return;                       // no space for a stand here at all
                    rows = Mathf.Min(rows, maxRows);
                }
            }

            List<Prof> prof = new List<Prof>();
            List<int> bands = new List<int>();
            prof.Add(new Prof(side * edge, -0.4f));
            prof.Add(new Prof(side * edge, 1.2f));
            bands.Add(0);                                       // front wall
            for (int r = 0; r < rows; r++)
            {
                float lat = edge + r * depth;
                float y = 1.2f + r * rise;
                prof.Add(new Prof(side * (lat + depth), y));     // tread
                bands.Add(1);
                prof.Add(new Prof(side * (lat + depth), y + rise));  // riser
                bands.Add(1);
            }
            float backLat = edge + rows * depth;
            float backY = 1.2f + rows * rise;

            MeshBuilder mb = new MeshBuilder(3);   // 0 concrete, 1 seating, 2 roof
            Extrude(mb, fr, i0, count, true, prof.ToArray(), bands.ToArray(), 0.06f, true, null, null);

            Prof[] backWall = { new Prof(side * (backLat + 1.5f), -0.4f), new Prof(side * (backLat + 1.5f), backY) };
            Extrude(mb, fr, i0, count, true, backWall, new[] { 0 }, 0.1f, true, null, null);

            if (roof)
            {
                float rh = backY + 7.5f;
                Prof[] canopy = { new Prof(side * (edge - 3f), rh - 2.6f), new Prof(side * (backLat + 2f), rh) };
                Extrude(mb, fr, i0, count, true, canopy, new[] { 2 }, 0.05f, true, null, null);
                int pillarStride = Mathf.Max(1, Mathf.RoundToInt(22f / StationSpacing));
                for (int s = 0; s < count; s += pillarStride)
                {
                    int idx = (i0 + s) % fr.Length;
                    Frame f = fr[idx];
                    Vector3 basePos = f.Pos + f.Right * (side * (backLat + 1.8f));
                    AddTaperedCylinder(mb, 0, basePos, 0.28f, 0.24f, rh, 8);
                }
            }

            Emit(parent, name, mb, new[] { pal.PitWall, pal.Grandstand, pal.GrandstandRoof }, false);
        }

        // ------------------------------------------------------------------ signage

        private static void BuildBrakingMarkersAndMarshalPosts(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Circuit_Signage");
            holder.transform.SetParent(parent, false);

            float edge = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth;
            MeshBuilder boards = new MeshBuilder(2);   // 0 board, 1 numerals

            // braking boards at the real heavy-braking corners: radius under ~110 m
            List<int[]> corners = FindCorners(fr, 0.52f, 40f);
            foreach (int[] c in corners)
            {
                int apex = c[0];
                for (int d = 3; d >= 1; d--)
                {
                    int back = Mathf.RoundToInt(d * 50f / StationSpacing);
                    int idx = (apex - back + fr.Length * 2) % fr.Length;
                    Frame f = fr[idx];
                    float lat = Mathf.Min(edge * 0.55f, spec.HalfWidth + 3.2f);
                    Quaternion rot = Quaternion.LookRotation(f.Fwd, Vector3.up);
                    Vector3 p = f.Pos - f.Right * lat;
                    AddTaperedCylinder(boards, 0, p, 0.09f, 0.09f, 1.05f, 6);
                    Vector3 face = p + Vector3.up * 1.55f;
                    AddBox(boards, 0, face, new Vector3(0.05f, 0.52f, 0.72f), rot);
                    for (int k = 0; k < d; k++)
                        AddBox(boards, 1, face + Vector3.up * (0.30f - k * 0.28f) - f.Right * 0.06f,
                            new Vector3(0.02f, 0.09f, 0.46f), rot);
                }
            }
            Emit(holder.transform, "Braking_Markers", boards, new[] { pal.BrakeBoard, pal.WhiteLine }, false);

            // marshal posts with LED flag panels, spaced along the lap on the spectator side
            MeshBuilder posts = new MeshBuilder(3);   // 0 cabin, 1 steel, 2 LED
            float lap = LapLength(fr);
            int stationCount = Mathf.Max(8, Mathf.RoundToInt(lap / 300f));
            for (int m = 0; m < stationCount; m++)
            {
                int idx = Mathf.RoundToInt(m / (float)stationCount * fr.Length) % fr.Length;
                Frame f = fr[idx];
                float outward = edge + spec.BarrierGap + 4.5f;
                Vector3 p = f.Pos + f.Right * outward;
                Quaternion rot = Quaternion.LookRotation(-f.Right, Vector3.up);
                AddBox(posts, 0, p + Vector3.up * 1.35f, new Vector3(1.30f, 1.35f, 1.20f), rot);
                AddBox(posts, 1, p + Vector3.up * 2.80f, new Vector3(1.45f, 0.10f, 1.35f), rot);

                Vector3 led = f.Pos - f.Right * Mathf.Min(edge * 0.5f, spec.HalfWidth + 2.4f) + Vector3.up * 1.65f;
                AddBox(posts, 1, led - Vector3.up * 0.85f, new Vector3(0.08f, 0.85f, 0.08f),
                    Quaternion.LookRotation(f.Fwd, Vector3.up));
                AddBox(posts, 2, led, new Vector3(0.06f, 0.42f, 0.72f), Quaternion.LookRotation(f.Fwd, Vector3.up));
            }
            Emit(holder.transform, "Marshal_Posts", posts,
                new[] { pal.MarshalPost, pal.Gantry, pal.FlagLed }, false);
        }

        // ------------------------------------------------------------------ prop siting helpers

        /// <summary>
        /// Accumulates props into bounded meshes instead of one enormous one, so frustum culling
        /// can discard the far side of the circuit. Replaces the previous approach of spawning a
        /// GameObject per trunk and per foliage cone (over 2000 objects at Spa).
        /// </summary>
        private sealed class PropBatcher
        {
            private readonly Transform parent;
            private readonly string baseName;
            private readonly Material[] mats;
            private readonly int subMeshes;
            private readonly int maxVerts;
            private MeshBuilder current;
            private int index;

            public PropBatcher(Transform parent, string baseName, Material[] mats, int maxVerts)
            {
                this.parent = parent;
                this.baseName = baseName;
                this.mats = mats;
                this.subMeshes = mats.Length;
                this.maxVerts = maxVerts;
            }

            public MeshBuilder MB
            {
                get
                {
                    if (current == null) current = new MeshBuilder(subMeshes);
                    return current;
                }
            }

            /// <summary>Call after finishing one prop; starts a new mesh once this one is full.</summary>
            public void EndProp()
            {
                if (current != null && current.VertexCount >= maxVerts) Flush();
            }

            public void Flush()
            {
                if (current == null) return;
                Emit(parent, baseName + "_" + index.ToString("00"), current, mats, false);
                current = null;
                index++;
            }
        }

        /// <summary>
        /// Uniform grid over the centreline so scenery can be rejected near the track in O(1).
        /// The previous forest builders either scanned the whole centreline per candidate or, at Spa,
        /// skipped the test entirely and grew pine trees on the racing line.
        /// </summary>
        private sealed class TrackGrid
        {
            private const float Cell = 40f;
            private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
            private readonly Frame[] frames;

            public TrackGrid(Frame[] fr)
            {
                frames = fr;
                for (int i = 0; i < fr.Length; i++)
                {
                    long key = Key(fr[i].Pos.x, fr[i].Pos.z);
                    List<int> list;
                    if (!cells.TryGetValue(key, out list)) { list = new List<int>(); cells[key] = list; }
                    list.Add(i);
                }
            }

            private static long Key(float x, float z)
            {
                long ix = (long)Mathf.Floor(x / Cell);
                long iz = (long)Mathf.Floor(z / Cell);
                return (ix << 32) ^ (iz & 0xffffffffL);
            }

            /// <summary>Horizontal distance to the nearest centreline sample (searched over 5 cells).</summary>
            public float Distance(Vector3 p, out float trackY)
            {
                return Distance(p, -1, 0, out trackY);
            }

            /// <summary>
            /// As Distance, but ignoring stations within <paramref name="window"/> of
            /// <paramref name="aroundIdx"/>. Used to measure how much room a trackside structure has
            /// before it would reach a different part of the circuit.
            /// </summary>
            public float Distance(Vector3 p, int aroundIdx, int window, out float trackY)
            {
                float best = float.MaxValue;
                trackY = p.y;
                int n = frames.Length;
                long ix = (long)Mathf.Floor(p.x / Cell);
                long iz = (long)Mathf.Floor(p.z / Cell);
                for (long dx = -2; dx <= 2; dx++)
                {
                    for (long dz = -2; dz <= 2; dz++)
                    {
                        List<int> list;
                        if (!cells.TryGetValue(((ix + dx) << 32) ^ ((iz + dz) & 0xffffffffL), out list)) continue;
                        for (int k = 0; k < list.Count; k++)
                        {
                            int idx = list[k];
                            if (aroundIdx >= 0)
                            {
                                int gap = Mathf.Abs(idx - aroundIdx);
                                if (Mathf.Min(gap, n - gap) <= window) continue;
                            }
                            Vector3 q = frames[idx].Pos;
                            float a = q.x - p.x, b = q.z - p.z;
                            float d = a * a + b * b;
                            if (d < best) { best = d; trackY = q.y; }
                        }
                    }
                }
                return best == float.MaxValue ? 1e5f : Mathf.Sqrt(best);
            }
        }

        private static bool PointInsideLoop(Frame[] fr, float x, float z)
        {
            bool inside = false;
            int n = fr.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = fr[i].Pos.x, zi = fr[i].Pos.z;
                float xj = fr[j].Pos.x, zj = fr[j].Pos.z;
                if (((zi > z) != (zj > z)) && (x < (xj - xi) * (z - zi) / (zj - zi) + xi)) inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Finds the largest open area enclosed by the circuit: the point inside the loop that is
        /// furthest from any part of the track, and how far that is. Used to site Monaco's harbour
        /// basin, which the previous build placed at fixed world coordinates that sat on the track.
        /// The u window restricts the search to areas bordered by a particular stretch of the lap,
        /// so the harbour lands against the quay rather than in the Casino loop.
        /// </summary>
        private static Vector3 LargestEnclosedOpening(Frame[] fr, float uFrom, float uTo, out float radius)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < fr.Length; i++)
            {
                Vector3 p = fr[i].Pos;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            int cx = Mathf.Clamp(Mathf.RoundToInt((maxX - minX) / 14f), 8, 90);
            int cz = Mathf.Clamp(Mathf.RoundToInt((maxZ - minZ) / 14f), 8, 90);

            Vector3 best = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            radius = 0f;
            bool windowed = uFrom >= 0f;
            for (int ix = 1; ix < cx; ix++)
            {
                float x = Mathf.Lerp(minX, maxX, ix / (float)cx);
                for (int iz = 1; iz < cz; iz++)
                {
                    float z = Mathf.Lerp(minZ, maxZ, iz / (float)cz);
                    if (!PointInsideLoop(fr, x, z)) continue;

                    float bestSq = float.MaxValue;
                    int nearest = 0;
                    for (int i = 0; i < fr.Length; i++)
                    {
                        float dx = fr[i].Pos.x - x, dz = fr[i].Pos.z - z;
                        float d = dx * dx + dz * dz;
                        if (d < bestSq) { bestSq = d; nearest = i; }
                    }
                    if (windowed)
                    {
                        float u = fr[nearest].U;
                        bool inWindow = (uFrom <= uTo) ? (u >= uFrom && u <= uTo) : (u >= uFrom || u <= uTo);
                        if (!inWindow) continue;
                    }
                    float r = Mathf.Sqrt(bestSq);
                    if (r > radius) { radius = r; best = new Vector3(x, 0f, z); }
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ Bahrain

        private static void BuildBahrain(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Bahrain_Trackside");
            holder.transform.SetParent(parent, false);
            TrackGrid grid = new TrackGrid(fr);

            // Grandstands at the corners that actually have them at Sakhir
            BuildGrandstand(holder.transform, fr, spec, pal, 0.185f, 0.240f, -1f, 10f, 13, 0.52f, 0.82f, true, "Turn1_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.315f, 0.350f, 1f, 10f, 11, 0.52f, 0.82f, true, "Turn4_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.540f, 0.585f, 1f, 10f, 11, 0.52f, 0.82f, true, "Turn10_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.790f, 0.830f, -1f, 10f, 10, 0.52f, 0.82f, false, "Turn13_Grandstand");

            // Sakhir Tower, inside Turn 1
            {
                Frame f = fr[IndexAtU(fr, 0.212f)];
                Vector3 basePos = f.Pos + f.Right * 95f + f.Fwd * 30f;
                basePos.y = f.Pos.y;
                MeshBuilder t = new MeshBuilder(3);   // 0 structure, 1 glazing, 2 trim
                AddTaperedCylinder(t, 0, basePos, 13.5f, 11.5f, 3.0f, 20);
                for (int floor = 0; floor < 9; floor++)
                {
                    float y = 3.0f + floor * 4.6f;
                    float r = 12.5f - floor * 0.55f;
                    AddTaperedCylinder(t, floor % 2 == 0 ? 1 : 0, basePos + Vector3.up * y, r, r, 3.4f, 20);
                    AddTaperedCylinder(t, 2, basePos + Vector3.up * (y + 3.4f), r + 1.1f, r + 1.1f, 0.5f, 20);
                }
                AddTaperedCylinder(t, 2, basePos + Vector3.up * 45f, 8.5f, 3.0f, 9f, 16);
                Emit(holder.transform, "Sakhir_VIP_Tower", t,
                    new[] { pal.PitBuilding, pal.Grandstand, pal.RolexGold }, false);
            }

            // Night-race floodlight masts around the whole lap
            if (spec.NightRace)
            {
                MeshBuilder m = new MeshBuilder(2);   // 0 steel, 1 lamp
                float lap = LapLength(fr);
                int masts = Mathf.RoundToInt(lap / 130f);
                float edge = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + 14f;
                for (int i = 0; i < masts; i++)
                {
                    int idx = Mathf.RoundToInt(i / (float)masts * fr.Length) % fr.Length;
                    Frame f = fr[idx];
                    float side = (i % 2 == 0) ? 1f : -1f;
                    Vector3 p = f.Pos + f.Right * (side * edge);
                    AddTaperedCylinder(m, 0, p, 0.62f, 0.34f, 26f, 10);
                    Quaternion rot = Quaternion.LookRotation(f.Fwd, Vector3.up);
                    AddBox(m, 0, p + Vector3.up * 26.6f, new Vector3(3.1f, 0.28f, 0.9f), rot);
                    for (int k = -2; k <= 2; k++)
                        AddBox(m, 1, p + Vector3.up * 26.9f + f.Right * (-side * 0.55f) +
                            Quaternion.LookRotation(f.Fwd, Vector3.up) * new Vector3(k * 1.15f, 0f, 0f),
                            new Vector3(0.46f, 0.30f, 0.16f), rot);
                }
                Emit(holder.transform, "Sakhir_Floodlights", m, new[] { pal.Floodlight, pal.LightEmissive }, false);
            }

            // Date palms in the desert, never inside the run-off
            {
                PropBatcher batch = new PropBatcher(holder.transform, "Sakhir_Date_Palms",
                    new[] { pal.PalmTrunk, pal.PalmLeaf }, 36000);
                float clear = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + 18f;
                int placed = 0;
                for (int i = 0; i < fr.Length && placed < 320; i += 6)
                {
                    Frame f = fr[i];
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int band = 0; band < 2; band++)
                        {
                            float lat = clear + 14f + band * 26f + UnityEngine.Random.Range(-7f, 9f);
                            Vector3 pos = f.Pos + f.Right * (side * lat) + f.Fwd * UnityEngine.Random.Range(-14f, 14f);
                            float ty;
                            if (grid.Distance(pos, out ty) < clear + 8f) continue;
                            pos.y = ty - spec.VergeDrop - 1.6f;
                            float h = UnityEngine.Random.Range(5.5f, 9.5f);
                            AddTaperedCylinder(batch.MB, 0, pos, 0.42f, 0.30f, h, 6);
                            Vector3 crown = pos + Vector3.up * h;
                            for (int k = 0; k < 6; k++)
                            {
                                float a = k / 6f * Mathf.PI * 2f;
                                Vector3 tip = crown + Vector3.up * 0.6f +
                                    new Vector3(Mathf.Cos(a), -0.30f, Mathf.Sin(a)) * 3.1f;
                                AddBox(batch.MB, 1, (crown + tip) * 0.5f,
                                    new Vector3(0.30f, 0.09f, 1.75f),
                                    Quaternion.LookRotation(tip - crown, Vector3.up));
                            }
                            batch.EndProp();
                            placed++;
                        }
                    }
                }
                batch.Flush();
            }

            BuildSponsorBridges(holder.transform, fr, spec, pal, new[] { 0.10f, 0.46f, 0.74f });
        }

        // ------------------------------------------------------------------ Spa-Francorchamps

        private static void BuildSpa(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Spa_Trackside");
            holder.transform.SetParent(parent, false);
            TrackGrid grid = new TrackGrid(fr);

            // The stands that define Spa
            BuildGrandstand(holder.transform, fr, spec, pal, 0.050f, 0.082f, 1f, 8f, 14, 0.55f, 0.85f, true, "LaSource_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.118f, 0.150f, 1f, 10f, 18, 0.62f, 0.85f, false, "EauRouge_Hillside_Stand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.140f, 0.190f, -1f, 12f, 20, 0.68f, 0.85f, false, "Raidillon_Hillside_Stand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.348f, 0.392f, -1f, 10f, 15, 0.55f, 0.85f, true, "LesCombes_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.505f, 0.545f, 1f, 12f, 13, 0.55f, 0.85f, false, "Pouhon_Spectator_Bank");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.965f, 0.995f, -1f, 8f, 12, 0.55f, 0.85f, true, "BusStop_Grandstand");

            // Kemmel hospitality village at Les Combes
            {
                MeshBuilder v = new MeshBuilder(2);
                float edge = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + 34f;
                for (int t = 0; t < 7; t++)
                {
                    int idx = (IndexAtU(fr, 0.352f) + t * 5) % fr.Length;
                    Frame g = fr[idx];
                    Vector3 pos = g.Pos + g.Right * edge;
                    Quaternion rot = Quaternion.LookRotation(g.Fwd, Vector3.up);
                    AddBox(v, t % 2, pos + Vector3.up * 2.4f, new Vector3(9f, 2.4f, 7f), rot);
                    AddBox(v, 1, pos + Vector3.up * 5.1f, new Vector3(9.6f, 0.22f, 7.6f), rot);
                }
                Emit(holder.transform, "Kemmel_Hospitality", v, new[] { pal.TeamRed, pal.TentFabric }, false);
            }

            // Ardennes pine forest, three depth bands, with genuine track clearance
            {
                PropBatcher batch = new PropBatcher(holder.transform, "Ardennes_Pine_Forest",
                    new[] { pal.TreeTrunk, pal.PineLeaf }, 36000);
                float clear = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + 8f;
                int placed = 0;
                for (int i = 0; i < fr.Length && placed < 900; i += 4)
                {
                    Frame f = fr[i];
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int band = 0; band < 3; band++)
                        {
                            float lat = clear + 10f + band * 20f + UnityEngine.Random.Range(-6f, 8f);
                            Vector3 pos = f.Pos + f.Right * (side * lat) + f.Fwd * UnityEngine.Random.Range(-9f, 9f);
                            float ty;
                            if (grid.Distance(pos, out ty) < clear + 4f) continue;
                            pos.y = ty - spec.VergeDrop - 2.2f;
                            float sc = UnityEngine.Random.Range(0.85f, 1.5f);
                            AddTaperedCylinder(batch.MB, 0, pos, 0.42f * sc, 0.24f * sc, 5.0f * sc, 5);
                            for (int c = 0; c < 3; c++)
                                AddTaperedCylinder(batch.MB, 1, pos + Vector3.up * ((4.4f + c * 3.2f) * sc),
                                    (3.5f - c * 0.9f) * sc, (2.2f - c * 0.7f) * sc, 4.4f * sc, 6);
                            batch.EndProp();
                            placed++;
                        }
                    }
                }
                batch.Flush();
            }

            BuildSponsorBridges(holder.transform, fr, spec, pal, new[] { 0.215f, 0.300f, 0.905f });
        }

        // ------------------------------------------------------------------ Monza

        private static void BuildMonza(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Monza_Trackside");
            holder.transform.SetParent(parent, false);
            TrackGrid grid = new TrackGrid(fr);

            BuildGrandstand(holder.transform, fr, spec, pal, 0.095f, 0.145f, -1f, 10f, 16, 0.55f, 0.85f, true, "Rettifilo_Tribune");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.175f, 0.215f, -1f, 12f, 12, 0.55f, 0.85f, false, "CurvaGrande_Stand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.295f, 0.325f, 1f, 10f, 13, 0.55f, 0.85f, true, "Roggia_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.350f, 0.385f, -1f, 10f, 12, 0.55f, 0.85f, false, "Lesmo_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.630f, 0.680f, 1f, 12f, 14, 0.55f, 0.85f, true, "Ascari_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.820f, 0.885f, -1f, 14f, 18, 0.58f, 0.85f, true, "Parabolica_Grandstand");

            // The 1955 Pista di Alta Velocita banking still stands in the park and the modern
            // circuit runs beneath it at Curva del Serraglio. Built as a real banked concrete arc.
            {
                Frame f = fr[IndexAtU(fr, 0.520f)];
                MeshBuilder b = new MeshBuilder(2);   // 0 concrete, 1 weathered surface
                Vector3 axis = Vector3.Cross(Vector3.up, f.Fwd).normalized;
                int spanSegs = 26;
                float half = 78f;
                Vector3[] lowIn = new Vector3[spanSegs + 1];
                Vector3[] lowOut = new Vector3[spanSegs + 1];
                Vector3[] topIn = new Vector3[spanSegs + 1];
                Vector3[] topOut = new Vector3[spanSegs + 1];
                for (int s = 0; s <= spanSegs; s++)
                {
                    float t = s / (float)spanSegs;
                    float lateral = Mathf.Lerp(-half, half, t);
                    // parabolic ramp: ground level at the ends, 9.5 m over the track, banked outward
                    float lift = Mathf.Max(0f, 9.5f * (1f - Mathf.Pow(lateral / half, 2f) * 1.02f));
                    float bank = lift * 0.62f;
                    Vector3 c = f.Pos + axis * lateral + Vector3.up * lift;
                    lowIn[s] = c - f.Fwd * 9f;
                    lowOut[s] = c + f.Fwd * 9f + Vector3.up * bank;
                    topIn[s] = lowIn[s] + Vector3.up * 1.1f;
                    topOut[s] = lowOut[s] + Vector3.up * 1.1f;
                }
                for (int s = 0; s < spanSegs; s++)
                {
                    Vector3 n = Vector3.Cross(topOut[s] - topIn[s], topIn[s + 1] - topIn[s]).normalized;
                    if (n.y < 0f) n = -n;
                    int a = b.Add(topIn[s], n, new Vector2(0f, s * 0.1f));
                    int c2 = b.Add(topOut[s], n, new Vector2(1f, s * 0.1f));
                    int d = b.Add(topOut[s + 1], n, new Vector2(1f, (s + 1) * 0.1f));
                    int e = b.Add(topIn[s + 1], n, new Vector2(0f, (s + 1) * 0.1f));
                    b.Quad(1, a, c2, d, e);

                    Vector3 no = (lowOut[s] - lowIn[s]).normalized;
                    int f0 = b.Add(lowOut[s], no, Vector2.zero);
                    int f1 = b.Add(topOut[s], no, Vector2.up);
                    int f2 = b.Add(topOut[s + 1], no, Vector2.one);
                    int f3 = b.Add(lowOut[s + 1], no, Vector2.right);
                    b.Quad(0, f0, f1, f2, f3);
                    b.Quad(0, f3, f2, f1, f0);
                }
                // support piers clear of the racing surface
                float clearSpan = spec.HalfWidth + spec.KerbPad + 6f;
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float lateral = side * (clearSpan + 6f + k * 22f);
                        float lift = Mathf.Max(0f, 9.5f * (1f - Mathf.Pow(lateral / half, 2f) * 1.02f));
                        Vector3 c = f.Pos + axis * lateral;
                        AddBox(b, 0, c + Vector3.up * lift * 0.5f, new Vector3(2.0f, lift * 0.5f, 10f),
                            Quaternion.LookRotation(f.Fwd, Vector3.up));
                    }
                }
                Emit(holder.transform, "Historic_Banked_Oval_Overpass", b,
                    new[] { pal.HistoricOval, pal.PitWall }, false);
            }

            // Parco di Monza woodland
            {
                PropBatcher batch = new PropBatcher(holder.transform, "Royal_Park_Woodland",
                    new[] { pal.TreeTrunk, pal.OakLeaf }, 36000);
                float clear = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + 8f;
                int placed = 0;
                for (int i = 0; i < fr.Length && placed < 850; i += 4)
                {
                    Frame f = fr[i];
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int band = 0; band < 3; band++)
                        {
                            float lat = clear + 12f + band * 22f + UnityEngine.Random.Range(-7f, 9f);
                            Vector3 pos = f.Pos + f.Right * (side * lat) + f.Fwd * UnityEngine.Random.Range(-9f, 9f);
                            float ty;
                            if (grid.Distance(pos, out ty) < clear + 4f) continue;
                            pos.y = ty - spec.VergeDrop - 1.4f;
                            float sc = UnityEngine.Random.Range(0.9f, 1.6f);
                            AddTaperedCylinder(batch.MB, 0, pos, 0.5f * sc, 0.34f * sc, 4.6f * sc, 5);
                            AddBlob(batch.MB, 1, pos + Vector3.up * (8.0f * sc),
                                new Vector3(4.4f * sc, 3.6f * sc, 4.4f * sc), 3);
                            batch.EndProp();
                            placed++;
                        }
                    }
                }
                batch.Flush();
            }

            BuildSponsorBridges(holder.transform, fr, spec, pal, new[] { 0.060f, 0.470f, 0.760f });
        }

        // ------------------------------------------------------------------ Monaco

        private static void BuildMonaco(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal)
        {
            GameObject holder = new GameObject("Monaco_Trackside");
            holder.transform.SetParent(parent, false);
            TrackGrid grid = new TrackGrid(fr);

            // Port Hercule: the basin sits inside the loop formed by the harbour front and the
            // swimming pool section, derived from the actual centreline rather than fixed coordinates.
            {
                // The basin is the open water bordered by Quai des Etats-Unis, the Piscine and
                // Boulevard Albert 1er, so restrict the search to that stretch of the lap.
                float clearance;
                Vector3 centre = LargestEnclosedOpening(fr, 0.66f, 0.10f, out clearance);
                centre.y = 0f;

                // Size from the half-diagonal, otherwise the rectangle's corners poke through the
                // circle of clearance and the water ends up laid over the road.
                const float aspect = 0.78f;
                float halfDiag = Mathf.Max(16f, clearance - 9f);
                float halfX = halfDiag / Mathf.Sqrt(1f + aspect * aspect);
                float halfZ = halfX * aspect;

                MeshBuilder h = new MeshBuilder(3);   // 0 water, 1 hull, 2 superstructure
                AddBox(h, 0, centre + Vector3.down * 0.55f, new Vector3(halfX, 0.35f, halfZ), Quaternion.identity);
                int berths = 9;
                for (int y = 0; y < berths; y++)
                {
                    float ang = y / (float)berths * Mathf.PI * 2f;
                    Vector3 pos = centre + new Vector3(Mathf.Cos(ang) * halfX * 0.68f, 1.3f, Mathf.Sin(ang) * halfZ * 0.68f);
                    float ty;
                    if (grid.Distance(pos, out ty) < 24f) continue;
                    Quaternion rot = Quaternion.Euler(0f, ang * Mathf.Rad2Deg + 90f, 0f);
                    float len = UnityEngine.Random.Range(22f, 40f);
                    AddBox(h, 1, pos, new Vector3(5.2f, 2.0f, len * 0.5f), rot);
                    AddBox(h, 2, pos + Vector3.up * 2.9f, new Vector3(3.9f, 1.3f, len * 0.28f), rot);
                    AddBox(h, 2, pos + Vector3.up * 4.9f, new Vector3(2.5f, 0.9f, len * 0.15f), rot);
                }
                Emit(holder.transform, "Port_Hercule", h,
                    new[] { pal.SeaWater, pal.YachtHull, pal.GrandstandRoof }, false);
            }

            // The tunnel: a genuinely covered section from Portier to the chicane braking zone
            {
                int i0 = IndexAtU(fr, 0.545f);
                int count = Mathf.RoundToInt(0.095f * fr.Length);
                MeshBuilder t = new MeshBuilder(3);   // 0 concrete, 1 lighting, 2 portal
                float hw = spec.HalfWidth + spec.KerbPad + 1.2f;

                Prof[] roofProf = { new Prof(-hw - 1.2f, 5.9f), new Prof(hw + 1.2f, 5.9f) };
                Extrude(t, fr, i0, count, true, roofProf, new[] { 0 }, 0.08f, true, null, null);
                for (int side = -1; side <= 1; side += 2)
                {
                    Prof[] wallProf = { new Prof(side * (hw + 1.2f), 0f), new Prof(side * (hw + 1.2f), 5.9f) };
                    Extrude(t, fr, i0, count, true, wallProf, new[] { 0 }, 0.10f, true, null, null);
                }
                Prof[] strip = { new Prof(-1.1f, 5.72f), new Prof(1.1f, 5.72f) };
                Extrude(t, fr, i0, count, true, strip, new[] { 1 }, 0.30f);

                foreach (int end in new[] { i0, (i0 + count) % fr.Length })
                {
                    Frame f = fr[end];
                    AddBox(t, 2, f.Pos + Vector3.up * 6.6f, new Vector3(hw + 3.4f, 1.0f, 1.4f),
                        Quaternion.LookRotation(f.Fwd, Vector3.up));
                }
                Emit(holder.transform, "Monte_Carlo_Tunnel", t,
                    new[] { pal.TunnelWall, pal.LightEmissive, pal.PitWall }, false);
            }

            // Monte-Carlo street frontage: apartment and hotel blocks lining the circuit
            {
                MeshBuilder b = new MeshBuilder(3);   // 0 render, 1 balcony, 2 roof
                float clear = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + 4f;
                int placed = 0;
                for (int i = 0; i < fr.Length && placed < 260; i += 4)
                {
                    Frame f = fr[i];
                    // leave the harbour side of the main straight and the quay open to the water
                    bool openWater = (f.U > 0.68f && f.U < 0.74f) || f.U < 0.09f || f.U > 0.955f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (openWater && side > 0) continue;
                        float lat = clear + 9f + UnityEngine.Random.Range(0f, 7f);
                        Vector3 pos = f.Pos + f.Right * (side * lat);
                        float ty;
                        if (grid.Distance(pos, out ty) < clear + 6f) continue;
                        pos.y = ty - 0.4f;
                        float w = UnityEngine.Random.Range(11f, 19f);
                        float d = UnityEngine.Random.Range(13f, 22f);
                        int storeys = UnityEngine.Random.Range(4, 11);
                        float sh = 3.15f;
                        Quaternion rot = Quaternion.LookRotation(f.Fwd, Vector3.up);
                        AddBox(b, 0, pos + Vector3.up * (storeys * sh * 0.5f),
                            new Vector3(w * 0.5f, storeys * sh * 0.5f, d * 0.5f), rot);
                        for (int s = 1; s < storeys; s++)
                            AddBox(b, 1, pos + Vector3.up * (s * sh) - f.Right * (side * (w * 0.5f + 0.35f)),
                                new Vector3(0.45f, 0.10f, d * 0.45f), rot);
                        AddBox(b, 2, pos + Vector3.up * (storeys * sh + 0.3f),
                            new Vector3(w * 0.52f, 0.3f, d * 0.52f), rot);
                        placed++;
                    }
                }
                Emit(holder.transform, "Monte_Carlo_Frontage", b,
                    new[] { pal.CityRender, pal.PitWall, pal.GrandstandRoof }, false);
            }

            // Temporary harbour-side grandstands and the Piscine stands
            BuildGrandstand(holder.transform, fr, spec, pal, 0.690f, 0.735f, 1f, 4f, 10, 0.55f, 0.80f, false, "Quayside_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.760f, 0.820f, 1f, 4f, 11, 0.55f, 0.80f, false, "Piscine_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.885f, 0.935f, -1f, 4f, 9, 0.55f, 0.80f, false, "Rascasse_Grandstand");
            BuildGrandstand(holder.transform, fr, spec, pal, 0.095f, 0.130f, -1f, 4f, 9, 0.55f, 0.80f, false, "SainteDevote_Grandstand");

            // Palms along Boulevard Albert 1er and the quay
            {
                MeshBuilder p = new MeshBuilder(2);
                float clear = spec.HalfWidth + spec.KerbPad + spec.RunoffWidth + spec.BarrierGap + 2.5f;
                for (int i = 0; i < fr.Length; i += 4)
                {
                    Frame f = fr[i];
                    if (!(f.U < 0.10f || f.U > 0.94f || (f.U > 0.66f && f.U < 0.76f))) continue;
                    Vector3 pos = f.Pos + f.Right * (clear + 3.0f);
                    float ty;
                    if (grid.Distance(pos, out ty) < clear + 1.5f) continue;
                    pos.y = ty - 0.2f;
                    float h = UnityEngine.Random.Range(6.0f, 8.5f);
                    AddTaperedCylinder(p, 0, pos, 0.34f, 0.26f, h, 7);
                    for (int k = 0; k < 7; k++)
                    {
                        float a = k / 7f * Mathf.PI * 2f;
                        Vector3 tip = pos + Vector3.up * (h + 0.5f) + new Vector3(Mathf.Cos(a), -0.28f, Mathf.Sin(a)) * 2.8f;
                        AddBox(p, 1, (pos + Vector3.up * h + tip) * 0.5f, new Vector3(0.26f, 0.08f, 1.6f),
                            Quaternion.LookRotation(tip - (pos + Vector3.up * h), Vector3.up));
                    }
                }
                Emit(holder.transform, "Monaco_Palms", p, new[] { pal.PalmTrunk, pal.PalmLeaf }, false);
            }
        }

        // ------------------------------------------------------------------ shared props

        private static void BuildSponsorBridges(Transform parent, Frame[] fr, CircuitSpec spec, Pal pal, float[] us)
        {
            MeshBuilder mb = new MeshBuilder(3);   // 0 steel, 1 panel A, 2 panel B
            float leg = spec.HalfWidth + spec.KerbPad + Mathf.Min(spec.RunoffWidth, 6f) + 2f;

            for (int b = 0; b < us.Length; b++)
            {
                Frame f = fr[IndexAtU(fr, us[b])];
                Quaternion rot = Quaternion.LookRotation(f.Fwd, Vector3.up);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 footing = f.Pos + f.Right * (side * leg);
                    AddTaperedCylinder(mb, 0, footing, 0.42f, 0.34f, 8.6f, 8);
                    AddBox(mb, 0, footing + Vector3.up * 0.35f, new Vector3(1.1f, 0.35f, 1.1f), rot);
                }
                AddBox(mb, 0, f.Pos + Vector3.up * 8.7f, new Vector3(leg + 1.4f, 0.30f, 1.5f), rot);
                AddBox(mb, b % 2 == 0 ? 1 : 2, f.Pos + Vector3.up * 9.9f,
                    new Vector3(leg + 0.6f, 1.15f, 0.35f), rot);
                AddBox(mb, 0, f.Pos + Vector3.up * 11.1f, new Vector3(leg + 1.4f, 0.14f, 1.2f), rot);
            }
            Emit(parent, "Overhead_Sponsor_Bridges", mb,
                new[] { pal.Gantry, pal.RolexGreen, pal.PirelliYellow }, false);
        }

        // ------------------------------------------------------------------ materials

        private sealed class Pal
        {
            public readonly Material Asphalt, WhiteLine, YellowLine, KerbPad, Runoff, Verge, Terrain;
            public readonly Material KerbRed, KerbWhite;
            public readonly Material Armco, Sponsor, Gantry, MarshalPost, BrakeBoard, FlagLed;
            public readonly Material PitWall, PitAsphalt, PitBuilding, Grandstand, GrandstandRoof;
            public readonly Material TreeTrunk, PineLeaf, OakLeaf, PalmTrunk, PalmLeaf;
            public readonly Material SeaWater, YachtHull, TunnelWall, HistoricOval, CityRender;
            public readonly Material RolexGreen, RolexGold, PirelliYellow, TentFabric, TeamRed;
            public readonly Material StartLight, Floodlight, LightEmissive;
            public readonly float Relief;

            public Pal(CircuitType t, CircuitSpec spec)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

                Asphalt = Mat(lit, "Mat_Asphalt", new Color(0.145f, 0.149f, 0.161f), 0.34f, 0.03f,
                    "Assets/Environments/Bahrain/Textures/T_Asphalt_Albedo.png");
                WhiteLine = Mat(lit, "Mat_WhiteLine", new Color(0.941f, 0.945f, 0.949f), 0.32f, 0.0f, null);
                YellowLine = Mat(lit, "Mat_YellowLine", new Color(0.945f, 0.804f, 0.098f), 0.32f, 0.0f, null);
                KerbPad = Mat(lit, "Mat_KerbPad", spec.Runoff * 0.86f, 0.22f, 0.0f, null);
                Runoff = Mat(lit, "Mat_Runoff", spec.Runoff, 0.14f, 0.0f, null);
                Verge = Mat(lit, "Mat_Verge", spec.Verge, 0.12f, 0.0f, null);
                Terrain = Mat(lit, "Mat_Terrain", spec.Terrain, 0.10f, 0.0f, null);

                KerbRed = Mat(lit, "Mat_KerbRed", new Color(0.769f, 0.114f, 0.098f), 0.42f, 0.02f,
                    "Assets/Environments/Bahrain/Textures/T_Kerb_Albedo.png");
                KerbWhite = Mat(lit, "Mat_KerbWhite", new Color(0.925f, 0.925f, 0.929f), 0.42f, 0.02f, null);

                Armco = Mat(lit, "Mat_Armco", new Color(0.706f, 0.722f, 0.745f), 0.62f, 0.65f,
                    "Assets/Environments/Bahrain/Textures/T_Barrier_Albedo.png");
                Sponsor = Mat(lit, "Mat_SponsorPanel", new Color(0.855f, 0.243f, 0.157f), 0.40f, 0.05f,
                    "Assets/Environments/Bahrain/Textures/T_Sponsor_Albedo.png");
                Gantry = Mat(lit, "Mat_Steel", new Color(0.216f, 0.220f, 0.239f), 0.58f, 0.78f, null);
                MarshalPost = Mat(lit, "Mat_MarshalPost", new Color(0.925f, 0.443f, 0.098f), 0.36f, 0.04f, null);
                BrakeBoard = Mat(lit, "Mat_BrakeBoard", new Color(0.086f, 0.090f, 0.102f), 0.44f, 0.04f, null);
                FlagLed = Emissive(lit, "Mat_FlagLed", new Color(0.10f, 0.95f, 0.22f), 2.0f);

                PitWall = Mat(lit, "Mat_Concrete", new Color(0.478f, 0.486f, 0.502f), 0.22f, 0.04f, null);
                PitAsphalt = Mat(lit, "Mat_PitAsphalt", new Color(0.208f, 0.212f, 0.224f), 0.30f, 0.02f, null);
                PitBuilding = Mat(lit, "Mat_PitBuilding", new Color(0.239f, 0.263f, 0.302f), 0.52f, 0.28f, null);
                Grandstand = Mat(lit, "Mat_Grandstand", new Color(0.180f, 0.239f, 0.376f), 0.36f, 0.08f, null);
                GrandstandRoof = Mat(lit, "Mat_Canopy", new Color(0.847f, 0.867f, 0.898f), 0.55f, 0.35f, null);

                TreeTrunk = Mat(lit, "Mat_TreeTrunk", new Color(0.310f, 0.235f, 0.176f), 0.10f, 0.0f, null);
                PineLeaf = Mat(lit, "Mat_PineFoliage", new Color(0.110f, 0.267f, 0.137f), 0.10f, 0.0f, null);
                OakLeaf = Mat(lit, "Mat_OakFoliage", new Color(0.208f, 0.435f, 0.176f), 0.13f, 0.0f, null);
                PalmTrunk = Mat(lit, "Mat_PalmTrunk", new Color(0.451f, 0.365f, 0.271f), 0.14f, 0.0f, null);
                PalmLeaf = Mat(lit, "Mat_PalmFronds", new Color(0.180f, 0.400f, 0.157f), 0.18f, 0.0f, null);

                SeaWater = Mat(lit, "Mat_Mediterranean", new Color(0.031f, 0.286f, 0.478f), 0.88f, 0.10f, null);
                YachtHull = Mat(lit, "Mat_YachtHull", new Color(0.949f, 0.953f, 0.965f), 0.72f, 0.10f, null);
                TunnelWall = Mat(lit, "Mat_TunnelConcrete", new Color(0.361f, 0.365f, 0.376f), 0.24f, 0.04f, null);
                HistoricOval = Mat(lit, "Mat_HistoricConcrete", new Color(0.518f, 0.502f, 0.463f), 0.26f, 0.04f, null);
                CityRender = Mat(lit, "Mat_MonacoFacade", new Color(0.824f, 0.784f, 0.706f), 0.24f, 0.03f, null);

                RolexGreen = Mat(lit, "Mat_RolexGreen", new Color(0.0f, 0.353f, 0.169f), 0.62f, 0.18f, null);
                RolexGold = Mat(lit, "Mat_RolexGold", new Color(0.867f, 0.706f, 0.204f), 0.82f, 0.78f, null);
                PirelliYellow = Mat(lit, "Mat_PirelliYellow", new Color(0.945f, 0.792f, 0.047f), 0.62f, 0.08f, null);
                TentFabric = Mat(lit, "Mat_TentFabric", new Color(0.918f, 0.922f, 0.933f), 0.28f, 0.0f, null);
                TeamRed = Mat(lit, "Mat_TeamRed", new Color(0.804f, 0.098f, 0.118f), 0.46f, 0.08f, null);

                StartLight = Emissive(lit, "Mat_StartLight", new Color(1.0f, 0.12f, 0.12f), 3.0f);
                Floodlight = Mat(lit, "Mat_FloodlightMetal", new Color(0.349f, 0.353f, 0.376f), 0.62f, 0.72f, null);
                LightEmissive = Emissive(lit, "Mat_LightEmissive", new Color(1.0f, 0.965f, 0.886f), 2.6f);

                Relief = (t == CircuitType.Spa) ? 26f : (t == CircuitType.Bahrain ? 9f : (t == CircuitType.Monza ? 4f : 0f));
            }

            private static Material Mat(Shader shader, string name, Color colour, float smoothness, float metallic, string texturePath)
            {
                Material m = new Material(shader) { name = name };
                m.color = colour;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colour);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(texturePath))
                {
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (tex != null)
                    {
                        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                    }
                }
#endif
                return m;
            }

            private static Material Emissive(Shader shader, string name, Color colour, float intensity)
            {
                Material m = Mat(shader, name, colour, 0.85f, 0f, null);
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", colour * intensity);
                if (m.HasProperty("_EmissiveColor")) m.SetColor("_EmissiveColor", colour * intensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return m;
            }
        }

        // ------------------------------------------------------------------ misc

        private static void DestroyNow(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        private static float MinY(List<Vector3> pts)
        {
            float m = float.MaxValue;
            for (int i = 0; i < pts.Count; i++) if (pts[i].y < m) m = pts[i].y;
            return m;
        }

        private static float MaxY(List<Vector3> pts)
        {
            float m = float.MinValue;
            for (int i = 0; i < pts.Count; i++) if (pts[i].y > m) m = pts[i].y;
            return m;
        }
    }
}
