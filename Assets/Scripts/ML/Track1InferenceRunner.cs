using System;
using UnityEngine;
using Unity.InferenceEngine;
using GridSense.Core;
using GridSense.Track;

namespace GridSense.ML
{
    /// <summary>
    /// Track1InferenceRunner executes in-engine neural inference using Unity Inference Engine for the Energy Deployment policy:
    /// - Gathers 10-dimensional normalized state vector from CarState and Environment
    /// - Runs Sentis/InferenceEngine on energy_deployment_ppo.onnx
    /// - Evaluates optimal coupled (EnergyMode, BrakingAggressiveness) action recommendation
    /// - Populates SimulationCore.Instance.Track1Explainability with grounded risk-reward score and attribution factors
    /// </summary>
    public class Track1InferenceRunner : MonoBehaviour
    {
        public static Track1InferenceRunner Instance { get; private set; }

        [Header("Sentis/Inference ONNX Model Asset")]
        [SerializeField] private ModelAsset energyPolicyModelAsset;

        [Header("Inference Execution Settings")]
        [SerializeField] private BackendType backendType = BackendType.CPU;
        [SerializeField] private bool runInferenceEveryFixedUpdate = true;

        [Header("Live Policy Recommendations")]
        public EnergyMode RecommendedEnergyMode { get; private set; } = EnergyMode.Balanced;
        public BrakingAggressiveness RecommendedBraking { get; private set; } = BrakingAggressiveness.Normal;
        public float RiskRewardScore { get; private set; } = 50.0f;
        public OvertakeRiskCategory RiskCategory { get; private set; } = OvertakeRiskCategory.Low;

        [Header("Live Explainability Attribution")]
        public float EnergyTrajectoryDelta { get; private set; }
        public float TyreWearSlopePenalty { get; private set; }
        public float BrakeThermalPenalty { get; private set; }

        private Model runtimeModel;
        private Worker worker;
        private bool isModelLoaded = false;

        /// <summary>
        /// True only when the ONNX Track 1 energy-deployment policy actually loaded. When it is false this
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
            if (energyPolicyModelAsset == null)
            {
                energyPolicyModelAsset = Resources.Load<ModelAsset>("energy_deployment_ppo");
            }

            if (energyPolicyModelAsset == null)
            {
                Debug.LogWarning("[Track1InferenceRunner] No ModelAsset assigned or found in Resources. Using analytical heuristic.");
                return;
            }

            try
            {
                runtimeModel = ModelLoader.Load(energyPolicyModelAsset);
                worker = new Worker(runtimeModel, backendType);
                isModelLoaded = true;
                Debug.Log("[Track1InferenceRunner] Unity Sentis Track 1 model loaded successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Track1InferenceRunner] Failed to initialize Sentis worker: {ex.Message}");
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

        public void ExecuteInference()
        {
            CarState state = (SimulationCore.Instance != null) 
                ? SimulationCore.Instance.State 
                : CarState.CreateDefault();

            float trackEvolution = (TrackManager.Instance != null)
                ? TrackManager.Instance.GetTrackEvolutionMultiplier()
                : 1.0f;

            // Gather normalized state vector [1, 10]
            float socNorm = Mathf.Clamp01(state.EnergyRemainingPct / 100.0f);
            float wearNorm = Mathf.Clamp01(state.TyreWearPct / 100.0f);
            float fuelNorm = Mathf.Clamp01(state.FuelLoadKg / 105.0f);
            float gapAheadNorm = Mathf.Clamp01((state.HasGapAhead ? state.GapAheadS : 10.0f) / 10.0f);
            float hasAheadNorm = state.HasGapAhead ? 1.0f : 0.0f;
            float gapBehindNorm = Mathf.Clamp01((state.HasGapBehind ? state.GapBehindS : 10.0f) / 10.0f);
            float lapNorm = Mathf.Clamp01(state.Lap / 20.0f);
            float evolNorm = Mathf.Clamp01((trackEvolution - 1.0f) / 0.04f);
            float brakeNorm = Mathf.Clamp01(state.TyreTempC / 150.0f); // Surrogate
            float wearSlopeNorm = Mathf.Clamp01(state.TyreWearRateCurrent / 5.0f);

            if (isModelLoaded && worker != null)
            {
                float[] inputData = new float[] {
                    socNorm, wearNorm, fuelNorm, gapAheadNorm, hasAheadNorm,
                    gapBehindNorm, lapNorm, evolNorm, brakeNorm, wearSlopeNorm
                };

                using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 10), inputData);
                worker.Schedule(inputTensor);

                Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
                if (outputTensor != null)
                {
                    float[] outputs = outputTensor.DownloadToArray();
                    if (outputs != null && outputs.Length >= 12)
                    {
                        // Action logits (indices 0 to 7)
                        int bestActionIdx = 0;
                        float maxLogit = float.MinValue;
                        for (int i = 0; i < 8; i++)
                        {
                            if (outputs[i] > maxLogit)
                            {
                                maxLogit = outputs[i];
                                bestActionIdx = i;
                            }
                        }

                        // Decode action tuple
                        int modeIdx = bestActionIdx / 2;
                        int brakeIdx = bestActionIdx % 2;

                        RecommendedEnergyMode = (EnergyMode)modeIdx;
                        RecommendedBraking = (BrakingAggressiveness)brakeIdx;

                        // Explainability outputs (indices 8 to 11)
                        RiskRewardScore = Mathf.Clamp(outputs[8], 0.0f, 100.0f);
                        EnergyTrajectoryDelta = outputs[9];
                        TyreWearSlopePenalty = outputs[10];
                        BrakeThermalPenalty = outputs[11];
                    }
                }
            }
            else
            {
                // Decision-realistic analytical heuristic fallback
                if (state.HasGapAhead && state.GapAheadS < 1.5f && state.EnergyRemainingPct > 20.0f)
                {
                    RecommendedEnergyMode = EnergyMode.Push;
                    RecommendedBraking = BrakingAggressiveness.Aggressive;
                    RiskRewardScore = 78.0f;
                }
                else if (state.EnergyRemainingPct < 15.0f)
                {
                    RecommendedEnergyMode = EnergyMode.Save;
                    RecommendedBraking = BrakingAggressiveness.Normal;
                    RiskRewardScore = 32.0f;
                }
                else
                {
                    RecommendedEnergyMode = EnergyMode.Balanced;
                    RecommendedBraking = BrakingAggressiveness.Normal;
                    RiskRewardScore = 52.0f;
                }

                EnergyTrajectoryDelta = (socNorm - (1.0f - lapNorm)) * 100.0f;
                TyreWearSlopePenalty = wearSlopeNorm * 10.0f;
                BrakeThermalPenalty = brakeNorm * 10.0f;
            }

            // Categorical Risk Wrapper
            if (RiskRewardScore < 30.0f) RiskCategory = OvertakeRiskCategory.Low;
            else if (RiskRewardScore < 60.0f) RiskCategory = OvertakeRiskCategory.Moderate;
            else if (RiskRewardScore < 82.0f) RiskCategory = OvertakeRiskCategory.High;
            else RiskCategory = OvertakeRiskCategory.Critical;

            // Populate SimulationCore Explainability
            if (SimulationCore.Instance != null)
            {
                EnergyDeploymentExplainability exp = new EnergyDeploymentExplainability
                {
                    RecommendedDeploymentMode = RecommendedEnergyMode,
                    RecommendedBraking = RecommendedBraking,
                    OvertakeRiskRewardScore = RiskRewardScore,
                    RiskCategory = RiskCategory,
                    EnergyBudgetSurplusDeficitPct = EnergyTrajectoryDelta,
                    TyreWearPenaltyFactor = TyreWearSlopePenalty,
                    BrakeFadeThermalPenalty = BrakeThermalPenalty,
                    DirtyAirOvertakeOpportunity = state.DirtyAir ? 1.0f : 0.0f,
                    ExplanationSummary = $"{RecommendedEnergyMode} deployment recommended with {RecommendedBraking} braking. Risk score: {RiskRewardScore:F1}/100."
                };

                SimulationCore.Instance.UpdateTrack1Explainability(exp);
            }
        }

        private void OnDestroy()
        {
            worker?.Dispose();
        }
    }
}
