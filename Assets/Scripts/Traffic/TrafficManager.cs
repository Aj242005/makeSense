using System;
using System.Collections.Generic;
using UnityEngine;
using GridSense.Core;
using GridSense.Track;

namespace GridSense.Traffic
{
    /// <summary>
    /// TrafficManager manages rule-based opponent cars on track:
    /// - Computes real-time gaps ahead and behind the player car
    /// - Triggers dirty-air wake and overtake scenarios
    /// - Strictly uses generic identifiers (CAR-02, RIVAL-ALPHA) without official names
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        public static TrafficManager Instance { get; private set; }

        [Header("Traffic Grid Setup (Generic Identifiers)")]
        [SerializeField] private List<TrafficCar> opponentCars = new List<TrafficCar>();
        [SerializeField] private bool enableTrafficSimulation = true;

        [Header("Live Relative Gap Telemetry")]
        public float LiveGapAheadS { get; private set; } = 0.0f;
        public bool HasLiveGapAhead { get; private set; } = false;
        public string AheadCarId { get; private set; } = "";

        public float LiveGapBehindS { get; private set; } = 0.0f;
        public bool HasLiveGapBehind { get; private set; } = false;
        public string BehindCarId { get; private set; } = "";

        public IReadOnlyList<TrafficCar> Opponents => opponentCars;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDefaultGrid();
        }

        private void InitializeDefaultGrid()
        {
            opponentCars.Clear();

            // Opponent 1 (Ahead by ~1.2s to create active dirty air & overtake test case)
            opponentCars.Add(new TrafficCar("RIVAL-ALPHA", 12, 110.0f, 92.2f));

            // Opponent 2 (Behind by ~2.8s)
            opponentCars.Add(new TrafficCar("CAR-BETA", 24, -220.0f, 93.5f));
        }

        private void FixedUpdate()
        {
            if (!enableTrafficSimulation) return;

            float dt = Time.fixedDeltaTime;
            float trackLengthM = (TrackManager.Instance != null && TrackManager.Instance.ActiveTrack != null)
                ? TrackManager.Instance.ActiveTrack.TotalLengthMeters
                : 5412.0f;

            float playerDistanceM = (SimulationCore.Instance != null)
                ? SimulationCore.Instance.State.DistanceIntoLapM
                : 0.0f;

            float playerSpeedMps = Mathf.Max(15.0f, trackLengthM / 90.0f); // ~60 m/s nominal reference

            // 1. Update all opponent positions
            for (int i = 0; i < opponentCars.Count; i++)
            {
                opponentCars[i].UpdatePosition(trackLengthM, playerDistanceM, dt);
            }

            // 2. Evaluate closest car ahead and closest car behind
            float minGapAheadM = float.MaxValue;
            float minGapBehindM = float.MaxValue;
            string bestAheadId = "";
            string bestBehindId = "";

            for (int i = 0; i < opponentCars.Count; i++)
            {
                TrafficCar car = opponentCars[i];
                float deltaM = car.DistanceAlongTrackM - playerDistanceM;

                // Wrap delta within [-trackLength/2, trackLength/2]
                if (deltaM < -trackLengthM * 0.5f) deltaM += trackLengthM;
                if (deltaM > trackLengthM * 0.5f) deltaM -= trackLengthM;

                if (deltaM > 0.0f)
                {
                    // Car is ahead
                    if (deltaM < minGapAheadM)
                    {
                        minGapAheadM = deltaM;
                        bestAheadId = car.DriverIdentifier;
                    }
                }
                else if (deltaM < 0.0f)
                {
                    // Car is behind
                    float distBehind = Mathf.Abs(deltaM);
                    if (distBehind < minGapBehindM)
                    {
                        minGapBehindM = distBehind;
                        bestBehindId = car.DriverIdentifier;
                    }
                }
            }

            // Convert distance gaps (meters) to time gaps (seconds)
            if (minGapAheadM < 600.0f) // Within ~10 seconds
            {
                HasLiveGapAhead = true;
                LiveGapAheadS = minGapAheadM / playerSpeedMps;
                AheadCarId = bestAheadId;
            }
            else
            {
                HasLiveGapAhead = false;
                LiveGapAheadS = 0.0f;
                AheadCarId = "";
            }

            if (minGapBehindM < 600.0f)
            {
                HasLiveGapBehind = true;
                LiveGapBehindS = minGapBehindM / playerSpeedMps;
                BehindCarId = bestBehindId;
            }
            else
            {
                HasLiveGapBehind = false;
                LiveGapBehindS = 0.0f;
                BehindCarId = "";
            }

            // 3. Update CarState in SimulationCore
            if (SimulationCore.Instance != null)
            {
                CarState state = SimulationCore.Instance.State;
                state.HasGapAhead = HasLiveGapAhead;
                state.GapAheadS = LiveGapAheadS;
                state.HasGapBehind = HasLiveGapBehind;
                state.GapBehindS = LiveGapBehindS;
                SimulationCore.Instance.UpdatePhysicsState(state);
            }
        }
    }
}
