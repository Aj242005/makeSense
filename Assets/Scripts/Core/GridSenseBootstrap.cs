using UnityEngine;
using UnityEngine.UIElements;
using GridSense.Core;
using GridSense.ML;
using GridSense.Physics;
using GridSense.UI;

namespace GridSense.Core
{
    /// <summary>
    /// Brings up the parts of GridSense that exist in code but were never placed in the scene:
    /// SimulationCore, both Sentis inference runners, and the pit-wall dashboard.
    ///
    /// This runs as a runtime initializer rather than as a scene component on purpose. The scene
    /// file is a 3.8 MB YAML document, and adding components to it by hand is a change that cannot
    /// be verified without opening the editor. Creating them at load time is equivalent at runtime,
    /// reversible, and cannot corrupt the scene.
    ///
    /// It is idempotent: if a component is already present in the scene, it is left alone.
    /// </summary>
    public static class GridSenseBootstrap
    {
        private const string HostName = "GridSense Runtime";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // Only bring the stack up in a scene that actually contains the car; this initializer
            // runs for every loaded scene.
            CarPhysicsController car = Object.FindFirstObjectByType<CarPhysicsController>();
            if (car == null) return;

            GameObject host = GameObject.Find(HostName);
            if (host == null)
            {
                host = new GameObject(HostName);
                Object.DontDestroyOnLoad(host);
            }

            bool addedCore = EnsureCore(host);
            bool addedT1 = EnsureTrack1(host);
            bool addedT3 = EnsureTrack3(host);
            string pitWall = EnsurePitWall(host);

            Debug.Log(string.Format(
                "[GridSenseBootstrap] SimulationCore {0} · Track 1 {1} · Track 3 {2} · pit wall {3}",
                addedCore ? "created" : "already in scene",
                addedT1 ? "created" : "already in scene",
                addedT3 ? "created" : "already in scene",
                pitWall));
        }

        private static bool EnsureCore(GameObject host)
        {
            if (SimulationCore.Instance != null) return false;
            if (Object.FindFirstObjectByType<SimulationCore>() != null) return false;
            host.AddComponent<SimulationCore>();
            return true;
        }

        private static bool EnsureTrack1(GameObject host)
        {
            if (Track1InferenceRunner.Instance != null) return false;
            if (Object.FindFirstObjectByType<Track1InferenceRunner>() != null) return false;
            host.AddComponent<Track1InferenceRunner>();
            return true;
        }

        private static bool EnsureTrack3(GameObject host)
        {
            if (Track3InferenceRunner.Instance != null) return false;
            if (Object.FindFirstObjectByType<Track3InferenceRunner>() != null) return false;
            host.AddComponent<Track3InferenceRunner>();
            return true;
        }

        /// <summary>
        /// Creates the pit-wall UIDocument, but only when both UI Toolkit assets are available.
        /// A UIDocument without PanelSettings renders nothing and logs every frame, so the honest
        /// behaviour when the setup step has not been run is to say so once and stop.
        /// </summary>
        private static string EnsurePitWall(GameObject host)
        {
            if (Object.FindFirstObjectByType<PitWallDashboardController>() != null) return "already in scene";

            PitWallResources res = PitWallResources.Load();
            if (res == null)
            {
                Debug.LogWarning(
                    "[GridSenseBootstrap] Pit wall not created: Assets/Resources/" +
                    PitWallResources.ResourcePath + ".asset is missing. " +
                    "Run Tools > GridSense > Set Up Pit Wall And AI Layer once in the editor.");
                return "not created (no resources asset)";
            }

            if (!res.IsComplete)
            {
                Debug.LogWarning(
                    "[GridSenseBootstrap] Pit wall not created: " + PitWallResources.ResourcePath +
                    " is missing its " +
                    (res.Layout == null ? "PitWallDashboard.uxml layout" : "PanelSettings") +
                    ". Run Tools > GridSense > Set Up Pit Wall And AI Layer to repair it.");
                return "not created (incomplete resources)";
            }

            GameObject go = new GameObject("Pit Wall");
            go.transform.SetParent(host.transform, false);

            UIDocument doc = go.AddComponent<UIDocument>();
            doc.panelSettings = res.Panel;
            doc.visualTreeAsset = res.Layout;
            doc.sortingOrder = 100f;

            go.AddComponent<PitWallDashboardController>();
            return "created";
        }
    }
}
