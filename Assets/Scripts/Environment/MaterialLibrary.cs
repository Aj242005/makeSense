using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridSense.Environment
{
    public static class MaterialLibrary
    {
        private static Shader GetAppropriateShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Diffuse");
            return s;
        }

        public static Material GetOrCreateMaterial(string matName, Color color, float smoothness = 0.5f, float metallic = 0.0f, string albedoTexPath = null, string normalTexPath = null)
        {
#if UNITY_EDITOR
            string dir = "Assets/Materials";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            string assetPath = $"{dir}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            bool isNew = false;

            if (mat == null)
            {
                Shader shader = GetAppropriateShader();
                mat = new Material(shader);
                mat.name = matName;
                isNew = true;
            }

            // Load and assign albedo texture if present
            if (!string.IsNullOrEmpty(albedoTexPath))
            {
                Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoTexPath);
                if (albedo != null)
                {
                    mat.SetTexture("_BaseMap", albedo);
                    mat.SetTexture("_MainTex", albedo);
                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetColor("_Color", Color.white);
                    mat.color = Color.white;
                }
                else
                {
                    mat.SetColor("_BaseColor", color);
                    mat.SetColor("_Color", color);
                    mat.color = color;
                }
            }
            else
            {
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);
                mat.color = color;
            }

            // Load and assign normal map if present
            if (!string.IsNullOrEmpty(normalTexPath))
            {
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexPath);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            if (isNew)
            {
                AssetDatabase.CreateAsset(mat, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            return mat;
#else
            Shader shader = GetAppropriateShader();
            Material mat = new Material(shader);
            mat.name = matName;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            mat.color = color;
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.enableInstancing = true;
            return mat;
#endif
        }
    }
}
