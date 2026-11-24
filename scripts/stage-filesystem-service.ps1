[CmdletBinding()]
param([string]$PackagePath)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageName = 'CopperStart.FileSystem.Service.0.5.0-dev.9.nupkg'
$releaseUrl = 'https://github.com/ilehtoranta/CopperScreen/releases/download/filesystem-service-0.5.0-dev.9'
$expectedHash = '67B6044DF71118F71D18B884611A78FD21C8768E932CE966EA262CD05080856E'

if ($PackagePath) {
    $bytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $PackagePath).ProviderPath)
} else {
    $client = [System.Net.Http.HttpClient]::new()
    try {
        $client.Timeout = [TimeSpan]::FromSeconds(60)
        $bytes = $client.GetByteArrayAsync("$releaseUrl/$packageName").GetAwaiter().GetResult()
    } finally {
        $client.Dispose()
    }
}
if ([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$bytes)) -cne $expectedHash) {
    throw "Package SHA-256 does not match the pinned $packageName."
}

$feed = Join-Path $root 'artifacts/filesystem-service-feed'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
[IO.File]::WriteAllBytes((Join-Path $feed $packageName), $bytes)
Write-Output "Staged the verified filesystem service package in $feed"
