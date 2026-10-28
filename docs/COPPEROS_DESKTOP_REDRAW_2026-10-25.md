# CopperOS repeated drawing validation — 2026-10-25

The redraw.5 development bundle reduces the same two-tile Tab repaint from
**0.420673 to 0.340542 guest seconds**, a **19.0% reduction**. Cold desktop
readiness changes from 15.865255 to 15.664936 guest seconds. The combined ROM,
CPU package, clock, DMA behavior, hardware ordering and drawing output are
unchanged. This measures modeled guest work, not host throughput acceptance or
physical A1200 timing.

The [previous text.4 bundle](COPPEROS_DESKTOP_TEXT_2026-10-25.md) already uses
blitter text and fills and has no TypeOfMem query in this path. The extended
[external probe](../scripts/probes/CopperOsBoot/README.md) showed that remaining
cost came largely from repeated private validation and native call machinery.

| Redraw method-entry visits | text.4 | redraw.5 |
| --- | ---: | ---: |
| Native address-envelope check | 780 | 390 |
| Private root validation | 270 | 90 |
| Layer descriptor lookup/validation | 60 | 30 |
| Owner-state validation | 75 | 30 |
| Owner layout Align4 helper | 1,725 | 30 |
| Full raster register-frame preparation | 30 | 0 |
| Management-only frame preparation | 0 | 30 |

## Changes and retained boundaries

- `LayersGraphicsBridgeCore.DispatchInto` admits the root and owned Layer
  descriptor once. It passes the contained lock state directly to the existing
  lock state machine, instead of repeating descriptor/root admission through
  `LayersLayerCore.LockInto`, `Unlock` or `AttemptLock`. Every continuation
  resume enters admission again. Public/general lock entrypoints still validate
  their inputs independently.
- The lock implementation splits admission from its unchanged task, nesting,
  FIFO, grant-token and continuation rules. Internal reads of the root's Exec
  base occur after validation and before any callback or wait. The library/root
  binding check similarly avoids validating the root again just to read its
  Layers base.
- Unified is the sole Layers ABI. `OwnedSize` now folds its SDK-derived layout
  into a compile-time 4,676-byte value, retaining the previous result for every
  profile byte. It no longer reconstructs that fixed layout at each owner check.
  Prepare/validate/teardown still check the actual slab and derived addresses.
- Native library management vectors copy only the original D0/D1 results they
  consume, retaining the same frame envelope and return behavior. Close/expunge
  reuses its owner-validation result instead of calculating it twice.
- The private graphics entry runs under the library pin held by both graphics
  callers. Open already validates the owner; the entry no longer repeats that
  whole check for lock and unlock. Library/root binding and live Layer descriptor
  admission remain on each call. Close and teardown retain owner checks.

No shared mutable cache or public semaphore substitution is introduced. Private
lock authority, ownership, delayed expunge, FIFO fairness, register preservation,
and DMA completion remain part of the exercised paths. A new regression checks
that corrupt private root/descriptor ownership still declines without mutation.

## Remaining cost

The redraw's 160 largest exclusive costs include about 145 ms in Exec, 60 ms in
Layers, 43 ms in graphics and 29 ms in the desktop provider. These are exclusive
CPU costs, not inclusive call durations. About 10 ms is attributed to the
blitter-wait loop. Roughly 54 ms remains in other/unmapped symbols; it is not
silently assigned to drawing. Exec library dispatch, register frames and task
scheduling are now the largest named group. Removing additional private checks
alone cannot remove that call machinery.

## Qualified inputs and existing disks

The ROM remains `2f948f05df3f2a530f43b773f5b473430ae918d74cf0176ab164f6375040b05b`.
All executable payload lengths remain unchanged, enabling the existing atomic
three-file update with a full-disk backup.

| Artifact | SHA-256 |
| --- | --- |
| DOS1 HDF | `ea6cc800f51832d41dadaa35b881214080617e7ef9f01644b1d371e3f652e5dc` |
| L:CopperGUI | `473bed6b273352740081d9b9ecd5e2290d9189ab0c85a73bfcd94d77dd8b55c6` |
| L:CopperHome | `bef364f643e65d27f9c27c7232fe8a86b3a01f15d13efa7f36a43d4dfc5b3c58` |
| C:Desktop | `a522e79895ffb093ca7b7c1713ce0350a10fd9a240110d602042dd2ca8f3a02b` |

The frozen text.4 Layers assembly receives a source-method overlay of ten
changed methods and six new internal helpers. Resolved IL comparison covers all
1,337 previous bodies, finds only those intended replacements/additions, and
requires each overlay body to match its compiled source. Other provider,
compiler and SDK inputs remain frozen. This avoids importing unrelated live
producer work. The GUI HUNK is 257,252 bytes, padded to 257,312; Home remains
41,456 bytes, padded to 41,500. Its text entry is 247634. Both bootstrap and Home
use `CopperOS.GUI.Text.v3`, with a new Home resident key.

The update recognizes original, blitter.3 and text.4 desktop seeds. Replays
from all three require exact seed reconstruction, preservation of modified
documents and unrelated bytes, an exact backup, idempotence, active-disk deferral
and atomic rejection of a customized GUI. Read-only settings keep the previous
marker and disk. The actual personal text.4 image upgrades to redraw.5; the new
`CopperOS.before-drawing-<id>.hdf` backup hashes exactly to its predecessor.
The independent image reader verifies every file/block/bitmap owner and the
checked image recipe reproduces the HDF exactly. No ROM or media enters Git.

## Validation

- Protocol v4 adds entry counts and observed return-address edges without
  changing v3 input or completion rules. Its text.4 baseline reproduces v3
  cycles and final state exactly. Qualified profile and normal ExecuteFrame
  control match at **799 fields / 113,539,498 cycles**, including CPU registers,
  Chip RAM, pixels and PCM. The completed selected-tile pixel hash remains
  `9F7A6279A029C17930E8B04800263C696DA17BEA04FD3EDF4F633564D7B3C371`.
- The embedded bundle passes **11 native tests, zero skips**: DMA pixel oracles,
  Home/Editor input, reset and writable cold reopen. The normal session's new
  redraw regression finishes in **17 PAL fields** with a **19-field bound**.
- Current-source ownership/lock/lifecycle/actor checks pass **122 cases**.
  The same newer fixture selection against frozen baseline and candidate has
  identical outcomes: 103 pass and 19 fail. Those 19 older-frozen-input failures
  concern missing friend access and historical allocation-layout expectations;
  they remain unavailable coverage, not passing evidence. The candidate adds no
  failure to that baseline. Source tests and actual native replays are distinct.
- Host upgrade/launch checks pass **69 cases**. Three optional historical
  checkpoint/workspace replays remain skipped and unavailable.
- Production `dotnet build CopperScreen.slnx -c Release` passes with zero
  warnings/errors. Changed producer Layers and frozen GUI/Home native builds
  pass. The full CopperStart solution retains the three existing Shell/DOS API
  errors and four unrelated provider warnings; this is not reported as passing.

Local evidence is under `artifacts/copperos-redraw-speed`: source-before,
compiled source, frozen method overlay and resolved comparison, baseline and
qualified profiles/control, native GUI/Home inputs, qualified-desktop,
reproduced-hdf, migration replay and personal-disk receipts. Initial candidate
and frozen-fixture failures remain separate from passing records.
