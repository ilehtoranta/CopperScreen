[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = (Resolve-Path -LiteralPath $PackagePath).Path
# Stage the exact signed NuGet.org archive for an offline restore.
$expectedHash = '32FDC533BCB44359BEFE9CB6BABF4EB7F52EBE30A04DBEA97DCD39D21ADF197F'
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'This is not the pinned CopperStart.FileSystem.Service 0.5.0-dev.2 artifact. Update the dependency and restore lock hashes deliberately when changing payloads.'
}
$feed = Join-Path $root 'artifacts/filesystem-service-feed'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
Copy-Item -LiteralPath $package -Destination (Join-Path $feed 'CopperStart.FileSystem.Service.0.5.0-dev.2.nupkg') -Force
Write-Output "Staged the verified filesystem service package in $feed"
