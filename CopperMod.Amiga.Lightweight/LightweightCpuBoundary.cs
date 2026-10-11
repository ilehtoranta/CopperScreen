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

    protected bool DispatchPendingInterrupt() => _machine.DispatchPendingCpuInterrupt();

    public virtual bool TryBeginBusAccessTraceBatch(
        M68kCpuState state,
        long targetCycle,
        out long batchTargetCycle)
    {
        _machine.AdvanceHardwareTo(state.Cycles);
        batchTargetCycle = _machine.ClampCpuBatchTarget(targetCycle);
        return batchTargetCycle > state.Cycles;
    }

    public void AfterBusAccessTraceBatch(
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

    public bool TryFastForwardStoppedInstruction(
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
internal sealed class LightweightJitCpuBoundary(LightweightA500Machine machine)
    : LightweightCpuBoundary(machine), IM68kPureCpuTraceBatchBoundary
{
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
