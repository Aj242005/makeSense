using System;
using System.Collections.Generic;
using UnityEngine;
using GridSense.Physics;
using GridSense.Environment;
using GridSense.Core;

namespace GridSense.UI
{
    public enum SimGameState
    {
        StartingScreen = 0,
        Racing = 1
    }

    /// <summary>
    /// The GridSense engineering console: the pre-session grid screen and the in-race overlay.
    ///
    /// Both surfaces are built on one relationship — command versus actual. Anything the models
    /// recommend is drawn in Command magenta beside what the driver actually did, because the point
    /// of this product is the gap between the two, not either number alone.
    ///
    /// The overlay lives entirely in the corners. The centre of the screen belongs to the circuit.
    /// </summary>
    public class F1TelemetryHUD : MonoBehaviour
    {
        public static F1TelemetryHUD Instance { get; private set; }

        [Header("Session")]
        [SerializeField] private SimGameState gameState = SimGameState.StartingScreen;
        [SerializeField] private CarPhysicsController car;
        [SerializeField] private int plannedStintLaps = 20;

        private CircuitType selectedCircuit = CircuitType.Bahrain;
        private CircuitType currentActiveCircuit = CircuitType.Bahrain;
        private TyreCompound selectedCompound = TyreCompound.Soft;

        public CircuitType SelectedCircuit { get { return selectedCircuit; } }
        public List<Vector3> CachedCenterline { get { return cachedCenterline; } }
        public bool IsRacing { get { return gameState == SimGameState.Racing; } }

        // --- live timing ---------------------------------------------------------------------
        public int CurrentLapNumber { get; private set; } = 1;
        public float CurrentLapTime { get; private set; }
        public float LastLapTime { get; private set; }
        public float BestLapTime { get; private set; }
        public float CurrentSectorTime { get; private set; }
        public int ActiveSector { get; private set; } = 1;

        public float S1Time { get; private set; }
        public float S2Time { get; private set; }
        public float S3Time { get; private set; }

        // No session history exists at launch, so there are no bests until the driver sets them.
        // The previous build shipped hardcoded placeholder splits that read as real data.
        public float BestS1Time { get; private set; }
        public float BestS2Time { get; private set; }
        public float BestS3Time { get; private set; }

        private Vector3 prevCarPos;
        private bool hasPassedS1, hasPassedS2;
        private float s2StartTime, s3StartTime;

        private List<Vector3> cachedCenterline;
        private CircuitPlate.Data plate;

        // --- damped readouts -----------------------------------------------------------------
        private Theme.Damped dSpeed, dBattery, dRisk;

        // --- starting-screen interaction -------------------------------------------------------
        private float sweepStart = -1f;
        private int hoverCircuit = -1;
        private int hoverCompound = -1;
        private bool hoverLaunch;
        private bool pitWallPresent;
        private float pitWallCheckedAt = -10f;
        private const float SweepSeconds = 0.62f;

        private static readonly CircuitType[] CircuitOrder =
        {
            CircuitType.Bahrain, CircuitType.Spa, CircuitType.Monaco, CircuitType.Monza
        };

        private static readonly TyreCompound[] CompoundOrder =
        {
            TyreCompound.Soft, TyreCompound.Medium, TyreCompound.Hard
        };

        // ==================================================================== lifecycle

        private void Awake()
        {
            Instance = this;
            LoadCircuit(selectedCircuit);
        }

        private void Start()
        {
            FindCar();
            PlaceCarOnPole();
            sweepStart = Time.unscaledTime;
        }

        private void FindCar()
        {
            if (car == null) car = UnityEngine.Object.FindFirstObjectByType<CarPhysicsController>();
        }

        private void LoadCircuit(CircuitType circuit)
        {
            plate = CircuitPlate.Get(circuit);
            cachedCenterline = plate.Centre;
        }

        private void PlaceCarOnPole()
        {
            if (car == null || cachedCenterline == null) return;
            Vector3 slot;
            Quaternion rot;
            TrackVisualBuilder.GetGridSlot(cachedCenterline, selectedCircuit, 0, out slot, out rot);
            Vector3 pole = slot + Vector3.up * 0.564f;
            car.TeleportVehicle(pole, rot);
            prevCarPos = pole;
            car.SetTyreCompound(selectedCompound);
        }

        private void Update()
        {
            if (gameState == SimGameState.StartingScreen)
            {
                HandleMenuKeys();
                return;
            }

            UpdateLapAndSectorTiming(Time.deltaTime);

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) ReturnToGrid();
        }

        private void HandleMenuKeys()
        {
            int ci = Array.IndexOf(CircuitOrder, selectedCircuit);
            int ti = Array.IndexOf(CompoundOrder, selectedCompound);

            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.S))
                SelectCircuit(CircuitOrder[Mathf.Min(CircuitOrder.Length - 1, ci + 1)]);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.W))
                SelectCircuit(CircuitOrder[Mathf.Max(0, ci - 1)]);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.D))
                SelectCompound(CompoundOrder[Mathf.Min(CompoundOrder.Length - 1, ti + 1)]);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.A))
                SelectCompound(CompoundOrder[Mathf.Max(0, ti - 1)]);
            else if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                BeginStint();
        }

        private void ReturnToGrid()
        {
            gameState = SimGameState.StartingScreen;
            sweepStart = Time.unscaledTime;
        }

        private void SelectCircuit(CircuitType c)
        {
            if (c == selectedCircuit) return;
            selectedCircuit = c;
            LoadCircuit(c);
            sweepStart = Time.unscaledTime;

            if (selectedCircuit != currentActiveCircuit)
            {
                currentActiveCircuit = selectedCircuit;
                GameObject env = GameObject.Find("EnvironmentManager") ?? GameObject.Find("Environment");
                if (env != null) TrackVisualBuilder.BuildCircuitVisuals(env.transform, selectedCircuit);
            }
            PlaceCarOnPole();
        }

        private void SelectCompound(TyreCompound c)
        {
            if (c == selectedCompound) return;
            selectedCompound = c;
            if (car != null) car.SetTyreCompound(selectedCompound);
        }

        private void BeginStint()
        {
            if (selectedCircuit != currentActiveCircuit)
            {
                currentActiveCircuit = selectedCircuit;
                GameObject env = GameObject.Find("EnvironmentManager") ?? GameObject.Find("Environment");
                if (env != null) TrackVisualBuilder.BuildCircuitVisuals(env.transform, selectedCircuit);
            }

            LoadCircuit(selectedCircuit);
            FindCar();
            PlaceCarOnPole();

            CurrentLapNumber = 1;
            CurrentLapTime = 0f;
            CurrentSectorTime = 0f;
            ActiveSector = 1;
            hasPassedS1 = hasPassedS2 = false;
            S1Time = S2Time = S3Time = 0f;
            BestS1Time = BestS2Time = BestS3Time = 0f;
            BestLapTime = LastLapTime = 0f;

            gameState = SimGameState.Racing;
        }

        // ==================================================================== timing

        private void UpdateLapAndSectorTiming(float dt)
        {
            if (car == null || cachedCenterline == null || cachedCenterline.Count < 8) return;

            CurrentLapTime += dt;
            CurrentSectorTime += dt;

            Vector3 curPos = car.transform.position;
            int n = cachedCenterline.Count - 1;

            float progress = NearestLapFraction(curPos, n);

            if (!hasPassedS1 && progress > 0.3333f && progress < 0.55f)
            {
                hasPassedS1 = true;
                ActiveSector = 2;
                S1Time = CurrentLapTime;
                if (BestS1Time <= 0f || S1Time < BestS1Time) BestS1Time = S1Time;
                s2StartTime = CurrentLapTime;
                CurrentSectorTime = 0f;
            }
            else if (hasPassedS1 && !hasPassedS2 && progress > 0.6667f && progress < 0.88f)
            {
                hasPassedS2 = true;
                ActiveSector = 3;
                S2Time = CurrentLapTime - s2StartTime;
                if (BestS2Time <= 0f || S2Time < BestS2Time) BestS2Time = S2Time;
                s3StartTime = CurrentLapTime;
                CurrentSectorTime = 0f;
            }

            Vector3 sfPos = cachedCenterline[TrackVisualBuilder.StartFinishIndex];
            Vector3 sfNext = cachedCenterline[TrackVisualBuilder.StartFinishIndex + 1];
            Vector3 sfFwd = (sfNext - sfPos).normalized;

            float prevSigned = Vector3.Dot(prevCarPos - sfPos, sfFwd);
            float curSigned = Vector3.Dot(curPos - sfPos, sfFwd);

            if (ActiveSector == 3 && CurrentLapTime > 12f)
            {
                bool crossed = prevSigned <= 1f && curSigned > 0f && (curSigned - prevSigned) < 20f;
                float lateral = Vector3.ProjectOnPlane(curPos - sfPos, sfFwd).magnitude;

                if (crossed && lateral < 25f)
                {
                    S3Time = CurrentLapTime - s3StartTime;
                    if (BestS3Time <= 0f || S3Time < BestS3Time) BestS3Time = S3Time;

                    LastLapTime = CurrentLapTime;
                    if (BestLapTime <= 0f || LastLapTime < BestLapTime) BestLapTime = LastLapTime;

                    CurrentLapNumber++;
                    CurrentLapTime = 0f;
                    CurrentSectorTime = 0f;
                    ActiveSector = 1;
                    hasPassedS1 = hasPassedS2 = false;
                }
            }

            prevCarPos = curPos;
        }

        /// <summary>Fraction of the lap completed, from the nearest centreline sample.</summary>
        private float NearestLapFraction(Vector3 pos, int n)
        {
            int best = 0;
            float bestSq = float.MaxValue;
            int stride = Mathf.Max(1, n / 220);
            for (int i = 0; i < n; i += stride)
            {
                float dx = cachedCenterline[i].x - pos.x;
                float dz = cachedCenterline[i].z - pos.z;
                float d = dx * dx + dz * dz;
                if (d < bestSq) { bestSq = d; best = i; }
            }
            return best / (float)n;
        }

        // ==================================================================== AI access

        private bool HasModels
        {
            get { return SimulationCore.Instance != null; }
        }

        /// <summary>
        /// True only when the trained policy actually loaded. The runner falls back to an
        /// analytical heuristic when the ONNX is missing, and that must not be labelled as PPO.
        /// </summary>
        private bool Track1ModelLoaded
        {
            get
            {
                return GridSense.ML.Track1InferenceRunner.Instance != null
                    && GridSense.ML.Track1InferenceRunner.Instance.IsModelLoaded;
            }
        }

        private EnergyDeploymentExplainability Energy
        {
            get
            {
                return SimulationCore.Instance != null
                    ? SimulationCore.Instance.EnergyExplainability
                    : EnergyDeploymentExplainability.CreateDefault();
            }
        }

        // ==================================================================== paint

        private void OnGUI()
        {
            Theme.Begin();
            if (gameState == SimGameState.StartingScreen) DrawGrid();
            else DrawOverlay();
        }

        // -------------------------------------------------------------------- starting screen

        private void DrawGrid()
        {
            float W = Screen.width, H = Screen.height;
            Color scrim = Theme.Ground;
            scrim.a = 0.90f;
            Theme.Fill(new Rect(0, 0, W, H), scrim);

            float margin = Mathf.Max(Theme.U(40f), (W - Theme.U(1560f)) * 0.5f);
            Rect frame = new Rect(margin, Theme.U(38f), W - margin * 2f, H - Theme.U(76f));

            // --- masthead
            float headH = Theme.U(76f);
            Theme.Text(new Rect(frame.x, frame.y, frame.width * 0.6f, Theme.U(56f)), "GRIDSENSE", Theme.Display);
            Theme.Text(new Rect(frame.x + Theme.U(3f), frame.y + Theme.U(52f), frame.width * 0.6f, Theme.U(16f)),
                "AI MOTORSPORT INTELLIGENCE", Theme.Label);

            string sessionLine = plate != null
                ? CircuitName(selectedCircuit).ToUpperInvariant() + "   ·   " +
                  Theme.CompoundName(selectedCompound) + " " + Theme.CompoundCode(selectedCompound) +
                  "   ·   " + plannedStintLaps + " LAP STINT"
                : "";
            Theme.Text(new Rect(frame.x, frame.y + Theme.U(26f), frame.width, Theme.U(20f)),
                sessionLine, Theme.Styled(Theme.Label, Theme.Ink, TextAnchor.MiddleRight));

            Theme.HRule(frame.x, frame.y + headH, frame.width, Theme.Rule);

            // --- split
            float bodyY = frame.y + headH + Theme.U(26f);
            float footH = Theme.U(46f);
            float bodyH = frame.yMax - footH - Theme.U(20f) - bodyY;

            float rightW = Mathf.Clamp(frame.width * 0.34f, Theme.U(360f), Theme.U(460f));
            float gutter = Theme.U(44f);
            float leftW = frame.width - rightW - gutter;

            DrawCircuitPlate(new Rect(frame.x, bodyY, leftW, bodyH));
            DrawSessionSheet(new Rect(frame.xMax - rightW, bodyY, rightW, bodyH));

            // --- controls legend
            Theme.HRule(frame.x, frame.yMax - footH, frame.width, Theme.Rule);
            DrawControlLegend(new Rect(frame.x, frame.yMax - footH + Theme.U(12f), frame.width, footH));
        }

        private void DrawCircuitPlate(Rect r)
        {
            if (plate == null || plate.Norm.Length < 2) return;

            float elevH = Theme.U(66f);
            float factsH = Theme.U(58f);
            float mapH = r.height - elevH - factsH - Theme.U(44f);
            Rect map = new Rect(r.x, r.y, r.width, mapH);

            // keep the outline square inside the available area so the shape is never stretched
            float side = Mathf.Min(map.width, map.height);
            Rect square = new Rect(map.x + (map.width - side) * 0.5f, map.y + (map.height - side) * 0.5f, side, side);

            float t = Mathf.Clamp01((Time.unscaledTime - sweepStart) / SweepSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            CircuitPlate.DrawSweep(plate, square, eased, Theme.U(3.2f), 0.13f);
            CircuitPlate.DrawCornerTicks(plate, square, eased, Theme.U(11f));

            // start/finish marker, once the sweep has begun
            if (eased > 0.02f)
            {
                Vector2 sf = CircuitPlate.ToRect(plate.Norm[0], square);
                float s = Theme.U(9f);
                Theme.Fill(new Rect(sf.x - s * 0.5f, sf.y - s * 0.5f, s, s), Theme.InkHi);
                Theme.Text(new Rect(sf.x + s, sf.y - Theme.U(8f), Theme.U(120f), Theme.U(16f)),
                    "START / FINISH", Theme.Micro);
            }

            // --- elevation
            Rect elev = new Rect(r.x, map.yMax + Theme.U(24f), r.width, elevH - Theme.U(20f));
            Theme.Text(new Rect(elev.x, elev.y - Theme.U(16f), r.width * 0.5f, Theme.U(14f)),
                "ELEVATION ALONG LAP", Theme.Label);
            CircuitPlate.DrawElevation(plate, elev);
            Theme.HRule(elev.x, elev.yMax, elev.width, Theme.Rule);
            Theme.Text(new Rect(elev.x, elev.yMax + Theme.U(4f), Theme.U(90f), Theme.U(14f)),
                plate.MinY.ToString("F0") + " m", Theme.Micro);
            
            Theme.Text(new Rect(elev.xMax - Theme.U(90f), elev.yMax + Theme.U(4f), Theme.U(90f), Theme.U(14f)),
                "+" + plate.MaxY.ToString("F0") + " m", Theme.Align(Theme.Micro, TextAnchor.MiddleRight));

            // --- measured facts, straight off the geometry
            Rect facts = new Rect(r.x, r.yMax - factsH, r.width, factsH);
            float col = facts.width / 4f;
            Fact(new Rect(facts.x, facts.y, col, facts.height), "LAP DISTANCE",
                (plate.LengthM / 1000f).ToString("F3"), "km");
            Fact(new Rect(facts.x + col, facts.y, col, facts.height), "CORNERS",
                plate.CornerIndex.Length.ToString(), "detected");
            Fact(new Rect(facts.x + col * 2f, facts.y, col, facts.height), "ELEVATION RANGE",
                (plate.MaxY - plate.MinY).ToString("F0"), "m");
            Fact(new Rect(facts.x + col * 3f, facts.y, col, facts.height), "TIGHTEST RADIUS",
                TightestRadius(plate).ToString(), "m");
        }

        private static int TightestRadius(CircuitPlate.Data d)
        {
            int best = int.MaxValue;
            for (int i = 0; i < d.CornerRadius.Length; i++)
                if (d.CornerRadius[i] < best) best = d.CornerRadius[i];
            return best == int.MaxValue ? 0 : best;
        }

        private void Fact(Rect r, string label, string value, string unit)
        {
            Theme.Text(new Rect(r.x, r.y, r.width, Theme.U(14f)), label, Theme.Label);
            float vw = Theme.U(96f);
            Theme.Text(new Rect(r.x, r.y + Theme.U(16f), vw, Theme.U(32f)), value, Theme.Metric);
            Theme.Text(new Rect(r.x + vw + Theme.U(4f), r.y + Theme.U(26f), r.width - vw, Theme.U(16f)),
                unit, Theme.Micro);
        }

        private void DrawSessionSheet(Rect r)
        {
            float y = r.y;

            Theme.Text(new Rect(r.x, y, r.width, Theme.U(14f)), "CIRCUIT", Theme.Label);
            y += Theme.U(20f);
            Theme.HRule(r.x, y, r.width, Theme.Rule);
            y += Theme.U(6f);

            float rowH = Theme.U(46f);
            hoverCircuit = -1;
            for (int i = 0; i < CircuitOrder.Length; i++)
            {
                Rect row = new Rect(r.x, y, r.width, rowH);
                if (row.Contains(Event.current.mousePosition)) hoverCircuit = i;
                DrawCircuitRow(row, CircuitOrder[i], i == hoverCircuit);
                y += rowH;
            }

            y += Theme.U(22f);
            Theme.Text(new Rect(r.x, y, r.width, Theme.U(14f)), "TYRE COMPOUND", Theme.Label);
            y += Theme.U(20f);
            Theme.HRule(r.x, y, r.width, Theme.Rule);
            y += Theme.U(6f);

            float cRowH = Theme.U(52f);
            hoverCompound = -1;
            for (int i = 0; i < CompoundOrder.Length; i++)
            {
                Rect row = new Rect(r.x, y, r.width, cRowH);
                if (row.Contains(Event.current.mousePosition)) hoverCompound = i;
                DrawCompoundRow(row, CompoundOrder[i], i == hoverCompound);
                y += cRowH;
            }

            // --- what you are about to run, and whether the models will be watching it.
            // Knowing after the stint that inference was never running is knowing too late, so the
            // readiness rows are the part that survives on a short screen; the three static facts
            // are already legible in the compound rows above and are dropped first.
            float btnH = Theme.U(58f);
            float launchTop = r.yMax - btnH;
            float room = launchTop - y - Theme.U(18f);

            bool showBlock = room >= Theme.U(64f);
            bool showHeader = room >= Theme.U(112f);
            bool showFacts = room >= Theme.U(182f);

            if (showBlock)
            {
                if (showHeader)
                {
                    y += Theme.U(26f);
                    Theme.Text(new Rect(r.x, y, r.width, Theme.U(14f)), "SESSION", Theme.Label);
                    y += Theme.U(20f);
                    Theme.HRule(r.x, y, r.width, Theme.Rule);
                    y += Theme.U(10f);
                }
                else
                {
                    y += Theme.U(12f);
                }

                if (showFacts)
                {
                    SessionRow(new Rect(r.x, y, r.width, Theme.U(20f)),
                        "Stint length", plannedStintLaps + " laps");
                    y += Theme.U(22f);

                    SessionRow(new Rect(r.x, y, r.width, Theme.U(20f)),
                        "Peak grip", Theme.RelativeGripPct(selectedCompound) + "% of soft");
                    y += Theme.U(22f);

                    SessionRow(new Rect(r.x, y, r.width, Theme.U(20f)),
                        "Wear rate", Theme.WearMultiplier(selectedCompound).ToString("0.00") + "x baseline");
                    y += Theme.U(26f);
                }

                ModelRow(new Rect(r.x, y, r.width, Theme.U(20f)), "Energy deployment",
                    GridSense.ML.Track1InferenceRunner.Instance != null,
                    GridSense.ML.Track1InferenceRunner.Instance != null
                        && GridSense.ML.Track1InferenceRunner.Instance.IsModelLoaded);
                y += Theme.U(22f);

                ModelRow(new Rect(r.x, y, r.width, Theme.U(20f)), "Degradation isolation",
                    GridSense.ML.Track3InferenceRunner.Instance != null,
                    GridSense.ML.Track3InferenceRunner.Instance != null
                        && GridSense.ML.Track3InferenceRunner.Instance.IsModelLoaded);
            }

            // --- launch
            Rect btn = new Rect(r.x, launchTop, r.width, btnH);
            hoverLaunch = btn.Contains(Event.current.mousePosition);
            DrawLaunch(btn);
        }

        private void SessionRow(Rect r, string key, string value)
        {
            Theme.Text(new Rect(r.x, r.y, r.width * 0.55f, r.height), key, Theme.Body);
            Theme.Text(new Rect(r.x + r.width * 0.45f, r.y, r.width * 0.55f, r.height),
                value, Theme.Styled(Theme.Body, Theme.Ink, TextAnchor.MiddleRight));
        }

        /// <summary>
        /// A model's readiness, stated before the stint rather than discovered during it. Three
        /// states, because "running" and "running the trained policy" are not the same claim.
        /// </summary>
        private void ModelRow(Rect r, string key, bool present, bool trained)
        {
            Theme.Text(new Rect(r.x, r.y, r.width * 0.55f, r.height), key, Theme.Body);

            string state = !present ? "OFFLINE" : (trained ? "READY" : "HEURISTIC");
            Color ink = !present ? Theme.Caution : (trained ? Theme.Ok : Theme.Caution);

            Theme.Text(new Rect(r.x + r.width * 0.45f, r.y, r.width * 0.55f - Theme.U(16f), r.height),
                state, Theme.Styled(Theme.Label, ink, TextAnchor.MiddleRight));

            if (present && trained)
                Theme.Tick(new Rect(r.xMax - Theme.U(12f), r.y + r.height * 0.5f - Theme.U(5f),
                    Theme.U(10f), Theme.U(10f)), Theme.Ok);
        }

        private void DrawCircuitRow(Rect row, CircuitType c, bool hover)
        {
            bool selected = c == selectedCircuit;
            CircuitPlate.Data d = CircuitPlate.Get(c);

            if (selected) Theme.Fill(row, Theme.Rule);
            else if (hover) Theme.Fill(row, new Color(Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 0.45f));

            float pad = Theme.U(12f);
            Color ink = selected ? Theme.InkHi : (hover ? Theme.Ink : Theme.Ink);

            Theme.Text(new Rect(row.x + pad, row.y + Theme.U(6f), row.width * 0.62f, Theme.U(20f)),
                CircuitName(c).ToUpperInvariant(), Theme.Tint(Theme.Value, ink));
            Theme.Text(new Rect(row.x + pad, row.y + Theme.U(26f), row.width * 0.62f, Theme.U(14f)),
                CircuitRegion(c), Theme.Micro);

            
            Theme.Text(new Rect(row.xMax - Theme.U(96f), row.y + Theme.U(6f), Theme.U(74f), Theme.U(20f)),
                (d.LengthM / 1000f).ToString("F3") + " km",
                Theme.Styled(Theme.Body, selected ? Theme.InkHi : Theme.Ink, TextAnchor.MiddleRight));
            
            Theme.Text(new Rect(row.xMax - Theme.U(96f), row.y + Theme.U(26f), Theme.U(74f), Theme.U(14f)),
                d.CornerIndex.Length + " corners", Theme.Align(Theme.Micro, TextAnchor.MiddleRight));

            if (selected)
                Theme.Tick(new Rect(row.xMax - Theme.U(18f), row.y + row.height * 0.5f - Theme.U(6f),
                    Theme.U(12f), Theme.U(12f)), Theme.Command);

            if (GUI.Button(row, GUIContent.none, GUIStyle.none)) SelectCircuit(c);
            Theme.HRule(row.x, row.yMax, row.width, new Color(Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 0.5f));
        }

        private void DrawCompoundRow(Rect row, TyreCompound c, bool hover)
        {
            bool selected = c == selectedCompound;

            if (selected) Theme.Fill(row, Theme.Rule);
            else if (hover) Theme.Fill(row, new Color(Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 0.45f));

            float pad = Theme.U(12f);
            Color band = Theme.Compound(c);

            // the sidewall band: the real colour code, used as identification not decoration
            Theme.Fill(new Rect(row.x + pad, row.y + row.height * 0.5f - Theme.U(11f), Theme.U(4f), Theme.U(22f)), band);

            Theme.Text(new Rect(row.x + pad + Theme.U(14f), row.y + Theme.U(8f), row.width * 0.5f, Theme.U(20f)),
                Theme.CompoundName(c) + "  " + Theme.CompoundCode(c),
                Theme.Tint(Theme.Value, selected ? Theme.InkHi : Theme.Ink));

            Theme.Text(new Rect(row.x + pad + Theme.U(14f), row.y + Theme.U(29f), row.width * 0.7f, Theme.U(14f)),
                CompoundNote(c), Theme.Micro);

            
            Theme.Text(new Rect(row.xMax - Theme.U(96f), row.y + Theme.U(8f), Theme.U(74f), Theme.U(16f)),
                "GRIP " + CompoundGrip(c), Theme.Align(Theme.Micro, TextAnchor.MiddleRight));
            Theme.Text(new Rect(row.xMax - Theme.U(96f), row.y + Theme.U(28f), Theme.U(74f), Theme.U(16f)),
                "WEAR " + CompoundWear(c), Theme.Align(Theme.Micro, TextAnchor.MiddleRight));

            if (selected)
                Theme.Tick(new Rect(row.xMax - Theme.U(18f), row.y + row.height * 0.5f - Theme.U(6f),
                    Theme.U(12f), Theme.U(12f)), Theme.Command);

            if (GUI.Button(row, GUIContent.none, GUIStyle.none)) SelectCompound(c);
            Theme.HRule(row.x, row.yMax, row.width, new Color(Theme.Rule.r, Theme.Rule.g, Theme.Rule.b, 0.5f));
        }

        private void DrawLaunch(Rect btn)
        {
            Theme.Fill(btn, hoverLaunch ? Theme.Command : Theme.Ground);
            if (!hoverLaunch)
            {
                Theme.HRule(btn.x, btn.y, btn.width, Theme.Command);
                Theme.HRule(btn.x, btn.yMax - Mathf.Max(1f, Theme.S), btn.width, Theme.Command);
                Theme.VRule(btn.x, btn.y, btn.height, Theme.Command);
                Theme.VRule(btn.xMax - Mathf.Max(1f, Theme.S), btn.y, btn.height, Theme.Command);
            }

            Color ink = hoverLaunch ? Theme.Ground : Theme.InkHi;
            GUIStyle c = Theme.Align(Theme.Value, TextAnchor.MiddleCenter);
            Theme.Text(btn, "BEGIN STINT", Theme.Tint(c, ink));

            Theme.Chevron(new Rect(btn.xMax - Theme.U(30f), btn.center.y - Theme.U(7f), Theme.U(9f), Theme.U(14f)),
                0, ink);

            if (GUI.Button(btn, GUIContent.none, GUIStyle.none)) BeginStint();
        }

        private void DrawControlLegend(Rect r)
        {
            // Only advertise the pit wall when it actually exists. A legend that promises a key
            // which does nothing is worse than no legend. Polled on a timer, never per frame:
            // OnGUI runs at least twice a frame and a scene-wide search is not free.
            if (Time.unscaledTime - pitWallCheckedAt > 1.5f)
            {
                pitWallCheckedAt = Time.unscaledTime;
                pitWallPresent = UnityEngine.Object.FindFirstObjectByType<PitWallDashboardController>() != null;
            }
            bool pitWall = pitWallPresent;

            string[,] keys =
            {
                { "W A S D", "drive" },
                { "SHIFT", "MGU-K deploy" },
                { "S", "regen harvest" },
                { "F", "DRS" },
                { pitWall ? "P" : "--", pitWall ? "pit wall" : "pit wall unavailable" },
                { "R", "reset to pole" }
            };

            float col = r.width / keys.GetLength(0);
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                float x = r.x + i * col;
                Theme.Text(new Rect(x, r.y, col, Theme.U(16f)), keys[i, 0], Theme.Tint(Theme.Label, Theme.Ink));
                Theme.Text(new Rect(x, r.y + Theme.U(15f), col, Theme.U(14f)), keys[i, 1], Theme.Micro);
            }
        }

        // -------------------------------------------------------------------- in-race overlay

        private void DrawOverlay()
        {
            float W = Screen.width, H = Screen.height;
            float m = Theme.U(22f);

            DrawTimingStrip(new Rect(m, m, Theme.U(330f), Theme.U(106f)));
            DrawCommandStrip(new Rect(W - m - Theme.U(400f), m, Theme.U(400f), Theme.U(106f)));
            DrawTyreEnergyRail(new Rect(m, m + Theme.U(122f), Theme.U(214f), Theme.U(230f)));
            DrawDriverStrip(new Rect(m, H - m - Theme.U(112f), Theme.U(320f), Theme.U(112f)));
            DrawMinimap(new Rect(W - m - Theme.U(230f), H - m - Theme.U(230f), Theme.U(230f), Theme.U(230f)));
        }

        /// <summary>
        /// How loud a panel is. Rank is carried by plate density and title ink; giving every panel
        /// an identical frame and an identical title weight is flatness, not a system.
        /// </summary>
        private enum Rank
        {
            Lead,       // the model's recommendation: the one thing this product exists to say
            Primary,    // acted on continuously while driving
            Reference   // consulted, not watched
        }

        /// <summary>
        /// A panel is a plate and a title. It earns a rule under that title only where a command
        /// reading sits against an actual reading — the one coordinate-system boundary on this
        /// surface, and the only enclosure the world allows.
        /// </summary>
        private Rect Panel(Rect r, string title, string status, Color statusInk, Rank rank)
        {
            Theme.Plate(r, rank == Rank.Lead ? 0.97f : (rank == Rank.Primary ? 0.93f : 0.86f));
            float pad = Theme.U(10f);
            float head = Theme.U(16f);

            Color titleInk = rank == Rank.Lead ? Theme.Ink : Theme.InkDim;
            Theme.Text(new Rect(r.x + pad, r.y + Theme.U(6f), r.width * 0.56f, head),
                title, Theme.Tint(Theme.Label, titleInk));

            if (!string.IsNullOrEmpty(status))
            {
                Theme.Text(new Rect(r.x + r.width * 0.44f, r.y + Theme.U(6f), r.width * 0.56f - pad, head),
                    status, Theme.Styled(Theme.Micro, statusInk, TextAnchor.MiddleRight));
            }

            float ruleY = r.y + Theme.U(24f);
            if (rank == Rank.Lead) Theme.HRule(r.x + pad, ruleY, r.width - pad * 2f, Theme.Command);
            return new Rect(r.x + pad, ruleY + Theme.U(7f), r.width - pad * 2f, r.yMax - ruleY - Theme.U(14f));
        }

        private void DrawTimingStrip(Rect r)
        {
            Rect body = Panel(r, "LAP " + CurrentLapNumber + " / " + plannedStintLaps, "", Theme.InkDim, Rank.Primary);

            Theme.Text(new Rect(body.x, body.y, body.width * 0.62f, Theme.U(40f)),
                Theme.LapTime(CurrentLapTime), Theme.Hero);

            float delta = BestLapTime > 0f ? CurrentLapTime - BestLapTime : 0f;
            
            if (BestLapTime > 0f)
            {
                Theme.Text(new Rect(body.xMax - Theme.U(96f), body.y + Theme.U(2f), Theme.U(96f), Theme.U(20f)),
                    Theme.Delta(delta), Theme.Styled(Theme.Value, Theme.DeltaInk(delta), TextAnchor.MiddleRight));
                
                Theme.Text(new Rect(body.xMax - Theme.U(96f), body.y + Theme.U(22f), Theme.U(96f), Theme.U(14f)),
                    "BEST " + Theme.LapTime(BestLapTime), Theme.Align(Theme.Micro, TextAnchor.MiddleRight));
            }
            else
            {
                
                Theme.Text(new Rect(body.xMax - Theme.U(110f), body.y + Theme.U(12f), Theme.U(110f), Theme.U(14f)),
                    "NO REFERENCE LAP YET", Theme.Align(Theme.Micro, TextAnchor.MiddleRight));
            }

            float chipW = (body.width - Theme.U(8f)) / 3f;
            float chipY = body.yMax - Theme.U(20f);
            SectorChip(new Rect(body.x, chipY, chipW, Theme.U(18f)), 1, S1Time, BestS1Time);
            SectorChip(new Rect(body.x + chipW + Theme.U(4f), chipY, chipW, Theme.U(18f)), 2, S2Time, BestS2Time);
            SectorChip(new Rect(body.x + (chipW + Theme.U(4f)) * 2f, chipY, chipW, Theme.U(18f)), 3, S3Time, BestS3Time);
        }

        private void SectorChip(Rect r, int sector, float time, float best)
        {
            bool active = ActiveSector == sector;
            Color ink;
            string text;

            if (active)
            {
                ink = Theme.InkHi;
                text = CurrentSectorTime.ToString("00.0");
            }
            else if (time > 0f)
            {
                ink = (best > 0f && time <= best + 0.0005f) ? Theme.SectorPersonal : Theme.SectorSlower;
                text = Theme.Split(time);
            }
            else
            {
                ink = Theme.InkDim;
                text = "--.---";
            }

            Theme.Fill(new Rect(r.x, r.y, r.width, Mathf.Max(1f, Theme.U(2f))), active ? Theme.InkHi : ink);
            Theme.Text(new Rect(r.x, r.y + Theme.U(3f), r.width, Theme.U(15f)),
                "S" + sector, Theme.Micro);
            
            Theme.Text(new Rect(r.x, r.y + Theme.U(3f), r.width, Theme.U(15f)), text, Theme.Styled(Theme.Micro, ink, TextAnchor.MiddleRight));
        }

        /// <summary>
        /// Track 1's output, given the space its importance deserves. This is the product's
        /// headline: what the policy commands, against what the driver is actually doing.
        /// </summary>
        private void DrawCommandStrip(Rect r)
        {
            bool live = HasModels;
            bool trained = Track1ModelLoaded;
            Rect body = Panel(r, "TRACK 1 · ENERGY DEPLOYMENT",
                live ? (trained ? "PPO · LIVE" : "HEURISTIC") : "MODEL OFFLINE",
                live && trained ? Theme.Ok : Theme.Caution, Rank.Lead);

            if (!live)
            {
                Theme.Text(new Rect(body.x, body.y, body.width, Theme.U(20f)),
                    "No inference running.", Theme.Tint(Theme.Body, Theme.Caution));
                Theme.Text(new Rect(body.x, body.y + Theme.U(22f), body.width, Theme.U(34f)),
                    "Add SimulationCore and Track1InferenceRunner\nto the scene to receive recommendations.",
                    Theme.Micro);
                return;
            }

            EnergyDeploymentExplainability e = Energy;

            string actual = car != null && car.IsBoostActive ? "PUSH"
                : (car != null && car.IsRegenerating ? "SAVE" : "BALANCED");

            // BALANCED is the longest mode and the default one: at 46u bold it needs roughly
            // 221u, so the box is sized for it rather than for PUSH.
            Theme.Text(new Rect(body.x, body.y, Theme.U(70f), Theme.U(13f)), "COMMAND", Theme.Label);
            Theme.Text(new Rect(body.x, body.y + Theme.U(11f), Theme.U(252f), Theme.U(40f)),
                e.RecommendedDeploymentMode.ToString().ToUpperInvariant(),
                Theme.Tint(Theme.Hero, Theme.Command));

            Theme.Text(new Rect(body.x + Theme.U(258f), body.y, Theme.U(112f), Theme.U(13f)), "ACTUAL", Theme.Label);
            Theme.Text(new Rect(body.x + Theme.U(258f), body.y + Theme.U(13f), Theme.U(112f), Theme.U(22f)),
                actual, Theme.Tint(Theme.Value, Theme.InkHi));

            float risk = dRisk.Track(e.OvertakeRiskRewardScore);
            
            Theme.Text(new Rect(body.x + Theme.U(258f), body.y + Theme.U(36f), Theme.U(112f), Theme.U(12f)),
                "OVERTAKE RISK", Theme.Label);
            Theme.Text(new Rect(body.x + Theme.U(258f), body.y + Theme.U(46f), Theme.U(112f), Theme.U(20f)),
                risk.ToString("F0") + " / 100",
                Theme.Styled(Theme.Body, RiskInk(e.RiskCategory), TextAnchor.MiddleLeft));

            Theme.HRule(body.x, body.y + Theme.U(50f), body.width, Theme.Rule);
            string why = string.IsNullOrEmpty(e.ExplanationSummary)
                ? "No rationale returned by the policy."
                : e.ExplanationSummary;
            Theme.Wrapped(new Rect(body.x, body.y + Theme.U(54f), body.width, Theme.U(22f)), why, Theme.Micro);
        }

        private static Color RiskInk(OvertakeRiskCategory c)
        {
            if (c == OvertakeRiskCategory.Critical) return Theme.Warning;
            if (c == OvertakeRiskCategory.High) return Theme.Caution;
            if (c == OvertakeRiskCategory.Moderate) return Theme.Ink;
            return Theme.Ok;
        }

        /// <summary>
        /// Tyre and energy in one rail, because they are one system. The wear-rate line at the
        /// bottom is the coupling itself: deployment aggressiveness feeding the wear forecast.
        /// </summary>
        private void DrawTyreEnergyRail(Rect r)
        {
            Rect body = Panel(r, "TYRE · ENERGY",
                Theme.CompoundName(selectedCompound) + " " + Theme.CompoundCode(selectedCompound),
                Theme.Compound(selectedCompound), Rank.Reference);

            float fl = car != null ? car.TyreWearFL : 0f;
            float fr = car != null ? car.TyreWearFR : 0f;
            float rl = car != null ? car.TyreWearRL : 0f;
            float rr = car != null ? car.TyreWearRR : 0f;

            float cw = (body.width - Theme.U(10f)) * 0.5f;
            float ch = Theme.U(30f);
            Corner(new Rect(body.x, body.y, cw, ch), "FL", fl);
            Corner(new Rect(body.x + cw + Theme.U(10f), body.y, cw, ch), "FR", fr);
            Corner(new Rect(body.x, body.y + ch + Theme.U(4f), cw, ch), "RL", rl);
            Corner(new Rect(body.x + cw + Theme.U(10f), body.y + ch + Theme.U(4f), cw, ch), "RR", rr);

            float y = body.y + ch * 2f + Theme.U(16f);
            Theme.HRule(body.x, y, body.width, Theme.Rule);
            y += Theme.U(8f);

            // energy: actual against the physics baseline for this point in the stint
            float soc = dBattery.Track(car != null ? car.BatteryEnergyPct : 100f);
            float target = car != null ? car.OptimalBatteryEnergyPct : 100f;

            Theme.Text(new Rect(body.x, y, Theme.U(120f), Theme.U(14f)), "ERS STATE OF CHARGE", Theme.Label);
            
            Theme.Text(new Rect(body.xMax - Theme.U(60f), y - Theme.U(2f), Theme.U(60f), Theme.U(20f)),
                soc.ToString("F0") + "%", Theme.Styled(Theme.Value, Theme.Energy, TextAnchor.MiddleRight));
            // The tick is the physics baseline, not a model command, so it is drawn in Ink.
            // Command magenta is reserved for output that genuinely came from a model.
            Theme.Bar(new Rect(body.x, y + Theme.U(20f), body.width, Theme.U(7f)), soc / 100f, Theme.Energy);
            float bx = body.x + Mathf.Clamp01(target / 100f) * body.width;
            Theme.Fill(new Rect(bx - Mathf.Max(1f, Theme.U(1f)), y + Theme.U(18f),
                Mathf.Max(2f, Theme.U(2f)), Theme.U(11f)), Theme.Ink);

            float dev = soc - target;
            Theme.Text(new Rect(body.x, y + Theme.U(30f), body.width, Theme.U(14f)),
                "BASELINE " + target.ToString("F0") + "%   " +
                (dev >= 0f ? "+" : "") + dev.ToString("F0") + " vs heuristic",
                Theme.Tint(Theme.Micro, Mathf.Abs(dev) > 12f ? Theme.Caution : Theme.InkDim));

            // the coupling, stated. TyreWearRateCurrent is the field the energy risk calculation
            // reads, so this line is literally the link between the two models.
            y += Theme.U(50f);
            Theme.HRule(body.x, y, body.width, Theme.Rule);
            Theme.Text(new Rect(body.x, y + Theme.U(6f), Theme.U(86f), Theme.U(14f)),
                "DEPLOYMENT", Theme.Label);
            Theme.Chevron(new Rect(body.x + Theme.U(88f), y + Theme.U(8f), Theme.U(6f), Theme.U(9f)),
                0, Theme.InkDim);
            Theme.Text(new Rect(body.x + Theme.U(100f), y + Theme.U(6f), body.width - Theme.U(100f), Theme.U(14f)),
                "WEAR RATE", Theme.Label);
            if (HasModels)
            {
                float rate = SimulationCore.Instance.State.TyreWearRateCurrent;
                Theme.Text(new Rect(body.x, y + Theme.U(20f), body.width, Theme.U(16f)),
                    rate.ToString("F2") + " %/lap feeding energy risk", Theme.Micro);
            }
            else
            {
                Theme.Text(new Rect(body.x, y + Theme.U(20f), body.width, Theme.U(16f)),
                    "not computed - model offline", Theme.Tint(Theme.Micro, Theme.Caution));
            }
        }

        private void Corner(Rect r, string label, float wearPct)
        {
            Theme.Text(new Rect(r.x, r.y, Theme.U(22f), Theme.U(14f)), label, Theme.Label);
            
            Theme.Text(new Rect(r.xMax - Theme.U(40f), r.y, Theme.U(40f), Theme.U(14f)),
                wearPct.ToString("F1") + "%", Theme.Styled(Theme.Micro, Theme.WearInk(wearPct), TextAnchor.MiddleRight));
            Theme.Bar(new Rect(r.x, r.y + Theme.U(16f), r.width, Theme.U(5f)),
                wearPct / 100f, Theme.WearInk(wearPct));
        }

        private void DrawDriverStrip(Rect r)
        {
            Rect body = Panel(r, "DRIVER", "", Theme.InkDim, Rank.Primary);

            float speed = dSpeed.Track(car != null ? car.CurrentSpeedKmh : 0f, 14f);
            DrawRevStrip(new Rect(body.x, body.y, body.width, Theme.U(6f)));

            Theme.Text(new Rect(body.x, body.y + Theme.U(12f), Theme.U(150f), Theme.U(44f)),
                speed.ToString("F0"), Theme.Hero);
            Theme.Text(new Rect(body.x + Theme.U(112f), body.y + Theme.U(34f), Theme.U(50f), Theme.U(16f)),
                "KM/H", Theme.Micro);

            // gear
            string gear = "N";
            if (car != null)
            {
                if (car.CurrentGear == VehicleGear.Reverse) gear = "R";
                else if (car.CurrentGear == VehicleGear.Neutral) gear = "N";
                else gear = ((int)car.CurrentGear).ToString();
            }
            Rect gbox = new Rect(body.x + Theme.U(170f), body.y + Theme.U(16f), Theme.U(38f), Theme.U(38f));
            Theme.Fill(gbox, Theme.Rule);
            GUIStyle gc = Theme.Align(Theme.Value, TextAnchor.MiddleCenter);
            Theme.Text(gbox, gear, Theme.Tint(gc, Theme.InkHi));
            Theme.Text(new Rect(gbox.x, gbox.yMax + Theme.U(2f), gbox.width, Theme.U(12f)),
                "GEAR", Theme.Align(Theme.Micro, TextAnchor.MiddleCenter));

            // DRS
            bool drs = car != null && car.DrsToggle == DrsState.Open;
            bool avail = car != null && car.DrsToggle == DrsState.Available;
            Rect dbox = new Rect(body.xMax - Theme.U(64f), body.y + Theme.U(12f), Theme.U(64f), Theme.U(28f));
            Theme.Fill(dbox, drs ? Theme.Ok : Theme.Rule);
            GUIStyle dc = Theme.Align(Theme.Label, TextAnchor.MiddleCenter);
            // InkDim is 4.11:1 on a Rule fill, under the floor, so the resting state uses Ink
            Theme.Text(dbox, "DRS", Theme.Tint(dc, drs ? Theme.Ground : (avail ? Theme.Ok : Theme.Ink)));
            Theme.Text(new Rect(dbox.x, dbox.yMax + Theme.U(4f), dbox.width, Theme.U(14f)),
                drs ? "OPEN" : (avail ? "ARMED" : "CLOSED"),
                Theme.Align(Theme.Micro, TextAnchor.MiddleCenter));
        }

        /// <summary>
        /// Shift lights driven by position within the current gear's speed band, not by absolute
        /// speed. The previous build lit them from raw km/h, so they read as a speedometer.
        /// </summary>
        private void DrawRevStrip(Rect r)
        {
            const int leds = 16;
            float gap = Theme.U(2f);
            float w = (r.width - (leds - 1) * gap) / leds;

            float ratio = 0f;
            if (car != null)
            {
                int g = Mathf.Clamp((int)car.CurrentGear, 1, 8);
                float bandLow = (g - 1) * 42f;
                float bandHigh = g * 42f + 28f;
                ratio = Mathf.Clamp01((car.CurrentSpeedKmh - bandLow) / Mathf.Max(1f, bandHigh - bandLow));
            }
            int lit = Mathf.RoundToInt(ratio * leds);

            for (int i = 0; i < leds; i++)
            {
                Color on = i < 6 ? Theme.Ok : (i < 12 ? Theme.Caution : Theme.Warning);
                Color c = i < lit ? on : new Color(on.r, on.g, on.b, 0.16f);
                Theme.Fill(new Rect(r.x + i * (w + gap), r.y, w, r.height), c);
            }
        }

        private void DrawMinimap(Rect r)
        {
            if (plate == null || plate.Norm.Length < 2) return;

            Rect body = Panel(r, CircuitName(selectedCircuit).ToUpperInvariant(),
                "S" + ActiveSector, CircuitPlate.SectorInk(plate, SectorSample()), Rank.Reference);

            float side = Mathf.Min(body.width, body.height);
            Rect map = new Rect(body.x + (body.width - side) * 0.5f, body.y + (body.height - side) * 0.5f, side, side);

            int texSize = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.RoundToInt(side)), 128, 512);
            Texture2D outline = CircuitPlate.Outline(selectedCircuit, texSize, Mathf.Max(1.6f, texSize / 150f));
            GUI.DrawTexture(map, outline, ScaleMode.StretchToFill, true);

            Vector2 sf = CircuitPlate.ToRect(plate.Norm[0], map);
            float s = Theme.U(6f);
            Theme.Fill(new Rect(sf.x - s * 0.5f, sf.y - s * 0.5f, s, s), Theme.InkHi);

            if (car != null)
            {
                Vector2 p = CircuitPlate.WorldToRect(plate, car.transform.position, map);
                float halo = Theme.U(11f);
                Color h = Theme.Command;
                h.a = 0.22f;
                Theme.Fill(new Rect(p.x - halo * 0.5f, p.y - halo * 0.5f, halo, halo), h);
                float b = Theme.U(5f);
                Theme.Fill(new Rect(p.x - b * 0.5f, p.y - b * 0.5f, b, b), Theme.Command);
            }
        }

        private int SectorSample()
        {
            if (plate == null || plate.Norm.Length == 0) return 0;
            if (ActiveSector == 1) return 0;
            if (ActiveSector == 2) return plate.SectorEnd1 + 1;
            return plate.SectorEnd2 + 1;
        }

        // ==================================================================== copy

        private static string CircuitName(CircuitType c)
        {
            if (c == CircuitType.Bahrain) return "Bahrain";
            if (c == CircuitType.Spa) return "Spa-Francorchamps";
            if (c == CircuitType.Monaco) return "Monaco";
            return "Monza";
        }

        private static string CircuitRegion(CircuitType c)
        {
            if (c == CircuitType.Bahrain) return "Sakhir · desert, night race";
            if (c == CircuitType.Spa) return "Ardennes · Eau Rouge, Pouhon";
            if (c == CircuitType.Monaco) return "Monte Carlo · street, tunnel";
            return "Royal Park · temple of speed";
        }

        private static string CompoundNote(TyreCompound c)
        {
            if (c == TyreCompound.Soft) return "Peak grip, shortest life. Qualifying and short stints.";
            if (c == TyreCompound.Medium) return "The balanced race tyre. Widest useful window.";
            return "Lowest grip, longest life. Long stints and hot tracks.";
        }

        // Both read from PacejkaTyreModel's compound profiles. The previous build printed
        // 100/92/84 and 2.20/1.25/0.70, which matched no constant in the simulation.
        private static string CompoundGrip(TyreCompound c)
        {
            return Theme.RelativeGripPct(c) + "%";
        }

        private static string CompoundWear(TyreCompound c)
        {
            return Theme.WearMultiplier(c).ToString("0.00") + "x";
        }
    }
}
