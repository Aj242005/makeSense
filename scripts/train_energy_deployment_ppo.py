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

if sys.platform == 'win32':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')

print("=== TRACK 1: PER-LAP BUDGETED VECTORIZED PPO TRAINING ===", flush=True)

class VectorizedF1LapEnv:
    def __init__(self, num_envs=8, lap_length_s=90.0, dt=1.0):
        self.num_envs = num_envs
        self.lap_length_s = lap_length_s
        self.dt = dt
        self.lap_budget_j = 4000000.0 # 4.0 MJ usable deploy per lap (FIA limit)
        
        self.lap_time_s = np.zeros(num_envs, dtype=np.float32)
        self.current_lap = np.zeros(num_envs, dtype=np.int32)
        self.lap_energy_remaining_j = np.zeros(num_envs, dtype=np.float32)
        self.stint_soc_pct = np.zeros(num_envs, dtype=np.float32)
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
            self._reset_env(e, init_lap=int((e / num_envs) * 20) + 1)

    def _reset_env(self, e, init_lap=1):
        self.current_lap[e] = init_lap
        self.lap_time_s[e] = 0.0
        self.lap_energy_remaining_j[e] = self.lap_budget_j
        stint_frac = (init_lap - 1) / 20.0
        self.stint_soc_pct[e] = 100.0 * (1.0 - stint_frac)
        self.tyre_wear_pct[e] = stint_frac * 65.0
        self.fuel_kg[e] = 105.0 * (1.0 - stint_frac)
        self.rotor_temp_c[e] = 450.0
        self.speed_mps[e] = 65.0
        self.has_traffic[e] = np.random.rand() < 0.45
        self.gap_ahead_s[e] = np.random.uniform(0.8, 2.2) if self.has_traffic[e] else 10.0
        self.gap_behind_s[e] = np.random.uniform(1.5, 5.0)
        self.steps_stuck[e] = 0
        self.wear_slope[e] = 0.02

    def get_obs(self):
        obs = np.zeros((self.num_envs, 10), dtype=np.float32)
        for e in range(self.num_envs):
            soc_norm = np.clip(self.lap_energy_remaining_j[e] / self.lap_budget_j, 0.0, 1.0)
            wear_norm = np.clip(self.tyre_wear_pct[e] / 100.0, 0.0, 1.0)
            fuel_norm = np.clip(self.fuel_kg[e] / 105.0, 0.0, 1.0)
            gap_ahead_norm = np.clip(self.gap_ahead_s[e] / 10.0, 0.0, 1.0)
            has_ahead_norm = 1.0 if self.has_traffic[e] else 0.0
            gap_behind_norm = np.clip(self.gap_behind_s[e] / 10.0, 0.0, 1.0)
            lap_norm = np.clip(self.current_lap[e] / 20.0, 0.0, 1.0)
            
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
        attributions = np.zeros((self.num_envs, 4), dtype=np.float32)
        
        for e in range(self.num_envs):
            mode_idx = int(actions[e] // 2)
            braking_idx = int(actions[e] % 2)
            modes[e] = mode_idx
            brakes[e] = braking_idx
            
            lap_phase = (self.lap_time_s[e] % 15.0) / 15.0
            is_straight = lap_phase < 0.60
            is_corner = 0.60 <= lap_phase < 0.85
            is_braking = lap_phase >= 0.85
            
            # 1. Powertrain Deploy / Harvest (4.0 MJ per lap budget)
            deploy_kw = [120.0, 45.0, 0.0, -50.0][mode_idx]
            if self.lap_energy_remaining_j[e] <= 0.0 and deploy_kw > 0:
                deploy_kw = 0.0
                
            if deploy_kw > 0:
                self.lap_energy_remaining_j[e] = max(0.0, self.lap_energy_remaining_j[e] - deploy_kw * 1000.0 * self.dt)
            elif deploy_kw < 0:
                self.lap_energy_remaining_j[e] = min(self.lap_budget_j, self.lap_energy_remaining_j[e] + abs(deploy_kw) * 880.0 * self.dt)
                
            if is_braking:
                regen_kw = 160.0 if braking_idx == 1 else 100.0
                self.lap_energy_remaining_j[e] = min(self.lap_budget_j, self.lap_energy_remaining_j[e] + regen_kw * 880.0 * self.dt)
                
            self.fuel_kg[e] = max(0.0, self.fuel_kg[e] - 0.024 * self.dt)
            
            # 2. Speed Dynamics
            if is_straight:
                accel = (deploy_kw / 120.0) * 4.0
                self.speed_mps[e] = np.clip(self.speed_mps[e] + accel * self.dt, 50.0, 92.0)
            elif is_corner:
                self.speed_mps[e] = np.clip(self.speed_mps[e] - 1.0 * self.dt, 45.0, 60.0)
            else:
                decel = 18.0 if braking_idx == 1 else 14.0
                self.speed_mps[e] = np.clip(self.speed_mps[e] - decel * self.dt, 25.0, 50.0)
                
            # 3. Brake Thermals
            heat_in = (180000.0 if braking_idx == 1 else 110000.0) if is_braking else 0.0
            cooling = (14.0 + 0.55 * self.speed_mps[e]) * (self.rotor_temp_c[e] - 27.0)
            self.rotor_temp_c[e] = np.clip(self.rotor_temp_c[e] + ((heat_in - cooling) / 4200.0) * self.dt, 27.0, 1050.0)
            
            # 4. Tyre Wear
            if is_corner and mode_idx == 0:
                self.wear_slope[e] = 0.08
            elif mode_idx == 0:
                self.wear_slope[e] = 0.04
            elif mode_idx == 1:
                self.wear_slope[e] = 0.025
            else:
                self.wear_slope[e] = 0.015
            self.tyre_wear_pct[e] = np.clip(self.tyre_wear_pct[e] + self.wear_slope[e] * self.dt, 0.0, 100.0)
            
            # 5. Tactical Traffic
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
                    
            # 6. Local Per-Lap Energy Tracking (Immediate Feedback)
            lap_target_energy = self.lap_budget_j * (1.0 - (self.lap_time_s[e] / self.lap_length_s))
            lap_deficit = np.clip((lap_target_energy - self.lap_energy_remaining_j[e]) / 800000.0, 0.0, 1.0)
            p_soc = 0.20 * (lap_deficit * lap_deficit)
            
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
                    
            step_reward = float(np.clip(r_pace + r_tactical - p_soc - p_wear - p_brake, -0.50, 0.60))
            rewards[e] = step_reward
            
            attributions[e] = np.array([
                np.clip(50.0 + 50.0 * step_reward, 0.0, 100.0),
                (self.lap_energy_remaining_j[e] - lap_target_energy) / 10000.0,
                float(p_wear * 10.0),
                float(p_brake * 10.0)
            ], dtype=np.float32)
            
            self.lap_time_s[e] += self.dt
            if self.lap_time_s[e] >= self.lap_length_s:
                dones[e] = 1.0
                next_lap = (self.current_lap[e] % 20) + 1
                self._reset_env(e, init_lap=next_lap)
                
        return self.get_obs(), rewards, dones, {"modes": modes, "brakes": brakes, "attributions": attributions}

# 2. ACTOR-CRITIC ARCHITECTURE
def layer_init(layer, std=np.sqrt(2), bias_const=0.0):
    nn.init.orthogonal_(layer.weight, std)
    nn.init.constant_(layer.bias, bias_const)
    return layer

class EnergyPolicyActorCritic(nn.Module):
    def __init__(self, state_dim=10, action_dim=8):
        super(EnergyPolicyActorCritic, self).__init__()
        
        self.backbone = nn.Sequential(
            layer_init(nn.Linear(state_dim, 64)),
            nn.Tanh(),
            layer_init(nn.Linear(64, 64)),
            nn.Tanh()
        )
        
        self.actor_head = layer_init(nn.Linear(64, action_dim), std=0.01)
        self.critic_head = layer_init(nn.Linear(64, 1), std=1.0)
        self.explainability_head = layer_init(nn.Linear(64, 4), std=0.5)

    def forward(self, x):
        features = self.backbone(x)
        logits = self.actor_head(features)
        value = self.critic_head(features)
        explainability = self.explainability_head(features)
        return logits, value, explainability

    def get_action_and_value(self, state, action=None):
        logits, value, exp = self.forward(state)
        probs = torch.distributions.Categorical(logits=logits)
        if action is None:
            action = probs.sample()
        return action, probs.log_prob(action), probs.entropy(), value, exp

class Track1InferenceModel(nn.Module):
    def __init__(self, trained_model):
        super(Track1InferenceModel, self).__init__()
        self.backbone = trained_model.backbone
        self.actor_head = trained_model.actor_head
        self.explainability_head = trained_model.explainability_head

    def forward(self, x):
        features = self.backbone(x)
        logits = self.actor_head(features)
        explainability = self.explainability_head(features)
        return torch.cat([logits, explainability], dim=1)

# 3. PPO TRAINING LOOP
def train_ppo(total_timesteps=400000, num_envs=8, num_steps=256, mini_batch_size=256, ppo_epochs=4, initial_lr=3e-4, gamma=0.99, gae_lambda=0.95):
    log_dir = f"runs/track1_ppo_stabilized_{int(time.time())}"
    writer = SummaryWriter(log_dir=log_dir)
    print(f"\n[TensorBoard] Active logging directory: {os.path.abspath(log_dir)}", flush=True)
    
    envs = VectorizedF1LapEnv(num_envs=num_envs)
    policy = EnergyPolicyActorCritic()
    optimizer = optim.Adam(policy.parameters(), lr=initial_lr, eps=1e-5)
    mse_loss = nn.MSELoss()
    
    batch_size = num_envs * num_steps
    num_updates = total_timesteps // batch_size
    start_time = time.time()
    
    global_step = 0
    obs = envs.get_obs()
    
    print(f"Starting {num_updates} PPO update iterations ({total_timesteps:,} total steps across {num_envs} vectorized environments)...", flush=True)
    
    history_steps = []
    history_rew = []
    history_exp_var = []
    history_vloss = []
    history_mix = []
    
    for update in range(1, num_updates + 1):
        frac = 1.0 - (update - 1.0) / num_updates
        lrnow = frac * initial_lr
        optimizer.param_groups[0]["lr"] = lrnow
        
        obs_buffer = np.zeros((num_steps, num_envs, 10), dtype=np.float32)
        actions_buffer = np.zeros((num_steps, num_envs), dtype=np.int64)
        logprobs_buffer = np.zeros((num_steps, num_envs), dtype=np.float32)
        rewards_buffer = np.zeros((num_steps, num_envs), dtype=np.float32)
        dones_buffer = np.zeros((num_steps, num_envs), dtype=np.float32)
        values_buffer = np.zeros((num_steps, num_envs), dtype=np.float32)
        attributions_buffer = np.zeros((num_steps, num_envs, 4), dtype=np.float32)
        update_modes, update_brakes = [], []
        
        for step in range(num_steps):
            global_step += num_envs
            obs_buffer[step] = obs
            
            with torch.no_grad():
                state_t = torch.from_numpy(obs)
                action, logprob, _, value, _ = policy.get_action_and_value(state_t)
                
            actions_buffer[step] = action.numpy()
            logprobs_buffer[step] = logprob.numpy()
            values_buffer[step] = value.squeeze(-1).numpy()
            
            next_obs, rews, dones, info = envs.step(action.numpy())
            rewards_buffer[step] = rews
            dones_buffer[step] = dones
            attributions_buffer[step] = info["attributions"]
            
            update_modes.extend(info["modes"].tolist())
            update_brakes.extend(info["brakes"].tolist())
            
            obs = next_obs
            
        with torch.no_grad():
            next_state_t = torch.from_numpy(next_obs)
            _, _, _, next_value, _ = policy.get_action_and_value(next_state_t)
            next_value = next_value.squeeze(-1).numpy()
            
        advantages = np.zeros_like(rewards_buffer)
        lastgaelam = np.zeros(num_envs, dtype=np.float32)
        
        for t in reversed(range(num_steps)):
            if t == num_steps - 1:
                nextnonterminal = 1.0 - dones_buffer[t]
                nextvalues = next_value
            else:
                nextnonterminal = 1.0 - dones_buffer[t]
                nextvalues = values_buffer[t + 1]
                
            delta = rewards_buffer[t] + gamma * nextvalues * nextnonterminal - values_buffer[t]
            advantages[t] = lastgaelam = delta + gamma * gae_lambda * nextnonterminal * lastgaelam
            
        returns = advantages + values_buffer
        
        b_obs = torch.tensor(obs_buffer.reshape(-1, 10), dtype=torch.float32)
        b_actions = torch.tensor(actions_buffer.reshape(-1), dtype=torch.long)
        b_logprobs = torch.tensor(logprobs_buffer.reshape(-1), dtype=torch.float32)
        b_values = torch.tensor(values_buffer.reshape(-1), dtype=torch.float32)
        b_returns = torch.tensor(returns.reshape(-1), dtype=torch.float32).unsqueeze(1)
        b_advantages = torch.tensor(advantages.reshape(-1), dtype=torch.float32)
        b_advantages = (b_advantages - b_advantages.mean()) / (b_advantages.std() + 1e-8)
        b_attributions = torch.tensor(attributions_buffer.reshape(-1, 4), dtype=torch.float32)
        
        y_true = returns.reshape(-1)
        y_pred = values_buffer.reshape(-1)
        var_y = np.var(y_true)
        explained_var = 1.0 - (np.var(y_true - y_pred) / var_y) if var_y > 1e-6 else 0.0
        
        b_inds = np.arange(batch_size)
        clipfracs = []
        
        for epoch in range(ppo_epochs):
            np.random.shuffle(b_inds)
            for start in range(0, batch_size, mini_batch_size):
                end = start + mini_batch_size
                mb_inds = b_inds[start:end]
                
                _, newlogprob, entropy, newvalue, pred_exp = policy.get_action_and_value(
                    b_obs[mb_inds], b_actions[mb_inds]
                )
                
                logratio = newlogprob - b_logprobs[mb_inds]
                ratio = torch.exp(logratio)
                
                with torch.no_grad():
                    clipfracs += [((ratio - 1.0).abs() > 0.2).float().mean().item()]
                    
                mb_adv = b_advantages[mb_inds]
                surr1 = ratio * mb_adv
                surr2 = torch.clamp(ratio, 0.8, 1.2) * mb_adv
                policy_loss = -torch.min(surr1, surr2).mean()
                
                v_clipped = b_values[mb_inds] + torch.clamp(newvalue.squeeze() - b_values[mb_inds], -0.2, 0.2)
                v_loss_unclipped = (newvalue - b_returns[mb_inds]) ** 2
                v_loss_clipped = (v_clipped.unsqueeze(1) - b_returns[mb_inds]) ** 2
                v_loss_max = torch.max(v_loss_unclipped, v_loss_clipped)
                value_loss = 0.5 * v_loss_max.mean()
                
                entropy_loss = -0.01 * entropy.mean()
                exp_loss = 0.5 * mse_loss(pred_exp, b_attributions[mb_inds])
                
                total_loss = policy_loss + value_loss + entropy_loss + exp_loss
                
                optimizer.zero_grad()
                total_loss.backward()
                nn.utils.clip_grad_norm_(policy.parameters(), 0.5)
                optimizer.step()
                
        elapsed = time.time() - start_time
        sps = global_step / elapsed
        mean_reward = float(np.mean(rewards_buffer))
        
        total_recent = len(update_modes)
        push_pct = (update_modes.count(0) / total_recent) * 100.0
        bal_pct = (update_modes.count(1) / total_recent) * 100.0
        hold_pct = (update_modes.count(2) / total_recent) * 100.0
        save_pct = (update_modes.count(3) / total_recent) * 100.0
        aggr_pct = (update_brakes.count(1) / total_recent) * 100.0
        
        writer.add_scalar("charts/ep_rew_mean", mean_reward, global_step)
        writer.add_scalar("charts/learning_rate", lrnow, global_step)
        writer.add_scalar("losses/explained_variance", explained_var, global_step)
        writer.add_scalar("losses/value_loss", value_loss.item(), global_step)
        writer.add_scalar("losses/policy_loss", policy_loss.item(), global_step)
        writer.add_scalar("losses/entropy", entropy.mean().item(), global_step)
        writer.add_scalar("losses/explainability_loss", exp_loss.item(), global_step)
        writer.add_scalar("actions/push_pct", push_pct, global_step)
        writer.add_scalar("actions/balanced_pct", bal_pct, global_step)
        writer.add_scalar("actions/hold_pct", hold_pct, global_step)
        writer.add_scalar("actions/save_pct", save_pct, global_step)
        writer.add_scalar("actions/aggressive_braking_pct", aggr_pct, global_step)
        
        if update % 15 == 0 or update == num_updates or update == 1:
            print(f"Update {update:3d}/{num_updates} | Step: {global_step:6d} | LR: {lrnow:.1e} | Rew: {mean_reward:+.3f} | ExplVar: {explained_var:+.3f} | VLoss: {value_loss.item():.4f} | Mix: [Push:{push_pct:4.1f}% Bal:{bal_pct:4.1f}% Hold:{hold_pct:4.1f}% Save:{save_pct:4.1f}%] | SPS: {sps:.0f}", flush=True)
            history_steps.append(global_step)
            history_rew.append(round(mean_reward, 4))
            history_exp_var.append(round(float(explained_var), 4))
            history_vloss.append(round(float(value_loss.item()), 4))
            history_mix.append({
                "push": round(push_pct, 1),
                "balanced": round(bal_pct, 1),
                "hold": round(hold_pct, 1),
                "save": round(save_pct, 1),
                "aggressive_braking": round(aggr_pct, 1)
            })

    writer.close()
    
    # 6. FINAL EVALUATION ROLLOUT
    print("\n--- RUNNING FINAL 2,000-STEP EVALUATION ROLLOUT ---", flush=True)
    eval_modes = {0: 0, 1: 0, 2: 0, 3: 0}
    eval_brakes = {0: 0, 1: 0}
    eval_env = VectorizedF1LapEnv(num_envs=1)
    eval_obs = eval_env.get_obs()
    
    for _ in range(2000):
        st = torch.from_numpy(eval_obs)
        with torch.no_grad():
            act, _, _, _, _ = policy.get_action_and_value(st)
        eval_obs, _, _, inf = eval_env.step(act.numpy())
        eval_modes[inf["modes"][0]] += 1
        eval_brakes[inf["brakes"][0]] += 1
        
    final_push_pct = (eval_modes[0] / 2000.0) * 100.0
    final_bal_pct = (eval_modes[1] / 2000.0) * 100.0
    final_hold_pct = (eval_modes[2] / 2000.0) * 100.0
    final_save_pct = (eval_modes[3] / 2000.0) * 100.0
    final_norm_pct = (eval_brakes[0] / 2000.0) * 100.0
    final_aggr_pct = (eval_brakes[1] / 2000.0) * 100.0
    
    print("\n=== FINAL CONVERGED DEPLOYMENT MODE MIX (2,000 EVALUATION STEPS) ===", flush=True)
    print(f"  Push Mode:             {final_push_pct:5.1f}%", flush=True)
    print(f"  Balanced Mode:         {final_bal_pct:5.1f}%", flush=True)
    print(f"  Hold Mode:             {final_hold_pct:5.1f}%", flush=True)
    print(f"  Save Mode:             {final_save_pct:5.1f}%", flush=True)
    print(f"  Normal Braking:        {final_norm_pct:5.1f}%", flush=True)
    print(f"  Aggressive Braking:    {final_aggr_pct:5.1f}%", flush=True)

    # 7. EXPORT ONNX
    os.makedirs("Assets/MLModels", exist_ok=True)
    onnx_path = "Assets/MLModels/energy_deployment_ppo.onnx"
    export_net = Track1InferenceModel(policy)
    export_net.eval()
    
    dummy_input = torch.tensor([[0.85, 0.15, 0.70, 0.12, 1.0, 0.35, 0.25, 0.50, 0.45, 0.05]], dtype=torch.float32)
    torch.onnx.export(
        export_net,
        dummy_input,
        onnx_path,
        export_params=True,
        opset_version=12,
        do_constant_folding=True,
        input_names=["car_state_features"],
        output_names=["action_and_explainability"],
        dynamic_axes={
            "car_state_features": {0: "batch_size"},
            "action_and_explainability": {0: "batch_size"}
        },
        dynamo=False
    )
    
    print(f"\nSuccessfully exported converged Track 1 ONNX model to: {onnx_path}", flush=True)
    
    metrics = {
        "model_name": "Track 1 Energy Deployment PPO Policy (Budget-Stabilized Vectorized Run)",
        "training_timesteps": total_timesteps,
        "total_wall_clock_seconds": round(elapsed, 2),
        "actual_throughput_sps": round(sps, 1),
        "tensorboard_log_dir": os.path.abspath(log_dir),
        "history_milestones": {
            "steps": history_steps,
            "rewards": history_rew,
            "explained_variance": history_exp_var,
            "value_losses": history_vloss,
            "mix_progression": history_mix
        },
        "final_converged_mix": {
            "push_pct": round(final_push_pct, 1),
            "balanced_pct": round(final_bal_pct, 1),
            "hold_pct": round(final_hold_pct, 1),
            "save_pct": round(final_save_pct, 1),
            "normal_braking_pct": round(final_norm_pct, 1),
            "aggressive_braking_pct": round(final_aggr_pct, 1)
        },
        "onnx_export_path": onnx_path
    }
    
    with open("training_artifacts/track1_metrics.json", "w", encoding="utf-8") as f:
        json.dump(metrics, f, indent=2)
        
    print("Saved updated metrics to: training_artifacts/track1_metrics.json", flush=True)

if __name__ == "__main__":
    train_ppo(total_timesteps=400000)
