[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = (Resolve-Path -LiteralPath $PackagePath).Path
# The development dependency is staged explicitly; no sibling source references or publication.
$expectedHash = '3400321124A10E12FE44359D4EAC0F9CFC91816CB7C8087EA8BEEAD7BDD74D52'
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'This is not the pinned CopperStart.FileSystem.Service 0.4.0-dev.4 artifact. Update the dependency and restore lock hashes deliberately when changing payloads.'
}
$feed = Join-Path $root 'artifacts/filesystem-service-feed'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
Copy-Item -LiteralPath $package -Destination (Join-Path $feed 'CopperStart.FileSystem.Service.0.4.0-dev.4.nupkg') -Force
Write-Output "Staged the verified filesystem service package in $feed"
