[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = (Resolve-Path -LiteralPath $PackagePath).Path
# Stage the exact signed NuGet.org archive for an offline restore.
$expectedHash = 'ACCF8693852B9BD0FEE2BFCE49190BE70670021ABAFEA57EBC74FF130E5A8DF9'
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'This is not the pinned CopperStart.FileSystem.Service 0.5.0-dev.3 artifact. Update the dependency and restore lock hashes deliberately when changing payloads.'
}
$feed = Join-Path $root 'artifacts/filesystem-service-feed'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
Copy-Item -LiteralPath $package -Destination (Join-Path $feed 'CopperStart.FileSystem.Service.0.5.0-dev.3.nupkg') -Force
Write-Output "Staged the verified filesystem service package in $feed"
