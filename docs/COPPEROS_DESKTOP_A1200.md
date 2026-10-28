# CopperOS desktop HDF on A1200

The **Amiga 1200 with CopperOS HDF** launcher boots the native CopperOS
Desktop directly. `S:Startup-Sequence` runs `C:Desktop`, which loads `L:CopperGUI`
and `L:CopperHome` and starts its own DOS Process, screen, window and input loop.
Home offers Files, Editor and About. No Shell proof bootstrap, typed startup
command, host OS service, vendor OS file or graphical host substitute is involved.

The PAL Alice/Lisa, 68EC020 interpreter, 2 MiB Chip RAM, no slow/Fast RAM profile
and integrated 1 MiB CopperStart ROM use the
[A1200 firmware contract](../CopperScreen/Firmware/CopperStartA1200.json).
No CPU or chipset execution changes are required. The
[native memory optimization](COPPEROS_DESKTOP_BOOT_SPEED_2026-10-25.md) and
[drawing optimization](COPPEROS_DESKTOP_GUI_SPEED_2026-10-25.md), followed by
[blitter fills and batched repainting](COPPEROS_DESKTOP_BLITTER_2026-10-25.md)
and [text DMA without repeated memory queries](COPPEROS_DESKTOP_TEXT_2026-10-25.md),
followed by [removal of repeated validation](COPPEROS_DESKTOP_REDRAW_2026-10-25.md)
the [Exec/graphics audit qualification](COPPEROS_GRAPHICS_AUDIT_2026-10-25.md),
the [original Kickstart call comparison](COPPEROS_GRAPHICS_BENCHMARK_2026-10-25.md)
the [graphics fast-call checkpoint](COPPEROS_GRAPHICS_FASTCALLS_2026-10-25.md)
[shared geometry/asynchronous fills](COPPEROS_GRAPHICS_GEOMETRY_2026-10-25.md)
[ownership/text setup improvements](COPPEROS_GRAPHICS_OWNERSHIP_2026-10-25.md)
[pixel fills/text loops](COPPEROS_GRAPHICS_PIXEL_2026-10-25.md)
[ownership reservations](COPPEROS_GRAPHICS_RESERVATION_2026-10-25.md)
[bitmap-copy setup](COPPEROS_GRAPHICS_COPY_2026-10-25.md)
[rectangle-fill setup](COPPEROS_GRAPHICS_FILL_2026-10-25.md)
[text staging](COPPEROS_GRAPHICS_TEXT_SETUP_2026-10-25.md)
[bitmap-copy DMA setup](COPPEROS_GRAPHICS_COPY_DMA_2026-10-25.md)
[short word fills](COPPEROS_GRAPHICS_WORD_FILL_2026-10-25.md)
[blitter release setup](COPPEROS_GRAPHICS_RELEASE_2026-10-26.md)
[shared text-plane setup](COPPEROS_GRAPHICS_TEXT_PLANES_2026-10-26.md)
[live bitmap-copy geometry](COPPEROS_GRAPHICS_COPY_ADMISSION_2026-10-26.md)
[single-row word copies](COPPEROS_GRAPHICS_COPY_WORDS_2026-10-26.md)
[packed text rows/default-glyph reuse](COPPEROS_GRAPHICS_TEXT_ROWS_2026-10-26.md)
[a permanent Layers dependency](COPPEROS_GRAPHICS_LAYER_PIN_2026-10-26.md)
[Layer admission/provider code generation](COPPEROS_GRAPHICS_LAYER_BRIDGE_2026-10-26.md)
[Exec/address setup](COPPEROS_GRAPHICS_EXEC_ADDRESS_2026-10-26.md)
[Layer result transport](COPPEROS_GRAPHICS_LAYER_RESULT_2026-10-26.md)
[native Layer admission](COPPEROS_GRAPHICS_LAYER_ADMISSION_2026-10-26.md)
[native empty-queue locks](COPPEROS_GRAPHICS_LAYER_LOCK_2026-10-26.md)
[bitmap-copy setup/submission](COPPEROS_GRAPHICS_COPY_SETUP_2026-10-26.md)
[bitmap-copy DMA channels](COPPEROS_GRAPHICS_COPY_CHANNELS_2026-10-26.md)
[ownership publication](COPPEROS_GRAPHICS_OWNERSHIP_PUBLISH_2026-10-26.md)
[bitmap-copy registers/row control](COPPEROS_GRAPHICS_COPY_REGISTERS_2026-10-26.md)
and [descending bitmap-copy DMA](COPPEROS_GRAPHICS_COPY_SHIFT_2026-10-26.md), followed by
[single-region text geometry](COPPEROS_GRAPHICS_TEXT_REGION_2026-10-26.md),
followed by [native Exec preemption](COPPEROS_EXEC_PREEMPTION_2026-10-26.md),
reduce startup from 54.9 to 12.16 guest seconds. The exact drawing interval is
about 42 ms, with about 86 ms from key-down to drawing return. The historical
Tab observation remains 0.16 guest seconds / eight PAL fields because that is
the input sequence's minimum observation window. Native profiling and normal
execution agree, and the framebuffer golden remains unchanged. General original
Kickstart performance parity remains active work.

## Image identity and reproduction

The [desktop media contract](../CopperScreen/Firmware/CopperOsA1200.json) pins
the 8 MiB raw DOS1 FFS image and its three HUNK inputs. The image SHA-256 is
`dc597fa79c5479d43e98abdedfcb885056f9c22affa161126ff1b27c39c3cfa7`.
Geometry remains `DH0:2:32:0:255` with 512-byte blocks.

The current desktop payloads are retained locally in
`../CopperStart/artifacts/exec-preemption/desktop-v2`; their frozen predecessor is in
`../CopperStart/artifacts/copperos-io-20261020/bundle-v1`. The earlier A500
qualification is [recorded separately](engine/COPPERSTART_COPPEROS_IO_2026-10-20.md).
Home batches each rectangle and uses the updated native GUI text entry. The historical Home
footer and About text still describe the A500 development profile; the actual fitted
machine and firmware are the A1200 configuration above.

Compose a fresh output directory, then stage it with the existing ROM:

```powershell
python scripts/build-copperos-desktop-image.py `
  --copperos-root ../CopperOS `
  --desktop-directory ../CopperStart/artifacts/exec-preemption/desktop-v2 `
  --output-directory artifacts/copperos-desktop-20261026
./scripts/prepare-copperos-bundle.ps1 `
  -CopperStartA1200Rom artifacts/copperos-bundle/CopperStart-A1200.rom `
  -CopperOsHdf artifacts/copperos-desktop-20261026/CopperOS.hdf
```

The recipe checks each HUNK's length and hash and the original producer script
hash before serialization. It requires the complete output image to reproduce
the media contract, verifies the boot checksum, and runs the producer's independent
full-tree, block-ownership and allocation-bitmap reader. The filesystem reader
normalizes only a private copy of the boot block; the emitted disk is unchanged.
The receipt records recipe/input identities and filesystem verification. Normal
build/publish/MSIX outputs embed the checked image. No binaries enter Git and no
producer package is published.

## Personal disks and upgrades

The desktop creates `CopperOS/A1200-Desktop/CopperOS.hdf` once in app data.
Reselection and updates reuse that writable image. The recognized drawing-system
update changes only the same-length native GUI, Home and launcher file data and keeps
a full `CopperOS.before-drawing-<id>.hdf` backup beside it. All other bytes and user
documents are preserved; custom components and read-only/active disks are left
intact. A missing existing desktop disk reports recovery guidance instead of reseeding it.

Saved app-created Shell proof configurations are recognized only by their exact
old workspace path and seed marker. Reopening or reselecting that launcher
configuration selects the separate desktop workspace. The old
`CopperOS/A1200/CopperOS.hdf`, its marker and any user edits are preserved.
Explicit custom disk selections and command-line profiles retain their paths and
write settings. This is not a filesystem upgrade of existing guest documents.

## Validation

`CopperOsDesktopBootTests.BundledA1200BootsNativeDesktop` runs without optional
ROM/media environment variables. It extracts the actual embedded bundle, creates
the normal A1200 session with empty floppy drives, requires the native desktop
message port/window and completed input wait, opens Editor through raw keyboard
events, resets to Home and cold reopens the writable HDF. PNG captures are saved
under the test output's `artifacts/desktop-boot-capture` directory.

The desktop app test `BundledCopperOsStartsWithoutFileSelectionAndReachesNativeDesktop`
selects the real launcher option, starts its runtime and requires the native
desktop port/window. Additional host cases check old workspace preservation,
reselection, cold app reopening, concurrent first use, ROM cache repair and missing
disk recovery. The old optional Shell proof replays remain separate historical
media coverage. Functional checks do not establish host throughput or installed
Store acquisition/update qualification.

On 2026-10-25 the focused host/launcher selection passed 11 tests with no skips,
including the full UI-runtime desktop boot. The bundled native boot/input/reset/
cold-reopen test passed with no skips. The production Release solution build
passed with zero warnings/errors. The independent image reader verified every
file, block owner, checksum and bitmap bit. The initial native test used an
incorrect expected blue RGB value in its final assertion; its captures showed
the desktop. That failed run remains separate from the corrected passing replay.
