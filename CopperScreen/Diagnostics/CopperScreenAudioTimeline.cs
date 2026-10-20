#if COPPERSCREEN_INSTALLED_TESTS
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CopperScreen;

// Two single-writer rings. No allocation, locks, serialization or stack sampling
// on either execution path. Freeze on the owner and join the native callback
// before export; the runtime stays alive for ordinary closing save commands.
internal sealed class CopperScreenAudioTimeline
{
    internal readonly record struct OwnerEvent(int Kind, long Start, long Reserved, long GuestDone,
        long Submitted, long End, long CpuStart100ns, long CpuEnd100ns, long Field,
        int QueueBefore, int QueueAfter, long MissingBefore, long MissingAfter, int Detail,
        int Gc0, int Gc1, int Gc2, double PublicationCoreMilliseconds);
    internal readonly record struct CallbackEvent(long Start, long End, int QueueBefore, int QueueAfter,
        int RequestedValues, long MissingValues, long CumulativeMissingValues, bool ExpectAudio);
    internal readonly AudioTimelineRing<OwnerEvent> Owner = new(4096, 16384);
    internal readonly AudioTimelineRing<CallbackEvent> Callbacks = new(4096, 16384);
    private readonly long _anchorQpc = Stopwatch.GetTimestamp();
    private readonly DateTime _anchorUtc = DateTime.UtcNow;
    private bool _frozen;
    private long _firstMissingQpc, _lastMissingValues;
    internal void Freeze() => Volatile.Write(ref _frozen, true);
    internal void RecordOwner(OwnerEvent value)
    {
        if (Volatile.Read(ref _frozen)) return;
        if (Volatile.Read(ref _firstMissingQpc) != 0) Owner.RetainIncidentWindow(4096);
        Owner.Add(value);
    }
    internal void RecordCallback(CallbackEvent value)
    {
        Volatile.Write(ref _lastMissingValues, value.CumulativeMissingValues);
        if (Volatile.Read(ref _frozen)) return;
        if (value.MissingValues > 0 && Volatile.Read(ref _firstMissingQpc) == 0)
            Volatile.Write(ref _firstMissingQpc, value.Start);
        if (Volatile.Read(ref _firstMissingQpc) != 0) Callbacks.RetainIncidentWindow(1024);
        Callbacks.Add(value);
    }

    internal object Export() => new { protocol = "copperscreen-audio-owner-timeline-v1",
        qpcFrequency = Stopwatch.Frequency, anchorQpc = _anchorQpc, anchorUtc = _anchorUtc,
        firstIncidentQpc = Volatile.Read(ref _firstMissingQpc),
        finalCumulativeMissingValues = Volatile.Read(ref _lastMissingValues),
        cpuTimeUnit = "100ns cumulative current-thread kernel+user; -1 means unavailable",
        ownerKinds = new { field = 1, commands = 2, demandWait = 3, recovery = 4 },
        owner = Owner.Export(), callbacks = Callbacks.Export() };

    internal static long CpuTime() => OperatingSystem.IsWindows() &&
        GetThreadTimes(new nint(-2), out _, out _, out var kernel, out var user) ? kernel + user : -1;

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetThreadTimes(nint thread, out long created, out long exited,
        out long kernel, out long user);
}

internal sealed class AudioTimelineRing<T>(int prefixCapacity, int tailCapacity) where T : struct
{
    private struct Slot { internal long Sequence; internal T Value; }
    private readonly Slot[] _prefix = new Slot[prefixCapacity > 0 ? prefixCapacity : throw new ArgumentOutOfRangeException(nameof(prefixCapacity))];
    private readonly Slot[] _tail = new Slot[tailCapacity > 0 ? tailCapacity : throw new ArgumentOutOfRangeException(nameof(tailCapacity))];
    private long _written;
    private long _stopAt = long.MaxValue, _notRecordedAfterIncident;

    // Called only by this ring's writer. Retain its pre-incident tail plus a
    // bounded following window, without copying or allocating on an underrun.
    internal void RetainIncidentWindow(int followingRecords)
    {
        if (followingRecords <= 0) throw new ArgumentOutOfRangeException(nameof(followingRecords));
        if (_stopAt == long.MaxValue) _stopAt = _written + followingRecords;
    }

    internal void Add(T value)
    {
        if (_written >= _stopAt)
        {
            Volatile.Write(ref _notRecordedAfterIncident, _notRecordedAfterIncident + 1);
            return;
        }
        var sequence = _written + 1;
        ref var slot = ref (sequence <= _prefix.Length ? ref _prefix[(int)sequence - 1]
            : ref _tail[(int)((sequence - _prefix.Length - 1) % _tail.Length)]);
        Volatile.Write(ref slot.Sequence, -sequence);
        slot.Value = value;
        Volatile.Write(ref slot.Sequence, sequence);
        Volatile.Write(ref _written, sequence);
    }

    internal object Export()
    {
        var total = Volatile.Read(ref _written);
        var retained = new List<object>();
        var incomplete = 0;
        void Read(long sequence, ref Slot slot)
        {
            var before = Volatile.Read(ref slot.Sequence);
            var value = slot.Value;
            Thread.MemoryBarrier();
            if (before != sequence || Volatile.Read(ref slot.Sequence) != sequence) { incomplete++; return; }
            retained.Add(new { sequence, value });
        }
        for (long i = 1; i <= Math.Min(total, _prefix.Length); i++) Read(i, ref _prefix[(int)i - 1]);
        for (var i = Math.Max(_prefix.Length + 1L, total - _tail.Length + 1); i <= total; i++)
            Read(i, ref _tail[(int)((i - _prefix.Length - 1) % _tail.Length)]);
        return new { total, overwritten = Math.Max(0, total - _prefix.Length - _tail.Length), incomplete,
            observedTotal = total + Volatile.Read(ref _notRecordedAfterIncident),
            notRecordedAfterIncident = Volatile.Read(ref _notRecordedAfterIncident),
            prefixCapacity = _prefix.Length, tailCapacity = _tail.Length, records = retained };
    }
}
#endif
