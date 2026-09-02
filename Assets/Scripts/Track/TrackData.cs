using System;
using UnityEngine;

namespace GridSense.Track
{
    public enum CornerSeverity
    {
        FlatOut,
        HighSpeed,
        MediumSpeed,
        HardBraking,
        Hairpin,
        Chicane,
        DecreasingRadius,
        Banked
    }

    [Serializable]
    public struct CornerDefinition
    {
        public int CornerNumber;
        public string CornerName;
        public float DistanceAlongTrackM;
        public CornerSeverity Severity;
        public string TurnDirection; // "Left" / "Right"
        public float MinApexSpeedKmh;
        public float BaseGripMultiplier;
        public Vector3 ApexWorldPosition;
    }

    [Serializable]
    public struct SectorDefinition
    {
        public int SectorNumber;
        public float StartDistanceM;
        public float EndDistanceM;
    }

    [CreateAssetMenu(fileName = "NewTrackData", menuName = "GridSense/Track Data", order = 1)]
    public class TrackData : ScriptableObject
    {
        [Header("Circuit Identity")]
        public string CircuitName;
        public string LayoutName;
        [TextArea(2, 4)]
        public string SourceProvenance;

        [Header("Geometric Attributes")]
        public float TotalLengthMeters;
        public Vector3 StartFinishLinePosition;
        public Vector3 StartFinishLineDirection = Vector3.forward;

        [Header("Sectors & Timing")]
        public SectorDefinition[] Sectors = new SectorDefinition[3];

        [Header("Corners & Key Reference Points")]
        public CornerDefinition[] Corners;

        [Header("Environment & Defaults")]
        public float DefaultAmbientTempC = 25.0f;
        public float DefaultTrackTempC = 35.0f;
        public float DefaultElevationM = 0.0f;

        public int GetSectorAtDistance(float distanceM)
        {
            if (Sectors == null || Sectors.Length < 3) return 1;
            if (distanceM < Sectors[0].EndDistanceM) return 1;
            if (distanceM < Sectors[1].EndDistanceM) return 2;
            return 3;
        }

        public CornerDefinition? GetNextCorner(float distanceM)
        {
            if (Corners == null || Corners.Length == 0) return null;
            for (int i = 0; i < Corners.Length; i++)
            {
                if (Corners[i].DistanceAlongTrackM > distanceM)
                    return Corners[i];
            }
            return Corners[0]; // Wrap around to turn 1
        }
    }
}
