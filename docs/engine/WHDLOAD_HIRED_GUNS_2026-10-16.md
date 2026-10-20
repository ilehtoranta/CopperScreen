# WHDLoad Hired Guns shared folder validation

Validation date: 2026-10-16. Hired Guns passes bounded campaign play, save, F10
quit, process close, fresh-process load and resumed gameplay through the existing
shared-folder handler. The fixture needed a standard WHDLoad SavePath setting;
no engine correction was required. Native functional replay used isolated
Release binaries on Windows.

## Machine and inputs

The replay uses PAL A500 hardware with 68010 interpreter execution, 8375 Agnus
(318069-10), 8362 OCS Denise, 2 MiB Chip RAM, no slow RAM and 8 MiB Zorro II Fast
RAM. The boot volume is a writable `DH0:` shared folder at boot priority 0; no
floppy or hardfile is attached. The display preserves the full 908 by 313 raster.

Supplied inputs are A500 Kickstart 3.1 revision 40.63, the Workbench 3.1 revision
40.42 Workbench disk and all five Hired Guns v1.08.39.25 DOS disks. The official
WHDLoad user package is 20.1 build 7190, dated 2026-10-13. The unchanged
`HiredGuns.slave` is version 1.0, dated 2004-11-15, 1,112 bytes, SHA-256
`C5A0E482BABBE296B95D52B5DEBC5E7855AE8D97CB01568E2C5AC88B9CAA8216`.
This slave uses its own loader and does not need an additional KickEmu ROM.
The frozen runner consumes Copper68k `1.5.4-qualified-dev.1`.

The [fixture utility](../../scripts/probes/WhdLoad/prepare_hiredguns.py) reads
supplied disks with the existing OFS/FFS extractor and follows the official
installer's file selections. It refuses an existing output directory and records
input hashes. Its game payload matches the manually prepared exploratory
fixture's 183 data files. ROMs, media, downloaded packages, screenshots and RAM
dumps remain in ignored local artifacts.

## Save directory correction

The initial native session reached the title, training gameplay, campaign map
and Abandoned Depot. Mouse input turned and stepped the first character. The
game displayed `SAVED OK. NO ERRORS.` for slot 1, but WHDLoad's cached writes
failed during F10 exit:

```text
DOS-Error #205 (object not found)
on deleting/writing file "SavedGames/Track01"
on deleting/writing file "SavedGames/Track00"
```

The old slave expects a `SavedGames` drawer. WHDLoad creates missing save
subdirectories when its standard SavePath mode is enabled. The fixture now
creates the writable `SYS:Saves` base directory and launches:

```text
WHDLoad Slave=HiredGuns.slave Data=game SavePath=SYS:Saves SaveDir=HiredGuns Preload NoNetwork NoReq SplashDelay=0 QuitKey=89
```

The native startup script writes `returned.txt` after a successful WHDLoad
return, or `whdload-failed.txt` for a warning/error return that reaches the
conditional. Saves must reach the host after normal exit before being counted
as persistent. Training's help screen omits the save/load shortcuts, so the
persistence replay uses the full campaign.

This correction changes fixture preparation and launch options. No engine,
filesystem packet, CPU, ROM or game-code change is involved. See WHDLoad's
[SavePath and write-cache options](https://www.whdload.de/docs/en/opt.html) and
[SaveFile directory contract](https://www.whdload.de/docs/autodoc.html).

## Reproduction and evidence

Follow the [probe instructions](../../scripts/probes/WhdLoad/README.md).
The [campaign input](../../scripts/probes/WhdLoad/hiredguns-campaign.json)
contains 77 ordered input events and runs for 18,454 completed fields. The
exploratory session used a separate disposable fixture; the corrected campaign
starts in a fresh process with a clean save area.

Local evidence is under `artifacts/whdload-2026-10-16/`: `inputs.json`,
`binaries.json`, `build.log`, the failed exploratory `interactive` session and
`system/whdload-output.txt`, and the corrected `campaign` captures/log.
Release production outputs are isolated under `production` because other
running replays held the ordinary solution outputs open. The production
solution build succeeded with zero warnings and errors.

The corrected campaign completed all 18,454 fields with no unsupported-feature
stop. All 38 bounded snapshots have null engine/video unsupported flags; 28
contain nonzero audio. F10 restored native DOS and Workbench, the generated
success marker was present, and the WHDLoad error log was empty. Deferred saves
created `Saves/HiredGuns/SavedGames/Track00` (512 bytes, containing `HGUN3925` and
`COPPER`) and `Track01` (167,936 bytes). Their hashes are in the
[machine-readable record](WHDLOAD_HIRED_GUNS_2026-10-16.json).

After that process exited with code 0, a second runner process cold-booted the
same shared folder. Its load menu showed `COPPER` in slot 1 at field 5,000. The
game reported `LOADED OK. NO ERRORS.` at field 7,250 and resumed four-view
gameplay at field 10,500, with the first character facing the wall reached
before saving. F10 again returned to native DOS and Workbench. The second run
completed 12,436 fields, exited with code 0, produced a new success marker and
an empty error log, and left both save hashes unchanged. All 51 snapshots have
null engine/video unsupported flags; 19 contain nonzero audio.

| Native check | Result |
| --- | --- |
| Shared-folder boot and unchanged WHDLoad/slave launch | PASS |
| Campaign map, Abandoned Depot, mouse turns and step | PASS |
| Named hard-disk save and deferred write flush | PASS |
| F10 return to native DOS and Workbench | PASS in both processes |
| Fresh-process slot enumeration and game-validated load | PASS |
| Resumed gameplay and preserved save bytes | PASS |

The [reopen input](../../scripts/probes/WhdLoad/hiredguns-reopen.json) contains
12 ordered events. The corrected fixture utility was also rerun to a new
directory: its startup script and all game files match the replay fixture and
its save base starts empty. Python syntax, ordered input events and local
documentation links were checked. Changes are confined to the probe and
documentation; no emulator regression suite or performance acceptance is
claimed for this fixture-only correction.

This is a bounded native compatibility check for the stated game and profile.
Capture-inclusive execution is not a host-throughput acceptance measurement.
