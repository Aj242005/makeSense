using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GridSense.Track;
using GridSense.Environment;
using GridSense.Physics;

namespace GridSense.EditorTools
{
    public static class GridAlignmentAudit
    {
        // After re-export with Y-97.48m offset, the Bahrain pit-straight asphalt
        // now sits at Y=0 in Unity world space.
        private const float TRACK_ASPHALT_Y  = 0.0f;
        // Pole Position (P1 grid box) on Bahrain main straight
        // Asphalt spans X=[356.0, 368.0], Centre at X=362.0; pit wall at X=370.0; grandstand at X=354.0
        // P1 placed at X=363.0m on the racing line, clear of barriers
        private const float P1_X = 3.50f;
        private const float P1_Z = -12.00f;
        // Car local origin is 0.564m above tyre contact patch (measured from VehicleVisualBuilder)
        private const float CAR_ORIGIN_ABOVE_GROUND = 0.564f;

        [MenuItem("GridSense/Audit/Align Car to Start Grid & Capture Evidence")]
        public static void AlignCarToStartGridAndCapture()
        {
            Debug.Log("===============================================================================");
            Debug.Log(">>> [GridSense Start Grid Audit] REBUILDING SCENE WITH BROADCAST TV FRAMING <<<");
            Debug.Log("===============================================================================");

            // 1. Rebuild scene with complete multi-submesh circuit hierarchy & accurate colliders
            SceneSetupEditor.CreateAndSetupMainScene();

            string scenePath = "Assets/Scenes/MainSimulation.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 2. Update Bahrain_TrackData start position with proven coordinates
            TrackData bahrainData = AssetDatabase.LoadAssetAtPath<TrackData>("Assets/Environments/Bahrain/Bahrain_TrackData.asset");
            Vector3 trackSurfacePos = new Vector3(P1_X, TRACK_ASPHALT_Y, P1_Z);
            Vector3 startHeading = Vector3.forward;  // +Z = racing direction down the pit straight towards Turn 1

            if (bahrainData != null)
            {
                bahrainData.StartFinishLinePosition = trackSurfacePos;
                bahrainData.StartFinishLineDirection = startHeading;
                EditorUtility.SetDirty(bahrainData);
                AssetDatabase.SaveAssets();
                Debug.Log($"[GridAlignmentAudit] TrackData: StartFinish = {trackSurfacePos}, Heading = {startHeading}");
            }

            // 3. Find Car & lock it precisely on the grid surface
            //    SpawnY = TRACK_ASPHALT_Y + CAR_ORIGIN_ABOVE_GROUND = 0.564m
            GameObject carGo = GameObject.Find("Ferrari_SF23");
            if (carGo == null)
            {
                Debug.LogError("[GridAlignmentAudit] Cannot find Ferrari_SF23 in scene!");
                return;
            }

            Vector3 carSpawnPos = new Vector3(P1_X, TRACK_ASPHALT_Y + CAR_ORIGIN_ABOVE_GROUND, P1_Z);
            carGo.transform.position = carSpawnPos;
            carGo.transform.rotation = Quaternion.identity;  // Local +Z (Front Nose) aligns with World +Z (towards Turn 1)

            Rigidbody rb = carGo.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity  = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic     = false;
            }

            UnityEngine.Physics.SyncTransforms();
            Debug.Log($"[GridAlignmentAudit] Car placed: pos={carGo.transform.position}, rot={carGo.transform.eulerAngles}");
            Debug.Log($"[GridAlignmentAudit] Track surface Y={TRACK_ASPHALT_Y}m, Car origin Y={carSpawnPos.y}m, offset={CAR_ORIGIN_ABOVE_GROUND}m");

            // 4. Save scene
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[GridAlignmentAudit] Scene saved: {scenePath}");

            // 5. Capture 4 broadcast evidence screenshots
            string outDir = @"C:\Users\AKSHIT JAIN\.gemini\antigravity-ide\brain\1dbeb2d6-7359-4b59-9ce0-394edf155b08";
            Directory.CreateDirectory(outDir);

            // A. Wide Establishing Shot — elevated left-trackside view framing car facing down the straight toward Turn 1
            CaptureAbsoluteView(outDir, "start_grid_wide_establishing_shot.png",
                new Vector3(P1_X - 11f, 4.2f, P1_Z - 11f),
                Quaternion.Euler(14f, 42f, 0f), 55f);

            // B. Grid Showcase: 3/4 front-left beauty angle looking back at front nose, wing, and livery
            CaptureAbsoluteView(outDir, "start_grid_position_showcase.png",
                new Vector3(P1_X - 4.5f, 1.3f, P1_Z + 5.5f),
                Quaternion.Euler(8f, 142f, 0f), 46f);

            // C. Chase Cam: low behind rear wing, looking forward past the halo and nose down the straight towards Turn 1
            CaptureAbsoluteView(outDir, "start_grid_chase_cam.png",
                new Vector3(P1_X, 1.6f, P1_Z - 5.5f),
                Quaternion.Euler(5f, 0f, 0f), 60f);

            // D. Cockpit POV: halo height, looking forward past nose cone down the straight toward Turn 1
            CaptureAbsoluteView(outDir, "start_grid_cockpit_view.png",
                new Vector3(P1_X, 1.25f, P1_Z + 0.3f),
                Quaternion.Euler(2f, 0f, 0f), 75f);

            Debug.Log("===============================================================================");
            Debug.Log(">>> [GridSense Start Grid Audit] BROADCAST EVIDENCE CAPTURED SUCCESSFULLY <<<");
            Debug.Log("===============================================================================");
        }

        /// <summary>
        /// Captures a screenshot using an absolute world-space camera position (not relative to car).
        /// </summary>
        private static void CaptureAbsoluteView(string outputDir, string fileName,
            Vector3 camWorldPos, Quaternion camWorldRot, float fov)
        {
            GameObject camObj = new GameObject("GridAuditCam");
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView   = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane  = 4000f;
            cam.clearFlags    = CameraClearFlags.Skybox;

            camObj.transform.position = camWorldPos;
            camObj.transform.rotation = camWorldRot;

            // Fill light attached to camera to illuminate the subject
            GameObject fillObj = new GameObject("FillLight");
            fillObj.transform.SetParent(camObj.transform, false);
            Light fill = fillObj.AddComponent<Light>();
            fill.type      = LightType.Directional;
            fill.intensity = 0.9f;
            fill.color     = new Color(1.0f, 0.97f, 0.93f);

            int W = 1920, H = 1080;
            RenderTexture rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            Texture2D shot = new Texture2D(W, H, TextureFormat.RGB24, false);

            cam.Render();
            RenderTexture.active = rt;
            shot.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            shot.Apply();

            cam.targetTexture  = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(fillObj);
            UnityEngine.Object.DestroyImmediate(camObj);

            byte[] bytes = shot.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(shot);

            string path = Path.Combine(outputDir, fileName);
            File.WriteAllBytes(path, bytes);
            Debug.Log($"[GridAlignmentAudit] Saved: {path} ({bytes.Length / 1024} KB)");
        }
    }
}
