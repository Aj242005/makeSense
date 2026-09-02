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
    /// F1TelemetryHUD delivers broadcast-grade Grand Prix simulation interface:
    /// - Starting Screen: Circuit Selector (Bahrain 5.412 km & Spa-Francorchamps 7.004 km) + Pirelli Tyre Selector (Soft, Medium, Hard).
    /// - In-Game HUD: 100% symmetrically center-aligned Timing Tower, Tyre Degradation, Speed/Gear Cluster, MGU-K SoC/Regen, and Telemetry Graphs.
    /// - 2D Tactical Radar GPS Minimap with live vehicle blip and sector split coloring.
    /// </summary>
    public class F1TelemetryHUD : MonoBehaviour
    {
        public static F1TelemetryHUD Instance { get; private set; }

        [Header("Game State")]
        [SerializeField] private SimGameState gameState = SimGameState.StartingScreen;

        [Header("Car Reference")]
        [SerializeField] private CarPhysicsController car;

        // Selected Circuit & Compound
        private CircuitType selectedCircuit = CircuitType.Bahrain;
        private CircuitType currentActiveCircuit = CircuitType.Bahrain;
        private TyreCompound selectedCompound = TyreCompound.Soft;

        public CircuitType SelectedCircuit => selectedCircuit;
        public List<Vector3> CachedCenterline => cachedCenterline;

        // --- Live Timing & Sector Analyzer State ---
        public int CurrentLapNumber { get; private set; } = 1;
        public float CurrentLapTime { get; private set; } = 0.0f;
        public float LastLapTime { get; private set; } = 0.0f;
        public float BestLapTime { get; private set; } = 0.0f;
        public float CurrentSectorTime { get; private set; } = 0.0f;
        public int ActiveSector { get; private set; } = 1;

        // Sector Split Times
        public float S1Time { get; private set; } = 0.0f;
        public float S2Time { get; private set; } = 0.0f;
        public float S3Time { get; private set; } = 0.0f;
        public float BestS1Time { get; private set; } = 28.450f;
        public float BestS2Time { get; private set; } = 38.620f;
        public float BestS3Time { get; private set; } = 23.180f;

        private Vector3 prevCarPos;
        private bool hasPassedS1 = false;
        private bool hasPassedS2 = false;
        private float s2StartTime = 0.0f;
        private float s3StartTime = 0.0f;

        // Rolling History for Live Telemetry Graphs (140 samples)
        private const int HISTORY_LEN = 140;
        private float[] speedHistory = new float[HISTORY_LEN];
        private float[] driverEnergyHistory = new float[HISTORY_LEN];
        private float[] optimalEnergyHistory = new float[HISTORY_LEN];
        private float[] driverWearHistory = new float[HISTORY_LEN];
        private float[] optimalWearHistory = new float[HISTORY_LEN];
        private int historyIndex = 0;
        private float sampleTimer = 0.0f;

        // 2D Minimap Spline Data
        private List<Vector3> cachedCenterline;
        private Vector2[] minimapPoints;
        private float minX = -350f, maxX = 750f, minZ = -220f, maxZ = 1250f;

        // Cached Textures & GUI Styles
        private Texture2D whitePixel;
        private GUIStyle speedNumberStyle;
        private GUIStyle speedUnitStyle;
        private GUIStyle gearStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardValueStyle;
        private GUIStyle badgeStyle;
        private GUIStyle legendStyle;
        private GUIStyle smallTextStyle;
        private GUIStyle timingTimeStyle;

        // Menu Styles
        private GUIStyle menuTitleStyle;
        private GUIStyle menuSubtitleStyle;
        private GUIStyle menuHeaderStyle;
        private GUIStyle menuButtonStyle;
        private GUIStyle menuDescStyle;

        private void Awake()
        {
            Instance = this;
            InitializeTextures();
            GenerateMinimapSplineData(selectedCircuit);
        }

        private void Start()
        {
            FindCar();
            if (car != null)
            {
                int startIdx = (selectedCircuit == CircuitType.Monza) ? 0 : 20;
                Vector3 spawnPos = (cachedCenterline != null && cachedCenterline.Count > startIdx + 2) ? cachedCenterline[startIdx] : new Vector3(0.0f, 0.0f, 0.0f);
                Vector3 spawnNext = (cachedCenterline != null && cachedCenterline.Count > startIdx + 2) ? cachedCenterline[startIdx + 1] : (spawnPos + Vector3.forward * 10f);
                Vector3 fwdDir = (spawnNext - spawnPos).normalized;
                Vector3 rightDir = Vector3.Cross(Vector3.up, fwdDir).normalized;

                Vector3 polePos = spawnPos - fwdDir * 6.0f + rightDir * 2.5f + Vector3.up * 0.564f;
                Quaternion poleRot = Quaternion.LookRotation(fwdDir, Vector3.up);

                car.TeleportVehicle(polePos, poleRot);
                prevCarPos = polePos;
                car.SetTyreCompound(selectedCompound);
            }

            for (int i = 0; i < HISTORY_LEN; i++)
            {
                speedHistory[i] = 0f;
                driverEnergyHistory[i] = 100f;
                optimalEnergyHistory[i] = 100f;
                driverWearHistory[i] = 0f;
                optimalWearHistory[i] = 0f;
            }
        }

        private void FindCar()
        {
            if (car == null)
            {
                car = FindFirstObjectByType<CarPhysicsController>(FindObjectsInactive.Include);
                if (car != null) prevCarPos = car.transform.position;
            }
        }

        private void Update()
        {
            if (car == null) FindCar();

            // ESC key toggles between Menu and Racing
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                gameState = (gameState == SimGameState.StartingScreen) ? SimGameState.Racing : SimGameState.StartingScreen;
            }

            if (gameState == SimGameState.StartingScreen)
            {
                if (car != null)
                {
                    car.ThrottleInput = 0.0f;
                    car.BrakeInput = 1.0f;
                    car.SteeringInput = 0.0f;
                }
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.0f) return;

            // 1. Advance Lap & Sector Timing
            UpdateLapAndSectorTiming(dt);

            // 2. Sample Telemetry History (~22 Hz)
            sampleTimer += dt;
            if (sampleTimer >= 0.045f)
            {
                sampleTimer = 0.0f;
                if (car != null)
                {
                    speedHistory[historyIndex] = car.CurrentSpeedKmh;
                    driverEnergyHistory[historyIndex] = car.BatteryEnergyPct;
                    optimalEnergyHistory[historyIndex] = car.OptimalBatteryEnergyPct;
                    driverWearHistory[historyIndex] = car.TyreWearPct;
                    optimalWearHistory[historyIndex] = car.OptimalTyreWearPct;
                }
                historyIndex = (historyIndex + 1) % HISTORY_LEN;
            }

            // 3. Reset Timing if car resets (R key)
            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                CurrentLapTime = 0.0f;
                CurrentSectorTime = 0.0f;
                ActiveSector = 1;
                hasPassedS1 = false;
                hasPassedS2 = false;
                if (car != null)
                {
                    car.transform.position = new Vector3(3.0f, 0.564f, 140.0f);
                    car.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
                    car.ResetVehicleState();
                }
            }
        }

        private void UpdateLapAndSectorTiming(float dt)
        {
            if (car == null) return;

            Vector3 curPos = car.transform.position;

            if (car.CurrentSpeedKmh > 1.0f || CurrentLapTime > 0.0f)
            {
                CurrentLapTime += dt;
                CurrentSectorTime += dt;
            }

            if (cachedCenterline != null && cachedCenterline.Count > 10)
            {
                int n = cachedCenterline.Count;
                int closestIdx = 0;
                float closestSqrDist = float.MaxValue;

                // Find closest waypoint to car
                for (int i = 0; i < n; i += 3)
                {
                    float dx = cachedCenterline[i].x - curPos.x;
                    float dz = cachedCenterline[i].z - curPos.z;
                    float sqrD = dx * dx + dz * dz;
                    if (sqrD < closestSqrDist)
                    {
                        closestSqrDist = sqrD;
                        closestIdx = i;
                    }
                }

                float progress = (float)closestIdx / n; // 0.0 to 1.0

                // Sector 1 -> Sector 2 Gate
                float s1Thresh = (selectedCircuit == CircuitType.Monza) ? 0.32f : 0.30f;
                float s2Thresh = (selectedCircuit == CircuitType.Monaco) ? 0.65f : ((selectedCircuit == CircuitType.Monza) ? 0.70f : 0.72f);

                if (ActiveSector == 1 && progress >= s1Thresh && progress <= (s1Thresh + 0.20f))
                {
                    hasPassedS1 = true;
                    ActiveSector = 2;
                    S1Time = CurrentLapTime;
                    s2StartTime = CurrentLapTime;
                    CurrentSectorTime = 0.0f;
                }
                // Sector 2 -> Sector 3 Gate
                else if (ActiveSector == 2 && progress >= s2Thresh && progress <= (s2Thresh + 0.18f))
                {
                    hasPassedS2 = true;
                    ActiveSector = 3;
                    S2Time = CurrentLapTime - s2StartTime;
                    s3StartTime = CurrentLapTime;
                    CurrentSectorTime = 0.0f;
                }

                // Sector 3 -> Next Lap Gate (Physically Crossing Start/Finish Line Plane)
                int sfIdx = (selectedCircuit == CircuitType.Monza) ? 0 : 20;
                Vector3 sfPos = cachedCenterline[sfIdx];
                Vector3 sfNext = (sfIdx < n - 1) ? cachedCenterline[sfIdx + 1] : cachedCenterline[0];
                Vector3 sfFwd = (sfNext - sfPos).normalized;

                // Signed distance to the Start/Finish line plane
                float prevSignedDist = Vector3.Dot(prevCarPos - sfPos, sfFwd);
                float currSignedDist = Vector3.Dot(curPos - sfPos, sfFwd);

                // Check if car physically crossed the finish line from behind (prev <= 1.0m) to ahead (curr > 0.0m)
                if (ActiveSector == 3 && CurrentLapTime > 12.0f)
                {
                    bool crossedLine = (prevSignedDist <= 1.0f && currSignedDist > 0.0f && (currSignedDist - prevSignedDist) < 20.0f);
                    float lateralDist = Vector3.ProjectOnPlane(curPos - sfPos, sfFwd).magnitude;

                    if (crossedLine && lateralDist < 25.0f)
                    {
                        S3Time = CurrentLapTime - s3StartTime;
                        LastLapTime = CurrentLapTime;

                        if (BestLapTime == 0.0f || LastLapTime < BestLapTime)
                        {
                            BestLapTime = LastLapTime;
                        }

                        if (S1Time > 0.0f && (BestS1Time == 0.0f || S1Time < BestS1Time)) BestS1Time = S1Time;
                        if (S2Time > 0.0f && (BestS2Time == 0.0f || S2Time < BestS2Time)) BestS2Time = S2Time;
                        if (S3Time > 0.0f && (BestS3Time == 0.0f || S3Time < BestS3Time)) BestS3Time = S3Time;

                        CurrentLapNumber++;
                        CurrentLapTime = 0.0f;
                        CurrentSectorTime = 0.0f;
                        ActiveSector = 1;
                        hasPassedS1 = false;
                        hasPassedS2 = false;
                    }
                }
            }

            prevCarPos = curPos;
        }

        private void InitializeTextures()
        {
            whitePixel = new Texture2D(1, 1);
            whitePixel.SetPixel(0, 0, Color.white);
            whitePixel.Apply();
        }

        private void GenerateMinimapSplineData(CircuitType circuit)
        {
            if (circuit == CircuitType.Bahrain)
                cachedCenterline = TrackVisualBuilder.GenerateBahrainGrandPrixSpline();
            else if (circuit == CircuitType.Spa)
                cachedCenterline = TrackVisualBuilder.GenerateSpaFrancorchampsSpline();
            else if (circuit == CircuitType.Monza)
                cachedCenterline = TrackVisualBuilder.GenerateMonzaGrandPrixSpline();
            else
                cachedCenterline = TrackVisualBuilder.GenerateMonacoGrandPrixSpline();

            int targetCount = 130;
            float step = (float)cachedCenterline.Count / targetCount;
            List<Vector2> resampled = new List<Vector2>();

            minX = float.MaxValue; maxX = float.MinValue;
            minZ = float.MaxValue; maxZ = float.MinValue;

            for (int i = 0; i < targetCount; i++)
            {
                int idx = Mathf.Clamp(Mathf.RoundToInt(i * step), 0, cachedCenterline.Count - 1);
                Vector2 p = new Vector2(cachedCenterline[idx].x, cachedCenterline[idx].z);
                resampled.Add(p);

                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minZ) minZ = p.y;
                if (p.y > maxZ) maxZ = p.y;
            }
            resampled.Add(new Vector2(cachedCenterline[0].x, cachedCenterline[0].z));

            minX -= 40f; maxX += 40f;
            minZ -= 40f; maxZ += 40f;

            minimapPoints = resampled.ToArray();
        }

        private void SetupStyles()
        {
            if (speedNumberStyle != null) return;

            speedNumberStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = Color.white }
            };

            speedUnitStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.60f, 0.70f, 0.85f) }
            };

            gearStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.55f, 0.65f, 0.80f) }
            };

            cardValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            legendStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.70f, 0.78f, 0.90f) }
            };

            smallTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.60f, 0.68f, 0.78f) }
            };

            timingTimeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            menuTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            menuSubtitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.85f, 0.95f) }
            };

            menuHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.95f, 0.80f, 0.20f) }
            };

            menuButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            menuDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                normal = { textColor = new Color(0.70f, 0.78f, 0.88f) }
            };
        }

        private void OnGUI()
        {
            SetupStyles();

            int screenW = Screen.width;
            int screenH = Screen.height;

            if (gameState == SimGameState.StartingScreen)
            {
                DrawStartingScreen(screenW, screenH);
                return;
            }

            float totalW = Mathf.Min(screenW - 40.0f, 1180.0f);
            float startX = (screenW - totalW) * 0.5f;

            DrawCenteredTopBar(startX, totalW);
            DrawCenteredGraphs(startX, totalW);
            DrawTacticalMinimap(screenW, screenH);
        }

        private void DrawStartingScreen(int screenW, int screenH)
        {
            GUI.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), whitePixel);
            GUI.color = Color.white;

            float menuW = Mathf.Min(screenW - 40.0f, 920.0f);
            float menuH = Mathf.Min(screenH - 40.0f, 540.0f);
            float menuX = (screenW - menuW) * 0.5f;
            float menuY = (screenH - menuH) * 0.5f;

            Rect menuRect = new Rect(menuX, menuY, menuW, menuH);
            DrawGlassCard(menuRect, new Color(0.06f, 0.08f, 0.12f, 0.96f), new Color(0.28f, 0.42f, 0.65f, 0.90f));

            // 1. Header Title & Subtitle
            GUI.Label(new Rect(menuX, menuY + 14, menuW, 30), "🏎️ GRIDSENSE F1 SIMULATION", menuTitleStyle);
            GUI.Label(new Rect(menuX, menuY + 44, menuW, 18), "Select Circuit & Pirelli Tyre Compound to Begin Session", menuSubtitleStyle);

            DrawLine(new Vector2(menuX + 30, menuY + 66), new Vector2(menuX + menuW - 30, menuY + 66), new Color(0.25f, 0.35f, 0.50f, 0.6f), 1);

            // 2. Section A: Grand Prix Circuit Selector (4 Interactive Cards)
            float secY = menuY + 76.0f;
            GUI.Label(new Rect(menuX + 30, secY, menuW - 60, 20), "1. SELECT GRAND PRIX CIRCUIT", menuHeaderStyle);

            float trackSpacing = 10.0f;
            float trackCardW = (menuW - 60.0f - trackSpacing * 3.0f) / 4.0f;
            float trackCardH = 74.0f;
            float trackCardY = secY + 22.0f;

            // Track Card 1: Bahrain
            DrawCircuitOptionCard(
                new Rect(menuX + 30 + 0 * (trackCardW + trackSpacing), trackCardY, trackCardW, trackCardH),
                CircuitType.Bahrain,
                "CIRCUIT 1", "BAHRAIN GP",
                "5.412 km | 15 Turns\nSakhir Desert");

            // Track Card 2: Spa-Francorchamps
            DrawCircuitOptionCard(
                new Rect(menuX + 30 + 1 * (trackCardW + trackSpacing), trackCardY, trackCardW, trackCardH),
                CircuitType.Spa,
                "CIRCUIT 2", "SPA - ARDENNES",
                "7.004 km | 19 Turns\nEau Rouge & Pouhon");

            // Track Card 3: Monaco
            DrawCircuitOptionCard(
                new Rect(menuX + 30 + 2 * (trackCardW + trackSpacing), trackCardY, trackCardW, trackCardH),
                CircuitType.Monaco,
                "CIRCUIT 3", "MONACO GP",
                "3.337 km | 19 Turns\nCasino & Tunnel");

            // Track Card 4: Monza
            DrawCircuitOptionCard(
                new Rect(menuX + 30 + 3 * (trackCardW + trackSpacing), trackCardY, trackCardW, trackCardH),
                CircuitType.Monza,
                "CIRCUIT 4", "MONZA GP",
                "5.793 km | 11 Turns\nTemple of Speed");

            // 3. Section B: Tyre Compound Selection (3 Interactive Cards)
            float tyreSecY = trackCardY + trackCardH + 12.0f;
            GUI.Label(new Rect(menuX + 30, tyreSecY, menuW - 60, 20), "2. SELECT PIRELLI TYRE COMPOUND", menuHeaderStyle);

            float cardSpacing = 14.0f;
            float tyreCardW = (menuW - 60.0f - cardSpacing * 2) / 3.0f;
            float tyreCardH = 140.0f;
            float tyreCardY = tyreSecY + 24.0f;

            DrawTyreOptionCard(
                new Rect(menuX + 30 + 0 * (tyreCardW + cardSpacing), tyreCardY, tyreCardW, tyreCardH),
                TyreCompound.Soft,
                "P-ZERO RED", "SOFT (C3)",
                new Color(0.92f, 0.18f, 0.18f),
                "Grip: 100% (High Speed)",
                "Wear: High (2.2x Rate)",
                "For Attack & Hotlaps");

            DrawTyreOptionCard(
                new Rect(menuX + 30 + 1 * (tyreCardW + cardSpacing), tyreCardY, tyreCardW, tyreCardH),
                TyreCompound.Medium,
                "P-ZERO YELLOW", "MEDIUM (C2)",
                new Color(0.95f, 0.80f, 0.15f),
                "Grip: 92% (Balanced)",
                "Wear: Medium (1.25x Rate)",
                "Optimal Race Strategy");

            DrawTyreOptionCard(
                new Rect(menuX + 30 + 2 * (tyreCardW + cardSpacing), tyreCardY, tyreCardW, tyreCardH),
                TyreCompound.Hard,
                "P-ZERO WHITE", "HARD (C1)",
                new Color(0.90f, 0.92f, 0.96f),
                "Grip: 84% (Durable)",
                "Wear: Low (0.70x Rate)",
                "Long Distance Stint");

            // 4. Section C: Launch Action Button & Controls Reference
            float bottomY = tyreCardY + tyreCardH + 16.0f;
            Rect startBtnRect = new Rect(menuX + (menuW - 280.0f) * 0.5f, bottomY, 280.0f, 42.0f);

            GUI.color = new Color(0.18f, 0.85f, 0.38f);
            if (GUI.Button(startBtnRect, "START RACE SESSION ➔", menuButtonStyle))
            {
                StartSimulation();
            }
            GUI.color = Color.white;

            GUI.Label(new Rect(menuX, bottomY + 46.0f, menuW, 16), "Controls: WASD / Arrows to Drive  |  Hold Shift for MGU-K Boost  |  S/Down to Regen  |  F for DRS  |  ESC to Menu", smallTextStyle);
        }

        private void DrawCircuitOptionCard(Rect r, CircuitType cType, string badge, string name, string desc)
        {
            bool isSelected = (selectedCircuit == cType);
            Color borderCol = isSelected ? new Color(0.25f, 0.90f, 0.45f, 1.0f) : new Color(0.22f, 0.30f, 0.44f, 0.65f);
            Color bgCol = isSelected ? new Color(0.10f, 0.16f, 0.24f, 0.98f) : new Color(0.07f, 0.09f, 0.14f, 0.92f);

            DrawGlassCard(r, bgCol, borderCol);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                selectedCircuit = cType;
                GenerateMinimapSplineData(selectedCircuit);

                if (selectedCircuit != currentActiveCircuit)
                {
                    currentActiveCircuit = selectedCircuit;
                    GameObject envObj = GameObject.Find("EnvironmentManager") ?? GameObject.Find("Environment");
                    if (envObj != null)
                    {
                        TrackVisualBuilder.BuildCircuitVisuals(envObj.transform, selectedCircuit);
                    }
                }

                if (car != null)
                {
                    int startIdx = (selectedCircuit == CircuitType.Monza) ? 0 : 20;
                    Vector3 spawnPos = (cachedCenterline != null && cachedCenterline.Count > startIdx + 2) ? cachedCenterline[startIdx] : new Vector3(0.0f, 0.0f, 0.0f);
                    Vector3 spawnNext = (cachedCenterline != null && cachedCenterline.Count > startIdx + 2) ? cachedCenterline[startIdx + 1] : (spawnPos + Vector3.forward * 10f);
                    Vector3 fwdDir = (spawnNext - spawnPos).normalized;
                    Vector3 rightDir = Vector3.Cross(Vector3.up, fwdDir).normalized;

                    Vector3 polePos = spawnPos - fwdDir * 6.0f + rightDir * 2.5f + Vector3.up * 0.564f;
                    Quaternion poleRot = Quaternion.LookRotation(fwdDir, Vector3.up);

                    car.TeleportVehicle(polePos, poleRot);
                    prevCarPos = polePos;
                }
            }

            DrawPillBadge(new Rect(r.x + 10, r.y + 10, 72, 18), badge, isSelected ? new Color(0.20f, 0.70f, 1.0f) : new Color(0.25f, 0.32f, 0.42f));
            GUI.Label(new Rect(r.x + 90, r.y + 10, r.width - 100, 20), name, cardValueStyle);
            GUI.Label(new Rect(r.x + 10, r.y + 34, r.width - 20, 16), desc, menuDescStyle);

            if (isSelected)
            {
                DrawPillBadge(new Rect(r.x + r.width - 95, r.y + 50, 85, 18), "SELECTED ✓", new Color(0.20f, 0.85f, 0.40f));
            }
            else
            {
                DrawPillBadge(new Rect(r.x + r.width - 95, r.y + 50, 85, 18), "SELECT", new Color(0.18f, 0.24f, 0.35f));
            }
        }

        private void DrawTyreOptionCard(Rect r, TyreCompound comp, string tag, string title, Color col, string gripTxt, string wearTxt, string recTxt)
        {
            bool isSelected = (selectedCompound == comp);
            Color borderCol = isSelected ? new Color(0.25f, 0.90f, 0.45f, 1.0f) : new Color(0.22f, 0.30f, 0.44f, 0.65f);
            Color bgCol = isSelected ? new Color(0.10f, 0.15f, 0.22f, 0.98f) : new Color(0.07f, 0.09f, 0.14f, 0.92f);

            DrawGlassCard(r, bgCol, borderCol);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                selectedCompound = comp;
                if (car != null) car.SetTyreCompound(selectedCompound);
            }

            DrawPillBadge(new Rect(r.x + 10, r.y + 8, r.width - 20, 18), tag, col);
            GUI.Label(new Rect(r.x + 10, r.y + 30, r.width - 20, 20), title, cardValueStyle);
            GUI.Label(new Rect(r.x + 10, r.y + 54, r.width - 20, 15), $"• {gripTxt}", menuDescStyle);
            GUI.Label(new Rect(r.x + 10, r.y + 70, r.width - 20, 15), $"• {wearTxt}", menuDescStyle);
            GUI.Label(new Rect(r.x + 10, r.y + 86, r.width - 20, 15), $"• {recTxt}", new GUIStyle(menuDescStyle) { normal = { textColor = new Color(0.95f, 0.75f, 0.20f) } });

            if (isSelected)
            {
                DrawPillBadge(new Rect(r.x + 12, r.y + r.height - 22, r.width - 24, 16), "ACTIVE ✓", new Color(0.20f, 0.85f, 0.40f));
            }
            else
            {
                DrawPillBadge(new Rect(r.x + 12, r.y + r.height - 22, r.width - 24, 16), "CLICK TO SELECT", new Color(0.18f, 0.24f, 0.35f));
            }
        }

        private void StartSimulation()
        {
            // Rebuild track if circuit changed
            if (selectedCircuit != currentActiveCircuit)
            {
                currentActiveCircuit = selectedCircuit;
                GameObject envObj = GameObject.Find("EnvironmentManager") ?? GameObject.Find("Environment");
                if (envObj != null)
                {
                    TrackVisualBuilder.BuildCircuitVisuals(envObj.transform, selectedCircuit);
                }
            }

            GenerateMinimapSplineData(selectedCircuit);

            if (car != null)
            {
                int startIdx = (selectedCircuit == CircuitType.Monza) ? 0 : 20;
                Vector3 spawnPos = (cachedCenterline != null && cachedCenterline.Count > startIdx + 2) ? cachedCenterline[startIdx] : new Vector3(0.0f, 0.0f, 0.0f);
                Vector3 spawnNext = (cachedCenterline != null && cachedCenterline.Count > startIdx + 2) ? cachedCenterline[startIdx + 1] : (spawnPos + Vector3.forward * 10f);
                Vector3 fwdDir = (spawnNext - spawnPos).normalized;
                Vector3 rightDir = Vector3.Cross(Vector3.up, fwdDir).normalized;

                Vector3 polePos = spawnPos - fwdDir * 6.0f + rightDir * 2.5f + Vector3.up * 0.564f;
                Quaternion poleRot = Quaternion.LookRotation(fwdDir, Vector3.up);

                car.TeleportVehicle(polePos, poleRot);
                prevCarPos = polePos;
                car.SetTyreCompound(selectedCompound);
            }

            CurrentLapNumber = 1;
            CurrentLapTime = 0.0f;
            CurrentSectorTime = 0.0f;
            ActiveSector = 1;
            hasPassedS1 = false;
            hasPassedS2 = false;

            // Target best benchmarks
            if (selectedCircuit == CircuitType.Bahrain)
            {
                BestS1Time = 28.450f;
                BestS2Time = 38.620f;
                BestS3Time = 23.180f;
            }
            else if (selectedCircuit == CircuitType.Spa)
            {
                BestS1Time = 30.150f;
                BestS2Time = 46.420f;
                BestS3Time = 28.250f;
            }
            else if (selectedCircuit == CircuitType.Monza)
            {
                BestS1Time = 26.850f;
                BestS2Time = 27.420f;
                BestS3Time = 25.830f;
            }
            else // Monaco
            {
                BestS1Time = 18.250f;
                BestS2Time = 32.180f;
                BestS3Time = 19.850f;
            }

            gameState = SimGameState.Racing;
        }

        private void DrawCenteredTopBar(float startX, float totalW)
        {
            float startY = 12.0f;
            float rowH = 82.0f;
            float spacing = 10.0f;
            float colW = (totalW - 3.0f * spacing) / 4.0f;

            // --- COLUMN 1: Timing & Lap Analyzer ---
            Rect col1 = new Rect(startX + 0 * (colW + spacing), startY, colW, rowH);
            DrawGlassCard(col1, new Color(0.05f, 0.07f, 0.11f, 0.92f), new Color(0.24f, 0.36f, 0.54f, 0.75f));

            DrawPillBadge(new Rect(col1.x + 8, col1.y + 8, 56, 16), $"LAP {CurrentLapNumber}", new Color(0.95f, 0.65f, 0.10f));
            GUI.Label(new Rect(col1.x + 68, col1.y + 8, colW - 74, 16), "TIMING ANALYZER", cardTitleStyle);

            string curTimeStr = FormatLapTime(CurrentLapTime);
            GUI.Label(new Rect(col1.x + 8, col1.y + 26, 105, 24), curTimeStr, timingTimeStyle);

            string bestStr = BestLapTime > 0.0f ? $"BEST: {FormatLapTime(BestLapTime)}" : "BEST: --:--.---";
            GUI.Label(new Rect(col1.x + 115, col1.y + 28, colW - 120, 16), bestStr, new GUIStyle(legendStyle) { normal = { textColor = new Color(0.85f, 0.40f, 1.0f) } });

            float secW = (colW - 24.0f) / 3.0f;
            float secY = col1.y + 54.0f;

            DrawSectorChip(new Rect(col1.x + 8, secY, secW, 18), "S1", S1Time, BestS1Time, ActiveSector == 1);
            DrawSectorChip(new Rect(col1.x + 8 + (secW + 4), secY, secW, 18), "S2", S2Time, BestS2Time, ActiveSector == 2);
            DrawSectorChip(new Rect(col1.x + 8 + (secW + 4) * 2, secY, secW, 18), "S3", S3Time, BestS3Time, ActiveSector == 3);


            // --- COLUMN 2: 4-Corner Tyre Degradation ---
            Rect col2 = new Rect(startX + 1 * (colW + spacing), startY, colW, rowH);
            DrawGlassCard(col2, new Color(0.05f, 0.07f, 0.11f, 0.92f), new Color(0.20f, 0.30f, 0.45f, 0.60f));

            string compoundTag = (selectedCompound == TyreCompound.Soft) ? "SOFT" : (selectedCompound == TyreCompound.Medium ? "MED" : "HARD");
            Color compoundCol = (selectedCompound == TyreCompound.Soft) ? new Color(0.85f, 0.15f, 0.15f) : (selectedCompound == TyreCompound.Medium ? new Color(0.95f, 0.80f, 0.15f) : new Color(0.90f, 0.92f, 0.96f));

            DrawPillBadge(new Rect(col2.x + 8, col2.y + 8, 38, 16), compoundTag, compoundCol);
            GUI.Label(new Rect(col2.x + 50, col2.y + 8, colW - 56, 16), "TYRE DEGRADATION", cardTitleStyle);

            float totalWear = car != null ? car.TyreWearPct : 0f;
            Color wearColor = totalWear > 40f ? new Color(0.95f, 0.25f, 0.25f) : (totalWear > 20f ? new Color(0.95f, 0.75f, 0.20f) : new Color(0.25f, 0.90f, 0.45f));
            GUI.Label(new Rect(col2.x + 8, col2.y + 26, 75, 24), $"{totalWear:F1}%", new GUIStyle(cardValueStyle) { normal = { textColor = wearColor } });

            float cW = (colW - 95.0f) * 0.5f;
            float cornerX = col2.x + 85.0f;

            float fl = car != null ? car.TyreWearFL : 0f;
            float fr = car != null ? car.TyreWearFR : 0f;
            float rl = car != null ? car.TyreWearRL : 0f;
            float rr = car != null ? car.TyreWearRR : 0f;

            DrawCornerWearItem(new Rect(cornerX, col2.y + 26, cW, 16), "FL", fl);
            DrawCornerWearItem(new Rect(cornerX + cW + 4, col2.y + 26, cW, 16), "FR", fr);
            DrawCornerWearItem(new Rect(cornerX, col2.y + 46, cW, 16), "RL", rl);
            DrawCornerWearItem(new Rect(cornerX + cW + 4, col2.y + 46, cW, 16), "RR", rr);

            DrawProgressBar(new Rect(col2.x + 8, col2.y + 54, 72, 14), 100f - totalWear, 100f, wearColor, "LIFE");


            // --- COLUMN 3: Speed, Rev Lights & Gear Cluster ---
            Rect col3 = new Rect(startX + 2 * (colW + spacing), startY, colW, rowH);
            DrawGlassCard(col3, new Color(0.04f, 0.06f, 0.09f, 0.95f), new Color(0.28f, 0.42f, 0.65f, 0.85f));

            float speedKmh = car != null ? car.CurrentSpeedKmh : 0f;
            DrawRevLights(new Rect(col3.x + 10, col3.y + 6, colW - 20, 7), speedKmh);

            GUI.Label(new Rect(col3.x + 10, col3.y + 24, 75, 32), $"{speedKmh:F0}", speedNumberStyle);
            GUI.Label(new Rect(col3.x + 88, col3.y + 36, 38, 16), "KM/H", speedUnitStyle);

            string gearStr = car != null ? (car.CurrentGear == VehicleGear.Reverse ? "R" : car.CurrentGear == VehicleGear.Neutral ? "N" : $"{(int)car.CurrentGear}") : "N";
            Color gearCol = (car != null && car.CurrentGear == VehicleGear.Reverse) ? new Color(0.9f, 0.4f, 0.1f) : new Color(0.85f, 0.15f, 0.15f);
            DrawPillBadge(new Rect(col3.x + 130, col3.y + 24, 34, 34), gearStr, gearCol, gearStyle);

            bool drsOpen = car != null && car.DrsToggle == DrsState.Open;
            Color drsCol = drsOpen ? new Color(0.18f, 0.92f, 0.38f) : new Color(0.22f, 0.28f, 0.38f);
            DrawPillBadge(new Rect(col3.x + 170, col3.y + 28, 50, 24), drsOpen ? "DRS ON" : "DRS OFF", drsCol);


            // --- COLUMN 4: Battery & MGU-K Strategy ---
            Rect col4 = new Rect(startX + 3 * (colW + spacing), startY, colW, rowH);
            DrawGlassCard(col4, new Color(0.05f, 0.07f, 0.11f, 0.92f), new Color(0.20f, 0.30f, 0.45f, 0.60f));

            float batteryPct = car != null ? car.BatteryEnergyPct : 100f;
            bool isBoost = car != null && car.IsBoostActive;
            bool isRegen = car != null && car.IsRegenerating;

            GUI.Label(new Rect(col4.x + 8, col4.y + 8, 110, 16), "BATTERY & ERS", cardTitleStyle);
            GUI.Label(new Rect(col4.x + 8, col4.y + 26, 65, 24), $"{batteryPct:F0}%", new GUIStyle(cardValueStyle) { normal = { textColor = new Color(0.25f, 0.85f, 1.0f) } });

            DrawProgressBar(new Rect(col4.x + 8, col4.y + 54, 70, 14), batteryPct, 100f, new Color(0.20f, 0.85f, 1.0f), "SoC");

            string statusStr = isBoost ? "⚡ BOOST (+200HP)" : (isRegen ? "🔋 +160kW REGEN" : (batteryPct > 95f ? "FULL STORE" : (batteryPct <= 0f ? "DEPLETED" : "DEPLOYING")));
            Color statusCol = isBoost ? new Color(0.95f, 0.65f, 0.10f) : (isRegen ? new Color(0.18f, 0.92f, 0.38f) : (batteryPct <= 0f ? new Color(0.9f, 0.2f, 0.2f) : new Color(0.20f, 0.50f, 0.85f)));
            DrawPillBadge(new Rect(col4.x + 82, col4.y + 24, colW - 90, 20), statusStr, statusCol);

            float optBatt = car != null ? car.OptimalBatteryEnergyPct : 100f;
            float optWear = car != null ? car.OptimalTyreWearPct : 0f;
            GUI.Label(new Rect(col4.x + 82, col4.y + 50, colW - 90, 16), $"AI: {optBatt:F0}% SoC | {optWear:F1}% Wear", new GUIStyle(smallTextStyle) { normal = { textColor = new Color(0.35f, 0.95f, 0.55f) } });
        }

        private void DrawCenteredGraphs(float startX, float totalW)
        {
            float graphY = 100.0f;
            float graphH = 96.0f;
            float spacing = 10.0f;
            float graphW = (totalW - 2.0f * spacing) / 3.0f;

            // 1. Speed Telemetry Graph
            DrawGraphCard(
                new Rect(startX + 0 * (graphW + spacing), graphY, graphW, graphH),
                "SPEED TELEMETRY",
                $"{car?.CurrentSpeedKmh:F0} KM/H",
                speedHistory, new Color(0.20f, 0.85f, 1.0f), "Speed",
                null, Color.clear, null,
                0f, 380f);

            // 2. Energy Deployment Strategy Graph
            DrawGraphCard(
                new Rect(startX + 1 * (graphW + spacing), graphY, graphW, graphH),
                "ENERGY DEPLOYMENT (SOC %)",
                $"Driver: {car?.BatteryEnergyPct:F0}%",
                driverEnergyHistory, new Color(1.0f, 0.75f, 0.15f), "My Drive",
                optimalEnergyHistory, new Color(0.25f, 0.95f, 0.40f), "Optimal AI",
                0f, 100f);

            // 3. Tyre Degradation Curve Graph
            float maxWearScale = Mathf.Max(15f, (car != null ? car.TyreWearPct * 1.35f : 15f));
            DrawGraphCard(
                new Rect(startX + 2 * (graphW + spacing), graphY, graphW, graphH),
                "TYRE DEGRADATION CURVE",
                $"Wear: {car?.TyreWearPct:F1}%",
                driverWearHistory, new Color(1.0f, 0.32f, 0.32f), "My Drive",
                optimalWearHistory, new Color(0.25f, 0.80f, 1.0f), "Optimal AI",
                0f, maxWearScale);
        }

        private void DrawSectorChip(Rect r, string label, float recordedTime, float bestTime, bool isActive)
        {
            Color chipBg = isActive ? new Color(0.95f, 0.65f, 0.10f) : (recordedTime > 0.0f ? (recordedTime <= bestTime ? new Color(0.80f, 0.35f, 1.0f) : new Color(0.20f, 0.85f, 0.40f)) : new Color(0.14f, 0.18f, 0.26f));
            DrawPillBadge(new Rect(r.x, r.y, 22, r.height), label, chipBg);

            string txt = isActive ? $"{CurrentSectorTime:F1}s" : (recordedTime > 0.0f ? $"{recordedTime:F1}s" : "--.-s");
            GUI.Label(new Rect(r.x + 24, r.y + 1, r.width - 24, r.height), txt, smallTextStyle);
        }

        private void DrawGraphCard(Rect r, string title, string liveValue, float[] data1, Color col1, string leg1, float[] data2, Color col2, string leg2, float minVal, float maxVal)
        {
            DrawGlassCard(r, new Color(0.05f, 0.07f, 0.11f, 0.90f), new Color(0.18f, 0.25f, 0.38f, 0.65f));

            GUI.Label(new Rect(r.x + 8, r.y + 6, r.width - 95, 16), title, cardTitleStyle);
            DrawPillBadge(new Rect(r.x + r.width - 86, r.y + 5, 78, 18), liveValue, new Color(0.14f, 0.20f, 0.30f));

            Rect plotArea = new Rect(r.x + 8, r.y + 26, r.width - 16, r.height - 44);
            DrawGridLines(plotArea);

            if (data2 != null && leg2 != null)
            {
                DrawSmoothCurve(plotArea, data2, minVal, maxVal, col2);
            }
            DrawSmoothCurve(plotArea, data1, minVal, maxVal, col1);

            float legY = r.y + r.height - 15;
            DrawLegendChip(new Rect(r.x + 8, legY, 75, 12), leg1, col1);
            if (leg2 != null)
            {
                DrawLegendChip(new Rect(r.x + 88, legY, 75, 12), leg2, col2);
            }
        }

        private void DrawRevLights(Rect r, float speedKmh)
        {
            int totalLeds = 15;
            float ledW = (r.width - (totalLeds - 1) * 3) / totalLeds;
            float speedRatio = Mathf.Clamp01(speedKmh / 350.0f);
            int activeCount = Mathf.RoundToInt(speedRatio * totalLeds);

            for (int i = 0; i < totalLeds; i++)
            {
                Rect ledRect = new Rect(r.x + i * (ledW + 3), r.y, ledW, r.height);
                Color baseCol;
                if (i < 5) baseCol = new Color(0.20f, 0.95f, 0.40f);
                else if (i < 10) baseCol = new Color(0.95f, 0.20f, 0.20f);
                else baseCol = new Color(0.30f, 0.60f, 1.0f);

                bool isOn = i < activeCount;
                Color fillCol = isOn ? baseCol : new Color(baseCol.r * 0.25f, baseCol.g * 0.25f, baseCol.b * 0.25f, 0.4f);

                GUI.color = fillCol;
                GUI.DrawTexture(ledRect, whitePixel);
            }
            GUI.color = Color.white;
        }

        private void DrawCornerWearItem(Rect r, string label, float wearPct)
        {
            Color c = wearPct > 40f ? Color.red : (wearPct > 20f ? Color.yellow : new Color(0.3f, 0.85f, 0.4f));
            GUI.Label(new Rect(r.x, r.y, 16, r.height), label, smallTextStyle);
            DrawProgressBar(new Rect(r.x + 16, r.y + 3, r.width - 16, 9), wearPct, 100f, c, $"{wearPct:F0}%");
        }

        private void DrawProgressBar(Rect r, float current, float max, Color fillCol, string text)
        {
            GUI.color = new Color(0.12f, 0.16f, 0.24f, 0.8f);
            GUI.DrawTexture(r, whitePixel);

            float fillW = Mathf.Clamp01(current / max) * r.width;
            GUI.color = fillCol;
            GUI.DrawTexture(new Rect(r.x, r.y, fillW, r.height), whitePixel);
            GUI.color = Color.white;

            if (!string.IsNullOrEmpty(text))
            {
                GUI.Label(new Rect(r.x, r.y - 1, r.width, r.height), text, new GUIStyle(badgeStyle) { fontSize = 8 });
            }
        }

        private void DrawPillBadge(Rect r, string text, Color bgCol, GUIStyle style = null)
        {
            GUI.color = bgCol;
            GUI.DrawTexture(r, whitePixel);
            GUI.color = Color.white;

            GUIStyle s = style ?? badgeStyle;
            GUI.Label(r, text, s);
        }

        private void DrawLegendChip(Rect r, string text, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(new Rect(r.x, r.y + 4, 8, 3), whitePixel);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 12, r.y - 2, r.width - 12, 14), text, new GUIStyle(legendStyle) { normal = { textColor = col } });
        }

        private void DrawGridLines(Rect r)
        {
            for (int i = 1; i <= 2; i++)
            {
                float y = r.y + (r.height * (i / 3.0f));
                DrawLine(new Vector2(r.x, y), new Vector2(r.x + r.width, y), new Color(0.18f, 0.24f, 0.35f, 0.35f), 1);
            }
        }

        private void DrawSmoothCurve(Rect r, float[] data, float minVal, float maxVal, Color col)
        {
            float range = Mathf.Max(0.001f, maxVal - minVal);
            int n = data.Length;
            float stepX = r.width / (n - 1);

            for (int i = 0; i < n - 1; i++)
            {
                int idx1 = (historyIndex + i) % n;
                int idx2 = (historyIndex + i + 1) % n;

                float v1 = Mathf.Clamp(data[idx1], minVal, maxVal);
                float v2 = Mathf.Clamp(data[idx2], minVal, maxVal);

                float y1 = r.y + r.height - ((v1 - minVal) / range * r.height);
                float y2 = r.y + r.height - ((v2 - minVal) / range * r.height);

                float x1 = r.x + (i * stepX);
                float x2 = r.x + ((i + 1) * stepX);

                DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), col, 2);
            }
        }

        private void DrawTacticalMinimap(int screenW, int screenH)
        {
            float mapSize = 220.0f;
            float mapX = screenW - mapSize - 20.0f;
            float mapY = screenH - mapSize - 20.0f;
            Rect mapRect = new Rect(mapX, mapY, mapSize, mapSize);

            DrawGlassCard(mapRect, new Color(0.05f, 0.07f, 0.11f, 0.92f), new Color(0.22f, 0.35f, 0.55f, 0.85f));

            string circuitTitle = (selectedCircuit == CircuitType.Bahrain) ? "5.412 KM SAKHIR" :
                                  (selectedCircuit == CircuitType.Spa ? "7.004 KM SPA" :
                                  (selectedCircuit == CircuitType.Monza ? "5.793 KM MONZA" : "3.337 KM MONACO"));
            string sectorStr = $"SEC {ActiveSector}";
            Color secBadgeCol = ActiveSector == 1 ? new Color(0.20f, 0.75f, 1.0f) :
                                (ActiveSector == 2 ? new Color(0.95f, 0.80f, 0.20f) : new Color(0.95f, 0.30f, 0.80f));

            GUI.Label(new Rect(mapX + 8, mapY + 6, 140, 16), circuitTitle, cardTitleStyle);
            DrawPillBadge(new Rect(mapX + mapSize - 68, mapY + 6, 60, 16), sectorStr, secBadgeCol);

            if (minimapPoints == null || minimapPoints.Length < 2) return;

            float pad = 18.0f;
            Rect mapInner = new Rect(mapX + pad, mapY + pad + 10, mapSize - pad * 2, mapSize - pad * 2 - 10);

            float s1Split = (selectedCircuit == CircuitType.Monaco) ? 0.30f : ((selectedCircuit == CircuitType.Monza) ? 0.32f : 0.31f);
            float s2Split = (selectedCircuit == CircuitType.Monaco) ? 0.65f : ((selectedCircuit == CircuitType.Monza) ? 0.70f : 0.72f);

            for (int i = 0; i < minimapPoints.Length - 1; i++)
            {
                Vector2 p1 = WorldToMinimapPixel(minimapPoints[i], mapInner);
                Vector2 p2 = WorldToMinimapPixel(minimapPoints[i + 1], mapInner);

                Color segCol = (i < minimapPoints.Length * s1Split) ? new Color(0.15f, 0.78f, 1.0f) :
                               (i < minimapPoints.Length * s2Split) ? new Color(1.0f, 0.82f, 0.20f) : new Color(0.95f, 0.30f, 0.85f);

                DrawLine(p1, p2, segCol, 3.5f);
                GUI.color = segCol;
                GUI.DrawTexture(new Rect(p1.x - 1.75f, p1.y - 1.75f, 3.5f, 3.5f), whitePixel);
            }

            Vector2 sfP = WorldToMinimapPixel(new Vector2(0f, 160f), mapInner);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(sfP.x - 4, sfP.y - 4, 8, 8), whitePixel);

            Vector3 carPos = car != null ? car.transform.position : Vector3.zero;
            if (car != null)
            {
                Vector2 carP = WorldToMinimapPixel(new Vector2(carPos.x, carPos.z), mapInner);
                float pulse = 1.0f + Mathf.Sin(Time.time * 8.0f) * 0.25f;

                GUI.color = new Color(1.0f, 0.40f, 0.10f, 0.45f);
                GUI.DrawTexture(new Rect(carP.x - 6 * pulse, carP.y - 6 * pulse, 12 * pulse, 12 * pulse), whitePixel);

                GUI.color = new Color(1.0f, 0.90f, 0.20f, 1.0f);
                GUI.DrawTexture(new Rect(carP.x - 3, carP.y - 3, 6, 6), whitePixel);

                Vector3 fwd = car.transform.forward;
                Vector2 dir2D = new Vector2(fwd.x, fwd.z).normalized;
                Vector2 headingP = carP + new Vector2(dir2D.x, -dir2D.y) * 14.0f;
                DrawLine(carP, headingP, new Color(1.0f, 0.95f, 0.30f), 2);
                GUI.color = Color.white;
            }
        }

        private Vector2 WorldToMinimapPixel(Vector2 worldPos, Rect rect)
        {
            float rangeX = maxX - minX;
            float rangeZ = maxZ - minZ;
            float maxRange = Mathf.Max(rangeX, rangeZ);
            if (maxRange <= 0.001f) maxRange = 1.0f;

            float normX = (worldPos.x - minX) / maxRange;
            float normZ = (worldPos.y - minZ) / maxRange;

            float offsetX = (1.0f - (rangeX / maxRange)) * 0.5f * rect.width;
            float offsetZ = (1.0f - (rangeZ / maxRange)) * 0.5f * rect.height;

            float px = rect.x + offsetX + (normX * rect.width);
            float py = rect.y + rect.height - offsetZ - (normZ * rect.height);
            return new Vector2(px, py);
        }

        private void DrawGlassCard(Rect r, Color bgCol, Color borderCol)
        {
            GUI.color = bgCol;
            GUI.DrawTexture(r, whitePixel);

            GUI.color = borderCol;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1), whitePixel);
            GUI.DrawTexture(new Rect(r.x, r.y + r.height - 1, r.width, 1), whitePixel);
            GUI.DrawTexture(new Rect(r.x, r.y, 1, r.height), whitePixel);
            GUI.DrawTexture(new Rect(r.x + r.width - 1, r.y, 1, r.height), whitePixel);
            GUI.color = Color.white;
        }

        private void DrawLine(Vector2 p1, Vector2 p2, Color col, float width)
        {
            Color prev = GUI.color;
            GUI.color = col;
            Vector2 d = p2 - p1;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float length = d.magnitude;

            GUIUtility.RotateAroundPivot(angle, p1);
            GUI.DrawTexture(new Rect(p1.x, p1.y - width * 0.5f, length, width), whitePixel);
            GUIUtility.RotateAroundPivot(-angle, p1);
            GUI.color = prev;
        }

        private string FormatLapTime(float timeSeconds)
        {
            if (timeSeconds <= 0.0f) return "0:00.000";
            int minutes = Mathf.FloorToInt(timeSeconds / 60.0f);
            float seconds = timeSeconds % 60.0f;
            return $"{minutes}:{seconds:00.000}";
        }
    }
}
