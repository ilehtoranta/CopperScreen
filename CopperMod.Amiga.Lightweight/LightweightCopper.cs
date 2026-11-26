using System.Runtime.CompilerServices;

namespace CopperMod.Amiga.Lightweight;

/// <summary>
/// Compact OCS Copper state. Address input and Chip-RAM output are separate
/// CCK phases; accepted addresses survive later pointer and control writes.
/// </summary>
internal sealed class LightweightCopper
{
    private enum ControlStage : byte
    {
        Dormant,
        ReadFirst,
        ReadSecond,
        WaitIdle,
        WaitCompare,
        SkipIdle,
        SkipDelay,
        SkipCompare,
        End,
        FrameRestart,
        JumpDelay,
        JumpFirstSlot,
        JumpRestart,
        LoadJumpPointer
    }

    private enum WordPurpose : byte
    {
        First,
        Second,
        FrameRestart,
        JumpRestart
    }

    private const ushort DmaconMaster = 0x0200;
    private const ushort DmaconCopper = 0x0080;
    private readonly uint _addressMask;

    internal LightweightCopper(uint addressMask = 0x0007_FFFEu) => _addressMask = addressMask;

    private ControlStage _stage;
    private WordPurpose _pendingPurpose;
    private uint _programCounter;
    private uint _pendingAddress;
    private long _pendingOutputCycle;
    private uint _decodeGeneration;
    private uint _pendingGeneration;
    private ushort _firstWord;
    private ushort _waitFirst;
    private ushort _waitSecond;
    private bool _skipNextMove;
    private bool _beamStopped;
    private bool _dmaEnabled;
    private bool _jumpSecondList;
    private uint _jumpDummyAddress;

    internal long NextCycle { get; private set; } = long.MaxValue;
    internal long PendingOutputCycle => _pendingOutputCycle;
    internal long LastOutputCycle { get; private set; } = long.MinValue;
    internal long LastAcceptedInputCycle { get; private set; } = long.MinValue;
    internal long LastInstructionFirstInputCycle { get; private set; } = long.MinValue;
    internal long LastMoveCycle { get; private set; } = long.MinValue;
    internal uint ProgramCounter => _programCounter;
    internal bool IsWaiting => _stage is ControlStage.WaitIdle or ControlStage.WaitCompare or ControlStage.End;
    internal bool HasPendingOutput => _pendingOutputCycle != long.MaxValue;

    internal void Reset()
    {
        _stage = ControlStage.Dormant;
        _pendingPurpose = default;
        _programCounter = 0;
        _pendingAddress = 0;
        _pendingOutputCycle = long.MaxValue;
        _decodeGeneration = 0;
        _pendingGeneration = 0;
        _firstWord = 0;
        _waitFirst = 0;
        _waitSecond = 0;
        _skipNextMove = false;
        _beamStopped = false;
        _dmaEnabled = false;
        _jumpSecondList = false;
        _jumpDummyAddress = 0;
        NextCycle = long.MaxValue;
        LastOutputCycle = long.MinValue;
        LastAcceptedInputCycle = long.MinValue;
        LastInstructionFirstInputCycle = long.MinValue;
        LastMoveCycle = long.MinValue;
    }

    internal void OnDmaconChanged(
        ushort previous,
        ushort current,
        long cycle,
        LightweightA500Machine machine)
    {
        var wasEnabled = IsDmaEnabled(previous);
        var enabled = IsDmaEnabled(current);
        _dmaEnabled = enabled;
        if (!wasEnabled && enabled)
        {
            if (_stage == ControlStage.Dormant)
            {
                Restart(machine.GetCopperListPointer(secondList: false), cycle);
            }
            ActivateAfter(cycle);
        }
        else if (wasEnabled && !enabled)
        {
            NextCycle = HasPendingOutput ? _pendingOutputCycle : long.MaxValue;
        }
    }

    internal void OnRegisterWrite(
        ushort offset,
        long cycle,
        LightweightA500Machine machine)
    {
        if (offset is not (LightweightRegisters.Copjmp1 or LightweightRegisters.Copjmp2))
            return;

        var previousAddress = _stage is ControlStage.JumpDelay or ControlStage.JumpFirstSlot or
            ControlStage.JumpRestart or ControlStage.LoadJumpPointer ? _jumpDummyAddress : _programCounter;
        var secondList = offset == LightweightRegisters.Copjmp2;
        Restart(machine.GetCopperListPointer(secondList), cycle);
        if (_dmaEnabled)
        {
            if (!machine.IsEcsAgnus)
            {
                _jumpSecondList = secondList;
                _jumpDummyAddress = previousAddress;
                _stage = ControlStage.JumpDelay;
            }
            ActivateAfter(cycle);
        }
    }

    internal void OnFrameStart(long cycle, LightweightA500Machine machine)
    {
        if (!machine.IsEcsAgnus)
        {
            // The vertical strobe starts a dummy read at the old Copper PC
            // before the first output from COP1LC on OCS. The automatic
            // strobe can reserve the H0 input phase; its H1 output reads the
            // old pointer before COP1LC is loaded.
            Restart(_programCounter, cycle);
            _stage = ControlStage.FrameRestart;
            NextCycle = _dmaEnabled && !_beamStopped ? cycle : _pendingOutputCycle;
            return;
        }

        Restart(machine.GetCopperListPointer(secondList: false), cycle);
        if (_dmaEnabled)
        {
            ActivateAfter(cycle);
        }
        else
        {
            // VSYNC requests a restart even with DMA off. The list address is
            // consumed when the pending restart obtains its bus cycle, so a
            // later COP1LC write must not leave us executing the previous list.
            // Ordinary mid-field DMA pauses do not request another restart.
            _stage = ControlStage.FrameRestart;
            NextCycle = HasPendingOutput ? _pendingOutputCycle : long.MaxValue;
        }
    }

    internal void OnBeamSyncChanged(long cycle, LightweightA500Machine machine)
    {
        _beamStopped = !machine.BeamSyncRunning;
        NextCycle = HasPendingOutput ? _pendingOutputCycle : long.MaxValue;
        if (!_beamStopped && _dmaEnabled) ActivateAfter(cycle);
    }

    internal void Step<TPath>(long cycle, LightweightA500Machine machine)
        where TPath : struct, ILightweightExecutionPath
    {
        System.Diagnostics.Debug.Assert(
            cycle == NextCycle,
            "Copper must advance at its published CCK.");

        if (_pendingOutputCycle == cycle)
        {
            CompleteWord(cycle, machine);
        }

        if (_beamStopped || !_dmaEnabled)
        {
            NextCycle = HasPendingOutput ? _pendingOutputCycle : long.MaxValue;
            return;
        }

        var horizontal = machine.BeamColorClock;
        var phase = ClassifyInput(horizontal, TPath.EnhancedDisplay ? machine.ColorClocksPerLine : 227);
        switch (_stage)
        {
            case ControlStage.JumpDelay:
                // The strobe crosses one CCK before it can request a restart.
                _stage = phase == CopperInputPhase.Normal ? ControlStage.JumpRestart : ControlStage.JumpFirstSlot;
                break;
            case ControlStage.JumpFirstSlot:
                // When the strobe reaches the first input before a RAM grant
                // has been reserved, only the internal pointer load retires.
                _stage = phase == CopperInputPhase.Normal && !HasPendingOutput &&
                    machine.CanCopperOwnOutputSlot(cycle + LightweightClock.CpuCyclesPerColorClock, normalInputPhase: true)
                    ? ControlStage.LoadJumpPointer : ControlStage.JumpRestart;
                break;
            case ControlStage.LoadJumpPointer:
                LoadJumpPointer(machine);
                break;
            default:
                if (phase == CopperInputPhase.Normal)
                    AdvanceControlInput(cycle, machine);
                break;
        }
        // The 227-CCK wrap dummy consumes no instruction address. Its
        // undocumented data-bus value is deliberately not exposed in H2.

        // Input acceptance cannot write DMACON or BPLCON0; MOVE outputs above
        // have already updated both gates before this input phase.
        // End already has no control transition. Frame restart and COPJMP
        // explicitly reactivate it; do not publish empty per-CCK work meanwhile.
        NextCycle = HasPendingOutput || _stage is not (ControlStage.Dormant or ControlStage.End)
                ? cycle + LightweightClock.CpuCyclesPerColorClock
                : long.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool OwnsPendingOutputSlot(long cycle)
        => _pendingOutputCycle == cycle;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool OwnsOutputSlot(long cycle)
        => _pendingOutputCycle == cycle || LastOutputCycle == cycle;

    private void AdvanceControlInput(long cycle, LightweightA500Machine machine)
    {
        if (HasPendingOutput)
        {
            return;
        }

        switch (_stage)
        {
            case ControlStage.FrameRestart:
                TryAcceptWord(cycle, WordPurpose.FrameRestart, machine);
                break;
            case ControlStage.JumpRestart:
                TryAcceptWord(cycle, WordPurpose.JumpRestart, machine);
                break;
            case ControlStage.ReadFirst:
                TryAcceptWord(cycle, WordPurpose.First, machine);
                break;
            case ControlStage.ReadSecond:
                TryAcceptWord(cycle, WordPurpose.Second, machine);
                break;
            case ControlStage.WaitIdle:
                // The post-decode phase advances on a free Copper clock even
                // though it does not transfer a word.
                if (machine.CanCopperOwnOutputSlot(cycle + LightweightClock.CpuCyclesPerColorClock,
                    normalInputPhase: true))
                {
                    _stage = ControlStage.WaitCompare;
                }
                break;
            case ControlStage.WaitCompare:
                // WAIT's wake-up memory cycle yields to higher-priority DMA
                // even though it does not fetch an instruction word (HRM ch. 2).
                // Testing after the comparator avoids bus checks while asleep.
                if (ComparisonSatisfied(machine, _waitFirst, _waitSecond) &&
                    machine.CanCopperOwnOutputSlot(cycle + LightweightClock.CpuCyclesPerColorClock,
                        normalInputPhase: true))
                {
                    _stage = ControlStage.ReadFirst;
                }
                break;
            case ControlStage.SkipIdle:
                _stage = ControlStage.SkipDelay;
                break;
            case ControlStage.SkipDelay:
                _stage = ControlStage.SkipCompare;
                break;
            case ControlStage.SkipCompare:
                _skipNextMove = ComparisonSatisfied(machine, _waitFirst, _waitSecond);
                _stage = ControlStage.ReadFirst;
                TryAcceptWord(cycle, WordPurpose.First, machine);
                break;
        }
    }

    private void TryAcceptWord(
        long inputCycle,
        WordPurpose purpose,
        LightweightA500Machine machine)
    {
        var outputCycle = inputCycle + LightweightClock.CpuCyclesPerColorClock;
        if (!machine.CanCopperOwnOutputSlot(outputCycle, normalInputPhase: true))
        {
            return;
        }

        _pendingPurpose = purpose;
        _pendingAddress = purpose == WordPurpose.JumpRestart ? _jumpDummyAddress : _programCounter;
        _pendingOutputCycle = outputCycle;
        _pendingGeneration = _decodeGeneration;
        if (purpose is WordPurpose.First or WordPurpose.Second)
            _programCounter = MaskAddress(_programCounter + 2);
#if LIGHTWEIGHT_DIAGNOSTICS
        LastAcceptedInputCycle = inputCycle;
        if (purpose == WordPurpose.First)
        {
            LastInstructionFirstInputCycle = inputCycle;
        }
#endif
    }

    private void CompleteWord(long cycle, LightweightA500Machine machine)
    {
        var purpose = _pendingPurpose;
        var address = _pendingAddress;
        var generation = _pendingGeneration;
        _pendingOutputCycle = long.MaxValue;
        LastOutputCycle = cycle;
        var value = machine.ReadChipWordBus(address);
        if (generation != _decodeGeneration)
        {
            return;
        }

        if (purpose == WordPurpose.First)
        {
            _firstWord = value;
            _stage = ControlStage.ReadSecond;
            return;
        }

        if (purpose == WordPurpose.Second)
        {
            DecodeInstruction(value, cycle, machine);
        }
        else if (purpose == WordPurpose.JumpRestart)
        {
            LoadJumpPointer(machine);
        }
        else
        {
            // The restart's retained RAM output is a dummy read, not IR1.
            // Sample COP1LC here, after arbitration and before the first fetch.
            _programCounter = MaskAddress(machine.GetCopperListPointer(secondList: false));
            _stage = ControlStage.ReadFirst;
        }
    }

    private void LoadJumpPointer(LightweightA500Machine machine)
    {
        _programCounter = MaskAddress(machine.GetCopperListPointer(_jumpSecondList));
        _stage = ControlStage.ReadFirst;
    }

    private void DecodeInstruction(
        ushort secondWord,
        long cycle,
        LightweightA500Machine machine)
    {
        var firstWord = _firstWord;
        if ((firstWord & 1) == 0)
        {
            _stage = ControlStage.ReadFirst;
            var offset = (ushort)(firstWord & 0x01FE);
            // SKIP inhibits the register write, not MOVE protection. Even a
            // skipped illegal destination stops Copper (vAmigaTS copskip4).
            if (!CanWriteRegister(offset, machine.GetCustomRegister(LightweightRegisters.Copcon), machine.IsEcsAgnus))
            {
                _stage = ControlStage.Dormant;
                return;
            }

            if (_skipNextMove)
            {
                _skipNextMove = false;
                return;
            }

#if LIGHTWEIGHT_DIAGNOSTICS
            LastMoveCycle = cycle;
#endif
            machine.WriteCustomRegisterFromCopper(offset, secondWord, cycle);
            return;
        }

        _waitFirst = firstWord;
        _waitSecond = secondWord;
        if (firstWord == 0xFFFF && secondWord == 0xFFFE)
        {
            _stage = ControlStage.End;
            return;
        }

        _stage = (secondWord & 1) == 0
            ? ControlStage.WaitIdle
            : ControlStage.SkipIdle;
    }

    private static bool ComparisonSatisfied(
        LightweightA500Machine machine,
        ushort firstWord,
        ushort secondWord)
    {
        if ((secondWord & 0x8000) == 0 && machine.BlitterBusy)
        {
            return false;
        }

        // VP7 is always compared. IR2 bit 15 controls BFD, not its mask.
        var mask = (ushort)(0x8000 | (secondWord & 0x7FFE));
        // The nominal counter wraps before the physical line boundary, just
        // as it does for Copper input opportunities. Do not compare a late
        // WAIT/SKIP against an impossible HP beyond the fitted line length.
        var horizontal = machine.BeamColorClock + 3;
        if (horizontal >= machine.ColorClocksPerLine)
            horizontal %= machine.ColorClocksPerLine;
        var beam = (ushort)(((machine.BeamLine & 0xFF) << 8) |
            (horizontal & 0x00FE));
        var target = (ushort)(firstWord & mask);
        var observed = (ushort)(beam & mask);
        return observed >= target;
    }

    private void Restart(uint address, long cycle)
    {
        _decodeGeneration++;
        _programCounter = MaskAddress(address);
        _firstWord = 0;
        _waitFirst = 0;
        _waitSecond = 0;
        _skipNextMove = false;
        _stage = ControlStage.ReadFirst;
        if (NextCycle != long.MaxValue)
        {
            ActivateAfter(cycle);
        }
    }

    private void ActivateAfter(long cycle)
    {
        var next = LightweightBusArbiter.AlignToSlot(cycle + 1);
        if (next <= cycle)
        {
            next = cycle + LightweightClock.CpuCyclesPerColorClock;
        }
        NextCycle = HasPendingOutput ? Math.Min(_pendingOutputCycle, next) : next;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDmaEnabled(ushort dmacon)
        => (dmacon & (DmaconMaster | DmaconCopper)) ==
            (DmaconMaster | DmaconCopper);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint MaskAddress(uint address) => address & _addressMask;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanWriteRegister(ushort offset, ushort copcon, bool ecs)
        => offset >= ((copcon & 0x0002) == 0 ? 0x080 : ecs ? 0 : 0x040);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static CopperInputPhase ClassifyInput(int physicalHorizontal, int lineLength)
    {
        if (lineLength != 227)
        {
            // Legacy AgnusCopperDmaPhases: opportunities follow the nominal
            // counter's low-bit transition, including the real line wrap.
            var current = (physicalHorizontal + 3) % lineLength;
            var previous = current == 0 ? lineLength - 1 : current - 1;
            var toggled = ((previous ^ current) & 1) != 0;
            return (current & 1) != 0 ? toggled ? CopperInputPhase.Normal : CopperInputPhase.None :
                toggled ? CopperInputPhase.None : CopperInputPhase.WrapDummy;
        }
        if (physicalHorizontal == 224)
        {
            return CopperInputPhase.WrapDummy;
        }
        if (physicalHorizontal == 225 ||
            physicalHorizontal < 224 && (physicalHorizontal & 1) == 0)
        {
            return CopperInputPhase.Normal;
        }
        return CopperInputPhase.None;
    }

    private enum CopperInputPhase : byte
    {
        None,
        Normal,
        WrapDummy
    }
}
