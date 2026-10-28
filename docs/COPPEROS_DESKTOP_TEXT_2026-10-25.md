# CopperOS text blitter and native redraw — 2026-10-25

The text.4 development bundle uses real guest blitter DMA for the desktop's
fixed eight-pixel text and removes repeated Exec.TypeOfMem calls from native
Layers memory-envelope checks. Private owner, generation, seal and lifetime
checks remain. No allocator-list scan can establish the accessibility or
lifetime of caller storage; it also incorrectly excludes readable ROM data.

The unchanged PAL Alice/Lisa, 68EC020 interpreter, 2 MiB Chip RAM profile gives:

| Native workload | blitter.3 | text.4 |
| --- | ---: | ---: |
| Cold desktop readiness, guest seconds | 20.773068 | 15.865255 |
| Tab selection repaint, guest seconds | 1.101756 | 0.420673 |

Cold readiness takes **23.6% less guest time**; redraw takes **61.8% less**.
Compared with the first 54.867-second desktop, startup now takes 71.1% less
guest time. These are modeled guest-work measurements, not physical A1200
measurements or host throughput acceptance. CPU clock, Copper68k package,
chipset ordering, DMA contention and display geometry are unchanged.

## Text rendering

`NativeGraphicsRasterBodies.CompactTextDma.cs` stages a complete monochrome
string once, then programs B/C/D DMA with constant A and first/last-word masks.
The staging read is bytewise CPU work, so fonts may reside in ROM or Fast RAM.
The template and selected destination planes must reside in Chip RAM and obey
the blitter's alignment, size and modulo requirements. No TypeOfMem query is
needed to select that hardware path.

The bounded path accepts up to 80 eight-pixel cells, font heights 1–32, no
kerning/spacing tables and zero extra character spacing. It handles all eight
draw modes, write masks, all sixteen destination alignments, clipping and
default-character slots. It preflights the complete locked ClipRect list and
all selected planes before drawing. Simple hidden regions are skipped; Smart
regions use their backing bitmaps. It waits for completion before relinquishing
the blitter or stack template, retains callee registers and publishes the final
logical cursor once. The bounded stack workspace is 2,992 bytes.

Existing CPU paths retain long strings, other font geometries, proportional
spacing, unsuitable memory and other ineligible shapes. Existing unsupported
style/admission behavior remains outside this fast path. The source hook is
after the existing text admission decision and before blitter acquisition.
No host drawing shim replaces the custom-chip operations.

## Layers and paired launcher

`CopperSharpRomLayersPlatform.IsMapped` now performs only the non-null,
non-wrapping address-envelope test. Native callers supply accessible storage;
host adapters retain their separate host-memory containment. The source's
permanent actor-query span helper follows the same policy. Neither change
removes private ownership or synchronization checks. Native envelope regression
cases explicitly admit ROM and caller-owned addresses without installing Exec.

The GUI's text entry moves from 247686 to 247582. Home uses that entry and
`CopperOS.GUI.Text.v2`; the small C:Desktop bootstrap preloads the same resident
key. Matching both users matters: an intermediate candidate used two different
keys and loaded the GUI twice. Its slower 21.614-second boot is retained under
`final-profile`/`final-control`; it is superseded, not acceptance evidence.

## Frozen producer and identities

The unchanged compiler/SDK and original provider assembly first reproduce
the old 257,312-byte GUI HUNK byte-for-byte (`59007352…`). To isolate this change
from unrelated live producer edits, the qualified frozen Layers assembly uses
a source-equivalent IL overlay of **only IsMapped**. The comparison covers all
1,327 method bodies and reports that single changed body; metadata and other
methods retain their original bytes. The new production source and the frozen
overlay each pass the same address-envelope cases. This is a development
qualification, not a release of the producer package.

The resulting GUI HUNK is 257,200 bytes, padded with 112 trailing zero bytes to
retain its previous filesystem allocation envelope. The paired Home is 41,456
bytes, padded to 41,500. Normal native LoadSeg and cold-reopen tests exercise
these padded files. All three updated system payloads keep their old lengths.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `2f948f05df3f2a530f43b773f5b473430ae918d74cf0176ab164f6375040b05b` |
| 8 MiB DOS1 HDF | `0eca0b4e67b3a1c0c16528d2543f01d436a5e10cf39cd1e542236a83225fed15` |
| L:CopperGUI | `f74e041915c2824e5d21dc2e76ecb7764833245a429bba08a238a3b6360ebe3c` |
| L:CopperHome | `e34c30c6fdc979cbc42ecd4f726421df7709ce97ef97855515d2b4213f1ac7d8` |
| C:Desktop | `95b1e91a5d0de56d3b5363a214b04776da5bdb84cdf56051a821fba53898e754` |

The graphics resident grows from 29,587 to 31,133 bytes. The reset bank is
byte-identical to blitter.3. The contract-checked FFS image recipe reproduces the
new HDF exactly and its independent reader checks all file, block and bitmap
ownership. ROMs, media and local artifacts remain outside Git.

## Existing personal disks

The existing atomic system-file updater recognizes both the original desktop
and blitter.3 seed markers. It now updates the paired GUI, Home and bootstrap
payloads together. Every predecessor component must match its exact recognized
hash, or already match the replacement, before publishing any changes. Custom
components, write-protected disks and active writable disks retain their data.

Only same-length payload data changes; file tables, metadata, allocation bitmap
and all other disk bytes survive. File.Replace retains a full
`CopperOS.before-drawing-<id>.hdf` backup beside the personal image. Replays from
both predecessor HDFs require exact equality to the new seed, preservation of a
modified ReadMe and an unrelated byte, exact backup, idempotence, active-disk
deferral and atomic rejection of a customized GUI.

The actual personal workspace moved from `11f825cd…` to text.4; its full new
backup hashes to that exact predecessor. The normal Release app was launched
through the remembered A1200 option and displayed the native desktop.

## Validation and retained evidence

- The [external probe](../scripts/probes/CopperOsBoot/README.md) uses immutable
  named inputs. Qualified profile/control finish at **813 fields / 115,528,948
  cycles**, with identical CPU state, Chip RAM, framebuffer and PCM. The final
  selected-tile pixel hash remains `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
  It records 86 actual BLTSIZE launches, including 36 text-plane launches.
- Native DMA pixel oracles cover 68000/ECS and 68EC020/AGA, all text modes,
  masks, alignments, Simple/Smart clipping, ROM fonts, the 80/81-cell boundary,
  guard bytes, cursor and callee registers. All **10 DMA theories pass**.
  Together with the embedded boot/Editor/reset/writable-reopen replay,
  **11 tests pass, zero skips**. The normal session reaches the checked desktop
  by 804 fields; its regression bound is 850 fields.
- Host migration/launch checks pass **65 tests**. Three optional historical
  workspace/checkpoint replays are skipped and remain unavailable coverage.
- The frozen emitter's CPU fallback selection passes **261 tests, zero skips**.
  The existing stale five-case text-admission theory remains excluded; its two
  old rejection expectations also fail before this change. An initial filter
  typo included that theory and is retained as a failed run.
- Current-source Layers actor/envelope tests pass **67 cases**. The isolated
  source and frozen-overlay envelope selections each pass ten cases.
- `dotnet build CopperScreen.slnx -c Release` passes with zero warnings/errors.
  The producer-wide CopperStart build still has three existing Shell/DOS API
  errors (`UnloadDosProcessSegment`, `LoadSegmentNative`, `UnloadSegmentNative`).
  Changed Layers/graphics projects compile; the frozen native GUI and Home
  builds pass the allocation/runtime/external-helper compatibility gates.

Evidence is under `artifacts/copperos-text-speed`: emitter/candidate, GUI
baseline and single-method overlay, Home builds, final-desktop, reproduced-hdf,
qualified-profile/control, migration replays and personal-disk receipts. Root
test TRX files retain both initial assertion/filter failures and corrected runs.
The [previous blitter record](COPPEROS_DESKTOP_BLITTER_2026-10-25.md) remains
historical evidence with its original inputs and results.
