# Amiga utility test collection

Collected 2026-10-27; latest testing 2026-11-01. Collection ID: `amiga-utilities-v1`.

The [final sweep](UTILITY_FINAL_SWEEP_2026-10-31.json) records bounded main-screen
or basic-invocation checks for the original twelve utilities under both ROMs.
Its exact ROM, GUI and machine inputs define the result; older exploratory
results below retain their recorded inputs and scope. The thirteenth utility,
[MUI Calc](UTILITY_MUI_CALC_2026-10-31.md), is installed with CopperOS's native
MUI library in private images. Its explicit classic profile opens under both
ROMs and [native built-ins construct calculator controls](UTILITY_MUI_BUILTINS_2026-10-31.md).
The [classic NewMenu fix](UTILITY_MUI_NEWMENU_2026-10-31.md) now allows original
Application/menu construction under both ROMs. Separate
[5 MiB native-window diagnostics](UTILITY_MUI_NATIVE_WINDOW_2026-11-01.md)
verify the original client's physical mouse `5 Enter 6 Enter + = 11` on
CopperStart and native Kickstart 3.1. The [Layers default-background
fix](UTILITY_MUI_BACKFILL_FIX_2026-11-01.md) now passes both full-client retests:
native close unlinks the OS window and releases all MUI objects/classes;
Kickstart returns to CLI and CopperStart clears the closed rectangle to its
background pen. Menus, paging, keyboard, full MUI API and full Workbench desktop
coverage remain unqualified. The retained 3 MiB CopperStart library-load failure
remains FAIL. See the [reviewed receipt](UTILITY_MUI_BACKFILL_FIX_2026-11-01.json).

Four [small MUI utilities added on 2026-11-01](UTILITY_MUI_SMALL_TOOLS_2026-11-01.md)
are MUImem 1.1, MUIJoin 1.0, MUIMenu 1.00 and MUI Icon Update 1.2. Their
complete archives/readmes and all member CRCs are verified. All four have now
been [tested on CopperStart and Kickstart 3.1](UTILITY_MUI_SMALL_TOOLS_AB_2026-11-01.md)
with CopperOS MUI. After the [compatibility fixes and retests](UTILITY_MUI_SMALL_TOOLS_FIX_2026-11-01.md),
all four pass their exercised functional workflows on both ROMs. Per-build
identities and retained failures are recorded separately; this is not full MUI
conformance, exhaustive utility coverage or performance acceptance.

This is a local collection for manual emulation checks and CopperStart versus
native Kickstart comparisons: **17 utilities, 24 original downloads, four ADFs
available to mount**. Downloads and archive integrity were checked. The
[first native Kickstart 1.3 exploration](UTILITY_VALIDATION_2026-10-27.md)
records the four ADFs' initial checks. The subsequent
[rebuilt CopperStart / Kickstart comparison](UTILITY_AB_2026-10-28.md) completes
both sides: Test Kit's selected audio controls work under both ROMs, while
ProTracker, X-Copy and SysInfo expose CopperStart startup differences. The size
blocker was resolved for that development ROM. Later checks are recorded in the
final sweep linked above. These are bounded observations, not full compatibility
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
| [PPMore 1.8a](https://aminet.net/package/text/show/PPMore18a) | Original archive plus a controlled local installation with ReqTools 38.385's 1.3 build and pinned PowerPacker library; [startup evidence](UTILITY_PPMORE_STARTUP_2026-10-30.json) | View fixed ASCII text, page/search, resize, open/cancel requester and quit. Record each verified scope separately. |

The four ADFs come directly from the author/authorised archive, or are extracted
unchanged from its ZIP. They are not rebuilt boot disks. Their bundled startup
files and dependencies are part of their identities. To isolate an application
from its boot environment later, make a separately identified installation of
its executable and dependencies on legally supplied guest system media.

PPMore's controlled installation uses ReqTools 38.385 from Fish 794's `libs13`
directory and PPShow's bundled PowerPacker library. PPShow's ReqTools build
requires 2.0+. The fixed ASCII reader and footer now reach their main screen
under both ROMs using the pinned native guest GUI and Shell prerequisites in
the explicit 2 MiB diagnostic profile. The original 1 MiB lane still fails;
the [main-screen receipt](UTILITY_PPMORE_MAIN_2026-10-30.json) preserves that
scope. The [startup receipt](UTILITY_PPMORE_STARTUP_2026-10-30.json) retains
the earlier empty recovery-CLI command-name failure; the controlled guest Shell
publishes a valid name. Navigation, requesters and packed files remain unverified.

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
| [MUI Calc 1.25](https://aminet.net/package/util/wb/MUI_Calc125) | MUI 2.0; retain all seven package files. CopperOS classic-client 68000 MUI profile and rebuilt CopperStart GUI provider. Separate 5 MiB diagnostic; retained 3 MiB load failure remains FAIL. See [current evidence](UTILITY_MUI_BACKFILL_FIX_2026-11-01.md). | PASS on both ROMs: physical mouse `5 ENTER 6 ENTER +` visibly produces 11; native window unlinking and empty MUI registries after settling. CopperStart restores the bare-screen background with zero stale pixels. Menus, paging, keyboard and complete MUI/Workbench coverage remain unqualified. |
| [MUImem 1.1](https://aminet.net/package/util/wb/MUImem) | MUI binary requires version 10 (requester says V12+); Radio.mui, graphics/icon/mathffp; Retina disabled. | PASS on both ROMs: labels/maximum counters, original helper and stack checks, close and full cleanup. Retina and exhaustive Radio modes remain unqualified. See [fixed A/B evidence](UTILITY_MUI_SMALL_TOOLS_FIX_2026-11-01.md). |
| [MUIJoin 1.0](https://aminet.net/package/util/wb/MUIJoin10) | OS 2.0+, MUI 2.3+; binary requires MUI major version 8; ASL. | PASS on both ROMs: preferences preservation, actual Split/rejoin file bytes and full cleanup. Periodic fixture limits remain; exhaustive ordering is unqualified. See [fixed A/B evidence](UTILITY_MUI_SMALL_TOOLS_FIX_2026-11-01.md). |
| [MUIMenu 1.00](https://aminet.net/package/util/wb/MUIMenu) | WB 2.0+, MUI 2.0+/classes, Popasl editor and classic OM_ADDMEMBER. | PASS on both ROMs: exactly one actual command execution, quit and full cleanup. Exhaustive editor/options coverage remains unqualified. See [fixed A/B evidence](UTILITY_MUI_SMALL_TOOLS_FIX_2026-11-01.md). |
| [MUI Icon Update 1.2](https://aminet.net/package/util/wb/MUI_IconUpd12) | MUI 2.0, Popasl.mui, icon.library; ARexx optional. | PASS on both ROMs: actual source image/type copied, target position/stack retained, source unchanged and full cleanup. CopperStart uses a retained guest classic-icon writer; Workbench refresh, newer formats and ARexx remain unqualified. See [fixed A/B evidence](UTILITY_MUI_SMALL_TOOLS_FIX_2026-11-01.md). |
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
