using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GridSense.EditorTools
{
    /// <summary>
    /// Standalone build pipeline script for GridSense AI Motorsport Intelligence System.
    /// Builds a release Standalone Windows 64-bit executable targeted for laptop integrated GPUs.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("GridSense/Build/Build Windows Standalone Release")]
        public static void PerformStandaloneBuild()
        {
            Debug.Log("=== STARTING GRIDSENSE WINDOWS STANDALONE RELEASE BUILD ===");

            // 1. Ensure URP & PBR textures are configured
            URPSetupHelper.EnsureURPConfigured();
            TextureGenerator.GenerateAllPBRTextures();

            // 2. Ensure Main Simulation Scene is generated and saved
            SceneSetupEditor.CreateAndSetupMainScene();

            // 3. Ensure destination directory exists at project root /Build/
            string outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Build");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            string exePath = Path.Combine(outputDirectory, "GridSense_Sim.exe");

            // 3. Configure BuildPlayerOptions
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/MainSimulation.unity" },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            // 4. Execute Build
            DateTime startTime = DateTime.Now;
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            TimeSpan duration = DateTime.Now - startTime;

            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"=== BUILD SUCCEEDED ===");
                Debug.Log($"Output Location: {exePath}");
                Debug.Log($"Total Build Size: {summary.totalSize / (1024 * 1024):F2} MB");
                Debug.Log($"Build Duration: {duration.TotalSeconds:F1} seconds");
                Debug.Log($"Total Warnings: {summary.totalWarnings}, Total Errors: {summary.totalErrors}");
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"=== BUILD FAILED ===");
                Debug.LogError($"Total Errors: {summary.totalErrors}");
                foreach (BuildStep step in report.steps)
                {
                    foreach (BuildStepMessage msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            Debug.LogError($"[Build Error] {msg.content}");
                        }
                    }
                }
            }
        }
    }
}
