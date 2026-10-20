# CopperStart standard-app qualification — 2026-10-16

Scope: P1-03's bounded standalone-ROM integration on Lightweight. This records
the production app session, not a Legacy host restoration, a full-library claim
or a Store certification result. See the [usage and artifact contract](COPPERSTART_APP.md)
and [machine-readable record](COPPERSTART_VALIDATION_2026-10-16.json).

## Inputs and configuration

The ROM is the 524,288-byte final native-checksum image, SHA-256
`ee645f0b8136904d12dde0eace1339b1010530671eeb28ebcd63d64a319620e1`.
Consumer qualification ID is `diskboot-2026-10-16-dev.1`; no producer release is
claimed. The [producer record](engine/COPPERSTART_DESERT_DREAM_2026-10-16.json)
retains the exact exported base, patch/source hashes and compiler inputs.
Producer fix commit `33eef25141c4f92558b2ea6e4dae3d89c5be040c` identifies the
upstream fix; it does not mean this ROM includes unrelated concurrent producer
working-tree changes.

The app preset fixes PAL OCS 8371/8362, 68000 interpreter, 512 KiB Chip + 512 KiB
slow RAM, no Fast RAM, RTC disabled, one read-only DF0, no hardfiles or shared
folders. This differs from the older 0-slow-RAM Lotus record; the older
fingerprints and observations are not relabeled. The current CPU package is
`Copper68k 1.5.4-qualified-dev.1`.

Lotus 2 uses the supplied ZIP with archive SHA-256
`297a528803eab78bb0b0a693353f6271387c947171ad71868279235b5a35dfa9`, entry
`Lotus Turbo Challenge 2 (Magnetic Fields + Gremlin).adf`, SHA-256
`82dc1faf56823653cd3b0b299e179932c1fae117b03cae220e121d96ae8b3a06`.
The recorded input is
[`corpus-lotus2.json`](../CopperScreen.Lightweight.Tests/Workloads/corpus-lotus2.json),
SHA-256 `b54d47135be66b7949a3c5fc10daad342801789570ce27799c6ed7cb15768fd3`.
Both entry and input hashes are asserted before replay.

## Checks and observed milestones

The production solution builds in Release with zero warnings/errors. The final
ordinary host run passes 230 cases with eight optional native cases unavailable;
the two CopperStart cases run separately with their actual supplied inputs.
All 74 disk tests pass. Package locks are unchanged. No engine or CPU execution
source changed; this work does not require new hardware timing or performance
acceptance measurements.

The focused pipeline exercises ten configuration/storage/negative cases, an
actual-ROM lifecycle case and the native Lotus replay. It checks:

- Explicit boot-source/ROM-path persistence in personal profiles and last-session
  settings; model selection cannot substitute proprietary firmware.
- Missing/wrong-size/foreign/malformed/ambiguous ROM rejection, incompatible
  hardware rejection and explicit Legacy rejection.
- An original boot disk executing guest code, live mouse movement and both mouse
  and joystick fire bits through guest register reads.
- Cold boot, reset-button restart, Ctrl + both Amiga reset, paused session
  replacement, stop/dispose/reopen, waiting without a disk and subsequent insertion.
- A production `CopperScreenSession.Create` replay with the exact selected ROM
  and media, compared against an independent scalar engine. Every field compares
  delivered PCM; checkpoints compare PC, D/A registers, cycle, Chip RAM and all
  native pixels against both doubled app presentation rows.

The inspected 14,000-field run reaches these separate milestones:

| Field | Observation |
| --- | --- |
| 3,000 | Lotus Turbo Challenge II title during loading. |
| 6,000 | The Company crack intro with PRESS FIRE, before scripted fire at 6,020. |
| 9,000 | High-score page before the next fire event. |
| 10,000 | Forest course selection/loading after scripted menu input. |
| 11,000 | Starting grid and SET, 0 MPH, timer 70. |
| 12,000 | Opening race, 9 MPH, timer 51. |
| 12,800 | Acceleration to 61 MPH, score 21,830, timer 35. |
| 14,000 | Race continues after release, 9 MPH, score 77,940, timer 11. |

These observations establish loading, menu progression and input-driven use
separately. They do not establish course/game completion. Non-silent PCM is
present in 9,990 fields; sample equality is a delivery/parity result, not a
listening or physical audio-fidelity assessment. No throughput result is claimed.

The final reproducible pipeline writes PNG checkpoints and `app-session.json`
under `artifacts/copperstart-p1-03/final/lotus-app`, plus TRX and qualification
records under its parent. An earlier replay retained raw BGRA checkpoints under
`artifacts/copperstart-p1-03/lotus-app`; PNG conversions from those pixels were
inspected first. The machine-readable record identifies the final run and exact
binary/capture hashes. ROMs, game images and screenshots remain outside Git.

## Outstanding release qualification

Installed package boot, update and user-data retention with CopperStart remain
unavailable on this host: there is no trusted signed test pair, and Developer
Mode is off. The [P1-02 validation](MSIX_VALIDATION_2026-10-16.md) records that
environment limitation. No trust, developer-mode or elevation settings were
changed. This source-session result does not close the installed acceptance gate.

Controlled ROM distribution/rights and a release producer version remain P0
dependencies. The current app expects a separately supplied qualified ROM in a
stable user-owned folder. CopperOS, general DOS/Workbench replacement, writable
guest-file persistence, other hardware/media and full-game completion remain
outside this qualified slice. Arte and Desert Dream retain their producer-side
evidence until separately exercised through this app workflow. No native
Kickstart failure was observed in this integration, so no new failure-control
replay or golden rebaseline was needed.
