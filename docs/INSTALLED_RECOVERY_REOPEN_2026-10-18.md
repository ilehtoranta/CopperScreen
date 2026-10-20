# Native audio reopen correction — 2026-10-18

**Signed `1.1.34.0` is ready; installed rerun pending.** The preceding signed
`1.1.33.0` run reproduced a durable post-S3 audio/guest stall. A stopped sink
could restart, but the existing device repeatedly returned `MA_UNAVAILABLE` after
sleep. Actual HDMI disable/restore did not repair that session. CopperOS,
checkpoint/export and fresh-process readback were not reached. Release budgets
remain **OPEN**, Lightweight remains the sole engine, and Legacy remains unavailable.

The [hash-bound record](INSTALLED_RECOVERY_REOPEN_2026-10-18.json) retains the
failed run, native snapshots, Windows transitions, cleanup, isolated source
inventory, build/control results, signed package and prepared fresh run. Its
303 references are local artifacts or source inputs; ROMs, media and credentials
remain outside Git. Earlier dated evidence remains unchanged.

## What the failed installed run established

The installed version-2 native-stop control passed: production recovery made one
restart attempt, then the eight-second window advanced 382 guest frames and
359 audio callbacks. This exercised restart of a stopped native sink; it did not
establish recovery of an invalidated Windows client.

Both real S3 transitions have Windows event receipts. Paused frame `16412` stayed
fixed across sleep. The first active follow-up advanced 363 frames/392 callbacks,
but recorded 118200 missing sample values. The paused follow-up advanced only
28 frames/26 callbacks and ended with the native device stopped. Its cumulative
restart count reached 14. These short bursts are not durable recovery acceptance.

At `13:10:40.510Z`, the app was unpaused at frame `16514`, with `16959` callbacks,
device state `stopped` and last recovery result `-22` (`MA_UNAVAILABLE`, verified
against the pinned binding). At `13:11:10.716Z`, immediately before the endpoint
cycle, frames/callbacks were unchanged and attempts had reached 41. The stall
therefore **predated** the endpoint interruption. The helper verified actual
PnP problem 22 while disabled, then problem 0/OK after restoration. At
`13:11:41.857Z`, frames/callbacks were still frozen and attempts had reached 67.
Snapshots remained responsive, without a reported guest fault. The controller's
raw result is **FAILED: Guest did not run after audio-device**.

Cleanup stopped the helper, restored the endpoint, copied the private diagnostic
state and removed its own test registration. No owner sound observation was
recorded for this candidate. Earlier sound replies do not qualify this run.

## Correction and verified scope

The runtime owner now reopens the native device when stop/start fails. It joins
the old callbacks before reinitializing and retains the PCM queue, gain and guest
machine. Failed initialization remains eligible for the existing bounded retry.
Reopening keeps the original backend: searching all backends during endpoint
absence could select a silent null sink. Native lifecycle calls remain outside
callbacks, following [miniaudio's guidance](https://miniaud.io/docs/manual/index.html).
Diagnostic snapshots synchronize with native lifetime changes and count reopen
attempts. Healthy callback-driven pacing and emulated hardware are unchanged.

The controller now also requires a started output and a callback newer than the
implementation's stall interval at the end of each follow-up window. Historical
native errors remain telemetry; current health is not inferred solely from a
stale result or increasing cumulative counts. These are functional observations,
not approved release recovery/performance budgets.

Completed validation:

- Final production solution build: **PASS**, zero warnings/errors.
- Production runtime/queue controls: **8 passed**, two explicitly unavailable
  diagnostic-only controls skipped, zero failures.
- Exact packaged diagnostic assemblies: **10 passed**, zero skips/failures.
- Signed package verification and all **266 payload files**: **PASS**. The CPU DLL
  retains its pinned hash; test-loaded app DLL matches the signed payload.
- The isolated 175-input app/engine/disk inventory differs from `1.1.33.0` only in
  `MiniaudioAudioOutput.cs`; 174 inputs are unchanged. Controller/support inputs
  have their own inventory. Unrelated working-tree changes are excluded.

An overly long initial staging path and test-fixture path caused build-tool
failures; their logs remain available. An unsigned publish was deliberately
stopped for the backend correction. The final short-path publish/signing and
short-path controls passed. No existing signed package version was overwritten
or published. The generated package tool's parent-Git source list is not used to
attribute this isolated candidate; the explicit input inventories are authoritative.

Exact diagnostic MSIX SHA-256:
`a2936e28a6b4754a59034e8c12cb67d219f387d7acad4ba0ad9ca7dc20a01a55`.
Identity: `CopperScreen.Test.29d66b7daf334d8cb16743b0e0cfed8b`.

The [recovery protocol](INSTALLED_RECOVERY.md) and fresh two-case run are prepared.
Failure cleanup ended the previous elevated helper. The owner must restart
`artifacts/start-recovery-host.ps1` from the existing Administrator PowerShell;
earlier automatic consent launches produced no visible prompt. The app/controller
remain non-elevated and delayed sound replies do not block checkpoint/export/reopen.

Still open: exact-candidate active/paused S3 and endpoint recovery for both boot
paths, native saved-work/export and fresh-process profile/settings readback,
physical sound observation, and fresh update/WACK evidence. The earlier
[`1.1.31.0` qualification](AUDIO_MSIX_WACK_VALIDATION_2026-10-18.md) retains its
original scope. P1-07 and Store release qualification remain open.
