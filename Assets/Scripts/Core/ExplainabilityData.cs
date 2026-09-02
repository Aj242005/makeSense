using System;
using UnityEngine;

namespace GridSense.Core
{
    /// <summary>
    /// Explainability output produced by Track 3 (Tyre Degradation Isolation Model).
    /// Regresses observed lap time delta into constituent physical confounds.
    /// </summary>
    [Serializable]
    public struct TyreDegradationExplainability
    {
        [Header("Isolated Pace Components (Seconds / Lap)")]
        public float FuelCorrectionDeltaS;     // Time gained/lost due to fuel load delta vs baseline
        public float TrafficDirtyAirDeltaS;    // Time lost due to dirty air wake & following distance
        public float TrackEvolutionDeltaS;     // Time gained due to rubbered-in track evolution
        public float TrueTyreDegradationDeltaS; // Isolated tyre pace loss (clean signal)
        public float TotalObservedDeltaS;      // Sum of all effects vs baseline reference lap

        [Header("Confidence & Validation")]
        public float PredictedWearPct;         // Isolated cumulative wear curve %
        public float ConfidenceBandLowerPct;   // Lower bound of 95% confidence interval
        public float ConfidenceBandUpperPct;   // Upper bound of 95% confidence interval
        public float GroundTruthWearPct;       // Internal physics engine true wear (for validation loop)
        public float ResidualErrorPct;         // Absolute error between prediction and ground truth

        public static TyreDegradationExplainability CreateDefault()
        {
            return new TyreDegradationExplainability
            {
                FuelCorrectionDeltaS = 0.0f,
                TrafficDirtyAirDeltaS = 0.0f,
                TrackEvolutionDeltaS = 0.0f,
                TrueTyreDegradationDeltaS = 0.0f,
                TotalObservedDeltaS = 0.0f,
                PredictedWearPct = 0.0f,
                ConfidenceBandLowerPct = 0.0f,
                ConfidenceBandUpperPct = 0.0f,
                GroundTruthWearPct = 0.0f,
                ResidualErrorPct = 0.0f
            };
        }
    }

    /// <summary>
    /// Explainability output produced by Track 1 (Energy Deployment Engine).
    /// Provides transparent rationale for recommended deployment mode, braking aggressiveness, and overtake scoring.
    /// </summary>
    [Serializable]
    public struct EnergyDeploymentExplainability
    {
        [Header("Engine Recommendations")]
        public EnergyMode RecommendedDeploymentMode;
        public BrakingAggressiveness RecommendedBraking;
        public float OvertakeRiskRewardScore;  // 0-100 score (higher = favorable overtake opportunity)
        public OvertakeRiskCategory RiskCategory;

        [Header("Attribution Factors (Weights)")]
        public float EnergyBudgetSurplusDeficitPct; // Delta vs target linear stint energy trajectory
        public float TyreWearPenaltyFactor;         // Penalty attributable to current TyreWearRateCurrent
        public float BrakeFadeThermalPenalty;       // Penalty attributable to elevated brake rotor temps
        public float DirtyAirOvertakeOpportunity;   // Incentive when trailing within DRS / overtake window

        [Header("Human-Readable Pit-Wall Rationale")]
        public string ExplanationSummary;

        public static EnergyDeploymentExplainability CreateDefault()
        {
            return new EnergyDeploymentExplainability
            {
                RecommendedDeploymentMode = EnergyMode.Balanced,
                RecommendedBraking = BrakingAggressiveness.Normal,
                OvertakeRiskRewardScore = 50.0f,
                RiskCategory = OvertakeRiskCategory.Moderate,
                EnergyBudgetSurplusDeficitPct = 0.0f,
                TyreWearPenaltyFactor = 0.0f,
                BrakeFadeThermalPenalty = 0.0f,
                DirtyAirOvertakeOpportunity = 0.0f,
                ExplanationSummary = "Target pacing maintained; nominal energy budget and thermal windows."
            };
        }
    }
}
