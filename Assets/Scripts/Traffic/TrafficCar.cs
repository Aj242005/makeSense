using System;
using UnityEngine;

namespace GridSense.Traffic
{
    /// <summary>
    /// TrafficCar represents a rule-based opponent vehicle on track:
    /// - Operates on a target pace profile (lap time delta vs baseline)
    /// - Tracks its distance along the lap
    /// - Executes basic defensive line positioning when a following car is within an overtake window (<1.0s)
    /// - Uses generic identification labels (e.g. CAR-02, RIVAL-ALPHA)
    /// </summary>
    [Serializable]
    public class TrafficCar
    {
        [Header("Identity (Generic Only)")]
        public string DriverIdentifier;        // "RIVAL-ALPHA", "CAR-02", etc.
        public string ChassisName;             // "Chassis-B", "Spec-2023"
        public int CarNumber;                  // Generic car number (e.g. 12, 24, 77)

        [Header("Positioning & Pace")]
        public float DistanceAlongTrackM;      // Position along circuit (0 to TotalLengthMeters)
        public float CurrentSpeedMps;          // Live speed in m/s
        public float TargetLapTimeS;           // Target baseline lap time (e.g. 92.5s)
        public float CurrentLap = 1;
        public int CurrentSector = 1;

        [Header("Tactical State")]
        public bool IsDefending = false;       // True if taking defensive inside line
        public float PaceModifier = 1.0f;      // Stint pace variation (e.g. 0.98 - 1.02)

        public TrafficCar(string id, int number, float initialDistanceM, float targetLapTimeS)
        {
            DriverIdentifier = id;
            ChassisName = "Spec-Chassis";
            CarNumber = number;
            DistanceAlongTrackM = initialDistanceM;
            TargetLapTimeS = targetLapTimeS;
            CurrentSpeedMps = 65.0f; // ~234 km/h initial pace
        }

        /// <summary>
        /// Advances opponent car position over timestep dt based on target pace and defensive tactics.
        /// </summary>
        public void UpdatePosition(float trackLengthM, float playerDistanceM, float dt)
        {
            if (dt <= 0.0f || trackLengthM <= 100.0f) return;

            // Target average speed (m/s) = track_length / target_lap_time
            float baseTargetSpeedMps = trackLengthM / Mathf.Max(60.0f, TargetLapTimeS);

            // Compute gap to player
            float deltaM = DistanceAlongTrackM - playerDistanceM;
            if (deltaM < -trackLengthM * 0.5f) deltaM += trackLengthM;
            if (deltaM > trackLengthM * 0.5f) deltaM -= trackLengthM;

            float gapSeconds = deltaM / Mathf.Max(10.0f, baseTargetSpeedMps);

            // Defensive logic: if player is right behind (gap between 0.1s and 0.9s), enter defensive mode
            if (gapSeconds > 0.1f && gapSeconds < 0.9f)
            {
                IsDefending = true;
                // Defensive line slightly compromises corner exit speed (-3% pace penalty)
                CurrentSpeedMps = baseTargetSpeedMps * PaceModifier * 0.97f;
            }
            else
            {
                IsDefending = false;
                CurrentSpeedMps = baseTargetSpeedMps * PaceModifier;
            }

            // Advance distance
            DistanceAlongTrackM += CurrentSpeedMps * dt;
            if (DistanceAlongTrackM >= trackLengthM)
            {
                DistanceAlongTrackM -= trackLengthM;
                CurrentLap++;
            }
        }
    }
}
