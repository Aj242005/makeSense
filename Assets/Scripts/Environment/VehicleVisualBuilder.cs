using System;
using System.Collections.Generic;
using UnityEngine;
using GridSense.Physics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridSense.Environment
{
    /// <summary>
    /// VehicleVisualBuilder instantiates the modular 3D F1 chassis, wheels, and steering wheel,
    /// configuring PBR materials with authentic textures and connecting the F1WheelAnimator component.
    /// </summary>
    public static class VehicleVisualBuilder
    {
        public static GameObject BuildFerrariSF23Visuals(Transform parent)
        {
            // Destroy any previous visual instances
            Transform oldBody = parent.Find("SF23_Body");
            if (oldBody != null)
            {
                UnityEngine.Object.DestroyImmediate(oldBody.gameObject);
            }

            GameObject bodyRoot = new GameObject("SF23_Body");
            bodyRoot.transform.SetParent(parent, false);
            bodyRoot.transform.localPosition = Vector3.zero;
            bodyRoot.transform.localRotation = Quaternion.identity;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

#if UNITY_EDITOR
            // Preload all textures into dictionary
            Dictionary<string, Texture2D> texDict = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
            string[] texNames = new string[] {
                "mcl35m_png", "mcl35m_c_png", "mcl35m_m_png", "mcl35m_h_png",
                "tyrewall_png", "tread_png", "st_wheel_png", "rim_png"
            };

            foreach (string tn in texNames)
            {
                string p1 = $"Assets/Models/F1_Chassis/{tn}.png";
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(p1);
                if (t == null)
                {
                    string p2 = $"Assets/Models/F1_Chassis/{tn}_baseColor.png";
                    t = AssetDatabase.LoadAssetAtPath<Texture2D>(p2);
                }
                if (t != null)
                {
                    texDict[tn] = t;
                    texDict[tn.Replace("_png", "")] = t;
                }
            }

            // Fallback default car livery if specific sub-textures not found
            Texture2D mainLivery = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/F1_Chassis/mcl35m_png.png") ??
                                  AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/F1_Chassis/mcl35m_png_baseColor.png");

            // 1. Chassis Body
            GameObject chassisPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/F1_Chassis/F1_Chassis_Body.obj");
            if (chassisPrefab != null)
            {
                GameObject chassisInstance = UnityEngine.Object.Instantiate(chassisPrefab, bodyRoot.transform);
                chassisInstance.name = "Chassis_Main";
                chassisInstance.transform.localPosition = Vector3.zero;
                chassisInstance.transform.localRotation = Quaternion.identity;
                ConfigureMaterials(chassisInstance, urpLit, texDict, mainLivery);
            }

            // 2. Animated Wheels with Exact Concentric Pivots
            F1WheelAnimator animator = parent.gameObject.GetComponent<F1WheelAnimator>() ?? parent.gameObject.AddComponent<F1WheelAnimator>();

            // Front Left Wheel Pivot: (-0.82425, -0.23018, 1.13949)
            GameObject flGo = new GameObject("Wheel_FL_Pivot");
            flGo.transform.SetParent(bodyRoot.transform, false);
            flGo.transform.localPosition = new Vector3(-0.82425f, -0.23018f, 1.13949f);
            flGo.transform.localRotation = Quaternion.identity;
            LoadWheelMesh("Assets/Models/F1_Chassis/F1_Wheel_FL.obj", flGo.transform, urpLit, texDict, mainLivery);
            animator.wheelFL = flGo.transform;

            // Front Right Wheel Pivot: (0.82458, -0.23018, 1.13934)
            GameObject frGo = new GameObject("Wheel_FR_Pivot");
            frGo.transform.SetParent(bodyRoot.transform, false);
            frGo.transform.localPosition = new Vector3(0.82458f, -0.23018f, 1.13934f);
            frGo.transform.localRotation = Quaternion.identity;
            LoadWheelMesh("Assets/Models/F1_Chassis/F1_Wheel_FR.obj", frGo.transform, urpLit, texDict, mainLivery);
            animator.wheelFR = frGo.transform;

            // Rear Left Wheel Pivot: (-0.79487, -0.22911, -2.55689)
            GameObject rlGo = new GameObject("Wheel_RL_Pivot");
            rlGo.transform.SetParent(bodyRoot.transform, false);
            rlGo.transform.localPosition = new Vector3(-0.79487f, -0.22911f, -2.55689f);
            rlGo.transform.localRotation = Quaternion.identity;
            LoadWheelMesh("Assets/Models/F1_Chassis/F1_Wheel_RL.obj", rlGo.transform, urpLit, texDict, mainLivery);
            animator.wheelRL = rlGo.transform;

            // Rear Right Wheel Pivot: (0.79263, -0.22911, -2.55688)
            GameObject rrGo = new GameObject("Wheel_RR_Pivot");
            rrGo.transform.SetParent(bodyRoot.transform, false);
            rrGo.transform.localPosition = new Vector3(0.79263f, -0.22911f, -2.55688f);
            rrGo.transform.localRotation = Quaternion.identity;
            LoadWheelMesh("Assets/Models/F1_Chassis/F1_Wheel_RR.obj", rrGo.transform, urpLit, texDict, mainLivery);
            animator.wheelRR = rrGo.transform;

            // 3. Cockpit Steering Wheel Pivot: (0.0, 0.01759, 0.07293)
            GameObject steerGo = new GameObject("Steering_Wheel_Pivot");
            steerGo.transform.SetParent(bodyRoot.transform, false);
            steerGo.transform.localPosition = new Vector3(0.0f, 0.01759f, 0.07293f);
            steerGo.transform.localRotation = Quaternion.identity;
            LoadWheelMesh("Assets/Models/F1_Chassis/F1_Steering_Wheel.obj", steerGo.transform, urpLit, texDict, mainLivery);
            animator.steeringWheel = steerGo.transform;

            Debug.Log("[VehicleVisualBuilder] Instantiated modular F1 chassis with authentic livery and concentric wheels.");
#endif
            return bodyRoot;
        }

#if UNITY_EDITOR
        private static void LoadWheelMesh(string path, Transform parent, Shader urpLit, Dictionary<string, Texture2D> texDict, Texture2D defaultTex)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab, parent);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                ConfigureMaterials(instance, urpLit, texDict, defaultTex);
            }
        }

        private static void ConfigureMaterials(GameObject root, Shader urpLit, Dictionary<string, Texture2D> texDict, Texture2D defaultTex)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null || r.sharedMaterials == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] != null)
                    {
                        string matName = mats[i].name.Replace(" (Instance)", "").Trim();

                        Texture2D assignedTex = null;
                        foreach (var kvp in texDict)
                        {
                            if (matName.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                assignedTex = kvp.Value;
                                break;
                            }
                        }

                        if (assignedTex == null)
                        {
                            if (matName.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                matName.IndexOf("tyre", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                texDict.TryGetValue("tyrewall_png", out assignedTex);
                            }
                            else if (matName.IndexOf("tread", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                texDict.TryGetValue("tread_png", out assignedTex);
                            }
                            else if (matName.IndexOf("st_wheel", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                texDict.TryGetValue("st_wheel_png", out assignedTex);
                            }
                            else
                            {
                                assignedTex = defaultTex;
                            }
                        }

                        Material newMat = new Material(urpLit);
                        newMat.name = mats[i].name;

                        if (assignedTex != null)
                        {
                            newMat.SetTexture("_BaseMap", assignedTex);
                            newMat.SetTexture("_MainTex", assignedTex);
                            newMat.SetColor("_BaseColor", Color.white);
                            newMat.SetFloat("_Smoothness", 0.65f);
                            newMat.SetFloat("_Metallic", 0.15f);
                        }
                        else
                        {
                            newMat.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.14f));
                            newMat.SetFloat("_Smoothness", 0.85f);
                            newMat.SetFloat("_Metallic", 0.80f);
                        }

                        newMat.enableInstancing = true;
                        mats[i] = newMat;
                    }
                }
                r.sharedMaterials = mats;
            }
        }
#endif
    }
}
