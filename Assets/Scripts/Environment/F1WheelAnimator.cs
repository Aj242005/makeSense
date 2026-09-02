using System;
using UnityEngine;
using GridSense.Physics;

namespace GridSense.Environment
{
    /// <summary>
    /// F1WheelAnimator animates realistic Formula 1 front wheel steering angles,
    /// 4-wheel rotation/spin proportional to velocity, and cockpit steering wheel rotation.
    /// </summary>
    public class F1WheelAnimator : MonoBehaviour
    {
        [Header("Car Controller Reference")]
        [SerializeField] private CarPhysicsController car;

        [Header("Wheel Transform Pivots")]
        public Transform wheelFL;
        public Transform wheelFR;
        public Transform wheelRL;
        public Transform wheelRR;
        public Transform steeringWheel;

        [Header("Steering & Spin Settings")]
        [SerializeField] private float maxSteerAngleDeg = 38.0f;
        [SerializeField] private float maxSteeringWheelAngleDeg = 160.0f;
        [SerializeField] private float wheelRadiusM = 0.334f; // 668mm F1 18-inch Pirelli tyre

        private float currentSteerAngle = 0.0f;
        private float currentWheelSpinDeg = 0.0f;

        private void Start()
        {
            if (car == null)
            {
                car = GetComponentInParent<CarPhysicsController>();
            }
        }

        private void Update()
        {
            if (car == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0f) return;

            // 1. Smooth Front Wheel Steer Angle
            float targetSteerAngle = car.SteeringInput * maxSteerAngleDeg;
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, dt * 18.0f);

            // 2. Wheel Spin Rotation around transverse axle (X-axis)
            float speedMps = car.CurrentSpeedMps;
            float rollDeltaDeg = (speedMps / wheelRadiusM) * Mathf.Rad2Deg * dt;
            if (car.CurrentGear == VehicleGear.Reverse) rollDeltaDeg = -rollDeltaDeg;
            currentWheelSpinDeg = (currentWheelSpinDeg + rollDeltaDeg) % 360.0f;

            // 3. Composite Rotations: Steer (Y) * Roll (X)
            Quaternion steerRot = Quaternion.Euler(0.0f, currentSteerAngle, 0.0f);
            Quaternion rollRot = Quaternion.Euler(currentWheelSpinDeg, 0.0f, 0.0f);

            // Front Left: Steers & Rolls
            if (wheelFL != null)
            {
                wheelFL.localRotation = steerRot * rollRot;
            }

            // Front Right: Steers & Rolls
            if (wheelFR != null)
            {
                wheelFR.localRotation = steerRot * rollRot;
            }

            // Rear Left: Rolls
            if (wheelRL != null)
            {
                wheelRL.localRotation = rollRot;
            }

            // Rear Right: Rolls
            if (wheelRR != null)
            {
                wheelRR.localRotation = rollRot;
            }

            // Cockpit Steering Wheel: Turns around Z
            if (steeringWheel != null)
            {
                float targetCockpitSteer = -car.SteeringInput * maxSteeringWheelAngleDeg;
                steeringWheel.localRotation = Quaternion.Euler(15.0f, 0.0f, targetCockpitSteer);
            }
        }
    }
}
