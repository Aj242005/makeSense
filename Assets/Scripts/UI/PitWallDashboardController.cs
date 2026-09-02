using System;
using UnityEngine;
using UnityEngine.UIElements;
using GridSense.Core;
using GridSense.Physics;
using GridSense.Track;

namespace GridSense.UI
{
    /// <summary>
    /// PitWallDashboardController manages the UI Toolkit Pit-Wall interface:
    /// - Listens to 50Hz telemetry updates from SimulationCore and CarPhysicsController.
    /// - Updates Driver Telemetry (Speed, Gear, RPM bar, Pedals, Chassis heat map).
    /// - Updates Track 1 Energy Deployment recommendations, SoC gauge, and Tactical AI explainability bars.
    /// - Updates Track 3 pure tyre degradation breakdown and isolated delta contributions.
    /// - Updates live timing sectors and lap deltas.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PitWallDashboardController : MonoBehaviour
    {
        [Header("Car Reference (Optional, auto-found)")]
        [SerializeField] private CarPhysicsController carPhysics;

        private UIDocument uiDocument;
        private VisualElement rootElement;

        // Header Elements
        private Label circuitLabel;
        private Label systemStatusLabel;
        private Label lapCounterLabel;
        private Label fuelRemainingLabel;
        private Label stintTimeLabel;

        // Driver Telemetry Elements
        private Label gearValue;
        private Label speedValue;
        private Label rpmValue;
        private VisualElement rpmBarFill;
        private Label throttleValue;
        private VisualElement throttleBarFill;
        private Label brakeValue;
        private VisualElement brakeBarFill;
        private Label latGValue;
        private Label longGValue;
        private Label drsStateLabel;

        // 4-Wheel Temperatures
        private Label tyreTempFL;
        private Label brakeTempFL;
        private Label tyreTempFR;
        private Label brakeTempFR;
        private Label tyreTempRL;
        private Label brakeTempRL;
        private Label tyreTempRR;
        private Label brakeTempRR;

        // Track 1 Energy Elements
        private Label batterySocPct;
        private Label batteryEnergyMj;
        private VisualElement batterySocFill;
        private Label powerFlowLabel;
        private VisualElement modeBadge;
        private Label modeBadgeText;
        private Label gapAheadLabel;
        private Label closingRateLabel;
        private Label overtakeProbLabel;

        // Track 1 Explainability
        private VisualElement featGapFill;
        private Label featGapVal;
        private VisualElement featSocFill;
        private Label featSocVal;
        private VisualElement featWearFill;
        private Label featWearVal;
        private VisualElement featThermFill;
        private Label featThermVal;

        // Track 3 Tyre Degradation Elements
        private Label tyreCompoundBadge;
        private Label totalTyreDeltaLabel;
        private Label deltaPureWearLabel;
        private VisualElement barPureWear;
        private Label deltaThermalLabel;
        private VisualElement barThermal;
        private Label deltaFuelLabel;
        private VisualElement barFuel;
        private Label deltaTrafficLabel;
        private VisualElement barTraffic;
        private Label deltaTrackLabel;
        private VisualElement barTrack;
        private Label strategyAdvisoryText;

        // Timing Elements
        private Label sector1Time;
        private Label sector1Delta;
        private Label sector2Time;
        private Label sector2Delta;
        private Label sector3Time;
        private Label sector3Delta;
        private Label lastLapTime;
        private Label bestLapTime;
        private Label optimalLapTime;

        private float sessionElapsedTime = 0.0f;
        private float bestLapRecorded = 89.840f;
        private float currentLapStartTime = 0.0f;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            if (carPhysics == null)
            {
                carPhysics = FindFirstObjectByType<CarPhysicsController>(FindObjectsInactive.Include);
            }
        }

        private void OnEnable()
        {
            rootElement = uiDocument.rootVisualElement;
            if (rootElement == null) return;

            BindUIElements();

            if (SimulationCore.Instance != null)
            {
                SimulationCore.Instance.OnStateUpdated += HandleCarStateUpdated;
                SimulationCore.Instance.OnTrack1ExplainabilityUpdated += HandleTrack1Updated;
                SimulationCore.Instance.OnTrack3ExplainabilityUpdated += HandleTrack3Updated;
                SimulationCore.Instance.OnLapCompleted += HandleLapCompleted;
                SimulationCore.Instance.OnSectorCompleted += HandleSectorCompleted;
            }
        }

        private void OnDisable()
        {
            if (SimulationCore.Instance != null)
            {
                SimulationCore.Instance.OnStateUpdated -= HandleCarStateUpdated;
                SimulationCore.Instance.OnTrack1ExplainabilityUpdated -= HandleTrack1Updated;
                SimulationCore.Instance.OnTrack3ExplainabilityUpdated -= HandleTrack3Updated;
                SimulationCore.Instance.OnLapCompleted -= HandleLapCompleted;
                SimulationCore.Instance.OnSectorCompleted -= HandleSectorCompleted;
            }
        }

        private void BindUIElements()
        {
            // Header
            circuitLabel = rootElement.Q<Label>("CircuitLabel");
            systemStatusLabel = rootElement.Q<Label>("SystemStatusLabel");
            lapCounterLabel = rootElement.Q<Label>("LapCounterLabel");
            fuelRemainingLabel = rootElement.Q<Label>("FuelRemainingLabel");
            stintTimeLabel = rootElement.Q<Label>("StintTimeLabel");

            // Telemetry
            gearValue = rootElement.Q<Label>("GearValue");
            speedValue = rootElement.Q<Label>("SpeedValue");
            rpmValue = rootElement.Q<Label>("RpmValue");
            rpmBarFill = rootElement.Q<VisualElement>("RpmBarFill");
            throttleValue = rootElement.Q<Label>("ThrottleValue");
            throttleBarFill = rootElement.Q<VisualElement>("ThrottleBarFill");
            brakeValue = rootElement.Q<Label>("BrakeValue");
            brakeBarFill = rootElement.Q<VisualElement>("BrakeBarFill");
            latGValue = rootElement.Q<Label>("LatGValue");
            longGValue = rootElement.Q<Label>("LongGValue");
            drsStateLabel = rootElement.Q<Label>("DrsStateLabel");

            // Corner temps
            tyreTempFL = rootElement.Q<Label>("TyreTempFL");
            brakeTempFL = rootElement.Q<Label>("BrakeTempFL");
            tyreTempFR = rootElement.Q<Label>("TyreTempFR");
            brakeTempFR = rootElement.Q<Label>("BrakeTempFR");
            tyreTempRL = rootElement.Q<Label>("TyreTempRL");
            brakeTempRL = rootElement.Q<Label>("BrakeTempRL");
            tyreTempRR = rootElement.Q<Label>("TyreTempRR");
            brakeTempRR = rootElement.Q<Label>("BrakeTempRR");

            // Track 1
            batterySocPct = rootElement.Q<Label>("BatterySocPct");
            batteryEnergyMj = rootElement.Q<Label>("BatteryEnergyMj");
            batterySocFill = rootElement.Q<VisualElement>("BatterySocFill");
            powerFlowLabel = rootElement.Q<Label>("PowerFlowLabel");
            modeBadge = rootElement.Q<VisualElement>("ModeBadge");
            modeBadgeText = rootElement.Q<Label>("ModeBadgeText");
            gapAheadLabel = rootElement.Q<Label>("GapAheadLabel");
            closingRateLabel = rootElement.Q<Label>("ClosingRateLabel");
            overtakeProbLabel = rootElement.Q<Label>("OvertakeProbLabel");

            // Explainability
            featGapFill = rootElement.Q<VisualElement>("FeatGapFill");
            featGapVal = rootElement.Q<Label>("FeatGapVal");
            featSocFill = rootElement.Q<VisualElement>("FeatSocFill");
            featSocVal = rootElement.Q<Label>("FeatSocVal");
            featWearFill = rootElement.Q<VisualElement>("FeatWearFill");
            featWearVal = rootElement.Q<Label>("FeatWearVal");
            featThermFill = rootElement.Q<VisualElement>("FeatThermFill");
            featThermVal = rootElement.Q<Label>("FeatThermVal");

            // Track 3
            tyreCompoundBadge = rootElement.Q<Label>("TyreCompoundBadge");
            totalTyreDeltaLabel = rootElement.Q<Label>("TotalTyreDeltaLabel");
            deltaPureWearLabel = rootElement.Q<Label>("DeltaPureWearLabel");
            barPureWear = rootElement.Q<VisualElement>("BarPureWear");
            deltaThermalLabel = rootElement.Q<Label>("DeltaThermalLabel");
            barThermal = rootElement.Q<VisualElement>("BarThermal");
            deltaFuelLabel = rootElement.Q<Label>("DeltaFuelLabel");
            barFuel = rootElement.Q<VisualElement>("BarFuel");
            deltaTrafficLabel = rootElement.Q<Label>("DeltaTrafficLabel");
            barTraffic = rootElement.Q<VisualElement>("BarTraffic");
            deltaTrackLabel = rootElement.Q<Label>("DeltaTrackLabel");
            barTrack = rootElement.Q<VisualElement>("BarTrack");
            strategyAdvisoryText = rootElement.Q<Label>("StrategyAdvisoryText");

            // Timing
            sector1Time = rootElement.Q<Label>("Sector1Time");
            sector1Delta = rootElement.Q<Label>("Sector1Delta");
            sector2Time = rootElement.Q<Label>("Sector2Time");
            sector2Delta = rootElement.Q<Label>("Sector2Delta");
            sector3Time = rootElement.Q<Label>("Sector3Time");
            sector3Delta = rootElement.Q<Label>("Sector3Delta");
            lastLapTime = rootElement.Q<Label>("LastLapTime");
            bestLapTime = rootElement.Q<Label>("BestLapTime");
            optimalLapTime = rootElement.Q<Label>("OptimalLapTime");
        }

        private void Update()
        {
            sessionElapsedTime += Time.deltaTime;
            if (stintTimeLabel != null)
            {
                int minutes = (int)(sessionElapsedTime / 60.0f);
                float seconds = sessionElapsedTime % 60.0f;
                stintTimeLabel.text = $"{minutes:D2}:{seconds:05.2f}";
            }

            // Continuous telemetry polling if carPhysics is attached
            if (carPhysics != null)
            {
                UpdateDriverInputs();
            }
        }

        private void UpdateDriverInputs()
        {
            if (throttleValue != null) throttleValue.text = $"{carPhysics.ThrottleInput * 100.0f:F0}%";
            if (throttleBarFill != null) throttleBarFill.style.width = Length.Percent(carPhysics.ThrottleInput * 100.0f);

            if (brakeValue != null) brakeValue.text = $"{carPhysics.BrakeInput * 100.0f:F0}%";
            if (brakeBarFill != null) brakeBarFill.style.width = Length.Percent(carPhysics.BrakeInput * 100.0f);

            if (drsStateLabel != null)
            {
                drsStateLabel.text = carPhysics.DrsToggle == DrsState.Open ? "ACTIVE (OPEN)" : "CLOSED";
                drsStateLabel.style.color = carPhysics.DrsToggle == DrsState.Open ? new StyleColor(new Color(0.06f, 0.72f, 0.51f)) : new StyleColor(new Color(0.97f, 0.98f, 0.99f));
            }
        }

        private void HandleCarStateUpdated(CarState state)
        {
            if (lapCounterLabel != null)
            {
                int maxLaps = SimulationCore.Instance != null ? SimulationCore.Instance.TotalStintLaps : 20;
                lapCounterLabel.text = $"LAP {state.Lap} / {maxLaps}";
            }

            if (fuelRemainingLabel != null)
            {
                fuelRemainingLabel.text = $"{state.FuelLoadKg:F1} kg";
            }

            if (batterySocPct != null) batterySocPct.text = $"{state.EnergyRemainingPct:F1}%";
            if (batteryEnergyMj != null) batteryEnergyMj.text = $"{state.EnergyRemainingPct * 1.8f:F1} MJ / 180.0 MJ STINT POOL";
            if (batterySocFill != null) batterySocFill.style.width = Length.Percent(Mathf.Clamp(state.EnergyRemainingPct, 0.0f, 100.0f));

            if (gapAheadLabel != null)
            {
                gapAheadLabel.text = state.HasGapAhead ? $"+{state.GapAheadS:F2}s (DIRTY AIR: {(state.DirtyAir ? "YES" : "NO")})" : "CLEAR TRACK";
            }

            // Tyre compound badge
            if (tyreCompoundBadge != null)
            {
                tyreCompoundBadge.text = $"{state.Compound.ToString().ToUpper()} — {state.Lap} LAPS";
            }
        }

        private void HandleTrack1Updated(EnergyDeploymentExplainability exp)
        {
            if (modeBadgeText != null)
            {
                modeBadgeText.text = exp.RecommendedDeploymentMode.ToString().ToUpper();
                Color modeColor = exp.RecommendedDeploymentMode switch
                {
                    EnergyMode.Push => new Color(0.93f, 0.27f, 0.27f),       // Red
                    EnergyMode.Balanced => new Color(0.06f, 0.72f, 0.51f),   // Green
                    EnergyMode.Hold => new Color(0.96f, 0.62f, 0.04f),       // Amber
                    EnergyMode.Save => new Color(0.02f, 0.71f, 0.83f),       // Cyan
                    _ => Color.white
                };
                modeBadgeText.style.color = new StyleColor(modeColor);
            }

            float deployKw = exp.RecommendedDeploymentMode switch
            {
                EnergyMode.Push => 120.0f,
                EnergyMode.Balanced => 45.0f,
                EnergyMode.Hold => 0.0f,
                EnergyMode.Save => -40.0f,
                _ => 45.0f
            };

            if (powerFlowLabel != null)
            {
                powerFlowLabel.text = $"Deploy: {deployKw:+0.0;-0.0;0.0} kW | {exp.RecommendedBraking}";
            }

            if (closingRateLabel != null) closingRateLabel.text = $"{exp.DirtyAirOvertakeOpportunity * 0.5f:+0.00;-0.00;0.00} s / lap";
            if (overtakeProbLabel != null) overtakeProbLabel.text = $"{exp.OvertakeRiskRewardScore:F1}% ({exp.RiskCategory})";

            // Explainability Feature Bars
            if (featGapFill != null) featGapFill.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.DirtyAirOvertakeOpportunity)) * 100.0f);
            if (featGapVal != null) featGapVal.text = $"{exp.DirtyAirOvertakeOpportunity * 100.0f:+0;-0;0}%";

            if (featSocFill != null) featSocFill.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.EnergyBudgetSurplusDeficitPct / 100.0f)) * 100.0f);
            if (featSocVal != null) featSocVal.text = $"{exp.EnergyBudgetSurplusDeficitPct:+0;-0;0}%";

            if (featWearFill != null) featWearFill.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.TyreWearPenaltyFactor)) * 100.0f);
            if (featWearVal != null) featWearVal.text = $"{exp.TyreWearPenaltyFactor * -100.0f:+0;-0;0}%";

            if (featThermFill != null) featThermFill.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.BrakeFadeThermalPenalty)) * 100.0f);
            if (featThermVal != null) featThermVal.text = $"{exp.BrakeFadeThermalPenalty * -100.0f:+0;-0;0}%";
        }

        private void HandleTrack3Updated(TyreDegradationExplainability exp)
        {
            if (totalTyreDeltaLabel != null)
            {
                totalTyreDeltaLabel.text = $"{exp.TotalObservedDeltaS:+0.000;-0.000;0.000}s / lap";
            }

            // Feature breakdowns
            if (deltaPureWearLabel != null) deltaPureWearLabel.text = $"{exp.TrueTyreDegradationDeltaS:+0.000;-0.000;0.000}s";
            if (barPureWear != null) barPureWear.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.TrueTyreDegradationDeltaS) / 1.5f) * 100.0f);

            if (deltaThermalLabel != null) deltaThermalLabel.text = $"+0.120s";
            if (barThermal != null) barThermal.style.width = Length.Percent(Mathf.Clamp01(0.120f / 1.5f) * 100.0f);

            if (deltaFuelLabel != null) deltaFuelLabel.text = $"{exp.FuelCorrectionDeltaS:+0.000;-0.000;0.000}s";
            if (barFuel != null) barFuel.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.FuelCorrectionDeltaS) / 1.5f) * 100.0f);

            if (deltaTrafficLabel != null) deltaTrafficLabel.text = $"{exp.TrafficDirtyAirDeltaS:+0.000;-0.000;0.000}s";
            if (barTraffic != null) barTraffic.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.TrafficDirtyAirDeltaS) / 1.5f) * 100.0f);

            if (deltaTrackLabel != null) deltaTrackLabel.text = $"{exp.TrackEvolutionDeltaS:+0.000;-0.000;0.000}s";
            if (barTrack != null) barTrack.style.width = Length.Percent(Mathf.Clamp01(Mathf.Abs(exp.TrackEvolutionDeltaS) / 1.5f) * 100.0f);

            if (strategyAdvisoryText != null)
            {
                int remainingLaps = Mathf.Max(1, Mathf.RoundToInt((35.0f - exp.PredictedWearPct) / 1.5f));
                strategyAdvisoryText.text = $"Pure wear: {exp.PredictedWearPct:F1}%. Projected cliff in {remainingLaps} laps. Pit window open L19-L22.";
            }
        }

        private void HandleSectorCompleted(int sector)
        {
            // Example sector update
            if (sector == 1 && sector1Delta != null)
            {
                sector1Delta.text = "-0.084s";
                sector1Delta.RemoveFromClassList("delta-yellow");
                sector1Delta.AddToClassList("delta-purple");
            }
        }

        private void HandleLapCompleted(int lap)
        {
            float lapDuration = sessionElapsedTime - currentLapStartTime;
            currentLapStartTime = sessionElapsedTime;

            if (lastLapTime != null)
            {
                int min = (int)(lapDuration / 60.0f);
                float sec = lapDuration % 60.0f;
                lastLapTime.text = $"{min}:{sec:05.3f}";
            }

            if (lapDuration < bestLapRecorded && lapDuration > 30.0f)
            {
                bestLapRecorded = lapDuration;
                if (bestLapTime != null)
                {
                    int min = (int)(bestLapRecorded / 60.0f);
                    float sec = bestLapRecorded % 60.0f;
                    bestLapTime.text = $"{min}:{sec:05.3f}";
                }
            }
        }
    }
}
