namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    /// <summary>Current digital horizontal sync assertion, before pin polarity selection.</summary>
    public bool ProgrammableHorizontalSyncActive => IsEcsAgnus && (_registers.Read(LightweightRegisters.Beamcon0) & 0x100) != 0
        ? InRasterInterval(_clock.ColorClock, _registers.Read(LightweightRegisters.Hsstrt), _registers.Read(LightweightRegisters.Hsstop))
        : false;

    /// <summary>Current digital vertical sync assertion, before pin polarity selection.</summary>
    public bool ProgrammableVerticalSyncActive
    {
        get
        {
            if (!IsEcsAgnus || (_registers.Read(LightweightRegisters.Beamcon0) & 0x200) == 0)
                return false;
            var line = _clock.Line;
            if (InterlaceEnabled && !_clock.IsLongField && _clock.ColorClock < _registers.Read(LightweightRegisters.Hcenter))
                line = line == 0 ? _clock.LinesThisField - 1 : line - 1;
            return InRasterInterval(line, _registers.Read(LightweightRegisters.Vsstrt), _registers.Read(LightweightRegisters.Vsstop));
        }
    }

    internal bool IsRasterBlanked(int line, int x)
    {
        var beam = IsEcsAgnus ? _registers.Read(LightweightRegisters.Beamcon0) : (ushort)0x20;
        var vertical = (beam & 0x1000) != 0
            ? InRasterInterval(line, _registers.Read(LightweightRegisters.Vbstrt), _registers.Read(LightweightRegisters.Vbstop))
            : line < ((beam & 0x20) != 0 ? 26 : 21) || line >= ((beam & 0x20) != 0 ? 311 : 261);
        var horizontal = (beam & 0x4000) != 0
            ? InRasterInterval(x / 2, _registers.Read(LightweightRegisters.Hbstrt), _registers.Read(LightweightRegisters.Hbstop))
            : x < 98;
        return vertical || horizontal;
    }

    private static bool InRasterInterval(int value, int start, int stop)
        => start <= stop ? value >= start && value < stop : value >= start || value < stop;

    // Hardwired Agnus RVB comparator: PAL 25, NTSC 20. Denise's visible
    // output begins on the following line. The fitted oscillator stays PAL.
    internal int SpriteControlReloadLine => IsEcsAgnus &&
        (_registers.Read(LightweightRegisters.Beamcon0) & 0x20) == 0 ? 20 : 25;

    private bool ReadLatchedLightPen => _lightPenLatched && (_video.EffectiveBplcon0 & 8) != 0 &&
        (!IsEcsAgnus || (_registers.Read(LightweightRegisters.Beamcon0) & 0x2000) == 0);
}
