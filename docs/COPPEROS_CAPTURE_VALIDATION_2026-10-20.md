# CopperOS capture validation and correction — 2026-10-20

**The earlier blank-midpoint finding was an incorrect visual review.** The
original signed `1.1.43.0` CopperOS start, midpoint and end PNGs are byte-for-byte
identical. Each contains the Shell text. The earlier signed `1.1.31.0` midpoint
PNG is also the same file content. No intermittent guest-display, presentation
or capture failure is established by those images.

The original [hour report](INSTALLED_SESSIONS_2026-10-20.md), its
`COMPLETED_WITH_DISPLAY_FINDING` JSON verdict and its visual-review annotations
are retained unchanged as historical evidence. This separate correction
supersedes their blank-capture interpretation. Their completed hour, native
save/readback, software-audio and memory results remain valid within their
declared scope. LWA-HOST-005 is closed as a review false positive.

Release budgets remain **OPEN**. Lightweight remains the active engine; Legacy
stays unavailable. No renderer, guest, CPU, DMA, pixel-generation or audio-pacing
change is warranted by this finding.

The [hash-bound correction record](COPPEROS_CAPTURE_VALIDATION_2026-10-20.json)
retains the original image audit, source comparison, signed packages, native
reports/layer images, capture sidecars, controls, logs and cleanup evidence.

## Original image audit

All four images are **24,247 bytes**, **797 × 807 pixels**, with SHA-256:

`0f5d68568db654c5b140a70cfee7b2c5c70fb4f5dcb1a0f898f2b41d7c694a81`

Their current hashes match the hashes already stored in the hour record. Full
decoded RGBA image comparison has no differing pixels. Each guest/presenter
region contains **2,962 visible neutral-light text pixels**, using the recorded
region `(0,193)–(797,763)`, opacity at least 128, minimum RGB channel at least 128
and maximum channel difference at most 8. Toolbar text is outside that region.
This is a predicate for the fixed proof console, not general OCR.

| Original retained image | Recorded hash matches | Neutral-light text pixels |
| --- | --- | ---: |
| Signed 43 start | Yes | 2,962 |
| Signed 43 midpoint | Yes | 2,962 |
| Signed 43 end | Yes | 2,962 |
| Signed 31 midpoint | Yes | 2,962 |

A readable start and an identical midpoint cannot support the earlier claim
that the midpoint PNG lost its text. The mistake was in the review, not a
subsequent change to those files. No raw image is replaced and no prior report is
silently rewritten.

## Capture validation change

Diagnostic installed captures now save a sidecar with the PNG SHA-256, presented
frame identifier, presenter text-pixel count, captured text-pixel count and
explicit proof-text expectation/result. The fixed CopperOS Shell/storage proof
requires at least 100 visible neutral-light pixels in both the presenter and
captured guest region. Missing proof text retains the failed PNG/sidecar and
fails the diagnostic session. CopperStart captures retain the same metadata
without a proof-console text requirement; game fades and title transitions are
not rejected as missing Shell text.

The image is rendered and saved before the mutable presenter bitmap is read
back. [Avalonia's pinned implementation](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.3/src/Avalonia.Base/Media/Imaging/WriteableBitmap.cs)
uses a bitmap lock even for `CopyPixels`; the probe does not read-lock the
presenter before capturing the suspect output. The
[render-target API](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.0.3/src/Avalonia.Base/Media/Imaging/RenderTargetBitmap.cs)
continues to render the normal visual tree. There is no retry-until-readable,
replacement image or forced guest rendering to manufacture a successful capture.

The guard, pixel readback and layered probe compile only with
`CopperScreenInstalledTests=true`. Normal release execution has no added
per-field observer. `-CaptureProbe` requires `-Smoke`; it cannot produce installed
hour acceptance. Seven focused controls cover missing text in either layer,
opacity, two rotations through all three presenter bitmaps, matching saved
hashes and retention/rejection of a genuinely blank image.

## Separating guest, presentation and capture

The bounded installed probe records these independent observations:

1. Native guest framebuffer and Shell text decoded from guest bitmap memory,
   copied through an owner-thread observation command.
2. The session's normal expanded framebuffer.
3. The front presenter bitmap and last presented frame identifier.
4. The normal window `RenderTargetBitmap` PNG, analyzed only inside presenter
   bounds.

The window is saved before presenter readback, on the UI thread. Native and UI
observations retain separate QPC timestamps; this quiet proof does not claim all
four observations belong to one identical guest field. No parallel device owner,
host DOS shim or alternate guest output path is introduced.

Signed diagnostic `1.1.46.0` uses the frozen signed-43 basis plus observers.
Its two installed processes complete native save/reopen and provide **64 paired
samples**, all containing text at every layer. The guarded diagnostic
`1.1.47.0` completes the same native paths and another **64 paired samples**, all
containing text at every layer. All **six standard guarded captures** pass their
presenter/capture text checks and match their saved hashes. All four processes
exit normally and both owned test registrations/private families are removed.
Both packages are development test artifacts with separate identities; neither
replaces the original signed 43 package.

| Diagnostic package | MSIX SHA-256 | Paired samples with text at every layer |
| --- | --- | ---: |
| `1.1.46.0` observers | `3a4646c7ee2a29adcc1bae7154a60429df1dd22614806dde128cb6b081795b0e` | 64 / 64 |
| `1.1.47.0` capture guard | `65136926c1d3a1b5df0b46294d3614575548398978ac216f05e482d6bb566446` | 64 / 64 |

Both packages pass fresh SDK signature verification and all 266 payload-file
checks. The source audit compares 190 signed-43 basis inputs: engine, disk,
runtime and main-window source are unchanged. Existing-input differences are the
diagnostic presenter/soak hooks and the frozen current packaging script/module;
new capture files are diagnostic-only. The presenter also retains an existing
inert `EffectiveSourceSize` getter from current layout work, unused by the frozen
main window. No shared CPU package is changed.

The production solution builds with **zero warnings/errors**. All **seven**
focused capture controls pass with no skips. Diagnostic control-build logs
retain existing CA1416 warnings from unrelated backup-I/O diagnostic code; no
warning policy is weakened. One early source-audit allowlist and one early SDK
command preparation were invalid ancillary attempts, corrected before recording
their passing evidence. Their reasons and the fresh verification logs remain
retained; no native failure is relabelled.

## Release boundary

The measured 19 CopperStart and 3 CopperOS presentation-buffer drops in the
original hours remain unchanged. They are separate observations and provide no
causal evidence for the false blank-image claim. Physical display/vsync, audible
quality, S3/PnP/HID/mixed-DPI/manual workflows, minimum/reference hardware and
numeric performance/audio-latency budgets remain open. This continuation adds
no new full-hour, WACK, production/Store-identity, acquisition or MSIX-update
verdict to the diagnostic 46/47 packages.

Before opening a new capture defect, check the retained file hash and pixel
evidence, then distinguish guest memory/video, presented pixels and captured
pixels. A rendered preview alone cannot override byte-identical, readable
evidence. A new physical-window report requires its own timestamped observation.
