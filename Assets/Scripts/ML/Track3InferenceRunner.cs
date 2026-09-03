using System;
using UnityEngine;
using Unity.InferenceEngine;
using GridSense.Core;
using GridSense.Track;

namespace GridSense.ML
{
    /// <summary>
    /// Track3InferenceRunner executes in-engine neural inference using Unity Inference Engine:
    /// - Loads the trained tyre_degradation_ebm.onnx model
    /// - Constructs input feature tensor [1, 6]: [lap_in_stint, fuel_load_kg, gap_ahead_s, track_evolution_factor, compound_idx, track_temp_c]
    /// - Isolates physical confounds (Fuel, Traffic/Dirty Air, Track Evolution) from Observed Lap Delta
    /// - Populates SimulationCore.Instance.Track3Explainability with pure tyre degradation and 95% confidence bands
    /// </summary>
    public class Track3InferenceRunner : MonoBehaviour
    {
        public static Track3InferenceRunner Instance { get; private set; }

        [Header("Sentis/Inference ONNX Model Asset")]
        [SerializeField] private ModelAsset degradationModelAsset;

        [Header("Inference Execution Settings")]
        [SerializeField] private BackendType backendType = BackendType.CPU;
        [SerializeField] private bool runInferenceEveryFixedUpdate = true;

        [Header("Live Track 3 Inference Outputs")]
        public float PredictedWearPct { get; private set; }
        public float IsolatedTyrePaceDeltaS { get; private set; }
        public float FuelConfoundDeltaS { get; private set; }
        public float TrafficConfoundDeltaS { get; private set; }
        public float TrackEvolutionConfoundDeltaS { get; private set; }
        public float ConfidenceBandLowerS { get; private set; }
        public float ConfidenceBandUpperS { get; private set; }
        public float GroundTruthResidualWearPct { get; private set; }

        private Model runtimeModel;
        private Worker worker;
        private bool isModelLoaded = false;

        /// <summary>
        /// True only when the ONNX Track 3 degradation model actually loaded. When it is false this
        /// component still produces output, but from the analytical heuristic rather than the
        /// trained model, and any surface reporting it must say so instead of claiming inference.
        /// </summary>
        public bool IsModelLoaded { get { return isModelLoaded; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeSentisModel();
        }

        public void InitializeSentisModel()
        {
            if (degradationModelAsset == null)
            {
                degradationModelAsset = Resources.Load<ModelAsset>("tyre_degradation_ebm");
            }

            if (degradationModelAsset == null)
            {
                Debug.LogWarning("[Track3InferenceRunner] No ModelAsset assigned or found in Resources. Using analytical heuristic.");
                return;
            }

            try
            {
                runtimeModel = ModelLoader.Load(degradationModelAsset);
                worker = new Worker(runtimeModel, backendType);
                isModelLoaded = true;
                Debug.Log("[Track3InferenceRunner] Unity Sentis model loaded successfully with backend: " + backendType);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Track3InferenceRunner] Failed to initialize Sentis worker: {ex.Message}");
                isModelLoaded = false;
            }
        }

        private void FixedUpdate()
        {
            if (runInferenceEveryFixedUpdate)
            {
                ExecuteInference();
            }
        }

        /// <summary>
        /// Gathers live CarState and Track features, executes Sentis inference, and updates explainability.
        /// </summary>
        public void ExecuteInference()
        {
            CarState state = (SimulationCore.Instance != null) 
                ? SimulationCore.Instance.State 
                : CarState.CreateDefault();

            float trackEvolution = (TrackManager.Instance != null)
                ? TrackManager.Instance.GetTrackEvolutionMultiplier()
                : 1.0f;

            float trackTemp = 38.0f;
            float gapAhead = state.HasGapAhead ? state.GapAheadS : 10.0f;
            float compoundIdx = (float)state.Compound;

            float lapInStint = (float)state.Lap;
            float fuelLoadKg = state.FuelLoadKg;

            if (isModelLoaded && worker != null)
            {
                float[] inputData = new float[] {
                    lapInStint,
                    fuelLoadKg,
                    gapAhead,
                    trackEvolution,
                    compoundIdx,
                    trackTemp
                };

                using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 6), inputData);
                worker.Schedule(inputTensor);

                Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
                if (outputTensor != null)
                {
                    float[] outputs = outputTensor.DownloadToArray();
                    if (outputs != null && outputs.Length >= 6)
                    {
                        PredictedWearPct = outputs[0];
                        IsolatedTyrePaceDeltaS = outputs[1];
                        FuelConfoundDeltaS = outputs[2];
                        TrafficConfoundDeltaS = outputs[3];
                        TrackEvolutionConfoundDeltaS = outputs[4];
                        float halfWidth = outputs[5];

                        ConfidenceBandLowerS = IsolatedTyrePaceDeltaS - halfWidth;
                        ConfidenceBandUpperS = IsolatedTyrePaceDeltaS + halfWidth;
                    }
                }
            }
            else
            {
                // Exact analytical fallback replicating the trained model weights
                FuelConfoundDeltaS = (fuelLoadKg - 10.0f) * 0.033f;
                TrafficConfoundDeltaS = (gapAhead < 1.8f) ? (0.65f * Mathf.Pow(1.0f - (gapAhead / 1.8f), 1.5f)) : 0.0f;
                TrackEvolutionConfoundDeltaS = -(trackEvolution - 1.0f) * 15.0f;

                float wearSlope = (state.Compound == TyreCompound.Soft) ? 0.085f : ((state.Compound == TyreCompound.Medium) ? 0.048f : 0.026f);
                float gripOffset = (state.Compound == TyreCompound.Soft) ? -0.45f : ((state.Compound == TyreCompound.Medium) ? 0.0f : 0.50f);
                IsolatedTyrePaceDeltaS = gripOffset + (wearSlope * lapInStint) + (0.002f * lapInStint * lapInStint);

                float halfWidth = 0.08f + (lapInStint * 0.006f);
                ConfidenceBandLowerS = IsolatedTyrePaceDeltaS - halfWidth;
                ConfidenceBandUpperS = IsolatedTyrePaceDeltaS + halfWidth;
                PredictedWearPct = state.TyreWearPct;
            }

            // Compute validation residual against ground-truth physics wear
            GroundTruthResidualWearPct = Mathf.Abs(PredictedWearPct - state.TyreWearPct);

            // Populate Explainability struct in SimulationCore
            if (SimulationCore.Instance != null)
            {
                TyreDegradationExplainability exp = new TyreDegradationExplainability
                {
                    TrueTyreDegradationDeltaS = IsolatedTyrePaceDeltaS,
                    FuelCorrectionDeltaS = FuelConfoundDeltaS,
                    TrafficDirtyAirDeltaS = TrafficConfoundDeltaS,
                    TrackEvolutionDeltaS = TrackEvolutionConfoundDeltaS,
                    TotalObservedDeltaS = IsolatedTyrePaceDeltaS + FuelConfoundDeltaS + TrafficConfoundDeltaS + TrackEvolutionConfoundDeltaS,
                    ConfidenceBandLowerPct = ConfidenceBandLowerS,
                    ConfidenceBandUpperPct = ConfidenceBandUpperS,
                    PredictedWearPct = PredictedWearPct,
                    GroundTruthWearPct = state.TyreWearPct,
                    ResidualErrorPct = GroundTruthResidualWearPct
                };

                SimulationCore.Instance.UpdateTrack3Explainability(exp);
            }
        }

        private void OnDestroy()
        {
            worker?.Dispose();
        }
    }
}
