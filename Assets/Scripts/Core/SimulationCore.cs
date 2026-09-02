using System;
using UnityEngine;
using GridSense.Track;

namespace GridSense.Core
{
    /// <summary>
    /// SimulationCore orchestrates the fixed-timestep execution loop:
    /// 1. Updates vehicle physics, tyre thermals/wear, and energy regeneration.
    /// 2. Maintains the single source of truth CarState.
    /// 3. Triggers synchronized in-process Sentis AI inference for Track 1 & Track 3.
    /// 4. Dispatches live state to the UI Toolkit pit-wall dashboard.
    /// </summary>
    public class SimulationCore : MonoBehaviour
    {
        public static SimulationCore Instance { get; private set; }

        [Header("Live State Single Source of Truth")]
        [SerializeField] private CarState currentCarState;

        [Header("Explainability Outputs")]
        [SerializeField] private TyreDegradationExplainability tyreExplainability;
        [SerializeField] private EnergyDeploymentExplainability energyExplainability;

        [Header("Stint Configuration")]
        [SerializeField] private int totalStintLaps = 20;
        [SerializeField] private float initialFuelKg = 105.0f;
        [SerializeField] private TyreCompound initialCompound = TyreCompound.Medium;

        [Header("Fixed Timestep Timing")]
        [SerializeField] private float fixedDeltaTime = 0.02f; // 50 Hz physics/inference tick

        public CarState State => currentCarState;
        public TyreDegradationExplainability TyreExplainability => tyreExplainability;
        public EnergyDeploymentExplainability EnergyExplainability => energyExplainability;
        public int TotalStintLaps => totalStintLaps;

        // Events for UI and AI consumers
        public event Action<CarState> OnStateUpdated;
        public event Action<int> OnSectorCompleted;
        public event Action<int> OnLapCompleted;
        public event Action<TyreDegradationExplainability> OnTrack3ExplainabilityUpdated;
        public event Action<EnergyDeploymentExplainability> OnTrack1ExplainabilityUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Time.fixedDeltaTime = fixedDeltaTime;
            currentCarState = CarState.CreateDefault(initialCompound, initialFuelKg);
            tyreExplainability = TyreDegradationExplainability.CreateDefault();
            energyExplainability = EnergyDeploymentExplainability.CreateDefault();
        }

        public void InitializeForTesting()
        {
            Instance = this;
            currentCarState = CarState.CreateDefault(initialCompound, initialFuelKg);
            tyreExplainability = TyreDegradationExplainability.CreateDefault();
            energyExplainability = EnergyDeploymentExplainability.CreateDefault();
        }

        private void FixedUpdate()
        {
            // 1. Advance track position & session progression
            if (TrackManager.Instance != null && TrackManager.Instance.ActiveTrack != null)
            {
                float trackLength = TrackManager.Instance.ActiveTrack.TotalLengthMeters;
                currentCarState.Sector = TrackManager.Instance.GetSectorForDistance(currentCarState.DistanceIntoLapM);
                currentCarState.TrackEvolutionFactor = TrackManager.Instance.GetTrackEvolutionMultiplier();

                float stintProgress = (float)(currentCarState.Lap - 1) / Mathf.Max(1, totalStintLaps);
                TrackManager.Instance.SetSessionProgression(stintProgress);
            }

            // 2. Dispatch updated CarState to registered listeners (Dashboard, AI controllers)
            OnStateUpdated?.Invoke(currentCarState);

            // 3. Persist 50Hz session telemetry snapshot
            TelemetryDatabase.Instance?.LogSample(currentCarState, tyreExplainability, energyExplainability);
        }

        /// <summary>
        /// Updates the current CarState from physics systems.
        /// </summary>
        public void UpdatePhysicsState(CarState newState)
        {
            currentCarState = newState;
        }

        /// <summary>
        /// Injects AI Track 3 tyre degradation inference output and explainability.
        /// </summary>
        public void UpdateTyreInference(float wearPct, float wearRateSlope, TyreDegradationExplainability explain)
        {
            currentCarState.TyreWearPct = wearPct;
            currentCarState.TyreWearRateCurrent = wearRateSlope;
            tyreExplainability = explain;
            OnTrack3ExplainabilityUpdated?.Invoke(explain);
        }

        public void UpdateTrack3Explainability(TyreDegradationExplainability explain)
        {
            tyreExplainability = explain;
            currentCarState.TyreWearPct = explain.PredictedWearPct;
            OnTrack3ExplainabilityUpdated?.Invoke(explain);
        }

        /// <summary>
        /// Injects AI Track 1 energy deployment recommendations and explainability.
        /// </summary>
        public void UpdateEnergyInference(EnergyMode mode, BrakingAggressiveness braking, EnergyDeploymentExplainability explain)
        {
            currentCarState.DeploymentMode = mode;
            currentCarState.Braking = braking;
            energyExplainability = explain;
            OnTrack1ExplainabilityUpdated?.Invoke(explain);
        }

        public void UpdateTrack1Explainability(EnergyDeploymentExplainability explain)
        {
            energyExplainability = explain;
            currentCarState.DeploymentMode = explain.RecommendedDeploymentMode;
            currentCarState.Braking = explain.RecommendedBraking;
            OnTrack1ExplainabilityUpdated?.Invoke(explain);
        }

        /// <summary>
        /// Explicit step method for testing and batch simulations.
        /// </summary>
        public void StepSimulation(float dt)
        {
            if (TrackManager.Instance != null && TrackManager.Instance.ActiveTrack != null)
            {
                currentCarState.Sector = TrackManager.Instance.GetSectorForDistance(currentCarState.DistanceIntoLapM);
                currentCarState.TrackEvolutionFactor = TrackManager.Instance.GetTrackEvolutionMultiplier();

                float stintProgress = (float)(currentCarState.Lap - 1) / Mathf.Max(1, totalStintLaps);
                TrackManager.Instance.SetSessionProgression(stintProgress);
            }

            // Advance distance, fuel burn, and thermals linearly if in test simulation
            currentCarState.DistanceIntoLapM += 65.0f * dt; // ~234 km/h
            currentCarState.FuelLoadKg = Mathf.Max(0.0f, currentCarState.FuelLoadKg - (0.0018f * dt));
            currentCarState.TyreTempC = Mathf.Clamp(currentCarState.TyreTempC + (0.05f * dt), 80.0f, 130.0f);
            currentCarState.TyreWearPct = Mathf.Clamp(currentCarState.TyreWearPct + (0.02f * dt), 0.0f, 100.0f);
            currentCarState.EnergyRemainingPct = Mathf.Clamp(currentCarState.EnergyRemainingPct - (0.08f * dt), 0.0f, 100.0f);

            OnStateUpdated?.Invoke(currentCarState);
            TelemetryDatabase.Instance?.LogSample(currentCarState, tyreExplainability, energyExplainability);
        }

        /// <summary>
        /// Handles sector completion trigger.
        /// </summary>
        public void TriggerSectorCompleted(int sector)
        {
            OnSectorCompleted?.Invoke(sector);
        }

        /// <summary>
        /// Handles lap completion trigger with custom sector splits.
        /// </summary>
        public void TriggerLapCompleted(float lapTime, float s1, float s2, float s3)
        {
            int completedLap = currentCarState.Lap;
            currentCarState.Lap = completedLap + 1;
            currentCarState.DistanceIntoLapM = 0.0f;
            OnLapCompleted?.Invoke(completedLap);

            float fuelUsed = initialFuelKg - currentCarState.FuelLoadKg;
            float energyUsed = (100.0f - currentCarState.EnergyRemainingPct) * 1.8f;
            TelemetryDatabase.Instance?.LogLapCompleted(completedLap, lapTime, s1, s2, s3, fuelUsed, energyUsed, currentCarState.TyreWearPct);

            Debug.Log($"[SimulationCore] Completed Lap {completedLap} (Time: {lapTime:F3}s, S1: {s1:F3}s, S2: {s2:F3}s, S3: {s3:F3}s). Advancing to Lap {currentCarState.Lap}.");
        }

        /// <summary>
        /// Handles lap completion trigger.
        /// </summary>
        public void TriggerLapCompleted(int lap)
        {
            currentCarState.Lap = lap + 1;
            currentCarState.DistanceIntoLapM = 0.0f;
            OnLapCompleted?.Invoke(lap);
            
            float fuelUsed = initialFuelKg - currentCarState.FuelLoadKg;
            float energyUsed = (100.0f - currentCarState.EnergyRemainingPct) * 1.8f;
            TelemetryDatabase.Instance?.LogLapCompleted(lap, 0.0f, 0.0f, 0.0f, 0.0f, fuelUsed, energyUsed, currentCarState.TyreWearPct);

            Debug.Log($"[SimulationCore] Completed Lap {lap}. Advancing to Lap {currentCarState.Lap}. Fuel remaining: {currentCarState.FuelLoadKg:F1}kg, Tyre Wear: {currentCarState.TyreWearPct:F1}%");
        }
    }
}
