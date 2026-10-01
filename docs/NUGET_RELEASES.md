# CopperDisk releases

CopperDisk is released through `.github/workflows/copperdisk-publish.yml` using
[NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
The workflow exchanges its GitHub OIDC identity for a short-lived NuGet credential.
No persistent NuGet API key is needed in repository secrets or local configuration.

## Publisher policy

The NuGet account `ilehtoranta` owns CopperDisk. Configure its trusted publisher
with these values:

| Field | Value |
| --- | --- |
| Policy name | `CopperDisk` |
| Package owner | `ilehtoranta` |
| Repository owner | `ilehtoranta` |
| Repository | `CopperScreen` |
| Workflow file | `copperdisk-publish.yml` |
| Environment | `nuget-copperdisk` |
| Package pattern | `CopperDisk` |
| Scope | Push new versions of existing packages |

The GitHub environment `nuget-copperdisk` permits deployments from `main` only.
The workflow also rejects other branches and requires a stable version that
matches `CopperDisk/CopperDisk.csproj`. Only the publishing job can request an
OIDC token; its package comes from the preceding build and validation job.

## Release procedure

1. Set the intended package version, release notes and packaged README in
   `CopperDisk/CopperDisk.csproj` and `CopperDisk/README.md`. Update the engine's
   CopperDisk package pin and affected lock files.
2. Build `CopperScreen.slnx` in Release, run the disk tests, and validate a
   packed-package consumer. Commit and push the release source to `main`.
3. Run the **CopperDisk release** workflow with the source version and
   `publish=false` to check the hosted build and inspect its package artifacts.
4. Run the same workflow at the reviewed source commit with `publish=true`.
   It builds the production solution, runs disk tests, performs SDK package
   validation, checks package identity and repository commit, and publishes
   the `.nupkg` and `.snupkg` using trusted publishing.
5. Verify the new version on NuGet.org, then update the website's installation
   command and published API details. Tag the release source with
   `copperdisk-v<version>`.

Published versions are immutable. Existing preview packages remain available.
The workflow publishes CopperDisk only; it does not release the engine or CPU.
Engine diagnostics retain their separate outputs and CI job. Native ROM/media
replays and performance measurements are separate from package validation.

## CopperDisk 3.0.0 — 2026-10-02

[CopperDisk 3.0.0](https://www.nuget.org/packages/CopperDisk/3.0.0) was published
with symbols through the restricted trusted publisher policy above. Release
source: `2e6f6d0bbf54b8882c1ce1f77ab7f278ff0e6dd7`, tagged `copperdisk-v3.0.0`.
The [publishing run](https://github.com/ilehtoranta/CopperScreen/actions/runs/36926993962)
completed successfully.

The production Release build and all 74 disk tests passed with no skipped disk
tests. SDK package validation against `2.1.1-boundary.1`, the hosted validation
run, and repository CI passed. A separate .NET 10 consumer restored `3.0.0`
from NuGet.org, compiled the documented track/IPF APIs, and verified ADF byte
writes, the updated sector view and wrapped encoded-track reads. The downloaded
assembly matched the validated hosted package, and its repository metadata
identified the release source commit.

The major version retains the documented API changes from stable `2.1.0`;
no disk decoder or emulated hardware behavior was changed for this release.
