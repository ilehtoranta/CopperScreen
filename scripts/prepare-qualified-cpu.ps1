param([string]$PackagePath)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$record = Get-Content (Join-Path $root 'docs/engine/COPPERSTART_NITRO_2026-10-23.json') -Raw | ConvertFrom-Json
$package = $record.cpuPackages | Where-Object { $_.version -eq $record.cpuLock.resolved }
$version = $package.version
$lock = Get-Content (Join-Path $root 'CopperScreen/packages.lock.json') -Raw | ConvertFrom-Json -AsHashtable
$cpu = $lock.dependencies['net10.0']['Copper68k']
if (!$package -or $cpu.resolved -cne $version -or $cpu.contentHash -cne $record.cpuLock.contentHash) {
    throw 'Current CPU qualification record differs from the locked dependency.'
}

function Assert-QualifiedPackage([string]$Path) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ine $package.package.sha256) {
        throw 'CPU package differs from the qualification record.'
    }
    $hash = [Convert]::ToBase64String([Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($Path)))
    if ($hash -cne $cpu.contentHash) {
        throw 'CPU package differs from the locked dependency.'
    }
}
$name = "Copper68k.$version.nupkg"
$feed = Join-Path $root 'artifacts/qualified-cpu-20261017/feed'
$destination = Join-Path $feed $name
New-Item -ItemType Directory -Path $feed -Force | Out-Null

if (Test-Path -LiteralPath $destination) {
    Assert-QualifiedPackage $destination
    return
}

if (!$PackagePath) {
    $cached = Join-Path $root "artifacts/packages/copper68k/$version/copper68k.$version.nupkg"
    if (Test-Path -LiteralPath $cached) {
        $PackagePath = $cached
    } elseif (!$package.published) {
        throw "Copper68k $version is unpublished. Supply the qualified package with -PackagePath; the preceding public candidate cannot replace it."
    }
}

$temporary = Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N') + '.nupkg')
try {
    if ($PackagePath) {
        Copy-Item -LiteralPath $PackagePath -Destination $temporary
    } else {
        $uri = "https://github.com/ilehtoranta/CopperMod/releases/download/copper68k-$version/$name"
        Invoke-WebRequest -Uri $uri -OutFile $temporary
    }
    Assert-QualifiedPackage $temporary
    Copy-Item -LiteralPath $temporary -Destination $destination
} finally {
    Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
}
