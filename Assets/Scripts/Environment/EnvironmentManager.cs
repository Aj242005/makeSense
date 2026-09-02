using System;
using UnityEngine;
using GridSense.Track;

namespace GridSense.Environment
{
    public enum WeatherCondition
    {
        Dry = 0,
        Overcast = 1,
        Damp = 2
    }

    /// <summary>
    /// EnvironmentManager manages ambient conditions, track temperatures, weather grip multipliers,
    /// and dynamically evaluates per-corner grip combined with session track evolution.
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        public static EnvironmentManager Instance { get; private set; }

        [Header("Ambient & Track Conditions")]
        [SerializeField] private float ambientTemperatureC = 27.0f;
        [SerializeField] private float trackSurfaceTemperatureC = 39.0f;
        [SerializeField] private WeatherCondition currentWeather = WeatherCondition.Dry;

        [Header("Weather Grip Multipliers")]
        [SerializeField] private float dryGripMultiplier = 1.00f;
        [SerializeField] private float overcastGripMultiplier = 0.98f;
        [SerializeField] private float dampGripMultiplier = 0.88f;

        public float AmbientTempC => ambientTemperatureC;
        public float TrackTempC => trackSurfaceTemperatureC;
        public WeatherCondition CurrentWeather => currentWeather;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetWeather(WeatherCondition weather)
        {
            currentWeather = weather;
        }

        public void SetTemperatures(float ambientC, float trackC)
        {
            ambientTemperatureC = ambientC;
            trackSurfaceTemperatureC = trackC;
        }

        /// <summary>
        /// Returns the base weather grip factor.
        /// </summary>
        public float GetWeatherGripMultiplier()
        {
            switch (currentWeather)
            {
                case WeatherCondition.Overcast: return overcastGripMultiplier;
                case WeatherCondition.Damp: return dampGripMultiplier;
                case WeatherCondition.Dry:
                default:
                    return dryGripMultiplier;
            }
        }

        /// <summary>
        /// Computes the total effective grip multiplier for a specific track location:
        /// Total Grip = Base Corner Grip * Session Track Evolution * Weather Multiplier
        /// Note: Track evolution is a physical reality in the simulation that Track 3 must isolate.
        /// </summary>
        public float GetEffectiveGripAtDistance(float distanceAlongTrackM)
        {
            float cornerBaseGrip = 1.0f;
            if (TrackManager.Instance != null && TrackManager.Instance.ActiveTrack != null)
            {
                CornerDefinition? nextCorner = TrackManager.Instance.GetUpcomingCorner(distanceAlongTrackM);
                if (nextCorner.HasValue)
                {
                    cornerBaseGrip = nextCorner.Value.BaseGripMultiplier;
                }
            }

            float evolutionMultiplier = (TrackManager.Instance != null) 
                ? TrackManager.Instance.GetTrackEvolutionMultiplier() 
                : 1.0f;

            float weatherMultiplier = GetWeatherGripMultiplier();

            return cornerBaseGrip * evolutionMultiplier * weatherMultiplier;
        }
    }
}
