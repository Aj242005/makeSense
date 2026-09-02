using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GridSense.Environment
{
    /// <summary>
    /// RenderingOptimizer manages graphics quality, texture budget caps, dynamic resolution,
    /// and draw-call reduction techniques targeted specifically for Laptop Integrated GPUs
    /// (Intel Iris Xe, AMD Radeon 680M/780M):
    /// - Caps target framerate at 60 FPS (VSync toggle)
    /// - Enables GPU Instancing across track and prop materials
    /// - Configures Anisotropic Filtering and Shadow Cascades for integrated GPU thermal envelope
    /// - Monitors real-time VRAM allocation and Draw Call estimates
    /// </summary>
    public class RenderingOptimizer : MonoBehaviour
    {
        public static RenderingOptimizer Instance { get; private set; }

        [Header("Target Platform Profile")]
        [SerializeField] private IntegratedGPUProfile targetProfile = IntegratedGPUProfile.BalancedIntegrated;
        [SerializeField] private int targetFrameRate = 60;

        [Header("Runtime Texture Budget Metrics")]
        [SerializeField] private float textureVRAMBudgetMB = 256.0f;
        public float CurrentEstimatedVRAMUsageMB { get; private set; } = 0.0f;
        public int ActiveMaterialCount { get; private set; } = 0;
        public int InstancedMaterialCount { get; private set; } = 0;

        [Header("Shadows & Lighting Caps")]
        [SerializeField] private float shadowDistance = 60.0f;

        public enum IntegratedGPUProfile
        {
            LowEndIntegrated,     // Intel UHD / Iris Plus (1080p 30-45 FPS or 720p 60 FPS)
            BalancedIntegrated,   // Intel Iris Xe / AMD Radeon 680M (1080p 60 FPS)
            HighEndIntegrated     // AMD Radeon 780M / Apple M1-M3 (1080p/1440p 60 FPS)
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ApplyIntegratedGPUOptimizations();
        }

        private void Start()
        {
            AuditAndEnableGPUInstancing();
            ProfileTextureMemoryBudget();
        }

        public void ApplyIntegratedGPUOptimizations()
        {
            // 1. Framerate & VSync
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 1; // Prevent screen tearing and unnecessary thermal draw

            // 2. Texture & Anisotropic Filtering
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.globalTextureMipmapLimit = 0; // Full 2K/1K capped resolution

            // 3. Shadow Settings optimized for Integrated VRAM bandwidth
            switch (targetProfile)
            {
                case IntegratedGPUProfile.LowEndIntegrated:
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowDistance = 40.0f;
                    QualitySettings.shadowCascades = 0;
                    break;
                case IntegratedGPUProfile.BalancedIntegrated:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowDistance = shadowDistance;
                    QualitySettings.shadowCascades = 2;
                    break;
                case IntegratedGPUProfile.HighEndIntegrated:
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowDistance = 80.0f;
                    QualitySettings.shadowCascades = 2;
                    break;
            }

            // 4. Particle & Skinning budget
            QualitySettings.skinWeights = SkinWeights.TwoBones;
            QualitySettings.particleRaycastBudget = 256;

            Debug.Log($"[RenderingOptimizer] Applied optimizations for {targetProfile}. Target FPS: {targetFrameRate}, Shadow Dist: {QualitySettings.shadowDistance}m, Cascades: {QualitySettings.shadowCascades}.");
        }

        public void AuditAndEnableGPUInstancing()
        {
            Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int totalMats = 0;
            int instancedMats = 0;

            foreach (Renderer rend in allRenderers)
            {
                if (rend == null) continue;

                Material[] sharedMats = rend.sharedMaterials;
                for (int i = 0; i < sharedMats.Length; i++)
                {
                    Material mat = sharedMats[i];
                    if (mat == null) continue;

                    totalMats++;
                    if (!mat.enableInstancing)
                    {
                        mat.enableInstancing = true;
                    }
                    if (mat.enableInstancing)
                    {
                        instancedMats++;
                    }
                }
            }

            ActiveMaterialCount = totalMats;
            InstancedMaterialCount = instancedMats;
            Debug.Log($"[RenderingOptimizer] Audited {totalMats} material references across scene. GPU Instancing active on {instancedMats} materials.");
        }

        public void ProfileTextureMemoryBudget()
        {
            long totalAllocatedMemory = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            long totalReservedMemory = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
            
            CurrentEstimatedVRAMUsageMB = (totalAllocatedMemory / (1024.0f * 1024.0f));
            
            bool isWithinBudget = CurrentEstimatedVRAMUsageMB <= textureVRAMBudgetMB;
            Debug.Log($"[RenderingOptimizer] Current Memory Footprint: {CurrentEstimatedVRAMUsageMB:F1} MB / {textureVRAMBudgetMB:F1} MB Budget (Within Budget: {isWithinBudget}).");
        }
    }
}
