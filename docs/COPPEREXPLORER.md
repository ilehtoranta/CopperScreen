# CopperExplorer shared backend

[CopperExplorer](https://github.com/ilehtoranta/CopperExplorer) is a private,
separately installed Windows Explorer product. CopperScreen owns the shared
`CopperDisk.FileSystem` 0.1.0-dev.6 sources; both products use this implementation
for image sessions, reversible Windows names, streaming and staged mutations.
The shared project references CopperDisk and the exact qualified
`CopperStart.FileSystem.Service` 0.5.0-dev.8 / Copper68k 1.5.12 packages. It has no
Avalonia, desktop executable or Lightweight dependency.

The service's ABI 4 discard gateway is opt-in for owned sparse stages. The guest
tracks and revalidates frees before each bounded discard; the shared transaction
maps those ranges to the selected partition before publication. Deletes and the
source half of moves reclaim eligible former file data in both products. DMS
recompresses reclaimed working sectors. Unsupported sparse backends retain the
ordinary save path. Neither the broker nor the desktop parses allocation maps.

`build/Build-CopperExplorerDependencies.ps1` builds local development nupkgs and
a generated test-fixture package in ignored artifacts. It copies the qualified
guest package after checking its SHA-256; it never compiles a replacement ROM.
Private publication is explicit (`-PublishPrivate`), requires package credentials
through the environment and refuses known non-private package IDs. Published
versions remain immutable. CopperExplorer also needs Actions access to these
packages for its locked restores.

CopperDisk 3.0.1-dev.2 adds the opt-in
`AmigaDosTrackDecoder.DecodeValidated` API. `ValidatedSectorImage.GetValidity`
distinguishes valid, missing, corrupt, conflicting and weak-dependent sectors;
its bounded `Read` rejects every accessed sector that is not valid. The existing
emulator-facing best-effort sector decoder retains its behavior. IPF filesystem
sessions use the strict view, stay read-only and fail explicitly when required
metadata or file data is unreadable. Unsupported protection/custom formats do not
become filesystem data merely because an IPF can be decoded into tracks.

See the [manager guide](DISK_IMAGE_MANAGER.md) for host-name restoration and
image operation behavior. Source names remain unchanged inside virtual views;
image-to-image transfers bypass the host-name encoding.

The installed Explorer milestone is **not accepted**. Local shared/backend and
native stream probes pass, but real Explorer navigation still fails. Trusted
signing inputs, Inno Setup 7.1.1, ARM64 tools and private-feed/CI qualification
remain outstanding. GitHub Actions refused to start the first workflows because
of account billing/spending limits. These gates are recorded in CopperExplorer's
`docs/QUALIFICATION.md`; headless checks do not replace installed Explorer tests.
