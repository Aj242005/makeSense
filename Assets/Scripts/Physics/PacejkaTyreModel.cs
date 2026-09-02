using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.Physics
{
    /// <summary>
    /// Configuration profile for a tyre compound.
    /// Defines base friction, Magic Formula (Pacejka) coefficients, thermal window, and wear multiplier.
    /// </summary>
    [Serializable]
    public struct TyreCompoundProfile
    {
        public TyreCompound Compound;
        public string Name;

        [Header("Grip & Friction")]
        public float BasePeakFriction;          // Peak friction coefficient mu (e.g. 1.25 for Soft, 1.10 for Med, 0.95 for Hard)
        public float OptimalSlipRatio;          // Slip ratio at peak longitudinal grip (e.g. 0.08 - 0.12)
        public float OptimalSlipAngleDeg;       // Slip angle at peak lateral grip (e.g. 6.0 - 8.5 deg)

        [Header("Pacejka Magic Formula Coefficients (B, C, D, E)")]
        public float StiffnessB;                // Stiffness factor
        public float ShapeC;                    // Shape factor (typically 1.3 - 1.65)
        public float CurvatureE;                // Curvature factor (typically -0.5 to 0.5)

        [Header("Thermal Operating Window (Celsius)")]
        public float MinOperatingTempC;         // Lower threshold for grip drop-off (e.g. 80°C)
        public float OptimalTempMinC;           // Optimal grip lower band (e.g. 90°C)
        public float OptimalTempMaxC;           // Optimal grip upper band (e.g. 110°C)
        public float MaxOperatingTempC;         // Upper threshold for blistering/overheating drop-off (e.g. 130°C)

        [Header("Degradation & Wear Rate")]
        public float BaseWearRateMultiplier;    // Wear rate scaling (e.g. 1.8x for Soft, 1.0x for Med, 0.55x for Hard)

        public static TyreCompoundProfile GetDefault(TyreCompound compound)
        {
            switch (compound)
            {
                case TyreCompound.Soft:
                    return new TyreCompoundProfile
                    {
                        Compound = TyreCompound.Soft,
                        Name = "Soft (C4/C5)",
                        BasePeakFriction = 1.28f,
                        OptimalSlipRatio = 0.09f,
                        OptimalSlipAngleDeg = 6.5f,
                        StiffnessB = 10.0f,
                        ShapeC = 1.65f,
                        CurvatureE = -0.5f,
                        MinOperatingTempC = 75.0f,
                        OptimalTempMinC = 85.0f,
                        OptimalTempMaxC = 105.0f,
                        MaxOperatingTempC = 125.0f,
                        BaseWearRateMultiplier = 1.75f
                    };
                case TyreCompound.Hard:
                    return new TyreCompoundProfile
                    {
                        Compound = TyreCompound.Hard,
                        Name = "Hard (C1/C2)",
                        BasePeakFriction = 1.02f,
                        OptimalSlipRatio = 0.11f,
                        OptimalSlipAngleDeg = 8.0f,
                        StiffnessB = 8.5f,
                        ShapeC = 1.45f,
                        CurvatureE = -0.3f,
                        MinOperatingTempC = 95.0f,
                        OptimalTempMinC = 105.0f,
                        OptimalTempMaxC = 125.0f,
                        MaxOperatingTempC = 145.0f,
                        BaseWearRateMultiplier = 0.55f
                    };
                case TyreCompound.Medium:
                default:
                    return new TyreCompoundProfile
                    {
                        Compound = TyreCompound.Medium,
                        Name = "Medium (C3)",
                        BasePeakFriction = 1.15f,
                        OptimalSlipRatio = 0.10f,
                        OptimalSlipAngleDeg = 7.2f,
                        StiffnessB = 9.2f,
                        ShapeC = 1.55f,
                        CurvatureE = -0.4f,
                        MinOperatingTempC = 85.0f,
                        OptimalTempMinC = 95.0f,
                        OptimalTempMaxC = 115.0f,
                        MaxOperatingTempC = 135.0f,
                        BaseWearRateMultiplier = 1.00f
                    };
            }
        }
    }

    /// <summary>
    /// Pacejka Magic Formula combined slip tyre model.
    /// Evaluates longitudinal force Fx and lateral force Fy based on slip ratio, slip angle, normal load Fz,
    /// thermal grip multiplier, and internal wear state.
    /// </summary>
    [Serializable]
    public class PacejkaTyreModel
    {
        [Header("Active Compound & Setup")]
        public TyreCompoundProfile Profile;
        public float PressurePsi = 22.5f;             // Nominal F1 front/rear tyre pressure (20 - 25 psi)
        public float OptimalPressurePsi = 22.5f;

        [Header("Live State")]
        public float NormalLoadN = 4000.0f;           // Vertical tyre load Fz
        public float SlipRatio = 0.0f;                // Longitudinal slip ratio kappa (-1.0 to 1.0)
        public float SlipAngleRad = 0.0f;             // Lateral slip angle alpha in radians
        public float LongitudinalForceN = 0.0f;       // Output Fx
        public float LateralForceN = 0.0f;            // Output Fy
        public float CombinedSlipFactor = 1.0f;       // Friction ellipse scale
        public float DissipatedPowerWatts = 0.0f;     // Slip energy power (feeds thermal and wear models)

        public PacejkaTyreModel(TyreCompound compound = TyreCompound.Medium)
        {
            Profile = TyreCompoundProfile.GetDefault(compound);
        }

        public void SetCompound(TyreCompound compound)
        {
            Profile = TyreCompoundProfile.GetDefault(compound);
        }

        /// <summary>
        /// Evaluates Pacejka Magic Formula: F = D * sin(C * atan(B * s - E * (B * s - atan(B * s))))
        /// </summary>
        public float EvaluateMagicFormula(float slip, float normalLoadN, float peakFrictionMu)
        {
            if (normalLoadN <= 1.0f) return 0.0f;

            float D = normalLoadN * peakFrictionMu; // Peak force
            float B = Profile.StiffnessB;
            float C = Profile.ShapeC;
            float E = Profile.CurvatureE;

            float Bx = B * slip;
            float force = D * Mathf.Sin(C * Mathf.Atan(Bx - E * (Bx - Mathf.Atan(Bx))));
            return force;
        }

        /// <summary>
        /// Computes combined slip forces using friction ellipse interaction,
        /// scaled by thermal and wear modifiers.
        /// </summary>
        public void ComputeForces(
            float slipRatio, 
            float slipAngleRad, 
            float normalLoadN, 
            float thermalGripMult, 
            float wearGripMult, 
            float surfaceVelocityMps)
        {
            SlipRatio = Mathf.Clamp(slipRatio, -1.0f, 1.0f);
            SlipAngleRad = slipAngleRad;
            NormalLoadN = Mathf.Max(0.0f, normalLoadN);

            // Pressure effect modifier (parabolic falloff around optimal PSI)
            float pressureDelta = PressurePsi - OptimalPressurePsi;
            float pressureGripMult = Mathf.Clamp01(1.0f - 0.015f * pressureDelta * pressureDelta);

            // Total combined peak friction coefficient
            float effectiveMu = Profile.BasePeakFriction * thermalGripMult * wearGripMult * pressureGripMult;

            // Pure longitudinal force Fx0
            float fx0 = EvaluateMagicFormula(SlipRatio / Mathf.Max(0.01f, Profile.OptimalSlipRatio), NormalLoadN, effectiveMu);

            // Pure lateral force Fy0 (slip angle normalized by optimal slip angle in radians)
            float optimalSlipAngleRad = Profile.OptimalSlipAngleDeg * Mathf.Deg2Rad;
            float fy0 = EvaluateMagicFormula(SlipAngleRad / Mathf.Max(0.01f, optimalSlipAngleRad), NormalLoadN, effectiveMu);

            // Combined slip coupling via friction ellipse:
            // sigma = sqrt((kappa / kappa_opt)^2 + (alpha / alpha_opt)^2)
            float normKappa = SlipRatio / Mathf.Max(0.01f, Profile.OptimalSlipRatio);
            float normAlpha = SlipAngleRad / Mathf.Max(0.01f, optimalSlipAngleRad);
            float combinedSlipNorm = Mathf.Sqrt(normKappa * normKappa + normAlpha * normAlpha);

            if (combinedSlipNorm > 1.0f)
            {
                CombinedSlipFactor = 1.0f / combinedSlipNorm;
                LongitudinalForceN = fx0 * CombinedSlipFactor;
                LateralForceN = fy0 * CombinedSlipFactor;
            }
            else
            {
                CombinedSlipFactor = 1.0f;
                LongitudinalForceN = fx0;
                LateralForceN = fy0;
            }

            // Calculate instantaneous dissipated slip power (Watts = N * m/s)
            // Power = |Fx * v_sx| + |Fy * v_sy|
            float slipSpeedLongitudinal = Mathf.Abs(SlipRatio * surfaceVelocityMps);
            float slipSpeedLateral = Mathf.Abs(Mathf.Sin(SlipAngleRad) * surfaceVelocityMps);
            DissipatedPowerWatts = (Mathf.Abs(LongitudinalForceN) * slipSpeedLongitudinal) + 
                                   (Mathf.Abs(LateralForceN) * slipSpeedLateral);
        }
    }
}
