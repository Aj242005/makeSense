using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GridSense.Track;
using GridSense.Environment;
using GridSense.Physics;
using GridSense.UI;

namespace GridSense.EditorTools
{
    public static class SceneSetupEditor
    {
        [MenuItem("GridSense/Scenes/Rebuild Main Simulation Scene")]
        public static void CreateAndSetupMainScene()
        {
            Debug.Log("===============================================================================");
            Debug.Log(">>> [GridSense Master Scene Setup] REBUILDING MAIN SIMULATION SCENE <<<");
            Debug.Log("===============================================================================");

            // 1. Create a fresh scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Setup Lighting & Atmosphere (Sakhir Circuit Daylight)
            GameObject lightingGo = new GameObject("Environment_Lighting");
            Light sunLight = lightingGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1.0f, 0.96f, 0.90f);
            sunLight.intensity = 1.25f;
            sunLight.shadows = LightShadows.Soft;
            lightingGo.transform.rotation = Quaternion.Euler(45.0f, -30.0f, 0.0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.70f, 0.84f, 0.98f);
            RenderSettings.ambientEquatorColor = new Color(0.75f, 0.70f, 0.65f);
            RenderSettings.ambientGroundColor = new Color(0.38f, 0.32f, 0.28f);
            RenderSettings.ambientIntensity = 1.05f;

            // 3. Setup Textures
            TextureGenerator.GenerateAllPBRTextures();

            // 4. Setup Environment Manager & Track Hierarchy
            GameObject envGo = new GameObject("EnvironmentManager");
            EnvironmentManager envMgr = envGo.AddComponent<EnvironmentManager>();
            RenderingOptimizer rendOpt = envGo.AddComponent<RenderingOptimizer>();
            TrackColliderSetup colliderSetup = envGo.AddComponent<TrackColliderSetup>();
            TrackManager trackMgr = envGo.AddComponent<TrackManager>();
            envGo.AddComponent<StandaloneRuntimeCapture>();

            TrackVisualBuilder.BuildCircuitVisuals(envGo.transform, CircuitType.Bahrain);
            colliderSetup.EnsureTrackColliders();

            // 5. Vehicle Chassis & Physics Controller positioned on Pole Position of Bahrain Main Straight
            // After OBJ re-export with -97.48m Y-offset, asphalt is at Y=0.
            // Car origin 0.564m above ground => carY = 0.564
            Vector3 startPos = new Vector3(3.0f, 0.564f, 140.00f);
            Vector3 startDir = Vector3.forward;

            GameObject carGo = new GameObject("Ferrari_SF23");
            carGo.transform.position = startPos;
            carGo.transform.rotation = Quaternion.LookRotation(startDir, Vector3.up);
            Rigidbody rb = carGo.AddComponent<Rigidbody>();
            rb.mass = 798.0f;
            rb.linearDamping = 0.15f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic = false;

            // BoxCollider elevated above wheel contact patch
            BoxCollider carCol = carGo.AddComponent<BoxCollider>();
            carCol.size = new Vector3(2.04f, 0.70f, 5.68f);
            carCol.center = new Vector3(0.0f, 0.45f, -0.476f);

            VehicleDynamics dynamics = carGo.AddComponent<VehicleDynamics>();
            CarPhysicsController carPhysics = carGo.AddComponent<CarPhysicsController>();
            AudioSource audioSrc = carGo.AddComponent<AudioSource>();
            carGo.AddComponent<GridSense.Audio.F1EngineSoundSystem>();
            VehicleVisualBuilder.BuildFerrariSF23Visuals(carGo.transform);

            // 6. Cameras: Chase Camera & Cockpit Camera
            GameObject chaseCamGo = new GameObject("ChaseCamera");
            Camera chaseCam = chaseCamGo.AddComponent<Camera>();
            chaseCam.tag = "MainCamera";
            chaseCam.fieldOfView = 60.0f;
            chaseCam.nearClipPlane = 0.1f;
            chaseCam.farClipPlane = 2500.0f;
            chaseCamGo.AddComponent<CameraFollow>();
            chaseCamGo.AddComponent<AudioListener>();
            chaseCamGo.transform.position = startPos - startDir * 5.2f + Vector3.up * 1.9f;
            chaseCamGo.transform.rotation = Quaternion.LookRotation(startDir + Vector3.down * 0.12f, Vector3.up);

            GameObject cockpitCamGo = new GameObject("CockpitCamera");
            cockpitCamGo.transform.SetParent(carGo.transform, false);
            cockpitCamGo.transform.localPosition = new Vector3(0.0f, 0.72f, 0.25f);
            cockpitCamGo.transform.localRotation = Quaternion.Euler(2.0f, 0.0f, 0.0f);
            Camera cockpitCam = cockpitCamGo.AddComponent<Camera>();
            cockpitCam.fieldOfView = 75.0f;
            cockpitCam.nearClipPlane = 0.05f;
            cockpitCam.farClipPlane = 2500.0f;
            cockpitCam.enabled = false;

            // 7. F1 Live Broadcast Telemetry HUD & 2D Minimap
            GameObject hudGo = new GameObject("F1_Telemetry_HUD");
            hudGo.AddComponent<F1TelemetryHUD>();

            // 8. Save Scene
            string sceneDir = "Assets/Scenes";
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }
            string scenePath = $"{sceneDir}/MainSimulation.unity";
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[SceneSetupEditor] MainSimulation scene saved successfully at: {scenePath} (Saved: {saved})");

            // Register in build settings
            EditorBuildSettingsScene[] originalScenes = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] newScenes = new EditorBuildSettingsScene[1];
            newScenes[0] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
            Debug.Log($"[SceneSetupEditor] Registered {scenePath} in EditorBuildSettings.");
        }
    }
}
