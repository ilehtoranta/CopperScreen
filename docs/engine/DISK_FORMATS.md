# CopperDisk floppy formats in Lightweight

2026-10-07. The desktop app, headless runner and scripted disk swaps support the
following media in DF0–DF3, directly or through selected ZIP entries.

| Format | Execution path | Writes |
| --- | --- | --- |
| Standard 880 KiB ADF | Canonical DD sector encoding; existing compact receiver | Optional in-memory writes and explicit Save ADF |
| ADZ | CopperDisk gzip decoding of standard or modern extended ADF | Read-only |
| DMS | CopperDisk complete, unencrypted DD archive decoding | Read-only |
| Modern extended ADF (`UAE-1ADF`) | CopperDisk normal/raw tracks, up to 84 cylinders; raw bytes and exact bit lengths retained; omitted tracks and zero raw bit lengths mean unformatted | Read-only |
| IPF | Existing preserved track/index/density/weak-region preparation | Read-only |
| SCP | CopperDisk first-revolution Amiga DD decoding; preserved receiver | Read-only |

Old `UAE--ADF`, partial/encrypted DMS, HD/non-Amiga SCP and unsupported geometry
fail explicitly according to CopperDisk's format validation. Preserved geometry
is limited to cylinders 0–83, heads 0–1, with no duplicate tracks or cells finer
than one CCK. Standard sector media uses 80 cylinders, two heads, and eleven DD or twenty-two HD
512-byte sectors per track. HD ADF/ADZ requires an HD mechanism configured through
`FloppyDriveTypes`; see [HD support and qualification](HD_FLOPPY_2026-10-18.md). Missing preserved tracks produce no flux.

The [Nitro follow-up](COPPERSTART_NITRO_2026-10-23.md) removes CopperDisk's
fixed 160-track restriction for UAE-1ADF/encoded media within that existing
84-cylinder range. Normal entries beyond cylinder 79 retain their physical
AmigaDOS headers; odd final track counts leave the absent head unformatted.
Zero-bit raw entries represent unwritten tracks and use a zero-filled PAL DD
revolution. Nonempty raw streams retain their stored bytes and bit lengths.

SCP is quantized at load time to CopperDisk's nominal DD grid and first stored
revolution. The engine retains its decoded transition bytes, including a pulse
terminating a long no-flux gap. It does not replay original interval timing,
analogue capture noise or changing revolutions. Nominal drive rotation remains
300 RPM. IPF density preparation and existing receiver timing remain unchanged.
This integration does not certify protected-game or physical receiver accuracy.

`LightweightFloppyImage.Prepare(bytes, filename)` owns validated decoded media;
`MountDisk(drive, prepared)` attaches it. ZIP extraction belongs to the host.
When supplied an archive filename, the factory identifies its already extracted
single image by signature. Preparation fails before the existing disk is replaced.
Track conversion occurs during preparation/mounting, never on a seek or per bit.
Standard sector images use the established path; extended ADZ uses preserved
tracks even though its extension is `.adz`. Incomplete decoded sector views are
never used to reconstruct preserved media.

Desktop pickers, drag/drop, startup settings, ZIP disk selection and delayed swaps
accept every listed format. Read-only media overrides a stored writable setting
at startup; an explicit attempt to enable writes or export ADF fails. Source
files and archives are never implicitly rewritten. The desktop retains its
32 MiB limit per source image/ZIP entry. ZIP bootability ordering is a best-effort
standard ADF/ADZ boot-block heuristic; extended/raw/DMS/IPF/SCP entries may need
explicit boot-disk selection. Minimal disk boot remains limited to standard ADF.

Runner `--disk` selects DF0; `--disk1`, `--disk2`, `--disk3` select external drives
with `--drives 4`. Existing `--adf`/`--ipf` aliases remain supported. ZIP selection
uses `archive.zip#/entry`; an unselected archive must contain exactly one supported
image. Input scripts accept `diskPath` or legacy `adfPath` and decode media before
execution. The existing exclusion of media-changing acceptance intervals remains.

Validation uses original synthetic media: ADZ, DMS, normal/raw extended ADF,
compressed extended ADF and SCP sectors recovered through Paula DMA; raw
non-word-sized track preservation; outer SCP cylinders and no-flux termination;
malformed replacement retention; four-drive ZIP/delayed/scripted swaps; write and
export rejection. Active raw-track stepping allocates zero bytes in the diagnostic
fixture. Native Kickstart 1.3 boots an original diagnostic boot block through all
six added representations with both scalar and conservative CPU execution; a
standard ADF control also passes. ROMs and captured/game media are not committed.

Run native diagnostics by supplying `COPPERSCREEN_KICKSTART13_ROM` with the A500
34.5 ROM (SHA-256 `EE05862D8102A08436AC4056DA7D549DB31625C7D47B24DFB7B3C9A5C113CA53`).
Absent ROM input skips native boot coverage; it is not a successful replay.
Synthetic capture conversion and diagnostic boot are bounded integration evidence,
not native game/Workbench compatibility or a throughput acceptance measurement.
See [storage](STORAGE.md) and [performance](PERFORMANCE.md) for retained limits.

Final validation: production Release build passed with zero warnings/errors;
diagnostic suite 1,144 passed, zero skipped (both supplied native ROM inputs);
host suite 177 passed with six optional native replays unavailable;
CopperDisk suite 74 passed. A native-ROM runner smoke check also exercised four
drives with direct ADZ, an unselected single-image ZIP, an explicit ZIP selector
and a frame-zero scripted swap. Its three frames are command-path coverage only.
