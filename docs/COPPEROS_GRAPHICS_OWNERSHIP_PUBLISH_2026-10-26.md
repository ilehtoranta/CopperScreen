# Ownership reservation and publication — 2026-10-26

The graphics-ownership-publish.32 development qualification follows
[bitmap-copy channels](COPPEROS_GRAPHICS_COPY_CHANNELS_2026-10-26.md). It bundles
the new standalone graphics ownership code with the exact reset bank and
unchanged layer-lock.9 HDF. The
[producer report](../../CopperStart/Docs/NativeGraphicsOwnershipPublish20261026.md)
records the invariant proof, complete 34-call table and remaining gaps.

| Artifact | SHA-256 |
| --- | --- |
| Combined ROM | `736de9ba7b3b6c163cdff6ee1278d7ae8d7ae74343d77acdf416639160f6411b` |
| Unchanged DOS1 HDF | `9cc2927c8647fea4a571b21c0805af25db4aaef13508eaf5a1d068e1ef0a3ff5` |

Own checks closure once after atomic reservation and relies on the established
Owner/Lock publication ordering. Disown's owner comparison already proves a
live grant because control release cannot close a held owner/lock. Repeated
checks add no protection and are omitted. Private Tag admission, close
cancellation, owner identity, queue/FIFO handoff and register preservation remain.
Four new native probes call the real close body at every ownership instruction
boundary on both CPUs; existing preemption and pending-DMA checks also pass.

Completed MC68000 guest-cycle medians:

| Call | Kickstart 3.1 | .31 | .32 |
| --- | ---: | ---: | ---: |
| OwnBlitter uncontended | 62 | 320 | 278 |
| DisownBlitter uncontended | 64 | 282 | 244 |
| Text fixed8, 8 characters | 7,912 | 9,800 | 9,712 |
| RectFill 16×8 | 2,368 | 3,092 | 3,002 |
| BltBitMap 16×8 | 2,416 | 4,466 | 4,380 |

The ownership pair uses **13.29% fewer cycles**. All 34 original result controls
match, and independent/full runs reproduce the timing table exactly. All 840
legacy copy shapes and 168 text-row shapes improve. Of 2,016 rectangle shapes,
2,012 improve, two tie and two regress by at most ten cycles. All 16 Layer
shapes improve through the graphics change; Layer implementation is unchanged.
The separate 1,680-shape expanded copy replay has 1,659 improvements, 18 ties and
three regressions of at most six cycles; all 840 original controls still match.
These are guest-cycle diagnostics, not host-throughput acceptance.

Across the full run and independent supplement, 165 distinct graphics cases
execute successfully. The first run's missing-environment native-range theory
remains recorded as a skip; the supplement executes both missing CPU cases.
All 11 bundled native tests pass. Host/migration has 82 passes and three
unavailable historical optional replays; four prior-seed upgrades execute.
Consumer Release is clean. Producer Release retains three existing Shell/DOS
API errors. Forty of 44 factory captures are unchanged and all reproduce exactly;
affected images shrink by 32 bytes.

Cold readiness remains **13.84 guest seconds** and redraw **160 ms / eight PAL
fields**, with identical profile/control CPU/RAM, pixel and PCM state. No visible
desktop timing gain is claimed. CPU/chipset, compiler, package and HDF payloads
are unchanged. Local evidence is
`../CopperStart/artifacts/graphics-ownership-publish/qualification.json`.
Ownership, text and other drawing gaps against Kickstart remain active work.
