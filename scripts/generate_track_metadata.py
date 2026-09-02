"""
Track Metadata Reference Data & ScriptableObject Generator
Circuits:
1. Bahrain International Circuit (Sakhir - Grand Prix Circuit): Length 5.412 km, 15 turns, 3 sectors.
2. Red Bull Ring (Spielberg): Length 4.318 km, 10 turns, 3 sectors.
3. Shanghai International Circuit: Length 5.451 km, 16 turns, 3 sectors.
4. Suzuka International Racing Course: Length 5.807 km, 18 turns, 3 sectors.
5. Yas Marina Circuit (Abu Dhabi - Post-2021 Layout): Length 5.281 km, 16 turns, 3 sectors.
"""

import json

CIRCUITS_METADATA = {
    "Bahrain": {
        "circuit_name": "Bahrain International Circuit",
        "layout": "Grand Prix Track",
        "length_meters": 5412.0,
        "sectors": [
            {"sector_number": 1, "start_distance_m": 0.0, "end_distance_m": 1640.0},
            {"sector_number": 2, "start_distance_m": 1640.0, "end_distance_m": 3870.0},
            {"sector_number": 3, "start_distance_m": 3870.0, "end_distance_m": 5412.0}
        ],
        "start_finish_pos": {"x": 0.0, "y": 0.0, "z": 0.0},
        "corners": [
            {"corner_number": 1, "name": "Turn 1 (Michael Schumacher)", "distance_m": 650.0, "severity": "HardBraking", "direction": "Right", "min_speed_kmh": 65.0, "base_grip": 1.0},
            {"corner_number": 2, "name": "Turn 2", "distance_m": 800.0, "severity": "Medium", "direction": "Left", "min_speed_kmh": 130.0, "base_grip": 1.0},
            {"corner_number": 3, "name": "Turn 3", "distance_m": 950.0, "severity": "FlatOut", "direction": "Right", "min_speed_kmh": 240.0, "base_grip": 1.0},
            {"corner_number": 4, "name": "Turn 4", "distance_m": 1550.0, "severity": "HardBraking", "direction": "Right", "min_speed_kmh": 135.0, "base_grip": 1.0},
            {"corner_number": 5, "name": "Turn 5", "distance_m": 1850.0, "severity": "HighSpeed", "direction": "Left", "min_speed_kmh": 220.0, "base_grip": 0.98},
            {"corner_number": 6, "name": "Turn 6", "distance_m": 2000.0, "severity": "HighSpeed", "direction": "Right", "min_speed_kmh": 210.0, "base_grip": 0.98},
            {"corner_number": 7, "name": "Turn 7", "distance_m": 2150.0, "severity": "MediumSpeed", "direction": "Left", "min_speed_kmh": 170.0, "base_grip": 0.98},
            {"corner_number": 8, "name": "Turn 8", "distance_m": 2550.0, "severity": "Hairpin", "direction": "Right", "min_speed_kmh": 70.0, "base_grip": 1.0},
            {"corner_number": 9, "name": "Turn 9", "distance_m": 2900.0, "severity": "TrickyBraking", "direction": "Left", "min_speed_kmh": 160.0, "base_grip": 0.95},
            {"corner_number": 10, "name": "Turn 10", "distance_m": 3100.0, "severity": "HairpinDownhill", "direction": "Left", "min_speed_kmh": 60.0, "base_grip": 0.95},
            {"corner_number": 11, "name": "Turn 11", "distance_m": 3750.0, "severity": "FastSweep", "direction": "Left", "min_speed_kmh": 210.0, "base_grip": 1.0},
            {"corner_number": 12, "name": "Turn 12", "distance_m": 4150.0, "severity": "FlatOut", "direction": "Right", "min_speed_kmh": 260.0, "base_grip": 1.0},
            {"corner_number": 13, "name": "Turn 13", "distance_m": 4350.0, "severity": "MediumBraking", "direction": "Right", "min_speed_kmh": 140.0, "base_grip": 1.0},
            {"corner_number": 14, "name": "Turn 14", "distance_m": 5050.0, "severity": "HardBraking", "direction": "Right", "min_speed_kmh": 110.0, "base_grip": 1.0},
            {"corner_number": 15, "name": "Turn 15", "distance_m": 5200.0, "severity": "AccelerationZone", "direction": "Right", "min_speed_kmh": 180.0, "base_grip": 1.0}
        ]
    },
    "RedBullRing": {
        "circuit_name": "Red Bull Ring",
        "layout": "Grand Prix Track",
        "length_meters": 4318.0,
        "sectors": [
            {"sector_number": 1, "start_distance_m": 0.0, "end_distance_m": 1200.0},
            {"sector_number": 2, "start_distance_m": 1200.0, "end_distance_m": 2900.0},
            {"sector_number": 3, "start_distance_m": 2900.0, "end_distance_m": 4318.0}
        ],
        "start_finish_pos": {"x": 0.0, "y": 0.0, "z": 0.0},
        "corners": [
            {"corner_number": 1, "name": "Turn 1 (Niki Lauda Kurve)", "distance_m": 400.0, "severity": "HardBraking", "direction": "Right", "min_speed_kmh": 140.0, "base_grip": 1.0},
            {"corner_number": 2, "name": "Turn 2", "distance_m": 1150.0, "severity": "Kink", "direction": "Left", "min_speed_kmh": 300.0, "base_grip": 1.0},
            {"corner_number": 3, "name": "Turn 3 (Remus)", "distance_m": 1400.0, "severity": "UphillHairpin", "direction": "Right", "min_speed_kmh": 65.0, "base_grip": 1.0},
            {"corner_number": 4, "name": "Turn 4 (Schlossgold)", "distance_m": 2200.0, "severity": "DownhillHardBraking", "direction": "Right", "min_speed_kmh": 115.0, "base_grip": 0.98},
            {"corner_number": 5, "name": "Turn 5", "distance_m": 2550.0, "severity": "FastLeft", "direction": "Left", "min_speed_kmh": 190.0, "base_grip": 1.0},
            {"corner_number": 6, "name": "Turn 6 (Gerhard Berger)", "distance_m": 2800.0, "severity": "MediumLeft", "direction": "Left", "min_speed_kmh": 165.0, "base_grip": 1.0},
            {"corner_number": 7, "name": "Turn 7", "distance_m": 3150.0, "severity": "FastSweep", "direction": "Right", "min_speed_kmh": 220.0, "base_grip": 1.0},
            {"corner_number": 8, "name": "Turn 8", "distance_m": 3450.0, "severity": "FastSweep", "direction": "Left", "min_speed_kmh": 230.0, "base_grip": 1.0},
            {"corner_number": 9, "name": "Turn 9 (Jochen Rindt)", "distance_m": 3850.0, "severity": "MediumHighRight", "direction": "Right", "min_speed_kmh": 195.0, "base_grip": 1.0},
            {"corner_number": 10, "name": "Turn 10", "distance_m": 4150.0, "severity": "FinalCorner", "direction": "Right", "min_speed_kmh": 180.0, "base_grip": 1.0}
        ]
    },
    "Shanghai": {
        "circuit_name": "Shanghai International Circuit",
        "layout": "Grand Prix Track",
        "length_meters": 5451.0,
        "sectors": [
            {"sector_number": 1, "start_distance_m": 0.0, "end_distance_m": 1550.0},
            {"sector_number": 2, "start_distance_m": 1550.0, "end_distance_m": 3500.0},
            {"sector_number": 3, "start_distance_m": 3500.0, "end_distance_m": 5451.0}
        ],
        "start_finish_pos": {"x": 0.0, "y": 0.0, "z": 0.0},
        "corners": [
            {"corner_number": 1, "name": "Turn 1 (Snail Entry)", "distance_m": 450.0, "severity": "DecreasingRadius", "direction": "Right", "min_speed_kmh": 110.0, "base_grip": 0.98},
            {"corner_number": 2, "name": "Turn 2", "distance_m": 700.0, "severity": "SlowTightening", "direction": "Right", "min_speed_kmh": 75.0, "base_grip": 0.96},
            {"corner_number": 3, "name": "Turn 3", "distance_m": 850.0, "severity": "HairpinSwitchback", "direction": "Left", "min_speed_kmh": 80.0, "base_grip": 0.98},
            {"corner_number": 4, "name": "Turn 4", "distance_m": 1050.0, "severity": "AccelerationExit", "direction": "Left", "min_speed_kmh": 160.0, "base_grip": 1.0},
            {"corner_number": 5, "name": "Turn 5", "distance_m": 1400.0, "severity": "FlatOutKink", "direction": "Right", "min_speed_kmh": 270.0, "base_grip": 1.0},
            {"corner_number": 6, "name": "Turn 6", "distance_m": 1750.0, "severity": "Hairpin", "direction": "Right", "min_speed_kmh": 75.0, "base_grip": 1.0},
            {"corner_number": 7, "name": "Turn 7", "distance_m": 2200.0, "severity": "HighSpeedEsses1", "direction": "Left", "min_speed_kmh": 240.0, "base_grip": 1.0},
            {"corner_number": 8, "name": "Turn 8", "distance_m": 2400.0, "severity": "HighSpeedEsses2", "direction": "Right", "min_speed_kmh": 210.0, "base_grip": 1.0},
            {"corner_number": 9, "name": "Turn 9", "distance_m": 2850.0, "severity": "MediumBraking", "direction": "Left", "min_speed_kmh": 125.0, "base_grip": 0.98},
            {"corner_number": 10, "name": "Turn 10", "distance_m": 3100.0, "severity": "MediumExit", "direction": "Right", "min_speed_kmh": 150.0, "base_grip": 1.0},
            {"corner_number": 11, "name": "Turn 11", "distance_m": 3500.0, "severity": "BankingEntry", "direction": "Left", "min_speed_kmh": 180.0, "base_grip": 1.0},
            {"corner_number": 12, "name": "Turn 12", "distance_m": 3750.0, "severity": "LongBankedTurn", "direction": "Right", "min_speed_kmh": 130.0, "base_grip": 1.0},
            {"corner_number": 13, "name": "Turn 13", "distance_m": 4050.0, "severity": "BankedExitStraight", "direction": "Right", "min_speed_kmh": 210.0, "base_grip": 1.0},
            {"corner_number": 14, "name": "Turn 14", "distance_m": 5000.0, "severity": "MassiveBrakingHairpin", "direction": "Right", "min_speed_kmh": 65.0, "base_grip": 1.0},
            {"corner_number": 15, "name": "Turn 15", "distance_m": 5150.0, "severity": "ShortChicane", "direction": "Left", "min_speed_kmh": 120.0, "base_grip": 1.0},
            {"corner_number": 16, "name": "Turn 16", "distance_m": 5350.0, "severity": "FinalTurn", "direction": "Left", "min_speed_kmh": 170.0, "base_grip": 1.0}
        ]
    },
    "Suzuka": {
        "circuit_name": "Suzuka International Racing Course",
        "layout": "Grand Prix Figure-8 Track",
        "length_meters": 5807.0,
        "sectors": [
            {"sector_number": 1, "start_distance_m": 0.0, "end_distance_m": 1950.0},
            {"sector_number": 2, "start_distance_m": 1950.0, "end_distance_m": 4100.0},
            {"sector_number": 3, "start_distance_m": 4100.0, "end_distance_m": 5807.0}
        ],
        "start_finish_pos": {"x": 0.0, "y": 0.0, "z": 0.0},
        "corners": [
            {"corner_number": 1, "name": "Turn 1", "distance_m": 500.0, "severity": "FastEntry", "direction": "Right", "min_speed_kmh": 230.0, "base_grip": 1.0},
            {"corner_number": 2, "name": "Turn 2", "distance_m": 650.0, "severity": "TighteningRight", "direction": "Right", "min_speed_kmh": 150.0, "base_grip": 1.0},
            {"corner_number": 3, "name": "Turn 3 (S-Curves 1)", "distance_m": 900.0, "severity": "FlowingS", "direction": "Left", "min_speed_kmh": 210.0, "base_grip": 1.0},
            {"corner_number": 4, "name": "Turn 4 (S-Curves 2)", "distance_m": 1100.0, "severity": "FlowingS", "direction": "Right", "min_speed_kmh": 190.0, "base_grip": 1.0},
            {"corner_number": 5, "name": "Turn 5 (S-Curves 3)", "distance_m": 1300.0, "severity": "FlowingS", "direction": "Left", "min_speed_kmh": 180.0, "base_grip": 1.0},
            {"corner_number": 6, "name": "Turn 6 (S-Curves 4)", "distance_m": 1500.0, "severity": "FlowingS", "direction": "Right", "min_speed_kmh": 195.0, "base_grip": 1.0},
            {"corner_number": 7, "name": "Turn 7 (Dunlop Curve)", "distance_m": 1800.0, "severity": "LongUphillLeft", "direction": "Left", "min_speed_kmh": 230.0, "base_grip": 0.98},
            {"corner_number": 8, "name": "Turn 8 (Degner 1)", "distance_m": 2250.0, "severity": "FastRightCurb", "direction": "Right", "min_speed_kmh": 200.0, "base_grip": 1.0},
            {"corner_number": 9, "name": "Turn 9 (Degner 2)", "distance_m": 2400.0, "severity": "NarrowRight", "direction": "Right", "min_speed_kmh": 140.0, "base_grip": 0.97},
            {"corner_number": 10, "name": "Turn 10 (Under-Bridge)", "distance_m": 2700.0, "severity": "FlatOutKink", "direction": "Left", "min_speed_kmh": 270.0, "base_grip": 1.0},
            {"corner_number": 11, "name": "Turn 11 (Hairpin)", "distance_m": 3050.0, "severity": "SlowHairpin", "direction": "Right", "min_speed_kmh": 65.0, "base_grip": 1.0},
            {"corner_number": 12, "name": "Turn 12 (200R)", "distance_m": 3500.0, "severity": "FastSweep", "direction": "Right", "min_speed_kmh": 240.0, "base_grip": 1.0},
            {"corner_number": 13, "name": "Turn 13 (Spoon Entry)", "distance_m": 3800.0, "severity": "DownhillLeft", "direction": "Left", "min_speed_kmh": 170.0, "base_grip": 1.0},
            {"corner_number": 14, "name": "Turn 14 (Spoon Exit)", "distance_m": 4050.0, "severity": "AcceleratingLeft", "direction": "Left", "min_speed_kmh": 150.0, "base_grip": 1.0},
            {"corner_number": 15, "name": "Turn 15 (130R)", "distance_m": 4950.0, "severity": "LegendaryFastLeft", "direction": "Left", "min_speed_kmh": 290.0, "base_grip": 1.0},
            {"corner_number": 16, "name": "Turn 16 (Casio Triangle 1)", "distance_m": 5400.0, "severity": "HeavyBrakingChicane", "direction": "Right", "min_speed_kmh": 85.0, "base_grip": 1.0},
            {"corner_number": 17, "name": "Turn 17 (Casio Triangle 2)", "distance_m": 5500.0, "severity": "ChicaneExit", "direction": "Left", "min_speed_kmh": 100.0, "base_grip": 1.0},
            {"corner_number": 18, "name": "Turn 18", "distance_m": 5650.0, "severity": "FinalCurvedStraight", "direction": "Right", "min_speed_kmh": 220.0, "base_grip": 1.0}
        ]
    },
    "YasMarina": {
        "circuit_name": "Yas Marina Circuit",
        "layout": "Grand Prix Modified Layout",
        "length_meters": 5281.0,
        "sectors": [
            {"sector_number": 1, "start_distance_m": 0.0, "end_distance_m": 1500.0},
            {"sector_number": 2, "start_distance_m": 1500.0, "end_distance_m": 3450.0},
            {"sector_number": 3, "start_distance_m": 3450.0, "end_distance_m": 5281.0}
        ],
        "start_finish_pos": {"x": 0.0, "y": 0.0, "z": 0.0},
        "corners": [
            {"corner_number": 1, "name": "Turn 1", "distance_m": 350.0, "severity": "MediumBraking", "direction": "Left", "min_speed_kmh": 140.0, "base_grip": 1.0},
            {"corner_number": 2, "name": "Turn 2", "distance_m": 850.0, "severity": "FlatOutKink", "direction": "Right", "min_speed_kmh": 280.0, "base_grip": 1.0},
            {"corner_number": 3, "name": "Turn 3", "distance_m": 1050.0, "severity": "FlatOutKink", "direction": "Left", "min_speed_kmh": 270.0, "base_grip": 1.0},
            {"corner_number": 4, "name": "Turn 4", "distance_m": 1350.0, "severity": "FastLeft", "direction": "Left", "min_speed_kmh": 230.0, "base_grip": 1.0},
            {"corner_number": 5, "name": "Turn 5 (New North Hairpin)", "distance_m": 1600.0, "severity": "HeavyBrakingHairpin", "direction": "Left", "min_speed_kmh": 70.0, "base_grip": 1.0},
            {"corner_number": 6, "name": "Turn 6 (Back Straight Chicane)", "distance_m": 2900.0, "severity": "HardBrakingChicane", "direction": "Left", "min_speed_kmh": 90.0, "base_grip": 1.0},
            {"corner_number": 7, "name": "Turn 7", "distance_m": 3050.0, "severity": "ChicaneExit", "direction": "Right", "min_speed_kmh": 110.0, "base_grip": 1.0},
            {"corner_number": 8, "name": "Turn 8", "distance_m": 3350.0, "severity": "FastEntry", "direction": "Left", "min_speed_kmh": 190.0, "base_grip": 1.0},
            {"corner_number": 9, "name": "Turn 9 (New Marsa Banked Turn)", "distance_m": 3600.0, "severity": "BankedHighSpeedSweep", "direction": "Left", "min_speed_kmh": 210.0, "base_grip": 1.05},
            {"corner_number": 10, "name": "Turn 10", "distance_m": 3950.0, "severity": "HotelEntryRight", "direction": "Right", "min_speed_kmh": 130.0, "base_grip": 0.98},
            {"corner_number": 11, "name": "Turn 11", "distance_m": 4200.0, "severity": "UnderHotelLeft", "direction": "Left", "min_speed_kmh": 110.0, "base_grip": 0.98},
            {"corner_number": 12, "name": "Turn 12", "distance_m": 4450.0, "severity": "HotelExitRight", "direction": "Right", "min_speed_kmh": 125.0, "base_grip": 0.98},
            {"corner_number": 13, "name": "Turn 13", "distance_m": 4650.0, "severity": "HotelComplexLeft", "direction": "Left", "min_speed_kmh": 140.0, "base_grip": 1.0},
            {"corner_number": 14, "name": "Turn 14", "distance_m": 4850.0, "severity": "MarinaSweepRight", "direction": "Right", "min_speed_kmh": 160.0, "base_grip": 1.0},
            {"corner_number": 15, "name": "Turn 15", "distance_m": 5050.0, "severity": "MediumRight", "direction": "Right", "min_speed_kmh": 135.0, "base_grip": 1.0},
            {"corner_number": 16, "name": "Turn 16", "distance_m": 5200.0, "severity": "FinalTurnRight", "direction": "Right", "min_speed_kmh": 150.0, "base_grip": 1.0}
        ]
    }
}

with open("track_metadata.json", "w", encoding="utf-8") as f:
    json.dump(CIRCUITS_METADATA, f, indent=2)

print("Generated track_metadata.json for all 5 circuits.")
