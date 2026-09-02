using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using GridSense.Environment;
using GridSense.Track;

namespace GridSense.EditorTools
{
    public static class Section6RenderingAudit
    {
        public static void RunSection6AuditAndCaptureEvidence()
        {
            Debug.Log("===============================================================================");
            Debug.Log(">>> [GridSense Section 6] RUNNING RENDERING PIPELINE & GRAPHICS AUDIT <<<");
            Debug.Log("===============================================================================");

            // Step 1: Ensure URP Pipeline Asset is configured and active
            URPSetupHelper.EnsureURPConfigured();

            // Step 2: Ensure Scene is created and loaded with all visual assets
            SceneSetupEditor.CreateAndSetupMainScene();
            string scenePath = "Assets/Scenes/MainSimulation.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Step 2: Apply Texture Caps and GPU Instancing
            int texturesOptimized = RenderingOptimizationEditor.OptimizeAllTextures(2048);
            int materialsOptimized = RenderingOptimizationEditor.OptimizeAllMaterials();
            Debug.Log($"[Section 6 - Asset Audit] Textures checked/capped at 2K: {texturesOptimized} modified. Materials with GPU Instancing enabled: {materialsOptimized} modified.");

            // Step 3: Apply Integrated GPU Profile
            QualitySettings.vSyncCount = 1;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.Medium;
            QualitySettings.shadowDistance = 60.0f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.particleRaycastBudget = 256;
            Application.targetFrameRate = 60;

            Debug.Log($"[Section 6 - Quality Profile] Target FPS: {Application.targetFrameRate}, VSync: {QualitySettings.vSyncCount}, Shadow Distance: {QualitySettings.shadowDistance}m, Cascades: {QualitySettings.shadowCascades}, Resolution: {QualitySettings.shadowResolution}");

            // Step 4: Audit Scene Hierarchy
            Renderer[] allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int totalRenderers = allRenderers.Length;
            int totalMaterials = 0;
            int instancedMaterials = 0;

            foreach (Renderer rend in allRenderers)
            {
                if (rend == null) continue;
                Material[] shared = rend.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (shared[i] == null) continue;
                    totalMaterials++;
                    if (shared[i].enableInstancing) instancedMaterials++;
                }
            }

            long totalAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            float vramEstimateMB = totalAllocatedBytes / (1024.0f * 1024.0f);
            bool withinVramBudget = vramEstimateMB <= 256.0f;

            Debug.Log($"[Section 6 - Geometry & Instancing] Total Scene Renderers: {totalRenderers}, Total Material References: {totalMaterials}, Instanced Material References: {instancedMaterials}");
            Debug.Log($"[Section 6 - Memory Footprint] Estimated Memory Allocation: {vramEstimateMB:F1} MB / 256.0 MB Budget (Within Budget: {withinVramBudget})");

            // Step 5: Render in-engine camera views & save PNG evidence
            string artifactsDir = @"C:\Users\AKSHIT JAIN\.gemini\antigravity-ide\brain\1dbeb2d6-7359-4b59-9ce0-394edf155b08";
            if (!Directory.Exists(artifactsDir))
            {
                Directory.CreateDirectory(artifactsDir);
            }

            CaptureRenderView(artifactsDir, "section6_car_closeup_showcase.png", new Vector3(2.2f, 0.75f, 2.8f), Quaternion.Euler(10f, -140f, 0f), 36f);
            CaptureRenderView(artifactsDir, "section6_chase_cam.png", new Vector3(0f, 1.85f, -4.8f), Quaternion.Euler(12f, 0f, 0f), 62f);
            CaptureRenderView(artifactsDir, "section6_trackside_apex.png", new Vector3(5.2f, 0.85f, 7.5f), Quaternion.Euler(4f, -150f, 0f), 38f);
            CaptureRenderView(artifactsDir, "section6_cockpit_view.png", new Vector3(0f, 0.72f, 0.25f), Quaternion.Euler(2f, 0f, 0f), 75f);
            CaptureRenderView(artifactsDir, "real_circuit_corner_showcase.png", new Vector3(6.8f, 1.4f, 12.0f), Quaternion.Euler(8f, -155f, 0f), 44f);

            Debug.Log("===============================================================================");
            Debug.Log(">>> [GridSense Section 6] RENDERING PIPELINE AUDIT & EVIDENCE CAPTURED <<<");
            Debug.Log("===============================================================================");
        }

        private static void CaptureRenderView(string outputDir, string fileName, Vector3 localPos, Quaternion localRot, float fov)
        {
            GameObject camObj = new GameObject("AuditTempCamera");
            Camera cam = camObj.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 3000f;
            cam.clearFlags = CameraClearFlags.Skybox;

            // Add directional fill light to enhance PBR reflections
            GameObject fillLightObj = new GameObject("AuditTempFillLight");
            fillLightObj.transform.SetParent(camObj.transform, false);
            Light fillLight = fillLightObj.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.75f;
            fillLight.color = new Color(1.0f, 0.98f, 0.95f);

            // Position camera relative to car if available, or world
            GameObject car = GameObject.Find("Ferrari_SF23");
            if (car != null)
            {
                camObj.transform.position = car.transform.position + car.transform.TransformDirection(localPos);
                camObj.transform.rotation = car.transform.rotation * localRot;
            }
            else
            {
                camObj.transform.position = localPos;
                camObj.transform.rotation = localRot;
            }

            int width = 1280;
            int height = 720;
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            byte[] pngBytes = tex.EncodeToPNG();
            string outPath = Path.Combine(outputDir, fileName);
            File.WriteAllBytes(outPath, pngBytes);

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(camObj);

            Debug.Log($"[Section 6 - Evidence Capture] Rendered viewpoint saved to: {outPath} ({pngBytes.Length / 1024} KB)");
        }
    }
}
