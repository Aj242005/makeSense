import os
import json

with open("track_metadata.json", "r", encoding="utf-8") as f:
    tracks = json.load(f)

severity_map = {
    "FlatOut": 0,
    "FlatOutKink": 0,
    "HighSpeed": 1,
    "FastSweep": 1,
    "FastLeft": 1,
    "FastEntry": 1,
    "LegendaryFastLeft": 1,
    "HighSpeedEsses1": 1,
    "HighSpeedEsses2": 1,
    "MediumSpeed": 2,
    "Medium": 2,
    "MediumLeft": 2,
    "MediumHighRight": 2,
    "MediumBraking": 2,
    "MediumExit": 2,
    "MarinaSweepRight": 2,
    "MediumRight": 2,
    "HardBraking": 3,
    "DownhillHardBraking": 3,
    "HeavyBrakingChicane": 3,
    "HeavyBrakingHairpin": 3,
    "MassiveBrakingHairpin": 3,
    "HardBrakingChicane": 3,
    "Hairpin": 4,
    "UphillHairpin": 4,
    "HairpinDownhill": 4,
    "HairpinSwitchback": 4,
    "SlowHairpin": 4,
    "Chicane": 5,
    "ShortChicane": 5,
    "ChicaneExit": 5,
    "DecreasingRadius": 6,
    "SlowTightening": 6,
    "TighteningRight": 6,
    "NarrowRight": 6,
    "Banked": 7,
    "BankingEntry": 7,
    "LongBankedTurn": 7,
    "BankedExitStraight": 7,
    "BankedHighSpeedSweep": 7
}

env_dir = "Assets/Environments"
os.makedirs(env_dir, exist_ok=True)

for key, data in tracks.items():
    circuit_name = data["circuit_name"]
    layout = data["layout"]
    length_m = data["length_meters"]
    sectors = data["sectors"]
    corners = data["corners"]
    
    track_folder = os.path.join(env_dir, key)
    os.makedirs(track_folder, exist_ok=True)
    
    yaml_lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: 00000000000000000000000000000000, type: 3}",
        f"  m_Name: {key}_TrackData",
        "  m_EditorClassIdentifier: GridSense.Track:TrackData",
        f"  CircuitName: {circuit_name}",
        f"  LayoutName: {layout}",
        f"  SourceProvenance: Official FIA circuit layout metrics with corner severities and 3 standard timing sectors.",
        f"  TotalLengthMeters: {length_m}",
        "  StartFinishLinePosition: {x: 0, y: 0, z: 0}",
        "  StartFinishLineDirection: {x: 0, y: 0, z: 1}",
        "  Sectors:",
    ]
    
    for s in sectors:
        yaml_lines.append(f"  - SectorNumber: {s['sector_number']}")
        yaml_lines.append(f"    StartDistanceM: {s['start_distance_m']}")
        yaml_lines.append(f"    EndDistanceM: {s['end_distance_m']}")
        
    yaml_lines.append("  Corners:")
    for c in corners:
        sev_val = severity_map.get(c.get("severity", "MediumSpeed"), 2)
        yaml_lines.append(f"  - CornerNumber: {c['corner_number']}")
        yaml_lines.append(f"    CornerName: {c['name']}")
        yaml_lines.append(f"    DistanceAlongTrackM: {c['distance_m']}")
        yaml_lines.append(f"    Severity: {sev_val}")
        yaml_lines.append(f"    TurnDirection: {c.get('direction', 'Right')}")
        yaml_lines.append(f"    MinApexSpeedKmh: {c.get('min_speed_kmh', 120.0)}")
        yaml_lines.append(f"    BaseGripMultiplier: {c.get('base_grip', 1.0)}")
        yaml_lines.append("    ApexWorldPosition: {x: 0, y: 0, z: 0}")
        
    yaml_lines.append("  DefaultAmbientTempC: 25")
    yaml_lines.append("  DefaultTrackTempC: 35")
    yaml_lines.append("  DefaultElevationM: 0")
    
    asset_file = os.path.join(track_folder, f"{key}_TrackData.asset")
    with open(asset_file, "w", encoding="utf-8") as out:
        out.write("\n".join(yaml_lines) + "\n")
        
    print(f"Generated TrackData asset: {asset_file}", flush=True)

print("All 5 TrackData assets generated successfully.")
