using System;

namespace GridSense.Core
{
    /// <summary>
    /// Tyre compound categories with distinct thermal windows, wear rates, and base grip.
    /// </summary>
    public enum TyreCompound
    {
        Soft = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// Hybrid powertrain energy deployment modes.
    /// </summary>
    public enum EnergyMode
    {
        Push = 0,     // Maximum electrical assist (+120kW draw, highest battery drain)
        Balanced = 1, // Standard delta pace deployment (~+60kW)
        Hold = 2,     // Neutral energy deployment (0kW net delta, sustain SoC)
        Save = 3      // Energy recovery harvest mode (-40kW draw, aggressive regen)
    }

    /// <summary>
    /// Braking aggressiveness mode affecting regen gain, lockup risk, and thermal load.
    /// </summary>
    public enum BrakingAggressiveness
    {
        Normal = 0,     // Conservative braking line, lower lockup risk, standard thermal rise
        Aggressive = 1  // Late/hard braking threshold: +35% regen energy, elevated lockup & brake fade risk
    }

    /// <summary>
    /// Drag Reduction System (DRS) state.
    /// </summary>
    public enum DrsState
    {
        Closed = 0,
        Available = 1,
        Open = 2
    }

    /// <summary>
    /// Categorical overtake risk classification derived from Track 1 risk-reward scoring.
    /// </summary>
    public enum OvertakeRiskCategory
    {
        Low = 0,
        Moderate = 1,
        High = 2,
        Critical = 3
    }
}
