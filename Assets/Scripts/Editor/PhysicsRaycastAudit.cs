using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GridSense.EditorTools
{
    public static class PhysicsRaycastAudit
    {
        public static void AuditRaycast()
        {
            string scenePath = "Assets/Scenes/MainSimulation.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject car = GameObject.Find("Ferrari_SF23");
            Debug.Log($"Car Found: {car != null}");
            if (car != null)
            {
                Rigidbody rb = car.GetComponent<Rigidbody>();
                Debug.Log($"Car Rigidbody: isKinematic={rb?.isKinematic}, useGravity={rb?.useGravity}, mass={rb?.mass}");
                
                Collider[] cols = car.GetComponentsInChildren<Collider>();
                Debug.Log($"Car Colliders count: {cols.Length}");
                foreach (var c in cols)
                {
                    Debug.Log($"  Car Collider: {c.GetType().Name} on {c.gameObject.name}, bounds={c.bounds}");
                }
            }

            // Raycast downward from (369.80, 110.0, -40.0)
            Vector3 rayStart = new Vector3(369.80f, 110.0f, -40.00f);
            RaycastHit[] hits = UnityEngine.Physics.RaycastAll(rayStart, Vector3.down, 100f);
            Debug.Log($"Downwards Raycast from {rayStart} got {hits.Length} hits:");
            foreach (var h in hits)
            {
                Debug.Log($"  HIT: point={h.point}, dist={h.distance:F2}m, normal={h.normal}, collider={h.collider.name} on GameObject {h.collider.gameObject.name}");
            }
        }
    }
}
