import os
import sys
import math
import numpy as np
import pandas as pd
import fastf1

# Ensure cache directory exists
os.makedirs("fastf1_cache", exist_ok=True)
fastf1.Cache.enable_cache("fastf1_cache")

print("=== FASTF1 REAL TELEMETRY INGESTION PIPELINE ===", flush=True)

races = [
    (2023, "Bahrain", "R"),
    (2023, "Austria", "R"),
    (2023, "Japan", "R"),
    (2023, "Abu Dhabi", "R")
]

all_stint_laps = []

for year, gp, session_type in races:
    print(f"\nIngesting real telemetry from {year} {gp} Grand Prix ({session_type})...", flush=True)
    try:
        session = fastf1.get_session(year, gp, session_type)
        session.load(laps=True, telemetry=False, weather=True, messages=False)
        
        laps = session.laps
        print(f"  Loaded {len(laps)} total raw laps from {gp}.", flush=True)
        
        # 1. Exclude pit in-laps and pit out-laps
        clean_laps = laps.pick_wo_box()
        # 2. Exclude Safety Car / Virtual Safety Car / Red Flag periods
        clean_laps = clean_laps[clean_laps["TrackStatus"] == "1"]
        # 3. Only dry slick compounds
        clean_laps = clean_laps[clean_laps["Compound"].isin(["SOFT", "MEDIUM", "HARD"])]
        clean_laps = clean_laps.dropna(subset=["LapTime", "LapNumber", "Stint", "Compound"])
        
        clean_laps["LapTimeS"] = clean_laps["LapTime"].dt.total_seconds()
        
        # Exclude extreme outliers (>108% of median)
        median_time = clean_laps["LapTimeS"].median()
        clean_laps = clean_laps[(clean_laps["LapTimeS"] >= median_time * 0.95) & (clean_laps["LapTimeS"] <= median_time * 1.08)]
        
        total_race_laps = int(laps["LapNumber"].max())
        
        extracted_stints = 0
        for (driver, stint_num), stint_group in clean_laps.groupby(["Driver", "Stint"]):
            if len(stint_group) < 6: # Discard very short incomplete stints
                continue
                
            compound_str = stint_group["Compound"].iloc[0]
            compound_idx = 0 if compound_str == "SOFT" else (1 if compound_str == "MEDIUM" else 2)
            
            # Baseline reference time = fastest lap in early laps (laps 1-3) of this stint
            stint_baseline_time = stint_group["LapTimeS"].iloc[:min(3, len(stint_group))].min()
            
            avg_track_temp = 35.0
            if session.weather_data is not None and not session.weather_data.empty:
                avg_track_temp = float(session.weather_data["TrackTemp"].mean())
            
            for idx, (_, row) in enumerate(stint_group.iterrows()):
                lap_num = row["LapNumber"]
                lap_in_stint = idx + 1
                lap_time_s = row["LapTimeS"]
                
                # Real observed delta in seconds
                observed_delta_s = lap_time_s - stint_baseline_time
                
                # Physical estimated fuel load
                laps_remaining = max(0, total_race_laps - lap_num)
                fuel_load_kg = min(105.0, laps_remaining * (105.0 / total_race_laps) + 5.0)
                
                # Session progression factor
                session_progression = float(lap_num) / float(total_race_laps)
                track_evolution_factor = 1.0 + (math.sqrt(session_progression) * 0.04)
                
                gap_ahead_s = 3.5 # Default non-turbulent gap
                
                all_stint_laps.append({
                    "grand_prix": gp,
                    "year": year,
                    "driver": str(driver),
                    "stint_id": f"{year}_{gp}_{driver}_{stint_num}",
                    "lap_number": int(lap_num),
                    "lap_in_stint": float(lap_in_stint),
                    "fuel_load_kg": float(fuel_load_kg),
                    "gap_ahead_s": float(gap_ahead_s),
                    "track_evolution_factor": float(track_evolution_factor),
                    "compound_idx": float(compound_idx),
                    "compound_name": compound_str,
                    "track_temp_c": float(avg_track_temp),
                    "lap_time_s": float(lap_time_s),
                    "observed_delta_s": float(observed_delta_s)
                })
            extracted_stints += 1
                
        print(f"  Extracted {extracted_stints} clean stints from {gp}. Total cumulative laps: {len(all_stint_laps)}.", flush=True)
        
    except Exception as e:
        print(f"  Error loading session for {year} {gp}: {e}", flush=True)

df_all = pd.DataFrame(all_stint_laps)
print(f"\n=== REAL FASTF1 INGESTION SUMMARY ===", flush=True)
print(f"Total clean Grand Prix race laps ingested: {len(df_all)}", flush=True)
print(f"Total distinct driver race stints: {df_all['stint_id'].nunique()}", flush=True)
print(f"Circuits covered: {df_all['grand_prix'].unique().tolist()}", flush=True)
print(f"\nCompounds breakdown:\n{df_all['compound_name'].value_counts()}", flush=True)

# Save the real fastf1 dataset
df_all.to_csv("training_artifacts/real_fastf1_stint_data.csv", index=False)
print("\nSaved real dataset to: training_artifacts/real_fastf1_stint_data.csv", flush=True)
