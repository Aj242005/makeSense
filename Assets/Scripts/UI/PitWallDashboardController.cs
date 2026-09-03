using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using GridSense.Core;
using GridSense.Physics;

namespace GridSense.UI
{
    /// <summary>
    /// Binds the pit wall to live state. Two disciplines run through the whole class:
    ///
    ///   Command versus actual — anything the models recommend is written into a magenta command
    ///   cell beside the driver's actual value, so the gap is the thing you read.
    ///
    ///   Honest degradation — when an inference runner is missing, the column says so and shows
    ///   nothing. It never falls back to a plausible-looking number, and the physics heuristic
    ///   baseline is never labelled as model output.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PitWallDashboardController : MonoBehaviour
    {
        [SerializeField] private CarPhysicsController car;
        [SerializeField] private KeyCode toggleKey = KeyCode.P;
        [SerializeField] private bool visibleOnStart;

        private UIDocument doc;
        private VisualElement root;
        private bool built;

        // masthead
        private Label sessionLine, lapValue, stintValue, fuelValue, inferenceValue;

        // stint column
        private Label stintStatus, lapElapsed, lapDelta;
        private Label s1Time, s2Time, s3Time;
        private VisualElement s1Bar, s2Bar, s3Bar;
        private Label speedValue, gearValue, drsValue, gapAheadValue, evolutionValue;
        private VisualElement lapTable;
        private Label lapTableEmpty;

        // track 3
        private Label track3Status;
        private VisualElement track3Offline, track3Body;
        private VisualElement fuelNeg, fuelPos, trafficNeg, trafficPos, evoNeg, evoPos, tyreNeg, tyrePos;
        private Label fuelValueS, trafficValueS, evolutionValueS, tyreValueS, totalDeltaS;
        private VisualElement bandSpan, bandLine, bandTruth;
        private Label bandLower, bandPredicted, bandUpper, groundTruthValue, residualValue;
        private Label wearFL, wearFR, wearRL, wearRR, tempFL, tempFR, tempRL, tempRR;
        private VisualElement wearFillFL, wearFillFR, wearFillRL, wearFillRR;

        // track 1
        private Label track1Status;
        private VisualElement track1Offline, track1Body;
        private Label modeCommand, modeActual, rationale, brakingCommand, riskValue, riskCategory;
        private VisualElement riskFill, socFill, socBug;
        private Label socValue, budgetDelta;
        private VisualElement budgetNeg, budgetPos, wearPenNeg, wearPenPos, brakePenNeg, brakePenPos, oppNeg, oppPos;
        private Label budgetFactor, wearPenFactor, brakePenFactor, oppFactor;

        private Label footerRight;
        private StintPlot stintPlot;

        private struct LapRecord
        {
            public int Lap;
            public float Time;
            public TyreCompound Compound;
            public float WearPct;
        }

        private readonly List<LapRecord> laps = new List<LapRecord>();
        private int lastSeenLap = 1;
        private float stintClock;
        private float bestLapSeen;

        [SerializeField] private int stintLaps = 20;

        // ==================================================================== lifecycle

        private void Awake()
        {
            doc = GetComponent<UIDocument>();
            if (car == null) car = UnityEngine.Object.FindFirstObjectByType<CarPhysicsController>();
        }

        private void OnEnable()
        {
            built = false;
        }

        private void Bind()
        {
            root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;

            VisualElement r = root;
            sessionLine = r.Q<Label>("SessionLine");
            lapValue = r.Q<Label>("LapValue");
            stintValue = r.Q<Label>("StintValue");
            fuelValue = r.Q<Label>("FuelValue");
            inferenceValue = r.Q<Label>("InferenceValue");

            stintStatus = r.Q<Label>("StintStatus");
            lapElapsed = r.Q<Label>("LapElapsed");
            lapDelta = r.Q<Label>("LapDelta");
            s1Time = r.Q<Label>("S1Time"); s2Time = r.Q<Label>("S2Time"); s3Time = r.Q<Label>("S3Time");
            s1Bar = r.Q<VisualElement>("S1Bar"); s2Bar = r.Q<VisualElement>("S2Bar"); s3Bar = r.Q<VisualElement>("S3Bar");
            speedValue = r.Q<Label>("SpeedValue");
            gearValue = r.Q<Label>("GearValue");
            drsValue = r.Q<Label>("DrsValue");
            gapAheadValue = r.Q<Label>("GapAheadValue");
            evolutionValue = r.Q<Label>("EvolutionValue");
            lapTable = r.Q<VisualElement>("LapTable");
            lapTableEmpty = r.Q<Label>("LapTableEmpty");

            track3Status = r.Q<Label>("Track3Status");
            track3Offline = r.Q<VisualElement>("Track3Offline");
            track3Body = r.Q<VisualElement>("Track3Body");
            fuelNeg = r.Q<VisualElement>("FuelNeg"); fuelPos = r.Q<VisualElement>("FuelPos");
            trafficNeg = r.Q<VisualElement>("TrafficNeg"); trafficPos = r.Q<VisualElement>("TrafficPos");
            evoNeg = r.Q<VisualElement>("EvolutionNeg"); evoPos = r.Q<VisualElement>("EvolutionPos");
            tyreNeg = r.Q<VisualElement>("TyreNeg"); tyrePos = r.Q<VisualElement>("TyrePos");
            fuelValueS = r.Q<Label>("FuelValueS");
            trafficValueS = r.Q<Label>("TrafficValueS");
            evolutionValueS = r.Q<Label>("EvolutionValueS");
            tyreValueS = r.Q<Label>("TyreValueS");
            totalDeltaS = r.Q<Label>("TotalDeltaS");
            bandSpan = r.Q<VisualElement>("BandSpan");
            bandLine = r.Q<VisualElement>("BandLine");
            bandTruth = r.Q<VisualElement>("BandTruth");
            bandLower = r.Q<Label>("BandLower");
            bandPredicted = r.Q<Label>("BandPredicted");
            bandUpper = r.Q<Label>("BandUpper");
            groundTruthValue = r.Q<Label>("GroundTruthValue");
            residualValue = r.Q<Label>("ResidualValue");
            wearFL = r.Q<Label>("WearFL"); wearFR = r.Q<Label>("WearFR");
            wearRL = r.Q<Label>("WearRL"); wearRR = r.Q<Label>("WearRR");
            tempFL = r.Q<Label>("TempFL"); tempFR = r.Q<Label>("TempFR");
            tempRL = r.Q<Label>("TempRL"); tempRR = r.Q<Label>("TempRR");
            wearFillFL = r.Q<VisualElement>("WearFillFL"); wearFillFR = r.Q<VisualElement>("WearFillFR");
            wearFillRL = r.Q<VisualElement>("WearFillRL"); wearFillRR = r.Q<VisualElement>("WearFillRR");

            track1Status = r.Q<Label>("Track1Status");
            track1Offline = r.Q<VisualElement>("Track1Offline");
            track1Body = r.Q<VisualElement>("Track1Body");
            modeCommand = r.Q<Label>("ModeCommand");
            modeActual = r.Q<Label>("ModeActual");
            rationale = r.Q<Label>("Rationale");
            brakingCommand = r.Q<Label>("BrakingCommand");
            riskValue = r.Q<Label>("RiskValue");
            riskCategory = r.Q<Label>("RiskCategory");
            riskFill = r.Q<VisualElement>("RiskFill");
            socFill = r.Q<VisualElement>("SocFill");
            socBug = r.Q<VisualElement>("SocBug");
            socValue = r.Q<Label>("SocValue");
            budgetDelta = r.Q<Label>("BudgetDelta");
            budgetNeg = r.Q<VisualElement>("BudgetNeg"); budgetPos = r.Q<VisualElement>("BudgetPos");
            wearPenNeg = r.Q<VisualElement>("WearPenNeg"); wearPenPos = r.Q<VisualElement>("WearPenPos");
            brakePenNeg = r.Q<VisualElement>("BrakePenNeg"); brakePenPos = r.Q<VisualElement>("BrakePenPos");
            oppNeg = r.Q<VisualElement>("OppNeg"); oppPos = r.Q<VisualElement>("OppPos");
            budgetFactor = r.Q<Label>("BudgetFactor");
            wearPenFactor = r.Q<Label>("WearPenFactor");
            brakePenFactor = r.Q<Label>("BrakePenFactor");
            oppFactor = r.Q<Label>("OppFactor");

            footerRight = r.Q<Label>("FooterRight");

            VisualElement plotHost = r.Q<VisualElement>("StintPlotHost");
            if (plotHost != null)
            {
                plotHost.Clear();
                stintPlot = new StintPlot();
                stintPlot.SetStintLength(stintLaps);
                plotHost.Add(stintPlot);
            }

            root.style.display = visibleOnStart ? DisplayStyle.Flex : DisplayStyle.None;
            built = true;
        }

        private void Update()
        {
            if (!built) Bind();
            if (!built || root == null) return;

            if (UnityEngine.Input.GetKeyDown(toggleKey))
            {
                bool showing = root.style.display == DisplayStyle.Flex;
                root.style.display = showing ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (root.style.display == DisplayStyle.None) return;

            if (F1TelemetryHUD.Instance != null && F1TelemetryHUD.Instance.IsRacing)
                stintClock += Time.deltaTime;

            RecordCompletedLaps();
            PaintMasthead();
            PaintStint();
            PaintTrack3();
            PaintTrack1();
        }

        // ==================================================================== data access

        private static bool CoreLive { get { return SimulationCore.Instance != null; } }

        private static bool Track1Live
        {
            get { return SimulationCore.Instance != null && GridSense.ML.Track1InferenceRunner.Instance != null; }
        }

        private static bool Track3Live
        {
            get { return SimulationCore.Instance != null && GridSense.ML.Track3InferenceRunner.Instance != null; }
        }

        /// <summary>
        /// The runner produces output either way, so the label has to distinguish a loaded ONNX
        /// policy from the analytical fallback. Calling the heuristic "PPO" would be a lie the
        /// user could not detect.
        /// </summary>
        private static bool Track1Model
        {
            get
            {
                return GridSense.ML.Track1InferenceRunner.Instance != null
                    && GridSense.ML.Track1InferenceRunner.Instance.IsModelLoaded;
            }
        }

        private static bool Track3Model
        {
            get
            {
                return GridSense.ML.Track3InferenceRunner.Instance != null
                    && GridSense.ML.Track3InferenceRunner.Instance.IsModelLoaded;
            }
        }

        private void RecordCompletedLaps()
        {
            F1TelemetryHUD hud = F1TelemetryHUD.Instance;
            if (hud == null) return;

            if (hud.CurrentLapNumber > lastSeenLap && hud.LastLapTime > 0f)
            {
                laps.Add(new LapRecord
                {
                    Lap = lastSeenLap,
                    Time = hud.LastLapTime,
                    Compound = car != null ? car.CurrentCompound : TyreCompound.Medium,
                    WearPct = car != null ? car.TyreWearPct : 0f
                });
                if (bestLapSeen <= 0f || hud.LastLapTime < bestLapSeen) bestLapSeen = hud.LastLapTime;

                if (stintPlot != null)
                {
                    // measured wear is the physics engine's own signal; predicted and the band are
                    // only supplied when Track 3 is genuinely running
                    bool modelLive = Track3Live;
                    TyreDegradationExplainability e = modelLive
                        ? SimulationCore.Instance.TyreExplainability
                        : default(TyreDegradationExplainability);
                    stintPlot.AddLap(lastSeenLap,
                        car != null ? car.TyreWearPct : 0f,
                        e.PredictedWearPct, e.ConfidenceBandLowerPct, e.ConfidenceBandUpperPct,
                        modelLive);
                }

                lastSeenLap = hud.CurrentLapNumber;
                RebuildLapTable();
            }
        }

        // ==================================================================== paint

        private void PaintMasthead()
        {
            F1TelemetryHUD hud = F1TelemetryHUD.Instance;

            if (sessionLine != null)
            {
                string circuit = hud != null ? hud.SelectedCircuit.ToString().ToUpperInvariant() : "--";
                string comp = car != null
                    ? Theme.CompoundName(car.CurrentCompound) + " " + Theme.CompoundCode(car.CurrentCompound)
                    : "--";
                sessionLine.text = circuit + "   ·   " + comp;
            }

            if (lapValue != null) lapValue.text = hud != null ? hud.CurrentLapNumber.ToString() : "--";
            if (stintValue != null) stintValue.text = Clock(stintClock);
            if (fuelValue != null)
                fuelValue.text = CoreLive
                    ? SimulationCore.Instance.State.FuelLoadKg.ToString("F1") + " kg"
                    : "--";

            if (inferenceValue != null)
            {
                bool both = Track1Live && Track3Live;
                bool models = Track1Model && Track3Model;
                bool any = Track1Live || Track3Live;
                string one = Track1Live
                    ? (Track1Model ? "TRACK 1" : "TRACK 1 HEURISTIC")
                    : (Track3Model ? "TRACK 3" : "TRACK 3 HEURISTIC");
                inferenceValue.text = !any ? "OFFLINE"
                    : (both ? (models ? "TRACK 1 + 3" : "HEURISTIC") : one);
                inferenceValue.EnableInClassList("is-offline", !(both && models));
                inferenceValue.EnableInClassList("is-command", both && models);
            }

            if (footerRight != null)
                footerRight.text = CoreLive
                    ? "state source: SimulationCore.CarState"
                    : "state source: CarPhysicsController (no SimulationCore in scene)";
        }

        private void PaintStint()
        {
            F1TelemetryHUD hud = F1TelemetryHUD.Instance;
            if (hud == null) return;

            if (stintStatus != null)
            {
                stintStatus.text = hud.IsRacing ? "LIVE" : "ON GRID";
                stintStatus.EnableInClassList("is-offline", !hud.IsRacing);
            }

            if (lapElapsed != null) lapElapsed.text = Theme.LapTime(hud.CurrentLapTime);

            if (lapDelta != null)
            {
                if (hud.BestLapTime > 0f)
                {
                    float d = hud.CurrentLapTime - hud.BestLapTime;
                    lapDelta.text = Theme.Delta(d);
                    lapDelta.style.color = Theme.DeltaInk(d);
                }
                else
                {
                    lapDelta.text = "no reference";
                    lapDelta.style.color = Theme.InkDim;
                }
            }

            PaintSector(s1Bar, s1Time, 1, hud.S1Time, hud.BestS1Time, hud.ActiveSector, hud.CurrentSectorTime);
            PaintSector(s2Bar, s2Time, 2, hud.S2Time, hud.BestS2Time, hud.ActiveSector, hud.CurrentSectorTime);
            PaintSector(s3Bar, s3Time, 3, hud.S3Time, hud.BestS3Time, hud.ActiveSector, hud.CurrentSectorTime);

            if (speedValue != null) speedValue.text = (car != null ? car.CurrentSpeedKmh : 0f).ToString("F0") + " km/h";

            if (gearValue != null)
            {
                string g = "N";
                if (car != null)
                {
                    if (car.CurrentGear == VehicleGear.Reverse) g = "R";
                    else if (car.CurrentGear != VehicleGear.Neutral) g = ((int)car.CurrentGear).ToString();
                }
                gearValue.text = g;
            }

            if (drsValue != null)
            {
                bool open = car != null && car.DrsToggle == DrsState.Open;
                bool armed = car != null && car.DrsToggle == DrsState.Available;
                drsValue.text = open ? "OPEN" : (armed ? "ARMED" : "CLOSED");
                drsValue.EnableInClassList("is-ok", open);
                drsValue.EnableInClassList("is-caution", armed);
            }

            if (gapAheadValue != null)
            {
                if (CoreLive)
                {
                    CarState s = SimulationCore.Instance.State;
                    gapAheadValue.text = s.HasGapAhead ? "+" + s.GapAheadS.ToString("F2") + " s" : "CLEAR";
                    gapAheadValue.EnableInClassList("is-caution", s.DirtyAir);
                }
                else gapAheadValue.text = "--";
            }

            if (evolutionValue != null)
                evolutionValue.text = CoreLive
                    ? "×" + SimulationCore.Instance.State.TrackEvolutionFactor.ToString("F3")
                    : "--";
        }

        private void PaintSector(VisualElement bar, Label time, int index,
                                 float recorded, float best, int activeSector, float liveTime)
        {
            if (bar == null || time == null) return;

            bar.RemoveFromClassList("is-active");
            bar.RemoveFromClassList("is-personal");
            bar.RemoveFromClassList("is-slower");
            bar.RemoveFromClassList("is-best");

            if (activeSector == index)
            {
                bar.AddToClassList("is-active");
                time.text = liveTime.ToString("00.0");
            }
            else if (recorded > 0f)
            {
                bool personal = best > 0f && recorded <= best + 0.0005f;
                bar.AddToClassList(personal ? "is-personal" : "is-slower");
                time.text = Theme.Split(recorded);
            }
            else
            {
                time.text = "--.---";
            }
        }

        private void RebuildLapTable()
        {
            if (lapTable == null) return;
            lapTable.Clear();

            int from = Mathf.Max(0, laps.Count - 8);
            for (int i = laps.Count - 1; i >= from; i--)
            {
                LapRecord rec = laps[i];
                VisualElement row = new VisualElement();
                row.AddToClassList("lap-row");

                row.Add(Cell(rec.Lap.ToString(), "lap-cell is-first", false));
                row.Add(Cell(Theme.LapTime(rec.Time), "lap-cell",
                    bestLapSeen > 0f && rec.Time <= bestLapSeen + 0.0005f));
                row.Add(Cell(Theme.CompoundName(rec.Compound), "lap-cell", false));
                row.Add(Cell(rec.WearPct.ToString("F1") + "%", "lap-cell", false));

                lapTable.Add(row);
            }

            if (lapTableEmpty != null)
                lapTableEmpty.style.display = laps.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static Label Cell(string text, string classes, bool fastest)
        {
            Label l = new Label(text);
            foreach (string c in classes.Split(' ')) l.AddToClassList(c);
            if (fastest) l.AddToClassList("is-fastest");
            return l;
        }

        // -------------------------------------------------------------------- track 3

        private void PaintTrack3()
        {
            bool live = Track3Live;
            if (track3Status != null)
            {
                track3Status.text = live ? (Track3Model ? "EBM · LIVE" : "HEURISTIC · NO MODEL") : "OFFLINE";
                track3Status.EnableInClassList("is-offline", !live || !Track3Model);
            }
            if (track3Offline != null) track3Offline.style.display = live ? DisplayStyle.None : DisplayStyle.Flex;
            if (track3Body != null) track3Body.EnableInClassList("hidden", !live);
            if (!live) return;

            TyreDegradationExplainability e = SimulationCore.Instance.TyreExplainability;

            // scale every attribution bar to the largest contribution, so the widths are comparable
            float max = Mathf.Max(0.05f, Mathf.Max(
                Mathf.Max(Mathf.Abs(e.FuelCorrectionDeltaS), Mathf.Abs(e.TrafficDirtyAirDeltaS)),
                Mathf.Max(Mathf.Abs(e.TrackEvolutionDeltaS), Mathf.Abs(e.TrueTyreDegradationDeltaS))));

            Signed(fuelNeg, fuelPos, fuelValueS, e.FuelCorrectionDeltaS, max, "s");
            Signed(trafficNeg, trafficPos, trafficValueS, e.TrafficDirtyAirDeltaS, max, "s");
            Signed(evoNeg, evoPos, evolutionValueS, e.TrackEvolutionDeltaS, max, "s");
            Signed(tyreNeg, tyrePos, tyreValueS, e.TrueTyreDegradationDeltaS, max, "s");

            if (totalDeltaS != null)
            {
                totalDeltaS.text = Theme.Delta(e.TotalObservedDeltaS, 3) + " s";
                totalDeltaS.style.color = Theme.DeltaInk(e.TotalObservedDeltaS);
            }

            PaintConfidenceBand(e);

            float fl = car != null ? car.TyreWearFL : 0f;
            float fr = car != null ? car.TyreWearFR : 0f;
            float rl = car != null ? car.TyreWearRL : 0f;
            float rr = car != null ? car.TyreWearRR : 0f;
            float mean = (fl + fr + rl + rr) * 0.25f;

            Corner(wearFL, wearFillFL, tempFL, fl, mean);
            Corner(wearFR, wearFillFR, tempFR, fr, mean);
            Corner(wearRL, wearFillRL, tempRL, rl, mean);
            Corner(wearRR, wearFillRR, tempRR, rr, mean);
        }

        /// <summary>
        /// The 95% band, drawn at the width the model actually reports. The build spec forbids
        /// implying tighter confidence than the model has, so nothing here is clamped for looks.
        /// </summary>
        private void PaintConfidenceBand(TyreDegradationExplainability e)
        {
            float lo = e.ConfidenceBandLowerPct;
            float hi = e.ConfidenceBandUpperPct;
            float mid = e.PredictedWearPct;
            float truth = e.GroundTruthWearPct;

            float top = Mathf.Max(1f, Mathf.Max(Mathf.Max(hi, mid), truth) * 1.25f);

            if (bandSpan != null)
            {
                float yHi = 1f - Mathf.Clamp01(hi / top);
                float yLo = 1f - Mathf.Clamp01(lo / top);
                bandSpan.style.top = Length.Percent(yHi * 100f);
                bandSpan.style.height = Length.Percent(Mathf.Max(1f, (yLo - yHi) * 100f));
            }
            if (bandLine != null) bandLine.style.top = Length.Percent((1f - Mathf.Clamp01(mid / top)) * 100f);
            if (bandTruth != null) bandTruth.style.top = Length.Percent((1f - Mathf.Clamp01(truth / top)) * 100f);

            if (bandLower != null) bandLower.text = "lower " + lo.ToString("F1") + "%";
            if (bandPredicted != null) bandPredicted.text = "predicted " + mid.ToString("F1") + "%";
            if (bandUpper != null) bandUpper.text = "upper " + hi.ToString("F1") + "%";
            if (groundTruthValue != null) groundTruthValue.text = truth.ToString("F1") + "%";
            if (residualValue != null)
            {
                float residual = Mathf.Abs(e.ResidualErrorPct);
                residualValue.text = e.ResidualErrorPct.ToString("F2") + " pts";
                residualValue.EnableInClassList("is-caution", residual > 3f && residual <= 6f);
                residualValue.EnableInClassList("is-warning", residual > 6f);
            }
        }

        private void Corner(Label value, VisualElement fill, Label note, float wear, float mean)
        {
            if (value != null) value.text = wear.ToString("F1") + "%";
            if (fill != null)
            {
                fill.style.width = Length.Percent(Mathf.Clamp01(wear / 100f) * 100f);
                fill.style.backgroundColor = Theme.WearInk(wear);
            }
            if (note != null)
            {
                float d = wear - mean;
                note.text = (d >= 0f ? "+" : "") + d.ToString("F1") + " vs car mean";
            }
        }

        // -------------------------------------------------------------------- track 1

        private void PaintTrack1()
        {
            bool live = Track1Live;
            if (track1Status != null)
            {
                track1Status.text = live ? (Track1Model ? "PPO · LIVE" : "HEURISTIC · NO MODEL") : "OFFLINE";
                track1Status.EnableInClassList("is-offline", !live || !Track1Model);
            }
            if (track1Offline != null) track1Offline.style.display = live ? DisplayStyle.None : DisplayStyle.Flex;
            if (track1Body != null) track1Body.EnableInClassList("hidden", !live);
            if (!live) return;

            EnergyDeploymentExplainability e = SimulationCore.Instance.EnergyExplainability;
            CarState s = SimulationCore.Instance.State;

            if (modeCommand != null) modeCommand.text = e.RecommendedDeploymentMode.ToString().ToUpperInvariant();
            if (modeActual != null)
            {
                string actual = car != null && car.IsBoostActive ? "PUSH"
                    : (car != null && car.IsRegenerating ? "SAVE" : s.DeploymentMode.ToString().ToUpperInvariant());
                modeActual.text = actual;
            }
            if (rationale != null)
                rationale.text = string.IsNullOrEmpty(e.ExplanationSummary) ? "--" : e.ExplanationSummary;

            if (brakingCommand != null) brakingCommand.text = e.RecommendedBraking.ToString().ToUpperInvariant();

            if (riskValue != null) riskValue.text = e.OvertakeRiskRewardScore.ToString("F0") + " / 100";
            if (riskFill != null)
            {
                riskFill.style.width = Length.Percent(Mathf.Clamp01(e.OvertakeRiskRewardScore / 100f) * 100f);
                riskFill.style.backgroundColor = RiskInk(e.RiskCategory);
            }
            if (riskCategory != null)
            {
                riskCategory.text = e.RiskCategory.ToString().ToUpperInvariant();
                riskCategory.style.color = RiskInk(e.RiskCategory);
            }

            float soc = car != null ? car.BatteryEnergyPct : s.EnergyRemainingPct;
            float target = car != null ? car.OptimalBatteryEnergyPct : s.EnergyRemainingPct;

            if (socFill != null) socFill.style.width = Length.Percent(Mathf.Clamp01(soc / 100f) * 100f);
            if (socBug != null)
            {
                // CarPhysicsController.OptimalBatteryEnergyPct is a physics heuristic, not policy
                // output, so this marker is drawn in Ink. Command magenta stays reserved for the
                // model's own recommendations.
                socBug.style.left = Length.Percent(Mathf.Clamp01(target / 100f) * 100f);
                socBug.style.backgroundColor = Theme.Ink;
            }
            if (socValue != null) socValue.text = soc.ToString("F0") + "%";
            if (budgetDelta != null)
            {
                float d = e.EnergyBudgetSurplusDeficitPct;
                budgetDelta.text = (d >= 0f ? "+" : "") + d.ToString("F1") + " pts";
                budgetDelta.EnableInClassList("is-ok", d >= 0f);
                budgetDelta.EnableInClassList("is-caution", d < 0f);
            }

            float max = Mathf.Max(0.05f, Mathf.Max(
                Mathf.Max(Mathf.Abs(e.EnergyBudgetSurplusDeficitPct), Mathf.Abs(e.TyreWearPenaltyFactor)),
                Mathf.Max(Mathf.Abs(e.BrakeFadeThermalPenalty), Mathf.Abs(e.DirtyAirOvertakeOpportunity))));

            Signed(budgetNeg, budgetPos, budgetFactor, e.EnergyBudgetSurplusDeficitPct, max, "");
            Signed(wearPenNeg, wearPenPos, wearPenFactor, -Mathf.Abs(e.TyreWearPenaltyFactor), max, "");
            Signed(brakePenNeg, brakePenPos, brakePenFactor, -Mathf.Abs(e.BrakeFadeThermalPenalty), max, "");
            Signed(oppNeg, oppPos, oppFactor, e.DirtyAirOvertakeOpportunity, max, "");
        }

        private static Color RiskInk(OvertakeRiskCategory c)
        {
            if (c == OvertakeRiskCategory.Critical) return Theme.Warning;
            if (c == OvertakeRiskCategory.High) return Theme.Caution;
            if (c == OvertakeRiskCategory.Moderate) return Theme.Ink;
            return Theme.Ok;
        }

        // -------------------------------------------------------------------- helpers

        /// <summary>
        /// Lays out one signed attribution bar. Negative grows left from the axis in Ok green
        /// (time gained, or a favourable weight); positive grows right in Warning red.
        /// </summary>
        private static void Signed(VisualElement neg, VisualElement pos, Label value,
                                   float v, float absMax, string unit)
        {
            float f = Mathf.Clamp01(Mathf.Abs(v) / Mathf.Max(0.0001f, absMax)) * 50f;

            if (neg != null)
            {
                bool on = v < 0f;
                neg.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (on)
                {
                    neg.style.right = Length.Percent(50f);
                    neg.style.left = StyleKeyword.Auto;
                    neg.style.width = Length.Percent(f);
                }
            }
            if (pos != null)
            {
                bool on = v >= 0f;
                pos.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (on)
                {
                    pos.style.left = Length.Percent(50f);
                    pos.style.right = StyleKeyword.Auto;
                    pos.style.width = Length.Percent(f);
                }
            }
            if (value != null)
                value.text = Theme.Delta(v, unit == "s" ? 3 : 2) + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
        }

        private static string Clock(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            float s = seconds - m * 60f;
            return string.Format("{0}:{1:00.0}", m, s);
        }
    }
}
