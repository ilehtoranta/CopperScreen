using System.Runtime.CompilerServices;

namespace CopperMod.Amiga.Lightweight;

/// <summary>
/// Direct OCS/ECS DDF and bitplane DMA path, including two-plane SuperHires. One accepted address
/// is retained until the following CCK samples Chip RAM.
/// </summary>
internal sealed class LightweightBitplanes
{
    private const ushort DmaconMaster = 0x0200;
    private const ushort DmaconBitplane = 0x0100;
    private const int DdfMask = 0x00FC;
    private const int DdfLimitReleaseHorizontal = 0x18;
    private const int DdfHardStopCompareHorizontal = 0xD7;
    private const int DdfHardStopTargetHorizontal = 0xD8;
    private const int FetchUnitColorClocks = 8;
    private readonly uint _addressMask;
    private readonly bool _ecs, _aga;
    private int _fmode, _pendingFmode, _pendingMode;
    private long _pendingFmodeCycle = long.MaxValue;
    private int _fetchPeriod = 8, _fetchUnit = 8, _maxFetchPlanes = 8;
    private int _ddfMask = DdfMask;

    internal LightweightBitplanes(uint addressMask = 0x0007_FFFEu, bool ecs = false, bool aga = false)
    {
        _addressMask = addressMask;
        _ecs = ecs; _aga = aga;
    }
    // Three bits per CCK, storing plane + 1 (zero is the idle slot).
    // Lores: -1,3,5,1,-1,2,4,0; hires: 3,1,2,0,3,1,2,0.
    private const uint LowResFetchOrder = 0x3585A0;
    private const uint HighResFetchOrder = 0x2D42D4;
    private const uint SuperHiresFetchOrder = 0x28A28A;

    private readonly uint[] _pointers = new uint[8];
    private readonly ushort[] _dataLatches = new ushort[8];
    private ushort _effectiveBplcon0;
    private int _effectivePlaneCount;
    private uint _fetchOrder;
    private int _terminalFetchOffset;
    private ushort _pendingBplcon0;
    private long _pendingBplcon0Cycle;
    private bool _startPermit;
    private bool _runActive;
    // Updated at the canonical sync edge; no clock-object lookup per DMA CCK.
    private bool _beamStopped;
    private bool _dmaEnabled;
    private long _sequenceOriginCycle;
    private long _terminalUnitStartCycle;
    private long _completionCycle;
    private int _ownerLine;
    private bool _hasPendingOutput;
    private int _pendingPlane;
    private uint _pendingAddress;
    private bool _pendingModulo;
    private long _pendingOutputCycle;

    internal long NextCycle { get; private set; } = long.MaxValue;
    internal long PendingOutputCycle => _hasPendingOutput ? _pendingOutputCycle : long.MaxValue;
    internal long LastInputCycle { get; private set; } = long.MinValue;
    internal long LastOutputCycle { get; private set; } = long.MinValue;
    internal int LastPlane { get; private set; } = -1;
    internal uint LastAddress { get; private set; }
    internal ushort EffectiveBplcon0 => _effectiveBplcon0;
    internal bool RunActive => _runActive;
    internal int OwnerLine => _ownerLine;

    internal void Reset()
    {
        _fmode = _pendingFmode = _pendingMode = 0; _pendingFmodeCycle = long.MaxValue;
        _fetchPeriod = _fetchUnit = _maxFetchPlanes = 8;
        Array.Clear(_pointers);
        Array.Clear(_dataLatches);
        _effectiveBplcon0 = 0;
        _effectivePlaneCount = 0;
        _ddfMask = DdfMask;
        _fetchOrder = LowResFetchOrder;
        _terminalFetchOffset = 0;
        _pendingBplcon0 = 0;
        _pendingBplcon0Cycle = long.MaxValue;
        _startPermit = false;
        _runActive = false;
        _beamStopped = false;
        _dmaEnabled = false;
        _sequenceOriginCycle = long.MaxValue;
        _terminalUnitStartCycle = long.MaxValue;
        _completionCycle = long.MaxValue;
        _ownerLine = -1;
        _hasPendingOutput = false;
        _pendingPlane = -1;
        _pendingAddress = 0;
        _pendingModulo = false;
        _pendingOutputCycle = long.MaxValue;
        NextCycle = long.MaxValue;
        LastInputCycle = long.MinValue;
        LastOutputCycle = long.MinValue;
        LastPlane = -1;
        LastAddress = 0;
    }

    internal void OnRegisterWrite(
        ushort offset,
        ushort value,
        long cycle,
        LightweightA500Machine machine)
    {
        if (_aga && offset == LightweightRegisters.Fmode)
        {
            _pendingFmode = value & 0x4003;
            _pendingFmodeCycle = LightweightBusArbiter.AlignToSlot(cycle + 2);
            Publish(_pendingFmodeCycle);
        }
        if (offset == LightweightRegisters.DmaconWrite)
            _dmaEnabled = IsDmaEnabled(machine.Dmacon);
        if (offset == LightweightRegisters.Bplcon0)
        {
            _pendingBplcon0 = value;
            // Public bus strobes are even; direct owner input can occur at an
            // odd CPU phase. Publish the first physical CCK at/after the delay.
            _pendingBplcon0Cycle = LightweightBusArbiter.AlignToSlot(
                cycle + LightweightClock.CpuCyclesPerColorClock);
            Publish(_pendingBplcon0Cycle);
        }
        else if (offset >= LightweightRegisters.BplPointerFirst &&
            offset <= LightweightRegisters.BplPointerLast)
        {
            var plane = (offset - LightweightRegisters.BplPointerFirst) >> 2;
            if ((uint)plane < (uint)_pointers.Length)
            {
                _pointers[plane] = machine.GetBitplanePointer(plane);
            }
        }

        if (ShouldClock())
        {
            Publish(NextCckAfter(cycle));
        }
    }

    internal void OnFrameStart(long cycle, LightweightA500Machine machine)
    {
        _startPermit = false;
        _runActive = false;
        _sequenceOriginCycle = long.MaxValue;
        _terminalUnitStartCycle = long.MaxValue;
        _completionCycle = long.MaxValue;
        _ownerLine = -1;
        if (ShouldClock())
        {
            Publish(NextCckAfter(cycle));
        }
    }

    private long _syncStopCycle;

    internal void OnBeamSyncChanged(long cycle, LightweightA500Machine machine)
    {
        _beamStopped = !machine.BeamSyncRunning;
        if (_beamStopped)
        {
            _syncStopCycle = cycle;
            NextCycle = Math.Min(_pendingFmodeCycle, Math.Min(_pendingBplcon0Cycle, PendingOutputCycle));
        }
        else
        {
            var delta = (cycle & ~1L) - _syncStopCycle;
            if (_sequenceOriginCycle != long.MaxValue) _sequenceOriginCycle += delta;
            if (_terminalUnitStartCycle != long.MaxValue) _terminalUnitStartCycle += delta;
            if (_completionCycle != long.MaxValue) _completionCycle += delta;
            Publish(NextCckAfter(cycle));
        }
    }

    internal void Step(long cycle, LightweightA500Machine machine)
    {
        System.Diagnostics.Debug.Assert(
            cycle == NextCycle,
            "Bitplanes must advance at their published CCK.");

        if (_hasPendingOutput && _pendingOutputCycle == cycle)
        {
            CompleteOutput(cycle, machine);
        }

        var modeChanged = _pendingFmodeCycle <= cycle;
        if (modeChanged) { _fmode = _pendingFmode; _pendingFmodeCycle = long.MaxValue; }
        if (_pendingBplcon0Cycle <= cycle || modeChanged)
        {
            if (_pendingBplcon0Cycle <= cycle)
            {
                _effectiveBplcon0 = _pendingBplcon0;
                _pendingBplcon0Cycle = long.MaxValue;
            }
            _effectivePlaneCount = GetSupportedPlaneCount(_effectiveBplcon0);
            var hires = (_effectiveBplcon0 & 0x8000) != 0;
            var super = _ecs && (_effectiveBplcon0 & 0x8040) == 0x40;
            _fetchOrder = super ? SuperHiresFetchOrder : hires ? HighResFetchOrder : LowResFetchOrder;
            _terminalFetchOffset = super ? 12 : hires ? 8 : 0;
            _ddfMask = super ? 0xFE : DdfMask;
            if (_aga)
            {
                var res = super ? 2 : hires ? 1 : 0;
                var fetch = _fmode & 3;
                var mode = fetch == 3 ? 2 : fetch == 0 ? 0 : 1;
                _fetchPeriod = (8 << mode) >> res;
                _fetchUnit = Math.Max(8, _fetchPeriod);
                _maxFetchPlanes = Math.Min(8, _fetchPeriod);
                _effectivePlaneCount = GetSupportedPlaneCount(_effectiveBplcon0);
                _terminalFetchOffset = (_fetchUnit - _fetchPeriod) * 2;
            }
        }

        if (_beamStopped)
        {
            NextCycle = Math.Min(_pendingFmodeCycle, Math.Min(_pendingBplcon0Cycle, PendingOutputCycle));
            return;
        }

        var dmaEnabled = _dmaEnabled;
        var planeCount = _effectivePlaneCount;
        AdvanceDdfControl(cycle, machine, dmaEnabled, planeCount);
        TryAcceptInput(cycle, machine, planeCount);

        if (_runActive || _hasPendingOutput)
        {
            // An active sequencer must observe the next CCK. Register input
            // and accepted RAM output cannot precede that physical phase, so
            // repeated minimum calculations cannot select an earlier event.
            NextCycle = cycle + LightweightClock.CpuCyclesPerColorClock;
            System.Diagnostics.Debug.Assert(_pendingBplcon0Cycle >= NextCycle);
            System.Diagnostics.Debug.Assert(!_hasPendingOutput || _pendingOutputCycle >= NextCycle);
            return;
        }

        NextCycle = Math.Min(_pendingFmodeCycle, _pendingBplcon0Cycle);
        if (dmaEnabled && planeCount != 0)
        {
            PublishNextInactiveControlCycle(cycle, machine);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool OwnsOutputSlot(long cycle)
        => _hasPendingOutput && _pendingOutputCycle == cycle || LastOutputCycle == cycle;

    internal uint GetPointer(int plane)
        => (uint)plane < (uint)_pointers.Length ? _pointers[plane] : 0;

    internal ushort GetDataLatch(int plane)
        => (uint)plane < (uint)_dataLatches.Length ? _dataLatches[plane] : (ushort)0;

    private void AdvanceDdfControl(
        long cycle,
        LightweightA500Machine machine,
        bool dmaEnabled,
        int planeCount)
    {
        var horizontal = machine.BeamColorClock;
        var hardLimits = !_ecs || (_effectiveBplcon0 & 0x20) == 0;
        if (_runActive && (!dmaEnabled || planeCount == 0))
        {
            _runActive = false;
            _sequenceOriginCycle = long.MaxValue;
            _terminalUnitStartCycle = long.MaxValue;
            _completionCycle = long.MaxValue;
            _ownerLine = -1;
        }
        if (!_startPermit && horizontal == (hardLimits ? DdfLimitReleaseHorizontal : 0))
        {
            _startPermit = true;
        }

        if (_runActive)
        {
            if (cycle < _completionCycle)
            {
                if (_completionCycle == long.MaxValue)
                {
                    if (horizontal == (machine.DdfStop & _ddfMask))
                        ArmStop(cycle, cycle);
                    else if (hardLimits && horizontal == DdfHardStopCompareHorizontal)
                        ArmStop(cycle, cycle + LightweightClock.CpuCyclesPerColorClock);
                }
                // The running sequencer cannot start again at this CCK.
                // Keep the inactive-window tests out of its steady path.
                return;
            }
            _runActive = false;
            _startPermit = false;
            _sequenceOriginCycle = long.MaxValue;
            _terminalUnitStartCycle = long.MaxValue;
            _completionCycle = long.MaxValue;
            _ownerLine = -1;
        }

        if (_startPermit && dmaEnabled && planeCount != 0 &&
            horizontal == (machine.GetCustomRegister(
                LightweightRegisters.Ddfstrt) & _ddfMask) &&
            IsVerticalWindowOpen(machine))
        {
            _runActive = true;
            _sequenceOriginCycle = cycle;
            _terminalUnitStartCycle = long.MaxValue;
            _completionCycle = long.MaxValue;
            _ownerLine = machine.BeamLine;
        }
    }

    private void TryAcceptInput(
        long cycle,
        LightweightA500Machine machine,
        int planeCount)
    {
        if (!_runActive)
        {
            return;
        }

        // AdvanceDdfControl has already retired a completed run at this CCK.
        // Starts and sync recovery cannot place its origin after this cycle.
        System.Diagnostics.Debug.Assert(cycle >= _sequenceOriginCycle && cycle < _completionCycle);

        // The active-run invariant makes this delta non-negative. One CCK is two
        // CPU cycles, so an arithmetic shift is the exact phase conversion
        // without the signed-division correction sequence.
        var deltaCcks = (cycle - _sequenceOriginCycle) >> 1;
        var slot = (int)(deltaCcks & (FetchUnitColorClocks - 1));
        var plane = (int)((_fetchOrder >> (slot * 3)) & 7) - 1;
        if (_aga)
        {
            slot = (int)(deltaCcks & (_fetchPeriod - 1));
            if (slot >= _maxFetchPlanes) return;
            var sequence = _maxFetchPlanes == 8 ? 0x15372648u : _maxFetchPlanes == 4 ? 0x1324u : 0x12u;
            plane = (int)(sequence >> (slot * 4) & 15) - 1;
        }
        if ((uint)plane >= (uint)planeCount)
        {
            return;
        }

        var outputCycle = cycle + LightweightClock.CpuCyclesPerColorClock;
        if (!machine.CanBitplaneOwnOutputSlot(outputCycle))
        {
            return;
        }

        _hasPendingOutput = true;
        _pendingPlane = plane; _pendingMode = _fmode;
        _pendingAddress = _pointers[plane];
        _pendingModulo = _terminalUnitStartCycle != long.MaxValue &&
            cycle >= _terminalUnitStartCycle + _terminalFetchOffset;
        _pendingOutputCycle = outputCycle;
#if LIGHTWEIGHT_DIAGNOSTICS
        LastInputCycle = cycle;
#endif
    }

    private void CompleteOutput(long cycle, LightweightA500Machine machine)
    {
        var plane = _pendingPlane;
        var address = _pendingAddress;
        var applyModulo = _pendingModulo;
        _hasPendingOutput = false;
        _pendingOutputCycle = long.MaxValue;
        var mode = _pendingMode & 3;
        var bits = mode == 3 ? 64 : mode == 0 ? 16 : 32;
        var wide = _aga ? machine.ReadAgaBitplane(address, mode) : machine.ReadChipWordBus(address);
        var value = (ushort)(wide >> (bits - 16));
        _dataLatches[plane] = value;
        if (_aga) machine.OnBitplaneWideOutput(plane, wide, bits, cycle);
        else machine.OnBitplaneDataOutput(plane, value, cycle);
        var pointer = MaskAddress(address + (uint)(_aga ? bits / 8 : 2));
        if (applyModulo)
        {
            // A terminal group may cross horizontal sync. BSCAN2 selects
            // by the beam at the modulo phase, not the DDF owner's row.
            var modulo = (_pendingMode & 0x4000) != 0
                ? machine.GetScanDoubleModulo(machine.BeamLine) : machine.GetBitplaneModulo(plane);
            pointer = MaskAddress(unchecked(pointer + (uint)modulo));
        }
        _pointers[plane] = pointer;
        machine.SetBitplanePointerFromDma(plane, pointer);
        LastOutputCycle = cycle;
#if LIGHTWEIGHT_DIAGNOSTICS
        LastPlane = plane;
        LastAddress = address;
#endif
    }

    private void ArmStop(long cycle, long targetCycle)
    {
        _ = cycle;
        var unitCycles = (_aga ? _fetchUnit : FetchUnitColorClocks) *
            LightweightClock.CpuCyclesPerColorClock;
        var delta = Math.Max(0, targetCycle - _sequenceOriginCycle);
        var units = (delta + unitCycles - 1) / unitCycles;
        _terminalUnitStartCycle = _sequenceOriginCycle + (units * unitCycles);
        _completionCycle = _terminalUnitStartCycle + unitCycles;
    }

    private static bool IsVerticalWindowOpen(LightweightA500Machine machine)
    {
        if (machine.IsEcsAgnus && machine.DiwHighValid)
        {
            var window = machine.DmaDisplayWindow;
            return machine.BeamLine >= window.VerticalStart && machine.BeamLine < window.VerticalStop ||
                window.VerticalStop > 4096 && machine.BeamLine < window.VerticalStop - 4096;
        }
        var start = machine.GetCustomRegister(LightweightRegisters.Diwstrt) >> 8;
        var stop = machine.GetCustomRegister(LightweightRegisters.Diwstop) >> 8;
        // OCS supplies V8 as the complement of V7, not from a comparison
        // with VSTART (e.g. $3C means line $13C even when VSTART is $2C).
        if (stop < 0x80)
        {
            stop += 0x100;
        }
        if (stop <= start)
        {
            stop += 0x100;
        }
        return machine.BeamLine >= start && machine.BeamLine < stop;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetSupportedPlaneCount(ushort bplcon0)
    {
        var count = (bplcon0 >> 12) & 7;
        if (_aga) { count |= (bplcon0 & 16) >> 1; return count <= _maxFetchPlanes ? count : 0; }
        if (_ecs && (bplcon0 & 0x8040) == 0x40) return Math.Min(count, 2);
        if ((bplcon0 & 0x8000) != 0) return count <= 4 ? count : 0;
        // OCS Agnus aliases BPU=7 to four lores DMA channels. Denise still
        // decodes the six physical data latches, including retained BPL5/6DAT.
        return count == 7 ? (_ecs ? 6 : 4) : count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldClock()
        => _runActive || _hasPendingOutput || _dmaEnabled && _effectivePlaneCount != 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDmaEnabled(ushort dmacon)
        => (dmacon & (DmaconMaster | DmaconBitplane)) ==
            (DmaconMaster | DmaconBitplane);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint MaskAddress(uint address) => address & _addressMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long NextCckAfter(long cycle)
        => LightweightBusArbiter.AlignToSlot(cycle + 1);

    private void PublishNextInactiveControlCycle(
        long cycle,
        LightweightA500Machine machine)
    {
        var horizontal = machine.BeamColorClock;
        var target = !_startPermit
            ? (_ecs && (_effectiveBplcon0 & 0x20) != 0 ? 0 : DdfLimitReleaseHorizontal)
            : machine.GetCustomRegister(LightweightRegisters.Ddfstrt) & _ddfMask;
        var colorClocksPerLine = machine.ColorClocksPerLine;
        if ((uint)target >= (uint)colorClocksPerLine)
        {
            return;
        }

        var delta = target - horizontal;
        if (delta <= 0)
        {
            delta += colorClocksPerLine;
        }
        Publish(cycle + ((long)delta * LightweightClock.CpuCyclesPerColorClock));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Publish(long cycle)
        => NextCycle = Math.Min(NextCycle, cycle);
}
