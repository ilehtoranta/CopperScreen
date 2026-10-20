# WHDLoad Hired Guns shared folder replay

This probe uses native Kickstart 3.1 and Workbench 3.1, WHDLoad and the original
Hired Guns slave. It exercises campaign gameplay, a named hard-disk save, F10
exit and a fresh-process reload through the existing shared-folder handler.
ROMs, Workbench, game files and generated fixtures stay outside Git.

## Prepare supplied inputs

Use Python with `lhafile` installed. Supply the Workbench 3.1 Workbench disk,
the five Hired Guns v1.08.39.25 DOS disks in order, and the official packages:

- [WHDLoad user package](https://www.whdload.de/whdload/WHDLoad_usr_small.lha)
- [Hired Guns installer and slave](https://www.whdload.de/games/HiredGuns.html)

The game itself comes from the supplied disks. The fixture script uses the
existing OFS/FFS extractor and the installer's file selections; it does not patch
game code. Its output directory must not already exist.

```powershell
python scripts/probes/WhdLoad/prepare_hiredguns.py `
  --workbench $Workbench31 --disk $Disk1 --disk $Disk2 --disk $Disk3 `
  --disk $Disk4 --disk $Disk5 --whdload $WhdloadLha --installer $HiredGunsLha `
  --output artifacts/whdload-fixture
```

The resulting `system` directory contains the bootable shared volume. The
generated startup script uses `SavePath=SYS:Saves SaveDir=HiredGuns` so WHDLoad
can create `SavedGames/TrackNN` beneath its save directory. Without SavePath,
the old slave's deferred writes fail if its `SavedGames` drawer is missing.
WHDLoad normally caches writes until exit: the game's success screen alone does
not establish persistence. Keep F10 exit and fresh-process reload in the replay.

## Machine and launch

Build the production solution in Release. If another process holds its output
files, use a separate production artifacts directory and pass that runner DLL.
Keep engine diagnostic test outputs separate.

```powershell
dotnet build CopperScreen.slnx -c Release
$Runner = 'CopperMod.Amiga.Lightweight.Runner/bin/Release/net10.0/CopperMod.Amiga.Lightweight.Runner.dll'
dotnet $Runner --rom $Kickstart31 `
  --host-dir-rw DH0 Workbench artifacts/whdload-fixture/system `
  --host-dir-boot-priority DH0 0 --cpu 68010 --agnus 8375-318069-10 `
  --denise 8362 --chip-ram-kib 2048 --slow-ram-kib 0 --fast-ram-kib 8192 `
  --frames 18454 --input-script scripts/probes/WhdLoad/hiredguns-campaign.json `
  --boot-probe artifacts/whdload-campaign --boot-probe-interval 250
```

Use a fresh fixture for the campaign script. It selects a four-character party,
enters the Abandoned Depot, turns and steps the first character, saves slot 1 as
`COPPER`, then exits with F10. The native startup script writes `returned.txt`
only after WHDLoad returns without a warning. Inspect `whdload-output.txt`,
the files under `Saves/HiredGuns/SavedGames`, and the gameplay captures.

These are functional probes. Their capture-inclusive throughput is not a
performance acceptance result. A missing supplied input is unavailable coverage.

## Reopen in a fresh process

After the campaign process exits successfully, require `returned.txt`, an empty
`whdload-output.txt` and the two nonempty save files `Track00` and `Track01`.
Record their hashes, remove the generated return marker, then run:

```powershell
Remove-Item -LiteralPath artifacts/whdload-fixture/system/returned.txt
dotnet $Runner --rom $Kickstart31 `
  --host-dir-rw DH0 Workbench artifacts/whdload-fixture/system `
  --host-dir-boot-priority DH0 0 --cpu 68010 --agnus 8375-318069-10 `
  --denise 8362 --chip-ram-kib 2048 --slow-ram-kib 0 --fast-ram-kib 8192 `
  --frames 12436 --input-script scripts/probes/WhdLoad/hiredguns-reopen.json `
  --boot-probe artifacts/whdload-reopen --boot-probe-interval 250
```

The input selects Continue, Hard Disk and slot 1, waits for loading, resumes
gameplay and quits with F10. Inspect the `COPPER` slot, the game's load-success
screen, the restored character view and the final Workbench. Require a new
successful return marker, an empty WHDLoad error log and unchanged save hashes.
