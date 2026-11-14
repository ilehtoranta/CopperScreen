[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = (Resolve-Path -LiteralPath $PackagePath).Path
# The development dependency is staged explicitly; no sibling source references or publication.
$expectedHash = '366314B88B2E457CD657A7A2333A9F71B5E736A8B16D2B001850F8009A714AD1'
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'This is not the pinned CopperStart.FileSystem.Service 0.2.0-dev.2 artifact. Update the dependency and restore lock hashes deliberately when changing payloads.'
}
$feed = Join-Path $root 'artifacts/filesystem-service-feed'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
Copy-Item -LiteralPath $package -Destination (Join-Path $feed 'CopperStart.FileSystem.Service.0.2.0-dev.2.nupkg') -Force
Write-Output "Staged the verified filesystem service package in $feed"
