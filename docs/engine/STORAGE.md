# Lightweight storage implementation and evidence

2026-09-18 work following accepted baseline `6b5cb1b`. The PAL OCS/68000/native
Kickstart 1.3 profile and one machine owner are unchanged. Implementation covers
read-only IPF, explicit-save writable ADF and the virtual CopperHDF interface.
**The complete milestone remains incomplete:** native IPF second-disk
compatibility is not fully verified. The owner accepted the current frozen
build's unresolved performance gate for commit/push on 2026-09-18; the invalid
measurement labels remain unchanged and follow-up benchmarks are required.
See [performance](PERFORMANCE.md) and [open issues](ISSUES.md).

## Media and timing contract

| Medium | Attachment | Guest writes / persistence |
| --- | --- | --- |
| Standard 880 KiB ADF | DF0–DF3; direct or selected ZIP entry | Starts protected. Enabling writes changes owned encoded tracks. Save ADF explicitly exports valid sectors using atomic destination replacement. |
| IPF | DF0–DF3; direct or selected ZIP entry | Permanently read-only. Enabling writes or exporting as ordinary ADF fails explicitly. |
| HDF | Construction-time CopperHDF unit configuration | Writable mounts update the backing file; Update/Flush and disposal flush it. Read-only mounts reject writes. No implicit ADF-style save step. |

`MountAdf` remains compatible. `MountIpf(drive, bytes)` and
`MountIpf(drive, LightweightIpfImage.Prepare(bytes))` expose indexed attachment.
`GetDriveFormat`, `CanWriteDrive` and host `CanExportAdf` expose capability.
The host/runner accept `archive.zip#/entry.ipf`; an unselected runner ZIP must
contain exactly one supported image. Desktop ZIP selection supports disk sets.
Runner `--disk`/`--ipf`/`--adf` select DF0; `--ipf1` through `--ipf3` and existing
ADF aliases select external drives with `--drives 4`. Input scripts accept
`diskPath` as well as legacy `adfPath`; `drive` defaults to zero.

CopperDisk decodes preserved tracks before attachment. Lightweight explicitly
disables word padding, starts at the stored index orientation and uses encoded
data directly, never a decoded sector reconstruction. Stored gaps, exact lengths,
weak regions and density metadata survive preparation. Supported geometry is
DD cylinders 0–83, sides 0–1. Invalid/duplicate geometry and cell intervals finer
than one CCK are rejected before replacing the old medium. Unknown density
profiles fail explicitly. Library metadata alone is not a compatibility claim.

Each drive retains an independent nominal 300 RPM spindle. Integer per-track
quotient/remainder timing handles uniform tracks; variable-density tracks use
mount-time cumulative deadlines. Seek, side and selection changes preserve
elapsed revolution phase. Index timing is independent of ADKCON FAST/slow.
Missing/unformatted outer tracks are no-flux in this bounded model. Coasting,
analog noise on unformatted media and arbitrary spindle-speed variation remain
unverified. Standard ADF keeps its compact path where behavior is equivalent.

IPF transitions feed a causal integer receiver. It keeps phase/frequency history,
emits zero through empty windows, and recovers from later pulses without sync
look-ahead. Index does not reset synchronization. Weak regions generate varying
transitions from a deterministic media/drive/cylinder/side/revolution/bit seed;
fixed decoder-generated weak bytes are not used as the live signal. Mount-time
arrays are cached per track; per-bit execution makes no media-interface calls,
decodes nothing and allocates nothing in the measured active fixture.

The receiver follows Keller's phase/frequency transfer law, including the 11/12-bit
accumulators, nominal increment 146 and bounded correction history. It is a
patent-derived model, **not a silicon-certified PLL**. Fast-mode transitions,
aperture and simultaneous pulses still need hardware traces. The existing reduced
ADF slow-mode receiver and its LWA-DISK-010 uncertainty remain separately recorded.

ADF export requires all eleven checksum-valid sectors on every changed track.
Unrepresentable custom/damaged tracks remain in memory; export fails. Desktop
save writes a temporary file beside the destination and replaces only on success;
failed save retains dirty state and the previous destination. Reset retains media
changes. Replacement/eject/restart guards require save or explicit discard. Close
has a discard confirmation. Source ZIPs are never rewritten. Automated host tests
exercise save/failure/reset/replacement and fresh reopening; the actual desktop
file picker, close confirmation and hardfile editor still lack an interactive
visual check on this host.

## CopperHDF interface

The port starts from Legacy commit
[`30615e8317a4069e879b585049ed97ffdfecd4fe`](https://github.com/ilehtoranta/CopperMod/tree/30615e8317a4069e879b585049ed97ffdfecd4fe).
The device remains **`copperhdf.device`**, a 64 KiB virtual Zorro II board,
manufacturer `$07DB`, product `$48`, diagnostic area at `$4000`. This milestone
does not implement A600/A1200 IDE, a physical SCSI controller or host directories.

Public `CopperDisk.AmigaHardfileConfiguration` contains unit, path, read-only,
missing-file creation size, Auto/RigidDiskBlock/Partition mode and optional DOS
environment metadata. Creation uses CreateNew and never overwrites an existing
file. Sizes must be multiples of 512 bytes. Auto prefers a checksum-valid RDB;
explicit RDB requires one; Partition uses the configured synthetic environment.
Metadata meanings/defaults follow Legacy. RDB checksums, bounded lists, partition
environment vectors, filesystem headers and load segments are handled in CopperDisk.
Guest registration and executable relocation stay in Lightweight.

Desktop profiles and **Settings > Floppy Drives > Hard disks** expose these
settings; applying hardware changes requires restart. Existing desktop `--hdf`
and `--hdf-readonly` options work. The runner supports repeated `--hdf`/`--hdf-ro`
for sequential Auto units. Custom units/modes/metadata are available through the
engine configuration and desktop profiles/editor, not new runner switches.

| Commands | Behavior |
| --- | --- |
| Read (2), Write (3) | Validated sector-aligned file offsets/lengths and contiguous guest RAM; actual completed bytes reported. |
| Update (4), Flush (8), Clear (5) | Update/Flush synchronize the backing stream; Clear completes without modifying media. |
| ChangeNum (13), ChangeState (14), ProtStatus (15), GetNumTracks (19) | Fixed mounted-media status; Legacy's sector-count meaning for command 19 is retained. |
| TD64 Read/Write/Seek/Format (24–27) | 64-bit offsets, including offsets above 4 GiB. |
| HD_SCSICMD (28) | Existing bounded TEST UNIT READY, INQUIRY and READ CAPACITY command subset. This is not a physical SCSI controller. |
| NSCMD_DEVICEQUERY (`$4000`) | Device type/subtype and terminated supported-command list. |

Unsupported commands fail explicitly; ordinary TD_FORMAT (11) is not added to
the pinned command set. Native OFS Quick Format uses ordinary write requests.
Errors use Exec/trackdisk values (bad address -5, bad length -4, unsupported -3,
write protection 28, backing I/O failure 20), correcting Legacy's mismatched
numeric constants. Whole ranges are checked before transfers; sector staging
preserves completed-byte counts on an I/O failure.

Gateways use the existing pinned Copper68k API and run synchronously on the machine
owner. `IO_QUICK` completes without ReplyMsg. Non-quick completion returns through
a guest thunk that executes Exec ReplyMsg normally, preserving the caller's A6
and return stack; there is no recursive CPU dispatch or per-CCK HDF polling.
Autoconfig mapping, diagnostic-ROM copying/relocation, device registration and
boot discovery have deterministic tests. Native boot required correcting the
BootNode ConfigDev pointer and executing the DOS resident initializer through
ordinary guest code. Failed candidate construction retains the quiesced original
hardfile handle; successful restart transfers lifetime by shared ownership.

## Verification and open compatibility

Release candidate engine SHA-256:
`5EB543BCF6AD41E718D72C9ADA2445C64D05341BF233AD59584C83318A076FAA`.
CopperDisk SHA-256:
`4F02010A01B314B6B385C81BF1AB608DDA0D799620105B20B986B438BB31DE8F`.
Local raw logs/captures are under ignored `artifacts/storage-2026-09-18/`.
No ROM, game or disposable hardfile is committed.

Subsequent Copper WAIT wake-up correction: current engine
`AF2550B2F47AC851AD2E29B5ECED7F7B39BB2944E63DC9C0931BB2C7ED38DAAF`.
The [native correction record](../NATIVE_VALIDATION.md#copper-wait-wake-up-correction-2026-09-18)
retains the before/after border evidence, 551 passing engine diagnostics and 93
passing host tests including native replays. CopperDisk is unchanged. Earlier
storage measurements below apply to the earlier frozen engine; performance must
be assessed separately for this corrected candidate.

Production Release build: zero warnings/errors. CopperDisk: **74 passed**.
Host suite with both native-input cases enabled: **93 passed, zero skipped**.
Engine diagnostic suite: **547 passed twice** with default runtime/test parallelism after
correcting the UART allocation fixture to warm its complete duplex path and measure
inside a separate non-inlined helper. The zero-allocation assertion is unchanged.
Earlier parallel runs reported allocation-counter failures in UART and receiver;
a later serial run also failed UART, so parallelism alone was not the cause.
All 547 checks passed with tiered compilation disabled before the fixture change.
Raw failures and that controlled experiment are retained; the precise runtime
allocation source is not established. Production active IPF/HDF measurements
separately report zero steady-state allocations. These diagnostics do not weaken
or replace production retention requirements.

Synthetic coverage separates decoder and receiver behavior: non-word-aligned and
110,000-bit tracks, including complete eleven-sector recovery through receiver and
raw DMA at both lengths; index orientation; stored gaps; density profiles 3–9 and gap
boundaries; weak variation/reproducibility; no-flux clock recovery; sync crossing
index; DMA cancellation; running four-drive selection and phase retention.
Density expectations derive from SPS-format facts, not Legacy agreement. Supplied
native disks use density types 1/2, so native profiles 3–9 remain unavailable.

HDF coverage includes the nine ported controller tests, RDB checksums, large RDB
blocks, partition metadata, filesystem headers, Autoconfig/relocation, TD64,
malformed/cyclic lists and impossible block sizes, write protection, invalid
buffers/ranges, failed file replacement, reset, restart and guest QUICK/reply
execution. Native OFS and ADF evidence is detailed in [NATIVE_VALIDATION.md](../NATIVE_VALIDATION.md#storage-extension-2026-09-18).

| Supplied native IPF | Observed bounded progress | Remaining required evidence |
| --- | --- | --- |
| Full Contact, disks 1/2 | Disk 1 title/advertisement; explicit DF0 disk-two swap, Start Game, fight against Tong Lo, changing health and a lost round in an 18,000-field replay. | Bounded gameplay and disk-two transition verified; no claim of complete game coverage. |
| Shadow of the Beast US, disks 1/2 | Intro/music, explicit disk-two prompt, second-disk title and outdoor gameplay with crouch, attack and jump in a 19,000-field replay. | Bounded gameplay and disk-two transition verified. Earlier premature-swap corruption was an input error, not established receiver failure. |
| Operation Thunderbolt FR, disks 1/2 | PAL check, trace protection and audio startup pass after CIA/CPU/Paula corrections. Bounded disk-one gameplay shows enemies, firing, score 200 and the continue screen. | Disk-two transition. Disk 2 decodes but has no successful native handling evidence. |

The [six-image manifest](storage-native-media-2026-09-18.json) records archive and
IPF-entry hashes. Bounded [Full Contact](../../CopperScreen.Lightweight.Tests/Workloads/ipf-fullcontact-storage.json)
and [Beast](../../CopperScreen.Lightweight.Tests/Workloads/ipf-beast-storage.json)
scripts preserve the observed input/swap sequence using ignored local media aliases.
They are bounded native reproductions, not performance golden workloads. Full Contact
has a completed fight replay; Beast responds to outdoor game controls, and Thunderbolt
now has a [bounded gameplay script](../../CopperScreen.Lightweight.Tests/Workloads/ipf-thunderbolt-storage.json).
Thunderbolt second-disk handling remains unverified. No title-specific
engine behavior has been added.

The ROM-free [trace probe](../../scripts/probes/Copper68kTrace/Program.cs) originally
reproduced the failure in `1.4.1-boundary.1`: set vector 9, enable T, execute NOP;
D0 remained 1 and SP unchanged. The exact development pin is now `1.4.1-trace.1`.
The fixed source lives in CopperMod and the unpublished package is supplied through
an ignored local feed, with no sibling project reference. Scalar and batched
execution both pass (D0=42 and six-byte exception frame). Run
`dotnet run --project scripts/probes/Copper68kTrace -c Release`.
See [CPU_TRACE.md](CPU_TRACE.md) for source provenance and clean-machine restore
requirements. No package publication is included.

## Independent evidence and limits

- [SPS IPF format description v1.6](https://www.kryoflux.com/download/ipf_documentation_v1.6.pdf)
  and [CAPS API documentation](https://info-coach.fr/atari/software/_fd_image/CAPSLib102a-40.pdf):
  exact track bits/index, density, weak/noise flags and seed/update distinctions.
  Decoder verification does not establish receiver electronics or native loading.
- [SPS density implementation](https://github.com/rsn8887/capsimg/blob/master/CAPSImg/CapsImageStd.cpp):
  numeric density-profile facts used to construct independent managed metadata;
  no SPS implementation code is imported. The managed representation keeps exact
  bit boundaries, rather than byte-rounded density intervals.
- [Keller/Commodore US4780844A](https://patents.google.com/patent/US4780844A/en),
  especially Tables I/II: causal separator transfer law. [Paula die analysis](https://www.techtravels.org/2026/08/inside-paulas-floppy-controller-part-i/)
  supplies hardware-derived structural evidence, with unresolved circuit details;
  neither source certifies every selected bound/phase of this implementation.
- [Commodore CIA chapter](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/hard_f.html):
  TOD read latching. ALARM-selected high-byte latch inhibition is supplementary
  hardware-derived behavior recorded in [WinUAE CIA research](https://github.com/tonioni/WinUAE/blob/master/cia.cpp),
  not an explicit HRM statement. The new regression retains existing ADF/native fingerprints.
- [Commodore expansion/boot documentation](https://www.theflatnet.de/pub/cbm/amiga/AmigaDevDocs/lib_32.html)
  and [Exec I/O definitions](https://amigadev.elowar.com/read/ADCD_2.1/Includes_and_Autodocs_2._guide/node005B.html):
  guest boot structures, completion and error semantics.
- [Motorola M68000 programmer reference](https://www.nxp.com/docs/en/reference-manual/M68000PRM.pdf):
  trace mode/vector 9. The missing exception is a CPU architectural defect;
  matching Legacy would not close it.

Remaining OCS register, physical disk/serial/CIA phases, interlace and keyboard
gaps stay in [ISSUES.md](ISSUES.md). Storage progress does not close them.
