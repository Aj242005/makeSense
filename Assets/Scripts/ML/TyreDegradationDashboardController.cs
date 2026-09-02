using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.ML
{
    /// <summary>
    /// TyreDegradationDashboardController manages the live Track 3 explainability data feed
    /// for pit-wall visualization and validation loop displays.
    /// </summary>
    public class TyreDegradationDashboardController : MonoBehaviour
    {
        public static TyreDegradationDashboardController Instance { get; private set; }

        [Header("Latest Explainability Attribution")]
        [SerializeField] private TyreDegradationExplainability currentAttribution;

        [Header("Validation Diagnostics")]
        public float LiveIsolatedDegradationS => currentAttribution.TrueTyreDegradationDeltaS;
        public float LiveFuelConfoundS => currentAttribution.FuelCorrectionDeltaS;
        public float LiveTrafficConfoundS => currentAttribution.TrafficDirtyAirDeltaS;
        public float LiveTrackEvolutionConfoundS => currentAttribution.TrackEvolutionDeltaS;
        public float LiveErrorBandLowerPct => currentAttribution.ConfidenceBandLowerPct;
        public float LiveErrorBandUpperPct => currentAttribution.ConfidenceBandUpperPct;
        public float LiveResidualErrorPct => currentAttribution.ResidualErrorPct;

        public TyreDegradationExplainability CurrentAttribution => currentAttribution;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (SimulationCore.Instance != null)
            {
                SimulationCore.Instance.OnTrack3ExplainabilityUpdated += HandleExplainabilityUpdated;
            }
        }

        private void HandleExplainabilityUpdated(TyreDegradationExplainability exp)
        {
            currentAttribution = exp;
        }

        private void OnDestroy()
        {
            if (SimulationCore.Instance != null)
            {
                SimulationCore.Instance.OnTrack3ExplainabilityUpdated -= HandleExplainabilityUpdated;
            }
        }
    }
}
