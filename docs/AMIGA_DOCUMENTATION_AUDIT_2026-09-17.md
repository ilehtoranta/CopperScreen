# Amiga documentation audit — 2026-09-17

Implementation follow-up, 2026-09-17: the documentation consolidation is complete.
See [current architecture](engine/ARCHITECTURE.md), [issues](engine/ISSUES.md),
[performance](engine/PERFORMANCE.md) and [preserved history](history/amiga/README.md).
The audit below describes the pre-consolidation state; its line numbers refer to
those originals. Benchmark portability remains separate, explicitly documented
tooling work. Engine, host, tests, scripts and workload contents were not changed
by this documentation consolidation.

Recommendation: make Lightweight the organizing subject of the current engine
documentation. Retire G6/G7 cutover instructions from active guidance, extract
the useful architecture, limitations and measurement practices, and archive the
development narrative. Preserve historical results and their original labels.

The original audit was a consolidation proposal, not a new acceptance policy.
It covered all seven Markdown files then in `CopperMod.Amiga/`,
their local links, and the surrounding build, ownership, workload and measurement
instructions. The current working tree was reviewed, including existing edits.
Those files and all application/engine code were left unchanged during the audit;
the authorized documentation implementation is recorded in the follow-up above.

The folder contains about 508 KiB of documentation. The plan alone is 5,926
lines / 369 KiB; the issue register is 1,134 lines / 78 KiB. It contains no Legacy
engine source. [Product ownership](PRODUCT_OWNERSHIP.md) already explains this,
but the folder name and repeated cutover language obscure it.

## File-by-file disposition

All source filenames below are relative to `CopperMod.Amiga/`.

| Document | Disposition | What survives in current documentation |
| --- | --- | --- |
| `LIGHTWEIGHT_A500_ENGINE_PLAN.md` | Extract current guidance; archive the full staged record. | Machine scope, single-owner architecture, integer clock/CCK ordering, CPU boundary, device ownership, reusable output, explicit unsupported behavior. Replace the giant plan with a short guide and prioritized open work. |
| `LIGHTWEIGHT_A500_ENGINE_ISSUES.md` | Keep and consolidate as the active limitations register. | Stable issue IDs, current status, implemented assumptions, impact, evidence needed, revisit triggers and regression coverage. Move detailed resolved investigations and benchmark transcripts into history. |
| `G6_HOST_MEASUREMENT_POLICY_V2.md` | Retire as the active policy; preserve its original text as historical policy v2. | Extract the host-load rules and measurement integrity requirements into a Lightweight performance guide. Remove G6/G7 routing and acceptance requirements from current instructions. |
| `LIGHTWEIGHT_A500_SUPPORTED_V1_READINESS.md` | Archive as the dated 2026-09-15/16 acceptance record. | One short current support/status summary. Keep the original acceptance scope, build hashes and one-off waiver together in the archive; this is no longer a live readiness checklist. |
| `LIGHTWEIGHT_A500_450_FPS_AUDIT.md` | Keep as dated performance history. | A short explanation that synthetic STOP workloads and native gameplay measure different work. Preserve the cost ladder, hashes and provisional classifications in the record. |
| `LIGHTWEIGHT_A500_NATIVE_GAMEPLAY_PROFILE.md` | Keep as dated performance history. | Profiling method and lessons about exclusive versus inclusive CPU cost. Preserve retained/rejected experiments; reprofile before treating its hotspot ranking as current. CPU implementation work belongs to CopperMod/Copper68k. |
| `LIGHTWEIGHT_COMMIT_ISOLATION_2026-09-16.md` | Archive with migration records. | At most a link from product ownership. Prepared worktrees, original host blockers and publication options are historical transaction details, not engine guidance. |

No entire source document should be deleted before extracting its useful content
and preserving its historical record. After that, remove duplicate active prose
and obsolete instructions. Git history remains a backup; a small, explicitly
historical archive makes important evidence easier to find.

## Findings that need correction

### Retired gates still look authoritative

[G6 policy](history/amiga/G6_HOST_MEASUREMENT_POLICY_V2.md), lines 7–15,
requires 600+360 frames, a 45 FPS candidate threshold and production remaining
Legacy. The pre-consolidation AGENTS.md, lines 21–26, directs contributors to that file.
These instructions conflict with the owner's retirement of G6 and the current
Lightweight application.

The plan's “Current contract” still says production remains Legacy (lines
269–271), despite the superseding notice at its top. The readiness document
likewise mixes instructions to select the old engine with notices that it is
unavailable. Current code defaults to Lightweight and rejects Legacy availability
in `CopperScreenSettings.cs:18` and `CopperScreenAvailability.cs:12–13`.

G6/G7 and Lightweight H0–H7 are different historical sequences. Preserve that
distinction in the archive. Completed H-stage stop/go procedures should not be
presented as the workflow for ordinary ongoing development. Retiring G6 alone
does not silently abolish every later Lightweight performance requirement.

### The issue register mixes open work and completed history

`LWA-VIDEO-003` is assigned to two distinct defects: cracktro DMA/host origin
(line 796) and level-1 Copper comparison corruption (line 837). Preserve the
historical collision in the archive. In the active register, retain the original
terrain/Copper ID and give the later cracktro entry a new unused ID, with an
explicit alias note and repaired cross-references.

The “Open follow-ups” section contains repaired issues such as LWA-CIA-003 and
LWA-EXEC-001. The “Explicit implementation gaps” section still states that native
gameplay is unfinished/unaccepted (lines 1082–1095), contradicting the acceptance
summary near the top. LWA-INPUT-004 also retains an earlier open live-recheck
statement. Reconcile these by exact acceptance scope; do not equate a synthetic
banner check with native UI coverage.

Use an index with columns for ID, subsystem, category, current status and next
action. Separate confirmed bugs, unsupported features, unverified hardware edges
and resolved regressions. Keep partially resolved entries explicit: for example,
the host blackout repair does not close residual interlace instability.

### Old-machine evidence is not locally reproducible

The seven documents contain 245 `.codex-tmp` mentions; that directory is absent
from this checkout. The plan/readiness also contain 18 absolute Windows path
occurrences, including `D:\TestData` and `C:/Users/vsys-admin/...`.
Historical paths should remain provenance, clearly marked as original-machine
locations. Active commands should use repository-relative paths, parameters or
documented environment variables.

The G6 policy cites two scripts absent here:
`scripts/run-agnus-g6-controlled-performance.ps1` and
`scripts/test-agnus-g6-host-contention.ps1`. The shared
`scripts/agnus-g6-host-load.ps1` is present and is consumed by Lightweight
harnesses. Do not delete it just because its name contains G6.

`CopperMod.Amiga/References` is absent too, although the plan refers to
`References/UndocumentedChipsetFeatures.md`. Build a short reference inventory
with bibliographic details, relevant sections and availability. Distinguish
hardware/manual evidence from emulator comparisons. Do not claim references or
raw captures were transferred merely because their paths or hashes were copied.

All relative Markdown link file targets in the seven documents resolve. That
does not establish reproducibility: many missing evidence/script paths are plain
code spans. Web destinations and Markdown heading anchors were not validated.

### The retained benchmark scripts need a separate portability update

Read-only host inventory reports AMD Ryzen 5 5600X, 6 cores / 12 logical CPUs.
Both [native retention](../scripts/run-lightweight-native-retention.ps1) and
[native paired comparison](../scripts/run-lightweight-native-paired.ps1), line 53,
explicitly reject a topology with only one efficiency class. Their hybrid P-core
requirement is unsuitable for this homogeneous AMD host. Historical CPU indices,
SMT siblings and affinity masks must not become new-machine defaults.

The retention script also checks an older cycle/output family at lines 147–149:
cycle `2063321672`, output `514760AD9BC62BCE`. Current native tests and
[native validation](NATIVE_VALIDATION.md) instead use cycle `2063321634`, output
`C65F87325946E5DA`. This identifies a frozen historical harness, not a ready
current-build retention command.

Both native harnesses require input-script SHA-256 beginning `B2CFB175...`.
The checked-in runner level-1 script hashes to `FB78167C...`; the portable host
test script hashes to `4C6B630D...`. Neither satisfies that frozen input check.
Several runner workloads still embed `D:/TestData` disk-swap paths. The host
test workload already supplies a portable pattern through `media/lemmings-disk2.zip`.

Create a versioned, portable Lightweight retention protocol separately from the
archived harness. Verify actual core/SMT topology, use Normal priority, retain
host-load telemetry and immutable results, and compare frozen Lightweight builds
on the same host. Record the homogeneous-host placement rule as a policy revision;
do not call it an unchanged execution of historical P-core policy v2. Keep Legacy
comparison available only as historical research, not a current release dependency.
Do not overwrite old expected fingerprints or lower thresholds merely to pass.

The 45 FPS G6 threshold is obsolete. The later 200 FPS objective and 95% retention
criterion have separate provenance: identify their workload and original host,
then state the intended current-host target explicitly. Old-machine FPS is not
a baseline measurement of this machine. No benchmark or replay was run for this audit.

## Proposed consolidated layout

Keep four active engine entry points and reuse existing product/test guides:

```text
CopperMod.Amiga.Lightweight/README.md   engine API, support summary and links
docs/engine/ARCHITECTURE.md             current ownership, clock and device contracts
docs/engine/ISSUES.md                   current limitations and regression index
docs/engine/PERFORMANCE.md              current protocol, workloads and evidence index
docs/NATIVE_VALIDATION.md              portable ROM/media replay instructions
docs/PRODUCT_OWNERSHIP.md              repository and package ownership
docs/history/amiga/README.md           dates, original host, evidence availability
docs/history/amiga/...                 original staged/acceptance/performance records
docs/history/migration/...             isolation and repository-move records
```

The root README remains the product/build entry point. The engine README is the
support/API entry point; the issue register owns detailed limitations. Keep only
a linked support summary in the archived readiness record. The performance guide
owns measurement instructions; dated measurements belong in history. A brief
prioritized work list in the issue guide is sufficient until a new roadmap is needed.

Archive the full plan, both performance investigations, supported-v1 acceptance,
isolation record and original G6 policy. Preserve a pre-consolidation copy of the
issue register as well. An archive index should explicitly say that historical
“current”, “next”, STOP/GO and permission language is not an instruction for
today's development. Preserve original measurement labels, including INVALID and
owner waiver, without reopening accepted sessions.

Each retained result should identify date, host, source/build identity, workload
and input hashes, measured interval, correctness fingerprints, result category,
and whether raw evidence is available locally or only documented historically.
Keep useful small scripts/manifests in Git; ROMs, media and generated binaries
stay out. Absence here does not prove that the original evidence was lost.

## Consolidation order and completion checks

1. Preserve the current edited documents, then extract architecture, support and
   issue status. Resolve the duplicate ID and contradictory acceptance statements.
2. Add archive records and the historical-status index. Replace old document
   locations with short forwarding notes where links must remain compatible.
3. Update root AGENTS.md, README, engine README, native-validation references and
   the website's issue-register link together. The installed Amiga timing skill
   also names the old plan/issue paths; forwarding notes preserve that entry point
   until its references are updated separately.
4. Publish a current performance guide with explicit policy provenance. Adapt and
   validate portable harnesses in a separate tooling change; keep their historical
   versions and sample labels intact. The documentation migration itself requires
   no new gameplay acceptance or FPS gate.
5. Check local links and anchors, source/test paths, workload hashes and active
   instructions. Active docs must agree on Lightweight availability, supported
   hardware, Copper68k package ownership, isolated diagnostic outputs and optional
   media coverage. Historical mentions may remain under the archive boundary.

Do not introduce a sibling CopperMod checkout dependency, include diagnostic engine
tests in production shared outputs, or change emulation/golden outputs during this
cleanup. No engine build or emulator suite is needed for a documentation-only move.
