import numpy as np
import torch
import torch.nn as nn
import torch.optim as optim

class VectorizedF1TacticalEnv:
    def __init__(self, num_envs=8, lap_length_s=90.0, total_laps=20, dt=1.0):
        self.num_envs = num_envs
        self.lap_length_s = lap_length_s
        self.total_laps = total_laps
        self.dt = dt
        self.total_stint_s = total_laps * lap_length_s
        self.total_energy_j = 80000000.0
        
        self.time_in_stint_s = np.zeros(num_envs, dtype=np.float32)
        self.lap_time_s = np.zeros(num_envs, dtype=np.float32)
        self.current_lap = np.zeros(num_envs, dtype=np.int32)
        self.energy_remaining_j = np.zeros(num_envs, dtype=np.float32)
        self.tyre_wear_pct = np.zeros(num_envs, dtype=np.float32)
        self.rotor_temp_c = np.zeros(num_envs, dtype=np.float32)
        self.fuel_kg = np.zeros(num_envs, dtype=np.float32)
        self.speed_mps = np.zeros(num_envs, dtype=np.float32)
        self.has_traffic = np.zeros(num_envs, dtype=bool)
        self.gap_ahead_s = np.zeros(num_envs, dtype=np.float32)
        self.gap_behind_s = np.zeros(num_envs, dtype=np.float32)
        self.steps_stuck = np.zeros(num_envs, dtype=np.int32)
        self.wear_slope = np.zeros(num_envs, dtype=np.float32)
        
        for e in range(num_envs):
            self._reset_env(e, init_lap=int((e / num_envs) * total_laps) + 1)

    def _reset_env(self, e, init_lap=1):
        self.current_lap[e] = init_lap
        self.time_in_stint_s[e] = (init_lap - 1) * self.lap_length_s
        self.lap_time_s[e] = np.random.uniform(0.0, self.lap_length_s)
        frac = self.time_in_stint_s[e] / self.total_stint_s
        self.energy_remaining_j[e] = self.total_energy_j * (1.0 - frac)
        self.tyre_wear_pct[e] = frac * 70.0
        self.fuel_kg[e] = 105.0 * (1.0 - frac)
        self.rotor_temp_c[e] = 450.0
        self.speed_mps[e] = 65.0
        self.has_traffic[e] = np.random.rand() < 0.50
        self.gap_ahead_s[e] = np.random.uniform(0.8, 2.2) if self.has_traffic[e] else 10.0
        self.gap_behind_s[e] = np.random.uniform(1.5, 5.0)
        self.steps_stuck[e] = 0
        self.wear_slope[e] = 0.02

    def get_obs(self):
        obs = np.zeros((self.num_envs, 10), dtype=np.float32)
        for e in range(self.num_envs):
            soc_norm = np.clip(self.energy_remaining_j[e] / self.total_energy_j, 0.0, 1.0)
            wear_norm = np.clip(self.tyre_wear_pct[e] / 100.0, 0.0, 1.0)
            fuel_norm = np.clip(self.fuel_kg[e] / 105.0, 0.0, 1.0)
            gap_ahead_norm = np.clip(self.gap_ahead_s[e] / 10.0, 0.0, 1.0)
            has_ahead_norm = 1.0 if self.has_traffic[e] else 0.0
            gap_behind_norm = np.clip(self.gap_behind_s[e] / 10.0, 0.0, 1.0)
            lap_norm = np.clip(self.current_lap[e] / float(self.total_laps), 0.0, 1.0)
            
            lap_phase = (self.lap_time_s[e] % 15.0) / 15.0
            evol_norm = np.clip(lap_phase, 0.0, 1.0)
            brake_norm = np.clip((self.rotor_temp_c[e] - 20.0) / 980.0, 0.0, 1.0)
            wear_slope_norm = np.clip(self.wear_slope[e] / 0.10, 0.0, 1.0)
            
            obs[e] = np.array([
                soc_norm, wear_norm, fuel_norm, gap_ahead_norm, has_ahead_norm,
                gap_behind_norm, lap_norm, evol_norm, brake_norm, wear_slope_norm
            ], dtype=np.float32)
        return obs

    def step(self, actions):
        rewards = np.zeros(self.num_envs, dtype=np.float32)
        dones = np.zeros(self.num_envs, dtype=np.float32)
        modes = np.zeros(self.num_envs, dtype=np.int32)
        brakes = np.zeros(self.num_envs, dtype=np.int32)
        
        for e in range(self.num_envs):
            mode_idx = actions[e] // 2
            braking_idx = actions[e] % 2
            modes[e] = mode_idx
            brakes[e] = braking_idx
            
            lap_phase = (self.lap_time_s[e] % 15.0) / 15.0
            is_straight = lap_phase < 0.60
            is_corner = 0.60 <= lap_phase < 0.85
            is_braking = lap_phase >= 0.85
            
            deploy_kw = [120.0, 45.0, 0.0, -50.0][mode_idx]
            if self.energy_remaining_j[e] <= 0.0 and deploy_kw > 0:
                deploy_kw = 0.0
                
            if deploy_kw > 0:
                self.energy_remaining_j[e] = max(0.0, self.energy_remaining_j[e] - deploy_kw * 1000.0 * self.dt)
            elif deploy_kw < 0:
                self.energy_remaining_j[e] = min(self.total_energy_j, self.energy_remaining_j[e] + abs(deploy_kw) * 880.0 * self.dt)
                
            if is_braking:
                regen_kw = 160.0 if braking_idx == 1 else 100.0
                self.energy_remaining_j[e] = min(self.total_energy_j, self.energy_remaining_j[e] + regen_kw * 880.0 * self.dt)
                
            self.fuel_kg[e] = max(0.0, self.fuel_kg[e] - 0.024 * self.dt)
            
            if is_straight:
                accel = (deploy_kw / 120.0) * 4.0
                self.speed_mps[e] = np.clip(self.speed_mps[e] + accel * self.dt, 50.0, 92.0)
            elif is_corner:
                self.speed_mps[e] = np.clip(self.speed_mps[e] - 1.0 * self.dt, 45.0, 60.0)
            else:
                decel = 18.0 if braking_idx == 1 else 14.0
                self.speed_mps[e] = np.clip(self.speed_mps[e] - decel * self.dt, 25.0, 50.0)
                
            heat_in = (180000.0 if braking_idx == 1 else 110000.0) if is_braking else 0.0
            cooling = (14.0 + 0.55 * self.speed_mps[e]) * (self.rotor_temp_c[e] - 27.0)
            self.rotor_temp_c[e] = np.clip(self.rotor_temp_c[e] + ((heat_in - cooling) / 4200.0) * self.dt, 27.0, 1050.0)
            
            if is_corner and mode_idx == 0:
                self.wear_slope[e] = 0.08
            elif mode_idx == 0:
                self.wear_slope[e] = 0.04
            elif mode_idx == 1:
                self.wear_slope[e] = 0.025
            else:
                self.wear_slope[e] = 0.015
            self.tyre_wear_pct[e] = np.clip(self.tyre_wear_pct[e] + self.wear_slope[e] * self.dt, 0.0, 100.0)
            
            prev_gap = self.gap_ahead_s[e]
            if self.has_traffic[e]:
                closing_speed = self.speed_mps[e] - 65.0
                self.gap_ahead_s[e] -= (closing_speed / 65.0) * self.dt
                if self.gap_ahead_s[e] <= 0.0:
                    self.has_traffic[e] = False
                    self.gap_ahead_s[e] = 10.0
                    self.steps_stuck[e] = 0
                elif self.gap_ahead_s[e] < 1.8:
                    if self.gap_ahead_s[e] >= prev_gap:
                        self.steps_stuck[e] += 1
                    else:
                        self.steps_stuck[e] = max(0, self.steps_stuck[e] - 1)
            else:
                if np.random.rand() < 0.02:
                    self.has_traffic[e] = True
                    self.gap_ahead_s[e] = np.random.uniform(1.2, 2.4)
                    self.steps_stuck[e] = 0
                    
            target_soc = 1.0 - (self.time_in_stint_s[e] / self.total_stint_s)
            actual_soc = self.energy_remaining_j[e] / self.total_energy_j
            soc_deficit = np.clip((target_soc - actual_soc) / 0.15, 0.0, 1.0)
            p_soc = 0.20 * (soc_deficit * soc_deficit)
            
            p_wear = 0.10 * np.clip(self.wear_slope[e] / 0.08, 0.0, 1.0)
            p_brake = 0.15 * (np.clip((self.rotor_temp_c[e] - 750.0) / 200.0, 0.0, 1.0) ** 2)
            
            if is_straight:
                if self.has_traffic[e] and self.gap_ahead_s[e] < 1.8:
                    closing_rate = (prev_gap - self.gap_ahead_s[e]) / self.dt
                    r_tactical = 0.30 * np.tanh(closing_rate * 2.0) - (0.15 * min(1.0, self.steps_stuck[e] / 4.0))
                    r_pace = 0.25 * np.tanh((self.speed_mps[e] - 65.0) / 15.0)
                else:
                    r_tactical = 0.0
                    r_pace = 0.35 * np.tanh((self.speed_mps[e] - 65.0) / 15.0)
            elif is_corner:
                r_tactical = 0.0
                r_pace = 0.20 if mode_idx in [1, 2] else -0.15
            else:
                r_tactical = 0.0
                if braking_idx == 1 and self.rotor_temp_c[e] < 750.0:
                    r_pace = 0.30
                elif braking_idx == 0 and self.rotor_temp_c[e] >= 750.0:
                    r_pace = 0.25
                else:
                    r_pace = 0.05
                    
            rewards[e] = float(np.clip(r_pace + r_tactical - p_soc - p_wear - p_brake, -0.50, 0.60))
            
            self.time_in_stint_s[e] += self.dt
            self.lap_time_s[e] += self.dt
            if self.lap_time_s[e] >= self.lap_length_s:
                self.lap_time_s[e] = 0.0
                self.current_lap[e] += 1
                
            if (self.current_lap[e] > self.total_laps) or (self.time_in_stint_s[e] >= self.total_stint_s):
                dones[e] = 1.0
                self._reset_env(e, init_lap=1)
                
        return self.get_obs(), rewards, dones, {"modes": modes, "brakes": brakes}

print("Testing Vectorized Environment...")
venv = VectorizedF1TacticalEnv(num_envs=8)
obs = venv.get_obs()
print("Vectorized Obs shape:", obs.shape)
next_obs, rews, dones, info = venv.step(np.zeros(8, dtype=np.int64))
print("Rewards sample:", rews)
