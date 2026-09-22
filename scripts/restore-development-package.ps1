[CmdletBinding()]
param(
    # Offline bootstrap uses the same verified bytes as the downloadable asset.
    [string]$PackagePath,
    [string]$FeedDirectory = (Join-Path $PSScriptRoot '../artifacts/development-packages')
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content -LiteralPath (Join-Path $repository 'docs/engine/copper68k-trace-development-2026-09-18.json') -Raw | ConvertFrom-Json
$filename = "$($manifest.package).$($manifest.version).nupkg"
$uri = "https://github.com/ilehtoranta/CopperMod/releases/download/copper68k-$($manifest.version)/$filename"
$feed = [IO.Path]::GetFullPath($FeedDirectory)
$destination = Join-Path $feed $filename

function Assert-PackageHash([string]$Path) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -cne $manifest.packageSha256) {
        throw "Development package hash mismatch: $Path. Keep the recorded package bytes; do not repack or replace this version."
    }
}

if (Test-Path -LiteralPath $destination) {
    Assert-PackageHash $destination
    Write-Output "Verified existing $filename ($($manifest.packageSha256))."
    return
}

$null = New-Item -ItemType Directory -Path $feed -Force
$temporary = Join-Path $feed ('.download-' + [Guid]::NewGuid().ToString('N') + '.tmp')
try {
    if ($PackagePath) {
        Copy-Item -LiteralPath (Resolve-Path -LiteralPath $PackagePath).Path -Destination $temporary
    }
    else {
        try {
            Invoke-WebRequest -Uri $uri -OutFile $temporary -ErrorAction Stop
        }
        catch {
            throw "Cannot obtain the pinned development package from $uri. Supply the original package with -PackagePath for offline restore. Download error: $($_.Exception.Message)"
        }
    }
    Assert-PackageHash $temporary
    # Publish into the local feed only after verification. Never overwrite an
    # existing package, including when another invocation wins this race.
    [IO.File]::Move($temporary, $destination)
    Write-Output "Prepared $filename ($($manifest.packageSha256))."
}
finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
