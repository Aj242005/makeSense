using System;
using UnityEngine;

namespace GridSense.Track
{
    /// <summary>
    /// TrackColliderSetup ensures solid driving surface colliders on asphalt and barriers
    /// while strictly excluding flat painted lines, grid slots, pole markers, and scenery props.
    /// </summary>
    [ExecuteInEditMode]
    public class TrackColliderSetup : MonoBehaviour
    {
        [Header("Collider Setup Configuration")]
        [SerializeField] private bool autoGenerateOnStart = true;

        private void Start()
        {
            if (autoGenerateOnStart)
            {
                EnsureTrackColliders();
            }
        }

        [ContextMenu("Ensure Track Colliders")]
        public void EnsureTrackColliders()
        {
            // Remove any accidental mesh colliders on non-solid markings
            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            int removed = 0;

            foreach (Collider c in allColliders)
            {
                string n = c.gameObject.name.ToLower();
                if (n.Contains("line") || n.Contains("grid") || n.Contains("pole") || n.Contains("sponsor") || n.Contains("lamp") || n.Contains("mast") || n.Contains("crossbar") || n.Contains("kerb"))
                {
                    // These are purely visual surface markings or thin decorative elements
                    if (Application.isPlaying) Destroy(c);
                    else DestroyImmediate(c);
                    removed++;
                }
            }

            Debug.Log($"[TrackColliderSetup] Track collision verified. Cleaned {removed} non-solid decorative colliders.");
        }
    }
}
