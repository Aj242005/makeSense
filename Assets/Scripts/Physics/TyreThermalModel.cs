using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.Physics
{
    /// <summary>
    /// TyreThermalModel simulates the thermodynamic and wear evolution of an individual tyre:
    /// 1. Temperature rise from mechanical friction / slip power dissipation.
    /// 2. Thermal conduction with track surface and convective cooling with ambient air at vehicle velocity.
    /// 3. Grip multiplier curve based on operating temperature window.
    /// 4. Ground-truth cumulative wear calculation (0 - 100%) driven by dissipated slip energy.
    /// </summary>
    [Serializable]
    public class TyreThermalModel
    {
        [Header("Thermal State")]
        public float CarcassTempC = 95.0f;           // Core carcass / bulk temperature in °C
        public float SurfaceTempC = 95.0f;           // Outer rubber tread surface temperature in °C
        public float OptimalTempMinC = 95.0f;        // Min bound for peak grip
        public float OptimalTempMaxC = 115.0f;       // Max bound for peak grip
        public float ThermalGripMultiplier = 1.0f;   // 0.0 - 1.0 grip factor based on temperature

        [Header("Thermal Physics Parameters")]
        public float ThermalMassJoulesPerC = 14500.0f; // Tyre carcass heat capacity (~14.5 kJ/°C)
        public float ConductionRateWPerC = 18.0f;      // Track surface contact conduction rate
        public float BaseConvectiveCoolingWPerC = 12.0f; // Static air cooling rate
        public float VelocityCoolingFactor = 0.85f;    // Convective air speed cooling factor (W/°C per m/s)

        [Header("Wear State (Ground Truth)")]
        [Range(0.0f, 100.0f)]
        public float GroundTruthWearPct = 0.0f;       // Cumulative physical wear percentage (0.0 to 100.0%)
        public float InstantaneousWearRateSlope = 0.0f; // % wear per second (feeds TyreWearRateCurrent)
        public float WearGripMultiplier = 1.0f;       // Grip reduction from cumulative wear

        // Wear scaling coefficient: converts Joules of slip energy to % wear
        private const float EnergyToWearCoeff = 1.25e-7f;

        public TyreThermalModel(TyreCompoundProfile profile, float initialTempC = 95.0f)
        {
            ApplyProfile(profile);
            CarcassTempC = initialTempC;
            SurfaceTempC = initialTempC;
        }

        public void ApplyProfile(TyreCompoundProfile profile)
        {
            OptimalTempMinC = profile.OptimalTempMinC;
            OptimalTempMaxC = profile.OptimalTempMaxC;
        }

        /// <summary>
        /// Integrates thermal energy and wear progression over FixedUpdate timestep dt.
        /// </summary>
        public void UpdateThermalAndWear(
            float dissipatedPowerWatts, 
            float vehicleSpeedMps, 
            float trackTempC, 
            float ambientTempC, 
            float compoundWearMultiplier, 
            float dt)
        {
            if (dt <= 0.0f) return;

            // 1. Heat generation from friction power (Watts = Joules / sec)
            float heatInputWatts = dissipatedPowerWatts * 0.92f; // ~92% of slip work converts directly to heat

            // 2. Convective cooling from relative air velocity: Q_conv = (h_base + h_v * v) * (T_tyre - T_ambient)
            float convectiveHeatTransferCoeff = BaseConvectiveCoolingWPerC + (VelocityCoolingFactor * vehicleSpeedMps);
            float convectiveLossWatts = convectiveHeatTransferCoeff * (CarcassTempC - ambientTempC);

            // 3. Conduction with track surface contact patch: Q_cond = k_cond * (T_tyre - T_track)
            float conductiveLossWatts = ConductionRateWPerC * (CarcassTempC - trackTempC);

            // 4. Net temperature delta: dT = (Q_in - Q_loss) / C_thermal * dt
            float netHeatWatts = heatInputWatts - convectiveLossWatts - conductiveLossWatts;
            float tempDelta = (netHeatWatts / ThermalMassJoulesPerC) * dt;

            CarcassTempC = Mathf.Clamp(CarcassTempC + tempDelta, ambientTempC - 5.0f, 180.0f);
            SurfaceTempC = Mathf.Lerp(SurfaceTempC, CarcassTempC + (heatInputWatts * 0.005f), dt * 4.0f);

            // 5. Evaluate Thermal Grip Multiplier
            // Parabolic / smooth plateau curve around optimal window
            if (CarcassTempC >= OptimalTempMinC && CarcassTempC <= OptimalTempMaxC)
            {
                ThermalGripMultiplier = 1.0f; // Peak operating window
            }
            else if (CarcassTempC < OptimalTempMinC)
            {
                // Cold tyre penalty (under-temperature falloff down to ~0.72)
                float coldDelta = OptimalTempMinC - CarcassTempC;
                ThermalGripMultiplier = Mathf.Clamp(1.0f - (0.012f * coldDelta + 0.0003f * coldDelta * coldDelta), 0.65f, 1.0f);
            }
            else
            {
                // Overheating / blistering penalty (over-temperature falloff down to ~0.70)
                float hotDelta = CarcassTempC - OptimalTempMaxC;
                ThermalGripMultiplier = Mathf.Clamp(1.0f - (0.015f * hotDelta + 0.0004f * hotDelta * hotDelta), 0.60f, 1.0f);
            }

            // 6. Integrate Cumulative Wear from Dissipated Slip Energy
            // Slip Energy in Joules = Power (Watts) * dt (seconds)
            float slipEnergyJoules = dissipatedPowerWatts * dt;
            float deltaWearPct = slipEnergyJoules * EnergyToWearCoeff * compoundWearMultiplier;

            // Elevated temperature amplifies wear exponentially if overheating (> OptimalTempMaxC)
            if (CarcassTempC > OptimalTempMaxC)
            {
                float overheatRatio = (CarcassTempC - OptimalTempMaxC) / 20.0f;
                deltaWearPct *= (1.0f + overheatRatio * 1.5f);
            }

            GroundTruthWearPct = Mathf.Clamp(GroundTruthWearPct + deltaWearPct, 0.0f, 100.0f);
            InstantaneousWearRateSlope = deltaWearPct / dt; // % wear per second

            // 7. Wear Grip Multiplier (progressive non-linear grip drop as tread thins)
            // 0 - 30% wear: ~99% grip; 30 - 70% wear: linear decline; >70% wear: cliff
            float wearFraction = GroundTruthWearPct / 100.0f;
            if (wearFraction < 0.25f)
            {
                WearGripMultiplier = 1.0f - (0.04f * wearFraction / 0.25f);
            }
            else if (wearFraction < 0.70f)
            {
                WearGripMultiplier = 0.96f - (0.16f * (wearFraction - 0.25f) / 0.45f);
            }
            else
            {
                // Degradation cliff
                float cliffFraction = (wearFraction - 0.70f) / 0.30f;
                WearGripMultiplier = Mathf.Clamp(0.80f - (0.35f * cliffFraction), 0.45f, 1.0f);
            }
        }
    }
}
