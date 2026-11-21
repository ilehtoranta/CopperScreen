using Copper68k;

namespace CopperMod.Amiga.Lightweight;

/// <summary>
/// Minimal Copper68k boundary for the single-clock A500 engine.
/// </summary>
/// <remarks>
/// Batches are bounded by the next device-visible edge. Bus transfers still
/// enter <see cref="IM68kBus"/> individually; this class does not speculate,
/// replay accesses, or maintain a second device timeline.
/// </remarks>
internal class LightweightCpuBoundary :
    IM68kInstructionBoundary,
    IM68kStoppedCpuFastForwardBoundary,
    IM68kConservativeBusAccessBatchBoundary
{
    private readonly LightweightA500Machine _machine;
    private int _scalarInstructionBudget;
    private int _scalarInstructions;

    internal LightweightCpuBoundary(LightweightA500Machine machine)
    {
        _machine = machine;
    }

    internal void BeginBatchProbe(int scalarInstructionBudget)
    {
        _scalarInstructionBudget = scalarInstructionBudget;
        _scalarInstructions = 0;
        _machine.AdvanceHardwareToCpuCycle();
    }

    public bool BeforeInstruction()
    {
        _machine.AdvanceHardwareToCpuCycle();
        return !_machine.DispatchPendingCpuInterrupt() &&
            _scalarInstructions < _scalarInstructionBudget;
    }

    public void AfterInstruction(long previousCycle, long currentCycle)
    {
        _ = previousCycle;
        _scalarInstructions++;
        _machine.AdvanceHardwareTo(currentCycle);
    }

    protected LightweightA500Machine Machine => _machine;
    protected bool DispatchPendingInterrupt() => _machine.DispatchPendingCpuInterrupt();
    public virtual bool PreservesBusDeadlineOnEarlyReturn => false;


    public virtual bool TryBeginBusAccessTraceBatch(
        M68kCpuState state,
        long targetCycle,
        out long batchTargetCycle)
    {
        _machine.AdvanceHardwareTo(state.Cycles);
        batchTargetCycle = _machine.ClampCpuBatchTarget(targetCycle);
        return batchTargetCycle > state.Cycles;
    }

    public virtual void AfterBusAccessTraceBatch(
        long previousCycle,
        long currentCycle,
        int instructionCount)
    {
        _ = previousCycle;
        if (instructionCount > 0)
        {
            _machine.AdvanceHardwareTo(currentCycle);
        }
    }

    public virtual bool TryFastForwardStoppedInstruction(
        M68kCpuState state,
        long targetCycle,
        out long advancedCycles)
    {
        var previousCycle = state.Cycles;
        var wakeCycle = _machine.ClampCpuBatchTarget(targetCycle);
        if (wakeCycle <= previousCycle)
        {
            advancedCycles = 0;
            return false;
        }

        _machine.AdvanceHardwareTo(wakeCycle);
        state.Cycles = wakeCycle;
        advancedCycles = wakeCycle - previousCycle;
        return true;
    }
}

// Only JIT exposes the pure compiled-slice contract. The 68000 interpreter's
// existing conservative boundary and execution path remain unchanged.
internal class LightweightJitCpuBoundary(LightweightA500Machine machine)
    : LightweightCpuBoundary(machine), IM68kPureCpuTraceBatchBoundary
{
    public override bool TryFastForwardStoppedInstruction(
        M68kCpuState state, long targetCycle, out long advancedCycles)
    {
        // Hand STOP back to the machine owner. It samples IPL between device
        // edges and can sleep to a field boundary with all fitted IPLs masked.
        advancedCycles = 0;
        return false;
    }

    public override bool TryBeginBusAccessTraceBatch(M68kCpuState state, long targetCycle, out long batchTargetCycle)
    {
        var allowed = base.TryBeginBusAccessTraceBatch(state, targetCycle, out batchTargetCycle);
        // Compiled slices may skip scalar BeforeInstruction. Accept pending
        // interrupts here, after hardware reaches the committed CPU cycle.
        if (DispatchPendingInterrupt()) { batchTargetCycle = state.Cycles; return false; }
        return allowed;
    }
    public bool TryBeginPureCpuTraceBatch(M68kCpuState state, long targetCycle, out long batchTargetCycle)
        => TryBeginBusAccessTraceBatch(state, targetCycle, out batchTargetCycle);
    public void AfterPureCpuTraceBatch(long previousCycle, long currentCycle, int instructionCount)
        => AfterBusAccessTraceBatch(previousCycle, currentCycle, instructionCount);
}

internal sealed class Lightweight040JitCpuBoundary(LightweightA500Machine machine)
    : LightweightJitCpuBoundary(machine), IM68kInstructionBoundary
{
    private long _targetCompletedFrames;

    public override bool PreservesBusDeadlineOnEarlyReturn => Machine.UsesAcceleratedCpuClock;

    internal void BeginFrameProbe(long targetCompletedFrames)
    {
        _targetCompletedFrames = targetCompletedFrames;
        Machine.AdvanceHardwareTo(Machine.ToBoardCycle(Machine.Cpu.Cycles));
    }

    bool IM68kInstructionBoundary.BeforeInstruction()
    {
        Machine.AdvanceHardwareTo(Machine.ToBoardCycle(Machine.Cpu.Cycles));
        // A live raster change can complete the field before the deadline
        // given to the CPU when the probe began.
        return Machine.CompletedFrames < _targetCompletedFrames && !DispatchPendingInterrupt();
    }

    void IM68kInstructionBoundary.AfterInstruction(long previousCycle, long currentCycle)
        => Machine.AdvanceHardwareTo(Machine.ToBoardCycle(currentCycle));

    public override bool TryBeginBusAccessTraceBatch(M68kCpuState state, long targetCycle, out long batchTargetCycle)
    {
        if (Machine.CompletedFrames >= _targetCompletedFrames)
        {
            batchTargetCycle = state.Cycles;
            return false;
        }
        if (!Machine.UsesAcceleratedCpuClock)
            return base.TryBeginBusAccessTraceBatch(state, targetCycle, out batchTargetCycle);

        Machine.AdvanceHardwareTo(Machine.ToBoardCycle(state.Cycles));
        if (Machine.CompletedFrames >= _targetCompletedFrames || DispatchPendingInterrupt())
        {
            batchTargetCycle = state.Cycles;
            return false;
        }
        var boardDeadline = Machine.ClampAcceleratedJitToDeviceEdge(Machine.ToBoardCycle(targetCycle));
        batchTargetCycle = Math.Min(targetCycle, Machine.ToCpuCycle(boardDeadline));
        return batchTargetCycle > state.Cycles;
    }

    public override void AfterBusAccessTraceBatch(long previousCycle, long currentCycle, int instructionCount)
    {
        if (instructionCount > 0)
            Machine.AdvanceHardwareTo(Machine.ToBoardCycle(currentCycle));
    }
}
