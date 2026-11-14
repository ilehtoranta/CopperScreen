# Optional timed storage and native Gayle IDE

These C# devices are opt-in construction-time additions to the Lightweight engine.
`Hardfiles` and the synchronous zero-latency CopperHDF path retain their defaults.
The host backing file stays owned by CopperDisk; portable filesystem code acquires
no application dependency. No ROMs, media, binaries or packages are distributed.

```csharp
var disk = new AmigaHardfileConfiguration(0, path, readOnly: true)
    { ExposeBootNode = false };
var timed = new LightweightA500Configuration
{
    Hardfiles = [disk],
    HardfileTimings = new Dictionary<int, LightweightDriveTimingProfile>
        { [0] = LightweightDriveTimingProfile.Hdd }
};
// On an Alice/Lisa, EC020 A1200 configuration, native ROM scsi.device uses:
var ide = a1200Configuration with
{
    Hardfiles = [],
    IdeMaster = new LightweightIdeDriveConfiguration(disk)
        { Timing = LightweightDriveTimingProfile.Cf }
};
```

Timing profiles are immutable version-1 records. The HDD preset specifies 500 µs
command overhead, 4 heads × 64 sectors/track, 3,600 RPM, 2 ms + 80 µs/cylinder seek,
200 µs head switch, one-sector track skew, 64 KiB bounded drive read-ahead and a
2 MiB/s abstract CopperHDF link. CF specifies 50 µs commands, no mechanical/media
delay or read-ahead, and an 8 MiB/s abstract link. These are synthetic assumptions,
not calibrated measurements. The filesystem benchmark supports JSON overrides.

`LightweightStorageDrive` shares mechanics/cache behavior between timed CopperHDF
and ATA. It uses integer canonical machine timestamps and rational spindle edges.
One scheduled media sector and bounded background prefetch feed protected command
buffers. Prefetch pauses at capacity. Adjacent requests reuse position; hits avoid
media access. On a miss/stream redirect, old speculative entries become
replaceable so abandoned predictions cannot pin the cache; valid hits remain
available until replacement. Foreground and command/PIO buffers stay protected.
Returning to a cached sector outside the active stream also redirects prefetch.
Retained successors are skipped and protected while the resumed stream fills
free space. This corrects metadata detours leaving background reads on an
abandoned stream; a failing-before test checks the next fetched sector and
zero allocations on resumption. Timing-profile parameters are unchanged.
Host file duration never determines a device deadline. CF bandwidth
is charged by the abstract CopperHDF interface once; native IDE pays its PIO bus
transfers instead. Queues, media/PIO buffers and caches allocate at construction.

Timed CopperHDF accepts 64 outstanding requests per timed unit, including pending
guest replies. Accepted requests become `NT_MESSAGE`, clear `IO_QUICK`, return
normally and complete on the shared clock. Data, `io_Actual` and `io_Error` become
visible together, followed by a PORTS interrupt. A board mailbox feeds a C# guest
interrupt server which calls real Exec `ReplyMsg`. Both its registration and IRQ
entry are generated with CopperSharp68k; regenerate with
`dotnet run --project tools/GenerateStorageCompletion -c Release -- <output.g.cs>`.
The generator checks compatibility, zero managed allocation sites and relocation
containment. Do not edit emitted instruction bytes.
The chained interrupt server loads SysBase from address 4: Exec supplies A6 as
scratch to interrupt servers. It preserves the callee-saved registers and returns
zero so other PORTS servers can run.

Timed reads must be aligned 512-byte requests of at most 1 MiB. Unaligned/oversize
requests return `IOERR_BADLENGTH`; overflow returns `IOERR_UNITBUSY`. Abort handles
queued, active and completed requests without duplicate replies; machine reset
clears old queues and caches. Units without profiles retain synchronous behavior.
This initial timed backend and the ATA master require read-only media.

Storage joins the existing next-device-cycle minimum. The clock stops at odd as
well as even storage deadlines, including during CPU bus contention and STOP.
It introduces no independent clock, generic event queue, recursive CPU execution
or per-CCK storage poll. Existing chipset output phases remain in the CCK loop.

The read-only ATA master implements IDENTIFY, CHS/LBA28 reads, READ MULTIPLE and
setup, initialization/geometry, diagnostic/reset, seek/verify/recalibrate and the
limited PIO/read-ahead controls used by native Kickstart. Unsupported commands and
writes return ATA errors. Main status acknowledges drive IRQ; alternate status
and debugger peeks do not. Gayle has its own masked interrupt latch, separate from
the live drive IRQ input. No slave, DMA, ATAPI, LBA48 or writable ATA is fitted.

Native PIO travels through Gayle task-file/data registers and actual CPU bus
accesses. The current conservative minimum is five mother ticks per IDE access.
The [Commodore Gayle specification](https://www.amigawiki.org/lib/exe/fetch.php?media=de:parts:gayle_specification.pdf),
[T13 ATA/ATAPI draft](https://courses.cs.washington.edu/courses/cse451/16au/readings/ata.pdf)
and bounded 39.106 native-driver traces inform decode, protocol and byte order.
The Gayle document describes the earlier 68000 part: precise AA fast/slow-bank
timing, byte-width edge behavior and electrical interrupt timing remain
unqualified. This is native-driver compatibility with modeled bus timing, not a
physical-cycle-accurate A1200 claim.

The CopperFS [matrix protocol](../../../CopperFS/docs/kickstart-storage-matrix.md)
defines the 1,188-trial original/Copper comparison. Diagnostics count read commands,
transport bytes, physical backing fetches, completions and PIO bytes. Normal runs
do not enable register tracing. Host, instruction-replay and full-ROM coverage
are reported separately; skipped external-media tests are unavailable coverage.
