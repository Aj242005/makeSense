using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GridSense.EditorTools
{
    [InitializeOnLoad]
    public static class URPSetupHelper
    {
        static URPSetupHelper()
        {
            EnsureURPConfigured();
        }

        [MenuItem("GridSense/Rendering/Ensure URP Asset Configured")]
        public static UniversalRenderPipelineAsset EnsureURPConfigured()
        {
            string dir = "Assets/Settings";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            string urpAssetPath = $"{dir}/GridSense_URP_Asset.asset";
            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpAssetPath);

            if (urpAsset == null)
            {
                // Create Renderer Data
                string rendererDataPath = $"{dir}/GridSense_UniversalRendererData.asset";
                UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererDataPath);
                if (rendererData == null)
                {
                    rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(rendererData, rendererDataPath);
                }

                // Create URP Pipeline Asset
                urpAsset = UniversalRenderPipelineAsset.Create(rendererData);
                urpAsset.shadowDistance = 60.0f;
                urpAsset.shadowCascadeCount = 2;
                urpAsset.renderScale = 1.0f;
                urpAsset.msaaSampleCount = 2;
                urpAsset.supportsHDR = false; // Optimize bandwidth for Iris Xe

                AssetDatabase.CreateAsset(urpAsset, urpAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[URPSetupHelper] Created GridSense URP Asset at: {urpAssetPath}");
            }

            // Assign to Graphics & Quality Settings
            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;

            Debug.Log($"[URPSetupHelper] Verified URP Pipeline Active: {GraphicsSettings.defaultRenderPipeline.name}");
            return urpAsset;
        }
    }
}
