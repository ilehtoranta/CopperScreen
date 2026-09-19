namespace CopperMod.Amiga.Lightweight;

internal sealed class LightweightClock
{
    internal const int CpuCyclesPerColorClock = 2;
    internal const int CpuCyclesPerLine = 454;
    internal const int PalShortFieldLines = 312;
    internal const int PalLongFieldLines = 313;
    internal const int PalLinesPerFrame = PalLongFieldLines;
    internal const long PalShortFieldCycles = (long)CpuCyclesPerLine * PalShortFieldLines;
    internal const long PalLongFieldCycles = (long)CpuCyclesPerLine * PalLongFieldLines;
    internal const long CpuCyclesPerFrame = PalLongFieldCycles;

    internal long Cycle { get; private set; }
    internal int Line { get; private set; }
    internal int ColorClock { get; private set; }
    internal long FrameStartCycle { get; private set; }
    internal long FrameNumber { get; private set; }
    internal int LinesThisField { get; private set; }
    internal bool IsLongField => LinesThisField == PalLongFieldLines;
    internal bool SyncStopped { get; private set; }
    internal long BeamCycleOffset { get; private set; }
    internal long NextLineCycle => SyncStopped ? long.MaxValue : Cycle + CpuCyclesPerLine -
        (ColorClock * CpuCyclesPerColorClock + _cpuPhase);
    internal long NextFrameCycle
    {
        get
        {
            if (SyncStopped) return long.MaxValue;
            var nominalEnd = FrameStartCycle +
                ((long)LinesThisField * CpuCyclesPerLine);
            if (nominalEnd > Cycle)
                return nominalEnd;

            // VPOSW can change LOF after the selected field's nominal end.
            // The beam cannot end retroactively, so the current rasterline is
            // allowed to finish and becomes the field boundary.
            var cyclesIntoLine = (ColorClock * CpuCyclesPerColorClock) + _cpuPhase;
            return Cycle + CpuCyclesPerLine - cyclesIntoLine;
        }
    }

    private int _cpuPhase;
    private bool _externalSync;
    private int _horizontalLimit = CpuCyclesPerLine / CpuCyclesPerColorClock;
    private long _syncStopCycle;

    internal void Reset()
    {
        Cycle = 0;
        Line = 0;
        ColorClock = 0;
        FrameStartCycle = 0;
        FrameNumber = 0;
        LinesThisField = PalLongFieldLines;
        _cpuPhase = 0;
        _externalSync = SyncStopped = false;
        BeamCycleOffset = 0;
        _horizontalLimit = CpuCyclesPerLine / CpuCyclesPerColorClock;
    }

    internal void SelectLongField(bool longField)
        => LinesThisField = longField ? PalLongFieldLines : PalShortFieldLines;

    internal void SetExternalSync(bool enabled, LightweightA500Machine machine)
    {
        _externalSync = enabled;
        if (enabled || !SyncStopped) return;
        // No line/field strobe was emitted while the HSYNC input was absent.
        // Resume this same vertical count, at H0, on the canonical clock.
        var heldCycles = Cycle - _cpuPhase - _syncStopCycle;
        FrameStartCycle += heldCycles + CpuCyclesPerLine;
        BeamCycleOffset = (BeamCycleOffset + heldCycles) % CpuCyclesPerLine;
        SyncStopped = false;
        _horizontalLimit = CpuCyclesPerLine / CpuCyclesPerColorClock;
        machine.OnBeamSyncChanged(Cycle);
    }

    // A pending CPU transfer uses the same physical CCK completion routine as
    // device-only advancement. Stay inside that loop while waiting instead of
    // re-entering the arbitrary-target AdvanceTo routine for every stolen slot.
    internal long AdvanceToCpuGrant(long firstCandidate, LightweightA500Machine machine)
    {
        machine.UpdateCpuRequestCandidate(firstCandidate);
        AdvanceTo(firstCandidate, machine);
        System.Diagnostics.Debug.Assert(_cpuPhase == 0);
        while (machine.IsCpuOutputOwned(Cycle))
        {
            var next = Cycle + CpuCyclesPerColorClock;
            machine.UpdateCpuRequestCandidate(next);
            Cycle = next;
            CompleteColorClock(machine);
        }
        return Cycle;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    internal void AdvanceTo(long targetCycle, LightweightA500Machine machine)
    {
        if (targetCycle < Cycle)
            throw new InvalidOperationException("The lightweight clock cannot move backwards.");

        // CPU accesses can stop on either CPU-cycle phase. Finish such a
        // partial CCK first, then keep the steady path to one add and one
        // boundary test per complete CCK. Active display workloads execute
        // this loop about 71,000 times per PAL frame.
        if (_cpuPhase != 0 && Cycle < targetCycle)
        {
            var remaining = CpuCyclesPerColorClock - _cpuPhase;
            var available = targetCycle - Cycle;
            if (available < remaining)
            {
                Cycle = targetCycle;
                _cpuPhase += (int)available;
                return;
            }

            Cycle += remaining;
            _cpuPhase = 0;
            CompleteColorClock(machine);
        }

        // Canonical cycles are nonnegative and targetCycle is at least Cycle.
        // Compute the complete-CCK limit once instead of subtracting per CCK.
        var lastCompleteCckStart = targetCycle - CpuCyclesPerColorClock;
        while (Cycle <= lastCompleteCckStart)
        {
            Cycle += CpuCyclesPerColorClock;
            CompleteColorClock(machine);
        }

        if (Cycle < targetCycle)
        {
            _cpuPhase = (int)(targetCycle - Cycle);
            Cycle = targetCycle;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void CompleteColorClock(LightweightA500Machine machine)
    {
        ColorClock++;
        if (ColorClock == _horizontalLimit)
        {
            ColorClock = 0;
            if (_externalSync)
            {
                if (!SyncStopped)
                {
                    SyncStopped = true;
                    _syncStopCycle = Cycle;
                    _horizontalLimit = 1;
                    machine.OnBeamSyncChanged(Cycle);
                }
            }
            else
            {
                Line++;
                machine.OnLineCompleted(Cycle);
                if (Line >= LinesThisField)
                {
                    Line = 0;
                    FrameStartCycle = Cycle;
                    FrameNumber++;
                    machine.OnFrameCompleted(Cycle);
                }
            }
            machine.CompleteOutputIfDue(Cycle);
        }
        if (Cycle >= machine.NextDeviceCycle)
        {
            machine.TickDevices(Cycle);
        }
    }
}
