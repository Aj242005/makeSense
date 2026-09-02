using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.Physics
{
    /// <summary>
    /// AeroModel simulates aerodynamic downforce, drag, DRS state, and dirty air wake:
    /// 1. Speed-squared aerodynamic downforce and drag forces via calibrated AnimationCurves.
    /// 2. Front / Rear aerodynamic balance distribution.
    /// 3. DRS (Drag Reduction System) discrete toggle (sheds ~22% drag, ~28% rear downforce).
    /// 4. Dirty air distance falloff model reducing downforce when trailing a car ahead.
    /// </summary>
    [Serializable]
    public class AeroModel
    {
        [Header("Aero Configuration")]
        [SerializeField] private float baseDownforceCoeffCl = 3.85f;  // High downforce F1 configuration
        [SerializeField] private float baseDragCoeffCd = 1.15f;       // Base aerodynamic drag coefficient
        [SerializeField] private float aeroBalanceFront = 0.435f;     // 43.5% front / 56.5% rear aero balance
        [SerializeField] private float airDensityKgPerM3 = 1.225f;    // Sea-level air density
        [SerializeField] private float frontalAreaM2 = 1.45f;         // Frontal reference area

        [Header("DRS Configuration")]
        public DrsState CurrentDrsState = DrsState.Closed;
        [SerializeField] private float drsDragReductionFactor = 0.22f; // -22% drag when DRS open
        [SerializeField] private float drsRearDownforceReduction = 0.28f; // -28% rear downforce when open

        [Header("Dirty Air Wake Model")]
        [SerializeField] private float dirtyAirMaxGapS = 1.8f;        // Trailing within 1.8s induces dirty air
        [SerializeField] private float dirtyAirMaxDownforceLoss = 0.32f; // Up to -32% downforce in close wake (0.4s)

        [Header("Live Aerodynamic Output Forces (Newtons)")]
        public float TotalDownforceN { get; private set; }
        public float FrontDownforceN { get; private set; }
        public float RearDownforceN { get; private set; }
        public float TotalDragN { get; private set; }
        public float DirtyAirLossFactor { get; private set; } = 0.0f;
        public bool InDirtyAirWake { get; private set; } = false;

        /// <summary>
        /// Computes aerodynamic downforce (front & rear) and drag forces.
        /// </summary>
        public void ComputeAeroForces(
            float speedMps, 
            float dynamicFrontRideHeightMm, 
            float dynamicRearRideHeightMm, 
            bool hasCarAhead, 
            float gapAheadS)
        {
            if (speedMps <= 1.0f)
            {
                TotalDownforceN = 0.0f;
                FrontDownforceN = 0.0f;
                RearDownforceN = 0.0f;
                TotalDragN = 0.0f;
                InDirtyAirWake = false;
                DirtyAirLossFactor = 0.0f;
                return;
            }

            // Dynamic pressure q = 0.5 * rho * v^2
            float dynamicPressure = 0.5f * airDensityKgPerM3 * speedMps * speedMps;

            // 1. Ground Effect Ride Height Sensitivity
            // Optimal underbody ground effect at ~25-35mm front ride height
            float frontRideHeightFactor = Mathf.Clamp(1.0f - Mathf.Abs(dynamicFrontRideHeightMm - 28.0f) * 0.008f, 0.82f, 1.05f);

            // 2. Evaluate Dirty Air Wake Factor
            DirtyAirLossFactor = 0.0f;
            InDirtyAirWake = false;

            if (hasCarAhead && gapAheadS > 0.0f && gapAheadS <= dirtyAirMaxGapS)
            {
                InDirtyAirWake = true;
                // Non-linear falloff: intense loss at 0.3-0.6s, tapering to 0 at 1.8s
                float wakeRatio = 1.0f - (gapAheadS / dirtyAirMaxGapS);
                DirtyAirLossFactor = dirtyAirMaxDownforceLoss * Mathf.Pow(wakeRatio, 1.5f);
            }

            float effectiveCl = baseDownforceCoeffCl * frontRideHeightFactor * (1.0f - DirtyAirLossFactor);

            // 3. Evaluate DRS Effect
            float effectiveCd = baseDragCoeffCd;
            float rearDownforceMod = 1.0f;

            if (CurrentDrsState == DrsState.Open)
            {
                effectiveCd *= (1.0f - drsDragReductionFactor);
                rearDownforceMod = (1.0f - drsRearDownforceReduction);
            }

            // 4. Compute Total Forces
            float rawDownforceN = dynamicPressure * frontalAreaM2 * effectiveCl;
            TotalDragN = dynamicPressure * frontalAreaM2 * effectiveCd;

            FrontDownforceN = rawDownforceN * aeroBalanceFront;
            RearDownforceN = rawDownforceN * (1.0f - aeroBalanceFront) * rearDownforceMod;
            TotalDownforceN = FrontDownforceN + RearDownforceN;
        }

        public void SetDrsState(DrsState state)
        {
            CurrentDrsState = state;
        }
    }
}
