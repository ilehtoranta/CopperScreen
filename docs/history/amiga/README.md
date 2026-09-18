# Amiga development history

These records were archived on 2026-09-17 from the current working-tree documents,
including their existing edits. They preserve the original investigations,
decisions, measurements and acceptance scope. Historical words such as “current”,
“next”, STOP/GO, approval requirements and Legacy cutover instructions describe
their dated context; they do not govern today's development.

Current guidance: [engine and supported profile](../../../CopperMod.Amiga.Lightweight/README.md),
[architecture](../../engine/ARCHITECTURE.md), [issues](../../engine/ISSUES.md),
[performance](../../engine/PERFORMANCE.md), [native validation](../../NATIVE_VALIDATION.md).
The old G6/G7 gate and completed Lightweight H0–H7 stages are distinct historical
sequences. Neither is reopened by the machine move or documentation consolidation.

| Record | Period / purpose |
| --- | --- |
| [Staged engine execution](LIGHTWEIGHT_A500_ENGINE_PLAN.md) | 2026-09-12–16; implementation, hardware assumptions, tests, optimization and host integration. |
| [Original issue register](LIGHTWEIGHT_A500_ENGINE_ISSUES.md) | Detailed investigations and unresolved assumptions before consolidation. Includes the original duplicate LWA-VIDEO-003; the cracktro entry is now LWA-VIDEO-007 in the active register. |
| [Supported-v1 acceptance](LIGHTWEIGHT_A500_SUPPORTED_V1_READINESS.md) | 2026-09-15/16; accepted profile, live checks, frozen builds and one-off performance waiver. |
| [450 FPS audit](LIGHTWEIGHT_A500_450_FPS_AUDIT.md) | 2026-09-14; matched synthetic workload and feature cost ladder, provisional evidence. |
| [Native gameplay profiling](LIGHTWEIGHT_A500_NATIVE_GAMEPLAY_PROFILE.md) | 2026-09-14; CPU/device attribution and retained/rejected optimization experiments. |
| [Original G6/G7 host policy v2](G6_HOST_MEASUREMENT_POLICY_V2.md) | Approved 2026-09-07; source of reusable host-load rules, with now-retired G6 acceptance/routing requirements. |
| [Migration records](../migration/README.md) | Commit isolation and repository ownership history. |

## Evidence availability and preservation

The original performance host had a hybrid topology with 24 logical CPUs; records
specify the actual P-core, affinity, sibling and power plan per run. These are
historical placements, not defaults for the current Ryzen 5 5600X host. Original
FPS results are not current-machine measurements. Some earlier records provide
only their own placement details; do not infer missing hardware metadata.

Referenced `.codex-tmp` directories, frozen binaries, traces and original-machine
user log paths are absent from this checkout. The records and hashes were retained;
the raw evidence was not recovered or replayed during consolidation. Absence here
does not establish loss at the original location. ROM/game media are user-supplied
and must not be added to Git. Historical `References` files are also absent; see
the [reference inventory](../../engine/ARCHITECTURE.md#hardware-reference-inventory).

Do not relabel old FAIL, INVALID / RERUN, provisional or skipped results. The final
supported-v1 build was accepted by owner waiver while its underlying contaminated
series remained invalid. This is not a blanket future waiver or proof of every
hardware edge. Store future measurements as separate dated records.

[Archive manifest](archive-manifest.json) records each source path, original
SHA-256 and archived SHA-256. Historical prose is preserved; only relative Markdown
link destinations were rebased to the new locations. Archive attributes disable
line-ending conversion for these snapshots so their byte hashes survive checkout.
Plain paths and command lines
inside the records intentionally retain their original context. Some original
top-level files remain as forwarding notes for existing bookmarks and skills.
