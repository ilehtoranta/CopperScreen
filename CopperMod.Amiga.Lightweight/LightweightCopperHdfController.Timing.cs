using Copper68k;
using CopperDisk;
using System.Buffers.Binary;

namespace CopperMod.Amiga.Lightweight;

internal sealed partial class CopperHdfController
{
    private readonly record struct TimedRequest(uint Address, uint Destination, ulong Sector, int Bytes);
    private sealed class TimedUnit(int number, LightweightStorageDrive drive)
    {
        internal readonly int Number = number;
        internal readonly LightweightStorageDrive Drive = drive;
        internal readonly TimedRequest[] Queue = new TimedRequest[64];
        internal readonly byte[] Staging = new byte[1024 * 1024];
        internal TimedRequest Active;
        internal int Head, Count, Outstanding, Completed, Copied;
        internal bool Running, WaitingSector;
        internal long PhaseCycle = long.MaxValue;
        internal int Phase;
    }
    private readonly record struct ReadyRequest(uint Address, TimedUnit Unit);
    private TimedUnit[] _timedUnits = [];
    private ReadyRequest[] _readyRequests = [];
    private int _readyHead, _readyCount;
    private uint _completionLatch;
    private bool _timedInterruptInstalled;
    private ILightweightStorageObserver? _storageObserver;
    internal bool TimedInterrupt => _readyCount != 0;
    internal long NextTimedCycle
    {
        get
        {
            var next = long.MaxValue;
            foreach (var unit in _timedUnits) next = Math.Min(next, Math.Min(unit.PhaseCycle, unit.Drive.NextCycle));
            return next;
        }
    }

    internal void ConfigureTimings(IReadOnlyDictionary<int, LightweightDriveTimingProfile> profiles, ILightweightStorageObserver? observer)
    {
        if (profiles.Count == 0) return;
        var units = new TimedUnit[profiles.Count]; var index = 0;
        foreach (var pair in profiles.OrderBy(p => p.Key))
        {
            if (!_hardfiles.TryGetValue(pair.Key, out var media) || !media.ReadOnly)
                throw new ArgumentException("Timed CopperHDF profiles require an attached read-only unit.");
            units[index++] = new(pair.Key, new LightweightStorageDrive(media, pair.Value, observer));
        }
        _storageObserver = observer; _timedUnits = units; _readyRequests = new ReadyRequest[checked(units.Length * 64)];
    }

    internal void ResetTimedStorage()
    {
        _readyHead = _readyCount = 0; _completionLatch = 0; _timedInterruptInstalled = false;
        foreach (var unit in _timedUnits)
        { unit.Head = unit.Count = unit.Outstanding = 0; unit.Running = unit.WaitingSector = false; unit.PhaseCycle = long.MaxValue; unit.Drive.Reset(); }
    }

    private bool TryQueueTimedRead(LightweightHdfBus bus, uint request, M68kCpuState state)
    {
        if (_timedUnits.Length == 0) return false;
        var command = bus.ReadWord(request + IoCommandOffset);
        if (command is not (CmdRead or TdRead64 or CmdUpdate or CmdClear or CmdFlush)) return false;
        TimedUnit? unit = null; var number = ReadUnit(bus, request);
        foreach (var candidate in _timedUnits) if (candidate.Number == number) { unit = candidate; break; }
        if (unit is null) return false;
        var read = command is CmdRead or TdRead64;
        var length = read ? bus.ReadLong(request + IoLengthOffset) : 0;
        var destination = read ? bus.ReadLong(request + IoDataOffset) : 0;
        var offset = read ? command == TdRead64 ? ReadOffset64(bus, request) : ReadOffset32(bus, request) : 0;
        byte error = 0;
        if (length > unit.Staging.Length || (length & 511) != 0 || (offset & 511) != 0 ||
            offset > (ulong)unit.Drive.SectorCount * 512 || length > (ulong)unit.Drive.SectorCount * 512 - offset) error = IoErrBadLength;
        else if (length != 0 && !bus.IsMappedMemoryRange(destination, (int)length)) error = IoErrBadAddress;
        else if (unit.Outstanding == 64) error = 0xFA; // IOERR_UNITBUSY
        if (error != 0)
        {
            CompleteIo(bus, request, error, 0);
            if ((bus.ReadByte(request + IoFlagsOffset) & 1) == 0)
            { var stack = state.A[7] - 4; bus.WriteLong(stack, _diagCopyBase + ReplyThunkOffset); state.A[7] = stack; }
            return true;
        }
        _hardfiles[number].IoHook?.ObserveCommand(command);
        if (read) _storageObserver?.ReadCommand(false, offset >> 9, length, bus.Cycle);
        // DoIO may reuse a request whose node is still NT_REPLYMSG. A deferred
        // device owns it as NT_MESSAGE until the real guest ReplyMsg runs.
        bus.WriteByte(request + 8, 5);
        bus.WriteByte(request + IoFlagsOffset, (byte)(bus.ReadByte(request + IoFlagsOffset) & ~1));
        CompleteIo(bus, request, 0, 0);
        unit.Queue[(unit.Head + unit.Count) % 64] = new(request, destination, offset >> 9, (int)length);
        unit.Count++; unit.Outstanding++;
        StartTimed(unit, bus.Cycle); bus.StorageChanged(); return true;
    }

    private static void StartTimed(TimedUnit unit, long cycle)
    {
        if (unit.Running || unit.Count == 0) return;
        unit.Active = unit.Queue[unit.Head]; unit.Head = (unit.Head + 1) % 64; unit.Count--;
        unit.Running = true; unit.WaitingSector = false; unit.Completed = unit.Copied = 0;
        unit.Phase = 1; unit.PhaseCycle = checked(cycle + Math.Max(1, unit.Drive.CommandCycles));
    }

    internal void StepTimed(long cycle)
    {
        if (_bootstrapBus is not { } bus) return;
        foreach (var unit in _timedUnits)
        {
            unit.Drive.Step(cycle);
            if (unit.PhaseCycle == cycle)
            {
                unit.PhaseCycle = long.MaxValue;
                if (unit.Phase == 1)
                {
                    if (unit.Active.Bytes == 0) { CompleteTimed(unit, 0, cycle); continue; }
                    unit.WaitingSector = true; unit.Drive.Request(unit.Active.Sector, cycle);
                }
                else
                {
                    unit.Completed = unit.Copied;
                    if (unit.Completed == unit.Active.Bytes) { CompleteTimed(unit, 0, cycle); continue; }
                    unit.WaitingSector = true;
                }
            }
            if (!unit.Running || !unit.WaitingSector || !unit.Drive.Ready) continue;
            if (unit.Drive.Failed) { CompleteTimed(unit, 20, cycle); continue; }
            unit.Drive.CopySector(unit.Staging.AsSpan(unit.Copied, 512)); unit.Copied += 512;
            unit.WaitingSector = false; unit.Phase = 2;
            unit.PhaseCycle = checked(cycle + Math.Max(1, unit.Drive.InterfaceCycles(512)));
            // Prepare the following media sector while this one traverses the
            // abstract interface. The medium and link are pipelined, not summed.
            if (unit.Copied < unit.Active.Bytes) unit.Drive.Request(unit.Active.Sector + (uint)(unit.Copied >> 9), cycle);
        }
    }

    private void CompleteTimed(TimedUnit unit, byte error, long cycle)
    {
        var bus = _bootstrapBus!; var request = unit.Active;
        if (unit.Completed != 0) bus.CopyToMemory(request.Destination, unit.Staging.AsSpan(0, unit.Completed));
        CompleteIo(bus, request.Address, error, (uint)unit.Completed);
        _readyRequests[(_readyHead + _readyCount) % _readyRequests.Length] = new(request.Address, unit); _readyCount++;
        _storageObserver?.Completed(false, cycle);
        unit.Running = unit.WaitingSector = false; unit.PhaseCycle = long.MaxValue;
        StartTimed(unit, cycle);
    }

    private bool AbortTimed(LightweightHdfBus bus, uint request)
    {
        for (var i = 0; i < _readyCount; i++)
            if (_readyRequests[(_readyHead + i) % _readyRequests.Length].Address == request) return true;
        foreach (var unit in _timedUnits)
        {
            if (unit.Running && unit.Active.Address == request)
            {
                unit.Drive.CancelForeground(bus.Cycle); CompleteTimed(unit, 0xFE, bus.Cycle); bus.StorageChanged(); return true;
            }
            for (var i = 0; i < unit.Count; i++)
            {
                if (unit.Queue[(unit.Head + i) % 64].Address != request) continue;
                for (var j = i; j < unit.Count - 1; j++) unit.Queue[(unit.Head + j) % 64] = unit.Queue[(unit.Head + j + 1) % 64];
                unit.Count--; CompleteIo(bus, request, 0xFE, 0);
                _readyRequests[(_readyHead + _readyCount) % _readyRequests.Length] = new(request, unit); _readyCount++;
                bus.StorageChanged(); return true;
            }
        }
        return false;
    }

    private uint PeekCompletion() => _readyCount == 0 ? 0 : _readyRequests[_readyHead].Address;
    private void PopCompletion()
    {
        if (_readyCount == 0 || _completionLatch != PeekCompletion()) return;
        var entry = _readyRequests[_readyHead]; _readyRequests[_readyHead] = default;
        entry.Unit.Outstanding--; _readyHead = (_readyHead + 1) % _readyRequests.Length; _readyCount--; _completionLatch = 0;
    }
    internal bool TryReadCompletionWord(uint address, out ushort value)
    {
        value = 0;
        if (_timedUnits.Length == 0 || !IsConfigured || address < ConfiguredBase || address - ConfiguredBase is not (0x6000 or 0x6002)) return false;
        if (address - ConfiguredBase == 0x6000) { _completionLatch = PeekCompletion(); value = (ushort)(_completionLatch >> 16); }
        else { value = (ushort)_completionLatch; PopCompletion(); }
        return true;
    }
    internal bool TryReadCompletionByte(uint address, out byte value)
    {
        value = 0;
        if (_timedUnits.Length == 0 || !IsConfigured || address < ConfiguredBase || address - ConfiguredBase is < 0x6000 or > 0x6003) return false;
        var index = (int)(address - ConfiguredBase - 0x6000);
        if (index == 0) _completionLatch = PeekCompletion();
        value = (byte)(_completionLatch >> ((3 - index) * 8)); if (index == 3) PopCompletion(); return true;
    }

    private void InstallTimedCompletion(M68kCpuState state)
    {
        if (_timedUnits.Length == 0 || _timedInterruptInstalled || _deviceBase == 0 || _bootstrapBus is not { } bus) return;
        var image = LightweightStorageCompletionCode.Image;
        var allocator = new BootMetadataAllocator(bus, 0, _execBase);
        var region = allocator.Allocate(image.Length + 40);
        if (region == 0) throw new InvalidOperationException("No public RAM for timed CopperHDF completion server.");
        var node = region; var context = region + 24; var code = region + 40;
        bus.CopyToMemory(code, image);
        foreach (var offset in LightweightStorageCompletionCode.Relocations)
            bus.WriteLong(code + (uint)offset, code + bus.ReadLong(code + (uint)offset));
        bus.WriteByte(node + 8, 2); bus.WriteLong(node + 10, _diagCopyBase + NameOffset);
        bus.WriteLong(node + 14, context); bus.WriteLong(node + 18, code + LightweightStorageCompletionCode.InterruptOffset);
        bus.WriteLong(context, ConfiguredBase); bus.WriteLong(context + 4, node); bus.WriteLong(context + 8, _deviceBase);
        state.A[1] = context; state.A[6] = _execBase;
        var stack = state.A[7] - 4; bus.WriteLong(stack, code + LightweightStorageCompletionCode.InitializeOffset); state.A[7] = stack;
        _timedInterruptInstalled = true;
    }
}
