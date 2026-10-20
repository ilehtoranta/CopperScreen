[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Import-Module (Join-Path $PSScriptRoot 'CopperScreen.Artifacts.psm1') -Force
$output = Join-Path $repository ('artifacts/engine-pack-' + [Guid]::NewGuid().ToString('N'))
$feed = Join-Path $output 'feed'
$null = New-Item -ItemType Directory -Path $feed
& dotnet pack "$repository/CopperDisk/CopperDisk.csproj" -c Release --artifacts-path "$output/disk-build" "-p:NuGetLockFilePath=$output/disk.packages.lock.json" -o $feed
if ($LASTEXITCODE -ne 0) { throw 'Disk package failed' }
& dotnet pack "$repository/CopperMod.Amiga.Lightweight/CopperMod.Amiga.Lightweight.csproj" -c Release --artifacts-path "$output/engine-build" -p:UseEmulatorPackageDependencies=true -p:LightweightDiagnostics=false "-p:RestoreAdditionalProjectSources=$feed" "-p:NuGetLockFilePath=$output/engine.packages.lock.json" -o $feed
if ($LASTEXITCODE -ne 0) { throw 'Engine package failed' }
Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' | Get-FileHash -Algorithm SHA256
Write-Host "Local development packages: $feed"
$compression = Compress-CopperScreenCompletedArtifacts -RepositoryRoot $repository -Path $output
if ($compression.Status -eq 'SKIPPED') { Write-Warning "Completed package output was not compressed: $($compression.Reason)" }
# No publishing and no changes to the production/diagnostic output or package locks.
