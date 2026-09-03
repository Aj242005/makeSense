using UnityEngine;
using UnityEngine.UIElements;

namespace GridSense.UI
{
    /// <summary>
    /// Holds the two UI Toolkit assets the pit wall needs at runtime. Kept as a single
    /// ScriptableObject in Resources so the bootstrapper can find them without the .uxml and .uss
    /// having to move into a Resources folder (which would break the stylesheet's project:// path).
    ///
    /// Created by Tools > GridSense > Set Up Pit Wall And AI Layer.
    /// </summary>
    [CreateAssetMenu(fileName = "GridSensePitWall", menuName = "GridSense/Pit Wall Resources")]
    public class PitWallResources : ScriptableObject
    {
        public const string ResourcePath = "GridSensePitWall";

        [Tooltip("Assets/UI/PitWallDashboard.uxml")]
        public VisualTreeAsset Layout;

        [Tooltip("A PanelSettings asset with a runtime theme assigned.")]
        public PanelSettings Panel;

        public bool IsComplete { get { return Layout != null && Panel != null; } }

        public static PitWallResources Load()
        {
            return Resources.Load<PitWallResources>(ResourcePath);
        }
    }
}
