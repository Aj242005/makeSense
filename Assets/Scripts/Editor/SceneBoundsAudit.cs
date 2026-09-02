using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GridSense.EditorTools
{
    public static class SceneBoundsAudit
    {
        public static void AuditScene()
        {
            string scenePath = "Assets/Scenes/MainSimulation.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject car = GameObject.Find("Ferrari_SF23");
            Debug.Log($"=== CAR POSITION: {car?.transform.position}, ROTATION: {car?.transform.rotation.eulerAngles} ===");

            GameObject trackRoot = GameObject.Find("Circuit_Geometry");
            if (trackRoot != null)
            {
                Debug.Log($"TrackRoot Pos: {trackRoot.transform.position}, Rot: {trackRoot.transform.rotation.eulerAngles}, Scale: {trackRoot.transform.localScale}");
                
                MeshRenderer[] renderers = trackRoot.GetComponentsInChildren<MeshRenderer>();
                Debug.Log($"Total Renderers under Circuit_Geometry: {renderers.Length}");

                foreach (MeshRenderer r in renderers)
                {
                    Bounds b = r.bounds;
                    // Check if near car
                    if (Vector3.Distance(b.center, car.transform.position) < 200f || b.Contains(car.transform.position))
                    {
                        Debug.Log($"[NEAR CAR] GameObject: {r.gameObject.name}, WorldBounds: Center={b.center}, Size={b.size}, Min={b.min}, Max={b.max}, Mat={r.sharedMaterial?.name}");
                    }
                }
            }
            else
            {
                Debug.LogError("Circuit_Geometry NOT FOUND!");
            }
        }
    }
}
