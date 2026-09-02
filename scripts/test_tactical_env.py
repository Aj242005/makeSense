import os
import sys
import time
import math
import json
import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim
from torch.utils.tensorboard import SummaryWriter

# Physical parameters for Track 1 Energy & Tactical Policy
class F1TacticalEnergyEnv:
    def __init__(self, lap_length_s=90.0, total_laps=20, dt=1.0):
        self.lap_length_s = lap_length_s
        self.total_laps = total_laps
        self.dt = dt
        self.total_stint_s = total_laps * lap_length_s
        self.total_energy_j = 80000000.0 # 80 MJ MGU-K deploy pool (4 MJ/lap * 20 laps)
        self.reset()

    def reset(self):
        self.time_in_stint_s = 0.0
        self.lap_time_s = 0.0
        self.current_lap = 1
        self.energy_remaining_j = self.total_energy_j
        self.tyre_wear_pct = 0.0
        self.rotor_temp_c = 450.0
        self.fuel_kg = 105.0
        self.speed_mps = 65.0
        
        # Traffic / tactical initialization
        self.has_traffic = np.random.rand() < 0.50
        self.gap_ahead_s = np.random.uniform(0.8, 2.2) if self.has_traffic else 10.0
        self.gap_behind_s = np.random.uniform(1.5, 5.0)
        self.steps_stuck = 0
        self.wear_slope = 0.02
        
        return self._get_obs()

    def _get_obs(self):
        # 10 Observation Channels
        soc_norm = np.clip(self.energy_remaining_j / self.total_energy_j, 0.0, 1.0)
        wear_norm = np.clip(self.tyre_wear_pct / 100.0, 0.0, 1.0)
        fuel_norm = np.clip(self.fuel_kg / 105.0, 0.0, 1.0)
        gap_ahead_norm = np.clip(self.gap_ahead_s / 10.0, 0.0, 1.0)
        has_ahead_norm = 1.0 if self.has_traffic else 0.0
        gap_behind_norm = np.clip(self.gap_behind_s / 10.0, 0.0, 1.0)
        lap_norm = np.clip(self.current_lap / float(self.total_laps), 0.0, 1.0)
        
        # Sector / zone feature: Straight (0.0 - 0.55), Corner (0.55 - 0.85), Braking (0.85 - 1.0)
        lap_phase = (self.lap_time_s % 15.0) / 15.0 # 15s recurring sector block
        evol_norm = np.clip(lap_phase, 0.0, 1.0)
        brake_norm = np.clip((self.rotor_temp_c - 20.0) / 980.0, 0.0, 1.0)
        wear_slope_norm = np.clip(self.wear_slope / 0.10, 0.0, 1.0)
        
        return np.array([
            soc_norm, wear_norm, fuel_norm, gap_ahead_norm, has_ahead_norm,
            gap_behind_norm, lap_norm, evol_norm, brake_norm, wear_slope_norm
        ], dtype=np.float32)

    def step(self, action_idx):
        mode_idx = action_idx // 2       # 0: Push (+120kW), 1: Balanced (+45kW), 2: Hold (0kW), 3: Save (-50kW)
        braking_idx = action_idx % 2     # 0: Normal, 1: Aggressive
        
        lap_phase = (self.lap_time_s % 15.0) / 15.0
        is_straight = lap_phase < 0.60
        is_corner = 0.60 <= lap_phase < 0.85
        is_braking = lap_phase >= 0.85
        
        # 1. Powertrain Deploy / Harvest
        deploy_kw = [120.0, 45.0, 0.0, -50.0][mode_idx]
        if self.energy_remaining_j <= 0.0 and deploy_kw > 0:
            deploy_kw = 0.0
            
        if deploy_kw > 0:
            self.energy_remaining_j = max(0.0, self.energy_remaining_j - deploy_kw * 1000.0 * self.dt)
        elif deploy_kw < 0:
            self.energy_remaining_j = min(self.total_energy_j, self.energy_remaining_j + abs(deploy_kw) * 880.0 * self.dt)
            
        # Braking Regen
        if is_braking:
            regen_kw = 160.0 if braking_idx == 1 else 100.0
            self.energy_remaining_j = min(self.total_energy_j, self.energy_remaining_j + regen_kw * 880.0 * self.dt)
            
        self.fuel_kg = max(0.0, self.fuel_kg - 0.024 * self.dt)
        
        # 2. Speed & Pace
        if is_straight:
            # On straights: Push gives max top speed
            accel = (deploy_kw / 120.0) * 4.0
            self.speed_mps = np.clip(self.speed_mps + accel * self.dt, 50.0, 92.0)
        elif is_corner:
            # In corners: speed is grip limited
            self.speed_mps = np.clip(self.speed_mps - 1.0 * self.dt, 45.0, 60.0)
        else:
            # Braking zone
            decel = 18.0 if braking_idx == 1 else 14.0
            self.speed_mps = np.clip(self.speed_mps - decel * self.dt, 25.0, 50.0)
            
        # 3. Brake Thermals
        if is_braking:
            heat_in = 180000.0 if braking_idx == 1 else 110000.0
        else:
            heat_in = 0.0
        cooling = (14.0 + 0.55 * self.speed_mps) * (self.rotor_temp_c - 27.0)
        self.rotor_temp_c = np.clip(self.rotor_temp_c + ((heat_in - cooling) / 4200.0) * self.dt, 27.0, 1050.0)
        
        # 4. Tyre Wear
        base_slip = 0.015
        if is_corner and mode_idx == 0:
            # Pushing in corners wastes tyre life with wheelspin
            self.wear_slope = 0.08
        elif mode_idx == 0:
            self.wear_slope = 0.04
        elif mode_idx == 1:
            self.wear_slope = 0.025
        else:
            self.wear_slope = 0.015
        self.tyre_wear_pct = np.clip(self.tyre_wear_pct + self.wear_slope * self.dt, 0.0, 100.0)
        
        # 5. Tactical Traffic
        prev_gap = self.gap_ahead_s
        if self.has_traffic:
            closing_speed = self.speed_mps - 65.0
            self.gap_ahead_s -= (closing_speed / 65.0) * self.dt
            if self.gap_ahead_s <= 0.0:
                self.has_traffic = False
                self.gap_ahead_s = 10.0
                self.steps_stuck = 0
            elif self.gap_ahead_s < 1.8:
                if self.gap_ahead_s >= prev_gap:
                    self.steps_stuck += 1
                else:
                    self.steps_stuck = max(0, self.steps_stuck - 1)
        else:
            if np.random.rand() < 0.02:
                self.has_traffic = True
                self.gap_ahead_s = np.random.uniform(1.2, 2.4)
                self.steps_stuck = 0
                
        # 6. Distinct Contextual Rewards
        # Target SoC linear schedule
        target_soc = 1.0 - (self.time_in_stint_s / self.total_stint_s)
        actual_soc = self.energy_remaining_j / self.total_energy_j
        soc_deficit = np.clip((target_soc - actual_soc) / 0.15, 0.0, 1.0)
        p_soc = 0.20 * (soc_deficit * soc_deficit)
        
        p_wear = 0.10 * np.clip(self.wear_slope / 0.08, 0.0, 1.0)
        p_brake = 0.15 * (np.clip((self.rotor_temp_c - 750.0) / 200.0, 0.0, 1.0) ** 2)
        
        if is_straight:
            if self.has_traffic and self.gap_ahead_s < 1.8:
                # Overtaking zone: Push is rewarded
                closing_rate = (prev_gap - self.gap_ahead_s) / self.dt
                r_tactical = 0.30 * math.tanh(closing_rate * 2.0) - (0.15 * min(1.0, self.steps_stuck / 4.0))
                r_pace = 0.25 * math.tanh((self.speed_mps - 65.0) / 15.0)
            else:
                # Clean air straight: Balanced is optimal
                r_tactical = 0.0
                r_pace = 0.35 * math.tanh((self.speed_mps - 65.0) / 15.0)
        elif is_corner:
            # Corner: Hold/Save saves tyre life
            r_tactical = 0.0
            r_pace = 0.20 if mode_idx in [1, 2] else -0.15
        else:
            # Braking: Aggressive regen is rewarded unless rotors overheat
            r_tactical = 0.0
            if braking_idx == 1 and self.rotor_temp_c < 750.0:
                r_pace = 0.30
            elif braking_idx == 0 and self.rotor_temp_c >= 750.0:
                r_pace = 0.25
            else:
                r_pace = 0.05
                
        step_reward = float(np.clip(r_pace + r_tactical - p_soc - p_wear - p_brake, -0.50, 0.60))
        
        # Supervised Explainability Vector
        attribution = np.array([
            np.clip(50.0 + 50.0 * step_reward, 0.0, 100.0),
            (actual_soc - target_soc) * 100.0,
            float(p_wear * 10.0),
            float(p_brake * 10.0)
        ], dtype=np.float32)
        
        self.time_in_stint_s += self.dt
        self.lap_time_s += self.dt
        if self.lap_time_s >= self.lap_length_s:
            self.lap_time_s = 0.0
            self.current_lap += 1
            
        done = (self.current_lap > self.total_laps) or (self.time_in_stint_s >= self.total_stint_s)
        
        return self._get_obs(), step_reward, done, {"attribution": attribution, "mode": mode_idx, "braking": braking_idx}

def run_test():
    env = F1TacticalEnergyEnv()
    obs = env.reset()
    print("Env test passed. Obs shape:", obs.shape)

if __name__ == "__main__":
    run_test()
