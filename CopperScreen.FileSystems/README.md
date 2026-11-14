# CopperScreen offline filesystem dependency

This host-only assembly bundles a frozen source snapshot of CopperFS for the
desktop disk setup UI. It has no engine reference, registers no guest service,
and is first used when a user formats a disk. New application installations
include it; users do not need a CopperFS checkout or a feature build flag.

`Upstream/CopperFS/snapshot.json` records the base repository commit, working-tree
status and SHA-256 of every imported file. CPX is currently uncommitted upstream
development, so its exact bytes are identified by those hashes, not by the base
commit alone. This is not a CopperFS package or format release. The UI names this
filesystem CopperNova (CNFS); it remains experimental and requires a compatible
guest handler. Its on-disk CPX4 format and upstream source names are unchanged.

The sources and license are copied byte-for-byte. Maintain filesystem changes
in CopperFS and refresh this consumer with:

```powershell
./scripts/update-copperfs-snapshot.ps1 -CopperFSRoot ../CopperFS
```

Build verification checks the imported files against `snapshot.props`; do not
patch them in this repository. The consumer compiles the upstream cores into
one internal application dependency and uses the pinned published CopperSharp
Amiga host support SDK, without a compiler checkout or native handler build.
The upstream namespaces remain unchanged. The CPX arithmetic helper is included
from its maintained prototype location. Guest handlers and native OS integration
are outside this assembly.

The portable formatters and their readers are reused here. Upstream write cores
remain in the snapshot because the SFS readers share validation and recovery
helpers with them; disk setup invokes only format and read-only verification.
The application does not publish these assemblies as standalone packages.
