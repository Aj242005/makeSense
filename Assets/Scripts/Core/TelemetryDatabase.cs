using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace GridSense.Core
{
    /// <summary>
    /// TelemetryDatabase provides high-throughput, non-blocking 50Hz session persistence.
    /// Uses a lock-free concurrent queue and background worker thread to ensure zero frame drops
    /// on the physics simulation thread, persisting telemetry snapshots and lap summaries
    /// to disk in CSV / structured session formats.
    /// </summary>
    public class TelemetryDatabase : MonoBehaviour
    {
        public static TelemetryDatabase Instance { get; private set; }

        [Header("Persistence Settings")]
        [SerializeField] private bool autoRecordOnPlay = true;
        [SerializeField] private string sessionDirectory = "TelemetrySessions";

        public struct TelemetryRecord
        {
            public float Timestamp;
            public int Lap;
            public float LapDistanceM;
            public float FuelLoadKg;
            public float EnergyRemainingPct;
            public EnergyMode Mode;
            public BrakingAggressiveness Braking;
            public float TyreWearPct;
            public float TyreTempC;
            public float TyreWearRateCurrent;
            public float GapAheadS;
            public bool DirtyAir;
            public float TrackEvolutionFactor;
            public float TrueTyreDegDeltaS;
            public float FuelCorrectionDeltaS;
            public float TrafficDirtyAirDeltaS;
            public float TrackEvolutionDeltaS;
            public float TotalObservedDeltaS;
            public float OvertakeScore;
            public float EnergySurplusDeficit;
        }

        public struct LapSummaryRecord
        {
            public int LapNumber;
            public float LapTimeS;
            public float Sector1TimeS;
            public float Sector2TimeS;
            public float Sector3TimeS;
            public float FuelUsedKg;
            public float EnergyUsedMJ;
            public float AvgTyreWear;
        }

        private readonly ConcurrentQueue<TelemetryRecord> telemetryQueue = new ConcurrentQueue<TelemetryRecord>();
        private readonly List<LapSummaryRecord> completedLaps = new List<LapSummaryRecord>();
        
        private Thread writerThread;
        private bool isRecording = false;
        private string currentSessionFilePath;
        private string currentLapSummaryFilePath;
        private int totalRecordsLogged = 0;
        private float sessionStartTime = 0.0f;

        public int TotalRecordsLogged => totalRecordsLogged;
        public IReadOnlyList<LapSummaryRecord> CompletedLaps => completedLaps;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void InitializeForTesting()
        {
            Instance = this;
            sessionStartTime = Time.time;
        }

        private void Start()
        {
            sessionStartTime = Time.time;
            if (autoRecordOnPlay)
            {
                StartNewSession("Ferrari_SF23", "ActiveTrack");
            }
        }

        public void StartNewSession(string carModel, string trackName)
        {
            StopSession();

            string rootDir = Path.Combine(Application.persistentDataPath, sessionDirectory);
            if (!Directory.Exists(rootDir))
            {
                Directory.CreateDirectory(rootDir);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sessionName = $"Session_{trackName}_{carModel}_{timestamp}";
            currentSessionFilePath = Path.Combine(rootDir, $"{sessionName}_telemetry.csv");
            currentLapSummaryFilePath = Path.Combine(rootDir, $"{sessionName}_laps.csv");

            // Write CSV headers
            try
            {
                using (StreamWriter sw = new StreamWriter(currentSessionFilePath, false, Encoding.UTF8))
                {
                    sw.WriteLine("Timestamp,Lap,LapDistanceM,FuelLoadKg,EnergyRemainingPct,Mode,Braking,TyreWearPct,TyreTempC,TyreWearRateCurrent,GapAheadS,DirtyAir,TrackEvolutionFactor,TrueTyreDegDeltaS,FuelCorrectionDeltaS,TrafficDirtyAirDeltaS,TrackEvolutionDeltaS,TotalObservedDeltaS,OvertakeScore,EnergySurplusDeficit");
                }

                using (StreamWriter sw = new StreamWriter(currentLapSummaryFilePath, false, Encoding.UTF8))
                {
                    sw.WriteLine("LapNumber,LapTimeS,Sector1S,Sector2S,Sector3S,FuelUsedKg,EnergyUsedMJ,AvgTyreWear");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TelemetryDatabase] Error creating session files: {ex.Message}");
                return;
            }

            isRecording = true;
            writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.BelowNormal
            };
            writerThread.Start();

            Debug.Log($"[TelemetryDatabase] Session started. Logging to: {currentSessionFilePath}");
        }

        public void LogSample(in CarState state, in TyreDegradationExplainability tyreExp, in EnergyDeploymentExplainability energyExp)
        {
            if (!isRecording) return;

            TelemetryRecord record = new TelemetryRecord
            {
                Timestamp = Time.time - sessionStartTime,
                Lap = state.Lap,
                LapDistanceM = state.DistanceIntoLapM,
                FuelLoadKg = state.FuelLoadKg,
                EnergyRemainingPct = state.EnergyRemainingPct,
                Mode = state.DeploymentMode,
                Braking = state.Braking,
                TyreWearPct = state.TyreWearPct,
                TyreTempC = state.TyreTempC,
                TyreWearRateCurrent = state.TyreWearRateCurrent,
                GapAheadS = state.GapAheadS,
                DirtyAir = state.DirtyAir,
                TrackEvolutionFactor = state.TrackEvolutionFactor,
                TrueTyreDegDeltaS = tyreExp.TrueTyreDegradationDeltaS,
                FuelCorrectionDeltaS = tyreExp.FuelCorrectionDeltaS,
                TrafficDirtyAirDeltaS = tyreExp.TrafficDirtyAirDeltaS,
                TrackEvolutionDeltaS = tyreExp.TrackEvolutionDeltaS,
                TotalObservedDeltaS = tyreExp.TotalObservedDeltaS,
                OvertakeScore = energyExp.OvertakeRiskRewardScore,
                EnergySurplusDeficit = energyExp.EnergyBudgetSurplusDeficitPct
            };

            telemetryQueue.Enqueue(record);
        }

        public void LogLapCompleted(int lapNumber, float lapTime, float s1, float s2, float s3, float fuelUsed, float energyUsed, float avgTyreWear)
        {
            LapSummaryRecord summary = new LapSummaryRecord
            {
                LapNumber = lapNumber,
                LapTimeS = lapTime,
                Sector1TimeS = s1,
                Sector2TimeS = s2,
                Sector3TimeS = s3,
                FuelUsedKg = fuelUsed,
                EnergyUsedMJ = energyUsed,
                AvgTyreWear = avgTyreWear
            };

            completedLaps.Add(summary);

            if (!string.IsNullOrEmpty(currentLapSummaryFilePath) && File.Exists(currentLapSummaryFilePath))
            {
                try
                {
                    string line = $"{lapNumber},{lapTime:F3},{s1:F3},{s2:F3},{s3:F3},{fuelUsed:F2},{energyUsed:F2},{avgTyreWear:F3}";
                    File.AppendAllLines(currentLapSummaryFilePath, new[] { line });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TelemetryDatabase] Failed to append lap summary: {ex.Message}");
                }
            }
        }

        private void WriterLoop()
        {
            List<TelemetryRecord> batch = new List<TelemetryRecord>(128);
            StringBuilder sb = new StringBuilder(4096);

            while (isRecording || !telemetryQueue.IsEmpty)
            {
                batch.Clear();
                while (batch.Count < 128 && telemetryQueue.TryDequeue(out TelemetryRecord record))
                {
                    batch.Add(record);
                }

                if (batch.Count > 0)
                {
                    sb.Clear();
                    for (int i = 0; i < batch.Count; i++)
                    {
                        var r = batch[i];
                        sb.Append(r.Timestamp.ToString("F3")).Append(',')
                          .Append(r.Lap).Append(',')
                          .Append(r.LapDistanceM.ToString("F1")).Append(',')
                          .Append(r.FuelLoadKg.ToString("F2")).Append(',')
                          .Append(r.EnergyRemainingPct.ToString("F1")).Append(',')
                          .Append((int)r.Mode).Append(',')
                          .Append((int)r.Braking).Append(',')
                          .Append(r.TyreWearPct.ToString("F2")).Append(',')
                          .Append(r.TyreTempC.ToString("F1")).Append(',')
                          .Append(r.TyreWearRateCurrent.ToString("F4")).Append(',')
                          .Append(r.GapAheadS.ToString("F2")).Append(',')
                          .Append(r.DirtyAir ? "1" : "0").Append(',')
                          .Append(r.TrackEvolutionFactor.ToString("F3")).Append(',')
                          .Append(r.TrueTyreDegDeltaS.ToString("F4")).Append(',')
                          .Append(r.FuelCorrectionDeltaS.ToString("F4")).Append(',')
                          .Append(r.TrafficDirtyAirDeltaS.ToString("F4")).Append(',')
                          .Append(r.TrackEvolutionDeltaS.ToString("F4")).Append(',')
                          .Append(r.TotalObservedDeltaS.ToString("F4")).Append(',')
                          .Append(r.OvertakeScore.ToString("F1")).Append(',')
                          .Append(r.EnergySurplusDeficit.ToString("F2")).AppendLine();
                    }

                    try
                    {
                        File.AppendAllText(currentSessionFilePath, sb.ToString());
                        Interlocked.Add(ref totalRecordsLogged, batch.Count);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TelemetryDatabase] Write error: {ex.Message}");
                    }
                }
                else
                {
                    Thread.Sleep(20);
                }
            }
        }

        public void StopSession()
        {
            if (!isRecording) return;
            isRecording = false;

            if (writerThread != null && writerThread.IsAlive)
            {
                writerThread.Join(500);
            }

            Debug.Log($"[TelemetryDatabase] Session stopped. Total logged records: {totalRecordsLogged}");
        }

        private void OnDestroy()
        {
            StopSession();
        }

        private void OnApplicationQuit()
        {
            StopSession();
        }
    }
}
