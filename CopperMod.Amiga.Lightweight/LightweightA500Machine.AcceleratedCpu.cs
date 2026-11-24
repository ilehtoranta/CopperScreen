using System.Numerics;
using System.Reflection;
using Copper68k;

namespace CopperMod.Amiga.Lightweight;

public sealed partial class LightweightA500Machine
{
    private readonly Action? _timedCpuBatchBoundary;

    internal bool UsesAcceleratedCpuClock => _cpuClockShift != 0;

    internal static void ValidateCpuClockMultiplier(int multiplier, M68kCpuModel model, M68kExecutionMode mode)
    {
        if (!BitOperations.IsPow2(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), "The CPU clock multiplier must be a positive power of two.");
        if (multiplier != 1 && (model != M68kCpuModel.M68040 || mode != M68kExecutionMode.Jit))
            throw new ArgumentException("An accelerated CPU clock requires 68040 JIT execution.", nameof(multiplier));
    }

    private static void EnableUncachedFastRamTraces(M68kCoreOptions options)
    {
        // The pinned Copper68k package predates this option. Its interpreter
        // fallback still uses the same accelerated clock until the pin is updated.
        typeof(M68kCoreOptions).GetProperty("EnableUncachedFastRamTraces")?.SetValue(options, true);
    }

    private static Action? GetTimedCpuBatchBoundaryRequest(IM68kCore cpu)
    {
        // Local development packages can opt in while the published pin
        // continues to use its existing device boundary.
        var request = cpu.GetType().GetMethod("GetTimedBusBoundaryRequest",
            BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        return request?.Invoke(cpu, null) as Action;
    }

    // This changes only the end of a 040 JIT slice. AdvanceHardwareTo still
    // executes every video and DMA phase, and a CPU bus access ends the slice.
    internal long ClampAcceleratedJitToDeviceEdge(long targetCycle)
    {
        if (_timedCpuBatchBoundary is null)
            return ClampCpuBatchTarget(targetCycle);

        var deadline = Math.Min(targetCycle, NextFrameCycle);
        deadline = Math.Min(deadline, _clock.NextLineCycle);
        deadline = Math.Min(deadline, _copper.NextCycle);
        deadline = Math.Min(deadline, _blitter.NextCycle);
        deadline = Math.Min(deadline, _paula.NextCycle);
        deadline = Math.Min(deadline, _nextDiskCycle);
        deadline = Math.Min(deadline, _diskDma.NextCycle);
        deadline = Math.Min(deadline, _nextVerticalBlankLatchCycle);
        deadline = Math.Min(deadline, _nextCiaInterruptCycle);
        deadline = Math.Min(deadline, _nextInterruptPinEvaluationCycle);
        deadline = Math.Min(deadline, _nextStorageCycle);
        return Math.Max(ToBoardCycle(_cpu.State.Cycles), deadline);
    }

    internal void RequireTimedCpuBusBoundary() => _timedCpuBatchBoundary?.Invoke();

    internal long ToCpuCycle(long boardCycle) => boardCycle << _cpuClockShift;
    internal long ToBoardCycle(long cpuCycle) => cpuCycle >> _cpuClockShift;

    internal void SynchronizeCpuToBoardCycle(long boardCycle)
        => _cpu.State.Cycles = Math.Max(_cpu.State.Cycles, ToCpuCycle(boardCycle));

    internal bool TryGetAcceleratedRam(uint address, int bytes, out byte[] memory, out int offset)
    {
        if (bytes > 0 && _fastRam is { } fast && fast.ContainsRange(address, bytes))
        {
            memory = fast.Memory;
            offset = (int)(address - fast.ConfiguredBase);
            return true;
        }

        memory = null!;
        offset = 0;
        return false;
    }

    internal bool TryGetAcceleratedRom(uint address, int bytes, out byte[] memory, out int offset)
    {
        if (address >= KickstartAlias)
            return TryGetRomRange(address, bytes, out memory, out offset);

        memory = null!;
        offset = 0;
        return false;
    }
}
