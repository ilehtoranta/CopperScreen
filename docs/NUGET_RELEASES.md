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
