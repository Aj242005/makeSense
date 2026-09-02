using System;
using UnityEngine;

namespace GridSense.Core
{
    /// <summary>
    /// Unified CarState struct: Single source of truth shared across
    /// Physics simulation, Track 1 (Energy Intelligence), Track 3 (Tyre Degradation Isolation),
    /// and the UI Toolkit pit-wall dashboard.
    /// </summary>
    [Serializable]
    public struct CarState
    {
        [Header("Lap & Session Positioning")]
        public int Lap;
        public int Sector;
        public float DistanceIntoLapM;

        [Header("Tyre Model State (Coupled to Track 3)")]
        public TyreCompound Compound;         // Soft, Medium, Hard
        public float TyreWearPct;             // 0-100% wear estimated by degradation model
        public float TyreTempC;               // Operating carcass/surface temperature in Celsius
        public float TyreWearRateCurrent;     // Instantaneous slope (% wear / lap), feeds Energy risk calculation

        [Header("Powertrain & Energy State (Coupled to Track 1)")]
        public float EnergyRemainingPct;      // 0-100% remaining stint energy budget
        public EnergyMode DeploymentMode;     // Push, Balanced, Hold, Save
        public BrakingAggressiveness Braking; // Normal, Aggressive (affects regen gain & lockup/fade risk)
        public float FuelLoadKg;              // Live fuel mass (decreases over stint)

        [Header("Traffic & Environment Confounds")]
        public float GapAheadS;               // Gap to car ahead in seconds
        public bool HasGapAhead;              // True if a car ahead exists in range
        public float GapBehindS;              // Gap to car behind in seconds
        public bool HasGapBehind;             // True if a car behind exists
        public bool DirtyAir;                 // True if trailing closely in aerodynamic wake
        public float TrackEvolutionFactor;    // Session progression grip multiplier (e.g. 1.00 to 1.05)

        /// <summary>
        /// Creates a default initialized CarState for the start of a session or stint.
        /// </summary>
        public static CarState CreateDefault(TyreCompound compound = TyreCompound.Medium, float initialFuelKg = 110.0f)
        {
            return new CarState
            {
                Lap = 1,
                Sector = 1,
                DistanceIntoLapM = 0.0f,
                Compound = compound,
                TyreWearPct = 0.0f,
                TyreTempC = 95.0f, // Optimal starting tyre temp
                TyreWearRateCurrent = 0.0f,
                EnergyRemainingPct = 100.0f,
                DeploymentMode = EnergyMode.Balanced,
                Braking = BrakingAggressiveness.Normal,
                FuelLoadKg = initialFuelKg,
                GapAheadS = 0.0f,
                HasGapAhead = false,
                GapBehindS = 0.0f,
                HasGapBehind = false,
                DirtyAir = false,
                TrackEvolutionFactor = 1.0f
            };
        }

        public override string ToString()
        {
            string gapStr = HasGapAhead ? $"+{GapAheadS:F2}s" : "CLEAR";
            return $"[CarState] L{Lap} S{Sector} ({DistanceIntoLapM:F0}m) | Tyre: {Compound} {TyreWearPct:F1}% ({TyreTempC:F1}°C) | Energy: {EnergyRemainingPct:F1}% [{DeploymentMode}] | Braking: {Braking} | Fuel: {FuelLoadKg:F1}kg | Gap: {gapStr} | DirtyAir: {DirtyAir}";
        }
    }
}
