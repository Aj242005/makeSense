using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.Physics
{
    /// <summary>
    /// BrakeThermalModel simulates carbon-carbon brake disc thermodynamics:
    /// 1. Heat accumulation from kinetic braking energy dissipation.
    /// 2. Forced convective duct cooling with realistic multi-second thermal time constant (tau ~ 9 - 14s).
    /// 3. Temperature-dependent brake friction / stopping torque fade curve.
    /// 4. Coupling with BrakingAggressiveness (Aggressive increases thermal load and fade risk).
    /// </summary>
    [Serializable]
    public class BrakeThermalModel
    {
        [Header("Brake Disc Temperature")]
        public float RotorTempC = 450.0f;           // Core carbon rotor temperature in °C
        public float MinOptimalTempC = 350.0f;       // Lower bound of optimal operating window
        public float MaxOptimalTempC = 750.0f;       // Upper bound of optimal operating window
        public float OverheatFadeTempC = 880.0f;     // Severe thermal fade boundary

        [Header("Physical Constants & Duct Cooling")]
        public float RotorThermalMass = 4200.0f;     // Carbon-carbon rotor heat capacity (~4.2 kJ/°C per corner)
        public float BaseCoolingWattsPerC = 14.0f;   // Radiation & baseline duct conduction
        public float SpeedCoolingFactor = 0.55f;     // Brake duct convective cooling scaling with forward airspeed

        [Header("Live Stopping Factor & Fade")]
        public float FrictionEfficiency = 1.0f;      // 0.0 - 1.0 multiplier on maximum brake torque
        public bool IsFading = false;
        public bool IsCold = false;

        public BrakeThermalModel(float initialTempC = 450.0f)
        {
            RotorTempC = initialTempC;
        }

        /// <summary>
        /// Updates rotor temperature and computes stopping power fade for a given corner.
        /// </summary>
        public void UpdateBrakeThermal(
            float mechanicalBrakingPowerWatts, 
            float vehicleSpeedMps, 
            float ambientTempC, 
            BrakingAggressiveness aggressiveness, 
            float dt)
        {
            if (dt <= 0.0f) return;

            // 1. Heat Input from mechanical braking work (Joules / sec)
            // If Aggressive mode is selected, heavier late braking generates concentrated thermal spike (+25%)
            float aggressionFactor = (aggressiveness == BrakingAggressiveness.Aggressive) ? 1.25f : 1.0f;
            float heatInputWatts = mechanicalBrakingPowerWatts * aggressionFactor;

            // 2. Convective Duct Cooling (empirically calibrated for carbon-carbon multi-second retention)
            // Cooling time constant tau = ThermalMass / (h_base + h_speed * v) ~ 10-15 seconds at 250 km/h
            float ductCoolingCoeff = BaseCoolingWattsPerC + (SpeedCoolingFactor * vehicleSpeedMps);
            float coolingLossWatts = ductCoolingCoeff * (RotorTempC - ambientTempC);

            // 3. Integrate temperature delta
            float netHeatWatts = heatInputWatts - coolingLossWatts;
            float deltaTempC = (netHeatWatts / RotorThermalMass) * dt;

            RotorTempC = Mathf.Clamp(RotorTempC + deltaTempC, ambientTempC, 1250.0f);

            // 4. Compute Brake Friction Efficiency & Thermal Fade Curve
            if (RotorTempC >= MinOptimalTempC && RotorTempC <= MaxOptimalTempC)
            {
                // Optimal carbon-carbon operating band (350°C - 750°C)
                FrictionEfficiency = 1.0f;
                IsFading = false;
                IsCold = false;
            }
            else if (RotorTempC < MinOptimalTempC)
            {
                // Cold brakes: carbon-carbon friction coefficient is lower below 350°C
                float coldDeficit = MinOptimalTempC - RotorTempC;
                FrictionEfficiency = Mathf.Clamp(1.0f - (0.0015f * coldDeficit), 0.70f, 1.0f);
                IsCold = true;
                IsFading = false;
            }
            else
            {
                // Overheated brakes: thermal fade above 750°C, severe beyond 880°C
                float overheatDelta = RotorTempC - MaxOptimalTempC;
                float fadeRatio = overheatDelta / (OverheatFadeTempC - MaxOptimalTempC);
                FrictionEfficiency = Mathf.Clamp(1.0f - (0.45f * fadeRatio * fadeRatio), 0.35f, 1.0f);
                IsFading = (RotorTempC > 800.0f);
                IsCold = false;
            }
        }
    }
}
