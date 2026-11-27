[CmdletBinding()]
param([string]$PackagePath)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$packageName = 'CopperStart.FileSystem.Service.0.5.0-dev.12.nupkg'
$releaseUrl = 'https://github.com/ilehtoranta/CopperScreen/releases/download/filesystem-service-0.5.0-dev.12'
$expectedHash = '6AA967D32B394CF248DAE92C08C2A4B4C60DF904920237E5A04BD48B7AAE0913'

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
