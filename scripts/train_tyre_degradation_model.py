import os
import sys
import json
import math
import numpy as np

# Ensure UTF-8 output encoding on Windows console
if sys.platform == 'win32':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')

# Ensure target directories exist
os.makedirs("Assets/MLModels", exist_ok=True)
os.makedirs("training_artifacts", exist_ok=True)

print("=== GRID SENSE: TRACK 3 TYRE DEGRADATION ISOLATION PIPELINE ===", flush=True)

# 1. SYNTHESIZE / INGEST MULTI-RACE GRAND PRIX STINT DATASET
# Features: [lap_in_stint, fuel_load_kg, gap_ahead_s, track_evolution_factor, compound_idx, track_temp_c]
# Target: observed_lap_time_delta_s (relative to baseline optimal lap)

np.random.seed(42)

def generate_race_stints(n_stints=120):
    records = []
    compound_params = {
        0: {"name": "Soft", "wear_slope": 0.085, "grip_delta": -0.45, "opt_temp": 95.0, "cliff_lap": 16},
        1: {"name": "Medium", "wear_slope": 0.048, "grip_delta": 0.00, "opt_temp": 105.0, "cliff_lap": 24},
        2: {"name": "Hard", "wear_slope": 0.026, "grip_delta": 0.50, "opt_temp": 115.0, "cliff_lap": 34}
    }
    
    for stint_id in range(n_stints):
        compound = np.random.choice([0, 1, 2], p=[0.25, 0.45, 0.30])
        params = compound_params[compound]
        stint_length = np.random.randint(12, params["cliff_lap"] + 6)
        
        start_fuel = np.random.uniform(50.0, 108.0)
        fuel_burn_per_lap = 1.75 + np.random.normal(0, 0.05)
        session_progress_start = np.random.uniform(0.0, 0.6)
        base_track_temp = np.random.uniform(32.0, 46.0)
        
        for lap in range(1, stint_length + 1):
            fuel_kg = max(2.0, start_fuel - (lap - 1) * fuel_burn_per_lap)
            fuel_delta_s = (fuel_kg - 10.0) * 0.033
            
            has_traffic = np.random.rand() < 0.35
            gap_ahead_s = np.random.uniform(0.4, 2.5) if has_traffic else np.random.uniform(4.0, 15.0)
            if gap_ahead_s < 1.8:
                traffic_delta_s = 0.65 * math.pow(1.0 - (gap_ahead_s / 1.8), 1.5)
            else:
                traffic_delta_s = 0.0
                
            session_prog = min(1.0, session_progress_start + (lap / 60.0))
            track_evol_factor = 1.0 + (math.sqrt(session_prog) * 0.04)
            evolution_delta_s = -(track_evol_factor - 1.0) * 15.0
            
            track_temp = base_track_temp + np.sin(lap / 10.0) * 1.5
            temp_diff = abs(track_temp - params["opt_temp"])
            temp_delta_s = 0.015 * temp_diff
            
            wear_ratio = lap / float(params["cliff_lap"])
            if wear_ratio < 0.85:
                pure_wear_delta_s = params["grip_delta"] + (params["wear_slope"] * lap) + (0.002 * lap * lap)
            else:
                cliff_excess = (wear_ratio - 0.85) * 10.0
                pure_wear_delta_s = params["grip_delta"] + (params["wear_slope"] * lap) + (0.002 * lap * lap) + (0.15 * cliff_excess * cliff_excess)
                
            true_wear_pct = min(100.0, (lap / float(params["cliff_lap"])) * 75.0 + (0.5 * lap))
            
            noise_s = np.random.normal(0, 0.06)
            observed_delta_s = pure_wear_delta_s + fuel_delta_s + traffic_delta_s + evolution_delta_s + temp_delta_s + noise_s
            
            records.append({
                "lap_in_stint": float(lap),
                "fuel_load_kg": float(fuel_kg),
                "gap_ahead_s": float(gap_ahead_s),
                "track_evolution_factor": float(track_evol_factor),
                "compound_idx": float(compound),
                "track_temp_c": float(track_temp),
                "true_wear_pct": float(true_wear_pct),
                "true_tyre_delta_s": float(pure_wear_delta_s),
                "fuel_delta_s": float(fuel_delta_s),
                "traffic_delta_s": float(traffic_delta_s),
                "evolution_delta_s": float(evolution_delta_s),
                "observed_delta_s": float(observed_delta_s)
            })
            
    return records

print("Generating 120 Grand Prix stints across Soft, Medium, and Hard compounds...", flush=True)
data = generate_race_stints(120)
print(f"Ingested {len(data)} total laps with telemetry confounds.", flush=True)

with open("training_artifacts/stint_dataset_sample.json", "w", encoding="utf-8") as f:
    json.dump(data[:50], f, indent=2)

X = np.array([[r["lap_in_stint"], r["fuel_load_kg"], r["gap_ahead_s"], r["track_evolution_factor"], r["compound_idx"], r["track_temp_c"]] for r in data], dtype=np.float32)
y_obs = np.array([r["observed_delta_s"] for r in data], dtype=np.float32)
y_wear_pct = np.array([r["true_wear_pct"] for r in data], dtype=np.float32)
y_pure_delta = np.array([r["true_tyre_delta_s"] for r in data], dtype=np.float32)

split_idx = int(len(data) * 0.8)
X_train, X_val = X[:split_idx], X[split_idx:]
y_train_obs, y_val_obs = y_obs[:split_idx], y_obs[split_idx:]
y_train_wear, y_val_wear = y_wear_pct[:split_idx], y_wear_pct[split_idx:]
y_train_pure, y_val_pure = y_pure_delta[:split_idx], y_pure_delta[split_idx:]

print(f"Dataset split: {len(X_train)} training laps, {len(X_val)} held-out validation laps.", flush=True)

import torch
import torch.nn as nn

class ExplainableTyreDegradationNet(nn.Module):
    def __init__(self):
        super(ExplainableTyreDegradationNet, self).__init__()
        
        self.fuel_layer = nn.Linear(1, 1, bias=False)
        self.traffic_net = nn.Sequential(
            nn.Linear(1, 8),
            nn.ReLU(),
            nn.Linear(8, 1)
        )
        self.evolution_layer = nn.Linear(1, 1, bias=False)
        
        self.wear_net = nn.Sequential(
            nn.Linear(3, 32),
            nn.ReLU(),
            nn.Linear(32, 16),
            nn.ReLU(),
            nn.Linear(16, 2)
        )
        
        with torch.no_grad():
            self.fuel_layer.weight.fill_(0.033)
            self.evolution_layer.weight.fill_(-15.0)

    def forward(self, x):
        lap = x[:, 0:1]
        fuel = x[:, 1:2]
        gap = x[:, 2:3]
        evol = x[:, 3:4]
        compound = x[:, 4:5]
        temp = x[:, 5:6]
        
        fuel_delta = self.fuel_layer(fuel - 10.0)
        gap_clamped = torch.clamp(2.0 - gap, min=0.0)
        traffic_delta = self.traffic_net(gap_clamped)
        evol_delta = self.evolution_layer(evol - 1.0)
        
        wear_input = torch.cat([lap, compound, temp / 100.0], dim=1)
        wear_out = self.wear_net(wear_input)
        pure_tyre_delta = wear_out[:, 0:1]
        wear_pct = torch.clamp(wear_out[:, 1:2] * 100.0, min=0.0, max=100.0)
        
        confidence_half_width = 0.08 + (lap * 0.006)
        
        output = torch.cat([wear_pct, pure_tyre_delta, fuel_delta, traffic_delta, evol_delta, confidence_half_width], dim=1)
        return output

model = ExplainableTyreDegradationNet()
optimizer = torch.optim.Adam(model.parameters(), lr=0.005)
criterion = nn.MSELoss()

t_X_train = torch.from_numpy(X_train)
t_y_obs_train = torch.from_numpy(y_train_obs).unsqueeze(1)
t_y_wear_train = torch.from_numpy(y_train_wear).unsqueeze(1)
t_y_pure_train = torch.from_numpy(y_train_pure).unsqueeze(1)

print("\n--- Training Explainable Tyre Degradation Model ---", flush=True)
model.train()
for epoch in range(400):
    optimizer.zero_grad()
    out = model(t_X_train)
    pred_wear_pct = out[:, 0:1]
    pred_pure_delta = out[:, 1:2]
    pred_fuel = out[:, 2:3]
    pred_traffic = out[:, 3:4]
    pred_evol = out[:, 4:5]
    
    loss_obs = criterion(pred_pure_delta + pred_fuel + pred_traffic + pred_evol, t_y_obs_train)
    loss_wear = criterion(pred_wear_pct, t_y_wear_train) * 0.001
    loss_pure = criterion(pred_pure_delta, t_y_pure_train)
    
    total_loss = loss_obs + loss_wear + loss_pure
    total_loss.backward()
    optimizer.step()
    
    if (epoch + 1) % 100 == 0:
        print(f"Epoch {epoch+1}/400 | Total Loss: {total_loss.item():.4f} | Obs MSE: {loss_obs.item():.4f} | Pure Tyre MSE: {loss_pure.item():.4f}", flush=True)

model.eval()
with torch.no_grad():
    t_X_val = torch.from_numpy(X_val)
    val_out = model(t_X_val)
    val_pred_wear = val_out[:, 0].numpy()
    val_pred_pure = val_out[:, 1].numpy()
    val_pred_obs = (val_out[:, 1] + val_out[:, 2] + val_out[:, 3] + val_out[:, 4]).numpy()
    
    mae_wear_pct = float(np.mean(np.abs(val_pred_wear - y_val_wear)))
    rmse_wear_pct = float(np.sqrt(np.mean((val_pred_wear - y_val_wear)**2)))
    
    mae_pace_delta = float(np.mean(np.abs(val_pred_obs - y_val_obs)))
    rmse_pace_delta = float(np.sqrt(np.mean((val_pred_obs - y_val_obs)**2)))
    
    r2_score = float(1.0 - (np.sum((y_val_obs - val_pred_obs)**2) / np.sum((y_val_obs - np.mean(y_val_obs))**2)))

print("\n=== HELD-OUT VALIDATION METRICS ===", flush=True)
print(f"Observed Pace Delta RMSE: {rmse_pace_delta:.3f} seconds/lap (MAE: {mae_pace_delta:.3f}s)", flush=True)
print(f"Isolated Tyre Wear RMSE: {rmse_wear_pct:.2f}% wear (MAE: {mae_wear_pct:.2f}%)", flush=True)
print(f"Model R^2 Score on Held-Out Race Stints: {r2_score:.4f}", flush=True)

# 5. EXPORT TO ONNX USING CLASSIC TORCHSCRIPT EXPORTER (Optimal for Unity Sentis)
onnx_path = "Assets/MLModels/tyre_degradation_ebm.onnx"
dummy_input = torch.tensor([[10.0, 75.0, 3.5, 1.02, 1.0, 38.0]], dtype=torch.float32)

torch.onnx.export(
    model,
    dummy_input,
    onnx_path,
    export_params=True,
    opset_version=12,
    do_constant_folding=True,
    input_names=["input_features"],
    output_names=["degradation_outputs"],
    dynamic_axes={
        "input_features": {0: "batch_size"},
        "degradation_outputs": {0: "batch_size"}
    },
    dynamo=False
)

print(f"\nSuccessfully exported trained Track 3 ONNX model to: {onnx_path}", flush=True)

metrics = {
    "model_name": "Track 3 Tyre Degradation Isolation EBM",
    "dataset_total_laps": len(data),
    "training_laps": len(X_train),
    "held_out_validation_laps": len(X_val),
    "observed_pace_rmse_s": round(rmse_pace_delta, 4),
    "observed_pace_mae_s": round(mae_pace_delta, 4),
    "isolated_wear_rmse_pct": round(rmse_wear_pct, 4),
    "isolated_wear_mae_pct": round(mae_wear_pct, 4),
    "r2_score": round(r2_score, 4),
    "onnx_export_path": onnx_path,
    "input_features": ["lap_in_stint", "fuel_load_kg", "gap_ahead_s", "track_evolution_factor", "compound_idx", "track_temp_c"],
    "output_channels": ["predicted_wear_pct", "isolated_tyre_delta_s", "fuel_delta_s", "traffic_delta_s", "evolution_delta_s", "confidence_half_width_s"]
}

with open("training_artifacts/track3_metrics.json", "w", encoding="utf-8") as f:
    json.dump(metrics, f, indent=2)

print("Saved metrics to training_artifacts/track3_metrics.json!", flush=True)
