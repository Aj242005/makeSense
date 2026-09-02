using System;
using UnityEngine;
using UnityEditor;

namespace GridSense.EditorTools
{
    public static class InspectImportedPrefab
    {
        public static void Inspect()
        {
            GameObject car = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/F1_Chassis/F1_Car_Mesh.obj");
            if (car != null)
            {
                Debug.Log($"Car Prefab Root: {car.name}");
                foreach (Transform child in car.transform)
                {
                    Debug.Log($"  Child: {child.name}, Filter: {child.GetComponent<MeshFilter>() != null}, Renderer: {child.GetComponent<MeshRenderer>() != null}");
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        foreach (Material m in mr.sharedMaterials)
                        {
                            Debug.Log($"    Mat: {(m != null ? m.name : "null")}, MainTex: {(m != null && m.mainTexture != null ? m.mainTexture.name : "none")}");
                        }
                    }
                }
            }

            GameObject circuit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Environments/Bahrain/Bahrain_Circuit_Mesh.obj");
            if (circuit != null)
            {
                Debug.Log($"Circuit Prefab Root: {circuit.name}, Child count: {circuit.transform.childCount}");
                int i = 0;
                foreach (Transform child in circuit.transform)
                {
                    if (i < 10)
                    {
                        MeshRenderer mr = child.GetComponent<MeshRenderer>();
                        Debug.Log($"  Circuit Child {i}: {child.name}, Mat: {(mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : "none")}");
                    }
                    i++;
                }
            }
        }
    }
}
