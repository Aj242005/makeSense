using System;
using UnityEngine;

namespace GridSense.Track
{
    /// <summary>
    /// TrackManager controls the active circuit metadata, provides distance lookups,
    /// evaluates current sector boundaries, and computes dynamic track evolution multipliers.
    /// </summary>
    public class TrackManager : MonoBehaviour
    {
        public static TrackManager Instance { get; private set; }

        [Header("Active Circuit Configuration")]
        [SerializeField] private TrackData activeTrackData;
        [SerializeField] private float sessionProgression01 = 0.0f; // 0.0 at stint start -> 1.0 at stint end
        [SerializeField] private float maxTrackEvolutionGripBonus = 0.04f; // Up to +4% grip evolution over stint

        public TrackData ActiveTrack => activeTrackData;
        public float SessionProgression => sessionProgression01;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetActiveTrack(TrackData track)
        {
            activeTrackData = track;
            Debug.Log($"[TrackManager] Active track set to: {track.CircuitName} ({track.TotalLengthMeters:F0}m)");
        }

        public void SetSessionProgression(float progress01)
        {
            sessionProgression01 = Mathf.Clamp01(progress01);
        }

        /// <summary>
        /// Calculates the dynamic track evolution grip multiplier based on stint progression.
        /// One of the three confounds Track 3 (Tyre Degradation Isolation) must isolate.
        /// </summary>
        public float GetTrackEvolutionMultiplier()
        {
            // Grip increases non-linearly as rubber is laid down on track line
            return 1.0f + (Mathf.Sqrt(sessionProgression01) * maxTrackEvolutionGripBonus);
        }

        /// <summary>
        /// Returns the sector (1, 2, or 3) for a given distance along the lap.
        /// </summary>
        public int GetSectorForDistance(float distanceM)
        {
            if (activeTrackData == null) return 1;
            return activeTrackData.GetSectorAtDistance(distanceM);
        }

        /// <summary>
        /// Retrieves the upcoming corner definition for Track 1 braking & deployment planning.
        /// </summary>
        public CornerDefinition? GetUpcomingCorner(float currentDistanceM)
        {
            if (activeTrackData == null) return null;
            return activeTrackData.GetNextCorner(currentDistanceM);
        }
    }
}
