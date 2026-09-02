using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GridSense.EditorTools
{
    public static class TextureGenerator
    {
        [MenuItem("GridSense/Rendering/Generate PBR Textures")]
        public static void GenerateAllPBRTextures()
        {
            string dir = "Assets/Textures";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 1. Ferrari SF23 Livery Texture (2048x2048)
            GenerateLiveryTexture($"{dir}/Ferrari_SF23_Livery_Albedo.png", 2048, 2048);

            // 2. Carbon Fiber Twill Normal Map (1024x1024)
            GenerateCarbonNormalMap($"{dir}/Ferrari_Carbon_Normal.png", 1024, 1024);

            // 3. Pirelli P-Zero Tyre Sidewall (1024x1024)
            GenerateTyreSidewallTexture($"{dir}/Pirelli_Tyre_Sidewall.png", 1024, 1024);

            // 4. Track Asphalt Grain Albedo (2048x2048)
            GenerateAsphaltAlbedo($"{dir}/Track_Asphalt_Albedo.png", 2048, 2048);

            // 5. Track Asphalt Normal Map (1024x1024)
            GenerateAsphaltNormalMap($"{dir}/Track_Asphalt_Normal.png", 1024, 1024);

            // 6. FIA Kerb Texture with Rubber Skid Marks (1024x1024)
            GenerateKerbTexture($"{dir}/FIA_Kerb_Albedo.png", 1024, 1024);

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{dir}/Ferrari_SF23_Livery_Albedo.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{dir}/Ferrari_Carbon_Normal.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{dir}/Pirelli_Tyre_Sidewall.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{dir}/Track_Asphalt_Albedo.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{dir}/Track_Asphalt_Normal.png", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset($"{dir}/FIA_Kerb_Albedo.png", ImportAssetOptions.ForceUpdate);
            Debug.Log("[TextureGenerator] Generated and force-reimported all 6 high-resolution PBR texture maps in Assets/Textures/");
        }

        private static void GenerateLiveryTexture(string path, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            Color rossoCorsa = new Color(0.88f, 0.04f, 0.08f, 1.0f); // Vibrant Ferrari Rosso Corsa
            Color carbonBlack = new Color(0.10f, 0.10f, 0.11f, 1.0f);
            Color modenaYellow = new Color(0.98f, 0.85f, 0.05f, 1.0f);
            Color sponsorWhite = new Color(0.96f, 0.96f, 0.96f, 1.0f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    float v = (float)y / height;

                    Color c = rossoCorsa;

                    // Carbon fiber floor and lower edges (bottom 10%)
                    if (v < 0.10f)
                    {
                        int pattern = (x / 4 + y / 4) % 2;
                        c = pattern == 0 ? new Color(0.08f, 0.08f, 0.09f) : new Color(0.12f, 0.12f, 0.13f);
                    }
                    // Yellow Modena Racing Centerline
                    else if (u > 0.492f && u < 0.508f)
                    {
                        c = modenaYellow;
                    }
                    // Santander / Shell White Sponsor Logo Panel on nose and sidepods
                    else if ((u > 0.38f && u < 0.62f && v > 0.35f && v < 0.45f) ||
                             (u > 0.20f && u < 0.35f && v > 0.65f && v < 0.75f) ||
                             (u > 0.65f && u < 0.80f && v > 0.65f && v < 0.75f))
                    {
                        c = sponsorWhite;
                    }
                    // Scuderia Ferrari Yellow Shield Badge
                    else if ((u > 0.15f && u < 0.22f && v > 0.80f && v < 0.88f) ||
                             (u > 0.78f && u < 0.85f && v > 0.80f && v < 0.88f))
                    {
                        c = modenaYellow;
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void GenerateCarbonNormalMap(string path, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int patternX = (x / 4) % 2;
                    int patternY = (y / 4) % 2;
                    float nx = (patternX == patternY) ? 0.6f : 0.4f;
                    float ny = (patternX == patternY) ? 0.4f : 0.6f;
                    tex.SetPixel(x, y, new Color(nx, ny, 1.0f, 1.0f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void GenerateTyreSidewallTexture(string path, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float maxR = width * 0.48f;
            float minR = width * 0.25f;

            Color rubberColor = new Color(0.14f, 0.14f, 0.15f, 1.0f);
            Color softRed = new Color(0.92f, 0.10f, 0.12f, 1.0f); // Pirelli Soft Red Stripe
            Color white = new Color(0.95f, 0.95f, 0.95f, 1.0f);
            Color yellow = new Color(0.95f, 0.80f, 0.05f, 1.0f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    Color c = rubberColor;

                    if (dist > minR && dist < maxR)
                    {
                        // Outer Compound Ring
                        if (dist > maxR - (width * 0.04f) && dist < maxR - (width * 0.01f))
                        {
                            c = softRed;
                        }
                        // P-ZERO Text Branding Marks at 0, 90, 180, 270 degrees
                        float angle = Mathf.Atan2(y - center.y, x - center.x) * Mathf.Rad2Deg;
                        if (angle < 0) angle += 360f;

                        bool isLogoSector = (angle > 80 && angle < 100) || (angle > 260 && angle < 280);
                        if (isLogoSector && dist > (minR + maxR) * 0.5f - 10 && dist < (minR + maxR) * 0.5f + 10)
                        {
                            c = yellow;
                        }
                    }
                    else if (dist <= minR)
                    {
                        c = new Color(0.08f, 0.08f, 0.09f, 1.0f); // Inner Carbon Rim
                    }
                    else
                    {
                        c = Color.clear;
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void GenerateAsphaltAlbedo(string path, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            System.Random rand = new System.Random(42);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float noise = (float)(rand.NextDouble() * 0.08 - 0.04);
                    float baseTone = 0.22f + noise;
                    tex.SetPixel(x, y, new Color(baseTone, baseTone, baseTone * 1.02f, 1.0f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void GenerateAsphaltNormalMap(string path, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            System.Random rand = new System.Random(99);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = 0.5f + (float)(rand.NextDouble() * 0.12 - 0.06);
                    float ny = 0.5f + (float)(rand.NextDouble() * 0.12 - 0.06);
                    tex.SetPixel(x, y, new Color(nx, ny, 1.0f, 1.0f));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void GenerateKerbTexture(string path, int width, int height)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, true);
            Color red = new Color(0.85f, 0.10f, 0.12f);
            Color white = new Color(0.95f, 0.95f, 0.95f);
            Color rubberScuff = new Color(0.20f, 0.20f, 0.20f);

            for (int y = 0; y < height; y++)
            {
                int stripeIndex = (y / (height / 8)) % 2;
                Color baseColor = (stripeIndex == 0) ? red : white;

                for (int x = 0; x < width; x++)
                {
                    float u = (float)x / width;
                    Color c = baseColor;

                    // Dark rubber scuff marks along inner apex edge (left 35%)
                    if (u < 0.35f)
                    {
                        float blend = (1.0f - (u / 0.35f)) * 0.45f;
                        c = Color.Lerp(baseColor, rubberScuff, blend);
                    }

                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
