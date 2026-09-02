using System;
using UnityEngine;
using GridSense.Core;

namespace GridSense.Physics
{
    public enum WheelPosition
    {
        FrontLeft = 0,
        FrontRight = 1,
        RearLeft = 2,
        RearRight = 3
    }

    [Serializable]
    public class WheelCorner
    {
        public WheelPosition Position;
        public WheelCollider Collider;
        public Transform VisualTransform;

        [Header("Tyre & Brake Systems")]
        public PacejkaTyreModel TyreModel;
        public TyreThermalModel TyreThermal;
        public BrakeThermalModel BrakeThermal;

        [Header("Live Corner State")]
        public float NormalLoadN = 4000.0f;
        public float SuspensionCompressionM = 0.0f;
        public float WheelRpm = 0.0f;
        public float SlipRatio = 0.0f;
        public float SlipAngleRad = 0.0f;
        public bool IsGrounded = true;
        public bool IsLockedUp = false;

        public WheelCorner(WheelPosition pos, TyreCompound compound)
        {
            Position = pos;
            TyreModel = new PacejkaTyreModel(compound);
            TyreThermal = new TyreThermalModel(TyreModel.Profile);
            BrakeThermal = new BrakeThermalModel();
        }
    }

    /// <summary>
    /// VehicleDynamics manages rigid-body vehicle physics:
    /// 1. Dynamic load transfer (longitudinal under accel/braking, lateral under cornering).
    /// 2. Per-corner spring, damper, and anti-roll bar (ARB) dynamics.
    /// 3. Ride height computation (front and rear) for aerodynamic downforce integration.
    /// 4. Coordination of 4 Pacejka tyre contact patches and brake assemblies.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleDynamics : MonoBehaviour
    {
        [Header("Rigid Body & Mass Distribution")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private float vehicleMassKg = 798.0f; // 2023 FIA minimum weight without fuel
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0.0f, -0.15f, 0.05f); // Low CG (~0.30m above ground)
        [SerializeField] private float frontWeightDistribution = 0.455f; // 45.5% front / 54.5% rear

        [Header("Chassis Dimensions (Meters)")]
        [SerializeField] private float wheelbaseM = 3.60f;
        [SerializeField] private float frontTrackWidthM = 1.62f;
        [SerializeField] private float rearTrackWidthM = 1.56f;
        [SerializeField] private float cgHeightM = 0.31f;

        [Header("Suspension & Anti-Roll Bars")]
        [SerializeField] private float frontSpringRateNPerM = 120000.0f; // 120 N/mm
        [SerializeField] private float rearSpringRateNPerM = 140000.0f;  // 140 N/mm
        [SerializeField] private float damperRateNPerMps = 11000.0f;
        [SerializeField] private float frontArbStiffness = 35000.0f;      // Anti-roll bar front
        [SerializeField] private float rearArbStiffness = 22000.0f;       // Anti-roll bar rear

        [Header("Ride Heights (Output to Aero Model)")]
        [SerializeField] private float staticFrontRideHeightMm = 30.0f;
        [SerializeField] private float staticRearRideHeightMm = 45.0f;
        public float DynamicFrontRideHeightMm { get; private set; }
        public float DynamicRearRideHeightMm { get; private set; }

        [Header("4 Corner Wheel Assemblies")]
        [SerializeField] private WheelCorner[] wheels = new WheelCorner[4];

        [Header("Live Dynamic Telemetry")]
        public Vector3 LocalAccelerationMps2 { get; private set; }
        public float SpeedKmh => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0.0f;
        public float SpeedMps => rb != null ? rb.linearVelocity.magnitude : 0.0f;
        public float TotalNormalLoadN { get; private set; }

        private Vector3 previousVelocity;

        public Rigidbody ChassisRigidbody => rb;
        public WheelCorner[] Wheels => wheels;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.mass = vehicleMassKg;
                rb.centerOfMass = centerOfMassOffset;
            }

            // Initialize wheel corners if empty
            if (wheels[0] == null || wheels[0].TyreModel == null)
            {
                wheels[0] = new WheelCorner(WheelPosition.FrontLeft, TyreCompound.Medium);
                wheels[1] = new WheelCorner(WheelPosition.FrontRight, TyreCompound.Medium);
                wheels[2] = new WheelCorner(WheelPosition.RearLeft, TyreCompound.Medium);
                wheels[3] = new WheelCorner(WheelPosition.RearRight, TyreCompound.Medium);
            }
        }

        public void SetCompound(TyreCompound compound)
        {
            for (int i = 0; i < 4; i++)
            {
                wheels[i].TyreModel.SetCompound(compound);
                wheels[i].TyreThermal.ApplyProfile(wheels[i].TyreModel.Profile);
            }
        }

        public void UpdateMass(float fuelMassKg)
        {
            float totalMass = vehicleMassKg + fuelMassKg;
            if (rb != null) rb.mass = totalMass;
        }

        /// <summary>
        /// Computes dynamic weight transfer and distributes vertical normal loads across all 4 wheels.
        /// </summary>
        public void ComputeDynamicLoadTransfer(float aeroDownforceFrontN, float aeroDownforceRearN, float dt)
        {
            if (dt <= 0.0f || rb == null) return;

            // 1. Calculate local acceleration (G-forces)
            Vector3 currentVelocity = rb.linearVelocity;
            Vector3 worldAcc = (currentVelocity - previousVelocity) / dt;
            previousVelocity = currentVelocity;
            LocalAccelerationMps2 = transform.InverseTransformDirection(worldAcc);

            float ax = LocalAccelerationMps2.z; // Longitudinal acceleration (+accel, -braking)
            float ay = LocalAccelerationMps2.x; // Lateral acceleration (+right, -left)

            float totalMass = rb.mass;
            float gravity = 9.81f;
            float totalGravityN = totalMass * gravity;

            // 2. Static load distribution
            float staticFrontLoadTotalN = (totalGravityN * frontWeightDistribution) + aeroDownforceFrontN;
            float staticRearLoadTotalN = (totalGravityN * (1.0f - frontWeightDistribution)) + aeroDownforceRearN;

            // 3. Longitudinal Load Transfer: delta_Fz_long = (m * ax * h_cg) / L
            float deltaLoadLongN = (totalMass * ax * cgHeightM) / wheelbaseM;

            float dynamicFrontAxleN = Mathf.Max(200.0f, staticFrontLoadTotalN - deltaLoadLongN);
            float dynamicRearAxleN = Mathf.Max(200.0f, staticRearLoadTotalN + deltaLoadLongN);

            // 4. Lateral Load Transfer: delta_Fz_lat = (m * ay * h_cg) / track_width
            float frontArbRatio = frontArbStiffness / (frontArbStiffness + rearArbStiffness);
            float deltaLoadLatFrontN = (totalMass * ay * cgHeightM * frontArbRatio) / frontTrackWidthM;
            float deltaLoadLatRearN = (totalMass * ay * cgHeightM * (1.0f - frontArbRatio)) / rearTrackWidthM;

            // 5. Assign per-wheel normal load (Fz)
            // Front Left & Front Right
            wheels[(int)WheelPosition.FrontLeft].NormalLoadN = Mathf.Max(100.0f, (dynamicFrontAxleN * 0.5f) - deltaLoadLatFrontN);
            wheels[(int)WheelPosition.FrontRight].NormalLoadN = Mathf.Max(100.0f, (dynamicFrontAxleN * 0.5f) + deltaLoadLatFrontN);

            // Rear Left & Rear Right
            wheels[(int)WheelPosition.RearLeft].NormalLoadN = Mathf.Max(100.0f, (dynamicRearAxleN * 0.5f) - deltaLoadLatRearN);
            wheels[(int)WheelPosition.RearRight].NormalLoadN = Mathf.Max(100.0f, (dynamicRearAxleN * 0.5f) + deltaLoadLatRearN);

            TotalNormalLoadN = wheels[0].NormalLoadN + wheels[1].NormalLoadN + wheels[2].NormalLoadN + wheels[3].NormalLoadN;

            // 6. Dynamic Ride Height Calculation (affected by downforce & pitch compression)
            float frontCompressionM = (dynamicFrontAxleN - (totalGravityN * frontWeightDistribution)) / (frontSpringRateNPerM * 2.0f);
            float rearCompressionM = (dynamicRearAxleN - (totalGravityN * (1.0f - frontWeightDistribution))) / (rearSpringRateNPerM * 2.0f);

            DynamicFrontRideHeightMm = Mathf.Clamp(staticFrontRideHeightMm - (frontCompressionM * 1000.0f), 5.0f, 65.0f);
            DynamicRearRideHeightMm = Mathf.Clamp(staticRearRideHeightMm - (rearCompressionM * 1000.0f), 10.0f, 85.0f);
        }
    }
}
