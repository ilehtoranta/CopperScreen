namespace CopperMod.Amiga.Lightweight;

/// <summary>Display-chip revision, independent of Agnus memory and beam control.</summary>
public enum LightweightDeniseModel
{
    Mos8362,
    Mos8373,
    /// <summary>AGA 4203 Lisa; requires the matching Alice/A1200 layout.</summary>
    Lisa4203
}
