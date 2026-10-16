param([string]$PackagePath)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$record = Get-Content (Join-Path $root 'docs/engine/COPPER68K_QUALIFIED_CANDIDATE_2026-10-11.json') -Raw | ConvertFrom-Json
$version = '1.5.4-qualified-dev.1'
$name = "Copper68k.$version.nupkg"
$feed = Join-Path $root 'artifacts/qualified-cpu-20261011/feed'
$destination = Join-Path $feed $name
New-Item -ItemType Directory -Path $feed -Force | Out-Null

if (Test-Path -LiteralPath $destination) {
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ine $record.packageSha256) {
        throw 'Existing frozen CPU package differs from the qualification record.'
    }
    return
}

$temporary = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.nupkg')
try {
    if ($PackagePath) {
        Copy-Item -LiteralPath $PackagePath -Destination $temporary
    } else {
        $uri = "https://github.com/ilehtoranta/CopperMod/releases/download/copper68k-$version/$name"
        Invoke-WebRequest -Uri $uri -OutFile $temporary
    }
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ine $record.packageSha256) {
        throw 'Downloaded CPU package differs from the qualification record.'
    }
    $lock = Get-Content (Join-Path $root 'CopperScreen/packages.lock.json') -Raw | ConvertFrom-Json -AsHashtable
    $cpu = $lock.dependencies['net10.0']['Copper68k']
    $hash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($temporary)))
    if ($cpu.resolved -cne $version -or $hash -cne $cpu.contentHash) {
        throw 'Frozen CPU package differs from the locked dependency.'
    }
    Copy-Item -LiteralPath $temporary -Destination $destination
} finally {
    Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
}
