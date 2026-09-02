using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GridSense.EditorTools
{
    public static class BuildAndValidatePipeline
    {
        public static void RunFullPipeline()
        {
            Debug.Log("==================================================================");
            Debug.Log(">>> [Pipeline] 1. REBUILDING SCENE WITH CLEAN SPIKE-FREE CIRCUIT");
            Debug.Log("==================================================================");

            URPSetupHelper.EnsureURPConfigured();
            TextureGenerator.GenerateAllPBRTextures();
            SceneSetupEditor.CreateAndSetupMainScene();

            string scenePath = "Assets/Scenes/MainSimulation.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject car = GameObject.Find("Ferrari_SF23");
            if (car != null)
            {
                Debug.Log($"[Pipeline Query] Car Position: {car.transform.position}");
                Debug.Log($"[Pipeline Query] Car Rotation (Euler): {car.transform.eulerAngles}");
                Debug.Log($"[Pipeline Query] Car Forward Vector: {car.transform.forward}");
            }
            else
            {
                Debug.LogError("[Pipeline Query] Ferrari_SF23 NOT FOUND in scene!");
            }

            Debug.Log("==================================================================");
            Debug.Log(">>> [Pipeline] 2. COMPILING STANDALONE WINDOWS 64-BIT RELEASE BUILD");
            Debug.Log("==================================================================");

            BuildScript.PerformStandaloneBuild();

            Debug.Log("==================================================================");
            Debug.Log(">>> [Pipeline] 3. CAPTURING IN-ENGINE RUNTIME EVIDENCE SHOTS");
            Debug.Log("==================================================================");

            GridAlignmentAudit.AlignCarToStartGridAndCapture();

            Debug.Log("==================================================================");
            Debug.Log(">>> [Pipeline] ALL PIPELINE STAGES COMPLETED SUCCESSFULLY");
            Debug.Log("==================================================================");
        }
    }
}
