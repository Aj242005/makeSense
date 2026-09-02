using System;
using System.Collections.Generic;
using UnityEngine;
using GridSense.Track;
using GridSense.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridSense.Environment
{
    public enum CircuitType
    {
        Bahrain = 0, // Bahrain International Circuit (Sakhir - 5.412 km, 15 Turns, 15m elevation)
        Spa = 1,     // Circuit de Spa-Francorchamps (Ardennes - 7.004 km, 19 Turns, 65m elevation)
        Monaco = 2,  // Circuit de Monaco (Monte Carlo - 3.337 km, 19 Turns, 42m elevation)
        Monza = 3    // Autodromo Nazionale Monza (Temple of Speed - 5.793 km, 11 Turns, 8m elevation)
    }

    /// <summary>
    /// TrackVisualBuilder constructs world-class official FIA Formula 1 circuits with immersive trackside architecture:
    /// - Zero-Overlap Spline Geometry: All 4 official Grand Prix circuits have clean, non-intersecting centerlines.
    /// - Grounded Elevation Baseline: Start/Finish, Pit Complex, Grandstands, and Starting Lights are grounded at Y = 0m.
    /// - Continuous 3D Road & Runoff Mesh: Seamless 4-vertex cross-section following 3D elevation.
    /// - Authentic Trackside Atmosphere & Infrastructure for all 4 official circuits:
    ///   * Bahrain International Circuit (Sakhir): Al Sakhir 8-Story VIP Tower, Tensile Grandstands, Date Palm Oasis, 28m Stadium Night Floodlights.
    ///   * Circuit de Spa-Francorchamps (Ardennes): Eau Rouge Hillside Grandstands, Multi-Tier Pine Forest, Kemmel Hospitality Village, Pouhon Grandstand.
    ///   * Circuit de Monaco (Monte Carlo): Monte Carlo Covered Tunnel, Port Hercule Yacht Marina, Swimming Pool Grandstands.
    ///   * Autodromo Nazionale Monza (Temple of Speed): Historic Banked Oval Bridge, Royal Park Oak Woodlands, Parabolica & Ascari Grandstands.
    /// - Universal F1 Safety Infrastructure: Braking Distance Marker Boards (150m, 100m, 50m), Marshall Posts, LED Flag Panels, Overhead Sponsor Bridges, TecPro Tire Barriers.
    /// </summary>
    public static class TrackVisualBuilder
    {
        public static GameObject BuildCircuitVisuals(Transform parent, CircuitType circuitType)
        {
            Transform existing = parent.Find("F1_Circuit_Geometry");
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            GameObject trackRoot = new GameObject("F1_Circuit_Geometry");
            trackRoot.transform.SetParent(parent, false);
            trackRoot.transform.localPosition = Vector3.zero;
            trackRoot.transform.localRotation = Quaternion.identity;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // --- Comprehensive Material Palette ---
            Material asphaltMat = CreateMaterial(urpLit, "Mat_Asphalt", new Color(0.16f, 0.16f, 0.18f), 0.35f, 0.05f, "Assets/Environments/Bahrain/Textures/T_Asphalt_Albedo.png");
            Material pitAsphaltMat = CreateMaterial(urpLit, "Mat_PitAsphalt", new Color(0.22f, 0.22f, 0.24f), 0.30f, 0.02f, null);
            Material kerbRedMat = CreateMaterial(urpLit, "Mat_KerbRed", new Color(0.85f, 0.12f, 0.12f), 0.50f, 0.05f, "Assets/Environments/Bahrain/Textures/T_Kerb_Albedo.png");
            Material kerbWhiteMat = CreateMaterial(urpLit, "Mat_KerbWhite", new Color(0.95f, 0.95f, 0.95f), 0.50f, 0.05f, null);
            Material kerbYellowMat = CreateMaterial(urpLit, "Mat_KerbYellow", new Color(0.98f, 0.85f, 0.10f), 0.50f, 0.05f, null);
            Material whiteLineMat = CreateMaterial(urpLit, "Mat_WhiteLine", new Color(0.98f, 0.98f, 0.98f), 0.40f, 0.02f, null);
            Material yellowLineMat = CreateMaterial(urpLit, "Mat_YellowLine", new Color(0.98f, 0.82f, 0.10f), 0.40f, 0.02f, null);

            Material terrainMat;
            if (circuitType == CircuitType.Bahrain)
                terrainMat = CreateMaterial(urpLit, "Mat_SandTerrain", new Color(0.72f, 0.62f, 0.48f), 0.10f, 0.00f, null);
            else if (circuitType == CircuitType.Spa)
                terrainMat = CreateMaterial(urpLit, "Mat_ArdennesGrass", new Color(0.24f, 0.45f, 0.22f), 0.15f, 0.00f, null);
            else if (circuitType == CircuitType.Monza)
                terrainMat = CreateMaterial(urpLit, "Mat_MonzaParkGrass", new Color(0.28f, 0.46f, 0.24f), 0.15f, 0.00f, null);
            else // Monaco (Riviera Coastal City)
                terrainMat = CreateMaterial(urpLit, "Mat_MonacoCityGround", new Color(0.32f, 0.34f, 0.36f), 0.25f, 0.02f, null);

            Material barrierMat = CreateMaterial(urpLit, "Mat_ArmcoBarrier", new Color(0.70f, 0.72f, 0.75f), 0.70f, 0.60f, "Assets/Environments/Bahrain/Textures/T_Barrier_Albedo.png");
            Material sponsorMat = CreateMaterial(urpLit, "Mat_SponsorBanner", new Color(0.90f, 0.25f, 0.15f), 0.45f, 0.05f, "Assets/Environments/Bahrain/Textures/T_Sponsor_Albedo.png");
            Material pitWallMat = CreateMaterial(urpLit, "Mat_ConcretePitWall", new Color(0.45f, 0.47f, 0.50f), 0.25f, 0.05f, null);
            Material pitBuildingMat = CreateMaterial(urpLit, "Mat_PitBuilding", new Color(0.25f, 0.28f, 0.32f), 0.55f, 0.30f, null);
            Material grandstandMat = CreateMaterial(urpLit, "Mat_Grandstand", new Color(0.18f, 0.24f, 0.38f), 0.40f, 0.10f, null);
            Material grandstandRoofMat = CreateMaterial(urpLit, "Mat_GrandstandRoof", new Color(0.85f, 0.88f, 0.92f), 0.60f, 0.40f, null);
            Material gantryMat = CreateMaterial(urpLit, "Mat_GantryMetal", new Color(0.20f, 0.20f, 0.22f), 0.60f, 0.80f, null);

            // Nature & City Materials
            Material treeTrunkMat = CreateMaterial(urpLit, "Mat_TreeTrunk", new Color(0.35f, 0.25f, 0.18f), 0.10f, 0.00f, null);
            Material treeLeafMat = CreateMaterial(urpLit, "Mat_PineFoliage", new Color(0.12f, 0.30f, 0.14f), 0.10f, 0.00f, null);
            Material oakLeafMat = CreateMaterial(urpLit, "Mat_MonzaOakFoliage", new Color(0.22f, 0.48f, 0.18f), 0.15f, 0.00f, null);
            Material palmTrunkMat = CreateMaterial(urpLit, "Mat_PalmTrunk", new Color(0.48f, 0.38f, 0.28f), 0.15f, 0.00f, null);
            Material palmLeafMat = CreateMaterial(urpLit, "Mat_PalmFronds", new Color(0.18f, 0.42f, 0.16f), 0.20f, 0.00f, null);
            Material seaWaterMat = CreateMaterial(urpLit, "Mat_MediterraneanSea", new Color(0.02f, 0.32f, 0.55f), 0.85f, 0.10f, null);
            Material yachtWhiteMat = CreateMaterial(urpLit, "Mat_YachtHull", new Color(0.96f, 0.96f, 0.98f), 0.75f, 0.10f, null);
            Material tunnelWallMat = CreateMaterial(urpLit, "Mat_TunnelConcrete", new Color(0.38f, 0.38f, 0.40f), 0.25f, 0.05f, null);
            Material historicOvalMat = CreateMaterial(urpLit, "Mat_HistoricOvalConcrete", new Color(0.50f, 0.48f, 0.45f), 0.30f, 0.05f, null);

            // Infrastructure & Sponsor Materials
            Material rolexGreenMat = CreateMaterial(urpLit, "Mat_RolexGreen", new Color(0.00f, 0.38f, 0.18f), 0.65f, 0.20f, null);
            Material rolexGoldMat = CreateMaterial(urpLit, "Mat_RolexGold", new Color(0.88f, 0.72f, 0.20f), 0.85f, 0.80f, null);
            Material pirelliYellowMat = CreateMaterial(urpLit, "Mat_PirelliYellow", new Color(0.95f, 0.80f, 0.05f), 0.65f, 0.10f, null);
            Material tireStackMat = CreateMaterial(urpLit, "Mat_TireStack", new Color(0.10f, 0.10f, 0.11f), 0.20f, 0.00f, null);
            Material marshallPostMat = CreateMaterial(urpLit, "Mat_MarshallPost", new Color(0.95f, 0.45f, 0.10f), 0.40f, 0.05f, null);
            Material brakeBoardMat = CreateMaterial(urpLit, "Mat_BrakeBoard", new Color(0.10f, 0.10f, 0.12f), 0.50f, 0.05f, null);
            Material tentFabricMat = CreateMaterial(urpLit, "Mat_TentFabric", new Color(0.92f, 0.92f, 0.94f), 0.30f, 0.00f, null);
            Material teamRedMat = CreateMaterial(urpLit, "Mat_TeamRed", new Color(0.85f, 0.10f, 0.12f), 0.50f, 0.10f, null);
            Material teamBlueMat = CreateMaterial(urpLit, "Mat_TeamBlue", new Color(0.10f, 0.25f, 0.75f), 0.50f, 0.10f, null);

            Material startLightRed = CreateMaterial(urpLit, "Mat_StartLightRed", new Color(1.0f, 0.15f, 0.15f), 0.90f, 0.0f, null);
            startLightRed.EnableKeyword("_EMISSION");
            startLightRed.SetColor("_EmissionColor", new Color(1.0f, 0.10f, 0.10f) * 3.0f);

            Material floodlightMat = CreateMaterial(urpLit, "Mat_FloodlightMetal", new Color(0.35f, 0.35f, 0.38f), 0.65f, 0.75f, null);
            Material lightEmissiveMat = CreateMaterial(urpLit, "Mat_LightEmissive", new Color(1.0f, 0.98f, 0.90f), 0.90f, 0.0f, null);
            lightEmissiveMat.EnableKeyword("_EMISSION");
            lightEmissiveMat.SetColor("_EmissionColor", new Color(1.0f, 0.95f, 0.85f) * 2.5f);

            Material flagLedGreen = CreateMaterial(urpLit, "Mat_FlagLedGreen", new Color(0.10f, 1.0f, 0.20f), 0.90f, 0.0f, null);
            flagLedGreen.EnableKeyword("_EMISSION");
            flagLedGreen.SetColor("_EmissionColor", new Color(0.10f, 1.0f, 0.20f) * 2.0f);

            // 1. Get 3D Spline with Authentic Zero-Overlap Profile
            List<Vector3> centerline;
            if (circuitType == CircuitType.Bahrain)
                centerline = GenerateBahrainGrandPrixSpline();
            else if (circuitType == CircuitType.Spa)
                centerline = GenerateSpaFrancorchampsSpline();
            else if (circuitType == CircuitType.Monza)
                centerline = GenerateMonzaGrandPrixSpline();
            else
                centerline = GenerateMonacoGrandPrixSpline();

            // 2. Vast Environment Terrain Base
            GameObject terrain = GameObject.CreatePrimitive(PrimitiveType.Plane);
            terrain.name = (circuitType == CircuitType.Bahrain) ? "Sakhir_Desert_Terrain" : (circuitType == CircuitType.Spa ? "Ardennes_Forest_Terrain" : (circuitType == CircuitType.Monza ? "Monza_Park_Terrain" : "Monaco_Riviera_Ground"));
            terrain.transform.SetParent(trackRoot.transform, false);
            terrain.transform.localPosition = new Vector3(0.0f, -0.25f, 300.0f);
            terrain.transform.localScale = new Vector3(500.0f, 1.0f, 500.0f);
            terrain.GetComponent<Renderer>().sharedMaterial = terrainMat;

            // 3. Continuous 3D Road Ribbon with Runoff Berms
            float trackWidth = (circuitType == CircuitType.Monaco) ? 12.0f : 16.0f;
            BuildContinuousRoadRibbon(trackRoot.transform, centerline, trackWidth, asphaltMat, terrainMat);

            // 4. Intelligent Curvature-Based Kerbs
            Material secondaryKerbMat = (circuitType == CircuitType.Spa) ? kerbYellowMat : kerbWhiteMat;
            BuildCurvatureBasedKerbs(trackRoot.transform, centerline, trackWidth, kerbRedMat, secondaryKerbMat);

            // 5. Continuous Safety Armco Barriers with Overlap Protection
            BuildContinuousBarriers(trackRoot.transform, centerline, trackWidth, barrierMat, sponsorMat);

            // 6. Starting Grid & Overhead FIA Start/Finish Lights Gantry (Aligned with track heading)
            int sfIdx = (circuitType == CircuitType.Monza) ? 0 : Mathf.Min(20, centerline.Count - 1);
            Vector3 sfPos = centerline[sfIdx];
            BuildStartingGridAndGantry(trackRoot.transform, centerline, sfIdx, trackWidth, whiteLineMat, yellowLineMat, gantryMat, startLightRed);

            // 7. Realistic Open Pit Complex & Grandstands (Aligned with Start/Finish straight)
            if (circuitType != CircuitType.Monaco)
            {
                BuildDetailedPitComplex(trackRoot.transform, centerline, sfIdx, trackWidth, pitWallMat, pitBuildingMat, grandstandMat, grandstandRoofMat, pitAsphaltMat, barrierMat);
            }

            // 8. Universal Braking Distance Marker Boards (150m, 100m, 50m)
            BuildBrakingDistanceMarkers(trackRoot.transform, centerline, trackWidth, brakeBoardMat, whiteLineMat);

            // 9. FIA Marshall Safety Posts & Digital LED Flag Panels
            BuildMarshallPostsAndLedPanels(trackRoot.transform, centerline, trackWidth, marshallPostMat, gantryMat, flagLedGreen);

            // 10. Overhead Sponsor Truss Bridges (Rolex, Pirelli, Aramco)
            BuildOverheadSponsorBridges(trackRoot.transform, centerline, trackWidth, gantryMat, rolexGreenMat, rolexGoldMat, pirelliYellowMat);

            // 11. Energy-Absorbing Tire Barrier Stacks
            BuildEnergyAbsorbingTireStacks(trackRoot.transform, centerline, trackWidth, tireStackMat, barrierMat);

            // 12. Circuit-Specific World Landmark Features
            if (circuitType == CircuitType.Spa)
            {
                BuildSpaIconicLandmarks(trackRoot.transform, centerline, trackWidth, grandstandMat, grandstandRoofMat, tentFabricMat, teamRedMat, teamBlueMat);
                BuildDenseArdennesPineForest(trackRoot.transform, centerline, trackWidth, treeTrunkMat, treeLeafMat);
            }
            else if (circuitType == CircuitType.Bahrain)
            {
                BuildBahrainIconicLandmarks(trackRoot.transform, sfPos, grandstandMat, grandstandRoofMat, rolexGoldMat, tentFabricMat);
                BuildSakhirPalmOasis(trackRoot.transform, centerline, trackWidth, palmTrunkMat, palmLeafMat);
                BuildCircuitFloodlights(trackRoot.transform, centerline, trackWidth, floodlightMat, lightEmissiveMat);
            }
            else if (circuitType == CircuitType.Monza)
            {
                BuildMonzaIconicLandmarks(trackRoot.transform, centerline, trackWidth, historicOvalMat, grandstandMat, grandstandRoofMat);
                BuildMonzaRoyalParkForest(trackRoot.transform, centerline, trackWidth, treeTrunkMat, oakLeafMat);
            }
            else // Monaco
            {
                BuildMonacoIconicLandmarks(trackRoot.transform, centerline, trackWidth, tunnelWallMat, lightEmissiveMat, seaWaterMat, yachtWhiteMat, palmTrunkMat, palmLeafMat);
            }

            Debug.Log($"[TrackVisualBuilder] Built Ultra-Realistic Circuit: {circuitType} ({centerline.Count} waypoints, zero overlaps, full trackside infrastructure)");
            return trackRoot;
        }

        public static List<Vector3> GenerateBahrainGrandPrixSpline()
        {
            List<Vector3> pts = new List<Vector3>();
            pts.Add(new Vector3(0f, 0f, 0f));

            Vector3 cur = AddLine(pts, new Vector3(0f, 0f, 0f), 0f, 1040f, 8.0f);
            float h = 0.0f;

            cur = AddArc(pts, cur, ref h, 130.0f, 42.0f, 3.0f);
            cur = AddArc(pts, cur, ref h, -70.0f, 58.0f, 3.5f);
            cur = AddArc(pts, cur, ref h, 40.0f, 85.0f, 4.0f);
            cur = AddLine(pts, cur, h, 450.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 90.0f, 75.0f, 4.0f);
            cur = AddLine(pts, cur, h, 135.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -45.0f, 115.0f, 4.0f);
            cur = AddArc(pts, cur, ref h, 90.0f, 100.0f, 4.0f);
            cur = AddArc(pts, cur, ref h, -45.0f, 115.0f, 4.0f);
            cur = AddLine(pts, cur, h, 195.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 130.0f, 45.0f, 3.0f);
            cur = AddLine(pts, cur, h, 295.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -55.0f, 75.0f, 4.0f);
            cur = AddArc(pts, cur, ref h, -90.0f, 45.0f, 3.0f);
            cur = AddLine(pts, cur, h, 715.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -65.0f, 90.0f, 4.0f);
            cur = AddLine(pts, cur, h, 175.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 45.0f, 145.0f, 5.0f);
            cur = AddLine(pts, cur, h, 135.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 80.0f, 70.0f, 4.0f);

            float r15 = 85.0f;
            Vector2 t15_entry = new Vector2(r15, -r15);
            float dx = t15_entry.x - cur.x;
            float dz = t15_entry.y - cur.z;
            float ret_heading = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            if (ret_heading < 0) ret_heading += 360.0f;

            float turn_in = ret_heading - h;
            if (turn_in > 180f) turn_in -= 360f;
            if (turn_in < -180f) turn_in += 360f;
            cur = AddArc(pts, cur, ref h, turn_in, 70.0f, 4.0f);

            float distToEntry = Mathf.Sqrt((t15_entry.x - cur.x) * (t15_entry.x - cur.x) + (t15_entry.y - cur.z) * (t15_entry.y - cur.z));
            cur = AddLine(pts, cur, h, distToEntry, 8.0f);

            float turn_14 = 270.0f - h;
            if (turn_14 > 180f) turn_14 -= 360f;
            if (turn_14 < -180f) turn_14 += 360f;
            cur = AddArc(pts, cur, ref h, turn_14, 55.0f, 3.5f);
            cur = AddArc(pts, cur, ref h, 90.0f, r15, 4.0f);
            pts[pts.Count - 1] = new Vector3(0f, 0f, 0f);

            ApplyElevationProfile(pts, new (float, float)[]
            {
                (0.00f, 0.0f),
                (0.18f, 0.0f),
                (0.24f, 4.5f),
                (0.30f, 15.0f),
                (0.40f, 6.0f),
                (0.48f, 2.0f),
                (0.55f, 4.5f),
                (0.62f, 0.5f),
                (0.72f, 1.0f),
                (0.82f, 5.5f),
                (0.90f, 3.0f),
                (1.00f, 0.0f)
            });

            return pts;
        }

        public static List<Vector3> GenerateSpaFrancorchampsSpline()
        {
            List<Vector3> pts = new List<Vector3>();
            pts.Add(new Vector3(0f, 0f, 0f));

            Vector3 cur = AddLine(pts, new Vector3(0f, 0f, 0f), 0f, 420f, 8.0f);
            float h = 0.0f;

            cur = AddArc(pts, cur, ref h, 140.0f, 30.0f, 3.0f);
            cur = AddLine(pts, cur, h, 420.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -45.0f, 110.0f, 4.0f);
            cur = AddArc(pts, cur, ref h, 70.0f, 130.0f, 4.0f);
            cur = AddArc(pts, cur, ref h, -125.0f, 150.0f, 4.0f);
            cur = AddLine(pts, cur, h, 1000.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 85.0f, 48.0f, 3.5f);
            cur = AddArc(pts, cur, ref h, -85.0f, 48.0f, 3.5f);
            cur = AddArc(pts, cur, ref h, 75.0f, 75.0f, 4.0f);
            cur = AddLine(pts, cur, h, 240.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 170.0f, 44.0f, 3.5f);
            cur = AddLine(pts, cur, h, 130.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -80.0f, 58.0f, 4.0f);
            cur = AddLine(pts, cur, h, 220.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -110.0f, 115.0f, 4.5f);
            cur = AddLine(pts, cur, h, 250.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 85.0f, 52.0f, 3.5f);
            cur = AddArc(pts, cur, ref h, -85.0f, 52.0f, 3.5f);
            cur = AddLine(pts, cur, h, 150.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, 80.0f, 62.0f, 4.0f);
            cur = AddArc(pts, cur, ref h, 95.0f, 80.0f, 4.0f);
            cur = AddLine(pts, cur, h, 600.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -30.0f, 220.0f, 5.0f);
            cur = AddLine(pts, cur, h, 320.0f, 8.0f);
            cur = AddArc(pts, cur, ref h, -35.0f, 240.0f, 5.0f);

            Vector2 bus_entry = new Vector2(-20.0f, -60.0f);
            float dx = bus_entry.x - cur.x;
            float dz = bus_entry.y - cur.z;
            float aim_h = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            if (aim_h < 0) aim_h += 360.0f;

            float delta_turn = aim_h - h;
            if (delta_turn > 180f) delta_turn -= 360f;
            if (delta_turn < -180f) delta_turn += 360f;
            cur = AddArc(pts, cur, ref h, delta_turn, 90.0f, 4.0f);

            float dist_to_entry = Mathf.Sqrt((bus_entry.x - cur.x) * (bus_entry.x - cur.x) + (bus_entry.y - cur.z) * (bus_entry.y - cur.z));
            cur = AddLine(pts, cur, h, dist_to_entry, 8.0f);

            float turn_straight = 0.0f - h;
            if (turn_straight > 180f) turn_straight -= 360f;
            if (turn_straight < -180f) turn_straight += 360f;

            cur = AddArc(pts, cur, ref h, turn_straight + 65.0f, 26.0f, 3.0f);
            cur = AddArc(pts, cur, ref h, -65.0f, 26.0f, 3.0f);

            float lead_dist = Mathf.Sqrt(cur.x * cur.x + cur.z * cur.z);
            if (lead_dist > 0.5f)
            {
                cur = AddLine(pts, cur, 0.0f, lead_dist, 8.0f);
            }

            pts[pts.Count - 1] = new Vector3(0f, 0f, 0f);

            ApplyElevationProfile(pts, new (float, float)[]
            {
                (0.00f, 0.0f),
                (0.06f, 0.0f),
                (0.12f, 0.0f),
                (0.18f, 28.0f),
                (0.32f, 65.0f),
                (0.40f, 60.0f),
                (0.48f, 32.0f),
                (0.58f, 14.0f),
                (0.68f, 8.0f),
                (0.75f, 2.0f),
                (0.90f, 1.0f),
                (0.96f, 0.0f),
                (1.00f, 0.0f)
            });

            return pts;
        }

        public static List<Vector3> GenerateMonzaGrandPrixSpline()
        {
            float scale = 1.855f;
            Vector2[] anchorsRaw = new Vector2[]
            {
                // Start/Finish Straight (Rettifilo Tribune) - Heading West
                new Vector2(0.0f, 0.0f),         // 0: Start/Finish Line
                new Vector2(-120.0f, 0.0f),      // 1: Mid Straight
                new Vector2(-250.0f, 0.0f),      // 2: Speed Trap
                new Vector2(-340.0f, 0.0f),      // 3: T1 Heavy Braking Zone (350 km/h)
                
                // Turns 1 & 2: Variante del Rettifilo (Authentic Smooth Right-Left Chicane)
                new Vector2(-375.0f, 4.0f),      // 4: T1 Entry Right
                new Vector2(-400.0f, 20.0f),     // 5: T1 Apex Right
                new Vector2(-420.0f, 32.0f),     // 6: T2 Mid Chicane
                new Vector2(-445.0f, 38.0f),     // 7: T2 Apex Left
                new Vector2(-485.0f, 40.0f),     // 8: T2 Exit & Curva Grande Entry
                
                // Turn 3: Curva Grande / Biassono (Sweeping Right arc into the park)
                new Vector2(-555.0f, 52.0f),     // 9: Curva Grande Mid
                new Vector2(-625.0f, 85.0f),     // 10: Curva Grande Apex Arc
                new Vector2(-675.0f, 150.0f),    // 11: Curva Grande Exit
                
                // Run to Variante della Roggia
                new Vector2(-695.0f, 230.0f),    // 12: Roggia Approach
                new Vector2(-710.0f, 315.0f),    // 13: Roggia Braking
                
                // Turns 4 & 5: Variante della Roggia (Left-Right Chicane)
                new Vector2(-722.0f, 355.0f),    // 14: T4 Roggia Entry Left
                new Vector2(-732.0f, 372.0f),    // 15: T4 Apex Left
                new Vector2(-726.0f, 392.0f),    // 16: T5 Apex Right
                new Vector2(-732.0f, 412.0f),    // 17: T5 Exit
                
                // Run to Curve di Lesmo
                new Vector2(-740.0f, 475.0f),    // 18: Lesmo Approach
                new Vector2(-745.0f, 530.0f),    // 19: T6 Lesmo 1 Braking
                
                // Turn 6: Prima Variante di Lesmo (Lesmo 1 - 90 deg Right)
                new Vector2(-735.0f, 555.0f),    // 20: T6 Lesmo 1 Entry
                new Vector2(-705.0f, 575.0f),    // 21: T6 Lesmo 1 Apex Right
                new Vector2(-665.0f, 580.0f),    // 22: T6 Lesmo 1 Exit
                
                // Link to Lesmo 2
                new Vector2(-605.0f, 585.0f),    // 23: Lesmo Link
                new Vector2(-560.0f, 590.0f),    // 24: T7 Lesmo 2 Braking
                
                // Turn 7: Seconda Variante di Lesmo (Lesmo 2 - 90 deg Right onto Serraglio Straight)
                new Vector2(-525.0f, 582.0f),    // 25: T7 Lesmo 2 Entry
                new Vector2(-500.0f, 560.0f),    // 26: T7 Lesmo 2 Apex Right
                new Vector2(-475.0f, 520.0f),    // 27: T7 Lesmo 2 Exit
                
                // Curva del Serraglio & DRS Straight (Passing under Old Oval Banked Bridge)
                new Vector2(-410.0f, 435.0f),    // 28: Serraglio Straight Upper
                new Vector2(-340.0f, 345.0f),    // 29: Serraglio Straight Mid (Old Oval Underpass)
                new Vector2(-270.0f, 255.0f),    // 30: Serraglio Straight Lower
                new Vector2(-210.0f, 180.0f),    // 31: Serraglio Kink
                new Vector2(-160.0f, 125.0f),    // 32: Ascari Braking (DRS 1 Exit)
                
                // Turns 8, 9, 10: Variante Ascari (Left-Right-Left High Speed Complex)
                new Vector2(-130.0f, 98.0f),     // 33: T8 Ascari Entry Left
                new Vector2(-112.0f, 88.0f),     // 34: T8 Ascari Apex Left
                new Vector2(-85.0f, 94.0f),      // 35: T9 Ascari Apex Right
                new Vector2(-58.0f, 80.0f),      // 36: T10 Ascari Apex Left
                new Vector2(-32.0f, 66.0f),      // 37: T10 Ascari Exit
                
                // Rettifilo Centrale (Back Straight / Opposite Straight) - Heading East
                new Vector2(50.0f, 62.0f),       // 38: Back Straight Entry
                new Vector2(160.0f, 60.0f),      // 39: Back Straight Mid (340 km/h)
                new Vector2(270.0f, 58.0f),      // 40: Back Straight Upper
                new Vector2(370.0f, 55.0f),      // 41: Parabolica Braking Zone
                
                // Turn 11: Curva Parabolica / Curva Alboreto (180 deg Increasing-Radius Right Turn)
                new Vector2(420.0f, 50.0f),      // 42: Parabolica Entry Right
                new Vector2(455.0f, 30.0f),      // 43: Parabolica Tight Apex
                new Vector2(465.0f, -5.0f),      // 44: Parabolica Apex
                new Vector2(450.0f, -35.0f),     // 45: Parabolica Opening Radius
                new Vector2(410.0f, -50.0f),     // 46: Parabolica Sweeping Exit
                new Vector2(340.0f, -45.0f),     // 47: Parabolica Acceleration Zone
                new Vector2(240.0f, -25.0f),     // 48: Main Straight Entry Arc
                new Vector2(130.0f, -5.0f)       // 49: Joining Start/Finish Straight
            };

            int nAnchors = anchorsRaw.Length;
            Vector2[] scaledAnchors = new Vector2[nAnchors];
            for (int i = 0; i < nAnchors; i++)
            {
                scaledAnchors[i] = anchorsRaw[i] * scale;
            }

            List<Vector3> pts = new List<Vector3>();
            int samplesPerSeg = 18;

            for (int i = 0; i < nAnchors; i++)
            {
                Vector2 p0 = scaledAnchors[(i - 1 + nAnchors) % nAnchors];
                Vector2 p1 = scaledAnchors[i];
                Vector2 p2 = scaledAnchors[(i + 1) % nAnchors];
                Vector2 p3 = scaledAnchors[(i + 2) % nAnchors];

                for (int s = 0; s < samplesPerSeg; s++)
                {
                    float t = s / (float)samplesPerSeg;
                    Vector2 pt2D = CatmullRom2D(p0, p1, p2, p3, t);
                    pts.Add(new Vector3(pt2D.x, 0f, pt2D.y));
                }
            }
            pts.Add(new Vector3(pts[0].x, 0f, pts[0].z));

            // Apply Monza Gentle Elevation Profile (+8m at Lesmo, 0m at Rettifilo)
            ApplyElevationProfile(pts, new (float, float)[]
            {
                (0.00f, 0.0f),    // Start/Finish Straight (Rettifilo Tribune)
                (0.10f, 0.0f),    // T1/T2 Variante del Rettifilo
                (0.20f, 2.5f),    // T3 Curva Grande
                (0.32f, 4.0f),    // T4/T5 Variante della Roggia
                (0.44f, 8.0f),    // T6/T7 Curve di Lesmo (Peak +8m)
                (0.58f, 3.5f),    // Curva del Serraglio (Under Old Banked Oval)
                (0.68f, 2.0f),    // T8/T9/T10 Variante Ascari
                (0.80f, 1.0f),    // Rettifilo Centrale Back Straight
                (0.92f, 0.0f),    // T11 Curva Parabolica
                (1.00f, 0.0f)     // Main Straight Closure
            });

            return pts;
        }

        public static List<Vector3> GenerateMonacoGrandPrixSpline()
        {
            float scale = 1.70f;
            Vector2[] anchorsRaw = new Vector2[]
            {
                // Start/Finish & Sainte Dévote
                new Vector2(0f, 0f),       // 0: Start Grid
                new Vector2(0f, 60f),      // 1: Start/Finish line (Z=100m)
                new Vector2(0f, 150f),     // 2: Main Straight
                new Vector2(0f, 240f),     // 3: Sainte Devote Braking
                new Vector2(5f, 270f),     // 4: T1 Sainte Devote Entry
                new Vector2(25f, 285f),    // 5: T1 Sainte Devote Apex
                new Vector2(55f, 290f),    // 6: T1 Exit
                
                // Beau Rivage Uphill Climb (Heading East at high elevation Z=305..335m)
                new Vector2(120f, 305f),   // 7: Beau Rivage Lower
                new Vector2(190f, 320f),   // 8: Beau Rivage Mid
                new Vector2(260f, 335f),   // 9: Beau Rivage Upper
                
                // Massenet & Casino Square
                new Vector2(315f, 360f),   // 10: T3 Massenet Entry
                new Vector2(335f, 395f),   // 11: T3 Massenet Apex
                new Vector2(325f, 430f),   // 12: T3 Massenet Exit
                new Vector2(320f, 460f),   // 13: T4 Casino Square Entry
                new Vector2(345f, 480f),   // 14: T4 Casino Square Crest (+42m summit)
                new Vector2(380f, 485f),   // 15: T4 Casino Exit
                
                // Mirabeau Haute & Fairmont Hairpin (Descending South)
                new Vector2(420f, 480f),   // 16: Run to Mirabeau
                new Vector2(450f, 465f),   // 17: T5 Mirabeau Haute Apex
                new Vector2(435f, 430f),   // 18: Downhill Plunge to Hairpin
                new Vector2(410f, 400f),   // 19: T6 Fairmont Hairpin Entry
                new Vector2(375f, 390f),   // 20: T6 Fairmont Hairpin Apex (180 deg)
                new Vector2(390f, 370f),   // 21: T6 Hairpin Exit (Heads East at lower latitude)
                
                // Mirabeau Bas & Portier (Lower Latitude Z=350..365m)
                new Vector2(430f, 365f),   // 22: T7 Mirabeau Bas Apex
                new Vector2(475f, 360f),   // 23: Link to Portier
                new Vector2(515f, 350f),   // 24: T8 Portier Entry
                new Vector2(520f, 320f),   // 25: T8 Portier Apex (Entering Tunnel)
                
                // The Tunnel (Dropping to Sea Level Y=0m, heading West-South-West)
                new Vector2(480f, 275f),   // 26: Tunnel Upper Arc
                new Vector2(425f, 235f),   // 27: Tunnel Mid
                new Vector2(360f, 205f),   // 28: Tunnel Lower
                new Vector2(290f, 195f),   // 29: Tunnel Exit
                
                // Nouvelle Chicane & Harbor Straight (Z=175..190m, 200m South of Beau Rivage!)
                new Vector2(230f, 190f),   // 30: T10 Nouvelle Chicane Entry
                new Vector2(215f, 180f),   // 31: T10 Chicane Left
                new Vector2(195f, 185f),   // 32: T11 Chicane Right
                new Vector2(150f, 180f),   // 33: Harbor Straight
                new Vector2(90f, 175f),    // 34: Quayside
                
                // Tabac & Swimming Pool (Running South along Marina)
                new Vector2(55f, 165f),    // 35: T12 Tabac Apex
                new Vector2(48f, 145f),    // 36: Run to Swimming Pool
                new Vector2(45f, 120f),    // 37: T13 Louis Chiron Entry
                new Vector2(35f, 95f),     // 38: T13 Left
                new Vector2(45f, 75f),     // 39: T14 Right
                new Vector2(45f, 50f),     // 40: Swimming Pool Mid Straight
                new Vector2(35f, 35f),     // 41: T15 Exit Chicane Right
                new Vector2(45f, 20f),     // 42: T16 Exit Chicane Left
                
                // La Rascasse & Antony Noghès
                new Vector2(45f, 10f),     // 43: Approach Rascasse
                new Vector2(40f, -5f),     // 44: T17 Rascasse Entry
                new Vector2(25f, -12f),    // 45: T17 Rascasse Apex (145 deg)
                new Vector2(12f, -8f),     // 46: T17 Rascasse Exit
                new Vector2(6f, -4f),      // 47: Link to Antony Noghes
                new Vector2(2f, -1f)       // 48: T18 Antony Noghes Apex
            };

            int nAnchors = anchorsRaw.Length;
            Vector2[] scaledAnchors = new Vector2[nAnchors];
            for (int i = 0; i < nAnchors; i++)
            {
                scaledAnchors[i] = anchorsRaw[i] * scale;
            }

            List<Vector3> pts = new List<Vector3>();
            int samplesPerSeg = 18;

            for (int i = 0; i < nAnchors; i++)
            {
                Vector2 p0 = scaledAnchors[(i - 1 + nAnchors) % nAnchors];
                Vector2 p1 = scaledAnchors[i];
                Vector2 p2 = scaledAnchors[(i + 1) % nAnchors];
                Vector2 p3 = scaledAnchors[(i + 2) % nAnchors];

                for (int s = 0; s < samplesPerSeg; s++)
                {
                    float t = s / (float)samplesPerSeg;
                    Vector2 pt2D = CatmullRom2D(p0, p1, p2, p3, t);
                    pts.Add(new Vector3(pt2D.x, 0f, pt2D.y));
                }
            }
            pts.Add(new Vector3(pts[0].x, 0f, pts[0].z));

            // Apply 42m Monaco Elevation Profile (Main Straight at Y = 0.0m, Casino at +42.0m)
            ApplyElevationProfile(pts, new (float, float)[]
            {
                (0.00f, 0.0f),    // Start/Finish Straight (Boulevard Albert 1er)
                (0.12f, 3.5f),    // Turn 1 Sainte Dévote
                (0.24f, 28.0f),   // Beau Rivage Uphill Surge
                (0.34f, 42.0f),   // Massenet & Casino Square Summit (+42m)
                (0.42f, 26.0f),   // Mirabeau Haute Downhill Plunge
                (0.48f, 14.0f),   // Fairmont Hairpin
                (0.53f, 5.0f),    // Mirabeau Bas
                (0.58f, 0.0f),    // Portier & Tunnel Entrance (Sea level 0m)
                (0.68f, 0.0f),    // The Tunnel
                (0.74f, 0.0f),    // Nouvelle Chicane
                (0.80f, 0.0f),    // Tabac Quayside
                (0.88f, 0.0f),    // Swimming Pool / Piscine
                (0.96f, 0.0f),    // La Rascasse & Antony Noghès
                (1.00f, 0.0f)     // Main Straight Closure
            });

            return pts;
        }

        private static Vector2 CatmullRom2D(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            float x = 0.5f * ((2f * p1.x) + (-p0.x + p2.x) * t + (2f * p0.x - 5f * p1.x + 4f * p2.x - p3.x) * t2 + (-p0.x + 3f * p1.x - 3f * p2.x + p3.x) * t3);
            float y = 0.5f * ((2f * p1.y) + (-p0.y + p2.y) * t + (2f * p0.y - 5f * p1.y + 4f * p2.y - p3.y) * t2 + (-p0.y + 3f * p1.y - 3f * p2.y + p3.y) * t3);
            return new Vector2(x, y);
        }

        private static void ApplyElevationProfile(List<Vector3> pts, (float u, float y)[] controlPoints)
        {
            int n = pts.Count;
            float totalLen = 0f;
            float[] dists = new float[n];
            dists[0] = 0f;

            for (int i = 0; i < n - 1; i++)
            {
                totalLen += Vector3.Distance(pts[i], pts[i + 1]);
                dists[i + 1] = totalLen;
            }

            for (int i = 0; i < n; i++)
            {
                float u = dists[i] / totalLen;
                float y = controlPoints[0].y;

                for (int k = 0; k < controlPoints.Length - 1; k++)
                {
                    float u0 = controlPoints[k].u;
                    float y0 = controlPoints[k].y;
                    float u1 = controlPoints[k + 1].u;
                    float y1 = controlPoints[k + 1].y;

                    if (u >= u0 && u <= u1)
                    {
                        float t = (u - u0) / Mathf.Max(0.0001f, u1 - u0);
                        float smoothT = t * t * (3.0f - 2.0f * t);
                        y = Mathf.Lerp(y0, y1, smoothT);
                        break;
                    }
                }

                pts[i] = new Vector3(pts[i].x, y, pts[i].z);
            }
        }

        private static Vector3 AddLine(List<Vector3> pts, Vector3 start, float headingDeg, float length, float step)
        {
            float hRad = headingDeg * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(hRad), 0f, Mathf.Cos(hRad));
            int n = Mathf.Max(1, Mathf.RoundToInt(length / step));
            float actualStep = length / n;
            for (int i = 1; i <= n; i++) pts.Add(start + dir * (i * actualStep));
            return pts[pts.Count - 1];
        }

        private static Vector3 AddArc(List<Vector3> pts, Vector3 start, ref float headingDeg, float turnAngleDeg, float radius, float step)
        {
            bool isRight = turnAngleDeg > 0;
            float centerHRad = (headingDeg + (isRight ? 90f : -90f)) * Mathf.Deg2Rad;
            Vector3 center = start + new Vector3(Mathf.Sin(centerHRad) * radius, 0f, Mathf.Cos(centerHRad) * radius);

            float arcLen = Mathf.Abs(turnAngleDeg) * Mathf.Deg2Rad * radius;
            int n = Mathf.Max(2, Mathf.RoundToInt(arcLen / step));

            for (int i = 1; i <= n; i++)
            {
                float t = (float)i / n;
                float curH = headingDeg + turnAngleDeg * t;
                float toPtHRad = (curH + (isRight ? -90f : 90f)) * Mathf.Deg2Rad;
                Vector3 pt = center + new Vector3(Mathf.Sin(toPtHRad) * radius, 0f, Mathf.Cos(toPtHRad) * radius);
                pts.Add(pt);
            }

            headingDeg = (headingDeg + turnAngleDeg) % 360f;
            if (headingDeg < 0) headingDeg += 360f;
            return pts[pts.Count - 1];
        }

        private static void BuildContinuousRoadRibbon(Transform parent, List<Vector3> pts, float width, Material roadMat, Material shoulderMat)
        {
            int n = pts.Count;
            int chunkCount = Mathf.CeilToInt((float)n / 250);

            for (int c = 0; c < chunkCount; c++)
            {
                int startIdx = c * 250;
                int endIdx = Mathf.Min(startIdx + 250, n - 1);
                int segCount = endIdx - startIdx;
                if (segCount < 1) continue;

                Vector3[] verts = new Vector3[(segCount + 1) * 4];
                Vector2[] uvs = new Vector2[(segCount + 1) * 4];
                Vector3[] normals = new Vector3[(segCount + 1) * 4];

                int[] roadTris = new int[segCount * 6];
                int[] shoulderTris = new int[segCount * 12];

                float runningU = 0f;

                for (int i = 0; i <= segCount; i++)
                {
                    int ptIdx = startIdx + i;
                    Vector3 p = pts[ptIdx];
                    Vector3 fwd = (ptIdx < n - 1) ? (pts[ptIdx + 1] - p).normalized : (pts[0] - p).normalized;
                    if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized * (width * 0.5f);

                    if (i > 0)
                    {
                        runningU += Vector3.Distance(pts[ptIdx - 1], p) * 0.15f;
                    }

                    int vBase = i * 4;
                    // v0: Left Shoulder Outer (-16m)
                    verts[vBase + 0] = p - right * 2.0f - Vector3.up * 0.25f;
                    // v1: Left Track Edge (-6m)
                    verts[vBase + 1] = p - right;
                    // v2: Right Track Edge (+6m)
                    verts[vBase + 2] = p + right;
                    // v3: Right Shoulder Outer (+16m)
                    verts[vBase + 3] = p + right * 2.0f - Vector3.up * 0.25f;

                    uvs[vBase + 0] = new Vector2(0f, runningU);
                    uvs[vBase + 1] = new Vector2(0.2f, runningU);
                    uvs[vBase + 2] = new Vector2(0.8f, runningU);
                    uvs[vBase + 3] = new Vector2(1f, runningU);

                    Vector3 norm = Vector3.Cross(fwd, right).normalized;
                    if (norm.y < 0) norm = -norm;
                    normals[vBase + 0] = norm;
                    normals[vBase + 1] = norm;
                    normals[vBase + 2] = norm;
                    normals[vBase + 3] = norm;

                    if (i < segCount)
                    {
                        int tRoad = i * 6;
                        int v = i * 4;
                        roadTris[tRoad + 0] = v + 1;
                        roadTris[tRoad + 1] = v + 5;
                        roadTris[tRoad + 2] = v + 2;
                        roadTris[tRoad + 3] = v + 2;
                        roadTris[tRoad + 4] = v + 5;
                        roadTris[tRoad + 5] = v + 6;

                        int tSh = i * 12;
                        shoulderTris[tSh + 0] = v + 0;
                        shoulderTris[tSh + 1] = v + 4;
                        shoulderTris[tSh + 2] = v + 1;
                        shoulderTris[tSh + 3] = v + 1;
                        shoulderTris[tSh + 4] = v + 4;
                        shoulderTris[tSh + 5] = v + 5;

                        shoulderTris[tSh + 6] = v + 2;
                        shoulderTris[tSh + 7] = v + 6;
                        shoulderTris[tSh + 8] = v + 3;
                        shoulderTris[tSh + 9] = v + 3;
                        shoulderTris[tSh + 10] = v + 6;
                        shoulderTris[tSh + 11] = v + 7;
                    }
                }

                Mesh m = new Mesh { name = $"Road_Chunk_{c}" };
                m.vertices = verts;
                m.uv = uvs;
                m.normals = normals;
                m.subMeshCount = 2;
                m.SetTriangles(roadTris, 0);
                m.SetTriangles(shoulderTris, 1);

                GameObject go = new GameObject($"Road_Chunk_{c}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new Material[] { roadMat, shoulderMat };

                MeshCollider col = go.AddComponent<MeshCollider>();
                col.sharedMesh = m;
            }
        }

        private static void BuildCurvatureBasedKerbs(Transform parent, List<Vector3> pts, float trackW, Material redMat, Material altMat)
        {
            GameObject kerbsRoot = new GameObject("Circuit_Apex_Kerbs");
            kerbsRoot.transform.SetParent(parent, false);

            int n = pts.Count;
            int kerbCount = 0;

            for (int i = 2; i < n - 2; i += 2)
            {
                Vector3 p = pts[i];
                Vector3 fwd1 = (pts[i - 2] - pts[i]).normalized;
                Vector3 fwd2 = (pts[i + 2] - pts[i]).normalized;

                float turnAngle = Vector3.SignedAngle(-fwd1, fwd2, Vector3.up);
                if (Mathf.Abs(turnAngle) < 1.0f) continue;

                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                int apexSide = (turnAngle > 0) ? 1 : -1;
                float segLen = Vector3.Distance(pts[i], pts[i + 1]);

                Vector3 kerbPos = p + right * (apexSide * (trackW * 0.5f + 0.55f)) + Vector3.up * 0.02f;
                GameObject kerb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kerb.name = "ApexKerb";
                kerb.transform.SetParent(kerbsRoot.transform, false);
                kerb.transform.position = kerbPos;
                kerb.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                kerb.transform.localScale = new Vector3(1.1f, 0.05f, Mathf.Min(segLen * 1.05f, 2.5f));

                bool isRed = (kerbCount % 2 == 0);
                kerb.GetComponent<Renderer>().sharedMaterial = isRed ? redMat : altMat;
                UnityEngine.Object.Destroy(kerb.GetComponent<Collider>());
                kerbCount++;
            }
        }

        private static void BuildContinuousBarriers(Transform parent, List<Vector3> pts, float trackW, Material barMat, Material sponMat)
        {
            GameObject barRoot = new GameObject("Circuit_Safety_Barriers");
            barRoot.transform.SetParent(parent, false);

            int n = pts.Count;
            for (int i = 0; i < n - 1; i += 6)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 barPos = p + right * (side * (trackW * 0.5f + 8.5f)) + Vector3.up * 0.65f;

                    bool overlapsTrack = false;
                    for (int k = 0; k < n; k += 3)
                    {
                        if (Mathf.Abs(k - i) <= 2) continue;
                        float d = Vector2.Distance(new Vector2(barPos.x, barPos.z), new Vector2(pts[k].x, pts[k].z));
                        if (d < (trackW * 0.5f + 3.5f))
                        {
                            overlapsTrack = true;
                            break;
                        }
                    }
                    if (overlapsTrack) continue;

                    GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = "SafetyBarrier";
                    bar.transform.SetParent(barRoot.transform, false);
                    bar.transform.position = barPos;
                    bar.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                    bar.transform.localScale = new Vector3(0.4f, 1.3f, 5.8f);

                    bool isSponsor = (i / 6) % 3 == 0;
                    bar.GetComponent<Renderer>().sharedMaterial = isSponsor ? sponMat : barMat;

                    BoxCollider col = bar.GetComponent<BoxCollider>();
                    if (col != null) col.isTrigger = false;
                }
            }
        }

        private static void BuildStartingGridAndGantry(Transform parent, List<Vector3> pts, int sfIdx, float trackW, Material whiteMat, Material yellowMat, Material gantryMat, Material redLightMat)
        {
            Vector3 sfPos = pts[sfIdx];
            Vector3 fwd = (sfIdx < pts.Count - 1) ? (pts[sfIdx + 1] - sfPos).normalized : (pts[0] - sfPos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            GameObject gridRoot = new GameObject("Circuit_Starting_Grid");
            gridRoot.transform.SetParent(parent, false);

            GameObject sfLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sfLine.name = "StartFinish_Line";
            sfLine.transform.SetParent(gridRoot.transform, false);
            sfLine.transform.position = sfPos + Vector3.up * 0.02f;
            sfLine.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            sfLine.transform.localScale = new Vector3(trackW, 0.04f, 1.2f);
            sfLine.GetComponent<Renderer>().sharedMaterial = whiteMat;
            UnityEngine.Object.Destroy(sfLine.GetComponent<Collider>());

            GameObject gantryRoot = new GameObject("FIA_Start_Gantry");
            gantryRoot.transform.SetParent(gridRoot.transform, false);
            gantryRoot.transform.position = sfPos;
            gantryRoot.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            GameObject pLeft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pLeft.name = "Gantry_Pillar_L";
            pLeft.transform.SetParent(gantryRoot.transform, false);
            pLeft.transform.localPosition = new Vector3(-trackW * 0.5f - 2.5f, 4.0f, 0.0f);
            pLeft.transform.localScale = new Vector3(0.5f, 4.0f, 0.5f);
            pLeft.GetComponent<Renderer>().sharedMaterial = gantryMat;
            UnityEngine.Object.Destroy(pLeft.GetComponent<Collider>());

            GameObject pRight = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pRight.name = "Gantry_Pillar_R";
            pRight.transform.SetParent(gantryRoot.transform, false);
            pRight.transform.localPosition = new Vector3(trackW * 0.5f + 2.5f, 4.0f, 0.0f);
            pRight.transform.localScale = new Vector3(0.5f, 4.0f, 0.5f);
            pRight.GetComponent<Renderer>().sharedMaterial = gantryMat;
            UnityEngine.Object.Destroy(pRight.GetComponent<Collider>());

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Gantry_Crossbeam";
            beam.transform.SetParent(gantryRoot.transform, false);
            beam.transform.localPosition = new Vector3(0.0f, 7.8f, 0.0f);
            beam.transform.localScale = new Vector3(trackW + 6.0f, 0.8f, 1.2f);
            beam.GetComponent<Renderer>().sharedMaterial = gantryMat;
            UnityEngine.Object.Destroy(beam.GetComponent<Collider>());

            for (int k = 0; k < 5; k++)
            {
                float lightX = -3.0f + (k * 1.5f);
                GameObject lightPod = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lightPod.name = $"Start_Light_{k + 1}";
                lightPod.transform.SetParent(gantryRoot.transform, false);
                lightPod.transform.localPosition = new Vector3(lightX, 6.8f, -0.4f);
                lightPod.transform.localScale = new Vector3(0.8f, 1.2f, 0.3f);
                lightPod.GetComponent<Renderer>().sharedMaterial = redLightMat;
                UnityEngine.Object.Destroy(lightPod.GetComponent<Collider>());
            }

            for (int i = 0; i < 10; i++)
            {
                float zOffset = -20.0f - (i * 14.0f);
                float xOffset = (i % 2 == 0) ? 2.5f : -2.5f;

                GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Grid_Slot_{i + 1}";
                box.transform.SetParent(gantryRoot.transform, false);
                box.transform.localPosition = new Vector3(xOffset, 0.02f, zOffset);
                box.transform.localScale = new Vector3(2.0f, 0.04f, 4.0f);
                box.GetComponent<Renderer>().sharedMaterial = (i == 0) ? yellowMat : whiteMat;
                UnityEngine.Object.Destroy(box.GetComponent<Collider>());
            }
        }

        private static void BuildDetailedPitComplex(Transform parent, List<Vector3> pts, int sfIdx, float trackW, Material wallMat, Material garageMat, Material gsMat, Material roofMat, Material pitAspMat, Material barMat)
        {
            Vector3 sfPos = pts[sfIdx];
            Vector3 fwd = (sfIdx < pts.Count - 1) ? (pts[sfIdx + 1] - sfPos).normalized : (pts[0] - sfPos).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            GameObject pitRoot = new GameObject("Circuit_PitComplex_Grandstands");
            pitRoot.transform.SetParent(parent, false);
            pitRoot.transform.position = sfPos;
            pitRoot.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            GameObject pitWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pitWall.name = "Pit_Wall";
            pitWall.transform.SetParent(pitRoot.transform, false);
            pitWall.transform.localPosition = new Vector3(trackW * 0.5f + 1.5f, 0.55f, 40.0f);
            pitWall.transform.localScale = new Vector3(0.5f, 1.1f, 320.0f);
            pitWall.GetComponent<Renderer>().sharedMaterial = wallMat;

            GameObject pitRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pitRoad.name = "Pit_Lane_Road";
            pitRoad.transform.SetParent(pitRoot.transform, false);
            pitRoad.transform.localPosition = new Vector3(trackW * 0.5f + 6.5f, 0.01f, 40.0f);
            pitRoad.transform.localScale = new Vector3(9.0f, 0.02f, 320.0f);
            pitRoad.GetComponent<Renderer>().sharedMaterial = pitAspMat;

            GameObject garage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            garage.name = "Pit_Garages";
            garage.transform.SetParent(pitRoot.transform, false);
            garage.transform.localPosition = new Vector3(trackW * 0.5f + 18.0f, 2.25f, 40.0f);
            garage.transform.localScale = new Vector3(14.0f, 4.5f, 280.0f);
            garage.GetComponent<Renderer>().sharedMaterial = garageMat;

            GameObject gs = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gs.name = "Main_Grandstand_Seating";
            gs.transform.SetParent(pitRoot.transform, false);
            gs.transform.localPosition = new Vector3(-trackW * 0.5f - 18.0f, 2.5f, 0.0f);
            gs.transform.localScale = new Vector3(14.0f, 5.0f, 240.0f);
            gs.GetComponent<Renderer>().sharedMaterial = gsMat;

            GameObject gsRoof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gsRoof.name = "Grandstand_Canopy";
            gsRoof.transform.SetParent(pitRoot.transform, false);
            gsRoof.transform.localPosition = new Vector3(-trackW * 0.5f - 14.0f, 6.2f, 0.0f);
            gsRoof.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, -12.0f);
            gsRoof.transform.localScale = new Vector3(16.0f, 0.4f, 240.0f);
            gsRoof.GetComponent<Renderer>().sharedMaterial = roofMat;
            UnityEngine.Object.Destroy(gsRoof.GetComponent<Collider>());
        }

        private static void BuildBrakingDistanceMarkers(Transform parent, List<Vector3> pts, float trackW, Material boardMat, Material textMat)
        {
            GameObject root = new GameObject("Circuit_Braking_Markers");
            root.transform.SetParent(parent, false);

            int n = pts.Count;
            int[] markerIndices = new int[] { (int)(n * 0.10f), (int)(n * 0.25f), (int)(n * 0.45f), (int)(n * 0.70f), (int)(n * 0.90f) };

            for (int m = 0; m < markerIndices.Length; m++)
            {
                int apexIdx = markerIndices[m];
                if (apexIdx >= n - 15) continue;

                for (int d = 3; d >= 1; d--)
                {
                    int ptIdx = Mathf.Max(0, apexIdx - (d * 5));
                    Vector3 p = pts[ptIdx];
                    Vector3 fwd = (pts[ptIdx + 1] - p).normalized;
                    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                    Vector3 boardPos = p - right * (trackW * 0.5f + 2.8f) + Vector3.up * 0.85f;

                    GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    board.name = $"BrakeMarker_{d * 50}m";
                    board.transform.SetParent(root.transform, false);
                    board.transform.position = boardPos;
                    board.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                    board.transform.localScale = new Vector3(0.15f, 1.4f, 0.9f);
                    board.GetComponent<Renderer>().sharedMaterial = boardMat;
                    UnityEngine.Object.Destroy(board.GetComponent<Collider>());

                    GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    bar.name = "Stripe";
                    bar.transform.SetParent(board.transform, false);
                    bar.transform.localPosition = new Vector3(0.55f, 0.0f, 0.0f);
                    bar.transform.localScale = new Vector3(0.2f, 0.25f * d, 0.8f);
                    bar.GetComponent<Renderer>().sharedMaterial = textMat;
                    UnityEngine.Object.Destroy(bar.GetComponent<Collider>());
                }
            }
        }

        private static void BuildMarshallPostsAndLedPanels(Transform parent, List<Vector3> pts, float trackW, Material postMat, Material metalMat, Material ledMat)
        {
            GameObject root = new GameObject("Circuit_Marshall_Posts_LEDs");
            root.transform.SetParent(parent, false);

            for (int i = 25; i < pts.Count - 10; i += 50)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                Vector3 hutPos = p + right * (trackW * 0.5f + 14.0f);
                GameObject hut = new GameObject($"MarshallPost_{i}");
                hut.transform.SetParent(root.transform, false);
                hut.transform.position = hutPos;
                hut.transform.rotation = Quaternion.LookRotation(-right, Vector3.up);

                GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cabin.transform.SetParent(hut.transform, false);
                cabin.transform.localPosition = new Vector3(0.0f, 1.8f, 0.0f);
                cabin.transform.localScale = new Vector3(2.4f, 2.2f, 2.4f);
                cabin.GetComponent<Renderer>().sharedMaterial = postMat;
                UnityEngine.Object.Destroy(cabin.GetComponent<Collider>());

                Vector3 ledPos = p - right * (trackW * 0.5f + 1.8f) + Vector3.up * 1.5f;
                GameObject ledPanel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ledPanel.name = $"LED_FlagPanel_{i}";
                ledPanel.transform.SetParent(root.transform, false);
                ledPanel.transform.position = ledPos;
                ledPanel.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                ledPanel.transform.localScale = new Vector3(0.2f, 0.9f, 1.6f);
                ledPanel.GetComponent<Renderer>().sharedMaterial = ledMat;
                UnityEngine.Object.Destroy(ledPanel.GetComponent<Collider>());
            }
        }

        private static void BuildOverheadSponsorBridges(Transform parent, List<Vector3> pts, float trackW, Material metalMat, Material rolexMat, Material goldMat, Material pirelliMat)
        {
            GameObject root = new GameObject("Circuit_Overhead_Sponsor_Bridges");
            root.transform.SetParent(parent, false);

            int n = pts.Count;
            int[] bridgeIndices = new int[] { (int)(n * 0.28f), (int)(n * 0.65f), (int)(n * 0.88f) };

            for (int b = 0; b < bridgeIndices.Length; b++)
            {
                int idx = bridgeIndices[b];
                if (idx >= n - 1) continue;

                Vector3 p = pts[idx];
                Vector3 fwd = (pts[idx + 1] - p).normalized;

                GameObject bridge = new GameObject($"SponsorBridge_{b + 1}");
                bridge.transform.SetParent(root.transform, false);
                bridge.transform.position = p;
                bridge.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

                GameObject pL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pL.transform.SetParent(bridge.transform, false);
                pL.transform.localPosition = new Vector3(-trackW * 0.5f - 4.5f, 4.5f, 0.0f);
                pL.transform.localScale = new Vector3(0.6f, 4.5f, 0.6f);
                pL.GetComponent<Renderer>().sharedMaterial = metalMat;
                UnityEngine.Object.Destroy(pL.GetComponent<Collider>());

                GameObject pR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pR.transform.SetParent(bridge.transform, false);
                pR.transform.localPosition = new Vector3(trackW * 0.5f + 4.5f, 4.5f, 0.0f);
                pR.transform.localScale = new Vector3(0.6f, 4.5f, 0.6f);
                pR.GetComponent<Renderer>().sharedMaterial = metalMat;
                UnityEngine.Object.Destroy(pR.GetComponent<Collider>());

                GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arch.transform.SetParent(bridge.transform, false);
                arch.transform.localPosition = new Vector3(0.0f, 8.2f, 0.0f);
                arch.transform.localScale = new Vector3(trackW + 10.0f, 1.8f, 2.2f);
                arch.GetComponent<Renderer>().sharedMaterial = (b % 2 == 0) ? rolexMat : pirelliMat;
                UnityEngine.Object.Destroy(arch.GetComponent<Collider>());

                GameObject badge = GameObject.CreatePrimitive(PrimitiveType.Cube);
                badge.transform.SetParent(arch.transform, false);
                badge.transform.localPosition = new Vector3(0.0f, 0.0f, -0.55f);
                badge.transform.localScale = new Vector3(0.4f, 0.5f, 0.1f);
                badge.GetComponent<Renderer>().sharedMaterial = goldMat;
                UnityEngine.Object.Destroy(badge.GetComponent<Collider>());
            }
        }

        private static void BuildEnergyAbsorbingTireStacks(Transform parent, List<Vector3> pts, float trackW, Material tireMat, Material barMat)
        {
            GameObject root = new GameObject("Circuit_Tire_Barrier_Stacks");
            root.transform.SetParent(parent, false);

            for (int i = 15; i < pts.Count - 10; i += 28)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 stackPos = p + right * (side * (trackW * 0.5f + 9.5f)) + Vector3.up * 0.70f;

                    GameObject stack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    stack.name = "TecPro_TireBuffer";
                    stack.transform.SetParent(root.transform, false);
                    stack.transform.position = stackPos;
                    stack.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                    stack.transform.localScale = new Vector3(0.8f, 1.4f, 5.2f);
                    stack.GetComponent<Renderer>().sharedMaterial = tireMat;
                    UnityEngine.Object.Destroy(stack.GetComponent<Collider>());
                }
            }
        }

        private static void BuildSpaIconicLandmarks(Transform parent, List<Vector3> pts, float trackW, Material gsMat, Material roofMat, Material tentMat, Material redTeamMat, Material blueTeamMat)
        {
            GameObject root = new GameObject("Spa_World_Landmarks");
            root.transform.SetParent(parent, false);

            if (pts.Count > 180)
            {
                Vector3 erPos = pts[160];
                Vector3 erFwd = (pts[165] - erPos).normalized;
                Vector3 erRight = Vector3.Cross(Vector3.up, erFwd).normalized;

                GameObject erGrandstand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                erGrandstand.name = "EauRouge_Hillside_Grandstand";
                erGrandstand.transform.SetParent(root.transform, false);
                erGrandstand.transform.position = erPos - erRight * 42.0f + Vector3.up * 8.0f;
                erGrandstand.transform.rotation = Quaternion.LookRotation(erRight, Vector3.up);
                erGrandstand.transform.localScale = new Vector3(18.0f, 12.0f, 120.0f);
                erGrandstand.GetComponent<Renderer>().sharedMaterial = gsMat;
            }

            if (pts.Count > 350)
            {
                Vector3 kPos = pts[320];
                Vector3 kFwd = (pts[325] - kPos).normalized;
                Vector3 kRight = Vector3.Cross(Vector3.up, kFwd).normalized;

                for (int t = 0; t < 6; t++)
                {
                    Vector3 tentPos = kPos + kRight * 45.0f + (kFwd * (t * 22.0f)) + Vector3.up * 2.0f;

                    GameObject tent = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tent.name = $"VIP_Marquee_{t + 1}";
                    tent.transform.SetParent(root.transform, false);
                    tent.transform.position = tentPos;
                    tent.transform.rotation = Quaternion.LookRotation(kFwd, Vector3.up);
                    tent.transform.localScale = new Vector3(14.0f, 4.0f, 14.0f);
                    tent.GetComponent<Renderer>().sharedMaterial = (t % 2 == 0) ? redTeamMat : blueTeamMat;
                }
            }

            if (pts.Count > 650)
            {
                Vector3 pPos = pts[620];
                Vector3 pFwd = (pts[625] - pPos).normalized;
                Vector3 pRight = Vector3.Cross(Vector3.up, pFwd).normalized;

                GameObject pouhonGs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pouhonGs.name = "Pouhon_Spectator_Grandstand";
                pouhonGs.transform.SetParent(root.transform, false);
                pouhonGs.transform.position = pPos + pRight * 38.0f + Vector3.up * 6.0f;
                pouhonGs.transform.rotation = Quaternion.LookRotation(-pRight, Vector3.up);
                pouhonGs.transform.localScale = new Vector3(16.0f, 8.0f, 140.0f);
                pouhonGs.GetComponent<Renderer>().sharedMaterial = gsMat;
            }
        }

        private static void BuildBahrainIconicLandmarks(Transform parent, Vector3 sfPos, Material gsMat, Material roofMat, Material goldMat, Material fabricMat)
        {
            GameObject root = new GameObject("Bahrain_World_Landmarks");
            root.transform.SetParent(parent, false);

            GameObject towerRoot = new GameObject("Al_Sakhir_VIP_Tower");
            towerRoot.transform.SetParent(root.transform, false);
            towerRoot.transform.position = new Vector3(75.0f, sfPos.y, 80.0f);

            for (int floor = 0; floor < 8; floor++)
            {
                float radius = 24.0f - (floor * 1.2f);
                float floorY = 2.5f + (floor * 4.5f);

                GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                deck.name = $"Floor_{floor + 1}";
                deck.transform.SetParent(towerRoot.transform, false);
                deck.transform.localPosition = new Vector3(0.0f, floorY, 0.0f);
                deck.transform.localScale = new Vector3(radius, 2.0f, radius);
                deck.GetComponent<Renderer>().sharedMaterial = (floor % 2 == 0) ? gsMat : goldMat;
            }

            for (int c = 0; c < 5; c++)
            {
                GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                canopy.name = $"Tensile_Canopy_{c + 1}";
                canopy.transform.SetParent(root.transform, false);
                canopy.transform.position = new Vector3(-45.0f, sfPos.y + 7.5f, 100.0f + (c * 60.0f));
                canopy.transform.rotation = Quaternion.Euler(0.0f, 0.0f, -15.0f);
                canopy.transform.localScale = new Vector3(20.0f, 0.4f, 48.0f);
                canopy.GetComponent<Renderer>().sharedMaterial = fabricMat;
            }
        }

        private static void BuildMonzaIconicLandmarks(Transform parent, List<Vector3> pts, float trackW, Material ovalMat, Material gsMat, Material roofMat)
        {
            GameObject root = new GameObject("Monza_World_Landmarks");
            root.transform.SetParent(parent, false);

            int n = pts.Count;

            // 1. Famous Historic Banked Oval Bridge (Over Serraglio Straight at ~58% distance)
            int bridgeIdx = (int)(n * 0.58f);
            if (bridgeIdx < n - 1)
            {
                Vector3 bPos = pts[bridgeIdx];
                Vector3 bFwd = (pts[bridgeIdx + 1] - bPos).normalized;
                Vector3 bRight = Vector3.Cross(Vector3.up, bFwd).normalized;

                GameObject ovalBridge = new GameObject("Historic_Banked_Oval_Overpass");
                ovalBridge.transform.SetParent(root.transform, false);
                ovalBridge.transform.position = bPos;
                ovalBridge.transform.rotation = Quaternion.LookRotation(bFwd, Vector3.up);

                // High Banked Concrete Arch (45 deg banking)
                GameObject bridgeDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bridgeDeck.name = "Banked_Oval_Deck";
                bridgeDeck.transform.SetParent(ovalBridge.transform, false);
                bridgeDeck.transform.localPosition = new Vector3(0.0f, 9.5f, 0.0f);
                bridgeDeck.transform.localRotation = Quaternion.Euler(0.0f, 75.0f, 38.0f); // 38 deg high speed banking!
                bridgeDeck.transform.localScale = new Vector3(24.0f, 2.5f, trackW + 18.0f);
                bridgeDeck.GetComponent<Renderer>().sharedMaterial = ovalMat;
                UnityEngine.Object.Destroy(bridgeDeck.GetComponent<Collider>());

                // Heavy Concrete Pillars
                GameObject pillarL = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillarL.transform.SetParent(ovalBridge.transform, false);
                pillarL.transform.localPosition = new Vector3(-trackW * 0.5f - 4.0f, 4.5f, 0.0f);
                pillarL.transform.localScale = new Vector3(2.5f, 9.0f, 4.0f);
                pillarL.GetComponent<Renderer>().sharedMaterial = ovalMat;
                UnityEngine.Object.Destroy(pillarL.GetComponent<Collider>());

                GameObject pillarR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillarR.transform.SetParent(ovalBridge.transform, false);
                pillarR.transform.localPosition = new Vector3(trackW * 0.5f + 4.0f, 4.5f, 0.0f);
                pillarR.transform.localScale = new Vector3(2.5f, 9.0f, 4.0f);
                pillarR.GetComponent<Renderer>().sharedMaterial = ovalMat;
                UnityEngine.Object.Destroy(pillarR.GetComponent<Collider>());
            }

            // 2. Curva Parabolica Spectator Grandstand (at ~92% distance)
            int paraIdx = (int)(n * 0.92f);
            if (paraIdx < n - 1)
            {
                Vector3 pPos = pts[paraIdx];
                Vector3 pFwd = (pts[paraIdx + 1] - pPos).normalized;
                Vector3 pRight = Vector3.Cross(Vector3.up, pFwd).normalized;

                GameObject paraGs = GameObject.CreatePrimitive(PrimitiveType.Cube);
                paraGs.name = "Parabolica_Grandstand";
                paraGs.transform.SetParent(root.transform, false);
                paraGs.transform.position = pPos + pRight * 36.0f + Vector3.up * 6.0f;
                paraGs.transform.rotation = Quaternion.LookRotation(-pRight, Vector3.up);
                paraGs.transform.localScale = new Vector3(16.0f, 8.0f, 150.0f);
                paraGs.GetComponent<Renderer>().sharedMaterial = gsMat;
            }
        }

        private static void BuildMonzaRoyalParkForest(Transform parent, List<Vector3> pts, float trackW, Material trunkMat, Material foliageMat)
        {
            GameObject forestRoot = new GameObject("Monza_Royal_Park_Woodland");
            forestRoot.transform.SetParent(parent, false);

            int n = pts.Count;
            for (int i = 0; i < n - 1; i += 10)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                for (int layer = 1; layer <= 2; layer++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float lateralDist = trackW * 0.5f + (layer * 18.0f) + UnityEngine.Random.Range(-3.0f, 4.0f);
                        Vector3 treePos = p + right * (side * lateralDist);

                        bool overlapsTrack = false;
                        for (int k = 0; k < n; k += 4)
                        {
                            if (Mathf.Abs(k - i) < 14) continue;
                            float d = Vector2.Distance(new Vector2(treePos.x, treePos.z), new Vector2(pts[k].x, pts[k].z));
                            if (d < (trackW * 0.5f + 8.0f))
                            {
                                overlapsTrack = true;
                                break;
                            }
                        }
                        if (overlapsTrack) continue;

                        GameObject tree = new GameObject($"MonzaOak_L{layer}_{i}");
                        tree.transform.SetParent(forestRoot.transform, false);
                        tree.transform.position = treePos;

                        float scale = UnityEngine.Random.Range(0.95f, 1.55f);

                        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        trunk.transform.SetParent(tree.transform, false);
                        trunk.transform.localPosition = new Vector3(0f, 3.5f * scale, 0f);
                        trunk.transform.localScale = new Vector3(0.7f * scale, 3.5f * scale, 0.7f * scale);
                        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
                        UnityEngine.Object.Destroy(trunk.GetComponent<Collider>());

                        // Large rounded oak canopy crown
                        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        crown.transform.SetParent(tree.transform, false);
                        crown.transform.localPosition = new Vector3(0f, 7.5f * scale, 0f);
                        crown.transform.localScale = new Vector3(6.5f * scale, 5.0f * scale, 6.5f * scale);
                        crown.GetComponent<Renderer>().sharedMaterial = foliageMat;
                        UnityEngine.Object.Destroy(crown.GetComponent<Collider>());
                    }
                }
            }
        }

        private static void BuildMonacoIconicLandmarks(Transform parent, List<Vector3> pts, float trackW, Material tunnelMat, Material lightMat, Material seaMat, Material yachtMat, Material palmTrunk, Material palmLeaf)
        {
            GameObject root = new GameObject("Monaco_World_Landmarks");
            root.transform.SetParent(parent, false);

            int n = pts.Count;

            // 1. The Famous Monte Carlo Covered Tunnel (Turn 9 at ~58% to 68% distance)
            int tunnelStart = (int)(n * 0.58f);
            int tunnelEnd = (int)(n * 0.68f);

            GameObject tunnelRoot = new GameObject("Monte_Carlo_Covered_Tunnel");
            tunnelRoot.transform.SetParent(root.transform, false);

            for (int i = tunnelStart; i < tunnelEnd; i += 3)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;

                // Concrete Tunnel Overhead Roof
                GameObject arch = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arch.name = "Tunnel_Arch";
                arch.transform.SetParent(tunnelRoot.transform, false);
                arch.transform.position = p + Vector3.up * 6.2f;
                arch.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                arch.transform.localScale = new Vector3(trackW + 6.0f, 0.6f, 4.5f);
                arch.GetComponent<Renderer>().sharedMaterial = tunnelMat;
                UnityEngine.Object.Destroy(arch.GetComponent<Collider>());

                // Ceiling Lighting Strip
                GameObject lightStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lightStrip.name = "Tunnel_Ceiling_Light";
                lightStrip.transform.SetParent(arch.transform, false);
                lightStrip.transform.localPosition = new Vector3(0.0f, -0.35f, 0.0f);
                lightStrip.transform.localScale = new Vector3(0.4f, 0.1f, 0.95f);
                lightStrip.GetComponent<Renderer>().sharedMaterial = lightMat;
                UnityEngine.Object.Destroy(lightStrip.GetComponent<Collider>());
            }

            // 2. Port Hercule Mediterranean Yacht Marina Basin & Mega-Yachts
            GameObject seaBasin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seaBasin.name = "Port_Hercule_Marina_Water";
            seaBasin.transform.SetParent(root.transform, false);
            seaBasin.transform.position = new Vector3(200.0f, -0.45f, 150.0f);
            seaBasin.transform.localScale = new Vector3(260.0f, 0.2f, 220.0f);
            seaBasin.GetComponent<Renderer>().sharedMaterial = seaMat;
            UnityEngine.Object.Destroy(seaBasin.GetComponent<Collider>());

            // 5 Luxury Mega-Yachts Moored along Swimming Pool / Tabac
            for (int y = 0; y < 5; y++)
            {
                Vector3 yachtPos = new Vector3(160.0f + (y * 20.0f), 1.2f, 80.0f + (y * 30.0f));
                GameObject yacht = GameObject.CreatePrimitive(PrimitiveType.Cube);
                yacht.name = $"Mega_Yacht_{y + 1}";
                yacht.transform.SetParent(root.transform, false);
                yacht.transform.position = yachtPos;
                yacht.transform.rotation = Quaternion.Euler(0.0f, 25.0f, 0.0f);
                yacht.transform.localScale = new Vector3(12.0f, 4.5f, 32.0f);
                yacht.GetComponent<Renderer>().sharedMaterial = yachtMat;

                // Yacht Bridge Deck
                GameObject bridgeDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bridgeDeck.name = "BridgeDeck";
                bridgeDeck.transform.SetParent(yacht.transform, false);
                bridgeDeck.transform.localPosition = new Vector3(0.0f, 0.65f, 0.1f);
                bridgeDeck.transform.localScale = new Vector3(0.75f, 0.6f, 0.55f);
                bridgeDeck.GetComponent<Renderer>().sharedMaterial = yachtMat;
            }

            // 3. Mediterranean Palm Trees along Boulevard Albert 1er & Beau Rivage
            for (int p = 0; p < n - 1; p += 25)
            {
                Vector3 pt = pts[p];
                Vector3 fwd = (pts[p + 1] - pt).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                Vector3 palmPos = pt - right * (trackW * 0.5f + 12.0f);
                GameObject palm = new GameObject($"MonacoPalm_{p}");
                palm.transform.SetParent(root.transform, false);
                palm.transform.position = palmPos;

                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.transform.SetParent(palm.transform, false);
                trunk.transform.localPosition = new Vector3(0f, 3.5f, 0f);
                trunk.transform.localScale = new Vector3(0.45f, 3.5f, 0.45f);
                trunk.GetComponent<Renderer>().sharedMaterial = palmTrunk;
                UnityEngine.Object.Destroy(trunk.GetComponent<Collider>());

                for (int f = 0; f < 3; f++)
                {
                    GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    crown.transform.SetParent(palm.transform, false);
                    crown.transform.localPosition = new Vector3(0f, 7.0f + (f * 0.35f), 0f);
                    crown.transform.localScale = new Vector3(5.5f - (f * 0.7f), 0.3f, 5.5f - (f * 0.7f));
                    crown.GetComponent<Renderer>().sharedMaterial = palmLeaf;
                    UnityEngine.Object.Destroy(crown.GetComponent<Collider>());
                }
            }
        }

        private static void BuildDenseArdennesPineForest(Transform parent, List<Vector3> pts, float trackW, Material trunkMat, Material foliageMat)
        {
            GameObject forestRoot = new GameObject("Ardennes_Pine_Forest");
            forestRoot.transform.SetParent(parent, false);

            for (int i = 0; i < pts.Count - 1; i += 12)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                for (int layer = 1; layer <= 3; layer++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float lateralDist = trackW * 0.5f + (layer * 16.0f) + UnityEngine.Random.Range(-3.0f, 4.0f);
                        Vector3 treePos = p + right * (side * lateralDist);

                        GameObject tree = new GameObject($"PineTree_L{layer}_{i}");
                        tree.transform.SetParent(forestRoot.transform, false);
                        tree.transform.position = treePos;

                        float scale = UnityEngine.Random.Range(0.85f, 1.45f);

                        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        trunk.transform.SetParent(tree.transform, false);
                        trunk.transform.localPosition = new Vector3(0f, 3.0f * scale, 0f);
                        trunk.transform.localScale = new Vector3(0.6f * scale, 3.0f * scale, 0.6f * scale);
                        trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
                        UnityEngine.Object.Destroy(trunk.GetComponent<Collider>());

                        for (int c = 0; c < 3; c++)
                        {
                            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            cone.transform.SetParent(tree.transform, false);
                            cone.transform.localPosition = new Vector3(0f, (5.2f + (c * 2.6f)) * scale, 0f);
                            cone.transform.localScale = new Vector3((4.4f - (c * 1.1f)) * scale, 1.8f * scale, (4.4f - (c * 1.1f)) * scale);
                            cone.GetComponent<Renderer>().sharedMaterial = foliageMat;
                            UnityEngine.Object.Destroy(cone.GetComponent<Collider>());
                        }
                    }
                }
            }
        }

        private static void BuildSakhirPalmOasis(Transform parent, List<Vector3> pts, float trackW, Material trunkMat, Material frondMat)
        {
            GameObject palmRoot = new GameObject("Sakhir_Palm_Oasis");
            palmRoot.transform.SetParent(parent, false);

            for (int i = 0; i < pts.Count - 1; i += 22)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 palmPos = p + right * (side * (trackW * 0.5f + UnityEngine.Random.Range(20.0f, 36.0f)));

                    GameObject palm = new GameObject($"DatePalm_{i}");
                    palm.transform.SetParent(palmRoot.transform, false);
                    palm.transform.position = palmPos;

                    GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    trunk.transform.SetParent(palm.transform, false);
                    trunk.transform.localPosition = new Vector3(0f, 4.0f, 0f);
                    trunk.transform.localScale = new Vector3(0.5f, 4.0f, 0.5f);
                    trunk.GetComponent<Renderer>().sharedMaterial = trunkMat;
                    UnityEngine.Object.Destroy(trunk.GetComponent<Collider>());

                    for (int f = 0; f < 3; f++)
                    {
                        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        crown.transform.SetParent(palm.transform, false);
                        crown.transform.localPosition = new Vector3(0f, 8.0f + (f * 0.4f), 0f);
                        crown.transform.localScale = new Vector3(6.5f - (f * 0.8f), 0.35f, 6.5f - (f * 0.8f));
                        crown.GetComponent<Renderer>().sharedMaterial = frondMat;
                        UnityEngine.Object.Destroy(crown.GetComponent<Collider>());
                    }
                }
            }
        }

        private static void BuildCircuitFloodlights(Transform parent, List<Vector3> pts, float trackW, Material metalMat, Material emissiveMat)
        {
            GameObject lightsRoot = new GameObject("Circuit_Floodlights");
            lightsRoot.transform.SetParent(parent, false);

            for (int i = 0; i < pts.Count - 1; i += 32)
            {
                Vector3 p = pts[i];
                Vector3 fwd = (pts[i + 1] - p).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                Vector3 polePos = p + right * (trackW * 0.5f + 18.0f);

                GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = "Floodlight_Pole";
                pole.transform.SetParent(lightsRoot.transform, false);
                pole.transform.position = polePos + Vector3.up * 10.0f;
                pole.transform.localScale = new Vector3(0.5f, 10.0f, 0.5f);
                pole.GetComponent<Renderer>().sharedMaterial = metalMat;
                UnityEngine.Object.Destroy(pole.GetComponent<Collider>());

                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "Floodlight_Head";
                head.transform.SetParent(lightsRoot.transform, false);
                head.transform.position = polePos + Vector3.up * 20.0f - right * 2.0f;
                head.transform.localScale = new Vector3(4.0f, 1.8f, 1.4f);
                head.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                head.GetComponent<Renderer>().sharedMaterial = emissiveMat;
                UnityEngine.Object.Destroy(head.GetComponent<Collider>());
            }
        }

        private static Material CreateMaterial(Shader shader, string name, Color color, float smoothness, float metallic, string texturePath)
        {
            Material mat = new Material(shader) { name = name };
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(texturePath))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null)
                {
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                }
            }
#endif
            return mat;
        }
    }
}
