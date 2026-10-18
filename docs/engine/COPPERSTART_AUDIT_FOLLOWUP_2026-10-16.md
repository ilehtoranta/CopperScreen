# CopperStart compatibility audit follow-up — 2026-10-16

The subsequent [Inside the Machine continuation](COPPERSTART_INSIDE_MACHINE_2026-10-18.md)
identifies and fixes the stopped CIA TOD counters behind the black-loader result
below. This dated audit and its original ROM/results remain historical evidence.

CopperStart commit `50f8583a40d24c34a06d4bed71ab31db19d47b95` removes the
remaining listed public input budgets in formatting, resident/KickTag tables,
DOS/BOOPSI tags, DOS transfers, ReadArgs, patterns and HUNK loading. Retained
host adapters also accept stale caller-owned message links, complete
case-sensitive names and word-aligned, separate DOS packets. Allocation
arithmetic, private ownership and actual workspace bounds remain.

The next media test exposed two additional firmware issues. Native Kickstart
hands boot code enabled DMA master/blitter bits; CopperStart left them off.
Trackdisk also treated idle DSKEN as an active transfer, rejecting valid read
and motor requests. The firmware now enables the blitter at handoff, accepts
idle disk enables and restores the incoming DMA enables after reading.
An original boot-block probe independently confirms success, 512 bytes read
and the expected payload under both native Kickstart and the corrected ROM.
The previous ROM returns `TDERR_DriveInUse` for read and motor calls.

The rebuilt ROM is 524,288 bytes, with 446,468 linked code bytes and SHA-256
`AB2E09C7673E871CA6C518375317E8CAD7AB3F60A4CDEE2424D5B4E1AEE53D48`.
The Release CopperStart solution builds without warnings or errors.

| Coverage | Passed |
| --- | ---: |
| Affected semantic cores | 133 |
| BOOPSI attributes | 21 |
| Retained host messages, packets and Icon | 74 |
| Compiled MC68000 formatter/I/O parity | 3 |
| Fresh native boot and standalone-reader ROMs | 50 |
| Exact-ROM DOS, startup scripts and console | 22 |
| **Total, no failures or skips** | **303** |

The media results are separate from these component tests:

| Media | Result on the corrected ROM |
| --- | --- |
| North & South, Captain Paranoia variant | The DMA fixes clear the startup gates. The game then opens the absent native `graphics.library` and calls InitRastPort through its null base. Gameplay remains blocked. All 330 batch/scalar capture files match. |
| Shadow of the Beast, Paranoimia variant | Its AllocAbs request covers `$1BDE0..$69240`, overlapping allocated device/boot state and reserved bootstrap ranges. This needs a different memory layout. Native control reaches the title and disk II/I prompts. Gameplay is unverified. |
| Inside the Machine, DESiRE v1.0.1 | The supplied readme specifies OCS 512 KiB Chip + 512 KiB slow. Native control shows animated scenes and credits. CopperStart reaches the custom loader but stays black through 12,000 fields without starting disk DMA. Its frame counter advances; the cause is unresolved. All 305 batch/scalar captures match. |
| Arte by Sanity, regression | Both 40,000-field runs reach effects, credits, later 3D scenes and the earlier intro sequence. All 505 BMP/state/RAM/PCM capture files and final fingerprints match between CPU modes. |

AllocAbs is documented to fail for memory already in use. Removing that overlap
check would overwrite live firmware objects; the Shadow of the Beast finding
is a placement problem. [Exec AllocAbs autodoc](https://developer.amigaos3.net/autodocs/exec.library/AllocAbs.html).

[The evidence record](COPPERSTART_AUDIT_FOLLOWUP_2026-10-16.json) contains exact
producer, ROM, media, input, binary, TRX and manifest identities. Local captures
remain under `artifacts/copperstart-audit2-2026-10-16`. Diagnostic patched copies
of our ROM isolate the first two blockers; only source-built images provide
the final qualification. ROMs, media and captures are not committed.

All replays use the recorded PAL OCS 68000 A500, 512 KiB Chip plus 512 KiB slow
RAM and one read-only drive. Audio activity is sampled; listening and fidelity
are unassessed. These bounded results do not establish general AmigaOS
compatibility or host-throughput acceptance.
