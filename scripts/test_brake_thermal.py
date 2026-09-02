class BrakeThermalModelTest:
    def __init__(self, initial_temp_c=400.0):
        self.rotor_temp_c = initial_temp_c
        self.min_optimal_temp_c = 350.0
        self.max_optimal_temp_c = 750.0
        self.overheat_fade_temp_c = 880.0
        
        self.rotor_thermal_mass = 4200.0  # J/°C
        self.base_cooling_watts_per_c = 14.0  # W/°C
        self.speed_cooling_factor = 0.55  # W/(°C * m/s)
        
        self.friction_efficiency = 1.0
        self.is_fading = False
        self.is_cold = False

    def update(self, mechanical_braking_power_watts, vehicle_speed_mps, ambient_temp_c, aggressive, dt):
        if dt <= 0.0:
            return
        
        aggression_factor = 1.25 if aggressive else 1.0
        heat_input_watts = mechanical_braking_power_watts * aggression_factor
        
        duct_cooling_coeff = self.base_cooling_watts_per_c + (self.speed_cooling_factor * vehicle_speed_mps)
        cooling_loss_watts = duct_cooling_coeff * (self.rotor_temp_c - ambient_temp_c)
        
        net_heat_watts = heat_input_watts - cooling_loss_watts
        delta_temp_c = (net_heat_watts / self.rotor_thermal_mass) * dt
        
        self.rotor_temp_c = max(ambient_temp_c, min(1250.0, self.rotor_temp_c + delta_temp_c))
        
        if self.rotor_temp_c >= self.min_optimal_temp_c and self.rotor_temp_c <= self.max_optimal_temp_c:
            self.friction_efficiency = 1.0
            self.is_fading = False
            self.is_cold = False
        elif self.rotor_temp_c < self.min_optimal_temp_c:
            cold_deficit = self.min_optimal_temp_c - self.rotor_temp_c
            self.friction_efficiency = max(0.70, 1.0 - (0.0015 * cold_deficit))
            self.is_cold = True
            self.is_fading = False
        else:
            overheat_delta = self.rotor_temp_c - self.max_optimal_temp_c
            fade_ratio = overheat_delta / (self.overheat_fade_temp_c - self.max_optimal_temp_c)
            self.friction_efficiency = max(0.35, 1.0 - (0.45 * fade_ratio * fade_ratio))
            self.is_fading = (self.rotor_temp_c > 800.0)
            self.is_cold = False

def run_empirical_verification():
    dt = 0.02 # 50 Hz FixedUpdate
    ambient_c = 25.0
    brake = BrakeThermalModelTest(initial_temp_c=400.0) # Warm rotors at entry to braking zone
    
    print("=== EMPIRICAL BRAKE THERMAL VERIFICATION RUN ===", flush=True)
    print("Phase 1: Hard Braking Event (Turn 1 Entry: 320 km/h -> 85 km/h over 3.0s)", flush=True)
    
    time_elapsed = 0.0
    print(f"t = 0.00s | Speed: 320 km/h | Rotor Temp: {brake.rotor_temp_c:.1f}°C | Friction Efficiency: {brake.friction_efficiency*100:.1f}%", flush=True)
    
    for step in range(int(3.0 / dt)):
        t_phase = step * dt
        frac = t_phase / 3.0
        speed_mps = (1.0 - frac) * 88.8 + frac * 23.6
        power_watts = 220000.0 * (speed_mps / 88.8)
        
        brake.update(power_watts, speed_mps, ambient_c, aggressive=True, dt=dt)
        time_elapsed += dt
        
        if (step + 1) % 25 == 0:
            print(f"t = {time_elapsed:.2f}s | Speed: {speed_mps*3.6:.0f} km/h | Power: {power_watts/1000:.1f} kW | Rotor Temp: {brake.rotor_temp_c:.1f}°C | Efficiency: {brake.friction_efficiency*100:.1f}% | Fade: {brake.is_fading}", flush=True)

    peak_temp = brake.rotor_temp_c
    print(f"\n>> PEAK ROTOR TEMPERATURE REACHED: {peak_temp:.1f}°C (Friction Efficiency: {brake.friction_efficiency*100:.1f}%, Fading: {brake.is_fading})", flush=True)
    
    print("\n--- PHASE 2: BRAKE RELEASE & COOL-DOWN DOWN STRAIGHT (20.0 seconds) ---", flush=True)
    print("Brakes released (0 kW). Accelerating 85 km/h -> 310 km/h on subsequent straightaway.\n", flush=True)
    
    print(f"{'Time Elapsed':<14} | {'Speed (km/h)':<13} | {'Rotor Temp (°C)':<16} | {'Delta vs Peak (°C)':<20} | {'Friction Eff.':<14} | {'Status'}", flush=True)
    print("-" * 95, flush=True)
    
    cooldown_seconds = 20.0
    for step in range(int(cooldown_seconds / dt)):
        t_cool = (step + 1) * dt
        accel_frac = min(1.0, t_cool / 12.0)
        speed_mps = 23.6 + accel_frac * (86.1 - 23.6)
        
        brake.update(0.0, speed_mps, ambient_c, aggressive=False, dt=dt)
        time_elapsed += dt
        
        if (step + 1) % 50 == 0: # Every 1.0s
            delta_peak = brake.rotor_temp_c - peak_temp
            status = "Fading" if brake.is_fading else ("Optimal" if not brake.is_cold else "Cold")
            print(f"t = {t_cool:5.1f}s (tot {time_elapsed:5.1f}s) | {speed_mps*3.6:6.1f} km/h    | {brake.rotor_temp_c:6.1f} °C        | {delta_peak:+6.1f} °C            | {brake.friction_efficiency*100:5.1f}%        | {status}", flush=True)

if __name__ == "__main__":
    run_empirical_verification()
