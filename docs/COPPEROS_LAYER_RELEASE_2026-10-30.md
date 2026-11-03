# CopperOS Layers release qualification - 2026-10-30/31

Checkpoint `diskboot-2026-10-30-a1200-desktop-single-bank.42` follows
[RectFill overhead](COPPEROS_RECTFILL_2026-10-30.md). Work began October 30;
qualification completed October 31. The HDF and frozen reset/compiler inputs
are unchanged. This development image isolates graphics/Layers changes from
concurrent producer system-ROM migration.

The native Layers owner reuses the admission issued by immediate acquisition
only while that draw retains the same Layer grant and permanent library pin.
Release checks object identity/liveness and shares the existing queue, task,
owner, nesting, late-waiter and semaphore-publication transitions. Guest backfill
callbacks discard the saved admission and force full validation on return.
Queued/resumed acquisitions also retain full admission. No Forbid/Disable scope
is added across drawing and no hardware wait or scheduling boundary is removed.

The private drawing bridge saves fewer registers because the outer graphics
entry already protects the public caller. Fixed descriptors retain complete
admission with a shorter extent calculation; expanded compatible records retain
their maximum/end checks. Ordinary and unknown-owner paths still validate owned
records, and unknown operations retain published-owner dispatch before task
checks. The changes introduce no memory-type query or host shortcut.

| MC68000, no outer lock | Kickstart | `.41` | `.42` | Reduction |
| --- | ---: | ---: | ---: | ---: |
| RectFill 16x8 | 3,570 | 7,824 | 7,142 | 8.7% |
| RectFill 184x32 | 7,278 | 10,672 | 10,100 | 5.4% |
| Text 8 | 10,136 | 14,176 | 13,492 | 4.8% |
| Text 64 | 32,258 | 36,626 | 35,944 | 1.9% |

The small-call instruction profile reduces time inside the owner kernel from
2,846 to 2,038 cycles (**28.4%**). The net return reduction is 682 cycles: 808
saved inside the kernel, offset by 126 additional caller cycles for saving and
selecting the issued admission. Small RectFill still costs approximately twice
Kickstart; parity is not claimed.

The installed-owned-layer protocol remains PAL 8375/OCS, 2 MiB Chip, display DMA
off, IPL 7, 3 warmups, 21 samples, completed median guest cycles. All 16 rows
across both CPUs and outer-lock nesting match baseline state/pixels; MC68000
also matches original v40.63. Original-020 timing is not claimed. These are
guest-cycle diagnostics, separate from host-throughput acceptance.

| Bundle identity | Value |
| --- | --- |
| ROM file | 524,288 bytes, one standard 512 KiB bank |
| Used code/data, vectors, command bridge and footer | 520,462 bytes |
| Padding | 3,826 bytes |
| ROM SHA-256 | `501D63931DFF0735B24B71FBD10F7DD2521146310C1B672383A7B967AE3DF76F` |
| Unchanged HDF SHA-256 | `DC597FA79C5479D43E98ABDEDFCB885056F9C22AFFA161126FF1B27C39C3CFA7` |

Graphics totals 25,864 bytes, including its 928-byte initializer in the checked
preceding gap. Footer headroom is 644 bytes and checksum is `$FFFFFFFF`.
The bundle and build-time hash pin select this exact image.

The paired A1200 runs agree on CPU, full Chip RAM, pixels, audio, cycles, frames,
display registers and milestones. Pixels remain
`9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.

| Guest measurement | `.41` | `.42` |
| --- | ---: | ---: |
| Cold ready | 14.563181882 s | 14.543149995 s |
| DrawChanges, Tab at field 1,000 | 28.329849 ms | 27.240164 ms |
| Key-down to DrawChanges return | 73.251675 ms | 72.161990 ms |

The eight-field scenario is unchanged. Graphics selection: **200 tests pass**, with **2 optional crossover cases
unavailable**. Four known extended-image identity failures remain explicitly
excluded and their expectations are unchanged. All 34 flat-call rows and the
168-row text control pass. Controls cover 210 full-admission transitions and
78 additional held-release transitions per CPU, late queue arrivals and real
waiter handoff, foreign/null tasks, high-bit nesting, Forbid depths 0/125/126,
identity/liveness invalidation, callback-triggered full admission and unknown
operation delegation without an Exec/task context. The 139 emitted descriptor
envelope probes per CPU pass. Public graphics register assertions remain active.

The app passes 11 native graphics/text/desktop checks and 69 launcher/migration
checks; 3 optional workspace checks remain unavailable. An additional 38 host
Layers lifecycle/locking/library controls pass separately from ROM/media runs.
Both Release solutions build; the qualified incremental producer build reports
four dependency-resolution warnings, the consumer none.

The producer retains the final image, size proof, maps, inputs, emitter/source
snapshots and measurements in
`CopperStart/artifacts/graphics-layer-release-20261030/rom-qualified`.
Consumer paired captures are
`artifacts/graphics-layer-release-20261030/qualified-{profile,control}`.
Earlier trial images are retained separately and are not bundled. ROM/media,
local artifacts and credentials are not committed or published.
