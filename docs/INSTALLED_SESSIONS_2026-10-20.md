# P1-07 signed 1.1.43.0 installed hours — 2026-10-20

**Both installed sessions completed 3,600 measured seconds, followed by successful
fresh-process save/readback checks.** All four native processes exited normally
with code `0`. Real WASAPI output recorded zero missing samples, starved callbacks,
callback errors, submission failures or suppressed PCM, including warmup and both
reopens. Memory observations are descriptive; release budgets remain **OPEN**.
Lightweight remains the only engine and Legacy stays unavailable.

The overall record is **COMPLETED_WITH_DISPLAY_FINDING**. CopperOS's retained
midpoint render has a blank blue guest area. Its start and end renders show the
Shell text, and native storage readback succeeds afterward. Physical display
behavior and the cause of the intermittent blank capture remain unassigned. This
is duration/storage/software-audio acceptance, not complete visual or release
qualification.

The [machine-readable evidence](INSTALLED_SESSIONS_2026-10-20.json) binds the
unchanged package, frozen controller/modules, 13 synthetic trace controls, native
reports, five-second samples, external observer, audio timelines, six reviewed
hour renders, transcripts, retained private files, backups and cleanup. The
[installed-session protocol](INSTALLED_SESSIONS.md) remains
`copperscreen-installed-soak-v1`; these are full sessions, not smoke tests.

## Exact candidate and scope

| Item | Value |
| --- | --- |
| Signed package | `CopperScreen-1.1.43.0-win-x64.msix` |
| Identity | `CopperScreen.Test.40a6779884774716bfe9998be653a665` |
| Publisher | `CN=CopperScreen Installed Tests` |
| Package SHA-256 | `7a34dc3fbe845959d1ea94f1e85e5b61031e55064797a9f73a42d098319cad02` |
| Controller start / completion, Europe/Helsinki | `2026-10-19T23:56:07.5923175+03:00` / `2026-10-20T02:18:22.3282013+03:00` |
| Host | Ryzen 5 5600X, 6 cores / 12 threads, approximately 32 GiB RAM; Windows 11 Home x64 build 26200; Balanced power plan |
| Activation | Actual MSIX installation and four non-elevated native WindowsApps processes |

The exact package is reused without rebuilding, repacking or re-signing. Fresh
signature verification passes, all 266 payload files match their package record,
and all 12 reported loaded-assembly hashes match the signed payload. The existing
local signing trust is unchanged and no private key is exported. The same hash
already has [signed update/persistence acceptance](AUDIO_MSIX_VALIDATION_2026-10-19.md)
and [fresh WACK overall PASS](AUDIO_MSIX_WACK_VALIDATION_2026-10-19.md); neither
procedure is rerun or relabelled here.

Both paths use the qualified standalone CopperStart ROM
`2b3d6d31663a9bbc5baa68d0d8549b5079e81b8d075ce79e95b2975c01515db9`,
PAL OCS A500 / 68000 / 512 KiB Chip + 512 KiB slow RAM. There is no RTC, Fast RAM,
HDF, shared-directory substitution or host DOS shim. Copper68k remains pinned.
The retained ROM and CopperOS seed are development proof assets, not admitted
shipping content.

CopperStart uses the protected Lotus II ADF and frozen `corpus-lotus2.json`,
reaches at least 14,000 observed runtime frames, then runs the game/score/title/
attract cycle for an hour. Its asynchronous inputs do not establish scalar
checkpoint alignment, full-game completion or a native high-score save.
CopperOS boots a new owned workspace, runs native `storage` create/mutate/verify,
checkpoints, then observes the quiet Shell for an hour. This is not an active
CopperOS audio demonstration or a continuously busy OS workload.

Normal background applications and independent development work remain allowed.
This task ran no builds or tests during either measured hour. The controller
held a scoped system/display wake request and released it at completion; it did
not change the power plan, affinity, emulated clock or priority policy.

## Measured hours

| Observation | CopperStart | CopperOS |
| --- | ---: | ---: |
| Monotonic seconds | 3,600.0506 | 3,600.0330 |
| Retained samples | 714 | 719 |
| Maximum sample gap, seconds | 8.6895 | 5.1676 |
| Complete guest fields | 179,718 | 179,718 |
| WASAPI callbacks | 168,754 | 168,753 |
| Requested / consumed sample values | 345,608,192 / 345,608,192 | 345,606,144 / 345,606,144 |
| Missing sample values / starved callbacks | 0 / 0 | 0 / 0 |
| Callback errors / submission failures / suppressed PCM | 0 / 0 / 0 | 0 / 0 / 0 |
| Guest fields with nonzero PCM | 174,181 | 0 |
| Nonzero output callbacks | 163,810 | 0 |
| Presentation-skipped / presentation-buffer-dropped fields | 0 / 19 | 0 / 3 |
| Host field-start intervals above 80 ms | 0 | 0 |
| Private memory first → last, MiB | 183.0 → 184.9 | 185.2 → 178.6 |
| Sampled private memory min–max, MiB | 179.0–186.6 | 177.3–185.2 |
| Working set first → last / sampled max, MiB | 210.8 → 189.6 / 211.3 | 213.4 → 207.6 / 214.2 |
| Managed memory first → last / sampled max, MiB | 29.3 → 31.8 / 42.3 | 29.3 → 27.7 / 44.1 |
| Gen 0 / 1 / 2 collections during samples | 102 / 15 / 1 | 108 / 11 / 1 |
| Actual app-log growth, bytes | 1,988,484 | 1,973,484 |

No paused, faulted, stalled, reversed or over-30-second-gapped sample series is
accepted. The 30-second bound checks observation continuity; it is not a release
responsiveness budget. QPC supplies the full durations; no UTC/QPC clock-change
episodes were detected by the retained two-second comparison. Memory values are
discrete observations, not a continuous peak measurement, shipping limit or
proof that every leak is absent.

Warmup counters remain separate from measured deltas. All four complete process
timelines additionally report `firstIncidentQpc=0` and
`finalCumulativeMissingValues=0`, including final save/export and normal close
preparation. Their writers are frozen and the native audio sink is joined before
export; the bounded timeline rings are not a full per-field recording of the
entire hour. Supplied quiet PCM in CopperOS is not queue starvation. Neither
callback delivery nor these host interval buckets establish physical sound
quality, display/vsync pacing or a formal engine-throughput result. Loaded-host
timing is **INVALID / RERUN for formal performance acceptance**.

## Saves, restart and cleanup

- CopperStart's original ADF and both exports retain
  `82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.
- CopperOS completes native create/mutate/verify before measurement, native
  readback after the hour and native readback in the fresh installed process.
  Both processes retain the complete saved image hash
  `e58ea8d751039e5ddfc6ee77f72e705ba48ed4b75a8df96de87a926dc8f84bb0`.
- Both `.copperos` backups are independently opened and checked: Format 1,
  qualified seed/pair identity, 901,120-byte image and matching image digest.
- Profile and settings hashes match across each restart. Six retained host-file
  copies match those reported hashes after uninstall.
- Only this run's diagnostic registration is removed. Its family-private storage
  is absent; copied private state, transcripts, logs, protected media and exports
  remain under ignored `artifacts/audio43-hours-8d028009/`.

All 86 preflight dated record hashes remain unchanged. No earlier result, ROM,
media, signed package or credential is overwritten, published or committed.
The current protocol guide clarifies reuse of a clean candidate family and the
version-specific reserve: eight slots in signed 31 and ten in signed 43. That
documentation correction changes no controller, duration gate or candidate byte.

## Intermittent CopperOS render finding

The reviewed start image contains the registered Shell and
`COPPEROS STORAGE CREATED AND VERIFIED`. The midpoint render is blank dark blue.
The end render contains the original Shell text again **before** the post-hour
input command. Both post-hour and fresh-process native storage readbacks pass.
The earlier signed `1.1.31.0` retained midpoint render also has a blank guest area;
that historical report and image are unchanged. These observations do not assign
a common cause or establish when the behavior was introduced.

No contemporaneous physical-window observation was supplied. Capture fidelity,
presentation and guest display state need a bounded follow-up that distinguishes
them. The 19 / 3 presentation-buffer-drop observations are retained separately;
their relationship to the blank render is unproven. No check is disabled and no
image is replaced to turn this into visual acceptance.

P1-07 remains open for this display finding, physical audio/display/S3/PnP/HID/
mixed-DPI/manual workflows, remaining native Kickstart hours, named minimum and
reference hardware, numeric performance and audio latency budgets. Final
production/Store-identity qualification and the optional WACK dependency finding
also remain open. See the [qualification guide](RELEASE_QUALIFICATION.md),
[packaging guide](MSIX_PACKAGING.md) and [roadmap](PRODUCTIZATION_ROADMAP.md).
