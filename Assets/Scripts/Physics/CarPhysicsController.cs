using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.Physics
{
    public enum VehicleGear
    {
        Reverse = -1,
        Neutral = 0,
        Gear1 = 1,
        Gear2 = 2,
        Gear3 = 3,
        Gear4 = 4,
        Gear5 = 5,
        Gear6 = 6,
        Gear7 = 7,
        Gear8 = 8
    }

    /// <summary>
    /// CarPhysicsController delivers responsive, authentic Formula 1 vehicle dynamics:
    /// - Razor-sharp steering responsiveness with increased turning angle and downforce bite.
    /// - 1,000 HP Hybrid Powertrain + Shift Key MGU-K Boost (1,200 HP, 365+ km/h top speed)
    /// - Strict Battery Lock: When Battery reaches 0%, MGU-K boost is disabled until regenerated.
    /// - Dynamic Tyre Compound Selection (Soft, Medium, Hard) with progressive wear.
    /// - MGU-K Regenerative Braking recovery (+160 kW into battery on aggressive braking).
    /// - 3D Surface & Elevation Tracking: Smoothly follows 3D road slopes without self-raycast interference.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarPhysicsController : MonoBehaviour
    {
        [Header("Tyre Compound & Wear Settings")]
        [SerializeField] public TyreCompound CurrentCompound = TyreCompound.Soft;

        [Header("Powertrain & Top Speed Specs")]
        [SerializeField] private float baseTopSpeedKmh = 345.0f;   // ~95.8 m/s standard race pace
        [SerializeField] private float boostTopSpeedKmh = 368.0f;  // ~102.2 m/s with Shift MGU-K boost + DRS
        [SerializeField] private float reverseTopSpeedKmh = 72.0f; // ~20.0 m/s reverse gear
        [SerializeField] private float baseAccelMps2 = 15.0f;      // 0-100 in 2.3s
        [SerializeField] private float boostAccelMps2 = 19.5f;     // 0-100 in 1.9s
        [SerializeField] private float brakeDecelMps2 = 64.0f;     // ~6.5G peak F1 carbon-disc braking deceleration
        [SerializeField] private float powerBrakeDecelMps2 = 92.0f; // ~9.4G active MGU-K back-thrust stopping power
        [SerializeField] private float reverseAccelMps2 = 10.0f;

        [Header("Live Driver Inputs & Telemetry")]
        [Range(-1.0f, 1.0f)] public float SteeringInput = 0.0f;
        [Range(0.0f, 1.0f)] public float ThrottleInput = 0.0f;
        [Range(0.0f, 1.0f)] public float BrakeInput = 0.0f;
        [Range(0.0f, 1.0f)] public float ReverseInput = 0.0f;
        public bool IsBoostActive = false;
        public bool IsPowerBrakeActive = false;
        public bool IsRegenerating { get; private set; } = false;
        public DrsState DrsToggle = DrsState.Closed;
        public VehicleGear CurrentGear { get; private set; } = VehicleGear.Gear1;

        [Header("Live Strategy & Telemetry (Feeds HUD)")]
        public float CurrentSpeedKmh => Mathf.Abs(currentForwardSpeedMps) * 3.6f;
        public float CurrentSpeedMps => Mathf.Abs(currentForwardSpeedMps);
        public float BatteryEnergyPct { get; private set; } = 100.0f;
        public float OptimalBatteryEnergyPct { get; private set; } = 100.0f;
        public float TyreWearPct { get; private set; } = 0.0f;
        public float OptimalTyreWearPct { get; private set; } = 0.0f;
        public float TyreWearFL { get; private set; } = 0.0f;
        public float TyreWearFR { get; private set; } = 0.0f;
        public float TyreWearRL { get; private set; } = 0.0f;
        public float TyreWearRR { get; private set; } = 0.0f;
        public float TotalRegenHarvestedMj { get; private set; } = 0.0f;

        public float CurrentGripFactor { get; private set; } = 1.0f;

        private Rigidbody rb;
        private float currentForwardSpeedMps = 0.0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            ResetVehicleState();
        }

        public void TeleportVehicle(Vector3 position, Quaternion rotation)
        {
            currentForwardSpeedMps = 0.0f;
            ThrottleInput = 0.0f;
            BrakeInput = 0.0f;
            ReverseInput = 0.0f;
            SteeringInput = 0.0f;

            transform.position = position;
            transform.rotation = rotation;

            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            ResetVehicleState();
        }

        public void ResetVehicleState()
        {
            BatteryEnergyPct = 100.0f;
            OptimalBatteryEnergyPct = 100.0f;
            TyreWearPct = 0.0f;
            OptimalTyreWearPct = 0.0f;
            TyreWearFL = 0.0f;
            TyreWearFR = 0.0f;
            TyreWearRL = 0.0f;
            TyreWearRR = 0.0f;
            TotalRegenHarvestedMj = 0.0f;
            currentForwardSpeedMps = 0.0f;
            CurrentGear = VehicleGear.Gear1;
            DrsToggle = DrsState.Closed;
            IsBoostActive = false;
            IsRegenerating = false;
            CurrentGripFactor = GetCompoundBaseGrip();
        }

        public void SetTyreCompound(TyreCompound compound)
        {
            CurrentCompound = compound;
            ResetVehicleState();
            Debug.Log($"[CarPhysicsController] Selected Tyre Compound: {CurrentCompound}");
        }

        private float GetCompoundBaseGrip()
        {
            switch (CurrentCompound)
            {
                case TyreCompound.Soft: return 1.00f;
                case TyreCompound.Medium: return 0.92f;
                case TyreCompound.Hard: return 0.84f;
                default: return 1.00f;
            }
        }

        private float GetCompoundWearMultiplier()
        {
            switch (CurrentCompound)
            {
                case TyreCompound.Soft: return 2.20f;
                case TyreCompound.Medium: return 1.25f;
                case TyreCompound.Hard: return 0.70f;
                default: return 1.00f;
            }
        }

        private void Update()
        {
            bool keyForward = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
            bool keyBackward = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
            bool keyLeft = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
            bool keyRight = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);
            bool keyHandbrake = UnityEngine.Input.GetKey(KeyCode.Space);
            bool keyBoost = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

            float axisV = UnityEngine.Input.GetAxis("Vertical");
            float axisH = UnityEngine.Input.GetAxis("Horizontal");

            float reqForward = 0.0f;
            float reqBackward = 0.0f;

            if (keyForward || axisV > 0.05f)
            {
                reqForward = keyForward ? 1.0f : Mathf.Clamp01(axisV);
            }
            if (keyBackward || axisV < -0.05f)
            {
                reqBackward = keyBackward ? 1.0f : Mathf.Clamp01(-axisV);
            }

            // Strict Battery Boost Check
            bool canBoost = keyBoost && (BatteryEnergyPct > 0.05f);
            IsBoostActive = canBoost;

            if (reqForward > 0.01f)
            {
                if (currentForwardSpeedMps < -0.3f)
                {
                    ThrottleInput = 0.0f;
                    BrakeInput = reqForward;
                    ReverseInput = 0.0f;
                }
                else
                {
                    ThrottleInput = reqForward;
                    BrakeInput = 0.0f;
                    ReverseInput = 0.0f;
                    UpdateGear(currentForwardSpeedMps);
                }
            }
            else if (reqBackward > 0.01f)
            {
                if (currentForwardSpeedMps > 0.3f)
                {
                    ThrottleInput = 0.0f;
                    BrakeInput = reqBackward;
                    ReverseInput = 0.0f;
                }
                else
                {
                    ThrottleInput = 0.0f;
                    BrakeInput = 0.0f;
                    ReverseInput = reqBackward;
                    CurrentGear = VehicleGear.Reverse;
                }
            }
            else
            {
                ThrottleInput = 0.0f;
                BrakeInput = 0.0f;
                ReverseInput = 0.0f;
                if (Mathf.Abs(currentForwardSpeedMps) < 0.2f) CurrentGear = VehicleGear.Neutral;
                else UpdateGear(currentForwardSpeedMps);
            }

            IsPowerBrakeActive = keyHandbrake;
            if (keyHandbrake)
            {
                BrakeInput = 1.0f;
                ThrottleInput = 0.0f;
                ReverseInput = 0.0f;
            }

            float steer = 0.0f;
            if (keyLeft) steer -= 1.0f;
            if (keyRight) steer += 1.0f;
            if (Mathf.Abs(axisH) > 0.05f && !keyLeft && !keyRight) steer = axisH;
            SteeringInput = Mathf.Clamp(steer, -1.0f, 1.0f);

            if (UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                DrsToggle = DrsToggle == DrsState.Open ? DrsState.Closed : DrsState.Open;
            }

            UpdateBatteryAndTyreTelemetry(Time.deltaTime);
        }

        private void UpdateBatteryAndTyreTelemetry(float dt)
        {
            if (dt <= 0.0f) return;

            float speedKmh = CurrentSpeedKmh;

            if (ThrottleInput > 0.01f)
            {
                float drainRate = IsBoostActive ? 4.8f : 1.1f;
                BatteryEnergyPct = Mathf.Max(0.0f, BatteryEnergyPct - (drainRate * ThrottleInput * dt));

                if (BatteryEnergyPct <= 0.01f)
                {
                    BatteryEnergyPct = 0.0f;
                    IsBoostActive = false;
                }
            }

            if (BrakeInput > 0.05f && currentForwardSpeedMps > 5.0f)
            {
                IsRegenerating = true;
                float regenPowerKw = Mathf.Min(160.0f, currentForwardSpeedMps * 3.4f * BrakeInput);
                float regenPctPerSec = (regenPowerKw / 1800.0f) * 100.0f;
                BatteryEnergyPct = Mathf.Min(100.0f, BatteryEnergyPct + (regenPctPerSec * dt));
                TotalRegenHarvestedMj += (regenPowerKw * dt) / 1000.0f;
            }
            else
            {
                IsRegenerating = false;
            }

            float optDrainRate = (speedKmh > 200.0f) ? 1.4f : 0.5f;
            if (BrakeInput > 0.05f && currentForwardSpeedMps > 5.0f)
            {
                OptimalBatteryEnergyPct = Mathf.Min(100.0f, OptimalBatteryEnergyPct + (3.8f * dt));
            }
            else if (speedKmh > 50.0f)
            {
                OptimalBatteryEnergyPct = Mathf.Max(5.0f, OptimalBatteryEnergyPct - (optDrainRate * dt));
            }

            if (speedKmh > 2.0f)
            {
                float compoundMult = GetCompoundWearMultiplier();

                float baseWearRate = 0.007f * (speedKmh / 100.0f) * compoundMult;
                float lateralWearRate = Mathf.Abs(SteeringInput) * 0.042f * (speedKmh / 80.0f) * compoundMult;
                float brakeWearRate = BrakeInput * 0.032f * (speedKmh / 100.0f) * compoundMult;

                float driverTotalWearRate = (baseWearRate + lateralWearRate + brakeWearRate) * dt;

                TyreWearFL = Mathf.Min(100.0f, TyreWearFL + driverTotalWearRate * (SteeringInput > 0 ? 1.4f : 1.0f));
                TyreWearFR = Mathf.Min(100.0f, TyreWearFR + driverTotalWearRate * (SteeringInput < 0 ? 1.4f : 1.0f));
                TyreWearRL = Mathf.Min(100.0f, TyreWearRL + driverTotalWearRate * (ThrottleInput > 0.5f ? 1.3f : 1.0f));
                TyreWearRR = Mathf.Min(100.0f, TyreWearRR + driverTotalWearRate * (ThrottleInput > 0.5f ? 1.3f : 1.0f));

                TyreWearPct = (TyreWearFL + TyreWearFR + TyreWearRL + TyreWearRR) * 0.25f;

                float optimalWearRate = (baseWearRate * 0.65f + lateralWearRate * 0.45f + brakeWearRate * 0.35f) * dt;
                OptimalTyreWearPct = Mathf.Min(100.0f, OptimalTyreWearPct + optimalWearRate);
            }

            float baseGrip = GetCompoundBaseGrip();
            float wearGripDrop = Mathf.Pow(TyreWearPct / 100.0f, 1.15f) * 0.45f;
            CurrentGripFactor = Mathf.Clamp(baseGrip - wearGripDrop, 0.45f, 1.05f);
        }

        private void UpdateGear(float forwardSpeedMps)
        {
            float speedKmh = forwardSpeedMps * 3.6f;
            if (speedKmh < 1.0f) CurrentGear = VehicleGear.Gear1;
            else if (speedKmh < 85.0f) CurrentGear = VehicleGear.Gear1;
            else if (speedKmh < 135.0f) CurrentGear = VehicleGear.Gear2;
            else if (speedKmh < 185.0f) CurrentGear = VehicleGear.Gear3;
            else if (speedKmh < 230.0f) CurrentGear = VehicleGear.Gear4;
            else if (speedKmh < 275.0f) CurrentGear = VehicleGear.Gear5;
            else if (speedKmh < 315.0f) CurrentGear = VehicleGear.Gear6;
            else if (speedKmh < 345.0f) CurrentGear = VehicleGear.Gear7;
            else CurrentGear = VehicleGear.Gear8;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (rb == null || dt <= 0.0f) return;

            if (rb.isKinematic) rb.isKinematic = false;

            // 1. Forward / Reverse Propulsion
            if (ThrottleInput > 0.01f)
            {
                float topSpeedWearPenaltyKmh = (TyreWearPct / 100.0f) * 28.0f;
                float effectiveBaseTopSpeed = Mathf.Max(290.0f, baseTopSpeedKmh - topSpeedWearPenaltyKmh);
                float effectiveBoostTopSpeed = Mathf.Max(310.0f, boostTopSpeedKmh - topSpeedWearPenaltyKmh);

                float targetTopSpeedMps = (IsBoostActive ? effectiveBoostTopSpeed : effectiveBaseTopSpeed) / 3.6f;
                if (DrsToggle == DrsState.Open) targetTopSpeedMps += 4.5f;

                float baseA = IsBoostActive ? boostAccelMps2 : baseAccelMps2;
                float accel = baseA * ThrottleInput * CurrentGripFactor;

                if (currentForwardSpeedMps > 83.3f)
                {
                    accel *= Mathf.Clamp01(1.0f - ((currentForwardSpeedMps - 83.3f) / 25.0f));
                    accel = Mathf.Max(accel, 2.0f);
                }

                currentForwardSpeedMps = Mathf.MoveTowards(currentForwardSpeedMps, targetTopSpeedMps, accel * dt);
            }
            else if (ReverseInput > 0.01f)
            {
                float targetReverseMps = -(reverseTopSpeedKmh / 3.6f);
                currentForwardSpeedMps = Mathf.MoveTowards(currentForwardSpeedMps, targetReverseMps, reverseAccelMps2 * ReverseInput * dt);
            }
            else if (BrakeInput > 0.01f)
            {
                float baseDecel = IsPowerBrakeActive ? powerBrakeDecelMps2 : brakeDecelMps2;
                float effectiveBrakeDecel = baseDecel * Mathf.Lerp(1.0f, 0.85f, TyreWearPct / 100.0f) * CurrentGripFactor;
                currentForwardSpeedMps = Mathf.MoveTowards(currentForwardSpeedMps, 0.0f, effectiveBrakeDecel * BrakeInput * dt);

                if (IsPowerBrakeActive && Mathf.Abs(currentForwardSpeedMps) > 0.2f)
                {
                    rb.AddForce(-transform.forward * (rb.mass * 12.0f * Mathf.Sign(currentForwardSpeedMps)), ForceMode.Force);
                }
            }
            else
            {
                float coastDecel = 2.5f + (currentForwardSpeedMps * currentForwardSpeedMps * 0.0008f);
                currentForwardSpeedMps = Mathf.MoveTowards(currentForwardSpeedMps, 0.0f, coastDecel * dt);
            }

            // 2. High-Performance F1 Steering Dynamics (Substantially Increased Turn Rate & Responsiveness)
            if (Mathf.Abs(SteeringInput) > 0.01f)
            {
                float absSpeed = Mathf.Abs(currentForwardSpeedMps);
                
                // Allow full steering authority at low-to-medium speeds (for hairpins like La Source & Bus Stop)
                float speedSteerScale = Mathf.Clamp(absSpeed / 6.0f, 0.65f, 1.0f);
                
                // Retain high aerodynamic downforce turning bite at high speeds
                float highSpeedStability = Mathf.Clamp(1.0f / (1.0f + (absSpeed / 75.0f)), 0.55f, 1.0f);

                float lateralGripFactor = Mathf.Lerp(1.0f, 0.55f, Mathf.Pow(TyreWearPct / 100.0f, 1.1f));

                // Increased base yaw rate from 55 deg/s to 125 deg/s for agile, effortless cornering
                float yawRateDegPerS = SteeringInput * 125.0f * speedSteerScale * highSpeedStability * lateralGripFactor;

                if (currentForwardSpeedMps < -0.3f) yawRateDegPerS = -yawRateDegPerS;

                Quaternion turnDelta = Quaternion.Euler(0.0f, yawRateDegPerS * dt, 0.0f);
                rb.MoveRotation(rb.rotation * turnDelta);
            }

            // 3. 3D Elevation Ground Tracking (Ignoring self colliders!)
            Vector3 currentPos = rb.position;
            Vector3 rayStart = currentPos + Vector3.up * 4.0f;
            Vector3 groundNormal = Vector3.up;

            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(rayStart, Vector3.down, 12.0f);
            RaycastHit bestHit = default;
            bool foundGround = false;
            float closestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(transform)) continue;
                if (hits[i].distance < closestDist)
                {
                    closestDist = hits[i].distance;
                    bestHit = hits[i];
                    foundGround = true;
                }
            }

            if (foundGround)
            {
                float targetY = bestHit.point.y + 0.564f;
                currentPos.y = targetY;
                groundNormal = bestHit.normal;
            }

            Vector3 moveDir = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 newPos = currentPos + moveDir * (currentForwardSpeedMps * dt);
            rb.MovePosition(newPos);
            rb.linearVelocity = moveDir * currentForwardSpeedMps;

            if (groundNormal != Vector3.up)
            {
                Vector3 projectedFwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
                if (projectedFwd.sqrMagnitude > 0.001f)
                {
                    Quaternion targetSlopeRot = Quaternion.LookRotation(projectedFwd, groundNormal);
                    rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetSlopeRot, dt * 15.0f));
                }
            }
        }
    }
}
