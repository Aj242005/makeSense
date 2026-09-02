using System;
using System.IO;
using UnityEngine;

namespace GridSense.Environment
{
    /// <summary>
    /// StandaloneRuntimeCapture provides on-demand screenshot capability (F12 key)
    /// without ever closing or quitting the running game.
    /// </summary>
    public class StandaloneRuntimeCapture : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // F12 key: Capture high-res screenshot to EvidenceShots/
            if (UnityEngine.Input.GetKeyDown(KeyCode.F12))
            {
                CaptureManualScreenshot();
            }
        }

        public void CaptureManualScreenshot()
        {
            try
            {
                string outDir = @"C:\Unity-In-Diversity\makeSense\EvidenceShots";
                Directory.CreateDirectory(outDir);
                string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string shotPath = Path.Combine(outDir, $"screenshot_{timeStamp}.png");
                ScreenCapture.CaptureScreenshot(shotPath);
                Debug.Log($"[GridSense Capture] Saved screenshot: {shotPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GridSense Capture] Screenshot error: {ex.Message}");
            }
        }
    }
}
