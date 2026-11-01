# MUI Calc stale-pixel investigation — 2026-11-01

The evidence points to a **CopperStart Layers default-background backfill gap**.
The calculator process and native window are already gone. The absence of a
Workbench backdrop exposes the gap; it is not sufficient grounds to treat the
remaining pixels as expected behavior.

## Contract and source path

The AmigaOS [InstallLayerInfoHook autodoc](https://developer.amigaos3.net/autodocs/layers.library/InstallLayerInfoHook.html)
assigns the LayerInfo hook responsibility for areas without a layer. The default
hook selects normal backfill; `LAYERS_NOBACKFILL` explicitly suppresses it. The
hook is invoked when layer deletion exposes background. Installing a custom
hook is not a prerequisite for default backfill.

In `CopperStart/src/CopperStart.Layers/LayersMutationCallbackCore.cs`,
`TryPrepareBlank` calculates newly uncovered background rectangles only when
the policy is `GuestHook`. For `DefaultBackfill`, it returns success with an
empty workspace and count zero. `FinishPreparedInto` then takes the no-callback
commit path, leaving no default blank-area fill to execute. Layer-owned reveal
handling is separate and does not repair an area with no underlying layer.

`NativeIntuitionLibrary.CloseWindow` deletes the layer and unlinks/frees the
Window, then calls `NativeIntuitionRefresh.Repaint`. That repaint handles the
screen-bar layer and remaining windows. It cannot repair this case: the only
remaining layer covers the bar, not the calculator rectangle.

## Captured guest evidence

The retained v48 CopperStart capture at field 401,251 has:

- Screen `0x6703e0`, LayerInfo `0x6704c0`.
- LayerInfo.BlankHook at offset 94: **0**, the default backfill sentinel.
- Screen.FirstWindow: **0**; remaining layer `0x670fc0` bounds
  **[0, 0, 639, 10]**.
- MUI objects/classes and dispatch/callback depth: **0**.
- The previous `MUI_calc_process` is absent from task lists. Its parent CLI has
  no loaded command/module and return code **0**.

The final bitmap still displays the calculator at y16 and below. This confirms
the symptom is leftover bitmap content after successful teardown. It is outside
the remaining layer's bounds. Read-only RAM inspection used each capture's
recorded memory mapping; no guest memory was changed.

## Focused reproduction

An artifact-only test extends the existing `LayersVectorRouterTests` fixture
without changing production source or adding a failing test to the normal suite:

1. Initialize LayerInfo and a bitmap with the default background hook.
2. Create a Simple layer with its own backfill disabled, bounds [2,16,31,35].
3. Delete the layer through the public vector router.
4. Verify successful deletion and count background fill operations.

Results against the current CopperStart source:

| LayerInfo policy | Expected fills | Actual fills | Result |
| --- | ---: | ---: | --- |
| Default backfill | 1 | 0 | **FAIL**, reproduces missing fill |
| Explicit no backfill | 0 | 0 | PASS, control |

Artifacts are under `artifacts/stale-pixels-investigation/` in CopperScreen:
`DefaultBlankDiagnostic.cs`, `Diagnostic.targets`, and `test.log`. The diagnostic
was compiled using an isolated artifacts path and selected by its method name.
This is host semantic coverage, not a repaired native ROM/media replay. The
existing frozen MUI images and raw receipts remain unchanged. No matched native
Kickstart bare-screen reproduction was run in this investigation.

From the CopperStart checkout, reproduce with:

```powershell
dotnet test tests/CopperStart.Exec.Tests/CopperStart.Exec.Tests.csproj -c Release `
  --artifacts-path ../CopperScreen/artifacts/stale-pixels-investigation/build `
  -p:CustomAfterMicrosoftCommonTargets=C:/Users/ilkle/Koodit/GIT/CopperScreen/artifacts/stale-pixels-investigation/Diagnostic.targets `
  --filter FullyQualifiedName~InvestigationDeleteLastLayerHonorsLayerInfoBlankPolicy
```

## Repair scope

Repair background exposure in the Layers topology transaction: compute the
newly uncovered rectangles for default backfill, stage the default fill through
the existing transactional graphics provider, and preserve custom-hook and
explicit-no-backfill behavior. Keep rollback, bitmap clipping and protection of
remaining layers intact. Validate deletion and movement with partially covered
background, allocation failure, and custom/no-backfill policies before native
replay.

Clearing a screen rectangle in MUI or starting Workbench solely to cover the
symptom would bypass the faulty Layers behavior. No production fix, ROM rebuild,
or claim of resolved stale pixels is included in this investigation.

## Repair verification

The [subsequent fix and v50 retests](UTILITY_MUI_BACKFILL_FIX_2026-11-01.md)
verify default background clearing in the native provider and original Calc.
This investigation retains the pre-fix findings; its failure captures are not
relabeled.
