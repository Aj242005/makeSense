using System;
using UnityEngine;
using UnityEditor;

namespace GridSense.EditorTools
{
    public static class InspectCarMaterials
    {
        [MenuItem("GridSense/Audit/Inspect Car Materials")]
        public static void Inspect()
        {
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/F1_Chassis/F1_Car_Mesh.obj");
            if (modelPrefab == null)
            {
                Debug.LogError("Model prefab not found!");
                return;
            }

            Renderer[] renderers = modelPrefab.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"Car Renderers count: {renderers.Length}");
            foreach (Renderer r in renderers)
            {
                Debug.Log($"Renderer: {r.name}, sharedMaterials count: {r.sharedMaterials.Length}");
                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    Material m = r.sharedMaterials[i];
                    Debug.Log($"  Slot {i}: {(m != null ? m.name : "null")}, mainTex: {(m != null && m.mainTexture != null ? m.mainTexture.name : "null")}");
                }
            }
        }
    }
}
