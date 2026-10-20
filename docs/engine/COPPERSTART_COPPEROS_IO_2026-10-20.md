# Native CopperOS directory and save I/O qualification — 2026-10-20

The development DOS producer now retains private directory positions, projects
already validated entry headers once, scans validated bitmap words and copies
complete internal sectors with aligned longwords. Public FIB ordinals, arbitrary
caller-buffer handling, synchronous writes and save/restore ordering are preserved.
The application and GUI payloads are unchanged. Shipping firmware/workspace pins
remain unchanged.

The [evidence JSON](COPPERSTART_COPPEROS_IO_2026-10-20.json) binds 2922 artifact
hashes and 27 current source hashes; SHA-256 `585DFB67C83F1E7A880A080F359ACA9F98AFFF00D71D5481810D56B1BF34BF70`.
See the [implementation and reproduction guide](../../../CopperStart/Docs/NativeCopperOsIo.md).

## Exact development pair

| Input | Identity |
| --- | --- |
| ROM | 524,288 bytes; `F10C954B79C11E295BA1D58F417821F087D84B5D7CAA87920D2B7414B6D54914` |
| GUI | 257,312 bytes; `5900735263A90AC53626E6103FD6B232329667AE03D78105BC74D88E2BC085FD`; TextEntry 247686 |
| Desktop | 41,500 bytes; `5368BFDE4DC393B5E1899A7344C78BCD8FE07FBB27B8862CFA583DDC40A8A258` |
| Bootstrap | 652 bytes; `E7321120811F127E7B83F440E7C9C34DABCCB1209A0FF595284BD6F960595A2A` |
| Machine | PAL OCS MC68000; 524,288 Chip + 524,288 Slow; zero Fast; one native DF0 |

ROM code is 467,340 bytes after its eight-byte header, leaving 24,172 bytes before
unchanged graphics at `0xFF8000`. The complete ROM checksum is `FFFFFFFF`.
Native reset passes with zero skips. The source baseline reproduces the previous
DOS producer with zero resolved-code differences. The overlay has 24 allowed
changed/added entries; all ten other ROM producer hashes remain fixed. Reprojecting
current live source reproduces all four qualified DOS overlay files byte-for-byte.
The retained bundle is `CopperStart/artifacts/copperos-io-20261020/bundle-v1`.

## Work removed and limits

A 96-entry directory scan plus end-of-directory request falls from 4,945 to 193
handler block requests (96.1% fewer), excluding initial Examine. These include
requests served by the track cache, not just physical disk transfers. Names,
order and public ordinal keys are preserved. A native DOS journey covers all 96
entries, all 96 paired FIB steps, insertion/deletion and replacement of the lock.
It completes in 207567 guest fields, including deliberately uncached interleaving.

Each private object grows from 48 to 56 bytes, including file objects. Directory
locks retain four private words (16 bytes); the temporary cursor also carries the
directory key (20 bytes). Public FileLock bytes 20–39 are unchanged. Writes invalidate
private traversal positions before touching media, even on failure; failed reads
publish no cursor advance. Interleaved FIBs and new locks rebuild from public keys.

The bounded producer counts 55 bitmap words instead of 1,758 individual bit checks.
Tail padding and reserved/root/bitmap validation remain checked. Internal complete
sector copies use longwords; general/public buffer copies retain byte semantics.
No device writes are batched or deferred. The interval from `empty-save` through
`capacity-save` changes from 66,292 to 65800 guest fields; that includes open,
UI and save work and is not isolated save latency. The interval from `save-as`
through `files-reopen` changes from 9,808 to 6572 fields. These observations do not
establish host throughput, installed response time or release acceptance.

## Native safety and regression coverage

The final `qualified-v4` default run passes all nine selected facts with zero
skips in one invocation. Its frozen Editor/Save samples match the original
32-byte Latin-1 SHA-256 before execution. The three cold-read lengths and hashes
also match the prior original fixture independently of the current expected arrays.
The directory probe retains all 96 pairs, phase/entry receipts, a 500,000-field
total bound and a 40,000-field no-progress bound.

Current filesystem semantics pass 69 checks; the frozen producer passes 18, both
with zero skips. Native regression retains source/destination race detection,
non-truncating replacement, short/partial/failed writes, five Close failures,
rename/restoration failures, read-only/full media, metadata, retained recovery
copies, startup rollback, refresh/input pressure and process restart. Three files
are byte-exact after native MFM export and a fresh protected ROM boot.

| Native journey | Guest fields | Free after every stop | Lowest free observed at Wait | Lifetimes |
| --- | ---: | ---: | ---: | ---: |
| Editor | 89670 | 456888 | 261848 | 11 |
| Save | 668976 | 450016 | 235896 | 11 |
| Recovery | 97651 | 450016 | 251848 | 11 |

The three journeys complete 33 lifetimes: four normal and seven startup failures
per journey. Recovery observes 50 busy messages, 3 busy refreshes, three
controlled checkpoint holds and one physical keyboard close, plus mouse close
under queue pressure. The holds preserve IRQ/scheduling and are fixture controls,
not application delays. Stricter idle waits and these holds prevent clean latency
comparison with the previous record. Save's cold boot adds 4505 fields.
Lowest free at Wait is not a continuous peak or largest-free-block measurement.

CopperStart Release passes with 24 existing dependency/compiler warnings and zero
errors; CopperScreen Release passes with zero warnings/errors. The frozen replay
build passes with zero warnings/errors. Initial ROM/probe compile failures, the old 100,000-field directory timeout and
the altered-fixture native runs remain retained. The earlier combined record
`4AE06EAD4B091424CFCED1C61B70BCE27EE506C32DDB8A7E86D26F2DE955EE1B`
verified expanded 43/47-byte cold payloads and was superseded after the encoding
audit; it is not original-fixture regression evidence. The two samples were restored
from the prior qualification, and the preflight/cold-hash guards now prevent that
silent fixture drift. The final default run is separate from these retained attempts.
No skipped test or superseded run is counted as final replay coverage.

## Remaining desktop work

First measure startup, repaint, directory and save latency and complete whole-machine
peak/largest-block records, including loader transients and fragmentation. Individual
synchronous DOS calls still block the Desktop task. The 32-entry Files cache, 16 KiB
Editor limit, 256-byte root, 32 KiB stack and 8 KiB cleanup reserve are unchanged.
The extra private object bytes must be included in future complete memory records.

Then add bounded Files paging and create/rename/delete flows, and qualify Shell/Demo
launch and return with input/display ownership restoration. The existing full Shell
requires extra RAM; a child must first fit this 1 MiB profile. Qualify 100-cycle and
sustained stress, then freeze/admit the ROM/OS pair, installed workspace/update and
itch.io ZIP journey. Functional native tests do not complete these release gates.
Recovery remains recoverable replacement, not power-loss-safe atomic storage.
