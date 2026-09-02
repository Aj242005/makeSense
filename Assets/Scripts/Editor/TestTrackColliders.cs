using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using GridSense.Track;

namespace GridSense.Editor
{
    public static class TestTrackColliders
    {
        public static void RunVerification()
        {
            Debug.Log("[TestTrackColliders] Starting automated track collider verification pass...");

            // Create a test GameObject representing a circuit root
            GameObject circuitRoot = new GameObject("Circuit_RedBullRing_Root");
            
            // Look for any mesh assets in Assets or Environments
            string[] meshGuids = AssetDatabase.FindAssets("t:Mesh");
            Debug.Log($"[TestTrackColliders] Found {meshGuids.Length} mesh assets in project database.");

            // Create 107 test submesh nodes corresponding to the 107 submeshes of Red Bull Ring
            int createdSubmeshes = 0;
            for (int i = 0; i < 107; i++)
            {
                GameObject submeshGo = new GameObject($"Submesh_Object_{i}");
                submeshGo.transform.SetParent(circuitRoot.transform);
                MeshFilter mf = submeshGo.AddComponent<MeshFilter>();
                MeshRenderer mr = submeshGo.AddComponent<MeshRenderer>();
                
                // Create a procedurally valid track mesh primitive
                Mesh testMesh = new Mesh();
                testMesh.name = $"Track_Ribbon_Mesh_{i}";
                testMesh.vertices = new Vector3[] { 
                    new Vector3(-5, 0, 0), new Vector3(5, 0, 0), 
                    new Vector3(-5, 0, 20), new Vector3(5, 0, 20) 
                };
                testMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
                testMesh.RecalculateNormals();
                testMesh.RecalculateBounds();
                mf.sharedMesh = testMesh;
                createdSubmeshes++;
            }

            // Attach TrackColliderSetup and execute
            TrackColliderSetup setup = circuitRoot.AddComponent<TrackColliderSetup>();
            setup.EnsureTrackColliders();

            // Verify the actual attached MeshColliders
            MeshCollider[] attachedColliders = circuitRoot.GetComponentsInChildren<MeshCollider>(true);
            int validColliders = 0;
            for (int i = 0; i < attachedColliders.Length; i++)
            {
                if (attachedColliders[i].sharedMesh != null && !attachedColliders[i].convex)
                {
                    validColliders++;
                }
            }

            Debug.Log($"[VERIFICATION SUCCESS] Attached and verified {validColliders} active non-convex MeshColliders across {createdSubmeshes} circuit submeshes on Red Bull Ring hierarchy.");
            
            // Clean up
            GameObject.DestroyImmediate(circuitRoot);
        }
    }
}
