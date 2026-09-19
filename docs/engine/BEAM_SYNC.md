# PAL OCS beam synchronization — 2026-09-19

This is the first beam/synchronization slice: **ERSY with no external HSYNC
source**. VHPOSW/VPOSW counter repositioning, external pulse injection/qualification
and a genlocked display are still open. Performance acceptance is separate and
pending: the completed lores portion exceeds 1% (upper bound +2.47%), and the
series became host-invalid during hires. The earlier optimization waiver does
not apply to this change. See [the full measurement record](PERFORMANCE.md#ersy-absent-source-beam-synchronization--2026-09-19).

## Evidence and implementation boundary

Commodore's [BPLCON0 register description](https://amigadev.elowar.com/read/ADCD_2.1/Hardware_Manual_guide/node0022.html)
defines ERSY as switching the sync pads to external inputs. The independent
[vAmigaTS VPOS probes](https://github.com/dirkwhoffmann/vAmigaTS/tree/0489f55d22ef7560d924a304e30998aea6864264/Agnus/Registers/VPOS)
include source, bootable media and A500 OCS photographs. ERSY1 sets and clears ERSY
within a line. ERSY2 spans the line boundary: the
[OCS5 photograph](https://github.com/dirkwhoffmann/vAmigaTS/blob/0489f55d22ef7560d924a304e30998aea6864264/Agnus/Registers/VPOS/ersy2_A500_OCS5.JPG)
shows repeated `$4000` reads while the CPU continues executing. Horizontal
position is held at zero and vertical position stays `$40`.

At the normal horizontal terminal count, absent HSYNC now holds the beam at H0
without advancing V, emitting a CIA-B TOD pulse, ending a field, or generating
VERTB. Clearing ERSY resumes the same vertical count. Canonical CPU time continues;
CPU register/memory accesses, CIA timers, keyboard/UART, disk rotation/recovery,
disk serialization and Paula sample periods continue. H0 is not repeatedly
reserved for refresh, so the CPU can clear ERSY.

Agnus-controlled address inputs/control progress pause. Already accepted output
phases remain scheduled and finish. Disk/audio/sprite requests are rebuilt from
the resumed beam phase, not from absolute-cycle modulo 454. Cached integer phase
offsets change only on sync recovery/reset. There is no second clock, event queue,
per-bit callback, decoder change, allocation, or title-specific path.

This is a bounded digital model. The probes establish stationary counter readback,
CPU progress and the within-line distinction. They do **not** certify every DMA
state during a hold, the exact BPLCON0 pad-switch latency, oscillator/pulse
qualification, or Denise/monitor behavior during sync loss. Broader hardware
measurements remain necessary. The A4000/A3000 developer notes describe newer
Agnus genlock details; those have not been silently adopted as OCS proof.

## Host output and reset

`BeamSyncRunning` reports whether the beam is advancing. `ExecuteFrame` and
`CompletedFrames` describe output delivery: a missing hardware VSYNC cannot make
an invocation wait indefinitely. After at most 329 PAL line durations (149,366
CPU cycles), the engine publishes PCM and retains the last complete framebuffer.
That interval fits the existing 1,024-stereo-sample buffer and allows brief holds
to extend one field without unnecessary output splitting.

The fallback generates no TOD pulse, VERTB, LOF toggle, Copper restart, sprite
reload or bitplane field reset. Actual VSYNC retains those hardware side effects.
A field that lost synchronization is discarded for presentation; the next wholly
synchronized field replaces the image. This does not simulate the distorted CRT
image in the photograph. Consumers must accept variable, potentially short/empty
PCM intervals on recovery. The desktop advertises capacity for 1,024 stereo
samples, copies only the returned count, and does not pad or resample it. The
runner handles short/empty PCM when computing diagnostic output fingerprints.

CPU RESET releases ERSY while retaining canonical time and the resumed DMA phase.
Full machine reset restores the original zero-phase clock. Neither operation
changes media contents or resets disk position merely to compensate for sync.

## Verification

Reference: accepted commit `67c13f63081d3ea6a956c86f88cda914d0db33a1`.
Research and raw captures are local, ignored files under
`artifacts/beam-sync-2026-09-19/`; no ROM or probe media is committed.

| Probe ADF | SHA-256 |
| --- | --- |
| ERSY1 | `8F66811A0C61C0B9418A87C9BD08E49844B576899F43B092C96378E228B7D601` |
| ERSY2 | `62A26625598A0F7F89B3D7BC5F632408D6FFA3C3A03858341EF6E1D4BD6151F1` |

Both probes ran for 900 output invocations with supplied Kickstart 1.3, no input
script, and ordinary CPU execution. RAM snapshots identify the actual `values`
table immediately before the probe Copper list, **not** the embedded expected
table. ERSY2 values are at `$0704E8`; ERSY1 at `$0705C4`.

- ERSY2 reference: `40BA 40C3 40CE 40D7 40E1 4109 4112 411C 4126 4130 413A 4145 414E 4159 4162 416D`.
- ERSY2 candidate: `40BA 40C3 40CE 40D7 40E1 4000 4000 4000 4000 4000 4000 4000 4000 4000 4000 4000`.
- Hardware/source expectation: `40BF 40C9 40D3 40DD`, then twelve `$4000` values.
- ERSY1 reference and candidate are identical:
  `E066 E26E E46E E676 E86A EA6E EC6E EE76 F06A F26E F470 F675 F86B FA70 FC75 FE75`.

The held-counter behavior is repaired. The initial CPU/interrupt/register phases
still differ from hardware in both builds; this change does not certify the
entire probe or alter those expectations. ERSY1's displayed red results remain
an existing physical-phase discrepancy, not a newly claimed pass.

Focused engine cases cover hold/release, within-line toggles, active and
stopped CPUs, bounded output, TOD versus timers, all four audio DMA slots,
accepted audio transfer completion, disk/sprite phases, blitter suspension,
reset, odd CPU phases, fine/coarse advancement and period-modulation request
preservation. The optimized candidate passes all 577 isolated Release engine
tests, including the 60-output sustained-sync-loss zero-allocation check, an odd
CPU-phase bitplane deadline regression, and two terminal Copper restart cases.
All 577 also pass in Debug on the final fetch-order candidate.
CopperDisk passes 74. Production Release builds without
warnings or errors. All 94 host tests pass, including the new missing-sync/audio
capacity regression and native gameplay/keyboard replays (no skips). Initial
host failures exposed the old 962-sample capacity and then the historical native
fingerprint; their raw TRX files are preserved beside the passing results.
Source review then found and repaired an early audio request on sync recovery:
resume now restores a pending request only, preserving the period-modulation
low-byte request phase. Its initial frozen benchmark series was withdrawn and
left intact rather than used to accept the corrected candidate.
Performance remains separate in [PERFORMANCE.md](PERFORMANCE.md).

## Native fingerprint correction

Kickstart itself briefly sets ERSY to detect genlock. In the accepted reference,
the ignored bit leaves `BPLCON0=$1302` through boot; with absent-source behavior
Kickstart clears it and reaches `$1200`. This is why native cold-boot fingerprints
change even for a game that later leaves ERSY clear. The unchanged Lemmings input
script still reaches level one and assigns a digger; frame 14,520 shows ten
lemmings out and a dug passage. The bounded capture does not establish complete
game compatibility or physical correctness at unrelated boundaries.

| Unchanged workload: 10,920 warmup + 3,600 retained outputs | Accepted reference | ERSY candidate |
| --- | --- | --- |
| Final cycle | `2063321634` | `2063189682` |
| CPU | `6AE7090DA8AFB7E3` | `BCF441F9BABF1113` |
| Hardware | `334A819F6FDFB1CA` | `206DA179786FCB2A` |
| Output | `C65F87325946E5DA` | `F7328C7C24582FCB` |
| Final interleaved PCM samples | 1,922 | 1,924 |
| Measured steady-state allocation | 0 | 0 |

The final request-preservation build (`2E87659F28E3A4BE25DA0D7C348B947A4FF15B08634C91329102E3FE7D95F4DB`)
repeats all candidate values above in `native-final-workload-check.log`, with
zero steady-state allocation and no unsupported feature. That standalone run
checks compatibility/identity; it supplies no accepted throughput bound.

The host regression now records the corrected candidate expectations; the older
values above and all historical performance records remain intact. The frozen
performance harness still requires the old native fingerprint and must reject
this candidate for that workload. A differently fingerprinted run is not an
unchanged-work acceptance comparison. No prior waiver or diagnostic FPS total
establishes the requested one-sided 95% regression bound of at most 1%.

## Optimization follow-up

The owner requested optimization rather than accepting the initial performance
excess. The first complete optimized comparison used
`BB3F5233EA3FB132E89C018BB76B1CB4260144EFC61CE5A558D67CF7B3A0D7CA`, in
`artifacts/beam-opt-2026-09-19/candidate-terminal`. All 94 host tests pass with
native media enabled; the corrected gameplay and keyboard fingerprints are
unchanged by these optimizations.

The retained changes remove repeated work within the existing owner-thread phases:

- Active bitplane runs return before inactive-window checks. Plane count and
  DMA/sync enable state are maintained by existing register/reset transitions.
- Copper checks DMA/sync once after completing accepted output. Normal input
  phases cannot target the even refresh slots; an assertion retains that proof
  while general slot queries still perform the refresh check.
- Palette writes only advance the video deadline; they need not recompute every
  unrelated device deadline. Copper execution still finishes the ordinary full
  deadline refresh after its slot.
- The existing terminal Copper state stops scheduling empty polls. Frame restart
  and COPJMP explicitly reactivate it, and accepted output/bus state is preserved.
- The subsequent candidate prepares the eight-slot bitplane fetch order at the
  effective BPLCON0 transition. Three bits store each plane-plus-one value, with
  zero representing an idle slot. Per-CCK extraction replaces mode selection and
  table loading; output acceptance, ordering and terminal modulo phases remain
  unchanged.

Debug assertions also exposed an odd owner-cycle BPLCON0 write whose old pending
deadline could never equal an even CCK dispatch. Aligning that deadline to the
next eligible slot fixes the direct-input edge; normal even hardware strobes are
unchanged. The new regression distinguishes the value before and at cycle four.

Clock restructuring, sprite packing, alternate video paths, cached scroll
decoding and unchecked DMA word access were explored and reverted after
unhelpful, inconsistent or opposing workload results.
Their local logs remain evidence, not accepted gains. No scheduler, floating-point
timeline, sync look-ahead or title-specific behavior was introduced.

The retained fetch-order candidate is
`7778A0F26FCC2D2EE1F66C69172F48D7DFB6E61D2CD6623693A47FBDFC7D4C85`,
in `artifacts/beam-opt-2026-09-19/candidate-fetch-order`. Both short balanced
synthetic trials favor it over the terminal-only candidate with matching state
and zero allocation; those two-pair trials do not establish acceptance.
The initial fetch-order engine suite had one intermittent serial allocation
assertion failure (8,088 bytes); its isolated repeat and subsequent complete
Release and Debug suites pass. All raw TRX files are preserved; the initial
failure is not relabeled as a pass.

The separately versioned
[ERSY comparison v1](../../scripts/run-lightweight-ersy-comparison-v1.ps1) retains
the six-pair lengths/order, Normal priority, verified homogeneous CPU/SMT placement,
host-load policy v2 and one-sided 95% calculation. Synthetic complete-workload
fingerprints must match across builds. Native runs must match the independently
verified per-build identities in the table above, including cycles, output size
and zero allocation. The harness pins the accepted reference, runner and shared
dependencies and rejects swapped or corrupted identities before timing. This is
an explicit **changed-state native comparison**, not unchanged-state retention;
historical protocols and results remain untouched. See
[PERFORMANCE.md](PERFORMANCE.md) for the measured disposition.

The final six-pair series is valid. Lores and the defined native comparison meet
the ≤1% one-sided upper-bound requirement; hires has a +0.3736% mean frame-time
change and +3.7272% upper bound. On 2026-09-19 the owner explicitly accepted that
specific hires exception for the final frozen candidate and authorized commit
and push. The measured hires result remains INCONCLUSIVE; correctness passes and
the native speedup do not override or relabel the gate. The default ≤1% limit
continues to apply to future changes.
