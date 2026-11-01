# Layers default-background backfill fix — 2026-11-01

**Fixed and verified in v50-backfill.** Original MUI Calc computes
`5 Enter 6 Enter + = 11` and closes cleanly on CopperStart and native Kickstart
3.1 in the separate 5 MiB diagnostic profile. CopperStart's closed-window
rectangle now contains zero stale bitmap pixels, compared with 25,121 before
the fix. Both OS window unlinking and MUI cleanup pass.

The [reviewed receipt](UTILITY_MUI_BACKFILL_FIX_2026-11-01.json) pins sources,
provider/loader, ROMs, media, engine/CPU, raw results and captures. This qualifies
the bounded mouse workflow and default bare-screen background restoration.
Menus, paging, keyboard, full MUI API and full Workbench desktop coverage remain
unqualified. The retained 3 MiB load failure remains FAIL.

## Change

Deletion and movement/size changes now calculate background exposed outside all
surviving layers when LayerInfo selects default backfill. The pixel plan owns
the resulting rectangle workspace and stages pen-zero fills in the existing
save-under/reveal transaction. Fills are clipped to bitmap storage and do not
clear surviving layers. Custom LayerInfo hooks retain their callback path, and
explicit no-backfill remains a no-op. Plan teardown releases the workspace on
both successful completion and aborted preparation.

This fixes Layers itself. No MUI screen-clearing workaround or added Workbench
backdrop is involved. The [investigation](UTILITY_MUI_STALE_PIXELS_2026-11-01.md)
retains the original failure and contract references.

## Initial candidate host and compiler validation

- The original artifact-only reproducer passes after the change (141 selected
  tests total in the first focused run).
- 570 selected Layers host checks pass with zero skipped. Two excluded tests
  also fail in the retained pre-fix build: bootstrap trampoline bindings and a
  typed-codec source architecture check. An initial repository-root lookup
  failure from cross-repository test outputs is resolved by placing those
  outputs within CopperStart. The original broad result remains retained as
  568 pass / 3 fail, not relabeled as a passing suite.
- Seven dedicated regressions pass, including default/no-backfill pixels,
  surviving-layer protection, movement, off-screen clipping, provider failure
  and retry, and 28 allocation-failure positions. Successful deletion and
  allocation-failure retries free every new allocation.
- `dotnet build CopperStart.slnx -c Release` succeeds with an isolated artifacts
  path: 13 warnings, zero errors.
- MC68000 `IntuitionPayloadIsFreestanding` passes. The provider has no managed
  allocations/runtime dependencies or unresolved native targets. Compiled code
  is 354,064 bytes; HUNK image 370,828 bytes. SHA-256:
  `dc650f0e6b3acaa95ce0e886f0e1b8a887371c5797c421187be2b72f302bbb66`.

Retained logs in CopperScreen `artifacts/`:
`stale-pixels-fix-v1-host.log`, `stale-pixels-fix-v2-host.log`,
`stale-pixels-fix-v3-host.log`, `stale-pixels-fix-v4-regressions.log`,
`stale-pixels-fix-baseline-check.log`, `stale-pixels-fix-solution.log`, and
`stale-pixels-fix-v1-native-build.log`.

## Original-client replay inputs (v49-backfill)

Both runs retain `mui-calc-native-mouse-v3`, the separate
`a500-ocs-5m-diagnostic-v1` profile, unchanged original seven-file MUI Calc 1.25,
unchanged v48 CopperOS muimaster.library, frozen engine/CPU, and pinned ROMs.
The hardware mouse sequence is `5 Enter 6 Enter +`, followed by the real close
gadget and 5,000 additional guest fields after empty MUI registries are observed.

CopperStart retains ROM v140 and uses a private FFS image refreshed only with
the newly compiled disk GUI provider and its matching loader entry offset.
`refresh_utility_gui.py` validates the complete before/after filesystem inventory.
Image SHA-256:
`9ece460a7344e33533f041d0d59f7034f0caaf53fdc0f2f2b6f96efee98a97c2`.
Provider/loader receipts are in `artifacts/stale-pixels-fix-v1/guest/`.
The source core is shared with ROM builds; this replay qualifies its guest-loaded
provider deployment on the pinned ROM, without claiming a rebuilt-ROM replay.

Native Kickstart 3.1 retains the v48 OFS image: its real Intuition/Layers remain
active and the GUI loader skips installing the development replacement. Image
SHA-256:
`e1733f3cf0922dc1d1f10f9b10de37e16b2f486d108811a1d7e3d0a03a10e799`.

## Bitmap validation

The read-only inspector
`scripts/probes/UtilityStartup/inspect_mui_calc_backfill.py` reads the actual
screen BitMap planes using each capture's recorded RAM mapping. It measures
nonzero pen pixels in the live Calc window rectangle before and after closing,
and validates the final MUI registry cookie/counts/depths. Hardware sprites are
separate from these bitmap planes.

The v48 CopperStart counterexample has 25,121 nonzero pixels both before and
after closing: `artifacts/stale-pixels-fix-v48-baseline-bitmap.json`. A restored
bare-screen background must have zero in that rectangle after close; empty MUI
registries alone are insufficient.

## First native attempt: v49-backfill

Both original clients visibly compute `11` using the hardware mouse sequence.
Kickstart finishes at field 333,051 with empty MUI registries and a returned CLI
prompt. CopperStart finishes at 401,301 with empty MUI registries, but the OS
window remains linked (`Screen.FirstWindow = 0x6710a0`) and its bitmap is
unchanged: 25,121 nonzero pixels before and after closing. **Background
restoration fails; this attempt is not a passing fix.**

The raw mouse-v3 receipt says input/cleanup PASS because that protocol checks
MUI registries rather than OS window unlinking. It remains unchanged. The
reviewed bitmap result is `artifacts/stale-pixels-fix-v49-bitmap.json`.
Mouse-v4 now also requires the original OS window to be absent after settling;
it builds with zero warnings/errors. Pixel restoration remains a separate
read-only check.

An artifact-only native trace and a small window-close HDF probe reproduce the
failure without MUI. The initial ADF version did not reach screen-open results
on this retained ROM/media combination and is unavailable coverage, not a pass.

## Final native candidate: v50-backfill

The detailed artifact trace retains a valid workspace header, count 1, bitmap
640×256 and rectangle [12,16,331,195]. The native execution rejects the old
copy-clipping call before staging the fill (trace marker 49). The revised fill
path reads bitmap geometry into scalars and clamps its single destination's
inclusive bounds directly. No shared compiler or CPU source is changed.

The revised source passes the same 570 selected host tests, including all seven
new regression cases. The final solution rebuild has four warnings and zero
errors. Native freestanding compilation passes; code is 354,108 bytes and HUNK
image 370,940 bytes, SHA-256
`3eb6c22d1669f6d63c0aff7ebb16739b010ae2927800d071b286c6c2de64db4e`.
Logs are `stale-pixels-fix-v5-host.log`, `stale-pixels-fix-v2-solution.log`, and
`stale-pixels-fix-v2-native-build.log`.

Both short native HDF regressions pass under the pinned CopperStart ROM and
frozen engine/CPU:

| LayerInfo policy | Pixel before | Pixel after | FirstWindow after | Snapshot ledger |
| --- | ---: | ---: | ---: | ---: |
| Default backfill | 1 | 0 | 0 | 0 |
| Explicit no-backfill | 1 | 1 | 0 | 0 |

The guest probe opens, paints and closes a normal Simple window using public
Amiga calls. Its reset-lived resource records the observations. Source is
`scripts/probes/UtilityStartup/layer_background_probe.py`; the independent
reader is `inspect_layer_background_probe.py`. Native captures at field 6,000
and reviewed receipts are under `artifacts/stale-pixels-fix-v2/`:
`run-fix2-v1/`, `run-fix2-suppress-v1/`, `default-result.json`, and
`suppress-result.json`. Diagnostic trace instrumentation is absent from this
provider.

The full v50 original-client pair uses mouse-v4, with the same hardware input
sequence, bounds and 5,000-field settling period. In addition to empty MUI
registries, v4 verifies the original native window is no longer in its screen's
window list. This is a separate protocol version; v3 receipts are unchanged.
The CopperStart FFS image changes only the GUI provider/loader from the v48
client image. Its SHA-256 is
`4473c347e20345e0f13171876b0147bbd63c4cb00cd79a5e19a21ab63b084695`.
The MUI library and complete original Calc package remain unchanged.

Both full mouse-v4 runs terminate with exit code 0 and input/cleanup PASS.
All five hardware mouse releases are accepted in order. Reviewed arithmetic
images visibly show `11.000…` on both ROMs.

| Observation | Kickstart 3.1 | CopperStart |
| --- | ---: | ---: |
| Opened capture | 138,551 | 174,001 |
| Arithmetic result capture | 306,301 | 353,951 |
| Empty MUI registries observed | 328,001 | 396,251 |
| Settled final capture | 333,051 | 401,301 |
| Original OS window unlinked | PASS | PASS |
| Final objects/classes/dispatch/callback depth | 0/0/0/0 | 0/0/0/0 |

The cleanup and settled captures independently retain valid registry cookies
and zero counts/depths. Their observed field separation is 5,050, including the
diagnostic's 50-field sampling boundary. Kickstart visibly returns to `1>`.
CopperStart's `MUI_calc_process` disappears; its parent CLI has no loaded
command/module and return code 0.

The final CopperStart image shows a gray background with no Calc rectangle.
Read-only inspection of the actual screen bitmap planes measures **25,121
nonzero pixels before close and zero afterward** in the 320×180 rectangle.
`Screen.FirstWindow` is zero. The hardware mouse sprite is separate from those
planes. Detailed output is `artifacts/stale-pixels-fix-v50-bitmap.json`.

Captures and immutable raw receipts are in
`artifacts/utility-mui-2026-10-31/mui-calc-native-interaction-{kick31,copperstart}-v50-backfill/`.
Earlier v48/v49 stale-pixel failures remain unchanged. No ROM image, product
preset, engine/CPU pin or MUI library binary was changed for this repair; the
tested CopperStart deployment uses the rebuilt guest-loaded GUI provider.

## Source commits and isolated publish checks

The completed implementation is recorded in
[CopperOS 6ad194f](https://github.com/ilehtoranta/CopperOS/commit/6ad194fb7d3cfe84dc1770e583bf59db00f71e1f)
and [CopperStart 1cd778b](https://github.com/ilehtoranta/CopperStart/commit/1cd778bd4a6e5c8da1160f4d5599de2730220a73).

Before publication, detached checkouts containing only the selected changes
pass 3,902 MUI tests in each identity profile and the same 570 selected Layers
tests, with zero skipped. These checks use the documented local CopperSharp SDK
option; the development `g20` packages are unavailable from the public NuGet
feed. The Layers checkout includes its existing pinned CopperSFS submodule.
No unrelated workspace edits are required by these selected tests.

The isolated CopperScreen production build is unavailable because its qualified
CopperOS firmware/HDF bundle is not staged. The bundle guard remains intact;
this is not a successful production build. The utility probes and evidence do
not change the production app, machine presets or dependency pins.
