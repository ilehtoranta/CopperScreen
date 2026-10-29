# Amiga utility test collection

Collected 2026-10-27; latest testing 2026-10-29. Collection ID: `amiga-utilities-v1`.

The [sequential current-source checks](UTILITY_SEQUENTIAL_2026-10-29.md) verify
ProTracker's main editor and selected inputs under both ROMs. The remaining
utilities are pending in that sequence; older exploratory results below retain
their recorded inputs and scope.

This is a local collection for manual emulation checks and CopperStart versus
native Kickstart comparisons: **12 utilities, 14 original downloads, four ADFs
available to mount**. Downloads and archive integrity were checked. The
[first native Kickstart 1.3 exploration](UTILITY_VALIDATION_2026-10-27.md)
records the four ADFs' initial checks. The subsequent
[rebuilt CopperStart / Kickstart comparison](UTILITY_AB_2026-10-28.md) completes
both sides: Test Kit's selected audio controls work under both ROMs, while
ProTracker, X-Copy and SysInfo expose CopperStart startup differences. The size
blocker is resolved for this development ROM. The remaining eight utilities
remain **NOT RUN**. These are bounded observations, not full compatibility
qualifications or adoption of a new app firmware pin.

The [manifest](../scripts/qualification/amiga-utilities-v1.json) records exact
versions, source URLs, sizes, SHA-256 hashes, rights summaries, prerequisites and
suggested scenarios. Original distributions and extracted images live under
Git-ignored `media/utilities/`. Keep this collection separate from app packages,
ROMs and existing frozen regression/performance workloads.

## Get or verify the collection

From the repository root:

```powershell
./scripts/get-utility-test-media.ps1
./scripts/get-utility-test-media.ps1 -VerifyOnly

# A smaller first batch:
./scripts/get-utility-test-media.ps1 -Id protracker-23f,xcopy-521,amiga-test-kit,sysinfo-44
```

The script preserves original archives and extracts only the two identified
ZIP members into `ready/`. It checks every original and extracted ADF against
the manifest. Existing mismatched files cause a failure; changed upstream files
do not silently replace pinned inputs. `-VerifyOnly` checks local files without
downloading. `-OutputDirectory` can select another private local directory; keep
that directory out of version control. Neither mode runs guest software or
downloads a ROM, Workbench, or additional guest libraries.

The checked hashes identify the bytes acquired for this collection, not a
publisher signature or an emulator compatibility certification. ProTracker's
author updates 2.3F without changing the version name; its build date and hashes
must stay part of the identity.

## First A500 candidates

Start with the existing qualified A500 CopperStart configuration in
[COPPERSTART_APP.md](COPPERSTART_APP.md): PAL, 68000 interpreter, OCS,
512 KiB Chip + 512 KiB slow, no Fast RAM or RTG, one protected DF0. Use the same
hardware configuration for the native Kickstart 1.3 reference. These are
**candidates for that configuration**, not newly qualified workloads.

| Utility and source | Local disk or prerequisite | First useful scenario |
| --- | --- | --- |
| [ProTracker 2.3F, 2026-05-09](https://16-bits.org/other.php) | `originals/PT23F.ADF`; original LHA also retained | Build a small four-channel pattern using an original/generated sample; play/stop, edit loop points, open disk operations. Listen for channel order, looping and timing errors. |
| [X-Copy 5.21 English, 1991-11-17](https://jope.fi/xcopy/masterdisk.html) | `ready/xcopy_v5.21_en_1991-11-17_c911/xcopy_v5.21_en_1991-11-17_c911.adf` | Boot, then check a generated standard DD disk: track seeks, both sides, sector/error reporting, cancel/restart. |
| [Amiga Test Kit 1.21](https://github.com/keirf/amiga-stuff/releases/tag/testkit-v1.21) | `ready/AmigaTestKit-1.21/AmigaTestKit.adf`; executable also in original ZIP | Video cards, audio channels, memory, keyboard, mouse/joystick and CIA tests; record each subtest separately. |
| [SysInfo 4.4](https://sysinfo.d0.se/) | `originals/sysinfo_4.4.adf`; original LHA also retained | Enumerate chipset/CPU, memory, libraries and devices; scroll, run speed test, scroll again and quit/relaunch. |
| [PPMore 1.8a](https://aminet.net/package/text/show/PPMore18a) | Archive only; needs `reqtools.library` and `powerpacker.library`, absent from this archive | View fixed ASCII text, page/search, resize, open/cancel requester and quit. Pin compatible libraries before testing. |

The four ADFs come directly from the author/authorised archive, or are extracted
unchanged from its ZIP. They are not rebuilt boot disks. Their bundled startup
files and dependencies are part of their identities. To isolate an application
from its boot environment later, make a separately identified installation of
its executable and dependencies on legally supplied guest system media.

PPMore's older release is a prerequisite-investigation candidate. The collected
PPShow archive supplies versions of its two libraries, but compatibility with
PPMore 1.8a is not established. A missing library is unavailable coverage, not
an emulator failure.

X-Copy is especially useful for disk DMA, MFM, sync, drive selection and CIA
behaviour. Begin with read/check operations and standard DD media. Its disk also
contains Cyclone, which relies on external hardware: those functions are outside
this first scenario. Protected/nonstandard media and copy/format operations need
their own admitted storage configuration and disposable targets. The qualified
A500 CopperStart profile has one protected DF0; this collection does not expand
that write or drive contract.

## Expanded-machine and OS compatibility candidates

These inputs are worth keeping, but their prerequisites must be met and recorded
before interpreting results. Do not enlarge the A500 CopperStart profile to make
an application fit. Select a separately supported/qualified machine and firmware
pair, or mark the scenario **UNAVAILABLE: prerequisite**. A native A500+ OS 2.x
run alone does not qualify a corresponding CopperStart configuration.

| Utility and source | Requirements from the distribution | Suggested scenario |
| --- | --- | --- |
| [Personal Paint 7.1c](https://www.amigaforever.com/classic/download/) | Manual section 1.2 requires at least 2 MiB RAM; prefers 1 MiB Chip. Kickstart 1.2+ and Workbench 1.3 libraries. Beyond the qualified 1 MiB A500 profile. | Draw/fill, brush copy/rotate, palette edits, undo, file requester, original ILBM save/reload and cold-boot readback. Record hardware/software blitter settings. |
| [PPShow 4.0](https://aminet.net/package/gfx/show/PPShow40) | Kickstart and Workbench 2.0+; ReqTools and PowerPacker libraries included. DataTypes optional. | Original ILBM display, palette cycling, small generated ANIM5, cancel requester and return to desktop. |
| [DiskMaster 2.5.30a V37](https://aminet.net/package/util/dir/DiskMasterV37) | Readme says Kickstart V37+; Aminet architecture field says OS 2.1+. Resolve library requirements before selecting an OS 2.04 reference; ReqTools required. | Dual directory listing, sorting, create/rename/copy/delete scratch files and cancel requesters; verify file bytes after reboot. |
| [MuchMore 4.6](https://aminet.net/package/text/show/muchmore46) | AmigaOS 2.04+; plain text needs no optional XPK installation. Its bundled WBInfo helper requires 3.0+. | Long text, soft scrolling, fonts/styles, search, overlapping window redraw and quit/reopen. |
| [MemCheck 1.2 / 37.13](https://aminet.net/package/util/moni/MemCheck12) | Kickstart 2.04+ | Allocated-memory test, tested address ranges and error counts. Segment test writes across segments; use a fresh guest and reset afterwards. |
| [LhA 2.15](https://aminet.net/package/util/arc/lha) | Manual requires Kickstart 2.0+, at least 512 KiB RAM; 1 MiB recommended. Use `lha_68k` on 68000. | List/test/extract a known archive; archive original scratch files, extract on a fresh boot and compare bytes, names and protection bits. |
| [DeluxePaint V 5.0b9 demo](https://aminet.net/package/gfx/edit/DPaintV) | OS/RAM prerequisites still need verification. Readme specifies floppy use, root `Libs` and required fonts; do not assume an HDF installation works. Includes non-AGA low-res mode. | Drawing, fills, brushes/palette, artwork loading, menus/requesters. Save, Print, Delete and Copy are disabled by design. |

Archives remain in `originals/`. LHA files are distribution archives, not
mountable floppy images. `lha.run` is an **Amiga** self-extracting executable;
do not run it as a host command. Install archives only into a disposable guest
workspace with the required libraries and startup layout. Keep originals intact.

## What “free” means here

This collection includes unrestricted software, non-commercial personal-use
downloads and a commercial product's free demo. Acquisition does not grant
permission to bundle everything with CopperScreen.

- **Amiga Test Kit:** upstream public-domain / [Unlicense](https://github.com/keirf/amiga-stuff/blob/testkit-v1.21/COPYING).
- **ProTracker:** author-provided free download; this binary archive has no
  explicit redistribution licence. Do not apply the desktop `pt2-clone` licence
  to this different Amiga program.
- **X-Copy:** the archive's [licence](https://jope.fi/xcopy/xcopy_license.txt)
  permits private non-commercial use, excludes commercial/government use, and
  restricts distribution and paid bundles.
- **Personal Paint:** the included `PPaint.readme` permits private,
  non-commercial, non-profit use. Republication or distribution needs prior
  written permission from Cloanto or A-EON. Fetch from the official source.
- **SysInfo:** donation-supported, with no-profit distribution conditions;
  preserve the program, icons and documentation. See its included guide and
  [copyright page](https://sysinfo.d0.se/guide/copyrights).
- **PPMore, PPShow and MuchMore:** non-commercial freeware with additional
  distribution conditions in their manuals.
- **DiskMaster:** preserve the complete package; no-profit and PD-collection
  restrictions apply. **MemCheck:** commercial bundling requires permission.
- **LhA:** included manual releases the full version free, without registration.
- **DeluxePaint V demo:** the EA readme explicitly permits free, non-commercial
  distribution of this demo. Its functional limits are expected behaviour.

Full Deluxe Paint II/III/IV remain useful **user-supplied, licensed-copy**
candidates for classic GUI/blitter testing; they are not included here. The
[Computer History Museum release](https://computerhistory.org/blogs/deluxepaint-computer-history-museum-software-license-agreement/)
is DeluxePaint source under a personal, non-commercial licence with distribution
restrictions; it does not establish free redistribution of later retail disks.
Likewise, choose an exact Ultimate Soundtracker release and establish its source
and terms before adding it. ProTracker provides the initial tracker workload.
Any additional songs, samples, artwork and fonts need their own provenance;
prefer small original/generated fixtures for reproducible checks.

## CopperStart / native Kickstart A/B procedure

1. Freeze app/engine build, machine preset, CPU backend, video timing, memory,
   drives, write settings and input sequence. Record both ROM hashes and the
   CopperStart qualification ID. Change only the ROM between the two runs where
   the qualified contracts permit that pairing. Boot disks, system files,
   dependency libraries and application binaries must have recorded hashes too.
2. Start both runs cold with identical initial media. Use independent disposable
   copies for any admitted writes. Do not let one run's saved preferences or
   disk changes become the next run's starting state. Record both the original
   distribution hash and any installed/constructed disk hash.
3. Record boot, application entry, first screen, input response, one useful
   operation, requesters, quit/relaunch and restart. Use visible/semantic
   milestones as well as emulated field counts: boot progress can differ across
   ROMs. Missing media/libraries, deliberate demo limits and unmet hardware
   prerequisites need explicit outcomes.
4. Compare pictures/bitplanes and palettes for the same drawing, PCM/channel
   content for the same tracker pattern, and file bytes/metadata after a fresh
   protected reboot. Cross-ROM RAM addresses, whole-machine fingerprints, GUI
   text and startup timing are not expected to be identical. Keep exact replay
   goldens separate for each ROM/build.
5. Save evidence under ignored `artifacts/utility-ab/<run-id>/`: input manifest,
   scenario/actions, per-ROM outcomes, captures/audio, logs and before/after
   media hashes. Distinguish **PASS**, **FAIL**, **UNAVAILABLE: prerequisite** and
   **NOT RUN**, including individual operations within a utility.

A native-ROM pass and CopperStart failure is evidence of a compatibility
difference under that emulator configuration; it does not by itself identify a
chipset bug or prove hardware correctness. To check emulated hardware, repeat
the same native-ROM scenario on a reference emulator or real Amiga. Hardware
takeover tools such as Test Kit and X-Copy can give strong hardware coverage
while exercising little of DOS/Intuition after launch. File managers, viewers
and paint applications provide complementary OS coverage.

These scenarios measure correctness and compatibility. Guest benchmark scores
and short smoke runs are not host throughput acceptance; retain the separate
[performance protocols](engine/PERFORMANCE.md) and existing expected fingerprints.
