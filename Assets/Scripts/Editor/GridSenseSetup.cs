using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using GridSense.UI;

namespace GridSense.EditorTools
{
    /// <summary>
    /// One-click setup for the pit wall's UI Toolkit assets.
    ///
    /// UI Toolkit needs a PanelSettings asset carrying a runtime ThemeStyleSheet before a
    /// UIDocument will render anything. Neither existed in this project, which is part of why the
    /// pit-wall dashboard was never visible. This creates them, points a resources asset at the
    /// layout, and reports exactly what it did.
    ///
    /// The AI layer itself needs no editor step: GridSenseBootstrap creates SimulationCore and both
    /// inference runners at load time.
    /// </summary>
    [InitializeOnLoad]
    public static class GridSenseSetup
    {
        private const string UxmlPath = "Assets/UI/PitWallDashboard.uxml";
        private const string ThemePath = "Assets/UI/GridSenseRuntimeTheme.tss";
        private const string PanelPath = "Assets/UI/GridSensePanelSettings.asset";
        private const string ResourcesDir = "Assets/Resources";
        private const string ResourceAssetPath = "Assets/Resources/GridSensePitWall.asset";

        /// <summary>
        /// Runs the setup automatically the first time the project compiles, so the pit wall is
        /// live without anyone having to find a menu item. Without this the dashboard silently
        /// never instantiates and the P key advertised in the control legend does nothing.
        /// Cheap and idempotent: once the resources asset exists this returns immediately.
        /// </summary>
        static GridSenseSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (AssetDatabase.LoadAssetAtPath<PitWallResources>(ResourceAssetPath) != null) return;
                if (AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath) == null) return;
                Run(true);
            };
        }

        [MenuItem("Tools/GridSense/Set Up Pit Wall And AI Layer")]
        public static void Run()
        {
            Run(false);
        }

        private static void Run(bool silent)
        {
            string report = "";

            // --- 1. layout
            VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (layout == null)
            {
                if (!silent)
                {
                    EditorUtility.DisplayDialog("GridSense setup",
                        "Could not find " + UxmlPath + ".\n\nThe pit wall layout is missing, so there is nothing to wire up.",
                        "OK");
                }
                return;
            }
            report += "layout: " + UxmlPath + "\n";

            // --- 2. runtime theme
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            if (theme == null)
            {
                // reuse any theme the project already has before making another one
                string[] existing = AssetDatabase.FindAssets("t:ThemeStyleSheet");
                if (existing.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(existing[0]);
                    theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                    report += "theme: reused " + path + "\n";
                }
            }
            else
            {
                report += "theme: " + ThemePath + " (existing)\n";
            }

            if (theme == null)
            {
                Directory.CreateDirectory("Assets/UI");
                File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);
                theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
                report += "theme: created " + ThemePath + "\n";
            }

            if (theme == null)
            {
                Debug.LogWarning("[GridSenseSetup] " + ThemePath + " was written but Unity has not " +
                    "imported it as a ThemeStyleSheet yet. This usually resolves on the next asset " +
                    "refresh; if it does not, create a runtime theme manually " +
                    "(Assets > Create > UI Toolkit > TSS Theme File) and re-run " +
                    "Tools > GridSense > Set Up Pit Wall And AI Layer.");
                if (!silent)
                {
                    EditorUtility.DisplayDialog("GridSense setup",
                        "Created " + ThemePath + " but Unity did not import it as a ThemeStyleSheet.\n\n" +
                        "Try this menu item once more after the import finishes.",
                        "OK");
                }
                return;
            }

            // --- 3. panel settings
            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
                report += "panel settings: created " + PanelPath + "\n";
            }
            else
            {
                report += "panel settings: " + PanelPath + " (existing)\n";
            }

            panel.themeStyleSheet = theme;
            // The console is laid out in design pixels; scale it with the window so it holds at
            // 1080p and at 4K rather than shrinking into a corner.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);

            // --- 4. resources handle
            Directory.CreateDirectory(ResourcesDir);
            PitWallResources res = AssetDatabase.LoadAssetAtPath<PitWallResources>(ResourceAssetPath);
            if (res == null)
            {
                res = ScriptableObject.CreateInstance<PitWallResources>();
                AssetDatabase.CreateAsset(res, ResourceAssetPath);
                report += "resources: created " + ResourceAssetPath + "\n";
            }
            else
            {
                report += "resources: " + ResourceAssetPath + " (existing)\n";
            }

            res.Layout = layout;
            res.Panel = panel;
            EditorUtility.SetDirty(res);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GridSenseSetup] pit wall assets ready\n" + report);
            if (!silent)
            {
                EditorUtility.DisplayDialog("GridSense setup complete",
                    report +
                    "\nPress Play, then P to open the pit wall.\n\n" +
                    "SimulationCore and both inference runners are created automatically at load time " +
                    "by GridSenseBootstrap - no scene changes are needed.",
                    "OK");
            }
        }

        [MenuItem("Tools/GridSense/Report Scene Wiring")]
        public static void Report()
        {
            string s = "GridSense wiring\n\n";
            s += "PitWallDashboard.uxml   " + (AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath) != null ? "found" : "MISSING") + "\n";
            s += "PanelSettings           " + (AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath) != null ? "found" : "not created yet") + "\n";
            s += "Resources handle        " + (AssetDatabase.LoadAssetAtPath<PitWallResources>(ResourceAssetPath) != null ? "found" : "not created yet") + "\n";
            s += "energy_deployment_ppo   " + (Resources.Load("energy_deployment_ppo") != null ? "found" : "MISSING") + "\n";
            s += "tyre_degradation_ebm    " + (Resources.Load("tyre_degradation_ebm") != null ? "found" : "MISSING") + "\n";
            Debug.Log(s);
            EditorUtility.DisplayDialog("GridSense wiring", s, "OK");
        }
    }
}
