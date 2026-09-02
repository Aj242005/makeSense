using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.Physics
{
    /// <summary>
    /// PowertrainEnergyModel simulates:
    /// 1. 1.6L V6 Turbo ICE power and torque curve.
    /// 2. Hybrid MGU-K electrical boost and finite stint energy budget depletion (Push/Balanced/Hold/Save).
    /// 3. Regenerative braking recovery scaled by BrakingAggressiveness.
    /// 4. Fuel mass loss over time reducing total vehicle weight.
    /// </summary>
    [Serializable]
    public class PowertrainEnergyModel
    {
        [Header("ICE Engine Specifications")]
        [SerializeField] private float maxIcePowerWatts = 580000.0f; // 580 kW (~780 hp)
        [SerializeField] private float idleRpm = 4000.0f;
        [SerializeField] private float maxRpm = 15000.0f;
        [SerializeField] private float peakTorqueRpm = 10500.0f;

        [Header("Stint Finite Energy Pool (Formula-E / FIA Hybrid Framing)")]
        [SerializeField] private float totalStintEnergyJoules = 180000000.0f; // 180 MJ (~50 kWh)
        public float RemainingEnergyJoules { get; private set; }
        public float EnergyRemainingPct => Mathf.Clamp01(RemainingEnergyJoules / totalStintEnergyJoules) * 100.0f;

        [Header("MGU-K Hybrid Deployment Power deltas (Watts)")]
        [SerializeField] private float pushDeployPowerWatts = 120000.0f;     // +120 kW electrical assist
        [SerializeField] private float balancedDeployPowerWatts = 60000.0f;  // +60 kW electrical assist
        [SerializeField] private float holdDeployPowerWatts = 0.0f;          // 0 kW net assist
        [SerializeField] private float saveHarvestPowerWatts = -40000.0f;    // -40 kW harvest derate

        [Header("Regenerative Braking MGU-K Recovery")]
        [SerializeField] private float mguKMaxRegenNormalWatts = 120000.0f;    // 120 kW max normal regen
        [SerializeField] private float mguKMaxRegenAggressiveWatts = 160000.0f;// 160 kW max aggressive regen (+33%)
        [SerializeField] private float regenEfficiency = 0.88f;                // 88% kinetic-to-battery efficiency

        [Header("Fuel Mass & Consumption")]
        public float CurrentFuelMassKg = 105.0f;
        [SerializeField] private float baseFuelBurnRateKgPerSec = 0.024f; // ~1.8 kg / lap at race pace

        [Header("Live Powertrain Telemetry")]
        public float EngineRpm { get; private set; } = 4000.0f;
        public float TotalOutputPowerWatts { get; private set; }
        public float CurrentElectricalPowerFlowWatts { get; private set; } // Positive = deploying, Negative = regenerating
        public float TotalEnergyRegeneratedJoules { get; private set; } = 0.0f;

        public PowertrainEnergyModel(float initialFuelKg = 105.0f)
        {
            CurrentFuelMassKg = initialFuelKg;
            RemainingEnergyJoules = totalStintEnergyJoules;
        }

        /// <summary>
        /// Evaluates powertrain torque, battery state of charge, regen energy recovery, and fuel burn over dt.
        /// </summary>
        public float UpdatePowertrain(
            float throttleInput, 
            float brakeInput, 
            float vehicleSpeedMps, 
            EnergyMode deploymentMode, 
            BrakingAggressiveness brakingAggressiveness, 
            float dt)
        {
            if (dt <= 0.0f) return 0.0f;

            // 1. Calculate Engine RPM based on wheel speed & transmission ratio
            float wheelRadiusM = 0.36f;
            float currentWheelRps = vehicleSpeedMps / (2.0f * Mathf.PI * wheelRadiusM);
            float gearRatio = GetApproximateGearRatio(vehicleSpeedMps);
            EngineRpm = Mathf.Clamp(currentWheelRps * gearRatio * 60.0f, idleRpm, maxRpm);

            // 2. Evaluate ICE Power output
            float rpmFactor = Mathf.Clamp01((EngineRpm - idleRpm) / (peakTorqueRpm - idleRpm));
            float icePowerWatts = maxIcePowerWatts * rpmFactor * throttleInput;

            // 3. Evaluate Electrical Deploy / Harvest based on EnergyMode
            float targetElectricalAssistWatts = 0.0f;
            if (RemainingEnergyJoules > 0.0f && throttleInput > 0.1f)
            {
                switch (deploymentMode)
                {
                    case EnergyMode.Push:
                        targetElectricalAssistWatts = pushDeployPowerWatts * throttleInput;
                        break;
                    case EnergyMode.Balanced:
                        targetElectricalAssistWatts = balancedDeployPowerWatts * throttleInput;
                        break;
                    case EnergyMode.Hold:
                        targetElectricalAssistWatts = holdDeployPowerWatts;
                        break;
                    case EnergyMode.Save:
                        targetElectricalAssistWatts = saveHarvestPowerWatts; // Derate power to harvest
                        break;
                }
            }

            // Deduct battery discharge Joules: dE = P * dt
            if (targetElectricalAssistWatts > 0.0f)
            {
                float energyUsedJoules = targetElectricalAssistWatts * dt;
                RemainingEnergyJoules = Mathf.Max(0.0f, RemainingEnergyJoules - energyUsedJoules);
                CurrentElectricalPowerFlowWatts = targetElectricalAssistWatts;
            }
            else if (targetElectricalAssistWatts < 0.0f)
            {
                // Engine harvest mode charging battery
                float energyHarvestedJoules = Mathf.Abs(targetElectricalAssistWatts) * regenEfficiency * dt;
                RemainingEnergyJoules = Mathf.Min(totalStintEnergyJoules, RemainingEnergyJoules + energyHarvestedJoules);
                CurrentElectricalPowerFlowWatts = targetElectricalAssistWatts;
            }
            else
            {
                CurrentElectricalPowerFlowWatts = 0.0f;
            }

            // 4. Evaluate Regenerative Braking Recovery under Braking
            if (brakeInput > 0.05f && vehicleSpeedMps > 5.0f)
            {
                float maxRegenCapacityWatts = (brakingAggressiveness == BrakingAggressiveness.Aggressive)
                    ? mguKMaxRegenAggressiveWatts
                    : mguKMaxRegenNormalWatts;

                float kineticBrakingPowerWatts = (0.5f * 798.0f * vehicleSpeedMps * vehicleSpeedMps) * brakeInput;
                float regenPowerWatts = Mathf.Min(maxRegenCapacityWatts, kineticBrakingPowerWatts * regenEfficiency);

                float regenEnergyJoules = regenPowerWatts * dt;
                RemainingEnergyJoules = Mathf.Min(totalStintEnergyJoules, RemainingEnergyJoules + regenEnergyJoules);
                TotalEnergyRegeneratedJoules += regenEnergyJoules;

                CurrentElectricalPowerFlowWatts = -regenPowerWatts;
            }

            // 5. Fuel Mass Consumption
            float fuelBurnMultiplier = 1.0f + (targetElectricalAssistWatts < 0.0f ? 0.15f : 0.0f); // Higher fuel burn if harvesting
            float fuelBurnKg = baseFuelBurnRateKgPerSec * throttleInput * fuelBurnMultiplier * dt;
            CurrentFuelMassKg = Mathf.Max(0.0f, CurrentFuelMassKg - fuelBurnKg);

            // 6. Total Net Propulsion Power & Wheel Torque
            TotalOutputPowerWatts = Mathf.Max(0.0f, icePowerWatts + targetElectricalAssistWatts);
            float wheelAngularVelocityRadPerS = Mathf.Max(5.0f, currentWheelRps * 2.0f * Mathf.PI);
            float totalDriveTorqueNm = TotalOutputPowerWatts / wheelAngularVelocityRadPerS;

            return totalDriveTorqueNm;
        }

        private float GetApproximateGearRatio(float speedMps)
        {
            float speedKmh = speedMps * 3.6f;
            if (speedKmh < 80.0f) return 12.5f;   // Gear 1/2
            if (speedKmh < 140.0f) return 8.8f;   // Gear 3
            if (speedKmh < 190.0f) return 6.7f;   // Gear 4
            if (speedKmh < 240.0f) return 5.4f;   // Gear 5
            if (speedKmh < 285.0f) return 4.5f;   // Gear 6
            if (speedKmh < 320.0f) return 3.9f;   // Gear 7
            return 3.4f;                          // Gear 8
        }
    }
}
