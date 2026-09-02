using System;
using System.Collections.Generic;
using UnityEngine;
using GridSense.UI;
using GridSense.Core;

namespace GridSense.Environment
{
    public enum CameraViewMode
    {
        Chase = 0,
        Cockpit = 1,
        NoseCone = 2,
        TVTrackside = 3
    }

    /// <summary>
    /// CameraFollow provides rock-solid, vibration-free multi-camera broadcasting:
    /// - Chase Camera: Butter-smooth tracking with dynamic speed-FOV scaling (60° to 74°)
    /// - Cockpit Camera: Authentic driver halo perspective with subtle vibration filtering
    /// - Nose Cone Camera: Low-slung front wing on-board perspective
    /// - TV Trackside Camera: Authentic F1 broadcast panning cameras stationed every 120m along the track
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target & Mode")]
        [SerializeField] private Transform target;
        [SerializeField] private CameraViewMode currentMode = CameraViewMode.Chase;

        [Header("Chase Cam Configuration")]
        [SerializeField] private Vector3 chaseOffset = new Vector3(0.0f, 1.95f, -5.4f);
        [SerializeField] private float chaseLookAheadDist = 18.0f;
        [SerializeField] private float chasePositionSmoothTime = 0.035f;
        [SerializeField] private float chaseRotationDamping = 18.0f;

        [Header("Cockpit & Nose Offsets")]
        [SerializeField] private Vector3 cockpitOffset = new Vector3(0.0f, 0.70f, 0.22f);
        [SerializeField] private Vector3 noseOffset = new Vector3(0.0f, 0.42f, 1.90f);

        [Header("TV Trackside System")]
        [SerializeField] private float tvTowerIntervalM = 120.0f;
        [SerializeField] private float tvTowerLateralX = -12.0f;
        [SerializeField] private float tvTowerHeightY = 4.2f;

        private Camera cam;
        private Rigidbody targetRb;
        private Vector3 currentPosVelocity;
        private Vector3 smoothedLookTarget;
        private Vector3 currentTvTowerPos;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        private void Start()
        {
            FindCarTarget();

            if (!Application.isEditor)
            {
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            }

            if (target != null)
            {
                UpdateTvTowerPosition(target.position.z);
            }
        }

        private void FindCarTarget()
        {
            if (target == null)
            {
                GameObject car = GameObject.Find("Ferrari_SF23");
                if (car != null)
                {
                    target = car.transform;
                    targetRb = car.GetComponent<Rigidbody>();
                }
            }
        }

        private void Update()
        {
            // 1. Camera View Switching (C or V key)
            if (UnityEngine.Input.GetKeyDown(KeyCode.C) || UnityEngine.Input.GetKeyDown(KeyCode.V))
            {
                currentMode = (CameraViewMode)(((int)currentMode + 1) % 4);
                currentPosVelocity = Vector3.zero;
                if (target != null) UpdateTvTowerPosition(target.position.z);
                Debug.Log($"[CameraFollow] Switched to view: {currentMode}");
            }

            // 2. Fullscreen Toggle (F11 or Alt+Enter)
            if (UnityEngine.Input.GetKeyDown(KeyCode.F11) || ((UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt)) && UnityEngine.Input.GetKeyDown(KeyCode.Return)))
            {
                Screen.fullScreen = !Screen.fullScreen;
            }

            // 3. Car Reset (R key)
            if (UnityEngine.Input.GetKeyDown(KeyCode.R) && target != null)
            {
                Vector3 resetPos = new Vector3(3.5f, 0.564f, 140.0f);
                Quaternion resetRot = Quaternion.identity;

                if (F1TelemetryHUD.Instance != null && F1TelemetryHUD.Instance.CachedCenterline != null && F1TelemetryHUD.Instance.CachedCenterline.Count > 22)
                {
                    int sIdx = (F1TelemetryHUD.Instance.SelectedCircuit == CircuitType.Monza) ? 0 : 20;
                    Vector3 p = F1TelemetryHUD.Instance.CachedCenterline[sIdx];
                    Vector3 next = F1TelemetryHUD.Instance.CachedCenterline[sIdx + 1];
                    Vector3 fwd = (next - p).normalized;
                    resetPos = p + Vector3.up * 0.564f;
                    resetRot = Quaternion.LookRotation(fwd, Vector3.up);
                }

                target.position = resetPos;
                target.rotation = resetRot;
                if (targetRb != null)
                {
                    targetRb.linearVelocity = Vector3.zero;
                    targetRb.angularVelocity = Vector3.zero;
                }
                UpdateTvTowerPosition(resetPos.z);
                currentPosVelocity = Vector3.zero;
                smoothedLookTarget = target.position + target.forward * chaseLookAheadDist;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                FindCarTarget();
                if (target == null) return;
            }

            float speedMps = targetRb != null ? targetRb.linearVelocity.magnitude : 0.0f;
            float speedKmh = speedMps * 3.6f;

            switch (currentMode)
            {
                case CameraViewMode.Chase:
                    UpdateRockSolidChaseCam(speedKmh);
                    break;
                case CameraViewMode.Cockpit:
                    UpdateCockpitCam();
                    break;
                case CameraViewMode.NoseCone:
                    UpdateNoseConeCam();
                    break;
                case CameraViewMode.TVTrackside:
                    UpdateBroadcastTvCam();
                    break;
            }
        }

        private float currentCamYaw = 0.0f;
        private bool isCamYawInit = false;

        /// <summary>
        /// Rock-solid, vibration-free chase camera with smooth yaw damping and zero longitudinal throbbing.
        /// </summary>
        private void UpdateRockSolidChaseCam(float speedKmh)
        {
            if (cam != null)
            {
                // Smooth FOV speed sensation (60 deg at 0 km/h -> 72 deg at 360 km/h)
                float targetFov = Mathf.Lerp(60.0f, 72.0f, Mathf.Clamp01(speedKmh / 350.0f));
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 4.0f);
            }

            if (!isCamYawInit)
            {
                currentCamYaw = target.eulerAngles.y;
                isCamYawInit = true;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.0f) return;

            // 1. Smoothly follow target yaw without longitudinal spring bounce
            float targetYaw = target.eulerAngles.y;
            currentCamYaw = Mathf.LerpAngle(currentCamYaw, targetYaw, dt * 14.0f);

            // 2. Blend pitch with car elevation slope
            float targetPitch = Mathf.Clamp(target.eulerAngles.x, -25.0f, 25.0f);
            Quaternion camRot = Quaternion.Euler(targetPitch * 0.35f, currentCamYaw, 0.0f);

            // 3. Rigidly position camera at fixed offset behind the smoothed rotation
            Vector3 carCenter = target.position + Vector3.up * 0.40f;
            Vector3 targetCamPos = carCenter + (camRot * chaseOffset);

            // Smooth position tightly to eliminate physics timestep micro-jitter
            transform.position = Vector3.Lerp(transform.position, targetCamPos, dt * 35.0f);

            // 4. Look steady at front chassis focal point
            Vector3 lookTarget = carCenter + (target.forward * 4.5f) + (Vector3.up * 0.35f);
            Vector3 toLook = (lookTarget - transform.position).normalized;
            if (toLook.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(toLook, Vector3.up);
            }
        }

        /// <summary>
        /// Cockpit Camera locked directly to the halo with zero jitter.
        /// </summary>
        private void UpdateCockpitCam()
        {
            if (cam != null) cam.fieldOfView = 75.0f;
            transform.position = target.position + (target.rotation * cockpitOffset);
            transform.rotation = target.rotation * Quaternion.Euler(2.0f, 0.0f, 0.0f);
        }

        /// <summary>
        /// Nose Cone Camera locked to front wing assembly.
        /// </summary>
        private void UpdateNoseConeCam()
        {
            if (cam != null) cam.fieldOfView = 82.0f;
            transform.position = target.position + (target.rotation * noseOffset);
            transform.rotation = target.rotation;
        }

        /// <summary>
        /// Authentic TV Broadcast Trackside Camera:
        /// Static tower cameras placed every 120m trackside that pan smoothly to track the speeding car.
        /// Zero position vibration; 100% authentic broadcast TV feel.
        /// </summary>
        private void UpdateBroadcastTvCam()
        {
            float carZ = target.position.z;

            // Automatically switch to next/prev TV tower as car passes
            float towerIndex = Mathf.Floor((carZ + (tvTowerIntervalM * 0.5f)) / tvTowerIntervalM);
            float towerZ = towerIndex * tvTowerIntervalM + (tvTowerIntervalM * 0.35f);

            // If car moved past current tower zone, jump smoothly to next tower
            Vector3 targetTowerPos = new Vector3(tvTowerLateralX, tvTowerHeightY, towerZ);
            if (Vector3.Distance(currentTvTowerPos, targetTowerPos) > 1.0f)
            {
                currentTvTowerPos = targetTowerPos;
            }

            transform.position = currentTvTowerPos;

            // Smoothly look at the approaching/departing car with dynamic zoom
            Vector3 targetFocus = target.position + (Vector3.up * 0.5f);
            Vector3 toTarget = targetFocus - transform.position;
            float dist = toTarget.magnitude;

            // Dynamic broadcast zoom (telephoto lens when far, wide when near)
            if (cam != null)
            {
                float targetFov = Mathf.Clamp(Mathf.Lerp(16.0f, 48.0f, dist / 100.0f), 14.0f, 50.0f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 6.0f);
            }

            if (toTarget.sqrMagnitude > 0.01f)
            {
                Quaternion lookRot = Quaternion.LookRotation(toTarget, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * 24.0f);
            }
        }

        private void UpdateTvTowerPosition(float carZ)
        {
            float towerIndex = Mathf.Floor((carZ + (tvTowerIntervalM * 0.5f)) / tvTowerIntervalM);
            float towerZ = towerIndex * tvTowerIntervalM + (tvTowerIntervalM * 0.35f);
            currentTvTowerPos = new Vector3(tvTowerLateralX, tvTowerHeightY, towerZ);
            transform.position = currentTvTowerPos;
        }
    }
}
